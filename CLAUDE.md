# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

---

## Project Overview

**Farm Fury** is an Angry Birds-style physics destruction game. Farm animals launch from a cannon to wreck robot fortresses and reclaim the farm.

The repo contains two parallel codebases:
- `index.html` — **Phaser 3.60 prototype** (frozen at Prompt 12, kept as a reference/physics-validation artifact only — not under active development)
- `unity/` — **Unity 6.5 URP 2D port** (active development target for iOS/Android release)

This is the single source of truth for the Unity port. Detailed reasoning behind current values/conventions (root causes, dead ends, superseded fixes) lives in **`docs/HISTORY.md`** — read it when you need the "why," not just the "what." This file only states current truth.

Full GDD: **`FarmFury_GDD.md`** (v3.0, repo root — the living design doc, franchise format; supersedes the June 2026
`Other/FarmFuryUniverse/FarmFury_GDD_v2.docx`). Update it alongside this file when the design changes.

---

## Git Repository Scope

`Desktop/FarmFury/.git` is now a standalone repo (single remote: `origin` →
`github.com/tenbucksmobile-png/farmfury.git`) — safe to use ordinary `git add -A`/`git commit -a`
from inside it. This was NOT always true: until 2026-07-09 this directory was part of a single
git repo covering the entire home directory (`C:\Users\Personel\.git`), shared with CryptoAlgoBot,
Kaya, and an old IndabaCares copy, pushed to both `farmfury.git` and (accidentally)
`crypto-algo.git`. That history was extracted via `git subtree split --prefix=Desktop/FarmFury`
and force-pushed as this repo's current `master`, so FarmFury's commit history before that date is
preserved intact, but the home-dir mega-repo no longer exists for this project. If you ever find
`.git` missing from this directory or discover FarmFury files tracked from a parent directory's
repo again, something has regressed — this should be a normal, self-contained repo going forward.

---

## Current Status & Known Issues

Full session-by-session narrative (root causes, dead ends, exact measurements, superseded fixes)
now lives in **`docs/HISTORY.md`** — this section only lists what's still genuinely open, plus a
one-line pointer per recent major change. **Nothing in this project has been visually verified in
Play mode** — this dev environment has no Play-mode access, so every change below is verified by
static analysis, compile checks, and direct scene/asset-file inspection only. Treat all of it as
needing a live pass before shipping.

### Known open issues / flags

- **Franchise flow (2026-11-02) is compiled and wired but never seen in Play mode.** Every screen layout is from
  reference-canvas maths; check Landing / World Select / Level Select (8-column grid above 12 levels) / Character
  Select / Results / Pause / Settings / Shop / Locker on a real aspect ratio. Hat placement on animals is an
  approximation (half the body sprite's width at 82% height) — tune `AnimalCosmeticRenderer` after seeing it.
- **Art sources (2026-11-02):** animal poses come from `FarmFury_Technical/FarmFury_Artwork/Rough_Characters` via
  `tools/import_character_art.py` (all 8 animals; Horace's Trigger/Impact pending two Kling jobs — save them as
  `Rough_Characters/Horace/Horace_Trigger.png` / `Horace_Impact.png` and re-run the script + Wire Scene References).
  `Sprites/AppIcon.png` (Cluck thumbs-up + FF_Logo) is set as the default app icon by Wire Scene References.
- **Menu art is Stampede stand-ins** (`Assets/Sprites/UI/Franchise/`, gitignored like all sprites). The "Farm Fury
  Stampede" logo was inpainted out of all 28 Level Complete / Failed / Pause / New Character canvases (2026-11-02,
  OpenCV Telea; clean on sky backdrops, a soft smudge on the busier paid-world ones); the Leaderboard has no logo. Full list in `docs/ART_REQUIREMENTS.md` (Kling checklist for all nine worlds).
- **Monetisation needs the user's accounts:** create the `farmfury_`-prefixed products (Remove Ads, 4 coin packs,
  3 worlds, 5 cannon skins, 6 trails, 5 impact effects) in App Store Connect / Play Console; enter FarmFury's LevelPlay app keys and ad unit
  ids on the GameManager's AdManager (ads are inert until then); register the analytics events. No AdMob adapter is
  included until FarmFury has its own AdMob app id (the adapter crashes Android at launch without one; the id would
  go in the Android manifest and `FarmFuryIosBuild`).
- **Identity not set:** bundle id is still `com.DefaultCompany.2D-URP`, company `DefaultCompany`. The other titles
  use `com.farmfury.<game>` / "Tenbucks Mobile" — the user's call. productName stays "FarmFury" (changing it moves
  the Editor's PlayerPrefs); the home-screen label is set to "Farm Fury" at build time (`FarmFuryAndroidBuild`,
  `FarmFuryIosBuild` — neither compiled yet: batch checks build the Windows target only).
- **Not ported yet:** Stampede's Boot scene / Android AAB split + store build scripts, the iOS privacy manifest,
  the Character Story Cosmetics tab.
- **Worlds 2-9 levels (2026-11-02) are generated, not hand-built, and unseen in Play mode.** 141 levels from
  `WorldLevelGenerator` (see Recent work). Difficulty and solvability are inherited from World 1's towers but untested
  with the new animals' abilities; the Mothership backdrop has no painted ground (towers stand in space); prop placement
  (`SceneryBuilder.BuildWorldProps`) is code-placed and may crowd at wide zooms. Ducky/Horace/Gerald/Billy poses are
  Stampede platformer frames standing in for cannon poses (no Idle/Impact pose).

- **L19 ("Frostbound Watch") was re-dumped 2026-08-09**, replacing its earlier 6-block/3-robot
  layout with a simpler one — 3 `BlockType.Wood` blocks (artVariant `Block`, rendering with the
  World 2 ice reskin) + 3 IceHarvestorRobot instances (all detected as `RobotType.Harvester`, no
  Glacier-tier robot placed this time). Percy's character art was also fixed the same session (see
  2026-08-09 entry below) — a folder-name mismatch had it stuck on the pink-circle fallback. Not
  yet visually re-verified in Play mode (no Play-mode access here).
- **World 2 block reskin only covers 5 of ~14 `WoodArtVariant` shapes.** `BlockBase._sprNormalW2`/
  `_sprVerticalW2`/`_sprHorizontalW2`/`_sprDiagonalW2`/`_sprShortW2` (wired on both `WoodBlock.prefab`
  and `StoneBlock.prefab` from `Ice_block.png`/`Ice_vertical.png`/`Snow_block.png`/
  `Ice_diagonal.png`/`Snow_short.png`) are the only shapes with real ice/snow art today — Skew,
  Square, Cart, Barrel, RuinedWall, Tower, etc. have no ice counterpart and fall back to whichever
  of the 5 resolves as that world's "normal" tile. A level built with `LevelLayoutDumper` using
  those unwired shapes will read as a mix of ice blocks and leftover World 1 wood/stone art until
  more ice variants are supplied.
- **World 2 parallax seam re-introduced on purpose.** `EnvironmentDepthSystem._midgroundClipAboveW2`
  raised 0.21 -> 0.60 (2026-07-26, user request: "too much night sky in the parallax", explicitly
  chose "push it anyway, accept the seam" over waiting for new art). There's now a visible
  horizontal color-break around the old v=0.227 boundary where `ParallaxMidGrounds_W2.png`'s own
  baked-in sky (revealed between 0.227 and 0.60) meets `ParallaxFarHills_W2.png`'s sky behind it —
  confirmed via direct pixel composite, not guessed. The only real fix is new Midground art with
  taller terrain and/or no separate baked sky above the peaks; once that exists this can drop back
  toward 0.21-0.23 without the seam.
- **`Scenery_World2` is a first-draft, code-placed prop set, not visually verified.** Added
  2026-07-26 (user request: "add in the igloo, ice cannon and other built sprites") — mirrors
  `Scenery_World1`'s hand-placed barn/tree/fence with an Igloo, two IceTrees, BuriedRuin,
  IceSpire/IceCrystals/IceBoulder (see `SceneSetup.EnsureWorld2Scenery`), positioned by reading
  `Scenery_World1`'s own real coordinates from `Game.unity` and computing a comparable scale/
  position — but never seen rendered (no Play-mode access here). Treat every position as a
  starting point to drag around in-Editor; re-running Wire Scene References won't reset a
  hand-tuned position (same "only touch on first creation" convention as `FarmCannon`).
  `SceneryBuilder.ToggleHandAuthoredScenery` (new) shows/hides this group vs. `Scenery_World1`
  by `LevelData.world` on every level load, since both groups occupy the same world-space region.
- **World 2 ice-prop ground-seam scatter** (`SceneryBuilder.BuildIceGroundProps`) uses a
  placeholder X range copied from World 1's typical level span — no real World 2 `LevelData`
  exists yet to know the actual ground-seam extent.
- **Robot idle-patrol range (±0.3 world units)** is a conservative guess, not checked against
  every level's actual robot-to-robot clearance — could clip on an unusually dense future level.
- **`WildFlowers` sortingOrder=10** on L1's scene props — past the documented "decorative ≤1"
  rule, rendering in front of the cannon/animals. Not reverted since it may be deliberate
  (foreground depth); confirm intent before touching.
- **No level validator** (GDD calls for a physics-sim stability/solvability check before a level
  ships) — none exists.
- **`assets/` (raw Kling AI source art) is permanently removed from this repo**, relocated to the
  Desktop. `unity/Assets/Sprites/` (processed game art) is gitignored and exists in exactly one
  place: this machine's disk — if lost, recovery means restoring `assets/` and re-running the art
  pipeline.
### Recent major work (newest first — see `docs/HISTORY.md` for full detail on each)

- **2026-11-08 (newest) — Self-collapse fixed, verified headless** (user: "they still breaking by themselves").
  New `Editor/PhysicsProbe.cs` (menu FarmFury → Debug → Physics Probe, or batch `-executeMethod PhysicsProbe.Run`
  with env `FF_PROBE_LEVELS=first,last`, `FF_PROBE_WAKE=1` for a wake-everything stress mode) plays levels with no
  input and logs `[Probe]` timelines / overlaps / results. Root causes: the robot reveal pop scaled live colliders
  into the structure (colliders now stay off from the pop until `RobotEnemy.GoLive`, which LevelLoader follows with a
  Freeze); trimmed colliders overlapped stacked neighbours by ~0.04 (`BlockBase.TrimInset`); L01's bales overlapped
  0.3 u; the designer's deliberate tucks. Also: live blocks use a no-bounce material + damping, solver iterations
  24/12, robot collider bottom aligned to the art. Probe: all 18 World 1 levels 100% intact, 0 awake, 0 movement.
  Still open: in stress mode L05 (and marginally L03/L11) towers fall once everything is awake — they stand, but
  fragile once hit; widen posts there if they feel too twitchy.

