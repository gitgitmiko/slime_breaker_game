"""App icon (launcher + adaptive layers) and Play Store graphics from the transparent character renders.

    python3 asset_work/scripts/build_store_art.py

The art is the 2D set made with gflow (asset_work/ui_src: avatar bust, mascots, key art) plus the cut logo from
build_ui_modern.py; run that first. The Blender renders in asset_work/store/renders are no longer used here. Writes:
    asset_work/output/UI/AppIcon/APP_Icon.png             1024 legacy launcher icon (full-bleed)
    asset_work/output/UI/AppIcon/APP_Icon_Background.png  432 adaptive background layer
    asset_work/output/UI/AppIcon/APP_Icon_Foreground.png  432 adaptive foreground layer (art inside the 66% safe zone)
    asset_work/store/icon_512.png                         Play Store hi-res icon
    asset_work/store/feature_graphic_1024x500.png         Play Store feature graphic
"""
import math
import os
import random

from PIL import Image, ImageDraw, ImageFilter, ImageFont

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
RENDERS = os.path.join(ROOT, "asset_work", "store", "renders")
STORE = os.path.join(ROOT, "asset_work", "store")
ICON_OUT = os.path.join(ROOT, "asset_work", "output", "UI", "AppIcon")
FONT = "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf"

TOP = (124, 92, 255)
BOTTOM = (255, 120, 176)


def render(name):
    image = Image.open(os.path.join(RENDERS, name + ".png")).convert("RGBA")
    return image.crop(image.getchannel("A").getbbox())


def gradient(width, height, top=TOP, bottom=BOTTOM):
    image = Image.new("RGBA", (width, height))
    pixels = image.load()
    for y in range(height):
        for x in range(width):
            t = min(1.0, max(0.0, (y / height) * 0.8 + (x / width) * 0.2))
            pixels[x, y] = tuple(int(top[i] + (bottom[i] - top[i]) * t) for i in range(3)) + (255,)
    return image


def sparkles(image, count, seed, scale=1.0):
    rng = random.Random(seed)
    layer = Image.new("RGBA", image.size, (0, 0, 0, 0))
    draw = ImageDraw.Draw(layer)
    w, h = image.size
    for _ in range(count):
        x, y = rng.uniform(0, w), rng.uniform(0, h)
        r = rng.uniform(1.5, 6.0) * scale
        a = rng.randint(70, 190)
        draw.ellipse((x - r, y - r, x + r, y + r), fill=(255, 255, 255, a))
    glow = Image.new("RGBA", image.size, (255, 240, 255, 0))
    gd = ImageDraw.Draw(glow)
    gd.ellipse((w * 0.12, h * 0.08, w * 0.88, h * 0.84), fill=(255, 240, 255, 70))
    glow = glow.filter(ImageFilter.GaussianBlur(w * 0.08))
    image.alpha_composite(glow)
    image.alpha_composite(layer.filter(ImageFilter.GaussianBlur(0.6 * scale)))


