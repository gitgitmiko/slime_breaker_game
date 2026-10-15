"""Original sound effects, music, UI sprites, and icons for Slime Breaker.

Verified tools on this machine: numpy + Pillow + MIDIUtil (toolchain venv), `fluidsynth`, `oggenc`.
SFX are synthesized here (44.1 kHz). Music is an original MIDI arrangement rendered with the
MIT-licensed FluidR3 GM soundfont.

Run from the repository root:
  python3 asset_work/scripts/build_audio_ui.py
"""

import math
import os
import subprocess
import wave

import numpy as np
from midiutil import MIDIFile
from PIL import Image, ImageDraw, ImageFilter

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
UI = os.path.join(ROOT, "asset_work", "output", "UI")
SFX = os.path.join(ROOT, "asset_work", "output", "Audio", "SFX")
MUSIC = os.path.join(ROOT, "asset_work", "output", "Audio", "Music")
TMP = os.path.join(ROOT, "asset_work", "intermediate")
RATE = 44100
RNG = np.random.default_rng(1234)


def sh(cmd):
    print("CMD", " ".join(cmd), flush=True)
    subprocess.check_call(cmd)


# ---------------------------------------------------------------- synthesis helpers


def seconds(duration):
    return np.arange(int(duration * RATE)) / RATE


def envelope(n, attack=0.005, decay=0.0, sustain=1.0, release=0.1, curve=1.0):
    """ADSR in seconds -> gain array of length n."""
    a = max(1, int(attack * RATE))
    d = max(0, int(decay * RATE))
    r = max(1, int(release * RATE))
    env = np.ones(n)
    env[:a] = np.linspace(0.0, 1.0, a)
    if d > 0:
        end = min(n, a + d)
        env[a:end] = np.linspace(1.0, sustain, end - a)
        env[end:] = sustain
    tail = min(n, r)
    env[n - tail :] *= np.linspace(1.0, 0.0, tail) ** curve
    return env


def sine(freq, duration, phase=0.0):
    t = seconds(duration)
    if np.isscalar(freq):
        return np.sin(math.tau * freq * t + phase)
    # frequency sweep: integrate instantaneous frequency
    return np.sin(math.tau * np.cumsum(freq) / RATE + phase)


def sweep(f0, f1, duration, shape=1.0):
    t = np.linspace(0.0, 1.0, int(duration * RATE))
    return f0 + (f1 - f0) * (t ** shape)


def saw(freq, duration):
    t = seconds(duration)
    phase = (freq * t) % 1.0 if np.isscalar(freq) else (np.cumsum(freq) / RATE) % 1.0
    return 2.0 * phase - 1.0


def square(freq, duration, duty=0.5):
    t = seconds(duration)
    phase = (freq * t) % 1.0 if np.isscalar(freq) else (np.cumsum(freq) / RATE) % 1.0
    return np.where(phase < duty, 1.0, -1.0)


def noise(duration):
    return RNG.uniform(-1.0, 1.0, int(duration * RATE))


def lowpass(signal, cutoff):
    """One-pole low-pass; cutoff can be a scalar or an array (per sample)."""
    out = np.empty_like(signal)
    if np.isscalar(cutoff):
        alpha = np.full(len(signal), 1.0 - math.exp(-math.tau * cutoff / RATE))
    else:
        alpha = 1.0 - np.exp(-math.tau * np.asarray(cutoff) / RATE)
    state = 0.0
    for i in range(len(signal)):
        state += alpha[i] * (signal[i] - state)
        out[i] = state
    return out


def highpass(signal, cutoff):
    return signal - lowpass(signal, cutoff)


def bandpass(signal, low, high):
    return highpass(lowpass(signal, high), low)


def delay(signal, time, feedback=0.3, mix=0.3, repeats=4):
    out = signal.copy()
    step = int(time * RATE)
    tap = signal.copy()
    gain = mix
    for _ in range(repeats):
        tap = np.concatenate((np.zeros(step), tap))[: len(signal)] * feedback
        out += tap * gain / max(feedback, 1e-3)
        gain *= feedback
    return out


def mix(*layers):
    length = max(len(layer) for layer in layers)
    out = np.zeros(length)
    for layer in layers:
        out[: len(layer)] += layer
    return out


def normalize(signal, peak=0.9):
    m = np.max(np.abs(signal))
    return signal / m * peak if m > 1e-6 else signal


def pad(signal, duration):
    n = int(duration * RATE)
    return np.concatenate((signal, np.zeros(max(0, n - len(signal)))))


def write_wav(path, samples):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    data = (np.clip(samples, -1.0, 1.0) * 32767).astype("<i2")
    with wave.open(path, "w") as handle:
        handle.setnchannels(1)
        handle.setsampwidth(2)
        handle.setframerate(RATE)
        handle.writeframes(data.tobytes())


