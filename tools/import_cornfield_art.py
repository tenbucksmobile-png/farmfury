"""
Farm Fury - World 1 cornfield backdrop import (2026-11-08).

Cuts the white backgrounds off the Kling cornfield pieces and writes game-ready sprites to
unity/Assets/Sprites/Environment/Cornfield/, then prints the measured anchor values that
EnvironmentDepthSystem / PlatformBase use (paste them into the C# constants if the art changes).

Sources (FarmFury_Technical/FarmFury_Artwork):
  Rough_Canvas/DistantBarn.png              -> CornfieldFar.png      (sky + hazy hills, barn, windmill, silos)
  MeadowRuins/Cornfield/MidCornfield_B.png  -> CornfieldMid.png      (hazy mid-distance corn band)
  MeadowRuins/Cornfield/CornWall_A.png      -> CornWall.png          (corn stalks, play depth)
                                            -> CornSoil.png          (grass lip + tilled soil = ground strip)
  MeadowRuins/Cornfield/PlatformWide_A/B    -> PlatformWide_A/B.png  (raised islands)

Usage (repo root):  python tools/import_cornfield_art.py
"""

import os
import shutil
import numpy as np
from PIL import Image
from scipy import ndimage

DESKTOP = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
ART = os.path.join(DESKTOP, "FarmFury_Technical", "FarmFury_Artwork")
SRC = os.path.join(ART, "MeadowRuins", "Cornfield")
OUT = os.path.join(os.path.dirname(__file__), "..", "unity", "Assets", "Sprites", "Environment", "Cornfield")


def cut_white(path, min_pocket=150, white=226, sat=26):
    """RGBA array with the border-connected white background made transparent (soft 2px edge)."""
    rgb = np.asarray(Image.open(path).convert("RGB")).astype(np.int16)
    mn, mx = rgb.min(axis=2), rgb.max(axis=2)
    near_white = (mn >= white) & (mx - mn <= sat)
    labels, n = ndimage.label(near_white)
    # Border-connected white, plus any enclosed white pocket bigger than min_pocket px (the gaps between corn
    # stalks are walled in by leaves and the grass lip; none of this art has large intended white areas).
    border = np.unique(np.concatenate([labels[0], labels[-1], labels[:, 0], labels[:, -1]]))
    sizes = ndimage.sum(near_white, labels, index=np.arange(1, n + 1))
    big = np.nonzero(sizes >= min_pocket)[0] + 1
    bg = np.isin(labels, np.union1d(border[border > 0], big))
    alpha = np.where(bg, 0, 255).astype(np.float32)
    # Feather: pixels next to the background fade by how white they are (kills the white halo).
    ring = ndimage.binary_dilation(bg, iterations=2) & ~bg
    fade = np.clip((250.0 - mn) / 60.0, 0.0, 1.0) * 255.0
    alpha[ring] = np.minimum(alpha[ring], fade[ring])
    return np.dstack([rgb.astype(np.uint8), alpha.astype(np.uint8)])


def trim(rgba, pad=2):
    ys, xs = np.nonzero(rgba[:, :, 3] > 8)
    y0, y1 = max(ys.min() - pad, 0), min(ys.max() + pad + 1, rgba.shape[0])
    x0, x1 = max(xs.min() - pad, 0), min(xs.max() + pad + 1, rgba.shape[1])
    return rgba[y0:y1, x0:x1]


def solid_top(rgba, x0, x1, frac=0.97):
    """Scanning up from the bottom: the top row of the solid band at the bottom (columns x0..x1 >= frac opaque)."""
    a = (rgba[:, x0:x1, 3] > 128).mean(axis=1) >= frac
    y = rgba.shape[0] - 1
    while y > 0 and not a[y]:
        y -= 1                      # skip any transparent rows under the art
    while y > 0 and a[y - 1]:
        y -= 1
    return y


