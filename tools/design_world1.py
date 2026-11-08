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
MOUNT_VARIANT = {"Hill": 2, "Mesa": 3, "Scaffold": 4, "Wagon": 5, "Ramp": 6}
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

    def wagon_at(self, cx, ground_y, width, rolling=True):
        """A wagon standing on ground_y (e.g. a ramp plateau); rolling = parked until hit. Returns (deck y, left, right)."""
        m = MOUNTS["Wagon"]
        h = width / m["aspect"]
        surface = ground_y - 0.015 * h + m["uv"] * h
        self.islands.append(dict(x=cx, s=surface, w=width, v=5, solids=m["boxes"], h=h, uv=m["uv"], kind="Wagon", rolling=rolling))
        return surface, cx - width / 2 + m["flat"][0] * width, cx - width / 2 + m["flat"][1] * width

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
            out += [f"                P({f(i['x'])}, {f(i['s'])}, {f(i['w'])}, variant: {i['v']}"
                    + (", rolling: true" if i.get("rolling") else "") + ")," for i in self.islands]
            out.append("            },")
        if self.boss:
            out.append("            isBoss: true,")
        out.append("            referenceFraming: true);")
        return "\n".join(out)


# ═══════════════════════════════════════════ the levels ═══════════════════════════════════════════════════
# 2026-11-08 v3 (user: "build multiple stories high, so when they hit they slowly topple over and break down on the
# way down... random balancing beams... a wagon on a mount that rolls down into a bigger structure... some structures
# are too far apart"). Framed levels run live physics (blocks Dynamic-asleep, lower gravity), so these stacks topple
# for real. Everything sits between x 5.5 and 13.5.
C, B = "Cluck", "Bessie"
LEVELS = []


def level(*a, **k):
    L = Level(*a, **k)
    LEVELS.append(L)
    return L


def tower(L, xc, base, floors, width=1.9, h=1.15, taper=0.12, stone_floors=0, robots=None, top=None):
    """A multi-storey timber tower (posts + floor planks), narrowing as it rises. robots: kind per floor or None."""
    b, w = base, width
    for i in range(floors):
        if robots and i < len(robots) and robots[i]:
            L.robot(xc, b, robots[i])
        b = L.hut(xc - w / 2, xc + w / 2, b, h, stone=i < stone_floors, roof_stone=i < stone_floors)
        w = max(1.1, w - taper)
    if top:
        L.robot(xc, b, top)
    return b


def seesaw(L, xc, base, post_h, beam, left=None, right=None):
    """A long beam balanced on one post: things on its ends (robot kinds, 'crate', 'barrel', 'hay')."""
    t = L.post(xc, base, post_h)
    t = L.plank(xc - beam / 2, xc + beam / 2, t)
    for x, item in ((xc - beam / 2 + 0.45, left), (xc + beam / 2 - 0.45, right)):
        if item == "crate": L.box(x, t, 0.55, stone=False)
        elif item == "barrel": L.barrel(x, t)
        elif item == "hay": L.hay(x, t)
        elif item: L.robot(x, t, item)
    return t


def ramp_wagon(L, ramp_x, ramp_w=5.6, wagon_w=2.4):
    """A ramp (plateau left, slope down to the right) with a parked wagon at the plateau's edge.
    Returns (deck_y, deck_left, deck_right, slope_end_x)."""
    s, l, r = L.mount("Ramp", ramp_x, ramp_w)
    cx = r - 0.95 * wagon_w / 2.4
    deck, dl, dr = L.wagon_at(cx, s, wagon_w)
    slope_end = ramp_x - ramp_w / 2 + 0.98 * ramp_w
    return deck, dl, dr, slope_end


def edge_wagon(L, surface, edge_r, wagon_w=2.0):
    """A wagon parked at the right edge of a high surface (island / mesa top): when hit it tips off the edge and
    drops onto whatever is below. Returns (deck_y, deck_left, deck_right)."""
    cx = edge_r - 0.05 - 0.375 * wagon_w
    return L.wagon_at(cx, surface, wagon_w)


