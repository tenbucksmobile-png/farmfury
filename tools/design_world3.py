"""
Farm Fury - World 3 (Watermill Village) level designer (2026-11-10).

World 3 built on the World 1/2 template (user: "utilise the same framework you have for the first two worlds, be
creative and build the third world"): framed levels with live physics in a river valley — the robots have taken over
the mill village. Iron-banded mill timber and river-stone masonry, flour-sack stores, the river mounts (grassy islets,
stone bridge piers, fishing jetties, a rowing boat that slides down a slipway), the village contraptions (water pumps
that gush a jet of water, the mill's sack hoist dangling a millstone, millstones that roll), the river robots and the
Mill Stone Mauler boss, dropped off by the mothership. Ducky (World 3's animal) leads most lineups.
Uses the shared piece/mount/check code in design_world1.py (world=3).

Usage (repo root):  python tools/design_world3.py [--render OUTDIR]
Writes unity/Assets/Editor/LevelDataGenerator.World3.cs; then FarmFury -> Generate All Level Data.
"""
import math
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import design_world1 as d   # noqa: E402  (its World 1 levels build on import; harmless)

G = d.G
D, P, W, C, B = "Ducky", "Percy", "Woolly", "Cluck", "Bessie"
LEVELS = []
tower, seesaw, ramp_wagon, edge_wagon = d.tower, d.seesaw, d.ramp_wagon, d.edge_wagon


def level(k, name, par, birds, comment, weather="SummerMorning", boss=False):
    key = name.replace("'", "").replace(" ", "").replace("-", "")
    L = d.Level(f"W03_L{k:02d}_{key}", f"W3_L{k:02d}", name, par, birds, comment, boss=boss, world=3)
    L.weather = weather
    LEVELS.append(L)
    return L


ISLET = d.MOUNTS_W[3]["Islet"]


def islet(L, x, width):
    """A grassy river islet resting on the riverbed (its water band at ground level). Returns its grass surface."""
    return L.island(x, G + ISLET["uv"] * width / ISLET["aspect"], width, 0)


def cottage(L, xa, xb, b, h=1.2):
    """A river-stone cottage: cobble pillars at the ends and a stone lintel roof. Returns the roof top."""
    return L.hut(xa, xb, b, h, stone=True, roof_stone=True)


def sack_stilts(L, x0, n=3, gap=0.95):
    """Flour sacks standing side by side on the ground (they burst when hit); returns their top."""
    t = G
    for i in range(n):
        t = L.hay(x0 + (i - (n - 1) / 2) * gap, G)
    return t


# ═══════════════════════════════════════════ the levels ═══════════════════════════════════════════════════
L = level(1, "Millpond Mischief", 2, [D, D, C], """
Ducky's first level: robots have moved into the miller's two-storey timber shed by the millpond. A village water pump
stands by the door — knock it and it gushes a jet of water straight into the shed.""")
tower(L, 8.3, G, 2, width=1.9, robots=["pawn", "pawn"], top="pawn")
L.sunflower(6.3, G, 1.5)

L = level(2, "Stepping Stones", 2, [D, D, P], """
Three grassy islets across the shallows, each with a taller lookout than the last: one storey, two, then three.""",
          weather="RiverMist")
for x, w, floors in ((6.7, 2.0, 1), (9.4, 2.3, 2), (12.2, 2.3, 3)):
    s = islet(L, x, w)
    tower(L, x, s, floors, width=1.5, h=1.15, taper=0.08, robots=["pawn", None, "semi"][:floors], top="pawn")

L = level(3, "Miller's Barn", 2, [D, P, C], """
A four-storey barn on a river-stone ground floor, a see-saw balancing a robot against a crate, and a stack of flour
sacks out back.""")
tower(L, 9.9, G, 4, width=2.0, taper=0.12, stone_floors=1, robots=["semi", None, "pawn", None], top="pawn")
seesaw(L, 6.9, G, 1.0, 2.4, left="crate", right="pawn")
t = L.hay(12.6, G); L.hay(12.6, t)

