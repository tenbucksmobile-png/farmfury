"""
Farm Fury - World 2 (Frozen Tundra) framed-level art import (2026-11-09).

The frozen counterpart of import_cornfield_art.py. Kling pieces were generated on solid magenta (white
backgrounds can't be cut from snow and ice) in FarmFury_Technical/FarmFury_Artwork/FrozenTundra/Framed/;
this keys the magenta out (with despill on the soft edge), trims, bakes the distance haze into the far
layers, splits the frozen corn wall into stalks + snow ground strip, bakes the corner silhouettes, measures
the mounts (writes Scripts/Level/MountMeasurementsW2.cs + tools/mounts_w2.json) and pads the boss poses to
the Commander's 500 px convention so the boss scale in the level designer carries over.

Output: unity/Assets/Sprites/Environment/Frozen/ and unity/Assets/Sprites/Enemies/Robot/FrostTyrant*.png

Usage (repo root):  python tools/import_frozen_art.py
"""
import json
import os

import numpy as np
from PIL import Image, ImageFilter
from scipy import ndimage

from import_cornfield_art import trim, solid_top, measure_mount, strip_ground_line, DESKTOP

HERE = os.path.dirname(os.path.abspath(__file__))
SRC = os.path.join(DESKTOP, "FarmFury_Technical", "FarmFury_Artwork", "FrozenTundra", "Framed")
UNITY = os.path.join(HERE, "..", "unity", "Assets")
OUT = os.path.join(UNITY, "Sprites", "Environment", "Frozen")
ROBOTS = os.path.join(UNITY, "Sprites", "Enemies", "Robot")
HAZE = np.array([150, 172, 220], np.float32)   # cold night-blue haze, matches the aurora sky's lower band


def key_magenta(path, min_pocket=150):
    """RGBA with the (border-connected, plus large enclosed) magenta background removed and the edge despilled."""
    rgb = np.asarray(Image.open(path).convert("RGB")).astype(np.float32)
    r, g, b = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    m = np.minimum(r, b) - g                       # magenta-ness: red and blue both well above green
    bgish = (m > 55) & (r > 120) & (b > 50)
    labels, n = ndimage.label(bgish)
    border = np.unique(np.concatenate([labels[0], labels[-1], labels[:, 0], labels[:, -1]]))
    sizes = ndimage.sum(bgish, labels, index=np.arange(1, n + 1))
    big = np.nonzero(sizes >= min_pocket)[0] + 1
    bg = np.isin(labels, np.union1d(border[border > 0], big))
    alpha = np.where(bg, 0.0, 255.0)
    ring = ndimage.binary_dilation(bg, iterations=3) & ~bg
    soft = np.clip(1.0 - (m - 15.0) / 60.0, 0.0, 1.0) * 255.0
    alpha[ring] = np.minimum(alpha[ring], soft[ring])
    # Despill: pull the magenta tint out of the edge pixels.
    spill = np.clip(m / 120.0, 0.0, 1.0) * ring
    rgb[..., 0] -= spill * np.maximum(r - g, 0) * 0.85
    rgb[..., 2] -= spill * np.maximum(b - g, 0) * 0.85
    # Drop stray specks (generator "stars" and dust on the backdrop) so trimming hugs the real art.
    solid = alpha > 40
    lab, k = ndimage.label(solid)
    if k:
        area = ndimage.sum(solid, lab, index=np.arange(1, k + 1))
        tiny = np.nonzero(area < max(400, solid.sum() * 0.0005))[0] + 1
        alpha[np.isin(lab, tiny)] = 0
    return np.dstack([rgb.clip(0, 255), alpha]).astype(np.uint8)


def haze(rgba, amount, blur=0):
    out = rgba.astype(np.float32)
    out[:, :, :3] = out[:, :, :3] * (1 - amount) + HAZE * amount
    im = Image.fromarray(out.clip(0, 255).astype(np.uint8), "RGBA")
    if blur:
        im = im.filter(ImageFilter.GaussianBlur(blur))
    return np.asarray(im).copy()


