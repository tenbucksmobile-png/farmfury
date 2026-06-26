# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

---

## Project Overview

**Farm Fury** is an Angry Birds-style physics destruction game. Farm animals launch from a trebuchet to wreck robot fortresses and reclaim the farm.

The repo contains two parallel codebases:
- `index.html` — **Phaser 3.60 prototype** (complete through Prompt 12, production-validated)
- `unity/` — **Unity 6.5 URP 2D port** (active development target for iOS/Android release)

All AI-pipeline work (Claude Code + Higgsfield + Midjourney + Suno) targets the Unity port.

---

## GDD — Game Design Document

Full GDD: `C:\Users\Personel\Desktop\FarmFury_GDD_v2.docx`

### 8 Animals (tap mid-flight to trigger ability)
| Animal | Ability | World introduced |
|---|---|---|
| Cluck (chicken) | Cluster Bomb — 5 eggs scatter on tap | World 1 |
| Bessie (cow) | Ground Slam — radial shockwave on landing | World 1 |
| Percy (pig) | Bounce Roll — curls, bounces 3× gaining speed | World 2 |
| Woolly (sheep) | Triple Clone — splits into 3 at 15° spread | World 2 |
| Ducky (duck) | Skip Shot — skips flat surfaces 3× | World 3 |
| Horace (horse) | Rear Kick — kicks debris backward on impact | World 4 |
| Gerald (turkey) | Puff Up — inflates 3× on tap, wrecking ball | World 5 |
| Billy (goat) | Headbutt Through — penetrates first obstacle | World 6 |

### 6 Worlds, 6 Launchers (126 total levels)
| World | Launcher | Key mechanic | Levels |
|---|---|---|---|
| 1 — Meadow Ruins | Barn Trebuchet (drag angle+power) | Character weight affects range | 18 |
| 2 — Frozen Tundra | Ice Cannon (angle + ricochet) | Freeze zones slow flight | 22 |
| 3 — Watermill Village | Water Wheel (timing — tap to fire) | Fire spread on wood | 22 |
| 4 — Sky Islands | Airdrop Biplane (timing — tap to drop) | Updraft column steering | 24 |
| 5 — Sunken City | Torpedo Tube (angle + bubble pop) | Current lanes + lever switches | 22 |
| 6 — Robot Mothership | Gravity Sling (angle + gravity wells) | Zero-G ability modifiers | 16 |

### Star System
- 1★ — all robots dead (progress gate)
- 2★ — all dead + 1 animal remaining (mastery)
- 3★ — all dead + 2+ animals remaining (prestige)

---

## Development Roadmap (6 Phases)

### Phase 1 — Core Feel *(current)*
1.1 **Slingshot fix** — drag the BIRD backward (click bird → pull back → release). ✅ DONE — arm tracks drag, rubber-band line, ArmSnap coroutine.
1.2 **Trajectory arc** — dotted style, visible only while dragging, fades at midpoint on harder levels. ✅ DONE — 20 pooled SpriteRenderer dots, physics-accurate (gravity + linearDrag), alpha fade over last 20% of visible window; Level 0 shows full arc, Level 17 fades at midpoint.
1.3 **Destruction feedback** — block crack states (3 stages), block burst on death, robot flash+explode, screen shake. ✅ DONE — procedural Bresenham crack overlays (2 child SpriteRenderers, shared static sprites), procedural fragment burst (Rigidbody2D, no collider), robot white-flash + radial particle explosion, CameraShake singleton (auto-attached to Launcher GO).
1.4 **Audio** — procedural sounds: launch, wood impact, stone impact, robot death, win fanfare, fail buzzer. ✅ DONE — AudioManager singleton (auto-attached to Launcher GO); 6 clips built at runtime from DSP primitives (Sine, Noise, Sweep, ADSR); Win/Fail triggered via GameManager.OnStateChanged; hit sounds throttled at 80ms cooldown per block type.
1.5 **Camera** — smooth follow → smooth pan-back to launcher after landing. ✅ DONE — follow triggers on landing (IsInFlight=false, not fire-time); exponential-decay follow (frame-rate independent, 6 units/s); lands pause 0.8s then SmoothStep pan over 1.2s; _returnPending guards against duplicate coroutines. NOTE: new [SerializeField] camera fields won't take effect until Wire Scene References is re-run or values updated in Inspector.