L = level(4, "Down the Slipway", 2, [D, W, C], """
A rowing boat loaded with a crate and a robot sits at the top of the boat slipway. Knock it and it slides down the
planks into the five-storey boathouse.""", weather="SpringShower")
deck, dl, dr, end = ramp_wagon(L, 6.4, wagon_w=1.9)
L.box(dl + 0.3, deck, 0.45, stone=False); L.robot(dr - 0.35, deck, "pawn")
tower(L, end + 1.25, G, 5, width=2.0, taper=0.15, robots=["semi", None, "pawn", None, "semi"], top="pawn")

L = level(5, "Flour Store", 2, [D, P, B], """
The mill's flour store: a four-storey loft standing on stilts of flour sacks. Burst the sacks and the whole loft comes
down; a water pump guards the front.""")
x0 = 10.0
t = sack_stilts(L, x0)
f = L.plank(x0 - 1.5, x0 + 1.5, t)
tower(L, x0, f, 4, width=2.2, h=1.1, taper=0.2, robots=["semi", None, "pawn", None], top="pawn")
L.sunflower(7.6, G, 1.4)

L = level(6, "Bridge Toll", 3, [D, W, B], """
Two stone bridge piers carry little toll huts, joined by a plank bridge with a robot on it — and a big Harvester
lurks under the bridge like a troll.""")
tops = []
for xc in (7.6, 11.8):
    s_, l, r = L.mount("Mesa", xc, 1.8)
    c = (l + r) / 2
    tops.append((c, tower(L, c, s_, 2, width=1.0, h=1.15, taper=0.0, robots=["pawn", None])))
br = L.plank(tops[0][0] + 0.25, tops[1][0] - 0.25, max(tops[0][1], tops[1][1]))
L.robot((tops[0][0] + tops[1][0]) / 2, br, "semi")
L.robot(9.7, G, "harv")

L = level(7, "Fishing Jetty", 2, [W, D, C], """
In the morning mist: two fishing jetties on stilts, a two-storey net shed on one and a three-storey on the other, with
a water pump on the bank between them.""", weather="RiverMist")
for xc, w, floors in ((7.3, 2.2, 2), (11.4, 2.6, 3)):
    s_, l, r = L.mount("Scaffold", xc, w)
    c = (l + r) / 2
    tower(L, c, s_, floors, width=min(1.8, r - l - 0.1), h=1.15, taper=0.1, robots=["pawn", None, "semi"][:floors], top="pawn")
L.sunflower(9.35, G, 1.3)

L = level(8, "Runaway Millstone", 3, [D, P, W], """
A millstone stands on the edge of a tall bridge pier, right above a river-stone keep. Knock it off and it crashes
down on the keep's roof.""")
s_, l, r = L.mount("Mesa", 6.4, 1.6)
L.boulder(r - 0.5, s_, 0.95)
top = L.post(8.6, G, 1.6, stone=True); L.post(11.0, G, 1.6, stone=True)
rf = L.plank(8.4, 11.2, top, stone=True)
L.robot(9.8, G, "harv")
tower(L, 9.8, rf, 2, width=2.2, h=1.15, taper=0.3, robots=["pawn", None], top="pawn")
L.robot(12.5, G, "pawn")

L = level(9, "Cobble Cottages", 2, [D, W, P], """
A row of three river-stone cottages after a spring shower, a robot in each; the middle one has a timber loft on top.
A water pump stands at the end of the lane.""", weather="SpringShower")
for i, xc in enumerate((7.4, 9.8, 12.2)):
    rf = cottage(L, xc - 0.9, xc + 0.9, G)
    L.robot(xc, G, "pawn" if i != 1 else "semi")
    if i == 1:
        tower(L, xc, rf, 1, width=1.5, h=1.15, robots=["pawn"])
L.sunflower(5.9, G, 1.4)

L = level(10, "The Weir", 3, [D, P, B], """
The river weir: a staircase of river-stone steps falling away to the right with a robot on every step, and a
two-storey lookout on the top step.""", weather="RiverMist")
for row in range(4):
    for k in range(5 - row):
        L.box(7.4 + k * 0.8, G + row * 0.8, 0.8)
for k, kind in ((4, "pawn"), (3, "pawn"), (2, "semi")):
    L.robot(7.4 + k * 0.8, G + min(4, 5 - k) * 0.8, kind)
tower(L, 7.8, G + 3.2, 2, width=1.5, h=1.15, taper=0.1, robots=["pawn", None], top="pawn")

