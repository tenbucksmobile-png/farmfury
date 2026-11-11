"""
Farm Fury - World 4 (Sky Islands) framed-level art import (2026-11-11).

The sky counterpart of import_watermill_art.py. Reuses existing art first:
  * FarmFury_Stampede's Sky Island set: SI_Parallax_Far (sky over a sea of clouds), CloudTile (the cloud bank / near
    wall), StonePillar (cloud-wrapped column = the tall "mesa" mount), AnchorCloud (thundercloud contraption),
    GiantMushroom (corner silhouettes), the Storm Baron airship boss (two facings + a wrecked pose).
Kling filled the gaps (kling-image-v3_0_omni i2i, 2 credits each, on magenta, shaped after the World 3 counterpart and
styled after the Sky Island props) in FarmFury_Artwork/SkyIsland/Framed/: the Sky Cannon, ONE sheet with all six
construction pieces (airship post + plank, marble block / pillar / slab, cloud bale), the sky crane, the small floating
rock (hill), the sky dock, the slide ramp, the floating-island ground strip (also the wide island), the distant isles
strip and the thunder wheel. W4Skiff.png (a blimp, no flat deck) is unused: World 4 has no rolling-wagon mount.

Output: unity/Assets/Sprites/Environment/Sky/, Environment/Launchers/SkyCannon.png, Enemies/Robot/StormBaron*.png,
        Scripts/Level/MountMeasurementsW4.cs, tools/mounts_w4.json

Usage (repo root):  python tools/import_sky_art.py
"""
import json
import os

import numpy as np
from PIL import Image, ImageDraw
from scipy import ndimage

from import_cornfield_art import trim, solid_top, measure_mount, strip_ground_line, DESKTOP
from import_frozen_art import key_magenta, crop_bottom, pad_to
from import_watermill_art import lengthen, downsize, save, load_rgba

HERE = os.path.dirname(os.path.abspath(__file__))
ART = os.path.join(DESKTOP, "FarmFury_Technical", "FarmFury_Artwork", "SkyIsland")
SRC = os.path.join(ART, "Framed")
STAMPEDE = os.path.join(DESKTOP, "FarmFury_Stampede", "Assets", "_Project", "Sprites")
SKY_UI = os.path.join(STAMPEDE, "UI", "SkyIsland")
UNITY = os.path.join(HERE, "..", "unity", "Assets")
OUT = os.path.join(UNITY, "Sprites", "Environment", "Sky")
ROBOTS = os.path.join(UNITY, "Sprites", "Enemies", "Robot")
LAUNCHERS = os.path.join(UNITY, "Sprites", "Environment", "Launchers")
HAZE_W4 = np.array([206, 226, 246], np.float32)   # pale sky-blue haze (the far painting's horizon)


def haze_w4(rgba, amount):
    out = rgba.astype(np.float32)
    out[:, :, :3] = out[:, :, :3] * (1 - amount) + HAZE_W4 * amount
    return out.clip(0, 255).astype(np.uint8)


def keep_main(rgba, frac=0.15):
    """Drop separate bits (background clouds Kling added around the object): keep components >= frac of the largest."""
    solid = rgba[:, :, 3] > 40
    lab, n = ndimage.label(solid)
    if n <= 1: return rgba
    area = ndimage.sum(solid, lab, index=np.arange(1, n + 1))
    keep = np.nonzero(area >= area.max() * frac)[0] + 1
    out = rgba.copy()
    out[~np.isin(lab, keep), 3] = 0
    return out


def lengthen_v(rgba, ratio):
    """Tall pieces: repeat the middle vertically up to height = ratio x width (same trick as lengthen, rotated)."""
    t = np.ascontiguousarray(np.transpose(rgba, (1, 0, 2)))
    return np.ascontiguousarray(np.transpose(lengthen(t, ratio), (1, 0, 2)))


