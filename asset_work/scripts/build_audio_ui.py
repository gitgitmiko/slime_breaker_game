"""Original UI icons and sound effects for Slime Breaker.

Verified tools: ImageMagick `magick`, `oggenc`, `fluidsynth`.
SFX are synthesized in this script. Music is an original MIDI rendered
with the MIT-licensed FluidR3 GM soundfont.
"""

import math
import os
import struct
import subprocess
import wave

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
UI = os.path.join(ROOT, "asset_work", "output", "UI")
SFX = os.path.join(ROOT, "asset_work", "output", "Audio", "SFX")
MUSIC = os.path.join(ROOT, "asset_work", "output", "Audio", "Music")
TMP = os.path.join(ROOT, "asset_work", "intermediate")


def sh(cmd):
    print("CMD", " ".join(cmd), flush=True)
    subprocess.check_call(cmd)


def write_wav(path, samples, rate=22050):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with wave.open(path, "w") as handle:
        handle.setnchannels(1)
        handle.setsampwidth(2)
        handle.setframerate(rate)
        frames = bytearray()
        for sample in samples:
            value = max(-32767, min(32767, int(sample * 32767)))
            frames += struct.pack("<h", value)
        handle.writeframes(frames)


def env_adsr(index, total, attack, release):
    if index < attack:
        return index / max(1, attack)
    if index > total - release:
        return max(0.0, (total - index) / max(1, release))
    return 1.0


def tone(seconds, freq, rate=22050, volume=0.4, harmonics=()):
    total = int(seconds * rate)
    attack = int(0.01 * rate)
    release = int(min(0.12, seconds * 0.45) * rate)
    samples = []
    for i in range(total):
        t = i / rate
        wave_value = math.sin(2 * math.pi * freq * t)
        for harmonic_freq, gain in harmonics:
            wave_value += gain * math.sin(2 * math.pi * harmonic_freq * t)
        samples.append(wave_value * volume * env_adsr(i, total, attack, release))
    return samples


def mix(*tracks):
    length = max(len(track) for track in tracks)
    mixed = [0.0] * length
    for track in tracks:
        for i, sample in enumerate(track):
            mixed[i] += sample
    peak = max(1e-6, max(abs(sample) for sample in mixed))
    if peak > 0.95:
        mixed = [sample / peak * 0.95 for sample in mixed]
    return mixed


def noise(seconds, rate=22050, volume=0.2, cutoff=0.15):
    total = int(seconds * rate)
    samples = []
    state = 0.0
    seed = 1
    for i in range(total):
        seed = (1103515245 * seed + 12345) & 0x7FFFFFFF
        white = ((seed % 2000) / 1000.0) - 1.0
        state = state * (1 - cutoff) + white * cutoff
        samples.append(state * volume * env_adsr(i, total, int(0.005 * rate), int(seconds * 0.5 * rate)))
    return samples


def to_ogg(wav_path, ogg_path):
    os.makedirs(os.path.dirname(ogg_path), exist_ok=True)
    sh(["oggenc", "-Q", wav_path, "-o", ogg_path])


def sfx(name, samples):
    wav_path = os.path.join(TMP, name + ".wav")
    write_wav(wav_path, samples)
    to_ogg(wav_path, os.path.join(SFX, name + ".ogg"))


def varlen(value):
    buffer = [value & 0x7F]
    value >>= 7
    while value:
        buffer.append((value & 0x7F) | 0x80)
        value >>= 7
    return bytes(reversed(buffer))


def midi_note(delta, status, note, velocity=96):
    return varlen(delta) + bytes((status, note, velocity))


