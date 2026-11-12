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
# ── World 4 (Sky Islands, 2026-11-11) ────────────────────────────────────────────────────────────────────────
# Sky-blue airship timber, white marble (block / column / lintel slab) and cloud bales (tools/import_sky_art.py);
# crates and barrels keep their World 1 art. Mounts: wide floating isle, small floating rock, cloud-wrapped stone
# pillar, sky dock, slide ramp (no rolling wagon: the runaway is a thunder wheel). Contraptions: thundercloud
# (lightning bolts), sky crane with a thunder wheel, rolling thunder wheel.
SPRITE_W4 = {"post": "Environment/Sky/SkyPost", "plank": "Environment/Sky/SkyPlank",
             "crate": "Environment/World1Props/Plank_Short", "sbox": "Environment/Sky/SkyBlock",
             "pillar": "Environment/Sky/SkyPillar", "slab": "Environment/Sky/SkySlab",
             "hay": "Environment/Sky/CloudBale", "barrel": "Environment/World1Props/Barrel_Dynamite"}
# ── World 5 (Sunken City, 2026-11-12) ────────────────────────────────────────────────────────────────────────
# Barnacled shipwreck timber, teal sunken-city stone (block / column / lintel slab) and sponge bales
# (tools/import_sunken_art.py); crates and barrels keep their World 1 art. Mounts: coral reef mesas low / mid / tall
# (sized like World 4's cloud mountains). Contraptions: the pearl-spitting clam, the rolling ship's wheel.
SPRITE_W5 = {"post": "Environment/Sunken/SeaPost", "plank": "Environment/Sunken/SeaPlank",
             "crate": "Environment/World1Props/Plank_Short", "sbox": "Environment/Sunken/SeaBlock",
             "pillar": "Environment/Sunken/SeaPillar", "slab": "Environment/Sunken/SeaSlab",
             "hay": "Environment/Sunken/SpongeBale", "barrel": "Environment/World1Props/Barrel_Dynamite"}
# ── World 6 (Robot Mothership, 2026-11-12) ───────────────────────────────────────────────────────────────────
# Steel girder post and I-beam, riveted crate / energy column / steel slab and battery pods
# (tools/import_mothership_art.py); crates and barrels keep their World 1 art. Mounts: machinery pedestals low / mid /
# tall (sized like the reefs). Contraptions: the laser emitter (a jet of plasma orbs), the rolling gravity ring.
SPRITE_W6 = {"post": "Environment/Mothership/ShipPost", "plank": "Environment/Mothership/ShipPlank",
             "crate": "Environment/World1Props/Plank_Short", "sbox": "Environment/Mothership/ShipBlock",
             "pillar": "Environment/Mothership/ShipPillar", "slab": "Environment/Mothership/ShipSlab",
             "hay": "Environment/Mothership/BatteryPod", "barrel": "Environment/World1Props/Barrel_Dynamite"}
ART_W = {1: ART, 2: {k: _art_bounds(os.path.join(SPR, v + ".png")) for k, v in SPRITE_W2.items()},
         3: {k: _art_bounds(os.path.join(SPR, v + ".png")) for k, v in SPRITE_W3.items()},
         4: {k: _art_bounds(os.path.join(SPR, v + ".png")) for k, v in SPRITE_W4.items()},
         5: {k: _art_bounds(os.path.join(SPR, v + ".png")) for k, v in SPRITE_W5.items()},
         6: {k: _art_bounds(os.path.join(SPR, v + ".png")) for k, v in SPRITE_W6.items()}}
# Frost robots: pawn/semi art is 426 px tall (World 1's is 256), scaled down so they fit a storey; vis = height.
ROBOT_W = {1: ROBOT, 2: {"pawn": ("Basic", 4.4, 1.074, 0.0), "semi": ("SemiHarvester", 4.4, 1.074, 0.0),
                         "harv": ("Harvester", 7.5, 1.099, 0.0), "boss": ("Commander", 7.0, 2.004, 0.06)},
           # Watermill robots are Stampede's 256 px art like World 1's (the pawn is a wide river-sub); the boss is the
           # Mill Stone Mauler, padded to the Commander's 500 px canvas.
           3: ROBOT,
           # Sky robots are Stampede's 256 px art (rover, harvester, drone); the boss is the Storm Baron airship, padded
           # to the Commander's 500 px canvas.
           4: ROBOT,
           # Sea robots are Stampede's 256 px art too; the boss is the Deep Dredger, padded to the Commander's canvas -
           # 1.5x the Commander's scale (2026-11-12, user: "increase the size of the sunken world boss"; the wide
           # submarine read small on the 500 px canvas).
           5: dict(ROBOT, boss=("Commander", 10.5, 3.006, 0.06)),
           # Mothership robots are Stampede's 256 px art too; the boss is the Factory Machine, padded like the Commander.
           6: ROBOT}
MOUNTS_W = {1: MOUNTS, 2: _json.load(open(os.path.join(HERE, "mounts_w2.json"))),
            3: _json.load(open(os.path.join(HERE, "mounts_w3.json"))),
            4: _json.load(open(os.path.join(HERE, "mounts_w4.json"))),
            5: _json.load(open(os.path.join(HERE, "mounts_w5.json"))),
            6: _json.load(open(os.path.join(HERE, "mounts_w6.json")))}
KIND_W2 = {"Mesa": "IcePillar", "Scaffold": "FrostScaffold", "Wagon": "Sled", "Ramp": "SnowRamp", "Hill": "Floe"}
_W2_FLOE = MOUNTS_W[2]["Floe"]
KIND_W3 = {"Mesa": "Pier", "Scaffold": "Jetty", "Wagon": "Boat", "Ramp": "Slipway", "Hill": "Islet"}
_W3_ISLET = MOUNTS_W[3]["Islet"]
KIND_W4 = {"Mesa": "Pillar", "Scaffold": "Dock", "Ramp": "Ramp", "Hill": "Rock"}
KIND_W5 = {"Mesa": "ReefTall", "Hill": "ReefLow"}
_W5_REEF = MOUNTS_W[5]["ReefLow"]
KIND_W6 = {"Mesa": "DeckTall", "Hill": "DeckLow"}
_W6_DECK = MOUNTS_W[6]["DeckLow"]
_W4_ISLE = MOUNTS_W[4]["CloudLow"]   # World 4 islands are cloud mountains (design_world4.cloud) since 2026-11-11
ISLAND_W = {1: ISLAND, 2: [(_W2_FLOE["uv"], _W2_FLOE["aspect"])] * 2, 3: [(_W3_ISLET["uv"], _W3_ISLET["aspect"])] * 2,
            4: [(_W4_ISLE["uv"], _W4_ISLE["aspect"])] * 2, 5: [(_W5_REEF["uv"], _W5_REEF["aspect"])] * 2,
            6: [(_W6_DECK["uv"], _W6_DECK["aspect"])] * 2}
POST_W = {1: 0.276, 2: 0.34, 3: 0.32, 4: 0.30, 5: 0.30, 6: 0.30}       # visible post width (the frosted post's art is stockier)
PILLAR_W = {1: 0.35, 2: 0.42, 3: 0.40, 4: 0.36, 5: 0.36, 6: 0.36}      # visible stone / ice pillar width
# Hoist crane + load per world: crane aspect w/h, post centre, rope anchor (x, y from the bottom), load w/h, knot height.
_W3_CRANE = MOUNTS_W[3]["MillCrane"]
HOIST_W = {1: HOIST, 2: HOIST,
           3: dict(aspect=_W3_CRANE["aspect"], post=_W3_CRANE["post"], anchor=tuple(_W3_CRANE["anchor"]),
                   bucket_aspect=506 / 512, handle=0.99),
           4: dict(aspect=MOUNTS_W[4]["SkyCrane"]["aspect"], post=MOUNTS_W[4]["SkyCrane"]["post"],
                   anchor=tuple(MOUNTS_W[4]["SkyCrane"]["anchor"]), bucket_aspect=1.0, handle=0.99),
           5: HOIST, 6: HOIST}   # no crane in Worlds 5 and 6
