"""
Farm Fury - World 1 level designer (2026-11-08).

World 1 levels L02-L18 are designed here in VISIBLE terms (posts, planks, stone boxes, hay, barrels, raised
islands, robots standing on surfaces) and converted to LevelData footprints using each sprite's measured art
bounds (the block colliders are trimmed to the art on framed levels, see BlockBase.TrimColliderToArt). It writes
unity/Assets/Editor/LevelDataGenerator.World1.cs, checks every block is supported and every robot reachable, and
renders a preview of each level.

Usage (repo root):  python tools/design_world1.py [--render OUTDIR]
Then in Unity: FarmFury -> Generate All Level Data (or .\\Run-Unity.ps1 levels).
"""
import math
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
UNITY = os.path.join(HERE, "..", "unity", "Assets")
G = -5.6                                         # World 1 ground line (ReferenceFraming.World1GroundY)

# Art bounds of each block sprite in its own rect: (x0, x1, y0, y1), y measured from the BOTTOM (alpha > 0).
ART = {
    "post":    (0.316, 0.592, 0.071, 0.955),     # 2D_Block_Wood_Vertical   Wood/Vertical
    "plank":   (0.080, 0.976, 0.179, 0.683),     # Plank_Horizontal         Wood/Horizontal
    "crate":   (0.176, 0.810, 0.082, 0.835),     # Plank_Short              Wood/Short
    "sbox":    (0.186, 0.859, 0.191, 0.787),     # Stone_Square             Stone/Square
    "pillar":  (0.000, 0.756, 0.182, 0.910),     # Stone_Vertical           Stone/Vertical
    "slab":    (0.158, 0.893, 0.299, 0.818),     # Stone_Block              Stone/Block
    "hay":     (0.000, 1.000, 0.036, 0.874),     # Haybail                  Haybale
    "barrel":  (0.190, 0.804, 0.068, 0.908),     # Barrel_Dynamite          Barrel
}
SPRITE = {"post": "2D_Block_Wood_Vertical", "plank": "Plank_Horizontal", "crate": "Plank_Short",
          "sbox": "Stone_Square", "pillar": "Stone_Vertical", "slab": "Stone_Block", "hay": "Haybail",
          "barrel": "Barrel_Dynamite"}
CS = {  # piece -> (BlockType, WoodArtVariant or None, extra C# args)
    "post": ("Wood", "Vertical", ""), "plank": ("Wood", "Horizontal", ""), "crate": ("Wood", "Short", ""),
    "sbox": ("Stone", "Square", ""), "pillar": ("Stone", "Vertical", ""), "slab": ("Stone", "Block", ""),
    "hay": ("Haybale", None, ", passThrough: true, hp: 10f, mass: 3f"), "barrel": ("Barrel", None, ""),
}
# Robots: (RobotType, scale, visual size in world units, visual bottom padding fraction)
ROBOT = {"pawn": ("Basic", 5.5, 0.806, 0.0), "semi": ("SemiHarvester", 5.5, 0.806, 0.0),
         "harv": ("Harvester", 7.5, 1.099, 0.0), "boss": ("Commander", 7.0, 2.004, 0.06)}
ISLAND = [(0.754, 2874 / 863), (0.699, 3003 / 1182)]   # (surface UV from bottom, width/height) per variant
import json as _json
MOUNTS = _json.load(open(os.path.join(HERE, "mounts.json")))   # written by tools/import_cornfield_art.py
MOUNT_VARIANT = {"Hill": 2, "Mesa": 3, "Scaffold": 4, "Wagon": 5}
WOOD_T, STONE_T = 0.25, 0.30                            # visible plank / slab thickness


