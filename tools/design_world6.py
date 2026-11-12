"""
Farm Fury - World 6 (Robot Mothership) level designer (2026-11-12).

World 6 built with the same process as Worlds 1-5 (user: "go ahead and build W6 - mothership - applying the same
process"): framed levels with live physics, World 4's robot-built "cities of elevation" (design_world4.city) standing
on the mothership's machinery pedestals, the ship contraptions (a laser emitter that fires a jet of plasma orbs when
hit, a gravity ring that rolls into the structure), the mothership robots and the Factory Machine, dropped off by the
mothership. Every level has two rooms (the second, smaller city one room to the right); the boss level's second room is
the Factory Machine's keep. Billy (World 6's animal) leads.

Uses the shared piece / mount / check code in design_world1.py (world=6) and World 4's city generator, pointed at the
pedestal shapes (design_world4.CLOUDS).

Usage (repo root):  python tools/design_world6.py
Writes unity/Assets/Editor/LevelDataGenerator.World6.cs; then FarmFury -> Generate All Level Data.
"""
import os
import random as _random
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import design_world1 as d   # noqa: E402  (its World 1 levels build on import; harmless)
import design_world4 as w4  # noqa: E402  (its World 4 levels build on import; harmless - only the generator is used)

# The mountains the city stands on: World 6's machinery pedestals (tools/import_mothership_art.py, mounts_w6.json).
w4.CLOUDS = [(v, d.MOUNTS_W[6][n]) for v, n in ((0, "DeckLow"), (1, "DeckMid"), (2, "DeckTall"))]

G = d.G
AT = w4.AT
BI, GE, H, D, P, W, C, B = "Billy", "Gerald", "Horace", "Ducky", "Percy", "Woolly", "Cluck", "Bessie"
LEVELS = []


def _pick(k, name, par, birds, comment, weather, boss, robots, fixed, skip, seed_base, **kw):
    """Try seeds until the city passes every designer check. skip: pass over valid cities that fell in PhysicsProbe."""
    key = name.replace("'", "").replace(" ", "").replace("-", "")
    if kw.get("peaks", 4) >= 5: kw.setdefault("x0", 6.0); kw.setdefault("x1", 15.8)
    for seed in range(1200):
        L = d.Level(f"W06_L{k:02d}_{key}", f"W6_L{k:02d}", name, par, birds, comment, boss=boss, world=6)
        L.weather = weather
        rng = _random.Random(seed_base + seed)
        if fixed: fixed(L, rng)
        w4.city(L, rng, **kw)
        if not w4.settle_robots(L, rng, *robots): continue
        w4.fit_clouds(L)
        if d.check(L) or w4.cloud_checks(L): continue
        if any(b["vis"][3] > AT(11.0) for b in L.blocks): continue
        if skip > 0: skip -= 1; continue
        L.seed = seed
        return L
    raise RuntimeError(f"W6_L{k:02d} {name}: no valid city in 1200 seeds")


def ship_level(k, name, par, birds, comment, weather="StarField", robots=(4, 6), fixed=None, skip=0,
               room2=None, room2_skip=0, **kw):
    """Room 1: a robot city on machinery pedestals. Room 2: a smaller one (design_world4.room2_spec), its own seeds."""
    L = _pick(k, name, par, birds, comment, weather, False, robots, fixed, skip, 700000 + k * 1000, **kw)
    spec = dict(room2 or w4.room2_spec(k + 6, kw))   # World 6 is late in the game: room 2 at the later bands' size
    r2 = _pick(k, name, par, birds, comment, weather, False, spec.pop("robots"), spec.pop("fixed", None), room2_skip,
               800000 + k * 1000, **spec)
    L.add_room(r2)
    LEVELS.append(L)
    return L


def emitter(*xs):
    """Laser emitters on the deck in front of the city: hit one and it fires a jet of plasma orbs into the structure."""
    def f(L, rng):
        for x in xs: L.sunflower(x, G, 1.1)
    return f


