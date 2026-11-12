"""
Farm Fury - World 6 (Robot Mothership) framed-level art import (2026-11-12).

The spaceship counterpart of import_sunken_art.py. Reuses existing art first:
  * FarmFury_Stampede's Mothership set: RM_Parallax_Far (the mothership over a planet in space), DeckStrip / HullFill
    (the deck floor and the colour under it), SateliteDish (corner silhouettes, baked to deep space blue), Drone and Ship
    (SpaceDecor: things flying across), WarningBeacon (the landmark behind the cannon), LaserEmitter (the contraption
    that fires a jet of plasma orbs when hit; the orb is drawn here), GravitySling (the ring that rolls into the
    structure), the Factory Machine (the boss: FActoryMachine_Left idle, mirrored alert, Defeated hit).
Kling filled the gaps (kling-image-v3_0_omni i2i, 2 credits each, on magenta, laid out like the World 4 sheets so the
same splitters work) in FarmFury_Artwork/Mothership/Framed/: the Gravity Cannon (W6Cannon.png), ONE sheet with all six
construction pieces (W6Pieces.png: steel girder post, I-beam, riveted crate, energy column, steel slab, battery pod) and
ONE sheet with the three flat-topped machinery pedestals (W6Mounts.png).

Output: unity/Assets/Sprites/Environment/Mothership/, Environment/Launchers/GravityCannon.png,
        Enemies/Robot/FactoryMachine*.png, Scripts/Level/MountMeasurementsW6.cs, tools/mounts_w6.json

Usage (repo root):  python tools/import_mothership_art.py
"""
import json
import os

import numpy as np
from PIL import Image, ImageDraw

from import_cornfield_art import trim, solid_top, measure_mount, strip_ground_line, DESKTOP
from import_frozen_art import key_magenta, pad_to
from import_watermill_art import lengthen, downsize, save, load_rgba
from import_sky_art import keep_main, lengthen_v, split_pieces, sheet_part

HERE = os.path.dirname(os.path.abspath(__file__))
ART = os.path.join(DESKTOP, "FarmFury_Technical", "FarmFury_Artwork", "Mothership")
SRC = os.path.join(ART, "Framed")
STAMPEDE = os.path.join(DESKTOP, "FarmFury_Stampede", "Assets", "_Project", "Sprites")
SHIP_UI = os.path.join(STAMPEDE, "UI", "MotherShip")
UNITY = os.path.join(HERE, "..", "unity", "Assets")
OUT = os.path.join(UNITY, "Sprites", "Environment", "Mothership")
LAUNCHERS = os.path.join(UNITY, "Sprites", "Environment", "Launchers")
ROBOTS = os.path.join(UNITY, "Sprites", "Enemies", "Robot")


def plasma_orb(n=128):
    """A glowing cyan plasma orb (the laser emitter's shot), drawn here."""
    yy, xx = np.mgrid[:n, :n]
    d = np.sqrt((xx - n / 2 + 0.5) ** 2 + (yy - n / 2 + 0.5) ** 2) / (n / 2)
    core = np.clip(1 - d / 0.45, 0, 1)
    glow = np.clip(1 - d, 0, 1) ** 2
    rgba = np.zeros((n, n, 4), np.float32)
    rgba[:, :, 0] = 120 + 135 * core
    rgba[:, :, 1] = 220 + 35 * core
    rgba[:, :, 2] = 255
    rgba[:, :, 3] = np.clip(glow * 255 * 1.2 + core * 255, 0, 255)
    return rgba.astype(np.uint8)


