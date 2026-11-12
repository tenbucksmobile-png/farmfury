"""
Farm Fury - the paid worlds' framed-level art import (2026-11-12): World 7 Dustbowl Canyon, World 8 Harvest Fairground,
World 9 Crop Factory. The counterpart of import_mothership_art.py, one section per world.

Existing art first (FarmFury_Stampede's per-world sets):
  World 7: DC_Parallax_Far (far), GroundStrip / GroundFill (floor + the colour under it), Saguaro (corner silhouettes
           and the spine-shooting cactus contraption), Tumbleweed (rolling contraption + decor), SandstormDust (decor),
           WaterTower (landmark), the Drill Rig (boss).
  World 8: HF_Parallax_Far, GroundStrip / GroundFill, LampPost (silhouettes), FerrisWheel (landmark), PopcornCart
           (popcorn-blasting contraption), Pumpkin (rolling contraption), the Ringmaster (boss).
  World 9: CF_Parallax_Far, DeckStrip / FactoryFill, RobotArm (silhouettes), GrainSilo (landmark), Hopper (kernel-
           spraying contraption) + CornKernel, Drone (decor), the Harvester Prime (boss).
Drawn here: the cactus spine, popcorn, the cog (World 9's rolling contraption).
Kling filled the gaps (kling-image-v3_0_omni i2i, 2 credits each, 9 images, all first try) in
FarmFury_Artwork/<World>/Framed/: W<N>Cannon.png, W<N>Pieces.png (all six construction pieces on one sheet, painted over
a grey silhouette template so split_pieces works) and W<N>Mounts.png (three flat-topped mounts, same template trick).
W7Cannon came back on red, not magenta: rekey_border() turns the border-connected background magenta first.

Output: unity/Assets/Sprites/Environment/{Canyon,Fair,Factory}/, Environment/Launchers/{Prospector,Circus,Pneumatic}
Cannon.png, Enemies/Robot/{DrillRig,Ringmaster,HarvesterPrime}*.png, Scripts/Level/MountMeasurementsW7-9.cs,
tools/mounts_w7-9.json.

Usage (repo root):  python tools/import_paid_worlds_art.py
"""
import json
import os

import numpy as np
from PIL import Image, ImageDraw
from scipy import ndimage

from import_cornfield_art import trim, solid_top, measure_mount, strip_ground_line, DESKTOP
from import_frozen_art import key_magenta, pad_to
from import_watermill_art import lengthen, downsize, save, load_rgba
from import_sky_art import keep_main, lengthen_v, split_pieces, sheet_part

HERE = os.path.dirname(os.path.abspath(__file__))
ARTWORK = os.path.join(DESKTOP, "FarmFury_Technical", "FarmFury_Artwork")
STAMPEDE = os.path.join(DESKTOP, "FarmFury_Stampede", "Assets", "_Project", "Sprites")
UNITY = os.path.join(HERE, "..", "unity", "Assets")
ENV = os.path.join(UNITY, "Sprites", "Environment")
LAUNCHERS = os.path.join(ENV, "Launchers")
ROBOTS = os.path.join(UNITY, "Sprites", "Enemies", "Robot")

WORLDS = {
    7: dict(out="Canyon", art="DustbowlCanyon", ui="DustbowlCanyon", far="Environment/DC_Parallax_Far.png",
            soil="GroundStrip.png", fill="GroundFill.png", sil="Saguaro.png", sil_col=(46, 20, 14),
            landmark=("WaterTower.png", "WaterTower.png"), cannon="ProspectorCannon.png",
            pieces=("RanchPost", "RanchPlank", "SandBlock", "SandPillar", "SandSlab", "DustyBale"),
            mounts=("MesaLow", "MesaMid", "MesaTall"),
            boss=("DrillRig", "DrillRig_Left.png", "DrillRig_Right.png", "DrillRig_Defeated.png")),
    8: dict(out="Fair", art="HarvestFairground", ui="HarvestFairground", far="Environment/HF_Parallax_Far.png",
            soil="GroundStrip.png", fill="GroundFill.png", sil="LampPost.png", sil_col=(22, 14, 34),
            landmark=("FerrisWheel.png", "FerrisWheel.png"), cannon="CircusCannon.png",
            pieces=("FairPost", "FairPlank", "FairBlock", "FairPillar", "FairSlab", "PrizeBale"),
            mounts=("PodiumLow", "PodiumMid", "PodiumTall"),
            boss=("Ringmaster", "Ringmaster_Left.png", "Ringmaster_Right.png", "Ringmaster_Defeated.png")),
    9: dict(out="Factory", art="CropFactory", ui="CropFactory", far="Environment/CF_Parallax_Far.png",
            soil="DeckStrip.png", fill="FactoryFill.png", sil="RobotArm.png", sil_col=(12, 24, 26),
            landmark=("GrainSilo.png", "GrainSilo.png"), cannon="PneumaticCannon.png",
            pieces=("FactoryPost", "FactoryPlank", "FactoryBlock", "FactoryPillar", "FactorySlab", "ProduceCrate"),
            mounts=("HousingLow", "HousingMid", "HousingTall"),
            boss=("HarvesterPrime", "HarvesterPrime_Left.png", "HarvesterPrime_Right.png", "HarvesterPrime_Defeated.png")),
}


