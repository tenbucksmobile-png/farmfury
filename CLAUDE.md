# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

---

## Project Overview

**Farm Fury** is an Angry Birds-style physics destruction game. Farm animals launch from a cannon to wreck robot fortresses and reclaim the farm.

The repo contains two parallel codebases:
- `index.html` — **Phaser 3.60 prototype** (frozen at Prompt 12, kept as a reference/physics-validation artifact only — not under active development)
- `unity/` — **Unity 6.5 URP 2D port** (active development target for iOS/Android release)

This is the single source of truth for the Unity port. Detailed reasoning behind current values/conventions (root causes, dead ends, superseded fixes) lives in **`docs/HISTORY.md`** — read it when you need the "why," not just the "what." This file only states current truth.

Full GDD: `C:\Users\Personel\Desktop\FarmFury_GDD_v2.docx`

---

## Current Status & Known Issues

- **Only World 1 Level 1 (`L01_FirstContact`) is fully playable.** L02–L06 exist as `LevelData` assets but spawn floating ~4.4–4.9 units above the ground — they were generated under the pre-rebuild coordinate system (old ground Y=−2.5, launcher X=−5.5) and were never migrated to the current system (ground Y=−6.60, launcher X=−2.327). `LevelDataGenerator.cs`'s L02–L06 `Make()` calls still use the old Y range (≈ −0.9 to −2.3) — confirmed still true, not yet fixed. This is the single highest-leverage next step toward a playable World 1; see `docs/HISTORY.md` for the exact migration math.
- **`assets/` (raw art source) is currently absent from disk** — all 236 files were deleted outside any Claude Code session, but remain fully tracked in git (`git checkout -- assets/` restores them). The user has chosen to leave it deleted for now. `python tools/remove_backgrounds.py` will not work until it's restored.
- **`unity/Assets/Sprites/` (processed game art) is gitignored and exists in exactly one place: this machine's disk.** Not tracked by git in any commit. If lost, the only recovery path is restoring `assets/` and re-running the full art pipeline.
- **Monetisation/backend: 0% built.** `unity/Assets/Scripts/Monetisation/` exists but is empty — no Firebase, Unity IAP, AdMob, RevenueCat, or UGS references anywhere. Expected — this is Phase 6, correctly sequenced after content.
- **No level validator exists** (GDD §04 calls for one — a physics-sim script checking stability/solvability before a level ships). The L02–L06 bug above would have been caught by this.
- **No "Perfect" star tier** — `ScoreManager` implements 1/2/3★ only; the GDD's 4th tier (3★ + all blocks destroyed) is not implemented.
- 6 World 1 levels exist of 18 required (4 tutorial, 8 build, 4 twist, 2 boss — no boss level yet). L07–L18 are unwritten.

---

## GDD Summary

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

Only Cluck and Bessie are used in World 1 so far; the other 6 are fully scripted (`AnimalBase` subclasses) but await their worlds.

### 6 Worlds, 6 Launchers (126 total levels)
| World | Launcher | Key mechanic | Levels |
|---|---|---|---|
| 1 — Meadow Ruins | Farm Cannon (drag angle+power) | Character weight affects range | 18 |
| 2 — Frozen Tundra | Ice Cannon (angle + ricochet) | Freeze zones slow flight | 22 |
| 3 — Watermill Village | Water Wheel (timing — tap to fire) | Fire spread on wood | 22 |
| 4 — Sky Islands | Airdrop Biplane (timing — tap to drop) | Updraft column steering | 24 |
| 5 — Sunken City | Torpedo Tube (angle + bubble pop) | Current lanes + lever switches | 22 |
| 6 — Robot Mothership | Gravity Sling (angle + gravity wells) | Zero-G ability modifiers | 16 |

