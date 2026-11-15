"""HD audio for Slime Breaker (CPU only).

  python3 asset_work/scripts/build_audio_hd.py [music|ambience|sfx|all] [name ...]

Music: a small procedural composer writes orchestral MIDI per biome (+ menu and boss), FluidSynth renders it with the
GeneralUser GS soundfont (dry), pedalboard adds EQ / reverb / glue compression, the reverb tail is wrapped onto the
start so every track loops seamlessly, and pyloudnorm sets the loudness. Ambience beds and SFX are synthesised with
numpy and the same effect chain. Output goes to asset_work/output/Audio (OGG via oggenc).
"""

import math
import os
import random
import shutil
import subprocess
import sys

import numpy as np
import pretty_midi
import pyloudnorm as pyln
import soundfile as sf
from pedalboard import (Chorus, Compressor, Delay, Gain, HighpassFilter, HighShelfFilter, Limiter, LowpassFilter,
                        LowShelfFilter, Pedalboard, Reverb)

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
AUDIO = os.path.join(ROOT, "asset_work", "output", "Audio")
TMP = os.path.join(ROOT, "asset_work", "intermediate", "audio_hd")
SR = 44100
SOUNDFONT = os.environ.get("SB_SOUNDFONT", os.path.expanduser("~/game-toolchain/soundfonts/GeneralUser-GS.sf2"))

# ---------------------------------------------------------------- shared DSP


def to_stereo(x):
    return np.stack([x, x], axis=0) if x.ndim == 1 else x


def loudness_normalize(audio, target_lufs):
    meter = pyln.Meter(SR)
    loud = meter.integrated_loudness(audio.T)
    if not np.isfinite(loud):
        return audio
    return audio * (10 ** ((target_lufs - loud) / 20.0))


def peak_safety(audio, knee=0.86):
    """Soft-knee peak control that never adds gain (pedalboard's Limiter adds make-up gain equal to its threshold)."""
    a = np.asarray(audio, dtype=np.float32)
    mag = np.abs(a)
    over = mag > knee
    out = a.copy()
    out[over] = np.sign(a[over]) * (knee + (1.0 - knee) * np.tanh((mag[over] - knee) / (1.0 - knee)))
    return out * 0.97


def peak_normalize(audio, peak=0.89):
    m = np.max(np.abs(audio))
    return audio if m < 1e-9 else audio * (peak / m)


def write_ogg(audio, path, quality="4"):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    os.makedirs(TMP, exist_ok=True)
    wav = os.path.join(TMP, os.path.basename(path).replace(".ogg", ".wav"))
    sf.write(wav, np.clip(audio, -1, 1).T, SR, subtype="PCM_16")
    subprocess.run(["oggenc", "-Q", "-q", quality, wav, "-o", path], check=True)
    return path


def wrap_tail(audio, loop_samples):
    """Seamless loop: everything past the loop point (reverb, ringing notes) is mixed back onto the start."""
    head = audio[:, :loop_samples].copy()
    tail = audio[:, loop_samples:]
    n = min(tail.shape[1], loop_samples)
    head[:, :n] += tail[:, :n]
    return head


def crossfade_loop(audio, fade_seconds=2.0):
    """For noise beds: fold the last `fade` seconds onto the start with an equal-power crossfade."""
    f = int(fade_seconds * SR)
    body = audio[:, :-f].copy()
    end = audio[:, -f:]
    t = np.linspace(0, 1, f)
    body[:, :f] = body[:, :f] * np.sin(t * math.pi / 2) + end * np.cos(t * math.pi / 2)
    return body


def rng_for(name):
    """Deterministic per-asset RNG (Python's hash() is salted per process, so derive the seed from the name)."""
    return np.random.default_rng(sum(ord(c) * (i + 1) for i, c in enumerate(name)))


# ---------------------------------------------------------------- composer

SCALES = {
    "major": [0, 2, 4, 5, 7, 9, 11],
    "minor": [0, 2, 3, 5, 7, 8, 10],
    "harmonic": [0, 2, 3, 5, 7, 8, 11],
    "dorian": [0, 2, 3, 5, 7, 9, 10],
    "phrygian_dom": [0, 1, 4, 5, 7, 8, 10],
    "lydian": [0, 2, 4, 6, 7, 9, 11],
    "mixolydian": [0, 2, 4, 5, 7, 9, 10],
}

# Rhythm cells (beats) for one 4/4 bar of melody.
CELLS = [
    [1, 1, 1, 1], [1.5, 0.5, 1, 1], [0.5, 0.5, 1, 2], [1, 0.5, 0.5, 2], [2, 1, 1], [0.75, 0.25, 1, 0.5, 0.5, 1],
    [1, 1, 2], [0.5, 0.5, 0.5, 0.5, 2], [1.5, 0.5, 2], [3, 1],
]


class Song:
    def __init__(self, name, bpm, root, scale, progression, parts, drums=None, sections=None, swing=0.0, reverb=0.6,
                 wet=0.28, lufs=-17.0, seed=1, melody_octave=1):
        self.name, self.bpm, self.root, self.scale = name, bpm, root, SCALES[scale]
        self.progression = progression
        self.parts = parts
        self.drums = drums
        self.sections = sections or [("A", 8, 0.72), ("B", 8, 0.86), ("A2", 8, 1.0), ("C", 8, 0.8)]
        self.swing = swing
        self.reverb = reverb
        self.wet = wet
        self.lufs = lufs
        self.seed = seed
        self.melody_octave = melody_octave

    @property
    def bars(self):
        return sum(s[1] for s in self.sections)

    def beat_seconds(self):
        return 60.0 / self.bpm

    def degree_pitch(self, degree, octave=0):
        d = degree % 7
        o = degree // 7
        return self.root + self.scale[d] + 12 * (o + octave)

    def chord(self, degree):
        return [self.degree_pitch(degree + 2 * i) for i in range(3)]


def snap_to_chord(degree, chord_root):
    """Nearest scale degree that is a chord tone (root, third or fifth of the chord built on chord_root)."""
    best = degree
    for delta in (0, -1, 1, -2, 2, -3, 3):
        if (degree + delta - chord_root) % 7 in (0, 2, 4):
            return degree + delta
    return best


def voice_lead(previous, chord, low, high):
    """Pick the inversion/octave of `chord` closest to `previous` inside [low, high]."""
    candidates = []
    for octave in range(-3, 4):
        notes = sorted(n + 12 * octave for n in chord)
        for inv in range(3):
            v = sorted(notes[inv:] + [n + 12 for n in notes[:inv]])
            if v[0] >= low and v[-1] <= high:
                candidates.append(v)
    if not candidates:
        return sorted(chord)
    if previous is None:
        return min(candidates, key=lambda v: abs(sum(v) / 3 - (low + high) / 2))
    return min(candidates, key=lambda v: sum(abs(a - b) for a, b in zip(v, previous)))


