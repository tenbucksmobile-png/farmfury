"""
FarmFury — composites World 2's "LEVEL" banner (W2_level.png) with a per-level number badge
(NN.png) into a single LevelNN.png match-up header sprite.

MatchUpScreen only ever shows ONE header image per level (no separate banner+number layers at
runtime — see MatchUpScreen.Show()'s _levelHeaderSprites lookup), so the two source pieces the
user supplies (a shared "LEVEL" banner + a numeral badge per level) have to be pre-composited
offline into one flat PNG, matching how World 1's own LevelHeader1.png etc. are single flat images.

Added 2026-07-26 per user request: "match the number under it (middle with slight overlay)" —
the number sits centred under the banner, with its top edge tucked slightly BEHIND the banner's
bottom edge (paste number first, banner second, so the banner occludes the overlap band) — the
same "number peeking out from under LEVEL" look World 1's LevelHeader1.png already establishes.

Usage:
    pip install Pillow
    python tools/compose_w2_level_headers.py

Reads unity/Assets/Sprites/UI/Matchup_W2/W2_level.png + {19,20,21}.png (extend NUMBERS below for
future levels once their own NN.png badge exists), writes LevelNN.png back into the same folder.
Re-run any time either source file changes — SceneSetup.WireWorld2MatchUpCards always re-wires
whatever's on disk at these paths, no code change needed after re-running this script.
"""

from PIL import Image

FOLDER = "unity/Assets/Sprites/UI/Matchup_W2"
BANNER = "W2_level.png"
NUMBERS = ["19", "20", "21"]  # extend as new NN.png badges are added

NUM_WIDTH = 130   # target width (px) of the resized number badge — see field comment above
OVERLAP = 18      # px of the number's top edge tucked behind the banner's bottom edge


def compose(number: str) -> None:
    banner = Image.open(f"{FOLDER}/{BANNER}").convert("RGBA")
    bw, bh = banner.size

    num = Image.open(f"{FOLDER}/{number}.png").convert("RGBA")
    nw, nh = num.size
    scale = NUM_WIDTH / nw
    num_resized = num.resize((NUM_WIDTH, round(nh * scale)), Image.LANCZOS)
    rw, rh = num_resized.size

    num_top_y = bh - OVERLAP
    canvas = Image.new("RGBA", (bw, num_top_y + rh), (0, 0, 0, 0))

    num_x = (bw - rw) // 2
    canvas.alpha_composite(num_resized, (num_x, num_top_y))
    canvas.alpha_composite(banner, (0, 0))  # drawn last so it occludes the overlap band

    out_path = f"{FOLDER}/Level{number}.png"
    canvas.save(out_path)
    print(f"saved {out_path} {canvas.size}")


if __name__ == "__main__":
    for n in NUMBERS:
        compose(n)