### Phase 2 — UI/UX Shell
2.1 **HUD** — score (top-centre), bird queue (bottom-left, animated), pause button (top-right). ✅ DONE — HUDController singleton (auto-Canvas, ScreenSpaceOverlay, ScaleWithScreenSize 1920×1080); score via ScoreManager.OnScoreChanged; bird icons rebuilt via LevelLoader.OnBirdConsumed (+ catch-up if level already Playing in Start); staggered sine-wave bob animation using Time.unscaledTime; pause button toggles Time.timeScale + glyph ❚❚/▶; EventSystem created if missing (ENABLE_INPUT_SYSTEM aware). LevelLoader gained OnBirdConsumed event + BirdQueueSnapshot property. Wire Scene References adds HUD GO.
2.2 **Level Complete panel** — animated star pop-in (1→2→3 with bounce), score, next/replay. ✅ DONE — panel built inside HUDController (full-screen dark overlay + cream card, sortingOrder=100 same canvas); 3 TMP ★ slots grey on open, earned stars bounce 1→1.42→1 + grey→gold over 0.38s via `PopStar()` coroutine; staggered 0.30s/0.75s/1.20s via `AnimateStars()`; score + "BEST X" / "★ NEW BEST!" populated from ScoreManager at LevelComplete state; REPLAY → `RestartLevel()`, NEXT ▶ → `LoadNextLevel()`; panel hides on any non-LevelComplete state. Added helpers: `MakeCentredText`, `MakePanelButton`, `MakeFullScreenRect`. `using System.Collections` added.
2.3 **Level Failed panel** — try again / menu. ✅ DONE — panel built inside HUDController (480×300 card, same canvas/sortingOrder); "LEVEL FAILED!" title in deep red, score value, TRY AGAIN → `RestartLevel()`, MENU → `LoadMenu()`; `OnStateChanged` now uses a switch that shows exactly one result panel at a time and hides the other; no animation (instant show/hide).
2.4 **Pause menu** — resume/restart/menu, music+SFX toggles. ✅ DONE — pause button now shows a 380×340 card overlay (60% dark) instead of just toggling timeScale; RESUME/RESTART/MENU buttons + ♪ MUSIC and ◎ SFX toggle buttons with dark-on / grey-off colour states; `SetPaused(false)` always hides the panel; toggle state persisted via `PlayerPrefs` (`ff_sfx_enabled`, `ff_music_enabled`). `AudioManager` gained `SfxEnabled`/`MusicEnabled` static properties (loaded from PlayerPrefs in Awake) + `SetSfxEnabled()`/`SetMusicEnabled()` methods; `Play()` early-outs if `!SfxEnabled`.
2.5 **Level select** — scrollable grid, star counts, locked/unlocked states. ✅ DONE — `LevelSelectController` singleton; builds its own Canvas (sortingOrder 300, ScaleWithScreenSize 1920×1080); full-screen dark background + "SELECT LEVEL" title; `ScrollRect` + `GridLayoutGroup` (3 columns, 260×200 cards, 28px gap/padding) + `ContentSizeFitter` (grows content height for 18+ levels); `RefreshGrid()` destroys and rebuilds cards each show; unlocked cards are dark-navy Button (onClick → `ForceStartLevel`), locked cards are grey (no Button); stars shown as TMP rich-text "★★★" (gold/grey per count); level N locked if level N-1 has 0 stars. `GameManager.LoadMenu()` now guards with `SceneInBuild()` — stays in Game.unity when MainMenu isn't in build settings, letting `LevelSelectController` handle the Idle state. Wire Scene References now creates a "LevelSelect" GO.
2.6 **Main menu** — landing page art + PLAY button. ✅ DONE (updated) — `MainMenuController` singleton; Canvas sortingOrder 400; full-screen `LandingPage.png` splash image (all 8 animals, burning barns, "FARM FURY" logo baked into art) via `[SerializeField] _landingSprite`; dark vignette band at bottom; orange "▶ PLAY" button (360×100); version label bottom-right. Procedural animated background removed. Shows on `GameState.Idle`. Wire Scene References wires `_landingSprite` from `Assets/Sprites/UI/LandingPage.png`.

