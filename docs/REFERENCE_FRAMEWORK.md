# FarmFury reference framework

**Status (2026-11-08):** reference measured from one Angry Birds 2 session (`tools/review/sessions/
ref-angrybirds-20261108-141802`, 356 s, 4 levels / 3 worlds, 1600x720 phone, aspect 2.22). FarmFury values below
are computed from the code and level data (no FarmFury recording yet); replace them with a recorded session
(`record_session.py`, dev build) before final tuning.

## Why

FarmFury's worlds read as rough and unlike the genre. This framework defines the look and feel as numbers in
**screen units**, measures the reference game from recordings, measures FarmFury the same way, and applies the
differences as world-agnostic rules plus a per-world art spec, so all nine worlds follow one standard.

Units: **sh** = fraction of the visible screen height, **sw** = fraction of the screen width, **s** = seconds.
Positions are measured from the left (x) and from the bottom (y, "up").

## Loop

1. Record (`tools/review/record_session.py`, protocol in `tools/review/README.md`).
2. Measure (`analyze_video.py --only "<gameplay windows>"`), then read the stills/sheets for sizes and depth.
3. Compare (`compare.py`); out of band = FarmFury/reference outside 0.8-1.25.
4. Apply through the knobs listed below, all worlds at once.
5. Re-record FarmFury and repeat.

## How the reference was measured

- Sizes, positions and depth: read off full-resolution stills with a 5% grid at 35.4 s (tall-tower room), 195 s
  (storm world) and 309.8 s (tropical tutorial), plus the 10%-grid contact sheets of every shot.
- Flight: the preview-dot arc on two stills gives the curvature (2nd difference 0.0024-0.0025 sh per dot at
  0.042-0.048 sh dot spacing, i.e. y = 0.52-0.70·x² in sh); release-to-impact times from the frame sheets (1.2-1.5 s)
  give the speed. Angry Birds 2 zooms the camera a little during most flights, so per-frame flight fits are
  approximate (±30%); the dot-arc method is not affected by zoom.
- Camera, parallax, collapse: `analyze_video.py` restricted to the gameplay windows (ads/menus excluded).

## A. Camera and framing

| ID | Metric | FarmFury now | Reference (AB2, measured) | Knob |
|---|---|---|---|---|
| A1 | Camera during a shot | fixed | mostly fixed per room; drifts ≤0.06 sw at ~0.2 sw/s, plus a slight zoom toward the target during the flight | `CatapultLauncher` follow / zoom |
| A2 | Multi-room levels | none (one screen) | rooms cleared in turn; fast swoosh pan 0.3-0.7 s, 0.7-1.3 sw, peak 3-4.5 sw/s, with a speed-streak wipe | level structure (future) |
| A3 | Launcher x | ~0.37 sw | **0.26-0.27 sw** | `_cameraRestOffset.x` |
| A4 | Launcher → nearest structure | 0.26-0.28 sw | 0.26-0.35 sw (farthest target 0.57 sw) | level X offsets |
| A5 | Launcher height (base) | cannon centre 0.29 sh, ground 0.135 sh | **sling on a raised island, base 0.33-0.43 sh up**; sling 0.08-0.09 sh tall | camera y, launcher platform |
| A6 | Structure base height | ground line 0.135 sh, every structure on it | **0.20-0.40 sh up, each structure on its own island; heights vary within a level** | ground line / platforms |
| A7 | Zoom | per-level fixed ortho 4.5-8.0 (median 6.3) | per-room fixed framing, small in-flight zoom | `ComputeOrthoSizeForLevel` |

## B. Parallax and scenery depth

| ID | Metric | FarmFury now | Reference (AB2) | Knob |
|---|---|---|---|---|
| B1 | Far sky parallax during pans | not observable | **≈0.0** (sky fixed to screen) in room transitions | `EnvironmentDepthSystem` |
| B2 | Middle/near band parallax | not observable | ≈0.95-1.0 (moves with the playfield) | same |
| B3 | Depth cues | W1-W2: 3 painted flats; W3-W9: one painting | **atmospheric perspective**: background mesas/cliffs blurred, desaturated and hazed (pink haze, blue mist); playfield crisp and saturated | art spec |
| B4 | Foreground framing | none | **dark silhouette foreground** (trees, leaves, cliffs) cropping the left edge and corners, in front of the play layer | new foreground layer |
| B5 | Bottom of screen | flat ground strip | island rock dissolving into fog/cloud banks; no flat ground line | art spec |
| B6 | Weather/particle layer | none | rain streaks, drifting mist, sun glow/lens flare over the scene | per-world FX |
| B7 | Decorative props on the play layer | code-placed props at ground | sparse: a few flowers on island edges; nothing behind the structures that competes with them | `SceneryBuilder` |

## C. Sizing (at the level's framing)

**Correction (2026-11-08, L01 pass):** the first table used the auto-zoom's robot size table, which still assumes
500 px robot art; the robots were re-exported at 256 px in July, so robots are half the size that table implied.
Measured from the real sprites, World 1's objects are already **in proportion with each other**; what is off is
the **zoom** (everything ~1.6x too close). The fix is framing (`ReferenceFraming.cs`), not re-scaling art.

| ID | Metric | L01 before (ortho 4.79) | Reference (AB2) | L01 framed (ortho 8.0) |
|---|---|---|---|---|
| C1 | Animal height | 0.075 sh | 0.040-0.045 sh | 0.045 |
| C2 | SemiHarvester robot height | 0.094 sh | pig 0.04-0.06 (big 0.07) | 0.056 |
| C3 | Robot / animal | 1.25 | pig/bird 1.0-1.5 | 1.25 (unchanged) |
| C4 | Haybale (visual) | 0.086 sh | square block ~0.04 | 0.051 |
| C5 | Cannon height | 0.185 sh | sling 0.08-0.09 | 0.11 |
| C6 | Launcher x | ~0.40 sw | 0.26-0.27 sw | 0.26 |
| C7 | Ground line | physics ground at the screen's bottom edge, objects 0.5-1.0 above it | 0.33-0.43 (islands) | 0.12, shared by physics, paint, props, blocks |