def to_ogg(wav_path, ogg_path, quality="5"):
    os.makedirs(os.path.dirname(ogg_path), exist_ok=True)
    sh(["oggenc", "-Q", "-q", quality, wav_path, "-o", ogg_path])


def sfx(name, samples, peak=0.9):
    samples = normalize(samples, peak)
    wav_path = os.path.join(TMP, name + ".wav")
    write_wav(wav_path, samples)
    to_ogg(wav_path, os.path.join(SFX, name + ".ogg"))


# ---------------------------------------------------------------- sound design


def tone(freq, duration, attack=0.004, release=0.08, harmonics=((1, 1.0), (2, 0.35), (3, 0.15))):
    out = np.zeros(int(duration * RATE))
    for mult, gain in harmonics:
        out += sine(freq * mult if np.isscalar(freq) else freq * mult, duration) * gain
    return out * envelope(len(out), attack=attack, release=release)


def chime(notes, step, duration, release=0.35, detune=1.003):
    layers = []
    for index, freq in enumerate(notes):
        note = tone(freq, duration, release=release) + tone(freq * detune, duration, release=release) * 0.4
        layers.append(np.concatenate((np.zeros(int(index * step * RATE)), note)))
    return mix(*layers)


def build_sfx():
    # Hero bolt: short airy whoosh with a sparkle climbing up.
    cast_noise = bandpass(noise(0.22), 900, 5000) * envelope(int(0.22 * RATE), attack=0.01, release=0.15)
    cast_sparkle = tone(sweep(880, 1760, 0.18, 0.6), 0.18, release=0.1, harmonics=((1, 0.8), (2, 0.3)))
    sfx("SFX_Cast", mix(cast_noise * 0.5, cast_sparkle * 0.6))

    # Bolt impact on slime: squishy pop + bright tick.
    pop = lowpass(noise(0.12), 1800) * envelope(int(0.12 * RATE), attack=0.001, release=0.1, curve=2.0)
    squelch = tone(sweep(520, 140, 0.14, 0.5), 0.14, release=0.1)
    tick = tone(2400, 0.04, release=0.03, harmonics=((1, 1.0),))
    sfx("SFX_Hit", mix(pop * 0.7, squelch * 0.8, tick * 0.4))
    sfx("SFX_BoltHit", mix(pop * 0.5, tone(sweep(1500, 600, 0.12), 0.12, release=0.09) * 0.7, tick * 0.5))

    # Slime death: wet burst with bubbles.
    burst = lowpass(noise(0.32), sweep(2600, 300, 0.32)) * envelope(int(0.32 * RATE), attack=0.002, release=0.25, curve=1.5)
    bubbles = np.zeros(int(0.32 * RATE))
    for offset, freq in ((0.03, 700), (0.09, 950), (0.16, 1200)):
        b = tone(sweep(freq, freq * 1.8, 0.05), 0.05, release=0.04, harmonics=((1, 1.0),)) * 0.5
        start = int(offset * RATE)
        bubbles[start : start + len(b)] += b
    sfx("SFX_EnemyDeath", mix(burst * 0.8, bubbles))

    # Player hit: heavy thump, bass drop, grit.
    thump = tone(sweep(170, 55, 0.28, 0.4), 0.28, release=0.2, harmonics=((1, 1.0), (2, 0.2)))
    grit = lowpass(noise(0.18), 900) * envelope(int(0.18 * RATE), attack=0.001, release=0.15, curve=2.0)
    sfx("SFX_PlayerHit", mix(thump * 1.0, grit * 0.5))

    # Dash: rising whoosh.
    whoosh = bandpass(noise(0.3), sweep(300, 2400, 0.3, 0.7), sweep(900, 6000, 0.3, 0.7)) * envelope(int(0.3 * RATE), attack=0.03, release=0.18)
    sfx("SFX_Dash", mix(whoosh * 0.9, tone(sweep(300, 700, 0.25), 0.25, release=0.15, harmonics=((1, 0.6),)) * 0.25))

    # XP pickup: two quick bell notes.
    sfx("SFX_Pickup", chime((1318.5, 1760.0), 0.05, 0.16, release=0.12), peak=0.75)

    # Level up: ascending arpeggio with shimmer tail.
    arp = chime((523.25, 659.25, 783.99, 1046.5), 0.09, 0.5, release=0.4)
    shimmer = bandpass(noise(0.9), 3000, 9000) * envelope(int(0.9 * RATE), attack=0.2, release=0.6) * 0.15
    sfx("SFX_LevelUp", delay(mix(arp, shimmer), 0.16, feedback=0.35, mix=0.25))

    # Power-up appears: magical rising sweep with sparkles.
    rise = tone(sweep(220, 1760, 0.9, 1.6), 0.9, attack=0.05, release=0.3, harmonics=((1, 0.7), (2, 0.3), (3, 0.1)))
    sparkle = np.zeros(int(1.0 * RATE))
    for i in range(10):
        start = int(RNG.uniform(0.25, 0.9) * RATE)
        s = tone(RNG.choice((2093, 2637, 3136, 3520)), 0.08, release=0.07, harmonics=((1, 1.0),)) * 0.35
        sparkle[start : start + len(s)] += s
    sfx("SFX_PowerUpSpawn", mix(rise * 0.7, sparkle))

    # Power-up collected: triumphant major chord + bells.
    chord = mix(tone(392.0, 1.0, release=0.7), tone(493.88, 1.0, release=0.7), tone(587.33, 1.0, release=0.7), tone(783.99, 1.0, release=0.7)) * 0.3
    bells = chime((1567.98, 1975.53, 2349.32), 0.07, 0.6, release=0.5) * 0.6
    sfx("SFX_PowerUp", delay(mix(chord, bells), 0.2, feedback=0.3, mix=0.2))

    # Ice: glassy tinkle.
    ice = chime((2093, 2793.8, 3520, 4186), 0.03, 0.3, release=0.25, detune=1.01) * 0.6
    frost = bandpass(noise(0.35), 4000, 11000) * envelope(int(0.35 * RATE), attack=0.005, release=0.3) * 0.3
    sfx("SFX_Ice", mix(ice, frost))
    sfx("SFX_Freeze", mix(tone(sweep(3200, 1800, 0.12), 0.12, release=0.1, harmonics=((1, 1.0), (2.76, 0.4))) * 0.7, bandpass(noise(0.1), 3000, 9000) * envelope(int(0.1 * RATE), release=0.08) * 0.4))

    # Lightning: snap + crackle + short thunder.
    snap = highpass(noise(0.03), 2000) * envelope(int(0.03 * RATE), attack=0.0005, release=0.025, curve=3.0)
    crackle = bandpass(noise(0.25), 800, 6000) * (RNG.uniform(0, 1, int(0.25 * RATE)) > 0.75) * envelope(int(0.25 * RATE), release=0.2, curve=2.0)
    thunder = lowpass(noise(0.45), sweep(400, 80, 0.45)) * envelope(int(0.45 * RATE), attack=0.01, release=0.4, curve=1.5)
    sfx("SFX_Lightning", mix(snap * 1.0, crackle * 0.6, thunder * 0.8))

    # Fire nova: whoomph + rumble.
    whoomph = lowpass(noise(0.5), sweep(3000, 200, 0.5, 0.5)) * envelope(int(0.5 * RATE), attack=0.02, release=0.4)
    rumble = tone(sweep(120, 60, 0.5), 0.5, release=0.4, harmonics=((1, 1.0), (1.5, 0.3)))
    sfx("SFX_Fire", mix(whoomph * 0.9, rumble * 0.6))

    # Arcane missile: pew.
    pew = tone(sweep(1400, 500, 0.2, 0.8), 0.2, release=0.15, harmonics=((1, 1.0), (2, 0.4), (3, 0.15)))
    sfx("SFX_Arcane", mix(pew * 0.8, bandpass(noise(0.15), 1500, 5000) * envelope(int(0.15 * RATE), release=0.12) * 0.25))

    # Boss warning horn.
    horn = mix(saw(110, 0.8), saw(110.7, 0.8) * 0.8, saw(165, 0.8) * 0.5)
    horn = lowpass(horn, sweep(400, 1600, 0.8)) * envelope(int(0.8 * RATE), attack=0.05, release=0.4)
    sfx("SFX_BossWarn", mix(horn * 0.8, tone(55, 0.8, attack=0.05, release=0.5) * 0.4))

    # Boss slam: sub boom + debris.
    boom = tone(sweep(140, 35, 0.6, 0.35), 0.6, release=0.45, harmonics=((1, 1.0), (2, 0.25)))
    debris = lowpass(noise(0.4), sweep(5000, 400, 0.4)) * envelope(int(0.4 * RATE), attack=0.002, release=0.35, curve=2.0)
    sfx("SFX_BossSlam", mix(boom * 1.0, debris * 0.6))

    # UI click.
    sfx("SFX_UI", mix(tone(880, 0.07, release=0.05, harmonics=((1, 1.0), (2, 0.3))) * 0.7, highpass(noise(0.03), 3000) * envelope(int(0.03 * RATE), release=0.02) * 0.3), peak=0.7)

    # Victory fanfare / defeat sting.
    fanfare = chime((523.25, 659.25, 783.99, 1046.5, 1318.5), 0.13, 0.7, release=0.5)
    fanfare = mix(fanfare, pad(tone(261.63, 1.6, attack=0.05, release=1.0) * 0.3, 1.6), pad(tone(392.0, 1.6, attack=0.05, release=1.0) * 0.25, 1.6))
    sfx("SFX_Victory", delay(fanfare, 0.22, feedback=0.3, mix=0.2))
    defeat = chime((392.0, 349.23, 311.13, 233.08), 0.28, 0.9, release=0.7)
    sfx("SFX_Defeat", mix(defeat, pad(tone(116.54, 1.8, attack=0.1, release=1.2) * 0.35, 1.8)))

    # Exit portal: airy swirl plus a bright chime.
    swirl = bandpass(noise(0.7), sweep(400, 2800, 0.7, 0.7), sweep(1200, 7000, 0.7, 0.7)) * envelope(int(0.7 * RATE), attack=0.08, release=0.4)
    portal_chime = chime((659.25, 987.77, 1318.5), 0.08, 0.45, release=0.35)
    sfx("SFX_Portal", mix(swirl * 0.7, portal_chime * 0.55))

    # Loopable rain and wind ambience (a few seconds, faded at the ends so they tile).
    rain = highpass(bandpass(noise(4.0), 800, 9000), 400)
    rain *= envelope(len(rain), attack=0.4, release=0.4)
    drops = np.zeros_like(rain)
    for _ in range(80):
        start = int(RNG.uniform(0.2, 3.6) * RATE)
        tick = highpass(noise(0.04), 4000) * envelope(int(0.04 * RATE), attack=0.001, release=0.03) * RNG.uniform(0.15, 0.4)
        end = min(len(drops), start + len(tick))
        drops[start:end] += tick[: end - start]
    sfx("SFX_Rain", mix(rain * 0.55, drops), peak=0.55)

    wind = lowpass(noise(5.0), 700)
    t = np.arange(len(wind)) / RATE
    wind *= 0.55 + 0.45 * np.sin(t * 0.7) * np.sin(t * 1.3 + 0.4)
    wind *= envelope(len(wind), attack=0.5, release=0.5)
    gust = bandpass(noise(5.0), 200, 1400) * (0.3 + 0.2 * np.sin(t * 0.45 + 1.1))
    sfx("SFX_Wind", mix(wind * 0.8, gust * 0.25), peak=0.5)


