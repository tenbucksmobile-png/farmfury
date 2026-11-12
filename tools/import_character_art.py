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
ART = sys.argv[1] if len(sys.argv) > 1 and not sys.argv[1].startswith('--') else os.path.join(
    os.path.dirname(ROOT), 'FarmFury_Technical', 'FarmFury_Artwork')
SRC = os.path.join(ART, 'Rough_Characters')
OUT = os.path.join(ROOT, 'unity', 'Assets', 'Sprites', 'Characters')

POSE_CANVAS = 500
POSE_CONTENT = 400

# animal folder in Unity -> {output pose name: source path under Rough_Characters}
POSES = {
    'Ducky':  {'Idle': 'Ducky/Idle.png', 'Loaded': 'Ducky/Loaded.png', 'in flight': 'Ducky/InFLight.png',
               'Impact': 'Ducky/Impact.png', 'Trigger': 'Ducky/Launch.png'},
    # Billy (2026-11-12, user: "loading, flight, hit images are not good and seem small"): unused renders from his folder -
    # a full-body leap facing right for flight (InFlight.png was a thin streak facing left), the braced crouch for
    # loaded, knocked head over heels for the hit (was standing with a halo).
    'Billy':  {'Idle': 'Billy/Billy_Front.png', 'Loaded': 'Billy/Launch(1).png',
               'in flight': 'Billy/kling_20260721_IMAGE_replicate__5562_0.png',
               'Impact': 'Billy/Defeat.png', 'Trigger': 'Billy/Trigger1.png'},
    # Gerald (2026-11-11, user: "Gerald - he faces the wrong way"): his flight art is mirrored to face right (the way
    # shots fly) and his loaded pose is a Kling side-on crouch (magenta background).
    'Gerald': {'Idle': 'Gerald/Idle.png', 'Loaded': 'Gerald/Gerald_LoadedSide.png', 'in flight': 'Gerald/inflight.png',
               'Impact': 'Gerald/Impact.png', 'Trigger': 'Gerald/ability.png'},
    # Percy (2026-11-12, user: "check percy loaded, flight and bounce"): the old flight / loaded poses were his face turned
    # on its side and a sideways spring-body. Flight = a full-body leap facing right, loaded = curled in his spin ring
    # (upright), the bounce roll = curled into a ball.
    'Percy':  {'Idle': 'Percy/Idle2.png', 'Impact': 'Percy/Impact.png', 'Loaded': 'Percy/Launch2.png',
               'in flight': 'Percy/kling_20260721_IMAGE_Farm_Fury__5548_0.png', 'Trigger': 'Percy/Trigger.png'},
    'Bessie': {'Idle': 'Bessie/Bessie_front.png'},
    'Woolly': {'Idle': 'Wooly/Wooly_front.png'},
}

# Horace: front render for Idle/Loaded (like Ducky's front-facing Loaded) + Kling-generated action poses.
HORACE = {
    'Idle': 'Horace/kling_20260909_IMAGE___Front_fa_624_0.png',
    # 2026-11-11: real cannon poses (Kling, magenta): curled up ready (left figure of a pose sheet, mirrored to face
    # right) and a launched flying leap.
    'Loaded': 'Horace/Horace_CannonPoses.png',
    'in flight': 'Horace/Horace_Flight.png',
}

# Sources on Kling's solid magenta (keyed with import_frozen_art.key_magenta instead of the white-background rule).
MAGENTA = {'Gerald/Gerald_LoadedSide.png', 'Horace/Horace_CannonPoses.png', 'Horace/Horace_Flight.png'}
# Mirrored so the pose faces right (the direction every shot flies).
FLIP = {'Gerald/inflight.png', 'Horace/Horace_CannonPoses.png'}
# Pose sheets: keep only the left-most figure.
LEFT_FIGURE = {'Horace/Horace_CannonPoses.png'}