def compose(song):
    rnd = random.Random(song.seed)
    pm = pretty_midi.PrettyMIDI(initial_tempo=song.bpm)
    beat = song.beat_seconds()
    instruments = {}
    for role, spec in song.parts.items():
        is_drum = spec.get("drum", False)
        inst = pretty_midi.Instrument(program=spec.get("program", 0), is_drum=is_drum, name=role)
        instruments[role] = inst
        pm.instruments.append(inst)

    def note(role, pitch, start_beat, dur_beats, vel):
        spec = song.parts[role]
        jitter = rnd.uniform(-0.012, 0.012)
        swing = song.swing * beat * 0.33 if (start_beat % 1.0) == 0.5 else 0.0
        start = max(0.0, start_beat * beat + jitter + swing)
        end = start + max(0.05, dur_beats * beat * spec.get("legato", 0.95))
        v = int(max(1, min(127, vel * spec.get("volume", 1.0) + rnd.uniform(-7, 7))))
        instruments[role].notes.append(pretty_midi.Note(velocity=v, pitch=int(pitch), start=start, end=end))

    # Melodic material: two 4-bar phrases (A, B) reused with variation.
    def make_phrase(start_degree, register):
        bars = []
        degree = start_degree
        for b in range(4):
            cell = list(rnd.choice(CELLS))
            if b == 3:
                cell = rnd.choice([[2, 2], [3, 1], [1, 3], [4]])
            notes = []
            for i, d in enumerate(cell):
                strong = i == 0 or d >= 1.5
                step = rnd.choice([-2, -1, -1, 1, 1, 2]) if not strong else rnd.choice([-3, -2, 0, 2, 3])
                degree = max(register - 3, min(register + 7, degree + step))
                notes.append((degree, d))
            bars.append(notes)
        return bars

    phrase_a = make_phrase(4, 4)
    phrase_b = make_phrase(6, 7)

    bar = 0
    prev_pad = None
    for section, length, intensity in song.sections:
        for i in range(length):
            degree = song.progression[(bar) % len(song.progression)]
            chord = song.chord(degree)
            start = bar * 4
            vel = 64 + 40 * intensity
            for role, spec in song.parts.items():
                if section not in spec.get("sections", ("A", "B", "A2", "C")):
                    continue
                kind = spec["kind"]
                octave = spec.get("octave", 0)
                if kind == "pad":
                    voiced = voice_lead(prev_pad, [n + 12 * octave for n in chord], song.root + 12 * octave - 2, song.root + 12 * octave + 19)
                    if role == "pad":
                        prev_pad = voiced
                    hold = spec.get("hold", 4)
                    for k in range(0, 4, hold):
                        for n in voiced:
                            note(role, n, start + k, hold, vel * 0.72)
                elif kind == "bass":
                    root = chord[0] + 12 * octave
                    pattern = spec.get("pattern", "whole")
                    if pattern == "whole":
                        note(role, root, start, 4, vel * 0.85)
                    elif pattern == "half":
                        note(role, root, start, 2, vel * 0.85)
                        note(role, root + 7 if rnd.random() < 0.5 else root, start + 2, 2, vel * 0.75)
                    elif pattern == "pulse":
                        for k in range(8):
                            note(role, root + (12 if k % 4 == 3 else 0), start + k * 0.5, 0.5, vel * (0.9 if k % 2 == 0 else 0.7))
                    elif pattern == "march":
                        for k, (off, d) in enumerate(((0, 1), (1, 0.5), (1.5, 0.5), (2, 1), (3, 1))):
                            note(role, root + (7 if k == 3 else 0), start + off, d, vel * 0.8)
                elif kind == "arp":
                    tones = sorted(chord) + [chord[0] + 12]
                    tones = [t + 12 * octave for t in tones]
                    step = spec.get("step", 0.5)
                    shape = spec.get("shape", "up")
                    seq = tones if shape == "up" else tones[::-1] if shape == "down" else [tones[0], tones[2], tones[1], tones[2]]
                    count = int(4 / step)
                    for k in range(count):
                        n = seq[k % len(seq)]
                        accent = 1.0 if k % int(1 / step if step < 1 else 1) == 0 else 0.8
                        note(role, n, start + k * step, step, vel * 0.62 * accent)
                elif kind == "melody":
                    phrase = phrase_a if section in ("A", "A2") else phrase_b
                    cells = phrase[i % 4]
                    t = start
                    for d_index, (deg, d) in enumerate(cells):
                        beat_in_bar = t - start
                        # Strong beats (1 and 3) and long notes land on chord tones; the rest pass between them.
                        if abs(beat_in_bar - round(beat_in_bar)) < 1e-6 and int(round(beat_in_bar)) % 2 == 0 or d >= 1.5:
                            deg = snap_to_chord(deg, degree)
                        pitch = song.degree_pitch(deg, song.melody_octave + octave)
                        if section == "A2" and d >= 1 and rnd.random() < 0.3:
                            # Ornament: a quick neighbour note before the main one.
                            note(role, song.degree_pitch(deg + 1, song.melody_octave + octave), t, 0.25, vel * 0.8)
                            note(role, pitch, t + 0.25, d - 0.25, vel)
                        else:
                            note(role, pitch, t, d, vel * (1.0 if d_index == 0 else 0.9))
                        t += d
                elif kind == "counter":
                    phrase = phrase_b if section in ("A", "A2") else phrase_a
                    deg = snap_to_chord(phrase[i % 4][0][0] - 2, degree)
                    note(role, song.degree_pitch(deg, octave), start, 2, vel * 0.7)
                    note(role, song.degree_pitch(snap_to_chord(deg - 1, degree), octave), start + 2, 2, vel * 0.68)
                elif kind == "hits":
                    root = chord[0] + 12 * octave
                    for off in spec.get("beats", (0,)):
                        note(role, root, start + off, 0.5, vel * 0.9)
                elif kind == "drum":
                    for (pitch, off, v) in spec["pattern"](i, rnd):
                        note(role, pitch, start + off, 0.25, vel * v)
            bar += 1
    return pm


# Drum patterns (GM percussion): return [(note, beat_offset, velocity_scale), ...] for bar i.
def kit_adventure(i, rnd):
    hits = [(36, 0, 0.9), (36, 2.5, 0.7), (38, 1, 0.55), (38, 3, 0.6)]
    hits += [(42, k * 0.5, 0.35 + 0.1 * (k % 2 == 0)) for k in range(8)]
    if i % 8 == 7:
        hits += [(45, 3, 0.7), (48, 3.5, 0.7), (49, 0, 0.0)]
    if i % 8 == 0:
        hits.append((49, 0, 0.6))
    return hits


def kit_march(i, rnd):
    hits = [(36, 0, 0.8), (36, 2, 0.7)]
    hits += [(38, 1, 0.6), (38, 1.75, 0.35), (38, 3, 0.65), (38, 3.5, 0.4), (38, 3.75, 0.45)]
    if i % 4 == 0:
        hits.append((49, 0, 0.55))
    return hits