def split_pieces(path):
    """The one-sheet pieces image -> dict of the six pieces, by shape and position."""
    rgba = key_magenta(path)
    solid = rgba[:, :, 3] > 40
    lab, n = ndimage.label(solid)
    area = ndimage.sum(solid, lab, index=np.arange(1, n + 1))
    order = np.argsort(area)[::-1][:6]
    parts = []
    for i in order:
        ys, xs = np.nonzero(lab == i + 1)
        crop = rgba[ys.min():ys.max() + 1, xs.min():xs.max() + 1].copy()
        crop[lab[ys.min():ys.max() + 1, xs.min():xs.max() + 1] != i + 1, 3] = 0
        h, w = crop.shape[:2]
        parts.append(dict(x=xs.mean(), y=ys.mean(), w=w, h=h, img=trim(crop)))
    tall = sorted([p for p in parts if p["h"] > 1.8 * p["w"]], key=lambda p: p["x"])
    wide = sorted([p for p in parts if p["w"] > 2.5 * p["h"]], key=lambda p: p["x"])
    square = sorted([p for p in parts if p not in tall and p not in wide], key=lambda p: p["x"])
    assert len(tall) == 2 and len(wide) == 2 and len(square) == 2, [(p["w"], p["h"]) for p in parts]
    return dict(post=tall[0]["img"], pillar=tall[1]["img"], plank=wide[0]["img"], slab=wide[1]["img"],
                block=square[0]["img"], bale=square[1]["img"])


def cloud_floor(rgba):
    """The Kling cloud floor is a flat packed-cloud band with puffs hanging below and gaps between them, and one puff
    poking above the band. Make it a solid floor: clear everything above the band's top edge, then fill the gaps below
    with cloud blue that matches EnvironmentDepthSystem.SkyUnderfill at the bottom (so it runs on into the underfill)."""
    out = rgba.astype(np.float32)
    a = out[:, :, 3] > 128
    h, w = a.shape
    rows = a[:, int(w * 0.05):int(w * 0.95)].mean(axis=1)
    top = int(np.nonzero(rows >= 0.9)[0][0])               # the band's top outline: first nearly full-width row
    out[: max(0, top - 2), :, 3] = 0
    fill_top = np.array([236, 244, 253], np.float32)
    fill_bot = np.array([219, 235, 252], np.float32)       # = SkyUnderfill (0.86, 0.92, 0.99)
    for y in range(top + 4, h):
        t = (y - top) / max(1, h - top)
        col = fill_top * (1 - t) + fill_bot * t
        k = out[y, :, 3:4] / 255.0
        out[y, :, :3] = out[y, :, :3] * k + col * (1 - k)
        out[y, :, 3] = 255
    return out.clip(0, 255).astype(np.uint8)


def bolt_sprite(w=96, h=220):
    """A cartoon lightning bolt (the storm cloud's projectile), drawn here rather than generated."""
    im = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    pts = [(0.62, 0.0), (0.18, 0.52), (0.46, 0.52), (0.30, 1.0), (0.86, 0.38), (0.56, 0.38), (0.80, 0.0)]
    poly = [(x * (w - 12) + 6, y * (h - 12) + 6) for x, y in pts]
    d = ImageDraw.Draw(im)
    d.polygon(poly, fill=(255, 226, 64, 255), outline=(60, 40, 10, 255))
    d.line(poly + [poly[0]], fill=(60, 40, 10, 255), width=6, joint="curve")
    d.polygon([(x * 0.94 + w * 0.03, y * 0.97 + h * 0.01) for x, y in poly[:3]], fill=(255, 250, 200, 255))
    return np.asarray(im).copy()


