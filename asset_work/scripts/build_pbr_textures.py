"""HD PBR texture sets for the arena floors and props (CPU, numpy + Pillow).

Run from the repository root with the toolchain venv:
  python3 asset_work/scripts/build_pbr_textures.py asset_work/textures [name ...]

Each set is written by pbr.save_surface (albedo+height, normal, mask, optional emission). Ground sets are
authored for a 6 m tile; the arena shader breaks up repetition with a second rotated sample and macro noise.
"""

import math
import os
import sys
import time

import numpy as np
from PIL import Image, ImageDraw
from scipy import ndimage as ndi

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import pbr  # noqa: E402
from pbr import F, Layer, Surface, blur, fbm, leaf_polygon, mix, ramp, smoothstep, worley  # noqa: E402

N = 2048


def shade_ao(albedo, ao, amount=0.45):
    return albedo * (1.0 - amount + amount * ao)[..., None]


# ---------------------------------------------------------------- shared detail stamps


def grass_blades(n, seed, count, length=(10, 24), palette=None):
    """Short blade strokes, mostly upright in texture space with random lean, drawn tileable."""
    rng = np.random.default_rng(seed)
    palette = palette or [(0.20, 0.34, 0.09), (0.30, 0.46, 0.13), (0.42, 0.56, 0.18), (0.56, 0.62, 0.26)]
    layer = Layer(n)
    for _ in range(count):
        x, y = rng.uniform(0, n), rng.uniform(0, n)
        ln = rng.uniform(*length)
        a = rng.uniform(0, math.tau)
        c = palette[rng.integers(len(palette))]
        tone = rng.uniform(0.75, 1.15)
        rgba = tuple(int(min(1, ch * tone) * 255) for ch in c) + (int(rng.uniform(150, 235)),)
        p1 = (x + math.cos(a) * ln, y + math.sin(a) * ln)
        layer.line((x, y), p1, rgba, rng.uniform(120, 255), rng.uniform(0.8, 1.6), ln + 4)
    return layer.result()


def leaf_litter(n, seed, count, size=(16, 34), palette=None):
    rng = np.random.default_rng(seed)
    palette = palette or [(0.45, 0.30, 0.12), (0.55, 0.38, 0.14), (0.62, 0.46, 0.18), (0.36, 0.30, 0.14), (0.30, 0.36, 0.14), (0.66, 0.30, 0.10)]
    layer = Layer(n)
    for _ in range(count):
        x, y = rng.uniform(0, n), rng.uniform(0, n)
        ln = rng.uniform(*size)
        c = palette[rng.integers(len(palette))]
        tone = rng.uniform(0.7, 1.1)
        rgba = tuple(int(min(1, ch * tone) * 255) for ch in c) + (int(rng.uniform(200, 255)),)
        pts = leaf_polygon(x, y, ln, ln * rng.uniform(0.35, 0.55), rng.uniform(0, math.tau))
        layer.polygon(pts, rgba, rng.uniform(140, 230), ln)
    return layer.result()


def twigs(n, seed, count, length=(30, 80), colour=(0.22, 0.15, 0.09)):
    rng = np.random.default_rng(seed)
    layer = Layer(n)
    for _ in range(count):
        x, y = rng.uniform(0, n), rng.uniform(0, n)
        a = rng.uniform(0, math.tau)
        ln = rng.uniform(*length)
        tone = rng.uniform(0.7, 1.2)
        rgba = tuple(int(min(1, ch * tone) * 255) for ch in colour) + (230,)
        mid = (x + math.cos(a) * ln * 0.5, y + math.sin(a) * ln * 0.5)
        a2 = a + rng.uniform(-0.5, 0.5)
        end = (mid[0] + math.cos(a2) * ln * 0.5, mid[1] + math.sin(a2) * ln * 0.5)
        w = rng.uniform(1.5, 3.2)
        layer.line((x, y), mid, rgba, 200, w, ln + 4)
        layer.line(mid, end, rgba, 190, w * 0.8, ln + 4)
    return layer.result()


def pebbles(n, seed, cells, keep, radius=0.34):
    """Rounded stones on a voronoi lattice: returns (mask, dome height, per-stone tone)."""
    f1, _f2, cid = worley(n, seed, cells)
    chosen = (cid > 1.0 - keep).astype(F)
    size = radius * (0.6 + 0.8 * ((cid * 7.31) % 1.0))
    mask = smoothstep(size, size * 0.72, f1) * chosen
    dome = np.clip(1.0 - (f1 / np.maximum(size, 1e-3)) ** 2, 0.0, 1.0) * chosen
    return mask, np.sqrt(dome), (cid * 13.7) % 1.0


# ---------------------------------------------------------------- ground sets


def forest():
    """Grass-dominant forest floor: soft multi-scale colour variation, clover, dry tufts, a few bare-soil
    patches with leaf litter, twigs and pebbles. Large patches are low frequency so they read at 10 m."""
    macro = fbm(N, 100, freq=2, octaves=4, warp=0.2)
    soil_mask = smoothstep(0.74, 0.86, fbm(N, 101, freq=2, octaves=6, warp=0.12) * 0.75 + fbm(N, 108, freq=5, octaves=4) * 0.25)
    variation = fbm(N, 102, freq=3, octaves=6, warp=0.1)
    patchy = fbm(N, 109, freq=8, octaves=5)
    streak = fbm(N, 103, freq=8, octaves=4, stretch=(1, 5))
    fine = fbm(N, 104, freq=64, octaves=3)
    grass_col = ramp(variation * 0.55 + patchy * 0.3 + streak * 0.15,
                     [(0.0, (0.14, 0.24, 0.07)), (0.4, (0.22, 0.35, 0.10)), (0.7, (0.30, 0.43, 0.13)), (1.0, (0.42, 0.50, 0.18))])
    dry = smoothstep(0.66, 0.82, fbm(N, 106, freq=4, octaves=5, warp=0.15))
    grass_col = mix(grass_col, np.array((0.46, 0.44, 0.22), F) * (0.85 + 0.25 * fine[..., None]), dry * 0.45)
    f1, _f2, cid = worley(N, 115, 40)
    clover = smoothstep(0.45, 0.3, f1) * (cid > 0.82) * (1.0 - dry)
    grass_col = mix(grass_col, np.array((0.20, 0.38, 0.12), F), clover * 0.6)
    soil_col = ramp(fbm(N, 105, freq=14, octaves=5) * 0.6 + fine * 0.4,
                    [(0.0, (0.15, 0.11, 0.07)), (0.5, (0.26, 0.20, 0.13)), (1.0, (0.37, 0.30, 0.21))])
    moss = smoothstep(0.55, 0.7, fbm(N, 107, freq=10, octaves=5)) * soil_mask
    soil_col = mix(soil_col, np.array((0.19, 0.28, 0.09), F), moss * 0.6)

    blade_rgb, blade_a, blade_h = grass_blades(N, 110, 80000,
                                               palette=[(0.18, 0.30, 0.08), (0.26, 0.40, 0.11), (0.36, 0.48, 0.15), (0.48, 0.52, 0.22)])
    grass_area = 1.0 - soil_mask
    albedo = mix(soil_col, grass_col, grass_area)
    albedo = mix(albedo, blade_rgb, blade_a * np.clip(grass_area * 1.2, 0, 1) * 0.7)

    leaf_rgb, leaf_a, leaf_h = leaf_litter(N, 111, 900)
    litter = np.clip(soil_mask * 1.2 + 0.04, 0, 1) * leaf_a
    albedo = mix(albedo, leaf_rgb, litter)
    twig_rgb, twig_a, twig_h = twigs(N, 112, 120)
    albedo = mix(albedo, twig_rgb, twig_a * (0.25 + 0.75 * soil_mask))
    peb_mask, peb_dome, peb_tone = pebbles(N, 113, 70, 0.10)
    peb_mask *= np.clip(soil_mask * 1.3, 0, 1)
    peb_col = ramp(peb_tone, [(0.0, (0.30, 0.29, 0.27)), (0.5, (0.45, 0.43, 0.39)), (1.0, (0.58, 0.55, 0.48))])
    albedo = mix(albedo, peb_col * (0.8 + 0.25 * fine[..., None]), peb_mask)

    height = (macro * 0.2 + fine * 0.08 + grass_area * (0.25 + blade_h * 0.25 * blade_a) + clover * 0.05
              + litter * leaf_h * 0.25 + peb_dome * peb_mask * 0.6 + twig_a * 0.12)
    smooth = (0.24 * grass_area + 0.12 * soil_mask + litter * 0.12 + peb_mask * 0.3).astype(F)
    wet = smoothstep(0.7, 0.85, fbm(N, 114, freq=3, octaves=4)) * soil_mask
    smooth = np.clip(smooth + wet * 0.35, 0, 1)
    albedo = mix(albedo, albedo * 0.72, wet)
    surf = Surface(albedo, height, smooth, normal_strength=3.0)
    surf.albedo = shade_ao(surf.albedo, surf.ao, 0.35)
    return surf