GADGET_NAMES = {1: ("Sunflower", "IceBoulder"), 2: ("IceCrystal", "IceBoulder"), 3: ("WaterPump", "Millstone"),
                4: ("StormCloud", "ThunderWheel"), 5: ("Clam", "ShipsWheel"),
                6: ("LaserEmitter", "GravityRing")}

# ── Paid worlds (2026-11-12, tools/import_paid_worlds_art.py) ────────────────────────────────────────────────────
# World 7 Dustbowl Canyon: ranch timber, red sandstone (block / pillar / slab), dusty hay; red-rock mesas; the
# spine-shooting saguaro and a rolling tumbleweed. World 8 Harvest Fairground: candy-striped poles and planks, striped
# crates / carousel columns / stage slabs, prize cushions; circus podiums; the popcorn cart and a rolling pumpkin.
# World 9 Crop Factory: steel trusses and girders, steel crates / pipe columns / hazard plates, produce crates; machine
# housings; the corn hopper and a rolling cog. Crates and barrels keep their World 1 art; robots are Stampede's 256 px
# art like World 1's, the bosses (Drill Rig, Ringmaster, Harvester Prime) padded to the Commander's canvas.
for _w, _folder, _names in ((7, "Canyon", ("RanchPost", "RanchPlank", "SandBlock", "SandPillar", "SandSlab", "DustyBale")),
                            (8, "Fair", ("FairPost", "FairPlank", "FairBlock", "FairPillar", "FairSlab", "PrizeBale")),
                            (9, "Factory", ("FactoryPost", "FactoryPlank", "FactoryBlock", "FactoryPillar", "FactorySlab", "ProduceCrate"))):
    _spr = dict(zip(("post", "plank", "sbox", "pillar", "slab", "hay"), (f"Environment/{_folder}/{n}" for n in _names)))
    _spr.update(crate="Environment/World1Props/Plank_Short", barrel="Environment/World1Props/Barrel_Dynamite")
    globals()[f"SPRITE_W{_w}"] = _spr
    ART_W[_w] = {k: _art_bounds(os.path.join(SPR, v + ".png")) for k, v in _spr.items()}
    ROBOT_W[_w] = ROBOT
    MOUNTS_W[_w] = _json.load(open(os.path.join(HERE, f"mounts_w{_w}.json")))
    POST_W[_w], PILLAR_W[_w] = 0.30, 0.36
    HOIST_W[_w] = HOIST
KIND_W7 = {"Mesa": "MesaTall", "Hill": "MesaLow"}
KIND_W8 = {"Mesa": "PodiumTall", "Hill": "PodiumLow"}
KIND_W9 = {"Mesa": "HousingTall", "Hill": "HousingLow"}
for _w, _k in ((7, "MesaLow"), (8, "PodiumLow"), (9, "HousingLow")):
    ISLAND_W[_w] = [(MOUNTS_W[_w][_k]["uv"], MOUNTS_W[_w][_k]["aspect"])] * 2
GADGET_NAMES.update({7: ("Cactus", "Tumbleweed"), 8: ("PopcornCart", "Pumpkin"), 9: ("CornHopper", "Cog")})
INSET_W = {1: (0.14, 0.175), 2: (0.17, 0.21), 3: (0.16, 0.20), 4: (0.15, 0.18), 5: (0.15, 0.18), 6: (0.15, 0.18),
           7: (0.15, 0.18), 8: (0.15, 0.18), 9: (0.15, 0.18)}   # half the post / pillar width


def _rect_poly(cx, cy, w, h, rot):
    c, s_ = math.cos(math.radians(rot)), math.sin(math.radians(rot))
    return [(cx + c * dx - s_ * dy, cy + s_ * dx + c * dy) for dx, dy in ((-w / 2, -h / 2), (w / 2, -h / 2), (w / 2, h / 2), (-w / 2, h / 2))]


def _sat_overlap(a, b):
    """Penetration depth of two convex polygons (0 when apart)."""
    depth = 1e9
    for poly in (a, b):
        for i in range(len(poly)):
            x1, y1 = poly[i]; x2, y2 = poly[(i + 1) % len(poly)]
            nx, ny = y2 - y1, x1 - x2
            ln = math.hypot(nx, ny) or 1.0
            nx, ny = nx / ln, ny / ln
            pa = [px * nx + py * ny for px, py in a]; pb = [px * nx + py * ny for px, py in b]
            d = min(max(pa), max(pb)) - max(min(pa), min(pb))
            if d <= 0: return 0.0
            depth = min(depth, d)
    return depth


def _shrink(poly, by):
    """Pull a rectangle's edges in by `by` (the trimmed collider sits ~0.02 inside the visible art)."""
    cx = sum(p[0] for p in poly) / 4; cy = sum(p[1] for p in poly) / 4
    out = []
    for i, (x, y) in enumerate(poly):
        ex, ey = poly[(i + 1) % 4]; wx, wy = poly[(i - 1) % 4]
        ux, uy = ex - x, ey - y; lu = math.hypot(ux, uy) or 1
        vx, vy = wx - x, wy - y; lv = math.hypot(vx, vy) or 1
        out.append((x + ux / lu * by + vx / lv * by, y + uy / lu * by + vy / lv * by))
    return out