- **2026-11-08 (latest) — Live physics + tall Angry Birds structures + rolling wagons** (user: structures must be
  multi-storey and "slowly topple over and break down on the way down... balancing beams... a wagon on a mount that
  rolls down into a bigger structure... too far apart"). Framed levels now run LIVE physics: `BlockBase.GoLive()`
  makes every block Dynamic but ASLEEP (untouched stacks don't move; a hit wakes the touched part), gravityScale
  `BlockBase.LiveGravityScale` 0.6 (slower, readable topples), 0.6 s settling grace, `WakeNeighbours()` on destroy;
  robots `RobotEnemy.GoLive()` after the reveal pop (mass 4, asleep), crushed by impacts >= 2.5 (half) / 6 (all).
  New mount 6 Ramp (Kling art, plateau left + straight slope right, PolygonCollider from `MountRampPoly`) and
  `PlatformSpawnData.rolling`: a wagon parked Static until hit (`RollingWagon`: animal/projectile or momentum >= 3),
  then a 20-mass body nudged right that rolls down the slope carrying its cargo. design_world1.py v3: `tower()` (up
  to 5 tapering storeys), `seesaw()` balancing beams, `ramp_wagon()`; L05 Runaway Wagon, L09 Downhill Demolition
  (dynamite cargo), L13 Hay Ride, L17 Wagon Train use the ramp; all layouts between x 5.5 and 13.5. Box2D stacking
  of tall thin towers is unverified in Play mode — if a stack misbehaves when woken, widen its posts in the designer.

- **2026-11-08 (late) — Cosmetics: cannon skins, trails, impact effects** (user decision; hats removed). StoreProducts
  `Cannons` (golden, pumpkin, corncob, milkchurn, tractor) / `Trails` / `Impacts` (confetti, popcorn, fireworks,
  feathers, eggsplat); SaveManager `Get/SetEquippedCannon/Impact` + `GetEquipped(category)`; shop chooser, pages and
  Locker have three categories; ShopPromos rotates all three. In game: `CosmeticEffects.CannonSkin()` swaps the
  FarmCannon sprite on level start (skins sit on Cannon.png's 566x512 canvas at PPU 384, so offsets still fit);
  `CosmeticEffects.SpawnImpact` pops the burst when a robot dies (`ImpactBurst`). Art: Kling (5 skins, 3 bursts, a
  blank price card, 2 banners) + reused FeatherBurst/EggSplat, built by `tools/import_cosmetics.py` into
  `Sprites/Cosmetics/` and shop cards/icons in `Sprites/UI/Franchise/Shop/`. Store products to create: farmfury_cannon_*
  and farmfury_impact_* (hat products no longer exist). Unverified in Play mode.

- **2026-11-08 (night) — Angry Birds difficulty + mounts** (user request: "too hard requiring multiple hits"; structures
  on various mounts near and far). Robots: a direct animal hit kills (DirectHitDamageFraction 1.0; bosses keep their
  _damageResistance), falls 0.6, eggs 0.4. Stone base health 220 -> 70. Trimmed blocks take health/mass from their
  VISIBLE area (a post's padded rect was ~4x its wood). Platforms became a mount table (`LevelLoader.Mounts`,
  `LevelData.PlatformSpawnData.variant`): 0/1 floating islands, 2 Hill (art came out as an island — unused), 3 Mesa
  (tall rock), 4 Scaffold (deck only, shots pass the legs), 5 Wagon (bed only); each with measured surface + collider
  boxes generated into `Scripts/Level/MountMeasurements.cs` and `tools/mounts.json` by tools/import_cornfield_art.py
  (Kling art in MeadowRuins/Cornfield/Mount_*.png). design_world1.py gained `mount(kind, x, width)`; World 1 levels
  now spread targets near/mid/far on wagons, scaffolds, mesas and islands (L16: bridged twin towers on scaffolds).

- **2026-11-08 (evening) — World 1 rebuilt + new thrown abilities** (user requests, unverified in Play mode).
  L02-L18 redesigned Angry-Birds style in `tools/design_world1.py` (pieces described by visible edges: posts,
  planks, stone boxes/pillars/slabs, hay, barrels, raised islands, robot perches; it writes
  `Editor/LevelDataGenerator.World1.cs`, checks support + reach, and renders previews with `--render DIR`). All framed,
  difficulty ramps from a lean-to (L02) through towers, island huts, a stone bunker, a staircase, twin towers to the
  Commander's keep (L18). Edit the designs there, then Generate All Level Data. `LevelData.trimColliders` (set by
  WorldLevelGenerator for Worlds 3-9 built from these framed templates) keeps their colliders trimmed too.
  Abilities changed: Gerald = Feather Bombs, Woolly = Wool Balls, Billy = Mud Throw, all on the new runtime
  `AbilityProjectile` (burst-in-radius or bounce-and-hit; robots take egg-tier hits). Art in `Sprites/Abilities/`
  (Kling + Stampede FeatherBurst, via `tools/import_ability_badges.py`), wired onto the prefabs by
  `SceneSetup.WireAbilityProjectiles`; badges and Character Story text updated. SkyFlyby moved into the distance
  (behind the haze veil, smaller, slower, slightly transparent).

- **2026-11-08 (later) — World 1 cornfield backdrop + raised islands** (user request, framed World 1 levels only =
  L01 today). Existing art reused first: `Rough_Canvas/DistantBarn.png` is the far layer (sky, hazy hills, barn,
  windmill, silos). Only 8 Kling images were generated (16 credits, kling-image-v3_0_omni i2i at 2 credits/image,
  DistantBarn as style ref), saved in `FarmFury_Technical/FarmFury_Artwork/MeadowRuins/Cornfield/`; picks: MidCornfield_B,
  CornWall_A, PlatformWide_A/B. `tools/import_cornfield_art.py` cuts the white backgrounds, splits the corn wall into
  corn + soil strip, bakes the dark corner silhouettes and prints the measured anchors (soil grass line UV 0.837,
  island surface UV 0.754/0.699, barn base UV 0.052) into `Sprites/Environment/Cornfield/`. `EnvironmentDepthSystem`
  cornfield mode (`CornfieldActive`): far painting cover-fit with its barn base just behind the mid corn; tiled,
  mirror-every-other-tile strips with real parallax (mid corn follows the camera 0.55, corn wall + soil world-fixed,
  soil grass line on the physics ground, flat soil underfill when the camera dips); haze veil + mist at the corn
  tops; silhouettes order 40 in the corners. New `LevelData.platforms` (`PlatformSpawnData`: x, surfaceY, width,
  variant) spawned by `LevelLoader.SpawnPlatform` — layer Ground, tag "Ground", static box collider from the grass
  surface down, sortingOrder 1. L01's pile + robot stand on a 4.4 u island 1.6 u up (0.22 sh); `WorldLevelGenerator`
  drops anything on a template island back to the ground, so Worlds 3-9 are unchanged. Wired by
  `SceneSetup.Cornfield.cs`. Checked only against an offline mock-up render — unverified in Play mode.
  Follow-up the same day after the user's first device render: framed levels hide ALL of `Scenery_World1`
  (rocks, flowers, fences, tufts too); the cannon stands on a 3+2 haybale mount (`CatapultLauncher.ApplyMount`,
  framed only — lifts `_cannonRestPos`, so aim/fire/loaded animal all move with it; Haybail.png wired as
  `_mountBaleSprite`); L01 rebuilt on the ground as a shelter (2 posts + roof plank, placed by the planks' visible
  edges) with one SemiHarvester inside and one on the roof, plus a 3-bale stack — the island was dropped from L01
  (the platform feature stays for later levels); new `SkyFlyby.cs` (cornfield only): a biplane (MeadowRuins/Plane.png)
  crosses every 9-18 s with a smoke-puff trail, 30% of the time a Stampede scout drone instead; HUD lives queue
  halved (`HUDController` CardActive 104x135, CardQueue 78x101, smaller damage badge).
  **Block colliders trimmed to the art on framed levels** (`BlockBase.TrimColliderToArt`, from the sprite's tight-mesh
  vertex bounds; `Initialise(..., trimCollider)` passed `ReferenceFraming.Active(level)` by `LevelLoader`;
  `BlockBase.SetFlipX` mirrors the offset; health/mass/blast radius/explosion size still use `Footprint`). The sprites
  carry transparent padding (Plank_Horizontal's wood is the middle 48% of its height, the vertical post 27% of its
  width). NOT applied to unframed levels: their hand-dumped layouts rely on the padded rects touching —
  `tools/check_block_support.py` shows 109 levels would get blocks over a gap that `SettleIfUnsupported` drops at
  level start. Run that tool before turning on `referenceFraming` for any level (L01 passes).
  Later the same day (UI polish, user requests): Results score kept inside its box (best fit now wraps/truncates)
  and Play/Quit use the same edge inset as Home/Settings; Character Select carousel lowered/tightened
  (CarouselY -20, height 360, spacing 0.82), Play bottom-left / Back bottom-right, gold "PICK 3 ANIMALS" header
  above the lineup slots, white level name removed, and an ability badge (dark disc, bottom-right) on every card
  and filled slot from `MenuArt.abilityBadges` (`Sprites/UI/Franchise/Abilities/<Animal>.png`, made by
  `tools/import_ability_badges.py`: 5 reused effect sprites + 3 Kling icons for Woolly/Ducky/Billy).

- **2026-11-08 — Reference framework + L01 reframing** (user request: "not anything like angry birds"). Arcade's
  scrcpy/logcat recorder ported as `tools/review/` (record/import/analyze/compare in screen units) + dev-build
  telemetry `HighlightMarkers.cs`; `.\Run-Unity.ps1 build-android-dev`. An Angry Birds 2 session was measured into
  `docs/REFERENCE_FRAMEWORK.md`. Finding: World 1 sprites are in proportion with each other — the zoom was ~1.6x too
  close, and the physics ground (-6.6) sat a full unit below where every hand-placed prop/cannon stands (-5.6).
  `LevelData.referenceFraming` (L01 only so far) -> `ReferenceFraming.cs`: fixed ortho 8.0 (animal 0.045 sh),
  cannon at 0.26 sw, ground at 0.12 sh; `CatapultLauncher.SetGroundTop` moves the physics ground to -5.6 and
  `EnvironmentDepthSystem` pins the painted meadow (UV 0.19 of ParallaxForeground.png) to it. L01's bales/robot
  moved onto -5.6; `WorldLevelGenerator` shifts framed templates back to -6.6 (W3-W9 level 1 piles now rest on
  their ground, 0.43-0.66 lower than before). `CatapultLauncher.RobotNativePixelSize` still assumes 500 px robot
  art (files are 256 px) — only affects the old auto-zoom. Unverified in Play mode.
  Same day, step 2 + reveal (framed levels only): flight/aim come from `ReferenceFraming` screen-height targets —
  projectile gravity 0.5 sh/s² (gravityScale 0.40, passed through `AnimalBase.Launch(velocity, gravityScale)`;
  Woolly clones inherit it), launch speed 5.6-13.6 u/s, full pull 4.0 u, grab radius 2.0 u, preview dots scaled;
  the trajectory preview reads the same gravity. L01: 57 angle/power combos reach the pile, typical flight 1.0-1.5 s.
  New `Scripts/Level/LevelReveal.cs` (self-creating): AB2-style level intro — empty beat 0.35 s, all blocks appear at
  once in dust puffs, robots pop in top-first (0->1.15->1, 0.14 s apart), aiming locked via `LevelReveal.IsRevealing`.

- **2026-11-02 (later) — UI + art pass** (all user requests, unverified in Play mode): Character Select is Stampede's
  card carousel (tap the centred card, it flies into the next open lineup slot; slots start empty); the in-game HUD
  queue shows Stampede's thumbs-up portraits instead of cards; a boss clear goes Level Complete -> World Unlocked
  page (`WorldClearedScreen`, ported from Stampede) -> World Select, replacing the L18 transition video
  (`WorldTransitionManager`, `VideoChromaKey`, `ChromaKeyVideo.shader` deleted); real cannon poses for Ducky/Billy/
  Gerald (+ gaps for Percy/Bessie/Woolly/Horace) imported from FarmFury_Artwork; FarmFury logo on the results/pause/
  new-character canvases and the Leaderboard; `Sprites/AppIcon.png` set as default icon. `FarmFury_GDD.md` (v3.0)
  created as the living design doc.

- **2026-11-02 — Worlds 2-9 built from World 1's towers + Stampede art** (user request). `tools/import_stampede_worlds.py`
  copies Stampede's per-world backdrop (`<XX>_Parallax_Far` -> `Skies/World<N>_Backdrop.png`), props
  (`Environment/World<N>Props/`), robots (`Enemies/Robot/W<N>/<Tier>_left|right.png`, cropped to FarmFury's 256 px /
  bosses 447 px convention so World 1 robot scales carry over) and stand-in poses for Ducky/Horace/Gerald/Billy.
  `WorldLevelGenerator` (run by Generate All Level Data) makes `W<NN>_L<NN>_*.asset` for W2 levels 2-22 and every level of
  W3-W9 (counts from WorldDataGenerator): each is a World 1 tower L01-L17 in ramp order, mirrored every other level
  (`BlockSpawnData.flipX`), shifted ±0.35, with a few planks turned to stone and robots tier-upgraded more in later worlds;
  the last level is L18's fortress with the world's boss (RobotType.Commander). Same blocks everywhere (W2 keeps its ice
  reskin). Runtime by `LevelData.world`: `EnvironmentDepthSystem._worldBackdrops` (one uncropped painting, Midground/
  Foreground hidden), `LevelLoader._worldRobots` (prefabs `W<N>_<Tier>Robot`, made by `SceneSetup.Worlds.cs`),
  `SceneryBuilder._worldProps` (structures / clutter / sky props, seeded per level). `Scenery_World1` now only shows in
  World 1. Lineups lead with the world's new animal; paid worlds use Cluck/Bessie. World 2's boss uses the Commander.

- **2026-11-02 — Win/lose videos removed** (user request): `LevelCompleteManager`/`LevelFailedManager` now only run the
  0.5s slow-mo beat, then open Results — no Cluck celebration / robot taunt clip, and SceneSetup no longer wires them.
  `VideoChromaKey` stays (the L18 `WorldTransitionManager` video still uses it). Sections below describing the
  celebration sequence are historical.

- **2026-11-02 — Franchise flow: everything outside gameplay now follows FarmFury_Stampede/Arcade** (branch
  `align-franchise-ui`). User decisions: drop the MatchUp VS/countdown screen; keep a pre-level character select
  that offers animals as their worlds unlock; revive = +1 animal (5 coins / ad); Stampede art as stand-ins plus a
  Kling list (`docs/ART_REQUIREMENTS.md`); full monetisation; add the three paid worlds. Branch pushed to origin
  2026-11-02, not merged into `main`. Phases, one commit each:
  (1) save/data foundation, (2) GameFlow + Landing/World/Level/Character Select, (3) Pause/Results/New Character,
  (4) Settings family, (5a) store/ads/analytics/coins/revive/shop, (5b) hats/trails/Locker, (6) home-screen name,
  F1 debug panel, docs. Removed: MainMenuController, WorldMapController, World2MapController,
  World2LandingController, LevelMarker, MatchUpScreen, World2CharacterSelectScreen, GameplaySettings, and the HUD's
  result panels/Quit/Mute. Existing World 1 progress migrates once from `ff_stars_N` to levelId keys.
  Follow-up (same day, after the user re-ran Generate All Level Data + Wire Scene References in the Editor): every
  LevelData asset now serializes `isBossLevel` (only L18 = 1); the LevelPlay package added the scripting define
  `LEVELPLAY_DEPENDENCIES_INSTALLED` (Standalone) to ProjectSettings — expected, keep it; the Editor re-fitted the
  parallax layer scales to its Game view (`EnvironmentDepthSystem.RescaleToCamera`, recomputed at runtime anyway).

- **2026-08-09** — Large difficulty/stability session: a `[SerializeField]`-staleness audit,
  damage/explosion rebalance, several real bug fixes, World 1↔2 navigation, and a new World 2
  character-select screen. Full trace/reasoning for each finding in `docs/HISTORY.md`; current
  truth only, here:
  - **Fixed a real six-week-old live bug**: `impulseDamageMultiplier`/`minDamageImpulse` on
    `Robot.prefab` and both `WoodBlock.prefab`/`StoneBlock.prefab` were still serialized at
    `2.5`/`2.0` — the pre-2026-06-27 balance values — because a `[SerializeField]` default change
    never retroactively updates an already-existing prefab. Now self-healed unconditionally in
    `BlockBase.Awake()`/`RobotEnemy.Awake()` (same pattern `baseMaxHealth`/`baseMass`/`bounciness`
    already used), plus the stale prefab values corrected directly. Audited every other
    `[SerializeField]` on both classes for the same exposure — found one more live instance
    (`_explodeSizeMultiplier`, see below) and two look-alikes that are NOT bugs
    (`RobotEnemy._damageResistance`/`_structuralDamageResistance` — `CommanderRobot`'s `0.61`/`0.1`
    is deliberate boss tuning, self-healing those would have erased it).
  - **Difficulty rebalance** (user report: one animal/one barrel could clear an entire
    structure, sometimes without the blast even touching most of what died): narrowed Cluck's
    egg-cluster spread 80°→50° and Bessie's ground-slam radius/damage 3.6u/30→2.4u/22; stopped
    plain Wood/Stone destruction from chain-damaging neighbours at all (only genuine explosive
    props — Haybale/Barrel — still can); shrank the explosive blast radius `ExplosiveForceScale`
    0.4→0.25 of a prop's own footprint and halved damage against another explosive prop caught in
    the blast, so barrel/haybale chains need deliberately-close placement, not accidental level
    spacing; staggered the block-collapse cascade's recursive continuation (0.1s/tier — the block
    directly above a hit still wakes instantly, only checking what rests on THAT block is
    deferred) so a tall structure comes down over real time instead of one physics frame; raised
    Wood's base health 5→8. Also found and fixed the actual root cause of "one touch explodes the
    whole structure, even where the blast never touched": the support-loss wake cascade has no
    distance limit at all, and low block HP meant a falling piece landing on the next block often
    one-shot it too — a pure-physics domino chain unrelated to any blast radius.
  - **Debris-size fix**: `_explodeSizeMultiplier` — Stone/Haybale/Barrel were still rendering
    death bursts at `2.2` (over 4x Wood's correct `0.5`), the pre-2026-07-10 value, on the same
    "changed default never reaches an existing prefab" bug shape as above. Reduced to `1.0` (2x
    Wood's, not 4.4x) per explicit request, self-healed in each block type's own `Awake()`.
  - **`BlockBase.DestroyBlock()` now unconditionally re-runs the support-loss wake cascade** —
    `RobotEnemy.Die()`'s `ForceDestroy()` path (a boss crumbling a guarded structure) skipped
    `TakeDamage()` entirely, so anything resting on that block stayed permanently floating,
    ignoring gravity. Confirmed via `git blame`-style tracing, not guessed.
  - **World 1 ↔ World 2 navigation fixed** — added `GameManager.CurrentWorld` (persisted via
    `PlayerPrefs`), set the moment a player enters World 2 through `World2LandingController`. Both
    `WorldMapController` and `World2MapController`'s auto-show-on-`Idle` are now gated on it, and
    `MainMenuController`'s PLAY button reads it — a player who's reached World 2 once no longer
    has to replay L18 to get back in. Also fixed a genuine solid-black-screen bug this surfaced:
    `World2MapController` never hid itself once a level actually started (only ever hid via its
    own Back button), so `MatchUpScreen`'s fade-to-black overlay — a child of its canvas — stayed
    on screen forever after launching. Added an Editor debug menu (`FarmFury → Debug → Switch To
    World 1/2 Map`) for testing either state without replaying the unlock chain.
  - **Fixed Percy/Woolly's character art** — folders were named `Percy_Pig`/`Woolly_Sheep` on
    disk, but `SpriteWiring.cs` looks for the short-name convention every other character uses
    (`Percy`/`Woolly`) — this exact failure mode is already documented above in "Auto-Compile
    Pipeline." Silent mismatch left both animals on the salmon-pink procedural-fallback circle.
    Renamed both folders; re-running **Wire Sprites** now correctly wires all three of Percy's
    poses (`Loaded`/`in flight`/`Trigger`).
  - **`LevelLayoutDumper.InferWoodArtVariant()` was missing keyword checks for barrel/cart/wall/
    tower** despite `BlockBase` already having dedicated sprite fields and `WoodArtVariant` enum
    cases for all four — any `WoodenBarrel`/`WoodenCart`/`RuinedStoneWall`/`StoneTower` sprite
    silently fell through to `Auto` on every dump. Added the four keyword checks, then mechanically
    patched the 24 already-known-affected `Auto` instances across L03-L18 using each `B(...)`
    call's own `// sprite 'X'` comment as ground truth (not re-authored by hand).
  - **Repositioned 4 of 6 flagged heavy-overlap placements** in L06 (2 Stone_Vertical posts vs.
    the WoodenCart), L09 (1 SemiHarvester vs. Harvester), and L10 (1 dynamite barrel vs. a wood
    plank) — each re-verified against every other object in its level, not just the flagged pair.
    Left 2 SemiHarvester-vs-Harvester pairs (L07, L09) for manual review — their large sprite
    footprints made any small-nudge fix unreachable without abandoning the robot's guard position;
    confirmed these are cosmetic sprite-overlap only, not a physics bug (`RobotEnemy`'s collider
    is always a fixed 0.6×0.9 regardless of visual scale).
  - **New: `World2CharacterSelectScreen`** (`Scripts/UI/`) — a tap-to-choose roster grid shown
    before World 2's existing match-up VS-slam/countdown sequence. Percy/Woolly/Cluck/Bessie are
    selectable (derived from which `AnimalType` slots already have real card art in
    `World2MapController._animalCardSprites` — not a hardcoded list, so a future animal unlocks
    itself the moment its art is wired); Ducky/Horace/Gerald/Billy show locked (no art exists for
    them anywhere in the project yet). Picks fill a lineup queue sized to the level's own bird
    count, shown next to a non-interactive robot preview; confirming sets a new one-shot
    `GameManager.BirdQueueOverride` that `LevelLoader.LoadLevel()` consumes and clears without
    ever mutating the `LevelData` asset itself. Not visually verified in Play mode (no Play-mode
    access here).
  - Every fix in this session was closed by actually running `.\Run-Unity.ps1 check`/`setup`/
    `levels` directly (not just asking the user to) wherever the project state allowed it —
    several earlier attempts hit the batch-mode-vs-open-Editor project lock; retried once the lock
    cleared and confirmed clean each time.
- **2026-08-03** — Robot-vs-block damage rebalance, user-requested. (1) **Block explosions no
  longer damage robots at all** — `RobotEnemy.TakeExplosionDamage()`/`ExplosionDamageFraction`
  removed entirely; a robot now only takes damage from a direct animal hit, a connecting egg, or
  `TakeFallDamage()` when its own support block is destroyed (structural collapse, not a blast).
  (2) **Chain-reaction gating**: `WoodBlock._explodesOnRobots` renamed `_isExplosive` and
  repurposed — a plain block's (Wood/Stone) own destruction chain can no longer detonate a nearby
  explosive prop (Haybale/ExplodingBarrelBlock); only a direct hit from a character/ability can
  trigger THEIR explosion. An explosive prop's own chain is unchanged — it still freely
  damages/detonates anything nearby, so a Haybale/barrel exploding still chains into nearby
  Wood/Stone. (3) **StoneBlock gained its own death-burst art** — `_sprDamaged`/`_sprExplode` wired
  to a new user-supplied `StoneDebris.png` (was previously unwired, `fileID: 0`) — deliberately not
  a reuse of WoodBlock's `WoodDebris.png`, so shattering stone doesn't look like broken wood.
- **2026-07-26** — Recovered from a real data-loss incident caused by the cannon/scenery preview
  fix directly below (user report: "we have erased the barn and all the scenery from world 1, and
  nothing has rendered correctly from world 2"). Two separate problems, both traced to
  `GameObject.Find()` only ever searching ACTIVE objects:
  1. **World 1's hand-placed `Scenery_World1` children (`OldBarn_Right`, `OakTree`, `Windmill`,
     all 3 `WoodenFence`s, etc.) were gone from the saved scene entirely.** Recovered by diffing
     against the last git commit (none of this session's scene edits had been committed) and
     splicing the missing GameObject/Transform/SpriteRenderer blocks back in under the current
     `Scenery_World1`, generating fresh non-colliding fileIDs. (First recovery attempt used
     `max(existing fileID) + 1000` for new IDs and picked up Unity's own reserved
     `SceneRoots` sentinel object, whose fileID is exactly `Int64.MaxValue` — pushed the new IDs
     past it into corrupt territory; caught before saving, redone with a bounded-range generator.)
  2. **Three separate `Scenery_World2` hierarchies existed at once** — `EnsureWorld2Scenery`'s
     `GameObject.Find("Scenery_World2")` silently returned null whenever the group was inactive
     (from the very preview-toggle fix below), so it "helpfully" created a brand-new placeholder
     copy each time instead of finding the real one. The user's actual hand-built World 2 scene
     (17 objects — their own IceCannon prop, ice signs, snowdrifts, etc., not just the original
     5-prop placeholder) was still fully intact, just buried inactive under two duplicate
     placeholder copies — identified by content and restored as the only surviving copy; the two
     placeholder duplicates were deleted (including their entries in the scene's `SceneRoots`
     list). Root cause fixed properly: new `SceneSetup.FindIncludingInactive()` /
     `SceneryBuilder.FindIncludingInactive()` helpers (search the full hierarchy via
     `GetRootGameObjects()` + `GetComponentsInChildren(includeInactive: true)`) now back every
     lookup that could ever target a toggled-off object (`Scenery_World1`, `Scenery_World2`,
     `FarmCannon`, in both `SceneSetup.cs` and `SceneryBuilder.cs`).
- Fixed a real Edit-mode preview gap (user report: cannon "showing up somewhere
  it shouldn't", i.e. World 1's `Cannon.png` still visible while looking at World 2 in the Scene
  view). Root cause: `CatapultLauncher`'s cannon-sprite swap and `SceneryBuilder`'s
  `Scenery_World1`/`Scenery_World2` toggle both only fire at RUNTIME (`OnLevelStarted`) — an event
  that never fires in Edit mode — while the parallax backdrop already had its own Edit-mode
  preview (`PreviewWorld1Backdrop`/`PreviewWorld2Backdrop`). So switching backdrop previews never
  touched the cannon or scenery, leaving them stuck on whichever world was last shown. New shared
  `SceneSetup.PreviewCannonAndScenery(isWorld2)` helper now runs from both the manual preview menu
  items AND unconditionally at the end of every `Wire Scene References` pass (which always resets
  the backdrop to World 1 too) — so all three now always agree.
- L19 "Frostbound Watch" replaced with the user's real hand-built layout, dumped
  via the now-fixed `LevelLayoutDumper` (see the fix entry directly below) — 6 blocks + 3 robots
  (2 IceHarvestorRobot, 1 GlacierHarvestorRobot). `Generate All Level Data` regenerated the asset,
  `Wire Scene References` re-ran clean and auto-deleted the `LevelScratch` design container per
  normal cleanup. Noted in passing: that same wire pass logged `Created 'FarmCannon' GameObject`
  — it was missing from the saved scene and got recreated at `SceneSetup`'s documented fallback
  position `(-3.72, -4.7032)` scale `(1.47, 1.33)`, which matches this file's own "current truth"
  coordinate, so not a regression, but flagged in case the user had it somewhere else on purpose.
- Fixed a real `LevelLayoutDumper` bug (user report: "dumping but its not picking
  up any of the new robots") — World 2's Harvester/SemiHarvester-tier prefabs and sprites are
  spelled **"Harvestor"**/**"Glacier..."** (an o, matching `IceHarvestorRobot.prefab`/
  `GlacierHarvestorRobot.prefab`), but the dumper's type-detection only ever checked for World 1's
  spelling, **"Harvester"**. `GlacierHarvestorRobot`/`IceHarvestorRobot` prefab instances fell
  through to the generic "Robot" catch-all and silently dumped as `RobotType.Basic`; their raw
  sprites (`IceHarvestor_left.png` etc., which don't even contain "robot" as a substring) were
  skipped entirely. Both the prefab-path switch and the `LevelScratch` raw-sprite keyword chain
  now recognise "Harvestor"/"Glacier" alongside the existing "Harvester"/"SemiHarvest".
- World 2 parallax + scenery pass, both user-requested after seeing the current
  state. **Parallax**: `_midgroundClipAboveW2` raised 0.21 -> 0.60 to show far more ice terrain and
  far less aurora sky ("too much night sky in the parallax") — knowingly reintroduces a seam where
  Midground's own baked sky peeks through (verified via pixel composite at 0.35/0.45/0.55/0.60
  before picking 0.60); the user explicitly chose to accept that over waiting for revised Midground
  art. Field is now force-applied every Wire Scene References pass (previously a first-creation-
  only default) so it can't silently revert. **Scenery**: new `Scenery_World2` group (Igloo, 2x
  IceTree, BuriedRuin, IceSpire/IceCrystals/IceBoulder — `SceneSetup.EnsureWorld2Scenery`),
  code-placed (no Play-mode/Scene-view access here to hand-place it like `Scenery_World1`) by
  reading that group's own real `Game.unity` coordinates and computing comparable positions/
  scales — unverified, flagged as a first draft. `SceneryBuilder` gained
  `ToggleHandAuthoredScenery()`, shows/hides `Scenery_World1` vs. `Scenery_World2` by
  `LevelData.world` on every level load (previously neither group was ever hidden — harmless while
  only World 1 existed, but would have rendered both worlds' background structures on top of each
  other now that World 2 levels share the same world-space region).
- Two more World 2 visual reskins, both following the same "swap purely by
  `LevelData.world`" pattern already used for robots/backdrop (user question: "how do i build
  level 19 with new sprites, cannon"). **`CatapultLauncher._cannonSpriteW2`** (wired from
  `IceCannon.png`) swaps in `OnLevelStarted` for any `world=2` level — visual reskin only, the
  GDD's real Ice Cannon mechanic (angle + ricochet) isn't implemented. **`BlockBase` gained a
  5-shape ice/snow reskin** (`_sprNormalW2`/`_sprVerticalW2`/`_sprHorizontalW2`/`_sprDiagonalW2`/
  `_sprShortW2`, wired on both `WoodBlock.prefab` and `StoneBlock.prefab`) — `Initialise()` now
  takes a `world` param (threaded through `LevelLoader.SpawnBlock`) and resolves these 5 fields
  first when `world==2`, falling back to World 1 art for both an unwired W2 field AND every other
  `WoodArtVariant` shape that has no ice counterpart at all yet (see the matching Known Issues
  entry on coverage). Still plain `BlockType.Wood`/`BlockType.Stone` underneath — this is a visual
  reskin, not a new block type.
- World 2's MatchUp screen re-wired against a dedicated new art folder,
  `Assets/Sprites/UI/Matchup_W2/` (relabelled/corrected versions of the backdrop, robot cards, and
  a full ice-themed VS/countdown set — separate from the original assets that used to live mixed
  into the shared `Assets/Sprites/UI/MatchUp/`). `SceneSetup.WireWorld2MatchUpCards` now sources
  everything World-2-specific from this new folder; only genuinely shared/world-agnostic assets
  (Countdown.mp3, Btn_skip.png) still come from the old one. Per-level headers (global indices
  18-20) are now pre-composited images — `tools/compose_w2_level_headers.py` (new) merges the
  "LEVEL" banner (`W2_level.png`) with each numbered badge (`19.png`/`20.png`/`21.png`) into a
  single `LevelNN.png`, number centred under the banner with its top ~18px tucked behind the
  banner's bottom edge (paste order: number first, banner second) — per user request ("match the
  number under it (middle with slight overlay)"), matching how World 1's own `LevelHeader1.png`
  reads as one flat image rather than two separately-positioned layers (`MatchUpScreen` only ever
  shows one header sprite at a time, no runtime layering support). See the new Known Issues entry
  on why that array's indexing is fragile past local World 2 level 3, and the World-2-countdown-"1"
  entry for a related art gap found during this pass. **Cluck's
  fly-by REPLACED with Percy's** for any level whose `birds[0]` is Percy (World 2's own debut
  animal) — `MatchUpScreen` gained a parallel `PercyFlyBy()` pass reusing Cluck's exact arc/timing
  but spinning the icon continuously (`PercyRollSpinDegPerSec`) and using Percy's actual Bounce
  Roll ability art (`Percy_Pig/Trigger.png`, the curled-into-a-ball pose) instead of a plain
  in-flight sprite, per explicit user request ("have the pig flythrough doing its power of
  rolling"). `MatchUpScreen.Init()`, `WorldMapController`, and `World2MapController` all extended
  with the new `_percyFlySprite`/`_percyFallingClip` fields (wired on both maps for symmetry, even
  though only World 2 can trigger it today). **`CatapultLauncher` gained a World 2 cannon
  reskin** (`_cannonSpriteW2`, wired from `Assets/Sprites/Environment/Launchers/IceCannon.png`) —
  swapped purely by `LevelData.world` in `OnLevelStarted`, same "swap the sprite, not the
  GameObject" pattern `EnvironmentDepthSystem` uses for the backdrop. Visual reskin only — the
  GDD's actual Ice Cannon mechanic (angle + ricochet) is NOT implemented; the launcher is still a
  single static, non-rotating prop regardless of which sprite is showing.
- World 2's parallax backdrop re-wired against a user-supplied
  `ParallaxFarHills_W2.png` (aurora/starfield sky) via `Wire Scene References`; composited a
  simulation of the actual `ParallaxBandClip` shader logic against the existing
  `ParallaxMidGrounds_W2.png` and confirmed the existing clip tuning (FarHills=1.0,
  Midground=0.21, set 2026-07-19) still produces a clean seam with the new art — no retuning
  needed. Added World 2's first LevelData, L19 "Frostbound Watch" (`W2_L01`, id sorts right after
  L18 so it lands at `GameManager` index 18 / `World2MapController.GlobalIndexOffset`, i.e. the
  game's "Level 19") — Percy (pig) vs. a single IceHarvestorRobot, making the already-wired World
  2 MatchUp screen (`Matchup_W2.png` backdrop, `IceHarvestor_card.png`/pig cards, VS + 3-2-1-ready
  countdown, all wired since 2026-07-19) reachable in-game for the first time. See the Known
  Issues entry above — L19's block layout is a coordinate-math placeholder, not a real
  `LevelLayoutDumper` dump. `LevelDataGenerator.cs`'s Commander-boss fix and the
  `RobotEnemy.ExplosionDamageFraction` balance change below happened the same session.
- L18's Commander boss robot restored from a bare `scale (1,1)` (a stale dump
  artifact — flagged since 2026-07-19, user-reported "commander is very small") to its last
  known-good `6.985x7.555` in `LevelDataGenerator.cs`; worth a fresh `LevelLayoutDumper` re-dump
  once someone can eyeball it against L18's redesigned staircase in-Editor. Also lowered
  `RobotEnemy.ExplosionDamageFraction` 0.55 -> 0.20 (user request) — a haybale/barrel exploding
  directly against a robot now chips ~20% of its health instead of ~55%; block-to-block chain
  destruction (`WoodBlock.DamageNearby`'s `_areaDamage` falloff, which still levels neighbouring
  wood/ice blocks at full force) is a separate code path, untouched by this constant.
- **2026-07-19** — All 18 World 1 levels (L01-L18) redesigned via fresh `LevelLayoutDumper` dumps
  against the rebuilt parallax backdrop. Two live gameplay bugs fixed: structures left floating
  after being struck (`BlockBase`'s wake-cascade overlap box was too narrow), and the cannon going
  permanently unresponsive from L08 onward (an animal whose flight arc missed every collider fell
  forever, never clearing `CatapultLauncher`'s busy flag — fixed with a flight-time cap + far-
  below-ground fallback in `AnimalBase`). World 2 ("Frozen Tundra") front-end scaffolding built:
  `LevelData.world` field, `EnvironmentDepthSystem`'s World-2 backdrop branch (measured via pixel
  classification, not guessed), `World2LandingController`/`World2MapController` (22 markers,
  path hand-traced against the art, re-traced once after the source art changed),
  `FrostRobot`/`IceHarvestorRobot`/`GlacierHarvestorRobot` prefabs (reskins of the existing Basic/
  Harvester/SemiHarvester tiers, not new `RobotType` values), World-2 match-up art, an ice-prop
  ground-seam scatter, World 1's scenery grouped under `Scenery_World1` for easy hide/show, and
  Editor-only debug tools (jump to L18, preview either world's backdrop, show the World 2 screens
  directly) — all Play-mode-only or Editor-preview-only, zero production behavior change for World
  1 players. No real World 2 levels exist yet.
- **2026-07-18** — Parallax backdrop rebuilt as a real 3-layer masked stack (FarHills/Midground/
  Foreground, camera-reactive per-level zoom) after several earlier single-painting attempts all
  failed to cover every level's auto-zoom range; user confirmed happy with the result. L02
  redesigned. Robots given a left/right walking-patrol animation and (Commander only) a continuous
  3-pose taunting loop. Several real "Wire Scene References stomps hand-placed Editor state" bugs
  found and fixed along the way (FarmCannon position, `Layer_Midground` transform, a
  `GameObject.Find("Background")` name collision).
- **2026-07-16** — Monetisation (Phase 6) planning session (Supabase over Firebase, Crashlytics
  standalone). Settings page rebuilt into a 7-tab layout. L18 → "Frozen Tundra" world-transition
  video wired (lands on the main menu at the time — superseded 2026-07-19, now lands on
  `World2LandingController`). Several level-specific bug fixes (L11 stray robot, L12/L14 mid-air
  structure collapse cascade, L15 duplicate blocks, L17 header art).
- **2026-07-13 to 2026-07-14** — Cannon repositioned (and camera auto-zoom/launch-speed re-tuned
  to match), Commander boss toughness split into direct-hit vs. structural resistance, L18 redesigned
  into a Commander-solo boss fight.
- **2026-07-09 to 2026-07-12** — World 1 fully built out (L01-L18, no gaps) via the
  `LevelLayoutDumper` hand-authoring workflow, which is still the standard way to build a level
  (see "Adding a New Level" below). Introduced `RobotType.SemiHarvester`/`Commander`, the
  `WoodArtVariant` sprite-shape system, and the block/robot damage-factor model (`RobotEnemy`'s
  `DirectHitDamageFraction`/`ExplosionDamageFraction`/`EggDamageFraction`/`FallDamageFraction`)
  still in use today.
## Franchise flow (outside gameplay) — current truth as of 2026-11-02

Ported from `C:\Users\Personel\Desktop\FarmFury_Stampede` (code-built uGUI, itself ported from FarmFury_Arcade).
When changing any menu, save or shop behaviour, match Stampede's behaviour and numbers unless FarmFury's gameplay
needs otherwise. New code lives in namespaces `FarmFury.UI` (Scripts/UI/Flow, Scripts/UI/Kit), `FarmFury.Core`
(SaveManager, DataManager, monetisation) and `FarmFury.Utilities`; the older gameplay code stays global.

```
Landing (LandingPage.png poster; Play / Exit (not iOS) / Settings cog)
  -> World Select (badge carousel; 6 story worlds, then 3 paid worlds; paid & unbought -> world shop)
  -> Level Select (world backdrop; padlock / question / 1-2-3 star boards / boss shield; 6 cols, 8 above 12 levels)
  -> Character Select (Stampede's card carousel; tap the centred card and it flies down into the next open lineup
     slot; locked = silhouette; slots = LevelData.birds.Length, start empty, tap a slot to clear; Play once all full) -> Playing (HUD) -> Pause (resume / X / home / cog)
  -> revive prompt when the queue runs out -> Results (Level Complete / Failed canvas; payout, Double Coins, promo)
Settings cog -> Menu hub (SETTINGS / SHOP / Visit Our Store)
  -> Settings (Sound / Leaderboards / Character Story / Policies)   Shop (Coins + Restore / Worlds / Remove Ads / Cosmetics)
Overlays (OverlayScreen) open on top of any screen; Esc / Android back closes the top one, else steps back.
```

- **Save** (`SaveManager`, `FF_` keys): stars/completion/records by `levelId`; a world unlocks when the previous
  world's boss level (`LevelData.isBossLevel`, L18 for World 1) is cleared, a paid world by purchase; levels unlock
  in order, the boss after all others. Animals: `AnimalRoster.IntroWorld` (Cluck/Bessie W1, Percy/Woolly W2,
  Ducky W3, Horace W4, Gerald W5, Billy W6) — unlocked with their world, nothing stored. Coins, Remove Ads, worlds
  and cosmetics are checksum-protected (Stampede's FNV salt scheme).
- **Worlds**: 9 `WorldData` assets (`WorldDataGenerator`); `LevelData.world` (1-based int) maps to `WorldType`.
- **Monetisation**: `farmfury_` store ids (`StoreProducts`); Arcade prices (Remove Ads $4.99 +100 coins; coins
  100/500/5000/15000 at $0.99/$3.99/$9.99/$19.99; worlds $3.99; cosmetics $1.99 or 2500 coins). Cosmetics (2026-11-08, hats dropped): cannon skins, trails,
  impact effects — all shared by every animal (no per-character variants, no machines). Ads: rewarded (revive, Double Coins),
  interstitial every 6 level loads, banner on Pause / Level Failed — none with Remove Ads; inert without keys.
  Purchases and external links go through `ParentalGate`.
- **Art**: `MenuArt` / `ShopArt` / `LeaderboardArt` on GameFlow and per-world fields on `WorldData`, all wired by
  `SceneSetup.EnsureGameFlow` from `Assets/Sprites/UI/Franchise/` (Stampede stand-ins) and FarmFury's own
  `Sprites/UI/Icon` buttons + `LandingPage.png`.

## GDD Summary (see `FarmFury_GDD.md` for the full design)

### 8 Animals (tap mid-flight to trigger ability)
| Animal | Ability | World introduced |
|---|---|---|
| Cluck (chicken) | Cluster Bomb — 5 eggs scatter on tap | World 1 |
| Bessie (cow) | Ground Slam — radial shockwave on landing | World 1 |
| Percy (pig) | Bounce Roll — curls, bounces 3× gaining speed | World 2 |
| Woolly (sheep) | Wool Balls — throws 3 bouncy wool balls ahead (each hits up to 3 times) | World 2 |
| Ducky (duck) | Skip Shot — skips flat surfaces 3× | World 3 |
| Horace (horse) | Rear Kick — kicks debris backward on impact | World 4 |
| Gerald (turkey) | Feather Bombs — drops 3 bombs that burst in a small radius | World 5 |
| Billy (goat) | Mud Throw — hurls a mud ball that splats over a wide radius | World 6 |

All eight are scripted and art-wired; each leads the lineups of the world that introduces it (Worlds 2-6).

### 9 Worlds (160 levels: 6 story + 3 paid) — per-world launchers/mechanics below are PLANNED; every world uses the Farm Cannon today
| World | Launcher | Key mechanic | Levels |
|---|---|---|---|
| 1 — Meadow Ruins | Farm Cannon (drag angle+power) | Character weight affects range | 18 |
| 2 — Frozen Tundra | Ice Cannon (angle + ricochet) | Freeze zones slow flight | 22 |
| 3 — Watermill Village | Water Wheel (timing — tap to fire) | Fire spread on wood | 22 |
| 4 — Sky Islands | Airdrop Biplane (timing — tap to drop) | Updraft column steering | 24 |
| 5 — Sunken City | Torpedo Tube (angle + bubble pop) | Current lanes + lever switches | 22 |
| 6 — Robot Mothership | Gravity Sling (angle + gravity wells) | Zero-G ability modifiers | 16 |
| 7 — Dustbowl Canyon (paid) | Farm Cannon | — | 12 |
| 8 — Harvest Fairground (paid) | Farm Cannon | — | 12 |
| 9 — Crop Factory (paid) | Farm Cannon | — | 12 |

World 1 is hand-built (L01-L18). World 2's level 1 is L19 (hand-dumped); every other level of Worlds 2-9 is generated from World 1's towers by `WorldLevelGenerator` (2026-11-02, see Recent work), with each world's own robots, boss, backdrop and props. None of it verified in Play mode.

### Star System
- 1★ — all robots dead (progress gate)
- 2★ — all dead + 1 animal remaining (mastery)
- 3★ — all dead + 2+ animals remaining (prestige)
- "Perfect" — 3★ + all blocks destroyed (hardcore/leaderboard goal) — **not implemented**

---

## Commands

### Unity (Editor)
Open `unity/` in Unity Hub (Unity 6.5 / 6000.5.0f1). Open `Assets/Scenes/Game.unity`. Press Play — the ground, camera, and LevelLoader reference are all self-wired at runtime.

### Run-Unity.ps1 (batch automation)
Primary interface for batch Unity operations. Run from the repo root in PowerShell:

```powershell
.\Run-Unity.ps1 levels        # Generate/overwrite every LevelData asset (L01-L19 + Worlds 2-9) from LevelDataGenerator.cs —
                               #   safe: the generator is the source of truth for all of them
.\Run-Unity.ps1 setup         # Wire all Inspector references in Game.unity (= Wire Scene References)
.\Run-Unity.ps1 check         # Compile check — exits 1 on error
.\Run-Unity.ps1 build         # Windows 64-bit -> unity/Builds/Windows/
.\Run-Unity.ps1 build-webgl   # WebGL build  -> unity/Builds/WebGL/
.\Run-Unity.ps1 build-android # Android APK  -> unity/Builds/Android/
```

Logs written to `unity/Logs/batch_<command>.log`. Filtered output is printed to console.

### Editor Menu Items
| Menu | When to run |
|---|---|
| **FarmFury → Wire Scene References** | After adding a new prefab, level, or after clean checkout. Wires all Inspector refs in Game.unity. Also sets camera position/orthoSize and `_cameraRestOffset`. |
| **FarmFury → Generate All Level Data** | Recreate every LevelData asset (L01–L19; overwrites existing, sets `isBossLevel` — L18 only) and delete any stale `.asset` for a level whose `Make(...)` call was removed (see `DeleteStaleAsset`). L01 is kept in sync — safe to run. Run Wire Scene References after. |
| **FarmFury → Wire Sprites** | After adding new character art to `Assets/Sprites/Characters/`. Sets PPU and wires pose sprites into all 8 animal prefabs. |
| **FarmFury → Reimport Sprites** | Force-reimport all sprites in Launchers/, Characters/, and Enemies/ folders. Run if sprites look stale after the art pipeline. |
| **FarmFury → Reset Progress** | Clears all progress keys (SaveManager's `FF_` keys by levelId, plus the pre-port `ff_*_N` keys) — coins and purchases are kept. |
| **FarmFury → Generate World Data** | Creates/updates the 9 WorldData assets in `Assets/ScriptableObjects/Worlds` (identity fields only; art is wired by Wire Scene References). Run automatically by Wire Scene References when none exist. |
| **FarmFury → Debug → Open World Select / Open Level Select: … / Jump To Level 18 / Unlock All Levels** | Play-mode QA shortcuts (DebugJumpToLevel.cs). Unlock All Levels is a pref toggle (Editor/dev builds only). In Play mode **F1** opens the runtime DebugPanel (coins, unlocks, world/boss/Remove Ads/paid-world toggles, reset). |
| **FarmFury → Debug → Dump Level Layout To Log** | Preferred level-authoring workflow as of 2026-07-09 (see `LevelLayoutDumper.cs`). Drag real block/robot prefabs anywhere in the Scene view, or raw sprites under an empty GameObject named exactly `LevelScratch`, then run this — it writes ready-to-paste `B()`/`R()` code to `unity/Logs/level_layout_dump.txt` from the objects' actual transforms. **Delete the `LevelScratch` GameObject after dumping** — left in place it renders on top of every level regardless of which is loaded. |

### Art pipeline
After adding new Kling AI PNGs to `assets/` (currently absent from disk — see Current Status above):
```bash
pip install Pillow
python tools/remove_backgrounds.py   # strips white bg, writes to unity/Assets/Sprites/
```
Then open Unity — `EditorAutoSetup` and `SpriteAutoImporter` handle re-import automatically on compile. If sprites look stale, run **FarmFury → Reimport Sprites**.

`assets/` layout (per `tools/remove_backgrounds.py`'s `CHAR_MAP` and output paths):
```
assets/
  Backdrops/          sky painting, prop reference sheets
  <Name>_<Animal>/    per-character pose PNGs (e.g. Cluck_Chicken/ -> unity Characters/Cluck/)
  FarmCards/          HUD card portraits
  LevelCards/         6 world card images (all 6 exist)
  RobotEnemy/         Robot_Idle.png, HarvesterRobot.png
  Buildingblocks/     Cement.png, Metal.png, Wood.png — future block types (not yet wired)
  WorldProps/         per-world prop PNGs
  FrameSprites/       VFX spritesheets (DustCloud, EggSplat, Explosion, FeatherBurst, etc.)
```
World 1 props live directly in Unity (not `assets/`): `unity/Assets/Sprites/Environment/World1Props/`.

**Sprite sizing spec (Kling AI generation):** characters 1024×1024px, 75-80% canvas fill, white bg. Sky 1920×1080px, no alpha. Props/launchers 1024×1024px, white bg, element 75% of canvas.

---

## Auto-Compile Pipeline (non-obvious)

`EditorAutoSetup` (`unity/Assets/Editor/EditorAutoSetup.cs`) runs `[InitializeOnLoad]` on every compile:
1. Auto-generates levels if no `LevelData` assets exist in `Assets/ScriptableObjects/Levels/`
2. Auto-wires character sprites (PPU values) if the `Cluck/Loaded.png` sentinel is on wrong PPU or wrong sprite mode
3. Auto-copies card sprites: `assets/FarmCards/*.png` → `Assets/Sprites/UI/Cards/`, `assets/LevelCards/*.png` → `Assets/Sprites/UI/LevelCards/` (only copies files not yet present)
4. Auto-fixes the cannon sprite (Single mode, PPU=384)
5. Calls `AssetDatabase.Refresh()` to pick up files changed outside Unity (e.g. by `remove_backgrounds.py`)

`SpriteAutoImporter` (`unity/Assets/Editor/SpriteAutoImporter.cs`) is an `AssetPostprocessor` that enforces PPU and `Single` mode on import for launchers, UI cards, robot, and character sprites — **every category enforces `Single` mode on import; `Multiple` mode silently breaks `LoadAssetAtPath<Sprite>`/prefab wiring** (this exact bug cost an entire debugging arc — see `docs/HISTORY.md` Round 8).

---

## Architecture — Unity Port (`unity/`)

### Stack
Unity 6.5 (6000.5.0f1), URP 2D, Physics2D, New Input System, TextMeshPro.

### Coordinate System (current — all 6 World 1 levels now use this; do not use older values seen in stale comments)
- 1 Unity unit = 50 Phaser pixels (`x_unity = x_phaser / 50`, `y_unity = -(y_phaser - 770) / 50` — legacy conversion from the Phaser prototype).
- **Ground surface at Y = −6.60.** Launcher GO at (−2.327, −6.60, 0) — an abstract aim-math anchor only since the 2026-07-02 cannon swap; no visual GameObject corresponds to it any more.
- **Visual launcher is `FarmCannon`**, independent of the Launcher GO's position: at (−3.72, −4.7032, 0), scale (1.4711188, 1.3316845), sortingOrder=4. (Hand-repositioned twice since the 2026-07-13 "cannon too close" fix — moved closer to the barn door again during the 2026-07-17 parallax-backdrop session, then fine-tuned 2026-07-18 alongside the new decorative barrels/GnarledTree — see the Current Status entries for both. `SceneSetup.cs`'s `WireLauncher()` only stamps this position on first creation of the GameObject (fixed 2026-07-17, see below), so an already-existing hand-placed `FarmCannon` in `Game.unity` is the actual ground truth going forward; the fallback literal in code is re-synced to match whenever it's known to have drifted, but treat the live scene, not this doc line, as authoritative if the two ever disagree. `CatapultLauncher.ComputeOrthoSizeForLevel()`'s camera auto-zoom seeds its bounding box from this GameObject's real `SpriteRenderer.bounds`, not the abstract Launcher anchor.)
- Camera at (0, −2, −10), orthoSize = 4.5. `_cameraRestOffset = (2.327, 4.60)` → camera parks at (0, −2). Visible Y range at rest: −6.5 to +2.5 — **only 0.1 units above the ground line**, so any sizeable ground-standing sprite will clip the bottom of the screen unless deliberately raised with a small hover margin. This affects any future tall enemy in World 1, not just the current robot.
- **Level block/robot positions are stored as raw world-space coordinates in `LevelData`** — `LevelLoader.SpawnBlock`/`SpawnRobot` apply no offset; whoever authors a level is responsible for baking correct current-system coordinates directly.

```
Y convention: Ground surface = −6.60. Robot center (h=0.9, scale 0.6×0.9) → world y = −6.15 (sits on ground).
              Wood plank (h=0.4) resting on ground → center at world y ≈ −6.40. Stack upward by block height.
X convention: Launcher (abstract) at X=−2.327. Place structures to the right of it (positive X), reachable by the arc.
```

### Script Architecture
```
unity/Assets/Scripts/
  Core/
    GameManager.cs          — singleton (DontDestroyOnLoad); GameState = MainMenu/WorldSelect/LevelSelect/
                              CharacterSelect/Playing/Paused/LevelComplete/LevelFailed (franchise set, 2026-11-02).
                              StartLevel(LevelData, AnimalType[] lineup) (Character Select), RestartLevel() (same
                              lineup), ForceStartLevel(int) (Editor tools, authored queue), SetState (GameFlow
                              navigation), PauseGame/ResumeGame, app-background pause. CurrentLineup replaces the
                              old BirdQueueOverride; RunState (LevelRunState) holds the attempt's result. Owns the
                              economy (LevelPayout: 10 + 5/star on a first clear, 3 + 5 per new star on replays;
                              ClaimDoubleCoinsViaAd) and the revive (RequestRevive from LevelLoader when the queue
                              runs out with robots left -> ReviveOffered -> AcceptRevive 5 coins once / AcceptFreeRevive
                              Remove Ads once / AcceptReviveViaAd unlimited -> +1 of the last-fired animal), the
                              interstitial on level load and analytics calls. Single scene: no scene loads.
                              Awake adds DataManager/SaveManager/IAPManager/AdManager/AnalyticsManager to its own GO
                              if missing (Wire Scene References adds them too). TryAutoLoadLevels()/
                              BuildFallbackLevel() unchanged.
    DataManager.cs / SaveManager.cs / LevelRunState.cs / AnimalRoster.cs (namespace FarmFury.Core) — franchise
                              save/data layer, see "Franchise flow" below.
    IAPManager.cs / Store.cs / AdManager.cs / AnalyticsManager.cs (FarmFury.Core) — franchise monetisation, see below.
    BackgroundController.cs — sortingOrder=−100; cover-scale in Start(); LateUpdate() follows camera
    AudioManager.cs          — 7 DSP-generated SFX clips + 3 external MP3 clips (Assets/Audio/*.mp3):
                              _musicClip (loops from first GameState.Playing, never restarts across levels),
                              _cannonShotClip (replaces the procedural Launch clip at the same
                              AudioManager.Play(Sound.Launch) call site), _fallingClip (loops from Fire()
                              while a Cluck is airborne, fades 0.35s on AnimalBase.OnAnimalImpact — real
                              hits only, not CluckAnimal's hay pass-through punches). Lives on its own
                              dedicated scene GO (SceneSetup.EnsureAudioManager()), [DefaultExecutionOrder(-90)]
                              so it wins the singleton race against CatapultLauncher's fallback AddComponent.
                              SfxEnabled/MusicEnabled persisted via PlayerPrefs, toggled from both the
                              top-right Mute button and the Main Menu SETTINGS popup (same state, can't
                              disagree — the pause menu's own separate Music/SFX toggles were removed along
                              with the rest of that panel 2026-07-06).
                              Mix rebalanced 2026-07-26 (user-reported: cannon shot buried the Cluck falling/
                              scream loop, which starts in the same frame — see CatapultLauncher.Fire()):
                              FallingVolume raised 0.6->0.9, and Play()'s previously-uniform 0.8 PlayOneShot
                              scale is now a per-Sound VolumeScale[] array with Launch specifically dropped
                              to 0.5 — every other SFX (WoodHit/StoneHit/RobotDeath/Win/Fail/BlockDestroy/
                              RobotHit) keeps the original 0.8.
                              **Menu music added 2026-07-07**: a second _menuMusicClip
                              (SunriseMeadows_TransitionMusic.mp3) + dedicated _menuMusicSrc AudioSource,
                              independent of the gameplay loop above. OnStateChanged starts/stops the two
                              tracks so exactly one plays at a time: any menu state (MainMenu/WorldSelect/LevelSelect/CharacterSelect — franchise flow; was Idle) (covers both the landing
                              page and the Sunrise Meadows world map — both react to that same state, so
                              hooking it once here plays under either screen without needing to know which
                              is visible) starts menu music and stops gameplay music; GameState.Playing does
                              the reverse. Start() syncs once against GameManager.Instance.State on
                              subscribe, since State defaults to Idle at boot without ever firing a
                              transition event for it (TransitionTo only fires on an actual change) — without
                              this, a fresh launch landing on the menu would never start any music at all.
                              Gameplay music, cannon fire, and the Cluck falling loop are all untouched by
                              this addition (explicit user request when adding it, to avoid regressions).
    LevelCompleteManager.cs  — [DefaultExecutionOrder(-40)] owns the Level Complete "reward" beat, added
                              2026-07-07. Old flow: GameState -> LevelComplete fires HUDController's panel
                              immediately. New flow: Time.timeScale=0.2 for 0.5s (WaitForSecondsRealtime) ->
                              Time.timeScale=0 (hard freeze) -> CatapultLauncher.LastAnimalUsed's celebration
                              VideoClip + AudioClip play via the shared VideoChromaKey overlay (see below) ->
                              holds 4s -> fades 0.3s -> Time.timeScale=1 -> GameFlow.ShowLevelComplete() (was HUDController.ShowLevelCompletePanel)
                              (now public — HUDController's own OnStateChanged no longer calls it directly,
                              this class is the only caller). _celebrationClips[]/_celebrationAudioClips[] are
                              both indexed by AnimalType (8 slots) with an empty-slot fallback to index
                              Cluck, so every animal gets a celebration before every clip exists — wired via
                              SceneSetup.EnsureLevelCompleteManager() from Assets/Video/Cluck_Celebration.mp4
                              + Assets/Audio/Cluck_CelebratingLaugh.mp3 (paths updated 2026-07-07 when the
                              user reorganized source assets — see Current Status). Self-bootstraps via
                              CatapultLauncher.Awake() (same null-safety fallback pattern as AudioManager) if
                              missing from the scene, so it works even before a Wire Scene References pass.
    LevelFailedManager.cs    — mirrors LevelCompleteManager.cs exactly (added same day), but keys its clip
                              off GameManager.CurrentLevelIndex (which robot the player is up against) rather
                              than which animal was last fired: Time.timeScale=0.3 for 0.5s -> freeze -> the
                              current level's robot taunt VideoClip + AudioClip play -> hold 4s -> fade 0.3s
                              -> GameFlow.ShowLevelFailed() (franchise ResultsScreen; same decoupling as above).
                              _robotTauntClips[]/_robotTauntAudioClips[] are indexed by level number
                              (0-based), any level past the array's end or an empty slot falls back to index
                              0 — wired via SceneSetup.EnsureLevelFailedManager() from Robot_Celebration.mp4 +
                              Robot_CelebrateSound.mp3 (index 0 = L01, the only pair that exists today). Also
                              drives the Level Failed panel's RETRY button pulse once shown (see
                              HUDController.RetryButtonPulse below).
    CameraShake.cs           — singleton; tracks its own per-frame contribution as a delta and subtracts it
                              back out, so repeated shakes always net to exactly zero (never permanently
                              drifts the camera — see docs/HISTORY.md Round 7 for the bug this fixed)
    ParallaxScroller.cs      — speed 0.0 (world-fixed) – 1.0 (camera-locked); offsets X by camDelta×speed
    SceneryBuilder.cs        — subscribes to OnLevelStarted; _useExactPlacement=true + levelIdx=0 skips
                              entirely (L1 scenery is hand-authored permanent scene GOs); other levels use
                              deterministic System.Random(levelIdx*137+42) layout. PlaceExact() corrects for
                              native sprite size. FarmSilo excluded from all placement paths.
  Level/
    LevelData.cs             — ScriptableObject; birds[], blocks[], robots[] arrays; par bird count;
                              BlockSpawnData has optional passThrough/healthOverride/massOverride
                              (0 = use BlockBase defaults); BlockType.Haybale (enum value 2) renders
                              via HaybaleBlock.prefab (WoodBlock component + Haybail.png art)
    LevelLoader.cs            — instantiates prefabs; TryConsumeBird / PeekNextBird; OnBirdConsumed event;
                              BirdQueueSnapshot; DelayedLevelComplete/Failed coroutines -> GameManager;
                              AutoLoadPrefabs() (Editor-only, Awake()) auto-finds prefabs from Assets/Prefabs/;
                              SpawnRobot() re-derives BoxCollider2D size (0.6/scaleX, 0.9/scaleY) when a
                              custom visual scale is applied, keeping the hitbox pinned regardless of scale
  Animals/
    AnimalBase.cs             — abstract; Kinematic until Launch(); Pointer.current (New Input System, covers
                              Mouse+Touch); 5 pose sprites (_sprIdle/_sprLoaded/_sprInFlight/_sprImpact/
                              _sprAbility); HasRealSprites property; DestroyAnimal() fires OnAnimalDestroyed;
                              OnAnimalImpact fires on real collision hits only; sortingOrder=6 on all 8 subclasses.
                              OnCollisionEnter2D shows _sprImpact (e.g. Cluck_Impact.png) for the rest of the
                              _contactTimeout window (fixed 2026-07-26 — it used to get assigned then hidden
                              on the very next line in the same frame, so the reaction pose was never actually
                              visible before DestroyAnimal() removed the GameObject); only falls back to the
                              old instant-hide when no impact art is wired at all (procedural fallback circle).
    CluckAnimal.cs             — 5-egg cluster bomb in 120° spread from _eggPrefab (must be the egg GameObject
                              itself — _eggPrefab is typed GameObject, not EggProjectile); pass-through: punches
                              WoodBlock._passThrough=true at 70% velocity, skips base.OnCollisionEnter2D (so
                              hay punches never trigger OnAnimalImpact / stop the falling SFX). Calls
                              wood.TakeDamage(wood.MaxHealth) directly on pass-through (2026-07-06 fix) —
                              previously relied on BlockBase's own physics-impulse-derived damage from the
                              same collision, which varies with impact speed/angle across the Cluck's arc and
                              could leave a passThrough=true block alive after a graze, needing extra birds to
                              finish it off (user-reported: took all 3 Clucks to clear one haybale pile). Pass-
                              through blocks now always die in exactly one hit, any contact.
    BessieAnimal.cs            — vy-18 slam; shockwave 3.6u radius on Ground-tagged landing
    PercyAnimal / WoollyAnimal / DuckyAnimal / HoraceAnimal / GeraldAnimal / BillyAnimal
                              — Bounce Roll / Wool Balls / Skip Shot / Rear Kick / Feather Bombs / Mud Throw (the three thrown abilities use AbilityProjectile)
                              Through respectively; scripted and ready, not yet used in any shipped level
    EggProjectile.cs           — layer 10; flat _damage=15 on first contact only
  Blocks/
    BlockBase.cs               — spawns Static; TakeDamage() wakes ONLY the block actually hit (flips its
                              own Rigidbody2D to Dynamic), gated on _stayKinematic=true blocks (e.g.
                              HaybaleBlock) which never transition at all. A level-wide wake-every-block
                              sweep (FindObjectsByType<BlockBase>(), Unity 6 API) existed 2026-07-26 through
                              2026-07-10 — added for HaybaleBlock's own cluster (fixed by giving Haybale
                              _stayKinematic), but the same bug then resurfaced for Wood (hitting a haybale
                              in L03 shifted/collapsed a completely unrelated wood tower), so the sweep was
                              removed entirely rather than patched per block type — every block's
                              fall/collapse is now purely local, matching RobotEnemy's per-robot support-loss
                              check. **DestroyBlock() unconditionally re-runs the support-loss wake
                              cascade (2026-08-09)** — previously only TakeDamage()'s own hit path
                              triggered it, so RobotEnemy.Die()'s ForceDestroy() (a boss crumbling a
                              guarded structure) skipped it entirely, leaving anything resting on
                              that block permanently floating; now both paths always cascade, and the
                              recursive continuation into further tiers is staggered
                              (CascadeStaggerDelay=0.1s) rather than instant, so a tall structure
                              comes down over real time instead of one physics frame. health =
                              baseMaxHealth × area/stdArea; tints at 50%/25%/0% health; damage = impulse ×
                              1.0; on death: _sprExplode (if wired) shows a death-burst sprite via
                              SpawnExplosion() instead of the default 4-fragments-fly-outward-fade-1->0-
                              over-0.6s (HaybaleBlock wires this to Haybail_Damaged.png — at hp=10 it
                              always dies on the hit that damages it, so this explosion is the reaction
                              players actually see). ApplyOverrides(health, mass) applies BlockSpawnData
                              overrides after Initialise() — called by LevelLoader. Optional _sprDamaged +
                              PlayDamageFlash() for a brief pre-death art swap on hit (only wired where
                              dedicated damaged-state art exists, e.g. HaybaleBlock) — in practice this
                              never gets seen on a one-hit-kill block like Haybale; _sprExplode above is
                              the one that matters there.
    WoodBlock.cs                — baseMaxHealth=8 (raised from 5, 2026-08-09 difficulty pass — a
                              direct animal hit still one-shots a standard block same as before, but
                              a weaker landing-impact hit from a falling block is less likely to
                              also one-shot it, ~16.7 effective HP for a 1x1 block via area-scaling),
                              baseMass=5, bounciness=0.2; _passThrough (public); _sprDamaged/
                              _sprExplode both wired to WoodDebris.png — every wood plank gets a
                              dedicated hit-flash/death-burst instead of the generic 4-fragment fade.
                              _explodeSizeMultiplier self-heals to 0.5 for plain Wood, 1.0 for
                              Haybale (branches on `_isExplosive`, already deserialized from the
                              prefab by the time Awake() runs — see BlockBase.cs below for why this
                              needed self-healing in the first place). **No block explosion damages a
                              robot any more (2026-08-03)** — `RobotEnemy.TakeExplosionDamage()` was
                              removed entirely; a robot only ever takes damage from a direct animal
                              hit, an egg, or `TakeFallDamage()` when its own support is destroyed
                              (see RobotEnemy.cs below). **`_isExplosive` (bool, default false —
                              renamed from the old `_explodesOnRobots`, added 2026-07-10 fifth
                              balance pass)** gates the block-to-block chain reaction — **as of
                              2026-08-09, a plain block's (Wood/Stone) own destruction never calls
                              `DamageNearby()` at all** (used to still reach non-explosive
                              neighbours; removed entirely after a user report that one hit could
                              domino through a whole wall — physics/support-loss now does that job
                              instead, see BlockBase.cs's collapse cascade below), only a genuine
                              `_isExplosive` prop's (Haybale/ExplodingBarrelBlock) own destruction
                              still calls it. That chain can still reach another explosive prop, but
                              at half strength (`ExplosiveNeighbourDamageScale=0.5`, added the same
                              day so a barrel/haybale chain isn't a guaranteed sure thing). Wired true
                              on Haybale (via SceneSetup.EnsureHaybaleBlockPrefab()) and
                              ExplodingBarrelBlock (sets true in its own Awake()). Blast radius
                              (EffectiveBlastRadius) is not a flat constant — it's
                              Mathf.Max(_col.bounds.size.x, _col.bounds.size.y) *
                              ExplosiveForceScale (0.25f — lowered from 0.4f 2026-08-09, so ordinary
                              level-authoring spacing no longer falls inside another explosive prop's
                              blast; only deliberately-touching placements can still chain).
    ExplodingBarrelBlock.cs      — WoodBlock subclass, baseMaxHealth=5 (pops in one hit, same as
                              Haybale/Wood — NOT affected by WoodBlock's own baseMaxHealth=8 raise,
                              this class sets its own value after base.Awake()); _isExplosive=true,
                              _areaDamage=55 (bigger block-to-block chain damage than plain Wood's
                              25 — blast radius is shared/derived, see WoodBlock above, not a
                              separate override any more), _explodeSizeMultiplier=1.0 (explicit,
                              2026-08-09). WoodenBarrel.png art
                              (SceneSetup.EnsureExplodingBarrelPrefab()), reuses RobotEnemy's own
                              Explosion.png/Explosion_Robot.mp3 death FX via the existing
                              _sprExplode/_destroyClipOverride fields rather than new art. No longer
                              deals any direct robot damage (2026-08-03) — see WoodBlock.cs above.
    StoneBlock.cs                — baseMaxHealth=220, baseMass=8, bounciness=0.1,
                              _explodeSizeMultiplier=1.0 (self-healed 2026-08-09 — was still
                              serialized at the pre-2026-07-10 value 2.2, over 4x Wood's own 0.5).
                              **_sprDamaged/_sprExplode wired to StoneDebris.png (2026-08-03,
                              user-supplied)** — deliberately a separate asset from WoodBlock's
                              WoodDebris.png, so a shattering stone chunk doesn't look like broken
                              wood splinters. StoneBlock itself has no DamageNearby()/chain-reaction
                              call at all (inherited directly from BlockBase, not WoodBlock) — a
                              Stone block's own destruction was never able to chain-damage
                              neighbours in the first place.
  Enemies/
    RobotEnemy.cs                — HP=35 (class default; HarvesterRobot.prefab overrides to 28,
                              SemiHarvesterRobot.prefab to 26); scale=(0.6,0.9); BoxCollider2D.size=(1,1),
                              mass=20; 2 red eye child GOs; calls LevelLoader.NotifyRobotDestroyed;
                              Rigidbody2D.bodyType=Static set explicitly in Awake() (prefabs otherwise
                              default to Dynamic and free-fall under gravity — a real bug fixed
                              2026-07-18, see docs/HISTORY.md Round 12); _invincibleUntil =
                              Time.time+0.8f prevents instant death from fall-settling onto blocks at
                              level load; FlashDamage() swaps to _robotDamagedSprite briefly if wired,
                              else falls back to a white tint.
                              **Idle animation (2026-07-18)**: three mutually-exclusive modes, chosen
                              once in Awake(). (1) `_isTauntLoop` (= `_alertSprite != null`, unique
                              to CommanderRobot today) runs `TauntLoopRoutine()` — continuously
                              cycles `_robotSprite -> _alertSprite -> _criticalSprite -> repeat`
                              every 0.6s regardless of health, stopping only on death (Die() then
                              swaps to `_deathExplosionSprite` on the robot's own body). (2) a robot
                              with `_sprFacingRight` wired (Robot/HarvesterRobot/SemiHarvesterRobot)
                              runs `IdlePatrolRoutine()` — shuffles ±0.3 world units either side of
                              spawn X, swapping `_sr.sprite` between `_robotSprite` (left-facing) and
                              `_sprFacingRight` (right-facing) to match direction of travel; stops
                              instantly on any damage/Dynamic/destroyed. (3) neither field wired
                              falls back to the older in-place `IdleLookAroundRoutine()` (turn-and-
                              glance, no translation), same stop conditions as (2).
                              **Damage-factor model v2 (2026-07-10, fifth balance pass — see Known
                              Issues)**: every damage source is a fixed fraction of _maxHealth, no
                              impulse/speed dependency. TakeDirectHitDamage() (animal collision,
                              DirectHitDamageFraction=0.55, ~2 hits), TakeEggDamage() (one connecting
                              Cluster Bomb egg, EggDamageFraction=0.16, ~6-7 hits — deliberately the
                              weakest), TakeFallDamage() (MakeDynamicFromSupportLoss — a robot's support
                              block destroyed, structural collapse only, FallDamageFraction=0.15, ~7
                              hits). **TakeExplosionDamage()/ExplosionDamageFraction removed entirely
                              (2026-08-03, user request)** — no block explosion (Wood, Stone, Haybale,
                              ExplodingBarrelBlock) damages a robot any more, regardless of proximity;
                              a robot near a detonating prop is unaffected unless it was actually
                              resting on the block that broke, in which case TakeFallDamage() (a
                              distinct, weaker category driven by the fall itself, not the blast)
                              applies via CheckForRobotsOnTop() -> MakeDynamicFromSupportLoss(). Robot-
                              vs-robot contact damage (_robotContactDamage, default 18) is the one
                              remaining impulse-plus-floor category, added to the real impulse (not
                              just whichever is bigger) via OnCollisionEnter2D's robot-vs-robot branch —
                              any robot-vs-robot touch always counts as a hit regardless of
                              _minDamageImpulse.
  Scoring/
    ScoreManager.cs               — Robot +1000, Wood +100, Stone +200, Egg +50, bird-left bonus +500;
                              PlayerPrefs keys: ff_score_N, ff_stars_N (0-based, plain star count)
  Launcher/
    CatapultLauncher.cs            — visual launcher is FarmCannon (see Coordinate System above). Aim math
                              (rewritten 2026-07-26 — direction and power are now independent axes, "Angry
                              Birds" style, per explicit user request after every shot looked identical
                              regardless of drag direction): click bird -> drag -> PivotPos()-relative pull
                              ANGLE clamped to [_armRestAngle=200°, 200°+MaxLoadAngle=60°=260°], mirrored
                              through the pivot (-180°) to a 20°-80° launch angle (200°->20° flat shot,
                              260°->80° near-vertical lob) — DIRECTION now actually changes the shot, unlike
                              the old trebuchet-arm scheme this replaced, which clamped the same way but only
                              ever read the clamped value as a single 0-1 "how far pulled" fraction driving a
                              barely-varying 52°-58° range, discarding the real direction entirely (see
                              docs/HISTORY.md). Pull DISTANCE from the pivot (clamped to _maxDragDistance=2.4)
                              independently drives POWER: LaunchVelocity() speed _minLaunchSpeed=3.8-
                              _maxLaunchSpeed=9.3 m/s (both [SerializeField] — _maxLaunchSpeed already existed
                              serialized in Game.unity at a stale 16.8 from an even earlier, never-wired
                              design; fixed at launch, then raised twice more since — 3.0-6.0 -> 3.5-8.5
                              (2026-07-11, "allow for high and strong firing") -> 3.8-9.3 (2026-07-13,
                              re-tuned after the FarmCannon reposition pushed the launch point ~4.5 units
                              further from every level's content — see Current Status). Each raise re-synced
                              directly in the scene YAML per the [SerializeField] stale-value trap below).
                              Numerically
                              verified (same substep model as DrawTrajectory) to span L01's play area across
                              multiple angle/power combinations rather than one fixed landing zone. Paired
                              with AnimalBase.Launch()'s gravityScale=0.18 for a slower, loopier arc (2026-07-14).
                              LastAnimalUsed (public static AnimalType, added 2026-07-07) is set the instant
                              Fire() actually consumes a bird — read by LevelCompleteManager to pick which
                              animal's celebration video plays. Static rather than an instance lookup since
                              there's exactly one cannon per scene and this outlives any single AnimalBase
                              instance (destroyed on landing). Awake() also self-bootstraps
                              LevelCompleteManager/LevelFailedManager onto their own scene GOs if missing,
                              same null-safety fallback pattern as the existing AudioManager bootstrap right
                              above it in the same method.
                              MinAimRadius=0.6f: drag ANGLE (only) freezes until the pointer has moved that far
                              from the pivot, preventing hair-trigger full-power swings from small movements
                              (mobile touch fix, 2026-07-18) — pull distance/power tracks the pointer from the
                              first frame, no deadzone. CannonBarrelOffset=(1.1,0.4) = _launchPoint
                              (trajectory-arc origin AND Fire()'s actual spawn point). CannonLoadedBirdOffset
                              =(0.6212,0.4223) = fixed ready-bird position (cannon body doesn't rotate).
                              BirdScale=(4.9204,4.9204) (uniform — InFlight is the only pose shown on this
                              GO); ApplyBirdScale() re-derives col.radius /= BirdScale.x to keep the physics
                              hitbox correct after the visual scale is applied. On fire: CannonFireSequence()
                              coroutine — recoil tween (no DOTween in this project), smoke burst
                              (sortingOrder=6, between cannon@4 and animals@... see sortingOrder rule below),
                              then waits out CannonResetDelay=1.80s total. EnsureGroundExists() validates
                              against the launcher's own Y (dynamic, not hardcoded).
  UI/
    HUDController.cs                 — in-level HUD only (franchise flow, 2026-11-02): the animal queue top-left
                              (animated, damage badges) shows Stampede's thumbs-up portraits
                              (Sprites/UI/Franchise/ThumbsUp/<Animal>.png, aspect kept) — the old framed cards are no
                              longer used in gameplay (Character Select still uses the franchise cards), and top-right a round pause button (-> GameFlow.OpenPause), the coin
                              balance chip left of it (SaveManager.CoinsChanged) and the Locker button under it.
                              Visible only in Playing/Paused. Its old Scoreboard Level Complete/Failed panels,
                              PAUSED toggle and Quit/Mute buttons were removed — results/pause are GameFlow
                              screens, mute is Settings' Sound icon. Pause stays top-right (not bottom like
                              Stampede's HUD): the cannon and the aiming drag own the bottom-left.
    StoryContent.cs                   — FarmFury's lore text (Story/Characters/Robots/Worlds/Credits), read by
                              Settings -> Character Story.
    Kit/                              — franchise uGUI helpers copied from FarmFury_Stampede (namespace FarmFury.UI):
                              UIKit (code-built screens, 1920x1080 reference canvas, RoundButtonSize 160;
                              EventSystem uses InputSystemUIInputModule — FarmFury is Input-System-only),
                              SafeAreaFitter, FitContent, ZoomedCover, CardCarouselController, ConfettiBurst,
                              HoldButton, HangingSignDrop, HoldThenFade, SpinAroundY.
    Flow/                             — every screen outside gameplay, ported from FarmFury_Stampede (itself from
                              Arcade) so all Farm Fury titles share one flow. See "Franchise flow" below.
    VideoChromaKey.cs                    — added 2026-07-07. Drives a `VideoPlayer` through the
                              `FarmFury/ChromaKeyVideo` shader onto a full-screen `RawImage`, so a green-
                              screen character/robot clip can play over the frozen game scene with the
                              #00B140 background keyed out (see "Video Chroma Key / Celebration System"
                              below). `[RequireComponent(typeof(VideoPlayer))]`; `FindOrCreate()` is a
                              static factory — finds the scene's existing overlay or builds the whole
                              Canvas(sortingOrder=150)+Background `Image`+`RawImage`+`VideoPlayer` hierarchy
                              from scratch, the same "nothing needs pre-wiring" pattern `HUDController` uses
                              for its own Canvas. Exactly one instance ever exists — shared by
                              `LevelCompleteManager` and `LevelFailedManager` since their celebrations never
                              play simultaneously. `Play(clip, audioClip, plainRender)`/`FadeOut(duration)`/
                              `Stop()` are the only playback API — `plainRender` (added 2026-07-08, see
                              Current Status) skips the chroma-key shader/sky backdrop entirely for clips
                              with their background already baked in, e.g. the current Cluck_Celebration.mp4;
                              owns an `AudioSource` for the accompanying one-shot
                              laugh/taunt sound, started and faded in lockstep with the video. The
                              `Background` `Image` (sky art, sits behind the `RawImage` in sibling/render
                              order) is shown/hidden and faded together with the video too — see the
                              ghosting-bug Known Issues entry above for why it exists.
                              **Updated 2026-07-07** (same ghosting fix): `RawImage` now stays hidden until
                              a new `frameReady`-driven `HandleFrameReady` callback confirms the current
                              clip's first real frame has landed (prevents showing the previous clip's
                              stale `RenderTexture` content); `WaitUntilFirstFrame(timeout)` lets callers
                              avoid burning their on-screen hold timer on decode latency;
                              `HandlePrepared()`'s `RenderTexture` is now created with
                              `RenderTextureReadWrite.Linear` explicitly (this project is Linear colour
                              space — omitting it caused a colour-space mismatch between the sampled video
                              texture and the shader's key colour, the actual root cause of the ghosting);
                              `tolerance` default lowered `0.3 -> 0.20` (re-synced in
                              `SceneSetup.EnsureCelebrationVideoBackground()` every Wire Scene References
                              pass — see the `[SerializeField]` stale-value trap, `tolerance` is a public
                              field so the same risk applies). `HandleError`/diagnostic logging in
                              `HandlePrepared`/`HandleFrameReady` and `SampleRenderTexturePixels()` were
                              added during debugging and left in place.

unity/Assets/Editor/
  SceneSetup.cs           — FarmFury > Wire Scene References; wires all Inspector refs; sets camera
                              (0,0,-10) orthoSize=4.5; launcher at (-2.327,-6.60,0); creates FarmCannon
                              at (-7.54,-5.03,0) scale (1.4711188,1.3868444,1) **only when the GameObject
                              doesn't already exist** (fixed 2026-07-17 — this used to run UNCONDITIONALLY
                              on every Wire Scene References pass, silently reverting any hand-placed
                              reposition; regressed at least twice, see the 2026-07-17 Current Status entry.
                              An existing FarmCannon's transform is never touched here again — move/scale it
                              directly in the Editor and it stays put); ground center (0,-6.85,0) scale
                              (60,0.5,1) -> top at Y=-6.60 (physics-collider-only, no visual — ground/grass
                              visuals are user-authored scene GameObjects or the Background backdrop,
                              not code-generated). **GroundVisual_Placeholder / EnsureGroundVisual()
                              removed entirely 2026-07-17** (it used to recreate a tinted stand-in strip
                              every time it was missing, fighting the user's own deletion of it once real
                              backdrop art existed) — WireAll()'s placeholder-cleanup loop now removes any
                              leftover instance from an older scene save.
                              **`EnvironmentDepthSystem`/`Layer_Midground` removed entirely 2026-07-18**
                              (see Current Status) — used to be a second frozen backdrop layer rendered in
                              front of `Background`, hand-placed/computed multiple times without ever
                              reliably covering the camera at every per-level zoom. `EnsureBackground()`
                              now points `Background`'s own sprite directly at ParallaxMidground.png
                              instead (see below); `CleanupLegacyEnvironmentDepthSystem()` removes any
                              leftover `EnvironmentDepthSystem` GO from an older scene save.
                              **EnsureBackground()**'s `GameObject.Find("Background")` fallback (2026-07-17)
                              no longer matches ANY object named "Background" — scoped to root-level,
                              non-RectTransform objects only, since `VideoChromaKey`'s own celebration-overlay
                              child is also literally named "Background" and the ambiguity could attach a
                              stray `BackgroundController` to that UI Image instead of the real world sky
                              (see Current Status for the "white square" bug this caused).
                              **EnsureLevelCompleteManager()/EnsureLevelFailedManager()/
                              EnsureCelebrationVideoBackground()** (2026-07-07) — create/find each manager's
                              scene GO and wire its video clip from `Assets/Video/` and audio clip from
                              `Assets/Audio/` (paths updated 2026-07-07 when the user split these out of the
                              old `Assets/Sprites/UI/Video_Sound/` — see Current Status) via the shared
                              `WireArrayElement<T>` helper (same shape as `WireAudioClip` above, generalised
                              to any `Object`-derived asset type and array index). `EnsureCelebrationVideoBackground()`
                              must run after `EnsureBackground()` so `Background_SkyV1.png`'s Sprite import
                              settings are already correct by the time it loads it for
                              `VideoChromaKey._backgroundSprite` (also fixed 2026-07-07 — this path was
                              previously hardcoded to a nonexistent `SkyPainting.png`, so the sky backdrop
                              was silently never wired; see Current Status). Also extended
                              `EnsureAudioManager()`/`WireAudioClip()` calls with `_menuMusicClip` the same
                              day (see AudioManager.cs above).
  LevelDataGenerator.cs    — FarmFury > Generate All Level Data; LXX_ filenames must sort alphabetically;
                              header comment documents which levels (L01 only) use the current coordinate
                              system vs. which (L02-L06) are still on the old one
  SpriteWiring.cs           — FarmFury > Wire Sprites; sets per-character PPU; wires pose sprites; keys off
                              folder names under Assets/Sprites/Characters/<ShortName> (must match
                              tools/remove_backgrounds.py's CHAR_MAP short names exactly, e.g. "Cluck" not
                              "Cluck_Chicken" — a folder-name mismatch here silently wires nothing, no error)
  BuildScript.cs             — batch-mode entry points called by Run-Unity.ps1
  EditorAutoSetup.cs          — see "Auto-Compile Pipeline" above
  SpriteAutoImporter.cs        — see "Auto-Compile Pipeline" above
  PanelPreview.cs              — FarmFury > Debug > Run Panel Preview; QA tool added 2026-07-25 since this
                              project has no test framework and no OS-level input automation is available in
                              this environment. Drives GameManager's public state API directly (ForceStartLevel
                              -> FailLevel/CompleteLevel -> SendMessage("OnPauseClicked")) to force the Level
                              Failed/Complete/Pause panels to show in Play mode without playing an actual
                              level, screenshotting each via ScreenCapture.CaptureScreenshot. Must be launched
                              as a real interactive Editor (no -batchmode/-nographics — Play mode needs a
                              rendering Game view), unlike every other Run-Unity.ps1 command. Untested end to
                              end in this environment — the one attempt OOM'd during a cold asset reimport on
                              a 7.7GB-RAM machine before ever reaching Play mode; retry once free memory allows,
                              or run it directly from the Editor's own menu.
```

### Runtime Event Flow

**App start:** `GameFlow.Start()` shows Landing (GameState.MainMenu). No level is loaded until one is picked;
going back to any menu calls `LevelLoader.UnloadLevel()`.

**Level start:** Landing -> World Select -> Level Select -> Character Select (lineup) -> `GameManager.StartLevel(level,
lineup)` -> `BeginLevel` -> `TransitionTo(Playing)` -> `OnLevelStarted` -> `LevelLoader.LoadLevel()` (uses
`CurrentLineup`, else `LevelData.birds`) -> `ScoreManager.InitLevel()` -> `CatapultLauncher` loads the first animal ->
interstitial check (`AdManager.NotifyLevelLoaded`).

**Level complete:** last robot destroyed -> `DelayedLevelComplete()` (2s) -> `ScoreManager.FinaliseLevel()` (saves through
SaveManager: stars, records, completion; a boss level clears its world) -> `GameManager.CompleteLevel()` (RunState +
coin payout) -> LevelComplete -> `LevelCompleteManager` celebration video -> `GameFlow.ShowLevelComplete()` (ResultsScreen,
then the New Character page when a boss clear opened a world). Play -> next (Level Select; after a boss the World Unlocked page
(`WorldClearedScreen`, ported from Stampede 2026-11-02: the new world's badge revealed from a silhouette over turning
rays, confetti, "Next stop: …") when the clear opened a world, the New Worlds promo on a first boss clear, then World
Select). The L18 transition video, `WorldTransitionManager`, `VideoChromaKey` and its shader were removed the same day.

**Level failed:** last animal lands with robots left -> `NotifyBirdsExhausted()` -> `DelayedLevelFailed()` (1.5s) ->
`GameManager.RequestRevive()` -> revive prompt (+1 animal) or `FailLevel()` -> `LevelFailedManager` taunt video ->
`GameFlow.ShowLevelFailed()`.

**Replay / Retry / Restart:** `GameManager.RestartLevel()` -> same level, same lineup, no reload.

### Video Chroma Key / Celebration System (added 2026-07-07)

Green-screen character/robot clips play directly over the frozen game scene between the moment a
level ends and its result panel appearing — Cluck (or the robot) appears to stand in the actual
paused level environment rather than on a plain title card.

- **`unity/Assets/Shaders/ChromaKeyVideo.shader`** — URP unlit UI shader, `FarmFury/ChromaKeyVideo`.
  Keys out `_KeyColor` (default `#00B140`) with a `_Tolerance`-driven soft edge. Includes standard
  Unity UI stencil/clip-rect boilerplate so it still respects a `RectMask2D`/`Mask` ancestor if ever
  placed under one. **Saturation-gated** (fixed 2026-07-07, see Known Issues above) — pixels less
  than half as saturated as the key colour are forced fully opaque no matter how numerically close
  their raw RGB sits to the key, which is what makes plain RGB-distance chroma keying mistake dark/
  desaturated character shading for green in the first place. **Brightness-gated too, added
  2026-07-07** (see the Cluck ghosting Known Issues entry) — the saturation gate alone couldn't
  distinguish genuine dark shadow shading (must stay protected/opaque) from a bright, near-white
  anti-aliased edge-blend pixel right at the character's silhouette (was showing up as a "shiny
  lining" traced around the whole character once the ghosting itself was fixed). `brightWashout`
  now treats sufficiently bright pixels (`value > ~0.75`) as if fully saturated regardless of
  actual saturation, subjecting them to the real distance-to-key check instead of blanket
  protection, while dark desaturated pixels keep full protection unchanged.
- **`VideoChromaKey.cs`** (`Scripts/UI/`) — the runtime driver; one instance ever exists in a scene
  (`FindOrCreate()`), shared by `LevelCompleteManager` and `LevelFailedManager`. Owns the
  `VideoPlayer` (switched to `RenderTexture` mode so its output can feed the shader as `_MainTex`),
  the `RawImage` the shader renders onto, a `Background` `Image` behind it (sky art, fixes the
  ghosting/messy-backdrop issue), and an `AudioSource` for an accompanying one-shot laugh/taunt
  clip. `Play(clip, audioClip)` starts both; `FadeOut(duration)` fades video alpha + backdrop alpha
  + audio volume together over unscaled time (safe to call while `Time.timeScale == 0`); `Stop()`
  hides everything and resets state for the next `Play()`. Note: `VideoPlayer` and `AudioSource`
  both run on their own wall-clock timeline regardless of `Time.timeScale`, which is exactly why
  they can play correctly during the freeze-frame below.
- **`LevelCompleteManager.cs`** / **`LevelFailedManager.cs`** (`Scripts/Core/`) — orchestrate the
  actual sequence per state transition: brief slow-motion (`Time.timeScale` 0.2/0.3 for 0.5s
  real-time) → hard freeze (`Time.timeScale = 0`) → `VideoChromaKey.Play(...)` with the
  animal's/level's clip → hold 4s → `FadeOut(0.3s)` → `Time.timeScale = 1` →
  `GameFlow.ShowLevelComplete()`/`ShowLevelFailed()` (franchise ResultsScreen). Both self-bootstrap onto their
  own scene GO via `CatapultLauncher.Awake()` if missing (same pattern as `AudioManager`), and both
  have their video/audio clip arrays wired by `SceneSetup` (`EnsureLevelCompleteManager()`/
  `EnsureLevelFailedManager()`) from `Assets/Video/` (clips) and `Assets/Audio/` (accompanying
  sounds) — run **FarmFury → Wire Scene References** after adding new clips there. See the Core/ script tree entries above for the exact
  indexing/fallback rules for each manager's clip arrays.

### Key Implementation Rules
- **Input System ONLY:** `using UnityEngine.InputSystem;` — `UnityEngine.Input` is incompatible. Use `Pointer.current` (shared base class for Mouse/Pen/Touchscreen), not `Mouse.current` — this already covers touch input without needing the `EnhancedTouch` module. `Pointer.current.press.wasPressedThisFrame`/`.isPressed`/`.wasReleasedThisFrame`, `pointer.position.ReadValue()` → `Vector2`.
- Blocks spawn `RigidbodyType2D.Static`; first `TakeDamage()` calls `WakeAllStaticBlocks()` → `FindObjectsByType<BlockBase>()` (Unity 6 API). **`RobotEnemy` must also be explicitly `Static`** (set in `Awake()`) — it does not default that way, and a `Dynamic` robot free-falls under gravity from its spawn Y (real bug, fixed 2026-07-18).
- Animals start Kinematic → Dynamic on `Launch(velocity)`.
- Never destroy a physics body inside a collision callback — defer with a coroutine.
- **SpriteRenderer is auto-added:** both `BlockBase.Awake()` and `AnimalBase.Awake()` add one if null — prefabs don't need SpriteRenderer pre-added.
- **[SerializeField] stale value trap:** changing a `[SerializeField]` default in code does NOT affect already-serialised components. Use `private const` for values that must not be overridden.
- **Nested runtime-built components can't rely on the batch "Wire Scene References" pass re-firing their `Awake()`.** If a component is instantiated inside another component's `BuildUI()`/`Awake()`, an editor script that merely *finds* the already-serialized parent GO in a later session will not re-trigger the child's `Awake()`, so any `[SerializeField]` on the child silently never gets wired (no error — see `docs/HISTORY.md` Round 11). Put art/reference fields on the top-level persisted component and thread them down via an explicit `Init(...)` call instead.
- Ground collider: `localScale=(60,0.5,1)`, `BoxCollider2D.size=(1,1)` → world collider 60×0.5, top edge at Y=−2.5 (physics-only; visuals are hand-authored scene GOs, not code-generated). Never set both scale AND size to large values.
- **Effective mass formula:** both dynamic → `(mA × mB) / (mA + mB)`; one static → `movingBody.mass × 0.6`. Damage threshold impulse > 1.5. Damage = `impulse × 1.0` (no multiplier — blocks and robots both use ×1.0).
- **Robot spawn invincibility:** `RobotEnemy.Initialise()` sets `_invincibleUntil = Time.time + 0.8f`; `OnCollisionEnter2D` returns early while invincible.
- **Scenery sortingOrder rule:** decorative props (SceneryBuilder) must use `sortingOrder ≤ 1`. Blocks=2, robots=3, FarmCannon=4, CannonSmoke=5, animals=6. Any tie between two of these falls back to camera Z-distance sorting, which has caused multiple real invisible-object bugs in this project (trebuchet-vs-ground, animals-vs-cannon) — always assign a distinct explicit order for anything new.

### Physics Values
| Entity | mass | bounciness | linearDrag |
|---|---|---|---|
| CluckAnimal | 8 | 0.4 | 0.008 |
| BessieAnimal | 28 | 0.15 | 0.016 |
| WoodBlock | 5×area/std | 0.2 | — |
| StoneBlock | 8×area/std | 0.1 | — |
| RobotEnemy | 20 | 0.15 | — (Static body, doesn't fall) |

Physics2D Gravity Y: −20. Layers: Ground=6, Animal=7, Block=8, Robot=9, Egg=10. Animal `gravityScale`=0.18 (re-tuned for a slower/loopier arc — see `docs/HISTORY.md` Round 10).

### Scene Structure (Game.unity)
```
Main Camera           (0, −2, −10; orthoSize=4.5)
Global Light 2D
GameManager           (DontDestroyOnLoad; + DataManager, SaveManager, IAPManager, AdManager, AnalyticsManager)
GameFlow              (GameFlow.cs: builds every menu/results/overlay screen in code, MenuCanvas sortingOrder 400)
LevelLoader
ScoreManager
HUD
Background              (BackgroundController.cs; SpriteRenderer, sortingOrder=−100. `_skySprite`
                        is ParallaxFarHills.png — a defensive fallback sky, normally fully hidden
                        behind EnvironmentDepthSystem's stack below (Background_SkyV1.png, the
                        original file here, was found deleted from disk 2026-07-18 — see Current
                        Status). `ScaleToFillCamera()`/`LateUpdate()` auto-covers the camera every
                        frame regardless of orthoSize/aspect, unrelated to any of the same-day
                        parallax rebuilds. GroundVisual_Placeholder, which used to sit at
                        sortingOrder=−1, is gone permanently.)
EnvironmentDepthSystem  (EnvironmentDepthSystem.cs; 3 children — Layer_FarHills/Layer_Midground/
                        Layer_Foreground, sortingOrder −40/−30/−20, each ParallaxFarHills/
                        Midground/Foreground.png via the shared Assets/Materials/
                        ParallaxBandClip.mat (FarmFury/ParallaxBandClip shader), each revealing
                        only its own bottom [0, ClipAbove] UV band (theatre-flats stacking) — see
                        the 2026-07-18 Current Status entry for full history/reasoning.
                        RescaleToCamera() re-fits every layer to the camera's ACTUAL current
                        orthoSize/aspect/position on Awake() and every GameManager.OnLevelStarted —
                        NOT frozen/hand-placed, unlike this project's two earlier same-day attempts
                        at a single-painting version of this backdrop. _farHillsClipAbove=1.0 (top
                        layer fully uncropped, its own sky fills the rest) /
                        _midgroundClipAbove=0.55 / _foregroundClipAbove=0.30 are live-tunable
                        Inspector sliders, not baked into the shared material.)
Launcher               (CatapultLauncher.cs; abstract aim-math anchor, no visual meaning)
FarmCannon              (visual launcher; single static SpriteRenderer, Cannon.png, recoils on fire
                        via CannonFireSequence()/RecoilTo(), otherwise stays put — no rotation.
                        Position/scale are likewise hand-placed and frozen — SceneSetup.cs only
                        sets them when first creating this GameObject, never on an existing one.)
Scenery                 (SceneryBuilder.cs; _useExactPlacement=true skips L1)
[L1 scenery GOs]         OldBarn_Right, OakTree, WoodenFence×3, Rock×2, GrassTuft (variable count,
                        user has been adding more by hand), WildFlowers×2, WoodenBarrel×3 (added
                        2026-07-18, clustered near the barn at roughly X=-1.1 to -1.7), GnarledTree
                        (re-added 2026-07-18 at (8.22, -3.94) scale (2.675, 3.602), far right side
                        of the level — removed 2026-07-17 when the windmill silhouette moved into
                        ParallaxMidground.png itself, but the tree shape alone was brought back as
                        a separate hand-placed foreground prop); decorative Haybail×4 were deleted
                        earlier (superseded by gameplay HaybaleBlock instances at the same spots).
                        None of these are code-spawned or code-tracked by name — SceneSetup.cs's
                        cleanup passes never target scenery by name, so hand-added/moved/deleted L1
                        props are always safe from a Wire Scene References run.
BlockParent              (empty holder for code-spawned blocks)
RobotParent              (empty holder for code-spawned robots)
Ground                  (tag="Ground", layer=6; top edge at Y=−6.60; physics collider only)
```

### Prefabs
```
Prefabs/Animals/     CluckAnimal, BessieAnimal, PercyAnimal, WoollyAnimal,
                     DuckyAnimal, HoraceAnimal, GeraldAnimal, BillyAnimal, Egg
Prefabs/Blocks/      WoodBlock, StoneBlock, HaybaleBlock (WoodBlock component + Haybail.png art)
Prefabs/Enemies/     Robot (0.6×0.9 scale, red eye child GOs), HarvesterRobot (custom scale/HP, own art)
Prefabs/Environment/ Ground (static)
```

### Adding a New Animal
1. Create class extending `AnimalBase`; override `Awake()` (set colour/radius/mass before `base.Awake()`), implement `TriggerAbility()`
2. Add prefab to `Prefabs/Animals/`
3. Add `AnimalType` enum value to `LevelData.cs`
4. Handle in `LevelLoader.CreateNextAnimal()` switch expression and `GetAnimalIdleSprite()`, `CatapultLauncher.NextBirdFA()` drag constant
5. Run **FarmFury → Wire Scene References**

### Adding a New Level
Add `LevelData` ScriptableObject in `Assets/ScriptableObjects/Levels/` with filename `LXX_<Name>.asset` (alphabetical order = load order). Run **Wire Scene References**. Or add a `Make(...)` call to `LevelDataGenerator` and run **FarmFury → Generate All Level Data**. Use the current coordinate system (see above) — L01 and L02 are both safe references. Preferred workflow as of 2026-07-09: hand-build the level in the Scene view and use `LevelLayoutDumper` (**FarmFury → Debug → Dump Level Layout To Log**) to generate the `B()`/`R()` calls directly, rather than hand-typing coordinates — see the L02 Known Issues entry above.

---

## Development Roadmap (6 Phases)

- **Phase 1 — Core Feel:** ✅ done (slingshot→cannon aim, trajectory arc, destruction feedback, audio, camera follow).
- **Phase 2 — UI/UX Shell:** ✅ done, then **replaced 2026-11-02 by the franchise flow** (FarmFury_Stampede/Arcade's menus, results, settings, shop — see "Franchise flow").
- **Phase 3 — Character Roster:** ✅ done (all 8 animals scripted and art-wired).
- **Phase 4 — World 1 Completion:** ✅ done — L01–L18 all exist, no gaps, including a boss level (L18, `RobotType.Commander`). No level validator yet. None of L10-L18 visually verified in Play mode.
- **Phase 5 — Worlds 2–9:** levels/robots/bosses/backdrops/props built 2026-11-02 (generated from World 1's towers); the per-world launcher mechanics (Ice Cannon ricochet, Water Wheel, Biplane, Torpedo, Gravity Sling) are not built.
- **Phase 6 — Polish & Release:** monetisation **built 2026-11-02** as part of the franchise flow (Unity IAP, LevelPlay, UGS Analytics, coin economy, revive, Double Coins, shop, cosmetics) — ported from Stampede, so it follows the franchise's actual stack (UGS Analytics, LevelPlay mediation) rather than the July plan's AppLovin MAX/RevenueCat; Supabase backend still not built (no title has it yet). Open: store products/ad units/keys, AdMob app ids, bundle id, Boot scene + AAB split, device testing.

**Practical read:** all nine worlds are playable end to end on paper (160 levels, franchise menus, monetisation),
but nothing has been verified in Play mode. Next: a full play-through of every screen and world, the level validator,
Horace's two Kling poses, then store setup (bundle id, products, ad keys) and device testing.

---

## Phaser Prototype (`index.html`)

Frozen reference artifact — not under active development. Kept for physics-constant reference only.

### Running
```
npx serve .
# or
python -m http.server 8080
```

### Stack
Phaser 3.60 (CDN), Matter.js (bundled), WebAudio API (procedural sound), localStorage.

### History
`PROGRESS.txt` at the repo root is the prompt-by-prompt changelog for the Phaser prototype (Prompts 1–12). Read it for precise physics constants, level coordinate calculations, and tuning rationale (e.g. why MAX_VEL was reduced from 18→14 in Prompt 12).

### World Constants
| Constant | Value |
|---|---|
| World size | 1400 × 800 px |
| Viewport | 800 × 600 px |
| Ground Y | 770 |
| Gravity | 1.4 |
| MAX_VEL | 14 px/frame |

### File Order (class order matters in this single-file build)
1. Module helpers: `getStoredLevel`, `drawStarShape`, `genHillPts`
2. `LEVELS` array
3. `Block` → `Animal` → `CluckAnimal` → `BessieAnimal` → `Robot`
4. `LevelLoader` → `ScoreManager` → `Trebuchet`
5. `MenuScene` → `SandboxScene` → `new Phaser.Game(...)`

### Physics Critical Notes (Phaser)
- Never destroy a physics body inside a collision callback — use `scene.time.delayedCall(0, ...)`
- `body.blockRef` / `body.robotRef` must be stamped AFTER `setCircle()` (which replaces the body)
- Collision damage: `impulse = relSpeed * effMass * 0.15`, then `dmg = impulse * 2.5`. Threshold: `impulse > 2`
- Static-vs-static skipped (`bodyA.isStatic && bodyB.isStatic`); bird-vs-static-block must register
