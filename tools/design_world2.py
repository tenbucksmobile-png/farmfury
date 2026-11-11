"""
Farm Fury - World 2 (Frozen Tundra) level designer (2026-11-09).

World 2 built on the World 1 template (user: "take that template and build frozen tundra"): framed levels with live
physics, tall frozen structures (frosted timber, ice instead of stone), the frozen mounts (ice floes, ice pillars,
frosted scaffolds, snow ramps with sleds instead of wagons), the frozen contraptions (ice crystals that spray shards,
frosted hoists that drop a block of ice, rolling ice boulders), the frost robots and the Frost Tyrant boss, who is
dropped off by the mothership. Uses the shared piece/mount/check code in design_world1.py (world=2).

Usage (repo root):  python tools/design_world2.py [--render OUTDIR]
Writes unity/Assets/Editor/LevelDataGenerator.World2.cs; then FarmFury -> Generate All Level Data.
"""
import math
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import design_world1 as d   # noqa: E402  (its World 1 levels build on import; harmless)

G = d.G
P, W, C, B = "Percy", "Woolly", "Cluck", "Bessie"
LEVELS = []
tower, seesaw, ramp_wagon, edge_wagon = d.tower, d.seesaw, d.ramp_wagon, d.edge_wagon


def level(k, name, par, birds, comment, boss=False):
    key = name.replace("'", "").replace(" ", "")
    file = "L19_FrostboundWatch" if k == 1 else f"W02_L{k:02d}_{key}"
    L = d.Level(file, f"W2_L{k:02d}", name, par, birds, comment, boss=boss, world=2)
    L.weather = "AuroraNight"
    LEVELS.append(L)
    return L


# ═══════════════════════════════════════════ the levels ═══════════════════════════════════════════════════
L = level(1, "Frostbound Watch", 2, [P, P, C], """
Percy's first level: a frosted two-storey watch hut with a robot on each floor and one on the roof, and an ice
crystal by the door — hit it and it fires a fan of ice shards into the hut.""")
tower(L, 7.8, G, 2, width=1.8, robots=["pawn", "pawn"], top="pawn")
L.sunflower(5.9, G, 1.5)

L = level(2, "Snowdrift Watch", 2, [P, P, C], """
A three-storey tower on an ice floe; at the floe's left edge an ice boulder waits — knock it and it rolls straight
into the tower's legs.""")
L.weather = "Snowfall"
s = L.island(10.0, G + 1.2, 5.6, 0)
tower(L, 10.8, s, 3, width=1.8, robots=["semi", None, "pawn"], top="pawn")
L.boulder(8.2, s, 1.1)

L = level(3, "Icicle Ridge", 2, [P, W, C], """
A four-storey tower on an ice-block ground floor, and a see-saw beam balancing a robot against a crate.""")
L.weather = "IceFog"
tower(L, 9.8, G, 4, width=2.0, taper=0.12, stone_floors=1, robots=["semi", None, "pawn", None], top="pawn")
seesaw(L, 6.9, G, 1.0, 2.4, left="crate", right="pawn")

L = level(4, "Sled Run", 2, [P, W, C], """
A loaded sled sits at the top of a snow ramp. Knock it and it slides down the ice into the five-storey tower below.""")
L.weather = "PolarDawn"
deck, dl, dr, end = ramp_wagon(L, 6.4)
L.box(dl + 0.35, deck, 0.5, stone=False); L.robot(dl + 1.05, deck, "pawn")
tower(L, end + 1.25, G, 5, width=2.0, taper=0.15, robots=["semi", None, "pawn", None, "semi"], top="pawn")

L = level(5, "Frozen Silo", 2, [W, P, C], """
Woolly's level: a narrow six-storey frosted silo tower, an ice crystal in front of it.""")
tower(L, 10.2, G, 6, width=1.7, h=1.15, taper=0.08, robots=["semi", None, "pawn", None, "pawn", None], top="semi")
L.sunflower(8.0, G, 1.5)