class Level:
    def __init__(self, file, lid, name, par, birds, comment, boss=False):
        self.file, self.id, self.name, self.par, self.birds, self.comment, self.boss = file, lid, name, par, birds, comment, boss
        self.blocks, self.robots, self.islands = [], [], []

    # ── pieces (all take the VISIBLE bottom; return the visible top) ─────────────────────────────────────
    def _piece(self, kind, cx, bottom, vis_w, vis_h):
        x0, x1, y0, y1 = ART[kind]
        w, h = vis_w / (x1 - x0), vis_h / (y1 - y0)
        fx = cx - ((x0 + x1) / 2 - 0.5) * w
        fy = bottom - y0 * h + h / 2
        self.blocks.append(dict(kind=kind, x=fx, y=fy, w=w, h=h, vis=(cx - vis_w / 2, cx + vis_w / 2, bottom, bottom + vis_h)))
        return bottom + vis_h

    def post(self, x, b, h, stone=False):
        return self._piece("pillar", x, b, 0.35, h) if stone else self._piece("post", x, b, 0.276, h)

    def plank(self, xa, xb, b, stone=False):
        return self._piece("slab" if stone else "plank", (xa + xb) / 2, b, xb - xa, STONE_T if stone else WOOD_T)

    def box(self, x, b, s=0.8, stone=True):
        return self._piece("sbox" if stone else "crate", x, b, s, s)

    def hay(self, x, b):
        return self._piece("hay", x, b, 0.977, 0.977 * (ART["hay"][3] - ART["hay"][2]))

    def barrel(self, x, b):
        x0, x1, y0, y1 = ART["barrel"]
        return self._piece("barrel", x, b, 0.977 * (x1 - x0), 0.977 * (y1 - y0))

    def hut(self, xa, xb, b, h, stone=False, roof_stone=None):
        """Two posts at the ends and a roof across them; returns the roof's top."""
        inset = 0.175 if stone else 0.14
        top = self.post(xa + inset, b, h, stone)
        self.post(xb - inset, b, h, stone)
        return self.plank(xa, xb, top, stone if roof_stone is None else roof_stone)

    def island(self, x, surface, width, variant=0):
        self.islands.append(dict(x=x, s=surface, w=width, v=variant, solids=[(0.04, 0.96, 0.17, ISLAND[variant][0])],
                                 h=width / ISLAND[variant][1], uv=ISLAND[variant][0]))
        return surface

    def mount(self, kind, x, width):
        """A ground-standing mount (Hill/Mesa/Scaffold/Wagon). Returns (surface y, usable left x, usable right x)."""
        m = MOUNTS[kind]
        h = width / m["aspect"]
        surface = G + m["uv"] * h
        self.islands.append(dict(x=x, s=surface, w=width, v=MOUNT_VARIANT[kind], solids=m["boxes"], h=h, uv=m["uv"], kind=kind))
        l = x - width / 2 + m["flat"][0] * width
        r = x - width / 2 + m["flat"][1] * width
        return surface, l, r

    def robot(self, x, surface, kind="semi"):
        t, sc, vis, pad = ROBOT[kind]
        cy = surface + max(0.45, vis / 2) - pad * vis
        self.robots.append(dict(kind=kind, type=t, x=x, y=cy, s=sc, vis=vis, surface=surface))

    # ── output ───────────────────────────────────────────────────────────────────────────────────────────
    def cs(self):
        f = lambda v: f"{v:.3f}f"
        out = [f"        // ── {self.id}  {self.name} " + "─" * max(4, 60 - len(self.name)),
               *[f"        // {line}" for line in self.comment.strip().splitlines()],
               f'        Make(folder, "{self.file}",',
               f'            id: "{self.id}", name: "{self.name}", par: {self.par},',
               "            birds: new[] { " + ", ".join(f"AnimalType.{b}" for b in self.birds) + " },",
               "            blocks: new[]", "            {"]
        for b in self.blocks:
            t, v, extra = CS[b["kind"]]
            art = f", artVariant: WoodArtVariant.{v}" if v else ""
            out.append(f"                B(BlockType.{t}, {f(b['x'])}, {f(b['y'])}, {f(b['w'])}, {f(b['h'])}{art}{extra}),")
        out += ["            },", "            robots: new[]", "            {"]
        for r in self.robots:
            out.append(f"                R({f(r['x'])}, {f(r['y'])}, {f(r['s'])}, {f(r['s'])}, RobotType.{r['type']}),")
        out.append("            },")
        if self.islands:
            out += ["            platforms: new[]", "            {"]
            out += [f"                P({f(i['x'])}, {f(i['s'])}, {f(i['w'])}, variant: {i['v']})," for i in self.islands]
            out.append("            },")
        if self.boss:
            out.append("            isBoss: true,")
        out.append("            referenceFraming: true);")
        return "\n".join(out)