class Level:
    def __init__(self, file, lid, name, par, birds, comment, boss=False, world=1):
        self.file, self.id, self.name, self.par, self.birds, self.comment, self.boss = file, lid, name, par, birds, comment, boss
        self.world = world
        self.blocks, self.robots, self.islands = [], [], []
        self.weather, self.gadgets = "Golden", []
        self.rooms = 1   # multi-room levels (2026-11-12): items carry "room"; room r is laid out r * ROOM_STRIDE right

    def add_room(self, other):
        """Appends another level's layout as the next room: shifted ROOM_STRIDE per room to the right, tagged."""
        room, dx = self.rooms, self.rooms * ROOM_STRIDE
        for b in other.blocks:
            b = dict(b, room=room, x=b["x"] + dx, vc=(b["vc"][0] + dx, b["vc"][1]),
                     poly=[(x + dx, y) for x, y in b["poly"]], vis=(b["vis"][0] + dx, b["vis"][1] + dx, b["vis"][2], b["vis"][3]))
            self.blocks.append(b)
        for r in other.robots: self.robots.append(dict(r, room=room, x=r["x"] + dx))
        for i in other.islands:
            i = dict(i, room=room, x=i["x"] + dx)
            if "span" in i: i["span"] = (i["span"][0] + dx, i["span"][1] + dx)   # World 4 cloud plateau
            self.islands.append(i)
        for g in other.gadgets:
            g = dict(g, room=room, x=g["x"] + dx)
            if "head" in g: g["head"] = (g["head"][0] + dx, g["head"][1])
            if "anchor" in g: g["anchor"] = (g["anchor"][0] + dx, g["anchor"][1])
            if "bucket" in g: bl, br, bb, bt = g["bucket"]; g["bucket"] = (bl + dx, br + dx, bb, bt)
            if "cl" in g: g["cl"] = g["cl"] + dx
            self.gadgets.append(g)
        self.rooms += 1

    # ── pieces (all take the VISIBLE bottom; return the visible top) ─────────────────────────────────────
    def _piece(self, kind, cx, bottom, vis_w, vis_h):
        self._rpiece(kind, cx, bottom + vis_h / 2, vis_w, vis_h, 0.0)
        return bottom + vis_h

    def _rpiece(self, kind, vcx, vcy, vis_w, vis_h, rot):
        """A piece by its VISIBLE centre / size, tilted rot degrees counter-clockwise about that centre (2026-11-11)."""
        x0, x1, y0, y1 = ART_W[self.world][kind]
        w, h = vis_w / (x1 - x0), vis_h / (y1 - y0)
        ox, oy = ((x0 + x1) / 2 - 0.5) * w, ((y0 + y1) / 2 - 0.5) * h   # visible centre, relative to the rect centre
        c, s_ = math.cos(math.radians(rot)), math.sin(math.radians(rot))
        fx, fy = vcx - (c * ox - s_ * oy), vcy - (s_ * ox + c * oy)
        poly = _rect_poly(vcx, vcy, vis_w, vis_h, rot)
        xs, ys = [p[0] for p in poly], [p[1] for p in poly]
        self.blocks.append(dict(kind=kind, x=fx, y=fy, w=w, h=h, rot=rot, poly=poly, vc=(vcx, vcy), vs=(vis_w, vis_h),
                                vis=(min(xs), max(xs), min(ys), max(ys))))

    def beam(self, ax, ay, bx, by, stone=False, thick=None):
        """A plank whose LOWER face runs from (ax, ay) to (bx, by) — leaning planks, braces, ramps (2026-11-11)."""
        t = thick or (STONE_T if stone else WOOD_T)
        length = math.hypot(bx - ax, by - ay)
        rot = math.degrees(math.atan2(by - ay, bx - ax))
        nx, ny = -math.sin(math.radians(rot)), math.cos(math.radians(rot))   # the face's upward normal
        if ny < 0: nx, ny = -nx, -ny
        self._rpiece("slab" if stone else "plank", (ax + bx) / 2 + nx * t / 2, (ay + by) / 2 + ny * t / 2, length, t, rot)

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
        inset = INSET_W[self.world][1 if stone else 0]   # half the post width
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
        names = {2: KIND_W2, 3: KIND_W3, 4: KIND_W4, 5: KIND_W5, 6: KIND_W6, 7: KIND_W7, 8: KIND_W8, 9: KIND_W9}.get(self.world, {})
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
    def cs_items(self):
        """The B(...) / R(...) / P(...) / Gd(...) lines, without the surrounding Make call."""
        f = lambda v: f"{v:.3f}f"
        bl, rl, pl, gl = [], [], [], []
        for b in self.blocks:
            t, v, extra = CS[b["kind"]]
            art = f", artVariant: WoodArtVariant.{v}" if v else ""
            rot = f", rot: {f(b['rot'])}" if abs(b.get("rot", 0.0)) > 0.01 else ""
            rm = f", room: {b['room']}" if b.get("room") else ""
            bl.append(f"B(BlockType.{t}, {f(b['x'])}, {f(b['y'])}, {f(b['w'])}, {f(b['h'])}{art}{extra}{rot}{rm})")
        for r in self.robots:
            rm = f", room: {r['room']}" if r.get("room") else ""
            rl.append(f"R({f(r['x'])}, {f(r['y'])}, {f(r['s'])}, {f(r['s'])}, RobotType.{r['type']}{rm})")
        for i in self.islands:
            pl.append(f"P({f(i['x'])}, {f(i['s'])}, {f(i['w'])}, variant: {i['v']}" + (", rolling: true" if i.get("rolling") else "")
                      + (f", height: {f(i['h'])}" if i.get("stretch") else "") + (f", room: {i['room']}" if i.get("room") else "") + ")")
        for g in self.gadgets:
            extra = f", ropeLength: {f(g['rope'])}" if g["type"] == "HayHoist" else ""
            extra += ", faceLeft: true" if g["left"] else ""
            extra += f", room: {g['room']}" if g.get("room") else ""
            gl.append(f"Gd(GadgetType.{g['type']}, {f(g['x'])}, {f(g['y'])}, {f(g['size'])}{extra})")
        return bl, rl, pl, gl

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
            rot = f", rot: {f(b['rot'])}" if abs(b.get("rot", 0.0)) > 0.01 else ""
            rm = f", room: {b['room']}" if b.get("room") else ""
            out.append(f"                B(BlockType.{t}, {f(b['x'])}, {f(b['y'])}, {f(b['w'])}, {f(b['h'])}{art}{extra}{rot}{rm}),")
        out += ["            },", "            robots: new[]", "            {"]
        for r in self.robots:
            rm = f", room: {r['room']}" if r.get("room") else ""
            out.append(f"                R({f(r['x'])}, {f(r['y'])}, {f(r['s'])}, {f(r['s'])}, RobotType.{r['type']}{rm}),")
        out.append("            },")
        if self.islands:
            out += ["            platforms: new[]", "            {"]
            out += [f"                P({f(i['x'])}, {f(i['s'])}, {f(i['w'])}, variant: {i['v']}"
                    + (", rolling: true" if i.get("rolling") else "")
                    + (f", height: {f(i['h'])}" if i.get("stretch") else "")
                    + (f", room: {i['room']}" if i.get("room") else "") + ")," for i in self.islands]
            out.append("            },")
        if self.gadgets:
            out += ["            gadgets: new[]", "            {"]
            for g in self.gadgets:
                extra = f", ropeLength: {f(g['rope'])}" if g["type"] == "HayHoist" else ""
                extra += ", faceLeft: true" if g["left"] else ""
                extra += f", room: {g['room']}" if g.get("room") else ""
                out.append(f"                Gd(GadgetType.{g['type']}, {f(g['x'])}, {f(g['y'])}, {f(g['size'])}{extra}),")
            out.append("            },")
        if self.weather != "Golden":
            out.append(f"            weather: LevelWeather.{self.weather},")
        if self.world != 1:
            out.append(f"            world: {self.world},")
        if self.boss:
            out.append("            isBoss: true,")
        if self.rooms > 1:
            out.append(f"            rooms: {self.rooms},")
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


def _rng(L, *key):
    import random, zlib
    return random.Random(zlib.crc32(("%s|" % L.id + "|".join("%.2f" % k if isinstance(k, float) else str(k) for k in key)).encode()))


def brace(L, xa, xb, b, h, flip=False, thick=0.18):
    """A diagonal brace standing in a bay between two posts (inner faces xa..xb, floor b, height h): one corner rests
    on the floor, the rest clears the posts and the roof — the AB lattice look."""
    W, H, m = xb - xa - 0.08, h - 0.04, 0.0
    best = None
    for deg in range(20, 75):
        th = math.radians(deg)
        lb = min((W - thick * math.sin(th)) / math.cos(th), (H - thick * math.cos(th)) / math.sin(th))
        if lb > 0.3 and (best is None or lb > best[1]): best = (deg, lb)
    if best is None: return
    deg, lb = best
    th = math.radians(deg)
    ext_h = lb * math.sin(th) + thick * math.cos(th)
    L._rpiece("plank", (xa + xb) / 2, b + ext_h / 2 + 0.005, lb, thick, -deg if flip else deg)


