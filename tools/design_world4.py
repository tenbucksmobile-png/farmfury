"""
Farm Fury - World 4 (Sky Islands) level designer (2026-11-11).

World 4 built on the World 1-3 template (user: "build the next world utilising the exact process you have used for every
world build - applying the same frameworks"): framed levels with live physics, robot-built wonky structures (see
design_world1.tower and the set pieces), standing on floating isles and rocks at different heights above a sea of
clouds, the sky mounts (cloud-wrapped stone pillar, sky dock, slide ramp), the sky contraptions (thunderclouds that hurl
lightning, sky cranes dangling a thunder wheel, thunder wheels rolling down the slide), the sky robots and the Storm
Baron, dropped off by the mothership. Uses the shared piece/mount/check code in design_world1.py (world=4).

Usage (repo root):  python tools/design_world4.py [--render OUTDIR]
Writes unity/Assets/Editor/LevelDataGenerator.World4.cs; then FarmFury -> Generate All Level Data.
"""
import math
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import design_world1 as d   # noqa: E402  (its World 1 levels build on import; harmless)

G = d.G
H, D, P, W, C, B = "Horace", "Ducky", "Percy", "Woolly", "Cluck", "Bessie"
LEVELS = []
tower, seesaw, aframe, lean = d.tower, d.seesaw, d.aframe, d.lean
upside_pyramid, wobble_stack, stilt_house = d.upside_pyramid, d.wobble_stack, d.stilt_house
cantilever, balanced_rock, barrel_deck = d.cantilever, d.balanced_rock, d.barrel_deck


def level(k, name, par, birds, comment, weather="ClearSkies", boss=False):
    key = name.replace("'", "").replace(" ", "").replace("-", "")
    L = d.Level(f"W04_L{k:02d}_{key}", f"W4_L{k:02d}", name, par, birds, comment, boss=boss, world=4)
    L.weather = weather
    LEVELS.append(L)
    return L


ROCK = d.MOUNTS_W[4]["Rock"]


def isle(L, x, surface, width):
    """A wide floating isle hanging in the sky with its grass at `surface`. Returns the surface."""
    return L.island(x, surface, width, 0)


def rock(L, x, surface, width):
    """A small floating rock (grass mound on top) hanging in the sky. Returns its grass surface. Its underside hangs
    ~0.9 x width below the surface: keep surface > G + 0.9 * width so it clears the ground."""
    assert surface - ROCK["uv"] * width / ROCK["aspect"] >= G - 0.05, f"rock at x={x} dips into the ground"
    L.islands.append(dict(x=x, s=surface, w=width, v=2, solids=[(0.04, 0.96, 0.25, ROCK["uv"])],
                          h=width / ROCK["aspect"], uv=ROCK["uv"]))
    return surface


def runaway_wheel(L, ramp_x, ramp_w=5.4, wheel=1.0):
    """A slide ramp with a thunder wheel parked at the plateau's edge: knock it and it rolls down into whatever is
    below. Returns the slope's end x."""
    s, l, r = L.mount("Ramp", ramp_x, ramp_w)
    L.boulder(r - wheel * 0.55, s, wheel)
    return ramp_x - ramp_w / 2 + 0.98 * ramp_w


# ═══════════════════════════════════════════ the levels ═══════════════════════════════════════════════════
L = level(1, "Cloud Landing", 2, [H, H, C], """
Horace's first level: robots have set up camp on a floating isle — a crooked two-storey shed and a robot in a plank
tent. A thundercloud drifts in front: hit it and it hurls lightning into the shed.""")
s = isle(L, 9.6, G + 1.4, 5.6)
tower(L, 8.6, s, 2, width=1.8, robots=["pawn", None], top="pawn")
aframe(L, 10.9, s, h=1.2, half=0.8, robot="pawn")
L.sunflower(5.8, G, 1.8)

L = level(2, "Stepping Clouds", 2, [H, H, C], """
Three floating rocks at three heights, each with a little robot camp: a hut, a leaning crate stack, a single-post
lookout.""", weather="Windswept")
a = rock(L, 7.2, G + 2.0, 2.0)
r_ = L.hut(6.6, 7.8, a, 1.05); L.robot(7.2, a, "pawn"); L.robot(7.2, r_, "pawn")
b = rock(L, 9.7, G + 3.2, 2.0)
wobble_stack(L, 9.5, b, n=3, top="pawn")
c = rock(L, 12.2, G + 4.4, 2.0)
cantilever(L, 12.1, c, 1.0, 0.7, 0.7, tip="pawn")

