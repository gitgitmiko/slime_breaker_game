"""Tileable PBR texture synthesis for Slime Breaker (numpy + scipy + Pillow, CPU only).

Every generator returns a `Surface` (float32 arrays, all tileable). `save_surface` writes the Unity texture set:

  NAME.png          RGB albedo (sRGB) + A = height
  NAME_Normal.png   tangent-space normal map (OpenGL / Unity convention, Y up)
  NAME_Mask.png     R = ambient occlusion, G = smoothness, B = metallic
  NAME_Emit.png     optional emission colour (only when the surface glows)

Authoring is in display (sRGB) colours; Unity imports albedo as sRGB and everything else as linear.
"""

import math
import os

import numpy as np
from PIL import Image, ImageDraw, ImageFilter
from scipy import ndimage as ndi

F = np.float32


# ---------------------------------------------------------------- coordinates and noise


def grid(n):
    """(u, v) pixel-centre coordinates in [0, 1)."""
    c = (np.arange(n, dtype=F) + 0.5) / n
    u, v = np.meshgrid(c, c)
    return u, v


def _fade(t):
    return t * t * t * (t * (t * 6 - 15) + 10)


def perlin(u, v, period, seed):
    """Periodic gradient noise in roughly [-1, 1]. u, v are in lattice units; `period` is an int or (px, py)."""
    px, py = (period, period) if np.isscalar(period) else period
    rng = np.random.default_rng(seed)
    angle = rng.random((py, px)) * (2 * math.pi)
    gx = np.cos(angle).astype(F)
    gy = np.sin(angle).astype(F)
    x0 = np.floor(u).astype(np.int32)
    y0 = np.floor(v).astype(np.int32)
    fx = (u - x0).astype(F)
    fy = (v - y0).astype(F)
    xi = x0 % px
    yi = y0 % py
    xj = (xi + 1) % px
    yj = (yi + 1) % py
    n00 = gx[yi, xi] * fx + gy[yi, xi] * fy
    n10 = gx[yi, xj] * (fx - 1) + gy[yi, xj] * fy
    n01 = gx[yj, xi] * fx + gy[yj, xi] * (fy - 1)
    n11 = gx[yj, xj] * (fx - 1) + gy[yj, xj] * (fy - 1)
    sx = _fade(fx)
    sy = _fade(fy)
    top = n00 + (n10 - n00) * sx
    bottom = n01 + (n11 - n01) * sx
    return (top + (bottom - top) * sy) * F(1.41)


def norm01(a):
    lo, hi = float(a.min()), float(a.max())
    return ((a - lo) / max(hi - lo, 1e-8)).astype(F)


def fbm(n, seed, freq=4, octaves=6, gain=0.5, lacunarity=2, warp=0.0, ridged=False, stretch=(1, 1)):
    """Tileable fractal noise in [0, 1]. `warp` bends the domain with a second noise (unit-square units).

    `stretch=(sx, sy)` multiplies the frequency per axis (integers keep it tileable) for grain and streaks.
    """
    u, v = grid(n)
    if warp:
        # Warp lattice must be at least 3 cells or it degenerates into a straight ramp (visible diagonal smears).
        wf = max(3, freq)
        wu = perlin(u * wf, v * wf, wf, seed + 9001)
        wv = perlin(u * wf, v * wf, wf, seed + 9002)
        u = u + wu * warp
        v = v + wv * warp
    total = np.zeros((n, n), F)
    amp = 1.0
    norm = 0.0
    f = freq
    for octave in range(octaves):
        fx, fy = f * stretch[0], f * stretch[1]
        layer = perlin(u * fx, v * fy, (fx, fy), seed + octave * 31)
        if ridged:
            layer = 1.0 - np.abs(layer)
            layer *= layer
        total += layer * amp
        norm += amp
        amp *= gain
        f = int(f * lacunarity)
    return norm01(total / norm)