### Phase 3 — Character Roster ✅ DONE
All 8 animals scripted, all Kling AI art generated, backgrounds batch-removed via `tools/remove_backgrounds.py`, sprites imported to `unity/Assets/Sprites/Characters/<Name>/`, pose sprites wired into all 8 animal prefabs via **FarmFury → Wire Sprites**.

`AnimalBase` now holds 5 pose sprite fields (`_sprIdle`, `_sprLoaded`, `_sprInFlight`, `_sprImpact`, `_sprAbility`) and swaps them at key moments (sling load, launch, collision, ability trigger). All 8 subclasses guard procedural tint colors with `if (!HasRealSprites)` so tints only apply when no real sprite is wired.

#### Art Pipeline (Kling AI → Unity)

Raw sprites live in `assets/<Name>_<Animal>/` — one PNG per pose, white background, generated via Kling AI.

**Status (2026-06-25):**
| Character | Art done | Sprites imported | Script done |
|---|---|---|---|
| Cluck (chicken) | ✅ | ✅ | ✅ |
| Bessie (cow) | ✅ | ✅ | ✅ |
| Percy (pig) | ✅ | ✅ | ✅ |
| Woolly (sheep) | ✅ | ✅ | ✅ |
| Ducky (duck) | ✅ | ✅ | ✅ |
| Horace (horse) | ✅ | ✅ | ✅ |
| Gerald (turkey) | ✅ | ✅ | ✅ |
| Billy (goat) | ✅ | ✅ | ✅ |

**Pose set per character** (maps to game states):
| File | When used |
|---|---|
| `Idle.png` / `Idle2.png` | Waiting in bird queue |
| `Loaded.png` | Sitting in slingshot cup |
| `Launch.png` / `Launch2.png` | Release frame (brief) |
| `InFlight.png` | Main flight sprite |
| `Impact.png` | On collision |
| `Trigger.png` / `AbilityTrigger.png` | Ability activation |
| `Celebrate.png` / `Celebration.png` | Level win |
| `Defeat.png` | Level fail |

**Sprite sizing spec for Kling AI generation:**
- **Canvas**: 1024 × 1024 px, square (1:1)
- **Character fill**: 75–80% of canvas height — character centred, black outline consistent
- **Background**: white (batch-removed via Python/Pillow script before Unity import)
- **All 8 characters at same spec** for consistency

Physics radii driving the sizing (set in code, do NOT change):
- Small animals (Cluck, Percy + most others): `_col.radius = 0.36f` → 0.72u hitbox diameter
- Bessie: `_col.radius = 0.52f` → 1.04u hitbox diameter

Unity import: PPU per character set so sprite visual ≈ hitbox diameter (handled when sprites are wired in).

**Batch background-removal plan** (run once all 8 characters are done):
```python
# Python + Pillow — strips white/near-white backgrounds, saves transparent PNGs
# Script will live at tools/remove_backgrounds.py
```
Output goes to `unity/Assets/Sprites/Characters/<Name>/`.

#### Environment Art Pipeline (Kling AI → Unity)

All environment assets generated via Kling AI and stored in `assets/`. Same white-background → batch-removal pipeline as characters.

**Backdrops** (`assets/Backdrops/` — 26 files) — sky paintings + launchers + World 1 props:

| Asset | File | Status |
|---|---|---|
| **Sky — World 1 Meadow** | `SkyPainting.png` | ✅ |
| **Sky — World 2 Frozen Tundra** | `FrozenTundra.png` | ✅ |
| **Sky — World 3 Watermill Village** | `Watermill Village.png` | ✅ |
| **Sky — World 4 Sky Islands** | `SkyIslands.png` | ✅ |
| **Sky — World 5 Sunken City** | `SunkenCity.png` | ✅ |
| **Sky — World 6 Robot Mothership** | `RobotMothership.png` | ✅ |
| **Launcher — World 1 Barn Trebuchet (body)** | `Trabuchet_Body.png` | ✅ |
| **Launcher — World 1 Barn Trebuchet (arm)** | `Trabuchet_Arm.png` | ✅ |
| **Main Menu splash** | `LandingPage.png` → `unity/Assets/Sprites/UI/` | ✅ |
| ~~Trabuchet.png~~ (legacy single sprite, superseded) | `Trabuchet.png` | — |
| **Launcher — World 2 Ice Cannon** | `Ice Cannon.png` | ✅ |
| **Launcher — World 3 Water Wheel** | `WaterWheel.png` | ✅ |
| **Launcher — World 4 Airdrop Biplane** | `Plane.png` | ✅ |
| **Launcher — World 5 Torpedo Tube** | `Submarine.png` | ✅ |
| **Launcher — World 6 Gravity Sling** | `GravitySling.png` | ✅ |
| World 1 props (14) | FarmSilo, StoneTower, OakTree, GnarledTree, StoneArch, RuinedStoneWall, StoneWall(Tall), Haybail, WoodenCart, Rock, Grass Tuft, WildFlowers, Wooden Barrel, WoodenFence | ✅ |

**Robot Enemy** (`assets/RobotEnemy/` — 12 files):
- Grunt: Idle, Alert, Hit, Explode, Defeated, ReferenceSheet
- Commander: Commander, Commander_Alert, Commander_Defeated, Commander_Explode, Commander_Hit

**World Props** (`assets/WorldProps/<World>/`):
| World | Folder | Files | Status |
|---|---|---|---|
| 2 — Frozen Tundra | `IceTundra/` | 12 | ✅ |
| 3 — Watermill Village | `WatermillVillage/` | 12 | ✅ |
| 4 — Sky Islands | `SkyIslands/` | 12 | ✅ |
| 5 — Sunken City | `SunkenCity/` | 13 | ✅ |
| 6 — Robot Mothership | `RobotMothership/` | 11 | ✅ |

**Sky spec:** 1920×1080 px, no alpha (full painted scene, no white bg).
**Prop/launcher spec:** 1024×1024 px, white background, element fills 75% of canvas.
**Robot spec:** 1024×1024 px, white background, pose-per-file.

Unity import target: `unity/Assets/Sprites/` — mirroring the `assets/` folder structure.

### Phase 4 — World 1 Completion *(current)*
Visual groundwork done this phase:
- ✅ **Sky backdrop** — `BackgroundController.cs` (sortingOrder −100, camera-follow, cover-scale); `SkyPainting.png` wired via SceneSetup
- ✅ **Ground art** — brown earth fill + bright grass top strip, both procedural GOs in `SceneSetup.EnsureGround()`
- ✅ **Main menu art** — `LandingPage.png` replaces procedural animated background in `MainMenuController`
- ✅ **Two-part trebuchet** — `Trabuchet_Body.png` (static frame) + `Trabuchet_Arm.png` (rotating arm, pivot at fulcrum x=0.55); arm rotates `z = angleDeg − 190°` to match physics in `DrawArmAt()`
- ✅ **Sprite PPU calibration** — Kling AI sprites are 2720×1536 (trimmed to ~1944×1481 by Unity). PPU formula: `trimmed_height / (collider_radius × 2)`. Cluck: 2057, Bessie: 1424, Percy/Woolly/Billy: 2057, Ducky: 2468, Horace: 1851, Gerald: 1949. `EditorAutoSetup` auto-applies on compile via sentinel check on Cluck/Loaded.png.
- ✅ **Camera zoom** — `orthographicSize = 3.5` (was 5); camera rest offset `(2.8, 2.5)` relative to launcher; structures at x=15–17.5 now clearly visible right-of-center.
- ✅ **Trebuchet arm alignment** — `_pivotHeight = 2.53` (was 2.35); arm GO now sits at 95% of body height, connecting at the metal cap of the body sprite.
- ✅ **L01 structure redesign** — two towers (0.6u-tall blocks) with 2 robots: tower 1 at x=15.5 (3 wood + 1 stone cap, robot on top at y=2.8), tower 2 at x=17.5 (stone + wood, robot at y=1.6).
- ✅ **Animal cards art** — 8 Kling AI card portraits in `assets/FarmCards/` (barnwood frame, 1024×1024). Files: `Cluck_Chicken.png`, `Bessie_Cow.png`, `Percy_Pig.png`, `Woolly_Sheep.png`, `Ducky_Duck.png`, `Horace_Horse.png`, `Gerald_Turkey.png`, `Billy_Goat.png`.
- ✅ **Card HUD system** — `HUDController` now builds card widgets instead of flat circles. Active card: 108×142px (full brightness, larger bob), queue cards: 82×108px (dimmed). Each card has an orange damage badge (top-right, `⚡N`). `[SerializeField] Sprite[] _cardSprites` indexed by `AnimalType`. `EditorAutoSetup.AutoCopyCardSprites()` auto-copies `assets/FarmCards/*.png` → `Assets/Sprites/UI/Cards/` on compile. `SceneSetup.EnsureHUD()` wires sprites by keyword per animal. `SpriteAutoImporter` handles Cards/ at PPU=100 Single mode.