def shadowed(sprite, radius, offset, alpha=110):
    pad = radius * 3
    out = Image.new("RGBA", (sprite.width + pad * 2, sprite.height + pad * 2), (0, 0, 0, 0))
    shadow = Image.new("RGBA", sprite.size, (40, 10, 60, alpha))
    shadow.putalpha(sprite.getchannel("A").point(lambda v: v * alpha // 255))
    out.alpha_composite(shadow, (pad + offset[0], pad + offset[1]))
    out = out.filter(ImageFilter.GaussianBlur(radius))
    out.alpha_composite(sprite, (pad, pad))
    return out, pad


def place(canvas, sprite, height, center, shadow=True):
    scale = height / sprite.height
    sprite = sprite.resize((max(1, int(sprite.width * scale)), max(1, int(height))), Image.LANCZOS)
    if shadow:
        sprite, pad = shadowed(sprite, max(2, int(height * 0.025)), (0, int(height * 0.03)))
    else:
        pad = 0
    x = int(center[0] - sprite.width / 2)
    y = int(center[1] - sprite.height / 2)
    canvas.alpha_composite(sprite, (x, y))


def gflow_sprites(job, key="file"):
    """Keyed objects of a raw gflow sheet (asset_work/ui_src), in reading order."""
    import json
    import sys

    sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
    import build_ui_modern as kit

    with open(os.path.join(kit.SRC, "pick.json")) as handle:
        spec = json.load(handle)[job]
    cut = kit.key_out(Image.open(os.path.join(kit.RAW, job, spec[key])))
    sheet = kit.to_image(cut)
    return [sheet.crop(box) for box in kit.split(cut, merge=spec.get("merge", 10))]


def burst(size, alpha=70):
    """Soft white rays behind the icon art."""
    layer = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    draw = ImageDraw.Draw(layer)
    c = size / 2
    for k in range(14):
        a0 = k / 14 * math.tau
        a1 = a0 + math.tau / 28
        draw.polygon([(c, c), (c + math.cos(a0) * size, c + math.sin(a0) * size), (c + math.cos(a1) * size, c + math.sin(a1) * size)], fill=(255, 255, 255, alpha))
    return layer.filter(ImageFilter.GaussianBlur(size * 0.004))


def icon_art(size, safe):
    """2D witch bust (gflow avatar) with the pink boss dumpling peeking in, inside `safe` × size."""
    layer = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    hero = gflow_sprites("avatar")[0]
    boss = gflow_sprites("mascots")[0]
    box = size * safe
    hero_h = box * 0.98
    if hero.width * hero_h / hero.height > box * 1.04:
        hero_h *= box * 1.04 / (hero.width * hero_h / hero.height)
    place(layer, hero, hero_h, (size / 2 - box * 0.03, size / 2 - box * 0.02))
    place(layer, boss, box * 0.4, (size / 2 + box * 0.31, size / 2 + box * 0.31))
    return layer


def text(draw, xy, value, size, fill=(255, 255, 255), stroke=(70, 30, 120), anchor="mm"):
    font = ImageFont.truetype(FONT, size)
    draw.text(xy, value, font=font, fill=fill, stroke_width=max(2, size // 12), stroke_fill=stroke, anchor=anchor)


def main():
    os.makedirs(ICON_OUT, exist_ok=True)

    full = gradient(1024, 1024)
    full.alpha_composite(burst(1024))
    sparkles(full, 50, 3, 2.0)
    full.alpha_composite(icon_art(1024, 0.9))
    full.convert("RGB").save(os.path.join(ICON_OUT, "APP_Icon.png"))
    full.resize((512, 512), Image.LANCZOS).convert("RGB").save(os.path.join(STORE, "icon_512.png"))

    background = gradient(432, 432)
    background.alpha_composite(burst(432))
    sparkles(background, 30, 5, 0.9)
    background.convert("RGB").save(os.path.join(ICON_OUT, "APP_Icon_Background.png"))
    icon_art(432, 0.62).save(os.path.join(ICON_OUT, "APP_Icon_Foreground.png"))

    # Feature graphic: the gflow key art (witch left, dumplings right) with the logo in its empty upper centre.
    import json

    ui_src = os.path.join(ROOT, "asset_work", "ui_src")
    with open(os.path.join(ui_src, "pick.json")) as handle:
        key_file = json.load(handle)["keyart"]["file"]
    art = Image.open(os.path.join(ui_src, "raw", "keyart", key_file)).convert("RGBA")
    scale = 1024 / art.width
    art = art.resize((1024, int(art.height * scale + 0.5)), Image.LANCZOS)
    top = max(0, (art.height - 500) // 2 - 6)
    feature = art.crop((0, top, 1024, top + 500))
    logo = Image.open(os.path.join(ROOT, "asset_work", "output", "UI", "Art", "ART_Logo.png")).convert("RGBA")
    place(feature, logo, 200, (640, 112))
    feature.convert("RGB").save(os.path.join(STORE, "feature_graphic_1024x500.png"))
    print("STORE_ART_OK", flush=True)


if __name__ == "__main__":
    main()
