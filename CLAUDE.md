# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

---

## Project Overview

**Farm Fury** is an Angry Birds-style physics destruction game. Farm animals launch from a trebuchet to wreck robot fortresses and reclaim the farm.

The repo contains two parallel codebases:
- `index.html` — **Phaser 3.60 prototype** (complete through Prompt 12, production-validated)
- `unity/` — **Unity 6.5 URP 2D port** (active development target for iOS/Android release)

`unity/CLAUDE.md` is stale — the root CLAUDE.md is authoritative.

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

## Commands

### Unity (Editor)
Open `unity/` in Unity Hub (Unity 6.5 / 6000.5.0f1). Open `Assets/Scenes/Game.unity`. Press Play — the ground, camera, and LevelLoader reference are all self-wired at runtime.

### Run-Unity.ps1 (batch automation)
`Run-Unity.ps1` is the primary interface for all batch Unity operations. Run from the repo root in PowerShell:

```powershell
.\Run-Unity.ps1 levels        # Generate/overwrite all 6 LevelData ScriptableObjects
.\Run-Unity.ps1 setup         # Wire all Inspector references in Game.unity (= Wire Scene References)
.\Run-Unity.ps1 check         # Compile check — exits 1 on error
.\Run-Unity.ps1 build         # Windows 64-bit -> unity/Builds/Windows/
.\Run-Unity.ps1 build-webgl   # WebGL build  -> unity/Builds/WebGL/
.\Run-Unity.ps1 build-android # Android APK  -> unity/Builds/Android/
```

Logs written to `unity/Logs/batch_<command>.log`. Filtered output is printed to console.

### Art pipeline
After adding new Kling AI PNGs to `assets/`:
```bash
pip install Pillow
python tools/remove_backgrounds.py   # strips white bg, writes to unity/Assets/Sprites/
```
Then open Unity — `EditorAutoSetup` and `SpriteAutoImporter` handle re-import automatically on compile. If sprites look stale, run **FarmFury → Reimport Sprites**.

---

## Auto-Compile Pipeline (non-obvious)