- ✅ **Level select redesign** — `LevelSelectController` rewritten: world-art thumbnails (`_worldCardSprites[0..5]`), bottom dark gradient overlay for legibility, W# world label (top-left), large level number, gold/grey ★★★ stars, lock veil for locked cards. Falls back to 6 world-tinted solid colours if no art. `assets/LevelCards/` → `Assets/Sprites/UI/LevelCards/` auto-copy on compile; `SceneSetup.EnsureLevelSelect()` wires sprites by keyword (Meadow/Frozen/Watermill/Sky/Sunken/Mothership). Kling AI level card prompts: 6 landscape-orientation (800×600) painted scenes, one per world, full-bleed, no text.

Still to do: generate 6 world level card images via Kling AI and drop into `assets/LevelCards/` → all 18 Meadow Ruins levels (6 exist, 12 remaining) + World 1 props in scene + Robot Commander boss. Robot art sprites (`assets/RobotEnemy/`) not yet imported.

### Phase 5 — Worlds 2–6
Each world: new launcher, world physics modifier, new animals, all levels, environment art, music, boss

### Phase 6 — Polish & Release
Animations, particle systems, music, monetisation (ethical-first, no pay-to-win), achievements, iOS/Android

---

## Phaser Prototype (`index.html`)

### Running
Open `index.html` in a browser, or serve with:
```
npx serve .
# or
python -m http.server 8080
```

### Stack
- Phaser 3.60 (CDN), Matter.js (bundled), WebAudio API (procedural sound), localStorage

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

---

## Unity Port (`unity/`)

### Running
Open `unity/` in Unity Hub (Unity 6.5 / 6000.5.0f1). Open `Assets/Scenes/Game.unity`. Press Play — the ground, camera, and LevelLoader reference are all self-wired at runtime. Run **FarmFury → Wire Scene References** only when adding new levels, prefabs, or after a clean checkout.

### Stack
- Unity 6.5, URP 2D, Physics2D, New Input System, TextMeshPro

### Coordinate Conversion
- 1 Unity unit = 50 Phaser pixels
- `x_unity = x_phaser / 50`
- `y_unity = -(y_phaser - 770) / 50`
- Ground surface at Y = 0 (Unity world). Ground GO centre at (14, −0.5). Trebuchet base at (11.2, 0, 0). Camera at (13, 2.5, −10), orthoSize = 5 (10u tall). _cameraRestOffset = (1.8, 2.5) relative to launcher.

### Physics Settings
- Gravity Y: −20. Layers: Ground=6, Animal=7, Block=8, Robot=9, Egg=10