L = level(6, "Glacier Steps", 2, [P, W, C], """
A staircase of ice blocks on a low floe with a robot on every step and a two-storey lookout on the top step.""")
L.weather = "Snowfall"
s = L.island(9.8, G + 1.0, 5.6, 0)
for row in range(4):
    for k in range(5 - row):
        top = L.box(7.6 + (row + k) * 0.8, s + row * 0.8, 0.8)
    if row < 3: L.robot(7.6 + row * 0.8, s + (row + 1) * 0.8, "pawn")
tower(L, 10.4, s + 3.2, 2, width=1.6, h=1.15, taper=0.1, robots=["semi", None], top="pawn")

L = level(7, "Polar Outpost", 2, [W, W, C], """
In a blizzard: robots in frosted lean-tos along a floe, two of them two storeys high; a frosted hoist hangs a block
of ice over the middle one.""")
L.weather = "Blizzard"
s = L.island(9.8, G + 1.8, 6.6, 0)
for x, two in ((7.5, True), (9.35, False), (11.1, True)):
    r = L.hut(x - 0.65, x + 0.65, s, 1.15)
    L.robot(x, s, "pawn")
    if two:
        L.hut(x - 0.55, x + 0.55, r, 1.15)
        L.robot(x, r, "pawn")
L.hoist(12.5, s, 4.8, 0.6)

L = level(8, "Ice Bridge", 2, [P, W, B], """
Two ice pillars carrying two-storey huts joined by a plank bridge with a robot on it.""")
L.weather = "IceFog"
tops = []
for xc in (7.4, 11.8):
    s_, l, r = L.mount("Mesa", xc, 2.6)
    c = (l + r) / 2
    tops.append((c, tower(L, c, s_, 2, width=1.0, h=1.15, taper=0.0, robots=["pawn", None])))
br = L.plank(tops[0][0] + 0.25, tops[1][0] - 0.25, max(tops[0][1], tops[1][1]))
L.robot((tops[0][0] + tops[1][0]) / 2, br, "semi")

L = level(9, "Avalanche", 3, [P, P, W], """
An ice boulder sits on the edge of a high floe, right above an ice keep. Knock it off and it crashes down on the
keep's roof.""")
hi = L.island(6.4, G + 4.4, 3.0, 0)
L.boulder(7.4, hi, 1.1)
top = L.post(8.6, G, 1.6, stone=True); L.post(11.0, G, 1.6, stone=True)
r = L.plank(8.4, 11.2, top, stone=True)
L.robot(9.8, G, "harv")
tower(L, 9.8, r, 2, width=2.2, h=1.15, taper=0.3, robots=["pawn", None], top="pawn")
L.robot(12.4, G, "pawn")

L = level(10, "Barrel Igloo", 2, [W, P, B], """
Frost barrels packed into an ice-block mound with robots on top; see-saws to either side.""")
L.weather = "PolarDawn"
for row, n in enumerate((4, 3, 2)):
    b = G + row * 0.8
    for k in range(n):
        top = L.box(9.6 + (k - (n - 1) / 2) * 0.8, b, 0.8)
L.barrel(7.8, G); L.barrel(11.4, G)
tower(L, 9.6, G + 1.6 + 0.8, 2, width=1.6, h=1.15, taper=0.1, robots=["semi", "pawn"], top="pawn")
seesaw(L, 6.2, G, 0.9, 2.2, left="crate", right="pawn")
seesaw(L, 13.0, G, 0.9, 2.2, left="pawn", right="barrel")

L = level(11, "Twin Spires", 3, [P, W, C], """
Two four-storey frosted towers on icy scaffolds, joined at the top by a plank bridge with a robot on it.""")
L.weather = "Snowfall"
cs = []
for xc in (8.0, 11.9):
    s_, l, r = L.mount("Scaffold", xc, 2.4)
    c = (l + r) / 2
    cs.append(c)
    top = tower(L, c, s_, 4, width=1.8, h=1.15, taper=0.12, robots=["semi", None, "pawn", None])