def ring(x=4.9):
    """A gravity ring parked on the deck: hit it and it rolls right into the city's foot."""
    def f(L, rng):
        L.boulder(x, G, 1.1)
    return f


def both(*fs):
    def f(L, rng):
        for g in fs: g(L, rng)
    return f


# ═══════════════════════════════════════════ the levels ═══════════════════════════════════════════════════════
ship_level(1, "Boarding Party", 2, [BI, BI, C], """
Billy storms aboard: three pedestals climbing to the right, a crooked robot district on each.""",
           robots=(3, 4), peaks=3, profile="rise", stack=0.3, walk=0.5, x0=6.4, x1=13.6, fixed=emitter(4.7))
ship_level(2, "Cargo Bay", 2, [BI, GE, C], """
Four stacks of machinery stepping up and down, walkways strung between the roofs.""", weather="SolarFlare",
           robots=(4, 6), peaks=4, profile="random", stack=0.4, walk=0.9, fixed=ring())
ship_level(3, "Engine Room", 2, [BI, H, C], """
Under red alert: districts built around the engines, the tallest in the middle.""", weather="RedAlert",
           robots=(4, 6), peaks=4, profile="mount", stack=0.5, fixed=emitter(4.6, 5.6))
ship_level(4, "Gravity Well", 2, [BI, GE, W], """
A town falling away from a high lookout, walkways all the way down.""", weather="NebulaGlow",
           robots=(5, 7), peaks=4, profile="fall", walk=1.0)
ship_level(5, "Assembly Line", 3, [BI, D, B], """
Districts stacked two pieces high on every pedestal - towers on towers.""",
           robots=(5, 7), peaks=4, profile="valley", stack=0.9, fixed=ring())
ship_level(6, "Reactor Core", 3, [BI, P, B], """
Pedestals and machinery pillars carry the town, walkways from roof to roof.""", weather="SolarFlare",
           robots=(5, 7), peaks=4, profile="random", mounts=("cloud", "mesa"), walk=1.0, fixed=emitter(4.8))
ship_level(7, "Power Down", 3, [BI, W, B], """
The lights go down over a town of five districts.""", weather="PowerDown",
           robots=(6, 8), peaks=5, profile="random", stack=0.5)
ship_level(8, "Hangar Deck", 3, [BI, GE, C], """
A valley town with the dynamite stored in the middle.""",
           robots=(5, 7), peaks=4, profile="valley", fixed=lambda L, rng: (L.barrel(10.6, G), L.barrel(11.2, G)))
ship_level(9, "Nebula Run", 3, [BI, H, B], """
A mountain-shaped skyline of crowned districts in the nebula's glow.""", weather="NebulaGlow",
           robots=(6, 8), peaks=4, profile="mount", stack=0.7, fixed=both(emitter(5.0), emitter(15.6)))
ship_level(10, "Red Alert", 3, [BI, D, B], """
Five districts crammed together while the alarms sound.""", weather="RedAlert",
           robots=(6, 8), peaks=5, profile="valley", stack=0.6, fixed=ring())
ship_level(11, "Control Tower", 3, [BI, W, B], """
A town partly on the pedestals and partly on the deck, stacked high.""",
           robots=(6, 8), peaks=4, profile="random", ground=0.35, stack=0.8, fixed=emitter(4.8))
ship_level(12, "Shield Generator", 3, [BI, P, W], """
Every district crowned with something taller.""", weather="SolarFlare",
           robots=(6, 8), peaks=4, profile="rise", mounts=("mesa", "cloud"), stack=1.0)
ship_level(13, "Docking Clamps", 3, [BI, GE, B], """
Pedestals and pillars, the town bridged end to end.""", weather="PowerDown",
           robots=(6, 9), peaks=5, profile="random", mounts=("cloud", "mesa"), walk=1.0, fixed=ring())