### Script Architecture
```
unity/Assets/Scripts/
  Core/
    GameManager.cs      — singleton (DontDestroyOnLoad); states: Idle/Playing/LevelComplete/LevelFailed
                          ForceStartLevel(int) boots a level without LoadScene (used for direct Editor play)
    BackgroundController.cs — sky painting backdrop; sortingOrder=−100; ScaleToFillCamera() cover-scale in Start();
                          LateUpdate() follows camera so backdrop appears infinite during flight
  Level/
    LevelData.cs        — ScriptableObject; birds[], blocks[], robots[] arrays; par bird count
    LevelLoader.cs      — instantiates prefabs; owns bird queue (_birdQueue); TryConsumeBird / PeekNextBird
                          notifies GameManager via DelayedLevelComplete / DelayedLevelFailed coroutines
  Animals/
    AnimalBase.cs       — abstract; Kinematic until Launch(); tap ability via Mouse.current (New Input System)
                          5 pose sprite fields (_sprIdle/Loaded/InFlight/Impact/Ability); HasRealSprites property
                          SetLoadedPose() called by CatapultLauncher; sprite swaps at Launch/collision/ability
                          DestroyAnimal() fires OnAnimalDestroyed event after _contactTimeout seconds
    CluckAnimal.cs      — 5-egg cluster bomb in 120° spread; eggs spawned from _eggPrefab (wired by SceneSetup)
    BessieAnimal.cs     — vy-18 slam on tap; shockwave 3.6u radius on Ground-tagged landing
    PercyAnimal.cs      — Bounce Roll: curls on tap, bounces 3× gaining speed (PhysicsMaterial bounciness boost)
    WoollyAnimal.cs     — Triple Clone: splits into 3 clones at ±15° on tap (Instantiate + SetAsClone)
    DuckyAnimal.cs      — Skip Shot: flattens trajectory on tap, skips off Ground up to 3×
    HoraceAnimal.cs     — Rear Kick: arms on tap; on first structure contact blasts nearby objects backward
    GeraldAnimal.cs     — Puff Up: inflates 3× on tap (localScale × 3, mass × 4, forward impulse)
    BillyAnimal.cs      — Headbutt Through: isTrigger window on tap, deals _penetrateDamage via OnTriggerEnter2D
    EggProjectile.cs    — layer 10; flat _damage=15 on first contact only
  Blocks/
    BlockBase.cs        — abstract; spawns Static; wakes ALL blocks on first TakeDamage(); health = baseMaxHealth × area/stdArea
                          _baseColor captured in Initialise() (after subclass sets material colour in Awake)
                          OnHealthChanged: full health → _baseColor, 67% → orange, 33% → red-orange, 0% → red
    WoodBlock.cs        — baseMaxHealth=20, baseMass=5, bounciness=0.2; starts brown (0.65,0.38,0.12)
    StoneBlock.cs       — baseMaxHealth=50, baseMass=8, bounciness=0.1; starts grey (0.55,0.55,0.58)
  Enemies/
    RobotEnemy.cs       — HP=35, impulse damage × 2.5; calls LevelLoader.NotifyRobotDestroyed on death
                          Steel blue-grey body (0.38,0.44,0.54), scale (0.7,0.8); 2 red eye child GOs added in Awake
  Scoring/
    ScoreManager.cs     — singleton; Robot +1000, Wood +100, Stone +200, Egg +50, bird-left bonus +500
                          PlayerPrefs keys: ff_score_N, ff_stars_N
  Launcher/
    CatapultLauncher.cs — drag-to-aim slingshot; trajectory preview (LineRenderer); ArmSnap coroutine; camera follow
                          Two-part trebuchet: _trebuchetBodySprite (static frame GO at localPos 0,1.18) +
                          _trebuchetArmSprite (rotating GO at localPos 0,_pivotHeight; pivot set to fulcrum x=0.55
                          in TextureImporter). DrawArmAt() rotates arm sprite z = angleDeg − 190°; hides _armLine
                          when arm sprite is present. Physics: _pivotHeight=2.3, _armLongLength=1.15, _armShortLength=0.95

unity/Assets/Scripts/UI/
  HUDController.cs         — HUD singleton; Canvas built at runtime; score text, bird-queue icons, pause btn;
                             Level Complete panel (animated stars, REPLAY/NEXT); Level Failed panel (TRY AGAIN/MENU);
                             Pause menu (RESUME/RESTART/MENU + ♪ MUSIC / ◎ SFX toggles persisted via PlayerPrefs)
  LevelSelectController.cs — Level select singleton; Canvas sortingOrder 300; ScrollRect + GridLayoutGroup 3-col grid;
                             activates on GameState.Idle; ForceStartLevel on card click; RefreshGrid() rebuilds on show;
                             "← BACK" button → MainMenuController.Instance.Show()
  MainMenuController.cs    — Main menu singleton; Canvas sortingOrder 400; full-screen LandingPage.png image +
                             dark bottom vignette + orange "▶ PLAY" button; [SerializeField] _landingSprite wired
                             by SceneSetup; shows on startup (State==Idle); IsVisible guards DelayedAutoStart()

unity/Assets/Editor/
  SceneSetup.cs         — FarmFury > Wire Scene References
                          Wires: GameManager._levels, LevelLoader prefabs+parents, CatapultLauncher,
                                 Camera (pos+orthoSize), _cameraRestOffset, Ground, Egg prefab into CluckAnimal,
                                 BackgroundController._skySprite, MainMenuController._landingSprite,
                                 CatapultLauncher._trebuchetBodySprite + _trebuchetArmSprite (PPU=384, arm pivot=(0.55,0.5)).
                          Sets camera to (13,2.5,-10) orthoSize=5. Updates _pivotHeight/armLongLength/armShortLength.
                          NOTE: TextureImporter.spriteAlignment was removed in Unity 6 — use TextureImporterSettings
                          (ReadTextureSettings / SetTextureSettings) to set custom sprite pivots.
  LevelDataGenerator.cs — FarmFury > Generate All Level Data
                          Creates/overwrites LevelData assets in Assets/ScriptableObjects/Levels/ for all 6
                          shipped levels. Level filenames must be alphabetical (L01 < L02 ...) — GameManager
                          loads them in that order.
  SpriteWiring.cs       — FarmFury > Wire Sprites
                          Sets PPU on all character PNGs in Assets/Sprites/Characters/<Name>/ (per-character PPU
                          so visual diameter ≈ physics collider). Wires _sprIdle/Loaded/InFlight/Impact/Ability
                          into each animal prefab via case-insensitive filename matching.
                          PPU map: Cluck/Percy/Woolly/Billy=1067, Bessie=740, Ducky=1280, Horace=960, Gerald=1010
  BuildScript.cs        — Batch-mode entry points: GenerateLevels, WireScene, BuildWindows, BuildWebGL, BuildAndroid
```