# ═══════════════════════════════════════════ the levels ═══════════════════════════════════════════════════
C, B = "Cluck", "Bessie"
LEVELS = []


def level(*a, **k):
    L = Level(*a, **k)
    LEVELS.append(L)
    return L


L = level("L02_StoneWall", "W1_L02", "Harvest Yard", 2, [C, C, C], """
Easy: a wooden lean-to with a robot sheltering inside, and a robot hiding between crates on a hay wagon.""")
L.hut(5.9, 7.7, G, 1.3); L.robot(6.8, G, "semi")
s, l, r = L.mount("Wagon", 11.4, 4.4)
L.box(l + 0.5, s, 0.6, stone=False); L.robot(l + 1.3, s, "pawn"); L.box(l + 2.1, s, 0.6, stone=False)

L = level("L03_TheTower", "W1_L03", "The Tower", 2, [C, C, C], """
A three-storey wooden tower with a robot on every floor and one on the roof. Knock the bottom posts out and
the whole thing comes down on them.""")
r1 = L.hut(8.0, 9.8, G, 1.3); L.robot(8.9, G, "semi")
r2 = L.hut(8.2, 9.6, r1, 1.2); L.robot(8.9, r1, "pawn")
r3 = L.hut(8.35, 9.45, r2, 1.1); L.robot(8.9, r2, "semi")
L.robot(8.9, r3, "pawn")
L.hay(7.2, G)

L = level("L04_EggPractice", "W1_L04", "Egg Practice", 2, [C, C, C], """
Three exposed robots along a raised island, each ducking behind a small crate: spread Cluck's eggs across them.""")
s = L.island(10.0, G + 1.8, 6.6, 0)
for x in (8.0, 10.0, 12.0):
    L.box(x - 0.75, s, 0.6, stone=False)
    L.robot(x, s, "pawn")

L = level("L05_TheGauntlet", "W1_L05", "The Gauntlet", 2, [C, C, C], """
Three targets at rising distance and height: a ground hut, a hut up on a timber scaffold, a robot on a rock mesa.""")
L.robot(6.2, G, "semi"); L.hut(5.3, 7.1, G, 1.25)
s, l, r = L.mount("Scaffold", 10.2, 2.6)
L.robot(10.15, s, "pawn"); L.hut(9.35, 10.95, s, 1.15)
s, l, r = L.mount("Mesa", 14.2, 2.6)
L.robot((l + r) / 2, s, "semi")

L = level("L06_DoubleBarrel", "W1_L06", "Double Barrel", 2, [C, C, C], """
A two-room farmhouse with a dynamite barrel beside each robot, and a third robot on the roof.""")
h = 1.4
for x in (7.0, 8.9, 10.8):
    top = L.post(x, G, h)
L.plank(6.86, 8.9, top); r = L.plank(8.9, 10.94, top)
L.robot(7.6, G, "semi"); L.barrel(8.45, G)
L.robot(10.25, G, "semi"); L.barrel(9.4, G)
L.robot(8.9, r, "pawn")
s, l, r = L.mount("Scaffold", 14.0, 2.4)
L.box(l + 0.4, s, 0.55, stone=False); L.robot(l + 1.2, s, "pawn")

L = level("L07_BarrelRow", "W1_L07", "Barrel Row", 2, [C, C, C], """
A stone-pillar bridge over a row of dynamite on an island: two robots on the bridge, one guarding the far end.""")
s = L.island(10.3, G + 1.2, 7.4, 0)
for x in (8.9, 9.8, 10.7):
    L.barrel(x, s)
top = L.post(8.3, s, 1.0, stone=True); L.post(11.3, s, 1.0, stone=True)
r = L.plank(8.0, 11.6, top)
L.robot(9.3, r, "pawn"); L.robot(10.6, r, "semi")
L.hay(12.6, s); L.robot(13.4, s, "semi")