ship_level(14, "Bridge Deck", 3, [BI, H, B], """
A fortress town, the highest districts at the back.""", weather="NebulaGlow",
           robots=(6, 9), peaks=5, profile="rise", stack=0.7, walk=0.9, fixed=emitter(4.7, 5.6))
ship_level(15, "Machine Gate", 3, [BI, B, B], """
The last approach: a valley town in red alert, dynamite at the gate.""", weather="RedAlert",
           robots=(6, 9), peaks=5, profile="valley", stack=0.7,
           fixed=lambda L, rng: (L.barrel(5.6, G), L.sunflower(4.6, G, 1.1)))


def _keep(L, rng):
    """The Factory Machine's keep: a steel gate on a broad pedestal with a crooked tower he lands on."""
    s_ = w4.cloud(L, 13.4, AT(2.0), 3.4)
    top = L.post(12.2, s_, 1.5, stone=True); L.post(14.6, s_, 1.5, stone=True)
    r_ = L.plank(11.9, 14.9, top, stone=True)
    L.robot(13.4, s_, "semi")
    t = d.tower(L, 13.4, r_, 3, width=2.2, h=1.1, taper=0.2, robots=["pawn", None, "semi"], wonky=False)   # he lands on it
    L.robot(13.4, t, "boss")


# Boss level, two rooms: the Factory Machine's outer works first, then (room 2) his keep, where the mothership drops
# him off.
_keep_room = _pick(16, "The Factory Machine", 3, [BI, B, B], "", "RedAlert", True, (6, 9), _keep, 0, 716000,
                   peaks=3, profile="rise", x0=5.8, x1=11.0, stack=0.6)
_town = _pick(16, "The Factory Machine", 3, [BI, B, B], """
Boss: the Factory Machine's outer works, then his keep at the heart of the ship (the mothership drops him on it).""",
              "RedAlert", True, (5, 7), None, 0, 616000, peaks=4, profile="mount", mounts=("cloud", "mesa"),
              stack=0.6, walk=0.9)
_town.add_room(_keep_room)
LEVELS.append(_town)

for _L in LEVELS: w4.fit_clouds(_L)


# ═══════════════════════════════════════════ output ═══════════════════════════════════════════════════════
def write_cs():
    d.prune_all(LEVELS)   # robots hidden behind a mound (2026-11-12)
    body = "\n\n".join(L.cs() for L in LEVELS)
    keep = ", ".join(f'"{L.file}"' for L in LEVELS)
    src = f"""// GENERATED by tools/design_world6.py — edit the designs there, not here.
// World 6 (Robot Mothership) levels W6_L01-W6_L16 (2026-11-12): the World 1-5 template aboard the mothership — framed,
// live physics, robot-built cities on machinery pedestals, two rooms per level, the ship contraptions, the Factory Machine.

using UnityEditor;

public static partial class LevelDataGenerator
{{
    static void MakeWorld6Framed(string folder)
    {{
        // Drop any older World 6 asset that isn't one of these (the generated W06_* set had different names).
        var keep = new System.Collections.Generic.HashSet<string> {{ {keep} }};
        foreach (var guid in AssetDatabase.FindAssets("t:LevelData", new[] {{ folder }}))
        {{
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string file = System.IO.Path.GetFileNameWithoutExtension(path);
            if (file.StartsWith("W06_") && !keep.Contains(file)) AssetDatabase.DeleteAsset(path);
        }}

{body}
    }}
}}
"""
    path = os.path.join(d.UNITY, "Editor", "LevelDataGenerator.World6.cs")
    open(path, "w", encoding="utf-8").write(src)
    print("wrote", os.path.normpath(path))


if __name__ == "__main__":
    bad = 0
    for L in LEVELS:
        for msg in d.check(L) + w4.cloud_checks(L):
            print(f"{L.id}: {msg}"); bad += 1
    print(f"{len(LEVELS)} levels, {sum(len(L.blocks) for L in LEVELS)} blocks, {sum(len(L.robots) for L in LEVELS)} robots, {bad} issues")
    write_cs()
