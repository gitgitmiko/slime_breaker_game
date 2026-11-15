"""Modern "candy arcade" UI kit for Dumpling Witch.

Two sources, one look (chunky shapes, thick dark-violet outline, glossy faces, a darker lip under every button):

* 9-slice frames, buttons, bars, the joystick and the tintable glyphs are drawn here with Pillow, at twice the
  canvas size (import them with 200 pixels per unit) and supersampled, so their borders stay crisp on any screen.
* Illustrated art (logo, skill / upgrade / item / HUD icons, hero avatar, key art) is generated with gflow-cli
  (Google Flow; see gflow_ui_queue.py) on a green chroma background. The raw sheets are committed in
  asset_work/ui_src/raw; this script keys the background out, splits each sheet into its objects and saves them
  under the names listed in asset_work/ui_src/pick.json.

Usage:
  python3 asset_work/scripts/build_ui_modern.py            # everything
  python3 asset_work/scripts/build_ui_modern.py frames     # procedural sprites only
  python3 asset_work/scripts/build_ui_modern.py art        # cut the gflow sheets only
  python3 asset_work/scripts/build_ui_modern.py sheet      # contact sheet of the result (asset_work/previews/UI_Kit.png)
"""
import json
import math
import os
import sys

import numpy as np
from PIL import Image, ImageChops, ImageDraw, ImageFilter
from scipy import ndimage

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
UI = os.path.join(ROOT, "asset_work", "output", "UI")
PANELS = os.path.join(UI, "Panels")
ICONS = os.path.join(UI, "Icons")
ART = os.path.join(UI, "Art")
SRC = os.path.join(ROOT, "asset_work", "ui_src")
RAW = os.path.join(SRC, "raw")

OUT = 2          # output pixels per canvas unit (sprites import at 200 PPU)
SS = 3           # supersampling on top of that
K = OUT * SS     # working pixels per canvas unit


def rgba(hex_colour, alpha=255):
    hex_colour = hex_colour.lstrip("#")
    return (int(hex_colour[0:2], 16), int(hex_colour[2:4], 16), int(hex_colour[4:6], 16), alpha)


INK = rgba("#22123F")          # outline
P950 = rgba("#170D2E")
P900 = rgba("#1F1340")
P800 = rgba("#2B1B55")
P700 = rgba("#3B2773")
P600 = rgba("#5038A6")
P500 = rgba("#6B4DDC")
P400 = rgba("#8C6FF5")
P300 = rgba("#B6A0FF")
LILAC = rgba("#DCD0FF")
CREAM = rgba("#FFF4D8")
GOLD3 = rgba("#FFE680")
GOLD4 = rgba("#FFCB3D")
GOLD5 = rgba("#FFA81F")
GOLD6 = rgba("#D97A10")
GOLD7 = rgba("#A8540C")
WHITE = (255, 255, 255, 255)


def u(value):
    return int(round(value * K))


def blank(width, height):
    return Image.new("RGBA", (u(width), u(height)), (0, 0, 0, 0))


