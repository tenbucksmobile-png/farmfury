"""
Farm Fury - World 7 (Dustbowl Canyon, paid) level designer (2026-11-12).

The World 1-6 template in the red-rock desert (see tools/design_paid.py for the shared kit): robot-built frontier towns
on canyon mesas - mine headframes, water towers, two-storey saloons, sandstone arches - walkways strung between them,
the saguaro that fires a volley of spines when hit, a tumbleweed that rolls into the town's foot, dynamite. Two rooms
per level; level 12 is the Drill Rig's quarry: the outer works, then his keep (the mothership drops him on it).
Any animal can lead (a paid world: the whole roster is unlocked by then).

Usage (repo root):  python tools/design_world7.py
Writes unity/Assets/Editor/LevelDataGenerator.World7.cs; then FarmFury -> Generate All Level Data.
"""
import design_paid as p
from design_paid import AT, G, BI, GE, H, D, P, W, C, B

p.use_world(7, ("MesaLow", "MesaMid", "MesaTall"))
w = p.World(7, "Dustbowl Canyon")
cactus = lambda *xs: p.emitter(1.4, *xs)
weed = p.roller

w.level(1, "Dusty Corral", 2, [C, B, H], """
A frontier town climbing three mesas, a saguaro on guard out front.""", "HighNoon",
        robots=(4, 5), peaks=3, profile="rise", stack=0.3, walk=0.5, x0=6.4, x1=13.6, fixed=cactus(4.7))
w.level(2, "Sunbaked Gulch", 3, [H, C, B], """
Four mesas in the sun, headframes and water towers, a tumbleweed ready to roll.""", "HeatHaze",
        robots=(4, 6), peaks=4, profile="random", stack=0.4, walk=0.8, fixed=weed())
w.level(3, "Mesa Pass", 3, [GE, B, D], """
The tallest mesa in the middle, saloons stacked on saloons.""", "DesertDusk",
        robots=(5, 7), peaks=4, profile="mount", stack=0.6, fixed=cactus(4.6, 5.5))
w.level(4, "Sandstorm Saloon", 3, [P, H, C], """
A town falling away from a high lookout, half lost in the sandstorm.""", "Sandstorm",
        robots=(5, 7), peaks=4, profile="fall", walk=1.0, fixed=p.dynamite(5.4))
w.level(5, "Mesa Mine", 3, [BI, W, B], """
Headframes over the mine shafts, towers on towers.""", "HighNoon",
        robots=(5, 7), peaks=4, profile="valley", stack=0.9, fixed=weed())
w.level(6, "Tumbleweed Gulch", 3, [D, C, H], """
Mesas and sandstone pillars carry the town, walkways from roof to roof.""", "HeatHaze",
        robots=(5, 8), peaks=4, profile="random", mounts=("cloud", "mesa"), walk=1.0, fixed=p.both(weed(4.6), cactus(5.8)))
w.level(7, "Canyon Night", 3, [GE, P, B], """
Five districts under the canyon stars.""", "CanyonNight",
        robots=(5, 8), peaks=5, profile="random", stack=0.5)
w.level(8, "Gold Rush", 3, [H, BI, C], """
A valley town with the dynamite stacked at its heart.""", "DesertDusk",
        robots=(6, 8), peaks=4, profile="valley", fixed=p.dynamite(10.6, 11.2))
w.level(9, "Rattlesnake Ridge", 3, [W, D, B], """
A skyline shaped like a mountain, crowned with water towers.""", "HighNoon",
        robots=(6, 8), peaks=4, profile="mount", stack=0.8, fixed=p.both(cactus(5.0), weed(4.4)))
w.level(10, "Dust Devil", 3, [B, GE, H], """
Five districts crammed into the gulch while the sandstorm howls.""", "Sandstorm",
        robots=(5, 9), peaks=5, profile="valley", stack=0.6, fixed=weed())
w.level(11, "Boomtown", 3, [BI, P, C], """
Half on the mesas, half on the canyon floor, stacked high and bridged end to end.""", "CanyonNight",
        robots=(5, 9), peaks=5, profile="random", ground=0.35, stack=0.8, walk=0.9, fixed=cactus(4.7, 5.6))

w.boss_level(12, "The Drill Rig's Quarry", 3, [B, H, GE], """
Boss: the Drill Rig's outer works, then his keep at the head of the quarry (the mothership drops him on it).""",
             "DesertDusk", p.keep_tower("water_tower"),
             town_kw=dict(peaks=4, profile="mount", mounts=("cloud", "mesa"), stack=0.6, walk=0.9),
             keep_kw=dict(peaks=2, profile="rise", x0=6.8, x1=11.0, stack=0.5))


if __name__ == "__main__":
    w.report()
    w.write_cs("World 7 (Dustbowl Canyon, paid) levels W7_L01-W7_L12 (2026-11-12): frontier towns on canyon mesas, "
               "two rooms per level, saguaros / tumbleweeds / dynamite, the Drill Rig.")
