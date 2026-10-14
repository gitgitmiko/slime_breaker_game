"""Procedural, tileable textures for Slime Breaker (ground, bark, rock) with numpy + Pillow.

Run from the repository root with the toolchain venv:
  python3 asset_work/scripts/build_textures.py asset_work/textures
"""

import math
import os
import sys

import numpy as np
from PIL import Image, ImageDraw, ImageFilter


def tileable_noise(size, seed, octaves=5, base_cells=3):
    """Tileable value noise built from smoothed random grids."""
    rng = np.random.default_rng(seed)
    total = np.zeros((size, size))
    amplitude = 1.0
    norm = 0.0
    cells = max(2, int(base_cells))
    for _ in range(octaves):
        grid = rng.random((cells, cells))
        img = Image.fromarray((grid * 255).astype(np.uint8), mode="L")
        # Tile the small grid 3x3 before resizing so the bicubic filter wraps seamlessly.
        tiled = Image.new("L", (cells * 3, cells * 3))
        for ty in range(3):
            for tx in range(3):
                tiled.paste(img, (tx * cells, ty * cells))
        big = tiled.resize((size * 3, size * 3), Image.BICUBIC)
        layer = np.asarray(big.crop((size, size, size * 2, size * 2)), dtype=np.float64) / 255.0
        total += layer * amplitude
        norm += amplitude
        amplitude *= 0.5
        cells *= 2
    total /= norm
    return (total - total.min()) / max(1e-6, total.max() - total.min())


def to_rgb(array):
    return (np.clip(array, 0.0, 1.0) * 255).astype(np.uint8)


def lerp(a, b, t):
    return a + (b - a) * t


def normal_map(height, path, strength=2.0):
    dx = (np.roll(height, -1, axis=1) - np.roll(height, 1, axis=1)) * strength
    dy = (np.roll(height, -1, axis=0) - np.roll(height, 1, axis=0)) * strength
    normal = np.dstack((-dx, dy, np.ones_like(height)))
    normal /= np.linalg.norm(normal, axis=2, keepdims=True)
    Image.fromarray(to_rgb(normal * 0.5 + 0.5), mode="RGB").save(path, optimize=True)


def ground(path, size=1024):
    rng = np.random.default_rng(7)
    low = tileable_noise(size, 11, octaves=4, base_cells=4)
    mid = tileable_noise(size, 12, octaves=5, base_cells=16)
    fine = tileable_noise(size, 13, octaves=3, base_cells=64)
    dark = np.array([0.16, 0.36, 0.14])
    grass = np.array([0.33, 0.62, 0.22])
    light = np.array([0.52, 0.78, 0.30])
    dirt = np.array([0.42, 0.33, 0.20])
    colour = np.zeros((size, size, 3))
    for channel in range(3):
        base = lerp(dark[channel], grass[channel], low)
        base = lerp(base, light[channel], np.clip((mid - 0.55) * 2.2, 0.0, 1.0) * 0.8)
        patch = np.clip((low - 0.72) * 5.0, 0.0, 1.0)
        base = lerp(base, dirt[channel], patch * 0.6)
        base += (fine - 0.5) * 0.08
        colour[..., channel] = base
    image = Image.fromarray(to_rgb(colour), mode="RGB")

    # Grass blades: short bright strokes, drawn with wrap-around so the texture still tiles.
    overlay = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    draw = ImageDraw.Draw(overlay)
    for _ in range(9000):
        x = rng.uniform(0, size)
        y = rng.uniform(0, size)
        length = rng.uniform(4, 11)
        angle = rng.uniform(-0.55, 0.55) - math.pi / 2
        shade = rng.uniform(0.0, 1.0)
        tone = (
            int(lerp(70, 150, shade)),
            int(lerp(130, 215, shade)),
            int(lerp(55, 90, shade)),
            int(rng.uniform(90, 170)),
        )
        for ox in (-size, 0, size):
            for oy in (-size, 0, size):
                draw.line(
                    [(x + ox, y + oy), (x + ox + math.cos(angle) * length, y + oy + math.sin(angle) * length)],
                    fill=tone,
                    width=1,
                )
    image = Image.alpha_composite(image.convert("RGBA"), overlay).convert("RGB")
    image = image.filter(ImageFilter.GaussianBlur(0.35))
    image.save(path, optimize=True)
    normal_map(low * 0.6 + mid * 0.3 + fine * 0.1, path.replace(".png", "_Normal.png"), strength=2.2)


def bark(path, size=512):
    streak = tileable_noise(size, 21, octaves=4, base_cells=3)
    detail = tileable_noise(size, 22, octaves=4, base_cells=24)
    grain = np.zeros((size, size))
    for row in range(size):
        grain[row, :] = streak[(row // 6) % size, :]
    grain = grain * 0.65 + detail * 0.35
    dark = np.array([0.24, 0.15, 0.09])
    light = np.array([0.52, 0.36, 0.22])
    colour = np.zeros((size, size, 3))
    for channel in range(3):
        colour[..., channel] = lerp(dark[channel], light[channel], grain)
    Image.fromarray(to_rgb(colour), mode="RGB").save(path, optimize=True)
    normal_map(grain, path.replace(".png", "_Normal.png"), strength=1.6)


def rock(path, size=512):
    low = tileable_noise(size, 31, octaves=4, base_cells=4)
    fine = tileable_noise(size, 32, octaves=4, base_cells=32)
    dark = np.array([0.33, 0.35, 0.40])
    light = np.array([0.62, 0.64, 0.68])
    moss = np.array([0.30, 0.48, 0.24])
    colour = np.zeros((size, size, 3))
    for channel in range(3):
        base = lerp(dark[channel], light[channel], low * 0.7 + fine * 0.3)
        base = lerp(base, moss[channel], np.clip((low - 0.68) * 4.0, 0.0, 1.0) * 0.55)
        colour[..., channel] = base
    Image.fromarray(to_rgb(colour), mode="RGB").save(path, optimize=True)
    normal_map(low * 0.5 + fine * 0.5, path.replace(".png", "_Normal.png"), strength=2.4)


def main():
    out = sys.argv[1] if len(sys.argv) > 1 else "asset_work/textures"
    os.makedirs(out, exist_ok=True)
    ground(os.path.join(out, "TEX_Forest_Ground.png"))
    bark(os.path.join(out, "TEX_Bark.png"))
    rock(os.path.join(out, "TEX_Rock.png"))
    print("TEXTURES_OK", out, flush=True)


if __name__ == "__main__":
    main()
