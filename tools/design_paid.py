"""
Farm Fury - shared level-design kit for the paid worlds (2026-11-12): World 7 Dustbowl Canyon, World 8 Harvest
Fairground, World 9 Crop Factory (tools/design_world7.py / 8 / 9 use it).

Same process as Worlds 4-6 (user: "recall your process of building the worlds, apply this and build the pay worlds ...
apply the structure plan (be creative)"): framed levels with live physics, World 4's robot-built "cities of elevation"
(design_world4.city: districts on mounts of different heights, pieces stacked on pieces, walkways between roofs, robots
last), two rooms per level (the second a smaller city one room to the right), the boss level's second room the boss's
keep where the mothership drops him off. Each world adds its own robot-built set pieces to the generator's piece table,
so its skyline reads as that world:
  World 7  headframe (a mine headframe: an A of leaning timbers with a crossbeam), water_tower (stilts, a deck and a
           sandstone tank), saloon (a two-storey false-front hut, the top storey off-centre), rock_arch (a sandstone arch
           with a boulder balanced on it).
  World 8  carousel (a canopy on a disc on one pole, riders underneath), high_striker (a thin crate tower with a stick
           of dynamite for a bell), prize_stack (a pyramid of prize cushions), big_top (a tent pitched on a booth roof).
  World 9  conveyor (a girder on legs with crates riding it), press (a steel plate held over a robot on two thin legs,
           a steel crate on top), silo_stack (stacked steel crates under a produce crate), crane_arm (a lopsided girder
           on one post with a robot out on the long end).
A level is the first seed whose city passes every designer check (design_world1.check + cloud_checks), so every level
is valid by construction and stable from run to run.
"""
import os
import random as _random
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import design_world1 as d   # noqa: E402  (its World 1 levels build on import; harmless)
import design_world4 as w4  # noqa: E402  (its World 4 levels build on import; harmless - only the generator is used)

G = d.G
AT = w4.AT
BI, GE, H, D, P, W, C, B = "Billy", "Gerald", "Horace", "Ducky", "Percy", "Woolly", "Cluck", "Bessie"

_base_piece = w4._piece


# ═══════════════════════════════════════════ World 7: frontier pieces ═══════════════════════════════════════════
def headframe(L, x, b, rng, side, stacked=False):
    """A mine headframe: two timbers leaning into an A, a crossbeam balanced on the apex, robots on its ends and a
    little winding-house hut on top."""
    h = rng.uniform(0.9, 1.2) if stacked else rng.uniform(1.3, 1.8)
    half = rng.uniform(0.5, 0.65)
    n0 = len(L.blocks)
    L.beam(x - half, b, x - 0.03, b + h)
    L.beam(x + 0.03, b + h, x + half, b)
    tip = max(y for bl in L.blocks[n0:] for _, y in bl["poly"])
    w = rng.uniform(1.7, 2.1)
    t = L.plank(x - w / 2, x + w / 2, tip + 0.004)
    L.robot(x - w / 2 + 0.4, t, "pawn")
    if rng.random() < 0.6: L.robot(x + w / 2 - 0.4, t, "pawn")
    if not stacked and rng.random() < 0.5: L.hut(x - 0.45, x + 0.45, t, 0.8)


def water_tower(L, x, b, rng, side, stacked=False):
    """Stilts, a deck and a sandstone tank on it, a robot sheltering underneath."""
    h = rng.uniform(0.9, 1.2) if stacked else rng.uniform(1.5, 2.1)
    w = rng.uniform(1.15, 1.3)
    t = L.hut(x - w / 2, x + w / 2, b, h)
    if not stacked: L.robot(x, b, "pawn")
    tt = L.box(x + rng.uniform(-0.15, 0.15), t, 0.8, stone=True)
    L.robot(x, tt, rng.choice(["pawn", "semi"]))