L = level(11, "Grain Seesaws", 2, [W, D, B], """
At lantern-lit dusk: a river-stone grain mound with a watch-house on top, powder kegs at its foot and see-saws
to either side.""", weather="LanternDusk")
for row, n in enumerate((3, 2)):
    for k in range(n):
        L.box(9.8 + (k - (n - 1) / 2) * 0.8, G + row * 0.8, 0.8)
L.barrel(8.2, G); L.barrel(11.4, G)
tower(L, 9.8, G + 1.6, 2, width=1.6, h=1.15, taper=0.1, robots=["semi", "pawn"], top="pawn")
seesaw(L, 6.4, G, 0.9, 2.2, left="crate", right="pawn")
seesaw(L, 13.1, G, 0.9, 2.2, left="pawn", right="hay")

L = level(12, "Mill Race", 3, [D, W, C], """
A rowing boat with a robot aboard waits on top of a tall bridge pier. Tip it over the edge into the five-storey mill
tower below; a three-storey tower stands on an islet behind.""")
s_, l, r = L.mount("Mesa", 6.9, 1.9)
deck, dl, dr = edge_wagon(L, s_, r, 1.7)
L.robot((dl + dr) / 2, deck, "pawn")
tower(L, 10.2, G, 5, width=2.0, taper=0.15, robots=["semi", None, "pawn", None, "semi"], top="pawn", room=(8.0, 11.9))
s2 = islet(L, 13.0, 2.2)
tower(L, 13.0, s2, 3, width=1.5, h=1.15, taper=0.1, robots=["pawn", None, "semi"], room=(12.0, 14.2))

L = level(13, "Twin Mills", 3, [D, P, W], """
Two four-storey mill towers on fishing jetties, joined at the top by a plank bridge with a robot on it — after the
shower a rainbow hangs over the valley.""", weather="SpringShower")
cs = []
for xc in (8.0, 11.9):
    s_, l, r = L.mount("Scaffold", xc, 2.4)
    c = (l + r) / 2
    cs.append(c)
    top = tower(L, c, s_, 4, width=1.8, h=1.15, taper=0.12, robots=["semi", None, "pawn", None])
br = L.plank(cs[0] + 0.3, cs[1] - 0.3, top)
L.robot((cs[0] + cs[1]) / 2, br, "semi")

L = level(14, "Lantern Row", 3, [W, D, B], """
At dusk: a long two-storey row of lodgings full of robots, with the mill's sack hoist dangling a millstone over the
roof. Shoot the rope.""", weather="LanternDusk")
for x in (7.4, 9.4, 11.4):
    top = L.post(x, G, 1.3)
L.plank(7.23, 9.4, top); f1 = L.plank(9.4, 11.57, top)
for x in (8.4, 10.4): L.robot(x, G, "semi")
r = L.hut(7.9, 10.9, f1, 1.15); L.robot(8.7, f1, "pawn"); L.robot(10.1, f1, "pawn")
L.hoist(13.9, G, 6.4, 0.3)

L = level(15, "Thunderhead", 3, [D, P, B], """
In a summer thunderstorm: a five-storey granary on stilts of flour sacks, and a millstone parked on the slipway behind
it — send it rolling.""", weather="Thunderstorm")
s, l, r = L.mount("Ramp", 6.0, 5.0)
L.boulder(l + 0.9, s, 1.0)
x0 = 6.0 + 2.5 + 1.8
t = sack_stilts(L, x0)
f = L.plank(x0 - 1.5, x0 + 1.5, t)
tower(L, x0, f, 5, width=2.2, h=1.1, taper=0.2, robots=["semi", "pawn", None, "semi", None], top="pawn", wonky=False)   # on bale stilts: wonky settled by itself (probe)

