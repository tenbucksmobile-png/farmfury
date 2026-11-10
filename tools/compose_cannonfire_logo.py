"""
Farm Fury Cannonfire logo (2026-11-10, second design; user: the wooden sign was "far too close to Stampede - the logo
must be original ... have the cannonfire look like it has been fired out of the cannon").

Kling drew the cannon and its blast well (Logo2_B.png) but could not reliably spell the word along the blast, so the
logo is composed here:
  * the FARM FURY(TM) wordmark   - FarmFury's own original mark (originals/FF_Logo_FarmFury.png)
  * the cannon, smoke and flame  - Logo2_B.png (magenta keyed), everything except its static text block
  * "CANNONFIRE"                 - set in Titan One with the wordmark's look (yellow-to-orange fill, dark brown outline,
                                   drop shadow), letter by letter along the blast's arc, small at the muzzle and growing
                                   as they fly out, each tilted to the arc, with a hot glow behind them
Output: FarmFury_Artwork/Cannonfire/Logo_Cannonfire.png (transparent) and Logo_CannonfireBlast.png (no wordmark, for the
app icon)

Usage (repo root):  python tools/compose_cannonfire_logo.py
"""
import math
import os

import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont

from import_cornfield_art import trim, DESKTOP
from import_frozen_art import key_magenta

ART = os.path.join(DESKTOP, "FarmFury_Technical", "FarmFury_Artwork", "Cannonfire")
FONT = r"C:\Windows\Fonts\TitanOne-Regular.ttf"
TOP, BOTTOM = (255, 214, 64), (236, 92, 10)        # the wordmark's fill gradient (sampled by eye from FF_Logo)
OUTLINE, SHADOW = (58, 26, 8), (40, 16, 4)