def main():
    p = lambda n: os.path.join(SRC, n)
    s = lambda n: os.path.join(SHIP_UI, n)
    print("Writing to", os.path.normpath(OUT))

    far = load_rgba(os.path.join(STAMPEDE, "Environment", "RM_Parallax_Far.png"))
    far[:, :, 3] = 255
    save(downsize(far, 2048), OUT, "SpaceFar.png")

    # Deck floor: Stampede's deck strip. Ground line = the top of the solid deck.
    soil = trim(load_rgba(s("DeckStrip.png")))
    sh, sw = soil.shape[:2]
    top = solid_top(soil, int(sw * 0.05), int(sw * 0.95), frac=0.97)
    save(soil, OUT, "SpaceSoil.png")
    print(f"  SpaceSoil ground line UV (from bottom) = {(sh - top - 4) / sh:.3f}   <- EnvironmentDepthSystem.SpaceSoilGroundUV")
    fill = load_rgba(s("HullFill.png"))[:, :, :3].reshape(-1, 3).mean(axis=0) / 255.0
    print(f"  HullFill average colour = ({fill[0]:.2f}, {fill[1]:.2f}, {fill[2]:.2f})   <- SpaceUnderfill")

    dish = trim(load_rgba(s("SateliteDish.png"))).astype(np.float32)
    dish[:, :, :3] = dish[:, :, :3] * 0.06 + np.array([10, 14, 34], np.float32)

    def silhouette(img, fade_left):
        c = img.copy()
        n = c.shape[1]
        t = np.linspace(0.0, 1.0, n)
        ramp = np.clip((t if fade_left else 1.0 - t) / 0.35, 0.0, 1.0)
        c[:, :, 3] *= (ramp * ramp * (3 - 2 * ramp))[None, :]
        return trim(c.clip(0, 255).astype(np.uint8))
    save(silhouette(dish, fade_left=False), OUT, "SpaceSilhouetteL.png")
    save(silhouette(dish[:, ::-1].copy(), fade_left=True), OUT, "SpaceSilhouetteR.png")

    save(downsize(trim(load_rgba(s("Drone.png"))), 384), OUT, "Decor_Drone.png")
    save(downsize(trim(load_rgba(s("Ship.png"))), 384), OUT, "Decor_Ship.png")
    save(trim(load_rgba(s("WarningBeacon.png"))), OUT, "WarningBeacon.png")

    # The Gravity Cannon on Cannon.png's 566x512 canvas over its own bounding box.
    base = load_rgba(os.path.join(LAUNCHERS, "Cannon.png"))
    ys, xs = np.nonzero(base[:, :, 3] > 40)
    box = (xs.min(), ys.min(), xs.max() + 1, ys.max() + 1)
    can = trim(keep_main(strip_ground_line(key_magenta(p("W6Cannon.png")))))
    ch, cw = can.shape[:2]
    k = min((box[2] - box[0]) / cw, (box[3] - box[1]) / ch)
    im = Image.fromarray(can, "RGBA").resize((max(1, int(cw * k)), max(1, int(ch * k))), Image.LANCZOS)
    canvas = Image.new("RGBA", (base.shape[1], base.shape[0]), (0, 0, 0, 0))
    canvas.paste(im, ((box[0] + box[2]) // 2 - im.width // 2, box[3] - im.height), im)
    save(np.asarray(canvas).copy(), LAUNCHERS, "GravityCannon.png")

    pc = split_pieces(p("W6Pieces.png"))
    save(downsize(lengthen_v(pc["post"], 1399 / 462), 1400), OUT, "ShipPost.png")
    save(downsize(lengthen(pc["plank"], 1400 / 186), 1400), OUT, "ShipPlank.png")
    save(downsize(pc["block"], 1400), OUT, "ShipBlock.png")
    save(downsize(lengthen_v(pc["pillar"], 1400 / 418), 1400), OUT, "ShipPillar.png")
    save(downsize(lengthen(pc["slab"], 1400 / 233), 1400), OUT, "ShipSlab.png")
    save(pad_to(pc["bale"], 500, (0, 63, 500, 480)), OUT, "BatteryPod.png")

    save(downsize(trim(load_rgba(s("LaserEmitter.png"))), 512), OUT, "LaserEmitter.png")
    save(plasma_orb(), OUT, "PlasmaOrb.png")
    save(downsize(trim(load_rgba(s("GravitySling.png"))), 512), OUT, "GravityRing.png")

    # The boss: the Factory Machine, padded like Commander.png. Idle faces the cannon (the _Left art).
    idle = trim(load_rgba(s("FActoryMachine_Left.png")))
    save(pad_to(idle, 500, (65, 23, 449, 469)), ROBOTS, "FactoryMachine.png")
    ih, iw = idle.shape[:2]
    k = min((449 - 65) / iw, (469 - 23) / ih)
    for src, name, flip in (("FActoryMachine.png", "FactoryMachine_Alert.png", True),
                            ("FActoryMachine_Defeated.png", "FactoryMachine_Hit.png", False)):
        body = trim(load_rgba(s(src)))
        if flip: body = body[:, ::-1].copy()
        im = Image.fromarray(body, "RGBA").resize((max(1, int(body.shape[1] * k)), max(1, int(body.shape[0] * k))), Image.LANCZOS)
        if im.width > 480 or im.height > 480:
            f = min(480 / im.width, 480 / im.height); im = im.resize((int(im.width * f), int(im.height * f)), Image.LANCZOS)
        canvas = Image.new("RGBA", (500, 500), (0, 0, 0, 0))
        canvas.paste(im, ((65 + 449) // 2 - im.width // 2, 469 - im.height), im)
        save(np.asarray(canvas).copy(), ROBOTS, name)

    space_decor(p("W6SpaceDecor.png"))
    import_mounts()


def space_decor(path):
    """SpaceDecor's sky (2026-11-12, user: "remove the flying purple drones ... insert things like asteroids, galaxy milky
    ways, comets to make the sky look busy ... remove the smaller motherships"): one Kling sheet (W6SpaceDecor.png, 2
    credits) - asteroid, comet, spiral galaxy, milky-way band. Keyed on the border only: the milky way's own pink would
    read as enclosed background pockets."""
    from scipy import ndimage
    rgba = key_magenta(path, min_pocket=10 ** 9)
    solid = rgba[:, :, 3] > 40
    lab, n = ndimage.label(solid)
    area = ndimage.sum(solid, lab, index=np.arange(1, n + 1))
    parts = []
    for i in np.argsort(area)[::-1][:4]:
        ys, xs = np.nonzero(lab == i + 1)
        crop = rgba[ys.min():ys.max() + 1, xs.min():xs.max() + 1].copy()
        crop[lab[ys.min():ys.max() + 1, xs.min():xs.max() + 1] != i + 1, 3] = 0
        parts.append(dict(x=xs.mean(), w=int(np.ptp(xs)) + 1, h=int(np.ptp(ys)) + 1, img=crop))
    milky = max(parts, key=lambda q: q["w"] / q["h"])
    rest = sorted([q for q in parts if q is not milky], key=lambda q: q["x"])
    for q, name in ((rest[0], "Decor_Asteroid.png"), (rest[1], "Decor_Comet.png"), (rest[2], "Decor_Galaxy.png"),
                    (milky, "Decor_MilkyWay.png")):
        save(downsize(trim(q["img"]), 1024), OUT, name)


MOUNTS = {"DeckLow": 0, "DeckMid": 1, "DeckTall": 2}


def import_mounts():
    data, cs = {}, []
    sheet = key_magenta(os.path.join(SRC, "W6Mounts.png"))
    for name, part in MOUNTS.items():
        rgba = trim(keep_main(strip_ground_line(sheet_part(sheet, part)), frac=0.05))
        rgba = downsize(rgba, 1600)
        save(rgba, OUT, f"Mount_{name}.png")
        h, w = rgba.shape[:2]
        uv, boxes, flat, poly = measure_mount(rgba, "solid")
        boxes = boxes[:1]
        data[name] = dict(uv=uv, aspect=w / h, boxes=boxes, flat=flat, poly=poly)
        print(f"  {name}: surfaceUV {uv:.3f}, flat top x {flat[0]:.2f}-{flat[1]:.2f}, aspect {w / h:.3f}")
        v4 = ", ".join(f"new Vector4({b[0]:.3f}f, {b[1]:.3f}f, {b[2]:.3f}f, {b[3]:.3f}f)" for b in boxes)
        cs.append(f"    public const float {name}UV = {uv:.3f}f;\n"
                  f"    public static readonly Vector4[] {name}Solids = {{ {v4} }};")
    json.dump(data, open(os.path.join(HERE, "mounts_w6.json"), "w"), indent=1)
    out = os.path.join(UNITY, "Scripts", "Level", "MountMeasurementsW6.cs")
    open(out, "w", encoding="utf-8").write(
        "// GENERATED by tools/import_mothership_art.py from the pedestal art - re-run it if the art changes.\n"
        "using UnityEngine;\n\npublic static class MountMeasurementsW6\n{\n" + "\n".join(cs) + "\n}\n")
    print("  wrote MountMeasurementsW6.cs + tools/mounts_w6.json")


if __name__ == "__main__":
    main()