def rekey_border(path):
    """A Kling image whose flat background came out some other colour: paint the border-connected background magenta
    (colour distance < 48 from the border's median), so key_magenta handles it like the rest. Returns a temp path."""
    rgb = np.asarray(Image.open(path).convert("RGB")).astype(np.float32)
    border = np.concatenate([rgb[0], rgb[-1], rgb[:, 0], rgb[:, -1]])
    bg = np.median(border, axis=0)
    near = np.linalg.norm(rgb - bg, axis=2) < 48
    r, g, b = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    near |= (r > 90) & (g < 0.3 * r) & (b >= 0.9 * g)   # the darker red ground shadow (rust and wood have b < g)
    lab, _ = ndimage.label(near)
    edge = np.unique(np.concatenate([lab[0], lab[-1], lab[:, 0], lab[:, -1]]))
    mask = np.isin(lab, edge[edge > 0])
    out = rgb.copy()
    out[mask] = (230, 20, 230)
    tmp = os.path.join(HERE, "__pycache__", "rekeyed_" + os.path.basename(path))
    os.makedirs(os.path.dirname(tmp), exist_ok=True)
    Image.fromarray(out.astype(np.uint8), "RGB").save(tmp)
    return tmp


def weathered_wood(rgba):
    """Re-colour a piece as sun-bleached brown timber: keep its shading (value), set a wood hue and soften saturation."""
    im = Image.fromarray(rgba, "RGBA")
    h, s_, v = [np.asarray(c).astype(np.float32) for c in im.convert("RGB").convert("HSV").split()]
    h[:] = 20.0
    s_ = s_ * 0.6
    hsv = Image.merge("HSV", [Image.fromarray(c.clip(0, 255).astype(np.uint8)) for c in (h, s_, v)]).convert("RGB")
    return np.dstack([np.asarray(hsv), rgba[:, :, 3]]).astype(np.uint8)


def silhouette(img, col, fade_left):
    c = img.astype(np.float32)
    c[:, :, :3] = c[:, :, :3] * 0.06 + np.array(col, np.float32)
    n = c.shape[1]
    t = np.linspace(0.0, 1.0, n)
    ramp = np.clip((t if fade_left else 1.0 - t) / 0.35, 0.0, 1.0)
    c[:, :, 3] *= (ramp * ramp * (3 - 2 * ramp))[None, :]
    return trim(c.clip(0, 255).astype(np.uint8))


