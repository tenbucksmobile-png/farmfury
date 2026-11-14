"""
Pads every PNG under unity/Assets/Sprites whose width or height is not a multiple of 4 with transparent pixels
(split across both sides, so the sprite's centre moves at most half a pixel) until both are.

Why: Unity's block compression (ASTC on iOS, ETC2 on Android) is skipped for a texture whose size isn't a multiple
of 4 - it silently ships as raw RGBA32 (4 bytes/pixel + mipmaps). Found 2026-11-14: 432 of 848 sprites were raw,
which made the first iOS build 878 MB. Run this after any tool that cuts new art (tools/import_*.py etc.).

Skipped: sprites in Multiple mode (their slice rects would shift) and 9-slice sprites (their borders would).

    python tools/pad_sprites_to_4.py            # pad
    python tools/pad_sprites_to_4.py --check    # list offenders only, exit 1 if any
"""
import os
import re
import sys

from PIL import Image

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "unity", "Assets", "Sprites")


def skip_reason(png):
    meta = png + ".meta"
    if not os.path.exists(meta):
        return None
    text = open(meta, encoding="utf-8").read()
    if re.search(r"spriteMode: 2", text):
        return "multiple-mode sheet"
    b = re.search(r"spriteBorder: \{x: ([\d.]+), y: ([\d.]+), z: ([\d.]+), w: ([\d.]+)\}", text)
    if b and any(float(x) for x in b.groups()):
        return "9-slice border"
    return None


def main():
    check = "--check" in sys.argv
    padded, skipped, offenders = 0, [], 0
    for dirpath, _, files in os.walk(ROOT):
        if "~" in dirpath:
            continue
        for name in files:
            if not name.lower().endswith(".png"):
                continue
            path = os.path.join(dirpath, name)
            with Image.open(path) as im:
                w, h = im.size
                if w % 4 == 0 and h % 4 == 0:
                    continue
                offenders += 1
                reason = skip_reason(path)
                if reason:
                    skipped.append(f"{os.path.relpath(path, ROOT)} ({reason})")
                    continue
                if check:
                    print(f"  {os.path.relpath(path, ROOT)}  {w}x{h}")
                    continue
                nw, nh = (w + 3) // 4 * 4, (h + 3) // 4 * 4
                src = im.convert("RGBA")
            out = Image.new("RGBA", (nw, nh), (0, 0, 0, 0))
            out.paste(src, ((nw - w) // 2, (nh - h) // 2))
            out.save(path, optimize=True)
            padded += 1
    for s in skipped:
        print("  skipped", s)
    if check:
        print(f"{offenders - len(skipped)} sprite(s) not a multiple of 4")
        sys.exit(1 if offenders - len(skipped) else 0)
    print(f"padded {padded} sprite(s), skipped {len(skipped)}")


if __name__ == "__main__":
    main()