def save(rgba, name):
    Image.fromarray(rgba, "RGBA").save(os.path.join(OUT, name), optimize=True)
    print(f"  {name}: {rgba.shape[1]}x{rgba.shape[0]}")


def main():
    os.makedirs(OUT, exist_ok=True)
    print("Writing to", os.path.normpath(OUT))

    # Far: the painting as-is (opaque, carries the sky).
    shutil.copyfile(os.path.join(ART, "Rough_Canvas", "DistantBarn.png"), os.path.join(OUT, "CornfieldFar.png"))
    print("  CornfieldFar.png: copied (barn base measured at v=0.052 from the bottom)")

    # Mid corn band: cut out, trimmed, edges shaved so mirrored tiles meet cleanly.
    mid = trim(cut_white(os.path.join(SRC, "MidCornfield_B.png")))
    mid = mid[:, 12:-12]
    save(mid, "CornfieldMid.png")

    # Corn wall + soil: shave the ragged left/right ends, then split at the grass lip.
    wall = cut_white(os.path.join(SRC, "CornWall_A.png"))
    wall = trim(wall)
    w = wall.shape[1]
    wall = wall[:, int(w * 0.03):int(w * 0.975)]
    w = wall.shape[1]
    grass_top = solid_top(wall, int(w * 0.05), int(w * 0.95))      # first fully solid row = grass surface
    corn = trim(wall[:grass_top + 30])                             # stalk bases tuck under the soil strip
    soil = wall[grass_top - 45:]                                   # a little grass fringe above the surface
    save(corn, "CornWall.png")
    save(soil, "CornSoil.png")

    # Foreground silhouettes: clumps of the corn wall cut at its sparsest columns (u 0.208 / 0.774, measured),
    # near-black, with the inner (on-screen) edge faded so no hard cut shows. The outer edge sits off-screen.
    def silhouette(u0, u1, fade_left):
        w = corn.shape[1]
        c = corn[:, int(w * u0):int(w * u1)].astype(np.float32)
        c[:, :, :3] = c[:, :, :3] * 0.07 + np.array([4, 7, 3], np.float32)
        n = c.shape[1]
        t = np.linspace(0.0, 1.0, n)
        ramp = np.clip((t if fade_left else 1.0 - t) / 0.28, 0.0, 1.0)
        ramp = ramp * ramp * (3 - 2 * ramp)
        c[:, :, 3] *= ramp[None, :]
        return trim(c.clip(0, 255).astype(np.uint8))
    save(silhouette(0.0, 0.208, fade_left=False), "CornSilhouetteL.png")
    save(silhouette(0.55, 0.774, fade_left=True), "CornSilhouetteR.png")
    soil_ground_uv = (soil.shape[0] - 45 - 12) / soil.shape[0]     # surface ~12 px into the solid grass
    print(f"  CornSoil ground line UV (from bottom) = {soil_ground_uv:.3f}")

    # Sky fly-bys (SkyFlyby.cs): the biplane prop art and Stampede's robot scout drone, trimmed.
    for src, name in ((os.path.join(ART, "MeadowRuins", "Plane.png"), "FlybyPlane.png"),
                      (os.path.join(DESKTOP, "FarmFury_Stampede", "Assets", "_Project", "Sprites", "Robots", "Drone.png"),
                       "FlybyDrone.png")):
        save(trim(np.asarray(Image.open(src).convert("RGBA")).copy()), name)

    for tag in ("A", "B"):
        plat = trim(cut_white(os.path.join(SRC, f"PlatformWide_{tag}.png")))
        h, w = plat.shape[:2]
        top = solid_top(plat[: int(h * 0.6)], int(w * 0.25), int(w * 0.75), frac=0.99)
        save(plat, f"PlatformWide_{tag}.png")
        # The walkable surface sits a few px into the solid grass top.
        surface_uv = (h - (top + 10)) / h
        print(f"  PlatformWide_{tag} surface UV (from bottom) = {surface_uv:.3f}, aspect w/h = {w / h:.3f}")


