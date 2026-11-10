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
# Hay hoist art (tools/import_cornfield_art.py --ambience; keep in step with HayHoist.cs): crane aspect w/h, post
# centre across the width, rope anchor under the arm tip (x, y from the bottom), bucket w/h, rope knot height in the bucket.
HOIST = dict(aspect=1944 / 1882, post=0.244, anchor=(0.93, 0.70), bucket_aspect=1302 / 1724, handle=0.97)
MOUNT_VARIANT = {"Hill": 2, "Mesa": 3, "Scaffold": 4, "Wagon": 5, "Ramp": 6}
WOOD_T, STONE_T = 0.25, 0.30                            # visible plank / slab thickness

# ── World 2 (Frozen Tundra, 2026-11-09) ──────────────────────────────────────────────────────────────────────────
# The same pieces and mounts, frozen: frosted timber and ice instead of stone (BlockBase's World 2 sprites, wired by
# SceneSetup), the frozen mounts (tools/import_frozen_art.py), the frost robots. Art bounds are measured from the
# sprite files themselves. World 1 helper names map onto the frozen mounts, so tower()/ramp_wagon()/... work as-is.
SPR = os.path.join(UNITY, "Sprites")
SPRITE_W2 = {"post": "Environment/Frozen/FrostPost", "plank": "Environment/Frozen/FrostPlank",
             "crate": "Environment/World2Props/WoodBlock", "sbox": "Environment/World2Props/Ice_block",
             "pillar": "Environment/World2Props/Ice_vertical", "slab": "Environment/Frozen/IceSlab",
             "hay": "Environment/Frozen/FrozenBale", "barrel": "Environment/World2Props/Barrel_Dynamite"}


def _art_bounds(path):
    from PIL import Image
    import numpy as _np
    a = _np.asarray(Image.open(path).convert("RGBA"))[:, :, 3] > 0
    ys, xs = _np.nonzero(a)
    h, w = a.shape
    return (xs.min() / w, (xs.max() + 1) / w, (h - 1 - ys.max()) / h, (h - ys.min()) / h)


# ── World 3 (Watermill Village, 2026-11-10) ──────────────────────────────────────────────────────────────────────
# Iron-banded mill timber, river-stone masonry (cobble block / pillar / lintel slab) and flour sacks
# (tools/import_watermill_art.py); crates and barrels keep their World 1 art. Mounts: river islet, stone bridge pier,
# fishing jetty, rowing boat (the rolling mount), boat slipway. Contraptions: water pump, mill hoist with a millstone,
# rolling millstone.
SPRITE_W3 = {"post": "Environment/Watermill/MillPost", "plank": "Environment/Watermill/MillPlank",
             "crate": "Environment/World1Props/Plank_Short", "sbox": "Environment/Watermill/MillStoneBlock",
             "pillar": "Environment/Watermill/MillStonePillar", "slab": "Environment/Watermill/MillSlab",
             "hay": "Environment/Watermill/FlourSacks", "barrel": "Environment/World1Props/Barrel_Dynamite"}
ART_W = {1: ART, 2: {k: _art_bounds(os.path.join(SPR, v + ".png")) for k, v in SPRITE_W2.items()},
         3: {k: _art_bounds(os.path.join(SPR, v + ".png")) for k, v in SPRITE_W3.items()}}
# Frost robots: pawn/semi art is 426 px tall (World 1's is 256), scaled down so they fit a storey; vis = height.
ROBOT_W = {1: ROBOT, 2: {"pawn": ("Basic", 4.4, 1.074, 0.0), "semi": ("SemiHarvester", 4.4, 1.074, 0.0),
                         "harv": ("Harvester", 7.5, 1.099, 0.0), "boss": ("Commander", 7.0, 2.004, 0.06)},
           # Watermill robots are Stampede's 256 px art like World 1's (the pawn is a wide river-sub); the boss is the
           # Mill Stone Mauler, padded to the Commander's 500 px canvas.
           3: ROBOT}
MOUNTS_W = {1: MOUNTS, 2: _json.load(open(os.path.join(HERE, "mounts_w2.json"))),
            3: _json.load(open(os.path.join(HERE, "mounts_w3.json")))}