# ---------------------------------------------------------------- music


SOUNDFONTS = [
    os.path.expanduser("~/game-toolchain/syslibs/usr/share/sounds/sf2/FluidR3_GM.sf2"),
    "/usr/share/sounds/sf2/FluidR3_GM.sf2",
]


def render_midi(midi, name, gain="0.9"):
    soundfont = next(path for path in SOUNDFONTS if os.path.isfile(path))
    midi_path = os.path.join(TMP, name + ".mid")
    wav_path = os.path.join(TMP, name + ".wav")
    os.makedirs(TMP, exist_ok=True)
    with open(midi_path, "wb") as handle:
        midi.writeFile(handle)
    sh(["fluidsynth", "-ni", "-g", gain, "-F", wav_path, "-r", str(RATE), soundfont, midi_path])
    to_ogg(wav_path, os.path.join(MUSIC, name + ".ogg"), quality="4")


def arena_music():
    """Original 16-bar loop in D minor, 128 BPM: driving bass, drums, pad, lead and counter-melody."""
    midi = MIDIFile(5, removeDuplicates=False, deinterleave=False)
    bpm = 128
    for track in range(5):
        midi.addTempo(track, 0, bpm)
    lead, pad_track, bass, drums, counter = 0, 1, 2, 3, 4
    midi.addProgramChange(lead, 0, 0, 81)  # lead square
    midi.addProgramChange(pad_track, 1, 0, 89)  # warm pad
    midi.addProgramChange(bass, 2, 0, 39)  # synth bass 2
    midi.addProgramChange(counter, 3, 0, 11)  # vibraphone
    # chord progression per bar (root midi, chord intervals)
    progression = [(50, (0, 3, 7)), (46, (0, 4, 7)), (53, (0, 4, 7)), (48, (0, 4, 7))] * 4
    D, E, F, G, A, Bb, C = 62, 64, 65, 67, 69, 70, 72
    phrases = [
        [(D, 1), (F, 0.5), (A, 0.5), (G, 1), (F, 1)],
        [(E, 1), (G, 0.5), (Bb, 0.5), (A, 1.5), (G, 0.5)],
        [(F, 0.5), (G, 0.5), (A, 1), (C + 2, 1), (A, 1)],
        [(G, 1), (F, 0.5), (E, 0.5), (D, 2)],
    ]
    for bar, (root, intervals) in enumerate(progression):
        start = bar * 4
        velocity_pad = 55
        for interval in intervals:
            midi.addNote(pad_track, 1, root + interval + 12, start, 4, velocity_pad)
        # bass: root on beats, octave hop on the off-beat
        for beat in range(4):
            midi.addNote(bass, 2, root - 12, start + beat, 0.5, 95)
            midi.addNote(bass, 2, root, start + beat + 0.5, 0.45, 80)
        # drums (channel 9)
        for beat in range(4):
            midi.addNote(drums, 9, 36, start + beat, 0.25, 105)
            midi.addNote(drums, 9, 42, start + beat, 0.2, 70)
            midi.addNote(drums, 9, 42, start + beat + 0.5, 0.2, 55)
        midi.addNote(drums, 9, 38, start + 1, 0.25, 95)
        midi.addNote(drums, 9, 38, start + 3, 0.25, 95)
        if bar % 4 == 3:
            midi.addNote(drums, 9, 38, start + 3.5, 0.2, 80)
            midi.addNote(drums, 9, 49, start + 4, 0.5, 70)
        # lead melody enters after the intro bars
        if bar >= 2:
            time = start
            for note, length in phrases[bar % 4]:
                transpose = 12 if bar >= 10 else 0
                midi.addNote(lead, 0, note + transpose, time, length * 0.95, 88)
                time += length
        # counter melody: arpeggiated chord tones, every 8th
        if bar >= 6:
            for step in range(8):
                interval = intervals[step % len(intervals)]
                midi.addNote(counter, 3, root + interval + 24 - (12 if step % 4 >= 2 else 0), start + step * 0.5, 0.45, 60)
    render_midi(midi, "MUS_Arena")


