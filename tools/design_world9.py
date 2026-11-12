"""
Farm Fury - World 9 (Crop Factory, paid) level designer (2026-11-12).

The World 1-6 template inside the robots' crop factory (see tools/design_paid.py for the shared kit): robot-built
production lines on machine housings - conveyors with crates riding them, hydraulic presses held over robots on thin
legs, stacked steel crates, lopsided crane arms - walkways between them, the corn hopper that sprays kernels when hit, a
cog that rolls into the line. Two rooms per level; level 12 is the Harvester Prime's control floor: the outer line,
then his keep (the mothership drops him on it). The last world: the biggest robot counts.

Usage (repo root):  python tools/design_world9.py
Writes unity/Assets/Editor/LevelDataGenerator.World9.cs; then FarmFury -> Generate All Level Data.
"""
import design_paid as p
from design_paid import AT, G, BI, GE, H, D, P, W, C, B

p.use_world(9, ("HousingLow", "HousingMid", "HousingTall"))
w = p.World(9, "Crop Factory")
hopper = lambda *xs: p.emitter(1.5, *xs)
cog = p.roller

w.level(1, "Clocking In", 2, [GE, C, B], """
The first shift: a production line over three machines, a corn hopper by the door.""", "ShiftStart",
        robots=(4, 6), peaks=3, profile="rise", stack=0.3, walk=0.5, x0=6.4, x1=13.6, fixed=hopper(4.7))
w.level(2, "Conveyor Belt", 3, [BI, H, C], """
Conveyors and presses stepping up and down the line, a cog ready to roll.""", "SteamWorks",
        robots=(5, 6), peaks=4, profile="random", stack=0.4, walk=0.8, fixed=cog())
w.level(3, "Hydraulic Press", 3, [B, D, P], """
Presses stacked on presses, the tallest machine in the middle.""", "Overdrive",
        robots=(5, 7), peaks=4, profile="mount", stack=0.6, fixed=hopper(4.6, 5.5))
w.level(4, "Packing Floor", 3, [W, GE, C], """
A line falling away from a high gantry, crates everywhere.""", "ShiftStart",
        robots=(5, 7), peaks=4, profile="fall", walk=1.0, fixed=p.dynamite(5.4))
w.level(5, "Night Shift", 3, [H, BI, B], """
Towers on towers in the dark.""", "NightShift",
        robots=(6, 8), peaks=4, profile="valley", stack=0.9, fixed=cog())
w.level(6, "Steam Valve", 3, [D, P, C], """
Housings and pipe columns carry the line, walkways from roof to roof.""", "SteamWorks",
        robots=(6, 8), peaks=4, profile="random", mounts=("cloud", "mesa"), walk=1.0, fixed=p.both(cog(4.6), hopper(5.8)))
w.level(7, "Overtime", 3, [GE, W, B], """
Five stations crammed onto the floor.""", "Overtime",
        robots=(6, 9), peaks=5, profile="random", stack=0.5)
w.level(8, "Canning Line", 3, [C, H, B], """
A valley line with the dynamite stacked by the canning machine.""", "Overdrive",
        robots=(6, 9), peaks=4, profile="valley", fixed=p.dynamite(10.6, 11.2))
w.level(9, "Crane Bay", 3, [BI, D, P], """
A skyline shaped like a mountain, crane arms reaching over it.""", "NightShift",
        robots=(6, 9), peaks=4, profile="mount", stack=0.8, fixed=p.both(hopper(5.0), cog(4.4)))
w.level(10, "Meltdown", 3, [B, GE, H], """
Five stations in overdrive, the floor thick with steam.""", "Overdrive",
        robots=(7, 9), peaks=5, profile="valley", stack=0.6, fixed=cog())
w.level(11, "Assembly Hall", 3, [W, BI, C], """
Half on the machines, half on the floor, stacked high and bridged end to end.""", "Overtime",
        robots=(7, 9), peaks=5, profile="random", ground=0.35, stack=0.8, walk=0.9, fixed=hopper(4.7, 5.6))

w.boss_level(12, "The Harvester Prime", 3, [B, BI, H], """
Boss: the Harvester Prime's outer line, then his keep on the control floor (the mothership drops him on it).""",
             "Overdrive", p.keep_tower("press"),
             town_kw=dict(peaks=4, profile="mount", mounts=("cloud", "mesa"), stack=0.6, walk=0.9),
             keep_kw=dict(peaks=2, profile="rise", x0=6.8, x1=11.0, stack=0.5))


if __name__ == "__main__":
    w.report()
    w.write_cs("World 9 (Crop Factory, paid) levels W9_L01-W9_L12 (2026-11-12): robot production lines on machine "
               "housings, two rooms per level, corn hoppers / cogs / dynamite, the Harvester Prime.")