def fit_cannon(path, name):
    """The Kling cannon on Cannon.png's 566x512 canvas over its own bounding box (PPU 384, same offsets)."""
    base = load_rgba(os.path.join(LAUNCHERS, "Cannon.png"))
    ys, xs = np.nonzero(base[:, :, 3] > 40)
    box = (xs.min(), ys.min(), xs.max() + 1, ys.max() + 1)
    can = trim(keep_main(strip_ground_line(key_magenta(path))))
    ch, cw = can.shape[:2]
    k = min((box[2] - box[0]) / cw, (box[3] - box[1]) / ch)
    im = Image.fromarray(can, "RGBA").resize((max(1, int(cw * k)), max(1, int(ch * k))), Image.LANCZOS)
    canvas = Image.new("RGBA", (base.shape[1], base.shape[0]), (0, 0, 0, 0))
    canvas.paste(im, ((box[0] + box[2]) // 2 - im.width // 2, box[3] - im.height), im)
    save(np.asarray(canvas).copy(), LAUNCHERS, name)


def boss(ui, name, idle_file, alert_file, hit_file):
    """Padded like Commander.png. Idle faces the cannon (the _Left art); alert = the _Right art mirrored."""
    idle = trim(load_rgba(os.path.join(ui, idle_file)))
    save(pad_to(idle, 500, (40, 23, 474, 469)), ROBOTS, f"{name}.png")
    ih, iw = idle.shape[:2]
    k = min((474 - 40) / iw, (469 - 23) / ih)
    for src, out, flip in ((alert_file, f"{name}_Alert.png", True), (hit_file, f"{name}_Hit.png", False)):
        body = trim(load_rgba(os.path.join(ui, src)))
        if flip: body = body[:, ::-1].copy()
        im = Image.fromarray(body, "RGBA").resize((max(1, int(body.shape[1] * k)), max(1, int(body.shape[0] * k))), Image.LANCZOS)
        if im.width > 490 or im.height > 480:
            f = min(490 / im.width, 480 / im.height); im = im.resize((int(im.width * f), int(im.height * f)), Image.LANCZOS)
        canvas = Image.new("RGBA", (500, 500), (0, 0, 0, 0))
        canvas.paste(im, ((40 + 474) // 2 - im.width // 2, 469 - im.height), im)
        save(np.asarray(canvas).copy(), ROBOTS, out)


def column_part(rgba, index):
    """The index-th of three mounts. The canyon's foot shrubs join neighbours, so sheet_part's separate-shapes split
    does not work: cut where the fewest pixels stand in each between-mounts window of columns, then keep the biggest
    shape in that column."""
    a = rgba[:, :, 3] > 40
    count = np.convolve(a.sum(axis=0).astype(np.float32), np.ones(9) / 9, mode="same")
    w = a.shape[1]
    cuts = [int(lo * w) + int(np.argmin(count[int(lo * w):int(hi * w)])) for lo, hi in ((0.22, 0.48), (0.52, 0.78))]
    w = rgba.shape[1]
    edges = [0] + cuts + [w]
    out = rgba.copy()
    out[:, :edges[index], 3] = 0
    out[:, edges[index + 1]:, 3] = 0
    return trim(keep_main(out, frac=0.3))


def import_mounts(world, sheet_path, names, out):
    data, cs = {}, []
    sheet = key_magenta(sheet_path)
    for part, name in enumerate(names):
        rgba = trim(keep_main(strip_ground_line(column_part(sheet, part)), frac=0.05))
        rgba = downsize(rgba, 1600)
        save(rgba, out, f"Mount_{name}.png")
        h, w = rgba.shape[:2]
        uv, boxes, flat, poly = measure_mount(rgba, "solid")
        boxes = boxes[:1]
        if world == 8:
            # The podiums are straight-sided boxes whose bulb rim reads as a full-width plateau (0.01-0.99); every other
            # world's mounts are solid only in their middle ~60%, which is what lets neighbouring districts overhang a
            # mount's edge. Inset the podium's solid band and plateau to the middle 80% (2026-11-12: with the full
            # width, World 8's cities collided podium-to-podium and W8_L09 found no layout in 1500 seeds).
            boxes = [(max(b[0], 0.1), min(b[1], 0.9), b[2], b[3]) for b in boxes]
            flat = (max(flat[0], 0.1), min(flat[1], 0.9))
        data[name] = dict(uv=uv, aspect=w / h, boxes=boxes, flat=flat, poly=poly)
        print(f"  {name}: surfaceUV {uv:.3f}, flat top x {flat[0]:.2f}-{flat[1]:.2f}, aspect {w / h:.3f}")
        v4 = ", ".join(f"new Vector4({b[0]:.3f}f, {b[1]:.3f}f, {b[2]:.3f}f, {b[3]:.3f}f)" for b in boxes)
        cs.append(f"    public const float {name}UV = {uv:.3f}f;\n"
                  f"    public static readonly Vector4[] {name}Solids = {{ {v4} }};")
    json.dump(data, open(os.path.join(HERE, f"mounts_w{world}.json"), "w"), indent=1)
    path = os.path.join(UNITY, "Scripts", "Level", f"MountMeasurementsW{world}.cs")
    open(path, "w", encoding="utf-8").write(
        "// GENERATED by tools/import_paid_worlds_art.py from the mount art - re-run it if the art changes.\n"
        f"using UnityEngine;\n\npublic static class MountMeasurementsW{world}\n{{\n" + "\n".join(cs) + "\n}\n")
    print(f"  wrote MountMeasurementsW{world}.cs + tools/mounts_w{world}.json")


# ── drawn contraption shots and the cog ───────────────────────────────────────────────────────────────────────────
def spine(w=48, h=192):
    """A cactus spine: a long tan needle with a dark outline, point at the top."""
    im = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    pts = [(w / 2, 2), (w * 0.78, h * 0.85), (w / 2, h - 2), (w * 0.22, h * 0.85)]
    d.polygon(pts, fill=(238, 214, 160, 255), outline=(60, 36, 20, 255), width=5)
    return np.asarray(im).copy()


def popcorn(n=128):
    """A popcorn puff: a cluster of cream bumps with a butter-yellow core and a dark outline."""
    im = Image.new("RGBA", (n, n), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    blobs = [(0.5, 0.36, 0.22), (0.32, 0.52, 0.2), (0.68, 0.52, 0.2), (0.5, 0.66, 0.2), (0.4, 0.4, 0.16), (0.62, 0.38, 0.16)]
    for cx, cy, r in blobs:
        d.ellipse([(cx - r) * n - 4, (cy - r) * n - 4, (cx + r) * n + 4, (cy + r) * n + 4], fill=(70, 45, 25, 255))
    for cx, cy, r in blobs:
        d.ellipse([(cx - r) * n, (cy - r) * n, (cx + r) * n, (cy + r) * n], fill=(255, 248, 225, 255))
    d.ellipse([0.44 * n, 0.48 * n, 0.56 * n, 0.6 * n], fill=(250, 205, 90, 255))
    return np.asarray(im).copy()


def cog(n=512, teeth=10):
    """A teal factory cog with a dark outline, a hub and hazard-striped centre (rolls like the other wheels)."""
    big = n * 2
    im = Image.new("RGBA", (big, big), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    c = big / 2
    def gear(r_out, r_in, col):
        pts = []
        for i in range(teeth * 4):
            a = i * np.pi * 2 / (teeth * 4)
            r = r_out if (i % 4) in (1, 2) else r_in
            pts.append((c + r * np.cos(a), c + r * np.sin(a)))
        d.polygon(pts, fill=col)
    gear(big * 0.48, big * 0.39, (24, 34, 38, 255))
    gear(big * 0.45, big * 0.365, (58, 128, 128, 255))
    d.ellipse([c - big * 0.3, c - big * 0.3, c + big * 0.3, c + big * 0.3], fill=(24, 34, 38, 255))
    d.ellipse([c - big * 0.27, c - big * 0.27, c + big * 0.27, c + big * 0.27], fill=(78, 150, 148, 255))
    for k in range(6):   # spokes cut-outs
        a = k * np.pi / 3 + np.pi / 6
        x, y = c + big * 0.17 * np.cos(a), c + big * 0.17 * np.sin(a)
        d.ellipse([x - big * 0.055, y - big * 0.055, x + big * 0.055, y + big * 0.055], fill=(24, 34, 38, 255))
    d.ellipse([c - big * 0.1, c - big * 0.1, c + big * 0.1, c + big * 0.1], fill=(24, 34, 38, 255))
    d.ellipse([c - big * 0.08, c - big * 0.08, c + big * 0.08, c + big * 0.08], fill=(245, 200, 40, 255))
    return np.asarray(im.resize((n, n), Image.LANCZOS)).copy()


def main():
    for world, w in WORLDS.items():
        ui = os.path.join(STAMPEDE, "UI", w["ui"])
        src = os.path.join(ARTWORK, w["art"], "Framed")
        out = os.path.join(ENV, w["out"])
        u = lambda n: os.path.join(ui, n)
        print(f"World {world} -> {os.path.normpath(out)}")

        far = load_rgba(os.path.join(STAMPEDE, w["far"]))
        far[:, :, 3] = 255
        save(downsize(far, 2048), out, f"{w['out']}Far.png")

        soil = trim(load_rgba(u(w["soil"])))
        sh, sw = soil.shape[:2]
        top = solid_top(soil, int(sw * 0.05), int(sw * 0.95), frac=0.97)
        save(soil, out, f"{w['out']}Soil.png")
        print(f"  {w['out']}Soil ground line UV (from bottom) = {(sh - top - 4) / sh:.3f}   <- EnvironmentDepthSystem.{w['out']}SoilGroundUV")
        fill = load_rgba(u(w["fill"]))[:, :, :3].reshape(-1, 3).mean(axis=0) / 255.0
        print(f"  {w['fill']} average colour = ({fill[0]:.2f}, {fill[1]:.2f}, {fill[2]:.2f})   <- {w['out']}Underfill")

        sil = trim(load_rgba(u(w["sil"])))
        save(silhouette(sil, w["sil_col"], fade_left=False), out, f"{w['out']}SilhouetteL.png")
        save(silhouette(sil[:, ::-1].copy(), w["sil_col"], fade_left=True), out, f"{w['out']}SilhouetteR.png")

        save(trim(load_rgba(u(w["landmark"][0]))), out, w["landmark"][1])

        cannon_src = os.path.join(src, f"W{world}Cannon.png")
        if world == 7: cannon_src = rekey_border(cannon_src)
        fit_cannon(cannon_src, w["cannon"])

        pc = split_pieces(os.path.join(src, f"W{world}Pieces.png"))
        post, plank, block, pillar, slab, bale = w["pieces"]
        save(downsize(lengthen_v(pc["post"], 1399 / 462), 1400), out, f"{post}.png")
        if world == 7: pc["plank"] = weathered_wood(pc["plank"])   # Kling painted it red like the slab
        save(downsize(lengthen(pc["plank"], 1400 / 186), 1400), out, f"{plank}.png")
        save(downsize(pc["block"], 1400), out, f"{block}.png")
        save(downsize(lengthen_v(pc["pillar"], 1400 / 418), 1400), out, f"{pillar}.png")
        save(downsize(lengthen(pc["slab"], 1400 / 233), 1400), out, f"{slab}.png")
        save(pad_to(pc["bale"], 500, (0, 63, 500, 480)), out, f"{bale}.png")

        boss(ui, *w["boss"])
        import_mounts(world, os.path.join(src, f"W{world}Mounts.png"), w["mounts"], out)

    # Contraptions and decor.
    can = os.path.join(ENV, "Canyon")
    dc = os.path.join(STAMPEDE, "UI", "DustbowlCanyon")
    save(downsize(trim(load_rgba(os.path.join(dc, "Saguaro.png"))), 512), can, "Cactus.png")
    save(spine(), can, "Spine.png")
    save(downsize(trim(load_rgba(os.path.join(dc, "Tumbleweed.png"))), 512), can, "Tumbleweed.png")
    save(downsize(trim(load_rgba(os.path.join(dc, "SandstormDust.png"))), 1024), can, "Decor_Dust.png")
    fair = os.path.join(ENV, "Fair")
    hf = os.path.join(STAMPEDE, "UI", "HarvestFairground")
    save(downsize(trim(load_rgba(os.path.join(hf, "PopcornCart.png"))), 512), fair, "PopcornCart.png")
    save(popcorn(), fair, "Popcorn.png")
    save(downsize(trim(load_rgba(os.path.join(hf, "Pumpkin.png"))), 512), fair, "Pumpkin.png")
    fac = os.path.join(ENV, "Factory")
    cf = os.path.join(STAMPEDE, "UI", "CropFactory")
    save(downsize(trim(load_rgba(os.path.join(cf, "Hopper.png"))), 512), fac, "CornHopper.png")
    save(downsize(trim(load_rgba(os.path.join(STAMPEDE, "UI", "CornKernel.png"))), 128), fac, "Kernel.png")
    save(cog(), fac, "Cog.png")
    save(downsize(trim(load_rgba(os.path.join(cf, "Drone.png"))), 384), fac, "Decor_Drone.png")


if __name__ == "__main__":
    main()