br = L.plank(cs[0] + 0.3, cs[1] - 0.3, top)
L.robot((cs[0] + cs[1]) / 2, br, "semi")

L = level(12, "Frost Keep", 2, [W, P, B], """
In a blizzard: a Harvester in an ice keep with a two-storey watch-house and a see-saw on its roof. High on a floe to
the left a sled waits at the edge above the frost barrel at the keep's door.""")
L.weather = "Blizzard"
top = L.post(8.6, G, 1.6, stone=True); L.post(11.0, G, 1.6, stone=True)
r = L.plank(8.4, 11.2, top, stone=True)
L.robot(9.8, G, "harv"); L.barrel(7.8, G)
t = tower(L, 9.8, r, 2, width=2.2, h=1.15, taper=0.3, robots=["pawn", None])
seesaw(L, 9.8, t, 0.7, 2.4, left="pawn", right="barrel")
hi = L.island(6.4, G + 4.6, 2.8, 0)
deck, dl, dr = edge_wagon(L, hi, 6.4 + 1.4 * 0.92, 2.0)
L.box(dl + 0.35, deck, 0.5, stone=False); L.robot(dl + 1.0, deck, "pawn")

L = level(13, "Sled Train", 3, [P, W, C], """
A sled carrying a little hut slides off the snow ramp into an ice-and-timber fortress with a four-storey tower.""")
deck, dl, dr, end = ramp_wagon(L, 5.8)
r = L.hut(dl + 0.15, dl + 1.35, deck, 1.15); L.robot(dl + 0.75, deck, "pawn")
x0 = end + 0.3
L.barrel(x0 + 0.3, G)
t = L.box(x0 + 1.1, G, 0.8); t = L.box(x0 + 1.1, t, 0.8); L.robot(x0 + 1.1, t, "pawn")
f = L.hut(x0 + 2.0, x0 + 4.2, G, 1.3, stone=True, roof_stone=True); L.robot(x0 + 3.1, G, "semi")
tower(L, x0 + 3.1, f, 4, width=1.9, h=1.15, taper=0.12, robots=["pawn", None, "semi", None], top="harv")

L = level(14, "Crystal Garden", 2, [W, W, P], """
Three short frosted towers, each with an ice crystal in front of it: set the crystals off and their shards do the
work.""")
L.weather = "IceFog"
for i, xc in enumerate((7.6, 10.0, 12.4)):
    tower(L, xc, G, 2 + (i % 2), width=1.5, h=1.15, taper=0.1, robots=["pawn", None, "pawn"][:2 + (i % 2)], top="pawn")
    L.sunflower(xc - 1.15, G, 1.3)

L = level(15, "Hanging Ice", 3, [P, W, B], """
A long two-storey frosted barracks full of robots, with a tall frosted hoist hanging a block of ice over its roof.""")
L.weather = "Snowfall"
for x in (7.4, 9.4, 11.4):
    top = L.post(x, G, 1.3)
L.plank(7.23, 9.4, top); f1 = L.plank(9.4, 11.57, top)
for x in (8.4, 10.4): L.robot(x, G, "semi")
r = L.hut(7.9, 10.9, f1, 1.15); L.robot(8.7, f1, "pawn"); L.robot(10.1, f1, "pawn")
L.hoist(13.9, G, 6.4, 0.3)

