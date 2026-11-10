"""
Farm Fury - World 3 (Watermill Village) framed-level art import (2026-11-10).

The watermill counterpart of import_frozen_art.py. Reuses existing art first:
  * FarmFury_Stampede's Watermill set: WV_Parallax_Far (sky + river valley), WV_Parallax_Mid (the village band:
    watermill, cottages, bridge), WV_Parallax_Near (bulrushes and ferns), the Mill Stone Mauler boss poses.
  * FarmFury_Artwork/WatermillVillage: FishingBoat (the rolling mount), Millstone (rolling contraption + hoist load).
Kling filled the gaps (kling-image-v3_0_omni i2i, 2 credits each, on magenta, each shaped after its World 1/2
counterpart and styled after the WatermillVillage props) in FarmFury_Artwork/WatermillVillage/Framed/: the Mill
Cannon, mill timber post + plank, river-stone block / pillar / lintel slab, flour sacks, the sack-hoist crane, the
village water pump, the river islet / bridge pier / fishing jetty / boat slipway mounts, the riverbank soil strip and
the distant hills.

Output: unity/Assets/Sprites/Environment/Watermill/, Environment/Launchers/MillCannon.png,
        Enemies/Robot/MillMauler*.png, Scripts/Level/MountMeasurementsW3.cs, tools/mounts_w3.json

Usage (repo root):  python tools/import_watermill_art.py
"""
import json
import os

import numpy as np
from PIL import Image
from scipy import ndimage

from import_cornfield_art import trim, solid_top, measure_mount, strip_ground_line, cut_white, DESKTOP
from import_frozen_art import key_magenta, haze, crop_bottom, pad_to

HERE = os.path.dirname(os.path.abspath(__file__))
ART = os.path.join(DESKTOP, "FarmFury_Technical", "FarmFury_Artwork", "WatermillVillage")
SRC = os.path.join(ART, "Framed")
STAMPEDE = os.path.join(DESKTOP, "FarmFury_Stampede", "Assets", "_Project", "Sprites")
UNITY = os.path.join(HERE, "..", "unity", "Assets")
OUT = os.path.join(UNITY, "Sprites", "Environment", "Watermill")
ROBOTS = os.path.join(UNITY, "Sprites", "Enemies", "Robot")
LAUNCHERS = os.path.join(UNITY, "Sprites", "Environment", "Launchers")
HAZE_W3 = np.array([196, 214, 226], np.float32)   # soft blue-green valley haze (the far painting's horizon)


def save(rgba, folder, name):
    os.makedirs(folder, exist_ok=True)
    Image.fromarray(rgba, "RGBA").save(os.path.join(folder, name), optimize=True)
    print(f"  {name}: {rgba.shape[1]}x{rgba.shape[0]}")


def load_rgba(path):
    return np.asarray(Image.open(path).convert("RGBA")).copy()


def haze_w3(rgba, amount):
    out = rgba.astype(np.float32)
    out[:, :, :3] = out[:, :, :3] * (1 - amount) + HAZE_W3 * amount
    return out.clip(0, 255).astype(np.uint8)


def defringe_red(rgba, iterations=6):
    """The slab came back on crimson, not magenta: drop red-dominant pixels along the edge (the leftover fringe)."""
    out = rgba.copy()
    rgb = out[:, :, :3].astype(np.int16)
    reddish = (rgb[:, :, 0] - rgb[:, :, 1] > 70) & (rgb[:, :, 2] - rgb[:, :, 1] > 20)
    # Its drop shadow is a dark magenta band: purple-tinted pixels anywhere (the stone is grey/beige, the moss green).
    purple = (rgb[:, :, 2] - rgb[:, :, 1] > 25) & (rgb[:, :, 0] - rgb[:, :, 1] > 35)
    out[purple, 3] = 0
    for _ in range(iterations):
        solid = out[:, :, 3] > 0
        edge = solid & ~ndimage.binary_erosion(solid)
        kill = edge & reddish
        if not kill.any(): break
        out[kill, 3] = 0
    return out


def drop_shadow_grey(rgba):
    """White-background art with a soft grey drop shadow: remove the light, colourless shadow pixels."""
    out = rgba.copy()
    rgb = out[:, :, :3].astype(np.int16)
    mn, mx = rgb.min(axis=2), rgb.max(axis=2)
    shadow = (mx - mn < 22) & (mn > 70)
    lab, n = ndimage.label(shadow & (out[:, :, 3] > 0))
    if n:
        sizes = ndimage.sum(shadow, lab, index=np.arange(1, n + 1))
        big = np.nonzero(sizes > 2000)[0] + 1
        out[np.isin(lab, big), 3] = 0
    return out