L = level(3, "Marble Folly", 2, [H, P, C], """
A five-storey tower of airship timber on a marble ground floor, the robots' idea of a temple. A see-saw beam balances a
robot against a crate.""", weather="SunsetClouds")
tower(L, 9.8, G, 5, width=2.0, taper=0.12, stone_floors=1, robots=["semi", None, "pawn", None, "pawn"], top="pawn")
seesaw(L, 6.9, G, 1.0, 2.4, left="crate", right="pawn")

L = level(4, "Thunder Slide", 2, [H, P, C], """
A thunder wheel waits at the top of the slide. Knock it and it rolls down into the crooked five-storey tower at the
bottom.""")
end = runaway_wheel(L, 6.2)
tower(L, end + 1.25, G, 5, width=2.0, taper=0.15, robots=["semi", None, "pawn", None, "semi"], top="pawn")

L = level(5, "Upside Down Temple", 2, [H, W, C], """
On a high isle the robots built their marble temple upside down, a lookout on top and robots sheltering under the
overhangs.""", weather="ClearSkies")
s = isle(L, 9.9, G + 1.6, 6.4)
top = upside_pyramid(L, 9.9, s, rows=(1, 2, 3, 4), s=0.62, stone=True)
L.robot(8.4, s, "pawn"); L.robot(11.4, s, "pawn")
tower(L, 9.9, top, 2, width=1.9, h=1.05, taper=0.2, robots=["semi", "pawn"], top="pawn")

L = level(6, "Sky Crane", 2, [H, W, C], """
A sky crane on the right dangles a thunder wheel over a robot's tent; a three-storey tower stands on the dock behind a
thundercloud.""", weather="Windswept")
s_, l_, r_ = L.mount("Scaffold", 8.2, 2.6)
tower(L, (l_ + r_) / 2, s_, 3, width=1.7, h=1.05, taper=0.1, robots=["semi", None, "pawn"], top="pawn")
aframe(L, 11.4, G, h=1.2, half=0.8, robot="pawn")
L.hoist(14.2, G, 4.2, 0.4)
L.sunflower(5.9, G, 1.7)

L = level(7, "Pillar Perches", 2, [H, P, W], """
Two cloud-wrapped stone pillars, each crowned with a hut on stilts, and a stack of crates leaning between them.""",
              weather="SunsetClouds")
for x in (7.4, 11.8):
    s_, l_, r_ = L.mount("Mesa", x, 1.9)
    c_ = (l_ + r_) / 2
    L.hut(c_ - 0.55, c_ + 0.55, s_, 1.0); L.robot(c_, s_, "pawn")
wobble_stack(L, 9.6, G, n=4, top="semi")

L = level(8, "Storm Front", 3, [H, W, C], """
Rain and lightning: a stone ground floor with two rooms, a crooked four-storey tower above, and a thundercloud by the
gate.""", weather="StormFront")
for x in (8.2, 10.0, 11.8):
    top = L.post(x, G, 1.4, stone=True)
L.plank(8.025, 10.0, top, stone=True); f1 = L.plank(10.0, 11.975, top, stone=True)
L.robot(9.1, G, "semi"); L.robot(10.9, G, "semi")
tower(L, 10.0, f1, 4, width=2.6, taper=0.35, robots=["pawn", None, "semi", None], top="pawn")
L.sunflower(6.6, G, 1.8)

L = level(9, "Dynamite Deck", 2, [H, P, B], """
A floating isle carries a house built on a deck of four dynamite barrels: set one off and it all drops. A lean-to on
the far edge shelters another robot.""", weather="ClearSkies")
s = isle(L, 9.6, G + 1.2, 6.6)
dk = barrel_deck(L, 7.4, 10.8, s, n=4)
r_ = L.hut(7.7, 10.5, dk, 1.1); L.robot(8.5, dk, "semi"); L.robot(9.7, dk, "pawn")
L.robot(9.1, r_, "pawn")
aframe(L, 11.7, s, h=1.15, half=0.75, robot="pawn")

L = level(10, "Balanced Rocks", 2, [H, B, C], """
Bessie's sky debut: big marble blocks balanced on single posts across three floating rocks, a robot on each, and a
lookout tower in the middle.""", weather="Windswept")
a = rock(L, 6.9, G + 1.8, 1.8)
balanced_rock(L, 6.9, a, h=1.2, s=0.8, top="pawn")
s = isle(L, 9.7, G + 1.0, 3.4)
tower(L, 9.7, s, 3, width=1.8, h=1.05, taper=0.15, robots=["semi", None, "pawn"], top="pawn")
b = rock(L, 12.6, G + 2.6, 1.8)
balanced_rock(L, 12.6, b, h=1.0, s=0.75, top="pawn")