Only World 1 exists today (Worlds 2–6 are Phase 5, 0% started).

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
.\Run-Unity.ps1 levels        # Generate/overwrite all 6 LevelData ScriptableObjects — safe for L01
                               #   (kept in sync since 2026-07-01), destructive-neutral for L02-L06
                               #   (they're already broken; regenerating won't fix or worsen them)
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
| **FarmFury → Generate All Level Data** | Recreate the 6 World 1 LevelData assets (overwrites existing). L01 is kept in sync — safe to run. Run Wire Scene References after. |
| **FarmFury → Wire Sprites** | After adding new character art to `Assets/Sprites/Characters/`. Sets PPU and wires pose sprites into all 8 animal prefabs. |
| **FarmFury → Reimport Sprites** | Force-reimport all sprites in Launchers/, Characters/, and Enemies/ folders. Run if sprites look stale after the art pipeline. |
| **FarmFury → Reset World Map Progress** | Clears `ff_score_N`/`ff_stars_N` PlayerPrefs for all 18 levels — standalone from Wire Scene References since it's runtime save data, not scene/asset wiring. Run when leftover test progress (e.g. a level showing 3-star art from earlier testing) makes the world map's lock/star state confusing to read. |

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

### Coordinate System (current — do not use older values seen in L02–L06 or stale comments)
- 1 Unity unit = 50 Phaser pixels (`x_unity = x_phaser / 50`, `y_unity = -(y_phaser - 770) / 50` — legacy conversion from the Phaser prototype).
- **Ground surface at Y = −6.60.** Launcher GO at (−2.327, −6.60, 0) — an abstract aim-math anchor only since the 2026-07-02 cannon swap; no visual GameObject corresponds to it any more.
- **Visual launcher is `FarmCannon`**, independent of the Launcher GO's position: at (−3.0012, −5.1223, 0), scale (1.4711188, 1.3868444), sortingOrder=4. (User-verified ground truth as of 2026-07-03 — do not re-derive from camera/sprite math.)
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
    GameManager.cs          — singleton (DontDestroyOnLoad); states: Idle/Playing/LevelComplete/LevelFailed
                              ForceStartLevel(int) boots a level without LoadScene (used for direct Editor play
                              and for Replay/Restart — see Runtime Event Flow below)
                              TryAutoLoadLevels() auto-discovers LevelData assets in Editor builds
                              BuildFallbackLevel() creates a hardcoded procedural level when no assets found
                              LoadMenu() is scene-optional: only loads MainMenu if it's in Build Settings
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
                              pause menu and the Main Menu SETTINGS popup (same state, can't disagree).
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
                              OnAnimalImpact fires on real collision hits only; sortingOrder=6 on all 8 subclasses
    CluckAnimal.cs             — 5-egg cluster bomb in 120° spread from _eggPrefab (must be the egg GameObject
                              itself — _eggPrefab is typed GameObject, not EggProjectile); pass-through: punches
                              WoodBlock._passThrough=true at 70% velocity, skips base.OnCollisionEnter2D (so
                              hay punches never trigger OnAnimalImpact / stop the falling SFX)
    BessieAnimal.cs            — vy-18 slam; shockwave 3.6u radius on Ground-tagged landing
    PercyAnimal / WoollyAnimal / DuckyAnimal / HoraceAnimal / GeraldAnimal / BillyAnimal
                              — Bounce Roll / Triple Clone / Skip Shot / Rear Kick / Puff Up / Headbutt
                              Through respectively; scripted and ready, not yet used in any shipped level
    EggProjectile.cs           — layer 10; flat _damage=15 on first contact only
  Blocks/
    BlockBase.cs               — spawns Static; wakes ALL blocks on first TakeDamage() via
                              FindObjectsByType<BlockBase>() (Unity 6 API); health = baseMaxHealth ×
                              area/stdArea; tints at 50%/25%/0% health; damage = impulse × 1.0; on death:
                              4 fragments fly outward, fade 1->0 over 0.6s. ApplyOverrides(health, mass)
                              applies BlockSpawnData overrides after Initialise() — called by LevelLoader.
                              Optional _sprDamaged + PlayDamageFlash() for a brief art swap on hit (only
                              wired where dedicated damaged-state art exists, e.g. HaybaleBlock)
    WoodBlock.cs                — baseMaxHealth=80, baseMass=5, bounciness=0.2; _passThrough (public)
    StoneBlock.cs                — baseMaxHealth=220, baseMass=8, bounciness=0.1
  Enemies/
    RobotEnemy.cs                — HP=35 (class default; HarvesterRobot.prefab overrides to 40); impulse
                              damage ×1.0; scale=(0.6,0.9); BoxCollider2D.size=(1,1), mass=20; 2 red eye
                              child GOs; calls LevelLoader.NotifyRobotDestroyed; Rigidbody2D.bodyType=Static
                              set explicitly in Awake() (prefabs otherwise default to Dynamic and free-fall
                              under gravity — a real bug fixed 2026-07-18, see docs/HISTORY.md Round 12);
                              _invincibleUntil = Time.time+0.8f prevents instant death from fall-settling
                              onto blocks at level load; FlashDamage() swaps to _robotDamagedSprite briefly
                              if wired, else falls back to a white tint
  Scoring/
    ScoreManager.cs               — Robot +1000, Wood +100, Stone +200, Egg +50, bird-left bonus +500;
                              PlayerPrefs keys: ff_score_N, ff_stars_N (0-based, plain star count)
  Launcher/
    CatapultLauncher.cs            — visual launcher is FarmCannon (see Coordinate System above). Aim math:
                              click bird -> drag -> PivotPos()-relative angle clamped to [_armRestAngle=218°,
                              218°+MaxLoadAngle=50°] -> loadFrac -> LaunchVelocity() speed 4.0-4.9 m/s, angle
                              58°-52° (both ends stay high — always visibly arched; re-tuned 2026-07-14,
                              paired with AnimalBase.Launch()'s gravityScale=0.18 for a slower, loopier arc).
                              MinAimRadius=0.6f: drag angle freezes until the pointer has moved that far from
                              the pivot, preventing hair-trigger full-power swings from small movements
                              (mobile touch fix, 2026-07-18). CannonBarrelOffset=(1.1,0.4) = _launchPoint
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
    HUDController.cs                 — Canvas built at runtime; card widgets anchored top-left (anchorMin
                              0.02,1); orange ⚡N damage badge; Level Complete/Failed/Pause panels; SafeArea
                              RectTransform wrapper (Screen.safeArea-driven) for score/queue/pause elements
    LevelSelectController.cs          — grid-based level select (ScrollRect + GridLayoutGroup). Unwired for
                              World 1 (superseded by WorldMapController) but kept in place for World 2+
    MainMenuController.cs             — LandingPage.png background (title/character art baked in); PLAY
                              (bottom-left) and SETTINGS (bottom-right), both corner-anchored; PLAY opens
                              WorldMapController; SETTINGS opens a Music/SFX popup sharing AudioManager state
                              with the pause menu
    WorldMapController.cs              — Sunrise Meadows (World 1) level map; ScreenSpaceOverlay Canvas,
                              sortingOrder 300; 18 LevelMarker instances positioned via PathPositions —
                              hand-traced against the full visible S-curve (pond -> up past the windmill/
                              ruins bend -> back down -> tail to the fortress), not just a graph-shortest-
                              path between the two ends. Every earlier automated trace (BFS-bin, skeleton
                              shortest-path) independently found the upper bend to be a topological dead-
                              end and excluded it — defensible by pure connectivity, but wrong for this
                              purpose: it rendered as markers bunched in a flat row hugging the bottom,
                              confirmed by rendering the old points back onto the source art and matching a
                              reported screenshot exactly (see docs/HISTORY.md Round 14). Unlock rule
                              (level 1 always unlocked, level N needs level N-1 >=1 star) reuses
                              ScoreManager.GetBestStars(); position indicator (90x90) slides then bobs to
                              the newest unlocked pin, hover offset +100 above the marker (single
                              IndicatorRoutine coroutine). NEXT LEVEL (600x150) and Home (150x150, matches
                              the landing page's PLAY/SETTINGS icon scale) both live inside a
                              Screen.safeArea-driven SafeArea child (same ApplySafeArea pattern as
                              HUDController/MatchUpScreen), corner-anchored with a small fixed inset —
                              real rendered sprites, not baked into the background. Owns MatchUpScreen's
                              art fields directly (see note below on why).
    LevelMarker.cs                     — UI Image+Button (not SpriteRenderer — Button needs uGUI) + level
                              number TMP label; MarkerSize=80x120 (2:3 aspect, matches the 256x384 source
                              art). Refresh(unlocked,stars,...) picks locked/unlocked/star1/2/3 art (falls
                              back to 3-star art — no dedicated 2-star asset yet). LevelMarker_Unlocked.png
                              /LevelMarker_3stars.png have a static "1"/"3" baked into their own art (not a
                              dynamic level-index number) — a level showing the "3" badge means it was
                              previously 3-starred (real save data, e.g. leftover test progress), not a bug;
                              see FarmFury -> Reset World Map Progress above.
    MatchUpScreen.cs                    — full-screen animal-vs-robot transition, entirely non-interactive
                              (no tap-to-continue, no close button — removed 2026-07-21). Scripted sequence:
                              MatchUpBackground.png backdrop -> LevelHeader1.png pops in (top-anchored inside
                              its own SafeArea, level 1 only — no LevelHeader2/3/... exist, a known accepted
                              gap) -> animal/robot cards (560x560, tilted ±10° outward) slide in from both
                              edges while VS.png (220x220) slams in between them, timed to land exactly as
                              the cards arrive -> 2s hold -> countdown 3/2/1 (each ~2s: pop in, hold, pop
                              out) -> READY! -> fades to opaque black (NOT the whole panel to transparent —
                              see below) -> GameManager.ForceStartLevel(). Cards read fresh from
                              GameManager.GetLevelData(index).birds[0] / .robots[0].robotType on every Show()
                              — different levels show different matchups, not a static splash. Shows
                              "COMING SOON" and returns to the map (no launch) when the level has no
                              LevelData yet. Art sourced from Assets/Sprites/UI/MatchUp/ (dedicated folder,
                              distinct from the HUD's Assets/Sprites/UI/Cards/) — only Cluck/Bessie/Basic/
                              Harvester have art there today, matching L01-L06. **Fade-to-black, not fade-
                              to-transparent:** the screen used to fade its whole CanvasGroup to alpha 0
                              before launching, which revealed the World Map's own background/pins
                              underneath for the fade's duration (it's a sibling inside the same canvas) —
                              looked like "the game navigates back to the map before gameplay loads." Fixed
                              by fading an opaque black Image overlay IN instead; GameManager.ForceStartLevel()
                              then triggers WorldMapController.HidePanel(), which deactivates the whole map
                              canvas (MatchUpScreen included, since it's a child) atomically in one frame —
                              the last thing rendered before that cut is solid black, never stale map/card
                              content (see docs/HISTORY.md Round 15). Art fields are NOT [SerializeField]
                              here — passed into Init() by WorldMapController instead, because nested
                              runtime-built child components never get their Awake() re-fired by the batch
                              "Wire Scene References" editor pass, so any [SerializeField] living directly on
                              them silently never saves (see docs/HISTORY.md Round 11) — put art fields on
                              the top-level persisted component and thread down via Init() for any future
                              nested UI component.

unity/Assets/Editor/
  SceneSetup.cs           — FarmFury > Wire Scene References; wires all Inspector refs; sets camera
                              (0,0,-10) orthoSize=4.5; launcher at (-2.327,-6.60,0); creates/wires FarmCannon
                              at (-3.0012,-5.1223,0) scale (1.4711188,1.3868444,1); ground center
                              (0,-2.75,0) scale (60,0.5,1) -> top at Y=-2.5 (physics-collider-only — no
                              longer generates visual ground layers; deletes leftover code-generated
                              GroundFill/GrassBase/etc. from older runs; ground/grass visuals are entirely
                              user-authored scene GameObjects now)
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
```

### Runtime Event Flow

**Level start:**
`GameManager.ForceStartLevel(idx)` (Editor play, and Replay/Restart) or `GameManager.StartLevel(idx)` (menu, full scene load) → `TransitionTo(Playing)` → fires `OnLevelStarted` → `LevelLoader.HandleLevelStarted(data)` → `LoadLevel()` spawns blocks/robots/birds → `ScoreManager.InitLevel()` resets counters → `CatapultLauncher` loads the first bird → `SnapCameraToRest()`.

**Level complete:**
All robots destroyed → `LevelLoader.NotifyRobotDestroyed()` → `_spawnedRobots.Count == 0` → `DelayedLevelComplete()` (2s wait) → `ScoreManager.FinaliseLevel()` → `GameManager.CompleteLevel()` → `TransitionTo(LevelComplete)` → `HUDController` shows panel.

**Level failed:**
`CatapultLauncher` detects no more birds and calls `LevelLoader.NotifyBirdsExhausted()` → `DelayedLevelFailed()` (1.5s wait) → `GameManager.FailLevel()` → `TransitionTo(LevelFailed)` → `HUDController` shows panel.

**Replay / Try Again / Restart:** all route through `GameManager.RestartLevel()` → `ForceStartLevel(CurrentLevelIndex)` — the same no-reload path Editor play uses, not a full `SceneManager.LoadScene()`.

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
GameManager           (DontDestroyOnLoad)
LevelLoader
ScoreManager
HUD
Background_SkyV1      (SpriteRenderer — sky painting)
Launcher               (CatapultLauncher.cs; abstract aim-math anchor, no visual meaning)
FarmCannon              (visual launcher; single static SpriteRenderer, Cannon.png, recoils on fire
                        via CannonFireSequence()/RecoilTo(), otherwise stays put — no rotation)
Scenery                 (SceneryBuilder.cs; _useExactPlacement=true skips L1)
[L1 scenery GOs]         OldBarn_Right, OakTree, GnarledTree, Windmill, WoodenFence×4, Rock×2,
                        GrassTuft×5, WildFlowers — decorative Haybail×4 were deleted (superseded by
                        gameplay HaybaleBlock instances at the same spots)
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
Add `LevelData` ScriptableObject in `Assets/ScriptableObjects/Levels/` with filename `LXX_<Name>.asset` (alphabetical order = load order). Run **Wire Scene References**. Or add a `Make(...)` call to `LevelDataGenerator` and run **FarmFury → Generate All Level Data**. Use the current coordinate system (see above) — do not copy Y/X values from L02–L06, which are on the old, broken system.

---

## Development Roadmap (6 Phases)

- **Phase 1 — Core Feel:** ✅ done (slingshot→cannon aim, trajectory arc, destruction feedback, audio, camera follow).
- **Phase 2 — UI/UX Shell:** ✅ done (HUD, Level Complete/Failed panels, pause menu, Sunrise Meadows world map, main menu).
- **Phase 3 — Character Roster:** ✅ done (all 8 animals scripted and art-wired).
- **Phase 4 — World 1 Completion:** current phase. Only L01 is playable; L02–L06 need coordinate migration (see Current Status); L07–L18 unwritten; no boss level yet; no level validator.
- **Phase 5 — Worlds 2–6:** not started (0%).
- **Phase 6 — Polish & Release:** not started. Monetisation stack per GDD §06 (Firebase, UGS, AdMob + AppLovin MAX, Unity IAP + RevenueCat, 7 revenue streams — full pricing/mechanics in GDD §05). Guardrails: no ads in first 20 levels, no pay-to-win, no energy system, cosmetics never randomised.

**Practical read:** the single highest-leverage next step is fixing the L02–L06 coordinate bug (small, well-understood — see Current Status above), followed by authoring L07–L18. Monetisation is correctly sequenced last.

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