def saloon(L, x, b, rng, side, stacked=False):
    """A two-storey false-front saloon: a wide booth, a narrower storey set off-centre on its roof."""
    w = 1.4 if stacked else rng.uniform(1.6, 1.8)
    t = L.hut(x - w / 2, x + w / 2, b, rng.uniform(0.95, 1.1))
    L.robot(x, b, "pawn")
    off = rng.uniform(-0.22, 0.22)
    w2 = rng.uniform(1.1, 1.2)
    t2 = L.hut(x + off - w2 / 2, x + off + w2 / 2, t, 0.95)
    L.robot(x + off, t, "pawn")
    if rng.random() < 0.5: L.robot(x + off, t2, "semi")


def rock_arch(L, x, b, rng, side, stacked=False):
    """A sandstone arch (two pillars and a slab) with a boulder balanced on top, robots under and on it."""
    w = rng.uniform(1.5, 1.7)
    t = L.hut(x - w / 2, x + w / 2, b, rng.uniform(1.1, 1.5), stone=True)
    L.robot(x, b, "pawn")
    tt = L.box(x + rng.uniform(-0.25, 0.25), t, 0.7, stone=True)
    L.robot(x, tt, "pawn")


# ═══════════════════════════════════════════ World 8: fairground pieces ═════════════════════════════════════════
def carousel(L, x, b, rng, side, stacked=False):
    """A carousel: one pole, a disc on it, a canopy on two short poles over the disc, a rider underneath."""
    t = L.post(x, b, rng.uniform(0.7, 0.9) if stacked else rng.uniform(1.0, 1.4))
    disc = rng.uniform(1.9, 2.2)
    t = L.plank(x - disc / 2, x + disc / 2, t)
    L.robot(x, t, "pawn")   # one rider, clear of the canopy poles (robot collider 0.62 wide)
    c = L.hut(x - disc / 2 + 0.05, x + disc / 2 - 0.05, t, 0.95)
    if rng.random() < 0.6: L.robot(x, c, "semi")


def high_striker(L, x, b, rng, side, stacked=False):
    """Ring the bell: a thin tower of crates, a lid, and a stick of dynamite for the bell next to a robot."""
    t = b
    for k in range(rng.randint(2, 3) if stacked else rng.randint(4, 5)):
        t = L.box(x + (0.04 if k % 2 else -0.04), t, 0.5, stone=False)
    t = L.plank(x - 0.7, x + 0.7, t)
    L.barrel(x - 0.3 * side, t)
    L.robot(x + 0.36 * side, t, "pawn")


def prize_stack(L, x, b, rng, side, stacked=False):
    """A pyramid of prize cushions (2-1, sometimes 3-2-1), a robot crowning it."""
    s = 0.98
    rows = (2, 1) if stacked or rng.random() < 0.7 else (3, 2, 1)
    t = b
    for n in rows:
        top = t
        for i in range(n):
            top = L.hay(x + (i - (n - 1) / 2) * s, t)
        t = top
    L.robot(x, t, rng.choice(["pawn", "semi"]))


def big_top(L, x, b, rng, side, stacked=False):
    """A ticket booth with a little circus tent pitched at one end of its roof and a robot at the other, one more
    robot in the booth. (A robot can't stand inside the tent: its sloping sides are lower than the robot.)"""
    t = L.hut(x - 0.85, x + 0.85, b, rng.uniform(0.95, 1.2))
    L.robot(x, b, "pawn")
    d.aframe(L, x - 0.4 * side, t, h=rng.uniform(0.75, 0.9), half=0.42)
    L.robot(x + 0.5 * side, t, rng.choice(["pawn", "semi"]))


# ═══════════════════════════════════════════ World 9: factory pieces ════════════════════════════════════════════
def conveyor(L, x, b, rng, side, stacked=False):
    """A girder on legs with crates riding along it, robots between the crates and underneath."""
    w = rng.uniform(2.6, 3.1)
    t = L.hut(x - w / 2, x + w / 2, b, rng.uniform(0.9, 1.1) if stacked else rng.uniform(1.1, 1.5))
    if not stacked: L.robot(x, b, "pawn")
    L.box(x - w / 2 + 0.45, t, 0.55, stone=False)
    L.box(x + w / 2 - 0.45, t, 0.55, stone=False)
    L.robot(x, t, rng.choice(["pawn", "semi"]))


