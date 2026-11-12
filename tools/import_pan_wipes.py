"""
Farm Fury - debris for the world-specific room-change wipes (PanWipe, 2026-11-12).

    python tools/import_pan_wipes.py

Cuts the separate pieces out of art that is already in the project, into unity/Assets/Sprites/Effects/PanWipe/:
  Leaf_<n>.png     the golden leaves of the corn-husk trail burst (UI/Franchise/Cosmetics/CornHuskTrail.png)
  Feather_<n>.png  the feathers of the feather impact (Cosmetics/Impact_Feathers.png)
World 1's hurricane gust blows them across the screen; World 5's bubbles reuse Sunken/Decor_Bubble + the fish.
"""
import os

import numpy as np
from PIL import Image
from scipy import ndimage

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SPR = os.path.join(ROOT, "unity", "Assets", "Sprites")
OUT = os.path.join(SPR, "Effects", "PanWipe")


def pieces(path, min_frac, max_count):
    """The separate opaque pieces of an image, biggest first: (rgba crop, area)."""
    a = np.asarray(Image.open(path).convert("RGBA"))
    solid = a[:, :, 3] > 40
    lab, n = ndimage.label(solid)
    area = ndimage.sum(solid, lab, index=np.arange(1, n + 1))
    keep = [i + 1 for i in np.argsort(area)[::-1] if area[i] >= area.max() * min_frac][:max_count]
    out = []
    for k in keep:
        ys, xs = np.nonzero(lab == k)
        y0, y1, x0, x1 = ys.min(), ys.max() + 1, xs.min(), xs.max() + 1
        crop = a[y0:y1, x0:x1].copy()
        crop[lab[y0:y1, x0:x1] != k, 3] = 0
        out.append(crop)
    return out


def main():
    os.makedirs(OUT, exist_ok=True)
    # Leaves: skip the big clump in the middle of the burst (the first, by far the largest piece); keep the loose ones.
    leaves = pieces(os.path.join(SPR, "UI", "Franchise", "Cosmetics", "CornHuskTrail.png"), 0.02, 14)[1:]
    leaves = [l for l in leaves if min(l.shape[:2]) >= 12][:10]
    for i, l in enumerate(leaves):
        Image.fromarray(l, "RGBA").save(os.path.join(OUT, f"Leaf_{i}.png"))
    feathers = pieces(os.path.join(SPR, "Cosmetics", "Impact_Feathers.png"), 0.15, 6)
    for i, f in enumerate(feathers):
        Image.fromarray(f, "RGBA").save(os.path.join(OUT, f"Feather_{i}.png"))
    print(f"wrote {len(leaves)} leaves, {len(feathers)} feathers to {os.path.normpath(OUT)}")


if __name__ == "__main__":
    main()