L = level("L08_FortressAssault", "W1_L08", "Fortress Assault", 3, [C, C, C, C], """
A two-storey fort: stone ground floor with two rooms, timber upper floor, a robot on top.""")
for x in (8.2, 10.0, 11.8):
    top = L.post(x, G, 1.4, stone=True)
L.plank(8.025, 10.0, top, stone=True); f1 = L.plank(10.0, 11.975, top, stone=True)
L.robot(9.1, G, "semi"); L.robot(10.9, G, "semi")
r = L.hut(8.5, 11.5, f1, 1.2)
L.robot(10.0, f1, "pawn"); L.robot(10.0, r, "semi")
s, l, r = L.mount("Mesa", 14.4, 2.4)
L.robot((l + r) / 2, s, "pawn")

L = level("L09_StoneBastion", "W1_L09", "Stone Bastion", 3, [C, C, C, C], """
A stone bunker on an island hides a Harvester between two barrels; a lookout stands on the roof and a robot
sits on a hay mound out front.""")
s = L.island(10.6, G + 1.4, 5.6, 1)
t = L.box(8.75, s, 0.75); L.box(8.75, t, 0.75)
t = L.box(12.45, s, 0.75); top = L.box(12.45, t, 0.75)
r = L.plank(8.3, 12.9, top, stone=True)
L.robot(10.6, s, "harv"); L.barrel(9.55, s); L.barrel(11.65, s)
L.robot(10.6, r, "pawn")
t = L.hay(6.1, G); L.hay(7.05, G); t = L.hay(6.575, t - 0.04)
L.robot(6.575, t, "semi")

L = level("L10_BessiesDebut", "W1_L10", "Bessie's Debut", 2, [C, C, B], """
Bessie's first level: a stone pyramid with a robot on top — slam it — and two robots sheltering beside it.""")
for row, n in enumerate((3, 2, 1)):
    b = G + row * 0.8
    for k in range(n):
        top = L.box(9.4 + (k - (n - 1) / 2) * 0.8, b, 0.8)
L.robot(9.4, top, "semi")
L.robot(7.5, G, "pawn"); L.hay(6.7, G)
L.robot(11.4, G, "pawn")
s, l, r = L.mount("Wagon", 14.4, 3.8)
t = L.box(l + 0.6, s, 0.7); L.robot(l + 0.6, t, "pawn"); L.robot(l + 1.6, s, "semi")

L = level("L11_FullRoster", "W1_L11", "Full Roster", 3, [C, C, B, C], """
Three tiers: a ground hut, a two-storey tower on a low island, and a lookout on a high rock mesa.""")
L.robot(6.4, G, "semi"); L.hut(5.5, 7.3, G, 1.25)
s = L.island(10.0, G + 1.6, 3.2, 0)
r1 = L.hut(9.0, 11.0, s, 1.2); L.robot(10.0, s, "pawn")
L.hut(9.2, 10.8, r1, 1.1); L.robot(10.0, r1, "semi")
s, l, r = L.mount("Mesa", 14.0, 2.6)
L.robot((l + r) / 2, s, "pawn")

L = level("L12_HeavyGuard", "W1_L12", "Heavy Guard", 2, [C, C, B], """
A Harvester behind stone walls with a barrel at the door; pawns stand guard on the roof and on a high ledge.""")
top = L.post(8.4, G, 1.6, stone=True); L.post(10.8, G, 1.6, stone=True)
r = L.plank(8.2, 11.0, top, stone=True)
L.robot(9.6, G, "harv"); L.barrel(7.6, G)
L.robot(9.6, r, "pawn")
s = L.island(13.2, G + 2.4, 2.4, 0)
L.box(12.6, s, 0.6, stone=False)
L.robot(13.4, s, "pawn")

L = level("L13_TenStrong", "W1_L13", "Ten Strong", 3, [C, C, B, C], """
A tall timber tower on haybale stilts: a robot on every floor. Take out the hay and watch it fall.""")
for x in (8.55, 9.5, 10.45):
    t = L.hay(x, G)
f = L.plank(8.0, 11.0, t - 0.04)
for storey in range(3):
    L.robot(9.5, f, "pawn" if storey % 2 else "semi")
    f = L.hut(8.2, 10.8, f, 1.15)