def press(L, x, b, rng, side, stacked=False):
    """A hydraulic press: a heavy steel plate held over a robot on two thin legs, a steel crate stacked on it."""
    t = L.hut(x - 0.8, x + 0.8, b, rng.uniform(1.15, 1.4), roof_stone=True)
    L.robot(x, b, "pawn")
    tt = L.box(x + rng.uniform(-0.15, 0.15), t, 0.7, stone=True)
    L.robot(x, tt, "pawn")


def silo_stack(L, x, b, rng, side, stacked=False):
    """Steel crates stacked up a little crooked, a produce crate on top and a robot on a lid."""
    t = b
    for k in range(1 if stacked else rng.randint(2, 3)):
        t = L.box(x + (0.05 if k % 2 else -0.05), t, 0.7, stone=True)
    t = L.hay(x, t)
    t = L.plank(x - 0.75, x + 0.75, t)
    L.robot(x, t, "semi")


def crane_arm(L, x, b, rng, side, stacked=False):
    """A crane arm: one post, a girder stuck on it lopsided, a robot out on the long end."""
    h = rng.uniform(0.8, 1.0) if stacked else rng.uniform(1.3, 1.9)
    long_, short = rng.uniform(1.6, 2.0), rng.uniform(0.5, 0.7)
    left, right = (short, long_) if side > 0 else (long_, short)
    d.cantilever(L, x, b, h, left, right, tip=rng.choice(["pawn", "semi"]))


THEMES = {
    "headframe": headframe, "water_tower": water_tower, "saloon": saloon, "rock_arch": rock_arch,
    "carousel": carousel, "high_striker": high_striker, "prize_stack": prize_stack, "big_top": big_top,
    "conveyor": conveyor, "press": press, "silo_stack": silo_stack, "crane_arm": crane_arm,
}
# Each world's piece table: its own pieces weighted in, plus a share of World 4's absurd pieces (robots still don't
# know how to build). Stack tables: what can go on top of a district.
PIECES = {
    7: (["headframe", "headframe", "water_tower", "water_tower", "saloon", "saloon", "rock_arch", "rock_arch",
         "lollipop", "balcony", "teeter", "spire", "stilts"],
        ["water_tower", "saloon", "headframe", "spire", "crown"]),
    8: (["carousel", "carousel", "high_striker", "high_striker", "prize_stack", "prize_stack", "big_top", "big_top",
         "teeter", "teeter", "lollipop", "upturned", "spire"],
        ["carousel", "high_striker", "big_top", "spire", "lollipop"]),
    9: (["conveyor", "conveyor", "press", "press", "silo_stack", "silo_stack", "crane_arm", "crane_arm",
         "upturned", "chimney", "balcony", "slab", "pyramid"],
        ["press", "silo_stack", "conveyor", "chimney", "crown"]),
}


_base_shape = w4._shape


def _shape_narrowest(need, top_width):
    """design_world4._shape with a tie-break: when a mount must be taller than any shape allows unstretched, every
    shape ends at MAX_STRETCH with the same score and the FIRST (the low, wide one) won, widened to ~6 u - World 8's
    straight-sided podiums then swallowed their neighbours' districts (W8_L09 found no layout in 1500 seeds). Prefer the
    narrowest of equal scores. Used by World 8 only, so the already-generated Worlds 4-7 and 9 keep their output."""
    import math
    best = None
    for v, m in w4.CLOUDS:
        f0, f1 = m["flat"]
        w = top_width / (f1 - f0 - 0.04)
        h = need / m["uv"]
        st = h / (w / m["aspect"])
        if st > w4.MAX_STRETCH: w, st = h * m["aspect"] / w4.MAX_STRETCH, w4.MAX_STRETCH
        score = abs(math.log(max(st, w4.MIN_STRETCH))) + (1.0 if st < w4.MIN_STRETCH else 0.0)
        if best is None or (score, w) < (best[0], best[3]): best = (score, v, m, w, h)
    return best[1:]


