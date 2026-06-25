"""
Farm Fury — Batch background removal for Kling AI art.
Strips white/near-white backgrounds using corner flood-fill (preserves white feathers,
wool, etc. that are not connected to the image border).

Usage (run from repo root):
    pip install Pillow
    python tools/remove_backgrounds.py

Output mirrors assets/ structure into unity/Assets/Sprites/.
"""

import os
from collections import deque
from PIL import Image

# ── Sky painting filenames — copy as-is (no bg removal, no white bg) ──────────
SKIES = {
    "SkyPainting.png",
    "FrozenTundra.png",
    "Watermill Village.png",
    "SkyIslands.png",
    "SunkenCity.png",
    "RobotMothership.png",
}

# ── Launcher filenames (white bg, separate output folder) ─────────────────────
LAUNCHERS = {
    "Trabuchet.png",
    "Ice Cannon.png",
    "WaterWheel.png",
    "Plane.png",
    "Submarine.png",
    "GravitySling.png",
}

# ── Character folder-name → short name for output path ────────────────────────
CHAR_MAP = {
    "Cluck_Chicken": "Cluck",
    "Bessie_Cow":    "Bessie",
    "Percy_Pig":     "Percy",
    "Woolly_Sheep":  "Woolly",
    "Ducky_Duck":    "Ducky",
    "Horace_Horse":  "Horace",
    "Gerald_Turkey": "Gerald",
    "Billy_Goat":    "Billy",
}

# ── World prop folder → output sub-folder name ────────────────────────────────
WORLD_MAP = {
    "IceTundra":       "World2_IceTundra",
    "WatermillVillage":"World3_Watermill",
    "SkyIslands":      "World4_SkyIslands",
    "SunkenCity":      "World5_SunkenCity",
    "RobotMothership": "World6_Mothership",
}

THRESHOLD = 235  # pixels with R,G,B all >= threshold are considered "white background"

def remove_white_bg(img: Image.Image) -> Image.Image:
    """Flood-fill from all four corners, making connected near-white pixels transparent."""
    img  = img.convert("RGBA")
    w, h = img.size
    px   = img.load()

    visited = [[False] * h for _ in range(w)]
    q       = deque()

    def is_white(x, y):
        r, g, b, _ = px[x, y]
        return r >= THRESHOLD and g >= THRESHOLD and b >= THRESHOLD

    # Seed from all four corners
    for sx, sy in [(0, 0), (w - 1, 0), (0, h - 1), (w - 1, h - 1)]:
        if not visited[sx][sy] and is_white(sx, sy):
            q.append((sx, sy))
            visited[sx][sy] = True

    while q:
        x, y = q.popleft()
        r, g, b, _ = px[x, y]
        px[x, y]   = (r, g, b, 0)           # make transparent

        for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            nx, ny = x + dx, y + dy
            if 0 <= nx < w and 0 <= ny < h and not visited[nx][ny] and is_white(nx, ny):
                visited[nx][ny] = True
                q.append((nx, ny))

    return img

def save(img: Image.Image, out_path: str):
    os.makedirs(os.path.dirname(out_path), exist_ok=True)
    img.save(out_path)
    print(f"  → {out_path}")

def copy_as_is(src: str, out_path: str):
    os.makedirs(os.path.dirname(out_path), exist_ok=True)
    with open(src, "rb") as f:
        data = f.read()
    with open(out_path, "wb") as f:
        f.write(data)
    print(f"  → {out_path}  (copied, no removal)")

def process_with_removal(src: str, out_path: str):
    img = Image.open(src)
    img = remove_white_bg(img)
    save(img, out_path)

# ── Characters ────────────────────────────────────────────────────────────────
print("\n=== Characters ===")
for folder_name, char_name in CHAR_MAP.items():
    src_dir = os.path.join("assets", folder_name)
    if not os.path.isdir(src_dir):
        print(f"  [SKIP] {src_dir} not found")
        continue
    for fname in os.listdir(src_dir):
        if not fname.lower().endswith(".png"):
            continue
        src      = os.path.join(src_dir, fname)
        out_path = os.path.join("unity", "Assets", "Sprites", "Characters", char_name, fname)
        process_with_removal(src, out_path)

# ── Robot Enemy ───────────────────────────────────────────────────────────────
print("\n=== Robot Enemy ===")
src_dir = os.path.join("assets", "RobotEnemy")
if os.path.isdir(src_dir):
    for fname in os.listdir(src_dir):
        if not fname.lower().endswith(".png"):
            continue
        src      = os.path.join(src_dir, fname)
        out_path = os.path.join("unity", "Assets", "Sprites", "Enemies", "Robot", fname)
        process_with_removal(src, out_path)

# ── Backdrops: skies, launchers, World 1 props ────────────────────────────────
print("\n=== Backdrops ===")
src_dir = os.path.join("assets", "Backdrops")
if os.path.isdir(src_dir):
    for fname in os.listdir(src_dir):
        if not fname.lower().endswith(".png"):
            continue
        src = os.path.join(src_dir, fname)
        if fname in SKIES:
            out_path = os.path.join("unity", "Assets", "Sprites", "Environment", "Skies", fname)
            copy_as_is(src, out_path)
        elif fname in LAUNCHERS:
            out_path = os.path.join("unity", "Assets", "Sprites", "Environment", "Launchers", fname)
            process_with_removal(src, out_path)
        else:
            out_path = os.path.join("unity", "Assets", "Sprites", "Environment", "World1Props", fname)
            process_with_removal(src, out_path)

# ── World Props (Worlds 2–6) ──────────────────────────────────────────────────
print("\n=== World Props ===")
world_props_dir = os.path.join("assets", "WorldProps")
if os.path.isdir(world_props_dir):
    for world_folder in os.listdir(world_props_dir):
        src_dir = os.path.join(world_props_dir, world_folder)
        if not os.path.isdir(src_dir):
            continue
        out_sub = WORLD_MAP.get(world_folder, world_folder)
        print(f"  {world_folder} → {out_sub}")
        for fname in os.listdir(src_dir):
            if not fname.lower().endswith(".png"):
                continue
            src      = os.path.join(src_dir, fname)
            out_path = os.path.join("unity", "Assets", "Sprites", "Environment", out_sub, fname)
            process_with_removal(src, out_path)

print("\nDone. All sprites written to unity/Assets/Sprites/")
print("Run 'FarmFury > Wire Scene References' in Unity after import completes.")