def lengthen(rgba, aspect, cap=0.22):
    """Long pieces: repeat the middle section (alternately mirrored) between the end caps up to the aspect."""
    h, w = rgba.shape[:2]
    c = int(w * cap)
    left, mid, right = rgba[:, :c], rgba[:, c:w - c], rgba[:, w - c:]
    need = int(aspect * h) - 2 * c
    parts, k = [], 0
    while sum(x.shape[1] for x in parts) < need:
        parts.append(mid if k % 2 == 0 else mid[:, ::-1]); k += 1
    body = np.concatenate(parts, axis=1)[:, :max(need, 1)]
    return np.concatenate([left, body, right], axis=1)


def downsize(rgba, max_side):
    h, w = rgba.shape[:2]
    s = max_side / max(h, w)
    if s >= 1: return rgba
    return np.asarray(Image.fromarray(rgba, "RGBA").resize((int(w * s), int(h * s)), Image.LANCZOS)).copy()


def main():
    p = lambda n: os.path.join(SRC, n)
    print("Writing to", os.path.normpath(OUT))

    # ── backdrop strips ───────────────────────────────────────────────────────────────────────────────────────
    far = load_rgba(os.path.join(STAMPEDE, "Environment", "WV_Parallax_Far.png"))
    far[:, :, 3] = 255
    save(far, OUT, "MillFar.png")
    ridges = crop_bottom(trim(key_magenta(p("W3Hills.png"))), 0.04)
    save(haze_w3(ridges, 0.5), OUT, "MillRidges.png")
    mid = trim(load_rgba(os.path.join(STAMPEDE, "Environment", "WV_Parallax_Mid.png")))
    save(haze_w3(mid, 0.12), OUT, "MillMid.png")
    wall = trim(load_rgba(os.path.join(STAMPEDE, "Environment", "WV_Parallax_Near.png")))
    save(wall, OUT, "MillWall.png")

    # Riverbank soil: the grass line is the top of the solid loam band under the grass blades.
    soil = trim(strip_ground_line(key_magenta(p("W3Soil.png"))))
    sh, sw = soil.shape[:2]
    soil = soil[:, int(sw * 0.02):int(sw * 0.98)]        # shave the rounded ends so mirrored tiles meet
    sh, sw = soil.shape[:2]
    top = solid_top(soil, int(sw * 0.05), int(sw * 0.95), frac=0.97)
    save(soil, OUT, "MillSoil.png")
    print(f"  MillSoil ground line UV (from bottom) = {(sh - top - 8) / sh:.3f}   <- EnvironmentDepthSystem.MillSoilGroundUV")

    # Corner silhouettes: bulrush clumps from the near band, baked to deep river teal, inner edge faded.
    def silhouette(u0, u1, fade_left):
        cw = wall.shape[1]
        c = wall[:, int(cw * u0):int(cw * u1)].astype(np.float32)
        c[:, :, :3] = c[:, :, :3] * 0.06 + np.array([10, 30, 30], np.float32)
        n = c.shape[1]
        t = np.linspace(0.0, 1.0, n)
        ramp = np.clip((t if fade_left else 1.0 - t) / 0.28, 0.0, 1.0)
        c[:, :, 3] *= (ramp * ramp * (3 - 2 * ramp))[None, :]
        return trim(c.clip(0, 255).astype(np.uint8))
    save(silhouette(0.0, 0.16, fade_left=False), OUT, "MillSilhouetteL.png")
    save(silhouette(0.84, 1.0, fade_left=True), OUT, "MillSilhouetteR.png")

    # ── the Mill Cannon: fitted into Cannon.png's 566x512 canvas over its own bounding box (same pose), so
    #    CatapultLauncher's barrel / loaded-animal offsets and FarmCannon's scale carry over unchanged.
    base = load_rgba(os.path.join(LAUNCHERS, "Cannon.png"))
    ys, xs = np.nonzero(base[:, :, 3] > 40)
    box = (xs.min(), ys.min(), xs.max() + 1, ys.max() + 1)
    can = trim(key_magenta(p("W3Cannon.png")))
    ch, cw = can.shape[:2]
    s = min((box[2] - box[0]) / cw, (box[3] - box[1]) / ch)
    im = Image.fromarray(can, "RGBA").resize((max(1, int(cw * s)), max(1, int(ch * s))), Image.LANCZOS)
    canvas = Image.new("RGBA", (base.shape[1], base.shape[0]), (0, 0, 0, 0))
    canvas.paste(im, ((box[0] + box[2]) // 2 - im.width // 2, box[3] - im.height), im)
    save(np.asarray(canvas).copy(), LAUNCHERS, "MillCannon.png")

    # ── construction pieces ───────────────────────────────────────────────────────────────────────────────────
    for src, name, aspect in (("W3Post.png", "MillPost.png", 0), ("W3Plank.png", "MillPlank.png", 7.5),
                              ("W3StoneBlock.png", "MillStoneBlock.png", 0), ("W3StonePillar.png", "MillStonePillar.png", 0),
                              ("W3Slab.png", "MillSlab.png", 6.0)):
        art = key_magenta(p(src))
        if src == "W3Slab.png": art = defringe_red(art)
        art = trim(strip_ground_line(art))
        art = lengthen(art, aspect) if aspect else art
        save(downsize(art, 1400), OUT, name)

    # Flour sacks: padded to Haybail.png's 500 px canvas (bale rows 63..480, full width) so the cannon mount and the
    # Haybale block reuse their constants unchanged.
    sacks = trim(strip_ground_line(key_magenta(p("W3Sacks.png"))))
    save(pad_to(sacks, 500, (0, 63, 500, 480)), OUT, "FlourSacks.png")

    # ── contraptions ──────────────────────────────────────────────────────────────────────────────────────────
    pump = trim(strip_ground_line(key_magenta(p("W3Pump.png"))))
    save(downsize(pump, 900), OUT, "WaterPump.png")

    crane = trim(strip_ground_line(key_magenta(p("W3Crane.png"))))
    crane = downsize(crane, 1000)
    save(crane, OUT, "MillCrane.png")
    a = crane[:, :, 3] > 128
    h, w = a.shape
    upper = a[: int(h * 0.45)]
    cols = np.nonzero(upper.any(axis=0))[0]
    tip = cols.max()                                         # the jib arm's far end
    ax = (tip - 0.05 * w) / w
    col = np.nonzero(upper[:, int(ax * w)])[0]
    ay = (h - col.max()) / h                                 # underside of the arm there, from the bottom
    row = np.nonzero(a[int(h * 0.6), : w // 2])[0]
    post = row.mean() / w
    crane_meas = dict(anchor=(round(float(ax), 3), round(float(ay), 3)), post=round(float(post), 3), aspect=w / h)
    print(f"  MillCrane anchor {crane_meas['anchor']}, post {crane_meas['post']}, aspect {w / h:.3f}")

    # Millstone: the source shows bricks stacked behind the stone; keep only the disc (Hough circle on the cut-out).
    import cv2
    ms = cut_white(os.path.join(ART, "Millstone.png"))
    gray = cv2.cvtColor(ms[:, :, :3], cv2.COLOR_RGB2GRAY)
    gray = cv2.medianBlur(gray, 7)
    hh, ww = gray.shape
    circles = cv2.HoughCircles(gray, cv2.HOUGH_GRADIENT, dp=2, minDist=ww, param1=80, param2=40,
                               minRadius=int(ww * 0.25), maxRadius=int(ww * 0.48))
    if circles is not None:
        cx, cy, r = circles[0][0]
    else:
        ys, xs = np.nonzero(ms[:, :, 3] > 128); cx, cy, r = xs.mean(), ys.mean(), (ys.max() - ys.min()) / 2
    r *= 0.985
    yy, xx = np.mgrid[:hh, :ww]
    d = np.sqrt((xx - cx) ** 2 + (yy - cy) ** 2)
    edge = np.clip((r - d) / 3.0, 0, 1)
    ms = ms.astype(np.float32); ms[:, :, 3] *= edge
    disc = trim(ms.clip(0, 255).astype(np.uint8))
    save(downsize(disc, 512), OUT, "Millstone.png")
    print(f"  Millstone circle centre ({cx:.0f},{cy:.0f}) r {r:.0f} of {ww}")

    # ── the boss: the Mill Stone Mauler (Stampede), padded like Commander.png (500 px, body bbox 65..449 x 23..469).
    #    Idle faces the cannon (left); alert = the other pose mirrored to face left too; hit = the dazed pose.
    wv = os.path.join(STAMPEDE, "UI", "WatermillVillage")
    idle = trim(strip_ground_line(load_rgba(os.path.join(wv, "MilestoneMaulerLeft.png"))))
    save(pad_to(idle, 500, (65, 23, 449, 469)), ROBOTS, "MillMauler.png")
    ih, iw = idle.shape[:2]
    s = min((449 - 65) / iw, (469 - 23) / ih)
    for src, name, flip in (("MileStoneMauler.png", "MillMauler_Alert.png", True),
                            ("MileStoneMauler_Defeated.png", "MillMauler_Hit.png", False)):
        body = trim(strip_ground_line(load_rgba(os.path.join(wv, src))))
        if flip: body = body[:, ::-1].copy()
        im = Image.fromarray(body, "RGBA").resize((max(1, int(body.shape[1] * s)), max(1, int(body.shape[0] * s))), Image.LANCZOS)
        if im.width > 480 or im.height > 480:
            f = min(480 / im.width, 480 / im.height); im = im.resize((int(im.width * f), int(im.height * f)), Image.LANCZOS)
        canvas = Image.new("RGBA", (500, 500), (0, 0, 0, 0))
        canvas.paste(im, ((65 + 449) // 2 - im.width // 2, 469 - im.height), im)
        save(np.asarray(canvas).copy(), ROBOTS, name)

    import_mounts(crane_meas)


# Watermill mounts, same kinds as World 1's (LevelLoader mount variants 0-6 for World 3 levels).
MOUNTS = {  # name -> (source, kind, flip, keyer)
    "Islet":   ("W3Islet.png", "island", False, "magenta"),
    "Pier":    ("W3Pier.png", "solid", False, "magenta"),
    "Jetty":   ("W3Jetty.png", "deck", False, "magenta"),
    "Boat":    (os.path.join(ART, "FishingBoat.png"), "deck", False, "white"),
    "Slipway": ("W3Slipway.png", "profile", False, "magenta"),
}


def import_mounts(crane_meas):
    data, cs = {}, []
    for name, (src, kind, flip, keyer) in MOUNTS.items():
        path = src if os.path.isabs(src) else os.path.join(SRC, src)
        rgba = drop_shadow_grey(cut_white(path)) if keyer == "white" else key_magenta(path)
        rgba = trim(strip_ground_line(rgba))
        if flip:
            rgba = rgba[:, ::-1].copy()
        rgba = downsize(rgba, 1600)
        save(rgba, OUT, f"Mount_{name}.png")
        h, w = rgba.shape[:2]
        extra = {}
        if kind == "island":
            top = solid_top(rgba[: int(h * 0.6)], int(w * 0.25), int(w * 0.75), frac=0.99)
            uv = (h - (top + 10)) / h
            boxes, flat, poly = [(0.04, 0.96, 0.25, uv)], (0.04, 0.96), None
        else:
            uv, boxes, flat, poly = measure_mount(rgba, kind)
        if name == "Boat":
            # The rowing boat slides on its keel: two skid circles where the hull's bottom curve sits lowest.
            a = rgba[:, :, 3] > 128
            low = np.array([np.nonzero(a[:, x])[0].max() if a[:, x].any() else -1 for x in range(w)])
            r = 0.10
            skids = []
            for fx in (0.32, 0.68):
                bottom_uv = (h - 1 - low[int(fx * w)]) / h
                skids.append((fx, round(float(bottom_uv + r), 3), r))
            extra["skids"] = skids
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
    data["MillCrane"] = crane_meas
    ax, ay = crane_meas["anchor"]
    cs.append(f"    // The mill hoist crane (HayHoist on World 3): rope anchor under the jib's tip and the post centre.\n"
              f"    public static readonly Vector2 MillCraneAnchor = new Vector2({ax:.3f}f, {ay:.3f}f);\n"
              f"    public const float MillCranePost = {crane_meas['post']:.3f}f;")
    json.dump(data, open(os.path.join(HERE, "mounts_w3.json"), "w"), indent=1)
    out = os.path.join(UNITY, "Scripts", "Level", "MountMeasurementsW3.cs")
    open(out, "w", encoding="utf-8").write(
        "// GENERATED by tools/import_watermill_art.py from the watermill mount art - re-run it if the art changes.\n"
        "using UnityEngine;\n\npublic static class MountMeasurementsW3\n{\n" + "\n".join(cs) + "\n}\n")
    print("  wrote MountMeasurementsW3.cs + tools/mounts_w3.json")


if __name__ == "__main__":
    main()
