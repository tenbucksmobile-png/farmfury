# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

---

## Project Overview

**Farm Fury** is an Angry Birds-style physics destruction game. Farm animals launch from a trebuchet to wreck robot fortresses and reclaim the farm.

The repo contains two parallel codebases:
- `index.html` — **Phaser 3.60 prototype** (frozen at Prompt 12, kept as a reference/physics-validation artifact only — not under active development)
- `unity/` — **Unity 6.5 URP 2D port** (active development target for iOS/Android release)

This root CLAUDE.md is the single source of truth. (A duplicate/contradictory `unity/CLAUDE.md` existed with a stale, pre-rebuild coordinate system and script list — it was deleted 2026-07-01; see Audit Findings below.)

---

## Audit Findings (2026-07-01)

A full audit was run against this file, the GDD, and the actual project state. Corrections below are now folded into the relevant sections throughout this file — this block exists as a standing summary so the history isn't lost. Re-verify anything here before relying on it if significant time has passed.

**Critical bug — L02–L06 spawn floating in mid-air.** `LevelDataGenerator.cs` still carries a header comment for the *pre-rebuild* coordinate system (`ground = -2.5`, `launcher X = -5.5`) and L02–L06 were generated under that system (block/robot Y ≈ −1.5 to −2.3). The live scene was recalibrated to `ground Y = −6.60`, `launcher X = −2.327` (see Phase 4), but **L02–L06's baked Y values were never migrated** — `LevelLoader.SpawnBlock`/`SpawnRobot` place objects at the raw stored position with no offset applied, so these five levels currently spawn their entire structure ~4.4–4.9 units above the actual ground, disconnected from it, and their X positions are calibrated to the old launcher position rather than the current one. **Only L01 (hand-edited directly in the asset, not via the generator) uses correct current-system coordinates.** L02–L06 need their baked positions recalculated (roughly: old Y − 4.1, and X re-checked against the new launcher at X=−2.327) before they're playable. Until fixed, treat L02–L06 as non-functional despite existing as assets.

**`LevelDataGenerator.cs` L01 landmine — RESOLVED 2026-07-01.** The generator's L01 entry was rewritten to match reality and extended with a 6-bale hay-pile obstacle (see "L01 gameplay pass" below); it's now safe to run **FarmFury → Generate All Level Data** again — it reproduces the current L01 design instead of destroying it. L02–L06 are untouched and still on the old coordinate system (see bug above) — regenerating will not make them worse, but won't fix them either.

**L01 gameplay pass — 2026-07-01.** Fixed two real bugs found while wiring up the drag/aim/pass-through mechanics against the user's request:
1. `CatapultLauncher.DrawTrajectory()` was fully implemented but **never called** — the dotted aiming-arc preview (`_trajDotRenderers`, built in `Awake()`) never actually drew during drag. Fixed: now called from the drag-hold branch of `HandleInput()`, and its start position corrected from the fixed rest `_launchPoint` to `BucketWorldPos(_dragAngle)` (the actual point `Fire()` spawns the animal from).
2. `LevelLoader.SpawnBlock()` never applied `BlockSpawnData.passThrough` / `.healthOverride` / `.massOverride` to the spawned block — `WoodBlock.cs`'s own comment claimed LevelLoader did this; it didn't. This silently broke the Cluck pass-through mechanic for any level using it. Fixed: added `BlockBase.ApplyOverrides(health, mass)`, called from `SpawnBlock()` along with setting `WoodBlock._passThrough` directly.

Added a new `BlockType.Haybale` (enum value 2, `LevelData.cs`) and a `HaybaleBlock.prefab` — mechanically identical to `WoodBlock` (same component, `SceneSetup.EnsureHaybaleBlockPrefab()` creates it if missing), just wired to render `Assets/Sprites/Environment/World1Props/Haybail.png` instead of the generic wood-plank art. **No new art was needed** — `Haybail.png` was already imported (used for the decorative scenery), and `BlockBase`'s destruction visuals (procedural crack overlays at 50%/25% health, tint shift, 4 fading fragments) are entirely sprite-agnostic, so haybale destruction "just works" the same as any other block once the sprite is wired.

**L01 now has an actual obstacle**, not just a bare robot: 4 haybale blocks (`passThrough=true`, `healthOverride=60`, `massOverride=3` — lighter/weaker than standard wood so the tutorial stays easy). Positions were originally guessed, then corrected in the round-2 pass below to match the user's real hand-placed decorative haybales exactly. Requires the user to run **FarmFury → Wire Scene References** once (materializes `HaybaleBlock.prefab` and wires `_haybalePrefab` into `LevelLoader`) before it'll spawn correctly — that menu item wasn't run this session since the project was locked by the user's own open Editor instance.