L.robot(9.5, f, "semi")
s, l, r = L.mount("Scaffold", 13.8, 2.4)
L.robot((l + r) / 2 - 0.2, s, "pawn"); L.box(r - 0.35, s, 0.5, stone=False)

L = level("L14_StoneAndPawns", "W1_L14", "Stone And Pawns", 3, [C, C, B, B], """
A stone staircase on a low island with a pawn on every step.""")
s = L.island(10.2, G + 1.0, 5.6, 1)
for row in range(4):
    for k in range(5 - row):
        top = L.box(8.0 + (row + k) * 0.8, s + row * 0.8, 0.8)
    L.robot(8.0 + row * 0.8, s + (row + 1) * 0.8, "pawn")

L = level("L15_FiveStones", "W1_L15", "Five Stones", 2, [C, B, C], """
Five stone pillars hold up a long slab on a high island, two robots on top; a Harvester guards the near end
on the island's edge.""")
s = L.island(11.0, G + 2.6, 7.2, 0)
for x in (9.2, 10.2, 11.2, 12.2, 13.2):
    top = L.post(x, s, 1.0, stone=True)
L.plank(9.0, 11.2, top, stone=True); r = L.plank(11.2, 13.4, top, stone=True)
L.robot(10.1, r, "semi"); L.robot(12.3, r, "semi")
L.robot(8.3, s, "harv")
s, l, r = L.mount("Wagon", 5.0, 3.6)
L.robot(l + 1.0, s, "semi"); L.box(l + 1.8, s, 0.55, stone=False)

L = level("L16_TwinTowers", "W1_L16", "Twin Towers", 3, [C, C, B, C], """
Two timber towers on scaffolds joined by a plank bridge at the top, a robot on every floor and one on the bridge.""")
for xc in (8.4, 12.8):
    s_, l, r = L.mount("Scaffold", xc, 2.4)
    c = (l + r) / 2
    r1 = L.hut(c - 0.9, c + 0.9, s_, 1.15); L.robot(c, s_, "semi")
    r2 = L.hut(c - 0.75, c + 0.75, r1, 1.05); L.robot(c, r1, "pawn")
br = L.plank(8.364 + 0.45, 12.764 - 0.45, r2)   # overlaps each roof by 0.3 (support probe is the middle 90%)
L.robot(10.6, br, "semi")

L = level("L17_EightStrong", "W1_L17", "Eight Strong", 3, [C, C, B, C], """
A farmstead fortress: a stone wall with barrels, a ground hut, and a two-storey keep on an island behind.""")
t = L.box(6.6, G, 0.8); t = L.box(6.6, t, 0.8); L.robot(6.6, t, "pawn")
L.barrel(7.5, G); L.barrel(5.7, G)
L.robot(9.2, G, "semi"); L.hut(8.3, 10.1, G, 1.25)
s = L.island(12.8, G + 1.8, 4.0, 1)
r1 = L.hut(11.6, 14.0, s, 1.3, stone=True, roof_stone=True); L.robot(12.3, s, "semi"); L.barrel(13.3, s)
r2 = L.hut(11.9, 13.7, r1, 1.15); L.robot(12.8, r1, "pawn")
L.robot(12.8, r2, "harv")

L = level("L18_CaptainsLastStand", "W1_L18", "Captain's Last Stand", 3, [C, C, B, B], """
Boss: the Commander stands on a stone keep on a high island, a guard inside and barrels at the gate; a pawn
watches from a stone tower below.""", boss=True)
s = L.island(11.6, G + 2.4, 5.4, 1)
top = L.post(10.2, s, 1.6, stone=True); L.post(13.0, s, 1.6, stone=True)
r = L.plank(9.9, 13.3, top, stone=True)
L.robot(11.6, s, "semi"); L.barrel(9.4, s); L.barrel(13.8, s)
L.robot(11.6, r, "boss")
t = L.box(7.4, G, 0.8); t = L.box(7.4, t, 0.8); t = L.box(7.4, t, 0.8)
L.robot(7.4, t, "pawn")