L = level("L02_StoneWall", "W1_L02", "Harvest Yard", 2, [C, C, C], """
Warm-up: a two-storey timber shed with a robot on each floor, and a see-saw beam with a robot balanced on one end.""")
tower(L, 7.4, G, 2, width=1.8, robots=["semi", "pawn"])
seesaw(L, 10.6, G, 1.0, 2.6, left="crate", right="pawn")

L = level("L03_TheTower", "W1_L03", "The Tower", 2, [C, C, C], """
A five-storey tapering timber tower, a robot every other floor and one on top. Hit it low and it leans, topples
and breaks apart on the way down.""")
tower(L, 9.4, G, 5, width=2.0, taper=0.14, robots=["semi", None, "pawn", None, "pawn"], top="semi")
L.hay(7.6, G)

L = level("L04_EggPractice", "W1_L04", "Egg Practice", 2, [C, C, C], """
Three exposed robots along a raised island, each under a little lean-to: spread Cluck's eggs across them.""")
s = L.island(9.8, G + 1.8, 6.6, 0)
for x in (7.8, 9.8, 11.8):
    L.hut(x - 0.6, x + 0.6, s, 1.05)
    L.robot(x, s, "pawn")

L = level("L05_TheGauntlet", "W1_L05", "Runaway Wagon", 2, [C, C, C], """
A loaded hay wagon is parked at the top of a ramp. Knock it and it rolls down the slope straight into the
three-storey tower at the bottom.""")
deck, dl, dr, end = ramp_wagon(L, 6.4)
L.box(dl + 0.35, deck, 0.5, stone=False); L.robot(dl + 1.05, deck, "pawn")
tower(L, end + 1.25, G, 3, width=1.9, robots=["semi", "pawn", "semi"], top="pawn")

L = level("L06_DoubleBarrel", "W1_L06", "Double Barrel", 2, [C, C, C], """
A two-storey farmhouse: dynamite beside each ground-floor robot, a lookout room upstairs and one on the roof.""")
for x in (7.0, 8.9, 10.8):
    top = L.post(x, G, 1.4)
L.plank(6.86, 8.9, top); f1 = L.plank(8.9, 10.94, top)
L.robot(7.6, G, "semi"); L.barrel(8.45, G)
L.robot(10.25, G, "semi"); L.barrel(9.4, G)
r = L.hut(7.9, 9.9, f1, 1.15); L.robot(8.9, f1, "pawn")
L.robot(8.9, r, "pawn")

L = level("L07_BarrelRow", "W1_L07", "Barrel Row", 2, [C, C, C], """
A wagon carrying dynamite is parked on the edge of a high island. Knock it off and it drops onto the stone bridge
over a row of barrels, a robot on the bridge and a lookout tower at the end.""")
s = L.island(10.0, G + 1.2, 6.0, 0)
for x in (8.4, 9.3, 10.2):
    L.barrel(x, s)
top = L.post(7.8, s, 1.0, stone=True); L.post(10.8, s, 1.0, stone=True)
r = L.plank(7.5, 11.1, top)
L.robot(9.3, r, "pawn")
tower(L, 12.2, s, 2, width=1.4, robots=["semi", None], top="pawn")
hi = L.island(6.0, G + 3.8, 3.0, 1)
deck, dl, dr = edge_wagon(L, hi, 6.0 + 1.5 * 0.92, 2.2)
L.barrel(dl + 0.5, deck); L.robot(dl + 1.25, deck, "pawn")

L = level("L08_FortressAssault", "W1_L08", "Fortress Assault", 3, [C, C, C, C], """
Three storeys: a stone ground floor with two rooms, two timber floors above, a robot on every level.""")
for x in (8.2, 10.0, 11.8):
    top = L.post(x, G, 1.4, stone=True)
L.plank(8.025, 10.0, top, stone=True); f1 = L.plank(10.0, 11.975, top, stone=True)
L.robot(9.1, G, "semi"); L.robot(10.9, G, "semi")
tower(L, 10.0, f1, 2, width=2.6, taper=0.5, robots=["pawn", "semi"], top="pawn")