def tower(L, xc, base, floors, width=1.9, h=1.15, taper=0.12, stone_floors=0, robots=None, top=None, wonky=True, stilts=True, room=None):
    """A multi-storey timber tower (posts + floor planks), narrowing as it rises. robots: kind per floor or None.

    wonky (2026-11-11, user: "structures dont follow real world physics - they utilise singular, angles, etc etc to make
    strange and easily toppled structures... robots dont know how to build structures"): each storey is built a little
    wrong, seeded per level so it is stable from run to run: storeys drift sideways onto lopsided overhanging roofs,
    empty storeys stand on a single post (a T), carry a diagonal brace, stand on stacked-crate legs or on tall stilts.
    Nothing has to balance — live blocks start asleep, so it stands until it is hit, then everything goes.
    room: (left x, right x) the roofs' overhangs must stay inside (a neighbouring structure); None = free."""
    rng = _rng(L, "tower", xc, base)
    pw = POST_W[L.world]
    b, w, c = base, width, xc
    sa, sb = None, None                      # the span the next storey must stand inside (the roof below)
    for i in range(floors):
        rk = robots[i] if robots and i < len(robots) else None
        stone = i < stone_floors
        if sa is not None:                   # drift within the roof below (onto its overhang, if any)
            lo, hi = sa + w / 2 + 0.02, sb - w / 2 - 0.02
            if wonky and hi > lo: c = rng.uniform(lo, hi) if rng.random() < 0.75 else min(max(c, lo), hi)
            elif hi > lo: c = min(max(c, lo), hi)
            else: c = (sa + sb) / 2
        xa, xb = c - w / 2, c + w / 2
        style = "hut"
        if wonky and not stone:
            if rk is None and i > 0:
                style = rng.choices(["hut", "tee", "brace", "stilts", "crates"], [2, 3, 3, 1.5 if stilts else 0, 1.5])[0]
            elif rk is None:
                style = rng.choices(["hut", "brace", "crates"], [2, 3, 2])[0]
            elif w >= 1.6:
                style = rng.choices(["hut", "crates"], [3, 1])[0]
        ov_l = ov_r = 0.0
        if wonky and rng.random() < 0.65:    # a lopsided roof that sticks out on one side
            if rng.random() < 0.5: ov_l = rng.uniform(0.3, 0.85)
            else: ov_r = rng.uniform(0.3, 0.85)
        if rk: L.robot(c, b, rk)
        inset = INSET_W[L.world][1 if stone else 0]
        if style == "tee":
            px = c + rng.uniform(-0.18, 0.18) * w
            if sa is not None: px = min(max(px, sa + pw), sb - pw)
            t = L.post(px, b, h)
        elif style == "crates":
            n = max(2, round(h / 0.5)); cs_ = h / n
            for lx in (xa + cs_ / 2 + 0.02, xb - cs_ / 2 - 0.02):
                tt = b
                for _ in range(n): tt = L.box(lx, tt, cs_, stone=False)
            t = tt
        else:
            hh = h * (1.45 if style == "stilts" else 1.0)
            t = L.post(xa + inset, b, hh, stone)
            L.post(xb - inset, b, hh, stone)
            if style == "brace": brace(L, xa + 2 * inset, xb - 2 * inset, b, hh, flip=rng.random() < 0.5)
        sa, sb = xa - ov_l, xb + ov_r
        if room:
            sa, sb = max(sa, min(xa, room[0])), min(sb, max(xb, room[1]))
        b = L.plank(sa, sb, t, stone)
        w = max(1.1, w - taper)
    L.last_top_x = c
    if top:
        L.robot(c, b, top)
    return b


def aframe(L, x, b, h=1.1, half=0.75, robot=None):
    """A tent of two planks leaning on each other (nearly touching at the apex) with a robot sheltering inside."""
    if robot: L.robot(x, b, robot)
    L.beam(x - half, b, x - 0.03, b + h)
    L.beam(x + 0.03, b + h, x + half, b)


def lean(L, foot_x, b, top_x, top_y):
    """A plank leaning from the floor against something: lower face from (foot_x, b) to (top_x, top_y)."""
    L.beam(foot_x, b, top_x, top_y)


def upside_pyramid(L, x, b, rows=(1, 2, 3), s=0.62, stone=False, robots=None, gap=0.06):
    """An upside-down pyramid of crates (or stone boxes): each row wider than the one under it, every piece only just
    touching the one below. robots: kinds stood on the top row (left to right). Returns the top."""
    t = b
    for n in rows:
        for k in range(n):
            L.box(x + (k - (n - 1) / 2) * (s + gap), t, s, stone=stone)
        t += s
    if robots:
        n = rows[-1]
        for k, rk in enumerate(robots[:n]):
            if rk: L.robot(x + (k - (n - 1) / 2) * (s + gap), t, rk)
    return t


def wobble_stack(L, x, b, n=4, s=0.62, lean_by=0.22, stone=False, top=None):
    """Crates stacked off-centre, each step further over — a leaning tower held up by nothing but luck."""
    t, cx = b, x
    for k in range(n):
        t = L.box(cx, t, s, stone=stone)
        cx += lean_by * s * (1 if k % 3 != 2 else -0.6)
    if top: L.robot(cx - lean_by * s, t, top)
    return t


def stilt_house(L, x, b, legs_h=2.4, w=1.7, robot=None, top=None, room_h=1.05):
    """A hut perched on two tall skinny stilts and a deck much wider than them — top-heavy."""
    t = L.post(x - 0.32, b, legs_h); L.post(x + 0.32, b, legs_h)
    d = L.plank(x - w / 2 - 0.3, x + w / 2 + 0.3, t)
    if robot: L.robot(x, d, robot)
    r = L.hut(x - w / 2, x + w / 2, d, room_h)
    if top: L.robot(x, r, top)
    return r


def cantilever(L, post_x, b, h, left, right, tip=None, stone=False):
    """One post, a plank stuck on it lopsided (left / right lengths from the post); tip: what sits on the long end
    (robot kind, 'barrel', 'crate', 'rock'). Returns (plank top, tip x)."""
    t = L.post(post_x, b, h, stone)
    t = L.plank(post_x - left, post_x + right, t, stone)
    tx = post_x + right - 0.4 if right >= left else post_x - left + 0.4
    if tip == "barrel": L.barrel(tx, t)
    elif tip == "crate": L.box(tx, t, 0.55, stone=False)
    elif tip == "rock": L.box(tx, t, 0.6, stone=True)
    elif tip: L.robot(tx, t, tip)
    return t, tx


def balanced_rock(L, x, b, h=1.6, s=1.0, top="pawn"):
    """A big stone block balanced on one thin post, a robot on top."""
    t = L.post(x, b, h)
    t = L.box(x, t, s, stone=True)
    if top: L.robot(x, t, top)
    return t


def barrel_deck(L, xa, xb, b, n=3):
    """A plank deck standing on dynamite barrels instead of legs. Returns the deck top."""
    t = b
    for k in range(n):
        t = L.barrel(xa + 0.35 + k * (xb - xa - 0.7) / max(1, n - 1), b)
    return L.plank(xa, xb, t)


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
# 2026-11-11 v5 (user: "review the AG videos again - structures dont follow real world physics - they utilise singular,
# angles, etc etc to make strange and easily toppled structures - do the same, be creative and inventive (dont follow
# physics) - playful - 'robots dont know how to build structures'"): towers are built wonky (see tower()), and every
# level gets AB-style set pieces: tents of leaning planks, upside-down pyramids, leaning crate stacks, huts on stilts,
# lopsided cantilevers, rocks balanced on one post, decks standing on dynamite. Nothing has to balance: it all starts
# asleep, stands until it is hit, then goes.
L = level("L02_StoneWall", "W1_L02", "Harvest Yard", 2, [C, C, C], """
Warm-up: a crooked three-storey shed, a robot hiding in a tent of two leaning planks, a see-saw beam with a robot
balanced on one end, and a sunflower in front: hit it and it sprays seeds into the shed.""")
tower(L, 8.0, G, 3, width=1.8, robots=["semi", None, "pawn"], top="pawn")
aframe(L, 10.4, G, h=1.2, half=0.8, robot="pawn")
seesaw(L, 12.3, G, 1.0, 2.2, left="crate", right="pawn")
L.sunflower(5.9, G)