def crop_bottom(rgba, frac, fade=0.18):
    h = rgba.shape[0]
    out = trim(rgba[: int(h * (1 - frac))]).astype(np.float32)
    n = out.shape[0]
    ramp = np.clip((n - 1 - np.arange(n)) / max(1.0, n * fade), 0, 1)
    out[:, :, 3] *= (ramp * ramp * (3 - 2 * ramp))[:, None]
    return out.astype(np.uint8)


def save(rgba, folder, name):
    os.makedirs(folder, exist_ok=True)
    Image.fromarray(rgba, "RGBA").save(os.path.join(folder, name), optimize=True)
    print(f"  {name}: {rgba.shape[1]}x{rgba.shape[0]}")


def pad_to(rgba, size, box):
    """Place trimmed art in a size x size canvas so its bounding box lands on box = (x0, y0, x1, y1) (fit, centred
    horizontally in the box, feet on the box bottom)."""
    x0, y0, x1, y1 = box
    h, w = rgba.shape[:2]
    s = min((x1 - x0) / w, (y1 - y0) / h)
    im = Image.fromarray(rgba, "RGBA").resize((max(1, int(w * s)), max(1, int(h * s))), Image.LANCZOS)
    canvas = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    cx = (x0 + x1) // 2 - im.width // 2
    canvas.paste(im, (cx, y1 - im.height), im)
    return np.asarray(canvas).copy()