def kit_epic(i, rnd):
    hits = [(36, 0, 1.0), (36, 0.75, 0.6), (36, 2, 0.9), (36, 2.5, 0.6), (38, 1, 0.8), (38, 3, 0.85)]
    hits += [(42, k * 0.5, 0.4) for k in range(8)]
    if i % 4 == 0:
        hits.append((49, 0, 0.8))
    if i % 4 == 3:
        hits += [(41, 3, 0.8), (43, 3.25, 0.8), (45, 3.5, 0.85), (48, 3.75, 0.9)]
    return hits


def kit_hand(i, rnd):
    hits = [(64, 0, 0.9), (63, 1, 0.6), (62, 1.5, 0.55), (64, 2, 0.8), (63, 2.75, 0.6), (62, 3.5, 0.6)]
    hits += [(70, k * 0.5, 0.3) for k in range(8)]
    return hits


def kit_light(i, rnd):
    hits = [(54, k * 1.0 + 0.5, 0.35) for k in range(4)]
    if i % 2 == 0:
        hits.append((36, 0, 0.45))
    return hits


def kit_wood(i, rnd):
    return [(76, 0, 0.6), (77, 1.5, 0.5), (76, 2, 0.55), (77, 3, 0.5)] + [(70, k * 0.5, 0.25) for k in range(8)]


def songs():
    return [
        Song("MUS_Menu", 74, 53, "major", [0, 5, 3, 4], {
            "pad": dict(kind="pad", program=49, volume=0.7, octave=0),
            "harp": dict(kind="arp", program=46, volume=0.85, octave=1, step=0.5, shape="up"),
            "melody": dict(kind="melody", program=73, volume=0.95, sections=("A", "B", "A2")),
            "bass": dict(kind="bass", program=42, volume=0.7, octave=-1, pattern="whole"),
            "celesta": dict(kind="arp", program=8, volume=0.45, octave=2, step=1.0, shape="alberti", sections=("B", "A2")),
        }, reverb=0.85, wet=0.36, lufs=-19, seed=11),
        Song("MUS_Forest", 112, 50, "dorian", [0, 3, 6, 0, 0, 3, 5, 6], {
            "pad": dict(kind="pad", program=48, volume=0.6, octave=0, hold=2),
            "pizz": dict(kind="arp", program=45, volume=0.8, octave=0, step=0.5, shape="alberti"),
            "melody": dict(kind="melody", program=73, volume=1.0, sections=("A", "A2", "C")),
            "oboe": dict(kind="melody", program=68, volume=0.9, sections=("B",)),
            "horn": dict(kind="counter", program=60, volume=0.7, octave=0, sections=("B", "A2")),
            "bass": dict(kind="bass", program=43, volume=0.85, octave=-1, pattern="half"),
            "drums": dict(kind="drum", drum=True, volume=0.55, pattern=kit_adventure, sections=("B", "A2", "C")),
        }, reverb=0.65, wet=0.26, lufs=-17, seed=21),
        Song("MUS_Desert", 100, 52, "phrygian_dom", [0, 1, 0, 6, 0, 1, 2, 1], {
            "pad": dict(kind="pad", program=49, volume=0.55),
            "lead": dict(kind="melody", program=104, volume=1.0, legato=0.8),
            "oboe": dict(kind="counter", program=68, volume=0.7, sections=("B", "A2")),
            "pluck": dict(kind="arp", program=107, volume=0.6, octave=0, step=0.5, shape="alberti", sections=("A", "A2", "C")),
            "bass": dict(kind="bass", program=45, volume=0.85, octave=-1, pattern="march"),
            "drums": dict(kind="drum", drum=True, volume=0.6, pattern=kit_hand),
        }, reverb=0.7, wet=0.3, lufs=-17, seed=31),
        Song("MUS_Snow", 84, 55, "lydian", [0, 1, 0, 4, 5, 1, 3, 4], {
            "pad": dict(kind="pad", program=49, volume=0.6, octave=1),
            "choir": dict(kind="pad", program=53, volume=0.45, octave=0, sections=("B", "A2")),
            "box": dict(kind="arp", program=10, volume=0.6, octave=2, step=0.5, shape="up"),
            "melody": dict(kind="melody", program=73, volume=0.95, sections=("A", "B", "A2")),
            "bass": dict(kind="bass", program=42, volume=0.65, octave=-1, pattern="whole"),
            "drums": dict(kind="drum", drum=True, volume=0.35, pattern=kit_light, sections=("B", "A2")),
        }, reverb=0.9, wet=0.4, lufs=-19, seed=41),
        Song("MUS_Thunder", 124, 52, "minor", [0, 5, 2, 6], {
            "trem": dict(kind="arp", program=44, volume=0.7, octave=0, step=0.25, shape="up"),
            "pad": dict(kind="pad", program=48, volume=0.55),
            "brass": dict(kind="hits", program=61, volume=0.85, octave=0, beats=(0, 1.5, 3), sections=("B", "A2")),
            "melody": dict(kind="melody", program=60, volume=1.0),
            "timp": dict(kind="hits", program=47, volume=0.9, octave=-1, beats=(0, 2)),
            "bass": dict(kind="bass", program=43, volume=0.9, octave=-1, pattern="pulse"),
            "drums": dict(kind="drum", drum=True, volume=0.6, pattern=kit_epic, sections=("B", "A2", "C")),
        }, reverb=0.6, wet=0.24, lufs=-16, seed=51),
        Song("MUS_Swamp", 96, 45, "harmonic", [0, 3, 0, 4], {
            "marimba": dict(kind="arp", program=12, volume=0.7, octave=1, step=0.5, shape="alberti"),
            "pad": dict(kind="pad", program=89, volume=0.5),
            "melody": dict(kind="melody", program=71, volume=1.0, legato=0.85),
            "bassoon": dict(kind="bass", program=70, volume=0.85, octave=-1, pattern="march"),
            "drums": dict(kind="drum", drum=True, volume=0.5, pattern=kit_wood),
        }, reverb=0.75, wet=0.32, lufs=-18, seed=61, melody_octave=1),
        Song("MUS_Shadow", 80, 48, "harmonic", [0, 5, 3, 4], {
            "choir": dict(kind="pad", program=52, volume=0.7),
            "low": dict(kind="pad", program=49, volume=0.6, octave=-1),
            "melody": dict(kind="melody", program=10, volume=0.9, sections=("A", "B", "A2")),
            "bass": dict(kind="bass", program=43, volume=0.8, octave=-1, pattern="whole"),
            "timp": dict(kind="hits", program=47, volume=0.7, octave=-1, beats=(0,), sections=("B", "A2")),
        }, reverb=0.92, wet=0.42, lufs=-19, seed=71),
        Song("MUS_Volcano", 132, 50, "minor", [0, 5, 6, 0, 0, 5, 3, 4], {
            "taiko": dict(kind="hits", program=116, volume=0.95, octave=-1, beats=(0, 0.75, 1.5, 2, 3, 3.5)),
            "trem": dict(kind="arp", program=44, volume=0.65, octave=0, step=0.25, shape="down"),
            "brass": dict(kind="melody", program=61, volume=1.0),
            "horn": dict(kind="counter", program=60, volume=0.75, sections=("B", "A2")),
            "bass": dict(kind="bass", program=43, volume=0.95, octave=-1, pattern="pulse"),
            "drums": dict(kind="drum", drum=True, volume=0.55, pattern=kit_epic, sections=("B", "A2")),
        }, reverb=0.6, wet=0.24, lufs=-16, seed=81),
        Song("MUS_Storm", 128, 55, "harmonic", [0, 6, 5, 4], {
            "trem": dict(kind="arp", program=48, volume=0.7, octave=0, step=0.25, shape="up"),
            "melody": dict(kind="melody", program=60, volume=1.0),
            "trombone": dict(kind="counter", program=57, volume=0.75, octave=-1, sections=("B", "A2")),
            "pad": dict(kind="pad", program=49, volume=0.5),
            "bass": dict(kind="bass", program=43, volume=0.9, octave=-1, pattern="pulse"),
            "drums": dict(kind="drum", drum=True, volume=0.6, pattern=kit_march),
        }, reverb=0.7, wet=0.28, lufs=-16, seed=91),
        Song("MUS_Crystal", 90, 52, "lydian", [0, 2, 3, 1], {
            "vibes": dict(kind="arp", program=11, volume=0.7, octave=1, step=0.5, shape="up"),
            "glock": dict(kind="arp", program=9, volume=0.4, octave=2, step=1.0, shape="alberti", sections=("B", "A2")),
            "pad": dict(kind="pad", program=95, volume=0.55),
            "harp": dict(kind="arp", program=46, volume=0.55, octave=0, step=0.25, shape="down", sections=("A2", "C")),
            "melody": dict(kind="melody", program=73, volume=0.9, sections=("A", "B", "A2")),
            "bass": dict(kind="bass", program=42, volume=0.6, octave=-1, pattern="whole"),
        }, reverb=0.9, wet=0.4, lufs=-19, seed=101),
        Song("MUS_Royal", 108, 46, "major", [0, 3, 4, 0, 5, 3, 1, 4], {
            "harpsi": dict(kind="arp", program=6, volume=0.6, octave=1, step=0.5, shape="alberti"),
            "pad": dict(kind="pad", program=48, volume=0.6),
            "melody": dict(kind="melody", program=56, volume=1.0),
            "horn": dict(kind="counter", program=60, volume=0.75, sections=("B", "A2")),
            "bass": dict(kind="bass", program=43, volume=0.8, octave=-1, pattern="march"),
            "timp": dict(kind="hits", program=47, volume=0.7, octave=-1, beats=(0, 2), sections=("A2",)),
            "drums": dict(kind="drum", drum=True, volume=0.5, pattern=kit_march, sections=("B", "A2")),
        }, reverb=0.75, wet=0.3, lufs=-17, seed=111),
        Song("MUS_Boss", 144, 50, "harmonic", [0, 5, 2, 6, 0, 3, 4, 4], {
            "choir": dict(kind="pad", program=52, volume=0.75, hold=2),
            "ost": dict(kind="arp", program=48, volume=0.75, octave=-1, step=0.25, shape="alberti"),
            "brass": dict(kind="melody", program=61, volume=1.0),
            "horn": dict(kind="counter", program=60, volume=0.8, sections=("B", "A2", "C")),
            "taiko": dict(kind="hits", program=116, volume=0.9, octave=-1, beats=(0, 1, 2, 2.5, 3)),
            "bass": dict(kind="bass", program=43, volume=0.95, octave=-1, pattern="pulse"),
            "drums": dict(kind="drum", drum=True, volume=0.65, pattern=kit_epic),
        }, reverb=0.65, wet=0.26, lufs=-15, seed=121),
    ]


