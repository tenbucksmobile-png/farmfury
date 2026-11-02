"""Copy FarmFury_Stampede's per-world art into the FarmFury Unity project (Worlds 3-9).

    python tools/import_stampede_worlds.py [path/to/FarmFury_Stampede]

Writes (all gitignored, like every sprite in this project):
  unity/Assets/Sprites/Environment/Skies/World<N>_Backdrop.png   <- Stampede <XX>_Parallax_Far.png (opaque)
  unity/Assets/Sprites/Environment/World<N>Props/<Name>.png      <- props, cropped to their visible pixels
  unity/Assets/Sprites/Enemies/Robot/W<N>/<Tier>_left|right.png  <- robots, cropped and resized
  unity/Assets/Sprites/Characters/<Name>/*.png                   <- stand-in poses for Ducky/Horace/Gerald/Billy
  unity/Assets/Sprites/UI/Franchise/ThumbsUp/<Name>.png          <- thumbs-up portraits for the in-game HUD queue

Robots follow FarmFury's own robot-art convention so every World 1 tower's robot scales carry over unchanged:
a tight-cropped sprite 256 px tall (bosses 447 px, Commander.png's visible height), imported at PPU 1746 by
SceneSetup. "_left" faces the cannon (FarmFury's _robotSprite); a missing side is mirrored from the other.
Tiers map onto FarmFury's RobotType: Basic, Harvester, SemiHarvester, Boss (= RobotType.Commander).
Which prop is a big background structure vs ground clutter vs a sky prop lives in SceneSetup.WorldPropSets.
"""
import os
import sys
from PIL import Image, ImageOps

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
UNITY = os.path.join(ROOT, 'unity', 'Assets', 'Sprites')
STAMPEDE = sys.argv[1] if len(sys.argv) > 1 else os.path.join(os.path.dirname(ROOT), 'FarmFury_Stampede')
SP = os.path.join(STAMPEDE, 'Assets', '_Project', 'Sprites')

TIER_HEIGHT = 256
BOSS_HEIGHT = 447
PROP_MAX = 1024