def main():
    p = lambda n: os.path.join(SRC, n)
    print("Writing to", os.path.normpath(OUT))

    # Landforms: far glaciers (hazed, behind the veil) and the near frozen farm hills.
    ridges = crop_bottom(trim(key_magenta(p("W2Ridges_A.png"))), 0.07)
    save(haze(ridges, 0.45, blur=2), OUT, "FrozenRidges.png")
    hills = crop_bottom(trim(key_magenta(p("W2Hills_B.png"))), 0.06)
    save(haze(hills, 0.15, blur=1), OUT, "FrozenHills.png")

    # Mid frozen field: keyed, trimmed, edges shaved so mirrored tiles meet.
    mid = trim(key_magenta(p("W2Mid.png")))
    mid = mid[:, 12:-12]
    save(haze(mid, 0.12), OUT, "FrozenMid.png")

    # Frozen corn wall + snow ground: split at the snow lip like the corn wall.
    wall = trim(key_magenta(p("W2Wall.png")))
    w = wall.shape[1]
    wall = wall[:, int(w * 0.01):int(w * 0.99)]
    w = wall.shape[1]
    ground_top = solid_top(wall, int(w * 0.05), int(w * 0.95))
    corn = trim(wall[:ground_top + 30])
    soil = wall[ground_top - 40:]
    save(corn, OUT, "FrozenWall.png")
    save(soil, OUT, "FrozenSoil.png")
    print(f"  FrozenSoil ground line UV (from bottom) = {(soil.shape[0] - 40 - 12) / soil.shape[0]:.3f}")

    # Corner silhouettes: frozen corn clumps baked to deep night navy, inner edge faded.
    def silhouette(u0, u1, fade_left):
        cw = corn.shape[1]
        c = corn[:, int(cw * u0):int(cw * u1)].astype(np.float32)
        c[:, :, :3] = c[:, :, :3] * 0.06 + np.array([8, 12, 30], np.float32)
        n = c.shape[1]
        t = np.linspace(0.0, 1.0, n)
        ramp = np.clip((t if fade_left else 1.0 - t) / 0.28, 0.0, 1.0)
        c[:, :, 3] *= (ramp * ramp * (3 - 2 * ramp))[None, :]
        return trim(c.clip(0, 255).astype(np.uint8))
    save(silhouette(0.0, 0.2, fade_left=False), OUT, "FrozenSilhouetteL.png")
    save(silhouette(0.6, 0.8, fade_left=True), OUT, "FrozenSilhouetteR.png")

    # Snow-capped bale: padded to Haybail.png's 500 px canvas (bale rows 63..480, full width) so the cannon mount
    # and the Haybale block reuse their constants unchanged.
    bale = trim(strip_ground_line(key_magenta(p("W2Bale.png"))))
    save(pad_to(bale, 500, (0, 63, 500, 480)), OUT, "FrozenBale.png")

    # Frosted crane (hoist) + its measurements (rope anchor under the arm tip, post centre).
    crane = trim(key_magenta(p("W2Crane.png")))
    save(crane, OUT, "FrozenCrane.png")
    a = crane[:, :, 3] > 128
    h, w = a.shape
    for fx in (0.9, 0.93):
        col = np.nonzero(a[:, int(w * fx)])[0]
        runs = np.split(col, np.nonzero(np.diff(col) > 1)[0] + 1)
        print(f"  FrozenCrane column {fx}: opaque runs (from bottom) {[(round((h - r[-1]) / h, 3), round((h - r[0]) / h, 3)) for r in runs]}")
    row = np.nonzero(a[int(h * 0.75), : w // 2])[0]
    print(f"  FrozenCrane post centre x = {row.mean() / w:.3f}, aspect {w / h:.3f}")

    # The boss: four poses padded like Commander.png (500 px, body bbox x 65..449, y 23..469).
    # All poses share the idle pose's scale and feet line (one source framing), so the boss doesn't change size
    # while its taunt loop cycles; the explosion is fitted to the canvas.
    idle = trim(strip_ground_line(key_magenta(p("W2Boss_A.png"))))
    save(pad_to(idle, 500, (65, 23, 449, 469)), ROBOTS, "FrostTyrant.png")
    ih, iw = idle.shape[:2]
    s = min((449 - 65) / iw, (469 - 23) / ih)
    for src, name in (("W2Boss_Alert.png", "FrostTyrant_Alert.png"), ("W2Boss_Hit.png", "FrostTyrant_Hit.png")):
        if not os.path.exists(p(src)):
            print("  missing", src); continue
        body = trim(strip_ground_line(key_magenta(p(src))))
        im = Image.fromarray(body, "RGBA").resize((max(1, int(body.shape[1] * s)), max(1, int(body.shape[0] * s))), Image.LANCZOS)
        canvas = Image.new("RGBA", (500, 500), (0, 0, 0, 0))
        canvas.paste(im, ((65 + 449) // 2 - im.width // 2, 469 - im.height), im)
        save(np.asarray(canvas).copy(), ROBOTS, name)
    if os.path.exists(p("W2Boss_Explode.png")):
        save(pad_to(trim(key_magenta(p("W2Boss_Explode.png"))), 500, (20, 10, 480, 480)), ROBOTS, "FrostTyrant_Explode.png")

    # Construction pieces for frozen towers (World 2 block reskin, 2026-11-09): frosted plank and post, ice slab.
    # The generator drew the plank and slab short (about 3:1); towers span them at 6-8:1, so lengthen them here by
    # repeating the middle section (alternately mirrored, so the joins are invisible) between the two end caps.
    def lengthen(rgba, aspect, cap=0.22):
        h, w = rgba.shape[:2]
        c = int(w * cap)
        left, mid, right = rgba[:, :c], rgba[:, c:w - c], rgba[:, w - c:]
        need = int(aspect * h) - 2 * c
        parts, k = [], 0
        while sum(x.shape[1] for x in parts) < need:
            parts.append(mid if k % 2 == 0 else mid[:, ::-1]); k += 1
        body = np.concatenate(parts, axis=1)[:, :max(need, 1)]
        return np.concatenate([left, body, right], axis=1)
    for src, name, aspect in (("W2Plank.png", "FrostPlank.png", 7.5), ("W2Post.png", "FrostPost.png", 0),
                              ("W2IceSlab.png", "IceSlab.png", 6.0)):
        if os.path.exists(p(src)):
            art = trim(strip_ground_line(key_magenta(p(src))))
            save(lengthen(art, aspect) if aspect else art, OUT, name)

    import_mounts()


# Frozen mounts, same kinds as World 1's (LevelLoader mount variants 0-6 for World 2 levels).
MOUNTS = {  # name -> (source, kind, flip)
    "Floe": ("W2Floe.png", "island", False),
    "IcePillar": ("W2Pillar.png", "solid", False),
    "FrostScaffold": ("W2Scaffold.png", "deck", False),
    "Sled": ("W2Sled.png", "deck", True),          # mirrored: the curled runner tips lead down the ramp (+x)
    "SnowRamp": ("W2Ramp.png", "profile", False),
}


def import_mounts():
    data, cs = {}, []
    for name, (src, kind, flip) in MOUNTS.items():
        path = os.path.join(SRC, src)
        if not os.path.exists(path):
            print("  missing", src); continue
        rgba = trim(strip_ground_line(key_magenta(path)))
        if flip:
            rgba = rgba[:, ::-1].copy()
        save(rgba, OUT, f"Mount_{name}.png")
        h, w = rgba.shape[:2]
        if kind == "island":
            # Walkable surface a few px into the solid snow top; the slab under it is the collider.
            top = solid_top(rgba[: int(h * 0.6)], int(w * 0.25), int(w * 0.75), frac=0.99)
            uv = (h - (top + 10)) / h
            boxes, flat, poly = [(0.04, 0.96, 0.25, uv)], (0.04, 0.96), None
        else:
            uv, boxes, flat, poly = measure_mount(rgba, kind)
        extra = {}
        if name == "Sled":
            # Runner skids: two small circles where the runners touch the bottom (front and back).
            a = rgba[:, :, 3] > 128
            # Lowest opaque row per column; the runners rest where that is within 4% of the very bottom.
            low = np.array([np.nonzero(a[:, x])[0].max() if a[:, x].any() else -1 for x in range(w)])
            xs = np.nonzero(low >= low.max() - 0.04 * h)[0]
            x0, x1 = xs.min() / w, xs.max() / w
            # The runner's bottom edge is slightly tilted in the art, so the measured contact stretch is short
            # (x0..x1); the skids go at the runner's rear and front by eye (the curl is at the right after flipping).
            r = 0.09
            extra["skids"] = [(0.16, r, r), (0.74, r, r)]
        data[name] = dict(uv=uv, aspect=w / h, boxes=boxes, flat=flat, poly=poly, **extra)
        print(f"  {name}: surfaceUV {uv:.3f}, flat top x {flat[0]:.2f}-{flat[1]:.2f}, {len(boxes)} solids")
        v4 = ", ".join(f"new Vector4({b[0]:.3f}f, {b[1]:.3f}f, {b[2]:.3f}f, {b[3]:.3f}f)" for b in boxes)
        cs.append(f"    public const float {name}UV = {uv:.3f}f;\n"
                  f"    public static readonly Vector4[] {name}Solids = {{ {v4} }};")
        if poly:
            v2 = ", ".join(f"new Vector2({px:.3f}f, {py:.3f}f)" for px, py in poly)
            cs.append(f"    public static readonly Vector2[] {name}Poly = {{ {v2} }};")
        if "skids" in extra:
            v3 = ", ".join(f"new Vector3({x:.3f}f, {y:.3f}f, {r:.3f}f)" for x, y, r in extra["skids"])
            cs.append(f"    public static readonly Vector3[] {name}Skids = {{ {v3} }};")
    json.dump(data, open(os.path.join(HERE, "mounts_w2.json"), "w"), indent=1)
    out = os.path.join(UNITY, "Scripts", "Level", "MountMeasurementsW2.cs")
    open(out, "w", encoding="utf-8").write(
        "// GENERATED by tools/import_frozen_art.py from the frozen mount art - re-run it if the art changes.\n"
        "using UnityEngine;\n\npublic static class MountMeasurementsW2\n{\n" + "\n".join(cs) + "\n}\n")
    print("  wrote MountMeasurementsW2.cs + tools/mounts_w2.json")


if __name__ == "__main__":
    main()