# ═══════════════════════════════════════════ checks ═══════════════════════════════════════════════════════
LAUNCH = (-2.62, -2.69)          # barrel tip with the haybale mount (CatapultLauncher.ApplyMount)
GRAV, VMIN, VMAX = 8.0, 5.6, 13.6


def check(L):
    issues = []
    # support: 0.08 u probe under 90% of each block's (trimmed) collider, against ground/islands/other blocks
    for i, b in enumerate(L.blocks):
        l, r, bot, top = b["vis"]
        cx, hw = (l + r) / 2, (r - l) * 0.45
        ok = bot <= G + 0.08
        for isl in L.islands:
            base = isl["s"] - isl["uv"] * isl["h"]
            for (x0, x1, y0, y1) in isl["solids"]:
                sl, sr = isl["x"] - isl["w"] / 2 + x0 * isl["w"], isl["x"] - isl["w"] / 2 + x1 * isl["w"]
                st = base + y1 * isl["h"]
                if sl < cx + hw and sr > cx - hw and st - 0.6 < bot <= st + 0.08: ok = True
        for j, o in enumerate(L.blocks):
            if j != i:
                l2, r2, b2, t2 = o["vis"]
                if l2 < cx + hw and r2 > cx - hw and b2 < bot and t2 > bot - 0.08: ok = True
        if not ok: issues.append(f"block {i} ({b['kind']}) unsupported at x={cx:.2f} y={bot:.2f}")
    # reach: some angle/power passes within 0.45 u of each robot (obstacles ignored)
    for k, rb in enumerate(L.robots):
        hit = False
        for ang in range(20, 81, 2):
            for v in [VMIN + (VMAX - VMIN) * q / 20 for q in range(21)]:
                vx, vy = v * math.cos(math.radians(ang)), v * math.sin(math.radians(ang))
                t = 0
                while t < 4:
                    x, y = LAUNCH[0] + vx * t, LAUNCH[1] + vy * t - GRAV * t * t / 2
                    if (x - rb["x"]) ** 2 + (y - rb["y"]) ** 2 < 0.45 ** 2: hit = True; break
                    if y < G - 1: break
                    t += 0.01
                if hit: break
            if hit: break
        if not hit: issues.append(f"robot {k} at ({rb['x']:.2f},{rb['y']:.2f}) out of reach")
        if rb["x"] > 16.5: issues.append(f"robot {k} off-screen at 16:9")
    return issues


def write_cs():
    body = "\n\n".join(L.cs() for L in LEVELS)
    src = f"""// GENERATED by tools/design_world1.py — edit the designs there, not here.
// World 1 levels L02-L18 (2026-11-08 rebuild): Angry-Birds-style structures on the ground, raised islands and
// mounts, laid out by the sprites' visible edges for the reference framing (fixed zoom, trimmed colliders).

public static partial class LevelDataGenerator
{{
    static void MakeWorld1Rebuilt(string folder)
    {{
{body}
    }}
}}
"""
    path = os.path.join(UNITY, "Editor", "LevelDataGenerator.World1.cs")
    open(path, "w", encoding="utf-8").write(src)
    print("wrote", os.path.normpath(path))