# world -> (backdrop file under Sprites/Environment, prop folder under Sprites/UI, props, robots)
# robots: tier -> (left source, right source); either may be None (mirrored from the other).
G = 'Robots'
WORLDS = {
    3: ('WV_Parallax_Far.png', 'WatermillVillage/Props',
        ['Cottage', 'WaterWheel', 'WeepingWillow', 'OldWell', 'StoneArch', 'StoneBridge',
         'FlowerBasket', 'Millstone', 'MossyLog', 'RiverReed', 'WoodenCrate', 'Wooddock'],
        {'Basic': (None, 'UI/WatermillVillage/WaterRobot.png'),
         'Harvester': (f'{G}/Robot_Harvest_left.png', f'{G}/Robot_Harvest_right.png'),
         'SemiHarvester': (f'{G}/ScoutRobot_left.png', f'{G}/ScoutRobot_right.png'),
         'Boss': ('UI/WatermillVillage/MilestoneMaulerLeft.png', 'UI/WatermillVillage/MileStoneMauler.png')}),
    4: ('SI_Parallax_Far.png', 'SkyIsland',
        ['GiantMushroom', 'SkyTemple', 'StonePillar', 'Windvane', 'Cloud', 'AnchorCloud',
         'AirBalloon', 'Kite', 'HangingLantern'],
        {'Basic': (f'{G}/DriftRobot_left.png', f'{G}/DriftRobot_right.png'),
         'Harvester': (f'{G}/Robot_Harvest_left.png', f'{G}/Robot_Harvest_right.png'),
         'SemiHarvester': (f'{G}/Drone.png', None),
         'Boss': ('UI/SkyIsland/StormBaron.png', 'UI/SkyIsland/StormBaron_Right.png')}),
    5: ('SC_Parallax_Far.png', 'SunkenCity',
        ['SunkenArchway', 'SunkenPillar', 'SunkenStatue', 'Anchor', 'Submarine', 'Kelp', 'CoralFormation',
         'Floral', 'Clam', 'TreasureChest', 'AirVent', 'ShipsWheel', 'Fish_1', 'Fish_2', 'Fish_3', 'Fish_4',
         'Fish_5', 'Fish_6'],
        {'Basic': ('UI/SunkenCity/Shrimp.png', 'UI/SunkenCity/Shrimp_Right.png'),
         'Harvester': (None, 'UI/SunkenCity/Crab.png'),
         'SemiHarvester': ('UI/SunkenCity/JellyFish.png', 'UI/SunkenCity/JellyFish.png'),
         'Boss': ('UI/SunkenCity/DeepDredger_Left.png', 'UI/SunkenCity/DeepDredger.png')}),
    6: ('RM_Parallax_Far.png', 'MotherShip',
        ['ControlTower', 'SateliteDish', 'WarningBeacon', 'ContainmentPod', 'EnergyShield', 'SteelGirder',
         'PlasmaConduit', 'LaserEmitter', 'RocketBooster', 'CircuitPanel', 'Ship'],
        {'Basic': ('UI/MotherShip/PatrolRobot_left.png', 'UI/MotherShip/PatrolRobot_right.png'),
         'Harvester': ('UI/MotherShip/HarvestorRobot_left.png', 'UI/MotherShip/HarvestorRobot_right.png'),
         'SemiHarvester': ('UI/MotherShip/Drone.png', None),
         'Boss': ('UI/MotherShip/FActoryMachine_Left.png', 'UI/MotherShip/FActoryMachine.png')}),
    7: ('DC_Parallax_Far.png', 'DustbowlCanyon',
        ['RanchHouse', 'WaterTower', 'Windpump', 'DeadTree', 'Saguaro', 'SignPost',
         'Boulder', 'Tumbleweed', 'WaterBarrel', 'MineCart', 'Wagon', 'Crate'],
        {'Basic': ('UI/DustbowlCanyon/Scout_left.png', 'UI/DustbowlCanyon/Scout_right.png'),
         'Harvester': ('UI/DustbowlCanyon/Harvester_left.png', 'UI/DustbowlCanyon/Harvester_right.png'),
         'SemiHarvester': ('UI/DustbowlCanyon/Drone.png', None),
         'Boss': ('UI/DustbowlCanyon/DrillRig_Left.png', 'UI/DustbowlCanyon/DrillRig_Right.png')}),
    8: ('HF_Parallax_Far.png', 'HarvestFairground',
        ['BigTop', 'FerrisWheel', 'Carousel', 'HelterSkelter', 'GameStall', 'LampPost',
         'PopcornCart', 'Pumpkin', 'PrizeCrates', 'CandyApple'],
        {'Basic': ('UI/HarvestFairground/Scout_left.png', 'UI/HarvestFairground/Scout_right.png'),
         'Harvester': ('UI/HarvestFairground/Harvester_left.png', 'UI/HarvestFairground/Harvester_right.png'),
         'SemiHarvester': ('UI/HarvestFairground/Drone.png', None),
         'Boss': ('UI/HarvestFairground/Ringmaster_Left.png', 'UI/HarvestFairground/Ringmaster_Right.png')}),
    9: ('CF_Parallax_Far.png', 'CropFactory',
        ['GrainSilo', 'ProcessingPlant', 'CogMachine', 'Hopper', 'RobotArm', 'SignalPost',
         'ChemDrum', 'CornCan', 'CrateStack', 'CropCrate', 'Gearbox', 'Forklift', 'SteelCrate'],
        {'Basic': ('UI/CropFactory/Scout_left.png', 'UI/CropFactory/Scout_right.png'),
         'Harvester': ('UI/CropFactory/Harvester_left.png', 'UI/CropFactory/Harvester_right.png'),
         'SemiHarvester': ('UI/CropFactory/Drone.png', None),
         'Boss': ('UI/CropFactory/HarvesterPrime_Left.png', 'UI/CropFactory/HarvesterPrime_Right.png')}),
}