# ── Mounts (2026-11-08): ground-standing hill / mesa / scaffold / wagon ─────────────────────────────────────────
MOUNTS = {  # name -> (source file, kind)  kind: "solid" = whole body is collider, "deck" = only the top deck
    "Hill": ("Mount_Hill.png", "solid"), "Mesa": ("Mount_Mesa.png", "solid"),
    "Scaffold": ("Mount_Scaffold.png", "deck"), "Wagon": ("Mount_Wagon.png", "deck"),
    "Ramp": ("Mount_Ramp.png", "profile"),
}
CUT = {"Wagon": dict(white=175, sat=22)}   # the wagon's pale grey drop shadow goes with the background


def strip_ground_line(rgba):
    """Drop thin full-width lines/shadows hugging the bottom (wider than the object's own feet)."""
    a = rgba[:, :, 3] > 128
    y = a.shape[0] - 1
    while y > 0 and a[y].mean() > 0.5:
        rgba[y, :, 3] = 0; y -= 1
    return rgba


def measure_mount(rgba, kind):
    a = rgba[:, :, 3] > 128
    h, w = a.shape
    c0, c1 = int(w * 0.42), int(w * 0.58)
    rows = np.nonzero(a[:, c0:c1].mean(axis=1) >= 0.9)[0]
    top = int(rows[0])                                   # first solid row in the middle = walking surface
    surface_uv = (h - (top + max(2, h // 200))) / h

    def extent(y):
        xs = np.nonzero(a[min(max(y, 0), h - 1)])[0]
        return (xs.min() / w, (xs.max() + 1) / w) if len(xs) else (0.45, 0.55)

    boxes = []
    if kind == "profile":
        # Polygon traced along the top: for each sampled column, the first row of a solid run (skips grass blades).
        pts, n = [], 24
        for i in range(n + 1):
            x = min(w - 1, int((0.02 + 0.96 * i / n) * w))
            col = a[:, x]
            y = 0
            while y < h - 8 and not col[y:y + 8].all(): y += 1
            pts.append((x / w, (h - y) / h))
        # Flatten the plateau to its median height (grass tufts poke above it) so a wagon sits level on top.
        level = float(np.median([q[1] for q in pts if q[0] < 0.36]))
        pts = [(x, min(y, level)) for x, y in pts]
        left, right = pts[0][0], pts[-1][0]
        poly = [(left, 0.0)] + pts + [(right, 0.0)]
        # Plateau = the flat stretch at the top-left: columns within 3% of the highest point.
        top_uv = max(q[1] for q in pts)
        flat = [q[0] for q in pts if q[1] >= top_uv - 0.03]
        return top_uv, [], (min(flat), max(flat)), poly
    if kind == "deck":
        y = top
        while y < h and a[y, c0:c1].mean() > 0.8: y += 1   # deck thickness
        x0, x1 = extent(top + (y - top) // 2)
        boxes.append((x0 + 0.01, x1 - 0.01, (h - y) / h, surface_uv))
    else:
        bands = 3
        span = h - top
        for b in range(bands):
            ya, yb = top + span * b // bands, top + span * (b + 1) // bands
            ex = [extent(yy) for yy in range(ya, yb, max(1, (yb - ya) // 6))]
            x0, x1 = max(e[0] for e in ex), min(e[1] for e in ex)   # inner (conservative) extent of the band
            boxes.append((x0, x1, (h - yb) / h, surface_uv if b == 0 else (h - ya) / h))
    flat = extent(top + max(2, h // 100))
    if kind == "solid":   # the walking surface is the whole flat top (grass overhangs the rock below it)
        boxes[0] = (flat[0] + 0.01, flat[1] - 0.01, boxes[0][2], boxes[0][3])
    return surface_uv, boxes, flat, None


def import_mounts():
    import json
    data, cs = {}, []
    for name, (src, kind) in MOUNTS.items():
        path = os.path.join(SRC, src)
        if not os.path.exists(path): print("  missing", src); continue
        rgba = trim(strip_ground_line(cut_white(path, **CUT.get(name, {}))))
        uv, boxes, flat, poly = measure_mount(rgba, kind)
        save(rgba, f"Mount_{name}.png")
        h, w = rgba.shape[:2]
        data[name] = dict(uv=uv, aspect=w / h, boxes=boxes, flat=flat, poly=poly)
        print(f"  {name}: surfaceUV {uv:.3f}, flat top x {flat[0]:.2f}-{flat[1]:.2f}, {len(boxes)} solids")
        v4 = ", ".join(f"new Vector4({b[0]:.3f}f, {b[1]:.3f}f, {b[2]:.3f}f, {b[3]:.3f}f)" for b in boxes)
        cs.append(f"    public const float Mount{name}UV = {uv:.3f}f;\n"
                  f"    public static readonly Vector4[] Mount{name}Solids = {{ {v4} }};")
        if poly:
            v2 = ", ".join(f"new Vector2({px:.3f}f, {py:.3f}f)" for px, py in poly)
            cs.append(f"    public static readonly Vector2[] Mount{name}Poly = {{ {v2} }};")
    json.dump(data, open(os.path.join(os.path.dirname(__file__), "mounts.json"), "w"), indent=1)
    out = os.path.join(os.path.dirname(__file__), "..", "unity", "Assets", "Scripts", "Level", "MountMeasurements.cs")
    open(out, "w", encoding="utf-8").write(
        "// GENERATED by tools/import_cornfield_art.py from the mount art - re-run it if the art changes.\n"
        "using UnityEngine;\n\npublic static class MountMeasurements\n{\n" + "\n".join(cs) + "\n}\n")
    print("  wrote MountMeasurements.cs + tools/mounts.json")


# ── Ambience + contraptions (2026-11-09): landform layers, clouds, sunflower, hay hoist ─────────────────────────
AMB = os.path.join(SRC, "Ambience")
HAZE = np.array([222, 214, 190], np.float32)    # pale golden haze, matches the far painting's distant hills


def haze(rgba, amount, blur=0):
    """Atmospheric perspective baked in: colours pulled toward the haze colour, optionally softened."""
    out = rgba.astype(np.float32)
    out[:, :, :3] = out[:, :, :3] * (1 - amount) + HAZE * amount
    im = Image.fromarray(out.clip(0, 255).astype(np.uint8), "RGBA")
    if blur:
        from PIL import ImageFilter
        im = im.filter(ImageFilter.GaussianBlur(blur))
    return np.asarray(im).copy()


def crop_bottom_band(rgba, frac, fade=0.18):
    """Drop the flat ground strip the generator painted under the landform, then fade the new bottom edge out so
    it melts into the mist instead of ending on a hard line."""
    h = rgba.shape[0]
    out = trim(rgba[: int(h * (1 - frac))]).astype(np.float32)
    n = out.shape[0]
    ramp = np.clip((n - 1 - np.arange(n)) / max(1.0, n * fade), 0, 1)
    out[:, :, 3] *= (ramp * ramp * (3 - 2 * ramp))[:, None]
    return out.astype(np.uint8)


def key_blue(path):
    """Clouds painted on flat blue: alpha = distance from the blue, colour un-mixed from it."""
    rgb = np.asarray(Image.open(path).convert("RGB")).astype(np.float32)
    bg = np.median(np.concatenate([rgb[:20].reshape(-1, 3), rgb[-20:].reshape(-1, 3)]), axis=0)
    # How far toward white each pixel is from the background, along the blue->white axis.
    span = np.array([255, 255, 255], np.float32) - bg
    a = ((rgb - bg) * span).sum(axis=2) / (span * span).sum()
    a = np.clip((a - 0.12) / 0.6, 0, 1)
    col = np.where(a[..., None] > 0.02, (rgb - bg * (1 - a[..., None])) / np.maximum(a[..., None], 0.02), 255)
    return np.dstack([col.clip(0, 255), a * 255]).astype(np.uint8)


def import_ambience():
    def pick(name):
        return os.path.join(AMB, name)
    ridges = crop_bottom_band(trim(cut_white(pick("FarRidges_A.png"))), 0.085)
    save(haze(ridges, 0.62, blur=3), "CornRidges.png")
    hills = crop_bottom_band(trim(cut_white(pick("NearHills_B.png"))), 0.05)
    save(haze(hills, 0.22, blur=1), "CornHills.png")

    clouds = key_blue(pick("Clouds.png"))
    labels, n = ndimage.label(clouds[:, :, 3] > 20)
    k = 0
    for sl in ndimage.find_objects(labels):
        if sl is None: continue
        c = clouds[sl]
        if c.shape[0] < 80 or c.shape[1] < 120: continue
        save(trim(c, pad=6), f"Cloud_{k}.png"); k += 1

    flower = trim(cut_white(pick("Sunflower_B.png")))
    save(flower, "Sunflower.png")
    bucket = cut_white(pick("Bucket.png"))
    # The grey drop shadow under the tub: light, unsaturated pixels in the bottom rows go.
    rgb = bucket[:, :, :3].astype(np.int16)
    mn, mx = rgb.min(axis=2), rgb.max(axis=2)
    ys = np.nonzero(bucket[:, :, 3] > 128)[0]
    y0 = int(ys.max() - (ys.max() - ys.min()) * 0.12)
    shadow = (mx - mn < 24) & (mn > 120)
    shadow[:y0] = False
    bucket[shadow, 3] = 0
    bucket = trim(bucket)
    save(bucket, "HoistBucket.png")

    crane = trim(cut_white(pick("HoistCrane.png")))
    save(crane, "HoistCrane.png")
    a = crane[:, :, 3] > 128
    h, w = a.shape
    # Pulley: the lowest opaque pixels in the right-most fifth (the wheel's rim) — the rope leaves from there.
    right = a[:, int(w * 0.8):]
    ys, xs = np.nonzero(right)
    bottom = ys.max()
    wheel_x = (xs[ys >= bottom - 40].mean() + int(w * 0.8)) / w
    # Post: the opaque run in the left half at 55% height.
    row = a[int(h * 0.55), : w // 2]
    px = np.nonzero(row)[0]
    print(f"  HoistCrane pulley UV = ({wheel_x:.3f}, {(h - bottom) / h:.3f}), post centre x = {px.mean() / w:.3f}, aspect {w / h:.3f}")
    ba = bucket[:, :, 3] > 128
    print(f"  HoistBucket aspect {bucket.shape[1] / bucket.shape[0]:.3f}; tub top (rim) ~ handle measured by eye")

    # Sunflower seed (procedural): a small dark striped teardrop.
    N = 96
    yy, xx = np.mgrid[0:N, 0:N].astype(np.float32) / N - 0.5
    shape = (xx / 0.24) ** 2 + ((yy + 0.08 * np.sign(yy) * 0) / (0.44 - 0.18 * (yy + 0.5))) ** 2 <= 1.0
    seed = np.zeros((N, N, 4), np.uint8)
    stripe = (np.abs(xx) < 0.05) | (np.abs(np.abs(xx) - 0.15) < 0.03)
    seed[shape] = (52, 44, 40, 255)
    seed[shape & stripe] = (226, 222, 205, 255)
    edge = shape & ~ndimage.binary_erosion(shape, iterations=4)
    seed[edge] = (20, 16, 14, 255)
    save(trim(seed), "SunflowerSeed.png")


if __name__ == "__main__":
    import sys as _sys
    if "--ambience" not in _sys.argv:
        main()
        import_mounts()
    import_ambience()
