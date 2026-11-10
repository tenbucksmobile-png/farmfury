"""
Farm Fury Cannonfire branding (2026-11-10): the game is renamed "Farm Fury Cannonfire" to run with Farm Fury Arcade
and Farm Fury Stampede. Second design (user: the first, a wooden "CANNONFIRE" sign, was "far too close to Stampede";
"have the cannonfire look like it has been fired out of the cannon"; "cluck image cannot be the same as Stampede").

Inputs in FarmFury_Technical/FarmFury_Artwork/Cannonfire/:
  Logo_Cannonfire.png       FARM FURY wordmark + the cannon blasting CANNONFIRE (tools/compose_cannonfire_logo.py)
  Logo_CannonfireBlast.png  the same without the wordmark (app icon: it says only "Cannonfire")
  Poster_B.png              Kling landing art with no text (FarmFury's own angry in-flight Cluck, the animal gang);
                            the logo is composited on top so it is always spelled right
Outputs:
  * Sprites/UI/Franchise/Menu/FF_Logo.png  <- the logo (Leaderboard corner)
  * Sprites/UI/Franchise/Canvas/*_Wide.png <- the small FARM FURY mark baked into the top-left of every results / pause /
                                              new-character canvas is found by template matching (on the pre-rename
                                              originals), painted out and covered with the logo
  * Sprites/UI/LandingPage.png             <- the poster with the logo at top centre
  * Sprites/AppIcon.png                    <- 1024: the cannon blasting CANNONFIRE over red-orange rays from the cannon
The pre-rename files are kept once in Cannonfire/originals/ as *_FarmFury.png, so this can be re-run.

Usage (repo root):  python tools/brand_cannonfire.py [--poster Poster_B.png]

Final pick (2026-11-10, user): the logo is Logo_C.png (the user's transparent cut of the wooden-sign logo) on every
canvas and the Leaderboard; the landing page is the user's own poster ic.png (logo already in it), used as is; the
app icon stays the generated one. So by default: --logo Logo_C.png --landing ic.png --no-icon.
"""
import os
import shutil
import sys

import cv2
import numpy as np
from PIL import Image, ImageDraw, ImageFilter

from import_cornfield_art import DESKTOP

HERE = os.path.dirname(os.path.abspath(__file__))
ART = os.path.join(DESKTOP, "FarmFury_Technical", "FarmFury_Artwork", "Cannonfire")
ORIGINALS = os.path.join(ART, "originals")
UI = os.path.join(HERE, "..", "unity", "Assets", "Sprites", "UI")
CANVAS = os.path.join(UI, "Franchise", "Canvas")
LOGO_OUT = os.path.join(UI, "Franchise", "Menu", "FF_Logo.png")


def original(path):
    """The pre-rename copy of path (made once), so re-runs always start from the untouched art."""
    os.makedirs(ORIGINALS, exist_ok=True)
    root, ext = os.path.splitext(os.path.basename(path))
    keep = os.path.join(ORIGINALS, root + "_FarmFury" + ext)
    if os.path.exists(path) and not os.path.exists(keep):
        shutil.copy2(path, keep)
    return keep


