# FarmFury — Unity Project

## Overview
Physics destruction game (Angry Birds style). Unity 6.5 (6000.5.0f1), URP 2D.
Ported from a validated Phaser.js prototype at `C:\Users\Personel\Desktop\FarmFury\index.html`.

## Stack
- **Engine:** Unity 6.5, Universal Render Pipeline 2D
- **Physics:** Unity Physics 2D (Rigidbody2D, BoxCollider2D, CircleCollider2D)
- **Packages:** Cinemachine, Input System, TextMeshPro (built-in)
- **Language:** C# (.NET 10, Unity 6)
- **Repo:** https://github.com/tenbucksmobile-png/farmfury (push via `git subtree push --prefix=FarmFury farmfury main` from `C:\Users\Personel`)

## Project Location
`C:\Users\Personel\Desktop\FarmFury\unity`

The project is tracked in the git repo at `C:\Users\Personel\Desktop\FarmFury`.
All work (Phaser prototype + Unity project) lives under that single repo root.

## Physics Settings
- Gravity Y: -20
- Default Contact Offset: 0.01
- Layers: Ground=6, Animal=7, Block=8, Robot=9, Egg=10

## Physics Values (from prototype)
- CluckAnimal: mass=8, bounciness=0.4, linearDrag=0.008
- BessieAnimal: mass=28, bounciness=0.15, linearDrag=0.016
- WoodBlock: baseMaxHealth=20, baseMass=5, bounciness=0.2
- StoneBlock: baseMaxHealth=50, baseMass=8, bounciness=0.1
- RobotEnemy: maxHealth=35
- Impulse damage multiplier: 2.5, minDamageImpulse: 2
- Launch velocity starting point: ~17 units/s (prototype MAX_VEL=14 px/frame ÷ 50px/unit × 60fps)

## Scoring (matches prototype)
- Robot: +1000 pts
- Wood block: +100 pts
- Stone block: +200 pts
- Egg hit: +50 pts
- Bird remaining bonus: +500 pts each
- Stars: 1=all robots dead, 2=all dead+1 bird left, 3=all dead+2+ birds left
- Persisted via PlayerPrefs keys: `ff_score_N`, `ff_stars_N`

## Scene Structure
```
Bootstrap.unity  — GameManager (DontDestroyOnLoad)
MainMenu.unity   — level select
Game.unity       — main gameplay scene
  ├─ Main Camera
  ├─ Global Light 2D
  ├─ GameManager    (GameManager.cs)
  ├─ LevelLoader    (LevelLoader.cs)
  ├─ ScoreManager   (ScoreManager.cs)
  └─ Ground         (Ground prefab, tag="Ground", layer=Ground)
```

## Script Architecture
```
Core/
  GameManager.cs      — singleton, DontDestroyOnLoad, game state machine
                        States: Idle / Playing / LevelComplete / LevelFailed
Level/
  LevelData.cs        — ScriptableObject; also JSON-deserializable via FromJson()
  LevelLoader.cs      — reads LevelData, instantiates prefabs, owns robot/bird queues
Animals/
  AnimalBase.cs       — abstract; kinematic until Launch(), single ability on click
  CluckAnimal.cs      — Cluster Bomb: 5 eggs in 120° spread on click
  BessieAnimal.cs     — Ground Slam: vy-18 on click, shockwave 3.6u radius on landing
Blocks/
  BlockBase.cs        — abstract; static on spawn, wake-all on first TakeDamage()
  WoodBlock.cs        — HP=20, mass=5
  StoneBlock.cs       — HP=50, mass=8
Enemies/
  RobotEnemy.cs       — HP=35, impulse damage, notifies LevelLoader on death
Scoring/
  ScoreManager.cs     — singleton, PlayerPrefs persistence
```

## Key Implementation Notes
- Blocks spawn as `RigidbodyType2D.Static` — first `TakeDamage()` wakes ALL static blocks
- `WakeAllStaticBlocks()` uses `FindObjectsByType<BlockBase>(FindObjectsInactive.Exclude)` (Unity 6 API)
- Collision effMass: both dynamic → reduced mass formula; one static → movingBody.mass × 0.6
- Animals start `Kinematic`, switch to `Dynamic` on `Launch(velocity)`
- `GameManager` listens on `OnLevelStarted` event; `LevelLoader` subscribes in `OnEnable`
- Ground must have tag `"Ground"` for BessieAnimal shockwave to trigger
- `DontDestroyOnLoad` object appears as separate root in Hierarchy during play mode

## Prefabs Built
```
Prefabs/
  Animals/   CluckAnimal, BessieAnimal
  Blocks/    WoodBlock (brown), StoneBlock (grey)
  Enemies/   Robot (dark grey)
  Environment/ Ground (green, static)
```

## Prefabs Still Needed
```
  Animals/   Egg
  Launcher/  Catapult
  VFX/       WoodFragment, StoneFragment, ShockwaveRing
  UI/        HUD, LevelCompletePanel, LevelFailedPanel
```

## Scripts Still Needed
```
  Launcher/  ILauncher.cs, CatapultLauncher.cs
  Core/      CameraShake.cs, SceneLoader.cs
  Animals/   EggProjectile.cs
  UI/        UIManager.cs, HUDController.cs
```

## Next Steps
1. Create first LevelData ScriptableObject (L01 First Contact)
2. Wire LevelLoader prefab references in Inspector
3. Place WoodBlock + Robot in Game scene to test physics
4. Build CatapultLauncher script and prefab
5. Wire up camera follow via Cinemachine

## Coordinate Conversion (Prototype → Unity)
- 1 Unity unit = 50 Phaser prototype pixels
- x_unity = x_phaser / 50
- y_unity = -(y_phaser - 770) / 50  (flip Y, offset from ground)
- Ground in Unity at Y = -5 (world), matches prototype ground at y=770