L = level("L09_StoneBastion", "W1_L09", "Downhill Demolition", 3, [C, C, C, C], """
A wagon loaded with dynamite waits at the top of a ramp. Send it rolling into the stone bunker at the bottom.""")
deck, dl, dr, end = ramp_wagon(L, 6.2)
L.barrel(dl + 0.45, deck); L.barrel(dl + 1.15, deck)
x0 = end + 0.4
t = L.box(x0 + 0.4, G, 0.8); L.box(x0 + 0.4, t, 0.8)
t = L.box(x0 + 3.2, G, 0.8); top = L.box(x0 + 3.2, t, 0.8)
r = L.plank(x0 - 0.05, x0 + 3.65, top, stone=True)
L.robot(x0 + 1.8, G, "harv")
r2 = L.hut(x0 + 0.9, x0 + 2.7, r, 1.15); L.robot(x0 + 1.8, r, "pawn")
L.robot(x0 + 1.8, r2, "semi")

L = level("L10_BessiesDebut", "W1_L10", "Bessie's Debut", 2, [C, C, B], """
Bessie's first level: a four-row stone pyramid crowned by a robot, flanked by two see-saw beams.""")
for row, n in enumerate((4, 3, 2, 1)):
    b = G + row * 0.8
    for k in range(n):
        top = L.box(9.6 + (k - (n - 1) / 2) * 0.8, b, 0.8)
L.robot(9.6, top, "semi")
seesaw(L, 6.9, G, 0.9, 2.2, left="crate", right="pawn")
seesaw(L, 12.4, G, 0.9, 2.2, left="pawn", right="barrel")

L = level("L11_FullRoster", "W1_L11", "Mesa Drop", 3, [C, C, B, C], """
A wagon with a robot aboard is parked on top of a tall rock mesa. Tip it over the edge and it slides down the
mesa's side into the three-storey tower below; a second tower stands on a low island behind.""")
s, l, r = L.mount("Mesa", 7.2, 3.6)
deck, dl, dr = edge_wagon(L, s, r, 2.0)
L.box(dl + 0.3, deck, 0.5, stone=False); L.robot(dl + 0.95, deck, "pawn")
tower(L, 10.4, G, 3, width=1.9, robots=["semi", "pawn", "semi"], top="pawn")
s2 = L.island(12.9, G + 1.2, 2.4, 0)
tower(L, 12.9, s2, 2, width=1.6, robots=["pawn", "semi"], top=None)

L = level("L12_HeavyGuard", "W1_L12", "Heavy Guard", 2, [C, C, B], """
A Harvester inside a stone keep, a see-saw on its roof balancing a pawn against a barrel. High on an island to the
left a wagon waits at the edge, right above the dynamite barrel at the keep's door.""")
top = L.post(8.6, G, 1.6, stone=True); L.post(11.0, G, 1.6, stone=True)
r = L.plank(8.4, 11.2, top, stone=True)
L.robot(9.8, G, "harv"); L.barrel(7.8, G)
seesaw(L, 9.8, r, 0.8, 2.8, left="pawn", right="barrel")
L.robot(12.4, G, "pawn"); L.box(13.1, G, 0.6, stone=False)
hi = L.island(6.4, G + 4.2, 2.8, 0)
deck, dl, dr = edge_wagon(L, hi, 6.4 + 1.4 * 0.92, 2.0)
L.box(dl + 0.35, deck, 0.5, stone=False); L.robot(dl + 1.0, deck, "pawn")

L = level("L13_TenStrong", "W1_L13", "Hay Ride", 3, [C, C, B, C], """
A hay wagon with a robot riding on top waits on the ramp; below it stands a four-storey tower on haybale stilts.""")
deck, dl, dr, end = ramp_wagon(L, 6.0)
t = L.hay(dl + 0.55, deck); L.robot(dl + 1.3, deck, "pawn")
x0 = end + 1.6
for x in (x0 - 0.95, x0, x0 + 0.95):
    t = L.hay(x, G)