def rebrand_canvases(logo):
    old = np.asarray(Image.open(original(LOGO_OUT)).convert("RGBA"))
    old_mask = (old[:, :, 3] > 128).astype(np.uint8)
    old_gray = cv2.cvtColor(old[:, :, :3], cv2.COLOR_RGB2GRAY)
    done = 0
    for name in sorted(os.listdir(CANVAS)):
        if not name.endswith(".png") or "Character_Canvas" in name or name == "Canvas.png":
            continue
        path = os.path.join(CANVAS, name)
        img = np.asarray(Image.open(original(path)).convert("RGBA")).copy()
        h, w = img.shape[:2]
        region = cv2.cvtColor(img[: h // 3, : w // 2, :3], cv2.COLOR_RGB2GRAY)
        best = (-1, None, None)
        for tw in range(120, 420, 6):
            s = tw / old.shape[1]
            t = cv2.resize(old_gray, (tw, max(8, int(old.shape[0] * s))))
            m = cv2.resize(old_mask, (t.shape[1], t.shape[0]), interpolation=cv2.INTER_NEAREST)
            if t.shape[0] >= region.shape[0] or t.shape[1] >= region.shape[1]: continue
            r = cv2.matchTemplate(region, t, cv2.TM_CCORR_NORMED, mask=m)
            _, v, _, loc = cv2.minMaxLoc(r)
            if np.isfinite(v) and v > best[0]: best = (v, loc, t.shape)
        score, loc, (th, tw) = best
        if score < 0.9:
            print(f"  {name}: no FARM FURY mark (best {score:.2f}) - left as is")
            continue
        m = cv2.resize(old_mask, (tw, th), interpolation=cv2.INTER_NEAREST)
        full = np.zeros((h, w), np.uint8)
        full[loc[1]:loc[1] + th, loc[0]:loc[0] + tw] = m
        full = cv2.dilate(full, np.ones((7, 7), np.uint8)) * 255
        rgb = cv2.inpaint(np.ascontiguousarray(img[:, :, :3]), full, 7, cv2.INPAINT_TELEA)
        out = Image.fromarray(np.dstack([rgb, img[:, :, 3]]), "RGBA")
        # The logo is a wide cannon-blast composition: about 1.55x the old mark's width, its wordmark where the old was.
        lw = int(tw * LOGO_SCALE)
        lg = logo.resize((lw, int(logo.height * lw / logo.width)), Image.LANCZOS)
        out.alpha_composite(lg, (max(8, loc[0] - int(tw * 0.04)), max(8, loc[1] - int(th * 0.35))))
        out.save(path, optimize=True)
        done += 1
    print(f"  {done} canvases re-branded")


def poster(logo, src):
    art = Image.open(os.path.join(ART, src)).convert("RGBA")
    w, h = art.size
    lw = int(w * 0.35)
    lg = logo.resize((lw, int(logo.height * lw / logo.width)), Image.LANCZOS)
    x, y = (w - lw) // 2, int(h * 0.025)
    # A soft warm halo behind the logo so it reads over the sun and the sky.
    a = np.asarray(lg)[:, :, 3]
    glow = Image.fromarray(np.dstack([np.full_like(a, 255), np.full_like(a, 246), np.full_like(a, 214),
                                      (a * 0.5).astype(np.uint8)]), "RGBA")
    halo = Image.new("RGBA", art.size, (0, 0, 0, 0))
    halo.alpha_composite(glow, (x, y))
    art.alpha_composite(halo.filter(ImageFilter.GaussianBlur(18)))
    art.alpha_composite(lg, (x, y))
    dst = os.path.join(UI, "LandingPage.png")
    original(dst)
    art.convert("RGB").save(dst, optimize=True)
    print(f"  LandingPage.png <- {src} + logo")


def app_icon(blast):
    n = 1024
    # Rays bursting from the lower left (where the cannon sits): yellow-orange near it, deep red toward the far corner,
    # with a dark vignette. Deliberately not Stampede's concentric rings.
    yy, xx = np.mgrid[:n, :n].astype(np.float32)
    cx, cy = n * 0.18, n * 0.86
    ang = np.arctan2(cy - yy, xx - cx)
    dist = np.clip(np.hypot(xx - cx, yy - cy) / (n * 1.25), 0, 1)[..., None]
    rays = (np.sin(ang * 18.0) > 0).astype(np.float32)[..., None]
    base = np.array([196, 34, 18], np.float32) * dist + np.array([255, 196, 60], np.float32) * (1 - dist)
    light = base + (255 - base) * 0.16 * rays
    bg = Image.fromarray(light.clip(0, 255).astype(np.uint8), "RGB").convert("RGBA")
    vign = Image.new("L", (n, n), 0)
    ImageDraw.Draw(vign).ellipse([-n * 0.25, -n * 0.25, n * 1.25, n * 1.25], fill=255)
    vign = vign.filter(ImageFilter.GaussianBlur(n * 0.08))
    bg = Image.composite(bg, Image.new("RGBA", (n, n), (70, 10, 4, 255)), vign)
    s = min(n * 0.94 / blast.width, n * 0.86 / blast.height)
    b = blast.resize((int(blast.width * s), int(blast.height * s)), Image.LANCZOS)
    bg.alpha_composite(b, ((n - b.width) // 2, (n - b.height) // 2 + int(n * 0.02)))
    dst = os.path.join(UI, "..", "AppIcon.png")
    original(dst)
    bg.convert("RGB").save(dst, optimize=True)
    print("  AppIcon.png <- cannon blast over rays")


LOGO_SCALE = 1.15   # canvas logo width as a multiple of the old FARM FURY mark's width


def landing_as_is(src):
    """A finished poster (logo already painted in): copied straight to LandingPage.png."""
    dst = os.path.join(UI, "LandingPage.png")
    original(dst)
    img = Image.open(os.path.join(ART, src)).convert("RGB")
    img.save(dst, optimize=True)
    # The side fill for screens wider than the poster (LandingScreen): a small, heavily blurred copy.
    small = img.resize((img.width // 4, img.height // 4), Image.LANCZOS).filter(ImageFilter.GaussianBlur(14))
    small.save(os.path.join(UI, "LandingPage_Blur.png"), optimize=True)
    print(f"  LandingPage.png <- {src} (as is), LandingPage_Blur.png")


def main():
    arg = lambda k, d: sys.argv[sys.argv.index(k) + 1] if k in sys.argv else d
    name = arg("--logo", "Logo_C.png")
    logo = Image.open(os.path.join(ART, name)).convert("RGBA")
    logo = logo.crop(logo.getbbox())
    original(LOGO_OUT)
    logo.save(LOGO_OUT, optimize=True)
    print(f"  FF_Logo.png <- {name} {logo.width}x{logo.height}")
    rebrand_canvases(logo)
    if "--poster" in sys.argv:                       # compose a text-free poster with the logo
        poster(logo, arg("--poster", "Poster_B.png"))
    else:
        landing_as_is(arg("--landing", "ic.png"))
    if "--icon" in sys.argv:                         # rebuild the icon (the current one is final)
        app_icon(Image.open(os.path.join(ART, "Logo_CannonfireBlast.png")).convert("RGBA"))


if __name__ == "__main__":
    main()