L = level(16, "Waterwheel Keep", 2, [W, D, B], """
Storm over the mill: a Harvester in a river-stone keep with a watch-house and a see-saw on its roof. On a bridge pier
to the left a rowing boat waits at the edge above the powder keg at the keep's door.""", weather="Thunderstorm")
top = L.post(8.8, G, 1.6, stone=True); L.post(11.2, G, 1.6, stone=True)
rf = L.plank(8.6, 11.4, top, stone=True)
L.robot(10.0, G, "harv"); L.barrel(8.0, G)
t = tower(L, 10.0, rf, 2, width=2.2, h=1.15, taper=0.3, robots=["pawn", None])
seesaw(L, 10.0, t, 0.7, 2.4, left="pawn", right="barrel")
s_, l, r = L.mount("Mesa", 6.0, 1.7)
deck, dl, dr = edge_wagon(L, s_, r, 1.6)
L.box((dl + dr) / 2, deck, 0.45, stone=False)

L = level(17, "Riverside Market", 3, [D, W, C], """
Market day on a long islet: three market stalls with robots, two of them two storeys high, and the sack hoist
dangling a millstone over the last one.""")
s = islet(L, 9.6, 6.6)
for x, two in ((7.3, True), (9.15, False), (10.9, True)):
    rr = L.hut(x - 0.65, x + 0.65, s, 1.15)
    L.robot(x, s, "pawn")
    if two:
        L.hut(x - 0.55, x + 0.55, rr, 1.15)
        L.robot(x, rr, "pawn")
L.hoist(13.4, G, 5.6, 0.45)

L = level(18, "Rainbow Green", 3, [D, P, W], """
On the village green after the rain: a six-storey mill tower with see-saws balancing robots on both sides.""",
          weather="SpringShower")
tower(L, 9.8, G, 6, width=2.1, h=1.15, taper=0.16, stone_floors=1, robots=["semi", None, "pawn", None, "pawn", None], top="semi")
seesaw(L, 6.9, G, 0.9, 2.2, left="crate", right="pawn")
seesaw(L, 12.8, G, 0.9, 2.2, left="pawn", right="barrel")

L = level(19, "Old Stone Arch", 3, [W, D, B], """
The old river-stone arch: two cobble columns spanned by a lintel, a Harvester under it, a gatehouse on top — and the
sack hoist dangling a millstone over the arch.""", weather="RiverMist")
for xc in (8.4, 11.2):
    t = L.box(xc, G, 0.8); t = L.box(xc, t, 0.8); t = L.box(xc, t, 0.8)
arch = L.plank(7.8, 11.8, t, stone=True)
L.robot(9.8, G, "harv")
rr = L.hut(8.6, 11.0, arch, 1.15); L.robot(9.8, arch, "pawn")
tower(L, 9.8, rr, 2, width=1.8, h=1.15, taper=0.2, robots=["semi", None], top="pawn")
L.hoist(16.2, G, 6.2, 0.3)

L = level(20, "Miller's Manor", 3, [D, W, B], """
The miller's manor on its own islet: a river-stone hall with a robot on the roof and a three-storey timber tower at
each end, lit by lanterns at dusk.""", weather="LanternDusk")
s = islet(L, 10.0, 7.0)
for row, n in enumerate((5, 4)):
    for k in range(n):
        top = L.box(10.0 + (k - (n - 1) / 2) * 0.8, s + row * 0.8, 0.8)
L.robot(10.0, top, "pawn")
for xc in (7.25, 12.75):
    tower(L, xc, s, 3, width=1.4, h=1.15, taper=0.05, robots=["pawn", None, "semi"], top="pawn")

L = level(21, "Sluice Gate Fortress", 3, [W, D, B], """
In the storm: the robots' sluice-gate fortress on an islet — a river-stone ground floor and three timber storeys —
with a millstone poised on the slipway in front.""", weather="Thunderstorm")
s0, l0, r0 = L.mount("Ramp", 5.6, 4.6)
L.boulder(l0 + 0.8, s0, 0.95)
s = islet(L, 11.2, 5.2)
for x in (9.6, 11.2, 12.8):
    top = L.post(x, s, 1.3, stone=True)
L.plank(9.4, 11.2, top, stone=True); f1 = L.plank(11.2, 13.0, top, stone=True)
L.robot(10.4, s, "semi"); L.robot(12.0, s, "semi")
tower(L, 11.2, f1, 3, width=2.6, h=1.15, taper=0.35, robots=["pawn", None, "semi"], top="pawn")

