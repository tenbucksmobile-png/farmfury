"""Copy the hand-made cannon-game poses from FarmFury_Artwork into the Unity project.

    python tools/import_character_art.py [path/to/FarmFury_Artwork]

Source: FarmFury_Technical/FarmFury_Artwork/Rough_Characters/<Animal>/ (Kling renders, mostly 2048x2048 on white).
Each pose has its white background flood-filled away from the edges (tools/remove_backgrounds.py's method, so
white inside the character survives), is cropped to its visible pixels and centred on FarmFury's 500x500 pose
canvas at POSE_CONTENT px across — the same framing as Cluck/Bessie's art, so SpriteWiring's per-animal PPU
sizes them the same. Output names are the ones SpriteWiring matches: Idle, Loaded, "in flight", Impact, Trigger.

Replaces the Stampede platformer stand-ins tools/import_stampede_worlds.py wrote for Ducky/Gerald/Billy (that
script leaves a folder alone once it has art). Horace has no cannon poses here; his come from Kling
(see docs/ART_REQUIREMENTS.md) and are listed under HORACE once generated.
"""
import os
import sys
import numpy as np
from PIL import Image
from scipy import ndimage

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ART = sys.argv[1] if len(sys.argv) > 1 else os.path.join(
    os.path.dirname(ROOT), 'FarmFury_Technical', 'FarmFury_Artwork')
SRC = os.path.join(ART, 'Rough_Characters')
OUT = os.path.join(ROOT, 'unity', 'Assets', 'Sprites', 'Characters')

POSE_CANVAS = 500
POSE_CONTENT = 400

# animal folder in Unity -> {output pose name: source path under Rough_Characters}
POSES = {
    'Ducky':  {'Idle': 'Ducky/Idle.png', 'Loaded': 'Ducky/Loaded.png', 'in flight': 'Ducky/InFLight.png',
               'Impact': 'Ducky/Impact.png', 'Trigger': 'Ducky/Launch.png'},
    'Billy':  {'Idle': 'Billy/Billy_Front.png', 'Loaded': 'Billy/Loaded.png', 'in flight': 'Billy/InFlight.png',
               'Impact': 'Billy/Impact.png', 'Trigger': 'Billy/Trigger1.png'},
    'Gerald': {'Idle': 'Gerald/Idle.png', 'Loaded': 'Gerald/Launch.png', 'in flight': 'Gerald/inflight.png',
               'Impact': 'Gerald/Impact.png', 'Trigger': 'Gerald/ability.png'},
    'Percy':  {'Idle': 'Percy/Idle2.png', 'Impact': 'Percy/Impact.png'},
    'Bessie': {'Idle': 'Bessie/Bessie_front.png'},
    'Woolly': {'Idle': 'Wooly/Wooly_front.png'},
}

# Horace: front render for Idle/Loaded (like Ducky's front-facing Loaded) + Kling-generated action poses.
HORACE = {
    'Idle': 'Horace/kling_20260909_IMAGE___Front_fa_624_0.png',
    'Loaded': 'Horace/kling_20260909_IMAGE___Front_fa_624_0.png',
}


WHITE = 235          # tools/remove_backgrounds.py's threshold: R,G,B all >= this is background white
SPECK_FRACTION = 0.01  # detached opaque bits smaller than this share of the largest piece are dropped
# Only these sources get speck removal — elsewhere small detached bits are wanted effects (dizzy stars, sparkles).
SPECKY = {'Horace/kling_20260909_IMAGE___Front_fa_624_0.png'}   # has a render watermark under the hooves


def remove_white_bg(im):
    """Same rule as tools/remove_backgrounds.py (near-white connected to the image border becomes transparent,
    so white inside the character survives), done with scipy labelling instead of a Python flood fill."""
    rgb = np.asarray(im.convert('RGB'))
    white = (rgb >= WHITE).all(axis=2)
    labels, _ = ndimage.label(white)
    edge = np.unique(np.concatenate([labels[0], labels[-1], labels[:, 0], labels[:, -1]]))
    background = np.isin(labels, edge[edge > 0])
    alpha = np.where(background, 0, 255).astype(np.uint8)
    return Image.fromarray(np.dstack([rgb, alpha]), 'RGBA')


def drop_specks(im):
    """Removes small detached pieces (render watermarks, stray dust) that would widen the crop."""
    a = np.asarray(im)
    labels, n = ndimage.label(a[:, :, 3] > 20)
    if n <= 1:
        return im
    sizes = ndimage.sum(np.ones_like(labels), labels, range(1, n + 1))
    keep = np.isin(labels, 1 + np.nonzero(sizes >= sizes.max() * SPECK_FRACTION)[0])
    a = a.copy()
    a[:, :, 3] = np.where(keep, a[:, :, 3], 0)
    return Image.fromarray(a, 'RGBA')


def prepare(path, specks=False):
    im = Image.open(path)
    has_alpha = im.mode == 'RGBA' and im.getchannel('A').getextrema()[0] < 250
    im = im.convert('RGBA') if has_alpha else remove_white_bg(im)
    if specks:
        im = drop_specks(im)
    bbox = im.getchannel('A').point(lambda a: 255 if a > 20 else 0).getbbox()
    if bbox:
        im = im.crop(bbox)
    scale = POSE_CONTENT / max(im.size)
    im = im.resize((max(1, round(im.width * scale)), max(1, round(im.height * scale))), Image.LANCZOS)
    canvas = Image.new('RGBA', (POSE_CANVAS, POSE_CANVAS), (0, 0, 0, 0))
    canvas.alpha_composite(im, ((POSE_CANVAS - im.width) // 2, (POSE_CANVAS - im.height) // 2))
    return canvas


def main():
    jobs = dict(POSES)
    jobs['Horace'] = dict(HORACE)
    for extra in ('in flight', 'Impact', 'Trigger'):
        p = os.path.join(SRC, 'Horace', f'Horace_{extra.replace(" ", "")}.png')
        if os.path.exists(p):
            jobs['Horace'][extra] = os.path.relpath(p, SRC)
    for animal, poses in jobs.items():
        folder = os.path.join(OUT, animal)
        os.makedirs(folder, exist_ok=True)
        for pose, rel in poses.items():
            prepare(os.path.join(SRC, rel), specks=rel in SPECKY).save(os.path.join(folder, f'{pose}.png'))
        print(f'{animal}: {", ".join(poses)}')


if __name__ == '__main__':
    main()