L = level("L03_TheTower", "W1_L03", "The Tower", 2, [C, C, C], """
A six-storey tower the robots built themselves: crate legs, a storey on one post, stilts, lopsided roofs. Next to it a
stack of crates leans further over with every crate, a robot on top. Hit either low and it all comes down.""")
tower(L, 9.6, G, 6, width=2.1, h=1.2, taper=0.16, robots=["semi", None, "pawn", None, "pawn", None], top="semi")
wobble_stack(L, 7.0, G, n=4, top="pawn")

L = level("L04_EggPractice", "W1_L04", "Egg Practice", 2, [C, C, C], """
A raised island: a hut perched on two skinny stilts, an upside-down pyramid of crates with robots on top, a hut,
and a plank stuck lopsided on one post with a robot out on its far end.""")
L.weather = "MorningMist"
s = L.island(9.8, G + 1.8, 6.6, 0)
stilt_house(L, 7.5, s, legs_h=1.5, w=1.3, robot="pawn", top="pawn")
upside_pyramid(L, 9.65, s, rows=(1, 2, 3), robots=["pawn", None, "pawn"])
r = L.hut(10.85, 11.85, s, 1.05); L.robot(11.35, s, "pawn")
cantilever(L, 12.45, s, 1.0, 0.3, 1.05, tip="pawn")

L = level("L05_TheGauntlet", "W1_L05", "Runaway Wagon", 2, [C, C, C], """
A loaded hay wagon is parked at the top of a ramp. Knock it and it rolls down the slope straight into the crooked
five-storey tower at the bottom.""")
deck, dl, dr, end = ramp_wagon(L, 6.4)
L.box(dl + 0.35, deck, 0.5, stone=False); L.robot(dl + 1.05, deck, "pawn")
tower(L, end + 1.25, G, 5, width=2.0, taper=0.15, robots=["semi", None, "pawn", None, "semi"], top="pawn")

L = level("L06_DoubleBarrel", "W1_L06", "Double Barrel", 2, [C, C, C], """
A farmhouse built on a deck that stands on four dynamite barrels instead of legs: set one off and the whole house
drops. A hay hoist on the right dangles a bucket over a robot by the door; a sunflower stands at the front.""")
L.weather = "Overcast"
d = barrel_deck(L, 7.0, 10.8, G, n=4)
r = L.hut(7.3, 10.5, d, 1.15); L.robot(8.2, d, "semi"); L.robot(9.6, d, "semi")
tower(L, 8.9, r, 2, width=1.9, h=1.05, taper=0.2, robots=["pawn", None], top="pawn")
L.robot(11.4, G, "pawn")
L.hoist(14.1, G, 4.0, 0.3)
L.sunflower(6.0, G)

L = level("L07_BarrelRow", "W1_L07", "Barrel Row", 2, [C, C, C], """
A wagon carrying dynamite is parked on the edge of a high island. Knock it off and it drops onto the stone bridge
over a row of barrels, a robot on the bridge and a crooked lookout tower at the end.""")
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
Rain: a stone ground floor with two rooms and a crooked four-storey timber tower rising from it, a plank leaning on
its wall like a ramp, and a robot sheltering in a plank tent at the back. A sunflower stands guard at the gate.""")
L.weather = "Rain"
for x in (8.2, 10.0, 11.8):
    top = L.post(x, G, 1.4, stone=True)
L.plank(8.025, 10.0, top, stone=True); f1 = L.plank(10.0, 11.975, top, stone=True)
L.robot(9.1, G, "semi"); L.robot(10.9, G, "semi")
tower(L, 10.0, f1, 4, width=2.6, taper=0.35, robots=["pawn", None, "semi", None], top="pawn")
lean(L, 6.9, G, 7.98, G + 1.25)
aframe(L, 13.0, G, h=1.2, half=0.8, robot="pawn")
L.sunflower(6.0, G)

L = level("L09_StoneBastion", "W1_L09", "Downhill Demolition", 3, [C, C, C], """
A wagon loaded with dynamite waits at the top of a ramp. Send it rolling into the stone bunker at the bottom, which
carries a lopsided timber house, a plank cantilevered off its roof with a barrel on the end, and a lookout.""")
deck, dl, dr, end = ramp_wagon(L, 6.2)
L.barrel(dl + 0.45, deck); L.barrel(dl + 1.15, deck)
x0 = end + 0.4
t = L.box(x0 + 0.4, G, 0.8); L.box(x0 + 0.4, t, 0.8)
t = L.box(x0 + 3.2, G, 0.8); top = L.box(x0 + 3.2, t, 0.8)
r = L.plank(x0 - 0.05, x0 + 3.65, top, stone=True)
L.robot(x0 + 1.8, G, "harv")
r2 = L.hut(x0 + 0.9, x0 + 2.7, r, 1.15); L.robot(x0 + 1.8, r, "pawn")
cantilever(L, x0 + 3.3, r, 0.9, 0.3, 1.0, tip="barrel")
tower(L, x0 + 1.8, r2, 2, width=1.5, h=1.05, robots=["semi", None], top="pawn")

L = level("L10_BessiesDebut", "W1_L10", "Bessie's Debut", 2, [C, C, B], """
Bessie's first level: an upside-down stone pyramid (the robots built it wrong way up) carrying a two-storey lookout,
robots sheltering under its overhangs, and two see-saw beams.""")
L.weather = "MorningMist"
top = upside_pyramid(L, 9.6, G, rows=(1, 2, 3, 4), s=0.66, stone=True)
L.robot(8.2, G, "pawn"); L.robot(11.0, G, "pawn")
tower(L, 9.6, top, 2, width=1.9, h=1.1, taper=0.2, robots=["semi", "pawn"], top="pawn")
seesaw(L, 6.6, G, 0.9, 2.2, left="crate", right="pawn")
seesaw(L, 12.9, G, 0.9, 2.2, left="pawn", right="barrel")

L = level("L11_FullRoster", "W1_L11", "Mesa Drop", 3, [C, C, B], """
A wagon with a robot aboard is parked on top of a tall rock mesa. Tip it over the edge and it slides down the
mesa's side into the crooked five-storey tower below; on a low island behind, crates lean over a robot.""")
L.weather = "Dusk"
s, l, r = L.mount("Mesa", 7.2, 3.6)
deck, dl, dr = edge_wagon(L, s, r, 2.0)
L.box(dl + 0.3, deck, 0.5, stone=False); L.robot(dl + 0.95, deck, "pawn")
tower(L, 10.4, G, 5, width=2.0, taper=0.15, robots=["semi", None, "pawn", None, "semi"], top="pawn")
s2 = L.island(12.9, G + 1.2, 2.4, 0)
wobble_stack(L, 12.5, s2, n=4, lean_by=0.25, top="pawn")
L.robot(13.6, s2, "semi")

L = level("L12_HeavyGuard", "W1_L12", "Heavy Guard", 2, [C, C, B], """
A Harvester inside a stone keep with a crooked watch-house on its roof and a see-saw on top balancing a pawn against
a barrel; a big rock balanced on one post with a robot on it. High on an island to the left a wagon waits at the
edge, right above the dynamite barrel at the keep's door.""")
L.weather = "Rain"
top = L.post(8.6, G, 1.6, stone=True); L.post(11.0, G, 1.6, stone=True)
r = L.plank(8.4, 11.2, top, stone=True)
L.robot(9.8, G, "harv"); L.barrel(7.8, G)
t = tower(L, 9.8, r, 2, width=2.2, h=1.05, taper=0.3, robots=["pawn", None])
seesaw(L, 9.8, t, 0.7, 2.4, left="pawn", right="barrel")
balanced_rock(L, 12.7, G, h=1.5, s=0.9, top="pawn")
hi = L.island(6.4, G + 4.2, 2.8, 0)
deck, dl, dr = edge_wagon(L, hi, 6.4 + 1.4 * 0.92, 2.0)
L.box(dl + 0.35, deck, 0.5, stone=False); L.robot(dl + 1.0, deck, "pawn")