def write_midi(path):
    # Original 8-bar loop in C major, 96 BPM. Not a copied melody.
    tempo = 625000  # microseconds per quarter at 96 BPM
    ticks = 480
    events = bytearray()
    events += b"\x00\xFF\x51\x03" + tempo.to_bytes(3, "big")
    events += b"\x00\xC0\x00"  # piano
    events += b"\x00\xC1\x48"  # strings on channel 1
    melody = [72, 74, 76, 79, 76, 74, 67, 69, 72, 67, 64, 67, 69, 72, 74, 72]
    bass = [48, 48, 43, 43, 41, 41, 43, 43]
    step = ticks // 2
    for index, note in enumerate(melody):
        events += midi_note(0 if index == 0 else step, 0x90, note, 88)
        events += midi_note(step // 2, 0x80, note, 0)
        if index % 2 == 1:
            root = bass[index // 2]
            events += midi_note(0, 0x91, root, 70)
            events += midi_note(0, 0x91, root + 7, 60)
            events += midi_note(step, 0x81, root, 0)
            events += midi_note(0, 0x81, root + 7, 0)
    events += b"\x00\xFF\x2F\x00"
    header = b"MThd" + struct.pack(">IHHH", 6, 0, 1, ticks)
    track = b"MTrk" + struct.pack(">I", len(events)) + bytes(events)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "wb") as handle:
        handle.write(header + track)


def render_music():
    soundfonts = [
        "/usr/share/sounds/sf2/FluidR3_GM.sf2",
        os.path.expanduser("~/game-toolchain/syslibs/usr/share/sounds/sf2/FluidR3_GM.sf2"),
    ]
    soundfont = next(path for path in soundfonts if os.path.isfile(path))
    midi_path = os.path.join(TMP, "MUS_Arena.mid")
    wav_path = os.path.join(TMP, "MUS_Arena.wav")
    write_midi(midi_path)
    sh(["fluidsynth", "-ni", "-F", wav_path, "-r", "22050", soundfont, midi_path])
    to_ogg(wav_path, os.path.join(MUSIC, "MUS_Arena.ogg"))


def icon(name, symbol):
    path = os.path.join(UI, "Icons", name + ".png")
    os.makedirs(os.path.dirname(path), exist_ok=True)
    sh(
        [
            "magick",
            "-size",
            "256x256",
            "xc:none",
            "-fill",
            "#14343c",
            "-draw",
            "roundrectangle 12,12 244,244 48,48",
            "-fill",
            "#1f8c9e",
            "-draw",
            "circle 128,128 128,36",
            "-fill",
            "#f2c14e",
            "-stroke",
            "#f2c14e",
            "-strokewidth",
            "10",
            "-draw",
            symbol,
            path,
        ]
    )


def panels():
    os.makedirs(os.path.join(UI, "Panels"), exist_ok=True)
    sh(
        [
            "magick",
            "-size",
            "512x320",
            "xc:none",
            "-fill",
            "#10262c",
            "-stroke",
            "#f2c14e",
            "-strokewidth",
            "10",
            "-draw",
            "roundrectangle 8,8 504,312 36,36",
            os.path.join(UI, "Panels", "UI_Panel.png"),
        ]
    )
    sh(
        [
            "magick",
            "-size",
            "420x120",
            "xc:none",
            "-fill",
            "#1f8c9e",
            "-draw",
            "roundrectangle 4,4 416,116 28,28",
            os.path.join(UI, "Panels", "UI_Button.png"),
        ]
    )
    sh(
        [
            "magick",
            "-size",
            "420x120",
            "xc:none",
            "-fill",
            "#d12e6b",
            "-draw",
            "roundrectangle 4,4 416,116 28,28",
            os.path.join(UI, "Panels", "UI_Button_Danger.png"),
        ]
    )


def main():
    os.makedirs(TMP, exist_ok=True)
    sfx("SFX_Hit", mix(tone(0.09, 660, volume=0.35), tone(0.07, 990, volume=0.2)))
    sfx("SFX_PlayerHit", mix(tone(0.14, 220, volume=0.4), noise(0.1, volume=0.15)))
    sfx("SFX_Dash", tone(0.16, 520, volume=0.28, harmonics=((780, 0.4), (1040, 0.2))))
    sfx("SFX_Pickup", mix(tone(0.08, 880, volume=0.3), tone(0.12, 1320, volume=0.22)))
    sfx("SFX_LevelUp", mix(tone(0.18, 523, volume=0.3), tone(0.22, 659, volume=0.25), tone(0.28, 784, volume=0.22)))
    sfx("SFX_Death", mix(tone(0.45, 196, volume=0.35, harmonics=((98, 0.4),)), noise(0.3, volume=0.12)))
    sfx("SFX_BossWarn", mix(tone(0.35, 146, volume=0.4), tone(0.35, 155, volume=0.25)))
    sfx("SFX_BossSlam", mix(noise(0.28, volume=0.45, cutoff=0.4), tone(0.2, 90, volume=0.35)))
    sfx("SFX_UI", tone(0.06, 740, volume=0.25))
    sfx("SFX_Victory", mix(tone(0.2, 523, volume=0.28), tone(0.28, 659, volume=0.24), tone(0.4, 784, volume=0.22), tone(0.5, 1046, volume=0.18)))
    sfx("SFX_Defeat", mix(tone(0.35, 311, volume=0.3), tone(0.5, 233, volume=0.28)))
    render_music()
    panels()
    icons = {
        "ICON_Upgrade_Damage": "polygon 128,70 112,168 128,148 144,168",
        "ICON_Upgrade_Speed": "line 64,168 168,88",
        "ICON_Upgrade_Health": "polygon 128,72 176,112 176,168 80,168 80,112",
        "ICON_Upgrade_AttackRate": "circle 128,128 128,78",
        "ICON_Upgrade_Dash": "polygon 70,160 150,96 118,96 168,96 140,128",
        "ICON_Upgrade_Range": "circle 128,128 128,72",
        "ICON_Upgrade_Xp": "polygon 128,64 146,116 200,116 156,146 172,198 128,164 84,198 100,146 56,116 110,116",
        "ICON_Upgrade_Armor": "polygon 128,68 180,96 180,148 128,184 76,148 76,96",
    }
    for name, symbol in icons.items():
        icon(name, symbol)
    print("AUDIO_UI_OK", flush=True)


if __name__ == "__main__":
    main()
