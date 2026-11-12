"""
Farm Fury - World 5 (Sunken City) level designer (2026-11-12).

World 5 built with the same process as Worlds 1-4 (user: "begin with W5 - utilise the exact same process as the other
worlds ... apply the same two room panning, and enhanced structures also"): framed levels with live physics, the
robot-built "cities of elevation" of World 4 (design_world4.city: absurd, top-heavy districts on peaks at different
heights, walkways on pylons between them) standing on coral reef mesas that rise from the sea floor, the sea
contraptions (a clam that spits a jet of pearls when hit, a ship's wheel that rolls into the structure), the sea robots
and the Deep Dredger, dropped off by the mothership. Every level has two rooms (LevelData.rooms): the second, smaller
city one room to the right; the boss level's second room is the Dredger's keep. Gerald (World 5's animal) leads.

Uses the shared piece / mount / check code in design_world1.py (world=5) and World 4's city generator, pointed at the
reef shapes (design_world4.CLOUDS) instead of the cloud mountains.

Usage (repo root):  python tools/design_world5.py
Writes unity/Assets/Editor/LevelDataGenerator.World5.cs; then FarmFury -> Generate All Level Data.
"""
import os
import random as _random
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import design_world1 as d   # noqa: E402  (its World 1 levels build on import; harmless)
import design_world4 as w4  # noqa: E402  (its World 4 levels build on import; harmless - only the generator is used)

# The mountains the city stands on: World 5's coral reef mesas (tools/import_sunken_art.py, mounts_w5.json).
w4.CLOUDS = [(v, d.MOUNTS_W[5][n]) for v, n in ((0, "ReefLow"), (1, "ReefMid"), (2, "ReefTall"))]

G = d.G
AT = w4.AT
GE, H, D, P, W, C, B = "Gerald", "Horace", "Ducky", "Percy", "Woolly", "Cluck", "Bessie"
LEVELS = []


def _pick(k, name, par, birds, comment, weather, boss, robots, fixed, skip, seed_base, **kw):
    """Try seeds until the city passes every designer check (support, overlaps, robots, reach from the raised cannon,
    nothing inside a reef, under the HUD). skip: pass over that many valid cities (one that fell in PhysicsProbe)."""
    key = name.replace("'", "").replace(" ", "").replace("-", "")
    if kw.get("peaks", 4) >= 5: kw.setdefault("x0", 6.0); kw.setdefault("x1", 15.8)
    for seed in range(1200):
        L = d.Level(f"W05_L{k:02d}_{key}", f"W5_L{k:02d}", name, par, birds, comment, boss=boss, world=5)
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
    raise RuntimeError(f"W5_L{k:02d} {name}: no valid city in 1200 seeds")


def reef_level(k, name, par, birds, comment, weather="SunlitShallows", robots=(4, 6), fixed=None, skip=0,
               room2=None, room2_skip=0, **kw):
    """Room 1: a sunken city on reef peaks. Room 2: a smaller one (design_world4.room2_spec), its own seeds."""
    L = _pick(k, name, par, birds, comment, weather, False, robots, fixed, skip, 500000 + k * 1000, **kw)
    spec = dict(room2 or w4.room2_spec(k, kw))
    r2 = _pick(k, name, par, birds, comment, weather, False, spec.pop("robots"), spec.pop("fixed", None), room2_skip,
               600000 + k * 1000, **spec)
    L.add_room(r2)
    LEVELS.append(L)
    return L


# ── fixed contraptions ────────────────────────────────────────────────────────────────────────────────────────
def clam(*xs):
    """Clams on the sea floor in front of the city: hit one and it spits a jet of pearls into the structure."""
    def f(L, rng):
        for x in xs: L.sunflower(x, G, 1.1)
    return f


def wheel(x=4.9):
    """A ship's wheel parked on the sea floor: hit it and it rolls right into the city's foot."""
    def f(L, rng):
        L.boulder(x, G, 1.1)
    return f


def both(*fs):
    def f(L, rng):
        for g in fs: g(L, rng)
    return f


# ═══════════════════════════════════════════ the levels ═══════════════════════════════════════════════════════
reef_level(1, "Coral Landing", 2, [GE, GE, C], """
Gerald's first dive: three reef peaks climbing to the right, a crooked sunken district on each.""",
           robots=(3, 4), peaks=3, profile="rise", stack=0.3, walk=0.5, x0=6.4, x1=13.6, fixed=clam(4.7))