KIND_W2 = {"Mesa": "IcePillar", "Scaffold": "FrostScaffold", "Wagon": "Sled", "Ramp": "SnowRamp", "Hill": "Floe"}
_W2_FLOE = MOUNTS_W[2]["Floe"]
KIND_W3 = {"Mesa": "Pier", "Scaffold": "Jetty", "Wagon": "Boat", "Ramp": "Slipway", "Hill": "Islet"}
_W3_ISLET = MOUNTS_W[3]["Islet"]
ISLAND_W = {1: ISLAND, 2: [(_W2_FLOE["uv"], _W2_FLOE["aspect"])] * 2, 3: [(_W3_ISLET["uv"], _W3_ISLET["aspect"])] * 2}
POST_W = {1: 0.276, 2: 0.34, 3: 0.32}       # visible post width (the frosted post's art is stockier)
PILLAR_W = {1: 0.35, 2: 0.42, 3: 0.40}      # visible stone / ice pillar width
# Hoist crane + load per world: crane aspect w/h, post centre, rope anchor (x, y from the bottom), load w/h, knot height.
_W3_CRANE = MOUNTS_W[3]["MillCrane"]
HOIST_W = {1: HOIST, 2: HOIST,
           3: dict(aspect=_W3_CRANE["aspect"], post=_W3_CRANE["post"], anchor=tuple(_W3_CRANE["anchor"]),
                   bucket_aspect=506 / 512, handle=0.99)}
GADGET_NAMES = {1: ("Sunflower", "IceBoulder"), 2: ("IceCrystal", "IceBoulder"), 3: ("WaterPump", "Millstone")}


