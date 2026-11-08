#!/usr/bin/env python3
"""Draws the app icon: an ace of spades with a red casino chip on deep purple, in the game's colours.

    Tools/.external/pyenv/bin/python Tools/make_icon.py

Writes, for Unity (Editor/AppIcons.cs puts them on the Android build):
  Assets/Art/Icon/AppIcon.png             1024 square, the whole icon (legacy and round launcher icons)
  Assets/Art/Icon/AppIconBackground.png   1024, the adaptive icon's background layer
  Assets/Art/Icon/AppIconForeground.png   1024, the adaptive icon's foreground, kept inside the 66/108 safe circle
and for the store listing:
  PlayStore/icon-512.png                  the 512 x 512 Play Store icon (Play rounds the corners itself)
"""
import math
import os

from PIL import Image, ImageDraw, ImageFilter, ImageFont

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ICON_DIR = os.path.join(ROOT, "Assets", "Art", "Icon")
STORE_DIR = os.path.join(ROOT, "PlayStore")
FONT = os.path.join(ROOT, "Assets", "Resources", "Fonts", "BebasNeue-Regular.ttf")

S = 2048            # drawn at twice the size and scaled down, for clean edges
PURPLE_IN, PURPLE_OUT = (92, 24, 120), (18, 6, 30)
GOLD, GOLD_DARK = (240, 192, 80), (170, 118, 30)
CARD, INK, RED = (255, 249, 236), (24, 18, 28), (200, 16, 46)


def background():
    """A radial glow from just above the middle to near-black purple at the edges."""
    img = Image.new("RGB", (S, S))
    px = img.load()
    cx, cy, r = S * 0.5, S * 0.42, S * 0.75
    for y in range(S):
        for x in range(S):
            t = min(1.0, math.hypot(x - cx, y - cy) / r) ** 1.3
            px[x, y] = tuple(int(a + (b - a) * t) for a, b in zip(PURPLE_IN, PURPLE_OUT))
    return img.convert("RGBA")


def spade(draw, cx, cy, s, fill):
    """A spade of height about 1.1 s centred on (cx, cy)."""
    r = 0.27 * s
    for side in (-1, 1):
        draw.ellipse([cx + side * 0.25 * s - r, cy + 0.1 * s - r, cx + side * 0.25 * s + r, cy + 0.1 * s + r], fill=fill)
    draw.polygon([(cx, cy - 0.55 * s), (cx - 0.51 * s, cy + 0.06 * s), (cx + 0.51 * s, cy + 0.06 * s)], fill=fill)
    draw.polygon([(cx, cy + 0.05 * s), (cx - 0.18 * s, cy + 0.58 * s), (cx + 0.18 * s, cy + 0.58 * s)], fill=fill)


def shadow(layer, blur, offset, strength=0.55):
    """A soft drop shadow of a layer's shape."""
    a = layer.split()[3].filter(ImageFilter.GaussianBlur(blur)).point(lambda v: int(v * strength))
    sh = Image.new("RGBA", layer.size, (0, 0, 0, 0))
    sh.putalpha(a)
    out = Image.new("RGBA", layer.size, (0, 0, 0, 0))
    out.paste(sh, offset, sh)
    return out


def card(scale):
    """The ace, tilted a little, on its own transparent layer."""
    layer = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    w, h = 0.40 * S * scale, 0.56 * S * scale
    cx, cy = S * 0.44, S * 0.46
    box = [cx - w / 2, cy - h / 2, cx + w / 2, cy + h / 2]
    d.rounded_rectangle(box, radius=0.06 * w, fill=GOLD)
    inset = 0.035 * w
    d.rounded_rectangle([box[0] + inset, box[1] + inset, box[2] - inset, box[3] - inset], radius=0.05 * w, fill=CARD)
    spade(d, cx, cy + 0.02 * h, 0.52 * w, INK)
    font = ImageFont.truetype(FONT, int(0.22 * w))
    for corner, (x, y) in enumerate(((box[0] + 0.15 * w, box[1] + 0.14 * h), (box[2] - 0.15 * w, box[3] - 0.14 * h))):
        mark = Image.new("RGBA", (int(0.3 * w), int(0.42 * w)), (0, 0, 0, 0))
        md = ImageDraw.Draw(mark)
        md.text((mark.width / 2, 0.16 * w), "A", font=font, fill=INK, anchor="mm")
        spade(md, mark.width / 2, 0.33 * w, 0.11 * w, INK)
        if corner == 1:
            mark = mark.rotate(180)
        layer.alpha_composite(mark, (int(x - mark.width / 2), int(y - 0.2 * w)))
    return layer.rotate(-9, resample=Image.BICUBIC, center=(cx, cy))


def chip(scale):
    """A red casino chip with white edge spots and a gold star, lower right."""
    layer = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    r = 0.165 * S * scale
    cx, cy = S * 0.5 + 0.21 * S * scale, S * 0.5 + 0.21 * S * scale
    d.ellipse([cx - r, cy - r, cx + r, cy + r], fill=RED)
    for i in range(8):   # the edge spots
        a = i * math.pi / 4
        px, py = cx + math.cos(a) * r * 0.82, cy + math.sin(a) * r * 0.82
        sw, sh = r * 0.2, r * 0.13
        spot = Image.new("RGBA", (int(sw * 2), int(sw * 2)), (0, 0, 0, 0))
        ImageDraw.Draw(spot).rounded_rectangle([sw - sh, sw - sw / 2, sw + sh, sw + sw / 2], radius=sh * 0.3, fill=CARD)
        spot = spot.rotate(-math.degrees(a), resample=Image.BICUBIC)
        layer.alpha_composite(spot, (int(px - sw), int(py - sw)))
    ri = r * 0.6
    d.ellipse([cx - ri, cy - ri, cx + ri, cy + ri], fill=CARD)
    d.ellipse([cx - ri * 0.86, cy - ri * 0.86, cx + ri * 0.86, cy + ri * 0.86], fill=RED)
    star = [(cx + math.sin(k * math.pi / 5) * ri * (0.62 if k % 2 == 0 else 0.26),
             cy - math.cos(k * math.pi / 5) * ri * (0.62 if k % 2 == 0 else 0.26)) for k in range(10)]
    d.polygon(star, fill=GOLD)
    return layer


def foreground(scale):
    """The card and chip with their shadows."""
    out = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    c, k = card(scale), chip(scale)
    out.alpha_composite(shadow(c, S * 0.02 * scale, (int(S * 0.012), int(S * 0.02))))
    out.alpha_composite(c)
    out.alpha_composite(shadow(k, S * 0.015 * scale, (int(S * 0.01), int(S * 0.016))))
    out.alpha_composite(k)
    return out


def save(img, path, size):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    img.resize((size, size), Image.LANCZOS).save(path)
    print("wrote", os.path.relpath(path, ROOT))


def main():
    bg = background()
    full = bg.copy()
    full.alpha_composite(foreground(1.0))
    save(full, os.path.join(ICON_DIR, "AppIcon.png"), 1024)
    save(full.convert("RGB"), os.path.join(STORE_DIR, "icon-512.png"), 512)
    save(bg, os.path.join(ICON_DIR, "AppIconBackground.png"), 1024)
    # Launchers crop adaptive icons to as little as the middle 66/108 of the layer: shrink the art to fit inside that.
    save(foreground(0.62), os.path.join(ICON_DIR, "AppIconForeground.png"), 1024)


if __name__ == "__main__":
    main()