L = level(22, "The Mill Stone Mauler", 3, [D, W, B], """
Boss: the mothership drops the Mill Stone Mauler onto a three-storey keep on an islet; water pumps guard the keep's
door and a sentry watches from a river-stone tower.""", boss=True)
s = islet(L, 10.9, 5.4)
top = L.post(9.5, s, 1.5, stone=True); L.post(12.3, s, 1.5, stone=True)
rf = L.plank(9.2, 12.6, top, stone=True)
L.robot(10.9, s, "semi"); L.barrel(8.8, s); L.barrel(13.0, s)
t = tower(L, 10.9, rf, 3, width=2.4, h=1.15, taper=0.2, robots=["pawn", None, "semi"], wonky=False)   # the boss lands on it
L.robot(10.9, t, "boss")
t = L.box(6.4, G, 0.8)
for _ in range(3): t = L.box(6.4, t, 0.8)
L.robot(6.4, t, "pawn")
L.sunflower(7.6, G, 1.4)


# Second rooms (2026-11-12, user: "apply the same now to W2 ... then apply W3 ... do the panning to room 2 for both
# worlds"): every level gets a generated second room (design_world1.room2_for, this world's pieces and robots), the
# boss keep moves to room 2. Room 1 of every level stays exactly as designed above.
ROOM2_SKIP = {}   # level number -> valid rooms to pass over (one that fell by itself in Unity's PhysicsProbe)
d.add_second_rooms(LEVELS, 1, ROOM2_SKIP)


# ═══════════════════════════════════════════ output ═══════════════════════════════════════════════════════
def write_cs():
    d.prune_all(LEVELS)   # robots hidden behind a mound (2026-11-12)
    body = "\n\n".join(L.cs() for L in LEVELS)
    keep = ", ".join(f'"{L.file}"' for L in LEVELS)
    src = f"""// GENERATED by tools/design_world3.py — edit the designs there, not here.
// World 3 (Watermill Village) levels W3_L01-W3_L22 (2026-11-10): the World 1/2 template in the river valley — framed,
// live physics, river mounts and village contraptions, the Mill Stone Mauler boss.

using UnityEditor;

public static partial class LevelDataGenerator
{{
    static void MakeWorld3Framed(string folder)
    {{
        // Drop any older World 3 asset that isn't one of these (the generated W03_* set had different names).
        var keep = new System.Collections.Generic.HashSet<string> {{ {keep} }};
        foreach (var guid in AssetDatabase.FindAssets("t:LevelData", new[] {{ folder }}))
        {{
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string file = System.IO.Path.GetFileNameWithoutExtension(path);
            if (file.StartsWith("W03_") && !keep.Contains(file)) AssetDatabase.DeleteAsset(path);
        }}

{body}
    }}
}}
"""
    path = os.path.join(d.UNITY, "Editor", "LevelDataGenerator.World3.cs")
    open(path, "w", encoding="utf-8").write(src)
    print("wrote", os.path.normpath(path))