class Level:
    def __init__(self, file, lid, name, par, birds, comment, boss=False, world=1):
        self.file, self.id, self.name, self.par, self.birds, self.comment, self.boss = file, lid, name, par, birds, comment, boss
        self.world = world
        self.blocks, self.robots, self.islands = [], [], []
        self.weather, self.gadgets = "Golden", []

    # ── pieces (all take the VISIBLE bottom; return the visible top) ─────────────────────────────────────
    def _piece(self, kind, cx, bottom, vis_w, vis_h):
        x0, x1, y0, y1 = ART_W[self.world][kind]
        w, h = vis_w / (x1 - x0), vis_h / (y1 - y0)
        fx = cx - ((x0 + x1) / 2 - 0.5) * w
        fy = bottom - y0 * h + h / 2
        self.blocks.append(dict(kind=kind, x=fx, y=fy, w=w, h=h, vis=(cx - vis_w / 2, cx + vis_w / 2, bottom, bottom + vis_h)))
        return bottom + vis_h

    def post(self, x, b, h, stone=False):
        return self._piece("pillar", x, b, PILLAR_W[self.world], h) if stone else self._piece("post", x, b, POST_W[self.world], h)

    def plank(self, xa, xb, b, stone=False):
        return self._piece("slab" if stone else "plank", (xa + xb) / 2, b, xb - xa, STONE_T if stone else WOOD_T)

    def box(self, x, b, s=0.8, stone=True):
        return self._piece("sbox" if stone else "crate", x, b, s, s)

    def hay(self, x, b):
        a = ART_W[self.world]["hay"]
        return self._piece("hay", x, b, 0.977, 0.977 * (a[3] - a[2]) / max(1e-6, a[1] - a[0]))

    def barrel(self, x, b):
        x0, x1, y0, y1 = ART_W[self.world]["barrel"]
        if self.world == 2:   # keep World 1's visible barrel size; the frost barrel's own aspect
            return self._piece("barrel", x, b, 0.6, 0.6 * (y1 - y0) / (x1 - x0) * 285 / 409)
        return self._piece("barrel", x, b, 0.977 * (x1 - x0), 0.977 * (y1 - y0))

    def hut(self, xa, xb, b, h, stone=False, roof_stone=None):
        """Two posts at the ends and a roof across them; returns the roof's top."""
        inset = {1: (0.14, 0.175), 2: (0.17, 0.21), 3: (0.16, 0.20)}[self.world][1 if stone else 0]   # half the post width
        top = self.post(xa + inset, b, h, stone)
        self.post(xb - inset, b, h, stone)
        return self.plank(xa, xb, top, stone if roof_stone is None else roof_stone)

    def island(self, x, surface, width, variant=0):
        isl = ISLAND_W[self.world][variant]
        self.islands.append(dict(x=x, s=surface, w=width, v=variant, solids=[(0.04, 0.96, 0.17, isl[0])],
                                 h=width / isl[1], uv=isl[0]))
        return surface

    def wagon_at(self, cx, ground_y, width, rolling=True):
        """A wagon standing on ground_y (e.g. a ramp plateau); rolling = parked until hit. Returns (deck y, left, right)."""
        m = self._m("Wagon")
        h = width / m["aspect"]
        surface = ground_y - 0.015 * h + m["uv"] * h
        self.islands.append(dict(x=cx, s=surface, w=width, v=5, solids=m["boxes"], h=h, uv=m["uv"], kind="Wagon", rolling=rolling))
        return surface, cx - width / 2 + m["flat"][0] * width, cx - width / 2 + m["flat"][1] * width

    def _m(self, kind):
        names = {2: KIND_W2, 3: KIND_W3}.get(self.world, {})
        return MOUNTS_W[self.world][names.get(kind, kind)]

    def mount(self, kind, x, width):
        """A ground-standing mount (Hill/Mesa/Scaffold/Wagon). Returns (surface y, usable left x, usable right x)."""
        m = self._m(kind)
        h = width / m["aspect"]
        surface = G + m["uv"] * h
        self.islands.append(dict(x=x, s=surface, w=width, v=MOUNT_VARIANT[kind], solids=m["boxes"], h=h, uv=m["uv"], kind=kind))
        l = x - width / 2 + m["flat"][0] * width
        r = x - width / 2 + m["flat"][1] * width
        return surface, l, r

    # ── contraptions (2026-11-09) ───────────────────────────────────────────────────────────────────────
    def sunflower(self, x, surface, h=1.7):
        """A sunflower standing on surface; hit it and it sprays seeds to the right. Head ~0.72 h up."""
        self.gadgets.append(dict(type=GADGET_NAMES[self.world][0], x=x, y=surface, size=h, rope=0.0, left=False,
                                 head=(x, surface + 0.72 * h), r=0.24 * h))

    def boulder(self, x, surface, d=1.2):
        """A rolling ice boulder (World 2) parked on a surface; hit it and it rolls right into whatever is there."""
        self.gadgets.append(dict(type=GADGET_NAMES[self.world][1], x=x, y=surface, size=d, rope=0.0, left=False,
                                 head=(x, surface + d / 2), r=d / 2))

    def hoist(self, post_x, surface, h, rope, face_left=True):
        """A hay hoist: post foot at (post_x, surface), crane h tall, its arm reaching ~0.68 h sideways; a bucket hangs
        rope below the arm tip. Returns the bucket's (x, bottom y) so the target can be placed under it."""
        hs = HOIST_W[self.world]
        cw = h * hs["aspect"]
        left = post_x - cw * ((1 - hs["post"]) if face_left else hs["post"])
        ax = left + cw * ((1 - hs["anchor"][0]) if face_left else hs["anchor"][0])
        ay = surface + h * hs["anchor"][1]
        bw = 0.75; bh = bw / hs["bucket_aspect"]
        handle = ay - rope
        bottom = handle - hs["handle"] * bh
        self.gadgets.append(dict(type="HayHoist", x=post_x, y=surface, size=h, rope=rope, left=face_left,
                                 anchor=(ax, ay), bucket=(ax - bw / 2, ax + bw / 2, bottom, handle), cw=cw, cl=left))
        return ax, bottom

    def robot(self, x, surface, kind="semi"):
        t, sc, vis, pad = ROBOT_W[self.world][kind]
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
        if self.gadgets:
            out += ["            gadgets: new[]", "            {"]
            for g in self.gadgets:
                extra = f", ropeLength: {f(g['rope'])}" if g["type"] == "HayHoist" else ""
                extra += ", faceLeft: true" if g["left"] else ""
                out.append(f"                Gd(GadgetType.{g['type']}, {f(g['x'])}, {f(g['y'])}, {f(g['size'])}{extra}),")
            out.append("            },")
        if self.weather != "Golden":
            out.append(f"            weather: LevelWeather.{self.weather},")
        if self.world != 1:
            out.append(f"            world: {self.world},")
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


# 2026-11-09 v4 (user: "the structures must be built higher, multiple stories and topple easier"; plus weather and the
# farm contraptions): most towers gained one to three storeys (4-6 now), robots every other floor so a toppled tower
# takes several down with it; each level has a weather mood; sunflowers (seed spray) and hay hoists (dropping bucket).
L = level("L02_StoneWall", "W1_L02", "Harvest Yard", 2, [C, C, C], """
Warm-up: a three-storey timber shed with robots inside and on the roof, a see-saw beam with a robot balanced on one
end, and a sunflower in front: hit it and it sprays seeds into the shed.""")
tower(L, 7.6, G, 3, width=1.8, robots=["semi", None, "pawn"], top="pawn")
seesaw(L, 10.9, G, 1.0, 2.6, left="crate", right="pawn")
L.sunflower(5.7, G)