WHITE = 235          # tools/remove_backgrounds.py's threshold: R,G,B all >= this is background white
SPECK_FRACTION = 0.01  # detached opaque bits smaller than this share of the largest piece are dropped
# Only these sources get speck removal — elsewhere small detached bits are wanted effects (dizzy stars, sparkles).
SPECKY = {'Horace/kling_20260909_IMAGE___Front_fa_624_0.png',   # has a render watermark under the hooves
          'Billy/kling_20260721_IMAGE_replicate__5562_0.png',     # render watermark bottom-right
          'Percy/kling_20260721_IMAGE_Farm_Fury__5548_0.png'}


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


# Keep only the largest opaque piece: drops a baked-in ground shadow from a mid-air pose (and any specks).
LARGEST_ONLY = {'Percy/kling_20260721_IMAGE_Farm_Fury__5548_0.png'}


def largest_only(im):
    # The ground shadow (mid grey, unsaturated) can touch the feet; clear it from the lowest fifth of the figure first.
    a = np.asarray(im).copy()
    rgb = a[:, :, :3].astype(int)
    rows = np.nonzero(a[:, :, 3].max(axis=1) > 20)[0]
    if len(rows):
        y0 = rows[-1] - (rows[-1] - rows[0]) // 5
        grey = (rgb.max(axis=2) - rgb.min(axis=2) < 28) & (rgb.max(axis=2) > 80)
        grey[:y0] = False
        a[:, :, 3] = np.where(grey, 0, a[:, :, 3])
    im = Image.fromarray(a, 'RGBA')
    a = np.asarray(im)
    labels, n = ndimage.label(a[:, :, 3] > 20)
    if n <= 1:
        return im
    sizes = ndimage.sum(np.ones_like(labels), labels, range(1, n + 1))
    a = a.copy()
    a[:, :, 3] = np.where(labels == 1 + int(np.argmax(sizes)), a[:, :, 3], 0)
    return Image.fromarray(a, 'RGBA')


def left_figure(im):
    a = np.asarray(im)
    labels, n = ndimage.label(a[:, :, 3] > 20)
    if n <= 1:
        return im
    sizes = ndimage.sum(np.ones_like(labels), labels, range(1, n + 1))
    big = [i + 1 for i in range(n) if sizes[i] >= sizes.max() * 0.2]
    first = min(big, key=lambda k: np.nonzero(labels == k)[1].mean())
    a = a.copy()
    a[:, :, 3] = np.where(labels == first, a[:, :, 3], 0)
    return Image.fromarray(a, 'RGBA')


def prepare(path, specks=False, rel=''):
    im = Image.open(path)
    has_alpha = im.mode == 'RGBA' and im.getchannel('A').getextrema()[0] < 250
    if rel in MAGENTA:
        from import_frozen_art import key_magenta
        im = Image.fromarray(key_magenta(path), 'RGBA')
        specks = True
    else:
        im = im.convert('RGBA') if has_alpha else remove_white_bg(im)
    if rel in LEFT_FIGURE:
        im = left_figure(im)
    if rel in LARGEST_ONLY:
        im = largest_only(im)
    if rel in FLIP:
        im = im.transpose(Image.FLIP_LEFT_RIGHT)
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
    only = sys.argv[sys.argv.index('--only') + 1].split(',') if '--only' in sys.argv else None
    jobs = dict(POSES)
    jobs['Horace'] = dict(HORACE)
    for extra in ('Impact', 'Trigger'):
        p = os.path.join(SRC, 'Horace', f'Horace_{extra.replace(" ", "")}.png')
        if os.path.exists(p):
            jobs['Horace'][extra] = os.path.relpath(p, SRC)
    for animal, poses in jobs.items():
        if only and animal not in only:
            continue
        folder = os.path.join(OUT, animal)
        os.makedirs(folder, exist_ok=True)
        for pose, rel in poses.items():
            prepare(os.path.join(SRC, rel), specks=rel in SPECKY, rel=rel).save(os.path.join(folder, f'{pose}.png'))
        print(f'{animal}: {", ".join(poses)}')


if __name__ == '__main__':
    main()