def snow():
    macro = fbm(N, 200, freq=2, octaves=5, warp=0.3)
    u, v = pbr.grid(N)
    warp = fbm(N, 201, freq=3, octaves=4) - 0.5
    ripple = 0.5 + 0.5 * np.sin((v * 22 + u * 4 + warp * 2.5) * math.tau)
    ripple *= smoothstep(0.35, 0.7, fbm(N, 202, freq=4, octaves=4))
    fine = fbm(N, 203, freq=48, octaves=4)
    ice = smoothstep(0.7, 0.78, fbm(N, 204, freq=3, octaves=5, warp=0.3))
    height = macro * 0.55 + ripple * 0.18 + fine * 0.07
    height = height * (1.0 - ice * 0.6) + ice * 0.25
    albedo = ramp(height, [(0.0, (0.58, 0.66, 0.78)), (0.45, (0.76, 0.80, 0.86)), (1.0, (0.86, 0.88, 0.91))])
    albedo = mix(albedo, np.array((0.74, 0.83, 0.92), F) * (0.92 + 0.12 * fine[..., None]), ice * 0.6)
    rocks, dome, tone = pebbles(N, 205, 26, 0.015, radius=0.4)
    dusting = smoothstep(0.35, 0.7, fbm(N, 206, freq=32, octaves=3))
    rock_col = ramp(tone, [(0.0, (0.22, 0.22, 0.24)), (1.0, (0.40, 0.40, 0.42))])
    albedo = mix(albedo, mix(rock_col, albedo, dusting * 0.6), rocks)
    height = height + dome * rocks * 0.45
    rng = np.random.default_rng(207)
    sparkle = (rng.random((N, N)) > 0.9975).astype(F)
    sparkle = np.clip(blur(sparkle, 0.6) * 4.0, 0, 1)
    smooth = np.clip(0.42 + 0.45 * ice + sparkle * 0.5 - rocks * 0.2, 0, 1)
    albedo = np.clip(albedo + sparkle[..., None] * 0.25, 0, 1)
    surf = Surface(albedo, height, smooth, normal_strength=1.6)
    surf.albedo = shade_ao(surf.albedo, surf.ao, 0.25)
    return surf


def sand():
    u, v = pbr.grid(N)
    warp = fbm(N, 301, freq=2, octaves=4) - 0.5
    warp2 = fbm(N, 302, freq=6, octaves=3) - 0.5
    phase = (v * 34 + u * 6 + warp * 3.0 + warp2 * 0.6) * math.tau
    ripple = (0.5 + 0.5 * np.sin(phase)) ** 1.6
    amp = smoothstep(0.25, 0.65, fbm(N, 303, freq=3, octaves=4))
    dunes = fbm(N, 300, freq=2, octaves=5, warp=0.2)
    grain = fbm(N, 304, freq=128, octaves=2)
    height = dunes * 0.5 + ripple * amp * 0.3 + grain * 0.04
    albedo = ramp(dunes * 0.6 + ripple * amp * 0.25 + grain * 0.15,
                  [(0.0, (0.58, 0.43, 0.26)), (0.5, (0.76, 0.60, 0.38)), (1.0, (0.90, 0.77, 0.53))])
    clay = smoothstep(0.66, 0.74, fbm(N, 305, freq=3, octaves=5, warp=0.3))
    f1, f2, cid = worley(N, 306, 22)
    crack = smoothstep(0.08, 0.02, f2 - f1) * clay
    clay_col = ramp(cid, [(0.0, (0.66, 0.50, 0.34)), (1.0, (0.78, 0.62, 0.43))])
    albedo = mix(albedo, clay_col, clay * 0.85)
    albedo = mix(albedo, albedo * 0.55, crack)
    height = mix(height, 0.45 + f1 * 0.15, clay * 0.7) - crack * 0.25
    stones, dome, tone = pebbles(N, 307, 60, 0.05, radius=0.3)
    albedo = mix(albedo, ramp(tone, [(0.0, (0.42, 0.33, 0.24)), (1.0, (0.66, 0.55, 0.42))]), stones)
    height = height + dome * stones * 0.4
    smooth = np.clip(0.14 + stones * 0.2 + grain * 0.05, 0, 1)
    surf = Surface(albedo, height, smooth, normal_strength=2.4)
    surf.albedo = shade_ao(surf.albedo, surf.ao, 0.3)
    return surf


def thunder():
    macro = fbm(N, 400, freq=2, octaves=5, warp=0.3)
    fine = fbm(N, 401, freq=48, octaves=3)
    soil = ramp(fbm(N, 402, freq=10, octaves=5) * 0.7 + fine * 0.3,
                [(0.0, (0.15, 0.15, 0.12)), (0.5, (0.25, 0.25, 0.19)), (1.0, (0.36, 0.35, 0.27))])
    grass_area = smoothstep(0.45, 0.62, fbm(N, 403, freq=4, octaves=5, warp=0.3))
    blade_rgb, blade_a, blade_h = grass_blades(N, 404, 36000, palette=[(0.26, 0.31, 0.15), (0.34, 0.38, 0.18), (0.44, 0.44, 0.24)])
    albedo = mix(soil, blade_rgb, blade_a * grass_area * 0.9)
    burn = smoothstep(0.7, 0.8, fbm(N, 405, freq=3, octaves=5, warp=0.4))
    albedo = mix(albedo, np.array((0.04, 0.04, 0.04), F) + fine[..., None] * 0.05, burn * 0.9)
    puddle = smoothstep(0.72, 0.76, fbm(N, 406, freq=3, octaves=5, warp=0.25)) * (1 - burn)
    albedo = mix(albedo, np.array((0.05, 0.06, 0.07), F), puddle * 0.85)
    height = macro * 0.5 + fine * 0.08 + grass_area * blade_a * blade_h * 0.25
    height = mix(height, np.full_like(height, 0.2), puddle)
    smooth = np.clip(0.18 + puddle * 0.75 + grass_area * 0.05, 0, 1)
    surf = Surface(albedo, height, smooth, normal_strength=2.6)
    surf.albedo = shade_ao(surf.albedo, surf.ao, 0.35)
    return surf


def swamp():
    macro = fbm(N, 500, freq=2, octaves=5, warp=0.35)
    fine = fbm(N, 501, freq=40, octaves=4)
    mud = ramp(fbm(N, 502, freq=8, octaves=6, warp=0.2) * 0.7 + fine * 0.3,
               [(0.0, (0.15, 0.13, 0.08)), (0.5, (0.26, 0.23, 0.14)), (1.0, (0.38, 0.34, 0.21))])
    algae = smoothstep(0.5, 0.65, fbm(N, 503, freq=6, octaves=5))
    mud = mix(mud, np.array((0.18, 0.28, 0.08), F) * (0.8 + 0.4 * fine[..., None]), algae * 0.7)
    water = smoothstep(0.62, 0.66, fbm(N, 504, freq=3, octaves=5, warp=0.3))
    duckweed = smoothstep(0.75, 0.82, fbm(N, 505, freq=36, octaves=3)) * water
    water_col = np.array((0.04, 0.07, 0.05), F)
    albedo = mix(mud, water_col, water * 0.92)
    albedo = mix(albedo, np.array((0.24, 0.38, 0.10), F), duckweed)
    leaf_rgb, leaf_a, leaf_h = leaf_litter(N, 506, 700, palette=[(0.22, 0.18, 0.08), (0.30, 0.24, 0.10), (0.20, 0.26, 0.10)])
    albedo = mix(albedo, leaf_rgb, leaf_a * (1 - water) * 0.8)
    height = macro * 0.5 + fine * 0.12 + leaf_a * leaf_h * 0.15
    height = mix(height, np.full_like(height, 0.18), water)
    smooth = np.clip(0.35 + water * 0.6 - duckweed * 0.5 + algae * 0.1, 0, 1)
    surf = Surface(albedo, height, smooth, normal_strength=2.4)
    surf.albedo = shade_ao(surf.albedo, surf.ao, 0.35)
    return surf