def main():
    p = lambda n: os.path.join(SRC, n)
    print("Writing to", os.path.normpath(OUT))

    # ── backdrop strips ───────────────────────────────────────────────────────────────────────────────────────
    far = load_rgba(os.path.join(STAMPEDE, "Environment", "SI_Parallax_Far.png"))
    far[:, :, 3] = 255
    save(downsize(far, 2048), OUT, "SkyFar.png")
    isles = trim(keep_main(key_magenta(p("W4Ridges.png")), frac=0.02))
    save(haze_w4(isles[:, ::-1].copy(), 0.5), OUT, "SkyRidges.png")   # mirrored so it doesn't echo the mid band
    save(haze_w4(isles, 0.45), OUT, "SkyMid.png")
    wall = trim(load_rgba(os.path.join(SKY_UI, "CloudTile.png")))
    save(wall, OUT, "SkyWall.png")

    # Ground: the floating-island strip; the grass line is the top of the solid grass band under the clover leaves.
    # 2026-11-11: a cloud floor (W4CloudFloor.png) replaced the grassy island top W4Soil.png (user: "theres no grass in
    # the sky"); the "grass line" is now the flat top of the packed cloud.
    soil = trim(keep_main(strip_ground_line(key_magenta(p("W4CloudFloor.png"))), frac=0.3))
    soil = cloud_floor(soil)
    sh, sw = soil.shape[:2]
    soil = soil[:, int(sw * 0.06):int(sw * 0.94)]        # shave the rounded ends so mirrored tiles meet
    sh, sw = soil.shape[:2]
    top = solid_top(soil, int(sw * 0.05), int(sw * 0.95), frac=0.97)
    save(soil, OUT, "SkySoil.png")
    print(f"  SkySoil ground line UV (from bottom) = {(sh - top - 8) / sh:.3f}   <- EnvironmentDepthSystem.SkySoilGroundUV")

    # Corner silhouettes: the giant mushroom baked to deep twilight blue, inner edge faded.
    mush = trim(load_rgba(os.path.join(SKY_UI, "GiantMushroom.png"))).astype(np.float32)
    mush[:, :, :3] = mush[:, :, :3] * 0.06 + np.array([18, 28, 58], np.float32)

    def silhouette(img, fade_left):
        c = img.copy()
        n = c.shape[1]
        t = np.linspace(0.0, 1.0, n)
        ramp = np.clip((t if fade_left else 1.0 - t) / 0.35, 0.0, 1.0)
        c[:, :, 3] *= (ramp * ramp * (3 - 2 * ramp))[None, :]
        return trim(c.clip(0, 255).astype(np.uint8))
    save(silhouette(mush, fade_left=False), OUT, "SkySilhouetteL.png")
    save(silhouette(mush[:, ::-1].copy(), fade_left=True), OUT, "SkySilhouetteR.png")

    # Sky decor (SkyDecor, 2026-11-11): Stampede props floating in the sky, plus the rooster vane by the cannon.
    for src, name in (("AirBalloon.png", "Decor_Balloon.png"), ("Kite.png", "Decor_Kite.png"),
                      ("HangingLantern.png", "Decor_Lantern.png"), ("FloatingPlum.png", "Decor_Plum.png"),
                      ("Windvane.png", "Decor_Windvane.png")):
        save(downsize(trim(load_rgba(os.path.join(SKY_UI, src))), 512), OUT, name)

    # ── the Sky Cannon: fitted into Cannon.png's 566x512 canvas over its own bounding box (as the Mill Cannon) ──
    base = load_rgba(os.path.join(LAUNCHERS, "Cannon.png"))
    ys, xs = np.nonzero(base[:, :, 3] > 40)
    box = (xs.min(), ys.min(), xs.max() + 1, ys.max() + 1)
    can = trim(keep_main(strip_ground_line(key_magenta(p("W4Cannon.png")))))
    ch, cw = can.shape[:2]
    s = min((box[2] - box[0]) / cw, (box[3] - box[1]) / ch)
    im = Image.fromarray(can, "RGBA").resize((max(1, int(cw * s)), max(1, int(ch * s))), Image.LANCZOS)
    canvas = Image.new("RGBA", (base.shape[1], base.shape[0]), (0, 0, 0, 0))
    canvas.paste(im, ((box[0] + box[2]) // 2 - im.width // 2, box[3] - im.height), im)
    save(np.asarray(canvas).copy(), LAUNCHERS, "SkyCannon.png")

    # ── construction pieces (one sheet), proportioned like World 3's ──────────────────────────────────────────
    pc = split_pieces(p("W4Pieces.png"))
    save(downsize(lengthen_v(pc["post"], 1399 / 462), 1400), OUT, "SkyPost.png")
    save(downsize(lengthen(pc["plank"], 1400 / 186), 1400), OUT, "SkyPlank.png")
    save(downsize(pc["block"], 1400), OUT, "SkyBlock.png")
    save(downsize(lengthen_v(pc["pillar"], 1400 / 418), 1400), OUT, "SkyPillar.png")
    save(downsize(lengthen(pc["slab"], 1400 / 233), 1400), OUT, "SkySlab.png")
    # Cloud bale: padded to Haybail.png's 500 px canvas (bale rows 63..480, full width) like the flour sacks.
    save(pad_to(pc["bale"], 500, (0, 63, 500, 480)), OUT, "CloudBale.png")

    # ── contraptions ──────────────────────────────────────────────────────────────────────────────────────────
    storm = trim(load_rgba(os.path.join(SKY_UI, "AnchorCloud.png")))
    save(storm, OUT, "StormCloud.png")
    save(bolt_sprite(), OUT, "Bolt.png")

    crane = trim(keep_main(strip_ground_line(key_magenta(p("W4Crane.png")))))
    crane = downsize(crane, 1000)
    save(crane, OUT, "SkyCrane.png")
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
    print(f"  SkyCrane anchor {crane_meas['anchor']}, post {crane_meas['post']}, aspect {w / h:.3f}")

    # Thunder wheel: keep the disc (drop the sparkles), circular edge.
    wheel = trim(keep_main(key_magenta(p("W4Orb.png")), frac=0.5)).astype(np.float32)
    hh, ww = wheel.shape[:2]
    r = min(hh, ww) / 2 * 0.985
    yy, xx = np.mgrid[:hh, :ww]
    d = np.sqrt((xx - ww / 2) ** 2 + (yy - hh / 2) ** 2)
    wheel[:, :, 3] *= np.clip((r - d) / 3.0, 0, 1)
    save(downsize(trim(wheel.clip(0, 255).astype(np.uint8)), 512), OUT, "ThunderWheel.png")

    # ── the boss: the Storm Baron airship (Stampede), padded like Commander.png (500 px, body 65..449 x 23..469).
    #    Idle = StormBaron (faces the cannon); alert = the other facing, mirrored back; hit = the wrecked pose.
    idle = trim(load_rgba(os.path.join(SKY_UI, "StormBaron.png")))
    save(pad_to(idle, 500, (65, 23, 449, 469)), ROBOTS, "StormBaron.png")
    ih, iw = idle.shape[:2]
    s = min((449 - 65) / iw, (469 - 23) / ih)
    for src, name, flip in (("StormBaron_Right.png", "StormBaron_Alert.png", True),
                            ("StormBaronDefeated.png", "StormBaron_Hit.png", False)):
        body = trim(load_rgba(os.path.join(SKY_UI, src)))
        if flip: body = body[:, ::-1].copy()
        im = Image.fromarray(body, "RGBA").resize((max(1, int(body.shape[1] * s)), max(1, int(body.shape[0] * s))), Image.LANCZOS)
        if im.width > 480 or im.height > 480:
            f = min(480 / im.width, 480 / im.height); im = im.resize((int(im.width * f), int(im.height * f)), Image.LANCZOS)
        canvas = Image.new("RGBA", (500, 500), (0, 0, 0, 0))
        canvas.paste(im, ((65 + 449) // 2 - im.width // 2, 469 - im.height), im)
        save(np.asarray(canvas).copy(), ROBOTS, name)

    import_mounts(crane_meas)


# Sky mounts (LevelLoader mount variants for World 4 levels): 0/1 wide floating isle, 2 hill = small floating rock,
# 3 mesa = cloud-wrapped stone pillar, 4 scaffold = sky dock, 6 ramp = the slide ramp. No rolling wagon (variant 5).
MOUNTS = {  # name -> (source, kind, keyer)
    # Cloud mountains (2026-11-11, user: "bigger floating clouds that sit like mountains on which the robot structures
    # are built"): three shapes split from one Kling sheet (W4CloudMountains.png, guided by a flat-top mesa template),
    # standing on the ground. They replaced the floating isle (W4Soil) and rock (W4Island) as mounts 0/1/2.
    "CloudLow":  ("W4CloudMountains.png#0", "solid", "magenta"),
    "CloudMid":  ("W4CloudMountains.png#1", "solid", "magenta"),
    "CloudTall": ("W4CloudMountains.png#2", "solid", "magenta"),
    "Pillar": (os.path.join(SKY_UI, "StonePillar.png"), "solid", "alpha"),
    "Dock":   ("W4Dock.png", "deck", "magenta"),
    "Ramp":   ("W4RampCloud.png", "profile", "magenta"),   # cloud plateau + slide (was the grassy rock, W4Ramp.png)
}


def sheet_part(rgba, index, count=3):
    """The index-th (left to right) of the `count` largest separate shapes on a sheet."""
    solid = rgba[:, :, 3] > 40
    lab, n = ndimage.label(solid)
    area = ndimage.sum(solid, lab, index=np.arange(1, n + 1))
    big = sorted(np.argsort(area)[::-1][:count] + 1, key=lambda i: np.nonzero(lab == i)[1].mean())
    out = rgba.copy()
    out[lab != big[index], 3] = 0
    return trim(out)


def import_mounts(crane_meas):
    data, cs = {}, []
    for name, (src, kind, keyer) in MOUNTS.items():
        part = None
        if "#" in src: src, part = src.split("#"); part = int(part)
        path = src if os.path.isabs(src) else os.path.join(SRC, src)
        rgba = load_rgba(path) if keyer == "alpha" else key_magenta(path)
        if part is not None: rgba = sheet_part(rgba, part)
        rgba = trim(keep_main(strip_ground_line(rgba), frac=0.3))
        if name == "Isle":
            rgba = rgba[:, int(rgba.shape[1] * 0.02):int(rgba.shape[1] * 0.98)].copy()
        if name.startswith("Cloud"):   # cut the rounded bottom puffs flat: the mountain grows out of the cloud floor
            rgba = trim(rgba[: int(rgba.shape[0] * 0.95)].copy())
        rgba = downsize(rgba, 1600)
        save(rgba, OUT, f"Mount_{name}.png")
        h, w = rgba.shape[:2]
        if kind == "island" and not name.startswith("Cloud"):
            top = solid_top(rgba[: int(h * 0.6)], int(w * 0.25), int(w * 0.75), frac=0.99)
            uv = (h - (top + 10)) / h
            boxes, flat, poly = [(0.04, 0.96, 0.25, uv)], (0.04, 0.96), None
        else:
            uv, boxes, flat, poly = measure_mount(rgba, kind)
            if name.startswith("Cloud"):   # only the plateau band is solid: the cloud body below is soft (2026-11-11 sky
                boxes = boxes[:1]          # cities: neighbouring peaks overlap like a range, debris sinks into the fog)
        data[name] = dict(uv=uv, aspect=w / h, boxes=boxes, flat=flat, poly=poly)
        print(f"  {name}: surfaceUV {uv:.3f}, flat top x {flat[0]:.2f}-{flat[1]:.2f}, {len(boxes)} solids, aspect {w / h:.3f}")
        v4 = ", ".join(f"new Vector4({b[0]:.3f}f, {b[1]:.3f}f, {b[2]:.3f}f, {b[3]:.3f}f)" for b in boxes)
        cs.append(f"    public const float {name}UV = {uv:.3f}f;\n"
                  f"    public static readonly Vector4[] {name}Solids = {{ {v4} }};")
        if poly:
            v2 = ", ".join(f"new Vector2({px:.3f}f, {py:.3f}f)" for px, py in poly)
            cs.append(f"    public static readonly Vector2[] {name}Poly = {{ {v2} }};")
    data["SkyCrane"] = crane_meas
    ax, ay = crane_meas["anchor"]
    cs.append(f"    // The sky crane (HayHoist on World 4): rope anchor under the jib's tip and the post centre.\n"
              f"    public static readonly Vector2 SkyCraneAnchor = new Vector2({ax:.3f}f, {ay:.3f}f);\n"
              f"    public const float SkyCranePost = {crane_meas['post']:.3f}f;")
    json.dump(data, open(os.path.join(HERE, "mounts_w4.json"), "w"), indent=1)
    out = os.path.join(UNITY, "Scripts", "Level", "MountMeasurementsW4.cs")
    open(out, "w", encoding="utf-8").write(
        "// GENERATED by tools/import_sky_art.py from the sky mount art - re-run it if the art changes.\n"
        "using UnityEngine;\n\npublic static class MountMeasurementsW4\n{\n" + "\n".join(cs) + "\n}\n")
    print("  wrote MountMeasurementsW4.cs + tools/mounts_w4.json")


if __name__ == "__main__":
    main()