L = level(11, "Lookout Stilts", 3, [H, P, B], """
Huts perched on skinny stilts on two floating isles, joined by nothing but hope; a thunderclouds sits beneath the
first.""", weather="SunsetClouds")
s1 = isle(L, 7.8, G + 1.6, 3.2)
stilt_house(L, 7.8, s1, legs_h=1.4, w=1.3, robot="pawn", top="pawn")
s2 = isle(L, 11.6, G + 2.4, 3.2)
stilt_house(L, 11.6, s2, legs_h=1.2, w=1.3, robot="semi", top="pawn")
L.sunflower(6.0, G, 1.8)

L = level(12, "Harvest Moon", 3, [H, W, B], """
Under the stars a Harvester guards a marble keep with a crooked watch-house and a see-saw on its roof; a thunder
wheel sits at the top of the slide behind.""", weather="StarlitSky")
end = runaway_wheel(L, 5.8, ramp_w=4.8)
x0 = end + 0.6
top = L.post(x0, G, 1.6, stone=True); L.post(x0 + 2.4, G, 1.6, stone=True)
r_ = L.plank(x0 - 0.2, x0 + 2.6, top, stone=True)
L.robot(x0 + 1.2, G, "harv")
t = tower(L, x0 + 1.2, r_, 2, width=2.2, h=1.05, taper=0.3, robots=["pawn", None])
seesaw(L, L.last_top_x, t, 0.7, 2.4, left="pawn", right="barrel")

L = level(13, "Windmill Isle", 3, [H, P, W], """
Gusts over a big floating isle: a crooked four-storey mill tower, a plank tent and a leaning crate stack; a sky crane
hangs a thunder wheel over the tent.""", weather="Windswept")
s = isle(L, 9.6, G + 1.0, 7.4)
tower(L, 8.0, s, 4, width=1.8, h=1.05, taper=0.12, robots=["semi", None, "pawn", None], top="pawn")
aframe(L, 10.6, s, h=1.15, half=0.75, robot="pawn")
wobble_stack(L, 12.3, s, n=3, top="pawn")

L = level(14, "Cloud Bale Barn", 2, [H, B, C], """
A five-storey barn on stilts of cloud bales — they're soft, it wobbles — with robots on every other floor.""",
              weather="ClearSkies")
x0 = 10.2
for x in (x0 - 0.95, x0, x0 + 0.95):
    t = L.hay(x, G)
f = L.plank(x0 - 1.5, x0 + 1.5, t)
tower(L, x0, f, 5, width=2.2, h=1.1, taper=0.2, robots=["semi", "pawn", None, "semi", None], top="pawn")
L.sunflower(7.2, G, 1.7)

L = level(15, "Twin Docks", 3, [H, W, B], """
Two crooked four-storey towers on sky docks, joined at the top by a plank bridge with a robot on it, a robot in a tent
beneath.""", weather="SunsetClouds")
cs = []
for xc in (8.0, 11.9):
    s_, l_, r_ = L.mount("Scaffold", xc, 2.4)
    c_ = (l_ + r_) / 2
    cs.append(c_)
    top = tower(L, c_, s_, 4, width=1.8, h=1.05, taper=0.12, robots=["semi", None, "pawn", None], stilts=False)
br = L.plank(cs[0] + 0.3, cs[1] - 0.3, top)
L.robot((cs[0] + cs[1]) / 2, br, "semi")
aframe(L, (cs[0] + cs[1]) / 2, G, h=1.2, half=0.8, robot="pawn")

L = level(16, "Lightning Rods", 2, [H, P, B], """
In the storm, robots perch on marble columns of different heights topped with rocks; two thunderclouds wait to be set
off into them.""", weather="StormFront")
for x, h_ in ((8.0, 1.0), (9.2, 1.6), (10.4, 2.2), (11.6, 1.4)):
    balanced_rock(L, x, G, h=h_, s=0.7, top="pawn")
L.sunflower(6.2, G, 1.8)
L.sunflower(13.2, G, 1.6)