def shadow():
    f1, f2, cid = worley(N, 600, 7, jitter=0.9)
    edge = f2 - f1
    crack = smoothstep(0.06, 0.015, edge)
    fine = fbm(N, 601, freq=32, octaves=4)
    grain = fbm(N, 602, freq=8, octaves=5, warp=0.2)
    slab = ramp(grain * 0.6 + cid * 0.25 + fine * 0.15,
                [(0.0, (0.10, 0.09, 0.14)), (0.5, (0.19, 0.17, 0.25)), (1.0, (0.30, 0.27, 0.37))])
    chips = smoothstep(0.12, 0.05, edge) * smoothstep(0.55, 0.75, fbm(N, 603, freq=24, octaves=3))
    albedo = mix(slab, slab * 0.5, chips)
    albedo = mix(albedo, np.array((0.02, 0.01, 0.04), F), crack)
    vein = smoothstep(0.055, 0.015, edge) * smoothstep(0.3, 0.6, fbm(N, 604, freq=6, octaves=4))
    emit = vein[..., None] * np.array((0.55, 0.22, 1.0), F)
    albedo = mix(albedo, np.array((0.45, 0.25, 0.75), F), vein * 0.6)
    height = 0.6 + grain * 0.15 + fine * 0.06 - crack * 0.5 - chips * 0.2
    smooth = np.clip(0.38 + fine * 0.1 - crack * 0.3 + vein * 0.4, 0, 1)
    surf = Surface(albedo, height, smooth, emit=emit, normal_strength=3.0)
    surf.albedo = shade_ao(surf.albedo, surf.ao, 0.4)
    return surf


def volcano():
    f1, f2, cid = worley(N, 700, 9, jitter=1.0)
    edge = f2 - f1
    fine = fbm(N, 701, freq=40, octaves=4)
    lumps = fbm(N, 702, freq=6, octaves=6, warp=0.25)
    rock = ramp(lumps * 0.6 + fine * 0.25 + cid * 0.15,
                [(0.0, (0.06, 0.05, 0.05)), (0.5, (0.14, 0.12, 0.11)), (1.0, (0.24, 0.21, 0.19))])
    vh1, _v2, vid = worley(N, 703, 90)
    vesicle = smoothstep(0.22, 0.12, vh1) * (vid > 0.6)
    rock = mix(rock, rock * 0.35, vesicle)
    flow = smoothstep(0.4, 0.75, fbm(N, 704, freq=3, octaves=5, warp=0.3))
    lava = smoothstep(0.07, 0.015, edge) * flow
    hot = smoothstep(0.035, 0.005, edge) * flow
    lava_col = mix(np.array((0.55, 0.08, 0.01), F)[None, None, :] * np.ones((N, N, 1), F), np.array((1.0, 0.62, 0.12), F), hot)
    albedo = mix(rock, lava_col * 0.6, lava)
    ash = smoothstep(0.64, 0.8, fbm(N, 705, freq=4, octaves=5, warp=0.3)) * (1 - lava)
    albedo = mix(albedo, np.array((0.34, 0.32, 0.30), F) * (0.8 + 0.3 * fine[..., None]), ash * 0.75)
    emit = lava_col * (lava * (0.6 + 0.9 * hot))[..., None]
    height = 0.55 + lumps * 0.3 + fine * 0.08 - lava * 0.45 - vesicle * 0.15 - ash * 0.05
    smooth = np.clip(0.2 + fine * 0.1 + lava * 0.5 - ash * 0.15, 0, 1)
    surf = Surface(albedo, height, smooth, emit=emit, normal_strength=3.4)
    surf.albedo = shade_ao(surf.albedo, surf.ao, 0.45)
    return surf


def storm():
    f1, f2, cid = worley(N, 800, 10, jitter=0.85)
    edge = f2 - f1
    gap = smoothstep(0.07, 0.03, edge)
    fine = fbm(N, 801, freq=48, octaves=4)
    layers = fbm(N, 802, freq=6, octaves=5, stretch=(1, 3), warp=0.15)
    slab = ramp(cid * 0.4 + layers * 0.4 + fine * 0.2,
                [(0.0, (0.16, 0.18, 0.21)), (0.5, (0.27, 0.30, 0.34)), (1.0, (0.40, 0.43, 0.46))])
    moss = gap * smoothstep(0.4, 0.6, fbm(N, 803, freq=10, octaves=4))
    albedo = mix(slab, np.array((0.07, 0.08, 0.06), F), gap)
    albedo = mix(albedo, np.array((0.16, 0.24, 0.10), F), moss)
    puddle = (smoothstep(0.82, 0.86, cid) * smoothstep(0.08, 0.2, edge))
    albedo = mix(albedo, albedo * 0.45, puddle)
    height = 0.6 + layers * 0.15 + fine * 0.06 - gap * 0.5 - puddle * 0.1
    wet = 0.5 + 0.1 * fine
    smooth = np.clip(wet + puddle * 0.45 - gap * 0.3 - moss * 0.2, 0, 1)
    surf = Surface(albedo, height, smooth, normal_strength=3.0)
    surf.albedo = shade_ao(surf.albedo, surf.ao, 0.4)
    return surf


def crystal():
    """Dark amethyst bedrock split by thin glowing crystal veins, with a few faceted geode clusters."""
    fine = fbm(N, 901, freq=32, octaves=4)
    rock_n = fbm(N, 902, freq=6, octaves=6, warp=0.25)
    rock = ramp(rock_n * 0.7 + fine * 0.3, [(0.0, (0.09, 0.07, 0.13)), (0.5, (0.17, 0.13, 0.23)), (1.0, (0.28, 0.22, 0.36))])
    v1, v2, _vid = worley(N, 907, 6, jitter=0.95)
    vein = smoothstep(0.035, 0.008, v2 - v1) * smoothstep(0.35, 0.6, fbm(N, 908, freq=5, octaves=4))
    f1, f2, cid = worley(N, 903, 8, jitter=0.9)
    cluster = (cid > 0.92).astype(F) * smoothstep(0.42, 0.28, f1)
    g1, g2, gid = worley(N, 906, 56, jitter=1.0)
    shard = cluster * smoothstep(0.02, 0.05, g2 - g1)
    facet = np.clip(gid * 0.7 + (1.0 - f1 / 0.42) * 0.3, 0, 1)
    crystal_col = ramp(facet, [(0.0, (0.40, 0.25, 0.72)), (0.6, (0.66, 0.46, 0.95)), (1.0, (0.88, 0.76, 1.0))])
    albedo = mix(rock, crystal_col, shard)
    albedo = mix(albedo, np.array((0.62, 0.42, 0.95), F), vein * 0.7)
    rng = np.random.default_rng(905)
    glitter = np.clip(blur((rng.random((N, N)) > 0.9995).astype(F), 0.6) * 4, 0, 1)
    albedo = np.clip(albedo + glitter[..., None] * np.array((0.3, 0.2, 0.5), F), 0, 1)
    emit = ((shard * (0.12 + 0.3 * facet)) + vein * 0.55)[..., None] * np.array((0.55, 0.32, 1.0), F) + glitter[..., None] * 0.15
    height = rock_n * 0.45 + fine * 0.08 + shard * (0.45 + facet * 0.4) - vein * 0.15
    smooth = np.clip(0.38 + shard * 0.5 + vein * 0.4 + glitter * 0.3, 0, 1)
    surf = Surface(albedo, height, smooth, emit=emit, normal_strength=3.2)
    surf.albedo = shade_ao(surf.albedo, surf.ao, 0.4)
    return surf