def worley(n, seed, cells, jitter=1.0):
    """Tileable voronoi. Returns (F1, F2, cell_id_value) with distances in cell units."""
    rng = np.random.default_rng(seed)
    px = rng.random((cells, cells)).astype(F)
    py = rng.random((cells, cells)).astype(F)
    ident = rng.random((cells, cells)).astype(F)
    u, v = grid(n)
    U = u * cells
    V = v * cells
    cx = np.floor(U).astype(np.int32)
    cy = np.floor(V).astype(np.int32)
    f1 = np.full((n, n), 9.0, F)
    f2 = np.full((n, n), 9.0, F)
    cid = np.zeros((n, n), F)
    for dy in (-1, 0, 1):
        for dx in (-1, 0, 1):
            ix = (cx + dx) % cells
            iy = (cy + dy) % cells
            fx = cx + dx + 0.5 + (px[iy, ix] - 0.5) * jitter
            fy = cy + dy + 0.5 + (py[iy, ix] - 0.5) * jitter
            d = np.hypot(U - fx, V - fy).astype(F)
            closer = d < f1
            f2 = np.where(closer, f1, np.minimum(f2, d))
            cid = np.where(closer, ident[iy, ix], cid)
            f1 = np.where(closer, d, f1)
    return f1, f2, cid


def blur(a, sigma):
    return ndi.gaussian_filter(a, sigma, mode="wrap")


def smoothstep(lo, hi, x):
    """Hermite step; lo > hi gives the inverted step. lo and hi may be arrays."""
    denom = np.asarray(hi - lo, dtype=F)
    denom = np.where(np.abs(denom) < 1e-8, F(1e-8), denom)
    t = np.clip((x - lo) / denom, 0.0, 1.0)
    return (t * t * (3 - 2 * t)).astype(F)


def mix(a, b, t):
    if np.ndim(t) == 2 and np.ndim(a) == 3:
        t = t[..., None]
    return a + (b - a) * t


def ramp(t, stops):
    """Gradient map. `stops` is [(position, (r, g, b)), ...] in 0..1, sorted by position."""
    t = np.clip(t, 0.0, 1.0)
    out = np.zeros(t.shape + (3,), F)
    positions = [s[0] for s in stops]
    colours = np.array([s[1] for s in stops], F)
    for c in range(3):
        out[..., c] = np.interp(t, positions, colours[:, c])
    return out


# ---------------------------------------------------------------- height derived maps


def normal_from_height(height, strength=3.0):
    """Tangent-space normal (Unity: +Y up). `strength` scales height differences per texel."""
    n = height.shape[0]
    dx = (np.roll(height, -1, axis=1) - np.roll(height, 1, axis=1)) * 0.5 * n * strength / 256.0
    dy = (np.roll(height, -1, axis=0) - np.roll(height, 1, axis=0)) * 0.5 * n * strength / 256.0
    normal = np.dstack((-dx, dy, np.ones_like(height)))
    normal /= np.linalg.norm(normal, axis=2, keepdims=True)
    return (normal * 0.5 + 0.5).astype(F)


def ao_from_height(height, radii=(2, 6, 18), strength=1.0):
    """Cheap cavity occlusion: how far below the blurred neighbourhood each texel sits."""
    ao = np.zeros_like(height)
    for i, r in enumerate(radii):
        ao += np.clip((blur(height, r) - height) * (3.0 + i * 1.5), 0.0, 1.0) * (1.0 / len(radii))
    return np.clip(1.0 - ao * strength, 0.0, 1.0).astype(F)


# ---------------------------------------------------------------- stamping (wrap-around drawing)


def _wrap_positions(x, y, size, reach):
    xs = [x]
    ys = [y]
    if x < reach:
        xs.append(x + size)
    if x > size - reach:
        xs.append(x - size)
    if y < reach:
        ys.append(y + size)
    if y > size - reach:
        ys.append(y - size)
    return [(a, b) for a in xs for b in ys]