def render_midi(pm, name, seconds):
    os.makedirs(TMP, exist_ok=True)
    mid = os.path.join(TMP, name + ".mid")
    wav = os.path.join(TMP, name + "_dry.wav")
    pm.write(mid)
    # Dry render (no FluidSynth reverb/chorus); pedalboard does the room so the tail can be wrapped for looping.
    subprocess.run(["fluidsynth", "-ni", "-q", "-g", "0.5", "-R", "0", "-C", "0", "-r", str(SR), "-F", wav, SOUNDFONT, mid],
                   check=True, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    audio, sr = sf.read(wav, always_2d=True)
    audio = audio.T.astype(np.float32)
    need = int(seconds * SR)
    if audio.shape[1] < need:
        audio = np.pad(audio, ((0, 0), (0, need - audio.shape[1])))
    return audio[:, :need]


def master_music(audio, song):
    board = Pedalboard([
        HighpassFilter(cutoff_frequency_hz=32),
        LowShelfFilter(cutoff_frequency_hz=140, gain_db=1.5),
        Compressor(threshold_db=-20, ratio=2.2, attack_ms=25, release_ms=200),
        Reverb(room_size=song.reverb, damping=0.45, wet_level=song.wet, dry_level=0.82, width=1.0),
        HighShelfFilter(cutoff_frequency_hz=7500, gain_db=1.5),
    ])
    return board(audio, SR)


def build_music(only):
    out = os.path.join(AUDIO, "Music")
    for song in songs():
        if only and song.name not in only:
            continue
        loop_seconds = song.bars * 4 * song.beat_seconds()
        pm = compose(song)
        audio = render_midi(pm, song.name, loop_seconds + 6.0)
        audio = master_music(audio, song)
        audio = wrap_tail(audio, int(loop_seconds * SR))
        audio = loudness_normalize(audio, song.lufs)
        audio = peak_safety(audio)
        write_ogg(audio, os.path.join(out, song.name + ".ogg"), quality="4")
        print(f"MUSIC {song.name} {loop_seconds:.1f}s {song.bpm}bpm bars={song.bars}", flush=True)


# ---------------------------------------------------------------- synthesis helpers (ambience + sfx)


def noise(rng, n, colour="pink"):
    white = rng.standard_normal(n).astype(np.float32)
    if colour == "white":
        return white
    spec = np.fft.rfft(white)
    f = np.fft.rfftfreq(n, 1 / SR)
    f[0] = f[1]
    spec /= np.sqrt(f) if colour == "pink" else f
    out = np.fft.irfft(spec, n).astype(np.float32)
    return out / (np.max(np.abs(out)) + 1e-9)


def env_adsr(n, a, d, s, r):
    a, d, r = int(a * SR), int(d * SR), int(r * SR)
    sus = max(0, n - a - d - r)
    e = np.concatenate([np.linspace(0, 1, max(1, a)), np.linspace(1, s, max(1, d)), np.full(sus, s), np.linspace(s, 0, max(1, r))])
    return np.pad(e, (0, max(0, n - len(e))))[:n].astype(np.float32)


def sweep_sine(f0, f1, seconds, shape=1.0):
    n = int(seconds * SR)
    t = np.linspace(0, 1, n)
    freq = f0 + (f1 - f0) * t ** shape
    return np.sin(2 * math.pi * np.cumsum(freq) / SR).astype(np.float32)


def place(buffer, sound, at, gain=1.0, pan=0.0):
    i = int(at * SR)
    if i >= buffer.shape[1]:
        return
    n = min(len(sound), buffer.shape[1] - i)
    left = math.cos((pan + 1) * math.pi / 4)
    right = math.sin((pan + 1) * math.pi / 4)
    buffer[0, i:i + n] += sound[:n] * gain * left
    buffer[1, i:i + n] += sound[:n] * gain * right


def lfo(n, rate, depth, phase=0.0):
    t = np.arange(n) / SR
    return (1.0 - depth + depth * (0.5 + 0.5 * np.sin(2 * math.pi * rate * t + phase))).astype(np.float32)


def wind_bed(rng, seconds, intensity=1.0, bright=1.0):
    n = int(seconds * SR)
    out = np.zeros((2, n), np.float32)
    for ch in range(2):
        base = noise(rng, n, "pink")
        gust = lfo(n, 0.07 + rng.random() * 0.05, 0.6, rng.random() * 6) * lfo(n, 0.19, 0.3, rng.random() * 6)
        board = Pedalboard([LowpassFilter(cutoff_frequency_hz=900 * bright), HighpassFilter(cutoff_frequency_hz=80)])
        out[ch] = board(base[None, :], SR)[0] * gust * intensity
    return out


def bird_call(rng):
    kind = rng.integers(3)
    if kind == 0:
        notes = [sweep_sine(rng.uniform(2800, 3600), rng.uniform(3800, 5200), rng.uniform(0.06, 0.12), 0.6) for _ in range(rng.integers(2, 5))]
    elif kind == 1:
        notes = [sweep_sine(rng.uniform(4200, 5200), rng.uniform(2600, 3200), rng.uniform(0.05, 0.09)) for _ in range(rng.integers(3, 7))]
    else:
        f = rng.uniform(2200, 3000)
        notes = [sweep_sine(f, f * 1.25, 0.18), sweep_sine(f * 1.2, f * 0.95, 0.22)]
    parts = []
    for s in notes:
        e = env_adsr(len(s), 0.008, 0.02, 0.7, 0.03)
        parts.append(s * e)
        parts.append(np.zeros(int(rng.uniform(0.03, 0.08) * SR), np.float32))
    return np.concatenate(parts)


def cricket(rng, seconds):
    n = int(seconds * SR)
    t = np.arange(n) / SR
    carrier = np.sin(2 * math.pi * rng.uniform(4300, 5000) * t)
    pulse = (np.sin(2 * math.pi * 28 * t) > 0.2).astype(np.float32)
    groups = (np.sin(2 * math.pi * rng.uniform(1.4, 2.2) * t) > 0.3).astype(np.float32)
    return (carrier * pulse * groups * 0.15).astype(np.float32)


def frog(rng):
    f = rng.uniform(120, 220)
    n = int(rng.uniform(0.25, 0.45) * SR)
    t = np.arange(n) / SR
    body = np.sign(np.sin(2 * math.pi * f * t)) * 0.4 + np.sin(2 * math.pi * f * 2 * t) * 0.3
    am = (np.sin(2 * math.pi * rng.uniform(18, 30) * t) * 0.5 + 0.5) ** 2
    s = body * am * env_adsr(n, 0.02, 0.05, 0.8, 0.08)
    return Pedalboard([LowpassFilter(cutoff_frequency_hz=900)])(s[None, :].astype(np.float32), SR)[0]


def droplets(rng, n, rate, f_lo=900, f_hi=4000):
    out = np.zeros(n, np.float32)
    count = int(rate * n / SR)
    for _ in range(count):
        i = rng.integers(0, n - 2000)
        f = rng.uniform(f_lo, f_hi)
        m = int(rng.uniform(0.008, 0.03) * SR)
        t = np.arange(m) / SR
        blip = np.sin(2 * math.pi * f * t * (1 + 3 * t)) * np.exp(-t * 180)
        out[i:i + m] += blip.astype(np.float32) * rng.uniform(0.1, 0.4)
    return out


def bell(f, seconds, rng, partials=(1, 2.76, 5.4, 8.93)):
    n = int(seconds * SR)
    t = np.arange(n) / SR
    s = sum(np.sin(2 * math.pi * f * p * t) * math.exp(-i * 0.7) * np.exp(-t * (1.5 + i * 1.8)) for i, p in enumerate(partials))
    return (s * 0.3).astype(np.float32)


def rumble(rng, seconds, cutoff=140):
    n = int(seconds * SR)
    base = noise(rng, n, "brown")
    return Pedalboard([LowpassFilter(cutoff_frequency_hz=cutoff)])(base[None, :], SR)[0] * lfo(n, 0.11, 0.5)


# ---------------------------------------------------------------- ambience beds


def amb_forest(rng, s):
    out = wind_bed(rng, s, 0.35, 1.4)
    for _ in range(int(s * 0.9)):
        place(out, bird_call(rng), rng.uniform(0, s - 2), rng.uniform(0.05, 0.16), rng.uniform(-0.8, 0.8))
    out[0] += noise(rng, out.shape[1], "pink") * 0.012
    out[1] += noise(rng, out.shape[1], "pink") * 0.012
    return out, 0.55, -26


def amb_night(rng, s):
    out = wind_bed(rng, s, 0.2, 0.8)
    for k in range(4):
        c = cricket(rng, s) * rng.uniform(0.25, 0.6)
        out[0] += c * (0.3 + 0.7 * (k % 2))
        out[1] += c * (0.3 + 0.7 * ((k + 1) % 2))
    return out, 0.6, -27


def amb_desert(rng, s):
    out = wind_bed(rng, s, 0.9, 1.8)
    sand = noise(rng, out.shape[1], "white") * lfo(out.shape[1], 0.09, 0.8) * 0.03
    sand = Pedalboard([HighpassFilter(cutoff_frequency_hz=3500)])(sand[None, :], SR)[0]
    out += sand
    return out, 0.4, -25


def amb_snow(rng, s):
    out = wind_bed(rng, s, 1.0, 0.7)
    howl = sweep_sine(380, 520, s) * lfo(int(s * SR), 0.05, 1.0) * 0.04
    out += Pedalboard([LowpassFilter(cutoff_frequency_hz=700)])(howl[None, :].astype(np.float32), SR)[0]
    return out, 0.7, -25


def amb_thunder(rng, s):
    out = wind_bed(rng, s, 0.5, 1.0)
    for _ in range(3):
        at = rng.uniform(1, s - 8)
        length = rng.uniform(4, 7)
        r = rumble(rng, length, 180)
        r = r * env_adsr(len(r), 0.05, 0.6, 0.5, 3.0)
        place(out, r, at, rng.uniform(0.5, 0.9), rng.uniform(-0.5, 0.5))
    return out, 0.8, -24


def amb_swamp(rng, s):
    out = wind_bed(rng, s, 0.2, 0.8)
    for _ in range(int(s * 1.2)):
        place(out, frog(rng), rng.uniform(0, s - 1), rng.uniform(0.08, 0.2), rng.uniform(-0.9, 0.9))
    bubbles = droplets(rng, out.shape[1], 3, 200, 700)
    out[0] += bubbles * 0.4
    out[1] += bubbles * 0.3
    c = cricket(rng, s) * 0.25
    out += c
    return out, 0.65, -26


def amb_shadow(rng, s):
    n = int(s * SR)
    t = np.arange(n) / SR
    drone = sum(np.sin(2 * math.pi * f * t + rng.random() * 6) for f in (55.0, 55.4, 82.6, 110.7)) * 0.08
    drone = drone * lfo(n, 0.05, 0.5)
    whisper = Pedalboard([HighpassFilter(cutoff_frequency_hz=1800), LowpassFilter(cutoff_frequency_hz=4500)])(
        (noise(rng, n, "white") * lfo(n, 0.13, 0.95) * 0.04)[None, :], SR)[0]
    out = np.stack([drone + whisper, drone * 0.95 + np.roll(whisper, 2000)]).astype(np.float32)
    return out, 0.9, -27


def amb_volcano(rng, s):
    n = int(s * SR)
    out = np.stack([rumble(rng, s, 120), rumble(rng, s, 120)]) * 0.6
    bubbles = droplets(rng, n, 6, 120, 400)
    crackle = Pedalboard([HighpassFilter(cutoff_frequency_hz=2000)])(droplets(rng, n, 40, 2000, 6000)[None, :], SR)[0] * 0.5
    out[0] += bubbles * 0.6 + crackle
    out[1] += bubbles * 0.5 + np.roll(crackle, 3000)
    return out.astype(np.float32), 0.5, -24


def amb_storm(rng, s):
    out = wind_bed(rng, s, 1.2, 1.2)
    for _ in range(2):
        at = rng.uniform(1, s - 8)
        r = rumble(rng, 5, 220)
        place(out, r * env_adsr(len(r), 0.02, 0.4, 0.5, 2.5), at, 0.8, rng.uniform(-0.6, 0.6))
    return out, 0.6, -24


def amb_crystal(rng, s):
    out = wind_bed(rng, s, 0.15, 0.6)
    scale = [0, 2, 4, 6, 7, 9, 11]
    for _ in range(int(s * 0.7)):
        f = 880 * 2 ** (scale[rng.integers(7)] / 12 + rng.integers(0, 2))
        place(out, bell(f, 3.0, rng), rng.uniform(0, s - 3), rng.uniform(0.03, 0.08), rng.uniform(-0.9, 0.9))
    return out, 0.92, -27


def amb_royal(rng, s):
    out = wind_bed(rng, s, 0.25, 1.4)
    fountain = noise(rng, out.shape[1], "white")
    fountain = Pedalboard([HighpassFilter(cutoff_frequency_hz=600), LowpassFilter(cutoff_frequency_hz=6000)])(fountain[None, :], SR)[0] * 0.05
    fountain = fountain + droplets(rng, out.shape[1], 25, 1200, 3500) * 0.3
    out[0] += fountain
    out[1] += np.roll(fountain, 1200)
    for _ in range(int(s * 0.6)):
        place(out, bird_call(rng), rng.uniform(0, s - 2), rng.uniform(0.05, 0.12), rng.uniform(-0.8, 0.8))
    return out, 0.6, -26


def amb_rain(rng, s):
    n = int(s * SR)
    out = np.zeros((2, n), np.float32)
    for ch in range(2):
        hiss = Pedalboard([HighpassFilter(cutoff_frequency_hz=400), LowpassFilter(cutoff_frequency_hz=9000)])(noise(rng, n, "pink")[None, :], SR)[0] * 0.25
        out[ch] = hiss + droplets(rng, n, 140, 1500, 6000) * 0.5
    return out, 0.5, -23


def amb_wind(rng, s):
    return wind_bed(rng, s, 1.1, 1.0), 0.5, -24


AMBIENCE = {
    "AMB_Forest": amb_forest, "AMB_Night": amb_night, "AMB_Desert": amb_desert, "AMB_Snow": amb_snow,
    "AMB_Thunder": amb_thunder, "AMB_Swamp": amb_swamp, "AMB_Shadow": amb_shadow, "AMB_Volcano": amb_volcano,
    "AMB_Storm": amb_storm, "AMB_Crystal": amb_crystal, "AMB_Royal": amb_royal,
    "SFX_Rain": amb_rain, "SFX_Wind": amb_wind,
}


def build_ambience(only):
    for name, builder in AMBIENCE.items():
        if only and name not in only:
            continue
        rng = rng_for(name)
        seconds = 34.0
        audio, room, lufs = builder(rng, seconds + 2.0)
        audio = Pedalboard([Reverb(room_size=room, wet_level=0.25, dry_level=0.85, width=1.0)])(audio.astype(np.float32), SR)
        audio = crossfade_loop(audio, 2.0)
        audio = loudness_normalize(audio, lufs)
        audio = peak_safety(audio, 0.8)
        folder = "SFX" if name.startswith("SFX_") else "Ambience"
        write_ogg(audio, os.path.join(AUDIO, folder, name + ".ogg"), quality="3")
        print(f"AMBIENCE {name} {seconds:.0f}s", flush=True)


# ---------------------------------------------------------------- sound effects


def squelch(rng, length=0.28, pitch=1.0):
    n = int(length * SR)
    t = np.arange(n) / SR
    bloop = np.sin(2 * math.pi * np.cumsum(320 * pitch * np.exp(-t * 9) + 70) / SR) * np.exp(-t * 9)
    wet = noise(rng, n, "pink") * np.exp(-t * 22)
    wet = Pedalboard([LowpassFilter(cutoff_frequency_hz=2200 * pitch), HighpassFilter(cutoff_frequency_hz=180)])(wet[None, :], SR)[0]
    gurgle = np.sin(2 * math.pi * 40 * t) * 0.5 + 0.5
    return (bloop * 0.7 + wet * 0.6 * gurgle).astype(np.float32)


def whoosh(rng, length=0.35, f0=400, f1=2400, bright=1.0):
    n = int(length * SR)
    base = noise(rng, n, "pink")
    out = np.zeros(n, np.float32)
    chunk = 512
    for i in range(0, n, chunk):
        k = i / n
        fc = f0 + (f1 - f0) * math.sin(k * math.pi) ** 0.8
        seg = base[i:i + chunk]
        seg = Pedalboard([HighpassFilter(cutoff_frequency_hz=fc * 0.6), LowpassFilter(cutoff_frequency_hz=fc * 1.8 * bright)])(seg[None, :], SR)[0]
        out[i:i + len(seg)] = seg
    return out * np.sin(np.linspace(0, math.pi, n)) ** 1.4


def sparkle(rng, length=0.5, base=2000, count=8):
    n = int(length * SR)
    out = np.zeros(n, np.float32)
    for k in range(count):
        f = base * 2 ** (rng.integers(0, 12) / 12) * rng.choice([1, 2])
        b = bell(f, length, rng)[:n]
        start = int(k / count * n * 0.6)
        out[start:start + len(b)] += b[: n - start] * 0.35
    return out


def impact(rng, length=0.25, low=90, body=0.8, click=0.5):
    n = int(length * SR)
    t = np.arange(n) / SR
    thump = np.sin(2 * math.pi * np.cumsum(low * 2.2 * np.exp(-t * 25) + low) / SR) * np.exp(-t * 14) * body
    snap = Pedalboard([HighpassFilter(cutoff_frequency_hz=900), LowpassFilter(cutoff_frequency_hz=4500)])(noise(rng, n, "white")[None, :], SR)[0] * np.exp(-t * 70) * click * 0.6
    return (thump + snap).astype(np.float32)


def crack(rng, length=0.3):
    """Sharp descending zap over high band-passed noise."""
    n = int(length * SR)
    t = np.arange(n) / SR
    zap = np.sign(np.sin(2 * math.pi * np.cumsum(900 + 2600 * np.exp(-t * 28)) / SR)) * np.exp(-t * 26)
    fizz = Pedalboard([HighpassFilter(cutoff_frequency_hz=2800), LowpassFilter(cutoff_frequency_hz=9000)])(noise(rng, n, "white")[None, :], SR)[0] * np.exp(-t * 16)
    zap = Pedalboard([LowpassFilter(cutoff_frequency_hz=6000)])(zap.astype(np.float32)[None, :], SR)[0]
    return (zap * 0.55 + fizz * 0.5).astype(np.float32)


def door_creak(rng, length=0.95):
    """Slow wooden hinge groan: a wobbling low saw through a resonant band."""
    n = int(length * SR)
    t = np.arange(n) / SR
    pitch = 150 + 70 * t / length + 18 * np.sin(2 * math.pi * 7 * t) + 8 * np.sin(2 * math.pi * 19 * t)
    saw = 2.0 * (np.cumsum(pitch) / SR % 1.0) - 1.0
    env = np.minimum(1.0, t / 0.08) * np.minimum(1.0, (length - t) / 0.2)
    body = Pedalboard([HighpassFilter(cutoff_frequency_hz=300), LowpassFilter(cutoff_frequency_hz=1900)])(saw.astype(np.float32)[None, :], SR)[0]
    return (body * env * 0.8).astype(np.float32)


def boom(rng, length=1.4):
    n = int(length * SR)
    t = np.arange(n) / SR
    sub = np.sin(2 * math.pi * np.cumsum(38 + 60 * np.exp(-t * 6)) / SR) * np.exp(-t * 2.8)
    debris = Pedalboard([LowpassFilter(cutoff_frequency_hz=2500)])(noise(rng, n, "pink")[None, :], SR)[0] * np.exp(-t * 5) * 0.6
    crack = Pedalboard([HighpassFilter(cutoff_frequency_hz=1200)])(noise(rng, n, "white")[None, :], SR)[0] * np.exp(-t * 40) * 0.4
    return (sub + debris + crack).astype(np.float32)


def zap(rng, length=0.35):
    n = int(length * SR)
    t = np.arange(n) / SR
    buzz = np.sign(np.sin(2 * math.pi * (120 + 80 * rng.random()) * t)) * (rng.random(n) > 0.4)
    crackle = droplets(rng, n, 400, 3000, 9000)
    s = Pedalboard([HighpassFilter(cutoff_frequency_hz=600)])((buzz * 0.25 + crackle)[None, :].astype(np.float32), SR)[0]
    return s * np.exp(-t * 7)


def fire_burst(rng, length=0.8):
    n = int(length * SR)
    roar = Pedalboard([LowpassFilter(cutoff_frequency_hz=1400), HighpassFilter(cutoff_frequency_hz=80)])(noise(rng, n, "pink")[None, :], SR)[0]
    crack = droplets(rng, n, 60, 1500, 5000)
    e = env_adsr(n, 0.04, 0.2, 0.6, 0.45)
    return ((roar + crack * 0.6) * e).astype(np.float32)


def ice_crack(rng, length=0.6):
    n = int(length * SR)
    t = np.arange(n) / SR
    crack = Pedalboard([HighpassFilter(cutoff_frequency_hz=2500)])(noise(rng, n, "white")[None, :], SR)[0] * np.exp(-t * 30)
    ring = sparkle(rng, length, 2600, 5)
    return (crack * 0.6 + ring).astype(np.float32)


def jingle(name, notes, program, bpm=120, length=None, reverb=0.7):
    """Short orchestral cue rendered through the soundfont (level-up, victory, ...)."""
    pm = pretty_midi.PrettyMIDI(initial_tempo=bpm)
    beat = 60.0 / bpm
    end = 0.0
    for prog, voice in notes:
        inst = pretty_midi.Instrument(program=prog)
        for pitch, start, dur, vel in voice:
            inst.notes.append(pretty_midi.Note(velocity=vel, pitch=pitch, start=start * beat, end=(start + dur) * beat))
            end = max(end, (start + dur) * beat)
        pm.instruments.append(inst)
    seconds = (length or end) + 1.5
    audio = render_midi(pm, name, seconds)
    audio = Pedalboard([Reverb(room_size=reverb, wet_level=0.25, dry_level=0.85)])(audio, SR)
    return audio


def soft_pop(rng, seconds, f0, f1):
    """Mellow 'bloop': a sine gliding f0 -> f1 with a soft 6 ms attack and a quick round decay (calm when repeated)."""
    n = int(seconds * SR)
    t = np.arange(n) / SR
    freq = f0 + (f1 - f0) * (1 - np.exp(-t * 40))
    phase = np.cumsum(freq) / SR * math.tau
    tone = np.sin(phase) + 0.18 * np.sin(phase * 2.0)
    env = np.minimum(1.0, t / 0.006) * np.exp(-t * 22)
    return (tone * env * (1.0 + rng.uniform(-0.03, 0.03))).astype(np.float32)


def layer(seconds, *items):
    """Mix (sound, offset_seconds, gain) items into one mono buffer of `seconds`."""
    out = np.zeros(int(seconds * SR), np.float32)
    for sound, offset, gain in items:
        i = int(offset * SR)
        n = min(len(sound), len(out) - i)
        if n > 0:
            out[i:i + n] += np.asarray(sound[:n], dtype=np.float32) * gain
    return out


def sfx_specs():
    def mono(fn):
        return lambda rng: fn(rng)

    return {
        "SFX_Hit": mono(lambda r: layer(0.26, (squelch(r, 0.22, 1.2), 0.0, 0.8), (impact(r, 0.2, 140, 0.4, 0.3), 0.0, 1.0))),
        # Enemy bolt landing on the hero: a bright glassy crack with a fizzing tail, far from the low wet SFX_Hit.
        "SFX_RangedHit": mono(lambda r: layer(0.34, (crack(r, 0.3), 0.0, 1.0), (sparkle(r, 0.3, 3400, 5), 0.015, 0.45))),
        "SFX_DoorOpen": mono(lambda r: layer(1.25, (door_creak(r, 0.95), 0.0, 0.9), (impact(r, 0.28, 95, 0.9, 0.5), 0.0, 0.8), (impact(r, 0.3, 70, 1.0, 0.25), 0.92, 0.9))),
        "SFX_EnemyDeath": mono(lambda r: layer(0.55, (squelch(r, 0.4, 0.8), 0.0, 1.0), (squelch(r, 0.3, 1.4), 0.1, 0.6), (droplets(r, int(0.5 * SR), 14, 300, 900), 0.05, 0.3))),
        "SFX_BoltHit": mono(lambda r: layer(0.3, (impact(r, 0.18, 220, 0.35, 0.6), 0.0, 1.0), (sparkle(r, 0.3, 2400, 4), 0.0, 0.8))),
        "SFX_Cast": mono(lambda r: layer(0.36, (whoosh(r, 0.32, 600, 3200), 0.0, 0.7), (sparkle(r, 0.32, 1800, 5), 0.02, 1.0))),
        "SFX_Dash": mono(lambda r: whoosh(r, 0.36, 300, 1800) * 1.1),
        "SFX_PlayerHit": mono(lambda r: impact(r, 0.3, 75, 1.0, 0.7)),
        "SFX_Pickup": mono(lambda r: layer(0.22, (soft_pop(r, 0.2, 520, 780), 0.0, 0.6), (soft_pop(r, 0.16, 1040, 1170), 0.0, 0.08))),
        "SFX_Ice": mono(lambda r: ice_crack(r, 0.5)),
        "SFX_Freeze": mono(lambda r: ice_crack(r, 0.35) * 0.8),
        "SFX_Lightning": mono(lambda r: layer(1.3, (zap(r, 0.45), 0.0, 1.0), (boom(r, 1.2), 0.06, 0.5))),
        "SFX_Fire": mono(lambda r: fire_burst(r, 0.8)),
        "SFX_Arcane": mono(lambda r: layer(0.45, (whoosh(r, 0.4, 900, 4000, 1.3), 0.0, 0.5), (sparkle(r, 0.4, 1500, 7), 0.0, 1.0))),
        "SFX_BossSlam": mono(lambda r: boom(r, 1.4)),
        "SFX_BossWarn": mono(lambda r: layer(2.3, (rumble(r, 2.2, 160) * env_adsr(int(2.2 * SR), 0.3, 0.5, 0.8, 1.0), 0.0, 1.2),
                                              (sweep_sine(70, 55, 2.2) * env_adsr(int(2.2 * SR), 0.2, 0.4, 0.7, 1.0), 0.0, 0.4))),
        "SFX_UI": mono(lambda r: layer(0.1, (impact(r, 0.08, 900, 0.25, 0.35), 0.0, 1.0), (bell(2637, 0.08, r), 0.0, 0.25))),
        "SFX_Portal": mono(lambda r: layer(1.5, (whoosh(r, 1.4, 200, 1400, 0.8), 0.0, 0.6), (sparkle(r, 1.4, 1000, 12), 0.0, 0.8))),
        "SFX_PowerUpSpawn": "jingle_spawn",
        "SFX_PowerUp": "jingle_powerup",
        "SFX_LevelUp": "jingle_levelup",
        "SFX_Victory": "jingle_victory",
        "SFX_Defeat": "jingle_defeat",
    }


JINGLES = {
    "jingle_levelup": ([(8, [(72, 0, 0.5, 100), (76, 0.5, 0.5, 100), (79, 1, 0.5, 105), (84, 1.5, 1.5, 110)]),
                        (49, [(60, 0, 3, 70), (64, 0, 3, 70), (67, 0, 3, 70)])], 140),
    "jingle_powerup": ([(9, [(79, 0, 0.25, 100), (83, 0.25, 0.25, 100), (86, 0.5, 0.25, 100), (91, 0.75, 1.5, 110)]),
                        (46, [(67, 0, 2, 80), (71, 0.1, 2, 80), (74, 0.2, 2, 80)])], 150),
    "jingle_spawn": ([(11, [(76, 0, 0.5, 90), (83, 0.5, 1.5, 95)]), (95, [(64, 0, 2, 60), (71, 0, 2, 60)])], 120),
    "jingle_victory": ([(56, [(67, 0, 0.5, 105), (67, 0.5, 0.25, 100), (67, 0.75, 0.25, 100), (72, 1, 1, 112), (76, 2, 2, 115)]),
                        (61, [(55, 0, 1, 90), (60, 1, 1, 95), (64, 2, 2, 100)]), (47, [(43, 0, 1, 110), (48, 2, 2, 115)]),
                        (48, [(60, 2, 2, 80), (64, 2, 2, 80), (67, 2, 2, 80)])], 112),
    "jingle_defeat": ([(48, [(62, 0, 1.5, 80), (65, 0, 1.5, 80), (60, 1.5, 1.5, 75), (63, 1.5, 1.5, 75), (57, 3, 3, 70), (60, 3, 3, 70)]),
                       (68, [(74, 0, 1.5, 85), (72, 1.5, 1.5, 80), (69, 3, 3, 75)])], 72),
}


def build_sfx(only):
    out = os.path.join(AUDIO, "SFX")
    room = Pedalboard([Reverb(room_size=0.35, wet_level=0.14, dry_level=0.92), HighpassFilter(cutoff_frequency_hz=40),
                       LowpassFilter(cutoff_frequency_hz=10000)])
    for name, spec in sfx_specs().items():
        if only and name not in only:
            continue
        rng = rng_for(name)
        if isinstance(spec, str):
            notes, bpm = JINGLES[spec]
            audio = jingle(name, notes, None, bpm)
            audio = peak_normalize(audio, 0.85)
        else:
            mono = np.asarray(spec(rng), dtype=np.float32)
            audio = room(to_stereo(mono).astype(np.float32), SR)
            fade = int(0.01 * SR)
            audio[:, -fade:] *= np.linspace(1, 0, fade)
            audio = peak_normalize(audio, 0.89)
        write_ogg(audio, os.path.join(out, name + ".ogg"), quality="4")
        print(f"SFX {name} {audio.shape[1] / SR:.2f}s", flush=True)


def main():
    if not os.path.isfile(SOUNDFONT):
        raise SystemExit("Soundfont tidak ada: " + SOUNDFONT + " (lihat docs/asset_pipeline.md)")
    what = sys.argv[1] if len(sys.argv) > 1 else "all"
    only = set(sys.argv[2:])
    if what in ("music", "all"):
        build_music(only)
    if what in ("ambience", "all"):
        build_ambience(only)
    if what in ("sfx", "all"):
        build_sfx(only)
    print("AUDIO_HD_OK", flush=True)


if __name__ == "__main__":
    main()