def royal():
    u, v = pbr.grid(N)
    tiles = 6
    U, V = u * tiles, v * tiles
    row = np.floor(V)
    U = U + (row % 2) * 0.5
    cx, cy = np.floor(U), np.floor(V)
    lu, lv = U - cx, V - cy
    edge = np.minimum(np.minimum(lu, 1 - lu), np.minimum(lv, 1 - lv))
    grout = smoothstep(0.03, 0.012, edge)
    tile_id = ((np.sin(cx * 12.9898 + cy * 78.233) * 43758.5453) % 1.0).astype(F)
    vein_warp = fbm(N, 1001, freq=4, octaves=5, warp=0.4, ridged=True)
    veins = smoothstep(0.82, 0.96, vein_warp)
    cloud = fbm(N, 1002, freq=8, octaves=5)
    marble = ramp(cloud * 0.5 + tile_id * 0.5, [(0.0, (0.72, 0.68, 0.62)), (0.5, (0.84, 0.80, 0.74)), (1.0, (0.93, 0.90, 0.85))])
    marble = mix(marble, np.array((0.50, 0.46, 0.44), F), veins * 0.55)
    pink = (tile_id > 0.85).astype(F)
    marble = mix(marble, marble * np.array((1.0, 0.88, 0.86), F), pink * 0.6)
    wear = smoothstep(0.06, 0.02, edge) * smoothstep(0.5, 0.75, fbm(N, 1003, freq=28, octaves=3))
    moss = grout * smoothstep(0.45, 0.65, fbm(N, 1004, freq=12, octaves=4))
    albedo = mix(marble, marble * 0.78, wear)
    albedo = mix(albedo, np.array((0.25, 0.22, 0.18), F), grout)
    albedo = mix(albedo, np.array((0.22, 0.32, 0.11), F), moss)
    dirt = smoothstep(0.6, 0.85, fbm(N, 1005, freq=3, octaves=5, warp=0.3))
    albedo = mix(albedo, albedo * np.array((0.82, 0.76, 0.66), F), dirt * 0.6)
    fine = fbm(N, 1006, freq=64, octaves=3)
    height = 0.7 + cloud * 0.05 + fine * 0.03 - grout * 0.55 - wear * 0.15 + (tile_id - 0.5) * 0.04
    smooth = np.clip(0.62 + veins * 0.05 - grout * 0.5 - wear * 0.3 - dirt * 0.25, 0, 1)
    surf = Surface(albedo, height, smooth, normal_strength=3.0)
    surf.albedo = shade_ao(surf.albedo, surf.ao, 0.3)
    return surf


GROUNDS = {
    "TEX_Forest_Ground": (forest, 2048),
    "TEX_Snow_Ground": (snow, 2048),
    "TEX_Sand_Ground": (sand, 2048),
    "TEX_Thunder_Ground": (thunder, 1024),
    "TEX_Swamp_Ground": (swamp, 1024),
    "TEX_Shadow_Ground": (shadow, 1024),
    "TEX_Volcano_Ground": (volcano, 1024),
    "TEX_Storm_Ground": (storm, 1024),
    "TEX_Crystal_Ground": (crystal, 1024),
    "TEX_Royal_Ground": (royal, 1024),
}



# ---------------------------------------------------------------- prop surfaces (opaque, UV-mapped in Blender)

P = 1024


def bark_hd():
    ridged = fbm(P, 2001, freq=4, octaves=5, stretch=(6, 1), warp=0.04, ridged=True)
    plates = fbm(P, 2002, freq=6, octaves=4, stretch=(3, 1))
    fine = fbm(P, 2003, freq=48, octaves=3)
    height = ridged * 0.6 + plates * 0.25 + fine * 0.15
    albedo = ramp(height, [(0.0, (0.08, 0.06, 0.04)), (0.35, (0.20, 0.15, 0.10)), (0.7, (0.33, 0.26, 0.19)), (1.0, (0.46, 0.39, 0.31))])
    lichen = smoothstep(0.66, 0.78, fbm(P, 2004, freq=6, octaves=5)) * smoothstep(0.4, 0.7, height)
    albedo = mix(albedo, np.array((0.47, 0.52, 0.40), F) * (0.85 + 0.3 * fine[..., None]), lichen * 0.6)
    moss = smoothstep(0.62, 0.74, fbm(P, 2005, freq=4, octaves=5)) * smoothstep(0.55, 0.2, height)
    albedo = mix(albedo, np.array((0.16, 0.26, 0.07), F), moss * 0.7)
    smooth = 0.14 + fine * 0.08 + moss * 0.05
    surf = Surface(albedo, height, smooth, normal_strength=4.0)
    surf.albedo = shade_ao(surf.albedo, surf.ao, 0.5)
    return surf


def rock_hd():
    base = fbm(P, 2101, freq=3, octaves=7, warp=0.15)
    fine = fbm(P, 2102, freq=32, octaves=4)
    rng = np.random.default_rng(2103)
    speck = blur(rng.random((P, P)).astype(F), 0.7)
    albedo = ramp(base * 0.7 + fine * 0.3, [(0.0, (0.20, 0.20, 0.21)), (0.5, (0.38, 0.37, 0.36)), (1.0, (0.55, 0.53, 0.50))])
    albedo = mix(albedo, albedo * np.array((1.08, 1.0, 0.9), F), smoothstep(0.5, 0.8, fbm(P, 2104, freq=2, octaves=4)))
    albedo = np.clip(albedo + ((speck - 0.5) * 0.18)[..., None], 0, 1)
    f1, f2, cid = worley(P, 2105, 6)
    crack = smoothstep(0.02, 0.006, f2 - f1) * smoothstep(0.64, 0.78, fbm(P, 2106, freq=4, octaves=4))
    albedo = mix(albedo, albedo * 0.4, crack)
    # Lichen as irregular crusty patches (fbm), two colours, never as dots.
    patches = fbm(P, 2107, freq=7, octaves=6, warp=0.2)
    crust = fbm(P, 2108, freq=40, octaves=3)
    lichen = smoothstep(0.7, 0.78, patches) * smoothstep(0.3, 0.6, crust)
    tone = smoothstep(0.4, 0.6, fbm(P, 2109, freq=3, octaves=3))
    lichen_col = mix(np.array((0.66, 0.60, 0.40), F) * np.ones((P, P, 1), F), np.array((0.55, 0.60, 0.48), F), tone)
    albedo = mix(albedo, lichen_col, lichen * 0.7)
    height = base * 0.55 + fine * 0.25 - crack * 0.4 + lichen * 0.05
    smooth = np.clip(0.22 + fine * 0.1 - lichen * 0.1, 0, 1)
    surf = Surface(albedo, height, smooth, normal_strength=4.0)
    surf.albedo = shade_ao(surf.albedo, surf.ao, 0.45)
    return surf


def stone_ruin():
    base = fbm(P, 2201, freq=4, octaves=6, warp=0.1)
    fine = fbm(P, 2202, freq=40, octaves=4)
    streaks = fbm(P, 2203, freq=3, octaves=4, stretch=(8, 1))
    p1, _p2, pid = worley(P, 2204, 70)
    pits = smoothstep(0.24, 0.14, p1) * (pid > 0.9)
    albedo = ramp(base * 0.6 + fine * 0.4, [(0.0, (0.42, 0.39, 0.34)), (0.5, (0.60, 0.57, 0.50)), (1.0, (0.74, 0.71, 0.64))])
    albedo = mix(albedo, albedo * 0.72, smoothstep(0.55, 0.85, streaks) * 0.6)
    albedo = mix(albedo, albedo * 0.45, pits)
    f1, f2, _ = worley(P, 2205, 4)
    crack = smoothstep(0.022, 0.006, f2 - f1) * smoothstep(0.62, 0.76, fbm(P, 2206, freq=4, octaves=4))
    albedo = mix(albedo, np.array((0.12, 0.11, 0.10), F), crack)
    moss = smoothstep(0.6, 0.75, fbm(P, 2207, freq=6, octaves=5)) * (0.5 + pits * 0.5)
    albedo = mix(albedo, np.array((0.20, 0.30, 0.10), F), moss * 0.6)
    height = base * 0.4 + fine * 0.3 - pits * 0.3 - crack * 0.5
    smooth = np.clip(0.2 + fine * 0.1, 0, 1)
    surf = Surface(albedo, height, smooth, normal_strength=3.6)
    surf.albedo = shade_ao(surf.albedo, surf.ao, 0.45)
    return surf


def marble_hd():
    veins = smoothstep(0.8, 0.95, fbm(P, 2301, freq=3, octaves=6, warp=0.45, ridged=True))
    cloud = fbm(P, 2302, freq=5, octaves=5)
    albedo = ramp(cloud, [(0.0, (0.78, 0.75, 0.70)), (1.0, (0.95, 0.93, 0.89))])
    albedo = mix(albedo, np.array((0.42, 0.40, 0.40), F), veins * 0.6)
    gold_vein = smoothstep(0.93, 0.99, fbm(P, 2303, freq=3, octaves=5, warp=0.5, ridged=True))
    albedo = mix(albedo, np.array((0.85, 0.66, 0.30), F), gold_vein * 0.5)
    dirt = smoothstep(0.6, 0.9, fbm(P, 2304, freq=3, octaves=5, stretch=(4, 1)))
    albedo = mix(albedo, albedo * np.array((0.8, 0.76, 0.68), F), dirt * 0.5)
    height = cloud * 0.2 + fbm(P, 2305, freq=48, octaves=3) * 0.05
    smooth = np.clip(0.72 - dirt * 0.3, 0, 1)
    metal = gold_vein * 0.6
    return Surface(albedo, height, smooth, metal=metal, normal_strength=1.2)