def render(outdir):
    from PIL import Image, ImageOps
    import numpy as np
    S = os.path.join(UNITY, "Sprites")
    PX, camH, asp = 40, 16.0, 16 / 9
    camW = camH * asp
    cx, cy = -3.72 + (0.5 - 0.26) * camW, G + (0.5 - 0.12) * camH
    left, bottom = cx - camW / 2, cy - camH / 2
    cache = {}

    def load(p):
        if p not in cache: cache[p] = Image.open(os.path.join(S, p)).convert("RGBA")
        return cache[p]

    def put(canvas, img, x, y, w, h, alpha=1.0):
        im = img.resize((max(1, int(w * PX)), max(1, int(h * PX))))
        if alpha < 1:
            a = np.asarray(im).copy(); a[..., 3] = (a[..., 3] * alpha).astype(np.uint8); im = Image.fromarray(a)
        canvas.paste(im, (int((x - w / 2 - left) * PX), int((bottom + camH - (y + h / 2)) * PX)), im)

    base = Image.new("RGBA", (int(camW * PX), int(camH * PX)), (190, 215, 235, 255))
    far = load("Environment/Cornfield/CornfieldFar.png"); fw = camW; fh = fw * far.size[1] / far.size[0]
    put(base, far, cx, G - 0.3 + 4.2 * 0.8 + (0.5 - 0.052) * fh, fw, fh)
    for p, h, b in (("Environment/Cornfield/CornfieldMid.png", 4.2, G - 0.3), ("Environment/Cornfield/CornWall.png", 2.6, G - 0.15)):
        im = load(p); tw = h * im.size[0] / im.size[1]; k = math.floor((left - cx) / tw)
        for i in range(int(camW / tw) + 3):
            put(base, ImageOps.mirror(im) if (k + i) & 1 else im, cx + (k + i + 0.5) * tw, b + h / 2, tw, h)
    soil = load("Environment/Cornfield/CornSoil.png"); sh = 3.1; tw = sh * soil.size[0] / soil.size[1]
    for i in range(-2, 3): put(base, soil, cx + i * tw, G - sh * 0.837 + sh / 2, tw, sh)
    hb = load("Environment/World1Props/Haybail.png")
    for row in range(2):
        for k in range(3 - row): put(base, hb, -3.87 + (k - (2 - row) * 0.5) * 0.95, G + 1.15 * (0.5 - 0.038) + row * 0.8, 1.15, 1.15)
    can = load("Environment/Launchers/Cannon.png"); put(base, can, -3.72, -4.7032 + 1.61, can.size[0] / 384 * 1.4711, can.size[1] / 384 * 1.3317)
    robots = {"pawn": "Enemies/Robot/Robot_Pawn.png", "semi": "Enemies/Robot/Robot_SemiHarvest.png",
              "harv": "Enemies/Robot/HarvesterRobot.png", "boss": "Enemies/Robot/Commander.png"}
    os.makedirs(outdir, exist_ok=True)
    thumbs = []
    for L in LEVELS:
        c = base.copy()
        for isl in L.islands:
            name = f"PlatformWide_{'AB'[isl['v']]}" if isl["v"] < 2 else f"Mount_{isl['kind']}"
            im = load(f"Environment/Cornfield/{name}.png"); h = isl["h"]
            put(c, im, isl["x"], isl["s"] - isl["uv"] * h + h / 2, isl["w"], h)
        for b in L.blocks:
            put(c, load("Environment/World1Props/" + SPRITE[b["kind"]] + ".png"), b["x"], b["y"], b["w"], b["h"])
        for r in L.robots:
            im = load(robots[r["kind"]]); w = r["s"] * im.size[0] / (1746 if r["kind"] != "boss" else 1746); h = r["s"] * im.size[1] / 1746
            put(c, im, r["x"], r["y"], w, h)
        c = c.convert("RGB"); c.save(os.path.join(outdir, L.file + ".png")); thumbs.append((L, c))
    # contact sheet
    tw_, th_ = 480, 270
    sheet = Image.new("RGB", (tw_ * 3, th_ * math.ceil(len(thumbs) / 3)), (30, 30, 30))
    from PIL import ImageDraw
    d = ImageDraw.Draw(sheet)
    for i, (L, c) in enumerate(thumbs):
        t = c.resize((tw_, th_)); sheet.paste(t, ((i % 3) * tw_, (i // 3) * th_))
        d.text(((i % 3) * tw_ + 6, (i // 3) * th_ + 4), f"{L.id} {L.name}", fill=(255, 255, 0))
    sheet.save(os.path.join(outdir, "_sheet.png"))
    print("rendered", len(thumbs), "levels to", outdir)


if __name__ == "__main__":
    bad = 0
    for L in LEVELS:
        for msg in check(L):
            print(f"{L.id}: {msg}"); bad += 1
    print(f"{len(LEVELS)} levels, {sum(len(L.blocks) for L in LEVELS)} blocks, {sum(len(L.robots) for L in LEVELS)} robots, {bad} issues")
    write_cs()
    if "--render" in sys.argv:
        render(sys.argv[sys.argv.index("--render") + 1])