def render(outdir):
    """Offline mock-up of every level at the framed zoom (backdrop, mounts, contraptions, pieces, robots)."""
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

    WX = {  # mirrors CornfieldAmbience's watermill profiles
        "SummerMorning": dict(sky=(1, 1, 1), land=(1, 1, 1), haze=(.84, .93, .96), hazeA=.22, grade=((1, .95, .8), 0), rain=0),
        "RiverMist": dict(sky=(.92, .96, .98), land=(.88, .94, .93), haze=(.85, .92, .92), hazeA=.45, grade=((.78, .9, .92), .07), rain=0),
        "SpringShower": dict(sky=(.86, .9, .96), land=(.9, .95, .92), haze=(.8, .86, .92), hazeA=.3, grade=((.5, .6, .8), .06), rain=.5),
        "LanternDusk": dict(sky=(1, .76, .7), land=(.9, .76, .74), haze=(.95, .64, .58), hazeA=.3, grade=((.45, .2, .4), .14), rain=0),
        "Thunderstorm": dict(sky=(.5, .56, .64), land=(.62, .7, .7), haze=(.56, .63, .68), hazeA=.45, grade=((.1, .18, .28), .24), rain=1),
    }
    E = "Environment/Watermill/"
    bases = {}

    def backdrop(wx):
        w = WX[wx]
        base = Image.new("RGBA", (int(camW * PX), int(camH * PX)), (60, 90, 120, 255))
        far = tint(load(E + "MillFar.png"), w["sky"])
        anchor = G + 0.9 + 3.6 * 0.3
        fh = max(camW * far.size[1] / far.size[0], bottom + camH - anchor); fw = fh * far.size[0] / far.size[1]
        put(base, far, cx, anchor + fh / 2, fw, fh)
        strip(base, E + "MillRidges.png", 2.3, G + 3.5, w["sky"])
        overlay(base, w["haze"], w["hazeA"])
        strip(base, E + "MillMid.png", 3.6, G + 0.9, w["land"])
        strip(base, E + "MillWall.png", 2.4, G - 0.15, tuple(0.86 * v for v in w["land"]))
        soil = load(E + "MillSoil.png"); sh = 3.6; tw = sh * soil.size[0] / soil.size[1]
        for i in range(-2, 3): put(base, soil, cx + i * tw, G - sh * 0.697 + sh / 2, tw, sh)
        return base

    pieces = {k: v + ".png" for k, v in d.SPRITE_W3.items()}
    robots = {"pawn": "Enemies/Robot/W3/Basic_left.png", "semi": "Enemies/Robot/W3/SemiHarvester_left.png",
              "harv": "Enemies/Robot/W3/Harvester_left.png", "boss": "Enemies/Robot/MillMauler.png"}
    mounts = {0: "Mount_Islet", 1: "Mount_Islet", 2: "Mount_Islet", 3: "Mount_Pier", 4: "Mount_Jetty", 5: "Mount_Boat", 6: "Mount_Slipway"}
    os.makedirs(outdir, exist_ok=True)
    thumbs = []
    for L in LEVELS:
        if L.weather not in bases: bases[L.weather] = backdrop(L.weather)
        c = bases[L.weather].copy()
        hb = load(E + "FlourSacks.png")
        for row in range(2):
            for k in range(3 - row): put(c, hb, -3.87 + (k - (2 - row) * 0.5) * 0.95, G + 1.15 * (0.5 - 0.038) + row * 0.8, 1.15, 1.15)
        can = load("Environment/Launchers/MillCannon.png"); put(c, can, -3.72, G + 2.3, 2.1, 2.1 * can.size[1] / can.size[0])
        for isl in L.islands:
            im = load(E + mounts[isl["v"]] + ".png"); h = isl["h"]
            put(c, im, isl["x"], isl["s"] - isl["uv"] * h + h / 2, isl["w"], h)
        for g in L.gadgets:
            if g["type"] == "WaterPump":
                im = load(E + "WaterPump.png"); h = g["size"]; put(c, im, g["x"], g["y"] + h / 2, h * im.size[0] / im.size[1], h)
            elif g["type"] == "Millstone":
                im = load(E + "Millstone.png"); put(c, im, g["x"], g["y"] + g["size"] / 2, g["size"], g["size"])
            else:
                put(c, load(E + "MillCrane.png"), g["cl"] + g["cw"] / 2, g["y"] + g["size"] / 2, g["cw"], g["size"], flip=g["left"])
                bl, br, bb, bt = g["bucket"]; ax, ay = g["anchor"]
                ImageDraw.Draw(c).line([((ax - left) * PX, (bottom + camH - ay) * PX), ((ax - left) * PX, (bottom + camH - bt) * PX)], fill=(140, 107, 64, 255), width=2)
                im = load(E + "Millstone.png"); bh = (br - bl) * im.size[1] / im.size[0]
                put(c, im, (bl + br) / 2, bt - bh / 2, br - bl, bh)
        for b in L.blocks:
            put(c, load(pieces[b["kind"]]), b["x"], b["y"], b["w"], b["h"])
        for r in L.robots:
            im = load(robots[r["kind"]]); w = r["s"] * im.size[0] / 1746; h = r["s"] * im.size[1] / 1746
            put(c, im, r["x"], r["y"], w, h)
        overlay(c, *WX[L.weather]["grade"])
        import random as _r
        rnd = _r.Random(5); dr = ImageDraw.Draw(c)
        for _ in range(int(220 * WX[L.weather]["rain"])):
            x, y = rnd.uniform(0, c.size[0]), rnd.uniform(0, c.size[1])
            dr.line([x, y, x + 4, y + 22], fill=(210, 220, 240, 120), width=1)
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