def use_world(world, mounts):
    """Point World 4's city generator at this world's mounts and pieces."""
    w4.CLOUDS = [(v, d.MOUNTS_W[world][n]) for v, n in enumerate(mounts)]
    w4._shape = _shape_narrowest if world == 8 else _base_shape
    w4.BASE_PIECES, w4.STACK_PIECES = PIECES[world]

    def piece(L, kind, x, b, rng, side, stacked=False):
        if kind in THEMES: THEMES[kind](L, x, b, rng, side, stacked)
        else: _base_piece(L, kind, x, b, rng, side, stacked)
    w4._piece = piece


def pick(world, k, name, par, birds, comment, weather, boss, robots, fixed, skip, seed_base, **kw):
    """Try seeds until the city passes every designer check. skip: pass over valid cities that fell in PhysicsProbe."""
    key = name.replace("'", "").replace(" ", "").replace("-", "").replace("!", "")
    if kw.get("peaks", 4) >= 5: kw.setdefault("x0", 6.0); kw.setdefault("x1", 15.8)
    for seed in range(1500):
        L = d.Level(f"W{world:02d}_L{k:02d}_{key}", f"W{world}_L{k:02d}", name, par, birds, comment, boss=boss, world=world)
        L.weather = weather
        rng = _random.Random(seed_base + seed)
        try:
            if fixed: fixed(L, rng)
            w4.city(L, rng, **kw)
        except (ValueError, AssertionError):
            continue
        if not w4.settle_robots(L, rng, *robots): continue
        w4.fit_clouds(L)
        if d.check(L) or w4.cloud_checks(L): continue
        if any(b["vis"][3] > AT(11.0) for b in L.blocks): continue
        if skip > 0: skip -= 1; continue
        L.seed = seed
        return L
    raise RuntimeError(f"W{world}_L{k:02d} {name}: no valid city in 1500 seeds")