def save(image, name, folder=PANELS):
    out = image.resize((image.width // SS, image.height // SS), Image.LANCZOS)
    os.makedirs(folder, exist_ok=True)
    out.save(os.path.join(folder, name + ".png"), optimize=True)


def vgrad(size, top, bottom):
    """Vertical gradient image (working pixels)."""
    width, height = size
    t = np.linspace(0.0, 1.0, height, dtype=np.float32)[:, None, None]
    a = np.array(top, dtype=np.float32)[None, None, :]
    b = np.array(bottom, dtype=np.float32)[None, None, :]
    column = a + (b - a) * t
    return Image.fromarray(np.broadcast_to(column, (height, width, 4)).astype(np.uint8).copy(), "RGBA")


def rrect_mask(size, box, radius):
    mask = Image.new("L", size, 0)
    x0, y0, x1, y1 = box
    radius = max(0, min(radius, (x1 - x0) // 2, (y1 - y0) // 2))
    ImageDraw.Draw(mask).rounded_rectangle((x0, y0, x1 - 1, y1 - 1), radius=radius, fill=255)
    return mask


def ellipse_mask(size, box):
    mask = Image.new("L", size, 0)
    ImageDraw.Draw(mask).ellipse((box[0], box[1], box[2] - 1, box[3] - 1), fill=255)
    return mask


def fill(image, mask, top, bottom=None):
    """Paints a (gradient) colour through a mask onto image."""
    layer = vgrad(image.size, top, bottom or top)
    box = mask.getbbox()
    if box:
        # Stretch the gradient over the mask's own height so small shapes still get the full ramp.
        ramp = vgrad((image.width, max(1, box[3] - box[1])), top, bottom or top)
        layer = Image.new("RGBA", image.size, (0, 0, 0, 0))
        layer.paste(ramp, (0, box[1]))
    alpha = ImageChops.multiply(layer.getchannel("A"), mask)
    layer.putalpha(alpha)
    image.alpha_composite(layer)


def shade(colour, factor):
    """Darkens (factor < 1) or lightens toward white (factor > 1) a colour."""
    if factor <= 1:
        return tuple(int(c * factor) for c in colour[:3]) + (colour[3],)
    k = factor - 1
    return tuple(int(c + (255 - c) * k) for c in colour[:3]) + (colour[3],)


def top_light(image, mask, width, alpha=110, reach=0.55):
    """Thin light stroke along the upper inner edge of a shape (fades out toward the bottom)."""
    inner = mask.filter(ImageFilter.MinFilter(1 + 2 * (u(width) // 2)))
    edge = ImageChops.subtract(mask, inner)
    box = mask.getbbox()
    if not box:
        return
    fade = np.zeros((image.height, image.width), dtype=np.float32)
    span = max(1, int((box[3] - box[1]) * reach))
    fade[box[1]:box[1] + span, :] = np.linspace(1.0, 0.0, span, dtype=np.float32)[:, None]
    edge_alpha = (np.asarray(edge, dtype=np.float32) * fade * (alpha / 255.0)).astype(np.uint8)
    layer = Image.new("RGBA", image.size, (255, 255, 255, 0))
    layer.putalpha(Image.fromarray(edge_alpha, "L"))
    image.alpha_composite(layer)


def chunky(width, height, radius, top, bottom, lip=7, outline=5, gloss=0.2, ink=INK, lip_colour=None, light=120):
    """Outlined rounded button body: lip at the bottom, gradient face, top light and a glossy upper band."""
    image = blank(width, height)
    size = image.size
    fill(image, rrect_mask(size, (0, 0, size[0], size[1]), u(radius)), ink)
    o = u(outline)
    inner_radius = u(radius - outline)
    lip_colour = lip_colour or shade(bottom, 0.62)
    fill(image, rrect_mask(size, (o, o, size[0] - o, size[1] - o), inner_radius), lip_colour)
    face = rrect_mask(size, (o, o, size[0] - o, size[1] - o - u(lip)), inner_radius)
    fill(image, face, top, bottom)
    if light:
        top_light(image, face, 2.2, light)
    if gloss:
        g = u(4.5)
        band = rrect_mask(size, (o + g, o + g, size[0] - o - g, o + g + u(min(height * 0.3, 22))), u(max(4, radius - outline - 5)))
        fill(image, band, (255, 255, 255, int(255 * gloss)), (255, 255, 255, int(255 * gloss * 0.35)))
    return image


def chunky_circle(diameter, top, bottom, lip=7, outline=5, gloss=0.26, ink=INK):
    image = blank(diameter, diameter)
    size = image.size
    fill(image, ellipse_mask(size, (0, 0, size[0], size[1])), ink)
    o = u(outline)
    fill(image, ellipse_mask(size, (o, o, size[0] - o, size[1] - o)), shade(bottom, 0.62))
    face = ellipse_mask(size, (o, o, size[0] - o, size[1] - o - u(lip)))
    fill(image, face, top, bottom)
    top_light(image, face, 2.4, 130)
    if gloss:
        w = size[0]
        band = ellipse_mask(size, (int(w * 0.2), o + u(4), int(w * 0.8), int(w * 0.42)))
        fill(image, band, (255, 255, 255, int(255 * gloss)), (255, 255, 255, 0))
    return image


def soft_shadow(image, offset=3, blur=3, alpha=110):
    """Adds a drop shadow under an image (keeps the canvas size)."""
    shadow = Image.new("RGBA", image.size, (0, 0, 0, 0))
    a = image.getchannel("A").filter(ImageFilter.GaussianBlur(u(blur)))
    a = ImageChops.offset(a, 0, u(offset))
    shadow.putalpha(a.point(lambda v: int(v * alpha / 255)))
    shadow.alpha_composite(image)
    return shadow


# ---------------------------------------------------------------- frames


def window():
    """Dialog window: violet frame, dark inset body."""
    w, h, r = 200, 200, 40
    image = blank(w, h)
    size = image.size
    fill(image, rrect_mask(size, (0, 0, size[0], size[1]), u(r)), INK)
    o = u(5)
    fill(image, rrect_mask(size, (o, o, size[0] - o, size[1] - o), u(r - 5)), shade(P600, 0.7))
    frame = rrect_mask(size, (o, o, size[0] - o, size[1] - o - u(8)), u(r - 5))
    fill(image, frame, P400, P600)
    top_light(image, frame, 2.4, 140)
    b = u(17)
    fill(image, rrect_mask(size, (b - u(2.5), b - u(2.5), size[0] - b + u(2.5), size[1] - b - u(8) + u(2.5)), u(r - 14)), INK)
    body = rrect_mask(size, (b, b, size[0] - b, size[1] - b - u(8)), u(r - 17))
    fill(image, body, P800, P900)
    # Inner top shadow so the body reads as recessed.
    recess = rrect_mask(size, (b, b, size[0] - b, b + u(14)), u(r - 17))
    fill(image, ImageChops.multiply(recess, body), (10, 5, 26, 90), (10, 5, 26, 0))
    save(image, "UI_Panel")


def card():
    """HUD card (status, toast, timer, skill info): dark translucent body, outline, light rim."""
    w, h, r = 96, 96, 22
    image = blank(w, h)
    size = image.size
    fill(image, rrect_mask(size, (0, 0, size[0], size[1]), u(r)), INK[:3] + (235,))
    o = u(4)
    body = rrect_mask(size, (o, o, size[0] - o, size[1] - o), u(r - 4))
    cut = Image.new("RGBA", size, (0, 0, 0, 0))
    cut.putalpha(body)
    image = ImageChops.subtract(image, Image.merge("RGBA", (Image.new("L", size, 0),) * 3 + (body,)))
    fill(image, body, (52, 34, 104, 226), (30, 19, 66, 226))
    top_light(image, body, 2.0, 120, 0.7)
    save(image, "UI_Card_Glass")


def buttons():
    spec = {
        "UI_Button": (rgba("#9B7CFF"), rgba("#6843E0")),
        "UI_Button_Gold": (rgba("#FFE25A"), rgba("#FFA61C")),
        "UI_Button_Danger": (rgba("#FF7A86"), rgba("#E5304E")),
        "UI_Button_Green": (rgba("#8BEE6A"), rgba("#2FB552")),
        "UI_Button_Blue": (rgba("#6FD4FF"), rgba("#2A86EE")),
        "UI_Button_Ghost": (rgba("#5C4A96"), rgba("#43347A")),
    }
    for name, (top, bottom) in spec.items():
        ghost = name.endswith("Ghost")
        save(chunky(180, 112, 28, top, bottom, lip=8, gloss=0.1 if ghost else 0.24, light=70 if ghost else 130), name)
    # White card that takes a rarity tint (level-up choices, item cards).
    save(chunky(160, 160, 30, WHITE, rgba("#E6E0F4"), lip=9, gloss=0.0, lip_colour=rgba("#A69BC4"), light=0), "UI_Card_Tint")


def slots():
    w, r = 96, 22
    image = blank(w, w)
    size = image.size
    fill(image, rrect_mask(size, (0, 0, size[0], size[1]), u(r)), INK)
    o = u(4)
    rim = rrect_mask(size, (o, o, size[0] - o, size[1] - o), u(r - 4))
    fill(image, rim, P500, P700)
    top_light(image, rim, 2.0, 110)
    i = u(10)
    well = rrect_mask(size, (i, i, size[0] - i, size[1] - i), u(r - 9))
    fill(image, well, P950, P800)
    save(image, "UI_Slot")

    # Skill slot: the rim is white so the skill colour tints it.
    image = blank(w, w)
    fill(image, rrect_mask(size, (0, 0, size[0], size[1]), u(r)), INK)
    rim = rrect_mask(size, (o, o, size[0] - o, size[1] - o), u(r - 4))
    fill(image, rim, WHITE, rgba("#C9C4D6"))
    fill(image, rrect_mask(size, (i - u(2), i - u(2), size[0] - i + u(2), size[1] - i + u(2)), u(r - 7)), INK)
    fill(image, well, rgba("#3A2D5C"), rgba("#5A4A86"))
    save(image, "UI_Slot_Skill")


def bars():
    w, h, r = 96, 44, 15
    image = blank(w, h)
    size = image.size
    fill(image, rrect_mask(size, (0, 0, size[0], size[1]), u(r)), INK)
    o = u(4)
    well = rrect_mask(size, (o, o, size[0] - o, size[1] - o), u(r - 4))
    fill(image, well, rgba("#120A26"), rgba("#2A1D4E"))
    save(image, "UI_Bar_Track")
    save(image, "UI_Bar_Back")

    # Tintable fill: nearly square corners (it is also used as a Filled image, which stretches the caps).
    w, h, r = 64, 32, 7
    image = blank(w, h)
    size = image.size
    body = rrect_mask(size, (0, 0, size[0], size[1]), u(r))
    fill(image, body, WHITE, rgba("#C8C8D2"))
    gloss = rrect_mask(size, (u(3), u(3), size[0] - u(3), u(13)), u(5))
    fill(image, gloss, (255, 255, 255, 150), (255, 255, 255, 60))
    save(image, "UI_Bar_Fill")


def pills():
    # Plain white capsule (washes, chips, dividers).
    image = blank(64, 44)
    fill(image, rrect_mask(image.size, (0, 0, image.width, image.height), u(22)), WHITE)
    save(image, "UI_Pill")
    # Outlined glossy capsule for tags (HP, EXP, LANTAI, BOSS, rarity).
    save(chunky(96, 44, 22, WHITE, rgba("#D4D0E0"), lip=4, outline=4, gloss=0.0, lip_colour=rgba("#8F88A8"), light=0), "UI_Tag")
    # Soft-cornered white block (rarity wash inside slots).
    image = blank(64, 64)
    fill(image, rrect_mask(image.size, (0, 0, image.width, image.height), u(13)), WHITE)
    save(image, "UI_Wash")


def circles():
    image = blank(128, 128)
    fill(image, ellipse_mask(image.size, (0, 0, image.width, image.height)), WHITE)
    save(image, "UI_Circle")
    save(chunky_circle(96, rgba("#9B7CFF"), rgba("#6440DA")), "UI_Circle_Glass")
    save(chunky_circle(96, rgba("#FFE25A"), rgba("#FFA61C")), "UI_Circle_Gold")
    save(chunky_circle(96, rgba("#FF7A86"), rgba("#E5304E")), "UI_Circle_Red")
    save(chunky_circle(200, rgba("#7BE0FF"), rgba("#2B83F0"), lip=11, outline=6), "UI_Dash")

    # Cooldown ring (tintable).
    d = 200
    image = blank(d, d)
    size = image.size
    ring = ImageChops.subtract(ellipse_mask(size, (0, 0, size[0], size[1])), ellipse_mask(size, (u(13), u(13), size[0] - u(13), size[1] - u(13))))
    fill(image, ring, WHITE)
    save(image, "UI_Ring")

    # Joystick: translucent dish with a bright ring and four chevrons.
    d = 250
    image = blank(d, d)
    size = image.size
    fill(image, ellipse_mask(size, (u(4), u(4), size[0] - u(4), size[1] - u(4))), (26, 14, 52, 120), (26, 14, 52, 150))
    ring = ImageChops.subtract(ellipse_mask(size, (0, 0, size[0], size[1])), ellipse_mask(size, (u(6), u(6), size[0] - u(6), size[1] - u(6))))
    fill(image, ring, (255, 255, 255, 215))
    inner = ImageChops.subtract(ellipse_mask(size, (u(52), u(52), size[0] - u(52), size[1] - u(52))), ellipse_mask(size, (u(55), u(55), size[0] - u(55), size[1] - u(55))))
    fill(image, inner, (255, 255, 255, 70))
    draw = ImageDraw.Draw(image)
    c = size[0] / 2
    for k in range(4):
        angle = k * math.pi / 2
        dx, dy = math.sin(angle), -math.cos(angle)
        px, py = -dy, dx
        tip = (c + dx * u(108), c + dy * u(108))
        a = (c + dx * u(92) + px * u(15), c + dy * u(92) + py * u(15))
        b = (c + dx * u(92) - px * u(15), c + dy * u(92) - py * u(15))
        draw.line([a, tip, b], fill=(255, 255, 255, 225), width=u(6), joint="curve")
    save(image, "UI_Joystick_Base")
    save(chunky_circle(110, rgba("#FFFFFF"), rgba("#B9A6FF"), lip=8, gloss=0.5), "UI_Joystick_Knob")


def ribbon():
    """Title ribbon: gold band with folded tails (3-slice: the tails stay, the middle stretches)."""
    w, h = 260, 92
    image = blank(w, h)
    size = image.size
    draw = ImageDraw.Draw(image)
    band_h = 70
    tail_y = 16
    for side in (0, 1):
        def x(v):
            return u(v) if side == 0 else size[0] - u(v)
        # Tail behind the band, with a V notch.
        tail = [(x(4), u(tail_y)), (x(58), u(tail_y)), (x(58), u(tail_y + band_h - 2)), (x(4), u(tail_y + band_h - 2)), (x(24), u(tail_y + band_h / 2))]
        draw.polygon(tail, fill=INK)
        inset = [(x(13), u(tail_y + 5)), (x(58), u(tail_y + 5)), (x(58), u(tail_y + band_h - 7)), (x(13), u(tail_y + band_h - 7)), (x(30), u(tail_y + band_h / 2))]
        draw.polygon(inset, fill=GOLD6)
        # Fold between tail and band.
        fold = [(x(36), u(band_h - 2)), (x(58), u(band_h - 2)), (x(58), u(tail_y + band_h - 2))]
        draw.polygon(fold, fill=INK)
        draw.polygon([(x(41), u(band_h + 1)), (x(56), u(band_h + 1)), (x(56), u(tail_y + band_h - 8))], fill=GOLD7)
    band = Image.new("RGBA", size, (0, 0, 0, 0))
    box = (u(34), 0, size[0] - u(34), u(band_h))
    fill(band, rrect_mask(size, box, u(16)), INK)
    o = u(5)
    fill(band, rrect_mask(size, (box[0] + o, box[1] + o, box[2] - o, box[3] - o), u(11)), GOLD6)
    face = rrect_mask(size, (box[0] + o, box[1] + o, box[2] - o, box[3] - o - u(6)), u(11))
    fill(band, face, GOLD3, GOLD5)
    top_light(band, face, 2.2, 170)
    image.alpha_composite(band)
    save(image, "UI_Ribbon")

    # Violet variant (sub headers, floor banner).
    recolour = np.asarray(image).astype(np.float32)
    rgb = recolour[..., :3]
    lum = (rgb[..., 0] * 0.5 + rgb[..., 1] * 0.4 + rgb[..., 2] * 0.1) / 255.0
    is_ink = (np.abs(rgb - np.array(INK[:3], dtype=np.float32)).sum(axis=-1) < 60)
    violet = np.stack([lum * 150 + 20, lum * 105 + 12, lum * 235 + 20], axis=-1)
    rgb[...] = np.where(is_ink[..., None], rgb, np.clip(violet, 0, 255))
    save(Image.fromarray(recolour.astype(np.uint8), "RGBA"), "UI_Ribbon_Violet")


def glyph(name, painter, size=64, colour=CREAM, outline=4.5):
    """Cream glyph with the kit's dark outline; painter draws white-on-black into an L mask."""
    mask = Image.new("L", (u(size), u(size)), 0)
    painter(ImageDraw.Draw(mask), u(size))
    pad = u(outline)
    grown = mask.filter(ImageFilter.MaxFilter(1 + 2 * pad))
    # MaxFilter is square; round it off with a blur + threshold.
    grown = grown.filter(ImageFilter.GaussianBlur(pad * 0.45)).point(lambda v: 255 if v > 120 else int(v * 255 / 120))
    image = Image.new("RGBA", mask.size, (0, 0, 0, 0))
    fill(image, grown, INK)
    fill(image, mask, colour, shade(colour, 0.86))
    save(image, name)


def glyphs():
    def pause(draw, s):
        for x in (0.27, 0.57):
            draw.rounded_rectangle((s * x, s * 0.2, s * (x + 0.16), s * 0.8), radius=s * 0.06, fill=255)

    def chevron(draw, s):
        draw.line([(s * 0.24, s * 0.38), (s * 0.5, s * 0.64), (s * 0.76, s * 0.38)], fill=255, width=int(s * 0.13), joint="curve")
        for p in ((0.24, 0.38), (0.76, 0.38)):
            draw.ellipse((s * p[0] - s * 0.065, s * p[1] - s * 0.065, s * p[0] + s * 0.065, s * p[1] + s * 0.065), fill=255)

    def check(draw, s):
        points = [(s * 0.2, s * 0.52), (s * 0.42, s * 0.72), (s * 0.8, s * 0.3)]
        draw.line(points, fill=255, width=int(s * 0.15), joint="curve")
        for p in (points[0], points[2]):
            draw.ellipse((p[0] - s * 0.075, p[1] - s * 0.075, p[0] + s * 0.075, p[1] + s * 0.075), fill=255)

    def cross(draw, s):
        for a, b in (((0.26, 0.26), (0.74, 0.74)), ((0.74, 0.26), (0.26, 0.74))):
            draw.line([(s * a[0], s * a[1]), (s * b[0], s * b[1])], fill=255, width=int(s * 0.16))
            for p in (a, b):
                draw.ellipse((s * p[0] - s * 0.08, s * p[1] - s * 0.08, s * p[0] + s * 0.08, s * p[1] + s * 0.08), fill=255)

    def pointer(draw, s):
        draw.polygon([(s * 0.5, s * 0.14), (s * 0.84, s * 0.74), (s * 0.5, s * 0.6), (s * 0.16, s * 0.74)], fill=255)

    def play(draw, s):
        draw.polygon([(s * 0.3, s * 0.2), (s * 0.8, s * 0.5), (s * 0.3, s * 0.8)], fill=255)

    def plus(draw, s):
        draw.rounded_rectangle((s * 0.42, s * 0.18, s * 0.58, s * 0.82), radius=s * 0.05, fill=255)
        draw.rounded_rectangle((s * 0.18, s * 0.42, s * 0.82, s * 0.58), radius=s * 0.05, fill=255)

    glyph("UI_Icon_Pause", pause)
    glyph("UI_Icon_Arrow", chevron)
    glyph("UI_Icon_Check", check, colour=WHITE)
    glyph("UI_Icon_Close", cross, colour=WHITE)
    glyph("UI_Icon_Pointer", pointer, colour=WHITE, size=96)
    glyph("UI_Icon_Play", play)
    glyph("UI_Icon_Plus", plus, colour=WHITE)


def burst():
    """Soft sunburst behind level-up / result titles (white, additive-looking alpha)."""
    n = 768
    yy, xx = np.mgrid[0:n, 0:n].astype(np.float32)
    dx, dy = xx - n / 2, yy - n / 2
    radius = np.hypot(dx, dy) / (n / 2)
    angle = np.arctan2(dy, dx)
    rays = 0.5 + 0.5 * np.cos(angle * 14)
    rays = np.clip((rays - 0.5) * 6 + 0.5, 0, 1)
    falloff = np.clip(1 - radius, 0, 1) ** 1.4
    alpha = (rays * 0.75 + 0.25) * falloff * 255
    image = np.zeros((n, n, 4), dtype=np.uint8)
    image[..., :3] = 255
    image[..., 3] = alpha.astype(np.uint8)
    os.makedirs(PANELS, exist_ok=True)
    Image.fromarray(image, "RGBA").resize((512, 512), Image.LANCZOS).save(os.path.join(PANELS, "UI_Burst.png"), optimize=True)

    # Vertical fade strip (top bar scrim / boss warning band).
    strip = np.zeros((64, 8, 4), dtype=np.uint8)
    strip[..., :3] = 255
    strip[..., 3] = (np.linspace(1, 0, 64) ** 1.5 * 255).astype(np.uint8)[:, None]
    Image.fromarray(strip, "RGBA").save(os.path.join(PANELS, "UI_Fade.png"), optimize=True)


def frames():
    window()
    card()
    buttons()
    slots()
    bars()
    pills()
    circles()
    ribbon()
    glyphs()
    burst()


# ---------------------------------------------------------------- gflow sheets


def key_out(image):
    """Removes the flat chroma background connected to the image border. Returns an RGBA float array (0..1)."""
    rgb = np.asarray(image.convert("RGB")).astype(np.float32) / 255.0
    h, w, _ = rgb.shape
    border = np.concatenate([rgb[:6].reshape(-1, 3), rgb[-6:].reshape(-1, 3), rgb[:, :6].reshape(-1, 3), rgb[:, -6:].reshape(-1, 3)])
    bg = np.median(border, axis=0)
    dist = np.sqrt(((rgb - bg) ** 2).sum(axis=-1))
    near = dist < 0.34
    labels, _ = ndimage.label(near)
    edge_labels = np.unique(np.concatenate([labels[0], labels[-1], labels[:, 0], labels[:, -1]]))
    edge_labels = edge_labels[edge_labels != 0]
    background = np.isin(labels, edge_labels)
    # Small enclosed pockets of background colour (holes in a gear, gaps between wings) go too.
    chroma = (rgb[..., 1] > rgb[..., 0] + 0.3) & (rgb[..., 1] > rgb[..., 2] + 0.3)
    pockets = near & ~background & ((dist < 0.16) | chroma)
    background |= pockets
    zone = ndimage.binary_dilation(background, iterations=3)
    alpha = np.ones((h, w), dtype=np.float32)
    ramp = np.clip((dist - 0.10) / (0.34 - 0.10), 0.0, 1.0)
    alpha[zone] = ramp[zone]
    alpha[background & (dist < 0.10)] = 0.0
    # Un-mix the background from the soft edge and pull green spill back.
    a = np.clip(alpha, 0.05, 1.0)[..., None]
    colour = np.clip((rgb - (1.0 - a) * bg) / a, 0.0, 1.0)
    edge = zone & (alpha > 0)
    limit = np.maximum(colour[..., 0], colour[..., 2])
    spill = edge & (colour[..., 1] > limit)
    colour[..., 1] = np.where(spill, limit, colour[..., 1])
    # Edge pixels of an outlined object are ink: darken what is left of the fringe.
    fringe = edge & (alpha < 0.95)
    colour[fringe] = colour[fringe] * 0.55 + np.array(INK[:3], dtype=np.float32) / 255.0 * 0.45
    return np.dstack([colour, alpha])


def split(cut, merge=10, min_area=0.004):
    """Bounding boxes of the separate objects in a keyed sheet, in reading order."""
    alpha = cut[..., 3] > 0.5
    h, w = alpha.shape
    joined = ndimage.binary_dilation(alpha, iterations=merge)
    labels, count = ndimage.label(joined)
    boxes = []
    for index, slc in enumerate(ndimage.find_objects(labels), start=1):
        part = (labels[slc] == index) & alpha[slc]
        if part.sum() < min_area * h * w:
            continue
        ys, xs = np.nonzero(part)
        boxes.append((slc[1].start + xs.min(), slc[0].start + ys.min(), slc[1].start + xs.max() + 1, slc[0].start + ys.max() + 1))
    if not boxes:
        return []
    # Rows by vertical centre, then left to right.
    boxes.sort(key=lambda b: (b[1] + b[3]) / 2)
    rows, current = [], [boxes[0]]
    for box in boxes[1:]:
        last = current[-1]
        if (box[1] + box[3]) / 2 > last[3] - (last[3] - last[1]) * 0.25:
            rows.append(current)
            current = [box]
        else:
            current.append(box)
    rows.append(current)
    return [box for row in rows for box in sorted(row, key=lambda b: b[0])]


def to_image(cut):
    return Image.fromarray((np.clip(cut, 0, 1) * 255).astype(np.uint8), "RGBA")


def fit_square(image, size, margin=0.06):
    """Centres an object on a transparent square canvas."""
    inner = int(size * (1 - 2 * margin))
    scale = min(inner / image.width, inner / image.height)
    resized = image.resize((max(1, int(image.width * scale)), max(1, int(image.height * scale))), Image.LANCZOS)
    canvas = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    canvas.alpha_composite(resized, ((size - resized.width) // 2, (size - resized.height) // 2))
    return canvas


def cut_sheet(job, spec):
    path = os.path.join(RAW, job, spec["file"])
    cut = key_out(Image.open(path))
    boxes = split(cut, merge=spec.get("merge", 10))
    names = spec["names"]
    if len(boxes) != len(names):
        raise SystemExit("%s: %d objects found, %d names listed (%s)" % (job, len(boxes), len(names), spec["file"]))
    sheet = to_image(cut)
    for name, box in zip(names, boxes):
        if not name:
            continue
        folder = PANELS if name.startswith("UI_") else ICONS
        os.makedirs(folder, exist_ok=True)
        fit_square(sheet.crop(box), spec.get("size", 256)).save(os.path.join(folder, name + ".png"), optimize=True)


def cut_logo(spec):
    cut = key_out(Image.open(os.path.join(RAW, "logo", spec["file"])))
    alpha = cut[..., 3] > 0.5
    ys, xs = np.nonzero(alpha)
    image = to_image(cut).crop((xs.min(), ys.min(), xs.max() + 1, ys.max() + 1))
    width = 1000
    image = image.resize((width, int(image.height * width / image.width)), Image.LANCZOS)
    pad = Image.new("RGBA", (image.width + 24, image.height + 24), (0, 0, 0, 0))
    pad.alpha_composite(image, (12, 12))
    os.makedirs(ART, exist_ok=True)
    pad.save(os.path.join(ART, "ART_Logo.png"), optimize=True)


def cut_avatar(spec):
    """Hero bust inside a round gold-rimmed badge."""
    cut = key_out(Image.open(os.path.join(RAW, "avatar", spec["file"])))
    alpha = cut[..., 3] > 0.5
    ys, xs = np.nonzero(alpha)
    bust = to_image(cut).crop((xs.min(), ys.min(), xs.max() + 1, ys.max() + 1))
    size = 512
    badge = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    full = (size, size)
    fill(badge, ellipse_mask(full, (0, 0, size, size)), INK)
    fill(badge, ellipse_mask(full, (14, 14, size - 14, size - 14)), GOLD3, GOLD5)
    fill(badge, ellipse_mask(full, (40, 40, size - 40, size - 40)), INK)
    disc = ellipse_mask(full, (50, 50, size - 50, size - 50))
    fill(badge, disc, rgba("#8E78FF"), rgba("#4B2FA8"))
    zoom = spec.get("zoom", 1.0)
    scale = (size - 70) * zoom / max(bust.width, bust.height)
    bust = bust.resize((int(bust.width * scale), int(bust.height * scale)), Image.LANCZOS)
    layer = Image.new("RGBA", full, (0, 0, 0, 0))
    layer.alpha_composite(bust, ((size - bust.width) // 2 + spec.get("dx", 0), (size - bust.height) // 2 + spec.get("dy", 0)))
    layer.putalpha(ImageChops.multiply(layer.getchannel("A"), disc))
    badge.alpha_composite(layer)
    os.makedirs(ART, exist_ok=True)
    badge.resize((256, 256), Image.LANCZOS).save(os.path.join(ART, "ART_Avatar.png"), optimize=True)


def cut_keyart(spec):
    image = Image.open(os.path.join(RAW, "keyart", spec["file"])).convert("RGB")
    target = (1280, 720)
    scale = max(target[0] / image.width, target[1] / image.height)
    image = image.resize((int(image.width * scale + 0.5), int(image.height * scale + 0.5)), Image.LANCZOS)
    left, top = (image.width - target[0]) // 2, (image.height - target[1]) // 2
    os.makedirs(ART, exist_ok=True)
    image.crop((left, top, left + target[0], top + target[1])).save(os.path.join(ART, "ART_KeyArt.png"), optimize=True)


def art():
    with open(os.path.join(SRC, "pick.json")) as handle:
        pick = json.load(handle)
    for job, spec in pick.items():
        if job == "logo":
            cut_logo(spec)
        elif job == "avatar":
            cut_avatar(spec)
        elif job == "keyart":
            cut_keyart(spec)
        else:
            cut_sheet(job, spec)


def contact_sheet():
    """Everything on one dark sheet, for a quick look."""
    files = []
    for folder in (PANELS, ICONS, ART):
        if os.path.isdir(folder):
            files += [os.path.join(folder, f) for f in sorted(os.listdir(folder)) if f.endswith(".png") and f != "UI_White.png"]
    cell, columns = 200, 9
    rows = (len(files) + columns - 1) // columns
    sheet = Image.new("RGBA", (columns * cell, rows * (cell + 22)), (58, 110, 70, 255))
    draw = ImageDraw.Draw(sheet)
    for index, path in enumerate(files):
        image = Image.open(path).convert("RGBA")
        image.thumbnail((cell - 16, cell - 16), Image.LANCZOS)
        x, y = (index % columns) * cell, (index // columns) * (cell + 22)
        sheet.alpha_composite(image, (x + (cell - image.width) // 2, y + (cell - image.height) // 2))
        draw.text((x + 6, y + cell + 2), os.path.basename(path)[:-4][:30], fill=(255, 255, 255, 255))
    out = os.path.join(ROOT, "asset_work", "previews", "UI_Kit.png")
    sheet.convert("RGB").save(out)
    print(out)


def main():
    mode = sys.argv[1] if len(sys.argv) > 1 else "all"
    if mode in ("all", "frames"):
        frames()
    if mode in ("all", "art"):
        art()
    if mode in ("all", "sheet"):
        contact_sheet()
    print("UI_MODERN_OK", flush=True)


if __name__ == "__main__":
    main()