L = level("L03_TheTower", "W1_L03", "The Tower", 2, [C, C, C], """
A six-storey tapering timber tower, a robot every other floor and one on top. Hit it low and it leans, topples
and breaks apart on the way down.""")
tower(L, 9.4, G, 6, width=2.1, h=1.2, taper=0.16, robots=["semi", None, "pawn", None, "pawn", None], top="semi")
L.hay(7.4, G)

L = level("L04_EggPractice", "W1_L04", "Egg Practice", 2, [C, C, C], """
Robots under lean-tos along a raised island, two of them two storeys high: spread Cluck's eggs across them. A hay
hoist hangs a bucket over the middle one: cut its rope.""")
L.weather = "MorningMist"
s = L.island(9.8, G + 1.8, 6.6, 0)
for x, two in ((7.5, True), (9.35, False), (11.1, True)):
    r = L.hut(x - 0.6, x + 0.6, s, 1.05)
    L.robot(x, s, "pawn")
    if two:
        L.hut(x - 0.5, x + 0.5, r, 0.95)
        L.robot(x, r, "pawn")
L.hoist(12.5, s, 4.6, 0.7)

L = level("L05_TheGauntlet", "W1_L05", "Runaway Wagon", 2, [C, C, C], """
A loaded hay wagon is parked at the top of a ramp. Knock it and it rolls down the slope straight into the
five-storey tower at the bottom.""")
deck, dl, dr, end = ramp_wagon(L, 6.4)
L.box(dl + 0.35, deck, 0.5, stone=False); L.robot(dl + 1.05, deck, "pawn")
tower(L, end + 1.25, G, 5, width=2.0, taper=0.15, robots=["semi", None, "pawn", None, "semi"], top="pawn")

L = level("L06_DoubleBarrel", "W1_L06", "Double Barrel", 2, [C, C, C], """
A three-storey farmhouse: dynamite beside each ground-floor robot, a lookout room and an attic above. A hay hoist on
the right dangles a bucket over the first floor; a sunflower stands at the front door.""")
L.weather = "Overcast"
for x in (7.0, 8.9, 10.8):
    top = L.post(x, G, 1.4)
L.plank(6.86, 8.9, top); f1 = L.plank(8.9, 10.94, top)
L.robot(7.6, G, "semi"); L.barrel(8.45, G)
L.robot(10.25, G, "semi"); L.barrel(9.4, G)
r = L.hut(7.9, 9.9, f1, 1.15); L.robot(8.9, f1, "pawn")
r2 = L.hut(8.2, 9.6, r, 1.05); L.robot(8.9, r, "pawn")
L.robot(8.9, r2, "pawn")
L.hoist(14.1, G, 5.2, 0.5)
L.sunflower(6.0, G)

L = level("L07_BarrelRow", "W1_L07", "Barrel Row", 2, [C, C, C], """
A wagon carrying dynamite is parked on the edge of a high island. Knock it off and it drops onto the stone bridge
over a row of barrels, a robot on the bridge and a four-storey lookout tower at the end.""")
L.weather = "Dusk"
s = L.island(10.0, G + 1.2, 6.0, 0)
for x in (8.4, 9.3, 10.2):
    L.barrel(x, s)
top = L.post(7.8, s, 1.0, stone=True); L.post(10.8, s, 1.0, stone=True)
r = L.plank(7.5, 11.1, top)
L.robot(9.3, r, "pawn")
tower(L, 12.2, s, 4, width=1.4, h=1.1, taper=0.08, robots=["semi", None, "pawn", None], top="pawn")
hi = L.island(6.0, G + 3.8, 3.0, 1)
deck, dl, dr = edge_wagon(L, hi, 6.0 + 1.5 * 0.92, 2.2)
L.barrel(dl + 0.5, deck); L.robot(dl + 1.25, deck, "pawn")

L = level("L08_FortressAssault", "W1_L08", "Fortress Assault", 3, [C, C, C], """
Five storeys in the rain: a stone ground floor with two rooms and a four-storey timber tower rising from it. A
sunflower stands guard at the gate.""")
L.weather = "Rain"
for x in (8.2, 10.0, 11.8):
    top = L.post(x, G, 1.4, stone=True)
L.plank(8.025, 10.0, top, stone=True); f1 = L.plank(10.0, 11.975, top, stone=True)
L.robot(9.1, G, "semi"); L.robot(10.9, G, "semi")
tower(L, 10.0, f1, 4, width=2.6, taper=0.35, robots=["pawn", None, "semi", None], top="pawn")
L.sunflower(7.2, G)

