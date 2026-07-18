// FarmFury — Editor utility. Run via menu: FarmFury ▶ Generate All Level Data
// Coordinate system (current, post-rebuild): ground surface Y = -6.60, launcher X = -2.327.
// Block/robot positions are raw world-space — LevelLoader applies no offset at spawn time.
//
// L01-L09 all exist below as of 2026-07-11 (L06 built out of order, after L07 — nothing
// requires strict numeric sequencing since GameManager auto-discovers every LevelData asset
// regardless of gaps). The original auto-generated L06 (on the OLD pre-rebuild coordinate system,
// ground Y=-2.5/launcher X=-5.5, rigid-translated by a fixed delta dx=+3.173/dy=-4.10, never
// hand-built/verified) was removed 2026-07-09 and replaced by a real hand-built layout 2026-07-10.
// Every level from L02 onward is built individually via LevelLayoutDumper (drag real content into
// the Scene view, dump to code — see that file's header comment). See DeleteStaleAsset below for
// how old generated assets get cleaned up when a level's Make(...) call is removed or renamed.
// Bessie debuts at L07 (revised 2026-07-10 — an earlier same-day plan had her debuting at L05
// instead) — L06 stays Cluck-only even though it was built after L07, since L06 sits before L07
// in level order/progression.

using UnityEngine;
using UnityEditor;

public static class LevelDataGenerator
{
    [MenuItem("FarmFury/Generate All Level Data")]
    public static void GenerateAll() => GenerateAll(silent: false);