L = level(17, "Floating Market", 3, [H, W, C], """
Three floating rocks with robot stalls: a stall on dynamite legs, a lopsided cantilever with a barrel on the end, and
an upside-down pyramid of crates.""", weather="ClearSkies")
a = rock(L, 7.0, G + 2.2, 2.2)
dk = barrel_deck(L, 6.3, 7.7, a, n=2)
L.robot(7.0, dk, "pawn")
b = rock(L, 9.6, G + 2.3, 2.4)
cantilever(L, 9.2, b, 1.2, 0.4, 1.2, tip="barrel")
L.robot(9.9, b, "pawn")
c = rock(L, 12.3, G + 3.0, 2.4)
upside_pyramid(L, 12.3, c, rows=(1, 2, 3), s=0.55, robots=["pawn", None, "pawn"])

L = level(18, "Sky Temple", 3, [H, B, B], """
A marble temple of columns and slabs on a high isle, a crooked timber tower rising from its roof, robots in every
room.""", weather="SunsetClouds")
s = isle(L, 10.2, G + 1.8, 6.4)
for x in (8.4, 9.6, 10.8, 12.0):
    top = L.post(x, s, 1.1, stone=True)
L.plank(8.2, 10.2, top, stone=True); r_ = L.plank(10.2, 12.2, top, stone=True)
L.robot(9.0, s, "semi"); L.robot(11.4, s, "semi")
tower(L, 10.2, r_, 3, width=2.4, h=1.05, taper=0.3, robots=["pawn", None, "pawn"], top="pawn")

L = level(19, "Starlit Spires", 3, [H, P, W], """
Under the stars: three crooked spires on three pillars, each a little taller than the last.""", weather="StarlitSky")
for x, f_ in ((7.2, 2), (9.8, 3), (12.4, 2)):
    s_, l_, r_ = L.mount("Mesa", x, 2.0)
    c_ = (l_ + r_) / 2
    tower(L, c_, s_, f_, width=1.1, h=0.95, taper=0.0, robots=["pawn", None, "pawn"][:f_], top="pawn",
          room=(c_ - 1.25, c_ + 1.25))

L = level(20, "Wheel and Crane", 3, [H, W, B], """
A thunder wheel at the top of the slide and a sky crane over a stone hall with a crooked tower above it — two ways in.""",
              weather="ClearSkies")
end = runaway_wheel(L, 5.8, ramp_w=4.8)
x0 = end + 0.4
f = L.hut(x0, x0 + 2.4, G, 1.3, stone=True, roof_stone=True); L.robot(x0 + 1.2, G, "semi")
tower(L, x0 + 1.2, f, 3, width=2.0, h=1.05, taper=0.15, robots=["pawn", None, "semi"], top="pawn")
L.robot(x0 + 3.6, G, "pawn")
L.hoist(15.0, G, 4.0, 0.3)

L = level(21, "Stormbreak Citadel", 3, [H, B, B], """
The Storm Baron's outer citadel in a storm: a marble hall, two crooked towers, dynamite by the walls and a
thundercloud at the gate.""", weather="StormFront")
L.barrel(7.6, G)
f = L.hut(8.2, 10.4, G, 1.3, stone=True, roof_stone=True); L.robot(9.3, G, "semi")
tower(L, 9.3, f, 3, width=1.9, h=1.05, taper=0.12, robots=["pawn", None, "semi"], top="pawn", room=(7.8, 10.9))
s_, l_, r_ = L.mount("Scaffold", 12.2, 2.2)
tower(L, (l_ + r_) / 2, s_, 2, width=1.5, h=1.05, robots=["pawn", None], top="pawn", room=(11.0, 13.6))
L.sunflower(6.2, G, 1.8)

L = level(22, "Highwind Fortress", 3, [H, W, B], """
High winds: a fortress on a floating isle — a marble keep with a crooked tower, balanced rocks on either side.""",
              weather="Windswept")
s = isle(L, 10.0, G + 1.4, 7.0)
balanced_rock(L, 7.4, s, h=1.1, s=0.75, top="pawn")
top = L.post(8.9, s, 1.4, stone=True); L.post(11.1, s, 1.4, stone=True)
r_ = L.plank(8.7, 11.3, top, stone=True)
L.robot(10.0, s, "harv")
tower(L, 10.0, r_, 3, width=2.2, h=1.05, taper=0.25, robots=["pawn", None, "semi"], top="pawn")
balanced_rock(L, 12.6, s, h=0.9, s=0.7, top="pawn")