def obsidian():
    base = fbm(P, 2401, freq=3, octaves=6, warp=0.2)
    f1, f2, _ = worley(P, 2402, 8)
    ridge = smoothstep(0.08, 0.0, f2 - f1)
    albedo = ramp(base, [(0.0, (0.02, 0.015, 0.03)), (1.0, (0.10, 0.07, 0.14))])
    albedo = mix(albedo, np.array((0.25, 0.16, 0.38), F), ridge * 0.35)
    height = base * 0.3 + (1 - f1) * 0.4 + ridge * 0.2
    smooth = 0.86 - ridge * 0.2
    return Surface(albedo, height, smooth, normal_strength=2.4)


def cactus_skin():
    u, v = pbr.grid(P)
    ribs = 0.5 + 0.5 * np.cos(u * 12 * math.tau)
    fine = fbm(P, 2501, freq=24, octaves=4, stretch=(1, 3))
    albedo = ramp(ribs * 0.6 + fine * 0.4, [(0.0, (0.10, 0.20, 0.08)), (0.5, (0.20, 0.36, 0.14)), (1.0, (0.33, 0.48, 0.22))])
    layer = Layer(P)
    rng = np.random.default_rng(2502)
    # Areoles and spines stay muted tan, soft and sparse: bright one-pixel spines shimmered into white
    # sparkles (and bloom) whenever the camera moved.
    for col in range(12):
        x = (col + 0.0) / 12 * P
        for row in range(20):
            y = (row + rng.uniform(-0.2, 0.2)) / 20 * P
            layer.ellipse(x, y, 5, 5, (120, 112, 84, 255), 200)
            for k in range(3):
                a = rng.uniform(0, math.tau)
                ln = rng.uniform(6, 11)
                layer.line((x, y), (x + math.cos(a) * ln, y + math.sin(a) * ln), (140, 130, 100, 150), 160, 2.0, 20)
    sp_rgb, sp_a, sp_h = layer.result()
    sp_a = blur(sp_a, 1.2)
    albedo = mix(albedo, sp_rgb, sp_a * 0.7)
    height = ribs * 0.6 + fine * 0.15 + sp_a * sp_h * 0.08
    smooth = 0.18 + ribs * 0.06
    surf = Surface(albedo, height, smooth, normal_strength=1.8)
    surf.albedo = shade_ao(surf.albedo, surf.ao, 0.4)
    return surf


def mushroom_cap():
    base = fbm(P, 2601, freq=3, octaves=5)
    albedo = ramp(base, [(0.0, (0.55, 0.06, 0.04)), (0.6, (0.75, 0.12, 0.06)), (1.0, (0.86, 0.30, 0.10))])
    f1, _f2, cid = worley(P, 2602, 12, jitter=0.8)
    wart = smoothstep(0.26, 0.16, f1) * (cid > 0.35)
    albedo = mix(albedo, np.array((0.94, 0.91, 0.82), F), wart)
    height = base * 0.2 + wart * 0.6
    smooth = 0.55 - wart * 0.3
    return Surface(albedo, height, smooth, normal_strength=2.2)


def ice_hd():
    cloud = fbm(P, 2701, freq=3, octaves=6, warp=0.2)
    f1, f2, _ = worley(P, 2702, 5)
    crack = smoothstep(0.03, 0.005, f2 - f1)
    albedo = ramp(cloud, [(0.0, (0.55, 0.72, 0.86)), (1.0, (0.86, 0.94, 1.0))])
    albedo = mix(albedo, np.array((0.97, 0.99, 1.0), F), crack * 0.7)
    height = cloud * 0.3 + crack * 0.2
    smooth = 0.9 - crack * 0.3
    return Surface(albedo, height, smooth, normal_strength=1.4)


def cover(name):
    """Small tileable textures the prop shader lays over upward faces (moss, snow, sand, ash)."""
    n = 512
    a = fbm(n, 2800, freq=6, octaves=5)
    b = fbm(n, 2801, freq=32, octaves=3)
    t = a * 0.6 + b * 0.4
    stops = {
        "Moss": [(0.0, (0.55, 0.62, 0.40)), (1.0, (0.95, 1.0, 0.70))],
        "Snow": [(0.0, (0.86, 0.90, 0.96)), (1.0, (1.0, 1.0, 1.0))],
        "Sand": [(0.0, (0.80, 0.68, 0.48)), (1.0, (1.0, 0.92, 0.72))],
        "Ash": [(0.0, (0.55, 0.53, 0.52)), (1.0, (0.85, 0.83, 0.80))],
    }[name]
    return Surface(ramp(t, stops), t, np.full((n, n), 0.2, F), normal_strength=1.0)


# ---------------------------------------------------------------- alpha atlases (foliage)


class Sprite:
    """Supersampled RGB-on-black + alpha canvas for cut-out foliage cells."""

    def __init__(self, w, h, ss=3):
        self.w, self.h, self.ss = w, h, ss
        self.rgb = Image.new("RGB", (w * ss, h * ss), (0, 0, 0))
        self.a = Image.new("L", (w * ss, h * ss), 0)
        self.hgt = Image.new("L", (w * ss, h * ss), 0)
        self.dc = ImageDraw.Draw(self.rgb)
        self.da = ImageDraw.Draw(self.a)
        self.dh = ImageDraw.Draw(self.hgt)

    def poly(self, pts, colour, height=200):
        s = self.ss
        pts = [(x * s, y * s) for x, y in pts]
        self.dc.polygon(pts, fill=tuple(int(c * 255) for c in colour))
        self.da.polygon(pts, fill=255)
        self.dh.polygon(pts, fill=int(height))

    def line(self, p0, p1, colour, width, height=220):
        s = self.ss
        self.dc.line([(p0[0] * s, p0[1] * s), (p1[0] * s, p1[1] * s)], fill=tuple(int(c * 255) for c in colour), width=max(1, int(width * s)))
        self.da.line([(p0[0] * s, p0[1] * s), (p1[0] * s, p1[1] * s)], fill=255, width=max(1, int(width * s)))
        self.dh.line([(p0[0] * s, p0[1] * s), (p1[0] * s, p1[1] * s)], fill=int(height), width=max(1, int(width * s)))

    def result(self):
        size = (self.w, self.h)
        rgb = np.asarray(self.rgb.resize(size, Image.LANCZOS), dtype=F) / 255.0
        a = np.asarray(self.a.resize(size, Image.LANCZOS), dtype=F) / 255.0
        h = np.asarray(self.hgt.resize(size, Image.LANCZOS), dtype=F) / 255.0
        # Shapes were drawn over black with an opaque fill, so the downsampled RGB is premultiplied: undo it.
        rgb = rgb / np.maximum(a, 1e-3)[..., None]
        return np.clip(rgb, 0, 1), a, h


def bleed(rgb, alpha, sigma=6):
    """Fill transparent texels with nearby leaf colour so mips and alpha-clip edges stay clean."""
    acc = ndi.gaussian_filter(rgb * alpha[..., None], (sigma, sigma, 0))
    wsum = ndi.gaussian_filter(alpha, sigma)
    fill = acc / np.maximum(wsum, 1e-4)[..., None]
    return np.where((alpha < 0.5)[..., None], np.where((wsum > 1e-4)[..., None], fill, rgb), rgb).astype(F)