L = level(16, "Pillar Drop", 3, [P, W, C], """
A sled with a robot aboard sits on top of a tall ice pillar. Tip it over the edge into the five-storey tower below;
a three-storey tower stands on a floe behind.""")
L.weather = "PolarDawn"
s, l, r = L.mount("Mesa", 7.2, 3.4)
deck, dl, dr = edge_wagon(L, s, r, 2.0)
L.box(dl + 0.3, deck, 0.5, stone=False); L.robot(dl + 0.95, deck, "pawn")
tower(L, 10.4, G, 5, width=2.0, taper=0.15, robots=["semi", None, "pawn", None, "semi"], top="pawn")
s2 = L.island(12.9, G + 1.2, 2.4, 0)
tower(L, 12.9, s2, 3, width=1.6, h=1.15, taper=0.1, robots=["pawn", None, "semi"])

L = level(17, "Whiteout", 3, [W, P, B], """
In a blizzard: a five-storey tower on stilts of frozen hay bales, and an ice boulder on the snow ramp behind it.""")
L.weather = "Blizzard"
s, l, r = L.mount("Ramp", 6.0, 5.0)
L.boulder(l + 0.9, s, 1.1)
x0 = 6.0 + 2.5 + 1.8
for x in (x0 - 0.95, x0, x0 + 0.95):
    t = L.hay(x, G)
f = L.plank(x0 - 1.5, x0 + 1.5, t)
tower(L, x0, f, 5, width=2.2, h=1.1, taper=0.2, robots=["semi", "pawn", None, "semi", None], top="pawn", wonky=False)   # on bale stilts: wonky settled by itself (probe)

L = level(18, "Northern Lights", 3, [P, W, C], """
Under the aurora: a six-storey tower with see-saws balancing robots on both sides.""")
tower(L, 9.8, G, 6, width=2.1, h=1.15, taper=0.16, stone_floors=1, robots=["semi", None, "pawn", None, "pawn", None], top="semi")
seesaw(L, 6.9, G, 0.9, 2.2, left="crate", right="pawn")
seesaw(L, 12.8, G, 0.9, 2.2, left="pawn", right="barrel")

L = level(19, "Ice Palace", 3, [W, P, B], """
An ice palace on a floe: a hall of ice blocks with a tall frosted tower at each end and a robot in every room.""")
L.weather = "IceFog"
s = L.island(10.0, G + 1.0, 7.0, 0)
for row, n in enumerate((5, 4)):
    for k in range(n):
        top = L.box(10.0 + (k - (n - 1) / 2) * 0.8, s + row * 0.8, 0.8)
L.robot(10.0, top, "pawn")
for xc in (7.6, 12.4):
    tower(L, xc, s, 3, width=1.4, h=1.15, taper=0.05, robots=["pawn", None, "semi"], top="pawn")

L = level(20, "Glacier Gate", 3, [P, W, B], """
A gate of two ice towers joined by an ice-slab arch, robots in the gatehouse; a frosted hoist dangles a block of ice
over the arch.""")
L.weather = "Snowfall"
for xc in (8.4, 11.2):
    t = L.box(xc, G, 0.8); t = L.box(xc, t, 0.8); t = L.box(xc, t, 0.8)
arch = L.plank(7.8, 11.8, t, stone=True)
L.robot(9.8, G, "harv")
r = L.hut(8.6, 11.0, arch, 1.15); L.robot(9.8, arch, "pawn")
tower(L, 9.8, r, 2, width=1.8, h=1.15, taper=0.2, robots=["semi", None], top="pawn")
L.hoist(15.9, G, 6.2, 0.3)

L = level(21, "Frozen Fortress", 3, [W, P, B], """
A frozen fortress on a floe — an ice-block ground floor and three frosted storeys — with an ice boulder poised on the
snow ramp in front.""")
L.weather = "PolarDawn"
s0, l0, r0 = L.mount("Ramp", 5.6, 4.6)
L.boulder(l0 + 0.8, s0, 1.0)
s = L.island(11.0, G + 1.0, 5.2, 0)
for x in (9.4, 11.0, 12.6):
    top = L.post(x, s, 1.3, stone=True)