L = level("L13_TenStrong", "W1_L13", "Hay Ride", 3, [C, C, B], """
A hay wagon with a robot riding on top waits on the ramp; below it stands a crooked five-storey tower on haybale
stilts.""")
deck, dl, dr, end = ramp_wagon(L, 6.0)
t = L.hay(dl + 0.55, deck); L.robot(dl + 1.3, deck, "pawn")
x0 = end + 1.6
for x in (x0 - 0.95, x0, x0 + 0.95):
    t = L.hay(x, G)
f = L.plank(x0 - 1.5, x0 + 1.5, t)
tower(L, x0, f, 5, width=2.2, h=1.1, taper=0.2, robots=["semi", "pawn", None, "semi", None], top="pawn")

L = level("L14_StoneAndPawns", "W1_L14", "Stone And Pawns", 3, [C, B, B], """
Three stacks of stone blocks on a low island, each one leaning further than the last, a robot on every top, and a
hut on stilts at the end.""")
L.weather = "Overcast"
s = L.island(9.8, G + 1.0, 5.6, 1)
wobble_stack(L, 7.6, s, n=2, s=0.7, lean_by=0.2, stone=True, top="pawn")
wobble_stack(L, 8.75, s, n=3, s=0.7, lean_by=0.2, stone=True, top="pawn")
wobble_stack(L, 9.9, s, n=4, s=0.7, lean_by=0.2, stone=True, top="semi")
stilt_house(L, 11.8, s, legs_h=1.4, w=1.2, robot="pawn", top="pawn")

L = level("L15_FiveStones", "W1_L15", "Five Stones", 2, [C, B, C], """
On a high island: two rocks balanced on single posts with robots on top, a crooked three-storey timber house on a
row of stone pillars, and a Harvester guarding the edge.""")
L.weather = "MorningMist"
s = L.island(10.4, G + 2.4, 6.4, 0)
balanced_rock(L, 8.7, s, h=1.1, s=0.8, top="pawn")
for x in (10.2, 11.2, 12.2):
    top = L.post(x, s, 1.0, stone=True)
r = L.plank(9.95, 12.45, top, stone=True)
tower(L, 11.2, r, 3, width=2.2, h=1.1, taper=0.3, robots=["semi", None, "pawn"], top="semi")
balanced_rock(L, 13.2, s, h=0.9, s=0.7, top="pawn")
L.robot(7.7, s, "harv")

L = level("L16_TwinTowers", "W1_L16", "Twin Towers", 3, [C, C, B], """
Two crooked four-storey timber towers on scaffolds joined by a plank bridge at the top, robots inside and one on the
bridge. A sunflower stands in front of the left tower.""")
L.weather = "Rain"
cs = []
for xc in (8.0, 11.9):
    s_, l, r = L.mount("Scaffold", xc, 2.4)
    c = (l + r) / 2
    cs.append(c)
    top = tower(L, c, s_, 4, width=1.8, h=1.05, taper=0.12, robots=["semi", None, "pawn", None], stilts=False)
br = L.plank(cs[0] + 0.3, cs[1] - 0.3, top)
L.robot((cs[0] + cs[1]) / 2, br, "semi")
aframe(L, (cs[0] + cs[1]) / 2, G, h=1.2, half=0.8, robot="pawn")
L.sunflower(6.3, G)

L = level("L17_EightStrong", "W1_L17", "Wagon Train", 3, [C, C, B], """
A wagon carrying a little hut rolls off the ramp into a stone-and-timber fortress: a stone hall with a crooked
four-storey timber tower above; dynamite waits by the walls.""")
L.weather = "Dusk"
deck, dl, dr, end = ramp_wagon(L, 5.8)
r = L.hut(dl + 0.15, dl + 1.25, deck, 0.9); L.robot(dl + 0.7, deck, "pawn")
x0 = end + 0.3
L.barrel(x0 + 0.3, G)
t = L.box(x0 + 1.1, G, 0.8); t = L.box(x0 + 1.1, t, 0.8); L.robot(x0 + 1.1, t, "pawn")
f = L.hut(x0 + 2.0, x0 + 4.2, G, 1.3, stone=True, roof_stone=True); L.robot(x0 + 3.1, G, "semi")
tower(L, x0 + 3.1, f, 4, width=1.9, h=1.05, taper=0.12, robots=["pawn", None, "semi", None], top="harv")

L = level("L18_CaptainsLastStand", "W1_L18", "Captain's Last Stand", 3, [C, B, B], """
Boss, in a thunderstorm: the Commander stands on top of a crooked stone-and-timber keep on a high island; a guard
watches from a leaning stack of stone below.""", boss=True)
L.weather = "Rain"
s = L.island(10.8, G + 2.0, 5.4, 1)
top = L.post(9.4, s, 1.5, stone=True); L.post(12.2, s, 1.5, stone=True)
r = L.plank(9.1, 12.5, top, stone=True)
L.robot(10.8, s, "semi"); L.barrel(8.6, s); L.barrel(13.0, s)
t = tower(L, 10.8, r, 3, width=2.4, h=1.1, taper=0.2, robots=["pawn", None, "semi"])
L.robot(L.last_top_x, t, "boss")
wobble_stack(L, 6.4, G, n=4, s=0.8, lean_by=0.15, stone=True, top="pawn")


# ═══════════════════════════════════════════ checks ═══════════════════════════════════════════════════════
LAUNCH = (-2.62, -2.69)          # barrel tip with the haybale mount (CatapultLauncher.ApplyMount)
# Raised launch points (2026-11-12): World 4's floating mount (CatapultLauncher.SkyMountShift) and World 1's taller bale
# pyramid (two more rows, 1.6 u, + FarmMountShift's 1.2 u to the left); Worlds 2 and 3 got the same pyramid.
LAUNCH_SHIFT = {w: (-1.2, 1.6) for w in range(1, 10)}   # Worlds 1-9: the raised cannon (pyramid / floating)
ROOM_STRIDE = 32.0               # LevelData.RoomStride: room r is laid out (and the cannon moves) r * this to the right
GRAV, VMIN, VMAX = 8.0, 5.6, 13.6