def menu_music():
    """Calm original waltz-feel loop for the main menu, 96 BPM."""
    midi = MIDIFile(3, removeDuplicates=False, deinterleave=False)
    for track in range(3):
        midi.addTempo(track, 0, 96)
    harp, pad_track, bell = 0, 1, 2
    midi.addProgramChange(harp, 0, 0, 46)
    midi.addProgramChange(pad_track, 1, 0, 91)
    midi.addProgramChange(bell, 2, 0, 10)
    progression = [(50, (0, 3, 7)), (55, (0, 3, 7)), (46, (0, 4, 7)), (57, (0, 4, 7))] * 2
    melody = [74, 77, 81, 79, 77, 74, 72, 74]
    for bar, (root, intervals) in enumerate(progression):
        start = bar * 3
        for interval in intervals:
            midi.addNote(pad_track, 1, root + interval, start, 3, 48)
        for step in range(6):
            midi.addNote(harp, 0, root + intervals[step % 3] + 12 + (12 if step >= 3 else 0), start + step * 0.5, 0.5, 62)
        midi.addNote(bell, 2, melody[bar % len(melody)], start, 1.5, 58)
    render_midi(midi, "MUS_Menu", gain="0.7")


# ---------------------------------------------------------------- UI sprites and icons


SS = 4  # supersampling factor for crisp anti-aliased edges