**Visual-feedback round 2 — 2026-07-01, from user screenshot.** Play-test report: HUD cards overlapping the screen edge, trebuchet arm launch looking wrong, no visible Cluck in flight, HarvesterRobot floating in mid-air instead of nestled behind the hay, plus a request to use the real hay-pile positions instead of the guessed ones. Diagnosed and fixed by reading `Game.unity` directly (still no render/play-test access from this side):
- **Robot floating — root cause found.** `RobotEnemy.Awake()` sets `BoxCollider2D.size=(1,1)`, sized for the default `(0.6,0.9)` scale. L01's HarvesterRobot uses a custom `(4.3565,4.69371)` visual scale for a bigger, more imposing enemy, but nothing shrunk the collider to compensate — it inherited the full scale, becoming a ~4.36×4.69-unit hitbox that overlaps the ground (Y=−6.60) by roughly 2 units at spawn. Unity's physics forcefully separates deep overlaps on the first frame, launching the robot upward. Fixed in `LevelLoader.SpawnRobot()`: when a custom scale is applied, the collider size is now re-derived (`0.6/scaleX, 0.9/scaleY`) to keep the world-space hitbox pinned at the original 0.6×0.9 regardless of visual scale.
- **Trebuchet likely invisible — root cause found.** The hand-placed `Trabuchet_Body`/`Trabuchet_Arm`/`Trabuchet_Swing` scene GameObjects were never given a `sortingOrder` (default 0), tied with Ground/scenery (also ~0). `Trabuchet_Body` additionally sits at `Z=2`, deeper than Ground's `Z=0`. Unity's default 2D sort falls back to camera distance on sortingOrder ties, so the trebuchet was very likely rendering behind the terrain. Fixed in `SceneSetup.WireLauncher()`: now sets sortingOrder 3/4/5 (body/arm/swing) — matching the values `BuildTrebuchetBody()`'s code-driven creation path already used — and flattens Z to 0. Self-heals on the next **Wire Scene References** run.
- **Hay pile repositioned to the user's real placement.** Read the exact transforms of the 4 hand-placed decorative `Haybail`/`Haybail (1-3)` GameObjects directly from `Game.unity` (positions ~X 3.65–4.31, Y −4.88 to −5.65) and used those exact coordinates for the gameplay `HaybaleBlock` entries in both `L01_FirstContact.asset` and `LevelDataGenerator.cs`, replacing the earlier guessed grid. **The 4 decorative scene GOs should be deleted** once this is live — the gameplay blocks render the same `Haybail.png` art at the same spots, so keeping both doubles them up for no visual gain.
- **HUD safe-area gap.** Score/bird-queue/pause elements were anchored to the raw screen rect with no accounting for device notches/rounded corners. Added a `SafeArea` RectTransform wrapper (`Screen.safeArea`-driven) in `HUDController.BuildCanvas()` that these three now anchor inside. Note: this has no visible effect in a plain Editor Game view or a decorative phone-frame mockup image — it only matters when `Screen.safeArea` actually reflects inset simulation (a real device or Unity's Device Simulator).
- **Trebuchet arm position/scale not sticking in the Scene view — by design, not a bug.** `CatapultLauncher.DrawArmAt()` forcibly repositions `_armSpriteGO`/`_swingSpriteGO` to the pivot-derived position every frame at runtime (`Start()` calls it immediately). Manually dragging `Trabuchet_Arm`/`Trabuchet_Swing` in the Scene view while **not** in Play mode is edit-time-only reference positioning — it has no effect on Play mode. The values that actually drive runtime arm geometry are the `private const` fields at the top of `CatapultLauncher.cs` (`_pivotHeight`, `_armLongLength`, `_armShortLength`, `_armRestAngle`, `MaxLoadAngle`, `BucketFromPivot`) and the sprite's import pivot (`Trabuchet_Arm.png`). Adjust those, not the Transform.

**Visual-feedback round 3 — 2026-07-01, from 3 more user screenshots.** User: "stop guessing." Diagnosed by pixel-measuring the actual sprite art (not estimating from the code's stated assumptions) and by numerically integrating the actual flight physics (not eyeballing). Still no live render/play-test access.
- **Arm/swing pivots were genuinely wrong — precisely re-measured and fixed.** Cropped and pixel-inspected `Trabuchet_Arm.png` (183×203px): the bolt hole (where the arm hinges to the body) is centred at pixel (41, 29), i.e. sprite pivot **(0.224, 0.857)** — not the previous guessed (0.40, 0.56), which sat in the middle of the beam and made the body-mounted end visibly swing away from the body on every drag ("arm detached from body"). `Trabuchet_Swing.png` (512×512px) had **no custom pivot at all** (default centre 0.5/0.5) despite `CatapultLauncher.DrawArmAt()` positioning it at the arm tip every frame — since the sprite is a rope-hanging-a-bucket with the rope's top (its true attachment point) at pixel (222, 88), a centre pivot put half the rope floating above the tip with a visible gap ("swing detached from arm"). Fixed to pivot **(0.434, 0.828)**. Both self-heal on the next **Wire Scene References** run (`SceneSetup.WireLauncher()`).
- **Trajectory arc was landing at X≈7-9, not X≈5.16 as the old code comment claimed.** Numerically integrated `LaunchVelocity()`'s actual flight (gravity=-8, drag=0.008, matching `DrawTrajectory()`'s own substep loop) rather than trusting the stale comment. The old 6-9 m/s / 45°-22° range put max-power landings at X≈7.1-9, well past the robot, while reading as a flat line — both the "too low" complaint and the miscalibration were real. Recalibrated to speed **6.0-7.0 m/s**, angle **48°-42°** (both ends stay high, always visibly arched): min pull now lands ~X 3.1 (short of the hay), max pull lands ~X 4.9 (on the robot).
- **HarvesterRobot moved from X=5.15509 to X=4.8** — the old position was at or past the edge of the camera's visible frame ("robot off screen"); the new position is also a tighter, more visually "nestled behind the hay" placement and matches the recalibrated trajectory's landing spot.
- **Haybale HP dropped from 60 to 10** — a typical Cluck impact does ~15-20 impulse damage, so 60 HP was surviving multiple hits (showing damage tint but not exploding) instead of the intended one-hit kill. 10 reliably one-shots it off any real hit.
- **HarvesterRobot HP raised from 35 (class default) to 40**, set specifically on `HarvesterRobot.prefab` only (not the shared `RobotEnemy` class default, which would've affected the plain `Robot.prefab` used elsewhere too). With the recalibrated (slower) trajectory, a hay-clearing pass-through hit now deals ~24-28 damage — at the old 35 HP that left the robot on ~7-11 HP, technically not one-shot but fragile enough to read as instant death from any stray contact. 40 HP leaves a safer ~12-16 HP margin after the first hit, while a second solid hit (~34-40 direct) still reliably finishes it, matching L01's par=2.

**Trebuchet → Farm Cannon visual swap — 2026-07-02.** User-requested visual-only replacement; aiming math (drag detection, `LaunchVelocity()`, `DrawTrajectory()`'s physics) explicitly required to stay unchanged. `CatapultLauncher.cs` was substantially rewritten:
- **Removed:** `DrawArmAt()`, `ArmSnap()` coroutine, `BuildTrebuchetBody()`, `UpdateCounterweight()`, `ShortArmEnd()`, `BucketWorldPos()`; the `_trebuchetBodySprite`/`_trebuchetArmSprite`/`_trebuchetCounterweightSprite`/`_armSpriteGO`/`_swingSpriteGO`/`_counterweightGO`/`_armLine` fields; the counterweight-pendulum state (`_cwAngle`/`_cwVelocity`/`_currentArmAngle`/`_prevArmAngle`/`CwRopeLen`/`CwGravity`/`CwDamping`); the now-fully-dead `_armLongLength`/`_armShortLength`/`BucketFromPivot` constants (nothing referenced them once `DrawArmAt`/`BucketWorldPos`/`ShortArmEnd` were gone); the unused `_armAngle` field.
- **Kept unchanged:** `PivotPos()`, `_pivotHeight`, `_armRestAngle`, `MaxLoadAngle` (now purely abstract aim-math — no visual GameObject corresponds to "pivot" any more, but the drag-angle formula is byte-for-byte identical), `LaunchVelocity()`, `DrawTrajectory()`'s physics loop (only its start position changed), the trajectory dot pool, `_rubberBandLine`.
- **Added:** `BuildCannon()` (creates/finds `FarmCannon`, wires `Cannon.png`, sets sortingOrder=4 — see below for why that matters), `BuildSmokeParticles()` (procedural cone-burst `ParticleSystem`, no prefab needed), `SpawnSmoke()`, `CannonFireSequence()` + `RecoilTo()` coroutines (simple coroutine tween — confirmed no DOTween anywhere in this project before deciding), `CannonBarrelOffset=(1.1,0.4)` (= `_launchPoint`, both the trajectory-arc origin and `Fire()`'s actual animal spawn point) and `CannonLoadedBirdOffset=(0.9,0.3)` (fixed position for the loaded/ready bird — no longer tracks drag angle, since the cannon body doesn't rotate).
- `SceneSetup.WireLauncher()`: deletes `Trabuchet_Body`/`Arm`/`Swing` scene GOs if found, creates/positions `FarmCannon` (−4.5,−2.5,2 / scale 2.2×1.8), wires `Cannon.png` (already existed in the project, PPU=384 via the generic launcher-sprite import rule, default centre pivot — correct for a static non-rotating prop). Explicitly set `sortingOrder=4` on the cannon regardless of its Z=2 — Unity's 2D sort only falls back to Z-distance on sortingOrder *ties*; an explicit distinct order always wins, so this can't repeat the "trebuchet renders behind the ground" bug from the round-3 fixes above even though Z=2 alone caused exactly that for `Trabuchet_Body`.
- `SpriteAutoImporter.cs` / `EditorAutoSetup.cs`: removed the Trabuchet_Body/Arm-specific import branches (custom pivots, PPU=768) — `Cannon.png` needs no custom pivot and is already covered by the generic `Sprites/Environment/Launchers/` PPU=384 rule. `EditorAutoSetup.AutoFixLauncherSprites()` renamed to `AutoFixCannonSprite()`.
- Not visually verified (no render/play-test access) — the recoil/smoke/offset numbers come directly from the user's spec, not independently derived.

**Cannon position bug + ParticleSystem error — 2026-07-03.** Two follow-ups from the swap above:
1. **`ParticleSystem.main.duration` threw "Setting the duration while system is still playing is not supported."** `AddComponent<ParticleSystem>()` starts the system playing immediately (default `playOnAwake=true`) — configuring `main` right after (as `BuildSmokeParticles()` did) hit a live system. Fixed: `_smokePS.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear)` immediately after `AddComponent`, before touching any module.
2. **A duplicate cannon appeared in-game.** Root cause: the user had manually placed a second, plain (unwired, no script) SpriteRenderer-only GameObject directly in the scene, literally named `"Cannon"` (not `"FarmCannon"`) — found via `Game.unity` at the exact position/scale they later gave as the "correct" cannon placement (X=-3.0012, Y=-5.1223, Z=0, scale 1.4711188×1.3868444). Meanwhile `CatapultLauncher.BuildCannon()` searches for `"FarmCannon"` specifically, found none (that name never existed in the saved scene — `SceneSetup.WireLauncher()` had only run in a session where it wasn't saved, or not run at all), and created its own at the old default (-4.5,-2.5,2 / 2.2×1.8) — hence two cannons on screen at once. Fixed: deleted the stray `"Cannon"` GO from `Game.unity` directly, and updated both `CatapultLauncher.BuildCannon()`'s and `SceneSetup.WireLauncher()`'s default creation position/scale to the user-verified values (position Z now 0, not 2 — matches what the user actually placed).
3. **Robot "still in the wrong position" — was a knock-on effect of the cannon move, not a separate bug.** `_launchPoint = cannon position + CannonBarrelOffset(1.1,0.4)` moved along with the cannon, from ~(-2.35,-4.72) to ~(-1.90,-4.72). Numerically re-integrating `LaunchVelocity()`'s flight from the new origin showed max-pull now landing at X≈5.75 against a robot still sitting at X=4.8 — a real overshoot, not a guess. Rather than move the robot again (X=4.8 was already the fix for "robot off screen" two rounds ago, and the camera didn't move this round), retuned `LaunchVelocity()`'s speed range from 6.0-7.0 down to 5.5-6.5 m/s (angle range 48°-42° unchanged) so max pull again lands at X≈4.9, back on the robot.
4. **Not re-verified:** whether `CannonBarrelOffset`/`CannonLoadedBirdOffset` (both still (1.1,0.4)/(0.9,0.3), unchanged) still visually land on the barrel mouth now that the cannon sprite is smaller (scale dropped from 2.2×1.8 to 1.47×1.39, ~33% smaller) — these offsets are absolute world-units, not scaled by the cannon's own transform, so they may now overshoot past the visually smaller barrel. Flagged to the user, not changed without visual confirmation.

**Broken scene PPtr + sortingOrder tie — 2026-07-04.**
- **`Game.unity` had a broken PPtr** ("Local file identifier (1756833677) doesn't exist") after the Cannon-duplicate deletion above — I'd removed the GameObject/SpriteRenderer/Transform blocks but missed that Unity's internal `SceneRoots.m_Roots` bookkeeping array (every root-level Transform in the scene) still listed that Transform's fileID. Text-edited deletions don't get this internal list auto-synced the way an in-Editor delete would. Fixed by removing the one stale `- {fileID: 1756833677}` entry. Lesson: when hand-deleting a root-level GameObject from a `.unity` file, also grep for its component fileIDs across the *whole* file afterward, not just assume the GameObject/Component block is self-contained.
- **All 8 animal subclasses (`CluckAnimal`, `BessieAnimal`, `PercyAnimal`, `WoollyAnimal`, `DuckyAnimal`, `HoraceAnimal`, `GeraldAnimal`, `BillyAnimal`) override `_sr.sortingOrder = 4` in their own `Awake()`, unconditionally, right after `base.Awake()` sets it to 5.** This exactly ties with `FarmCannon`'s `sortingOrder=4` — Unity falls back to Z-distance on sortingOrder ties, so any bird could render *behind* the cannon, both while loaded (invisible in the barrel) and immediately after firing (invisible near the muzzle). This is very likely what read as "firing not working or rendering." Fixed all 8 to `sortingOrder = 5` (matching `AnimalBase`'s own already-documented intent — the override was redundant/wrong, not intentional). This bug predates the cannon swap — the trebuchet's arm was *also* set to sortingOrder=4 by an earlier fix this session, so animals were likely tied with the arm too; just never surfaced as a reported symptom.
- Added `AnimalBase.SetInFlightPose()` — sets `_sprInFlight` without touching physics/`IsLaunched`/`IsInFlight` (unlike `Launch()`, which does both). `CatapultLauncher.PrepareNextBird()` now calls this instead of `SetLoadedPose()`, per user request — the wings-out in-flight pose reads better poking out of a cannon muzzle than the seated loaded pose. Falls back to `_sprLoaded` then `_sprIdle` if `_sprInFlight` isn't wired on a given prefab.

**Round 4 — 2026-07-04, smoke texture + robot Y-clipping (both confirmed by computation) + chicken/fire still unresolved.**
- **Smoke rendered as squares — root cause confirmed.** `BuildSmokeParticles()` assigned a `Sprites/Default`-shader `Material` to the `ParticleSystemRenderer` but never gave it a texture — a bare quad with no texture renders as a plain square. Added `MakeSoftCircleTexture()` (soft radial falloff, squared alpha curve for a gentle edge — same generation approach as the existing `MakeDotSprite()`, but feathered instead of hard-edged) and assigned it as `mat.mainTexture`.
- **Robot "still outside the screen" — X was never the actual problem; camera math was.** Verified against a scenery object (`GnarledTree (1)`, the dead tree) confirmed visible in earlier screenshots at X=7.24 — proving the camera's horizontal range comfortably covers X=4.8, so moving the robot's X two rounds ago didn't address the real cause. Computed the camera's vertical viewport instead: rest Y=-2, orthoSize=4.5 → visible Y range is -6.5 to +2.5. The ground line is at Y=-6.60 — only 0.1 units below the camera's own bottom edge. Any sizeable sprite centred near ground level clips at the bottom of the screen regardless of X. Measured the HarvesterRobot sprite's actual art bounds within its padded canvas (612×408px, real content is rows 81-382, not the full canvas) and found the art itself — not just transparent padding — extended 0.23 units below the camera's bottom edge at Y=-6.25. Raised to **Y=-5.8215** (clears with a small margin). Trade-off: reads as slightly hovering rather than perfectly grounded; kept anyway since bottom-of-screen clipping is worse. **This camera/ground-line relationship (0.1u clearance) affects any future tall ground-standing enemy in World 1**, not just this one robot — worth designing around rather than re-discovering per level.
**Round 5 — 2026-07-05, `_eggPrefab` UnassignedReferenceException.** Confirms firing/rendering from round 4 is actually working now (the error trace shows a bird launched, was in flight, and the player successfully tapped to trigger `TriggerAbility()` — the failure was purely in egg spawning). Root cause: `SceneSetup.EnsureEggPrefab()` assigned `eggGO.GetComponent<EggProjectile>()` to `CluckAnimal._eggPrefab`, but that field is typed `GameObject` (`SpawnEggs()` does `Instantiate(_eggPrefab, ...)` expecting a GameObject) — a genuine type mismatch, not a missing-wiring omission. `SerializedProperty.objectReferenceValue` silently rejects an object of the wrong type rather than erroring, so `_eggPrefab` stayed null despite `EnsureEggPrefab()` appearing to "wire" it. Fixed the wiring code to assign `eggGO` directly, and also hand-fixed the already-broken `CluckAnimal.prefab` asset directly (`_eggPrefab: {fileID: 7747156685155071222, guid: 7a1a28516a153bb41b8fcd04ccf94559, type: 3}` — Egg.prefab's actual root GameObject fileID/guid) so it works immediately without needing another Wire Scene References run.

- **"No chicken in the cannon" / "pull to fire not working" — NOT independently re-confirmed as a new bug.** Re-verified from the scene file: `FarmCannon` exists with correct position/scale/sprite, `Launcher`'s `CatapultLauncher._cannonGO`/`_cannonSprite` are correctly wired, `LevelLoader._cluckPrefab`/`_blockParent`/`_robotParent` are all intact and correctly referenced, `RobotParent`'s transform is neutral (no stray scale/offset), and `GameManager._levels[0]`'s GUID matches `L01_FirstContact.asset` exactly (no stale duplicate level asset). Everything inspectable from the scene/code checks out. The most likely explanation is still the sortingOrder-tie fix from the previous round (all 8 animal scripts were overriding `sortingOrder=4`, tying with the cannon) — if this report was tested before that fix finished compiling, it would still show the old broken behavior. Not re-guessed further without either confirmation of a post-recompile retest, a Console error, or Hierarchy inspection during Play mode showing whether the ready-bird GameObject exists at all vs. exists-but-invisible.

**Round 6 — 2026-07-06/07, decorative haybails removed, smoke obscuring the flight path, robot Y insufficient (2nd attempt), egg-crash confirmed fire/render actually work.**
- **The 2026-07-05 `_eggPrefab` crash confirms firing and rendering ARE working** — its stack trace shows a bird launched, was mid-flight, and the player successfully tapped to trigger `TriggerAbility()`. The failure was isolated to egg spawning (see previous entry). So "cannot see it fly" in this round is a *different*, still-open report — not the same sortingOrder bug from round 4.
- **Smoke sortingOrder was covering the flying bird.** Smoke spawns exactly at `_launchPoint` and lasts 1.2s against a ~1.3-1.4s total flight — with `sortingOrder=6` (above animals at 5), a cloud rendering in front of the bird for nearly the entire flight would look exactly like "cannot see it fly through the air." Dropped to `sortingOrder=1` (clearly behind blocks/robots/cannon/animals — a background puff that can never cover gameplay-critical sprites). Not independently confirmed as *the* full explanation, but a real, concrete rendering-order bug found and fixed, same class as the earlier trebuchet/cannon sortingOrder-tie issues this whole session.
- **Robot Y=-5.8215 (round 4's fix) was still insufficient — "still too low".** Re-verified the underlying camera numbers directly against the saved scene (`Main Camera` position (0,-2,-10), `orthographic size: 4.5` — not assumed, read from `Game.unity`), confirming the earlier math was right in principle. Rather than nudge Y a third time on the same oversized scale, **halved the robot's scale** (4.3565×4.69371 → 2.17825×2.346855) and moved to **Y=-5.5607**, giving a 0.7-unit safety margin against the camera's bottom edge (vs. 0.2 units last time) — content now spans Y −5.8 to −5.4 against a camera range of −6.5 to 2.5. Collider is unaffected (already decoupled from visual scale in `LevelLoader.SpawnRobot()`). Trade-off unchanged: this is a deliberate hover, not a perfectly grounded sprite — the camera's viewport bottom edge is only 0.1 units above the ground line, so *nothing* sizeable can rest exactly on the ground and stay fully on-screen without a camera change (not attempted, would shift every other hand-placed prop in the scene).
- **Deleted all 4 decorative `Haybail`/`Haybail (1-3)` scene GameObjects** directly from `Game.unity` (they were superseded by the gameplay `HaybaleBlock` instances at the same positions since the round-3 hay-pile fix, and the user hadn't removed them yet). This time proactively checked and cleaned up `SceneRoots.m_Roots` for all 4 Transform fileIDs in the same pass (learned from the Cannon-deletion dangling-PPtr incident) — verified zero remaining references to any of the 4 objects' GameObject/SpriteRenderer/Transform fileIDs afterward. **Decorative scene props are never touched by code** — unlike the gameplay `HaybaleBlock`/`RobotEnemy` instances, which `LevelLoader` spawns fresh from `LevelData` every level load (same mechanism, "automatic" in the same sense the user asked about) — hand-placed scene GameObjects are permanent until someone (user or Claude) manually deletes them.

**Round 7 — 2026-07-08. `CameraShake` bug found (real, confirmed); smoke/cannon sortingOrder conflict (self-inflicted by round 6); robot Y/scale fix STILL not enough — abandoned further guessing in favour of asking the user for exact values; chicken-invisibility now suspected NOT actually related to any sortingOrder fix so far.**
- **"Screen shifts upward as haybails are destroyed" — confirmed, real bug, unrelated to anything else this session.** `CameraShake.DoShake()` did `cam.transform.position += <random point>` every frame with **no corresponding subtraction** — every call permanently random-walked the camera and never restored it. `BlockBase.DestroyBlock()` calls `CameraShake.Shake()` unconditionally on every block death (haybales included), and `Fire()` explicitly sets `_cameraFollowing=false` for the whole flight ("camera stays fixed" by design) — so nothing was ever correcting the drift once introduced. Fixed to track the shake's own contribution as a delta each frame and subtract it back out at the end, so it always nets to exactly zero — safe even if something else moves the camera concurrently, since it only adds/removes its own offset rather than overwriting the whole position.
- **"No smoke comes out of the cannon anymore" — self-inflicted by the previous round's fix.** Moving smoke to `sortingOrder=1` (to stop it covering the bird) put it *behind* the opaque `FarmCannon` sprite (order 4) — fully hidden. There's no integer between 4 and the animals' old order of 5, so animals moved to **6** (all 8 subclasses + `AnimalBase`) and smoke took **5** — now visible in front of the cannon, still behind the bird.
- **Robot Y/scale — resolved 2026-07-09 with user-provided exact values: position (5.6, -5.25), scale (5.7065, 7.009).** Notably *larger* than any of Claude's own attempts — confirms the "shrink it to fit" instinct from round 6 was backwards; the user wanted it bigger, just correctly placed. Applied directly to `L01_FirstContact.asset` and `LevelDataGenerator.cs`, with a comment telling future sessions not to re-derive this from camera math again — treat it as ground truth.
- **Chicken invisible (loaded AND flying) — now suspected this was never actually explained by either sortingOrder fix.** The 2026-07-05 `_eggPrefab` crash trace only proves `AnimalBase.Update()` ran (i.e. the GameObject exists and its `IsLaunched` logic executes) — it does NOT prove the sprite was ever visually rendering; that was an overreach in the round-5 write-up. Re-checked everything inspectable from files: sortingOrder (now non-conflicting), all 5 pose sprites wired with valid GUIDs on `CluckAnimal.prefab`, `SpriteRenderer.m_Enabled=1`, shared sprite material (same GUID used successfully by dozens of other confirmed-visible scene sprites) intact, camera `m_CullingMask` includes all layers (`4294967295`), spawn position math lands within camera bounds. Nothing left to check from static analysis — next step needs a live Play-mode Inspector screenshot of the actual CluckAnimal instance (SpriteRenderer's sprite/color/enabled fields as Unity currently has them at runtime, not what's baked into the prefab asset), not another guess.

**Round 8 — 2026-07-10. ROOT CAUSE FOUND: every character sprite, for all 8 characters, was imported in Multiple sprite mode.** The user pointed at `Assets/Sprites/Characters/Cluck` directly and reported `Cluck_InFlight.png` "not allowing me to drop into scene" — a huge, concrete lead. Checked its `.meta`: `spriteMode: 2` (Multiple), auto-sliced into **9 disconnected sub-sprites**. `CluckAnimal.prefab`'s `_sprInFlight` was wired to `Cluck_InFlight_0` — a 53×8px sliver fragment, not the actual art (the biggest fragment, `_2`, is 294×302px; the rest are similarly tiny scraps). At the animal's world scale this sliver is essentially imperceptible — this is the real explanation for "chicken invisible," not any of the sortingOrder theories from rounds 4-7 (those were real bugs too, worth having fixed, just not *this* bug).
- **Scope: every pose, every character.** Checked all 8 character folders — 100% of pose PNGs are Multiple mode; several have 2-16 sub-sprites (`Bessie_Trigger`/`Bessie_Trigger1`: 16 each, `Cluck_Trigger1`: 12, `Cluck_InFlight`: 9). Poses that happened to auto-slice into exactly 1 region mostly look fine by luck, not design.
- **Root cause of the root cause:** `SpriteAutoImporter.cs`'s character-sprite `OnPreprocessTexture()` branch was the *only* branch in the file that never enforced `SpriteImportMode.Single` — every other category (launchers, world props, blocks, robots, UI cards) explicitly does, each with a comment explaining that Multiple mode breaks `LoadAssetAtPath<Sprite>`/serialized references. Character sprites were simply missed when that convention was established. Fixed — the branch now enforces Single mode like everywhere else.
- **Compounding bug: the auto-heal safety net for this had been silently dead the whole time.** `EditorAutoSetup.AutoWireCharacterSprites()` gates on a sentinel file `Assets/Sprites/Characters/Cluck/Loaded.png` — which has never existed; the actual file is `Cluck_Loaded.png` (renamed at some point, sentinel never updated). `AssetImporter.GetAtPath()` returned null every time, so the guard always short-circuited and the whole auto-wire-on-compile mechanism never ran, for any reason, ever. Fixed the path, and broadened the trigger condition to also catch wrong sprite mode (not just stale PPU) so a future regression self-heals via `SpriteAutoImporter.ForceReimportAll()` + `SpriteWiring.WireAll()` on the next compile instead of needing someone to notice.
- **Required next step (not yet done — needs the Unity Editor, not a file edit):** run **FarmFury → Reimport Sprites** to force all character PNGs to reimport under the corrected Single-mode setting, then **FarmFury → Wire Sprites** (or the full **Wire Scene References**) to re-wire the now-correct whole sprites into all 8 animal prefabs. `SpriteWiring.cs` wires by `AssetDatabase.LoadAssetAtPath<Sprite>(path)`, not a hardcoded fileID, so it will correctly pick up the new Single-mode sprite automatically. Did not hand-edit the ~72 affected `.meta` files directly — Unity's own reimport engine doing the texture re-slicing is far less error-prone than hand-crafting that YAML, and the tooling to do it correctly already exists as two menu clicks.
- **Confirmed live 2026-07-10:** user reported the "Create New Animation" dialog appearing when dragging any character sprite into the scene — this is Unity's own tell for a Multiple-mode sprite sheet (it assumes multiple sub-sprites means you want a frame animation), independently confirming the diagnosis before the fix was even applied.
- **2026-07-11, once fixed and re-tested: `BirdScale` and `CannonLoadedBirdOffset` both needed retuning** — both were calibrated against the broken 53×8px sliver appearance, not the real sprite. User-verified replacements: `BirdScale = (0.274099, 0.251007)` (was (2.2676, 2.5454) — ~8x too large for the real sprite), `CannonLoadedBirdOffset = (0.6212, 0.4223)` (was (0.9, 0.3)). Refactored the previously-duplicated inline scale literal (set separately in both `PrepareNextBird()` and `Fire()`) into one shared `BirdScale` constant while touching this.
- **`ForceReimportAll()` had no access modifier → defaults to `private` in C#**, but `EditorAutoSetup.AutoWireCharacterSprites()` (fixed in round 8, above) calls it cross-class — `CS0122`. Made it `public`.

**Round 9 — 2026-07-12. SECOND, deeper root cause found: character sprite folders were misnamed, so `SpriteWiring.cs` was wiring nothing.** After the Multiple→Single sprite-mode fix (round 8) and a position/scale retune (this round, above), the user reported the loaded/flying bird now renders as "a yellow dot" — not the chicken. Checked `CluckAnimal.prefab`: **every pose sprite field (`_sprIdle`, `_sprLoaded`, `_sprInFlight`, `_sprImpact`) was `{fileID: 0}` (null)**. `AnimalBase.Awake()` falls back to a procedural circle when `_sprIdle` is null, and `CluckAnimal.Awake()` tints it yellow when `!HasRealSprites` — exactly "a yellow dot".
- **Root cause: `unity/Assets/Sprites/Characters/` contained `Cluck_Chicken/` and `Bessie_Cow/`, not `Cluck/` and `Bessie/`.** `SpriteWiring.cs`'s `CharPPU`/`CharPrefab` dictionaries are hardcoded to the short names (`"Cluck"`, `"Bessie"`) and build the search path as `Assets/Sprites/Characters/{charName}` — with the long-named folders, that path doesn't exist, `AssetDatabase.FindAssets` returns nothing, and `AssignSprite()` silently sets every field to null (only a `Debug.LogWarning`, easy to miss). Confirmed via `.meta` inspection that the round-8 sprite-mode fix DID work correctly (`spriteMode: 1` on `Cluck_InFlight.png` inside the misnamed folder) — this was a second, independent bug layered on top of the first, not a failure of the first fix.
- **Confirmed the short-name convention is the intended one, not a preference call:** `tools/remove_backgrounds.py`'s `CHAR_MAP` explicitly maps `"Cluck_Chicken": "Cluck"` (and all 7 others) for its output path — the tool has always been designed to strip the raw-art-folder suffix when writing into `unity/Assets/Sprites/Characters/`. The long-named folders directly under `Characters/` were never supposed to exist there; likely a raw `assets/` folder got copied in directly at some point, bypassing the tool.
- **Fixed by renaming the folders** (`Cluck_Chicken`→`Cluck`, `Bessie_Cow`→`Bessie`, `.meta` files moved along with their folders to preserve GUIDs) rather than changing `SpriteWiring.cs` to match the wrong names — keeps every other tool/doc reference (which all already assume short names) correct without further changes.
- **Still needs the user to run FarmFury → Wire Sprites (or Wire Scene References) once more** — the rename alone doesn't re-wire the prefabs; `SpriteWiring.WireAll()` needs to run again now that the expected path actually resolves. `_eggPrefab` (fixed separately, round 5) is untouched by this — `SpriteWiring` only ever touches the 5 sprite fields.

**Round 10 — 2026-07-13/15. Cannon-fire audio + Cluck falling SFX + looping music; replay fix; trajectory raised/slowed; then a second round of Cluck/haybail/robot bugs — this time root-caused from measured pixel/physics data instead of guessed.**
- **Audio added:** `SunriseMeadows_Background.mp3` loops continuously from the first `GameState.Playing` transition (never restarts across levels); `CannonShot.mp3` replaces the procedural launch clip at the existing `AudioManager.Play(Sound.Launch)` call site; `Cluck_falling.mp3` loops from the moment Cluck is fired and fades out over 0.35s the instant `AnimalBase.OnAnimalImpact` fires (a new event, added specifically for this — fires on real hits only, not `CluckAnimal`'s hay pass-through punches, which skip `base.OnCollisionEnter2D` entirely). `AudioManager` moved from a runtime-`AddComponent`-on-the-Launcher-GO pattern to a dedicated scene GO (`SceneSetup.EnsureAudioManager()`) so the three external clips can be serialized into `Game.unity` and survive real builds; `[DefaultExecutionOrder(-90)]` added so this instance reliably wins the singleton race against `CatapultLauncher`'s fallback `AddComponent<AudioManager>()` (now checked via `AudioManager.Instance == null`, not `GetComponent`, since the two are normally different GameObjects).
- **Replay/Try Again/pause-Restart fixed to reuse the proven no-reload reset path.** `GameManager.RestartLevel()` called `StartLevel()`, which does a full `SceneManager.LoadScene()` — a heavier, less-exercised path than `ForceStartLevel()` (used every time the game boots via `CatapultLauncher.DelayedAutoStart()`, and already resets all launcher/camera/bird state via the existing `OnLevelStarted` handler). Switched to `ForceStartLevel(CurrentLevelIndex)`. Also added an explicit `SnapCameraToRest()` call inside `OnLevelStarted()` — restart no longer reloads the scene, so a camera left mid-pan-back from the previous attempt needed an explicit reset (previously this only ran once at `Start()`).
- **Trajectory raised + slowed considerably**, numerically re-integrated (same gravity/drag/timestep model `DrawTrajectory()` itself uses) so the landing zone still lines up with the hay pile / robot despite the much floatier flight: `AnimalBase.Launch()`'s `gravityScale` 0.4→0.18; `CatapultLauncher.LaunchVelocity()`'s speed 5.5-6.5→4.0-4.9 m/s, angle 48°-42°→58°-52°; `DrawTrajectory()`'s gravity constant updated to match. Flight time grew ~1.4s→~2.3-2.5s, apex height ~1.0-1.1u→~1.6-2.0u.
- **Cluck_InFlight invisible — real root cause, measured not guessed.** `Cluck_InFlight.png` is a 500×500 canvas at PPU=2057 with real content trimmed to only 444×301px (measured via PIL bbox) — nowhere near the ~1481px height `SpriteWiring.cs`'s own PPU-calibration comment assumed, and none of the Cluck pose files match the documented "1024×1024" art spec (each is a different resolution: 908×512, 665×375, etc.). At the then-current `BirdScale (0.274, 0.251)` the bird rendered at ~0.06 world units — ~10x smaller than the 0.72u collider it's meant to match. Recalculated `BirdScale = (4.9204, 4.9204)` (uniform — InFlight is the only pose ever shown on this GameObject) to hit that 0.72u target exactly.
- **New bug from that same fix: Cluck destroyed haybails before any visible contact.** `CircleCollider2D.radius` scales with `transform.localScale` — bumping `BirdScale` ~18x to fix visibility scaled the physics hitbox ~18x too (collider radius 0.36 in local space → ~1.77 world units, versus a ~0.72u visible sprite). Fixed with `CatapultLauncher.ApplyBirdScale()`, which re-derives `col.radius /= BirdScale.x` right after setting the visual scale — same pattern `LevelLoader.SpawnRobot()` already used for the robot's `BoxCollider2D`.
- **Haybail "still too high" — the first attempted fix (round 9's -4.7) was itself the bug.** That fix assumed the haybale's *nominal* size (1.0×0.9, used for physics/health scaling) equalled its *rendered* size — wrong: `Haybail.png` imports at PPU=512 (the generic World1Props rule, not the 1-unit-native `isBlockSprite`/`isNewScenery` special cases), so at scale (1.0,0.9) the real rendered content (measured: 500×500 canvas, trimmed to 500×419px) is only ~0.7365 units tall. Recomputed from the real pixel content (including the asymmetric top/bottom padding's ~0.04u pivot offset) so the top bale's *visible* art rests on the base row's *visible* art: Y=-4.87 — landing almost exactly back on the original hand-placed -4.876. That original value (sourced from the user's own decorative-prop scene placement, round 3) was already correct; the round-9 "fix" was the actual regression.
- **Robot position updated again to user-provided (5.7, -5.36)**, scale unchanged (5.7065, 7.009) — same "treat as ground truth, don't re-derive" policy as the 2026-07-09 value. Screenshots continued showing something resembling a small car/robot near the bottom-right corner rather than a robot at this scale (which should occupy roughly half the screen height given the camera at (0,-2) orthoSize 4.5) — flagged to the user as possibly a stale build/screenshot rather than a data bug, unconfirmed.

**Sunrise Meadows world map — 2026-07-15.** New level-select screen for World 1, replacing the grid-based `LevelSelectController` as `MainMenuController`'s PLAY destination (`LevelSelectController` itself is untouched/unwired, kept for World 2+). Built from a user-supplied spec; two deliberate deviations, not silent: (1) implemented as a `ScreenSpaceOverlay` Canvas with UI `Image`/`Button` markers rather than literal world-space `SpriteRenderer`/`Transform` positions — a `SpriteRenderer` has no `Button`/click support of its own, and this keeps the screen consistent with every other menu (all Canvas-based) instead of contesting `CatapultLauncher` for camera ownership; the spec's X/Y path coordinates are honoured directly via a 1 unit = 100px mapping (matching the marker art's own PPU=100). (2) Reuses the existing `ScoreManager.GetBestStars()` convention (0-based `ff_stars_N`, plain star count) rather than the spec's separate 1-based `ff_stars_1..18` scheme with a `-1` "unlocked-but-unplayed" sentinel, so this screen and `LevelSelectController` can't disagree about what's unlocked — trade-off: can't distinguish "never played" from "played and failed" (both read 0 stars). Art already existed at `Assets/Sprites/UI/LevelCards/World1/` (SunriseMeadows.png, 4× LevelMarker_*.png, PlayerPosition.png — no dedicated 2-star marker yet, falls back to 3-star art) and was already correctly imported (PPU=100, Single mode) by the pre-existing generic `Sprites/UI/` rule. `SceneSetup.EnsureWorldMap()` wires all of it, plus the preview card's animal-vs-robot art (reusing `EnsureHUD`'s `CardKeywords`/`Sprites/UI/Cards/` convention). Known gaps: markers 13/14 sit only 50px apart vertically against an 84px-tall marker and will visually overlap (flagged in the original spec as "adjust after seeing in scene"); only 6 of 18 levels have `LevelData` assets, so `LevelPreviewCard` shows a "COMING SOON" placeholder with PLAY disabled for the rest rather than crashing.

**Raw art source (`assets/`) is currently absent from disk.** All 236 files under `assets/` were deleted from the local filesystem outside of any Claude Code session (cause unknown). They remain fully tracked in git at HEAD and are recoverable with `git checkout -- assets/`. The processed/imported sprites Unity actually uses (`unity/Assets/Sprites/`) are untouched and unaffected. The user chose to leave `assets/` deleted for now — **the documented art pipeline (`tools/remove_backgrounds.py`, adding new Kling AI PNGs) will not work until it's restored.**

**`unity/Assets/Sprites/` is entirely gitignored (`unity/.gitignore:37`) and is the only copy of all processed game art.** It is not tracked by git in any commit. Combined with the point above, this means processed sprite art currently exists in exactly one place: this machine's disk. If it's lost, the only recovery path is restoring `assets/` from git and re-running the full art pipeline (`remove_backgrounds.py` → Wire Sprites / Reimport Sprites) — there is no direct git-history recovery for `Sprites/` itself. Worth a deliberate decision (git-LFS, commit it anyway, or accept the risk) rather than leaving it implicit.

**Cleanup performed this session:** deleted `unity/Assets/LevelData.asset` + `.meta` (an orphaned, empty template LevelData asset sitting outside `Assets/ScriptableObjects/Levels/`, unreferenced by any code — likely leftover from an early/misconfigured generation run). Deleted `unity/CLAUDE.md` (stale duplicate, already flagged as non-authoritative, describing a pre-rebuild scene/script state that no longer exists). Both are recoverable from git history if needed.

**No repo-root `.gitignore`.** `unity/.gitignore` exists and correctly covers `Library/`, `Temp/`, `Logs/`, `Builds/`, etc. *inside* `unity/`, so build artifacts aren't at risk of being committed. Not currently a problem, just noted as a gap if root-level tooling is added later.

**Monetisation and backend: 0% built.** Confirmed via full-tree search — no Firebase, Unity IAP, AdMob, RevenueCat, or UGS package or code references exist anywhere in the project. This is expected per the roadmap (Phase 6), but is called out explicitly here since it's the largest gap between current state and the "monetisation-ready MVP" goal — see Gap Analysis below.

### Gap analysis vs GDD — path to MVP

| Area | GDD target | Current state | Gap |
|---|---|---|---|
| World 1 levels | 18 (4 tutorial, 8 build, 4 twist, 2 boss) | 6 exist (L01–L06); **only L01 is actually playable** (see bug above) | Need L07–L18 authored + L02–L06 coordinate-fixed; no boss level (Robot Commander) yet |
| Level validator | Physics-sim script checks stability/solvability before use (GDD §04) | Does not exist | No automated guard against broken/unsolvable levels — the L02–L06 bug would have been caught by this |
| Monetisation | 7 revenue streams, Firebase + UGS + AdMob + RevenueCat + Unity IAP (GDD §05–06) | None built | Entire stack unbuilt; this is Phase 6 in the roadmap, correctly sequenced after content, but worth flagging now since it's explicitly the stated goal for this pass |
| Worlds 2–6 | 6 launchers, 108 more levels, 6 more world art sets | 0% — World 1 (Trebuchet) only | Expected — Phase 5, not started |
| Analytics / crash reporting / leaderboards | Firebase Analytics, Crashlytics, UGS Leaderboards | None | Expected — Phase 6 |
| Star system | 1/2/3★ + "Perfect" (4th tier, all blocks destroyed) | 1/2/3★ implemented in `ScoreManager`; no "Perfect" tier | Minor — cheap to add when revisiting scoring |
| Content format | JSON level descriptors, CDN-hot-loadable for live ops (GDD §06) | Unity `LevelData` ScriptableObjects only | Reasonable pragmatic divergence for a solo dev pre-launch; JSON matters once live-ops/remote content delivery is actually needed, not before |

**Practical read:** the single highest-leverage next step toward "MVP ready to play" is fixing the L02–L06 coordinate bug (small, well-understood fix) — right now the game has exactly **one** fully playable level. After that, level content (L07–L18) is the bottleneck, not engineering. Monetisation is real work but is correctly sequenced last per the roadmap; wiring it in before there's a full, playable World 1 would be premature.

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
- "Perfect" — 3★ + all blocks destroyed (hardcore/leaderboard goal, GDD §04) — **not implemented in `ScoreManager` yet**, only the 1/2/3★ tiers exist

---

## Commands

### Unity (Editor)
Open `unity/` in Unity Hub (Unity 6.5 / 6000.5.0f1). Open `Assets/Scenes/Game.unity`. Press Play — the ground, camera, and LevelLoader reference are all self-wired at runtime.

### Run-Unity.ps1 (batch automation)
`Run-Unity.ps1` is the primary interface for all batch Unity operations. Run from the repo root in PowerShell:

```powershell
.\Run-Unity.ps1 levels        # Generate/overwrite all 6 LevelData ScriptableObjects — DO NOT RUN until
                               #   LevelDataGenerator's L01 entry is updated to match the live hand-edited
                               #   L01 asset; it will currently overwrite and destroy it. See Audit Findings.
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
| **FarmFury → Generate All Level Data** | To recreate the 6 World 1 LevelData assets (overwrites existing). **Currently destructive to L01** — see Audit Findings before running. Run Wire Scene References after. |
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
2.5 Level select — world-art thumbnails, bottom gradient overlay, lock veil, gold/grey ★★★. Superseded for World 1 by the Sunrise Meadows world map (2026-07-15, see Round 10 below) but left in place for World 2+.
2.6 Main menu — full-screen `LandingPage.png` splash + `Play.png` icon button (2026-07-13, replaced the earlier orange-rect + "▶ PLAY" text).
2.7 Sunrise Meadows world map (2026-07-15) — winding-path level select for World 1: 18 tappable markers, bobbing player-position indicator, tap-to-preview matchup card. See `WorldMapController.cs`/`LevelMarker.cs`/`LevelPreviewCard.cs` and Round 10 below for the full design/trade-off writeup.

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
✅ Sky backdrop, ground art (5-layer terrain), main menu art, trebuchet sprites, sprite PPU calibration, camera zoom (orthoSize=4.5), trebuchet arm alignment, animal card HUD, level select redesign with world thumbnails, trebuchet drag+swing animation mechanic, robot visibility fix, all 6 world level cards, SceneryBuilder (deterministic-RNG World 1 props per level), destruction improvements (4 fading fragments, damage at 50% health), all-wood early levels (L01–L03), **coordinate system rebuild (ground Y=−6.60, launcher X=−2.327, camera Y=−2)**, retuned block health (wood=80, stone=220), robot scale (0.6×0.9), **HarvesterRobot.prefab** (separate from Robot.prefab — spawned when `robotType=Harvester`), **RobotType enum** in LevelData, **_swingSpriteGO** in CatapultLauncher (tracked to arm tip, always visible), Cluck pass-through mechanic, sky painting import fixed, **L01 redesigned: 3 Cluck birds vs 1 HarvesterRobot (x=5.155, y=−6.25)**, SceneryBuilder exact placement mode, PlaceExact() native-size correction, ParallaxScroller component, **trebuchet geometry recalibrated** (armRestAngle=218°, armLongLength=0.571, pivotHeight=1.914 — derived from user Inspector values), **Cluck visual scale (2.2676, 2.5454)**, **bird gravityScale=0.4** (slower arc), **HUD cards moved to top-left** (anchorMin 0.02,1).

**Level 1 layout** — L01 is live in `L01_FirstContact.asset` as 3 Cluck birds vs 1 HarvesterRobot (`blocks: []`, robot at `(5.15509, -6.25)`, `robotType=Harvester`, `par=2`) — this is the actual current design, hand-edited directly in the asset and **out of sync with `LevelDataGenerator.cs`'s L01 code** (see Audit Findings — do not regenerate). All scenery (OldBarn_Right, OakTree, Haybails ×4, WoodenFence ×4, GnarledTree, Windmill, GrassTuft ×5, Rock ×2, WildFlowers) is hand-authored as permanent GameObjects in the scene. The `Trabuchet_Body`, `Trabuchet_Arm`, `Trabuchet_Swing` are also hand-placed scene GOs; CatapultLauncher references them via `_armSpriteGO` / `_swingSpriteGO` [SerializeField] fields wired by **Wire Scene References**.

**L02–L06 exist** (`L02_StoneWall`, `L03_TheTower`, `L04_EggPractice`, `L05_TheFortress`, `L06_BessiesDebut` — wood/stone structures, 3–5 birds, robots per level) but are **not currently playable** — generated under the pre-rebuild coordinate system, so they spawn floating above the current ground (see Audit Findings, Critical bug). Fixing their baked positions is the top-priority next step before any further level content work.

**Still to do:** fix L02–L06 coordinates; sync `LevelDataGenerator.cs` L01 entry with the live asset; add L07–L18 (GDD calls for: L07–L12 multi-structure/stone/3-bird, L13–L16 mixed materials/elevated platforms/chain reactions/4-bird, L17–L18 Robot Commander boss); no level validator exists yet (GDD §04 mandates one — none built).

### Phase 5 — Worlds 2–6
Each world: new launcher, world physics modifier, new animals, all levels, environment art, music, boss.

### Phase 6 — Polish & Release
Animations, particle systems, music, monetisation (ethical-first, no pay-to-win), achievements, iOS/Android.

**Monetisation stack per GDD §06 (none built yet — see Audit Findings):** Firebase (Auth, Firestore, Remote Config, Analytics, Cloud Messaging) as primary backend; Unity Gaming Services (Leaderboards, Cloud Save, Economy) as supplementary; Google AdMob + AppLovin MAX for ads; Unity IAP + RevenueCat for purchases/subscriptions. Seven revenue streams (rewarded ads, starter pack $1.99–2.99, season pass $4.99/mo, coin shop, power-ups, cosmetic shop, ad removal $3.99) — full pricing/mechanics in GDD §05. Guardrails: no ads in first 20 levels, no pay-to-win, no energy system, cosmetics never randomised.

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
- **Ground surface at Y = −6.60** (world space). Launcher GO at (−2.327, −6.60, 0) — an abstract aim-math anchor only since the 2026-07-02 cannon swap; `_pivotHeight = 1.914` no longer corresponds to any visual GameObject position. The visual launcher is FarmCannon at (−4.5, −2.5, 2), independent of the Launcher GO's position. Camera at (0, −2, −10), orthoSize = 4.5. `_cameraRestOffset = (2.327, 4.60)` → camera parks at (0, −2).
- **Level block/robot positions are stored as raw world-space coordinates in `LevelData`** — `LevelLoader.SpawnBlock`/`SpawnRobot` apply no offset, they place at exactly `data.position`. This means whoever authors a level is responsible for baking correct current-system coordinates directly. L01's asset was hand-migrated to the current system (old ground = −2.5 → new ground = −6.60, a −4.1 shift, plus an X re-check against the new launcher X=−2.327). **L02–L06 were never migrated and are currently broken** — see Audit Findings above.

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
    AudioManager.cs         — 7 DSP-generated SFX clips (Launch/WoodHit/StoneHit/RobotDeath/
                              Win/Fail/BlockDestroy/RobotHit) + 3 external MP3 clips wired via
                              SceneSetup.EnsureAudioManager() (Assets/Audio/*.mp3): _musicClip
                              (looping background music, starts once on first GameState.Playing,
                              never restarts on later level transitions), _cannonShotClip
                              (replaces the procedural Launch clip when non-null — same
                              AudioManager.Play(Sound.Launch) call site), _fallingClip (loops
                              from Fire() while a Cluck is airborne, stopped with a 0.35s fade
                              via AnimalBase.OnAnimalImpact — fires on real hits only, not
                              CluckAnimal's hay pass-through punches). [DefaultExecutionOrder(-90)]
                              so this instance wins the singleton race against CatapultLauncher's
                              fallback `AddComponent<AudioManager>()` (checked against
                              AudioManager.Instance now, not GetComponent, since AudioManager
                              normally lives on its own dedicated scene GO, not the Launcher GO).
                              SfxEnabled/MusicEnabled from PlayerPrefs; MusicEnabled toggles
                              _musicSrc.mute live from the pause menu.
    CameraShake.cs          — singleton, auto-attached to Launcher GO
    ParallaxScroller.cs     — MonoBehaviour; speed 0.0 (world-fixed) – 1.0 (camera-locked);
                              LateUpdate() offsets X by camDelta×speed for depth parallax;
                              attached to scenery props by SceneryBuilder.AddParallax()
    SceneryBuilder.cs        — see full description under "Key Implementation Rules" below;
                              subscribes to OnLevelStarted, places World1Prop sprites deterministically
                              (or skips entirely for L1, which is hand-authored in-scene)
  Level/
    LevelData.cs            — ScriptableObject; birds[], blocks[], robots[] arrays; par bird count;
                              BlockSpawnData has optional passThrough, healthOverride, massOverride
                              fields (0 = use BlockBase defaults; set by LevelLoader after spawn)
    LevelLoader.cs          — instantiates prefabs; TryConsumeBird / PeekNextBird; fires
                              OnBirdConsumed event; BirdQueueSnapshot property;
                              DelayedLevelComplete / DelayedLevelFailed coroutines → GameManager
                              AutoLoadPrefabs() runs in Awake() (Editor only) — auto-finds all prefabs
                              from Assets/Prefabs/ by type, so Inspector wiring is not required in Editor
  Animals/
    AnimalBase.cs           — abstract; Kinematic until Launch(); Mouse.current (New Input System);
                              5 pose sprites; HasRealSprites property; DestroyAnimal() fires OnAnimalDestroyed
    CluckAnimal.cs          — 5-egg cluster bomb in 120° spread; eggs from _eggPrefab;
                              pass-through: punches WoodBlock._passThrough=true at 70% velocity;
                              _lastVelocity tracked in FixedUpdate to capture pre-impact speed
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
    WoodBlock.cs            — baseMaxHealth=80, baseMass=5, bounciness=0.2;
                              _passThrough (public): Cluck punches through at 70% velocity;
                              set by LevelLoader.SpawnBlock() from BlockSpawnData.passThrough
    StoneBlock.cs           — baseMaxHealth=220, baseMass=8, bounciness=0.1
    (BlockBase.ApplyOverrides(health, mass) — applies BlockSpawnData.healthOverride/massOverride
     after Initialise(); called from LevelLoader.SpawnBlock(). BlockType.Haybale (LevelData.cs)
     spawns HaybaleBlock.prefab — same WoodBlock component, Haybail.png art via SceneSetup.)
  Enemies/
    RobotEnemy.cs           — HP=35, impulse damage ×1.0; scale=(0.6,0.9); BoxCollider2D.size=(1,1), mass=20;
                              2 red eye child GOs in Awake; calls LevelLoader.NotifyRobotDestroyed
  Scoring/
    ScoreManager.cs         — Robot +1000, Wood +100, Stone +200, Egg +50, bird-left bonus +500
                              PlayerPrefs keys: ff_score_N, ff_stars_N
  Launcher/
    CatapultLauncher.cs     — visual launcher is FarmCannon (fixed 2026-07-02, replaced the
                              trebuchet — see Audit Findings). Aiming math UNCHANGED from the
                              trebuchet system by explicit requirement: click bird → drag →
                              PivotPos()-relative angle clamped to [_armRestAngle=218°,
                              218°+MaxLoadAngle=50°] → loadFrac → LaunchVelocity() speed
                              6.0–7.0 m/s, angle 48°–42°. PivotPos()/_pivotHeight/_armRestAngle/
                              MaxLoadAngle are now abstract aim-math constants only — no visual
                              GameObject corresponds to "the pivot" any more.
                              Visual: single static "Cannon" sprite (_cannonSprite) on the
                              FarmCannon GO, never swapped. CannonBarrelOffset=(1.1,0.4) from the
                              cannon's rest position = _launchPoint (trajectory-arc origin AND
                              actual animal spawn point in Fire()). CannonLoadedBirdOffset=
                              (0.9,0.3) = where the ready/loaded bird sits (fixed, does not
                              track the drag — the cannon body doesn't rotate).
                              On fire: CannonFireSequence() coroutine — recoil X rest→rest−0.3
                              over 0.08s (RecoilTo() coroutine tween; no DOTween in this
                              project), smoke burst (procedural ParticleSystem, cone/25°, 15-burst,
                              1.2s life, upward forceOverLifetime.y=0.5) at t=0.08s, recoil
                              returns rest−0.3→rest over 0.4s, then waits out the remainder of
                              CannonResetDelay=1.80s total.
                              Cluck spawns at localScale (2.2676, 2.5454); _returnDelay=2.5s;
                              EnsureGroundExists() validates Y≈−2.5
  UI/
    HUDController.cs        — Canvas built at runtime; card widgets (active 200×260, queue 155×202, gap −55);
                              cards anchored top-left (anchorMin 0.02,1 pivot 0,1 pos 0,−12);
                              orange ⚡N damage badge; Level Complete/Failed/Pause panels
    LevelSelectController.cs — ScrollRect + GridLayoutGroup 3-col; RefreshGrid() rebuilds on show;
                              world-art thumbnails with gradient overlay; lock veil. Untouched but
                              currently unwired — WorldMapController replaced it as PLAY's
                              destination for World 1 (kept in place for World 2+).
    MainMenuController.cs   — LandingPage.png + dark vignette + Play.png icon button (square,
                              replaces the earlier orange-rect + "▶ PLAY" text); shows on
                              GameState.Idle; PLAY opens WorldMapController, not LevelSelectController
    WorldMapController.cs    — Sunrise Meadows (World 1) level map; ScreenSpaceOverlay Canvas,
                              sortingOrder 300; 18 LevelMarker instances positioned via the
                              spec's X/Y * 100px (matches marker art's PPU=100); unlock rule
                              (level 1 always unlocked, level N needs level N-1 >=1 star) reuses
                              ScoreManager.GetBestStars(), not a separate PlayerPrefs scheme;
                              bobbing PlayerPositionIndicator above the highest unlocked marker;
                              owns a LevelPreviewCard instance
    LevelMarker.cs           — one pin: UI Image+Button (not SpriteRenderer — Button needs uGUI)
                              + level-number TMP label; Refresh(unlocked,stars,...) picks
                              locked/unlocked/star1/2/3 art (falls back to 3-star art — no
                              dedicated 2-star asset yet); PlayLockedShake() damped-sine jitter
    LevelPreviewCard.cs      — centred popup on tap; level number, animal-vs-robot matchup read
                              from GameManager.GetLevelData(index).birds[0]/robots[0].robotType,
                              stars, PLAY button; shows "COMING SOON" (PLAY disabled) when the
                              level index has no LevelData yet; full-screen dismiss-catcher
                              behind a click-swallowing card Button

unity/Assets/Editor/
  SceneSetup.cs         — FarmFury > Wire Scene References; wires all Inspector refs;
                          sets camera (0,0,-10) orthoSize=4.5; launcher at (-2.327,-6.60,0);
                          deletes Trabuchet_Body/Arm/Swing scene GOs if found (fixed 2026-07-02
                          — replaced by FarmCannon); creates/wires FarmCannon at (-4.5,-2.5,2)
                          scale (2.2,1.8,1), wires Cannon.png (Single mode, PPU=384, default
                          centre pivot — static non-rotating prop) into _cannonSprite;
                          ground center (0,-2.75,0) scale (60,0.5,1) → top at Y=-2.5;
                          EnsureBackground() re-imports SkyPainting.png as Sprite PPU=100 if needed,
                          then wires it into BackgroundController._skySprite;
                          FarmSilo intentionally not wired — excluded from World 1 design;
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
- **Ground is recreated at runtime:** `CatapultLauncher.Start()` calls `EnsureGroundExists()`, which validates against `transform.position.y` (the launcher's own Y, currently −6.60) — this is correct and dynamic, **not** hardcoded to the old −2.5 value despite a stale inline comment at `CatapultLauncher.cs:168` and similar stale wording in `SceneSetup.cs` still saying "top at Y=-2.5" (cosmetic comment drift only, not a functional bug — unlike the L02–L06 level-data issue, which is real). **Correction 2026-07-01: `SceneSetup.EnsureGround()` no longer generates visual ground layers** — it's physics-collider-only now (`BoxCollider2D` + `Rigidbody2D` at surface Y=−6.60, no `SpriteRenderer`) and actively *deletes* any leftover code-generated layers named `GroundFill`/`GrassBase`/`GrassTips`/`SoilEdge`/`GrassTop` from older runs. Ground/grass visuals are entirely user-authored scene GameObjects now (own naming, own sortingOrder — verify they're ≤1 per the scenery convention below, since a same-order tie with hand-placed props like the trebuchet falls back to Z-distance sorting, which caused the trebuchet-invisibility bug fixed this session).
- **LevelLoader is auto-found:** `CatapultLauncher.Awake()` does `FindAnyObjectByType<LevelLoader>()` — Inspector wiring optional.
- **[SerializeField] stale value trap:** changing a `[SerializeField]` default in code does NOT affect already-serialised components. Use `private const` for values that must not be overridden.
- Ground collider: `localScale=(60,0.5,1)`, `BoxCollider2D.size=(1,1)` → world collider 60×0.5, top edge at Y=−2.5. Never set both scale AND size to large values.
- **Effective mass formula:** both dynamic → `(mA × mB) / (mA + mB)`; one static → `movingBody.mass × 0.6`. Damage threshold impulse > 1.5. Damage = `impulse × 1.0` (no multiplier — blocks and robots both use ×1.0).
- **Robot spawn invincibility:** `RobotEnemy.Initialise()` sets `_invincibleUntil = Time.time + 0.8f`. `OnCollisionEnter2D` returns early while invincible — prevents instant death from fall-settling onto blocks when levels load.
- **Scenery sortingOrder rule:** decorative props (SceneryBuilder) must use `sortingOrder ≤ 1`. Blocks are `sortingOrder=2`, robots `3`. Props with white-background PNGs (before `remove_backgrounds.py`) at sortingOrder=2 visually cover blocks, making structures appear missing. Always keep props behind gameplay elements.
- **SceneryBuilder** (`Scripts/Core/SceneryBuilder.cs`): subscribes to `GameManager.OnLevelStarted` in `Start()`. When `_useExactPlacement=true` AND `levelIdx=0`: returns immediately — Level 1 scenery is hand-authored as permanent scene objects under `Scenery_L1` GO. For all other levels: deterministic `System.Random(levelIdx × 137 + 42)` RNG layout. `Place()` bottom-anchors sprites via `pivot.y / pixelsPerUnit * scale`. `PlaceExact()` corrects for native sprite size: `localScale = desired_world_size / native_sprite_size` (so Canva Sx=W/100 formula is correct regardless of PPU). FarmSilo excluded from all placement paths.

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
Main Camera           (at 0, −2, −10; orthoSize=4.5)
Global Light 2D
GameManager           (GameManager.cs, DontDestroyOnLoad)
LevelLoader           (LevelLoader.cs)
ScoreManager          (ScoreManager.cs)
HUD                   (HUDController.cs)
Background_SkyV1      (SpriteRenderer — sky painting)
Launcher              (CatapultLauncher.cs, at world pos −2.327, −6.60, 0 — abstract aim-math
                        anchor only since 2026-07-02, no visual meaning any more)
FarmCannon            (visual launcher since 2026-07-02, replaced Trabuchet_Body/Arm/Swing —
                        single static SpriteRenderer, Cannon.png, at −4.5, −2.5, 2, scale
                        2.2×1.8, sortingOrder=4; position recoils on fire via CatapultLauncher's
                        CannonFireSequence()/RecoilTo(), otherwise stays put — no rotation)
Scenery               (SceneryBuilder.cs — World1Prop sprite refs; _useExactPlacement=true skips L1)
[L1 scenery GOs]      OldBarn_Right, OakTree, GnarledTree, Windmill, WoodenFence×4,
                        Haybail×4 (superseded by gameplay HaybaleBlock instances at the same
                        spots as of the L01 hay-pile fix — should be deleted, not yet confirmed
                        done), Rock×2, GrassTuft×5, WildFlowers
BlockParent           (empty holder for code-spawned blocks)
RobotParent           (empty holder for code-spawned robots)
Ground                (tag="Ground", layer=6; top edge at Y=−6.60; 5 visual layers below)
```

### Prefabs
```
Prefabs/Animals/     CluckAnimal, BessieAnimal, PercyAnimal, WoollyAnimal,
                     DuckyAnimal, HoraceAnimal, GeraldAnimal, BillyAnimal, Egg
Prefabs/Blocks/      WoodBlock, StoneBlock, HaybaleBlock (WoodBlock component + Haybail.png art)
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
Add `LevelData` ScriptableObject in `Assets/ScriptableObjects/Levels/` with filename `LXX_<Name>.asset` (alphabetical order = load order). Run **Wire Scene References**. Or add a `Make(...)` call to `LevelDataGenerator` and run **FarmFury → Generate All Level Data** — but see Audit Findings first: the L01 entry in that generator is currently stale and bulk regeneration will destroy the live L01 asset.

```
Y convention: Ground surface = −6.60. Robot center (h=0.9, scale 0.6×0.9) → world y = −6.15 (sits on ground).
              Wood plank (h=0.4) resting on ground → center at world y ≈ −6.40. Stack upward by block height.
              (Do not use the old −2.5-ground-relative numbers seen in L02–L06 or in LevelDataGenerator.cs
              comments — those predate the coordinate rebuild and are the source of the current L02–L06 bug.)
X convention: Launcher at X=−2.327. Place structures to the right of it (positive X), reachable by the arc.
```