Still open for later levels: plank thickness (World 1 plank art is 3.6-4:1, AB2 planks 7-20:1 — try sliced sprites
first), tower width (0.22 sw vs 0.10-0.15) and pieces per structure (24 vs 25-50 thin pieces).

## D. Shot

| ID | Metric | FarmFury now | Reference (AB2) | Ratio | Knob |
|---|---|---|---|---|---|
| D1 | Projectile gravity | 0.29 sh/s² (ortho 6.3) - 0.40 (ortho 4.5) | **≈0.5 sh/s²** (dot arc + flight times) | 0.6-0.8 | `AnimalBase.Launch` gravityScale |
| D2 | Launch speed (full pull) | 0.74-1.03 sh/s | ≈0.85 sh/s | ≈1.0 | `_maxLaunchSpeed` |
| D3 | Release → impact | ~1.5-3.6 s (power dependent) | **1.2-1.5 s** | ⚠ for long shots | D1 + A4 |
| D4 | Trajectory preview | dots | ~15 white dots, evenly spaced in time, covering the first ~60% of the arc | — | `DrawTrajectory` |
| D5 | Projectile after impact | disappears after 0.45 s | keeps tumbling through the structure, then poofs | — | `_contactTimeout` |

## E. Destruction

| ID | Metric | FarmFury now | Reference (AB2) | Knob |
|---|---|---|---|---|
| E1 | Collapse duration | (record FarmFury) | median 0.75 s, most 0.45-1.3 s, big towers up to 2.5 s | stagger, gravity, damping |
| E2 | Settle after peak | (record FarmFury) | median 0.47 s | damping, friction |
| E3 | Debris peak fall speed | blocks fall at 1.59 sh/s² (≈1.3 sh/s from 0.5 sh) | median ≈1.0 sh/s | world gravity / sh |
| E4 | Destruction feedback | debris sprite + fragments | many small splinters, score popups on every piece, dust puffs, a strike banner | FX |

## F. Pacing

| ID | Metric | FarmFury now | Reference (AB2) |
|---|---|---|---|
| F1 | Shots per room | lineup length | 1-3 per room, 2-4 rooms per level |
| F2 | Release → next aim | ≥1.8 s + flight | ~2-3 s (settle, then the next card) |

## What the reference changed in the code-only hypotheses

- **Flight is not the main gap.** In screen units FarmFury's gravity (0.29-0.40) and launch speed (0.74-1.03) are
  close to AB2's (≈0.5 and ≈0.85). A modest gravity raise (≈+40%) is enough; long FarmFury lobs are too slow.
- **A fixed camera per screen is genre-correct.** AB2 doesn't follow the bird far; it frames each room and moves
  between rooms. The missing pieces are the slight in-flight zoom and rooms, not a follow-cam.
- **The big gaps are framing and depth:** everything drawn ~1.6x too close (sprites are in proportion with each
  other), chunky towers built from short planks, the physics ground a full unit below where the scenery and blocks
  stand, and backdrops with no atmospheric depth or foreground.

## Applying it to all worlds

**World-agnostic rules (one place, every world):**
1. Framing (`ReferenceFraming`, done for L01): ortho 8.0 so the animal is 0.045 sh, launcher at 0.26 sw, ground
   at 0.12 sh, one shared ground line (World 1: y=-5.6). Planks thinner via sliced sprites (to test).
2. Architecture: towers 0.10-0.15 sw wide, storeys ~0.1 sh high, robots on each floor; 1-3 towers per level.
   Launcher on a raised platform at x≈0.26 sw, base ≈0.35 sh; structures on their own platforms 0.2-0.4 sh up,
   0.26-0.57 sw from the launcher. Level generator rebuilt around these rules instead of re-using World 1's chunky
   towers.
3. Physics: projectile gravityScale 0.18 → ≈0.25 (speed re-solved so every level stays reachable); target
   release-to-impact 1.2-1.5 s.
4. Camera: per-level framing as now, plus a slight push-in toward the impact point during flight.
5. Animal keeps tumbling after impact instead of vanishing at 0.45 s.

**Per-world art spec (same structure for all nine worlds):**

| Layer | Content | Treatment | Parallax |
|---|---|---|---|
| Sky | gradient + sun/moon/aurora glow | soft, low detail | 0.0 |
| Far | distant landforms (mesas, ice cliffs, sky islands...) | blurred, low contrast, tinted toward the world's haze colour | 0.1-0.2 |
| Mid | nearer landforms | slightly blurred, partly hazed | 0.4-0.6 |
| Play | launcher island, structure islands, robots, blocks | crisp, fully saturated, outlined | 1.0 |
| Fog | cloud/mist banks swallowing island bases | soft alpha, world-tinted | 1.0-1.1 |
| Foreground | dark silhouettes (trees, rocks, ice, girders per world) cropping left edge/corners | near-black, no detail | 1.2-1.3 |
| FX | weather (rain, snow, embers, bubbles, sparks) | sparse particles | screen |

Pixel targets at a 1080 px tall screen: animal 0.045 sh ≈ 49 px, robot 0.05 sh ≈ 54 px, plank thickness 0.015 sh ≈
16 px. Source art at 4x those sizes.

Acceptance: a FarmFury recording of each world lands every metric within 0.8-1.25 of the reference.
