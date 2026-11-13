# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

---

## Project Overview

**Farm Fury Cannonfire** (renamed from "Farm Fury" 2026-11-10, to run with Farm Fury Arcade and Farm Fury Stampede)
is an Angry Birds-style physics destruction game. Farm animals launch from a cannon to wreck robot fortresses and reclaim the farm.

The repo contains two parallel codebases:
- `index.html` — **Phaser 3.60 prototype** (frozen at Prompt 12, kept as a reference/physics-validation artifact only — not under active development)
- `unity/` — **Unity 6.5 URP 2D port** (active development target for iOS/Android release)

This is the single source of truth for the Unity port. Detailed reasoning behind current values/conventions (root causes, dead ends, superseded fixes) lives in **`docs/HISTORY.md`** — read it when you need the "why," not just the "what." This file only states current truth.

Full GDD: **`FarmFury_GDD.md`** (v3.1, repo root — the living design doc, franchise format; supersedes the June 2026
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
  Select / Results / Pause / Settings / Shop / Locker on a real aspect ratio. (Hats were removed 2026-11-08 —
  cosmetics are now cannon skins, trails and impact effects.)
- **Art sources (2026-11-02):** animal poses come from `FarmFury_Technical/FarmFury_Artwork/Rough_Characters` via
  `tools/import_character_art.py` (all 8 animals; Horace's Trigger/Impact pending two Kling jobs — save them as
  `Rough_Characters/Horace/Horace_Trigger.png` / `Horace_Impact.png` and re-run the script + Wire Scene References).
  `Sprites/AppIcon.png` (Cluck thumbs-up + FF_Logo) is set as the default app icon by Wire Scene References.
- **Menu art is Stampede stand-ins** (`Assets/Sprites/UI/Franchise/`, gitignored like all sprites). The "Farm Fury
  Stampede" logo was inpainted out of all 28 Level Complete / Failed / Pause / New Character canvases (2026-11-02,
  OpenCV Telea; clean on sky backdrops, a soft smudge on the busier paid-world ones); the Leaderboard has no logo. Full list in `docs/ART_REQUIREMENTS.md` (Kling checklist for all nine worlds).
- **Monetisation needs the user's accounts:** create the `farmfury_`-prefixed products (Remove Ads, 4 coin packs,
  3 worlds, 5 cannon skins, 6 trails, 5 impact effects) in App Store Connect / Play Console; enter FarmFury's LevelPlay app keys and ad unit
  ids on the GameManager's AdManager (ads are inert until then); register the analytics events. AdMob (2026-11-13):
  the adapter is in, with Cannonfire's own AdMob app ids in the Android manifest and `FarmFuryIosBuild` (see the
  2026-11-13 entry below).
- **Identity (2026-11-10):** productName "Farm Fury Cannonfire", company "Tenbucks Mobile", bundle id
  `com.farmfury.cannonfire` (Android + iOS; Standalone still `com.DefaultCompany.2D-URP`). The home-screen label is
  "Cannonfire" — subtitle only, like "Stampede" / "Arcade" — set at build time by `FarmFuryAndroidBuild` /
  `FarmFuryIosBuild` (neither compiled yet: batch checks build the Windows target only). The Editor's PlayerPrefs moved
  with the rename; they were copied once from `DefaultCompany\FarmFury` to `Tenbucks Mobile\Farm Fury Cannonfire`
  (the old registry key is untouched). Store products keep their `farmfury_` ids (not player-visible).
- **Branding (2026-11-10, final — user's pick):** the logo is `Cannonfire/Logo_C.png` (the user's transparent cut:
  FARM FURY™ over a firing cannon and a wooden CANNONFIRE sign), the landing page is the user's own poster
  `Cannonfire/ic.png` (1280x720, logo painted in) used as is, and the app icon is the generated one (cannon blasting
  CANNONFIRE over red-orange rays, `Sprites/AppIcon.png`). `python tools/brand_cannonfire.py` (defaults: --logo
  Logo_C.png --landing ic.png, icon untouched unless --icon) writes `Franchise/Menu/FF_Logo.png` (Leaderboard) and covers
  the old FARM FURY mark on the 20 results/pause/new-character canvases (template-matched on the pre-rename originals
  in `Cannonfire/originals/`, inpainted, logo at 1.4x the mark's width). Other attempts (Logo2_*, Logo_Cannonfire*,
  Poster_*, Landing_*) stay in that folder; `tools/compose_cannonfire_logo.py` builds the blast-lettering variant. Kling
  total for the rename 26 credits. The pale purple band at the canvases' left edge predates this (2026-11-02 inpaint).