def check(L, reach_world=None):
    rw = L.world if reach_world is None else reach_world
    issues = []
    # support (2026-11-11, polygons so tilted pieces count): each block's collider, dropped 0.1 u, must touch the
    # ground, a mount's solid or a block whose centre is lower. Balance is NOT checked on purpose: structures start
    # asleep and only need to touch ("robots don't know how to build" — precarious is the point).
    solids = []
    for isl in L.islands:
        base = isl["s"] - isl["uv"] * isl["h"]
        for (x0, x1, y0, y1) in isl["solids"]:
            sl, sr = isl["x"] - isl["w"] / 2 + x0 * isl["w"], isl["x"] - isl["w"] / 2 + x1 * isl["w"]
            solids.append([(sl, base + y0 * isl["h"]), (sr, base + y0 * isl["h"]), (sr, base + y1 * isl["h"]), (sl, base + y1 * isl["h"])])
    cols = [_shrink(b["poly"], 0.02) for b in L.blocks]
    for i, b in enumerate(L.blocks):
        low = [(x, y - 0.1) for x, y in cols[i]]
        ok = min(y for _, y in low) <= G + 0.001
        ok = ok or any(_sat_overlap(low, sp) > 0 for sp in solids)
        ok = ok or any(j != i and L.blocks[j]["vc"][1] < b["vc"][1] and _sat_overlap(low, cols[j]) > 0 for j in range(len(cols)))
        if not ok: issues.append(f"block {i} ({b['kind']}) unsupported at x={b['vc'][0]:.2f} y={b['vis'][2]:.2f}")
    # overlap: colliders that start inside each other burst apart the moment the stack wakes
    for i in range(len(cols)):
        for j in range(i + 1, len(cols)):
            d = _sat_overlap(cols[i], cols[j])
            if d > 0.004:
                issues.append(f"blocks {i} ({L.blocks[i]['kind']}) and {j} ({L.blocks[j]['kind']}) overlap {d:.3f} at x={L.blocks[i]['vc'][0]:.2f}")
    # robots: standing on something, not inside a block
    for k, rb in enumerate(L.robots):
        w = 0.5 * rb["vis"]; bot = rb["surface"]
        box = [(rb["x"] - w / 2, bot + 0.03), (rb["x"] + w / 2, bot + 0.03), (rb["x"] + w / 2, bot + 0.85 * rb["vis"]), (rb["x"] - w / 2, bot + 0.85 * rb["vis"])]
        for j, c in enumerate(cols):
            if _sat_overlap(box, c) > 0.03: issues.append(f"robot {k} at x={rb['x']:.2f} inside block {j} ({L.blocks[j]['kind']})")
        foot = [(rb["x"] - 0.15, bot - 0.1), (rb["x"] + 0.15, bot - 0.1), (rb["x"] + 0.15, bot + 0.02), (rb["x"] - 0.15, bot + 0.02)]
        if not (bot <= G + 0.01 or any(_sat_overlap(foot, c) > 0 for c in cols) or any(_sat_overlap(foot, sp) > 0 for sp in solids)):
            issues.append(f"robot {k} at x={rb['x']:.2f} y={bot:.2f} standing on nothing")
    # reach: some angle/power passes within 0.45 u of each robot (obstacles ignored)
    for k, rb in enumerate(L.robots):
        rx = rb["x"] - rb.get("room", 0) * ROOM_STRIDE   # in its own room's frame
        if not reachable(rx, rb["y"], world=rw): issues.append(f"robot {k} at ({rb['x']:.2f},{rb['y']:.2f}) out of reach")
        if rx > 16.5: issues.append(f"robot {k} off-screen at 16:9")
        if rb["y"] + rb["vis"] / 2 > G + 12.0: issues.append(f"robot {k} under the HUD (top {rb['y'] + rb['vis'] / 2 - G:.1f} u up)")
    # contraptions: reachable; a hoist's bucket hangs clear of everything and has a target below it
    for g in L.gadgets:
        gdx = g.get("room", 0) * ROOM_STRIDE
        if g["type"] in ("Sunflower", "IceCrystal", "IceBoulder", "WaterPump", "Millstone", "StormCloud", "ThunderWheel", "Clam", "ShipsWheel", "LaserEmitter", "GravityRing",
                         "Cactus", "Tumbleweed", "PopcornCart", "Pumpkin", "CornHopper", "Cog"):
            if not reachable(g["head"][0] - gdx, g["head"][1], world=rw): issues.append(f"{g['type']} at x={g['x']:.2f} out of reach")
            continue
        bl, br, bb, bt = g["bucket"]
        if not reachable((bl + br) / 2 - gdx, (bb + bt) / 2, world=rw): issues.append(f"hoist bucket at x={(bl + br) / 2:.2f} out of reach")
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


def reachable(tx, ty, tol=0.45, world=1):
    dx, dy = LAUNCH_SHIFT.get(world, (0.0, 0.0))
    lx, ly = LAUNCH[0] + dx, LAUNCH[1] + dy
    for ang in range(20, 81, 2):
        for v in [VMIN + (VMAX - VMIN) * q / 20 for q in range(21)]:
            vx, vy = v * math.cos(math.radians(ang)), v * math.sin(math.radians(ang))
            t = 0
            while t < 4:
                x, y = lx + vx * t, ly + vy * t - GRAV * t * t / 2
                if (x - tx) ** 2 + (y - ty) ** 2 < tol * tol: return True
                if y < G - 1: break
                t += 0.01
    return False


# ═══════════════════════════════════════════ second rooms ════════════════════════════════════════════════════
# 2026-11-12 (user: "lets return to first world - apply the same logic as we did for W4 ... introduce the panning and
# room 2 to all the levels"): every World 1 level gets a second farmyard one room to the right (LevelData rooms, see
# CLAUDE.md). Room 1 stays exactly as designed above. Room 2 is generated: 2-4 of the set pieces above spread across
# the yard (in later levels some on a mesa or a floating island), robots last; the first seed that passes every check
# (support, overlaps, robots, reach from the raised cannon, under the HUD) is kept, so it is stable from run to run.
FARM_PIECES = ["tower", "tower", "stilt", "aframe", "pyramid", "wobble", "rock", "cantilever", "seesaw", "deck"]


def _farm_piece(L, kind, x, b, rng, half):
    u = rng.uniform
    if kind == "tower":
        w = u(1.4, max(1.45, min(1.9, 2 * half - 0.3)))
        tower(L, x, b, rng.randint(2, 4), width=w, h=u(0.95, 1.15), taper=0.12,
              robots=[rng.choice([None, "pawn"]), None, rng.choice(["semi", "pawn"]), None],
              top=rng.choice([None, "pawn"]), room=(x - half + 0.05, x + half - 0.05))
    elif kind == "stilt":
        stilt_house(L, x, b, legs_h=u(1.3, 2.3), w=max(1.1, min(1.5, 2 * half - 1.0)), robot="pawn", top=rng.choice([None, "pawn"]))
    elif kind == "aframe":
        aframe(L, x, b, h=u(1.0, 1.25), half=u(0.65, 0.85), robot=rng.choice(["pawn", "semi"]))
    elif kind == "pyramid":
        upside_pyramid(L, x, b, rows=(1, 2, 3), s=0.55, robots=[rng.choice([None, "pawn"]), None, rng.choice([None, "pawn"])])
    elif kind == "wobble":
        wobble_stack(L, x, b, n=rng.randint(3, 5), s=0.62, lean_by=u(0.15, 0.25), stone=rng.random() < 0.3, top="pawn")
    elif kind == "rock":
        balanced_rock(L, x, b, h=u(1.1, 1.8), s=0.9, top=rng.choice(["pawn", "semi"]))
    elif kind == "cantilever":
        cantilever(L, x - 0.35, b, u(0.9, 1.5), 0.45, min(u(1.1, 1.5), half + 0.2), tip=rng.choice(["pawn", "barrel"]))
    elif kind == "seesaw":
        seesaw(L, x, b, u(0.7, 1.1), max(1.8, min(u(2.3, 2.8), 2 * half - 0.1)), left=rng.choice(["pawn", "crate", "barrel"]), right="pawn")
    elif kind == "deck":
        hw = max(0.6, min(0.9, half - 0.1))
        d = barrel_deck(L, x - hw, x + hw, b, n=3)
        L.robot(x, d, rng.choice(["pawn", "semi"]))


def farm_room(L, rng, pieces, mounts=0.0, x0=6.0, x1=15.2):
    pitch = (x1 - x0) / pieces
    for i in range(pieces):
        x = x0 + pitch * (i + 0.5) + rng.uniform(-0.2, 0.2)
        b, half = G, pitch / 2
        r = rng.random()
        if r < mounts * 0.5:
            b, l_, r_ = L.mount("Mesa", x, 1.9); x = (l_ + r_) / 2; half = min(half, (r_ - l_) / 2 + 0.4)
        elif r < mounts:
            w = min(2 * half - 0.2, rng.uniform(2.6, 3.2))
            b = L.island(x, G + rng.uniform(1.4, 2.4), w, rng.randint(0, 1)); half = min(half, w * 0.42)
        _farm_piece(L, rng.choice(FARM_PIECES), x, b, rng, half)


