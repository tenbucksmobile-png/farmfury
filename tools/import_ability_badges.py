"""
Farm Fury - ability badge icons for the Character Select cards (2026-11-08).

Writes unity/Assets/Sprites/UI/Franchise/Abilities/<Animal>.png (wired into MenuArt.abilityBadges by Wire Scene
References). Five reuse existing effect art from the franchise projects; Woolly, Ducky and Billy are Kling icons
(FarmFury_Technical/FarmFury_Artwork/Rough_Icons/Abilities/, white background cut out here).

Usage (repo root):  python tools/import_ability_badges.py
"""

import os
import numpy as np
from PIL import Image

from import_cornfield_art import cut_white, trim, DESKTOP

OUT = os.path.join(os.path.dirname(__file__), "..", "unity", "Assets", "Sprites", "UI", "Franchise", "Abilities")
STAMPEDE = os.path.join(DESKTOP, "FarmFury_Stampede", "Assets", "_Project", "Sprites", "Characters")
ARCADE = os.path.join(DESKTOP, "FarmFury_Arcade", "Assets", "_Project", "Sprites", "Characters")
KLING = os.path.join(DESKTOP, "FarmFury_Technical", "FarmFury_Artwork", "Rough_Icons", "Abilities")
GAME = os.path.join(os.path.dirname(__file__), "..", "unity", "Assets", "Sprites", "Characters")

# animal -> (source, needs white cut-out)
SOURCES = {
    "Cluck":  (os.path.join(GAME, "Cluck", "Egg.png"), False),            # Cluster Bomb: egg
    "Bessie": (os.path.join(STAMPEDE, "BessieSlam.png"), False),          # Ground Slam: burst
    "Percy":  (os.path.join(STAMPEDE, "Percy_effect.png"), False),        # Bounce Roll: curled up
    "Woolly": (os.path.join(KLING, "Woolly_WoolBall.png"), "grey"),       # Wool Balls (2026-11-08)
    "Ducky":  (os.path.join(KLING, "Ducky_Skip.png"), True),              # Skip Shot
    "Horace": (os.path.join(STAMPEDE, "horseshoe.png"), False),           # Rear Kick
    "Gerald": (os.path.join(KLING, "Gerald_FeatherBomb.png"), True),      # Feather Bombs (2026-11-08)
    "Billy":  (os.path.join(KLING, "Billy_MudBall.png"), True),           # Mud Throw (2026-11-08)
}


# In-game projectile / burst sprites for AbilityProjectile (wired onto the animal prefabs by Wire Scene References).
PROJECTILES = {
    "FeatherBomb":  (os.path.join(KLING, "Gerald_FeatherBomb.png"), True),
    "FeatherBurst": (os.path.join(STAMPEDE, "FeatherBurst.png"), False),
    "WoolBall":     (os.path.join(KLING, "Woolly_WoolBall.png"), "grey"),
    "MudBall":      (os.path.join(KLING, "Billy_MudBall.png"), True),
    "MudSplat":     (os.path.join(KLING, "Billy_MudSplat.png"), True),
}
PROJ_OUT = os.path.join(os.path.dirname(__file__), "..", "unity", "Assets", "Sprites", "Abilities")


def cut_grey(path, tol=22):
    """Border-connected background of the corner colour (for art on grey, e.g. the white wool ball)."""
    from scipy import ndimage
    rgb = np.asarray(Image.open(path).convert("RGB")).astype(np.int16)
    corner = rgb[4, 4]
    near = np.abs(rgb - corner).max(axis=2) <= tol
    labels, _ = ndimage.label(near)
    border = np.unique(np.concatenate([labels[0], labels[-1], labels[:, 0], labels[:, -1]]))
    bg = np.isin(labels, border[border > 0])
    rgba = np.dstack([rgb.astype(np.uint8), np.where(bg, 0, 255).astype(np.uint8)])
    # The wool ball has a drop shadow under it: drop the bottom band where only the dark shadow remains.
    a = rgba[:, :, 3] > 0
    rows = np.nonzero(a.any(axis=1))[0]
    dark = (rgb.max(axis=2) < 70) & a
    for y in rows[::-1]:
        if a[y].sum() and dark[y].sum() / a[y].sum() > 0.6: rgba[y, :, 3] = 0
        else: break
    return rgba


def load(src, cut):
    if cut == "grey": return cut_grey(src)
    return cut_white(src, min_pocket=10**9) if cut else np.asarray(Image.open(src).convert("RGBA")).copy()


def main():
    os.makedirs(OUT, exist_ok=True)
    for animal, (src, cut) in SOURCES.items():
        if not os.path.exists(src):
            print(f"  {animal}: MISSING {src}")
            continue
        # Border-connected white only: the sheep wool and water highlights are white too.
        rgba = load(src, cut)
        rgba = trim(rgba)
        img = Image.fromarray(rgba, "RGBA")
        img.thumbnail((256, 256), Image.LANCZOS)   # shown ~60 px on screen
        img.save(os.path.join(OUT, f"{animal}.png"), optimize=True)
        print(f"  {animal}.png  {img.size[0]}x{img.size[1]}")
    os.makedirs(PROJ_OUT, exist_ok=True)
    for name, (src, cut) in PROJECTILES.items():
        img = Image.fromarray(trim(load(src, cut)), "RGBA")
        img.thumbnail((256, 256), Image.LANCZOS)
        img.save(os.path.join(PROJ_OUT, f"{name}.png"), optimize=True)
        print(f"  Abilities/{name}.png  {img.size[0]}x{img.size[1]}")


if __name__ == "__main__":
    main()
