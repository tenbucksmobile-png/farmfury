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
    "Woolly": (os.path.join(KLING, "Woolly_Clone.png"), True),            # Triple Clone
    "Ducky":  (os.path.join(KLING, "Ducky_Skip.png"), True),              # Skip Shot
    "Horace": (os.path.join(STAMPEDE, "horseshoe.png"), False),           # Rear Kick
    "Gerald": (os.path.join(ARCADE, "Gerald_ability.png"), False),        # Puff Up
    "Billy":  (os.path.join(KLING, "Billy_Headbutt.png"), True),          # Headbutt Through
}


def main():
    os.makedirs(OUT, exist_ok=True)
    for animal, (src, cut) in SOURCES.items():
        if not os.path.exists(src):
            print(f"  {animal}: MISSING {src}")
            continue
        # Border-connected white only: the sheep wool and water highlights are white too.
        rgba = cut_white(src, min_pocket=10**9) if cut else np.asarray(Image.open(src).convert("RGBA")).copy()
        rgba = trim(rgba)
        img = Image.fromarray(rgba, "RGBA")
        img.thumbnail((256, 256), Image.LANCZOS)   # shown ~60 px on screen
        img.save(os.path.join(OUT, f"{animal}.png"), optimize=True)
        print(f"  {animal}.png  {img.size[0]}x{img.size[1]}")


if __name__ == "__main__":
    main()
