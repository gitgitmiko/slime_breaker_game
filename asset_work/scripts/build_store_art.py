"""App icon (launcher + adaptive layers) and Play Store graphics from the transparent character renders.

    python3 asset_work/scripts/build_store_art.py

Needs asset_work/store/renders/*.png from render_store_art.py (Blender). Writes:
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


def icon_art(size, safe):
    """Hero head and shoulders (the render's cut edge bleeds off the bottom) with a slime peeking in, inside `safe` × size."""
    layer = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    hero = render("hero_face")
    slime = render("slime")
    box = size * safe
    margin = (size - box) / 2
    hero_h = box * 1.12
    scaled_w = hero.width * hero_h / hero.height
    if scaled_w > box * 1.05:
        hero_h *= box * 1.05 / scaled_w
    # Hat tip at the top of the safe box; whatever is below the box bleeds past the layer edge.
    place(layer, hero, hero_h, (size / 2 - box * 0.04, margin + hero_h / 2), shadow=False)
    place(layer, slime, box * 0.36, (size / 2 + box * 0.34, size / 2 + box * 0.36))
    return layer


def text(draw, xy, value, size, fill=(255, 255, 255), stroke=(70, 30, 120), anchor="mm"):
    font = ImageFont.truetype(FONT, size)
    draw.text(xy, value, font=font, fill=fill, stroke_width=max(2, size // 12), stroke_fill=stroke, anchor=anchor)


def main():
    os.makedirs(ICON_OUT, exist_ok=True)

    full = gradient(1024, 1024)
    sparkles(full, 70, 3, 2.0)
    full.alpha_composite(icon_art(1024, 0.86))
    full.convert("RGB").save(os.path.join(ICON_OUT, "APP_Icon.png"))
    full.resize((512, 512), Image.LANCZOS).convert("RGB").save(os.path.join(STORE, "icon_512.png"))

    background = gradient(432, 432)
    sparkles(background, 40, 5, 0.9)
    background.convert("RGB").save(os.path.join(ICON_OUT, "APP_Icon_Background.png"))
    icon_art(432, 0.62).save(os.path.join(ICON_OUT, "APP_Icon_Foreground.png"))

    feature = gradient(1024, 500, (96, 70, 230), (255, 128, 180))
    sparkles(feature, 90, 9, 1.2)
    draw = ImageDraw.Draw(feature)
    place(feature, render("boss"), 250, (900, 360))
    place(feature, render("slime"), 110, (690, 420))
    place(feature, render("hero_full"), 430, (150, 275))
    text(draw, (600, 110), "DUMPLING WITCH", 70, fill=(255, 226, 120))
    text(draw, (600, 185), "100 lantai  •  10 boss  •  skill sihir", 30)
    feature.convert("RGB").save(os.path.join(STORE, "feature_graphic_1024x500.png"))
    print("STORE_ART_OK", flush=True)


if __name__ == "__main__":
    main()