def settle_robots(L, rng, lo, hi):
    """Robots go in last and nothing rests on them: drop any whose in-game 0.6 x 0.9 collider would start inside wood
    (it shoves the structure apart when it switches on after the reveal pop), trim at random to the range."""
    cols = [_shrink(b["poly"], 0.02) for b in L.blocks]
    keep, boxes = [], []
    for r in L.robots:
        bot = r["surface"]
        box = [(r["x"] - 0.31, bot + 0.02), (r["x"] + 0.31, bot + 0.02), (r["x"] + 0.31, bot + 0.93), (r["x"] - 0.31, bot + 0.93)]
        clear = not any(_sat_overlap(box, c) > 0.0 for c in cols) and not any(_sat_overlap(box, o) > 0.0 for o in boxes)
        if r["kind"] == "boss" or clear: keep.append(r); boxes.append(box)
    boss = [r for r in keep if r["kind"] == "boss"]
    rest = [r for r in keep if r["kind"] != "boss"]
    while len(rest) > hi: rest.pop(rng.randrange(len(rest)))
    L.robots = rest + boss
    return len(rest) >= lo


def room2_for(k, like, skip=0):
    """Level k's second room, in the world and weather of `like` (World 2/3 since 2026-11-12, same generator)."""
    import random
    pieces = 2 if k <= 5 else 3 if k <= 12 else 4
    lo, hi = (2, 3) if k <= 5 else (3, 4) if k <= 12 else (3, 5)
    mounts = 0.0 if k <= 4 else 0.25 if k <= 10 else 0.4
    for seed in range(1500):
        R = Level("room2", like.id, like.name, like.par, like.birds, "", world=like.world)
        R.weather = like.weather
        rng = random.Random(200000 + (like.world - 1) * 50000 + k * 1000 + seed)
        farm_room(R, rng, pieces, mounts)
        if not settle_robots(R, rng, lo, hi): continue
        if check(R): continue
        if any(b["vis"][3] > G + 9.5 for b in R.blocks): continue
        if skip > 0: skip -= 1; continue
        R.seed = seed
        return R
    raise RuntimeError(f"W{like.world}_L{k:02d}: no valid second room in 1500 seeds")


def add_second_rooms(levels, first_k, skips=None):
    """Gives every level in `levels` (numbered from first_k) its generated second room, in place. A boss level gets a
    new outer yard first and its keep (exactly as designed) as room 2, where the mothership drops the boss off."""
    skips = skips or {}
    for k, L in enumerate(list(levels), start=first_k):
        if L.boss:
            yard = room2_for(k, L, skips.get(k, 0))
            yard.file, yard.id, yard.name, yard.par, yard.birds, yard.boss = L.file, L.id, L.name, L.par, L.birds, True
            yard.comment = L.comment + "\nTwo rooms: an outer yard first, then the keep."
            yard.add_room(L)
            levels[levels.index(L)] = yard
        else:
            L.add_room(room2_for(k, L, skips.get(k, 0)))


ROOM2_SKIP = {}   # level number -> valid rooms to pass over (one that fell by itself in Unity's PhysicsProbe)
add_second_rooms(LEVELS, 2, ROOM2_SKIP)

# L01 is written by hand in LevelDataGenerator.cs; its second room is generated here and appended there.
L01_ROOM2 = Level("L01", "W1_L01", "First Contact", 2, [C, C, C], "", world=1)
L01_ROOM2.add_room(room2_for(1, L01_ROOM2, ROOM2_SKIP.get(1, 0)))


# ── hidden robots (2026-11-12, user: "there is always a robot that is protected behind the mound, under fallen structure
# that is impossible to reach - we should perhaps not place a robot there") ──────────────────────────────────────────
# reachable() ignores obstacles. A robot standing low behind a mound (a cloud, reef, pedestal, mesa or island) passes it
# while every real shot hits the mound first. prune_hidden_robots() drops every robot that no arc reaches without going
# through a mound's body; structures are untouched. Ramps, wagons and scaffolds (shots pass their legs) are not walls.
WALL_VARIANTS = (0, 1, 2, 3)


def _walls(L, room):
    out = []
    for i in L.islands:
        if i.get("room", 0) != room or i.get("v") not in WALL_VARIANTS: continue
        h = i["h"]; base = i["s"] - i["uv"] * h
        x0 = i["x"] - i["w"] / 2 - room * ROOM_STRIDE
        out.append((x0 + 0.1, x0 + i["w"] - 0.1, base, i["s"] - 0.05))
    return out


def reachable_clear(tx, ty, walls, tol=0.45, world=1):
    dx, dy = LAUNCH_SHIFT.get(world, (0.0, 0.0))
    lx, ly = LAUNCH[0] + dx, LAUNCH[1] + dy
    for ang in range(20, 86, 2):
        for v in [VMIN + (VMAX - VMIN) * q / 20 for q in range(21)]:
            vx, vy = v * math.cos(math.radians(ang)), v * math.sin(math.radians(ang))
            t = 0
            while t < 4:
                x, y = lx + vx * t, ly + vy * t - GRAV * t * t / 2
                if (x - tx) ** 2 + (y - ty) ** 2 < tol * tol: return True
                if y < G - 1 or any(a <= x <= b and lo <= y <= hi for a, b, lo, hi in walls): break
                t += 0.01
    return False


def prune_hidden_robots(L):
    """Drops robots behind a mound that no shot can reach (never a boss, never the last robot of a room). Returns how
    many were dropped."""
    keep, dropped = [], 0
    for r in L.robots:
        room = r.get("room", 0)
        rx = r["x"] - room * ROOM_STRIDE
        walls = [w for w in _walls(L, room)
                 if w[0] < rx + 0.3 and w[3] > r["y"] - 0.6                                  # left of it, above its feet
                 and not (w[0] - 0.2 <= rx <= w[1] + 0.2 and r["surface"] >= w[3] - 0.15)]    # not the mount it stands on
        if r["kind"] == "boss" or not walls or reachable_clear(rx, r["y"], walls, world=L.world):
            keep.append(r)
        else:
            dropped += 1
    for room in range(L.rooms):   # never empty a room
        if not any(r.get("room", 0) == room for r in keep):
            lost = [r for r in L.robots if r.get("room", 0) == room]
            if lost: keep.append(lost[0]); dropped -= 1
    L.robots = [r for r in L.robots if r in keep]
    return dropped


def prune_all(levels):
    n = 0
    for L in levels:
        k = prune_hidden_robots(L)
        if k: print(f"  {L.id}: dropped {k} robot(s) hidden behind a mound")
        n += k
    return n


def write_cs():
    prune_all(LEVELS + [L01_ROOM2])
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

    // L01's second room (2026-11-12): L01 itself is hand-written in LevelDataGenerator.cs, which appends these.
    static LevelData.BlockSpawnData[] L01Room2Blocks => new[]
    {{
@@L01B@@
    }};
    static LevelData.RobotSpawnData[] L01Room2Robots => new[]
    {{
@@L01R@@
    }};
    static LevelData.PlatformSpawnData[] L01Room2Platforms => new LevelData.PlatformSpawnData[]
    {{
@@L01P@@
    }};
}}
"""
    bl, rl, pl, _ = L01_ROOM2.cs_items()
    ind = lambda xs: "\n".join(f"        {x}," for x in xs)
    src = src.replace("@@L01B@@", ind(bl)).replace("@@L01R@@", ind(rl)).replace("@@L01P@@", ind(pl))
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
            im = load("Environment/World1Props/" + SPRITE[b["kind"]] + ".png")
            if abs(b.get("rot", 0)) > 0.01:
                im = im.resize((max(1, int(b["w"] * PX)), max(1, int(b["h"] * PX)))).rotate(b["rot"], expand=True, resample=Image.BICUBIC)
                put(c, im, b["x"], b["y"], im.size[0] / PX, im.size[1] / PX)
            else:
                put(c, im, b["x"], b["y"], b["w"], b["h"])
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