L.plank(9.19, 11.0, top, stone=True); f1 = L.plank(11.0, 12.81, top, stone=True)
L.robot(10.2, s, "semi"); L.robot(11.8, s, "semi")
tower(L, 11.0, f1, 3, width=2.6, h=1.15, taper=0.35, robots=["pawn", None, "semi"], top="pawn")

L = level(22, "The Frost Tyrant", 3, [P, W, B], """
Boss, under the aurora: the mothership drops the Frost Tyrant onto a three-storey ice-and-timber keep on a floe;
ice crystals guard the keep's door and a sentry watches from an ice-block tower.""", boss=True)
s = L.island(10.8, G + 2.0, 5.4, 0)
top = L.post(9.4, s, 1.5, stone=True); L.post(12.2, s, 1.5, stone=True)
r = L.plank(9.1, 12.5, top, stone=True)
L.robot(10.8, s, "semi"); L.barrel(8.6, s); L.barrel(13.0, s)
t = tower(L, 10.8, r, 3, width=2.4, h=1.15, taper=0.2, robots=["pawn", None, "semi"], wonky=False)   # the boss lands on it
L.robot(10.8, t, "boss")
t = L.box(6.6, G, 0.8)
for _ in range(3): t = L.box(6.6, t, 0.8)
L.robot(6.6, t, "pawn")
L.sunflower(7.9, G, 1.4)


# ═══════════════════════════════════════════ output ═══════════════════════════════════════════════════════
def write_cs():
    body = "\n\n".join(L.cs() for L in LEVELS)
    keep = ", ".join(f'"{L.file}"' for L in LEVELS)
    src = f"""// GENERATED by tools/design_world2.py — edit the designs there, not here.
// World 2 (Frozen Tundra) levels W2_L01-W2_L22 (2026-11-09): the World 1 template, frozen — framed, live physics,
// frozen mounts and contraptions, the Frost Tyrant boss.

using UnityEditor;

public static partial class LevelDataGenerator
{{
    static void MakeWorld2Framed(string folder)
    {{
        // Drop any older World 2 asset that isn't one of these (the generated W02_* set had different names).
        var keep = new System.Collections.Generic.HashSet<string> {{ {keep} }};
        foreach (var guid in AssetDatabase.FindAssets("t:LevelData", new[] {{ folder }}))
        {{
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string file = System.IO.Path.GetFileNameWithoutExtension(path);
            if (file.StartsWith("W02_") && !keep.Contains(file)) AssetDatabase.DeleteAsset(path);
        }}

{body}
    }}
}}
"""
    path = os.path.join(d.UNITY, "Editor", "LevelDataGenerator.World2.cs")
    open(path, "w", encoding="utf-8").write(src)
    print("wrote", os.path.normpath(path))