L = level("L09_StoneBastion", "W1_L09", "Downhill Demolition", 3, [C, C, C], """
A wagon loaded with dynamite waits at the top of a ramp. Send it rolling into the stone bunker at the bottom, which
carries a timber house and a lookout tower on its roof.""")
deck, dl, dr, end = ramp_wagon(L, 6.2)
L.barrel(dl + 0.45, deck); L.barrel(dl + 1.15, deck)
x0 = end + 0.4
t = L.box(x0 + 0.4, G, 0.8); L.box(x0 + 0.4, t, 0.8)
t = L.box(x0 + 3.2, G, 0.8); top = L.box(x0 + 3.2, t, 0.8)
r = L.plank(x0 - 0.05, x0 + 3.65, top, stone=True)
L.robot(x0 + 1.8, G, "harv")
r2 = L.hut(x0 + 0.9, x0 + 2.7, r, 1.15); L.robot(x0 + 1.8, r, "pawn")
tower(L, x0 + 1.8, r2, 2, width=1.5, h=1.05, robots=["semi", None], top="pawn")

L = level("L10_BessiesDebut", "W1_L10", "Bessie's Debut", 2, [C, C, B], """
Bessie's first level: a stone pyramid carrying a two-storey timber lookout, flanked by two see-saw beams.""")
L.weather = "MorningMist"
for row, n in enumerate((4, 3, 2)):
    b = G + row * 0.8
    for k in range(n):
        top = L.box(9.6 + (k - (n - 1) / 2) * 0.8, b, 0.8)
tower(L, 9.6, top, 2, width=1.6, h=1.1, taper=0.1, robots=["semi", "pawn"], top="pawn")
seesaw(L, 6.9, G, 0.9, 2.2, left="crate", right="pawn")
seesaw(L, 12.4, G, 0.9, 2.2, left="pawn", right="barrel")

L = level("L11_FullRoster", "W1_L11", "Mesa Drop", 3, [C, C, B], """
A wagon with a robot aboard is parked on top of a tall rock mesa. Tip it over the edge and it slides down the
mesa's side into the five-storey tower below; a three-storey tower stands on a low island behind.""")
L.weather = "Dusk"
s, l, r = L.mount("Mesa", 7.2, 3.6)
deck, dl, dr = edge_wagon(L, s, r, 2.0)
L.box(dl + 0.3, deck, 0.5, stone=False); L.robot(dl + 0.95, deck, "pawn")
tower(L, 10.4, G, 5, width=2.0, taper=0.15, robots=["semi", None, "pawn", None, "semi"], top="pawn")
s2 = L.island(12.9, G + 1.2, 2.4, 0)
tower(L, 12.9, s2, 3, width=1.6, taper=0.1, robots=["pawn", None, "semi"])

L = level("L12_HeavyGuard", "W1_L12", "Heavy Guard", 2, [C, C, B], """
A Harvester inside a stone keep with a two-storey timber watch-house on its roof and a see-saw on top balancing a
pawn against a barrel. High on an island to the left a wagon waits at the edge, right above the dynamite barrel at
the keep's door.""")
L.weather = "Rain"
top = L.post(8.6, G, 1.6, stone=True); L.post(11.0, G, 1.6, stone=True)
r = L.plank(8.4, 11.2, top, stone=True)
L.robot(9.8, G, "harv"); L.barrel(7.8, G)
t = tower(L, 9.8, r, 2, width=2.2, h=1.05, taper=0.3, robots=["pawn", None])
seesaw(L, 9.8, t, 0.7, 2.4, left="pawn", right="barrel")
L.robot(12.4, G, "pawn"); L.box(13.1, G, 0.6, stone=False)
hi = L.island(6.4, G + 4.2, 2.8, 0)
deck, dl, dr = edge_wagon(L, hi, 6.4 + 1.4 * 0.92, 2.0)
L.box(dl + 0.35, deck, 0.5, stone=False); L.robot(dl + 1.0, deck, "pawn")

L = level("L13_TenStrong", "W1_L13", "Hay Ride", 3, [C, C, B], """
A hay wagon with a robot riding on top waits on the ramp; below it stands a five-storey tower on haybale stilts.""")
deck, dl, dr, end = ramp_wagon(L, 6.0)
t = L.hay(dl + 0.55, deck); L.robot(dl + 1.3, deck, "pawn")
x0 = end + 1.6
for x in (x0 - 0.95, x0, x0 + 0.95):
    t = L.hay(x, G)
f = L.plank(x0 - 1.5, x0 + 1.5, t)
tower(L, x0, f, 5, width=2.2, h=1.1, taper=0.2, robots=["semi", "pawn", None, "semi", None], top="pawn")

