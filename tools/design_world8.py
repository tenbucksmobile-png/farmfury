"""
Farm Fury - World 8 (Harvest Fairground, paid) level designer (2026-11-12).

The World 1-6 template at the robots' harvest fair (see tools/design_paid.py for the shared kit): robot-built midways on
striped circus podiums - carousels on one pole, high strikers with a stick of dynamite for a bell, prize-cushion
pyramids, tents pitched on ticket booths - walkways between them, the popcorn cart that blasts popcorn when hit, a
pumpkin that rolls into the midway. Two rooms per level; level 12 is the Ringmaster's big top: the outer midway, then his
keep (the mothership drops him on it).

Usage (repo root):  python tools/design_world8.py
Writes unity/Assets/Editor/LevelDataGenerator.World8.cs; then FarmFury -> Generate All Level Data.
"""
import design_paid as p
from design_paid import AT, G, BI, GE, H, D, P, W, C, B

p.use_world(8, ("PodiumLow", "PodiumMid", "PodiumTall"))
w = p.World(8, "Harvest Fairground")
cart = lambda *xs: p.emitter(1.2, *xs)
pumpkin = p.roller

w.level(1, "Opening Night", 2, [P, C, B], """
The fair opens: a midway on three podiums, a popcorn cart by the gate.""", "FairDay",
        robots=(4, 5), peaks=3, profile="rise", stack=0.3, walk=0.5, x0=6.4, x1=13.6, fixed=cart(4.7))
w.level(2, "Carousel Row", 3, [W, P, C], """
Carousels and booths stepping up and down the midway, a pumpkin ready to roll.""", "GoldenHour",
        robots=(4, 6), peaks=4, profile="random", stack=0.4, walk=0.8, fixed=pumpkin())
w.level(3, "Ring the Bell", 3, [H, B, C], """
High strikers with dynamite bells - hit the bell and the whole booth goes.""", "FairDay",
        robots=(5, 7), peaks=4, profile="mount", stack=0.5, fixed=cart(4.6))
w.level(4, "Prize Pyramid", 3, [D, GE, C], """
Prize cushions stacked to the sky, the midway falling away from a high stand.""", "HarvestMoon",
        robots=(5, 7), peaks=4, profile="fall", walk=1.0, fixed=p.dynamite(5.4))
w.level(5, "Big Top Alley", 3, [BI, B, W], """
Tents pitched on tents, towers on towers.""", "CarnivalNight",
        robots=(5, 7), peaks=4, profile="valley", stack=0.9, fixed=pumpkin())
w.level(6, "Hall of Mirrors", 3, [GE, P, H], """
Podiums and carousel columns carry the fair, walkways from roof to roof.""", "FairDrizzle",
        robots=(5, 8), peaks=4, profile="random", mounts=("cloud", "mesa"), walk=1.0, fixed=p.both(pumpkin(4.6), cart(5.8)))
w.level(7, "Fireworks Finale", 3, [C, D, B], """
Five midway stands under the fireworks.""", "CarnivalNight",
        robots=(5, 8), peaks=5, profile="random", stack=0.5)
w.level(8, "Coconut Shy", 3, [H, W, C], """
A valley midway with the dynamite stacked by the shooting gallery.""", "GoldenHour",
        robots=(6, 8), peaks=4, profile="valley", fixed=p.dynamite(10.6, 11.2))
w.level(9, "Harvest Moon Fair", 3, [P, BI, B], """
A skyline shaped like a mountain, crowned with carousels.""", "HarvestMoon",
        robots=(6, 8), peaks=4, profile="mount", stack=0.8, fixed=p.both(cart(5.0), pumpkin(4.4)))
w.level(10, "Rainy Midway", 3, [B, GE, D], """
Five stands crammed together in the drizzle.""", "FairDrizzle",
        robots=(5, 9), peaks=5, profile="valley", stack=0.6, fixed=pumpkin())
w.level(11, "Grand Parade", 3, [BI, H, C], """
Half on the podiums, half on the fairground, stacked high and bridged end to end.""", "CarnivalNight",
        robots=(5, 9), peaks=5, profile="random", ground=0.35, stack=0.8, walk=0.9, fixed=cart(4.7, 5.6))

w.boss_level(12, "The Ringmaster's Big Top", 3, [B, P, H], """
Boss: the Ringmaster's outer midway, then his keep under the big top (the mothership drops him on it).""",
             "CarnivalNight", p.keep_tower("big_top"),
             town_kw=dict(peaks=4, profile="mount", mounts=("cloud", "mesa"), stack=0.6, walk=0.9),
             keep_kw=dict(peaks=2, profile="rise", x0=6.8, x1=11.0, stack=0.5))


if __name__ == "__main__":
    w.report()
    w.write_cs("World 8 (Harvest Fairground, paid) levels W8_L01-W8_L12 (2026-11-12): robot midways on circus podiums, "
               "two rooms per level, popcorn carts / pumpkins / dynamite, the Ringmaster.")