def leaf_cluster(sprite, ox, oy, size, rng, palette, count, leaf_len, leaf_w, stem=(0.22, 0.16, 0.08)):
    cx, cy = ox + size * 0.5, oy + size * 0.95
    tip = (ox + size * 0.5, oy + size * 0.12)
    sprite.line((cx, cy), tip, stem, size * 0.012)
    for k in range(count):
        t = rng.uniform(0.15, 1.0)
        bx = cx + (tip[0] - cx) * t
        by = cy + (tip[1] - cy) * t
        side = -1 if k % 2 else 1
        a = -math.pi / 2 + side * rng.uniform(0.35, 1.25) + rng.uniform(-0.15, 0.15)
        ln = leaf_len * rng.uniform(0.75, 1.15) * (1.15 - 0.35 * t)
        lx = bx + math.cos(a) * ln * 0.55
        ly = by + math.sin(a) * ln * 0.55
        sprite.line((bx, by), (lx, ly), stem, size * 0.006)
        col = palette[rng.integers(len(palette))]
        tone = rng.uniform(0.82, 1.15)
        col = tuple(min(1, c * tone) for c in col)
        pts = leaf_polygon(lx, ly, ln, ln * leaf_w, a, points=12)
        sprite.poly(pts, col, rng.uniform(170, 240))
        half = [(lx + (px - lx), ly + (py - ly)) for px, py in pts[: len(pts) // 2 + 1]]
        lighter = tuple(min(1, c * 1.12) for c in col)
        sprite.poly(half + [(lx, ly)], lighter, 245)
        ex = lx + math.cos(a) * ln * 0.45
        ey = ly + math.sin(a) * ln * 0.45
        sprite.line((lx - math.cos(a) * ln * 0.45, ly - math.sin(a) * ln * 0.45), (ex, ey), tuple(c * 0.75 for c in col), size * 0.004, 255)


def needle_branch(sprite, ox, oy, size, rng):
    base = (ox + size * 0.5, oy + size * 0.95)
    tip = (ox + size * 0.5, oy + size * 0.08)
    sprite.line(base, tip, (0.25, 0.17, 0.10), size * 0.016)
    for i in range(70):
        t = i / 70
        bx = base[0] + (tip[0] - base[0]) * t
        by = base[1] + (tip[1] - base[1]) * t
        for side in (-1, 1):
            ln = size * 0.30 * (1.0 - 0.75 * t) * rng.uniform(0.8, 1.1)
            a = -math.pi / 2 + side * rng.uniform(0.9, 1.35)
            col = (rng.uniform(0.10, 0.16), rng.uniform(0.24, 0.34), rng.uniform(0.10, 0.16))
            sprite.line((bx, by), (bx + math.cos(a) * ln, by + math.sin(a) * ln + ln * 0.25), col, size * 0.006)


def fern_frond(sprite, ox, oy, size, rng):
    base = (ox + size * 0.5, oy + size * 0.97)
    pts = []
    for i in range(30):
        t = i / 29
        pts.append((ox + size * (0.5 + 0.12 * math.sin(t * 2.2)), oy + size * (0.97 - 0.88 * t)))
    for a, b in zip(pts, pts[1:]):
        sprite.line(a, b, (0.18, 0.28, 0.08), size * 0.01)
    for i, (px, py) in enumerate(pts[2:-1]):
        t = i / len(pts)
        ln = size * 0.28 * math.sin(min(1.0, t * 1.3) * math.pi) + size * 0.03
        for side in (-1, 1):
            a = -math.pi / 2 + side * 1.25
            col = (rng.uniform(0.18, 0.26), rng.uniform(0.36, 0.46), rng.uniform(0.10, 0.16))
            leaf = leaf_polygon(px + math.cos(a) * ln * 0.5, py + math.sin(a) * ln * 0.5 + ln * 0.15, ln, ln * 0.22, a + side * 0.25, points=8)
            sprite.poly(leaf, col, 220)


def leaf_spray(sprite, ox, oy, size, rng, palette, twigs, leaves, leaf_len, leaf_w, stem=(0.20, 0.14, 0.07)):
    """A twig spray: main stem from the bottom centre, side twigs, leaves along every twig (dense canopy card)."""
    base = (ox + size * 0.5, oy + size * 0.97)
    top = (ox + size * 0.5 + rng.uniform(-0.05, 0.05) * size, oy + size * 0.1)
    sprite.line(base, top, stem, size * 0.014)
    branches = [(base, top)]
    for k in range(twigs):
        t = rng.uniform(0.2, 0.85)
        sx = base[0] + (top[0] - base[0]) * t
        sy = base[1] + (top[1] - base[1]) * t
        side = -1 if k % 2 else 1
        a = -math.pi / 2 + side * rng.uniform(0.55, 1.05)
        ln = size * rng.uniform(0.28, 0.42) * (1.1 - 0.4 * t)
        end = (sx + math.cos(a) * ln, sy + math.sin(a) * ln)
        sprite.line((sx, sy), end, stem, size * 0.008)
        branches.append(((sx, sy), end))
    for (p0, p1) in branches:
        dx, dy = p1[0] - p0[0], p1[1] - p0[1]
        heading = math.atan2(dy, dx)
        for k in range(leaves):
            t = rng.uniform(0.15, 1.0)
            bx, by = p0[0] + dx * t, p0[1] + dy * t
            side = -1 if k % 2 else 1
            a = heading + side * rng.uniform(0.5, 1.2)
            if t > 0.92:
                a = heading + rng.uniform(-0.25, 0.25)
            ln = leaf_len * rng.uniform(0.7, 1.15)
            lx, ly = bx + math.cos(a) * ln * 0.5, by + math.sin(a) * ln * 0.5
            col = palette[rng.integers(len(palette))]
            tone = rng.uniform(0.78, 1.18)
            col = tuple(min(1, c * tone) for c in col)
            pts = leaf_polygon(lx, ly, ln, ln * leaf_w, a, points=10)
            sprite.poly(pts, col, rng.uniform(170, 240))
            sprite.poly(pts[: len(pts) // 2 + 1] + [(lx, ly)], tuple(min(1, c * 1.12) for c in col), 245)
            sprite.line((lx - math.cos(a) * ln * 0.42, ly - math.sin(a) * ln * 0.42),
                        (lx + math.cos(a) * ln * 0.42, ly + math.sin(a) * ln * 0.42), tuple(c * 0.72 for c in col), size * 0.0035, 255)


def leaf_atlas():
    n = 1024
    c = n // 2
    sprite = Sprite(n, n)
    rng = np.random.default_rng(3001)
    broad = [(0.16, 0.30, 0.08), (0.22, 0.38, 0.10), (0.30, 0.44, 0.13), (0.26, 0.34, 0.09)]
    small = [(0.14, 0.27, 0.07), (0.20, 0.34, 0.09), (0.27, 0.40, 0.12)]
    leaf_spray(sprite, 0, 0, c, rng, broad, twigs=7, leaves=9, leaf_len=c * 0.16, leaf_w=0.5)
    leaf_spray(sprite, c, 0, c, rng, small, twigs=9, leaves=14, leaf_len=c * 0.09, leaf_w=0.58)
    needle_branch(sprite, 0, c, c, rng)
    fern_frond(sprite, c, c, c, rng)
    rgb, a, h = sprite.result()
    rgb = bleed(rgb, a)
    return rgb, a, h


def grass_clump(sprite, ox, oy, w, h, rng, count, palette, tip_colour, width=(0.012, 0.022), heads=None):
    for _ in range(count):
        bx = ox + w * (0.5 + rng.normal(0, 0.16))
        by = oy + h * 0.995
        height = h * rng.uniform(0.55, 0.96)
        lean = rng.normal(0, 0.18) * w
        bw = w * rng.uniform(*width)
        base = palette[rng.integers(len(palette))]
        segments = 6
        prev_l = (bx - bw, by)
        prev_r = (bx + bw, by)
        for sgm in range(1, segments + 1):
            t = sgm / segments
            x = bx + lean * t * t
            y = by - height * t
            ww = bw * (1.0 - t) + 0.4
            col = tuple(b + (tc - b) * t for b, tc in zip(base, tip_colour))
            shade = 0.55 + 0.45 * t
            col = tuple(min(1, ch * shade * rng.uniform(0.92, 1.08)) for ch in col)
            l = (x - ww, y)
            r = (x + ww, y)
            sprite.poly([prev_l, prev_r, r, l], col, 120 + 130 * t)
            prev_l, prev_r = l, r
        if heads and rng.random() < heads[0]:
            hx, hy = prev_l[0], prev_l[1]
            colour = heads[1][rng.integers(len(heads[1]))]
            for k in range(6):
                a = k / 6 * math.tau
                sprite.poly(leaf_polygon(hx + math.cos(a) * w * 0.02, hy + math.sin(a) * w * 0.02, w * 0.05, w * 0.03, a, 6), colour, 255)
            sprite.poly(leaf_polygon(hx, hy, w * 0.025, w * 0.025, 0, 6), (0.95, 0.80, 0.25), 255)


def grass_atlas():
    n = 1024
    c = n // 2
    sprite = Sprite(n, n)
    rng = np.random.default_rng(3101)
    green = [(0.10, 0.20, 0.05), (0.14, 0.26, 0.06), (0.18, 0.30, 0.08)]
    grass_clump(sprite, 0, 0, c, c, rng, 70, green, (0.42, 0.56, 0.18))
    dry = [(0.30, 0.26, 0.12), (0.38, 0.32, 0.16), (0.26, 0.24, 0.12)]
    grass_clump(sprite, c, 0, c, c, rng, 60, dry, (0.74, 0.64, 0.38))
    reeds = [(0.16, 0.22, 0.08), (0.22, 0.28, 0.10)]
    grass_clump(sprite, 0, c, c, c, rng, 26, reeds, (0.48, 0.52, 0.24), width=(0.01, 0.016))
    for _ in range(6):
        x = c * rng.uniform(0.3, 0.7)
        y = c + c * rng.uniform(0.1, 0.3)
        sprite.poly([(x - 7, y), (x + 7, y), (x + 7, y + 46), (x - 7, y + 46)], (0.30, 0.18, 0.09), 255)
    grass_clump(sprite, c, c, c, c, rng, 55, green, (0.40, 0.55, 0.18),
                heads=(0.35, [(0.95, 0.95, 0.92), (0.95, 0.80, 0.25), (0.65, 0.45, 0.85), (0.92, 0.45, 0.55)]))
    rgb, a, h = sprite.result()
    rgb = bleed(rgb, a)
    return rgb, a, h


def save_atlas(out, name, rgb, a, h, smooth=0.32):
    os.makedirs(out, exist_ok=True)
    Image.fromarray((np.dstack((rgb, a)) * 255 + 0.5).astype(np.uint8), "RGBA").save(os.path.join(out, name + ".png"), optimize=True)
    nrm = pbr.normal_from_height(blur(h, 1.5), 2.0)
    Image.fromarray((nrm * 255 + 0.5).astype(np.uint8), "RGB").save(os.path.join(out, name + "_Normal.png"), optimize=True)
    ao = np.clip(0.55 + 0.45 * h, 0, 1)
    mask = np.dstack((ao, np.full_like(h, smooth), np.zeros_like(h)))
    Image.fromarray((mask * 255 + 0.5).astype(np.uint8), "RGB").save(os.path.join(out, name + "_Mask.png"), optimize=True)


# ---------------------------------------------------------------- dim sum steamer set


def _streaks(n, seed, along_x, sigma=1.4, slow=6):
    """Fibre streaks: random per-line values blurred across the strip, drifting slowly along it. Values in [0, 1]."""
    rng = np.random.default_rng(seed)
    lines = ndi.gaussian_filter1d(rng.random(n).astype(F), sigma, mode="wrap")
    lines = (lines - lines.min()) / max(float(lines.max() - lines.min()), 1e-6)
    drift = fbm(n, seed + 7, freq=slow, octaves=3)
    # along_x False: streaks run along y (the value depends on x); True: they run along x (depends on y).
    field = lines[:, None] if along_x else lines[None, :]
    field = np.broadcast_to(field, (n, n)).astype(F)
    return np.clip(field * 0.8 + (drift - 0.5) * 0.35 + 0.1, 0, 1)


def bamboo_weave():
    """Twill-woven bamboo mat (the steamer floor): 16 strips per tile, 2-over-2-under, glossy varnish, gaps in shadow."""
    n = N
    strips = 16
    w = n // strips
    y, x = np.mgrid[0:n, 0:n]
    i = x // w
    j = y // w
    lx = ((x % w) + 0.5) / w
    ly = ((y % w) + 0.5) / w
    phase = (i + j) % 4
    vert_over = phase < 2
    across = np.where(vert_over, lx, ly)
    along = np.where(vert_over, ly, lx)
    run_t = (np.where(vert_over, phase, phase - 2) + along) / 2.0
    # Strip profile: rounded across the width, dipping where it goes under the crossing strip.
    profile = np.sin(np.pi * np.clip(across, 0, 1)) ** 0.55
    lift = 0.55 + 0.45 * np.sin(np.pi * np.clip(run_t, 0, 1)) ** 0.6
    height = profile * lift
    gap = smoothstep(0.07, 0.0, np.minimum(across, 1 - across))
    height = height * (1 - 0.7 * gap)
    # Fibres follow each visible strip's own direction.
    fib_v = _streaks(n, 6101, along_x=False)
    fib_h = _streaks(n, 6102, along_x=True)
    fibres = np.where(vert_over, fib_v, fib_h)
    rng = np.random.default_rng(6103)
    tone_v = rng.uniform(0.9, 1.08, strips)
    tone_h = rng.uniform(0.9, 1.08, strips)
    tone = np.where(vert_over, tone_v[np.clip(i, 0, strips - 1)], tone_h[np.clip(j, 0, strips - 1)])
    macro = fbm(n, 6104, freq=3, octaves=4)
    base = ramp(np.clip(fibres * 0.7 + macro * 0.3, 0, 1), [(0.0, (0.64, 0.48, 0.26)), (0.5, (0.79, 0.63, 0.38)), (1.0, (0.90, 0.78, 0.54))])
    albedo = base * tone[..., None] * (0.97 + 0.06 * macro[..., None])
    dive = 1.0 - 0.22 * (1.0 - np.sin(np.pi * np.clip(run_t, 0, 1)) ** 0.6)
    albedo = albedo * dive[..., None] * (1.0 - 0.62 * gap)[..., None]
    smooth = 0.38 + 0.18 * (1 - fibres) * (1 - gap)
    height = np.clip(height + (fibres - 0.5) * 0.04, 0, 1)
    surf = Surface(albedo, height, smooth, normal_strength=5.0)
    surf.albedo = shade_ao(surf.albedo, surf.ao, 0.55)
    return surf


def steamer_wood():
    """Pale steamer-wall wood: long straight grain, soft growth rings, lightly varnished."""
    n = P
    u, v = pbr.grid(n)
    warp = fbm(n, 6201, freq=3, octaves=4)
    rings = (v * 9.0 + warp * 2.2) % 1.0
    rings = np.abs(rings * 2 - 1)
    pores = _streaks(n, 6202, along_x=True, sigma=0.9, slow=10)
    fine = fbm(n, 6203, freq=14, octaves=3, stretch=(4, 1))
    t = np.clip(rings * 0.45 + pores * 0.3 + fine * 0.25, 0, 1)
    albedo = ramp(t, [(0.0, (0.80, 0.62, 0.38)), (0.5, (0.90, 0.76, 0.52)), (1.0, (0.97, 0.88, 0.68))])
    height = pores * 0.5 + fine * 0.5
    smooth = 0.42 + pores * 0.12
    surf = Surface(albedo, height, smooth, normal_strength=1.4)
    surf.albedo = shade_ao(surf.albedo, surf.ao, 0.2)
    return surf


def glitter_table():
    """Neutral sparkly base (tinted per flavour in Unity) + multi-colour glitter flecks in the emission map."""
    n = P
    rng = np.random.default_rng(6301)
    base = fbm(n, 6302, freq=5, octaves=5)
    albedo = np.dstack([np.full((n, n), 0.62, F)] * 3) * (0.82 + 0.3 * base[..., None])
    palette = np.array([(1.0, 0.92, 0.7), (1.0, 0.45, 0.62), (0.4, 0.9, 1.0), (0.75, 0.55, 1.0), (0.55, 1.0, 0.6), (1.0, 0.8, 0.3), (1.0, 1.0, 1.0)], F)
    emit = np.zeros((n, n, 3), F)
    for count, gain in ((26000, 0.45), (3000, 0.7)):
        xs = rng.integers(0, n, count)
        ys = rng.integers(0, n, count)
        colours = palette[rng.integers(0, len(palette), count)] * rng.uniform(gain, 1.0, count)[:, None].astype(F)
        np.add.at(emit, (ys, xs), colours)
    for c in range(3):
        emit[..., c] = ndi.gaussian_filter(emit[..., c], 0.75, mode="wrap")
    emit = np.clip(emit * 3.2, 0, 1.0)
    fleck = np.clip(emit.max(axis=2) * 2.0, 0, 1)
    height = fbm(n, 6303, freq=60, octaves=3) * 0.4 + fleck * 0.6
    smooth = np.clip(0.3 + fleck * 0.65, 0, 1)
    metal = np.clip(fleck * 1.2, 0, 1)
    return Surface(albedo, height, smooth, metal=metal, emit=emit, normal_strength=3.0)


GROUNDS.update({
    "TEX_Bamboo_Weave": (bamboo_weave, 2048),
    "TEX_Glitter_Table": (glitter_table, 1024),
})

PROPS = {
    "TEX_Steamer_Wood": (steamer_wood, 1024),
    "TEX_Bark_HD": (bark_hd, 1024),
    "TEX_Rock_HD": (rock_hd, 1024),
    "TEX_Stone_Ruin": (stone_ruin, 1024),
    "TEX_Marble_HD": (marble_hd, 1024),
    "TEX_Obsidian": (obsidian, 512),
    "TEX_Cactus_Skin": (cactus_skin, 512),
    "TEX_Mushroom_Cap": (mushroom_cap, 512),
    "TEX_Ice_HD": (ice_hd, 512),
    "TEX_Cover_Moss": (lambda: cover("Moss"), 512),
    "TEX_Cover_Snow": (lambda: cover("Snow"), 512),
    "TEX_Cover_Sand": (lambda: cover("Sand"), 512),
    "TEX_Cover_Ash": (lambda: cover("Ash"), 512),
}

def bamboo_leaf_atlas():
    """One sprig of long lanceolate bamboo leaves on a thin twig (single cell, base at the bottom centre)."""
    n = 512
    sprite = Sprite(n, n)
    rng = np.random.default_rng(3201)
    greens = [(0.26, 0.50, 0.14), (0.32, 0.58, 0.18), (0.38, 0.64, 0.22), (0.30, 0.54, 0.16)]
    x0, y0, y1 = n * 0.5, n * 0.98, n * 0.2
    sprite.line((x0, y0), (x0, y1), (0.35, 0.42, 0.16), 4)
    for k in range(8):
        t = 0.1 + k * 0.105
        px, py = x0, y0 + (y1 - y0) * t
        for side in (-1, 1):
            length = rng.uniform(150, 215) * (1.0 - 0.18 * t)
            angle = -math.pi / 2 + side * rng.uniform(0.5, 0.9)
            dx, dy = math.cos(angle), math.sin(angle)
            c = greens[rng.integers(len(greens))]
            pts = leaf_polygon(px + dx * length * 0.5, py + dy * length * 0.5, length, length * 0.19, angle)
            sprite.poly(pts, c, 190)
            sprite.line((px, py), (px + dx * length * 0.92, py + dy * length * 0.92), tuple(min(1, ch * 1.25) for ch in c), 2, 230)
    pts = leaf_polygon(x0, y1 - 70, 190, 34, -math.pi / 2)
    sprite.poly(pts, greens[1], 200)
    rgb, a, h = sprite.result()
    return bleed(rgb, a), a, h


ATLASES = {
    "TEX_Leaf_Atlas": leaf_atlas,
    "TEX_Grass_Atlas": grass_atlas,
    "TEX_Bamboo_Leaf_Atlas": bamboo_leaf_atlas,
}


def fx_textures(out):
    """Particle sprites (RGBA, white with alpha) for weather and ground mist."""
    def save(name, alpha, size):
        a = np.clip(alpha, 0, 1)
        rgba = np.dstack((np.ones_like(a), np.ones_like(a), np.ones_like(a), a))
        Image.fromarray((rgba * 255 + 0.5).astype(np.uint8), "RGBA").resize(size, Image.LANCZOS).save(os.path.join(out, name + ".png"))

    n = 256
    y, x = np.mgrid[0:n, 0:n].astype(F)
    u = (x + 0.5) / n * 2 - 1
    v = (y + 0.5) / n * 2 - 1
    r = np.sqrt(u * u + v * v)
    # Rain: thin bright core fading at both ends (stretched along velocity by the renderer).
    streak = np.exp(-(u / 0.12) ** 2) * np.clip(1 - np.abs(v), 0, 1) ** 0.7
    save("TEX_FX_Rain", streak, (64, 256))
    # Snowflake: soft disc with a faint six-armed crystal.
    ang = np.arctan2(v, u)
    arms = np.clip(np.cos(ang * 6) * 0.5 + 0.5, 0, 1) ** 6 * np.clip(1 - r / 0.9, 0, 1)
    flake = np.clip(np.exp(-(r / 0.35) ** 2) + arms * 0.6, 0, 1)
    save("TEX_FX_Snow", flake, (64, 64))
    # Splash ring: thin ring with a soft inner fill.
    ring = np.exp(-((r - 0.7) / 0.08) ** 2) + np.exp(-(r / 0.3) ** 2) * 0.15
    save("TEX_FX_Ring", ring, (128, 128))
    # Mist / smoke puff: noisy soft blob.
    puff_noise = fbm(n, 4001, freq=4, octaves=5)
    puff = np.clip(1 - r, 0, 1) ** 1.5 * (0.45 + 0.75 * puff_noise)
    save("TEX_FX_Mist", puff, (128, 128))
    # Glow: smooth gaussian (embers, fireflies, spores).
    save("TEX_FX_Glow", np.exp(-(r / 0.42) ** 2), (64, 64))
    # Bokeh: out-of-focus lamp disc, flat inside with a brighter rim and a soft edge.
    disc = np.clip((0.94 - r) / 0.1, 0, 1)
    rim = np.exp(-((r - 0.84) / 0.07) ** 2)
    save("TEX_FX_Bokeh", disc * (0.42 + 0.58 * rim), (128, 128))
    # Sparkle: four-point glint (soft core + thin cross) for floating glitter.
    cross = np.exp(-(np.abs(u) / 0.035) ** 2) * np.clip(1 - np.abs(v), 0, 1) ** 2 + np.exp(-(np.abs(v) / 0.035) ** 2) * np.clip(1 - np.abs(u), 0, 1) ** 2
    save("TEX_FX_Sparkle", np.clip(cross + np.exp(-(r / 0.16) ** 2), 0, 1), (64, 64))


def macro_noise(out):
    """Small tileable noise the ground shader samples in world space to hide tiling (R, G, B = three scales)."""
    n = 256
    rgb = np.dstack((fbm(n, 1, freq=2, octaves=4), fbm(n, 2, freq=4, octaves=4), fbm(n, 3, freq=8, octaves=3)))
    Image.fromarray((rgb * 255).astype(np.uint8), "RGB").save(os.path.join(out, "TEX_Noise_Macro.png"))


def lit_preview(surface, size=512):
    """Quick sun-lit look (albedo × lambert from the normal map) for contact sheets."""
    h = surface.height
    nrm = pbr.normal_from_height(h, surface.normal_strength) * 2 - 1
    sun = np.array((-0.45, 0.55, 0.7), F)
    sun /= np.linalg.norm(sun)
    lambert = np.clip((nrm * sun).sum(axis=2), 0, 1)
    img = surface.albedo * (0.35 + 0.75 * lambert)[..., None] * (0.6 + 0.4 * surface.ao)[..., None]
    if surface.emit is not None:
        img = img + surface.emit * 0.8
    tile = np.clip(img, 0, 1)
    tile = np.tile(tile, (2, 2, 1))
    return Image.fromarray((tile * 255).astype(np.uint8), "RGB").resize((size, size), Image.LANCZOS)


def main():
    out = sys.argv[1] if len(sys.argv) > 1 else "asset_work/textures"
    only = set(sys.argv[2:])
    preview_dir = os.path.join(os.path.dirname(out.rstrip("/")) or ".", "previews", "textures")
    os.makedirs(preview_dir, exist_ok=True)
    macro_noise(out)
    fx_textures(out)
    for name, (builder, size) in list(GROUNDS.items()) + list(PROPS.items()):
        if only and name not in only:
            continue
        start = time.time()
        surface = builder()
        pbr.save_surface(surface, out, name, size=size)
        lit_preview(surface).save(os.path.join(preview_dir, name + "_lit.png"))
        print(f"PBR {name} {size}px {time.time() - start:.1f}s seam={pbr.seam_error(surface.height):.2f}", flush=True)
    for name, builder in ATLASES.items():
        if only and name not in only:
            continue
        start = time.time()
        rgb, a, h = builder()
        save_atlas(out, name, rgb, a, h)
        preview = Image.fromarray((np.dstack((rgb * a[..., None] + (1 - a[..., None]) * 0.5, )) * 255).astype(np.uint8), "RGB")
        preview.resize((512, 512), Image.LANCZOS).save(os.path.join(preview_dir, name + "_lit.png"))
        print(f"ATLAS {name} {time.time() - start:.1f}s coverage={a.mean():.2f}", flush=True)
    print("PBR_TEXTURES_OK", out, flush=True)


if __name__ == "__main__":
    main()