L = level("L14_StoneAndPawns", "W1_L14", "Stone And Pawns", 3, [C, B, B], """
A stone staircase on a low island with a pawn on every step, and a two-storey timber lookout on the top step.""")
L.weather = "Overcast"
s = L.island(9.8, G + 1.0, 5.6, 1)
for row in range(4):
    for k in range(5 - row):
        top = L.box(7.6 + (row + k) * 0.8, s + row * 0.8, 0.8)
    if row < 3: L.robot(7.6 + row * 0.8, s + (row + 1) * 0.8, "pawn")
tower(L, 10.4, s + 3.2, 2, width=1.6, h=1.05, taper=0.1, robots=["semi", None], top="pawn")

L = level("L15_FiveStones", "W1_L15", "Five Stones", 2, [C, B, C], """
Five stone pillars hold a slab on a high island with a three-storey timber house on top; a Harvester guards the
edge.""")
L.weather = "MorningMist"
s = L.island(10.4, G + 2.4, 6.4, 0)
for x in (8.6, 9.6, 10.6, 11.6, 12.6):
    top = L.post(x, s, 1.0, stone=True)
L.plank(8.4, 10.6, top, stone=True); r = L.plank(10.6, 12.8, top, stone=True)
tower(L, 10.6, r, 3, width=2.6, h=1.1, taper=0.45, robots=["semi", None, "pawn"], top="semi")
L.robot(7.7, s, "harv")

L = level("L16_TwinTowers", "W1_L16", "Twin Towers", 3, [C, C, B], """
Two four-storey timber towers on scaffolds joined by a plank bridge at the top, robots inside and one on the bridge.
A sunflower stands in front of the left tower.""")
L.weather = "Rain"
cs = []
for xc in (8.0, 11.9):
    s_, l, r = L.mount("Scaffold", xc, 2.4)
    c = (l + r) / 2
    cs.append(c)
    top = tower(L, c, s_, 4, width=1.8, h=1.05, taper=0.12, robots=["semi", None, "pawn", None])
br = L.plank(cs[0] + 0.3, cs[1] - 0.3, top)
L.robot((cs[0] + cs[1]) / 2, br, "semi")
L.sunflower(6.3, G)

L = level("L17_EightStrong", "W1_L17", "Wagon Train", 3, [C, C, B], """
A wagon carrying a little hut rolls off the ramp into a stone-and-timber fortress: a stone hall with a four-storey
timber tower above; dynamite waits by the walls.""")
L.weather = "Dusk"
deck, dl, dr, end = ramp_wagon(L, 5.8)
r = L.hut(dl + 0.15, dl + 1.25, deck, 0.9); L.robot(dl + 0.7, deck, "pawn")
x0 = end + 0.3
L.barrel(x0 + 0.3, G)
t = L.box(x0 + 1.1, G, 0.8); t = L.box(x0 + 1.1, t, 0.8); L.robot(x0 + 1.1, t, "pawn")
f = L.hut(x0 + 2.0, x0 + 4.2, G, 1.3, stone=True, roof_stone=True); L.robot(x0 + 3.1, G, "semi")
tower(L, x0 + 3.1, f, 4, width=1.9, h=1.05, taper=0.12, robots=["pawn", None, "semi", None], top="harv")