### Editor Menu Items
| Menu | When to run |
|---|---|
| **FarmFury → Wire Scene References** | After adding a new prefab, level, or after clean checkout. Wires all Inspector refs in Game.unity. Also sets camera position/orthoSize and _cameraRestOffset. |
| **FarmFury → Generate All Level Data** | To recreate the 6 World 1 LevelData assets (overwrites existing). Run Wire Scene References after. |
| **FarmFury → Wire Sprites** | After adding new character art to Assets/Sprites/Characters/. Sets PPU and wires pose sprites into all 8 animal prefabs. |

### Batch Build Commands (CI / command line)
```bash
# Compile check
Unity.exe -batchmode -projectPath unity/ -executeMethod BuildScript.CompileCheck -quit

# Generate levels + wire scene
Unity.exe -batchmode -projectPath unity/ -executeMethod BuildScript.GenerateLevels -quit
Unity.exe -batchmode -projectPath unity/ -executeMethod BuildScript.WireScene -quit

# Build targets
Unity.exe -batchmode -projectPath unity/ -executeMethod BuildScript.BuildWindows -quit
Unity.exe -batchmode -projectPath unity/ -executeMethod BuildScript.BuildWebGL   -quit
Unity.exe -batchmode -projectPath unity/ -executeMethod BuildScript.BuildAndroid -quit
```
Builds output to `unity/Builds/`. Scene order: Bootstrap → MainMenu → Game.

