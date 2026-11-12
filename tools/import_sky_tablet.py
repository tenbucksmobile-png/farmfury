"""World 4's landmark behind the cannon (2026-11-12, user: "on sky islands insert this image bottom left of the cannon
(as per other worlds)"): the rune tablet in the clouds, cut from FarmFury_Artwork/SkyIsland/SkyTemple.png (2048 px, white
background) to Sprites/Environment/Sky/SkyTablet.png (wired as CatapultLauncher._tabletBackSprite).

    python tools/import_sky_tablet.py
"""
import os
import numpy as np
from PIL import Image
from scipy import ndimage

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SRC = os.path.join(os.path.dirname(ROOT), "FarmFury_Technical", "FarmFury_Artwork", "SkyIsland", "SkyTemple.png")
OUT = os.path.join(ROOT, "unity", "Assets", "Sprites", "Environment", "Sky", "SkyTablet.png")

rgb = np.asarray(Image.open(SRC).convert("RGB"))
white = (rgb >= 245).all(axis=2)
lab, _ = ndimage.label(white)
edge = np.unique(np.concatenate([lab[0], lab[-1], lab[:, 0], lab[:, -1]]))
alpha = np.where(np.isin(lab, edge[edge > 0]), 0, 255).astype(np.uint8)
alpha = ndimage.grey_erosion(alpha, size=2)
pieces, n = ndimage.label(alpha > 20)
if n > 1:   # the tablet and its cloud puffs (separate pieces), not stray specks
    sizes = ndimage.sum(np.ones_like(pieces), pieces, range(1, n + 1))
    alpha = np.where(np.isin(pieces, 1 + np.nonzero(sizes >= sizes.max() * 0.03)[0]), alpha, 0).astype(np.uint8)
im = Image.fromarray(np.dstack([rgb, alpha]), "RGBA")
im = im.crop(im.getchannel("A").point(lambda a: 255 if a > 20 else 0).getbbox())
im = im.resize((round(im.width * 1024 / im.height), 1024), Image.LANCZOS)
im.save(OUT)
print("wrote", OUT, im.size)