L = level(23, "Gathering Storm", 3, [H, B, B], """
The storm gathers over the last approach: upside-down pyramids on two floating rocks and a stilt house between them,
thunderclouds below.""", weather="StormFront")
a = rock(L, 7.4, G + 2.4, 2.4)
upside_pyramid(L, 7.4, a, rows=(1, 2, 3), s=0.55, robots=["pawn", None, "pawn"])
stilt_house(L, 9.9, G, legs_h=2.0, w=1.4, robot="semi", top="pawn")
b = rock(L, 12.4, G + 2.6, 2.4)
upside_pyramid(L, 12.4, b, rows=(1, 2, 3), s=0.55, stone=True, robots=["pawn", None, "pawn"])
L.sunflower(5.8, G, 1.8)

L = level(24, "The Storm Baron", 3, [H, B, B], """
Boss: the Storm Baron's airship is dropped on top of a marble-and-timber keep on a high isle (the mothership brings
it in); guards in the keep and on a leaning stone stack below.""", weather="StormFront", boss=True)
s = isle(L, 10.8, G + 2.0, 5.6)
top = L.post(9.4, s, 1.5, stone=True); L.post(12.2, s, 1.5, stone=True)
r_ = L.plank(9.1, 12.5, top, stone=True)
L.robot(10.8, s, "semi"); L.barrel(8.6, s); L.barrel(13.0, s)
t = tower(L, 10.8, r_, 3, width=2.4, h=1.1, taper=0.2, robots=["pawn", None, "semi"], wonky=False)   # the boss lands on it
L.robot(10.8, t, "boss")
wobble_stack(L, 6.4, G, n=4, s=0.8, lean_by=0.15, stone=True, top="pawn")