    public static void GenerateAll(bool silent)
    {
        const string folder = "Assets/ScriptableObjects/Levels";
        EnsureFolder("Assets/ScriptableObjects", "Levels");

        // ── W1_L01  First Contact ─────────────────────────────────────────────
        // Tutorial level. Player shoots Cluck through a loose pile of hay bales
        // to destroy the HarvesterRobot sheltering behind them.
        //
        // Coordinates use the CURRENT (post-rebuild) system: ground Y=-6.60, launcher
        // X=-2.327. This entry was previously out of sync with the live hand-edited
        // asset (old 2.5-ground-relative system) — see CLAUDE.md Audit Findings. Now
        // kept in sync: regenerating via "Generate All Level Data" reproduces this design.
        //
        // HarvesterRobot position/scale — user-provided ground truth, updated whenever the user
        // reports it's still wrong (2026-07-09: (5.6,-5.25); 2026-07-14: (5.7,-5.36), scale
        // (5.7065,7.009) unchanged both times). Do not re-derive this from camera/sprite-padding
        // math — three earlier rounds of Claude's own calculations all came back "still wrong".
        // BoxCollider2D is re-derived in LevelLoader.SpawnRobot() to stay pinned to the default
        // 0.6×0.9 world-space hitbox regardless of visual scale (fixed 2026-07-01 — previously
        // the collider inherited the full scale, deeply overlapping the ground at spawn and
        // getting launched into the air by physics separation) — this formula is scale-agnostic,
        // so position/scale changes here need no corresponding code change.
        //
        // Hay pile: base 3 positions originally matched the hand-placed decorative
        // "Haybail"/"Haybail (1-3)" scene GameObjects (those 4 decorative scene GOs were since
        // deleted, round 6 — superseded by these gameplay blocks). The internal stack gap (top
        // bale above the base row) was correctly recomputed 2026-07-14 from Haybail.png's real
        // pixel content (PIL-measured: 500x500 canvas at PPU=512, trimmed content 500x419px ->
        // 0.7365 units tall at scale 0.9, not the nominal 0.9) — that gap was NOT the bug being
        // fixed 2026-07-18. What was never checked until then: the base row's height *above the
        // true ground line* (Y=-6.60) — those Y values were copied straight from the user's old
        // decorative placement without ever validating them against the ground, and sat ~0.63
        // units above where a bale's bottom edge should actually touch (-6.60 + 0.7365/2 =
        // -6.232 for a bale centred there), i.e. the whole pile was floating with a visible gap
        // beneath it — reported as "the top haybail is still too high" from a screenshot showing
        // exactly that gap. Fixed by shifting all 4 Y values down by the same 0.627u so the
        // lowest base bale now rests at the ground line (preserving the base row's original
        // relative unevenness — an intentional hand-placed "pile" look, not a flat stack — and
        // the top bale's already-correct 0.7365u gap above the base row moves down with it).
        // hp=10 (fixed 2026-07-01, was 60 — that survived multiple hits instead of exploding in
        // one; a typical Cluck impact does ~15-20 impulse damage, so 10 reliably one-shots it).
        // passThrough=true lets Cluck punch through at 70% speed and continue to the robot.
        // Y values shifted up by +1.0 on 2026-07-26 (user-reported: pile sat below the camera's
        // visible safe line) — the previous baseline (ground-touching, per the 2026-07-18 fix
        // below) put the lowest bale's bottom edge at Y~-6.73, well past the camera's own visible
        // bottom (Y=-6.5 at rest — see CLAUDE.md Coordinate System) and therefore cropped/invisible
        // regardless of the physics ground being correct. Since GroundVisual_Placeholder's visible
        // grass top edge is Y=-5.3 (added the same day for the same underlying reason — the true
        // physics ground is basically off-screen), +1.0 lands the pile's lowest bottom edge at
        // Y~-5.73 — comfortably clear of the camera cutoff and roughly matching where the other
        // hand-placed props (barn/tree/fence) already sit visually, rather than the true (mostly
        // off-screen) physics ground line. All 4 bales stay _stayKinematic (see BlockBase.cs), so
        // once placed here none of them will ever move again, struck or not.
        // Top bale re-lowered 0.35u + recentred 2026-07-06 (see its own comment below) — the
        // "already-correct 0.7365u gap" note above refers to the *internal* single-bale-height
        // math, which was fine; the complaint this time was the cap bale balancing on a single
        // point above one base bale looking precarious/disconnected, not the gap size itself.
        // Layout replaced 2026-07-17 with a fresh hand-placed dump against the new
        // ParallaxMidground.png-only backdrop (FarmFury -> Debug -> Dump Level Layout To Log,
        // unity/Logs/level_layout_dump.txt) — user confirmed "the haybale and robot is the new
        // level 1". The raw dump also contained 2 Wood blocks sourced from 'WoodenFence' art at
        // (3.34,-5.368) and (1.55,-5.34) — deliberately EXCLUDED here, not just overlooked: those
        // coordinates sit almost exactly on top of the permanent decorative WoodenFence/
        // WoodenFence(1) scene objects (at 3.4887,-5.2833 and 2,-5.28), the same "decorative fence
        // prop picked up as gameplay blocks by mistake" bug already hit and fixed once before
        // (2026-07-12, L02) — and the user's own description of this dump ("the haybale and robot
        // is the new level 1") already scopes the real content to just those two. SemiHarvester
        // scale (3.564, 3.975) recorded here as the new standard size going forward — see chat
        // for the full callout, this replaces every earlier per-level custom SemiHarvester scale.
        // Not visually re-verified (no Play-mode access here).
        Make(folder, "L01_FirstContact",
            id: "W1_L01", name: "First Contact", par: 2,
            birds: new[] { AnimalType.Cluck, AnimalType.Cluck, AnimalType.Cluck },
            blocks: new[]
            {
                B(BlockType.Haybale, 5.48f, -5.39f, 0.977f, 0.977f, passThrough: true, hp: 10f, mass: 3f), // sprite 'Haybail'
                B(BlockType.Haybale, 5.44f, -5.58f, 0.977f, 0.977f, passThrough: true, hp: 10f, mass: 3f), // sprite 'Haybail'
                B(BlockType.Haybale, 4.82f, -5.45f, 0.977f, 0.977f, passThrough: true, hp: 10f, mass: 3f), // sprite 'Haybail'
                B(BlockType.Haybale, 5.21f, -4.85f, 0.977f, 0.977f, passThrough: true, hp: 10f, mass: 3f), // sprite 'Haybail'
            },
            robots: new[]
            {
                R(6.309f, -5.36f, 3.564f, 3.975f, RobotType.SemiHarvester), // sprite 'Robot_SemiHarvest' — new standard SemiHarvester size
            });

        // ── W1_L02  Harvest Yard ──────────────────────────────────────────────
        // Redesigned 2026-07-18 with a fresh hand-placed dump (FarmFury -> Debug -> Dump Level
        // Layout To Log, unity/Logs/level_layout_dump.txt) — user request: "i have redesigned
        // level2 - delete old level2 - update with new dump". Old layout (4 haybale + 2
        // SemiHarvester, no wood/barrels) fully replaced: 3 haybale + a small 3-plank wood
        // cluster (2 vertical Plank_Shork + 1 horizontal Plank_Horizontal bridging them) +
        // 2 ExplodingBarrel props, guarded by 3 SemiHarvester. Kept the same asset filename
        // ("L02_StoneWall") and id/name so no other reference needs updating — regenerating via
        // "Generate All Level Data" overwrites this asset in place, no separate deletion needed.
        // Not visually re-verified (no Play-mode access here).
        Make(folder, "L02_StoneWall",
            id: "W1_L02", name: "Harvest Yard", par: 2,
            birds: new[] { AnimalType.Cluck, AnimalType.Cluck, AnimalType.Cluck },
            blocks: new[]
            {
                B(BlockType.Haybale, 3.55f, -5.43f, 0.977f, 0.977f, passThrough: true, hp: 10f, mass: 3f), // sprite 'Haybail'
                B(BlockType.Haybale, 3.89f, -5.5f,  0.977f, 0.977f, passThrough: true, hp: 10f, mass: 3f), // sprite 'Haybail'
                B(BlockType.Haybale, 3.68f, -4.95f, 0.977f, 0.977f, passThrough: true, hp: 10f, mass: 3f), // sprite 'Haybail'
                B(BlockType.Wood, 4.53f, -5.18f, 0.592f, 1.256f, artVariant: WoodArtVariant.Vertical), // sprite 'Plank_Shork'
                B(BlockType.Wood, 4.9f,  -5.22f, 0.592f, 1.256f, artVariant: WoodArtVariant.Vertical), // sprite 'Plank_Shork'
                B(BlockType.Wood, 4.65f, -4.56f, 1f,     0.492f, artVariant: WoodArtVariant.Horizontal), // sprite 'Plank_Horizontal'
                B(BlockType.Barrel, 6.94f, -5.17f, 0.977f, 0.977f), // sprite 'Barrel_Dynamite'
                B(BlockType.Barrel, 7.54f, -5.21f, 0.977f, 0.977f), // sprite 'Barrel_Dynamite'
            },
            robots: new[]
            {
                R(6.24f,  -5.165f, 4.036f, 4.47f,  RobotType.SemiHarvester), // sprite 'Robot_SemiHarvest'
                R(4.706f, -4.136f, 3.726f, 3.974f, RobotType.SemiHarvester), // sprite 'Robot_SemiHarvest'
                R(7.238f, -3.965f, 3.602f, 3.912f, RobotType.SemiHarvester), // sprite 'Robot_SemiHarvest'
            });

        // ── W1_L03  ────────────────────────────────────────────────────────────
        // Layout re-simplified 2026-07-12 (user report: "level 3 is a bit difficult - let me
        // redesign it") — the previous rebuild (also 2026-07-12, same day) mixed haybale + wood
        // planks; this redesign drops the wood entirely and uses 6 haybale (all one-hit-kill,
        // passThrough) + 3 SemiHarvester, an easier structural read for an early level. Real
        // per-sprite scale/hp values captured directly from the Scene view. Not visually
        // re-verified (no Play-mode access here).
        Make(folder, "L03_TheTower",
            id: "W1_L03", name: "The Tower", par: 2,
            birds: new[] { AnimalType.Cluck, AnimalType.Cluck, AnimalType.Cluck },
            blocks: new[]
            {
                B(BlockType.Haybale, 3.686f, -5.153f, 1.188f, 1.143f, passThrough: true, hp: 10f, mass: 3f), // sprite 'Haybail'
                B(BlockType.Haybale, 4.14f,  -5.26f,  1.188f, 1.143f, passThrough: true, hp: 10f, mass: 3f), // sprite 'Haybail'
                B(BlockType.Haybale, 3.94f,  -4.53f,  1.188f, 1.143f, passThrough: true, hp: 10f, mass: 3f), // sprite 'Haybail'
                B(BlockType.Haybale, 6.42f,  -5.14f,  1.188f, 1.143f, passThrough: true, hp: 10f, mass: 3f), // sprite 'Haybail'
                B(BlockType.Haybale, 6.89f,  -5.25f,  1.188f, 1.143f, passThrough: true, hp: 10f, mass: 3f), // sprite 'Haybail'
                B(BlockType.Haybale, 6.65f,  -4.57f,  1.188f, 1.143f, passThrough: true, hp: 10f, mass: 3f), // sprite 'Haybail'
            },
            robots: new[]
            {
                R(5.28f, -5.14f, 5.914f, 5.967f, RobotType.SemiHarvester), // sprite 'Robot_SemiHarvest'
                R(3.98f, -3.76f, 5.914f, 5.967f, RobotType.SemiHarvester), // sprite 'Robot_SemiHarvest'
                R(6.67f, -3.79f, 5.914f, 5.967f, RobotType.SemiHarvester), // sprite 'Robot_SemiHarvest'
            });

        // ── W1_L04  ────────────────────────────────────────────────────────────
        // Layout replaced 2026-07-12 with the user's own hand-placed dump (FarmFury -> Debug ->
        // Dump Level Layout To Log, unity/Logs/level_layout_dump.txt) — part of the L01-L18
        // overhaul (L01-L04 SemiHarvester-only, Harvester introduced L05-L09, Bessie L10, full
        // roster L11-L17, Captain L18). All 4 robots are SemiHarvester (Harvester removed from
        // this level, matching the new L01-L04 SemiHarvester-only convention) — the densest
        // layout of the four so far (2-tier wood stack + a dynamite barrel). Cluck's Cluster
        // Bomb ability still unlocks here (AnimalBase.AbilityIntroLevelIndex = 3, 0-based, i.e.
        // L04) — unaffected by this layout swap.
        //
        // Compacted 2026-07-12 (same-day user report: "exceeds the safe area - move the sprites
        // closer together"): the raw dump's barrel reached X~9.2 (right edge), well past every
        // other level's established ~7.4 right edge. Pivot-and-compress applied — pivot = leftmost
        // haybale (X=4.632, held fixed), factor=0.7, landing the barrel/widest robot at X~7.4. Y
        // values untouched; a pure linear X compression only shrinks gaps, never breaks existing
        // physical relationships (wood stack, robots resting near it). Real per-sprite scale/hp
        // values captured directly from the Scene view. Not visually re-verified (no Play-mode
        // access here).
        // Widened 2026-07-13 (user report: "level 4 is repositioned too close together — slightly
        // reposition wider"): every X coordinate scaled 20% outward from the layout's own centre
        // pivot (X=6.02, the midpoint of the previous 4.632-7.408 span) — a pure linear expansion
        // around a fixed centre only grows gaps, never breaks the existing physical relationships
        // (wood stack, robots resting near/on it). Y values untouched. Now safe to exceed the old
        // ~7.4 "safe area" edge — the per-level camera auto-zoom (CatapultLauncher.
        // ComputeOrthoSizeForLevel) added 2026-07-11 fits the camera to each level's actual content
        // bounds automatically, so a wider layout just zooms out rather than clipping off-screen.
        Make(folder, "L04_EggPractice",
            id: "W1_L04", name: "Egg Practice", par: 2,
            birds: new[] { AnimalType.Cluck, AnimalType.Cluck, AnimalType.Cluck },
            blocks: new[]
            {
                B(BlockType.Haybale, 4.354f, -5.151f, 1.138f, 1.111f, passThrough: true, hp: 10f, mass: 3f), // sprite 'Haybail'
                B(BlockType.Haybale, 5.110f, -5.151f, 1.138f, 1.111f, passThrough: true, hp: 10f, mass: 3f), // sprite 'Haybail'
                B(BlockType.Haybale, 6.354f, -5.091f, 1.138f, 1.111f, passThrough: true, hp: 10f, mass: 3f), // sprite 'Haybail'
                B(BlockType.Wood,    5.740f, -5.111f, 1f, 1.064f, artVariant: WoodArtVariant.Shork2D),   // sprite 'Plank_2DShork'
                B(BlockType.Wood,    6.186f, -3.981f, 1f, 1.064f, artVariant: WoodArtVariant.Shork2D),   // sprite 'Plank_2DShork'
                B(BlockType.Wood,    5.732f, -4.55f,  1f, 0.492f, artVariant: WoodArtVariant.Horizontal), // sprite 'Plank_Horizontal'
                B(BlockType.Wood,    6.496f, -4.56f,  1f, 0.492f, artVariant: WoodArtVariant.Horizontal), // sprite 'Plank_Horizontal'
                B(BlockType.Wood,    6.454f, -3.561f, 1f, 0.467f, artVariant: WoodArtVariant.Horizontal), // sprite 'Plank_Horizontal'
                B(BlockType.Wood,    5.682f, -3.561f, 1f, 0.467f, artVariant: WoodArtVariant.Horizontal), // sprite 'Plank_Horizontal'
                B(BlockType.Barrel,  7.686f, -4.953f, 1.256f, 1.216f), // sprite 'Barrel_Dynamite'
            },
            robots: new[]
            {
                R(7.656f, -4.141f, 5.734f, 5.452f, RobotType.SemiHarvester), // sprite 'Robot_SemiHarvest'
                R(6.580f, -4.151f, 5.734f, 5.452f, RobotType.SemiHarvester), // sprite 'Robot_SemiHarvest'
                R(5.707f, -4.14f,  5.734f, 5.452f, RobotType.SemiHarvester), // sprite 'Robot_SemiHarvest'
                R(6.085f, -2.961f, 5.734f, 5.452f, RobotType.SemiHarvester), // sprite 'Robot_SemiHarvest'
            });

        // ── W1_L05  The Gauntlet ──────────────────────────────────────────────
        // Layout shifted up 2026-07-12 (user report: the previous dump was rendering over the
        // safe area) — re-dumped from the Scene view (FarmFury -> Debug -> Dump Level Layout To
        // Log, unity/Logs/level_layout_dump.txt) with the whole structure raised and shifted
        // left, still the L01-L18 overhaul's Harvester introduction: 4 SemiHarvester + 1
        // Harvester. Real per-sprite scale/hp values captured directly from the Scene view. Not
        // visually re-verified (no Play-mode access here).
        Make(folder, "L05_TheGauntlet",
            id: "W1_L05", name: "The Gauntlet", par: 2,
            birds: new[] { AnimalType.Cluck, AnimalType.Cluck, AnimalType.Cluck },
            blocks: new[]
            {
                B(BlockType.Haybale, 3.932f, -5.201f, 1.138f, 1.111f, passThrough: true, hp: 10f, mass: 3f), // sprite 'Haybail'
                B(BlockType.Haybale, 7.912f, -5.161f, 1.138f, 1.111f, passThrough: true, hp: 10f, mass: 3f), // sprite 'Haybail'
                B(BlockType.Haybale, 4.032f, -4.211f, 1.138f, 1.111f, passThrough: true, hp: 10f, mass: 3f), // sprite 'Haybail'
                B(BlockType.Wood,    3.202f, -5.251f, 1f, 1.064f, artVariant: WoodArtVariant.Shork2D),   // sprite 'Plank_2DShork'
                B(BlockType.Wood,    4.612f, -5.191f, 1f, 1.064f, artVariant: WoodArtVariant.Shork2D),   // sprite 'Plank_2DShork'
                B(BlockType.Wood,    3.282f, -4.331f, 1f, 1.064f, artVariant: WoodArtVariant.Shork2D),   // sprite 'Plank_2DShork'
                B(BlockType.Wood,    2.622f, -4.681f, 1f, 0.492f, artVariant: WoodArtVariant.Horizontal), // sprite 'Plank_Horizontal'
                B(BlockType.Wood,    3.912f, -4.721f, 1f, 0.492f, artVariant: WoodArtVariant.Horizontal), // sprite 'Plank_Horizontal'
                B(BlockType.Wood,    3.812f, -3.721f, 1f, 0.467f, artVariant: WoodArtVariant.Horizontal), // sprite 'Plank_Horizontal'
                B(BlockType.Wood,    2.892f, -3.701f, 1f, 0.467f, artVariant: WoodArtVariant.Horizontal), // sprite 'Plank_Horizontal'
                B(BlockType.Barrel,  2.462f, -5.171f, 1.256f, 1.216f), // sprite 'Barrel_Dynamite'
                B(BlockType.Barrel,  6.922f, -5.101f, 1.256f, 1.216f), // sprite 'Barrel_Dynamite'
            },
            robots: new[]
            {
                R(2.972f, -3.091f, 5.734f, 5.452f, RobotType.SemiHarvester), // sprite 'Robot_SemiHarvest'
                R(3.842f, -3.081f, 5.734f, 5.452f, RobotType.SemiHarvester), // sprite 'Robot_SemiHarvest'
                R(2.642f, -4.251f, 5.734f, 5.452f, RobotType.SemiHarvester), // sprite 'Robot_SemiHarvest'
                R(7.812f, -4.391f, 5.734f, 5.452f, RobotType.SemiHarvester), // sprite 'Robot_SemiHarvest'
                R(5.714f, -4.774f, 6.594f, 8.548f, RobotType.Harvester),     // sprite 'HarvesterRobot'
            });

        // Old L05 asset filename, orphaned by the rename above (L05_BessiesDebut -> L05_TheGauntlet
        // — GameManager auto-loads every LevelData asset it finds in this folder, so the stale
        // file would otherwise linger as a phantom extra level).
        DeleteStaleAsset(folder, "L05_BessiesDebut");

        DeleteStaleAsset(folder, "L06_BessiesDebut"); // stale old-plan filename, orphaned since the L05/L07 rename

        // ── W1_L06  Double Barrel ─────────────────────────────────────────────
        // Layout replaced 2026-07-12 with the user's own hand-placed dump (FarmFury -> Debug ->
        // Dump Level Layout To Log, unity/Logs/level_layout_dump.txt) — part of the L01-L18
        // overhaul (L01-L04 SemiHarvester-only, Harvester introduced L05-L09, Bessie L10, full
        // roster L11-L17, Captain L18). Caught late (2026-07-12, user report: "from level 6 the
        // scenes I designed have not rendered correctly") — this level had been skipped over
        // entirely earlier in the same rebuild session, still running its original 2026-07-10
        // pre-overhaul layout until now. 2 Harvester + 3 SemiHarvester, 2 dynamite barrels, 3
        // haybale, a stone block, and a dense wood scaffold. birds[] stays 3x Cluck, matching
        // L05. Real per-sprite scale/hp values captured directly from the Scene view. Not
        // visually re-verified (no Play-mode access here).
        Make(folder, "L06_DoubleBarrel",
            id: "W1_L06", name: "Double Barrel", par: 2,
            birds: new[] { AnimalType.Cluck, AnimalType.Cluck, AnimalType.Cluck },
            blocks: new[]
            {
                B(BlockType.Haybale, 2.34f, -5.51f, 0.977f, 0.977f, passThrough: true, hp: 10f, mass: 3f), // sprite 'Haybail'
                B(BlockType.Haybale, 3.07f, -5.53f, 0.977f, 0.977f, passThrough: true, hp: 10f, mass: 3f), // sprite 'Haybail'
                B(BlockType.Haybale, 2.72f, -4.96f, 0.977f, 0.977f, passThrough: true, hp: 10f, mass: 3f), // sprite 'Haybail'
                B(BlockType.Wood,    3.88f, -5.57f, 1f, 0.81f,  artVariant: WoodArtVariant.Short), // sprite 'Plank_Short'
                B(BlockType.Wood,    4.47f, -5.59f, 1f, 0.81f,  artVariant: WoodArtVariant.Short), // sprite 'Plank_Short'
                B(BlockType.Wood,    5.08f, -5.61f, 1f, 0.81f,  artVariant: WoodArtVariant.Short), // sprite 'Plank_Short'
                B(BlockType.Wood,    4.17f, -5f,    1f, 0.81f,  artVariant: WoodArtVariant.Short), // sprite 'Plank_Short'
                B(BlockType.Stone,   4.15f, -4.43f, 1f, 1f, artVariant: WoodArtVariant.Square), // sprite 'Stone_Square'
                B(BlockType.Barrel,  4.968f, -4.71f, 1.309f, 1.294f), // sprite 'Barrel_Dynamite'
                B(BlockType.Barrel,  7.3f,   -5.31f, 1.309f, 1.294f), // sprite 'Barrel_Dynamite'
                B(BlockType.Wood,    6.65f, -5.47f, 1f, 1.064f, artVariant: WoodArtVariant.Vertical), // sprite '2D_Block_Wood_Vertical'
                B(BlockType.Wood,    6.68f, -4.7f,  1f, 1.064f, artVariant: WoodArtVariant.Vertical), // sprite '2D_Block_Wood_Vertical'
                B(BlockType.Wood,    4.3f,  -3.97f, 1f, 0.492f, artVariant: WoodArtVariant.Horizontal), // sprite 'Plank_Horizontal'
                B(BlockType.Wood,    5.22f, -3.97f, 1f, 0.492f, artVariant: WoodArtVariant.Horizontal), // sprite 'Plank_Horizontal'
                B(BlockType.Wood,    6.16f, -3.99f, 1f, 0.492f, artVariant: WoodArtVariant.Horizontal), // sprite 'Plank_Horizontal'
            },
            robots: new[]
            {
                R(4.58f, -3.13f, 5.101f, 7.281f, RobotType.Harvester),     // sprite 'HarvesterRobot'
                R(5.96f, -3.13f, 5.101f, 7.281f, RobotType.Harvester),     // sprite 'HarvesterRobot'
                R(5.94f, -5.32f, 6.019f, 6.178f, RobotType.SemiHarvester), // sprite 'Robot_SemiHarvest'
                R(7.24f, -4.37f, 6.019f, 6.178f, RobotType.SemiHarvester), // sprite 'Robot_SemiHarvest'
                R(2.82f, -4.14f, 6.125f, 6.283f, RobotType.SemiHarvester), // sprite 'Robot_SemiHarvest'
            });

        // ── W1_L07  Barrel Row ────────────────────────────────────────────────
        // Layout replaced 2026-07-12 with the user's own hand-placed dump (FarmFury -> Debug ->
        // Dump Level Layout To Log, unity/Logs/level_layout_dump.txt) — part of the L01-L18
        // overhaul (L01-L04 SemiHarvester-only, Harvester introduced L05-L09, Bessie L10, full
        // roster L11-L17, Captain L18). Renamed off its old "Bessie's Debut" name/birds[] — user
        // confirmed 2026-07-12 the plan moved: "bessie debut is now only in level 10 when I
        // introduce the third robot - for now only cluck and his power of shooting eggs". birds[]
        // reverted to 3x Cluck, matching L05/L06/L08/L09. 2 Harvester + 4 SemiHarvester, a
        // WoodenCart prop, 2 dynamite barrels, 2 haybale, and 9 wood planks — real per-sprite
        // scale/hp values captured directly from the Scene view. Not visually re-verified (no
        // Play-mode access here).
        Make(folder, "L07_BarrelRow",
            id: "W1_L07", name: "Barrel Row", par: 2,
            birds: new[] { AnimalType.Cluck, AnimalType.Cluck, AnimalType.Cluck },
            blocks: new[]
            {
                B(BlockType.Wood,    2.48f, -5.29f,  1.722f, 1.421f, artVariant: WoodArtVariant.Cart), // sprite 'WoodenCart'
                B(BlockType.Barrel,  3.83f, -5.29f,  1.326f, 1.294f), // sprite 'Barrel_Dynamite'
                B(BlockType.Barrel,  7.51f, -4.29f,  1.326f, 1.294f), // sprite 'Barrel_Dynamite'
                B(BlockType.Haybale, 8.43f, -5.2f,   1.246f, 1.278f, passThrough: true, hp: 10f, mass: 3f), // sprite 'Haybail'
                B(BlockType.Haybale, 4.78f, -5.23f,  1.246f, 1.278f, passThrough: true, hp: 10f, mass: 3f), // sprite 'Haybail'
                B(BlockType.Wood,    4.1f,  -4.54f,  1f, 1.064f, artVariant: WoodArtVariant.Shork2D), // sprite 'Plank_2DShork'
                B(BlockType.Wood,    7.43f, -5.33f,  1f, 1.064f, artVariant: WoodArtVariant.Shork2D), // sprite 'Plank_2DShork'
                B(BlockType.Wood,    7.81f, -5.35f,  1f, 1.064f, artVariant: WoodArtVariant.Shork2D), // sprite 'Plank_2DShork'
                B(BlockType.Wood,    7.35f, -3.56f,  1f, 1.064f, artVariant: WoodArtVariant.Shork2D), // sprite 'Plank_2DShork'
                B(BlockType.Wood,    7.81f, -3.53f,  1f, 1.064f, artVariant: WoodArtVariant.Shork2D), // sprite 'Plank_2DShork'
                B(BlockType.Wood,    3.73f, -4.53f,  1f, 1.064f, artVariant: WoodArtVariant.Shork2D), // sprite 'Plank_2DShork'
                B(BlockType.Wood,    3.82f, -4.02f,  1f, 1f,     artVariant: WoodArtVariant.Horizontal2D), // sprite 'Plank_2DHorizontal'
                B(BlockType.Wood,    7.5f,  -3.1f,   1f, 1f,     artVariant: WoodArtVariant.Horizontal2D), // sprite 'Plank_2DHorizontal'
                B(BlockType.Wood,    5.79f, -5.43f,  1f, 0.81f,  artVariant: WoodArtVariant.Short), // sprite 'Plank_Short'
                B(BlockType.Wood,    5.82f, -3.92f,  1f, 0.81f,  artVariant: WoodArtVariant.Short), // sprite 'Plank_Short'
                B(BlockType.Wood,    5.73f, -1.9f,   1f, 0.81f,  artVariant: WoodArtVariant.Short), // sprite 'Plank_Short'
            },
            robots: new[]
            {
                R(5.73f, -2.82f,  6.07f,  7.858f, RobotType.Harvester),     // sprite 'HarvesterRobot'
                R(7.51f, -2.25f,  6.07f,  7.858f, RobotType.Harvester),     // sprite 'HarvesterRobot'
                R(3.82f, -3.51f,  5.156f, 5.266f, RobotType.SemiHarvester), // sprite 'Robot_SemiHarvest'
                R(5.72f, -1.19f,  5.156f, 5.266f, RobotType.SemiHarvester), // sprite 'Robot_SemiHarvest'
                R(8.39f, -4.34f,  5.156f, 5.266f, RobotType.SemiHarvester), // sprite 'Robot_SemiHarvest'
                R(5.77f, -4.7f,   5.156f, 5.266f, RobotType.SemiHarvester), // sprite 'Robot_SemiHarvest'
            });

        // Old L07 asset filename, orphaned by the rename above (L07_BessiesDebut -> L07_BarrelRow
        // — GameManager auto-loads every LevelData asset it finds in this folder, so the stale
        // file would otherwise linger as a phantom extra level).
        DeleteStaleAsset(folder, "L07_BessiesDebut");

        // ── W1_L08  Fortress Assault ──────────────────────────────────────────
        // Layout replaced 2026-07-12 with the user's own hand-placed dump (FarmFury -> Debug ->
        // Dump Level Layout To Log, unity/Logs/level_layout_dump.txt) — part of the L01-L18
        // overhaul (L01-L04 SemiHarvester-only, Harvester introduced L05-L09, Bessie L10, full
        // roster L11-L17, Captain L18). birds[] switched from Cluck/Cluck/Bessie to 3x Cluck
        // (matching L05-L07) — Bessie's debut moved to L10 (user confirmed 2026-07-12). 3
        // Harvester + 4 SemiHarvester, a WoodenCart + 2 WoodenBarrel props (both classified as
        // plain structural BlockType.Wood, not explosive — see LevelLayoutDumper.cs's "dynamite"
        // keyword fix, same day), 1 dynamite barrel, 1 haybale, 2 vertical stone pillars, and a
        // tall multi-tier wood tower. Real per-sprite scale/hp values captured directly from the
        // Scene view. Not visually re-verified (no Play-mode access here) — worth a live check
        // given this is the heaviest robot count shipped so far (7 robots).
        Make(folder, "L08_FortressAssault",
            id: "W1_L08", name: "Fortress Assault", par: 3,
            birds: new[] { AnimalType.Cluck, AnimalType.Cluck, AnimalType.Cluck },
            blocks: new[]
            {
                B(BlockType.Wood,    2.08f, -5.26f, 1.486f, 1.265f, artVariant: WoodArtVariant.Cart), // sprite 'WoodenCart'
                B(BlockType.Wood,    3.15f, -5.27f, 0.977f, 0.977f, artVariant: WoodArtVariant.Barrel), // sprite 'WoodenBarrel'
                B(BlockType.Wood,    8.04f, -5.21f, 0.977f, 0.977f, artVariant: WoodArtVariant.Barrel), // sprite 'WoodenBarrel'
                B(BlockType.Barrel,  6.64f, -5.22f, 0.977f, 0.977f), // sprite 'Barrel_Dynamite'
                B(BlockType.Haybale, 4.24f, -4.68f, 0.977f, 0.977f, passThrough: true, hp: 10f, mass: 3f), // sprite 'Haybail'
                B(BlockType.Wood,    3.79f, -5.38f, 1f, 0.81f,  artVariant: WoodArtVariant.Short), // sprite 'Plank_Short'
                B(BlockType.Wood,    4.42f, -5.37f, 1f, 0.81f,  artVariant: WoodArtVariant.Short), // sprite 'Plank_Short'
                B(BlockType.Wood,    8.08f, -4.54f, 1f, 1.064f, artVariant: WoodArtVariant.Shork2D), // sprite 'Plank_2DShork'
                B(BlockType.Wood,    8.06f, -3.63f, 1f, 1.064f, artVariant: WoodArtVariant.Shork2D), // sprite 'Plank_2DShork'
                B(BlockType.Wood,    8.05f, -2.85f, 1f, 1.064f, artVariant: WoodArtVariant.Shork2D), // sprite 'Plank_2DShork'
                B(BlockType.Wood,    6.14f, -5.2f,  1f, 1.064f, artVariant: WoodArtVariant.Shork2D), // sprite 'Plank_2DShork'
                B(BlockType.Wood,    6.13f, -4.42f, 1f, 1.064f, artVariant: WoodArtVariant.Shork2D), // sprite 'Plank_2DShork'
                B(BlockType.Wood,    6.15f, -3.62f, 1f, 1.064f, artVariant: WoodArtVariant.Shork2D), // sprite 'Plank_2DShork'
                B(BlockType.Wood,    6.3f,  -2.81f, 1f, 1f,     artVariant: WoodArtVariant.Skew), // sprite 'Plank_Skew'
                B(BlockType.Wood,    6.4f,  -2.3f,  1f, 0.492f, artVariant: WoodArtVariant.Horizontal), // sprite 'Plank_Horizontal'
                B(BlockType.Wood,    6.71f, -4.03f, 1f, 0.492f, artVariant: WoodArtVariant.Horizontal), // sprite 'Plank_Horizontal'
                B(BlockType.Wood,    7.64f, -4.04f, 1f, 0.492f, artVariant: WoodArtVariant.Horizontal), // sprite 'Plank_Horizontal'
                B(BlockType.Wood,    7.31f, -2.3f,  1f, 0.492f, artVariant: WoodArtVariant.Horizontal), // sprite 'Plank_Horizontal'
                B(BlockType.Wood,    8.17f, -2.3f,  1f, 0.492f, artVariant: WoodArtVariant.Horizontal), // sprite 'Plank_Horizontal'
                B(BlockType.Stone,   3.63f, -4.76f, 0.48f, 1f, artVariant: WoodArtVariant.Vertical), // sprite 'Stone_Vertical'
                B(BlockType.Stone,   3.63f, -4.04f, 0.48f, 1f, artVariant: WoodArtVariant.Vertical), // sprite 'Stone_Vertical'
            },
            robots: new[]
            {
                R(5.301f, -4.979f, 5.308f, 6.81f,  RobotType.Harvester),     // sprite 'HarvesterRobot'
                R(7.599f, -1.559f, 4.909f, 6.81f,  RobotType.Harvester),     // sprite 'HarvesterRobot'
                R(7.211f, -3.194f, 5.09f,  6.43f,  RobotType.Harvester),     // sprite 'HarvesterRobot'
                R(3.11f,  -4.59f,  4.589f, 4.545f, RobotType.SemiHarvester), // sprite 'Robot_SemiHarvest'
                R(6.4f,   -1.83f,  4.766f, 5.076f, RobotType.SemiHarvester), // sprite 'Robot_SemiHarvest'
                R(7.27f,  -5.15f,  5.253f, 5.386f, RobotType.SemiHarvester), // sprite 'Robot_SemiHarvest'
                R(4.17f,  -3.94f,  4.943f, 5.032f, RobotType.SemiHarvester), // sprite 'Robot_SemiHarvest'
            });

        // ── W1_L09  Stone Bastion ─────────────────────────────────────────────
        // Layout replaced 2026-07-12 with the user's own hand-placed dump (FarmFury -> Debug ->
        // Dump Level Layout To Log, unity/Logs/level_layout_dump.txt) — part of the L01-L18
        // overhaul (L01-L04 SemiHarvester-only, Harvester introduced L05-L09, Bessie L10, full
        // roster L11-L17, Captain L18). This is the last level in that Harvester-introduction
        // range: 4 Harvester + 2 SemiHarvester (heaviest Harvester count yet). birds[] switched
        // from Cluck/Cluck/Bessie to 3x Cluck (matching L05-L08) — Bessie's debut moved to L10.
        // 2 dynamite barrels, 4 haybale, a WoodenCart, 2 RuinedStoneWall pieces + 3 more Stone
        // blocks (skew/vertical), and a dense wood scaffold. Real per-sprite scale/hp values
        // captured directly from the Scene view. Not visually re-verified (no Play-mode access
        // here).
        Make(folder, "L09_StoneBastion",
            id: "W1_L09", name: "Stone Bastion", par: 3,
            birds: new[] { AnimalType.Cluck, AnimalType.Cluck, AnimalType.Cluck },
            blocks: new[]
            {
                B(BlockType.Wood,    6.79f, -3.49f,  1f, 1.064f, artVariant: WoodArtVariant.Shork2D),   // sprite 'Plank_2DShork'
                B(BlockType.Wood,    5.46f, -2.67f,  1f, 1f,     artVariant: WoodArtVariant.Skew),       // sprite 'Plank_Skew'
                B(BlockType.Wood,    7f,    -2.73f,  1f, 1f,     artVariant: WoodArtVariant.Skew),       // sprite 'Plank_Skew'
                B(BlockType.Wood,    5.56f, -2.25f,  1f, 1f,     artVariant: WoodArtVariant.Horizontal2D), // sprite 'Plank_2DHorizontal'
                B(BlockType.Wood,    7.38f, -2.23f,  1f, 1f,     artVariant: WoodArtVariant.Horizontal2D), // sprite 'Plank_2DHorizontal'
                B(BlockType.Wood,    6.47f, -2.24f,  1f, 1f,     artVariant: WoodArtVariant.Horizontal2D), // sprite 'Plank_2DHorizontal'
                B(BlockType.Barrel,  5.74f, -3.44f,  0.977f, 0.977f), // sprite 'Barrel_Dynamite'
                B(BlockType.Barrel,  6.54f, -4.74f,  0.977f, 0.977f), // sprite 'Barrel_Dynamite'
                B(BlockType.Haybale, 3.59f, -5.4f,   0.977f, 0.977f, passThrough: true, hp: 10f, mass: 3f), // sprite 'Haybail'
                B(BlockType.Haybale, 4.38f, -5.43f,  0.977f, 0.977f, passThrough: true, hp: 10f, mass: 3f), // sprite 'Haybail'
                B(BlockType.Haybale, 3.62f, -4.86f,  0.977f, 0.977f, passThrough: true, hp: 10f, mass: 3f), // sprite 'Haybail'
                B(BlockType.Wood,    2.31f, -5.34f,  1.662f, 1.383f, artVariant: WoodArtVariant.Cart),   // sprite 'WoodenCart'
                B(BlockType.Stone,   1.16f, -5.28f,  1.18f,  1.027f, artVariant: WoodArtVariant.RuinedWall), // sprite 'RuinedStoneWall'
                B(BlockType.Stone,   0.31f, -5.297f, 1.053f, 0.913f, artVariant: WoodArtVariant.RuinedWall), // sprite 'RuinedStoneWall'
                B(BlockType.Wood,    6.6f,  -5.46f,  1f, 1f, artVariant: WoodArtVariant.Diagonal), // sprite 'Plank_Diagonal'
                B(BlockType.Wood,    5.9f,  -5.47f,  1f, 1f, artVariant: WoodArtVariant.Diagonal), // sprite 'Plank_Diagonal'
                B(BlockType.Wood,    5.21f, -5.47f,  1f, 1f, artVariant: WoodArtVariant.Diagonal), // sprite 'Plank_Diagonal'
                B(BlockType.Stone,   6f,    -4.761f, 0.48f, 1.178f, artVariant: WoodArtVariant.Vertical), // sprite 'Stone_Vertical'
                B(BlockType.Stone,   5.49f, -4.01f,  1f, 1f, artVariant: WoodArtVariant.Diagonal), // sprite 'Stone_Diagonal'
                B(BlockType.Stone,   6.38f, -4.01f,  1f, 1f, artVariant: WoodArtVariant.Diagonal), // sprite 'Stone_Diagonal'
                B(BlockType.Haybale, 4.31f, -4.9f,   0.977f, 0.977f, passThrough: true, hp: 10f, mass: 3f), // sprite 'Haybail'
                B(BlockType.Wood,    5.27f, -3.46f,  1f, 1.064f, artVariant: WoodArtVariant.Shork2D), // sprite 'Plank_2DShork'
            },
            robots: new[]
            {
                R(7.715f, -5.238f, 4.837f, 5.995f, RobotType.Harvester),     // sprite 'HarvesterRobot'
                R(4.02f,  -4.05f,  4.692f, 5.724f, RobotType.Harvester),     // sprite 'HarvesterRobot'
                R(5.87f,  -1.51f,  4.692f, 5.724f, RobotType.Harvester),     // sprite 'HarvesterRobot'
                R(7.16f,  -1.52f,  4.692f, 5.724f, RobotType.Harvester),     // sprite 'HarvesterRobot'
                R(5.36f,  -4.81f,  4.899f, 4.677f, RobotType.SemiHarvester), // sprite 'Robot_SemiHarvest'
                R(6.34f,  -3.45f,  4.899f, 4.677f, RobotType.SemiHarvester), // sprite 'Robot_SemiHarvest'
            });

        // ── W1_L10  ────────────────────────────────────────────────────────────
        // Rebuilt from scratch 2026-07-13 (user request: "i have re built level 10 - see latest
        // dump; override current level 10 and wire the new one") — replaces the previous
        // hand-placed layout entirely, pasted verbatim from unity/Logs/level_layout_dump.txt
        // (FarmFury -> Debug -> Dump Level Layout To Log). The old layout had several blocks with
        // no real support beneath them (verified against its exact coordinates the same session —
        // a Stone/Wood/Barrel column floating 0.66-0.845 units above the ground, and one Wood
        // plank floating in open air with nothing below it at all), which is what
        // BlockBase.SettleIfUnsupported()/LevelLoader's settle-on-load pass was built to catch —
        // this new dump is a fresh Scene-view layout, not a data patch, so any residual small gaps
        // are expected to self-correct via that same mechanism at level load. id/name/par/birds[]
        // (Bessie's debut, 2x Cluck + 1x Bessie) are unchanged from the previous version — the dump
        // only captures blocks[]/robots[], not those level-level fields. No StoneTower/indestructible
        // structure in this rebuild. Two robots have no explicit RobotType in the dump (R(...)
        // without a type argument), defaulting to RobotType.Basic/"Robot_Pawn" art, matching the
        // dump's own 'Robot_Pawn' sprite comment. Not visually re-verified (no Play-mode access here).
        Make(folder, "L10_BessiesDebut",
            id: "W1_L10", name: "Bessie's Debut", par: 3,
            birds: new[] { AnimalType.Cluck, AnimalType.Cluck, AnimalType.Bessie },
            blocks: new[]
            {
                B(BlockType.Barrel, 5.9f, -5.24f, 1.27f, 1.205f), // sprite 'Barrel_Dynamite'
                B(BlockType.Barrel, 3.13f, -5.19f, 1.27f, 1.205f), // sprite 'Barrel_Dynamite'
                B(BlockType.Barrel, 4.89f, -0.97f, 1.27f, 1.205f), // sprite 'Barrel_Dynamite'
                B(BlockType.Haybale, 1.82f, -5.2f, 1.205f, 1.123f, passThrough: true, hp: 10f, mass: 3f), // sprite 'Haybail'
                B(BlockType.Haybale, 2.24f, -5.3f, 1.205f, 1.123f, passThrough: true, hp: 10f, mass: 3f), // sprite 'Haybail'
                B(BlockType.Haybale, 2.09f, -4.65f, 1.205f, 1.123f, passThrough: true, hp: 10f, mass: 3f), // sprite 'Haybail'
                B(BlockType.Wood, 2.91f, -4.45f, 1f, 1.064f, artVariant: WoodArtVariant.Vertical), // sprite 'Plank_2DShork'
                B(BlockType.Wood, 3.35f, -4.45f, 1f, 1.064f, artVariant: WoodArtVariant.Vertical), // sprite 'Plank_2DShork'
                B(BlockType.Wood, 5.74f, -4.5f, 1f, 1.064f, artVariant: WoodArtVariant.Vertical), // sprite 'Plank_2DShork'
                B(BlockType.Wood, 6.16f, -4.52f, 1f, 1.064f, artVariant: WoodArtVariant.Vertical), // sprite 'Plank_2DShork'
                B(BlockType.Wood, 3.19f, -3.97f, 1f, 1f, artVariant: WoodArtVariant.Horizontal), // sprite 'Plank_2DHorizontal'
                B(BlockType.Wood, 4.06f, -4f, 1f, 1f, artVariant: WoodArtVariant.Horizontal), // sprite 'Plank_2DHorizontal'
                B(BlockType.Wood, 4.93f, -4f, 1f, 1f, artVariant: WoodArtVariant.Horizontal), // sprite 'Plank_2DHorizontal'
                B(BlockType.Wood, 5.81f, -3.95f, 1f, 1f, artVariant: WoodArtVariant.Horizontal), // sprite 'Plank_2DHorizontal'
                B(BlockType.Wood, 6.64f, -3.97f, 1f, 1f, artVariant: WoodArtVariant.Horizontal), // sprite 'Plank_2DHorizontal'
                B(BlockType.Stone, 2.96f, -3.51f, 0.48f, 1f), // sprite 'Stone_Vertical'
                B(BlockType.Stone, 5.02f, -3.53f, 0.48f, 1f), // sprite 'Stone_Vertical'
                B(BlockType.Stone, 5f, -2.678f, 0.48f, 1.245f), // sprite 'Stone_Vertical'
                B(BlockType.Stone, 6.88f, -3.48f, 0.48f, 1f), // sprite 'Stone_Vertical'
                B(BlockType.Wood, 2.67f, -2.92f, 1f, 1f, artVariant: WoodArtVariant.Auto), // sprite 'Plank_Skew'
                B(BlockType.Wood, 3.31f, -2.27f, 1f, 1f, artVariant: WoodArtVariant.Auto), // sprite 'Plank_Skew'
                B(BlockType.Wood, 4.11f, -1.89f, 1.412f, 1.412f, artVariant: WoodArtVariant.Auto), // sprite 'Plank_Skew'
                B(BlockType.Wood, 5.56f, -1.86f, 1.412f, 1.412f, artVariant: WoodArtVariant.Auto), // sprite 'Plank_Skew'
                B(BlockType.Wood, 6.35f, -2.22f, 1.078f, 1.078f, artVariant: WoodArtVariant.Auto), // sprite 'Plank_Skew'
                B(BlockType.Wood, 7.02f, -2.81f, 1.078f, 1.078f, artVariant: WoodArtVariant.Auto), // sprite 'Plank_Skew'
                B(BlockType.Stone, 4.85f, -1.78f, 1f, 1f), // sprite 'Stone_Square'
            },
            robots: new[]
            {
                R(4.54f, -4.77f, 6.402f, 8.195f, RobotType.Harvester), // sprite 'HarvesterRobot'
                R(7.33f, -4.74f, 6.402f, 8.195f, RobotType.Harvester), // sprite 'HarvesterRobot'
                R(4.049f, -3.2f, 6.244f, 5.788f), // sprite 'Robot_Pawn'
                R(5.98f, -3.17f, 6.13f, 6.244f), // sprite 'Robot_Pawn'
                R(5.67f, -1.19f, 5.788f, 6.13f, RobotType.SemiHarvester), // sprite 'Robot_SemiHarvest'
                R(4.05f, -1.2f, 5.788f, 6.13f, RobotType.SemiHarvester), // sprite 'Robot_SemiHarvest'
            });

        // ── W1_L11  ────────────────────────────────────────────────────────────
        // New level 2026-07-12, built via the user's own hand-placed dump (FarmFury -> Debug ->
        // Dump Level Layout To Log) — introduces RobotType.Basic ("Robot_Pawn" art, its actual
        // gameplay debut — SceneSetup.WireRobotSprite() switched from the old Robot_Idle.png
        // placeholder to Robot_Pawn.png the same day) alongside Harvester + SemiHarvester, all 3
        // robot types in one level for the first time — this is what the match-up screen's new
        // third robot card (added same day) was built for. birds[] continues Cluck/Cluck/Bessie
        // from L10. Another large 'StoneTower' structure, marked indestructible: true like L10's.
        // The Robot_Pawn robot has no custom scale in the dump (LevelLayoutDumper's generic
        // "robot" branch previously dropped scale entirely — fixed same day, but this particular
        // dump predates that fix) so it spawns at the Robot prefab's own default visual scale.
        // Real per-sprite scale/hp values captured directly from the Scene view. Not visually
        // re-verified (no Play-mode access here).
        // Robot scale fixed 2026-07-13 (user report, confirmed against a screenshot: "its not that
        // there is a wrong sprite - they are the wrong size, also remove the stone tower"). Every
        // other level in this file keeps Harvester scale in roughly the 4.7-9.5 range and
        // SemiHarvester in roughly 4.5-7.9 (checked directly against the full file, not guessed) —
        // L11's original values (Harvester 10.862x12.922, SemiHarvester 10.514x10.913 x4) were the
        // largest anywhere in the set, well outside that range. Rescaled by a uniform 0.57 factor
        // (chosen to land both robot types right in the same neighbourhood as L10/L12, the levels
        // immediately before/after this one) applied identically to width AND height, so each
        // robot's original aspect ratio — and therefore its art — is completely unchanged, only
        // its size. Y positions are UNCHANGED here; shrinking a sprite around its own centre point
        // can leave a small visual gap between a robot's new (smaller) feet and the block it's
        // meant to stand on, so this may still need a small manual Y nudge per robot once seen in
        // the Editor, same as every other per-level placement in this file. StoneTower block
        // removed entirely per the same request.
        Make(folder, "L11_FullRoster",
            id: "W1_L11", name: "Full Roster", par: 3,
            birds: new[] { AnimalType.Cluck, AnimalType.Cluck, AnimalType.Bessie },
            blocks: new[]
            {
                B(BlockType.Wood,    1.473f, -5.378f, 1f, 0.81f,  artVariant: WoodArtVariant.Short), // sprite 'Plank_Short'
                B(BlockType.Wood,    2.08f,  -5.39f,  1f, 0.81f,  artVariant: WoodArtVariant.Short), // sprite 'Plank_Short'
                B(BlockType.Wood,    2.69f,  -5.39f,  1f, 0.81f,  artVariant: WoodArtVariant.Short), // sprite 'Plank_Short'
                B(BlockType.Wood,    1.8f,   -4.8f,   1f, 0.81f,  artVariant: WoodArtVariant.Short), // sprite 'Plank_Short'
                B(BlockType.Wood,    2.43f,  -4.81f,  1f, 0.81f,  artVariant: WoodArtVariant.Short), // sprite 'Plank_Short'
                B(BlockType.Stone,   3.36f,  -5.47f,  1.212f, 1.242f, artVariant: WoodArtVariant.Block), // sprite 'Stone_Block'
                B(BlockType.Wood,    3.93f,  -4.91f,  1f, 2.037f, artVariant: WoodArtVariant.Shork), // sprite 'Plank_Shork'
                B(BlockType.Wood,    5.59f,  -4.85f,  1f, 2.037f, artVariant: WoodArtVariant.Shork), // sprite 'Plank_Shork'
                B(BlockType.Wood,    3.89f,  -3.94f,  1f, 0.618f, artVariant: WoodArtVariant.VerticalShort), // sprite 'Plank_VeriticalShort'
                B(BlockType.Wood,    5.57f,  -3.91f,  1f, 0.618f, artVariant: WoodArtVariant.VerticalShort), // sprite 'Plank_VeriticalShort'
                B(BlockType.Barrel,  3.175f, -4.652f, 1.234f, 1.188f), // sprite 'Barrel_Dynamite'
                B(BlockType.Barrel,  4.668f, -5.207f, 1.279f, 1.173f), // sprite 'Barrel_Dynamite'
                B(BlockType.Wood,    3.17f,  -3.79f,  1f, 1.064f, artVariant: WoodArtVariant.Shork2D), // sprite 'Plank_2DShork'
                B(BlockType.Wood,    3.17f,  -2.91f,  1f, 1.064f, artVariant: WoodArtVariant.Shork2D), // sprite 'Plank_2DShork'
                B(BlockType.Wood,    4.68f,  -3.36f,  1f, 1.064f, artVariant: WoodArtVariant.Shork2D), // sprite 'Plank_2DShork'
                B(BlockType.Wood,    4.68f,  -2.57f,  1f, 1.064f, artVariant: WoodArtVariant.Shork2D), // sprite 'Plank_2DShork'
                B(BlockType.Wood,    4.69f,  -1.8f,   1f, 1.064f, artVariant: WoodArtVariant.Shork2D), // sprite 'Plank_2DShork'
                B(BlockType.Wood,    3.395f, -2.17f,  1f, 1f,     artVariant: WoodArtVariant.Skew), // sprite 'Plank_Skew'
                B(BlockType.Wood,    4.03f,  -1.55f,  1f, 1f,     artVariant: WoodArtVariant.Skew), // sprite 'Plank_Skew'
                B(BlockType.Wood,    4.836f, -1.23f,  1.212f, 1f, artVariant: WoodArtVariant.Horizontal2D), // sprite 'Plank_2DHorizontal'
                B(BlockType.Wood,    5.92f,  -1.22f,  1.106f, 1f, artVariant: WoodArtVariant.Horizontal2D), // sprite 'Plank_2DHorizontal'
                B(BlockType.Wood,    5.615f, -2.533f, 1.817f, 0.492f, artVariant: WoodArtVariant.Horizontal), // sprite 'Plank_Horizontal'
                B(BlockType.Haybale, 4.83f,  -0.72f,  1.067f, 1.098f, passThrough: true, hp: 10f, mass: 3f), // sprite 'Haybail'
                B(BlockType.Haybale, 5.82f,  -0.7f,   1.067f, 1.098f, passThrough: true, hp: 10f, mass: 3f), // sprite 'Haybail'
            },
            robots: new[]
            {
                R(2.04f, -3.63f, 6.191f, 7.366f, RobotType.Harvester),     // sprite 'HarvesterRobot'
                R(4.63f, -4.36f, 5.993f, 6.220f, RobotType.SemiHarvester), // sprite 'Robot_SemiHarvest'
                R(3.84f, -3.26f, 5.993f, 6.220f, RobotType.SemiHarvester), // sprite 'Robot_SemiHarvest'
                R(5.44f, -1.93f, 5.993f, 6.220f, RobotType.SemiHarvester), // sprite 'Robot_SemiHarvest'
                R(5.55f, -3.26f, 5.993f, 6.220f, RobotType.SemiHarvester), // sprite 'Robot_SemiHarvest'
                // RobotType.Basic/'Robot_Pawn' removed 2026-07-16 (user report: "small robot
                // rendering in the middle of the air"). It spawned at the Robot prefab's default
                // visual scale (see the level comment above — its custom scale was never captured
                // in the original dump) at X=7.155, well past the rest of this level's structure
                // (which tops out around X=5.92) — robots are spawned Static/no-gravity, so with
                // nothing built underneath it, it just sat there floating rather than falling or
                // resting on anything. Deleting it rather than repositioning it onto the existing
                // structure, since there's no record of where it was actually meant to stand.
            });

        // ── W1_L12  ────────────────────────────────────────────────────────────
        // New level 2026-07-12, built via the user's own hand-placed dump (FarmFury -> Debug ->
        // Dump Level Layout To Log). Heaviest robot count yet: 3 Harvester + 3 SemiHarvester + 3
        // Basic/Pawn (9 total). First dump captured AFTER two same-day LevelLayoutDumper fixes —
        // Robot_Pawn's scale is correctly recorded here (previously silently dropped), and the
        // 'WoodenBarrel' prop below is correctly BlockType.Wood (structural only), not the
        // explosive Barrel_Dynamite. birds[] continues Cluck/Cluck/Bessie from L10/L11. Real
        // per-sprite scale/hp values captured directly from the Scene view. Not visually
        // re-verified (no Play-mode access here).
        Make(folder, "L12_HeavyGuard",
            id: "W1_L12", name: "Heavy Guard", par: 3,
            birds: new[] { AnimalType.Cluck, AnimalType.Cluck, AnimalType.Bessie },
            blocks: new[]
            {
                B(BlockType.Stone,   1.56f,  -5.5f,   1f, 1f, artVariant: WoodArtVariant.Block), // sprite 'Stone_Block'
                B(BlockType.Stone,   2.29f,  -5.5f,   1f, 1f, artVariant: WoodArtVariant.Block), // sprite 'Stone_Block'
                B(BlockType.Stone,   1.96f,  -5.05f,  1f, 1f, artVariant: WoodArtVariant.Block), // sprite 'Stone_Block'
                B(BlockType.Stone,   1.23f,  -5.03f,  1f, 1f, artVariant: WoodArtVariant.Block), // sprite 'Stone_Block'
                B(BlockType.Stone,   1.62f,  -4.55f,  1f, 1f, artVariant: WoodArtVariant.Block), // sprite 'Stone_Block'
                B(BlockType.Wood,    2.96f,  -5.42f,  1f, 0.81f, artVariant: WoodArtVariant.Short), // sprite 'Plank_Short'
                B(BlockType.Wood,    1.67f,  -3.88f,  1f, 0.81f, artVariant: WoodArtVariant.Short), // sprite 'Plank_Short'
                B(BlockType.Wood,    5.31f,  -5.31f,  1f, 0.81f, artVariant: WoodArtVariant.Short), // sprite 'Plank_Short'
                B(BlockType.Wood,    5.33f,  -4.53f,  1f, 0.81f, artVariant: WoodArtVariant.Short), // sprite 'Plank_Short'
                B(BlockType.Wood,    5.34f,  -3.78f,  1f, 0.81f, artVariant: WoodArtVariant.Short), // sprite 'Plank_Short'
                B(BlockType.Wood,    2.76f,  -4.93f,  1f, 0.618f, artVariant: WoodArtVariant.VerticalShort), // sprite 'Plank_VeriticalShort'
                B(BlockType.Barrel,  3.7f,   -5.22f,  1.234f, 1.113f), // sprite 'Barrel_Dynamite'
                B(BlockType.Barrel,  1.74f,  -2.75f,  1.113f, 1.098f), // sprite 'Barrel_Dynamite'
                B(BlockType.Barrel,  5.34f,  -3.07f,  1.113f, 1.098f), // sprite 'Barrel_Dynamite'
                B(BlockType.Haybale, 7.99f,  -5.22f,  1.007f, 0.977f, passThrough: true, hp: 10f, mass: 3f), // sprite 'Haybail'
                B(BlockType.Haybale, 7.94f,  -4.57f,  0.977f, 0.977f, passThrough: true, hp: 10f, mass: 3f), // sprite 'Haybail'
                B(BlockType.Haybale, 7.08f,  -3.66f,  0.977f, 0.977f, passThrough: true, hp: 10f, mass: 3f), // sprite 'Haybail'
                B(BlockType.Wood,    5.29f,  -4.95f,  1f, 1f, artVariant: WoodArtVariant.Horizontal2D), // sprite 'Plank_2DHorizontal'
                B(BlockType.Wood,    5.31f,  -4.16f,  1f, 1f, artVariant: WoodArtVariant.Horizontal2D), // sprite 'Plank_2DHorizontal'
                B(BlockType.Wood,    6.22f,  -4.16f,  1f, 1f, artVariant: WoodArtVariant.Horizontal2D), // sprite 'Plank_2DHorizontal'
                B(BlockType.Wood,    7.14f,  -4.16f,  1f, 1f, artVariant: WoodArtVariant.Horizontal2D), // sprite 'Plank_2DHorizontal'
                B(BlockType.Wood,    8.02f,  -4.14f,  1f, 1f, artVariant: WoodArtVariant.Horizontal2D), // sprite 'Plank_2DHorizontal'
                B(BlockType.Wood,    3.91f,  -0.35f,  1f, 1f, artVariant: WoodArtVariant.Skew), // sprite 'Plank_Skew'
                B(BlockType.Wood,    2.56f,  -1.82f,  1f, 1f, artVariant: WoodArtVariant.Skew), // sprite 'Plank_Skew'
                B(BlockType.Wood,    3.24f,  -1.07f,  1f, 1f, artVariant: WoodArtVariant.Skew), // sprite 'Plank_Skew'
                B(BlockType.Wood,    4.36f,  -3.37f,  1f, 0.492f, artVariant: WoodArtVariant.Horizontal), // sprite 'Plank_Horizontal'
                B(BlockType.Wood,    3.29f,  -1.54f,  1f, 0.492f, artVariant: WoodArtVariant.Horizontal), // sprite 'Plank_Horizontal'
                B(BlockType.Wood,    4.23f,  -1.54f,  1f, 0.492f, artVariant: WoodArtVariant.Horizontal), // sprite 'Plank_Horizontal'
                B(BlockType.Wood,    5.15f,  -1.56f,  1f, 0.492f, artVariant: WoodArtVariant.Horizontal), // sprite 'Plank_Horizontal'
                B(BlockType.Wood,    3.46f,  -3.38f,  1f, 0.492f, artVariant: WoodArtVariant.Horizontal), // sprite 'Plank_Horizontal'
                B(BlockType.Stone,   2.12f,  -3.35f,  1.817f, 0.652f, artVariant: WoodArtVariant.Diagonal), // sprite 'Stone_Diagonal'
                B(BlockType.Wood,    2.31f,  -2.71f,  1f, 1.064f, artVariant: WoodArtVariant.Shork2D), // sprite 'Plank_2DShork'
                B(BlockType.Wood,    5.353f, -2.24f,  1.113f, 1.037f, artVariant: WoodArtVariant.Barrel), // sprite 'WoodenBarrel' — structural only, not explosive
                B(BlockType.Stone,   0.85f,  -5.5f,   1f, 1f, artVariant: WoodArtVariant.Block), // sprite 'Stone_Block'
            },
            robots: new[]
            {
                R(6.6f,  -4.87f, 5.748f, 7.346f, RobotType.Harvester),     // sprite 'HarvesterRobot'
                R(3.57f, -2.49f, 5.748f, 7.346f, RobotType.Harvester),     // sprite 'HarvesterRobot'
                R(4.84f, -0.7f,  5.748f, 7.346f, RobotType.Harvester),     // sprite 'HarvesterRobot'
                R(2.59f, -4.18f, 4.751f, 5.174f), // sprite 'Robot_Pawn'
                R(6.12f, -3.39f, 4.751f, 5.174f), // sprite 'Robot_Pawn'
                R(8f,    -3.45f, 4.751f, 5.174f), // sprite 'Robot_Pawn'
                R(4.48f, -5.17f, 6.178f, 6.019f, RobotType.SemiHarvester), // sprite 'Robot_SemiHarvest'
                R(3.69f, -4.34f, 6.178f, 6.019f, RobotType.SemiHarvester), // sprite 'Robot_SemiHarvest'
                R(7.08f, -2.87f, 6.178f, 6.019f, RobotType.SemiHarvester), // sprite 'Robot_SemiHarvest'
            });

        // ── W1_L13  ────────────────────────────────────────────────────────────
        // New level 2026-07-12, built via the user's own hand-placed dump (FarmFury -> Debug ->
        // Dump Level Layout To Log). Heaviest robot count yet: 3 Harvester + 4 SemiHarvester + 3
        // Basic/Pawn (10 total). birds[] continues Cluck/Cluck/Bessie from L10-L12. WoodenBarrel
        // prop (structural only, not explosive), 4 haybale, 3 dynamite barrels, 2 Stone_Vertical +
        // 2 Stone_Square, and a tall multi-tier wood scaffold. Real per-sprite scale/hp values
        // captured directly from the Scene view. Not visually re-verified (no Play-mode access
        // here).
        Make(folder, "L13_TenStrong",
            id: "W1_L13", name: "Ten Strong", par: 3,
            birds: new[] { AnimalType.Cluck, AnimalType.Cluck, AnimalType.Bessie },
            blocks: new[]
            {
                B(BlockType.Wood,    1.08f,  -5.199f, 1.265f, 1.139f, artVariant: WoodArtVariant.Barrel), // sprite 'WoodenBarrel'
                B(BlockType.Haybale, 1.895f, -5.181f, 1.085f, 1.139f, passThrough: true, hp: 10f, mass: 3f), // sprite 'Haybail'
                B(BlockType.Haybale, 2.74f,  -5.22f,  1.085f, 1.139f, passThrough: true, hp: 10f, mass: 3f), // sprite 'Haybail'
                B(BlockType.Haybale, 2.07f,  -5.35f,  1.085f, 1.139f, passThrough: true, hp: 10f, mass: 3f), // sprite 'Haybail'
                B(BlockType.Haybale, 1.94f,  -4.58f,  1.085f, 1.139f, passThrough: true, hp: 10f, mass: 3f), // sprite 'Haybail'
                B(BlockType.Wood,    3.501f, -5.37f,  1f, 0.81f,  artVariant: WoodArtVariant.Short), // sprite 'Plank_Short'
                B(BlockType.Wood,    4.15f,  -5.37f,  1f, 0.81f,  artVariant: WoodArtVariant.Short), // sprite 'Plank_Short'
                B(BlockType.Wood,    4.14f,  -3.15f,  1f, 0.81f,  artVariant: WoodArtVariant.Short), // sprite 'Plank_Short'
                B(BlockType.Wood,    6.23f,  -5.33f,  1f, 0.81f,  artVariant: WoodArtVariant.Short), // sprite 'Plank_Short'
                B(BlockType.Wood,    6.21f,  -5.42f,  1f, 0.81f,  artVariant: WoodArtVariant.Short), // sprite 'Plank_Short'
                B(BlockType.Wood,    6.83f,  -5.38f,  1f, 0.81f,  artVariant: WoodArtVariant.Short), // sprite 'Plank_Short'
                B(BlockType.Wood,    7.34f,  -4.88f,  1f, 2.037f, artVariant: WoodArtVariant.Shork), // sprite 'Plank_Shork'
                B(BlockType.Barrel,  6.67f,  -4.61f,  1.175f, 1.103f), // sprite 'Barrel_Dynamite'
                B(BlockType.Barrel,  3.92f,  -2.36f,  1.175f, 1.103f), // sprite 'Barrel_Dynamite'
                B(BlockType.Barrel,  5.59f,  -0.65f,  1.175f, 1.103f), // sprite 'Barrel_Dynamite'
                B(BlockType.Wood,    3.32f,  -3.92f,  1f, 1.105f, artVariant: WoodArtVariant.Vertical), // sprite '2D_Block_Wood_Vertical'
                B(BlockType.Wood,    6.01f,  -2.61f,  1f, 1.064f, artVariant: WoodArtVariant.Vertical), // sprite '2D_Block_Wood_Vertical'
                B(BlockType.Wood,    3.32f,  -2.43f,  1f, 1.064f, artVariant: WoodArtVariant.Vertical), // sprite '2D_Block_Wood_Vertical'
                B(BlockType.Wood,    7.48f,  -3.71f,  1f, 1.064f, artVariant: WoodArtVariant.Vertical), // sprite '2D_Block_Wood_Vertical'
                B(BlockType.Wood,    6.06f,  -3.865f, 1f, 1.173f, artVariant: WoodArtVariant.Vertical), // sprite '2D_Block_Wood_Vertical'
                B(BlockType.Stone,   3.31f,  -4.78f,  0.48f, 1f, artVariant: WoodArtVariant.Vertical), // sprite 'Stone_Vertical'
                B(BlockType.Stone,   6.03f,  -4.75f,  0.48f, 1f, artVariant: WoodArtVariant.Vertical), // sprite 'Stone_Vertical'
                B(BlockType.Wood,    5.46f,  -4.096f, 1f, 1.845f, artVariant: WoodArtVariant.Shork), // sprite 'Plank_Shork'
                B(BlockType.Wood,    4.2f,   -1.29f,  1.11f,  1f, artVariant: WoodArtVariant.Horizontal2D), // sprite 'Plank_2DHorizontal'
                B(BlockType.Wood,    5.13f,  -1.26f,  1.109f, 1f, artVariant: WoodArtVariant.Horizontal2D), // sprite 'Plank_2DHorizontal'
                B(BlockType.Wood,    6.08f,  -1.24f,  1.109f, 1f, artVariant: WoodArtVariant.Horizontal2D), // sprite 'Plank_2DHorizontal'
                B(BlockType.Wood,    5.68f,  -3.21f,  1.109f, 1f, artVariant: WoodArtVariant.Horizontal2D), // sprite 'Plank_2DHorizontal'
                B(BlockType.Wood,    6.61f,  -3.1f,   1.109f, 1f, artVariant: WoodArtVariant.Horizontal2D), // sprite 'Plank_2DHorizontal'
                B(BlockType.Wood,    7.57f,  -3.11f,  1.109f, 1f, artVariant: WoodArtVariant.Horizontal2D), // sprite 'Plank_2DHorizontal'
                B(BlockType.Wood,    6.23f,  -1.81f,  1f, 1f, artVariant: WoodArtVariant.Skew), // sprite 'Plank_Skew'
                B(BlockType.Wood,    3.47f,  -1.63f,  1f, 1f, artVariant: WoodArtVariant.Skew), // sprite 'Plank_Skew'
                B(BlockType.Wood,    6.86f,  -1.13f,  1f, 1f, artVariant: WoodArtVariant.Skew), // sprite 'Plank_Skew'
                B(BlockType.Stone,   3.45f,  -3.18f,  1f, 1f, artVariant: WoodArtVariant.Square), // sprite 'Stone_Square'
                B(BlockType.Stone,   4.83f,  -3.16f,  1f, 1f, artVariant: WoodArtVariant.Square), // sprite 'Stone_Square'
            },
            robots: new[]
            {
                R(4.38f, -4.21f, 6.194f, 7.561f, RobotType.Harvester),     // sprite 'HarvesterRobot'
                R(7.1f,  -2.23f, 6.194f, 7.561f, RobotType.Harvester),     // sprite 'HarvesterRobot'
                R(4.44f, -0.42f, 6.194f, 7.561f, RobotType.Harvester),     // sprite 'HarvesterRobot'
                R(4.81f, -5.34f, 4.824f, 4.728f, RobotType.SemiHarvester), // sprite 'Robot_SemiHarvest'
                R(2.76f, -4.45f, 4.824f, 4.728f, RobotType.SemiHarvester), // sprite 'Robot_SemiHarvest'
                R(5.51f, -2.64f, 5.254f, 5.302f, RobotType.SemiHarvester), // sprite 'Robot_SemiHarvest'
                R(5.48f, -5.34f, 4.824f, 4.728f, RobotType.SemiHarvester), // sprite 'Robot_SemiHarvest'
                R(6.62f, -3.69f, 4.25f,  4.298f), // sprite 'Robot_Pawn'
                R(4.77f, -2.23f, 4.442f, 4.92f),  // sprite 'Robot_Pawn'
                R(6.35f, -0.5f,  4.585f, 4.92f),  // sprite 'Robot_Pawn'
            });

        // ── W1_L14  ────────────────────────────────────────────────────────────
        // New level 2026-07-12, built via the user's own hand-placed dump (FarmFury -> Debug ->
        // Dump Level Layout To Log). 3 Basic/Pawn + 3 SemiHarvester + 1 Harvester (7 total).
        // birds[] continues Cluck/Cluck/Bessie from L10-L13. 4 Stone pieces (3 Square, 1 Skew, 1
        // Diagonal), 3 haybale, wood scaffold with short/vertical/skew planks, 2 dynamite barrels.
        // Real per-sprite scale/hp values captured directly from the Scene view. Not visually
        // re-verified (no Play-mode access here).
        Make(folder, "L14_StoneAndPawns",
            id: "W1_L14", name: "Stone and Pawns", par: 3,
            birds: new[] { AnimalType.Cluck, AnimalType.Cluck, AnimalType.Bessie },
            blocks: new[]
            {
                B(BlockType.Stone,   3.83f, -5.19f,  1f, 1f, artVariant: WoodArtVariant.Square), // sprite 'Stone_Square'
                B(BlockType.Stone,   3.57f, -4.67f,  1f, 1f, artVariant: WoodArtVariant.Square), // sprite 'Stone_Square'
                B(BlockType.Stone,   3.26f, -4.1f,   1f, 1f, artVariant: WoodArtVariant.Square), // sprite 'Stone_Square'
                B(BlockType.Wood,    1.65f, -5.61f,  1f, 0.81f,  artVariant: WoodArtVariant.Short), // sprite 'Plank_Short'
                B(BlockType.Wood,    1.63f, -5f,     1f, 0.81f,  artVariant: WoodArtVariant.Short), // sprite 'Plank_Short'
                B(BlockType.Wood,    2.32f, -5.64f,  1f, 0.81f,  artVariant: WoodArtVariant.Short), // sprite 'Plank_Short'
                B(BlockType.Wood,    4.16f, -4.23f,  1f, 0.492f, artVariant: WoodArtVariant.Horizontal), // sprite 'Plank_Horizontal'
                B(BlockType.Wood,    3.72f, -2.72f,  1f, 0.492f, artVariant: WoodArtVariant.Horizontal), // sprite 'Plank_Horizontal'
                B(BlockType.Wood,    6.04f, -2.69f,  1f, 0.492f, artVariant: WoodArtVariant.Horizontal), // sprite 'Plank_Horizontal'
                B(BlockType.Wood,    6.95f, -2.68f,  1f, 0.492f, artVariant: WoodArtVariant.Horizontal), // sprite 'Plank_Horizontal'
                B(BlockType.Wood,    4.6f,  -5.25f,  1f, 0.492f, artVariant: WoodArtVariant.Horizontal), // sprite 'Plank_Horizontal'
                B(BlockType.Stone,   3.37f, -3.345f, 1f, 1.131f, artVariant: WoodArtVariant.Skew), // sprite 'Stone_Skew'
                B(BlockType.Haybale, 5.5f,  -5.56f,  1.205f, 1.205f, passThrough: true, hp: 10f, mass: 3f), // sprite 'Haybail'
                B(BlockType.Haybale, 7.63f, -5.56f,  1.156f, 1.123f, passThrough: true, hp: 10f, mass: 3f), // sprite 'Haybail'
                B(BlockType.Haybale, 8.13f, -5.61f,  1.156f, 1.123f, passThrough: true, hp: 10f, mass: 3f), // sprite 'Haybail'
                B(BlockType.Wood,    3.35f, -2.18f,  1f, 1.064f, artVariant: WoodArtVariant.Shork2D), // sprite 'Plank_2DShork'
                B(BlockType.Wood,    6.47f, -4.34f,  1f, 0.618f, artVariant: WoodArtVariant.VerticalShort), // sprite 'Plank_VeriticalShort'
                B(BlockType.Wood,    6.52f, -5.23f,  0.772f, 1.71f, artVariant: WoodArtVariant.Shork), // sprite 'Plank_Shork'
                B(BlockType.Barrel,  6.277f, -1.935f, 1.287f, 1.352f), // sprite 'Barrel_Dynamite'
                B(BlockType.Barrel,  7.09f,  -1.95f,  1.287f, 1.352f), // sprite 'Barrel_Dynamite'
                B(BlockType.Stone,   4.89f,  -2.7f,   1.359f, 0.674f, artVariant: WoodArtVariant.Diagonal), // sprite 'Stone_Diagonal'
                B(BlockType.Stone,   4.12f,  -5.7f,   1f, 1f, artVariant: WoodArtVariant.Square), // sprite 'Stone_Square'
            },
            robots: new[]
            {
                R(4.18f, -3.52f, 4.99f,  5.617f), // sprite 'Robot_Pawn'
                R(6.48f, -3.5f,  5.389f, 5.845f), // sprite 'Robot_Pawn'
                R(7.88f, -4.58f, 5.389f, 5.845f), // sprite 'Robot_Pawn'
                R(3.09f, -5.46f, 6.358f, 6.985f, RobotType.SemiHarvester), // sprite 'Robot_SemiHarvest'
                R(2.34f, -4.79f, 6.358f, 6.985f, RobotType.SemiHarvester), // sprite 'Robot_SemiHarvest'
                R(5.26f, -4.6f,  6.358f, 6.985f, RobotType.SemiHarvester), // sprite 'Robot_SemiHarvest'
                R(4.65f, -1.51f, 7.846f, 10.5f,  RobotType.Harvester),     // sprite 'HarvesterRobot'
            });

        // ── W1_L15  ────────────────────────────────────────────────────────────
        // New level 2026-07-12, built via the user's own hand-placed dump (FarmFury -> Debug ->
        // Dump Level Layout To Log). 3 Basic/Pawn + 1 Harvester + 3 SemiHarvester (7 total).
        // birds[] continues Cluck/Cluck/Bessie from L10-L14. 5 Stone_Vertical pillars along the
        // base, 4 Stone_Block, dense wood scaffold (horizontal/2DShork/skew/shork planks), 3
        // dynamite barrels, 3 haybale. Real per-sprite scale/hp values captured directly from the
        // Scene view. Not visually re-verified (no Play-mode access here).
        Make(folder, "L15_FiveStones",
            id: "W1_L15", name: "Five Stones", par: 3,
            birds: new[] { AnimalType.Cluck, AnimalType.Cluck, AnimalType.Bessie },
            blocks: new[]
            {
                B(BlockType.Stone,   1.213f, -5.516f, 0.48f, 1f, artVariant: WoodArtVariant.Vertical), // sprite 'Stone_Vertical'
                B(BlockType.Stone,   2.6f,   -5.5f,   0.48f, 1f, artVariant: WoodArtVariant.Vertical), // sprite 'Stone_Vertical'
                B(BlockType.Stone,   3.86f,  -5.53f,  0.48f, 1f, artVariant: WoodArtVariant.Vertical), // sprite 'Stone_Vertical'
                B(BlockType.Stone,   5.18f,  -5.53f,  0.48f, 1f, artVariant: WoodArtVariant.Vertical), // sprite 'Stone_Vertical'
                B(BlockType.Stone,   6.53f,  -5.53f,  0.48f, 1f, artVariant: WoodArtVariant.Vertical), // sprite 'Stone_Vertical'
                B(BlockType.Wood,    1.18f,  -4.95f,  1f, 0.492f, artVariant: WoodArtVariant.Horizontal), // sprite 'Plank_Horizontal'
                B(BlockType.Wood,    2.53f,  -4.98f,  1f, 0.492f, artVariant: WoodArtVariant.Horizontal), // sprite 'Plank_Horizontal'
                B(BlockType.Wood,    1.93f,  -1.63f,  1f, 0.492f, artVariant: WoodArtVariant.Horizontal), // sprite 'Plank_Horizontal'
                B(BlockType.Wood,    2.89f,  -1.63f,  1f, 0.492f, artVariant: WoodArtVariant.Horizontal), // sprite 'Plank_Horizontal'
                B(BlockType.Wood,    3.85f,  -1.63f,  1f, 0.492f, artVariant: WoodArtVariant.Horizontal), // sprite 'Plank_Horizontal'
                B(BlockType.Wood,    4.81f,  -1.65f,  1f, 0.427f, artVariant: WoodArtVariant.Horizontal), // sprite 'Plank_Horizontal'
                B(BlockType.Wood,    3.84f,  -5f,     1f, 0.492f, artVariant: WoodArtVariant.Horizontal), // sprite 'Plank_Horizontal'
                B(BlockType.Wood,    5.1f,   -5f,     1f, 0.492f, artVariant: WoodArtVariant.Horizontal), // sprite 'Plank_Horizontal'
                B(BlockType.Wood,    5.27f,  -4.15f,  1f, 0.492f, artVariant: WoodArtVariant.Horizontal), // sprite 'Plank_Horizontal'
                B(BlockType.Wood,    6.45f,  -5f,     1f, 0.492f, artVariant: WoodArtVariant.Horizontal), // sprite 'Plank_Horizontal'
                // 4 of the original 8 Plank_2DShork entries removed here 2026-07-16 (user report:
                // "the sprites are not rendering positioning correctly"). Root cause: each pair
                // below sat only ~0.24-0.26 units apart on a 1-unit-wide sprite — ~75% overlap,
                // nowhere close to any other Shork2D usage in this file (e.g. L11's two instances
                // are a full 0.88 apart, a normal vertical stack) or any other block pairing in
                // this same level (every other neighbour here is spaced roughly its own footprint
                // apart). This reads as an accidental double-drag from the original Scene-view
                // dump, not a deliberate "layered debris" look — two near-identical sprites
                // rendering almost on top of each other at the same sortingOrder is exactly what
                // "not rendering positioning correctly" would look like. Kept the first (lower-X)
                // of each pair, removed the second: 2.04,-4.69 / 3.36,-4.72 / 4.62,-4.72 /
                // 5.9,-4.72. Not visually re-verified (no Play-mode access here).
                B(BlockType.Wood,    1.8f,   -4.67f,  1f, 1.064f, artVariant: WoodArtVariant.Shork2D), // sprite 'Plank_2DShork'
                B(BlockType.Wood,    3.1f,   -4.69f,  1f, 1.064f, artVariant: WoodArtVariant.Shork2D), // sprite 'Plank_2DShork'
                B(BlockType.Wood,    4.36f,  -4.69f,  1f, 1.064f, artVariant: WoodArtVariant.Shork2D), // sprite 'Plank_2DShork'
                B(BlockType.Wood,    5.66f,  -4.72f,  1f, 1.064f, artVariant: WoodArtVariant.Shork2D), // sprite 'Plank_2DShork'
                B(BlockType.Stone,   1.82f,  -4.08f,  1f, 1f, artVariant: WoodArtVariant.Block), // sprite 'Stone_Block'
                B(BlockType.Stone,   4.37f,  -4.11f,  1f, 1f, artVariant: WoodArtVariant.Block), // sprite 'Stone_Block'
                B(BlockType.Stone,   4.37f,  -3.57f,  1f, 1f, artVariant: WoodArtVariant.Block), // sprite 'Stone_Block'
                B(BlockType.Stone,   1.81f,  -3.59f,  1f, 1f, artVariant: WoodArtVariant.Block), // sprite 'Stone_Block'
                B(BlockType.Barrel,  1.094f, -4.309f, 1.156f, 1.27f), // sprite 'Barrel_Dynamite'
                B(BlockType.Barrel,  2.053f, -0.959f, 1.189f, 1.27f), // sprite 'Barrel_Dynamite'
                B(BlockType.Barrel,  5.32f,  -3.47f,  1.189f, 1.287f), // sprite 'Barrel_Dynamite'
                B(BlockType.Wood,    5.73f,  -1.94f,  1.031f, 1.031f, artVariant: WoodArtVariant.Skew), // sprite 'Plank_Skew'
                B(BlockType.Wood,    6.35f,  -2.58f,  1.031f, 1.031f, artVariant: WoodArtVariant.Skew), // sprite 'Plank_Skew'
                B(BlockType.Wood,    6.97f,  -3.18f,  1.031f, 1.031f, artVariant: WoodArtVariant.Skew), // sprite 'Plank_Skew'
                B(BlockType.Wood,    7.228f, -4.324f, 1.016f, 2.037f, artVariant: WoodArtVariant.Shork), // sprite 'Plank_Shork'
                B(BlockType.Haybale, 1.931f, -5.434f, 0.977f, 0.977f, passThrough: true, hp: 10f, mass: 3f), // sprite 'Haybail'
                B(BlockType.Haybale, 4.43f,  -5.48f,  0.977f, 0.977f, passThrough: true, hp: 10f, mass: 3f), // sprite 'Haybail'
                B(BlockType.Haybale, 7.2f,   -5.48f,  0.977f, 0.977f, passThrough: true, hp: 10f, mass: 3f), // sprite 'Haybail'
            },
            robots: new[]
            {
                R(1.82f, -2.58f, 5.56f,  6.073f), // sprite 'Robot_Pawn'
                R(2.88f, -0.72f, 5.56f,  6.073f), // sprite 'Robot_Pawn'
                R(4.4f,  -2.58f, 5.56f,  6.073f), // sprite 'Robot_Pawn'
                R(4.43f, -0.43f, 7.613f, 10.431f, RobotType.Harvester),     // sprite 'HarvesterRobot'
                R(2.55f, -4.46f, 6.472f, 6.073f,  RobotType.SemiHarvester), // sprite 'Robot_SemiHarvest'
                R(3.76f, -4.51f, 6.472f, 6.073f,  RobotType.SemiHarvester), // sprite 'Robot_SemiHarvest'
                R(6.41f, -4.48f, 6.472f, 6.073f,  RobotType.SemiHarvester), // sprite 'Robot_SemiHarvest'
            });

        // ── W1_L16  ────────────────────────────────────────────────────────────
        // New level 2026-07-12, built via the user's own hand-placed dump (FarmFury -> Debug ->
        // Dump Level Layout To Log). 3 SemiHarvester + 1 Harvester + 3 Basic/Pawn (7 total).
        // birds[] continues Cluck/Cluck/Bessie from L10-L15. Twin towers (Stone_Block base +
        // Stone_Vertical pillars), lots of Plank_Skew pieces bridging the upper gaps, 4 dynamite
        // barrels, 2 haybale. Real per-sprite scale/hp values captured directly from the Scene
        // view. Not visually re-verified (no Play-mode access here).
        Make(folder, "L16_TwinTowers",
            id: "W1_L16", name: "Twin Towers", par: 3,
            birds: new[] { AnimalType.Cluck, AnimalType.Cluck, AnimalType.Bessie },
            blocks: new[]
            {
                B(BlockType.Stone,   1.18f,  -5.56f,  1f, 1f, artVariant: WoodArtVariant.Block), // sprite 'Stone_Block'
                B(BlockType.Stone,   1.18f,  -5.05f,  1f, 1f, artVariant: WoodArtVariant.Block), // sprite 'Stone_Block'
                B(BlockType.Stone,   4.1f,   -5.57f,  1f, 1f, artVariant: WoodArtVariant.Block), // sprite 'Stone_Block'
                B(BlockType.Stone,   4.1f,   -5.06f,  1f, 1f, artVariant: WoodArtVariant.Block), // sprite 'Stone_Block'
                B(BlockType.Wood,    1.22f,  -4.44f,  1.114f, 0.81f, artVariant: WoodArtVariant.Short), // sprite 'Plank_Short'
                B(BlockType.Wood,    1.22f,  -3.85f,  1.114f, 0.81f, artVariant: WoodArtVariant.Short), // sprite 'Plank_Short'
                B(BlockType.Wood,    4.13f,  -4.44f,  1.114f, 0.81f, artVariant: WoodArtVariant.Short), // sprite 'Plank_Short'
                B(BlockType.Wood,    4.11f,  -3.87f,  1.114f, 0.81f, artVariant: WoodArtVariant.Short), // sprite 'Plank_Short'
                B(BlockType.Wood,    1.44f,  -3.12f,  1f, 1f, artVariant: WoodArtVariant.Skew), // sprite 'Plank_Skew'
                B(BlockType.Wood,    1.45f,  -0.36f,  1f, 1f, artVariant: WoodArtVariant.Skew), // sprite 'Plank_Skew'
                B(BlockType.Wood,    4.44f,  -3.07f,  1f, 1f, artVariant: WoodArtVariant.Skew), // sprite 'Plank_Skew'
                B(BlockType.Wood,    2.09f,   0.29f,  1f, 1f, artVariant: WoodArtVariant.Skew), // sprite 'Plank_Skew'
                B(BlockType.Wood,    2.98f,   0.702f, 1.593f, 1.594f, artVariant: WoodArtVariant.Skew), // sprite 'Plank_Skew'
                B(BlockType.Wood,    3.985f,  0.68f,  1.545f, 1.545f, artVariant: WoodArtVariant.Skew), // sprite 'Plank_Skew'
                B(BlockType.Wood,    4.92f,   0.19f,  1.069f, 1.064f, artVariant: WoodArtVariant.Skew), // sprite 'Plank_Skew'
                B(BlockType.Wood,    5.61f,  -0.38f,  1.069f, 1.064f, artVariant: WoodArtVariant.Skew), // sprite 'Plank_Skew'
                B(BlockType.Wood,    2.13f,  -2.74f,  1f, 1f, artVariant: WoodArtVariant.Horizontal2D), // sprite 'Plank_2DHorizontal'
                B(BlockType.Wood,    3.04f,  -2.71f,  1f, 1f, artVariant: WoodArtVariant.Horizontal2D), // sprite 'Plank_2DHorizontal'
                B(BlockType.Wood,    2.071f, -4.16f,  1.343f, 1f, artVariant: WoodArtVariant.Horizontal2D), // sprite 'Plank_2DHorizontal'
                B(BlockType.Wood,    3.233f, -4.16f,  1.114f, 1f, artVariant: WoodArtVariant.Horizontal2D), // sprite 'Plank_2DHorizontal'
                B(BlockType.Wood,    3.95f,  -2.69f,  1f, 1f, artVariant: WoodArtVariant.Horizontal2D), // sprite 'Plank_2DHorizontal'
                B(BlockType.Wood,    4.88f,  -2.66f,  1f, 1f, artVariant: WoodArtVariant.Horizontal2D), // sprite 'Plank_2DHorizontal'
                B(BlockType.Stone,   1.72f,  -2.4f,   0.48f, 1f, artVariant: WoodArtVariant.Vertical), // sprite 'Stone_Vertical'
                B(BlockType.Stone,   1.7f,   -1.65f,  0.48f, 1f, artVariant: WoodArtVariant.Vertical), // sprite 'Stone_Vertical'
                B(BlockType.Stone,   1.71f,  -0.9f,   0.48f, 1f, artVariant: WoodArtVariant.Vertical), // sprite 'Stone_Vertical'
                B(BlockType.Stone,   5.38f,  -2.3f,   0.48f, 1f, artVariant: WoodArtVariant.Vertical), // sprite 'Stone_Vertical'
                B(BlockType.Stone,   5.36f,  -1.6f,   0.48f, 1f, artVariant: WoodArtVariant.Vertical), // sprite 'Stone_Vertical'
                B(BlockType.Stone,   5.35f,  -0.91f,  0.48f, 1f, artVariant: WoodArtVariant.Vertical), // sprite 'Stone_Vertical'
                B(BlockType.Barrel,  2.241f, -3.44f,  1.434f, 1.336f), // sprite 'Barrel_Dynamite'
                B(BlockType.Barrel,  2.37f,  -2.047f, 1.368f, 1.221f), // sprite 'Barrel_Dynamite'
                B(BlockType.Barrel,  4.74f,  -2.09f,  1.336f, 1.205f), // sprite 'Barrel_Dynamite'
                B(BlockType.Barrel,  5.06f,  -5.145f, 1.401f, 1.287f), // sprite 'Barrel_Dynamite'
                B(BlockType.Haybale, 3.18f,  -5.206f, 1.221f, 1.172f, passThrough: true, hp: 10f, mass: 3f), // sprite 'Haybail'
                B(BlockType.Haybale, 4.65f,  -1.24f,  1.221f, 1.172f, passThrough: true, hp: 10f, mass: 3f), // sprite 'Haybail'
            },
            robots: new[]
            {
                R(2.31f, -1.09f, 6.13f,  6.016f, RobotType.SemiHarvester), // sprite 'Robot_SemiHarvest'
                R(5.06f, -4.12f, 6.13f,  6.016f, RobotType.SemiHarvester), // sprite 'Robot_SemiHarvest'
                R(3.18f, -3.47f, 6.13f,  6.016f, RobotType.SemiHarvester), // sprite 'Robot_SemiHarvest'
                R(6.55f, -4.78f, 7.194f, 9.662f, RobotType.Harvester),     // sprite 'HarvesterRobot'
                R(0.25f, -5.05f, 5.56f,  5.731f), // sprite 'Robot_Pawn'
                R(2.18f, -5.05f, 5.56f,  5.731f), // sprite 'Robot_Pawn'
                R(3.57f, -1.85f, 5.56f,  5.731f), // sprite 'Robot_Pawn'
            });

        // ── W1_L17  ────────────────────────────────────────────────────────────
        // New level 2026-07-12, built via the user's own hand-placed dump (FarmFury -> Debug ->
        // Dump Level Layout To Log). 4 Basic/Pawn + 2 SemiHarvester + 2 Harvester (8 total) — the
        // last "full roster" level before L18's Captain boss. birds[] continues Cluck/Cluck/Bessie
        // from L10-L16. First level to use '2D_Block_Wood_Flat' art (WoodArtVariant.Flat) — added
        // a dedicated _sprFlat field this same session (previously Flat aliased to _sprNormal/
        // Plank_Horizontal.png, a real art mismatch bug fixed alongside this level). 3 Stone
        // Diagonal, 5 Stone_Vertical, 9 flat wood blocks, 4 dynamite barrels, 3 haybale. Real
        // per-sprite scale/hp values captured directly from the Scene view. Not visually
        // re-verified (no Play-mode access here).
        Make(folder, "L17_EightStrong",
            id: "W1_L17", name: "Eight Strong", par: 3,
            birds: new[] { AnimalType.Cluck, AnimalType.Cluck, AnimalType.Bessie },
            blocks: new[]
            {
                B(BlockType.Stone,   0.87f,  -5.35f,  1f, 1f, artVariant: WoodArtVariant.Diagonal), // sprite 'Stone_Diagonal'
                B(BlockType.Stone,   0.86f,  -4.45f,  1f, 1f, artVariant: WoodArtVariant.Diagonal), // sprite 'Stone_Diagonal'
                B(BlockType.Stone,   0.87f,  -3.52f,  1f, 1f, artVariant: WoodArtVariant.Diagonal), // sprite 'Stone_Diagonal'
                B(BlockType.Wood,    2.75f,  -5.27f,  1f, 0.81f,  artVariant: WoodArtVariant.Short), // sprite 'Plank_Short'
                B(BlockType.Wood,    4.82f,  -5.27f,  1f, 0.81f,  artVariant: WoodArtVariant.Short), // sprite 'Plank_Short'
                B(BlockType.Wood,    0.95f,  -4.94f,  1f, 0.81f,  artVariant: WoodArtVariant.Short), // sprite 'Plank_Short'
                B(BlockType.Wood,    0.93f,  -4.04f,  1f, 0.81f,  artVariant: WoodArtVariant.Short), // sprite 'Plank_Short'
                B(BlockType.Wood,    4.65f,  -4.88f,  1f, 0.492f, artVariant: WoodArtVariant.Horizontal), // sprite 'Plank_Horizontal'
                B(BlockType.Wood,    2.68f,  -4.84f,  1f, 0.492f, artVariant: WoodArtVariant.Horizontal), // sprite 'Plank_Horizontal'
                B(BlockType.Barrel,  1.846f, -5.073f, 1.368f, 1.336f), // sprite 'Barrel_Dynamite'
                B(BlockType.Barrel,  0.907f, -2.751f, 1.401f, 1.319f), // sprite 'Barrel_Dynamite'
                B(BlockType.Barrel,  5.439f, -2.473f, 1.434f, 1.336f), // sprite 'Barrel_Dynamite'
                B(BlockType.Barrel,  7.965f, -5.009f, 1.466f, 1.303f), // sprite 'Barrel_Dynamite'
                B(BlockType.Haybale, 3.723f, -5.109f, 1.336f, 1.172f, passThrough: true, hp: 10f, mass: 3f), // sprite 'Haybail'
                B(BlockType.Haybale, 4.2f,    0.247f, 1.336f, 1.238f, passThrough: true, hp: 10f, mass: 3f), // sprite 'Haybail'
                B(BlockType.Haybale, 6.38f,  -5.06f,  1.336f, 1.172f, passThrough: true, hp: 10f, mass: 3f), // sprite 'Haybail'
                B(BlockType.Stone,   4.48f,  -4.48f,  0.48f, 1f, artVariant: WoodArtVariant.Vertical), // sprite 'Stone_Vertical'
                B(BlockType.Stone,   4.48f,  -3.75f,  0.48f, 1f, artVariant: WoodArtVariant.Vertical), // sprite 'Stone_Vertical'
                B(BlockType.Stone,   7.24f,  -3.76f,  0.48f, 1f, artVariant: WoodArtVariant.Vertical), // sprite 'Stone_Vertical'
                B(BlockType.Stone,   7.26f,  -5.26f,  0.48f, 1f, artVariant: WoodArtVariant.Vertical), // sprite 'Stone_Vertical'
                B(BlockType.Stone,   7.24f,  -4.48f,  0.48f, 1f, artVariant: WoodArtVariant.Vertical), // sprite 'Stone_Vertical'
                B(BlockType.Wood,    5.04f,  -3.17f,  1.18f, 1f, artVariant: WoodArtVariant.Flat), // sprite '2D_Block_Wood_Flat'
                B(BlockType.Wood,    4.24f,  -0.36f,  1.18f, 1f, artVariant: WoodArtVariant.Flat), // sprite '2D_Block_Wood_Flat'
                B(BlockType.Wood,    5.28f,  -0.38f,  1.18f, 1f, artVariant: WoodArtVariant.Flat), // sprite '2D_Block_Wood_Flat'
                B(BlockType.Wood,    6.31f,  -0.4f,   1.18f, 1f, artVariant: WoodArtVariant.Flat), // sprite '2D_Block_Wood_Flat'
                B(BlockType.Wood,    1.861f, -3.32f,  1.082f, 1f, artVariant: WoodArtVariant.Flat), // sprite '2D_Block_Wood_Flat'
                B(BlockType.Wood,    2.829f, -3.32f,  1.098f, 1f, artVariant: WoodArtVariant.Flat), // sprite '2D_Block_Wood_Flat'
                B(BlockType.Wood,    3.836f, -3.32f,  1.212f, 1f, artVariant: WoodArtVariant.Flat), // sprite '2D_Block_Wood_Flat'
                B(BlockType.Wood,    6f,     -3.17f,  1f, 1f, artVariant: WoodArtVariant.Flat), // sprite '2D_Block_Wood_Flat'
                B(BlockType.Wood,    6.9f,   -3.14f,  1f, 1f, artVariant: WoodArtVariant.Flat), // sprite '2D_Block_Wood_Flat'
                B(BlockType.Wood,    7.75f,  -3.16f,  1f, 1f, artVariant: WoodArtVariant.Flat), // sprite '2D_Block_Wood_Flat'
                B(BlockType.Wood,    2.54f,  -2.86f,  1f, 1.064f, artVariant: WoodArtVariant.Shork2D), // sprite 'Plank_2DShork'
                B(BlockType.Wood,    2.56f,  -1.98f,  1f, 1.064f, artVariant: WoodArtVariant.Shork2D), // sprite 'Plank_2DShork'
                B(BlockType.Wood,    2.73f,  -1.223f, 1.012f, 1.012f, artVariant: WoodArtVariant.Skew), // sprite 'Plank_Skew'
                B(BlockType.Wood,    3.33f,  -0.62f,  1f, 1f, artVariant: WoodArtVariant.Skew), // sprite 'Plank_Skew'
            },
            robots: new[]
            {
                R(1.86f, -2.49f, 5.503f, 5.617f), // sprite 'Robot_Pawn'
                R(2.69f, -4.16f, 5.503f, 5.617f), // sprite 'Robot_Pawn'
                R(6.38f, -4.03f, 5.503f, 5.617f), // sprite 'Robot_Pawn'
                R(5.4f,  -1.23f, 5.503f, 5.617f), // sprite 'Robot_Pawn'
                R(7.97f, -3.88f, 7.669f, 7.498f, RobotType.SemiHarvester), // sprite 'Robot_SemiHarvest'
                R(5.34f,  0.3f,  7.669f, 7.498f, RobotType.SemiHarvester), // sprite 'Robot_SemiHarvest'
                R(7.06f, -2.15f, 7.659f, 9.452f, RobotType.Harvester),     // sprite 'HarvesterRobot'
                R(3.85f, -2.2f,  7.659f, 9.452f, RobotType.Harvester),     // sprite 'HarvesterRobot'
            });

        // ── W1_L18  ────────────────────────────────────────────────────────────
        // Redesigned from scratch 2026-07-14 (user: "I have re-designed level 18 - see dump;
        // override current and replace with new"), fully replacing the earlier version (giant
        // indestructible StoneTower + 2 Basic + 2 SemiHarvester + hand-placed Commander — see git
        // history for that layout). New design is a Commander-solo boss fight: a fully
        // destructible ascending staircase of Stone_Square/Barrel_Dynamite/Wood_Skew/Stone_Skew
        // pieces (no indestructible structure at all this time), with the Commander as the ONLY
        // robot in the level. This lines up with the same-day match-up screen change (only the
        // Commander's card slides in, no deck-of-cards for guard robots) — this redesign removes
        // the guard robots from the level itself too, not just their match-up cards.
        // Dumped via FarmFury -> Debug -> Dump Level Layout To Log (unity/Logs/level_layout_dump.txt)
        // in two passes: blocks first, then a follow-up dump once the Commander was placed (the
        // first pass had an empty robots[] — see the LevelLayoutDumper Commander-detection bug
        // fixed the same session, which is WHY the first pass's robots[] came back empty at all:
        // the dumper had no "commander" keyword/prefab-name branch in either of its two scan paths,
        // so a placed CommanderRobot instance would have silently dumped as RobotType.Basic and a
        // raw Commander sprite would have been skipped with a warning — fixed before this second,
        // now-correct dump was taken). id/name/par/birds[] unchanged from the previous version.
        // Real per-sprite scale/hp values captured directly from the Scene view. Not visually
        // verified (no Play-mode access here) — worth a live check that a single Commander (HP=90,
        // well above every other robot type) reads as a satisfying solo boss fight without the
        // other robots' HP padding out the encounter, and that the staircase structure collapses
        // sensibly under the existing block-cascade rules (see BlockBase.CheckForBlocksOnTop).
        Make(folder, "L18_CaptainsLastStand",
            id: "W1_L18", name: "Captain's Last Stand", par: 3,
            birds: new[] { AnimalType.Cluck, AnimalType.Cluck, AnimalType.Bessie },
            blocks: new[]
            {
                B(BlockType.Stone, 4.04f, -5.28f, 1f, 1f, artVariant: WoodArtVariant.Square), // sprite 'Stone_Square'
                B(BlockType.Stone, 4.71f, -5.3f, 1f, 1f, artVariant: WoodArtVariant.Square), // sprite 'Stone_Square'
                B(BlockType.Stone, 5.36f, -5.32f, 1f, 1f, artVariant: WoodArtVariant.Square), // sprite 'Stone_Square'
                B(BlockType.Stone, 7.15f, -4.55f, 1f, 1f, artVariant: WoodArtVariant.Square), // sprite 'Stone_Square'
                B(BlockType.Stone, 7.14f, -3.97f, 1f, 1f, artVariant: WoodArtVariant.Square), // sprite 'Stone_Square'
                B(BlockType.Stone, 7.16f, -5.14f, 1f, 1f, artVariant: WoodArtVariant.Square), // sprite 'Stone_Square'
                B(BlockType.Stone, 3.37f, -4.68f, 1f, 1f, artVariant: WoodArtVariant.Square), // sprite 'Stone_Square'
                B(BlockType.Stone, 3.37f, -4.09f, 1f, 1f, artVariant: WoodArtVariant.Square), // sprite 'Stone_Square'
                B(BlockType.Stone, 3.37f, -5.28f, 1f, 1f, artVariant: WoodArtVariant.Square), // sprite 'Stone_Square'
                B(BlockType.Barrel, 2.5f, -4.98f, 1.45f, 1.466f), // sprite 'Barrel_Dynamite'
                B(BlockType.Barrel, 6.234f, -4.953f, 1.581f, 1.45f), // sprite 'Barrel_Dynamite'
                B(BlockType.Wood, 3.06f, -3.59f, 1f, 1f, artVariant: WoodArtVariant.Skew), // sprite 'Plank_Skew'
                B(BlockType.Wood, 3.71f, -2.95f, 1f, 1f, artVariant: WoodArtVariant.Skew), // sprite 'Plank_Skew'
                B(BlockType.Wood, 4.35f, -2.31f, 1f, 1f, artVariant: WoodArtVariant.Skew), // sprite 'Plank_Skew'
                B(BlockType.Wood, 4.97f, -1.71f, 1f, 1f, artVariant: WoodArtVariant.Skew), // sprite 'Plank_Skew'
                B(BlockType.Wood, 5.8f, -1.87f, 1.055f, 1.055f, artVariant: WoodArtVariant.Skew), // sprite 'Plank_Skew'
                B(BlockType.Wood, 6.49f, -2.47f, 1.055f, 1.055f, artVariant: WoodArtVariant.Skew), // sprite 'Plank_Skew'
                B(BlockType.Wood, 7.13f, -3.01f, 1.055f, 1.055f, artVariant: WoodArtVariant.Skew), // sprite 'Plank_Skew'
                B(BlockType.Wood, 7.77f, -3.63f, 1.055f, 1.055f, artVariant: WoodArtVariant.Skew), // sprite 'Plank_Skew'
                B(BlockType.Stone, 4.82f, -1.19f, 1f, 1f, artVariant: WoodArtVariant.Skew), // sprite 'Stone_Skew'
                B(BlockType.Stone, 4.25f, -1.76f, 1f, 1f, artVariant: WoodArtVariant.Skew), // sprite 'Stone_Skew'
                B(BlockType.Stone, 3.68f, -2.33f, 1f, 1f, artVariant: WoodArtVariant.Skew), // sprite 'Stone_Skew'
                B(BlockType.Stone, 5.72f, -1.24f, 1.1f, 1.1f, artVariant: WoodArtVariant.Skew), // sprite 'Stone_Skew'
                B(BlockType.Stone, 6.32f, -1.75f, 1.1f, 1.1f, artVariant: WoodArtVariant.Skew), // sprite 'Stone_Skew'
                B(BlockType.Stone, 6.91f, -2.29f, 1.1f, 1.1f, artVariant: WoodArtVariant.Skew), // sprite 'Stone_Skew'
            },
            robots: new[]
            {
                R(4.67f, -4.05f, 6.985f, 7.555f, RobotType.Commander), // sprite 'Commander'
            });

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        if (!silent)
            EditorUtility.DisplayDialog("FarmFury",
                "Generated 18 LevelData assets in\nAssets/ScriptableObjects/Levels\n(L01-L18)", "OK");
        Debug.Log("[FarmFury] Level data generation complete.");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    // Deletes a previously-generated LevelData asset that no longer has a Make(...) call —
    // see the L03-L06 removal comment above for why this exists.
    static void DeleteStaleAsset(string folder, string filename)
    {
        string path = $"{folder}/{filename}.asset";
        if (AssetDatabase.LoadAssetAtPath<LevelData>(path) != null)
        {
            AssetDatabase.DeleteAsset(path);
            Debug.Log($"[FarmFury] Deleted stale level asset {path}");
        }
    }

    static void Make(string folder, string filename,
        string id, string name, int par,
        AnimalType[] birds,
        LevelData.BlockSpawnData[] blocks,
        LevelData.RobotSpawnData[] robots)
    {
        string path = $"{folder}/{filename}.asset";
        if (AssetDatabase.LoadAssetAtPath<LevelData>(path) != null)
            AssetDatabase.DeleteAsset(path);

        var asset         = ScriptableObject.CreateInstance<LevelData>();
        asset.levelId     = id;
        asset.levelName   = name;
        asset.parBirds    = par;
        asset.birds       = birds;
        asset.blocks      = blocks;
        asset.robots      = robots;

        AssetDatabase.CreateAsset(asset, path);
        Debug.Log($"[FarmFury] Created {path}");
    }

    static void EnsureFolder(string parent, string child)
    {
        string full = $"{parent}/{child}";
        if (!AssetDatabase.IsValidFolder(full))
            AssetDatabase.CreateFolder(parent, child);
    }

    // passThrough, hp, mass are optional overrides — 0 values mean "use BlockBase default".
    // artVariant only matters for BlockType.Wood — Auto (default) lets BlockBase guess the art
    // orientation from the w/h aspect ratio like before; LevelLayoutDumper sets it explicitly
    // when it knows the real design-time sprite (see InferWoodArtVariant in that file).
    static LevelData.BlockSpawnData B(BlockType type, float x, float y, float w, float h,
                                      bool passThrough = false, float hp = 0f, float mass = 0f,
                                      WoodArtVariant artVariant = WoodArtVariant.Auto,
                                      bool indestructible = false,
                                      bool forceStayKinematic = false) =>
        new() { type = type, position = new Vector2(x, y), size = new Vector2(w, h),
                passThrough = passThrough, healthOverride = hp, massOverride = mass,
                artVariant = artVariant, indestructible = indestructible,
                forceStayKinematic = forceStayKinematic };

    static LevelData.RobotSpawnData R(float x, float y, float scaleX = 0f, float scaleY = 0f,
                                      RobotType robotType = RobotType.Basic) =>
        new() { position = new Vector2(x, y), scale = new Vector2(scaleX, scaleY), robotType = robotType };
}