L = level("L18_CaptainsLastStand", "W1_L18", "Captain's Last Stand", 3, [C, B, B], """
Boss, in a thunderstorm: the Commander stands on top of a four-storey stone-and-timber keep on a high island; a
guard watches from a stone tower below.""", boss=True)
L.weather = "Rain"
s = L.island(10.8, G + 2.0, 5.4, 1)
top = L.post(9.4, s, 1.5, stone=True); L.post(12.2, s, 1.5, stone=True)
r = L.plank(9.1, 12.5, top, stone=True)
L.robot(10.8, s, "semi"); L.barrel(8.6, s); L.barrel(13.0, s)
t = tower(L, 10.8, r, 3, width=2.4, h=1.1, taper=0.2, robots=["pawn", None, "semi"])
L.robot(10.8, t, "boss")
t = L.box(6.6, G, 0.8)
for _ in range(3): t = L.box(6.6, t, 0.8)
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
        if not reachable(rb["x"], rb["y"]): issues.append(f"robot {k} at ({rb['x']:.2f},{rb['y']:.2f}) out of reach")
        if rb["x"] > 16.5: issues.append(f"robot {k} off-screen at 16:9")
        if rb["y"] + rb["vis"] / 2 > G + 12.0: issues.append(f"robot {k} under the HUD (top {rb['y'] + rb['vis'] / 2 - G:.1f} u up)")
    # contraptions: reachable; a hoist's bucket hangs clear of everything and has a target below it
    for g in L.gadgets:
        if g["type"] in ("Sunflower", "IceCrystal", "IceBoulder", "WaterPump", "Millstone"):
            if not reachable(*g["head"]): issues.append(f"{g['type']} at x={g['x']:.2f} out of reach")
            continue
        bl, br, bb, bt = g["bucket"]
        if not reachable((bl + br) / 2, (bb + bt) / 2): issues.append(f"hoist bucket at x={(bl + br) / 2:.2f} out of reach")
        below = False
        for b in L.blocks:
            l2, r2, b2, t2 = b["vis"]
            if l2 < br and r2 > bl and b2 < bt and t2 > bb - 0.05:
                issues.append(f"hoist bucket overlaps block {b['kind']} at x={(l2 + r2) / 2:.2f}")
            if l2 < br and r2 > bl and t2 <= bb: below = True
        for r in L.robots:
            if abs(r["x"] - (bl + br) / 2) < 0.4 + 0.375:
                if r["y"] + r["vis"] / 2 > bb - 0.05 and r["y"] - r["vis"] / 2 < bt:
                    issues.append(f"hoist bucket overlaps a robot at x={r['x']:.2f}")
                elif r["y"] < bb: below = True
        if not below: issues.append(f"hoist bucket at x={(bl + br) / 2:.2f} has nothing under it")
    return issues