`EditorAutoSetup` (`unity/Assets/Editor/EditorAutoSetup.cs`) runs `[InitializeOnLoad]` on every compile. It:
1. **Auto-generates levels** if no `LevelData` assets exist in `Assets/ScriptableObjects/Levels/`
2. **Auto-wires character sprites** (PPU values) if the Cluck/Loaded.png sentinel is on wrong PPU
3. **Auto-copies card sprites**: `assets/FarmCards/*.png` → `Assets/Sprites/UI/Cards/` and `assets/LevelCards/*.png` → `Assets/Sprites/UI/LevelCards/` (only copies files not yet present — won't overwrite)
4. **Auto-fixes launcher sprites** (Single mode, PPU=768, custom pivots)
5. Calls `AssetDatabase.Refresh()` to pick up files changed outside Unity (e.g., by `remove_backgrounds.py`)

`SpriteAutoImporter` (`unity/Assets/Editor/SpriteAutoImporter.cs`) is an `AssetPostprocessor` that enforces PPU and `Single` mode on import for launchers, UI cards, robot, and character sprites. Key rule: **sprites in `Sprites/Environment/Launchers/` must be `Single` mode** — `Multiple` mode breaks `LoadAssetAtPath<Sprite>` and disconnects the trebuchet arm visually.

### Editor Menu Items
| Menu | When to run |
|---|---|
| **FarmFury → Wire Scene References** | After adding a new prefab, level, or after clean checkout. Wires all Inspector refs in Game.unity. Also sets camera position/orthoSize and _cameraRestOffset. |
| **FarmFury → Generate All Level Data** | To recreate the 6 World 1 LevelData assets (overwrites existing). Run Wire Scene References after. |
| **FarmFury → Wire Sprites** | After adding new character art to Assets/Sprites/Characters/. Sets PPU and wires pose sprites into all 8 animal prefabs. |
| **FarmFury → Reimport Sprites** | Force-reimport all sprites in Launchers/, Characters/, and Enemies/ folders. Run if sprites look stale after the art pipeline. |

---

## Development Roadmap (6 Phases)

### Phase 1 — Core Feel ✅ DONE
1.1 Slingshot fix — drag mechanic, arm tracks drag, ArmSnap coroutine.
1.2 Trajectory arc — 20 pooled SpriteRenderer dots, physics-accurate, alpha fade over last 20%; Level 0 full arc, Level 17 fades at midpoint.
1.3 Destruction feedback — procedural crack overlays (Bresenham, 2 child SRs), fragment burst, robot white-flash + radial particles, CameraShake singleton.
1.4 Audio — AudioManager singleton; 6 DSP-generated clips (Sine/Noise/Sweep/ADSR); hit sounds throttled at 80ms per block type.
1.5 Camera — exponential-decay follow (6u/s); 2.5s landing pause then SmoothStep pan back over 1.2s.

### Phase 2 — UI/UX Shell ✅ DONE
2.1 HUD — score, bird-queue card widgets with bob animation, pause button.
2.2 Level Complete panel — animated star pop-in (bounce 1→1.42→1), score, REPLAY/NEXT.
2.3 Level Failed panel — TRY AGAIN / MENU.
2.4 Pause menu — RESUME/RESTART/MENU + ♪ MUSIC / ◎ SFX toggles persisted via PlayerPrefs (`ff_sfx_enabled`, `ff_music_enabled`).
2.5 Level select — world-art thumbnails, bottom gradient overlay, lock veil, gold/grey ★★★.
2.6 Main menu — full-screen `LandingPage.png` splash + orange "▶ PLAY" button.

### Phase 3 — Character Roster ✅ DONE
All 8 animals scripted, all Kling AI art generated, backgrounds batch-removed via `tools/remove_backgrounds.py`, sprites imported to `unity/Assets/Sprites/Characters/<Name>/`, pose sprites wired into all 8 animal prefabs via **FarmFury → Wire Sprites**.

`AnimalBase` holds 5 pose sprite fields (`_sprIdle`, `_sprLoaded`, `_sprInFlight`, `_sprImpact`, `_sprAbility`) and swaps them at launch/collision/ability. All 8 subclasses guard procedural tint colors with `if (!HasRealSprites)`.

**Pose set per character:**
| File | When used |
|---|---|
| `Idle.png` / `Idle2.png` | Waiting in bird queue |
| `Loaded.png` | Sitting in bucket (sentinel for PPU check) |
| `Launch.png` / `Launch2.png` | Release frame |
| `InFlight.png` | Main flight sprite |
| `Impact.png` | On collision |
| `Trigger.png` / `AbilityTrigger.png` | Ability activation |

**Sprite sizing spec (Kling AI generation):** 1024×1024 px, character fills 75–80% of canvas, white background.
**Physics radii (do NOT change):** small animals `_col.radius = 0.36f`; Bessie `_col.radius = 0.52f`.
**PPU map:** Cluck/Percy/Woolly/Billy=1067, Bessie=740, Ducky=1280, Horace=960, Gerald=1010.

#### Environment Art Pipeline
Raw art in `assets/` → `python tools/remove_backgrounds.py` → `unity/Assets/Sprites/`.

**`assets/` folder layout:**
```
assets/
  Backdrops/          sky painting, trebuchet reference sheets + component PNGs
  <Name>_<Animal>/    per-character pose PNGs (e.g., Cluck_Chicken/)
  FarmCards/          HUD card portraits — Cluck_Chicken.png … Billy_Goat.png
  LevelCards/         6 world card images — Meadow.png, Frozen.png, WaterMill.png,
                      Sky.png, Sunken.png, Mothership.png  ← all 6 exist
  RobotEnemy/         Robot_Idle.png, HarvesterRobot.png (new)
  Buildingblocks/     Cement.png, Metal.png, Wood.png — future block types (not yet wired)
  WorldProps/         per-world prop PNGs: IceTundra/, WatermillVillage/,
                      SkyIslands/, SunkenCity/, RobotMothership/
  FrameSprites/       VFX spritesheets: DustCloud, EggSplat, Explosion,
                      FeatherBurst, ImpactStars, Shockwave, StoneDebris, ScoreStar
```

**World 1 props live in Unity directly** (not in assets/): `unity/Assets/Sprites/Environment/World1Props/` — 15 PNGs including 5 new ruins/barn props not yet wired. Filenames: no spaces (GrassTuft, WoodenBarrel — note: old names had spaces, now fixed).

**Sky spec:** 1920×1080 px, no alpha. **Prop/launcher spec:** 1024×1024 px, white bg, element 75% of canvas.

### Phase 4 — World 1 Completion *(current)*
✅ Sky backdrop, ground art (5-layer terrain), main menu art, trebuchet sprites, sprite PPU calibration, camera zoom (orthoSize=4.5, rest offset 5.5/2.5), trebuchet arm alignment (_pivotHeight=1.76), L01 redesign (two-tier cage, 2 robots), animal card HUD, level select redesign with world thumbnails, trebuchet drag mechanic, robot visibility fix, all 6 world level cards, SceneryBuilder (deterministic-RNG World 1 props per level), destruction improvements (4 fading fragments, damage at 50% health), all-wood early levels (L01–L03), full coordinate system rebuild (ground Y=−2.5, launcher X=−5.5), retuned block health (wood=80, stone=220), robot scale (0.6×0.9).

**New World 1 prop sprites added** (not yet wired to SceneryBuilder): `RuinedStoneWall.png`, `StoneTower.png`, `StoneWall(Tall).png`, `OldBarn.png`, `DamagedBarn.png`.

**New trebuchet sprite kit** (multi-part, not yet fully wired): `Trabuchet_Base.png` (static frame), `Trabuchet_Arm.png` (rotating), `Trabuchet_Counterweight.png`, `Trabuchet_Sling.png`, `Trabuchet_Loaded.png`, `Trabuchet_MidSwing.png`, `Trabuchet_Fired.png`. Future-world launchers also present but unwired: GravitySling, Ice Cannon, Plane, Submarine, WaterWheel.

**Still to do:** wire 5 new World 1 props into SceneryBuilder; decide trebuchet sprite approach (sprite-swap vs multi-part assembly); add remaining 12 Meadow Ruins levels (L07–L18); Robot Commander boss.

### Phase 5 — Worlds 2–6
Each world: new launcher, world physics modifier, new animals, all levels, environment art, music, boss.

### Phase 6 — Polish & Release
Animations, particle systems, music, monetisation (ethical-first, no pay-to-win), achievements, iOS/Android.

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
Phaser 3.60 (CDN), Matter.js (bundled), WebAudio API (procedural sound), localStorage.

### History
`PROGRESS.txt` at the repo root is the prompt-by-prompt changelog for the Phaser prototype (Prompts 1–12). Read it for precise physics constants, level coordinate calculations, and the rationale behind tuning decisions (e.g., why MAX_VEL was reduced from 18→14 in Prompt 12).

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

### Stack
Unity 6.5 (6000.5.0f1), URP 2D, Physics2D, New Input System, TextMeshPro.

### Coordinate Conversion
- 1 Unity unit = 50 Phaser pixels
- `x_unity = x_phaser / 50`
- `y_unity = -(y_phaser - 770) / 50`
- **Ground surface at Y = −2.5** (world space). Trebuchet base at (−5.5, −2.5, 0). Camera at (0, 0, −10), orthoSize = 4.5. `_cameraRestOffset = (5.5, 2.5)` → camera parks at (0, 0).
- **Level block/robot Y** = surface_offset − 2.5 (e.g. spec pos(3.0, 0.2) → world Y = −2.3, bottom at −2.5 = ground). World X is direct (spec X = world X).

### Physics Settings
- Gravity Y: −20. Layers: Ground=6, Animal=7, Block=8, Robot=9, Egg=10.

### Script Architecture
```
unity/Assets/Scripts/
  Core/
    GameManager.cs          — singleton (DontDestroyOnLoad); states: Idle/Playing/LevelComplete/LevelFailed
                              ForceStartLevel(int) boots a level without LoadScene (used for direct Editor play)
                              TryAutoLoadLevels() auto-discovers LevelData assets in Editor builds
                              BuildFallbackLevel() creates a hardcoded procedural level when no assets found
                              LoadMenu() is scene-optional: only loads MainMenu if it's in Build Settings;
                              otherwise LevelSelectController handles the Idle state in-scene
    BackgroundController.cs — sortingOrder=−100; cover-scale in Start(); LateUpdate() follows camera
    AudioManager.cs         — 6 DSP-generated clips; SfxEnabled/MusicEnabled from PlayerPrefs
    CameraShake.cs          — singleton, auto-attached to Launcher GO
  Level/
    LevelData.cs            — ScriptableObject; birds[], blocks[], robots[] arrays; par bird count
    LevelLoader.cs          — instantiates prefabs; TryConsumeBird / PeekNextBird; fires
                              OnBirdConsumed event; BirdQueueSnapshot property;
                              DelayedLevelComplete / DelayedLevelFailed coroutines → GameManager
                              AutoLoadPrefabs() runs in Awake() (Editor only) — auto-finds all prefabs
                              from Assets/Prefabs/ by type, so Inspector wiring is not required in Editor
  Animals/
    AnimalBase.cs           — abstract; Kinematic until Launch(); Mouse.current (New Input System);
                              5 pose sprites; HasRealSprites property; DestroyAnimal() fires OnAnimalDestroyed
    CluckAnimal.cs          — 5-egg cluster bomb in 120° spread; eggs from _eggPrefab
    BessieAnimal.cs         — vy-18 slam; shockwave 3.6u radius on Ground-tagged landing
    PercyAnimal.cs          — Bounce Roll: bounciness boost, 3 bounces
    WoollyAnimal.cs         — Triple Clone: Instantiate + SetAsClone at ±15°
    DuckyAnimal.cs          — Skip Shot: flattens trajectory, skips off Ground ×3
    HoraceAnimal.cs         — Rear Kick: blasts nearby objects backward on first contact
    GeraldAnimal.cs         — Puff Up: localScale×3, mass×4, forward impulse
    BillyAnimal.cs          — Headbutt Through: isTrigger window, _penetrateDamage via OnTriggerEnter2D
    EggProjectile.cs        — layer 10; flat _damage=15 on first contact only
  Blocks/
    BlockBase.cs            — spawns Static; wakes ALL blocks on first TakeDamage();
                              health = baseMaxHealth × area/stdArea; colour tints at 50%/25%/0% health;
                              damage = impulse × 1.0 (no multiplier); on death: 4 fragments fly outward,
                              FragmentFader coroutine fades alpha 1→0 over 0.6s then destroys
    WoodBlock.cs            — baseMaxHealth=80, baseMass=5, bounciness=0.2
    StoneBlock.cs           — baseMaxHealth=220, baseMass=8, bounciness=0.1
  Enemies/
    RobotEnemy.cs           — HP=35, impulse damage ×1.0; scale=(0.6,0.9); BoxCollider2D.size=(1,1), mass=20;
                              2 red eye child GOs in Awake; calls LevelLoader.NotifyRobotDestroyed
  Scoring/
    ScoreManager.cs         — Robot +1000, Wood +100, Stone +200, Egg +50, bird-left bonus +500
                              PlayerPrefs keys: ff_score_N, ff_stars_N
  Launcher/
    CatapultLauncher.cs     — click bird-in-bucket → drag to rotate arm → release to fire;
                              bird locked to BucketWorldPos(armAngle) throughout drag;
                              load fraction (dragAngle−190°)/50° → speed 7–13 m/s at 20°–50°;
                              DrawArmAt(): arm z = angleDeg − 190°; _pivotHeight=1.76,
                              _armLongLength=0.86, _armShortLength=0.71, MaxLoadAngle=50°;
                              _returnDelay=2.5s; orthoSize=4.5; EnsureGroundExists() validates Y≈−2.5
  UI/
    HUDController.cs        — Canvas built at runtime; card widgets (active 108×142, queue 82×108);
                              orange ⚡N damage badge; Level Complete/Failed/Pause panels
    LevelSelectController.cs — ScrollRect + GridLayoutGroup 3-col; RefreshGrid() rebuilds on show;
                              world-art thumbnails with gradient overlay; lock veil
    MainMenuController.cs   — LandingPage.png + dark vignette + orange PLAY; shows on GameState.Idle

unity/Assets/Editor/
  SceneSetup.cs         — FarmFury > Wire Scene References; wires all Inspector refs;
                          sets camera (0,0,-10) orthoSize=4.5; launcher at (-5.5,-2.5,0);
                          ground center (0,-2.75,0) scale (60,0.5,1) → top at Y=-2.5;
                          NOTE: use TextureImporterSettings (ReadTextureSettings/SetTextureSettings)
                          for pivots — spriteAlignment property was removed in Unity 6
  LevelDataGenerator.cs — FarmFury > Generate All Level Data; LXX_ filenames must sort alphabetically
  SpriteWiring.cs       — FarmFury > Wire Sprites; sets per-character PPU; wires pose sprites
  BuildScript.cs        — batch-mode entry points called by Run-Unity.ps1
  EditorAutoSetup.cs    — [InitializeOnLoad]; auto-generates levels, copies card sprites,
                          fixes launcher sprites, wires character sprites on every compile
  SpriteAutoImporter.cs — AssetPostprocessor; enforces PPU/Single mode on import;
                          FarmFury > Reimport Sprites to force-apply
```

### Runtime Event Flow

Understanding how a level start/end propagates through the singletons:

**Level start:**
`GameManager.ForceStartLevel(idx)` (Editor play) or `GameManager.StartLevel(idx)` (menu) → `TransitionTo(Playing)` → fires `OnLevelStarted` → `LevelLoader.HandleLevelStarted(data)` → `LoadLevel()` spawns blocks/robots/birds → `ScoreManager.InitLevel()` resets counters → `CatapultLauncher` loads the first bird.

**Level complete:**
All robots destroyed → `LevelLoader.NotifyRobotDestroyed()` → `_spawnedRobots.Count == 0` → `DelayedLevelComplete()` (2s wait) → `ScoreManager.FinaliseLevel()` → `GameManager.CompleteLevel()` → `TransitionTo(LevelComplete)` → `HUDController` shows panel.

**Level failed:**
`CatapultLauncher` detects no more birds and calls `LevelLoader.NotifyBirdsExhausted()` → `DelayedLevelFailed()` (1.5s wait) → `GameManager.FailLevel()` → `TransitionTo(LevelFailed)` → `HUDController` shows panel.

### Key Implementation Rules (Unity)
- **Input System ONLY:** `using UnityEngine.InputSystem;` — `Mouse.current.leftButton.wasPressedThisFrame`, `.isPressed`, `.wasReleasedThisFrame`. `mouse.position.ReadValue()` → `Vector2`. `UnityEngine.Input` is incompatible.
- Blocks spawn `RigidbodyType2D.Static`; first `TakeDamage()` calls `WakeAllStaticBlocks()` → `FindObjectsByType<BlockBase>()` (Unity 6 API).
- Animals start Kinematic → Dynamic on `Launch(velocity)`.
- Never destroy physics body in collision callback — defer with coroutine.
- **SpriteRenderer is auto-added:** both `BlockBase.Awake()` and `AnimalBase.Awake()` add one if null — prefabs don't need SpriteRenderer pre-added.
- **Ground is recreated at runtime:** `CatapultLauncher.Start()` calls `EnsureGroundExists()` — validates surface near Y=−2.5, width>5, recreates if stale. The authoritative visual ground is built by `SceneSetup.EnsureGround()` (5 layers: deep fill, soil, soil-edge, GrassBase, GrassTips; center at (0,−2.75,0), scale (60,0.5,1)) and saved into the scene file. Run **Wire Scene References** to regenerate it.
- **LevelLoader is auto-found:** `CatapultLauncher.Awake()` does `FindAnyObjectByType<LevelLoader>()` — Inspector wiring optional.
- **[SerializeField] stale value trap:** changing a `[SerializeField]` default in code does NOT affect already-serialised components. Use `private const` for values that must not be overridden.
- Ground collider: `localScale=(60,0.5,1)`, `BoxCollider2D.size=(1,1)` → world collider 60×0.5, top edge at Y=−2.5. Never set both scale AND size to large values.
- **Effective mass formula:** both dynamic → `(mA × mB) / (mA + mB)`; one static → `movingBody.mass × 0.6`. Damage threshold impulse > 1.5. Damage = `impulse × 1.0` (no multiplier — blocks and robots both use ×1.0).
- **Robot spawn invincibility:** `RobotEnemy.Initialise()` sets `_invincibleUntil = Time.time + 0.8f`. `OnCollisionEnter2D` returns early while invincible — prevents instant death from fall-settling onto blocks when levels load.
- **Scenery sortingOrder rule:** decorative props (SceneryBuilder) must use `sortingOrder ≤ 1`. Blocks are `sortingOrder=2`, robots `3`. Props with white-background PNGs (before `remove_backgrounds.py`) at sortingOrder=2 visually cover blocks, making structures appear missing. Always keep props behind gameplay elements.
- **SceneryBuilder** (`Scripts/Core/SceneryBuilder.cs`): subscribes to `GameManager.OnLevelStarted` in `Start()`. Uses deterministic `System.Random(levelIdx × 137 + 42)` so replays produce the same layout. `Place()` bottom-anchors sprites via `pivot.y / pixelsPerUnit * scale`. Avoids x=1–5 (structure zone) for ground-clutter props.

### Physics Values
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
Launcher          (CatapultLauncher.cs, at world pos −5.5, −2.5, 0)
Scenery           (SceneryBuilder.cs — 10 World1Prop sprite refs, rebuilt on OnLevelStarted)
BlockParent       (empty holder)
RobotParent       (empty holder)
Ground            (tag="Ground", layer=6; top edge at Y=−2.5; 5 visual layers below)
```

### Prefabs
```
Prefabs/Animals/     CluckAnimal, BessieAnimal, PercyAnimal, WoollyAnimal,
                     DuckyAnimal, HoraceAnimal, GeraldAnimal, BillyAnimal, Egg
Prefabs/Blocks/      WoodBlock, StoneBlock
Prefabs/Enemies/     Robot (0.6×0.9 scale, red eye child GOs)
Prefabs/Environment/ Ground (static)
```

### Adding a New Animal
1. Create class extending `AnimalBase`; override `Awake()` (set colour/radius/mass before `base.Awake()`), implement `TriggerAbility()`
2. Add prefab to `Prefabs/Animals/`
3. Add `AnimalType` enum value to `LevelData.cs`
4. Handle in `LevelLoader.CreateNextAnimal()` switch expression and `GetAnimalIdleSprite()`, `CatapultLauncher.NextBirdFA()` drag constant
5. Run **FarmFury → Wire Scene References**

### Adding a New Level
Add `LevelData` ScriptableObject in `Assets/ScriptableObjects/Levels/` with filename `LXX_<Name>.asset` (alphabetical order = load order). Run **Wire Scene References**. Or add a `Make(...)` call to `LevelDataGenerator` and run **FarmFury → Generate All Level Data**.

```
Y convention: Ground surface = −2.5. Block/robot Y = surface_offset − 2.5.
              Robot center (h=0.9, scale 0.6×0.9) → world y = −2.05 (sits on ground).
              Wood plank (h=0.4) → center at world y = −2.3. Stack upward by block height.
X convention: Structure zone ≈ x=2–4 (L01). Launcher bucket at x≈−6.
```
