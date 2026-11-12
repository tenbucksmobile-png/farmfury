"""
Farm Fury - World 5 (Sunken City) framed-level art import (2026-11-12).

The underwater counterpart of import_sky_art.py. Reuses existing art first:
  * FarmFury_Stampede's Sunken City set: SC_Parallax_Far (sunken ruins under light shafts), SeabedStrip / SeabedFill
    (the sea floor and the colour under it), Kelp (corner silhouettes, baked to deep blue-green), the fish, jellyfish
    and bubble (SeaDecor), the clam + pearl (the pearl-spitting contraption), the ship's wheel (the rolling one), the
    sunken statue (the landmark behind the cannon).
Kling filled the gaps (kling-image-v3_0_omni i2i, 2 credits each, on magenta, laid out like their World 4 counterparts
so the same splitters work) in FarmFury_Artwork/SunkenCity/Framed/: the Sea Cannon (W5Cannon.png, a brass diving-helmet
torpedo cannon), ONE sheet with all six construction pieces (W5Pieces.png: shipwreck post + plank, teal sunken-city
block / column / slab, sponge bale) and ONE sheet with the three flat-topped coral reef mounts (W5ReefMounts.png).

Output: unity/Assets/Sprites/Environment/Sunken/, Environment/Launchers/SeaCannon.png,
        Scripts/Level/MountMeasurementsW5.cs, tools/mounts_w5.json

Usage (repo root):  python tools/import_sunken_art.py
"""
import json
import os

import numpy as np
from PIL import Image

from import_cornfield_art import trim, solid_top, measure_mount, strip_ground_line, DESKTOP
from import_frozen_art import key_magenta, pad_to
from import_watermill_art import lengthen, downsize, save, load_rgba
from import_sky_art import keep_main, lengthen_v, split_pieces, sheet_part

HERE = os.path.dirname(os.path.abspath(__file__))
ART = os.path.join(DESKTOP, "FarmFury_Technical", "FarmFury_Artwork", "SunkenCity")
SRC = os.path.join(ART, "Framed")
STAMPEDE = os.path.join(DESKTOP, "FarmFury_Stampede", "Assets", "_Project", "Sprites")
SEA_UI = os.path.join(STAMPEDE, "UI", "SunkenCity")
UNITY = os.path.join(HERE, "..", "unity", "Assets")
OUT = os.path.join(UNITY, "Sprites", "Environment", "Sunken")
LAUNCHERS = os.path.join(UNITY, "Sprites", "Environment", "Launchers")
ROBOTS = os.path.join(UNITY, "Sprites", "Enemies", "Robot")