def reachable(tx, ty, tol=0.45):
    for ang in range(20, 81, 2):
        for v in [VMIN + (VMAX - VMIN) * q / 20 for q in range(21)]:
            vx, vy = v * math.cos(math.radians(ang)), v * math.sin(math.radians(ang))
            t = 0
            while t < 4:
                x, y = LAUNCH[0] + vx * t, LAUNCH[1] + vy * t - GRAV * t * t / 2
                if (x - tx) ** 2 + (y - ty) ** 2 < tol * tol: return True
                if y < G - 1: break
                t += 0.01
    return False


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

    def tint(img, c):
        a = np.asarray(img).astype(np.float32).copy(); a[..., :3] *= np.array(c[:3], np.float32); return Image.fromarray(a.clip(0, 255).astype(np.uint8))

    def strip(canvas, p, h, b, col=(1, 1, 1)):
        im = tint(load(p), col); tw = h * im.size[0] / im.size[1]; k = math.floor((left - cx) / tw)
        for i in range(int(camW / tw) + 3):
            put(canvas, ImageOps.mirror(im) if (k + i) & 1 else im, cx + (k + i + 0.5) * tw, b + h / 2, tw, h)

    def overlay(canvas, col, a):
        if a <= 0: return
        ov = Image.new("RGBA", canvas.size, tuple(int(255 * v) for v in col) + (int(255 * a),)); canvas.alpha_composite(ov)

    def backdrop(wx):
        w = WEATHER[wx]
        base = Image.new("RGBA", (int(camW * PX), int(camH * PX)), (190, 215, 235, 255))
        far = tint(load("Environment/Cornfield/CornfieldFar.png"), w["sky"]); fw = camW; fh = fw * far.size[1] / far.size[0]
        put(base, far, cx, G - 0.3 + 4.2 * 0.8 + (0.5 - 0.052) * fh, fw, fh)
        for i, ci in enumerate(range(min(w["clouds"], 4))):
            im = tint(load(f"Environment/Cornfield/Cloud_{ci % 3}.png"), w["sky"]); h = camH * 0.11
            put(base, im, left + camW * (0.15 + 0.22 * i), bottom + camH * (0.74 + 0.05 * (i % 2)), h * im.size[0] / im.size[1], h, 0.85)
        strip(base, "Environment/Cornfield/CornRidges.png", 4.4, G + 3.0, w["sky"])
        overlay(base, w["haze"], w["hazeA"])
        strip(base, "Environment/Cornfield/CornHills.png", 3.3, G + 2.7, w["land"])
        strip(base, "Environment/Cornfield/CornfieldMid.png", 4.2, G - 0.3, w["land"])
        strip(base, "Environment/Cornfield/CornWall.png", 2.6, G - 0.15, tuple(0.82 * v for v in w["land"]))
        soil = load("Environment/Cornfield/CornSoil.png"); sh = 3.1; tw = sh * soil.size[0] / soil.size[1]
        for i in range(-2, 3): put(base, soil, cx + i * tw, G - sh * 0.837 + sh / 2, tw, sh)
        return base

    WEATHER = {  # mirrors CornfieldAmbience's profiles (sky / land tints, haze, grade)
        "Golden": dict(sky=(1, 1, 1), land=(1, 1, 1), haze=(1, .87, .74), hazeA=.26, grade=((1, .85, .6), 0), clouds=4),
        "MorningMist": dict(sky=(.93, .97, 1), land=(.9, .95, .96), haze=(.86, .9, .95), hazeA=.42, grade=((.8, .88, 1), .06), clouds=2),
        "Overcast": dict(sky=(.72, .76, .82), land=(.8, .84, .85), haze=(.78, .8, .85), hazeA=.38, grade=((.25, .3, .42), .10), clouds=6),
        "Rain": dict(sky=(.55, .6, .7), land=(.66, .71, .75), haze=(.6, .65, .73), hazeA=.42, grade=((.12, .16, .3), .22), clouds=7),
        "Dusk": dict(sky=(1, .74, .62), land=(.93, .74, .68), haze=(1, .66, .5), hazeA=.3, grade=((.55, .2, .28), .12), clouds=3),
    }
    bases = {}
    can = load("Environment/Launchers/Cannon.png")
    robots = {"pawn": "Enemies/Robot/Robot_Pawn.png", "semi": "Enemies/Robot/Robot_SemiHarvest.png",
              "harv": "Enemies/Robot/HarvesterRobot.png", "boss": "Enemies/Robot/Commander.png"}
    os.makedirs(outdir, exist_ok=True)
    thumbs = []
    for L in LEVELS:
        if L.weather not in bases: bases[L.weather] = backdrop(L.weather)
        c = bases[L.weather].copy()
        hb = load("Environment/World1Props/Haybail.png")
        for row in range(2):
            for k in range(3 - row): put(c, hb, -3.87 + (k - (2 - row) * 0.5) * 0.95, G + 1.15 * (0.5 - 0.038) + row * 0.8, 1.15, 1.15)
        put(c, can, -3.72, -4.7032 + 1.61, can.size[0] / 384 * 1.4711, can.size[1] / 384 * 1.3317)
        for g in L.gadgets:
            if g["type"] == "Sunflower":
                im = load("Environment/Cornfield/Sunflower.png"); h = g["size"]; w = h * im.size[0] / im.size[1]
                put(c, im, g["x"], g["y"] + h / 2, w, h)
            else:
                im = load("Environment/Cornfield/HoistCrane.png")
                if g["left"]: im = ImageOps.mirror(im)
                put(c, im, g["cl"] + g["cw"] / 2, g["y"] + g["size"] / 2, g["cw"], g["size"])
                bl, br, bb, bt = g["bucket"]; ax, ay = g["anchor"]
                from PIL import ImageDraw as _D
                d = _D.Draw(c); d.line([((ax - left) * PX, (bottom + camH - ay) * PX), ((ax - left) * PX, (bottom + camH - bt) * PX)], fill=(140, 107, 64, 255), width=2)
                bim = load("Environment/Cornfield/HoistBucket.png"); bh = (br - bl) / bim.size[0] * bim.size[1]
                put(c, bim, (bl + br) / 2, bb + bh / 2, br - bl, bh)
        for isl in L.islands:
            name = f"PlatformWide_{'AB'[isl['v']]}" if isl["v"] < 2 else f"Mount_{isl['kind']}"
            im = load(f"Environment/Cornfield/{name}.png"); h = isl["h"]
            put(c, im, isl["x"], isl["s"] - isl["uv"] * h + h / 2, isl["w"], h)
        for b in L.blocks:
            put(c, load("Environment/World1Props/" + SPRITE[b["kind"]] + ".png"), b["x"], b["y"], b["w"], b["h"])
        for r in L.robots:
            im = load(robots[r["kind"]]); w = r["s"] * im.size[0] / (1746 if r["kind"] != "boss" else 1746); h = r["s"] * im.size[1] / 1746
            put(c, im, r["x"], r["y"], w, h)
        overlay(c, *WEATHER[L.weather]["grade"])
        if L.weather == "Rain":
            import random as _r
            from PIL import ImageDraw as _D
            rnd = _r.Random(3); d = _D.Draw(c)
            for _ in range(160):
                x, y = rnd.uniform(0, c.size[0]), rnd.uniform(0, c.size[1]); ln = rnd.uniform(25, 45)
                d.line([(x, y), (x - ln * 0.3, y + ln)], fill=(210, 222, 245, 70), width=1)
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
