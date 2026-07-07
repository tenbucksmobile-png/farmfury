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

## Git Repository Scope (Important)

This project's `.git` lives at `C:\Users\Personel\.git` — **the entire home directory is one
shared git repository**, not a repo scoped to `FarmFury\`. Running `git status`/`git add .`/
`git add -A`/`git commit -a` from anywhere inside it can pick up files from *any* other project
on this machine (CryptoAlgoBot, IndabaCares, Kaya, personal documents, spreadsheets, etc.) — this
has already happened historically; the commit history contains files from `Desktop/IndabaCares/`
and `Claude/Projects/Kaya/` that have nothing to do with this game. Two remotes are configured:
`farmfury` → `github.com/tenbucksmobile-png/farmfury.git` (this project) and `origin` →
`github.com/tenbucksmobile-png/crypto-algo.git` (a **different, unrelated** project — never push
here by mistake). Always stage explicit file paths one at a time when committing FarmFury work;
never use a wildcard/`-a` add. Discovered 2026-07-07 — not yet restructured into its own repo
(the user's call whether/when to do that; it's a bigger, more consequential change).

---

## Current Status & Known Issues

- **Audio/video source assets reorganized into dedicated folders — 2026-07-07, user-driven move.** All sound files now live in `unity/Assets/Audio/` (flat, no subfolders) and both celebration/taunt clips (`Cluck_Celebration.mp4`, `Robot_Celebration.mp4`) now live in `unity/Assets/Video/` — moved out of the old `Assets/Sprites/UI/Video_Sound/` (now deleted, was gitignored so nothing to clean up in git) and, briefly, out of `Assets/Audio` too (the .mp4s landed there first, then got their own folder). All `SceneSetup.cs` `AssetDatabase.LoadAssetAtPath` path strings were updated to match (`EnsureAudioManager`, `EnsureLevelCompleteManager`, `EnsureLevelFailedManager`, `EnsureCelebrationVideoBackground`, `WireMatchUpCards`). Scene references resolve by GUID, not path, so anything moved with its `.meta` file intact (confirmed for the audio moves) needed no scene changes; the two video files landed at `Assets/Video` without their original `.meta` (fresh copies), so Unity assigned them new GUIDs on next import — re-running **FarmFury → Wire Scene References** repointed `Game.unity` at the new GUIDs automatically (this is exactly what that command is for). If more audio/video assets move in the future, grep `SceneSetup.cs` for the old path and re-run Wire Scene References.
- **Countdown.mp3 added and synced to the MatchUpScreen countdown — 2026-07-07.** `MatchUpScreen.PlaySequence()`'s 3/2/1/READY beat previously ran on an arbitrary fixed 2s-per-numeral pace with no audio (see the AUDIO NOTE that used to be at the top of the file). Countdown.mp3's actual beep timing was measured by decoding it (ffmpeg) and analysing the energy envelope in Python: four beeps — three short taps at 0.02s/1.02s/2.02s (1.0s apart, for 3/2/1) then one longer confirmation tone at 3.02s running to the clip's end (~4.05s total, for READY). `MatchUpScreen` now plays the clip once via its own `AudioSource` right as "3" appears, and each numeral's pop-in/hold/pop-out (`CountdownBeat`) is paced to total exactly one beep interval so the next numeral lands on the next beep — constants documented at the top of the timing-constants block in `MatchUpScreen.cs`. The clip is threaded through `MatchUpScreen.Init()` (not a `[SerializeField]` on `MatchUpScreen` itself — see the existing class comment on why nested runtime-built components can't rely on Wire Scene References re-firing their `Awake()`) from a new `WorldMapController._countdownClip` field, wired by `SceneSetup.WireMatchUpCards()` from `Assets/Audio/Countdown.mp3` via a new generic `WireAudioClip(SerializedObject, ...)` helper (mirrors `WireSprite`'s shape).
- **Cluck's Level Complete celebration video renders invisible; the Level Failed robot taunt video is fine — 2026-07-07, unresolved.** Both use the exact same shared pipeline (`VideoChromaKey`, `FarmFury/ChromaKeyVideo` shader, same overlay `Canvas`/`RawImage` instance) and static analysis found no code-level asymmetry: `_celebrationClips[0]` is correctly wired to `Cluck_Celebration.mp4` in `Game.unity` (confirmed via the scene YAML and `Editor.log`), `AnimalType.Cluck == 0` matches the array index, and simulating the shader's exact chroma-key math in Python against real decoded frames of both clips shows Cluck compositing correctly (chicken clearly opaque against transparent green). One confirmed-real bug was found and fixed in the same pass: `SceneSetup.EnsureCelebrationVideoBackground()`/`EnsureBackground()` both hardcoded a sky asset path (`Assets/Sprites/Environment/Skies/SkyPainting.png`) that doesn't exist on disk — the real file is `Background_SkyV1.png` — so `VideoChromaKey._backgroundSprite` was silently never wired and the video's transparent regions always fell through to the frozen gameplay camera instead of a clean sky backdrop; fixed to point at the right file. This is a real defect but doesn't explain the character itself being invisible (it only affects what shows *behind* the keyed-out background, not the chicken's own opaque pixels). Root cause still unconfirmed — this environment has no Unity render/play access, so the actual runtime decode/composite can't be observed directly (Windows Media Foundation logged an "unknown color primaries, falling back to default may result in color shift" warning for both clips on import, which is a plausible but unverified lead). Needs the user to report exactly what they see (fully see-through to frozen gameplay, solid black, solid green, or something else) to narrow further.
- **Audio rework on Level Complete/Failed — 2026-07-07.** Gameplay music now stops the instant the state transitions to `LevelComplete`/`LevelFailed` (`AudioManager.OnStateChanged`), and the procedural `Win`/`Fail` DSP jingles (`AudioManager.Sound.Win`/`.Fail`) were removed entirely — previously both played on top of the celebration/taunt video's own accompanying audio clip and the still-looping gameplay music. Now the video's own `AudioClip` (played via `VideoChromaKey.Play`) is the only sound heard during that sequence.
- **Level Complete/Failed videos held 1s longer before fading — 2026-07-07.** `LevelCompleteManager._celebrationDuration`/`LevelFailedManager._tauntDuration` were `4f`, but the actual clips run ~4.04-4.05s — the hold was cutting off fractionally before the clip's own last frame finished and immediately fading, which read as "ends very abruptly" (user report). Bumped both to `5f` (clip length + a full 1s of held-still breathing room). **Both the C# defaults AND the already-serialized values in `Game.unity` were updated** — see the `[SerializeField]` stale-value trap below; changing only the code default would not have affected the live scene.
- **Haybail impact/destroy sound replaced with a dedicated explosion clip — 2026-07-07.** Every haybail hit is a guaranteed same-frame one-shot kill (hp=10, `passThrough=true` — see `CluckAnimal.OnCollisionEnter2D`'s pass-through branch), so the old sound stack for one "pop" was: the chicken's own explicit `AudioManager.Play(Sound.WoodHit)` punch sound, PLUS `BlockBase.PlayHitSound()`'s generic `WoodHit` (fired unconditionally inside `TakeDamage()`), PLUS `BlockBase.DestroyBlock()`'s generic `BlockDestroy` — three sounds stacked on one moment. Fixed by: (1) deleting the chicken's own punch-sound line in `CluckAnimal.cs` entirely; (2) adding two new generic per-prefab override fields directly on `BlockBase` — `_silentHit` (skips `PlayHitSound()` when true) and `_destroyClipOverride` (an `AudioClip` that plays via a new `AudioManager.PlayClip()` instead of the generic `BlockDestroy` sound when set) — same shape as the existing `_stayKinematic`/`_sprExplode` per-prefab overrides, so `WoodBlock`/`StoneBlock` are unaffected (both fields default to false/null); (3) wiring both fields on `HaybaleBlock.prefab` in `SceneSetup.EnsureHaybaleBlockPrefab()` from `Assets/Audio/Haybail_Exploding.mp3`. Unlike `AudioManager.Play(Sound, cooldown)`, `PlayClip()` has no cooldown gate — the user explicitly wants this sound to fire every single time, never throttled.
- **Landing page SETTINGS icon removed entirely — 2026-07-07, user request.** `MainMenuController`'s gear-icon button and its Music/SFX toggle popup (`BuildSettingsPopup`, `OnSettingsClicked`, etc.) are gone, not hidden — there is currently no way to toggle audio from the landing page. The equivalent toggle is still reachable in-game via `HUDController`'s top-right Mute button (same `AudioManager.MusicEnabled`/`SfxEnabled` state).
- **World 1 Levels 1–4 are playable; L05–L06 are not.** `L02_StoneWall`, `L03_TheTower`, and `L04_EggPractice` were migrated 2026-07-27 to the current coordinate system (ground Y=−6.60, launcher X=−2.327) via a uniform rigid-translation delta (dx=+3.173, dy=−4.10) from the old pre-rebuild system (ground Y=−2.5, launcher X=−5.5) — see `docs/HISTORY.md` for the exact math and why a pure delta was safe (preserves ground-resting/stacked-block relationships exactly, so no free-fall glitch on first hit). `L05_TheFortress`/`L06_BessiesDebut` still use the old system and still spawn floating above the ground — same migration, not yet applied. This is now the single highest-leverage next step toward a fully playable World 1.
- **`assets/` (raw art source) is currently absent from disk** — all 236 files were deleted outside any Claude Code session, but remain fully tracked in git (`git checkout -- assets/` restores them). The user has chosen to leave it deleted for now. `python tools/remove_backgrounds.py` will not work until it's restored.
- **`unity/Assets/Sprites/` (processed game art) is gitignored and exists in exactly one place: this machine's disk.** Not tracked by git in any commit. If lost, the only recovery path is restoring `assets/` and re-running the full art pipeline.
- **Monetisation/backend: 0% built.** `unity/Assets/Scripts/Monetisation/` exists but is empty — no Firebase, Unity IAP, AdMob, RevenueCat, or UGS references anywhere. Expected — this is Phase 6, correctly sequenced after content.
- **No level validator exists** (GDD §04 calls for one — a physics-sim script checking stability/solvability before a level ships). The L02–L06 bug above would have been caught by this.
- **No "Perfect" star tier** — `ScoreManager` implements 1/2/3★ only; the GDD's 4th tier (3★ + all blocks destroyed) is not implemented.
- 6 World 1 levels exist of 18 required (4 tutorial, 8 build, 4 twist, 2 boss — no boss level yet). L07–L18 are unwritten.
- **No ground/grass visual ever existed for L01 — fixed 2026-07-26 with a placeholder.** `EnsureGround()`'s physics collider is deliberately invisible (top edge Y=−6.60), and no one had hand-authored a replacement, so the sky backdrop ran straight to the bottom of the screen. Symptom reported by the user: haybails/HarvesterRobot/a landing Cluck all looked like they were sinking or "falling through the floor" near the bottom of the screen. Root cause confirmed NOT a physics bug (Ground's collider/Rigidbody2D/layer-collision setup all check out) — the camera's visible range at rest (Y −6.5 to +2.5) already clips 0.1 units above the true ground surface, so anything settling near true ground level visually vanishes with nothing to anchor it. `SceneSetup.EnsureGroundVisual()` now creates a tinted placeholder strip (top edge Y=−5.3, matching where props already rest) to close that gap — replace with real ground/grass art when available, then delete `GroundVisual_Placeholder` from the scene.
- **`LevelSelectController` removed entirely 2026-07-26** — user-reported the old "SELECT LEVEL" grid screen kept appearing instead of/behind the Sunrise Meadows world map. Root cause: it subscribed to `GameManager.OnStateChanged` and showed itself on `GameState.Idle` using the exact same Canvas `sortingOrder` (300) `WorldMapController` uses — the two were silently racing to show themselves on every Idle transition ever since `WorldMapController` superseded it for World 1 (2026-07-15); it was never actually disabled, just left running unused in the background until now. The script, its `SceneSetup.EnsureLevelSelect()` wiring, and the leftover `LevelSelect` scene GameObject (auto-cleaned by `WireAll()` on next run) are all gone. World 2+ needs its own map screen built following `WorldMapController`'s pattern when that content exists — not a resurrected grid select.
- **Pause access restored 2026-07-26** via a new top-right 3-button row (Quit/Mute/Pause, real icon art) — see HUDController.cs entry above. Briefly removed earlier the same day alongside the top-centre score readout ("0" box, still gone) before being reinstated in this fuller form.
- **L01 haybale pile Y-shifted +1.0 on 2026-07-26** (user-reported: pile sat below the camera's visible safe line — bottom edge was at Y≈−6.73 against a visible-camera-bottom of Y=−6.5). New positions put the lowest bale's bottom edge at Y≈−5.73, matching where the other hand-placed props visually sit rather than the (mostly off-screen) true physics ground line. All 4 bales are `_stayKinematic` (see BlockBase.cs entry), so once placed they never move again regardless of being struck.
- **Level Complete/Failed celebration videos added 2026-07-07** — see the "Video Chroma Key / Celebration System" section below for the full architecture. **Ghosting bug fixed same day**: the chroma-key shader originally computed pure RGB-Euclidean distance to the key colour, which can't distinguish a desaturated dark pixel (character outline/shadow shading) from a mid-tone green — both sit numerically "close" to `#00B140` in flat RGB space. User-reported symptom: Cluck's own body looked like a translucent "ghost" instead of a solid character, not just the greenscreen going transparent. Fixed in `ChromaKeyVideo.shader` with a saturation gate — pixels less than half as saturated as the key colour are forced fully opaque regardless of raw RGB distance. Also added an explicit sky-backdrop layer (`VideoChromaKey._backgroundSprite`, wired to `SkyPainting.png`) behind the keyed video, since the transparent areas previously fell through to whatever the frozen gameplay camera happened to be rendering (busy level art), which read as messy — a clean sky gives the character a consistent surface to stand on regardless of which level is paused.
- **L01 top haybale re-lowered 0.35u + recentred on 2026-07-06** — user reported it "still too high and off the mark": it previously balanced on a single point directly above one base-row bale (X=4.029, Y=−4.497). Moved to X=3.935 (base row's mean X), Y=−4.85, nesting it into the row instead of perching on one corner. The internal single-bale-height stacking math (0.7365u gap, from Haybail.png's measured trimmed content) was previously verified and is unaffected by this change — see `LevelDataGenerator.cs`'s comment above the `Make("L01_FirstContact", ...)` call. Unverified visually in this environment (no Unity render access) — re-check against a fresh screenshot.

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
                              tracks so exactly one plays at a time: GameState.Idle (covers both the landing
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
                              holds 4s -> fades 0.3s -> Time.timeScale=1 -> HUDController.ShowLevelCompletePanel()
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
                              -> HUDController.ShowLevelFailedPanel() (now public, same decoupling as above).
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
                              — Bounce Roll / Triple Clone / Skip Shot / Rear Kick / Puff Up / Headbutt
                              Through respectively; scripted and ready, not yet used in any shipped level
    EggProjectile.cs           — layer 10; flat _damage=15 on first contact only
  Blocks/
    BlockBase.cs               — spawns Static; wakes ALL blocks on first TakeDamage() via
                              FindObjectsByType<BlockBase>() (Unity 6 API) — EXCEPT blocks with
                              _stayKinematic=true, which never transition to Dynamic (added 2026-07-26:
                              HaybaleBlock sets this — hitting one haybale in a cluster used to wake and
                              visibly shift/tumble all of them, since the wake call is global, not
                              per-instance; now an untouched neighbour never physically moves). health =
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
                              independently drives POWER: LaunchVelocity() speed _minLaunchSpeed=3.0-
                              _maxLaunchSpeed=6.0 m/s (both [SerializeField] — _maxLaunchSpeed already existed
                              serialized in Game.unity at a stale 16.8 from an even earlier, never-wired
                              design; fixed to 6 directly in the scene YAML alongside adding the new
                              _minLaunchSpeed=3, per the [SerializeField] stale-value trap below). Numerically
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
    HUDController.cs                 — Canvas built at runtime; card widgets anchored top-left (anchorMin
                              0.02,1); orange N damage badge (enlarged + restyled with StyleAsGameNumber()
                              2026-07-26, enlarged again 2026-07-06 — user reported it was still too small
                              to read, especially on the smaller queued cards; badge now 92x44/text 34f on
                              the active card, 74x36/text 26f on queued cards, up from 72x34/24f and
                              56x28/18f. Leading "⚡" dropped the same day — LiberationSans SDF (the only
                              font asset this project ships) has no glyph for U+26A1, rendering it as a
                              tofu box that read as "a square before the numbers"; same class of bug as
                              the missing ★ glyph fixed in MatchUpScreen (docs/HISTORY.md Round 14) — same
                              bold/black-outline/gold-orange-gradient treatment as the
                              Level Complete/Failed score numbers, per user request to "match the game
                              font"); Level Complete/Failed/Pause panels; SafeArea RectTransform wrapper
                              (Screen.safeArea-driven) for the bird queue.
                              **Top-centre score readout ("0" box) REMOVED 2026-07-26** (user request — read
                              as an unstyled placeholder rectangle over the sky art); `BuildScoreDisplay()`
                              and its fields are gone entirely, not hidden. Score is still shown on the Level
                              Complete/Failed panels via their own score text.
                              **Top-right button row (`BuildTopRightButtons`, added later the same day)**
                              replaces the single plain-grey-circle pause button that briefly got removed in
                              the same pass as the score readout above. Three real icon buttons, top-right
                              corner, 150x150 each (enlarged from 64x64 2026-07-06 — user-reported too small;
                              now matches MainMenuController's PLAY/SETTINGS button size) with a 14px gap,
                              order left-to-right Quit/Mute/Pause (Pause flush in the screen corner). **Quit
                              is sized up to 187.5x187.5 (`quitBtnSize = btnSize / 0.80f`), not the shared
                              150x150** (2026-07-06, user-reported the three icons "don't look like they're
                              the same size") — `Btn_quite.png`'s drawn button only fills ~87%x80% of its
                              own 256x256 canvas (pixel-measured content bbox 222x205), unlike
                              `Btn_pause.png`, whose art fills its canvas edge-to-edge (256x256), so
                              rendering both into an identical box left QUIT visibly smaller; the position
                              math (`pos`) still uses the shared `btnSize+gap` grid since QUIT's right-edge
                              anchor doesn't move, it only grows further left, away from Mute:
                              **Pause** (`Btn_pause.png` <-> `Btn_play.png`, swapped not tinted) calls
                              `OnPauseClicked()` -> `SetPaused()`, which just flips `Time.timeScale` 0/1 and
                              re-derives its own icon via `RefreshTopPauseIcon()` — no popup opens (the old
                              PAUSED panel was removed entirely 2026-07-06, see below). **Mute**
                              (`Btn_music.png` <-> `NoSound.png`, sprite-swapped) toggles BOTH
                              `AudioManager.MusicEnabled` and `SfxEnabled` together via
                              `OnTopMuteToggleClicked()`; `RefreshTopMuteIcon()` re-derives which sprite to
                              show from actual AudioManager state (not a locally-tracked flag) so it stays
                              correct even if the Main Menu SETTINGS popup's separate toggles are used
                              instead. **Quit** (`_quitButtonSprite`) no longer closes the app — since
                              2026-07-06 (user request) `OnQuitClicked()` un-pauses if needed and calls
                              `GameManager.LoadMenu()` + `WorldMapController.SkipToMainMenu()`, the same
                              "land directly on the main menu, skip the world map flash" pattern the Level
                              Complete/Failed Home buttons use. The whole row (`_topRightRoot`) hides via
                              `SetTopBarVisible` alongside the bird queue whenever a full-screen end-of-level
                              panel is up, same as the old score readout used to. `_birdQueueRoot` was
                              actually missing from that toggle until earlier the same day (user-reported
                              leftover animal cards
                              still visible top-left behind the Level Complete panel) — `_topRightRoot` was
                              added to it from the start, so it doesn't repeat that bug.
                              **Level Complete/Failed panels (2026-07-25 redesign, Level Complete
                              restructured again 2026-07-26)** both use Scoreboard.png
                              (Assets/Sprites/UI/MatchUp/) as the whole backdrop instead of a plain coloured
                              box (Level Complete's box enlarged 620x363 -> 680x398 2026-07-26, same 653:382
                              native aspect, to give the star row more headroom inside the board's parchment
                              area), with LevelComplete.png/LevelFailed.png as a sign-topper title
                              overlapping the board's top edge. Falls back to the old plain box/text-button
                              look if any sprite is unwired, so neither panel ever renders blank. Score
                              number uses StyleAsGameNumber() — bold + black outline + gold-to-orange vertex
                              gradient on the default TMP font, the closest approximation of the game's own
                              bubble-lettering achievable without importing a new font asset (this project
                              ships only LiberationSans SDF). Level Complete shows 3 real ScoreStars.png
                              images (dropped from 4 2026-07-26) — all three are now the actual star rating,
                              no forced always-pops slot. The old 4th "always pops, reveals a LEVEL UP! text
                              label" slot and the separate Btn_play icon were both removed in the same pass;
                              Btn_play's bottom-left slot is now a single _levelUpStarSprite (Levelup.png —
                              "LEVEL UP" baked into the art, no text label needed) that pulses continuously
                              (LevelUpStarPulse(), scale 1<->1.12 sine wave, starts in ShowLevelCompletePanel/
                              stops in HideLevelCompletePanel) and fires the exact same handler Btn_play used
                              to (OnLevelCompletePlayClicked -> GameManager.LoadMenu()) — it's now the
                              panel's one obvious, animated tap target back to the world map, rather than a
                              static icon next to a separate forced-star/text reveal. Btn_home is unchanged
                              (bottom-right, mirrors the level-up star's position/size). Level Complete's
                              level-up star calls GameManager.LoadMenu() and relies on WorldMapController's
                              own OnStateChanged listener to show itself and slide the position indicator to
                              the newly-unlocked next level (no extra plumbing needed — see WorldMapController
                              below); its Btn_home and Level Failed's Btn_home both call the new
                              WorldMapController.SkipToMainMenu() so tapping Home lands directly on the main
                              menu instead of flashing the world map first.
                              **PAUSED popup REMOVED ENTIRELY 2026-07-06** (user request — a separate
                              RESUME/RESTART/MENU/QUIT/MUSIC/SFX card popping up over the game was more
                              friction than it was worth once Quit and Mute already had their own dedicated
                              top-right buttons, below). `BuildPausePanel()`/`ShowPausePanel()`/
                              `HidePausePanel()`/`MakePanelButton()` and the panel's own
                              Resume/Restart/Menu/Music/SFX click handlers are all gone, not hidden. Tapping
                              the top-right Pause button now pauses/resumes directly (`SetPaused()` still
                              just flips `Time.timeScale` 0/1) and swaps its own icon between
                              `Btn_pause.png` and `Btn_play.png` (reusing `_playButtonSprite`, the same
                              sprite the Level Failed panel's Try Again button uses) via
                              `RefreshTopPauseIcon()`, so the icon itself always shows the next tap's
                              action instead of opening a menu. Restart/return-to-menu are still reachable
                              via the Level Failed panel's Try Again/Home buttons — only in-pause access to
                              them was removed, not the underlying `GameManager.RestartLevel()`/`LoadMenu()`
                              calls, which both panels still use.
                              **Panel triggers decoupled from OnStateChanged 2026-07-07**:
                              `ShowLevelCompletePanel()`/`ShowLevelFailedPanel()` are now `public` and no
                              longer called from this class's own `OnStateChanged` on the LevelComplete/
                              LevelFailed transitions — `LevelCompleteManager`/`LevelFailedManager` (see
                              Core/) call them directly once their slow-motion -> freeze -> celebration/taunt
                              video -> fade sequence finishes, so the panel is always the reward/defeat
                              beat's *second* step, never the first. `OnStateChanged` still handles
                              `HideLevelFailedPanel()`/`HideLevelCompletePanel()`/`SetTopBarVisible(false)`
                              on those same transitions, just not the show calls.
                              **Score count-up added 2026-07-07**: `ShowLevelCompletePanel()` now animates
                              the score text 0 -> final over `ScoreCountUpDuration` (1.6s, unscaled time),
                              timed to land right as the last star pop finishes (staggered 0.3/0.75/1.2s + a
                              0.38s pop each, see `AnimateStars`/`PopStar` above) — previously the score text
                              was set instantly.
                              **RETRY button pulse added 2026-07-07**: `ShowLevelFailedPanel()` now starts
                              `RetryButtonPulse()` on the Try Again button (`_lfTryAgainRT`) — scale eases
                              `1.0 -> 1.05 -> 1.0` on an 0.8s loop via `1 + 0.025*(1-cos(2πt))`, deliberately
                              *never* dipping below 1.0 (unlike the Level Complete level-up star's symmetric
                              ±0.12 `LevelUpStarPulse`) — meant to read as an inviting nudge ("come on, you
                              can beat that robot"), not a wobble. Stops and resets to scale 1 in
                              `HideLevelFailedPanel()`.
    MainMenuController.cs             — LandingPage.png background (title/character art baked in); PLAY
                              (bottom-left) and SETTINGS (bottom-right), both corner-anchored; PLAY opens
                              WorldMapController; SETTINGS opens a Music/SFX popup sharing AudioManager state
                              with the top-right Mute toggle (HUDController's `OnTopMuteToggleClicked`) —
                              same persisted state, no separate pause-menu toggle exists any more
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
                              art fields directly (see note below on why). Public SkipToMainMenu()
                              (2026-07-25) — called by HUDController's Level Complete/Failed Home buttons:
                              HidePanel() then MainMenuController.Show(). Needed because LoadMenu() always
                              transitions GameState to Idle, which this class's own OnStateChanged reacts to
                              by showing itself (it's the PLAY destination) — SkipToMainMenu() runs
                              immediately after in the same call stack, hiding the map again before a frame
                              ever renders it, so "Home" lands on the main menu instead of flashing the map.
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
                              play simultaneously. `Play(clip, audioClip)`/`FadeOut(duration)`/`Stop()` are
                              the only playback API; owns an `AudioSource` for the accompanying one-shot
                              laugh/taunt sound, started and faded in lockstep with the video. The
                              `Background` `Image` (sky art, sits behind the `RawImage` in sibling/render
                              order) is shown/hidden and faded together with the video too — see the
                              ghosting-bug Known Issues entry above for why it exists.

unity/Assets/Editor/
  SceneSetup.cs           — FarmFury > Wire Scene References; wires all Inspector refs; sets camera
                              (0,0,-10) orthoSize=4.5; launcher at (-2.327,-6.60,0); creates/wires FarmCannon
                              at (-3.0012,-5.1223,0) scale (1.4711188,1.3868444,1); ground center
                              (0,-6.85,0) scale (60,0.5,1) -> top at Y=-6.60 (physics-collider-only — no
                              longer generates visual ground layers; deletes leftover code-generated
                              GroundFill/GrassBase/etc. from older runs; ground/grass visuals are meant to be
                              user-authored scene GameObjects, but none ever were for L01 — see
                              EnsureGroundVisual() below and the Known Issues entry on this). Also creates
                              **GroundVisual_Placeholder** (2026-07-26) — a tinted stand-in strip, top edge
                              Y=-5.3 (matching where hand-placed props already visually rest) down to Y=-12,
                              sortingOrder=-1, filling the gap left by the invisible physics-only ground so
                              nothing near ground level reads as "sinking into nothing." Delete this
                              GameObject once real ground/grass art replaces it — EnsureGroundVisual() only
                              (re)creates it if missing.
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

**Level start:**
`GameManager.ForceStartLevel(idx)` (Editor play, and Replay/Restart) or `GameManager.StartLevel(idx)` (menu, full scene load) → `TransitionTo(Playing)` → fires `OnLevelStarted` → `LevelLoader.HandleLevelStarted(data)` → `LoadLevel()` spawns blocks/robots/birds → `ScoreManager.InitLevel()` resets counters → `CatapultLauncher` loads the first bird → `SnapCameraToRest()`.

**Level complete:**
All robots destroyed → `LevelLoader.NotifyRobotDestroyed()` → `_spawnedRobots.Count == 0` → `DelayedLevelComplete()` (2s wait) → `ScoreManager.FinaliseLevel()` → `GameManager.CompleteLevel()` → `TransitionTo(LevelComplete)` → `LevelCompleteManager.HandleStateChanged` runs the celebration sequence (slow-motion → freeze → `CatapultLauncher.LastAnimalUsed`'s video+audio via the shared `VideoChromaKey` → fade) → only then calls `HUDController.ShowLevelCompletePanel()`. `HUDController`'s own `OnStateChanged` no longer shows the panel directly on this transition (see HUDController.cs above).

**Level failed:**
`CatapultLauncher` detects no more birds and calls `LevelLoader.NotifyBirdsExhausted()` → `DelayedLevelFailed()` (1.5s wait) → `GameManager.FailLevel()` → `TransitionTo(LevelFailed)` → `LevelFailedManager.HandleStateChanged` runs the taunt sequence (slow-motion → freeze → the current level's robot taunt video+audio via the same shared `VideoChromaKey` → fade) → only then calls `HUDController.ShowLevelFailedPanel()` (same decoupling as above).

**Replay / Try Again / Restart:** all route through `GameManager.RestartLevel()` → `ForceStartLevel(CurrentLevelIndex)` — the same no-reload path Editor play uses, not a full `SceneManager.LoadScene()`.

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
  desaturated character shading for green in the first place.
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
  `HUDController.ShowLevelCompletePanel()`/`ShowLevelFailedPanel()`. Both self-bootstrap onto their
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
- **Phase 2 — UI/UX Shell:** ✅ done (HUD, Level Complete/Failed panels, pause/resume toggle, Sunrise Meadows world map, main menu).
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