# ═══════════════════════════════════════════ output ═══════════════════════════════════════════════════════
def write_cs():
    body = "\n\n".join(L.cs() for L in LEVELS)
    keep = ", ".join(f'"{L.file}"' for L in LEVELS)
    src = f"""// GENERATED by tools/design_world4.py — edit the designs there, not here.
// World 4 (Sky Islands) levels W4_L01-W4_L24 (2026-11-11): the World 1-3 template above the clouds — framed, live
// physics, robot-built wonky structures on floating isles, sky mounts and contraptions, the Storm Baron boss.

using UnityEditor;

public static partial class LevelDataGenerator
{{
    static void MakeWorld4Framed(string folder)
    {{
        // Drop any older World 4 asset that isn't one of these (the generated W04_* set had different names).
        var keep = new System.Collections.Generic.HashSet<string> {{ {keep} }};
        foreach (var guid in AssetDatabase.FindAssets("t:LevelData", new[] {{ folder }}))
        {{
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string file = System.IO.Path.GetFileNameWithoutExtension(path);
            if (file.StartsWith("W04_") && !keep.Contains(file)) AssetDatabase.DeleteAsset(path);
        }}

{body}
    }}
}}
"""
    path = os.path.join(d.UNITY, "Editor", "LevelDataGenerator.World4.cs")
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

    def put(canvas, img, x, y, w, h, flip=False, rot=0.0):
        im = img.resize((max(1, int(w * PX)), max(1, int(h * PX))))
        if flip: im = ImageOps.mirror(im)
        if abs(rot) > 0.01: im = im.rotate(rot, expand=True, resample=Image.BICUBIC)
        canvas.paste(im, (int(x * PX - im.size[0] / 2 - left * PX), int((bottom + camH - y) * PX - im.size[1] / 2)), im)

    def tint(img, c):
        a = np.asarray(img).astype(np.float32).copy(); a[..., :3] *= np.array(c[:3], np.float32); return Image.fromarray(a.clip(0, 255).astype(np.uint8))

    def strip(canvas, p, h, b, col=(1, 1, 1)):
        im = tint(load(p), col); tw = h * im.size[0] / im.size[1]; k = math.floor((left - cx) / tw)
        for i in range(int(camW / tw) + 3):
            put(canvas, ImageOps.mirror(im) if (k + i) & 1 else im, cx + (k + i + 0.5) * tw, b + h / 2, tw, h)

    def overlay(canvas, col, a):
        if a <= 0: return
        canvas.alpha_composite(Image.new("RGBA", canvas.size, tuple(int(255 * v) for v in col) + (int(255 * a),)))

    WX = {  # mirrors CornfieldAmbience's sky profiles
        "ClearSkies": dict(sky=(1, 1, 1), land=(1, 1, 1), haze=(.86, .93, 1), hazeA=.2, grade=((.9, .96, 1), 0), rain=0),
        "Windswept": dict(sky=(.92, .96, 1), land=(.95, .98, 1), haze=(.84, .9, .98), hazeA=.28, grade=((.7, .82, 1), .05), rain=0),
        "SunsetClouds": dict(sky=(1, .82, .78), land=(1, .88, .86), haze=(1, .74, .66), hazeA=.3, grade=((.95, .5, .45), .1), rain=0),
        "StormFront": dict(sky=(.52, .58, .7), land=(.66, .72, .8), haze=(.58, .64, .74), hazeA=.42, grade=((.12, .18, .32), .22), rain=.8),
        "StarlitSky": dict(sky=(.42, .48, .72), land=(.6, .66, .86), haze=(.4, .46, .7), hazeA=.32, grade=((.12, .12, .4), .22), rain=0),
    }
    E = "Environment/Sky/"
    bases = {}

    def backdrop(wx):
        w = WX[wx]
        base = Image.new("RGBA", (int(camW * PX), int(camH * PX)), (120, 170, 230, 255))
        far = tint(load(E + "SkyFar.png"), w["sky"])
        anchor = G + 0.6
        fh = max(camW * far.size[1] / far.size[0], bottom + camH - anchor); fw = fh * far.size[0] / far.size[1]
        put(base, far, cx, anchor + fh / 2, fw, fh)
        overlay(base, w["haze"], w["hazeA"])
        strip(base, E + "SkyMid.png", 1.9, G + 3.4, w["land"])
        strip(base, E + "SkyWall.png", 1.5, G - 0.15, w["land"])
        soil = load(E + "SkySoil.png"); sh = 3.2; tw = sh * soil.size[0] / soil.size[1]
        for i in range(-3, 4): put(base, soil, cx + i * tw, G - sh * 0.749 + sh / 2, tw, sh, flip=bool(i & 1))
        return base

    pieces = {k: v + ".png" for k, v in d.SPRITE_W4.items()}
    robots = {"pawn": "Enemies/Robot/W4/Basic_left.png", "semi": "Enemies/Robot/W4/SemiHarvester_left.png",
              "harv": "Enemies/Robot/W4/Harvester_left.png", "boss": "Enemies/Robot/StormBaron.png"}
    mounts = {0: "Mount_Isle", 1: "Mount_Isle", 2: "Mount_Rock", 3: "Mount_Pillar", 4: "Mount_Dock", 6: "Mount_Ramp"}
    os.makedirs(outdir, exist_ok=True)
    thumbs = []
    for L in LEVELS:
        if L.weather not in bases: bases[L.weather] = backdrop(L.weather)
        c = bases[L.weather].copy()
        hb = load(E + "CloudBale.png")
        for row in range(2):
            for k in range(3 - row): put(c, hb, -3.87 + (k - (2 - row) * 0.5) * 0.95, G + 1.15 * (0.5 - 0.038) + row * 0.8, 1.15, 1.15)
        can = load("Environment/Launchers/SkyCannon.png"); put(c, can, -3.72, G + 2.3, 2.1, 2.1 * can.size[1] / can.size[0])
        for isl in L.islands:
            im = load(E + mounts[isl["v"]] + ".png"); h = isl["h"]
            put(c, im, isl["x"], isl["s"] - isl["uv"] * h + h / 2, isl["w"], h)
        for g in L.gadgets:
            if g["type"] == "StormCloud":
                im = load(E + "StormCloud.png"); h = g["size"]; put(c, im, g["x"], g["y"] + h / 2, h * im.size[0] / im.size[1], h)
            elif g["type"] == "ThunderWheel":
                im = load(E + "ThunderWheel.png"); put(c, im, g["x"], g["y"] + g["size"] / 2, g["size"], g["size"])
            else:
                put(c, load(E + "SkyCrane.png"), g["cl"] + g["cw"] / 2, g["y"] + g["size"] / 2, g["cw"], g["size"], flip=g["left"])
                bl, br, bb, bt = g["bucket"]; ax, ay = g["anchor"]
                ImageDraw.Draw(c).line([((ax - left) * PX, (bottom + camH - ay) * PX), ((ax - left) * PX, (bottom + camH - bt) * PX)], fill=(140, 107, 64, 255), width=2)
                im = load(E + "ThunderWheel.png"); bh = br - bl
                put(c, im, (bl + br) / 2, bt - bh / 2, br - bl, bh)
        for b in L.blocks:
            put(c, load(pieces[b["kind"]]), b["x"], b["y"], b["w"], b["h"], rot=b.get("rot", 0.0))
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