def render(outdir):
    from PIL import Image, ImageOps, ImageDraw
    import numpy as np
    S = os.path.join(d.UNITY, "Sprites")
    PX, camH, asp = 40, 16.0, 16 / 9
    camW = camH * asp
    cx, cy = -3.72 + (0.5 - 0.26) * camW, G + (0.5 - 0.12) * camH
    left, bottom = cx - camW / 2, cy - camH / 2
    cache = {}

    def load(p):
        if p not in cache: cache[p] = Image.open(os.path.join(S, p)).convert("RGBA")
        return cache[p]

    def put(canvas, img, x, y, w, h, alpha=1.0, flip=False):
        im = img.resize((max(1, int(w * PX)), max(1, int(h * PX))))
        if flip: im = ImageOps.mirror(im)
        if alpha < 1:
            a = np.asarray(im).copy(); a[..., 3] = (a[..., 3] * alpha).astype(np.uint8); im = Image.fromarray(a)
        canvas.paste(im, (int((x - w / 2 - left) * PX), int((bottom + camH - (y + h / 2)) * PX)), im)

    def tint(img, c):
        a = np.asarray(img).astype(np.float32).copy(); a[..., :3] *= np.array(c[:3], np.float32); return Image.fromarray(a.clip(0, 255).astype(np.uint8))

    def strip(canvas, p, h, b, col=(1, 1, 1)):
        im = tint(load(p), col); tw = h * im.size[0] / im.size[1]; k = math.floor((left - cx) / tw)
        for i in range(int(camW / tw) + 3):
            put(canvas, ImageOps.mirror(im) if (k + i) & 1 else im, cx + (k + i + 0.5) * tw, b + h / 2, tw, h)

    def overlay(canvas, col, a):
        if a <= 0: return
        canvas.alpha_composite(Image.new("RGBA", canvas.size, tuple(int(255 * v) for v in col) + (int(255 * a),)))

    WX = {  # mirrors CornfieldAmbience's frozen profiles
        "AuroraNight": dict(sky=(1, 1, 1), land=(.8, .86, 1), haze=(.52, .6, .86), hazeA=.2, grade=((.1, .14, .4), .10), snow=.35),
        "Snowfall": dict(sky=(.86, .9, 1), land=(.9, .94, 1), haze=(.74, .8, .96), hazeA=.32, grade=((.55, .65, .9), .06), snow=1),
        "Blizzard": dict(sky=(.68, .74, .88), land=(.78, .82, .92), haze=(.82, .86, .96), hazeA=.48, grade=((.75, .82, .96), .16), snow=1),
        "IceFog": dict(sky=(.8, .85, .96), land=(.84, .89, .97), haze=(.84, .9, 1), hazeA=.55, grade=((.82, .88, 1), .12), snow=.25),
        "PolarDawn": dict(sky=(1, .78, .84), land=(1, .9, .93), haze=(1, .74, .72), hazeA=.28, grade=((.95, .45, .5), .08), snow=.3),
    }
    E = "Environment/Frozen/"
    bases = {}

    def backdrop(wx):
        w = WX[wx]
        base = Image.new("RGBA", (int(camW * PX), int(camH * PX)), (30, 30, 60, 255))
        far = tint(load("Environment/Skies/ParallaxFarHills_W2.png"), w["sky"])
        anchor = G - 0.3 + 4.2 * 0.8
        fh = max(camW * far.size[1] / far.size[0], bottom + camH - anchor); fw = fh * far.size[0] / far.size[1]
        put(base, far, cx, anchor + fh / 2, fw, fh)
        strip(base, E + "FrozenRidges.png", 4.4, G + 3.0, w["sky"])
        overlay(base, w["haze"], w["hazeA"])
        strip(base, E + "FrozenHills.png", 3.3, G + 2.7, w["land"])
        strip(base, E + "FrozenMid.png", 4.2, G - 0.3, w["land"])
        strip(base, E + "FrozenWall.png", 2.6, G - 0.15, tuple(0.85 * v for v in w["land"]))
        soil = load(E + "FrozenSoil.png"); sh = 3.1; tw = sh * soil.size[0] / soil.size[1]
        for i in range(-2, 3): put(base, soil, cx + i * tw, G - sh * 0.851 + sh / 2, tw, sh)
        return base

    pieces = {k: v + ".png" for k, v in d.SPRITE_W2.items()}
    robots = {"pawn": "Enemies/Robot/FrostRobot_left.png", "semi": "Enemies/Robot/GlacierHarvestor_left.png",
              "harv": "Enemies/Robot/IceHarvestor_left.png", "boss": "Enemies/Robot/FrostTyrant.png"}
    mounts = {0: "Mount_Floe", 1: "Mount_Floe", 3: "Mount_IcePillar", 4: "Mount_FrostScaffold", 5: "Mount_Sled", 6: "Mount_SnowRamp"}
    os.makedirs(outdir, exist_ok=True)
    thumbs = []
    for L in LEVELS:
        if L.weather not in bases: bases[L.weather] = backdrop(L.weather)
        c = bases[L.weather].copy()
        hb = load(E + "FrozenBale.png")
        for row in range(2):
            for k in range(3 - row): put(c, hb, -3.87 + (k - (2 - row) * 0.5) * 0.95, G + 1.15 * (0.5 - 0.038) + row * 0.8, 1.15, 1.15)
        can = load("Environment/Launchers/IceCannon.png"); put(c, can, -3.72, G + 2.2, 1.6, 1.6 * can.size[1] / can.size[0])
        for isl in L.islands:
            im = load(E + mounts[isl["v"]] + ".png"); h = isl["h"]
            put(c, im, isl["x"], isl["s"] - isl["uv"] * h + h / 2, isl["w"], h)
        for g in L.gadgets:
            if g["type"] == "IceCrystal":
                im = load("Environment/World2Props/IceCrystals.png"); h = g["size"]; put(c, im, g["x"], g["y"] + h / 2, h * im.size[0] / im.size[1], h)
            elif g["type"] == "IceBoulder":
                im = load("Environment/World2Props/IceBoulder.png"); put(c, im, g["x"], g["y"] + g["size"] / 2, g["size"], g["size"])
            else:
                put(c, load(E + "FrozenCrane.png"), g["cl"] + g["cw"] / 2, g["y"] + g["size"] / 2, g["cw"], g["size"], flip=g["left"])
                bl, br, bb, bt = g["bucket"]; ax, ay = g["anchor"]
                ImageDraw.Draw(c).line([((ax - left) * PX, (bottom + camH - ay) * PX), ((ax - left) * PX, (bottom + camH - bt) * PX)], fill=(140, 107, 64, 255), width=2)
                im = load("Environment/World2Props/IceBlock.png"); bh = (br - bl) * im.size[1] / im.size[0]
                put(c, im, (bl + br) / 2, bt - bh / 2, br - bl, bh)
        for b in L.blocks:
            put(c, load(pieces[b["kind"]]), b["x"], b["y"], b["w"], b["h"])
        for r in L.robots:
            im = load(robots[r["kind"]]); w = r["s"] * im.size[0] / 1746; h = r["s"] * im.size[1] / 1746
            put(c, im, r["x"], r["y"], w, h)
        overlay(c, *WX[L.weather]["grade"])
        import random as _r
        rnd = _r.Random(5); dr = ImageDraw.Draw(c)
        for _ in range(int(160 * WX[L.weather]["snow"])):
            x, y = rnd.uniform(0, c.size[0]), rnd.uniform(0, c.size[1]); rr = rnd.uniform(1, 3.5)
            dr.ellipse([x - rr, y - rr, x + rr, y + rr], fill=(255, 255, 255, 200))
        c = c.convert("RGB"); c.save(os.path.join(outdir, L.file + ".png")); thumbs.append((L, c))
    tw_, th_ = 480, 270
    sheet = Image.new("RGB", (tw_ * 3, th_ * math.ceil(len(thumbs) / 3)), (30, 30, 30))
    dd = ImageDraw.Draw(sheet)
    for i, (L, c) in enumerate(thumbs):
        sheet.paste(c.resize((tw_, th_)), ((i % 3) * tw_, (i // 3) * th_))
        dd.text(((i % 3) * tw_ + 6, (i // 3) * th_ + 4), f"{L.id} {L.name} [{L.weather}]", fill=(255, 255, 0))
    sheet.save(os.path.join(outdir, "_sheet.png"))
    print("rendered", len(thumbs), "levels to", outdir)


if __name__ == "__main__":
    bad = 0
    for L in LEVELS:
        for msg in d.check(L):
            print(f"{L.id}: {msg}"); bad += 1
    print(f"{len(LEVELS)} levels, {sum(len(L.blocks) for L in LEVELS)} blocks, {sum(len(L.robots) for L in LEVELS)} robots, {bad} issues")
    write_cs()
    if "--render" in sys.argv:
        render(sys.argv[sys.argv.index("--render") + 1])