def letter(ch, size):
    """One letter as an RGBA image: gradient fill, thick outline, offset drop shadow."""
    font = ImageFont.truetype(FONT, size)
    stroke = max(3, size // 9)
    l, t, r, b = font.getbbox(ch, stroke_width=stroke)
    pad = stroke + size // 6
    w, h = r - l + pad * 2, b - t + pad * 2
    ox, oy = pad - l, pad - t
    shadow = Image.new("L", (w, h), 0)
    ImageDraw.Draw(shadow).text((ox + size // 14, oy + size // 10), ch, font=font, fill=255, stroke_width=stroke, stroke_fill=255)
    outline = Image.new("L", (w, h), 0)
    ImageDraw.Draw(outline).text((ox, oy), ch, font=font, fill=255, stroke_width=stroke, stroke_fill=255)
    fill = Image.new("L", (w, h), 0)
    ImageDraw.Draw(fill).text((ox, oy), ch, font=font, fill=255)
    grad = np.zeros((h, w, 3), np.float32)
    k = np.clip((np.arange(h)[:, None] - oy - (font.getbbox(ch)[1])) / max(1, font.getbbox(ch)[3] - font.getbbox(ch)[1]), 0, 1)
    for c in range(3):
        grad[:, :, c] = TOP[c] * (1 - k) + BOTTOM[c] * k
    out = np.zeros((h, w, 4), np.float32)
    s = np.asarray(shadow, np.float32) / 255
    o = np.asarray(outline, np.float32) / 255
    f = np.asarray(fill, np.float32) / 255
    out[:, :, :3] = np.array(SHADOW, np.float32) * s[..., None]
    out[:, :, 3] = s * 255
    out[:, :, :3] = out[:, :, :3] * (1 - o[..., None]) + np.array(OUTLINE, np.float32) * o[..., None]
    out[:, :, 3] = np.maximum(out[:, :, 3], o * 255)
    # A soft top highlight band on the fill, like the wordmark's glossy upper half.
    hl = np.clip(1.0 - k * 2.2, 0, 1) * 0.25
    fillrgb = grad + (255 - grad) * hl[..., None]
    out[:, :, :3] = out[:, :, :3] * (1 - f[..., None]) + fillrgb * f[..., None]
    return Image.fromarray(out.clip(0, 255).astype(np.uint8), "RGBA")


def main():
    src = key_magenta(os.path.join(ART, "Logo2_B.png"))
    h, w = src.shape[:2]
    # Keep the cannon, smoke and flame; drop Kling's static text block (top-left, above the flame).
    yy, xx = np.mgrid[:h, :w]
    text_block = ((xx < 1700) & (yy < 585)) | (yy < 300)   # the text block, and its stray TM above the flame
    blast = src.copy()
    blast[text_block, 3] = 0
    # Small enclosed magenta pockets (between wheel spokes) survive the keyer's pocket-size limit: drop any clearly
    # magenta pixel (red and blue both well above green; the flame is red/yellow with low blue, so it is untouched).
    rgb = blast[:, :, :3].astype(np.int16)
    blast[(np.minimum(rgb[:, :, 0], rgb[:, :, 2]) - rgb[:, :, 1]) > 60, 3] = 0
    canvas = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    canvas.alpha_composite(Image.fromarray(blast, "RGBA"))


    # CANNONFIRE along the blast: a gentle arc from just past the muzzle up to the flame's tip.
    word = "CANNONFIRE"
    p0, p1, bulge = np.array([960.0, 1120.0]), np.array([2130.0, 560.0]), 60.0
    d = p1 - p0
    n = np.array([d[1], -d[0]]) / np.linalg.norm(d)            # perpendicular, toward the upper left
    def at(t):
        return p0 + d * t + n * bulge * math.sin(math.pi * t)
    def tangent(t):
        dt = d + n * bulge * math.pi * math.cos(math.pi * t)
        return math.degrees(math.atan2(-dt[1], dt[0]))
    # Letters are spaced by their own widths along the arc (arc-length placement), small at the muzzle and growing as
    # they fly, then the whole run is scaled so it spans the blast from just past the muzzle to the flame's tip.
    ts = np.linspace(0, 1, 400)
    pts = np.array([at(t) for t in ts])
    seg = np.r_[0, np.cumsum(np.linalg.norm(np.diff(pts, axis=0), axis=1))]
    arclen = seg[-1]
    growth = [0.62 + 0.38 * (i / (len(word) - 1)) for i in range(len(word))]
    base = 200
    # The glyph's own advance (not the padded letter image), a touch tight so the outlines just kiss.
    widths = [ImageFont.truetype(FONT, int(base * g)).getlength(ch) * 0.97 for ch, g in zip(word, growth)]
    k = arclen * 0.98 / sum(widths)
    sizes = [int(base * g * k) for g in growth]
    widths = [w_ * k for w_ in widths]
    centres, run = [], 0.0
    for w_ in widths:
        centres.append(run + w_ / 2); run += w_
    t_of = lambda L: float(np.interp(L, seg, ts))
    glow = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    gd = ImageDraw.Draw(glow)
    for c_, sz in zip(centres, sizes):
        x, y = at(t_of(c_))
        r = sz * 0.75
        gd.ellipse([x - r, y - r * 0.8, x + r, y + r * 0.8], fill=(255, 236, 160, 150))
    canvas.alpha_composite(glow.filter(ImageFilter.GaussianBlur(28)))
    for ch, c_, sz in zip(word, centres, sizes):
        t = t_of(c_)
        img = letter(ch, sz).rotate(tangent(t), resample=Image.BICUBIC, expand=True)
        x, y = at(t)
        canvas.alpha_composite(img, (int(x - img.width / 2), int(y - img.height / 2)))

    # Blast-only version (cannon + CANNONFIRE, no wordmark): the app icon says only "Cannonfire".
    blast_only = trim(np.asarray(canvas))
    Image.fromarray(blast_only, "RGBA").save(os.path.join(ART, "Logo_CannonfireBlast.png"), optimize=True)

    # FARM FURY wordmark across the top.
    mark = Image.open(os.path.join(ART, "originals", "FF_Logo_FarmFury.png")).convert("RGBA")
    mark = mark.crop(mark.getbbox())
    mw = 1500
    mark = mark.resize((mw, int(mark.height * mw / mark.width)), Image.LANCZOS)
    canvas.alpha_composite(mark, (110, 110))

    out = trim(np.asarray(canvas))
    path = os.path.join(ART, "Logo_Cannonfire.png")
    Image.fromarray(out, "RGBA").save(path, optimize=True)
    print("wrote", path, out.shape[1], "x", out.shape[0])


if __name__ == "__main__":
    main()