reef_level(2, "Kelp Steps", 2, [GE, GE, C], """
Four reefs stepping up and down through the kelp, walkways strung between the roofs.""", weather="KelpForest",
           robots=(4, 5), peaks=4, profile="random", stack=0.4, walk=0.9)
reef_level(3, "Barnacle Row", 2, [GE, H, C], """
A row of barnacled districts, the tallest in the middle.""",
           robots=(4, 6), peaks=4, profile="mount", stack=0.5, fixed=wheel())
reef_level(4, "Pearl Divers", 2, [GE, H, C], """
A town falling away from a high lookout; clams guard the sea floor in front.""", weather="Undertow",
           robots=(4, 6), peaks=4, profile="fall", walk=1.0, fixed=clam(4.6, 5.6))
reef_level(5, "Drowned Market", 2, [GE, W, C], """
Market stalls stacked two high on every reef.""", weather="KelpForest",
           robots=(5, 7), peaks=4, profile="valley", stack=0.9)
reef_level(6, "Anchor Point", 2, [GE, W, C], """
Reef pillars and peaks carry the town, walkways from roof to roof.""",
           robots=(4, 6), peaks=4, profile="random", mounts=("cloud", "mesa"), walk=1.0, fixed=wheel())
reef_level(7, "Glowing Grotto", 3, [GE, P, W], """
In the deep, the plankton glows over a town of five districts.""", weather="GlowingDeep",
           robots=(5, 7), peaks=5, profile="random", stack=0.5, fixed=clam(4.8))
reef_level(8, "Shipwreck Alley", 2, [GE, P, C], """
A valley town built from the timbers of sunken ships.""", weather="MurkyDepths",
           robots=(5, 7), peaks=4, profile="valley", fixed=lambda L, rng: (L.barrel(10.6, G), L.barrel(11.2, G)))
reef_level(9, "Sponge Gardens", 2, [GE, B, C], """
Towers on towers over the sponge gardens, a mountain-shaped skyline.""",
           robots=(5, 7), peaks=4, profile="mount", stack=0.6)
reef_level(10, "Tidal Temple", 3, [GE, P, B], """
The current tugs at a temple town rising to the right.""", weather="Undertow",
           robots=(5, 7), peaks=4, profile="rise", x0=6.6, x1=14.4, fixed=both(clam(5.0), clam(15.6)))
reef_level(11, "Murky Moorings", 3, [GE, W, B], """
Five districts crammed together in the murk.""", weather="MurkyDepths",
           robots=(6, 8), peaks=5, profile="valley", stack=0.5)
reef_level(12, "Jellyfish Drift", 2, [GE, B, C], """
A town partly on the reefs and partly on the sea floor, stacked high.""", weather="GlowingDeep",
           robots=(5, 7), peaks=4, profile="random", ground=0.35, stack=0.7, fixed=wheel())
reef_level(13, "Treasure Vault", 3, [GE, W, B], """
Reefs and pillars, the town bridged end to end.""", weather="KelpForest",
           robots=(6, 8), peaks=4, profile="random", mounts=("cloud", "mesa"), walk=1.0)
reef_level(14, "Sunken Spires", 3, [GE, P, W], """
Every district crowned with something taller.""",
           robots=(6, 8), peaks=4, profile="rise", mounts=("mesa", "cloud"), stack=1.0, fixed=clam(4.8))
reef_level(15, "Clam Cove", 3, [GE, H, B], """
A market town on five small reefs, clams all along the floor.""", weather="SunlitShallows",
           robots=(6, 8), peaks=5, profile="random", stack=0.4, walk=0.8, fixed=clam(4.6, 5.5))
reef_level(16, "Abyss Edge", 3, [GE, B, B], """
A temple town on a mountain of reefs, its middle districts stacked high.""", weather="MurkyDepths",
           robots=(6, 8), peaks=4, profile="mount", stack=0.9, mounts=("cloud", "mesa"))
reef_level(17, "Lantern Reef", 3, [GE, P, W], """
In the glowing deep: districts rising away from the cannon.""", weather="GlowingDeep",
           robots=(5, 7), peaks=4, profile="rise", stack=0.6, fixed=wheel())
reef_level(18, "Undertow Towers", 3, [GE, W, B], """
High currents over a fortress town, the highest districts at the back.""", weather="Undertow",
           robots=(6, 9), peaks=5, profile="rise", stack=0.7, walk=0.9)