### Key Implementation Rules (Unity)
- **Input System ONLY:** `using UnityEngine.InputSystem;` — `Mouse.current.leftButton.wasPressedThisFrame`, `.isPressed`, `.wasReleasedThisFrame`. `mouse.position.ReadValue()` → `Vector2`. `UnityEngine.Input` is incompatible with this project's Player Settings.
- Blocks spawn `RigidbodyType2D.Static`; first `TakeDamage()` calls `WakeAllStaticBlocks()` which uses `FindObjectsByType<BlockBase>()` (Unity 6 API)
- Animals start Kinematic → Dynamic on `Launch(velocity)`
- Never destroy physics body in collision callback — defer with coroutine
- **SpriteRenderer is auto-added at runtime:** both `BlockBase.Awake()` and `AnimalBase.Awake()` do `if (_sr == null) _sr = gameObject.AddComponent<SpriteRenderer>();` — prefabs do not need a SpriteRenderer pre-added.
- **Ground is created at runtime:** `CatapultLauncher.Start()` calls `EnsureGroundExists()` which validates (surface near Y=0, width>5) and recreates the ground if the scene version is stale/buggy. Always runs before `ForceStartLevel`.
- **LevelLoader is auto-found:** `CatapultLauncher.Awake()` does `if (_levelLoader == null) _levelLoader = FindAnyObjectByType<LevelLoader>();` — Inspector wiring via Wire Scene References is optional.
- **[SerializeField] stale value trap:** changing a `[SerializeField]` default in code does NOT affect already-serialised components. Use `private const` for values that must not be overridden (e.g. `BirdClickRadius`).
- Ground collider maths: `localScale = (60,1,1)`, `BoxCollider2D.size = (1,1)` → world collider = 60×1. Never set both scale AND size to large values (60×60 = 3600 wide).
- **Effective mass formula:** both dynamic → `(mA × mB) / (mA + mB)`; one static → `movingBody.mass × 0.6`. Impulse threshold for damage: `> 2`. Damage = `impulse × 2.5`.

### Physics Values (matching prototype)
| Entity | mass | bounciness | linearDrag |
|---|---|---|---|
| CluckAnimal | 8 | 0.4 | 0.008 |
| BessieAnimal | 28 | 0.15 | 0.016 |
| WoodBlock | 5×area/std | 0.2 | — |
| StoneBlock | 8×area/std | 0.1 | — |
| RobotEnemy | 20 | 0.15 | — |

### Scene Structure (Game.unity)
```
Main Camera
Global Light 2D
GameManager       (GameManager.cs, DontDestroyOnLoad)
LevelLoader       (LevelLoader.cs)
ScoreManager      (ScoreManager.cs)
Launcher          (CatapultLauncher.cs, at world pos 11.2, 0, 0)
BlockParent       (empty holder)
RobotParent       (empty holder)
Ground            (Ground prefab, tag="Ground", layer=6)
```

### Prefabs
```
Prefabs/Animals/    CluckAnimal, BessieAnimal, PercyAnimal, WoollyAnimal,
                    DuckyAnimal, HoraceAnimal, GeraldAnimal, BillyAnimal, Egg
                    (all 8 have pose sprites wired; procedural circle fallback if sprites null)
Prefabs/Blocks/     WoodBlock (brown), StoneBlock (grey)
Prefabs/Enemies/    Robot (steel blue-grey, 0.7×0.8 scale, red eye child GOs)
Prefabs/Environment/ Ground (green, static)
```

### Adding a New Animal
1. Create class extending `AnimalBase`; override `Awake()` (set colour/radius/mass before `base.Awake()`), implement `TriggerAbility()`
2. Add prefab to `Prefabs/Animals/`
3. Add `AnimalType` enum value to `LevelData.cs`
4. Handle in `LevelLoader.CreateNextAnimal()` switch expression, `CatapultLauncher.NextBirdFA()` drag constant
5. Run **FarmFury → Wire Scene References**

### Adding a New Level
Add `LevelData` ScriptableObject in `Assets/ScriptableObjects/Levels/` with filename `LXX_<Name>.asset` (alphabetical order = load order). Run **Wire Scene References** to add to GameManager's `_levels` array. Or add a `Make(...)` call to `LevelDataGenerator` and run **FarmFury → Generate All Level Data**.

```
Y convention: Ground = 0 in Unity. Robot center (h=0.8, scale 0.7×0.8) → y=0.4.
              Wood center (h=0.4) → y=0.2. Stack upward by block height.
X convention: Structure zone in Unity coords ≈ 12–18 (600–900 px equivalent).
```