def canvas(width, height):
    return Image.new("RGBA", (width * SS, height * SS), (0, 0, 0, 0))


def finish(image, path, blur=0.0):
    out = image.resize((image.width // SS, image.height // SS), Image.LANCZOS)
    if blur:
        out = out.filter(ImageFilter.GaussianBlur(blur))
    os.makedirs(os.path.dirname(path), exist_ok=True)
    out.save(path, optimize=True)


def gradient(width, height, top, bottom):
    img = Image.new("RGBA", (width, height))
    px = img.load()
    for y in range(height):
        t = y / max(1, height - 1)
        colour = tuple(int(top[i] + (bottom[i] - top[i]) * t) for i in range(4))
        for x in range(width):
            px[x, y] = colour
    return img


def rounded_mask(width, height, radius):
    mask = Image.new("L", (width, height), 0)
    ImageDraw.Draw(mask).rounded_rectangle((0, 0, width - 1, height - 1), radius=radius, fill=255)
    return mask


def rounded_panel(size, radius, top, bottom, border, border_width, inset=0):
    width, height = size
    img = canvas(width, height)
    w, h = width * SS, height * SS
    body = gradient(w, h, top, bottom)
    mask = rounded_mask(w - inset * 2 * SS, h - inset * 2 * SS, radius * SS)
    img.paste(body.crop((0, 0, mask.width, mask.height)), (inset * SS, inset * SS), mask)
    draw = ImageDraw.Draw(img)
    if border_width > 0:
        draw.rounded_rectangle(
            (inset * SS, inset * SS, w - 1 - inset * SS, h - 1 - inset * SS),
            radius=radius * SS,
            outline=border,
            width=border_width * SS,
        )
    # glossy highlight on the top third
    gloss = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    gloss_draw = ImageDraw.Draw(gloss)
    gloss_draw.rounded_rectangle((inset * SS + 6 * SS, inset * SS + 5 * SS, w - inset * SS - 6 * SS, h // 2), radius=radius * SS, fill=(255, 255, 255, 36))
    img = Image.alpha_composite(img, gloss)
    return img


def panels():
    out = os.path.join(UI, "Panels")
    finish(rounded_panel((512, 320), 36, (28, 22, 66, 240), (12, 10, 34, 240), (242, 193, 78, 255), 5), os.path.join(out, "UI_Panel.png"))
    finish(rounded_panel((420, 120), 30, (104, 86, 230, 255), (58, 44, 150, 255), (236, 226, 255, 255), 3), os.path.join(out, "UI_Button.png"))
    finish(rounded_panel((420, 120), 30, (236, 86, 140, 255), (150, 36, 86, 255), (255, 220, 232, 255), 3), os.path.join(out, "UI_Button_Danger.png"))
    finish(rounded_panel((420, 120), 30, (242, 193, 78, 255), (182, 120, 30, 255), (255, 240, 200, 255), 3), os.path.join(out, "UI_Button_Gold.png"))
    finish(rounded_panel((160, 160), 28, (40, 32, 90, 230), (18, 14, 48, 230), (242, 193, 78, 255), 4), os.path.join(out, "UI_Slot.png"))
    finish(rounded_panel((512, 64), 24, (20, 16, 50, 220), (20, 16, 50, 220), (120, 100, 220, 255), 2), os.path.join(out, "UI_Bar_Back.png"))

    # circular joystick base and knob
    size = 256
    img = canvas(size, size)
    draw = ImageDraw.Draw(img)
    s = size * SS
    draw.ellipse((0, 0, s - 1, s - 1), fill=(20, 16, 50, 150), outline=(242, 193, 78, 230), width=5 * SS)
    draw.ellipse((s * 0.3, s * 0.3, s * 0.7, s * 0.7), outline=(255, 255, 255, 60), width=2 * SS)
    for angle in range(0, 360, 90):
        a = math.radians(angle)
        cx, cy = s / 2 + math.cos(a) * s * 0.4, s / 2 + math.sin(a) * s * 0.4
        draw.polygon(
            [
                (cx + math.cos(a) * 14 * SS, cy + math.sin(a) * 14 * SS),
                (cx + math.cos(a + 2.4) * 12 * SS, cy + math.sin(a + 2.4) * 12 * SS),
                (cx + math.cos(a - 2.4) * 12 * SS, cy + math.sin(a - 2.4) * 12 * SS),
            ],
            fill=(255, 255, 255, 110),
        )
    finish(img, os.path.join(out, "UI_Joystick_Base.png"))

    img = canvas(128, 128)
    draw = ImageDraw.Draw(img)
    s = 128 * SS
    body = gradient(s, s, (255, 255, 255, 235), (190, 180, 240, 235))
    mask = Image.new("L", (s, s), 0)
    ImageDraw.Draw(mask).ellipse((0, 0, s - 1, s - 1), fill=255)
    img.paste(body, (0, 0), mask)
    draw.ellipse((0, 0, s - 1, s - 1), outline=(104, 86, 230, 255), width=4 * SS)
    draw.ellipse((s * 0.2, s * 0.14, s * 0.6, s * 0.4), fill=(255, 255, 255, 120))
    finish(img, os.path.join(out, "UI_Joystick_Knob.png"))

    # soft circle for glow / dash fill
    img = canvas(128, 128)
    ImageDraw.Draw(img).ellipse((0, 0, 128 * SS - 1, 128 * SS - 1), fill=(255, 255, 255, 255))
    finish(img, os.path.join(out, "UI_Circle.png"))


def icon(name, painter, top=(104, 86, 230, 255), bottom=(46, 34, 120, 255)):
    size = 256
    img = rounded_panel((size, size), 52, top, bottom, (242, 193, 78, 255), 7)
    layer = Image.new("RGBA", img.size, (0, 0, 0, 0))
    draw = ImageDraw.Draw(layer)
    painter(draw, size * SS)
    shadow = layer.filter(ImageFilter.GaussianBlur(5 * SS))
    shadow_dark = Image.new("RGBA", img.size, (0, 0, 0, 0))
    shadow_dark.paste((10, 6, 30, 160), (0, 0), shadow.split()[3])
    img = Image.alpha_composite(img, shadow_dark)
    img = Image.alpha_composite(img, layer)
    finish(img, os.path.join(UI, "Icons", name + ".png"))


def star_points(cx, cy, outer, inner, points=5, rotation=-math.pi / 2):
    result = []
    for i in range(points * 2):
        radius = outer if i % 2 == 0 else inner
        angle = rotation + i * math.pi / points
        result.append((cx + math.cos(angle) * radius, cy + math.sin(angle) * radius))
    return result


GOLD = (255, 214, 102, 255)
WHITE = (255, 255, 255, 255)


def paint_damage(draw, s):
    # sword: blade, guard, grip
    c = s / 2
    draw.polygon([(c, s * 0.12), (c + s * 0.07, s * 0.2), (c + s * 0.05, s * 0.62), (c - s * 0.05, s * 0.62), (c - s * 0.07, s * 0.2)], fill=(230, 236, 255, 255))
    draw.rounded_rectangle((c - s * 0.2, s * 0.6, c + s * 0.2, s * 0.68), radius=s * 0.03, fill=GOLD)
    draw.rounded_rectangle((c - s * 0.045, s * 0.66, c + s * 0.045, s * 0.86), radius=s * 0.02, fill=(120, 70, 40, 255))
    draw.ellipse((c - s * 0.06, s * 0.84, c + s * 0.06, s * 0.94), fill=GOLD)


def paint_speed(draw, s):
    for i, dx in enumerate((-0.16, 0.0, 0.16)):
        x = s * (0.42 + dx)
        draw.polygon([(x, s * 0.3), (x + s * 0.16, s * 0.5), (x, s * 0.7), (x + s * 0.06, s * 0.5)], fill=GOLD if i == 2 else WHITE)


def paint_health(draw, s):
    c = s / 2
    r = s * 0.17
    draw.ellipse((c - 2 * r, s * 0.26, c, s * 0.26 + 2 * r), fill=(255, 92, 120, 255))
    draw.ellipse((c, s * 0.26, c + 2 * r, s * 0.26 + 2 * r), fill=(255, 92, 120, 255))
    draw.polygon([(c - 2 * r + s * 0.01, s * 0.26 + r * 1.3), (c + 2 * r - s * 0.01, s * 0.26 + r * 1.3), (c, s * 0.8)], fill=(255, 92, 120, 255))
    draw.ellipse((c - r * 1.2, s * 0.34, c - r * 0.6, s * 0.42), fill=(255, 255, 255, 150))


def paint_attack_rate(draw, s):
    c = s / 2
    draw.ellipse((s * 0.22, s * 0.22, s * 0.78, s * 0.78), outline=WHITE, width=int(s * 0.05))
    draw.line((c, c, c, s * 0.32), fill=GOLD, width=int(s * 0.05))
    draw.line((c, c, s * 0.66, c + s * 0.1), fill=GOLD, width=int(s * 0.05))
    for angle in range(0, 360, 45):
        a = math.radians(angle)
        draw.line((c + math.cos(a) * s * 0.3, c + math.sin(a) * s * 0.3, c + math.cos(a) * s * 0.25, c + math.sin(a) * s * 0.25), fill=WHITE, width=int(s * 0.03))


def paint_dash(draw, s):
    c = s / 2
    draw.polygon([(s * 0.2, s * 0.32), (s * 0.4, s * 0.32), (s * 0.58, s * 0.5), (s * 0.4, s * 0.68), (s * 0.2, s * 0.68), (s * 0.38, s * 0.5)], fill=WHITE)
    draw.polygon([(s * 0.46, s * 0.32), (s * 0.66, s * 0.32), (s * 0.84, s * 0.5), (s * 0.66, s * 0.68), (s * 0.46, s * 0.68), (s * 0.64, s * 0.5)], fill=GOLD)


def paint_range(draw, s):
    c = s / 2
    for radius, colour in ((0.32, WHITE), (0.2, GOLD), (0.08, WHITE)):
        draw.ellipse((c - s * radius, c - s * radius, c + s * radius, c + s * radius), outline=colour, width=int(s * 0.045))
    draw.ellipse((c - s * 0.03, c - s * 0.03, c + s * 0.03, c + s * 0.03), fill=GOLD)


def paint_xp(draw, s):
    draw.polygon(star_points(s / 2, s / 2, s * 0.34, s * 0.15), fill=GOLD)
    draw.polygon(star_points(s / 2, s / 2, s * 0.14, s * 0.06), fill=WHITE)


def paint_lifesteal(draw, s):
    c = s / 2
    r = s * 0.15
    draw.ellipse((c - 2 * r, s * 0.3, c, s * 0.3 + 2 * r), fill=(255, 92, 120, 255))
    draw.ellipse((c, s * 0.3, c + 2 * r, s * 0.3 + 2 * r), fill=(255, 92, 120, 255))
    draw.polygon([(c - 2 * r + s * 0.01, s * 0.3 + r * 1.3), (c + 2 * r - s * 0.01, s * 0.3 + r * 1.3), (c, s * 0.78)], fill=(255, 92, 120, 255))
    draw.rectangle((c - s * 0.035, s * 0.42, c + s * 0.035, s * 0.64), fill=GOLD)
    draw.rectangle((c - s * 0.1, s * 0.5, c + s * 0.1, s * 0.56), fill=GOLD)


def paint_armor(draw, s):
    c = s / 2
    draw.polygon([(c, s * 0.14), (s * 0.78, s * 0.26), (s * 0.74, s * 0.56), (c, s * 0.86), (s * 0.26, s * 0.56), (s * 0.22, s * 0.26)], fill=WHITE)
    draw.polygon([(c, s * 0.24), (s * 0.68, s * 0.32), (s * 0.65, s * 0.54), (c, s * 0.76), (s * 0.35, s * 0.54), (s * 0.32, s * 0.32)], fill=(104, 86, 230, 255))
    draw.line((c, s * 0.3, c, s * 0.7), fill=GOLD, width=int(s * 0.04))
    draw.line((s * 0.4, s * 0.46, s * 0.6, s * 0.46), fill=GOLD, width=int(s * 0.04))


def paint_ice(draw, s):
    c = s / 2
    for i in range(6):
        a = math.radians(i * 60)
        ex, ey = c + math.cos(a) * s * 0.34, c + math.sin(a) * s * 0.34
        draw.line((c, c, ex, ey), fill=WHITE, width=int(s * 0.045))
        for frac in (0.55, 0.8):
            px, py = c + math.cos(a) * s * 0.34 * frac, c + math.sin(a) * s * 0.34 * frac
            for sign in (-1, 1):
                b = a + sign * math.radians(40)
                draw.line((px, py, px + math.cos(b) * s * 0.09, py + math.sin(b) * s * 0.09), fill=(200, 240, 255, 255), width=int(s * 0.03))
    draw.ellipse((c - s * 0.06, c - s * 0.06, c + s * 0.06, c + s * 0.06), fill=(200, 240, 255, 255))


def paint_lightning(draw, s):
    draw.polygon([(s * 0.58, s * 0.12), (s * 0.3, s * 0.54), (s * 0.48, s * 0.54), (s * 0.4, s * 0.88), (s * 0.72, s * 0.42), (s * 0.54, s * 0.42)], fill=GOLD)
    draw.polygon([(s * 0.55, s * 0.2), (s * 0.38, s * 0.5), (s * 0.5, s * 0.5), (s * 0.46, s * 0.72), (s * 0.64, s * 0.46), (s * 0.52, s * 0.46)], fill=(255, 250, 220, 255))


def paint_fire(draw, s):
    c = s / 2
    flame = [(c, s * 0.12), (c + s * 0.22, s * 0.42), (c + s * 0.26, s * 0.62), (c + s * 0.14, s * 0.84), (c - s * 0.14, s * 0.84), (c - s * 0.26, s * 0.62), (c - s * 0.2, s * 0.4), (c - s * 0.06, s * 0.5)]
    draw.polygon(flame, fill=(255, 140, 40, 255))
    inner = [(c + s * 0.02, s * 0.4), (c + s * 0.14, s * 0.6), (c + s * 0.1, s * 0.8), (c - s * 0.1, s * 0.8), (c - s * 0.14, s * 0.62), (c - s * 0.04, s * 0.54)]
    draw.polygon(inner, fill=(255, 230, 120, 255))


def paint_arcane(draw, s):
    c = s / 2
    draw.ellipse((c - s * 0.2, c - s * 0.2, c + s * 0.2, c + s * 0.2), fill=(220, 150, 255, 255))
    draw.ellipse((c - s * 0.1, c - s * 0.13, c + s * 0.02, c - s * 0.02), fill=(255, 255, 255, 180))
    draw.ellipse((c - s * 0.36, c - s * 0.13, c + s * 0.36, c + s * 0.13), outline=WHITE, width=int(s * 0.035))
    for angle, radius in ((30, 0.33), (200, 0.3), (300, 0.36)):
        a = math.radians(angle)
        px, py = c + math.cos(a) * s * radius, c + math.sin(a) * s * radius
        draw.polygon(star_points(px, py, s * 0.05, s * 0.02, points=4), fill=GOLD)


def icons():
    upgrades = {
        "ICON_Upgrade_Damage": paint_damage,
        "ICON_Upgrade_Speed": paint_speed,
        "ICON_Upgrade_Health": paint_health,
        "ICON_Upgrade_AttackRate": paint_attack_rate,
        "ICON_Upgrade_Dash": paint_dash,
        "ICON_Upgrade_Range": paint_range,
        "ICON_Upgrade_Xp": paint_xp,
        "ICON_Upgrade_Armor": paint_armor,
        "ICON_Upgrade_Lifesteal": paint_lifesteal,
    }
    for name, painter in upgrades.items():
        icon(name, painter)
    icon("ICON_Skill_Ice", paint_ice, top=(80, 170, 240, 255), bottom=(26, 70, 150, 255))
    icon("ICON_Skill_Lightning", paint_lightning, top=(130, 110, 240, 255), bottom=(50, 36, 130, 255))
    icon("ICON_Skill_Fire", paint_fire, top=(240, 110, 60, 255), bottom=(130, 40, 30, 255))
    icon("ICON_Skill_Arcane", paint_arcane, top=(190, 90, 240, 255), bottom=(80, 30, 130, 255))


def main():
    os.makedirs(TMP, exist_ok=True)
    build_sfx()
    arena_music()
    menu_music()
    panels()
    icons()
    print("AUDIO_UI_OK", flush=True)


if __name__ == "__main__":
    main()