reef_level(19, "The Drowned Forum", 3, [GE, B, B], """
The Dredger's outer town: five districts on reefs and pillars, dynamite at the gate.""", weather="MurkyDepths",
           robots=(6, 9), peaks=5, profile="mount", mounts=("cloud", "mesa"), stack=0.6,
           fixed=lambda L, rng: (L.barrel(5.6, G), L.sunflower(4.6, G, 1.1)))
reef_level(20, "Trench Fortress", 3, [GE, H, B], """
A fortress town above the trench, the highest districts at the back.""", weather="KelpForest",
           robots=(6, 9), peaks=5, profile="rise", stack=0.7, walk=0.9)
reef_level(21, "Dredger's Gate", 3, [GE, B, B], """
The last approach: a valley town in the murk, clams on the floor.""", weather="MurkyDepths",
           robots=(6, 9), peaks=5, profile="valley", stack=0.7, fixed=clam(4.8))


def _keep(L, rng):
    """The Deep Dredger's keep: a stone gate on a broad reef with a crooked timber tower he lands on."""
    s_ = w4.cloud(L, 13.4, AT(2.0), 3.4)
    top = L.post(12.2, s_, 1.5, stone=True); L.post(14.6, s_, 1.5, stone=True)
    r_ = L.plank(11.9, 14.9, top, stone=True)
    L.robot(13.4, s_, "semi")
    t = d.tower(L, 13.4, r_, 3, width=2.2, h=1.1, taper=0.2, robots=["pawn", None, "semi"], wonky=False)   # he lands on it
    L.robot(13.4, t, "boss")


# Boss level, two rooms: the Dredger's outer town first, then (room 2) his keep at the back of the city, where the
# mothership drops him off.
_keep_room = _pick(22, "The Deep Dredger", 3, [GE, B, B], "", "GlowingDeep", True, (6, 9), _keep, 0, 622000,
                   peaks=3, profile="rise", x0=5.8, x1=11.0, stack=0.6)
_town = _pick(22, "The Deep Dredger", 3, [GE, B, B], """
Boss: the Deep Dredger's outer town, then his keep at the back of his city (the mothership drops him on it).""",
              "GlowingDeep", True, (5, 7), None, 0, 522000, peaks=4, profile="mount", mounts=("cloud", "mesa"),
              stack=0.6, walk=0.9)
_town.add_room(_keep_room)
LEVELS.append(_town)

for _L in LEVELS: w4.fit_clouds(_L)


# ═══════════════════════════════════════════ output ═══════════════════════════════════════════════════════
def write_cs():
    d.prune_all(LEVELS)   # robots hidden behind a mound (2026-11-12)
    body = "\n\n".join(L.cs() for L in LEVELS)
    keep = ", ".join(f'"{L.file}"' for L in LEVELS)
    src = f"""// GENERATED by tools/design_world5.py — edit the designs there, not here.
// World 5 (Sunken City) levels W5_L01-W5_L22 (2026-11-12): the World 1-4 template under the sea — framed, live
// physics, robot-built sunken cities on coral reef mesas, two rooms per level, the sea contraptions, the Deep Dredger.

using UnityEditor;

public static partial class LevelDataGenerator
{{
    static void MakeWorld5Framed(string folder)
    {{
        // Drop any older World 5 asset that isn't one of these (the generated W05_* set had different names).
        var keep = new System.Collections.Generic.HashSet<string> {{ {keep} }};
        foreach (var guid in AssetDatabase.FindAssets("t:LevelData", new[] {{ folder }}))
        {{
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string file = System.IO.Path.GetFileNameWithoutExtension(path);
            if (file.StartsWith("W05_") && !keep.Contains(file)) AssetDatabase.DeleteAsset(path);
        }}

{body}
    }}
}}
"""
    path = os.path.join(d.UNITY, "Editor", "LevelDataGenerator.World5.cs")
    open(path, "w", encoding="utf-8").write(src)
    print("wrote", os.path.normpath(path))


if __name__ == "__main__":
    bad = 0
    for L in LEVELS:
        for msg in d.check(L) + w4.cloud_checks(L):
            print(f"{L.id}: {msg}"); bad += 1
    print(f"{len(LEVELS)} levels, {sum(len(L.blocks) for L in LEVELS)} blocks, {sum(len(L.robots) for L in LEVELS)} robots, {bad} issues")
    write_cs()