# Stand-in cannon poses for the four animals FarmFury has no art for, from Stampede's platformer frames
# (right-facing: the cannon fires right). Written as Characters/<Name>/Loaded.png, "in flight.png" and
# Trigger.png, the names SpriteWiring looks for; fitted into FarmFury's 500x500 pose canvas with the art
# ~380 px across, like Cluck/Bessie/Percy/Woolly, so SpriteWiring's per-animal PPU sizes them the same.
CHARACTERS = {
    'Ducky':  {'Loaded': 'Ducky_StillStanding.png', 'in flight': 'Ducky_right.png',  'Trigger': 'Ducky_right.png'},
    'Horace': {'Loaded': 'Horace_right.png',        'in flight': 'Horace_right1.png', 'Trigger': 'Horace_right1.png'},
    'Gerald': {'Loaded': 'Gerald_right.png',        'in flight': 'Gerald_Jump.png',   'Trigger': 'Gerald_Right1.png'},
    'Billy':  {'Loaded': 'Billy_right.png',         'in flight': 'Billy_Sling.png',   'Trigger': 'Billy_right_ram1.png'},
}
# In-game HUD animal queue portraits (Stampede UI/ThumbsUp), renamed to FarmFury's animal names.
THUMBS_UP = {'Cluck': 'CluckThumbsUp.png', 'Bessie': 'BessieThumbsUp.png', 'Percy': 'PercyThumbsup.png',
             'Woolly': 'WoollyThumbsUp.png', 'Ducky': 'DuckyThumbsUp.png', 'Horace': 'HoraceThumbsUp.png',
             'Gerald': 'GeraldThumbsUp.png', 'Billy': 'BillyThumbsUp.png'}
POSE_CANVAS = 500
POSE_CONTENT = 380


def pose(rel):
    im = load(f'Characters/{rel}')
    im.thumbnail((POSE_CONTENT, POSE_CONTENT), Image.LANCZOS)
    scale = POSE_CONTENT / max(im.size)
    if scale > 1:
        im = im.resize((round(im.width * scale), round(im.height * scale)), Image.LANCZOS)
    canvas = Image.new('RGBA', (POSE_CANVAS, POSE_CANVAS), (0, 0, 0, 0))
    canvas.alpha_composite(im, ((POSE_CANVAS - im.width) // 2, (POSE_CANVAS - im.height) // 2))
    return canvas


def crop_visible(im):
    im = im.convert('RGBA')
    bbox = im.getchannel('A').point(lambda a: 255 if a > 20 else 0).getbbox()
    return im.crop(bbox) if bbox else im


def load(rel):
    path = os.path.join(SP, rel)
    if not os.path.exists(path):
        raise SystemExit(f'missing Stampede art: {path}')
    return crop_visible(Image.open(path))


def fit_height(im, h):
    return im.resize((max(1, round(im.width * h / im.height)), h), Image.LANCZOS)


def save(im, path):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    im.save(path)


def main():
    for world, (backdrop, prop_dir, props, robots) in WORLDS.items():
        bd = Image.open(os.path.join(SP, 'Environment', backdrop)).convert('RGB')
        save(bd, os.path.join(UNITY, 'Environment', 'Skies', f'World{world}_Backdrop.png'))

        for name in props:
            im = load(f'UI/{prop_dir}/{name}.png')
            im.thumbnail((PROP_MAX, PROP_MAX), Image.LANCZOS)
            save(im, os.path.join(UNITY, 'Environment', f'World{world}Props', f'{name}.png'))

        for tier, (left, right) in robots.items():
            h = BOSS_HEIGHT if tier == 'Boss' else TIER_HEIGHT
            l = fit_height(load(left), h) if left else None
            r = fit_height(load(right), h) if right else None
            l = l or ImageOps.mirror(r)
            r = r or ImageOps.mirror(l)
            out = os.path.join(UNITY, 'Enemies', 'Robot', f'W{world}')
            save(l, os.path.join(out, f'{tier}_left.png'))
            save(r, os.path.join(out, f'{tier}_right.png'))
        print(f'World {world}: backdrop, {len(props)} props, {len(robots)} robot tiers')

    for name, src in THUMBS_UP.items():
        save(Image.open(os.path.join(SP, 'UI', 'ThumbsUp', src)).convert('RGBA'),
             os.path.join(UNITY, 'UI', 'Franchise', 'ThumbsUp', f'{name}.png'))
    print(f'{len(THUMBS_UP)} thumbs-up portraits')

    for name, poses in CHARACTERS.items():
        folder = os.path.join(UNITY, 'Characters', name)
        if os.path.isdir(folder) and any(f.lower().endswith('.png') for f in os.listdir(folder)):
            print(f'{name}: art already present, left alone')
            continue
        for out_name, src in poses.items():
            save(pose(src), os.path.join(folder, f'{out_name}.png'))
        print(f'{name}: {len(poses)} stand-in poses')


if __name__ == '__main__':
    main()
