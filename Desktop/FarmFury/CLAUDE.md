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
- 2★ — all dead + 1 bird remaining (mastery)
- 3★ — all dead + 2+ birds remaining (prestige)

---

## Development Roadmap (6 Phases)

### Phase 1 — Core Feel *(current)*
1.1 **Slingshot fix** — drag the BIRD backward (click bird → pull back → release), not drag-from-tip. **CURRENT TASK.**
1.2 Trajectory arc — dotted style, visible only while dragging, fades at midpoint on harder levels
1.3 Destruction feedback — block crack states (3 stages), block burst on death, robot flash+explode, screen shake
1.4 Audio — procedural sounds: launch, wood impact, stone impact, robot death, win fanfare, fail buzzer
1.5 Camera — smooth follow → smooth pan-back to launcher after landing

### Phase 2 — UI/UX Shell
2.1 HUD: score (top-center), bird queue (left, animated), pause button
2.2 Level Complete panel: animated star pop-in (1→2→3 with bounce), score, next/replay
2.3 Level Failed panel: try again / menu
2.4 Pause menu: resume/restart/menu, music+SFX toggles
2.5 Level select: scrollable grid, star counts, locked/unlocked states
2.6 Main menu: logo, play button, animated farm background

### Phase 3 — Character Roster
Add remaining 6 animals in GDD world-introduction order:
Percy → Woolly → Ducky → Horace → Gerald → Billy

### Phase 4 — World 1 Completion
All 18 Meadow Ruins levels + JSON level generator + validator + environment art + Robot Commander boss

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

### File Order (class order matters)
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
Open `unity/` in Unity Hub (Unity 6.5 / 6000.5.0f1). Open `Assets/Scenes/Game.unity`. Press Play. If nothing loads, run **FarmFury → Wire Scene References** from the top menu first.

### Stack
- Unity 6.5, URP 2D, Physics2D, New Input System, TextMeshPro

### Coordinate Conversion
- 1 Unity unit = 50 Phaser pixels
- `x_unity = x_phaser / 50`
- `y_unity = -(y_phaser - 770) / 50`
- Ground at Y = −5 (Unity world). Trebuchet base at (11.2, 0, 0).

### Physics Settings
- Gravity Y: −20. Layers: Ground=6, Animal=7, Block=8, Robot=9, Egg=10

### Script Architecture
```
Core/
  GameManager.cs      — singleton (DontDestroyOnLoad); states: Idle/Playing/LevelComplete/LevelFailed
                        ForceStartLevel(int) boots a level without LoadScene (used for direct Editor play)
Level/
  LevelData.cs        — ScriptableObject; JSON-deserializable via FromJson()
  LevelLoader.cs      — instantiates prefabs; owns bird queue (_birdQueue); TryConsumeBird / PeekNextBird
Animals/
  AnimalBase.cs       — abstract; Kinematic until Launch(); tap ability via Mouse.current (New Input System)
  CluckAnimal.cs      — 5-egg cluster bomb
  BessieAnimal.cs     — ground slam + shockwave (3.6u radius)
  EggProjectile.cs    — damage on contact, layer 10
Blocks/
  BlockBase.cs        — abstract; spawns Static; wakes ALL blocks on first TakeDamage()
  WoodBlock.cs / StoneBlock.cs
Enemies/
  RobotEnemy.cs       — HP=35, impulse damage
Scoring/
  ScoreManager.cs     — singleton, PlayerPrefs persistence
Launcher/
  CatapultLauncher.cs — drag-to-aim, trajectory preview, fire, arm animation, camera follow
Editor/
  SceneSetup.cs       — FarmFury > Wire Scene References (auto-wires all Inspector refs)
```

### Key Implementation Rules (Unity)
- **Input System ONLY:** `using UnityEngine.InputSystem;` — `Mouse.current.leftButton.wasPressedThisFrame`, `.isPressed`, `.wasReleasedThisFrame`. `mouse.position.ReadValue()` → `Vector2`. `UnityEngine.Input` is incompatible with this project's Player Settings.
- Blocks spawn `RigidbodyType2D.Static`; first `TakeDamage()` calls `WakeAllStaticBlocks()` which uses `FindObjectsByType<BlockBase>()` (Unity 6 API)
- Animals start Kinematic → Dynamic on `Launch(velocity)`
- Never destroy physics body in collision callback — defer with coroutine
- `body.blockRef` / `body.robotRef` must be stamped AFTER `setCircle()` (which replaces the body)

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
Prefabs/Animals/    CluckAnimal, BessieAnimal, Egg
Prefabs/Blocks/     WoodBlock (brown), StoneBlock (grey)
Prefabs/Enemies/    Robot (dark grey)
Prefabs/Environment/ Ground (green, static)
```

### Adding a New Animal
1. Create class extending `AnimalBase`; override `Awake()` (set colour/radius/mass), `TriggerAbility()`
2. Add prefab to `Prefabs/Animals/`
3. Add `AnimalType` enum value to `LevelData.cs`
4. Handle in `LevelLoader.CreateNextAnimal()`, `CatapultLauncher.NextBirdFA()`
5. Run **FarmFury → Wire Scene References**

### Adding a New Level
Add `LevelData` ScriptableObject in `Assets/ScriptableObjects/Levels/`. Run **Wire Scene References** to add to GameManager's `_levels` array.

```
Y convention: Ground = 0 in Unity. Robot center (h=0.8) → y=0.4.
              Wood center (h=0.4) → y=0.2. Stack upward by block height.
X convention: Structure zone in Unity coords ≈ 12–18 (600–900 px equivalent).
```