class World:
    """One paid world's level list and output."""

    def __init__(self, world, title):
        self.world, self.title, self.levels = world, title, []

    def level(self, k, name, par, birds, comment, weather, robots=(4, 6), fixed=None, skip=0, room2=None,
              room2_skip=0, **kw):
        """Room 1: a robot city. Room 2: a smaller one (design_world4.room2_spec, the late bands' size), its own seeds."""
        base = 1_000_000 + self.world * 100_000
        L = self._pick_relaxing(k, name, par, birds, comment, weather, robots, fixed, skip, base + k * 1000, kw)
        spec = dict(room2 or w4.room2_spec(k + 8, kw))   # paid worlds are late-game: room 2 at the later bands' size
        r2 = self._pick_relaxing(k, name, par, birds, comment, weather, spec.pop("robots"), spec.pop("fixed", None),
                                 room2_skip, base + 50_000 + k * 1000, spec)
        L.add_room(r2)
        self.levels.append(L)
        return L

    def _pick_relaxing(self, k, name, par, birds, comment, weather, robots, fixed, skip, seed_base, kw):
        """pick(), and if no seed passes: one district fewer (down to 3), then a 'fall' skyline as 'random', then one
        robot fewer at the minimum - each relaxation logged, so a level that needed one is visible in the report."""
        kw, robots = dict(kw), tuple(robots)
        while True:
            try:
                return pick(self.world, k, name, par, birds, comment, weather, False, robots, fixed, skip, seed_base, **kw)
            except RuntimeError:
                if kw.get("peaks", 4) > 3:
                    kw["peaks"] = kw.get("peaks", 4) - 1
                    if kw["peaks"] < 5: kw.pop("x0", None); kw.pop("x1", None)
                    print(f"  W{self.world}_L{k:02d} {name}: relaxed to {kw['peaks']} districts")
                elif kw.get("profile") == "fall":
                    kw["profile"] = "random"
                    print(f"  W{self.world}_L{k:02d} {name}: relaxed the skyline to random")
                elif robots[0] > 3:
                    robots = (robots[0] - 1, robots[1])
                    print(f"  W{self.world}_L{k:02d} {name}: relaxed the robot minimum to {robots[0]}")
                else:
                    raise

    def boss_level(self, k, name, par, birds, comment, weather, keep, town_kw, keep_kw):
        """Boss: the outer works first, then (room 2) the keep, where the mothership drops the boss off."""
        base = 1_000_000 + self.world * 100_000 + 90_000
        keep_room = pick(self.world, k, name, par, birds, "", weather, True, (5, 8), keep, 0, base + 5_000, **keep_kw)
        town = pick(self.world, k, name, par, birds, comment, weather, True, (5, 7), None, 0, base, **town_kw)
        town.add_room(keep_room)
        self.levels.append(town)
        return town

    def write_cs(self, blurb):
        for L in self.levels: w4.fit_clouds(L)
        d.prune_all(self.levels)   # robots hidden behind a mound
        body = "\n\n".join(L.cs() for L in self.levels)
        keep = ", ".join(f'"{L.file}"' for L in self.levels)
        n = self.world
        src = f"""// GENERATED by tools/design_world{n}.py (with tools/design_paid.py) — edit the designs there, not here.
// {blurb}

using UnityEditor;

public static partial class LevelDataGenerator
{{
    static void MakeWorld{n}Framed(string folder)
    {{
        // Drop any older World {n} asset that isn't one of these (the generated W0{n}_* set had different names).
        var keep = new System.Collections.Generic.HashSet<string> {{ {keep} }};
        foreach (var guid in AssetDatabase.FindAssets("t:LevelData", new[] {{ folder }}))
        {{
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string file = System.IO.Path.GetFileNameWithoutExtension(path);
            if (file.StartsWith("W0{n}_") && !keep.Contains(file)) AssetDatabase.DeleteAsset(path);
        }}

{body}
    }}
}}
"""
        path = os.path.join(d.UNITY, "Editor", f"LevelDataGenerator.World{n}.cs")
        open(path, "w", encoding="utf-8").write(src)
        print("wrote", os.path.normpath(path))

    def report(self):
        bad = 0
        for L in self.levels:
            for msg in d.check(L) + w4.cloud_checks(L):
                print(f"{L.id}: {msg}"); bad += 1
        print(f"World {self.world}: {len(self.levels)} levels, {sum(len(L.blocks) for L in self.levels)} blocks, "
              f"{sum(len(L.robots) for L in self.levels)} robots, {bad} issues")
        return bad


def emitter(h, *xs):
    """The world's 'pump' contraption on the floor in front of the city: hit it and it fires a jet into the structure."""
    def f(L, rng):
        for x in xs: L.sunflower(x, G, h)
    return f


def roller(x=4.9, size=1.1):
    """The world's rolling contraption parked on the floor: hit it and it rolls right into the city's foot."""
    def f(L, rng):
        L.boulder(x, G, size)
    return f


def dynamite(*xs):
    def f(L, rng):
        for x in xs: L.barrel(x, G)
    return f


def both(*fs):
    def f(L, rng):
        for g in fs: g(L, rng)
    return f


def keep_tower(top_piece):
    """The boss keep: a broad mount with a stone gate, a robot inside, a straight (non-wonky) tower on the gate the
    boss lands on, and the world's own piece beside it."""
    def f(L, rng):
        s_ = w4.cloud(L, 13.4, AT(2.0), 3.4)
        top = L.post(12.2, s_, 1.5, stone=True); L.post(14.6, s_, 1.5, stone=True)
        r_ = L.plank(11.9, 14.9, top, stone=True)
        L.robot(13.4, s_, "semi")
        t = d.tower(L, 13.4, r_, 3, width=2.2, h=1.1, taper=0.2, robots=["pawn", None, "semi"], wonky=False)
        L.robot(13.4, t, "boss")
        if top_piece: THEMES[top_piece](L, 4.9, G, rng, 1)   # the world's own piece out front
    return f
