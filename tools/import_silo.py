"""World 1's broken silo behind the cannon (2026-11-12, user: "insert the broken Silo to the bottom left of the screen,
behind the cannon").

    python tools/import_silo.py

Source: FarmFury_Technical/FarmFury_Artwork/MeadowRuins/FarmSilo.png (1920 square, white background). The white
connected to the border becomes transparent (the silo's own white paint survives - it is enclosed), a 1 px halo is
trimmed, only the largest piece is kept (the source has a faint frame line at its edges), cropped and saved 1024 tall
to Sprites/Environment/Cornfield/BrokenSilo.png (wired as CatapultLauncher._siloSprite by Wire Scene References).
"""
import os
import numpy as np
from PIL import Image
from scipy import ndimage

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SRC = os.path.join(os.path.dirname(ROOT), 'FarmFury_Technical', 'FarmFury_Artwork', 'MeadowRuins', 'FarmSilo.png')
OUT = os.path.join(ROOT, 'unity', 'Assets', 'Sprites', 'Environment', 'Cornfield', 'BrokenSilo.png')

rgb = np.asarray(Image.open(SRC).convert('RGB'))
white = (rgb >= 245).all(axis=2)
lab, _ = ndimage.label(white)
edge = np.unique(np.concatenate([lab[0], lab[-1], lab[:, 0], lab[:, -1]]))
alpha = np.where(np.isin(lab, edge[edge > 0]), 0, 255).astype(np.uint8)
alpha = ndimage.grey_erosion(alpha, size=2)
pieces, n = ndimage.label(alpha > 20)
if n > 1:
    sizes = ndimage.sum(np.ones_like(pieces), pieces, range(1, n + 1))
    alpha = np.where(pieces == 1 + int(np.argmax(sizes)), alpha, 0).astype(np.uint8)
im = Image.fromarray(np.dstack([rgb, alpha]), 'RGBA')
im = im.crop(im.getchannel('A').point(lambda a: 255 if a > 20 else 0).getbbox())
scale = 1024 / im.height
im = im.resize((round(im.width * scale), 1024), Image.LANCZOS)
im.save(OUT)
print('wrote', OUT, im.size)