def main():
    p = lambda n: os.path.join(SRC, n)
    s = lambda n: os.path.join(SEA_UI, n)
    print("Writing to", os.path.normpath(OUT))

    # ── backdrop ───────────────────────────────────────────────────────────────────────────────────────────────
    far = load_rgba(os.path.join(STAMPEDE, "Environment", "SC_Parallax_Far.png"))
    far[:, :, 3] = 255
    save(downsize(far, 2048), OUT, "SeaFar.png")

    # Sea floor: Stampede's seabed strip (sand lip over pebbled rock). Ground line = the top of the solid sand band.
    soil = trim(load_rgba(s("SeabedStrip.png")))
    sh, sw = soil.shape[:2]
    top = solid_top(soil, int(sw * 0.05), int(sw * 0.95), frac=0.97)
    save(soil, OUT, "SeaSoil.png")
    print(f"  SeaSoil ground line UV (from bottom) = {(sh - top - 6) / sh:.3f}   <- EnvironmentDepthSystem.SeaSoilGroundUV")
    fill = load_rgba(s("SeabedFill.png"))[:, :, :3].reshape(-1, 3).mean(axis=0) / 255.0
    print(f"  SeabedFill average colour = ({fill[0]:.2f}, {fill[1]:.2f}, {fill[2]:.2f})   <- SeaUnderfill")

    # Corner silhouettes: kelp baked to deep sea blue-green, inner edge faded (as the World 4 mushroom).
    kelp = trim(load_rgba(s("Kelp.png"))).astype(np.float32)
    kelp[:, :, :3] = kelp[:, :, :3] * 0.08 + np.array([6, 34, 44], np.float32)

    def silhouette(img, fade_left):
        c = img.copy()
        n = c.shape[1]
        t = np.linspace(0.0, 1.0, n)
        ramp = np.clip((t if fade_left else 1.0 - t) / 0.35, 0.0, 1.0)
        c[:, :, 3] *= (ramp * ramp * (3 - 2 * ramp))[None, :]
        return trim(c.clip(0, 255).astype(np.uint8))
    save(silhouette(kelp, fade_left=False), OUT, "SeaSilhouetteL.png")
    save(silhouette(kelp[:, ::-1].copy(), fade_left=True), OUT, "SeaSilhouetteR.png")

    # Sea decor (SeaDecor): fish, a jellyfish, bubbles.
    for i in range(1, 7):
        save(downsize(trim(load_rgba(s(f"Fish_{i}.png"))), 256), OUT, f"Decor_Fish{i}.png")
    save(downsize(trim(load_rgba(s("JellyFish.png"))), 512), OUT, "Decor_Jelly.png")
    save(trim(load_rgba(s("Bubble.png"))), OUT, "Decor_Bubble.png")

    # Landmark behind the cannon (CatapultLauncher.PlaceSilo): the sunken statue.
    save(trim(load_rgba(s("SunkenStatue.png"))), OUT, "SunkenStatue.png")

    # ── the Sea Cannon: fitted into Cannon.png's 566x512 canvas over its own bounding box (as the Sky Cannon) ─────
    base = load_rgba(os.path.join(LAUNCHERS, "Cannon.png"))
    ys, xs = np.nonzero(base[:, :, 3] > 40)
    box = (xs.min(), ys.min(), xs.max() + 1, ys.max() + 1)
    can = trim(keep_main(strip_ground_line(key_magenta(p("W5Cannon.png")))))
    ch, cw = can.shape[:2]
    k = min((box[2] - box[0]) / cw, (box[3] - box[1]) / ch)
    im = Image.fromarray(can, "RGBA").resize((max(1, int(cw * k)), max(1, int(ch * k))), Image.LANCZOS)
    canvas = Image.new("RGBA", (base.shape[1], base.shape[0]), (0, 0, 0, 0))
    canvas.paste(im, ((box[0] + box[2]) // 2 - im.width // 2, box[3] - im.height), im)
    save(np.asarray(canvas).copy(), LAUNCHERS, "SeaCannon.png")

    # ── construction pieces (one sheet), proportioned like World 4's ────────────────────────────────────────────
    pc = split_pieces(p("W5Pieces.png"))
    save(downsize(lengthen_v(pc["post"], 1399 / 462), 1400), OUT, "SeaPost.png")
    save(downsize(lengthen(pc["plank"], 1400 / 186), 1400), OUT, "SeaPlank.png")
    save(downsize(pc["block"], 1400), OUT, "SeaBlock.png")
    save(downsize(lengthen_v(pc["pillar"], 1400 / 418), 1400), OUT, "SeaPillar.png")
    save(downsize(lengthen(pc["slab"], 1400 / 233), 1400), OUT, "SeaSlab.png")
    # Sponge bale: padded to Haybail.png's 500 px canvas (bale rows 63..480, full width) like the cloud bales.
    save(pad_to(pc["bale"], 500, (0, 63, 500, 480)), OUT, "SpongeBale.png")

    # ── contraptions: the clam spits pearls (Sunflower in pump mode), the ship's wheel rolls (RollingBoulder) ─────
    save(downsize(trim(load_rgba(s("Clam.png"))), 512), OUT, "Clam.png")
    save(downsize(trim(load_rgba(s("pearl.png"))), 128), OUT, "Pearl.png")
    save(downsize(trim(load_rgba(s("ShipsWheel.png"))), 512), OUT, "ShipsWheel.png")

    # ── the boss: the Deep Dredger (Stampede), padded like Commander.png (500 px, body 65..449 x 23..469) as the Storm
    #    Baron. Idle = DeepDredger_Left (faces the cannon); alert = the other facing, mirrored back; hit = the wreck.
    idle = trim(load_rgba(s("DeepDredger_Left.png")))
    save(pad_to(idle, 500, (65, 23, 449, 469)), ROBOTS, "DeepDredger.png")
    ih, iw = idle.shape[:2]
    k = min((449 - 65) / iw, (469 - 23) / ih)
    for src, name, flip in (("DeepDredger.png", "DeepDredger_Alert.png", True),
                            ("DeepDredger_Defeated.png", "DeepDredger_Hit.png", False)):
        body = trim(load_rgba(s(src)))
        if flip: body = body[:, ::-1].copy()
        im = Image.fromarray(body, "RGBA").resize((max(1, int(body.shape[1] * k)), max(1, int(body.shape[0] * k))), Image.LANCZOS)
        if im.width > 480 or im.height > 480:
            f = min(480 / im.width, 480 / im.height); im = im.resize((int(im.width * f), int(im.height * f)), Image.LANCZOS)
        canvas = Image.new("RGBA", (500, 500), (0, 0, 0, 0))
        canvas.paste(im, ((65 + 449) // 2 - im.width // 2, 469 - im.height), im)
        save(np.asarray(canvas).copy(), ROBOTS, name)

    import_mounts()


# Sea mounts (LevelLoader mount variants for World 5): 0/1/2 coral reef mesas low / mid / tall standing on the sea floor
# (stretched to height like World 4's cloud mountains), 3 mesa = the tall reef. No dock, wagon or ramp.
MOUNTS = {"ReefLow": 0, "ReefMid": 1, "ReefTall": 2}


def import_mounts():
    data, cs = {}, []
    sheet = key_magenta(os.path.join(SRC, "W5ReefMounts.png"))
    for name, part in MOUNTS.items():
        rgba = trim(keep_main(strip_ground_line(sheet_part(sheet, part)), frac=0.05))
        rgba = downsize(rgba, 1600)
        save(rgba, OUT, f"Mount_{name}.png")
        h, w = rgba.shape[:2]
        uv, boxes, flat, poly = measure_mount(rgba, "solid")
        boxes = boxes[:1]   # only the plateau band is solid: the coral and rubble below are soft (as World 4's clouds)
        data[name] = dict(uv=uv, aspect=w / h, boxes=boxes, flat=flat, poly=poly)
        print(f"  {name}: surfaceUV {uv:.3f}, flat top x {flat[0]:.2f}-{flat[1]:.2f}, aspect {w / h:.3f}")
        v4 = ", ".join(f"new Vector4({b[0]:.3f}f, {b[1]:.3f}f, {b[2]:.3f}f, {b[3]:.3f}f)" for b in boxes)
        cs.append(f"    public const float {name}UV = {uv:.3f}f;\n"
                  f"    public static readonly Vector4[] {name}Solids = {{ {v4} }};")
    json.dump(data, open(os.path.join(HERE, "mounts_w5.json"), "w"), indent=1)
    out = os.path.join(UNITY, "Scripts", "Level", "MountMeasurementsW5.cs")
    open(out, "w", encoding="utf-8").write(
        "// GENERATED by tools/import_sunken_art.py from the reef mount art - re-run it if the art changes.\n"
        "using UnityEngine;\n\npublic static class MountMeasurementsW5\n{\n" + "\n".join(cs) + "\n}\n")
    print("  wrote MountMeasurementsW5.cs + tools/mounts_w5.json")


if __name__ == "__main__":
    main()