class Layer:
    """RGBA + height canvas drawn at 2x then reduced; every stamp wraps so the result still tiles."""

    def __init__(self, n, ss=2):
        self.n = n
        self.ss = ss
        self.size = n * ss
        self.color = Image.new("RGBA", (self.size, self.size), (0, 0, 0, 0))
        self.height = Image.new("L", (self.size, self.size), 0)
        self.cd = ImageDraw.Draw(self.color)
        self.hd = ImageDraw.Draw(self.height)

    def polygon(self, pts, rgba, h, reach):
        s = self.ss
        for ox, oy in _wrap_positions(pts[0][0], pts[0][1], self.n, reach):
            shifted = [((px - pts[0][0] + ox) * s, (py - pts[0][1] + oy) * s) for px, py in pts]
            self.cd.polygon(shifted, fill=rgba)
            self.hd.polygon(shifted, fill=int(h))

    def line(self, p0, p1, rgba, h, width, reach):
        s = self.ss
        for ox, oy in _wrap_positions(p0[0], p0[1], self.n, reach):
            a = ((p0[0] - p0[0] + ox) * s, (p0[1] - p0[1] + oy) * s)
            b = ((p1[0] - p0[0] + ox) * s, (p1[1] - p0[1] + oy) * s)
            self.cd.line([a, b], fill=rgba, width=max(1, int(width * s)))
            self.hd.line([a, b], fill=int(h), width=max(1, int(width * s)))

    def ellipse(self, cx, cy, rx, ry, rgba, h):
        s = self.ss
        reach = max(rx, ry) + 2
        for ox, oy in _wrap_positions(cx, cy, self.n, reach):
            box = ((ox - rx) * s, (oy - ry) * s, (ox + rx) * s, (oy + ry) * s)
            self.cd.ellipse(box, fill=rgba)
            self.hd.ellipse(box, fill=int(h))

    def result(self):
        color = self.color.resize((self.n, self.n), Image.LANCZOS)
        height = self.height.resize((self.n, self.n), Image.LANCZOS)
        rgba = np.asarray(color, dtype=F) / 255.0
        return rgba[..., :3], rgba[..., 3], np.asarray(height, dtype=F) / 255.0


def leaf_polygon(cx, cy, length, width, angle, points=10):
    """Pointed leaf outline (a lens) rotated by `angle`."""
    out = []
    ca, sa = math.cos(angle), math.sin(angle)
    for i in range(points + 1):
        t = i / points
        x = (t - 0.5) * length
        y = math.sin(t * math.pi) ** 0.8 * width * 0.5
        out.append((x, y))
    for i in range(points, -1, -1):
        t = i / points
        x = (t - 0.5) * length
        y = -math.sin(t * math.pi) ** 0.8 * width * 0.5
        out.append((x, y))
    return [(cx + x * ca - y * sa, cy + x * sa + y * ca) for x, y in out]


# ---------------------------------------------------------------- surface container and IO


class Surface:
    def __init__(self, albedo, height, smooth, ao=None, metal=None, emit=None, normal_strength=3.0):
        self.albedo = np.clip(albedo, 0, 1).astype(F)
        self.height = norm01(height)
        self.smooth = np.clip(smooth, 0, 1).astype(F)
        self.ao = ao if ao is not None else ao_from_height(self.height)
        self.metal = metal if metal is not None else np.zeros_like(self.height)
        self.emit = emit
        self.normal_strength = normal_strength


def _u8(a):
    return (np.clip(a, 0.0, 1.0) * 255.0 + 0.5).astype(np.uint8)


def save_surface(surface, out_dir, name, size=None):
    os.makedirs(out_dir, exist_ok=True)
    h = surface.height
    albedo_rgba = np.dstack((surface.albedo, h))
    normal = normal_from_height(h, surface.normal_strength)
    mask = np.dstack((surface.ao, surface.smooth, surface.metal))

    def write(array, suffix, mode):
        image = Image.fromarray(_u8(array), mode)
        if size and image.width != size:
            image = image.resize((size, size), Image.LANCZOS)
        image.save(os.path.join(out_dir, name + suffix + ".png"), optimize=True)

    write(albedo_rgba, "", "RGBA")
    write(normal, "_Normal", "RGB")
    write(mask, "_Mask", "RGB")
    if surface.emit is not None:
        write(surface.emit, "_Emit", "RGB")


def luminance(rgb):
    return rgb[..., 0] * 0.2126 + rgb[..., 1] * 0.7152 + rgb[..., 2] * 0.0722


def seam_error(a):
    """Largest difference across the wrap seam relative to the typical neighbour difference (should be ~1)."""
    inner = np.abs(a[:, 1:] - a[:, :-1]).mean()
    seam = np.abs(a[:, 0] - a[:, -1]).mean()
    inner_v = np.abs(a[1:] - a[:-1]).mean()
    seam_v = np.abs(a[0] - a[-1]).mean()
    return max(seam / max(inner, 1e-6), seam_v / max(inner_v, 1e-6))