f = L.plank(x0 - 1.5, x0 + 1.5, t)
tower(L, x0, f, 4, width=2.2, robots=["semi", "pawn", "semi", None], top="pawn")

L = level("L14_StoneAndPawns", "W1_L14", "Stone And Pawns", 3, [C, C, B, B], """
A stone staircase on a low island with a pawn on every step, and a see-saw on the top step.""")
s = L.island(9.8, G + 1.0, 5.6, 1)
for row in range(4):
    for k in range(5 - row):
        top = L.box(7.6 + (row + k) * 0.8, s + row * 0.8, 0.8)
    if row < 3: L.robot(7.6 + row * 0.8, s + (row + 1) * 0.8, "pawn")
seesaw(L, 10.4, s + 3.2, 0.6, 1.8, left="pawn", right="crate")

L = level("L15_FiveStones", "W1_L15", "Five Stones", 2, [C, B, C], """
Five stone pillars hold a slab on a high island with a timber room on top; a Harvester guards the edge.""")
s = L.island(10.4, G + 2.4, 6.4, 0)
for x in (8.6, 9.6, 10.6, 11.6, 12.6):
    top = L.post(x, s, 1.0, stone=True)
L.plank(8.4, 10.6, top, stone=True); r = L.plank(10.6, 12.8, top, stone=True)
tower(L, 10.6, r, 2, width=2.6, taper=0.6, robots=["semi", "pawn"], top="semi")
L.robot(7.7, s, "harv")

L = level("L16_TwinTowers", "W1_L16", "Twin Towers", 3, [C, C, B, C], """
Two timber towers on scaffolds joined by a plank bridge at the top, a robot on every floor and one on the bridge.""")
for xc in (8.0, 11.9):
    s_, l, r = L.mount("Scaffold", xc, 2.4)
    c = (l + r) / 2
    r1 = L.hut(c - 0.9, c + 0.9, s_, 1.15); L.robot(c, s_, "semi")
    r2 = L.hut(c - 0.75, c + 0.75, r1, 1.05); L.robot(c, r1, "pawn")
br = L.plank(7.964 + 0.45, 11.864 - 0.45, r2)
L.robot(9.9, br, "semi")

L = level("L17_EightStrong", "W1_L17", "Wagon Train", 3, [C, C, B, C], """
A wagon carrying a little hut rolls off the ramp into a stone-and-timber fortress; dynamite waits by the walls.""")
deck, dl, dr, end = ramp_wagon(L, 5.8)
r = L.hut(dl + 0.15, dl + 1.25, deck, 0.9); L.robot(dl + 0.7, deck, "pawn")
x0 = end + 0.3
L.barrel(x0 + 0.3, G)
t = L.box(x0 + 1.1, G, 0.8); t = L.box(x0 + 1.1, t, 0.8); L.robot(x0 + 1.1, t, "pawn")
f = L.hut(x0 + 2.0, x0 + 4.2, G, 1.3, stone=True, roof_stone=True); L.robot(x0 + 3.1, G, "semi")
tower(L, x0 + 3.1, f, 2, width=1.9, robots=["pawn", "semi"], top="harv")

L = level("L18_CaptainsLastStand", "W1_L18", "Captain's Last Stand", 3, [C, C, B, B], """
Boss: the Commander stands on a three-storey stone-and-timber keep on a high island; guards balance on a see-saw
and watch from a stone tower below.""", boss=True)
s = L.island(10.8, G + 2.0, 5.4, 1)
top = L.post(9.4, s, 1.5, stone=True); L.post(12.2, s, 1.5, stone=True)
r = L.plank(9.1, 12.5, top, stone=True)
L.robot(10.8, s, "semi"); L.barrel(8.6, s); L.barrel(13.0, s)
r2 = L.hut(9.7, 11.9, r, 1.2); L.robot(10.8, r, "pawn")
L.robot(10.8, r2, "boss")
t = L.box(6.6, G, 0.8); t = L.box(6.6, t, 0.8); t = L.box(6.6, t, 0.8)
L.robot(6.6, t, "pawn")


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