- **Landing is two pages (2026-11-13, user's own 2400x1080 posters, drawn as cover):** page 1 `Sprites/UI/LandingTitle.png`
  (from `Cannonfire/ic.png`, Cluck + Ducky by the cannon) with a pulsing `PressStart.png` (`Rough_Text/Press Start.png`,
  only 323x60 - upscaled ~1.7x, may look soft; `UI/Kit/PulseScale.cs`); tap anywhere (or Enter / Space) -> page 2
  `Sprites/UI/LandingPage.png` (from `Cannonfire/LandingPage2.png`, no characters) with Play / Exit / cog. The game boots
  on page 1; `GameFlow.EnterLanding` (World Select back, Home) lands on page 2; Esc on page 2 -> page 1. Both imported at
  maxTextureSize 4096 (SpriteAutoImporter). `LandingPage_Blur.png` is wired but unused by the landing now. Unseen in Play mode.
- **Not ported yet:** Stampede's Boot scene / Android AAB split + store build scripts, the iOS privacy manifest,
  the Character Story Cosmetics tab.
- **World 2 (Frozen Tundra) was rebuilt 2026-11-09** and **World 3 (Watermill Village) built 2026-11-10** as 22 framed
  designs each (`tools/design_world2.py`, `tools/design_world3.py`) — probe-clean but unseen in Play mode. Worlds 4-9
  are framed designer levels too now (World 4 2026-11-11, Worlds 5-9 2026-11-12); `WorldLevelGenerator` generates
  nothing any more. Solvability with 3 animals over two rooms is untested in every world. Ducky/Horace/Gerald/Billy
  poses are Stampede platformer frames standing in for cannon poses (no Idle/Impact pose).

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

- **2026-11-13 (later) — Wording, swap advice everywhere, live cannon skins with a glow.** Not compiled by me (the
  Editor was open); unverified in Play mode.
  - Banners: "STAGE CLEARED!" (was ROOM CLEARED) and "ONE SHOT!  ADDING A FARM FURY". Revive prompt: "to add a Farm
    Fury" beside Yes / Watch Ad, the button column moved left (`RevivePromptScreen.ButtonsX` -170).
  - `TutorialHints.SwapAdvice` on every level except the W1_L01-L04 lessons: between shots it reads the current room
    (`LevelLoader.SpawnedBlocks` / `SpawnedRobots`) - robots under a roof -> Cluck / Gerald, a third stone -> Bessie /
    Horace, robots spread > 6.5 u -> Ducky / Billy / Woolly, a tower > 5.5 u -> Percy / Horace - and, if the cannon's
    animal isn't a fit and a fitting one waits in the lineup, says "Try <animal> here - <why>. Press here to switch!"
    with the hand at the swap button. Once per stage, 6 s or until the shot. Thresholds are first guesses.
  - Cannon skins apply the moment they're equipped: `SaveManager.EquippedChanged` (raised by SetEquippedCannon /
    Trail / Impact) -> `CatapultLauncher.ApplyCannonSkin` (also on level start). A skinned cannon gets
    `Launcher/CannonSkinGlow.cs`, made more prominent the same day: a gold OUTLINE (the cannon sprite drawn by
    `Resources/Shaders/SpriteSilhouette.shader` 12% larger about the art's centre), a 1.75x radial halo, both pulsing
    (order 3 at z -0.05/-0.04: in front of the bales, behind the cannon), and 8 twinkling stars circling it (order 5).
  - Locker: no "None" tile (tap the equipped item to take it off); three centred rows of equal 140 tiles / 50 gaps laid
    out from the top under the banner (`FirstRowTop`, `RowStep` 185); the Equipped tick inside the tile's corner.

- **2026-11-13 — Hidden robots, bosses, lighter flight, cross-promo, Robots tab, two-page landing.** Compiled, levels
  regenerated, scene wired; unverified in Play mode.
  - **Hidden robots** (user screenshots, Worlds 7/8): `design_world1.robot_exposed` replaces `reachable_clear` in
    `prune_hidden_robots`: numpy arcs (20-80 deg, `REAL_GRAV`, animal radius 0.22) stop at mounds AND blocks; a robot
    behind a mound stays only if some arc's first contact is the robot (box not padded upward, ties go to the block in
    front) or a piece within 1.6 u at the robot's height hit <= 50 deg down. `reachable()` keeps GRAV 8.0 on purpose
    (seed picks unchanged). All nine designers re-run: only `R(...)` lines changed (W4 -2, W5 -4, W6 -3 +1, W7 -4,
    W9 -4 vs the last commit; Worlds 1-3 and 8 unchanged). W8's screenshot robot was NOT caught - recheck in Play mode.
  - **Bosses:** `RobotEnemy` no longer runs the taunt loop (it cycled into the Hit/Defeated poses); one pose until
    death. `IsBoss` (`LevelLoader` marks RobotType.Commander): angular damping 3; tilted > 35 deg and resting 0.7 s ->
    `GetUp()` (lift + turn upright, 0.45 s). Any live robot that travels > 1.5 u and leaves the camera view, or falls
    6 u below its spawn, dies (counts as a kill).
  - **Flight:** `ReferenceFraming.ProjectileGravitySh` 0.5 -> 0.38 (user: "can only go so far ... feel heavy"; speed
    unchanged): ~32% more range, flatter arcs, ~15% longer in the air.
  - **Cross-promo:** `Core/CrossPromo.cs` (Stampede App Store 6825786519 / `com.farmfury.stampede`, Arcade 6804415300 /
    `com.farmfury.arcade`; Play referrer utm_source=cannonfire). Menu hub cards either side (`MenuHubScreen.PromoCard`,
    app icons on a wood rim, `Franchise/CrossPromo/*.png`, PLAY FREE plaque, parental gate, `promo_shown/tapped`
    xpromo_stampede / xpromo_arcade). No install rewards (store policy).
  - **Robots tab** (Character Story): Stampede's design - one world at a time (< >), the boss inside a turning
    `UI/Kit/RobotRing.cs` of its three robots, a card for the tapped one. Art `MenuArt.worldRobots` (36, the robot
    prefabs' `_robotSprite`, SceneSetup); copy `StoryContent.Bosses` (one per world), `RobotTypes`, `RobotTypeNames`
    (World 3-9 grunt names are placeholders).
  - **AdMob Android ad units (2026-11-13, user-created; dashboard-only - entered as AdMob instances in the LevelPlay
    Cannonfire Android app, never in the game):** banner `ca-app-pub-1264425755955045/4953509317`, interstitial
    `/6075019298`, rewarded `/6258852120`. AdMob Android APP id `ca-app-pub-1264425755955045~6913458246` is in
    `Assets/Plugins/Android/AndroidManifest.xml` (copied from Stampede: GameActivity launcher, AD_ID / ad-services
    permissions removed for Families) with Stampede's `mainTemplate.gradle` / `settingsTemplate.gradle` /
    `gradleTemplate.properties` / `proguard-user.txt` and `LevelPlay/Editor/ISAdMobAdapterDependencies.xml` (Android
    packages + the iOS pod IronSourceAdMobAdapter 5.12.0.0). **iOS (same day):** AdMob app id
    `ca-app-pub-1264425755955045~9969711472` written as GADApplicationIdentifier by `FarmFuryIosBuild` (which also
    adds `Assets/iOS/PrivacyInfo.xcprivacy`, Stampede's); iOS ad units (LevelPlay dashboard only): banner
    `ca-app-pub-1264425755955045/4586677801`, interstitial `/8273486420`, rewarded `/1792993501`.
    The Player Settings switches (useCustomMainManifest / MainGradleTemplate / GradlePropertiesTemplate /
    GradleSettingsTemplate / ProguardFile = 1, as in Stampede) must be ON or the files are ignored. **LevelPlay config
    lives in `SceneSetup.WireAdManagerConfig` constants** (applied by Wire Scene References; empty = leave the field
    alone): Android app key `2985da825`, ad units rewarded `5oi5ruaosq1nuv0p`, interstitial `yzpwovsnpro6lllh`, banner
    `rrhw0hhrp8cbibo3`; iOS app key `2985d6e9d`, rewarded `ucidbb5jdftywed2`, interstitial `wteebsq09o795d9d`, banner
    `xpfpse4z5v88lglq`. All LevelPlay config is in; untested on a device. Rewarded placements to create in both
    LevelPlay apps: `continue_after_death`, `double_coins_level_complete` (capping / pacing off). The active build
    target is now Android (needed by the Android Resolver; `Settings/Build Profiles/Android.asset`). The Unity Cloud
    project Cannonfire is linked to (`9d682642-…`, not Arcade's `54837130-…`) showed the name "Farm Fury Arcade" on
    2026-11-13 - a dashboard labelling mistake the user is fixing.
  - **Ads audit:** AdManager is byte-identical to Stampede's. Since the same day's setup (above) Cannonfire has the
    LevelPlay keys / ad unit ids, the AdMob adapter + both app ids and the iOS privacy manifest, matching Stampede.

- **2026-11-12 (latest) — Characters released per level, two per world** (user: "released after certain amount of
  levels on each world - not together, but two per world; so it goes to 'unlock character' as per stampede and arcade -
  on the carousel they are greyed out and cannot be selected until they are released"). `AnimalRoster.UnlockAfter`
  (levels completed in the animal's own world): Cluck 0 / Bessie 1 (W1), Percy 1 / Woolly 8 (W2), Ducky 1 / Horace 8
  (W3), Gerald 1 / Billy 8 (W4) - Horace, Gerald and Billy moved from Worlds 4 / 5 / 6. Derived in
  `SaveManager.IsAnimalUnlocked` (nothing stored; old saves re-evaluate). The releasing clear shows the New Character
  page (`TakeNewlyUnlockedAnimals`). Character Select: locked cards greyed (`LockedTint`, was a silhouette) with a
  "LOCKED / Clear N levels in <World> (k/N)" label. Compiled; unverified in Play mode. Level lineups still lead with
  the old intro order (only the slot count matters).

- **2026-11-12 (latest) — The paid worlds (7 Dustbowl Canyon, 8 Harvest Fairground, 9 Crop Factory) built on the
  World 1-6 template** (user: "recall your process of building the worlds, apply this and build the pay worlds ...
  utilise artwork available, otherwise access kling AI ... apply the structure plan (be creative)"). Compiled;
  unverified in Play mode.
  - **Art:** `python tools/import_paid_worlds_art.py` -> `Sprites/Environment/{Canyon,Fair,Factory}/`,
    `Launchers/{Prospector,Circus,Pneumatic}Cannon.png`, `Enemies/Robot/{DrillRig,Ringmaster,HarvesterPrime}*.png`,
    `Scripts/Level/MountMeasurementsW7-9.cs`, `tools/mounts_w7-9.json`. Reused (Stampede `UI/DustbowlCanyon`,
    `HarvestFairground`, `CropFactory`): DC/HF/CF_Parallax_Far (far), GroundStrip / DeckStrip + fills (floor), Saguaro /
    LampPost / RobotArm (corner silhouettes), WaterTower / FerrisWheel / GrainSilo (landmarks), Saguaro / Tumbleweed /
    PopcornCart / Pumpkin / Hopper + CornKernel (contraptions), SandstormDust / Drone (decor), the three bosses. Drawn:
    cactus spine, popcorn, the cog. Kling 9 images / 18 credits, all first try, in
    `FarmFury_Artwork/{DustbowlCanyon,HarvestFairground,CropFactory}/Framed/` (W<N>Cannon, W<N>Pieces, W<N>Mounts). New
    trick: Kling was given a grey SILHOUETTE TEMPLATE on magenta (six pieces / three flat-topped mounts) + a 2x2 Stampede
    style sheet (inputs must be <= 2:1 aspect) - no colour bleed from earlier worlds' sheets (World 6's did). W7Cannon
    came back on red (`rekey_border`); the canyon mesas' foot shrubs join them, so mounts are cut at column-occupancy
    minima (`column_part`); Kling painted W7's plank red, so it is re-hued to timber (`weathered_wood`).
  - **Code:** `EnvironmentDepthSystem` themes Canyon / Fair / Factory (`_sprCanyon*` etc., all "Flat" stacks like Worlds
    4-6; `Flat` replaces the old `Space || Sea || Sky` chains); `Core/PaidWorldDecor.cs` (tumbleweeds bouncing along the
    ground, sandstorm banks, a circling hawk / hot-air balloons, fireworks at CarnivalNight / HarvestMoon / steam plumes,
    sparks, a crate drone); 15 weathers (HighNoon Sandstorm DesertDusk CanyonNight HeatHaze / FairDay CarnivalNight
    HarvestMoon FairDrizzle GoldenHour / ShiftStart SteamWorks NightShift Overdrive Overtime); `BlockBase._spr*W7-9`;
    `CatapultLauncher._cannonSpriteW7-9`, `_mountBaleSpriteW7-9` (dusty hay, prize cushions, produce crates), the 4-row
    pyramid + FarmMountShift, landmarks (`_waterTowerBackSprite` etc.), wipes (sandstorm, confetti cannon, steam blast);
    `LevelLoader._platformSpritesW7-9` + `MountsW7-9`; `GadgetType` Cactus / PopcornCart / CornHopper (Sunflower pump mode)
    and Tumbleweed / Pumpkin / Cog (RollingBoulder); boss prefabs `DrillRigRobot` / `RingmasterRobot` /
    `HarvesterPrimeRobot` (`EnsurePaidWorldBossPrefabs`) on `_worldRobots[7-9].boss`. `SceneryBuilder` skips framed
    Worlds 2-9.
  - **Levels:** `tools/design_paid.py` (shared kit) + `tools/design_world7.py` / `8` / `9` -> `LevelDataGenerator.World7-9.cs`
    (`MakeWorld<N>Framed`, ids `W<N>_L01..L12`). World 4's city generator on each world's mounts, two rooms per level,
    level 12 = outer works + the boss's keep (room 2). Each world adds its own robot-built set pieces to the generator's
    table: W7 headframe / water_tower / saloon / rock_arch, W8 carousel / high_striker (dynamite bell) / prize_stack /
    big_top, W9 conveyor / press / silo_stack / crane_arm. A level that finds no valid seed relaxes (one district fewer,
    a 'fall' skyline -> random, one robot fewer at the minimum) and says so in the designer output. Designer: W7 12
    levels / 135 robots / 0 issues (L10, L11 relaxed to 4 districts), W8 12 / 133 / 0 (L07, L10 relaxed), W9 12 / 136 /
    0. World 8's podiums are boxes: their solid band / plateau are inset to the middle 80% (`import_paid_worlds_art`)
    and World 8 sizes mounts with `design_paid._shape_narrowest` (design_world4._shape's tie-break picked the low, wide
    shape for tall mounts; kept for Worlds 4-7 and 9 so their output is unchanged). PhysicsProbe (no input): all 72 rooms
    (Worlds 7-9, both rooms) stand untouched, no exceptions (`unity/Logs/probe_w7/8/9_room1/2.log`).
    `WorldLevelGenerator` now skips Worlds 2-9 (generates nothing) and keeps `W02_`-`W09_` files.

- **2026-11-12 (latest) — Polish round before the paid worlds** (user list). Compiled; unverified in Play mode.
  - **Robots hidden behind a mound:** `design_world1.prune_hidden_robots` / `prune_all` (called by every designer's
    `write_cs`): `reachable()` ignores obstacles, so a robot low behind a mound (cloud / reef / pedestal / mesa / island,
    mount variants 0-3; ramps, wagons and scaffolds are not walls) counted as reachable. `reachable_clear` traces the same
    arcs from the raised launch point and stops in a mound's body; robots no arc reaches are dropped (never a boss, never
    a room's last robot). Dropped: W4 L04/L06/L09/L10/L24, W5 L09, W6 L08/L16 (one each); Worlds 1-3 none.
  - **Deep Dredger 1.5x:** `ROBOT_W[5]` boss scale 7.0 -> 10.5 (vis 3.006) - the wide sub read small on its canvas.
  - **World 6 sky:** drones and small ships gone (SpaceDecor flyers and the warp wipe's drone); `Core/SpaceDecor.cs` -
    a faint milky-way band x2, spiral galaxies x2 (turning slowly), six asteroids drifting / tumbling at three depths,
    two comets streaking down-left every 5-11 s. Art: one Kling sheet (W6SpaceDecor.png, 2 credits) cut by
    `import_mothership_art.space_decor` (border-only keying: the milky way's pink must survive). The warp wipe tumbles
    asteroids instead of a drone.
  - **Sky Islands landmark:** the rune tablet in the clouds (`Sky/SkyTablet.png`, cut by `tools/import_sky_tablet.py`
    from `FarmFury_Artwork/SkyIsland/SkyTemple.png`), `_tabletBackSprite`, BackProp Tablet (floats 0.3 u).
  - **"Adding FarmFury"** replaces "+1 animal": the one-shot room banner and the revive prompt's reward label.
  - **Beginner tips** (`UI/TutorialHints.cs`, added to the HUD by HUDController): W1_L01 aiming (a cartoon hand drags
    back from the loaded animal until the first shot, then "Topple the structure onto the robots!"), W1_L02 "Press here
    to change characters" (hand at the switch button), W1_L03 rooms, W1_L04 abilities (tapping hand during the first
    flight). Nothing on any other level. Hooks: `CatapultLauncher.Fired` / `LoadedPosition`, `AnimalBase.AbilityUsed`,
    `HUDController.SwapButtonRect` / `_hintHandSprite` (`Sprites/UI/Franchise/Hint/HintHand.png`, Kling 1 credit).

- **2026-11-12 (latest) — World 6 Robot Mothership built on the World 1-5 template** (user: "go ahead and build W6 -
  mothership - applying the same process"). Compiled; unverified in Play mode.
  - **Art:** `python tools/import_mothership_art.py` -> `Sprites/Environment/Mothership/`, `Launchers/GravityCannon.png`,
    `Enemies/Robot/FactoryMachine*.png`, `MountMeasurementsW6.cs`, `tools/mounts_w6.json`. Reused (Stampede MotherShip):
    RM_Parallax_Far (far), DeckStrip / HullFill (floor + underfill), SateliteDish (silhouettes), Drone + Ship (SpaceDecor,
    a SeaDecor with no jellies / bubbles), WarningBeacon (landmark), LaserEmitter (pump-mode contraption firing a
    code-drawn PlasmaOrb), GravitySling (rolling GravityRing), the Factory Machine (boss). Kling 3 images / 6 credits in
    `FarmFury_Artwork/Mothership/Framed/` (W6Cannon, W6Pieces, W6Mounts). The pieces sheet's post (blue bundle) and
    column (white marble) bled in from the World 5 / World 4 sheets - usable, a 2-credit redo if they read wrong.
  - **Code:** the World 5 pattern again - `Space` theme in EnvironmentDepthSystem (`_sprSpace*`, no biplane), weathers
    StarField / RedAlert (a soft red grade, no flashing) / NebulaGlow / SolarFlare / PowerDown, `BlockBase._spr*W6`
    (the Worlds 4-6 "hi" slots), `_cannonSpriteW6` / `_mountBaleSpriteW6` (battery pods) / `_beaconBackSprite`,
    `_platformSpritesW6` + `MountsW6` (DeckLow / Mid / Tall), `GadgetType.LaserEmitter` / `GravityRing`,
    `FactoryMachineRobot.prefab` on `_worldRobots[6].boss`, the warp-jump wipe (deep-blue veil, two speeds of star
    streaks, star points, a drone; no flash). `WorldLevelGenerator` now makes Worlds 7-9 only.
  - **Levels:** `tools/design_world6.py` -> `LevelDataGenerator.World6.cs` (`MakeWorld6Framed`, ids `W6_L01..L16`): the
    city generator on the pedestals, two rooms each, emitters / gravity rings / dynamite, L16 = outer works + the Factory
    Machine's keep in room 2. Billy leads. Designer 0 issues; Worlds 1-5 designer output unchanged. PhysicsProbe (no input)
    both rooms 32/32 untouched (`unity/Logs/probe_w6_room1/2.log`).

- **2026-11-12 — Slow-motion shots, slower room change, wipes for Worlds 2-4** (user: "slow the transition down - it
  feels rushed ... the character still flys too fast, slow them down - almost slow mo - also then adjust the ability
  speed in parallel ... the user should feel the anxiety, anticipation of their shot - apply to all built worlds").
  - `ReferenceFraming.FlightPace` 0.6 (framed levels = every built world): launch speeds x pace, projectile gravity x
    pace^2 - the same arcs (preview, reach checks), 1/0.6 = 1.67x as long in the air. `AnimalBase.Pace` (set by
    `ApplyFlightTuning`): the launched animal's mass / pace (same momentum, same hits); abilities in step -
    `AbilitySpeed` floors x pace, `AbilityProjectile.Spawn` (thrown by an animal) gravity x pace^2 / mass / pace / life
    and growTime / pace / spin x pace, Cluck's eggs, Bessie's slam. The trajectory preview takes 1/pace substeps so it
    still shows the same stretch of arc. Contraptions keep their own speed.
  - `CatapultLauncher.RoomPanDuration` 1.0 -> 1.8 s (the swoosh still peaks mid-pan; the wipes stretch with it).
  - Wipes: World 2 blizzard whiteout (white veil, snow clouds, 160 snowflakes, driving streaks, ice splinters, a frozen
    bale), World 3 river splash (blue-green veil, spray, 70 spinning droplets, flour puffs, a flour sack), World 4 cloud
    bank (white veil, 38 clouds, a balloon and a kite). Shared sprites loaded as they are (no re-import).

- **2026-11-12 (latest) — World-themed room-change wipes, Worlds 1 and 5** (user: "a panning effect that is specific
  to a world - example - cornfield (hurricane wind), sunken world (bubbles) - that cover the whole screen while it pans";
  "build world 1 and world 5 first"). Compiled; checked with an offline render of the same maths, unverified in Play mode.
  - `Launcher/PanWipe.cs`: screen-space layers (a full-screen veil, or a swarm of one kind of sprite with size / start /
    spread / travel / spin / wobble / tint / order; fish flip to face their travel). Coverage envelope: builds over the
    first 38% of the pan, holds ~40-60% (the camera's fastest moment, the swoosh peak, the room swap), clears from 62%;
    ~85% opaque at its thickest. Orders 35-37: over the play layer, under the rain / grade and the HUD canvas.
  - `CatapultLauncher.WipeLayers(world)` (recipes) replaces the speed streaks where a recipe exists:
    World 1 hurricane gust = khaki dust veil, 26 dust clouds (cornfield Cloud_0/2 tinted), 50 wind streaks, 50 spinning
    golden leaves, 10 feathers, 2 tumbling haybales, all blasting right to left; World 5 bubble surge = teal veil, 85 big
    + 130 small bubbles boiling up and drifting left, a school of 12 fish darting across. Other worlds: the streaks.
  - Art: `Sprites/Effects/PanWipe/Leaf_0-9`, `Feather_0-3` cut by `tools/import_pan_wipes.py` from CornHuskTrail.png and
    Impact_Feathers.png (no Kling); wired by `SceneSetup.WireRoomWipes` (`_wipeDust/_wipeLeaves/_wipeFeathers/_wipeHay/
    _wipeBubble/_wipeFish`).

- **2026-11-12 (latest) — World 5 Sunken City built on the World 1-4 template** (user: "begin with W5 - utilise the
  exact same process as the other worlds, seek artwork in the artwork folders, if you dont have call on Kling AI ... apply
  the same two room panning, and enhanced structures also"). Compiled; unverified in Play mode.
  - **Art:** `python tools/import_sunken_art.py` -> `Sprites/Environment/Sunken/`, `Launchers/SeaCannon.png`,
    `Enemies/Robot/DeepDredger*.png`, `Scripts/Level/MountMeasurementsW5.cs`, `tools/mounts_w5.json`. Reused (Stampede
    Sunken City): SC_Parallax_Far (far), SeabedStrip / SeabedFill (floor + underfill colour), Kelp (corner silhouettes),
    Fish_1-6 / JellyFish / Bubble (`SeaDecor`), Clam + pearl, ShipsWheel, SunkenStatue (landmark), the Deep Dredger
    (boss, DeepDredger_Left idle / mirrored alert / Defeated hit). Kling: 3 images, 6 credits, in
    `FarmFury_Artwork/SunkenCity/Framed/`: W5Cannon (brass diving-helmet torpedo cannon), W5Pieces (one sheet: shipwreck
    post + plank, teal block / column / slab, sponge bale - split by `import_sky_art.split_pieces`), W5ReefMounts (three
    flat-topped coral reef mesas - `sheet_part`).
  - **Look:** `EnvironmentDepthSystem` Sea theme (`_sprSea*`, `Sea` / `SeaReady`, no mid band or wall like World 4, kelp
    sways 0.3); the biplane fly-by is off under the sea. Weathers SunlitShallows / KelpForest / MurkyDepths / GlowingDeep /
    Undertow in `CornfieldAmbience` (no clouds or crows, surface light, marine snow, glowing plankton, murk).
    `Core/SeaDecor.cs`: fish crossing at three depths (flipped to face their way; `SeaFishFacesRight`), jellyfish pulsing
    upward (glowing in GlowingDeep), bubbles rising.
  - **Pieces / cannon / mounts / contraptions / boss:** `BlockBase._spr*W5` (Worlds 4 and 5 share the "hi" slots in
    Initialise); `CatapultLauncher._cannonSpriteW5`, `_mountBaleSpriteW5` (sponge), 4-row pyramid + `FarmMountShift`,
    `_statueBackSprite` landmark; `LevelLoader._platformSpritesW5` + `MountsW5` (reef low / mid / tall, tall = mesa);
    `GadgetType.Clam` (Sunflower pump mode, pearls) and `GadgetType.ShipsWheel` (RollingBoulder);
    `DeepDredgerRobot.prefab` (`EnsureDeepDredgerPrefab`) on `_worldRobots[5].boss`.
  - **Levels:** `tools/design_world5.py` writes `Editor/LevelDataGenerator.World5.cs` (`MakeWorld5Framed`, deletes older
    `W05_*` files; ids `W5_L01..L22`): World 4's city generator (`design_world4.city`) on the reef shapes
    (`w4.CLOUDS` re-pointed), every level two rooms (`room2_spec`), clams / ship's wheels / dynamite as fixed
    contraptions, L22 = outer town + the Dredger's keep as room 2. Gerald leads. Designer 0 issues. PhysicsProbe (no
    input) both rooms: 44/44 untouched (`unity/Logs/probe_w5_room1/2.log`). No DeepDredger_Explode.png: the death burst
    falls back to the generic explosion.
    `WorldLevelGenerator` now skips worlds 2-5 (Worlds 6-9 only) and keeps `W05_` files.
  - Fixed on the way: World 4's room-1 seeds were pinned with `reach_world=1`, which moved when World 1's cannon was
    raised - now `reach_world=0` (the original ground-level launch, no LAUNCH_SHIFT entry); World 4 output identical.

- **2026-11-12 — Worlds 1-3: two rooms, taller cannon pyramid, a landmark behind the cannon** (user: "return
  to first world - apply the same logic as we did for W4"; then "apply the same now to W2 ... then apply W3"). Compiled,
  probed; unverified in Play mode.
  - **Cannon:** `CatapultLauncher.MountRows` 4 for Worlds 1-3 (bales 5-4-3-2, +1.6 u over the old 3-2) and
    `FarmMountShift` (-1.2, 0) - the same raised point as World 4. Designer `LAUNCH_SHIFT` 1-4 = (-1.2, 1.6).
  - **Landmark** (`PlaceSilo`, order 1, behind bales/cannon, travels with the rooms): W1 broken silo
    (`Cornfield/BrokenSilo.png`, cut by `tools/import_silo.py` from `MeadowRuins/FarmSilo.png`), W2 ice crystals
    (`World2Props/IceCrystals.png`), W3 mossy waterwheel (`World3Props/WaterWheel.png`); sizes in `BackProp`.
  - **Second rooms:** `design_world1.room2_for` / `farm_room` / `add_second_rooms` (seeded; 2-4 set pieces - wonky
    tower, stilt hut, plank tent, upside-down crate pyramid, leaning stack, balanced rock, cantilever, see-saw, dynamite
    deck - some on mesas / islands later; robots last via `settle_robots`; first seed passing every check). World 2/3
    designers call `d.add_second_rooms(LEVELS, 1)`. Boss levels (L18, W2_L22, W3_L22): a new outer yard first, the keep
    as room 2. Room 1 of every other level byte-identical. L01 (hand-written) appends `L01Room2*` from World1.cs.
    `ROOM2_SKIP` per designer for any room 2 that falls in the probe.
  - **Worlds 5-9 untouched:** `WorldLevelGenerator.Create` copies one room only (room 0; the boss template's last room
    shifted back) - assets identical but for ~1e-6 float rounding on the 5 boss levels.
  - Probe (no input): W1 both rooms 36/36 clean; W2-3 both rooms 88/88 clean (see `unity/Logs/probe_w1_room*.log`,
    `probe_w23_room*.log`).

- **2026-11-12 — Play-test fixes after the two-room pilot** (user list). Compiled; unverified in Play mode.
  - **Animal vanished mid-air:** `AnimalBase.MaxAbsX` (40) was measured from x = 0; room 2's cannon is at x ~ 27, so
    shots died at x = 40. Now measured from the launch x (`_launchX`).
  - **Floating plum removed** (Stampede's pellet) from `SkyDecor` (sprite still wired, unused).
  - **Loaded pose changed in room 2:** the display-only loaded animal kept its collider, and the pan dragged it
    through room 1's wreckage -> "hit" -> impact pose. `PrepareNextBird` now disables its colliders.
  - **Billy:** loaded = `Launch(1).png` (braced crouch), flight = `kling_20260721...5562` (full-body leap facing right,
    watermark specks dropped), impact = `Defeat.png`; goat-sized: radius 0.36 -> 0.44, PPU 2057 -> 1683.
  - **Percy:** loaded = `Launch2.png`, flight = `kling_20260721...5548` (leap facing right; ground shadow removed by
    `import_character_art.largest_only`), bounce = `Trigger.png`. `import_character_art.py --only <Animal>` added.
    Bounce Roll reworked: curls + dives, bounces off the first hit ONCE (bounciness 0.5, no boost, was 1.1 + 1.35x
    per bounce x3 - "bounced all over the screen"), stays curled (`KeepAbilityPose`), fades 0.6 s later (`EndSoon`).
  - **Abilities appear faster:** `AbilitySpeedFactor` 1.25 -> 1.6 and `AbilityProjectile` draws at order 7 (in front
    of the animal); at 1.25x they crept out from behind the animal's sprite for ~0.3 s.

- **2026-11-12 — Two-room levels, World 4 pilot** (user: "allow for two plays in one level... panning the camera
  across to another structure", AB2 rooms; then "utilise the swoosh_panning SFX... to coincide with the camera
  movement"). Compiled, probed; unverified in Play mode.
  - **Data:** `LevelData.rooms` + a `room` index on every Block/Robot/Platform/Gadget spawn entry; room r is laid out
    `LevelData.RoomStride` (32 u, > one screen width on any aspect) right of room 0, in absolute coordinates.
    Generator helpers take `room:` / `rooms:`. Older assets = 1 room.
  - **Flow (`LevelLoader`):** spawns one room at a time (`SpawnRoom`). The last robot of a non-final room ->
    `NextRoom()`: aim locked (`LevelReveal.LockAim`), wait for wreckage + the shot to settle, **+1 animal if the room
    took one shot** (banner "ONE SHOT! +1 ANIMAL", else "ROOM CLEARED!"), `CatapultLauncher.PanToRoom` (1.0 s
    ease-in-out, speed streaks, cannon + mount + loaded animal + camera move together), old room destroyed, next spawned,
    `LevelReveal.RevealRoom` (boss drop-in only in a boss level's last room), then the usual out-of-animals check.
    Lineup shared across rooms; stars count all rooms' robots. HUD: room dots top-centre (`SetRoomProgress`), `ShowBanner`.
    Ground widened for framed levels (`SetGroundSpan`); corner silhouettes ignore the room offset (`RoomPanX`).
  - **Sound:** `AudioManager._roomPanClip` = `Assets/Audio/Swoosh_panning.mp3` (from `Launch/`), `PlayRoomPan`: the clip
    peaks 1.9 s in, so it is joined 1.4 s in and peaks at mid-pan (the fastest frame); the tail fades 0.4 s after.
  - **World 4 designs:** `design_world4.city_level` adds a smaller second city (`room2_spec`: one fewer peak (3-4), the
    flipped skyline profile, 3-4 / 3-5 / 4-6 robots by level band, own seed range, checked from the real raised launch
    point) via `Level.add_room` (design_world1). Room 1 of L01-L23 is byte-identical to before; L24's room 1 is a new
    outer town and the Storm Baron's keep (pinned seed) is room 2. Designer 0 issues; PhysicsProbe both rooms
    (`FF_PROBE_ROOM=0|1`, new; `LevelLoader.DebugStartRoom`) all 24 stand untouched (`unity/Logs/probe_w4_room1/2.log`).
  - Open: difficulty with 3 animals over two rooms is untested; the weathervane stays in room 1.

- **2026-11-12 — World 4 cannon raised up-left** (user: "the character struggles to fly over the structure").
  `CatapultLauncher.SkyMountShift` (-1.2, +1.6) moves the cloud-bale mount + cannon (and so the barrel, aim pivot,
  loaded animal, preview) in World 4 only; the mount floats clear of the cloud floor. The framed camera now centres on
  `_cannonScenePos.x` (not the rest pos) so the framing doesn't follow. Designer: `LAUNCH_SHIFT` in design_world1.py;
  `check(L, reach_world=1)` keeps World 4's city seeds pinned (generated levels byte-identical; final report uses the
  real launch point: 0 issues). Compiled; unverified in Play mode.

- **2026-11-11 — Softer impacts; World 4 rebuilt as generated sky cities** (user: impact "too high and shoots
  the structure apart... just enough to topple should it hit correctly - if it doesnt nothing should happen - for
  example hitting a stone"; then "make your structures wilder, more absurd... almost like cities of elevation"; chose
  World 4 first, big & dense). Unverified in Play mode.
  - **Impact:** `AnimalBase.ImpactMassScale` 0.4 (animal body mass in hits: Cluck 8 -> 3.2, Bessie 28 -> 11.2; ability
    projectiles too, `AbilityProjectile`); `StoneBlock` baseMass 8 -> 16; the 0.6 x momentum `HitShove` (max 36) and
    `StackShove` removed — `ShoveOnHit` is now a small tip nudge (0.08 x momentum, max 3) only for a non-stone piece
    struck above its centre; live block linear damping 0.05 -> 0.3. `PhysicsProbe` gained a test shot
    (`FF_PROBE_SHOT=high|low`, `FF_PROBE_ANIMAL`, `FF_PROBE_SPEED`; logs `[Probe] SHOT`: toppled / thrown > 3 u / top
    piece speed / robots lost). World 1, Cluck at 10 u/s, high on the front piece: top speed 15-29 -> 1-9 u/s, thrown
    pieces 3-15 -> mostly 0-3; stone targets (L14/L15/L18) no longer move; low hits on solid bases do ~nothing, low
    hits on stilts / legs still bring towers down. All 86 framed levels still stand untouched (no-input probe).
  - **Sky cities (`tools/design_world4.py`):** absurd pieces (`lollipop`, `upturned` (widens as it rises), `spire`
    (zig-zag), `balcony`, `teeter_town`, `slab_on_point`, `chimney`, `stone_crown`, `pylon` + `walkway`) and a seeded
    generator `city()` / `city_level()`: 3-5 peaks per level on a skyline profile (rise / fall / valley / mount /
    random), a piece on each (sometimes a second stacked on top), walkways between neighbours, robots last
    (`settle_robots` drops any whose real 0.6 x 0.9 collider would overlap wood, trims to the level's range). A level is
    the first seed that passes every designer check (`skip=` passes over ones that fell in Unity). Fixed parts per
    level: thunderclouds, the slide + wheel (L04/L12/L20), the crane (L06), the Storm Baron's keep (L24). Cloud mounts
    are solid only in their plateau band now (`import_sky_art`: `boxes[:1]` for Cloud*), so peaks overlap like a range;
    `fit_clouds` no longer lifts clouds (`LIFT_CLOUDS`) and sizes only from pieces touching the plateau. Helpers are
    named `AT()`/`INSET` — `H`/`P` are Horace/Percy (shadowing them broke the generated C# once).
  - **Reveal fix (`LevelLoader.GoLiveAfterReveal`):** robot colliders are switched off before the blocks go live.
    Each robot's pop used to switch its collider off mid-reveal, and ending a touching contact wakes both bodies in
    Box2D — every pop woke the piece under its robot; balanced roofs shrugged it off, the sky cities' caps and
    balconies tipped (10 of 24 fell by themselves, non-deterministically). After: 24/24 World 4 cities stand untouched.
  - Open: solvability with 3 animals against 4-8 robots spread over several peaks is unchecked; Worlds 1-3 not yet
    rebuilt in the city style (user wants World 4 reviewed first).

- **2026-11-11 — World 4: cloud-mountain mounts, busy sky, backdrop actually on** (user: "insert bigger
  floating clouds that sit like mountains on which the robot structures are built... your extra floating items - I
  dont see them"). Unverified in Play mode; designer 24/24 0 issues, PhysicsProbe (no input, `unity/Logs/probe_clouds.log`)
  all 24 World 4 levels 100% intact, 0 awake, 0 movement, no exceptions.
  - **Root cause of "I don't see them":** `EnvironmentDepthSystem.CornfieldActive` listed worlds 1-3 only, so World 4's
    whole framed stack (cloud bank, isle soil, isles band, silhouettes, `CornfieldAmbience` weather, `SkyDecor`) never
    switched on — World 4 showed only the plain fallback painting. Now worlds 1-4.
  - **SkyDecor** is three depth bands: far (small, hazed toward the horizon, order -36 behind the haze veil), mid (-34 /
    lanterns -33), near (large, -29, in front of the isles band, behind the play layer): 5 balloons drifting both ways,
    4 kites, 2 floating plums, 7 lanterns, 0.02-0.15 of the screen height.
  - **Cloud mountains** replace the floating isle and rock as World 4 mounts 0/1/2 (`Mount_CloudLow/Mid/Tall.png`,
    `MountMeasurementsW4.Cloud*`): all-cloud mesas with a flat plateau, standing on the ground, split from one Kling
    sheet (`SkyIsland/Framed/W4CloudMountains.png`, guided by a hand-drawn flat-top mesa template + StormCloud.png as
    style; two text-only misses kept as `W4CloudMountains_miss1/2.png`; 6 credits). Base cropped flat (grows out of the
    cloud floor). New `PlatformSpawnData.height` (0 = art aspect; `P(..., height:)`) stretches a mount vertically
    (LevelLoader scales colliders by x and y separately).
  - **Designer:** `design_world4.isle()/rock()` now call `cloud()`; `fit_clouds()` sizes each cloud to what stands on
    its plateau (+0.35 u), picks the shape, stretches <= 1.3x, and raises a too-flat cloud (< 0.85 of its art's
    proportion, capped at G + 4) lifting everything on it. `cloud_checks()` flags pieces / robots / gadgets inside a
    cloud. Moved to clear the wider bases: thunderclouds on L01/L11/L23, L10's right rock (13.3), L24's stone stack (5.4).
  - **No grass in the sky (follow-up, user: "theres no grass in the sky... a row of windmills and other items in the
    background that we should remove"):** the ground strip `SkySoil.png` is now a cloud floor (Kling `W4CloudFloor.png`;
    `import_sky_art.cloud_floor` clears the puff poking above the flat band and fills the gaps below with cloud blue
    fading to `SkyUnderfill`; `SkySoilGroundUV` 0.749 -> 0.929); the slide ramp's grassy rock is a cloud plateau
    (`W4RampCloud.png`, Ramp surfaceUV 0.997, aspect 2.424). The isles band (`SkyMid`, the windmill/temple row) and the
    cloud-bank wall (`SkyWall`, read as a dashed fence over the new floor) are off for World 4 (`ThemeMid`/`ThemeWall`
    null for Sky; still wired). Kling 4 more credits (10 total for this round). Compiled / probed: NOT yet — the Editor
    was open; run Generate All Level Data (the ramp levels L04/L12/L20 moved with the new ramp measurements).
  - The same `levels` run finally regenerated W2_L17 Whiteout / W3_L15 Thunderhead (`wonky=False`): both probe clean
    (`unity/Logs/probe_w2w3.log`).

- **2026-11-11 — World 4 Sky Islands built on the World 1-3 template** (user: "build the next world utilising
  the exact process you have used for every world build... use the artwork available, but access kling AI for new art
  required, like the cannon and scenery (use credits most efficiently)"). Unverified in Play mode; designer-checked
  (24/24, 0 issues) and PhysicsProbe-checked (no input, `unity/Logs/probe_w4.log`): all 24 World 4 levels 100% intact, 0 awake, 0 movement
  - **Levels:** `tools/design_world4.py` writes `Editor/LevelDataGenerator.World4.cs` (`MakeWorld4Framed`, deletes older
    `W04_*` files; ids `W4_L01..L24`). Structures stand on floating isles (`isle()`) and floating rocks (`rock()`, mount
    variant 2, asserted clear of the ground) at different heights, plus the robot-built set pieces and wonky towers.
    Horace leads the lineups. `design_world1.py` knows `world=4` (`SPRITE_W4`, `KIND_W4`, `HOIST_W[4]`,
    `GADGET_NAMES[4]`). `WorldLevelGenerator` skips worlds 2-4 and keeps `W02_`-`W04_` files.
  - **Art:** `python tools/import_sky_art.py` -> `Sprites/Environment/Sky/`, `Launchers/SkyCannon.png`,
    `Enemies/Robot/StormBaron*.png`, `Scripts/Level/MountMeasurementsW4.cs`, `tools/mounts_w4.json`. Reused (Stampede):
    SI_Parallax_Far (far), CloudTile (cloud bank wall), StonePillar (mesa mount), AnchorCloud (thundercloud),
    GiantMushroom (corner silhouettes), the Storm Baron poses. Kling: 10 images, 20 credits, in
    `FarmFury_Artwork/SkyIsland/Framed/` — the six construction pieces came from ONE sheet (`W4Pieces.png`, split by
    `split_pieces`). Misses kept but unused: `W4Skiff.png` (a blimp with no deck - World 4 has no rolling wagon; the
    runaway is a thunder wheel down the slide ramp); `W4Island.png` is a boxy rock (used as the small floating rock);
    the wide isle and the ground strip are both `W4Soil.png`. Bolt.png is drawn in code.
  - **Look:** `EnvironmentDepthSystem` theme 4 (`Sky*` fields): far painting anchored just above the ground
    (`Sky ? g + 0.6`) so its cloud sea meets the cloud bank; one hazed row of distant isles high over the horizon
    (`SkyMidHeight/Sink` 1.9 / -3.4; no ridges band — two rows read as a repeating border); weathers ClearSkies /
    Windswept / SunsetClouds / StormFront / StarlitSky in `CornfieldAmbience` (existing effects only).
  - **Pieces:** `BlockBase._spr*W4` — sky timber post/plank (WoodBlock), marble block / column / slab (StoneBlock),
    cloud bales (HaybaleBlock, also the cannon mount `CatapultLauncher._mountBaleSpriteW4`).
  - **Cannon:** `CatapultLauncher._cannonSpriteW4` = the Sky Cannon (Cannon.png's canvas, PPU 384).
  - **Mounts:** `LevelLoader._platformSpritesW4` + `MountsW4`: cloud mountains low / mid / tall (0/1/2, were the wide isle
    and floating rock until the cloud-mountain change above), stone pillar (3), sky
    dock (4), none (5, falls back to World 1's wagon - unused), slide ramp (6).
  - **Contraptions:** `GadgetType.StormCloud` (`Sunflower` storm mode: three white flashes, two volleys of lightning
    bolts at -55..10 deg, each bursting and shoving), `GadgetType.ThunderWheel` (`RollingBoulder`), the sky crane
    (`HayHoist` with a thunder wheel load).
  - **Boss:** `StormBaronRobot.prefab` (`EnsureStormBaronPrefab`, before `WireLevelLoader`) on `_worldRobots[4].boss`;
    dropped off by the mothership on L24 onto a straight keep (`wonky=False`).
  - **Follow-up (user: "a little empty... kite and other flying objects... more difficult with the cannon floating"):**
    `Core/SkyDecor.cs` (built by EnvironmentDepthSystem, World 4 only): two hot-air balloons drifting across, two kites
    swaying, three paper lanterns rising (glowing at SunsetClouds / StarlitSky), the floating plum bobbing, all screen-
    placed at orders -34/-33; the rooster weathervane on the ground right of the cannon (order 1). Stampede art copied
    by `import_sky_art.py` as `Sky/Decor_*.png`. The World 4 cannon floats: `CatapultLauncher.LateUpdate` bobs the
    cloud-bale mount, the cannon and `_cannonRestPos` (so the barrel tip, aim pivot, loaded animal and trajectory
    preview move too) 0.28 u over 3.4 s, vertical only (the recoil owns x).
  - **Gerald / Horace cannon poses (same day):** Gerald's flight art is mirrored to face right (it faced left - the
    "wrong way") and his Loaded is a Kling side crouch; Horace got a Kling Loaded (left figure of a pose sheet,
    mirrored) and a Kling launched-leap flight. Registered in `tools/import_character_art.py` (`MAGENTA`, `FLIP`,
    `LEFT_FIGURE`). Kling 4 images / 8 credits; the two 2-reference "pose template" sheets drew the template chicken
    instead of the character - single-reference prompts with the pose in words worked. Horace still has no Impact.

- **2026-11-11 — Topple, don't break; "robots don't know how to build"; menu music + UI sizing** (user: theme on
  every non-gameplay page; smaller "Character" header and buttons; AB-style precarious structures; "structures should
  not explode - they should topple"; only the dynamite explodes and even that topples; a robot crushed by a falling
  structure explodes with dust clouds + fire burst + SFX; nothing left floating; animals don't explode on impact;
  abilities not slower than the flight and angled down). Unverified in Play mode. PhysicsProbe (no input, all 62
  framed levels, `unity/Logs/probe_2026-11-11.log`): 59 stand untouched (0 awake, 0 movement); 3 failed —
  W2_L22 (Frost Tyrant landing on the now-crooked keep: keep set `wonky=False` in design_world2/3 boss levels),
  W2_L17 Whiteout and W3_L15 Thunderhead (towers on frozen-bale / flour-sack stilts fall by themselves ~0.9 s in,
  i.e. when the new block watchdog starts; its probe depth was raised 0.1 -> 0.25 u and it now logs
  `[Probe] WATCHDOG woke ...`). Re-probe (`probe_w4.log`): W2_L22 and W3_L15 now
  clean. W2_L17's collapse starts ~1.0 s in, BEFORE any watchdog wake (so not the watchdog): its 5-storey wonky
  tower on frozen-bale stilts settles by itself; that tower (and W3_L15's twin) is now `wonky=False`. W4_L14 (same
  shape on cloud bales) is clean wonky.
  - **Pacing:** Level Complete counts up (`ResultsScreen.StartReveal/TickReveal`): score 0 -> final over 1.7 s easing
    out after 0.45 s, earned stars fill one by one at each third (a ping each), coin balance ticks up by the payout over
    0.7 s, then the buttons (the SafeArea CanvasGroup) and Double Coins fade in; a tap skips. Every page (Landing, World
    / Level / Character Select, Results, Pause) fades in over 0.35 s (`UI/Kit/ScreenFadeIn.cs`).
  - **Launch screams:** `AudioManager._fallingClips` (index = AnimalType) from `Assets/Audio/Falling/<Animal>_Falling.mp3`
    (user-supplied clips, originals in `FarmFury/Launch/`), wired by `SceneSetup.WireAudioArrays`. Every animal's
    plays once from the cannon shot (no longer looped) and fades on impact. All 8 animals have one (Percy added 2026-11-11).
  - **Music:** `AudioManager.SyncMusic()` (every frame): the world track only while actually playing (Playing,
    unfrozen, no revive prompt); every other page (menus, Pause, Locker, character switch, revive, Results) plays the
    menu theme. The world track pauses behind in-level pages and resumes; the theme resumes rather than restarting.
    Change-driven (mode/track), never polls `AudioSource.isPlaying`: our clips are imported without Preload Audio
    Data, so isPlaying reads false right after Play() and the first version restarted the music every frame (silence).
  - **UI:** `UIKit.RoundButtonSize` 160 -> 136 -> 112 (every menu round button); the HUD's pause / Locker / switch
    stack is its own 96 (`HUDController.BuildPauseButton`). The "Character" lettering baked
    into the 9 `*Character_Canvas_Wide.png` canvases shrunk to 72% by `tools/shrink_character_header.py` (originals in
    `Sprites/UI/Franchise/Canvas_originals~/`, always the source; `--scale` to retune; a soft inpaint smudge where the
    old lettering was).
  - **Blocks never break** (`BlockBase.BreaksOnHit` false): a hit wakes + shoves (`ShoveOnHit`/`StackShove`, eggs too)
    and `Disturb()`s (static blocks on unframed levels turn Dynamic and cascade); `TakeDamage` on a plain block is a
    Disturb. A block scores once it has toppled (> 40 deg or dropped 0.6 u). Hay no longer explodes; Cluck's hay
    pass-through removed. `ForceDestroy` (boss keep) is the only scripted exception. Floating watchdog:
    a live block asleep with nothing under its lowest corner is woken (`HasSupportBelow`, every ~0.35 s).
  - **Dynamite** (`ExplodingBarrelBlock`, `BreaksOnHit` true): goes off on its first hit (animal / ability / egg /
    debris impulse >= 2); the blast (3 u) shoves and wakes everything outward-and-up (never destroys blocks), sets
    other dynamite off 0.15 s later, blows up robots within 1.15 u and throws the rest.
  - **Robot death:** `RobotEnemy._fireBurstSprite` (World1Props/Explosion.png) + `_dustCloudSprite`
    (`Sprites/Effects/DustCloud.png`, from FarmFury_Artwork/Rough_Effects) — a fire burst in a ring of 5 billowing dust
    clouds (`BillowFade`), Explosion_Robot.mp3; bosses also show their exploding pose. Wired by `WireRobotDeathFx`.
    Crush rule unchanged (impulse >= 2.1 kills).
  - **Animals:** no star burst, no freeze on impact — the animal stays physical, keeps tumbling and pushing, then
    fades out once settled (0.5 s) or 3 s after impact. Bessie's shockwave fires once.
  - **Abilities:** `AnimalBase.AbilityAim(minDownDeg, offset)` / `AbilitySpeed(min)`: forward-and-down (at least
    minDown below the horizon and 12 deg steeper than the flight), 1.25x the animal's speed. Eggs 50 deg, Woolly 40,
    Ducky 35, Horace 30, Gerald 60, Billy 40; Percy dives at 30 then bounces. Gerald/Billy bursts now push (5 / 8).
  - **Angled pieces:** `BlockSpawnData.rotation` (deg, CCW; `B(..., rot:)`; mirrored levels negate it).
  - **Designer (`tools/design_world1.py`):** `_rpiece`/`beam` (rotated pieces), polygon support check, collider
    overlap check, robot-in-block / robot-on-nothing checks, rotated previews. `tower()` is **wonky** by default
    (seeded per level): storeys drift onto lopsided overhanging roofs; empty storeys on one post (T), with a diagonal
    `brace`, on crate-stack legs or stilts (`stilts=False` keeps heights equal, `room=(l, r)` bounds the overhangs).
    Set pieces: `aframe` (plank tent), `lean`, `upside_pyramid`, `wobble_stack`, `stilt_house`, `cantilever`,
    `balanced_rock`, `barrel_deck`. All 17 World 1 levels rebuilt around them (ids, mounts, wagons, gadgets kept);
    Worlds 2-3 get the wonky towers (W3_L12 needed `room`); Worlds 4-9 inherit World 1's new towers via the generator.
    Pre-existing checker notes (not from this change, probe-clean before): W2_L07/L08 frost robots flagged inside
    narrow huts, W2_L10/L19 ice box vs barrel/post, W3_L04 robot by a crate.

- **2026-11-10 — One strike, softer topple; logo + landing fit** (superseded 2026-11-11: blocks no longer break at all) (user: "structures are still too hard to
  topple... a soft strike, with a slow topple that speeds up as gravity takes over... remove all second strike
  requirements"; "the logo slightly smaller"; landing "sizing is too big"). Unverified in Play mode.
  - **Blocks:** an animal hitting any block breaks it on the first hit (`BlockBase.OnCollisionEnter2D`, stone
    included; Indestructible / stay-kinematic excepted). Stone base health 70 -> 30 so debris breaks it too.
  - **Topple:** `LiveGravityScale` 0.42 -> 0.5, live angular damping 0.15 -> 0.04, `HitShove` 0.35/22 -> 0.6/36, and a
    new `StackShove`: the strike also pushes every live block resting above / beside the struck one (high on its body,
    35% of the shove, fading with height), so the stack leans as one, slowly first, then accelerating.
  - **Robots:** `EggDamageFraction` 0.4 -> 1, `FallDamageFraction` 0.6 -> 1, any crush >= `CrushImpulse` kills (no half
    tier). Bosses still scale by `_damageResistance` / `_structuralDamageResistance`.
  - **Logo:** canvases' logo 1.4 -> 1.15x the old mark (`brand_cannonfire.py LOGO_SCALE`), Leaderboard `LogoBox`
    190x134 -> 162x114.
  - **Landing:** `LandingScreen` shows the whole poster (ZoomedCover zoom 0.5 = contained) over a blurred copy
    (`MenuArt.landingPosterBlur` = `UI/LandingPage_Blur.png`, made by `brand_cannonfire.py`) filling the side gaps on
    phones wider than 16:9 — covering had cropped FARM FURY and put CANNONFIRE under the Play/Exit buttons.
  - **Probe (no input) after the change:** all 62 framed levels (Worlds 1-3) 100% intact, 0 awake; only the Frost
    Tyrant settles 0.01 u. The one-strike feel itself is untested in Play mode.
  - Also today: `NextLevel` on Results opened the next level's Character Select (reverted 2026-11-11: Next returns to
    the world's Level Select, where the newly unlocked level is tapped -> Character Select -> play); Character Select slot ability icons;
    floating-robot watchdog + stuck-cannon safety net (see the other 2026-11-10 entries / HISTORY).

- **2026-11-10 — World music + ability sounds from Stampede** (user: "review the music from stampede and arcade for the
  different worlds - apply, leave the theme as is ... SFX attached to ability"). `AudioManager._worldMusic` (index =
  LevelData.world) plays Stampede's world track in Worlds 2-9 (`Assets/Audio/Worlds/`); World 1 keeps
  SunriseMeadows_Background and the menus keep SunriseMeadows_TransitionMusic. Arcade's tracks are byte-identical
  copies of Stampede's under other names, and Stampede itself reuses three files (Watermill = Sunken City, Sky Island =
  Crop Factory, Dustbowl = its HarvestMoon). `AudioManager._abilityClips` (index = AnimalType, `Assets/Audio/Abilities/`)
  plays on the ability tap (`AnimalBase.Kind` -> `AudioManager.PlayAbility`); Woolly has no franchise ability sound.
  Wired by `SceneSetup.WireAudioArrays`. Unheard in Play mode.

- **2026-11-10 (latest) — World 3 Watermill Village built on the World 1/2 template** (user: "utilise the same framework
  ... be creative and build the third world ... specific to the world theme ... utilise artwork that is already
  available ... produce a different cannon"). Unverified in Play mode; designer-checked and probe-checked (22/22 intact).
  - **Levels:** `tools/design_world3.py` writes `Editor/LevelDataGenerator.World3.cs` (`MakeWorld3Framed`, which deletes
    older `W03_*` files; ids stay `W3_L01..L22`). `design_world1.py` knows `world=3` (`SPRITE_W3`, `KIND_W3`, `HOIST_W`,
    `GADGET_NAMES`). `WorldLevelGenerator` now skips worlds 2 and 3. Ducky leads most lineups.
  - **Art:** `python tools/import_watermill_art.py` -> `Sprites/Environment/Watermill/`, `Launchers/MillCannon.png`,
    `Enemies/Robot/MillMauler*.png`, `Scripts/Level/MountMeasurementsW3.cs`, `tools/mounts_w3.json`. Reused: Stampede's
    WV_Parallax_Far/Mid/Near (far valley, village band, bulrushes), the Mill Stone Mauler poses, FishingBoat, Millstone.
    Kling (15 images, 30 credits) in `FarmFury_Artwork/WatermillVillage/Framed/`.
  - **Look:** `EnvironmentDepthSystem` theme 3 (`Mill*` fields; `ThemeMidHeight/Sink`, `ThemeWallHeight`,
    `MillRidgesHeight/Base`); weathers SummerMorning / RiverMist / SpringShower / LanternDusk / Thunderstorm in
    `CornfieldAmbience` (dragonflies, blossom petals, a rainbow, fireflies, storm).
  - **Pieces:** `BlockBase._spr*W3` — mill timber post/plank (WoodBlock), cobble block / pillar / lintel slab (StoneBlock),
    flour sacks (HaybaleBlock, also the cannon mount `CatapultLauncher._mountBaleSpriteW3`).
  - **Cannon:** `CatapultLauncher._cannonSpriteW3` = the Mill Cannon (Kling, on Cannon.png's canvas, PPU 384).
  - **Mounts:** `LevelLoader._platformSpritesW3` + `MountsW3`: river islet, bridge pier, fishing jetty, rowing boat
    (rolling mount, slides on two keel skids), boat slipway.
  - **Contraptions:** `GadgetType.WaterPump` (`Sunflower` pump mode: gushes a flat jet of splashing droplets),
    `GadgetType.Millstone` (`RollingBoulder`), the mill hoist (`HayHoist` with the mill crane + a millstone load; the
    crane's anchor/post are per-art params now).
  - **Boss:** `MillMaulerRobot.prefab` (`EnsureMillMaulerPrefab`, before `WireLevelLoader`) on `_worldRobots[3].boss`;
    dropped off by the mothership on L22.

- **2026-11-09 (latest) — World 2 Frozen Tundra rebuilt on the World 1 template** (user: "take that template and build
  frozen tundra... creative... a new boss... fly in on the same mothership... flip the mothership"). Unverified in Play
  mode; designer-checked and probe-checked (see HISTORY).
  - **Levels:** all 22 World 2 levels are framed designs in `tools/design_world2.py` (writes
    `Editor/LevelDataGenerator.World2.cs`, `MakeWorld2Framed`, which also deletes older `W02_*` assets). `L19_FrostboundWatch`
    keeps id W2_L01; the rest keep ids W2_L02..L22 (save keys) with new names/files. `WorldLevelGenerator` now skips
    world 2 and no longer deletes `W02_*` files. `design_world1.py` is world-aware (`Level(..., world=2)`): World 2
    pieces/robots/mounts measured from the World 2 sprites; World 1 helper names map onto frozen mounts.
  - **Look:** `EnvironmentDepthSystem` themes (`Theme*` properties, rebuilt when the world changes): World 2 far =
    the aurora painting (`ParallaxFarHills_W2.png`), glacier ridges with a frozen watchtower, frozen farm hills,
    frozen field, frozen corn wall over a snow lip, navy corner silhouettes (`Sprites/Environment/Frozen/`, Kling on
    magenta, keyed by `tools/import_frozen_art.py`). Stalks barely sway (frozen). Weather (`LevelWeather`
    AuroraNight / Snowfall / Blizzard / IceFog / PolarDawn) -> `CornfieldAmbience` frozen profiles: falling snow,
    blizzard streaks, animated aurora ribbons (procedural), glints twinkling on the snow.
  - **Pieces:** World 2 blocks are frosted timber (FrostPost / FrostPlank, World2Props WoodBlock / WoodVertical) and ice
    instead of stone (Ice_block / Ice_vertical / IceSlab; ice shatters into IceSplinters via `BlockBase._sprExplodeW2`);
    snow-capped bales (`FrozenBale.png`, also the cannon mount via `CatapultLauncher._mountBaleSpriteW2`); frost
    barrels. `BlockBase`: Square/Block variants resolve to the W2 normal/horizontal art.
  - **Mounts:** `LevelLoader._platformSpritesW2` + `MountsW2` (`MountMeasurementsW2.cs`): ice floe, ice pillar, frosted
    scaffold, snow ramp, and a **sled** (rolling-wagon equivalent, slides on two runner skids).
  - **Contraptions:** `GadgetType.IceCrystal` (Sunflower in crystal mode: ice shards, shatters), frosted hoist dropping
    a block of ice (`HayHoist` with `FrozenCrane.png` + `IceBlock.png`), `GadgetType.IceBoulder` (`RollingBoulder.cs`).
  - **Boss:** the **Frost Tyrant** (`FrostTyrantRobot.prefab`, `SceneSetup.EnsureBossRobotPrefab` shared with the
    Commander; FrostTyrant / _Alert / _Hit / _Explode, Kling) on `LevelLoader._commanderPrefabW2`; dropped off by the
    mothership (`LevelReveal.BossDropIn`), which now flies nose-first (`flipX`, the art's bow points right).
  - Kling: 21 images (~46 credits). Note: `WireBlockPrefab` re-imports the World2Props sprites it touches at PPU =
    width (WoodBlock, WoodVertical, IceSplinters, Barrel_Dynamite) — framed World 2 levels hide `Scenery_World2`
    anyway, but any hand-placed scene copy of those props would change size.
  - **Stability:** World 2's pieces are cropped tight, so `BlockBase.TrimColliderToArt` took its "full rect" branch and
    skipped `TrimInset`; neighbours overlapped ~0.02 u and towers shook themselves down once the reveal woke them. The
    full-rect branch now applies the inset too. `PhysicsProbe` (no input) on all 40 World 1 + World 2 levels: 100%
    intact, 0 awake, 0 movement.

- **2026-11-09 — World 1 scenery filled, weather, contraptions, taller towers, effect sizes, 3-animal lineups**
  (user: "the scene seems bare... wind, flowers that shoot objects, rain... mountains far and close", stay farm
  themed; then "structures must be built higher, multiple stories and topple easier", "effect art... visibly over
  sized", "one can choose four cards - this should remain at 3"). Unverified in Play mode; probe-verified.
  - **Depth:** `EnvironmentDepthSystem` cornfield mode gained two tiled landform strips — `CornRidges.png` (sandstone
    bluffs + ruined tower, haze baked in, parallax 0.85, order -37 behind the haze veil) and `CornHills.png` (wooded
    hills, water tower, farmhouse, parallax 0.70, order -33); bottoms fade into the mist. Art: 9 Kling images (18
    credits) in `FarmFury_Technical/FarmFury_Artwork/MeadowRuins/Cornfield/Ambience/`, cut by
    `python tools/import_cornfield_art.py --ambience` (also clouds, sunflower, seed, crane, bucket).
  - **Weather/ambience:** `LevelData.weather` (`LevelWeather` Golden/MorningMist/Overcast/Rain/Dusk) ->
    `CornfieldAmbience` (new, built by EnvironmentDepthSystem): per-weather profile tints the far painting/landforms/
    corn, sets haze + mist colour, a full-screen grade, drifting clouds, sun glow, crows, pollen/fireflies/chaff,
    ground fog, rain streaks + lightning, and the global `_FFWind` gust value. New `Shaders/WindSway.shader`
    (`Materials/WindSway.mat`, wired by SceneSetup) bends the mid corn, corn wall and corner silhouettes (mirrored
    lookup keeps tile seams closed). Sorting: far -40, sun -39, clouds -38, ridges -37, fly-bys/crows -36 (SkyFlyby
    moved from -37), veil -35, hills -33, mist -32, mid corn -30 ... ground fog -17, specks 30, rain 38, grade 39.
  - **Contraptions:** `LevelData.gadgets` (`GadgetSpawnData`: Sunflower/HayHoist) spawned by `LevelLoader.SpawnGadget`.
    `Sunflower` (trigger on the head; an animal/projectile or a falling body fires 7 `AbilityProjectile` seeds, 4-46
    deg to the right; one shot, then it wilts). `HayHoist` (crane is scenery; bucket mass 14 on a `DistanceJoint2D`
    from the arm tip, layer Default; a shot on the rope trigger or the bucket cuts it). Designer: `L.sunflower(x, y)`,
    `L.hoist(post_x, surface, h, rope, face_left)`; it checks reachability, bucket clearance and a target below.
  - **Taller structures (design_world1.py v4):** 4-6 storeys in most levels, a robot every other floor. Live blocks
    topple easier: `LiveMaterial` friction 0.9 -> 0.6, angular damping 0.6 -> 0.15, linear 0.1 -> 0.05.
    `PhysicsProbe` normal mode: all 18 levels 100% intact, 0 awake, 0 movement. Stress (wake-everything) mode: L08,
    L10, L14, L15 lose one robot to settling contacts — fragile once disturbed, by design.
  - **Robot fall-through fix:** `RobotEnemy.MakeDynamicFromSupportLoss` no longer turns a not-yet-live robot (colliders
    still off from the reveal pop) Dynamic — the stress probe caught L17's ground robot falling through the floor.
  - **Effect sizes:** new `EffectSizing` (visible-art size from the tight sprite mesh, burst = k x sqrt(w*h), the
    effect sprite scaled by its own native size). Block death bursts (k: wood 1.5, stone 1.6, hay 2.0, barrel 2.2 —
    `_explodeSizeMultiplier` now means k), robot explosions 1.5x the robot (were ~3.3x: localScale x 0.5 dated from
    0.6-scale robots), impact stars 1.5x the animal, block hit flash = the block's own shape (was a square 1.6x its
    width), reveal dust from the visible size, cosmetic impact 2.4 -> 1.3 u.
  - **Lineup capped at 3:** `LevelData.MaxLineup`/`LineupSize` drive Character Select's slots and LevelLoader's
    queue; World 1 designs use three animals; `WorldLevelGenerator` no longer adds a fourth on boss levels.
  - **Follow-up (user play-test: "requested a revival before the final destruction... clicking yes destroyed all
    robots"; "a drone flying instead of the plane"):** `LevelLoader.DelayedLevelFailed` now waits for the wreckage
    to settle (`WreckageMoving()`: any awake block/robot/bucket/wagon moving, or an ability projectile in the air;
    0.5 s quiet, 9 s cap) before offering the revive — tall towers take seconds to fall at gravity 0.6. `SkyFlyby`
    is biplane-only (`DroneChance` 0).
  - **Follow-up 2 (user: Horace's horseshoe ability "not working"; swirl "too oversized"; structures should "topple
    slightly easier when struck, however topple slower"):** Horace's Rear Kick (pushed debris BACKWARD, toward the
    cannon — looked like nothing happened) replaced by Horseshoe Throw (`HoraceAnimal`, 2 x `AbilityProjectile`
    mass 6, 3 hits, Stampede horseshoe -> `Sprites/Abilities/Horseshoe.png`, wired by `SceneSetup.WireAbilityProjectiles`).
    Ducky's swirl 0.4->1.5 u reduced to 0.35->1.05 u, burst radius x growth now ~1.1 u (was ~2 u).
    `BlockBase.LiveGravityScale` 0.6 -> 0.42 (slower collapses; robots/bucket/wagons share it), live friction 0.55,
    new `ShoveOnHit` (animal/projectile strike adds 0.35 x its momentum, max 22, at the contact point),
    `RobotEnemy` Crush/Smash 2.5/6 -> 2.1/5, revive settle cap 12 s.
  - **In-level character switch** (user: switch characters mid-gameplay, "same [icon] used in arcade and stampede -
    place under the locker... cannot get more characters than what they have left"): HUD `SwapBtn` under the Locker
    (`_swapSprite` = `Sprites/UI/Franchise/Menu/SwapCharacter.png`, Arcade/Stampede SwapCharacterIcon) ->
    `GameFlow.OpenCharacterSwap()`: freezes time, `CharacterSelectScreen.OpenSwap` with one slot per animal still in
    the queue (pre-filled; tap a slot to empty it), Play -> `LevelLoader.ReplaceRemainingBirds` +
    `CatapultLauncher.ReloadAfterSwap`, Back/Esc resumes. Only between shots (`CatapultLauncher.CanSwapAnimals`; the
    button dims otherwise). The launcher now ignores input while `Time.timeScale == 0` (also covers the Locker).
  - **Boss drop-in** (user: "on the boss level... have the boss be dropped off, jump onto a structure from the
    mothership, make the mothership fly off again"): `LevelReveal.BossDropIn` on framed boss levels (World 1 L18; the
    generated Worlds 2-9 boss levels are unframed, so they still pop in): the biggest robot skips the pop; the
    mothership (`Sprites/Environment/BossMothership.png`, Stampede `UI/MotherShip/Ship.png`, via
    `LevelLoader.MothershipSprite`) glides in from the right, hovers, the boss leaps out and lands on the keep (squash,
    dust, shake), the ship flies off up-left. Aiming stays locked until the landing.

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
Landing title (LandingTitle.png + PRESS START; tap) -> Landing menu (LandingPage.png; Play / Exit (not iOS) / Settings cog)
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
  in order, the boss after all others. Animals (2026-11-12, Stampede/Arcade-style "clear N levels" unlocks, but per
  world): `AnimalRoster.IntroWorld` + `UnlockAfter` - two per world, the whole roster out by Sky Islands, each released
  after N levels of its world are completed: Cluck from the start / Bessie after 1 (W1), Percy 1 / Woolly 8 (W2), Ducky
  1 / Horace 8 (W3), Gerald 1 / Billy 8 (W4); Worlds 5-9 introduce nobody. Derived, nothing stored
  (`SaveManager.IsAnimalUnlocked` / `LevelsCompletedIn`); `RecordLevelCompleted` notes what a clear released
  (`TakeNewlyUnlockedAnimals`) and GameManager.CompleteLevel shows the New Character page for it. Character Select
  greys locked cards (not pickable) with a "LOCKED / Clear N levels in <World> (k/N)" label; Character Story says when
  each joins. Debug "Unlock All Levels" releases everyone. Coins, Remove Ads, worlds
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
| Ducky (duck) | Water Swirls — fires 3 spinning water swirls that grow in flight and splash (push + damage) over a widening radius | World 3 |
| Horace (horse) | Horseshoe Throw — hurls 2 heavy horseshoes ahead that ricochet through the structure (3 hits each) | World 3 (after 8 levels) |
| Gerald (turkey) | Feather Bombs — drops 3 bombs that burst in a small radius | World 4 (after 1 level) |
| Billy (goat) | Mud Throw — hurls a mud ball that splats over a wide radius | World 4 (after 8 levels) |

All eight are scripted and art-wired. Two per world since 2026-11-12, each released after a number of levels in its
world (`AnimalRoster.UnlockAfter`): all eight are out by World 4. The level lineups were written for the old one-per-world order (Horace leads World 4, Gerald
World 5, Billy World 6); they only set the slot count, since Character Select starts empty and offers whoever is unlocked.

### 9 Worlds (160 levels: 6 story + 3 paid) — per-world launchers/mechanics below are PLANNED; every world uses the Farm Cannon today
| World | Launcher | Key mechanic | Levels |
|---|---|---|---|
| 1 — Meadow Ruins | Farm Cannon (drag angle+power) | Character weight affects range | 18 |
| 2 — Frozen Tundra | Ice Cannon (angle + ricochet) | Freeze zones slow flight | 22 |
| 3 — Watermill Village | Water Wheel (timing — tap to fire); Mill Cannon look built | Fire spread on wood | 22 |
| 4 — Sky Islands | Airdrop Biplane (timing — tap to drop) | Updraft column steering | 24 |
| 5 — Sunken City | Torpedo Tube (angle + bubble pop) | Current lanes + lever switches | 22 |
| 6 — Robot Mothership | Gravity Sling (angle + gravity wells) | Zero-G ability modifiers | 16 |
| 7 — Dustbowl Canyon (paid) | Farm Cannon | — | 12 |
| 8 — Harvest Fairground (paid) | Farm Cannon | — | 12 |
| 9 — Crop Factory (paid) | Farm Cannon | — | 12 |

World 1 (L01-L18, `tools/design_world1.py`), World 2 (W2_L01-L22, `tools/design_world2.py`, 2026-11-09), World 3 (W3_L01-L22, `tools/design_world3.py`, 2026-11-10), World 4 (W4_L01-L24, `tools/design_world4.py`, 2026-11-11) World 5 (W5_L01-L22, `tools/design_world5.py`, 2026-11-12), World 6 (W6_L01-L16, `tools/design_world6.py`, 2026-11-12) and the paid Worlds 7-9 (W<N>_L01-L12, `tools/design_world7.py` / `8` / `9` on `tools/design_paid.py`, 2026-11-12) are designed framed levels, all two rooms per level. None of it verified in Play mode.

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
                              — Bounce Roll / Wool Balls / Water Swirls / Horseshoe Throw / Feather Bombs / Mud Throw (the five thrown abilities use AbilityProjectile; Ducky's swirls use its growTo/growTime + burstPush, 2026-11-09)
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
- **Scenery sortingOrder rule:** decorative props (SceneryBuilder) must use `sortingOrder ≤ 1`. Blocks=2, robots=3, FarmCannon=4, CannonSmoke=5, animals=6. (Contraptions: hoist crane 1, sunflower/bucket/rope 2.) Any tie between two of these falls back to camera Z-distance sorting, which has caused multiple real invisible-object bugs in this project (trebuchet-vs-ground, animals-vs-cannon) — always assign a distinct explicit order for anything new.

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
