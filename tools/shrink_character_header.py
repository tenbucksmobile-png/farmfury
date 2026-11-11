"""Shrink the baked-in "Character" lettering on the Character Select canvases (user 2026-11-11: "slightly minimise
the 'character' header - it is too big").

All nine <XX>_Character_Canvas_Wide.png (+ CharacterCanvas_Wide.png) carry the same Stampede lettering at the same
spot over different backdrops, so the lettering is found by per-pixel agreement across the canvases (low spread =
lettering, high spread = backdrop). Each canvas gets the lettering inpainted out and pasted back at SCALE, centred on
the same point. Originals are kept once in Canvas_originals~/ (the ~ hides it from Unity) and always used as the
source, so the script can be re-run with a different SCALE.

    python tools/shrink_character_header.py [--scale 0.72]
"""
import argparse
import glob
import os
import shutil

import cv2
import numpy as np

ROOT = os.path.join(os.path.dirname(__file__), "..", "unity", "Assets", "Sprites", "UI", "Franchise")
CANVAS = os.path.join(ROOT, "Canvas")
ORIG = os.path.join(ROOT, "Canvas_originals~")
BOX = (500, 50, 1120, 240)  # x0, y0, x1, y1 around the lettering (1600x720 canvases)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--scale", type=float, default=0.72)
    args = ap.parse_args()

    os.makedirs(ORIG, exist_ok=True)
    names = sorted(os.path.basename(p) for p in glob.glob(os.path.join(CANVAS, "*Character_Canvas_Wide.png"))
                   if "NewCharacter" not in p) + ["CharacterCanvas_Wide.png"]
    for n in names:
        if not os.path.exists(os.path.join(ORIG, n)):
            shutil.copy2(os.path.join(CANVAS, n), os.path.join(ORIG, n))

    imgs = [cv2.imread(os.path.join(ORIG, n), cv2.IMREAD_UNCHANGED) for n in names]
    x0, y0, x1, y1 = BOX
    # Mask from the bright-sky canvases, where the dark outline + drop shadow stand out (v < 150 near the face).
    bright = [i for i, n in enumerate(names) if n.split("_")[0] in ("CharacterCanvas", "DC", "SI", "WV")]
    votes = np.zeros((y1 - y0, x1 - x0), np.int32)
    for i in bright:
        hsv = cv2.cvtColor(np.ascontiguousarray(imgs[i][y0:y1, x0:x1, :3]), cv2.COLOR_BGR2HSV).astype(int)
        h, s, v = hsv[..., 0], hsv[..., 1], hsv[..., 2]
        fill = (s > 140) & (h >= 5) & (h <= 30) & (v > 150)          # the orange letter face
        near = cv2.dilate(fill.astype(np.uint8), np.ones((31, 31), np.uint8)) > 0
        votes += (fill | ((v < 150) & near)).astype(np.int32)         # + the dark outline / drop shadow
    keep = (votes >= len(bright) - 1).astype(np.uint8)                # consensus: drops each canvas's own strays
    n_lab, lab, stats, _ = cv2.connectedComponentsWithStats(keep, 8)
    big = np.zeros_like(keep)
    for i in range(1, n_lab):
        if stats[i, cv2.CC_STAT_AREA] > 1500:
            big[lab == i] = 1
    keep = cv2.morphologyEx(big, cv2.MORPH_CLOSE, np.ones((3, 3), np.uint8))
    keep = cv2.dilate(keep, np.ones((3, 3), np.uint8))               # anti-aliased rim
    # Lettering pixels from the black-sky canvas: its rim blends into black, matching the outline.
    src = names.index("SC_Character_Canvas_Wide.png") if "SC_Character_Canvas_Wide.png" in names else 0
    letters = imgs[src][y0:y1, x0:x1, :3]
    ys, xs = np.nonzero(keep)
    bx0, bx1, by0, by1 = xs.min(), xs.max() + 1, ys.min(), ys.max() + 1
    print(f"lettering bbox in canvas: x {x0 + bx0}-{x0 + bx1}, y {y0 + by0}-{y0 + by1}, {keep.sum()} px")

    cut = letters[by0:by1, bx0:bx1]
    alpha = cv2.GaussianBlur(keep[by0:by1, bx0:bx1].astype(np.float32), (3, 3), 0)
    w, h = cut.shape[1], cut.shape[0]
    sw, sh = max(1, round(w * args.scale)), max(1, round(h * args.scale))
    small = cv2.resize(cut, (sw, sh), interpolation=cv2.INTER_AREA).astype(np.float32)
    small_a = cv2.resize(alpha, (sw, sh), interpolation=cv2.INTER_AREA)[..., None]
    cx, cy = x0 + (bx0 + bx1) / 2, y0 + (by0 + by1) / 2
    cy -= (h - sh) * 0.35  # nudge up a touch so it keeps hugging the top edge
    px, py = int(round(cx - sw / 2)), int(round(cy - sh / 2))

    for n, im in zip(names, imgs):
        # Each canvas's own lettering (some sit a few px off the shared spot): face + anything clearly darker than
        # that canvas's sky around it, joined to the shared mask.
        hsv = cv2.cvtColor(np.ascontiguousarray(im[y0:y1, x0:x1, :3]), cv2.COLOR_BGR2HSV).astype(int)
        h, s, v = hsv[..., 0], hsv[..., 1], hsv[..., 2]
        fill = (s > 140) & (h >= 5) & (h <= 30) & (v > 150)
        near = cv2.dilate(fill.astype(np.uint8), np.ones((31, 31), np.uint8)) > 0
        ring = cv2.dilate(near.astype(np.uint8), np.ones((25, 25), np.uint8)).astype(bool) & ~near
        thr = min(150, 0.62 * float(np.median(v[ring])))
        own = (fill | ((v < thr) & near)).astype(np.uint8)
        n_lab, lab, stats, _ = cv2.connectedComponentsWithStats(own, 8)
        for i in range(1, n_lab):
            if stats[i, cv2.CC_STAT_AREA] < 1500:
                own[lab == i] = 0
        grow = cv2.dilate(np.maximum(keep, own), np.ones((15, 15), np.uint8))
        full = np.zeros(im.shape[:2], np.uint8)
        full[y0:y1, x0:x1] = grow * 255
        rgb = np.ascontiguousarray(im[..., :3])
        clean = cv2.inpaint(rgb, full, 9, cv2.INPAINT_TELEA)
        region = clean[py:py + sh, px:px + sw].astype(np.float32)
        clean[py:py + sh, px:px + sw] = (small * small_a + region * (1 - small_a)).astype(np.uint8)
        out = im.copy()
        out[..., :3] = clean
        cv2.imwrite(os.path.join(CANVAS, n), out)
        print("wrote", n)


if __name__ == "__main__":
    main()
