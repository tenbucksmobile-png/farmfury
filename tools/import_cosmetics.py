"""
Farm Fury - cosmetics art import (2026-11-08): cannon skins, impact effects, shop cards and banners.

Sources: FarmFury_Technical/FarmFury_Artwork/Rough_Cosmetics/Cannons_Impacts/ (Kling, white or flat backgrounds)
plus two reused bursts (Stampede FeatherBurst, World1Props EggSplat).

Writes:
  unity/Assets/Sprites/Cosmetics/Cannon_<Name>.png   in-game skins on Cannon.png's 566x512 canvas, content in the
                                                     same box, so FarmFury's transform and barrel offsets still fit
  unity/Assets/Sprites/Cosmetics/Impact_<Name>.png   robot-defeat bursts
  unity/Assets/Sprites/UI/Franchise/Shop/<Name>_price.png  shop cards (blank sign + item + $1.99 plaque)
  unity/Assets/Sprites/UI/Franchise/Shop/<Name>.png        Locker / Character Story icons (sign + item)
  unity/Assets/Sprites/UI/Franchise/Shop/Cannons_Banner.png, Impacts_Banner.png

Usage (repo root):  python tools/import_cosmetics.py      then Wire Scene References.
"""
import os
import numpy as np
from PIL import Image
from scipy import ndimage

from import_cornfield_art import cut_white, trim, DESKTOP

SRC = os.path.join(DESKTOP, "FarmFury_Technical", "FarmFury_Artwork", "Rough_Cosmetics", "Cannons_Impacts")
UNITY = os.path.join(os.path.dirname(__file__), "..", "unity", "Assets", "Sprites")
OUT_GAME = os.path.join(UNITY, "Cosmetics")
OUT_SHOP = os.path.join(UNITY, "UI", "Franchise", "Shop")

# StoreProducts.Cannons / Impacts order. (file name, source, mirror)
CANNONS = [("Golden", "Cannon_Golden.png", False), ("Pumpkin", "Cannon_Pumpkin.png", False),
           ("CornCob", "Cannon_CornCob.png", False), ("MilkChurn", "Cannon_MilkChurn.png", False),
           ("Tractor", "Cannon_Tractor.png", True)]   # the tractor's barrel came out pointing left
IMPACTS = [("Confetti", os.path.join(SRC, "Impact_Confetti.png"), "white"),
           ("Popcorn", os.path.join(SRC, "Impact_Popcorn.png"), "corner"),
           ("Fireworks", os.path.join(SRC, "Impact_Fireworks.png"), "white"),
           ("Feathers", os.path.join(DESKTOP, "FarmFury_Stampede", "Assets", "_Project", "Sprites", "Characters", "FeatherBurst.png"), None),
           ("EggSplat", os.path.join(UNITY, "Environment", "World1Props", "EggSplat.png"), None)]

# Cannon.png: 566x512, content box x 0..490, y 54..490 (from the top).
CANNON_CANVAS, CANNON_BOX = (566, 512), (0, 54, 490, 490)


def cut_corner(path, tol=26):
    """Border-connected background of the corner colour (popcorn is on light blue so the white kernels survive)."""
    rgb = np.asarray(Image.open(path).convert("RGB")).astype(np.int16)
    near = np.abs(rgb - rgb[4, 4]).max(axis=2) <= tol
    labels, _ = ndimage.label(near)
    border = np.unique(np.concatenate([labels[0], labels[-1], labels[:, 0], labels[:, -1]]))
    bg = np.isin(labels, border[border > 0])
    return np.dstack([rgb.astype(np.uint8), np.where(bg, 0, 255).astype(np.uint8)])


def load(path, mode):
    if mode == "white": return cut_white(path, min_pocket=10**9)
    if mode == "corner": return cut_corner(path)
    return np.asarray(Image.open(path).convert("RGBA")).copy()


def fit(img, box_w, box_h):
    s = min(box_w / img.width, box_h / img.height)
    return img.resize((max(1, int(img.width * s)), max(1, int(img.height * s))), Image.LANCZOS)


def blank_card():
    card = Image.fromarray(trim(cut_white(os.path.join(SRC, "Card_Blank.png"))), "RGBA")   # incl. the gap between the chains
    a = np.asarray(card)[:, :, 3] > 128
    wide = a.mean(axis=1) > 0.6
    y = 0
    while y < len(wide) and not wide[y]: y += 1
    top = y
    while y < len(wide) and wide[y]: y += 1
    return card, (top, y)   # the sign board's rows


def card_with(item, card, board, with_price):
    top, bottom = board
    out = card.copy() if with_price else card.crop((0, 0, card.width, bottom + 4))
    bw, bh = card.width, bottom - top
    art = fit(item, int(bw * 0.74), int(bh * 0.74))
    out.alpha_composite(art, ((bw - art.width) // 2, top + (bh - art.height) // 2))
    return out


def main():
    os.makedirs(OUT_GAME, exist_ok=True)
    card, board = blank_card()

    for name, src, mirror in CANNONS:
        img = Image.fromarray(trim(load(os.path.join(SRC, src), "white")), "RGBA")
        if mirror: img = img.transpose(Image.FLIP_LEFT_RIGHT)
        x0, y0, x1, y1 = CANNON_BOX
        art = fit(img, x1 - x0, y1 - y0)
        canvas = Image.new("RGBA", CANNON_CANVAS, (0, 0, 0, 0))
        canvas.alpha_composite(art, (x0 + (x1 - x0 - art.width) // 2, y1 - art.height))   # wheels on the same line
        canvas.save(os.path.join(OUT_GAME, f"Cannon_{name}.png"), optimize=True)
        card_with(img, card, board, True).save(os.path.join(OUT_SHOP, f"Cannon{name}_price.png"), optimize=True)
        card_with(img, card, board, False).save(os.path.join(OUT_SHOP, f"Cannon{name}.png"), optimize=True)
        print("  cannon", name)

    for name, src, mode in IMPACTS:
        img = Image.fromarray(trim(load(src, mode)), "RGBA")
        g = fit(img, 512, 512)
        g.save(os.path.join(OUT_GAME, f"Impact_{name}.png"), optimize=True)
        card_with(img, card, board, True).save(os.path.join(OUT_SHOP, f"Impact{name}_price.png"), optimize=True)
        card_with(img, card, board, False).save(os.path.join(OUT_SHOP, f"Impact{name}.png"), optimize=True)
        print("  impact", name)

    for name in ("Cannons", "Impacts"):
        b = Image.fromarray(trim(cut_white(os.path.join(SRC, f"Banner_{name}.png"), min_pocket=10**9)), "RGBA")
        b = fit(b, 1238, 492)
        b.save(os.path.join(OUT_SHOP, f"{name}_Banner.png"), optimize=True)
        print("  banner", name, b.size)


if __name__ == "__main__":
    main()
