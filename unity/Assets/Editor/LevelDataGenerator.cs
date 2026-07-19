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
        // Layout replaced AGAIN 2026-07-18 with a fresh dump against the new 3-layer parallax
        // backdrop (see unity/Logs/level_layout_dump.txt) — user request: "remove level 1 and use
        // new design." 4 haybale (up from the previous 4-bale pile — same count, new positions/a
        // slightly taller stack) + 1 SemiHarvester, same composition shape as the previous layout,
        // no wood/fence blocks in this dump so no decorative-prop-collision exclusion was needed
        // this time. Not visually re-verified (no Play-mode access here).
        Make(folder, "L01_FirstContact",
            id: "W1_L01", name: "First Contact", par: 2,
            birds: new[] { AnimalType.Cluck, AnimalType.Cluck, AnimalType.Cluck },
            blocks: new[]
            {
                B(BlockType.Haybale, 5.73f, -5.55f, 0.977f, 0.977f, passThrough: true, hp: 10f, mass: 3f), // sprite 'Haybail'
                B(BlockType.Haybale, 6.16f, -5.68f, 0.977f, 0.977f, passThrough: true, hp: 10f, mass: 3f), // sprite 'Haybail'
                B(BlockType.Haybale, 6.03f, -5.06f, 0.977f, 0.977f, passThrough: true, hp: 10f, mass: 3f), // sprite 'Haybail'
                B(BlockType.Haybale, 6.86f, -5.66f, 0.977f, 0.977f, passThrough: true, hp: 10f, mass: 3f), // sprite 'Haybail'
            },
            robots: new[]
            {
                R(7.658f, -5.492f, 6.001f, 6.153f, RobotType.SemiHarvester), // sprite 'Robot_SemiHarvest'
            });

        // ── W1_L02  Harvest Yard ──────────────────────────────────────────────
        // Redesigned AGAIN 2026-07-18, second dump the same day — user request: "see new dump -
        // level 2 new design". Previous same-day redesign (3 haybale + Shork/Horizontal wood
        // cluster + 2 Barrel + 3 SemiHarvester) fully replaced by this fresh dump: 4 haybale +
        // 3 wood pieces (Diagonal/Skew/Horizontal, no barrels this time) + 2 SemiHarvester (down
        // from 3). Kept the same asset filename ("L02_StoneWall") and id/name so no other
        // reference needs updating — regenerating via "Generate All Level Data" overwrites this
        // asset in place, no separate deletion needed. Not visually re-verified (no Play-mode
        // access here).
        Make(folder, "L02_StoneWall",
            id: "W1_L02", name: "Harvest Yard", par: 2,
            birds: new[] { AnimalType.Cluck, AnimalType.Cluck, AnimalType.Cluck },
            blocks: new[]
            {
                B(BlockType.Haybale, 5.85f, -5.49f, 0.977f, 0.977f, passThrough: true, hp: 10f, mass: 3f), // sprite 'Haybail'
                B(BlockType.Haybale, 6.17f, -5.63f, 0.977f, 0.977f, passThrough: true, hp: 10f, mass: 3f), // sprite 'Haybail'
                B(BlockType.Haybale, 5.8f,  -5.1f,  0.977f, 0.977f, passThrough: true, hp: 10f, mass: 3f), // sprite 'Haybail'
                B(BlockType.Haybale, 5.46f, -5.65f, 0.977f, 0.977f, passThrough: true, hp: 10f, mass: 3f), // sprite 'Haybail'
                B(BlockType.Wood, 7.01f, -5.63f, 1f, 1f,     artVariant: WoodArtVariant.Diagonal), // sprite 'Plank_Diagonal'
                B(BlockType.Wood, 7.15f, -4.94f, 1f, 1f,     artVariant: WoodArtVariant.Skew), // sprite 'Plank_Skew'
                B(BlockType.Wood, 7.03f, -4.4f,  1f, 0.492f, artVariant: WoodArtVariant.Horizontal), // sprite 'Plank_Horizontal'
            },
            robots: new[]
            {
                R(7.04f, -3.9f,  5.215f, 5.745f, RobotType.SemiHarvester), // sprite 'Robot_SemiHarvest'
                R(7.82f, -5.59f, 5.215f, 5.745f, RobotType.SemiHarvester), // sprite 'Robot_SemiHarvest'
            });

        // ── W1_L03  ────────────────────────────────────────────────────────────
        // Redesigned 2026-07-18 with a fresh hand-placed dump (FarmFury -> Debug -> Dump Level
        // Layout To Log, unity/Logs/level_layout_dump.txt) — user request: "see dump for level 3
        // new design". Old layout (6 haybale + 3 SemiHarvester, no wood/stone) fully replaced by
        // a taller mixed structure: 3 short wood planks + a decorative WoodenBarrel-art Wood
        // block (dumper classified it BlockType.Wood, not Barrel — distinct from the real
        // Barrel_Dynamite prop right next to it) + a real ExplodingBarrel + a Stone_Square block
        // + 2 vertical Plank_2DShork + a Skew plank + 2 Horizontal planks near the top, guarded
        // by 3 SemiHarvester (2 stacked near the top, 1 at the base). Kept the same asset
        // filename ("L03_TheTower") and id/name — regenerating via "Generate All Level Data"
        // overwrites this asset in place, no separate deletion needed. Not visually re-verified
        // (no Play-mode access here).
        Make(folder, "L03_TheTower",
            id: "W1_L03", name: "The Tower", par: 2,
            birds: new[] { AnimalType.Cluck, AnimalType.Cluck, AnimalType.Cluck },
            blocks: new[]
            {
                B(BlockType.Wood, 5.659f, -5.576f, 1f, 0.81f, artVariant: WoodArtVariant.Auto), // sprite 'Plank_Short'
                B(BlockType.Wood, 6.22f,  -5.6f,   1f, 0.81f, artVariant: WoodArtVariant.Auto), // sprite 'Plank_Short'
                B(BlockType.Wood, 5.68f,  -4.49f,  1f, 0.81f, artVariant: WoodArtVariant.Auto), // sprite 'Plank_Short'
                B(BlockType.Wood, 7.61f,  -5.52f,  0.977f, 0.977f, artVariant: WoodArtVariant.Auto), // sprite 'WoodenBarrel'
                B(BlockType.Barrel, 7.61f, -4.85f, 0.977f, 0.977f), // sprite 'Barrel_Dynamite'
                B(BlockType.Stone, 5.66f, -5.06f, 1f, 1f, artVariant: WoodArtVariant.Square), // sprite 'Stone_Square'
                B(BlockType.Wood, 5.57f, -3.87f, 1f, 1.064f, artVariant: WoodArtVariant.Vertical), // sprite 'Plank_2DShork'
                B(BlockType.Wood, 5.89f, -3.87f, 1f, 1.064f, artVariant: WoodArtVariant.Vertical), // sprite 'Plank_2DShork'
                B(BlockType.Wood, 5.83f, -3.11f, 1f, 1f,     artVariant: WoodArtVariant.Skew), // sprite 'Plank_Skew'
                B(BlockType.Wood, 5.73f, -2.53f, 1f, 0.492f, artVariant: WoodArtVariant.Horizontal), // sprite 'Plank_Horizontal'
                B(BlockType.Wood, 6.64f, -2.53f, 1f, 0.492f, artVariant: WoodArtVariant.Horizontal), // sprite 'Plank_Horizontal'
            },
            robots: new[]
            {
                R(5.75f, -1.987f, 5.103f, 6.242f, RobotType.SemiHarvester), // sprite 'Robot_SemiHarvest'
                R(6.68f, -2.01f,  5.103f, 6.242f, RobotType.SemiHarvester), // sprite 'Robot_SemiHarvest'
                R(6.95f, -5.49f,  5.103f, 6.242f, RobotType.SemiHarvester), // sprite 'Robot_SemiHarvest'
            });

        // ── W1_L04  ────────────────────────────────────────────────────────────
        // Redesigned 2026-07-18 with a fresh hand-placed dump (FarmFury -> Debug -> Dump Level
        // Layout To Log, unity/Logs/level_layout_dump.txt) — user request: "see level 4 dump new
        // design". Old layout (3 haybale + 2-tier wood stack + 1 barrel + 4 SemiHarvester) fully
        // replaced by a taller, denser structure: a WoodenCart-art Wood block + 2 haybale + 4
        // Stone pieces (2 Square base + 2 Vertical) + 2 vertical Plank_2DShork + a horizontal
        // Plank_2DHorizontal + a dynamite barrel + a Skew plank + 2 short vertical planks near the
        // top, guarded by 5 SemiHarvester (up from 4, 2 of them stacked near the very top).
        // Cluck's Cluster Bomb ability still unlocks here (AnimalBase.AbilityIntroLevelIndex = 3,
        // 0-based, i.e. L04) — unaffected by this layout swap. Kept the same asset filename
        // ("L04_EggPractice") and id/name — regenerating via "Generate All Level Data" overwrites
        // this asset in place, no separate deletion needed. Not visually re-verified (no Play-mode
        // access here).
        Make(folder, "L04_EggPractice",
            id: "W1_L04", name: "Egg Practice", par: 2,
            birds: new[] { AnimalType.Cluck, AnimalType.Cluck, AnimalType.Cluck },
            blocks: new[]
            {
                B(BlockType.Wood, 4.98f, -5.39f, 1.632f, 1.432f, artVariant: WoodArtVariant.Auto), // sprite 'WoodenCart'
                B(BlockType.Haybale, 7.87f, -5.6f, 0.977f, 0.977f, passThrough: true, hp: 10f, mass: 3f), // sprite 'Haybail'
                B(BlockType.Haybale, 5f,    -4.96f, 0.977f, 0.977f, passThrough: true, hp: 10f, mass: 3f), // sprite 'Haybail'
                B(BlockType.Stone, 6.577f, -5.667f, 1f, 1f,       artVariant: WoodArtVariant.Square), // sprite 'Stone_Square'
                B(BlockType.Stone, 7.16f,  -5.68f,  1f, 1f,       artVariant: WoodArtVariant.Square), // sprite 'Stone_Square'
                B(BlockType.Stone, 6.88f,  -5.1f,   0.48f, 1f,    artVariant: WoodArtVariant.Vertical), // sprite 'Stone_Vertical'
                B(BlockType.Stone, 7.37f,  -5.1f,   0.48f, 1f,    artVariant: WoodArtVariant.Vertical), // sprite 'Stone_Vertical'
                B(BlockType.Wood, 6.41f, -4.98f, 1f, 1.064f, artVariant: WoodArtVariant.Vertical), // sprite 'Plank_2DShork'
                B(BlockType.Wood, 6.42f, -4.17f, 1f, 1.064f, artVariant: WoodArtVariant.Vertical), // sprite 'Plank_2DShork'
                B(BlockType.Wood, 6.97f, -4.59f, 1f, 1f,     artVariant: WoodArtVariant.Horizontal), // sprite 'Plank_2DHorizontal'
                B(BlockType.Barrel, 5.98f, -5.43f, 0.977f, 0.977f), // sprite 'Barrel_Dynamite'
                B(BlockType.Wood, 6.64f, -3.52f, 1f, 1f,     artVariant: WoodArtVariant.Skew), // sprite 'Plank_Skew'
                B(BlockType.Wood, 6.45f, -2.88f, 1f, 0.618f, artVariant: WoodArtVariant.Auto), // sprite 'Plank_VeriticalShort'
                B(BlockType.Wood, 7.22f, -2.87f, 1f, 0.618f, artVariant: WoodArtVariant.Auto), // sprite 'Plank_VeriticalShort'
            },
            robots: new[]
            {
                R(4.98f, -4.38f, 3.967f, 4.552f, RobotType.SemiHarvester), // sprite 'Robot_SemiHarvest'
                R(7.84f, -4.99f, 3.967f, 4.552f, RobotType.SemiHarvester), // sprite 'Robot_SemiHarvest'
                R(6.95f, -4.18f, 3.967f, 4.552f, RobotType.SemiHarvester), // sprite 'Robot_SemiHarvest'
                R(7.16f, -2.4f,  3.967f, 4.552f, RobotType.SemiHarvester), // sprite 'Robot_SemiHarvest'
                R(6.42f, -2.4f,  3.967f, 4.552f, RobotType.SemiHarvester), // sprite 'Robot_SemiHarvest'
            });

        // ── W1_L05  The Gauntlet ──────────────────────────────────────────────
        // Redesigned 2026-07-18 with a fresh hand-placed dump (FarmFury -> Debug -> Dump Level
        // Layout To Log, unity/Logs/level_layout_dump.txt) — user request: "level 5 new design
        // dumped". Old layout (3 haybale + 2-tier Shork2D wood stack + 2 barrels + 4
        // SemiHarvester + 1 Harvester) fully replaced by a taller central column: 4 vertical
        // Stone pieces at the base + 2 short wood planks + a horizontal plank + a Skew plank +
        // 2 tiers of paired Horizontal/Shork2D planks + 3 dynamite barrels woven through the
        // stack + 2 haybale near the base, guarded by 4 SemiHarvester (3 clustered near the top,
        // 1 mid-height) + 1 Harvester (base, largest scale in the level — matches this level's
        // established Harvester-introduction role). Kept the same asset filename
        // ("L05_TheGauntlet") and id/name — regenerating via "Generate All Level Data" overwrites
        // this asset in place, no separate deletion needed. Not visually re-verified (no
        // Play-mode access here).
        Make(folder, "L05_TheGauntlet",
            id: "W1_L05", name: "The Gauntlet", par: 2,
            birds: new[] { AnimalType.Cluck, AnimalType.Cluck, AnimalType.Cluck },
            blocks: new[]
            {
                B(BlockType.Stone, 4.47f, -5.37f, 0.48f, 1f, artVariant: WoodArtVariant.Vertical), // sprite 'Stone_Vertical'
                B(BlockType.Stone, 5.81f, -5.37f, 0.48f, 1f, artVariant: WoodArtVariant.Vertical), // sprite 'Stone_Vertical'
                B(BlockType.Stone, 6.22f, -5.39f, 0.48f, 1f, artVariant: WoodArtVariant.Vertical), // sprite 'Stone_Vertical'
                B(BlockType.Stone, 4.84f, -5.37f, 0.48f, 1f, artVariant: WoodArtVariant.Vertical), // sprite 'Stone_Vertical'
                B(BlockType.Wood, 4.62f, -4.65f, 1f, 0.81f, artVariant: WoodArtVariant.Auto), // sprite 'Plank_Short'
                B(BlockType.Wood, 5.98f, -4.67f, 1f, 0.81f, artVariant: WoodArtVariant.Auto), // sprite 'Plank_Short'
                B(BlockType.Wood, 5.22f, -4.24f, 1f, 0.492f, artVariant: WoodArtVariant.Horizontal), // sprite 'Plank_Horizontal'
                B(BlockType.Wood, 5.39f, -3.71f, 1f, 1f,     artVariant: WoodArtVariant.Skew), // sprite 'Plank_Skew'
                B(BlockType.Wood, 5.1f,  -3.2f,  1f, 1f,     artVariant: WoodArtVariant.Horizontal), // sprite 'Plank_2DHorizontal'
                B(BlockType.Wood, 5.91f, -3.18f, 1f, 1f,     artVariant: WoodArtVariant.Horizontal), // sprite 'Plank_2DHorizontal'
                B(BlockType.Wood, 4.8f,  -2.8f,  1f, 1.064f, artVariant: WoodArtVariant.Vertical), // sprite 'Plank_2DShork'
                B(BlockType.Wood, 6.32f, -2.78f, 1f, 1.064f, artVariant: WoodArtVariant.Vertical), // sprite 'Plank_2DShork'
                B(BlockType.Wood, 4.7f,  -2.33f, 1f, 1f,     artVariant: WoodArtVariant.Horizontal), // sprite 'Plank_2DHorizontal'
                B(BlockType.Wood, 6.26f, -2.33f, 1f, 1f,     artVariant: WoodArtVariant.Horizontal), // sprite 'Plank_2DHorizontal'
                B(BlockType.Barrel, 5.33f, -5.31f, 0.977f, 0.977f), // sprite 'Barrel_Dynamite'
                B(BlockType.Barrel, 6.02f, -3.97f, 0.977f, 0.977f), // sprite 'Barrel_Dynamite'
                B(BlockType.Barrel, 5.52f, -2.71f, 0.977f, 0.977f), // sprite 'Barrel_Dynamite'
                B(BlockType.Haybale, 6.755f, -5.425f, 0.977f, 0.977f, passThrough: true, hp: 10f, mass: 3f), // sprite 'Haybail'
                B(BlockType.Haybale, 7.38f,  -5.425f, 0.977f, 0.977f, passThrough: true, hp: 10f, mass: 3f), // sprite 'Haybail'
            },
            robots: new[]
            {
                R(5.54f, -1.96f,  5.102f, 5.856f, RobotType.SemiHarvester), // sprite 'Robot_SemiHarvest'
                R(6.26f, -1.83f,  5.102f, 5.856f, RobotType.SemiHarvester), // sprite 'Robot_SemiHarvest'
                R(4.76f, -1.85f,  5.102f, 5.856f, RobotType.SemiHarvester), // sprite 'Robot_SemiHarvest'
                R(4.55f, -3.96f,  5.102f, 5.856f, RobotType.SemiHarvester), // sprite 'Robot_SemiHarvest'
                R(7.05f, -4.61f,  8.954f, 7.685f, RobotType.Harvester),     // sprite 'HarvesterRobot'
            });

        // Old L05 asset filename, orphaned by the rename above (L05_BessiesDebut -> L05_TheGauntlet
        // — GameManager auto-loads every LevelData asset it finds in this folder, so the stale
        // file would otherwise linger as a phantom extra level).
        DeleteStaleAsset(folder, "L05_BessiesDebut");

        DeleteStaleAsset(folder, "L06_BessiesDebut"); // stale old-plan filename, orphaned since the L05/L07 rename

        // ── W1_L06  Double Barrel ─────────────────────────────────────────────
        // Redesigned 2026-07-18 with a fresh hand-placed dump (FarmFury -> Debug -> Dump Level
        // Layout To Log, unity/Logs/level_layout_dump.txt) — user request: "level 6 is dumped".
        // Old layout (2 Harvester + 3 SemiHarvester, 2 barrels, 3 haybale, a stone block, dense
        // wood scaffold) fully replaced by a tall central column: a WoodenCart-art Wood block +
        // 2 vertical Stone pieces + a short vertical plank + 2 haybale near the base + 2 vertical
        // Plank_2DShork tiers + a Stone_Skew piece + 2 Horizontal planks + 3 dynamite barrels
        // woven through the stack + a Skew wood plank + 2 more Horizontal planks near the top,
        // guarded by 4 SemiHarvester (mostly mid-to-upper) + 1 Harvester (base, by far the
        // largest scale used in any level so far — 12.235x11.186). birds[] stays 3x Cluck,
        // matching every level before it. Kept the same asset filename ("L06_DoubleBarrel") and
        // id/name — regenerating via "Generate All Level Data" overwrites this asset in place, no
        // separate deletion needed. Not visually re-verified (no Play-mode access here).
        Make(folder, "L06_DoubleBarrel",
            id: "W1_L06", name: "Double Barrel", par: 2,
            birds: new[] { AnimalType.Cluck, AnimalType.Cluck, AnimalType.Cluck },
            blocks: new[]
            {
                B(BlockType.Wood, 5.73f, -5.34f, 1.686f, 1.428f, artVariant: WoodArtVariant.Auto), // sprite 'WoodenCart'
                B(BlockType.Stone, 5.38f, -5f,   0.48f, 1f, artVariant: WoodArtVariant.Vertical), // sprite 'Stone_Vertical'
                B(BlockType.Stone, 6.29f, -4.99f, 0.48f, 1f, artVariant: WoodArtVariant.Vertical), // sprite 'Stone_Vertical'
                B(BlockType.Wood, 5.88f, -4.47f, 1f, 0.618f, artVariant: WoodArtVariant.Auto), // sprite 'Plank_VeriticalShort'
                B(BlockType.Haybale, 4.74f, -5.32f, 0.977f, 0.977f, passThrough: true, hp: 10f, mass: 3f), // sprite 'Haybail'
                B(BlockType.Haybale, 4.32f, -5.53f, 0.977f, 0.977f, passThrough: true, hp: 10f, mass: 3f), // sprite 'Haybail'
                B(BlockType.Wood, 5.38f, -4.24f, 1f, 1.064f, artVariant: WoodArtVariant.Vertical), // sprite 'Plank_2DShork'
                B(BlockType.Wood, 6.39f, -4.27f, 1f, 1.064f, artVariant: WoodArtVariant.Vertical), // sprite 'Plank_2DShork'
                B(BlockType.Wood, 6.45f, -1.99f, 1f, 1.064f, artVariant: WoodArtVariant.Vertical), // sprite 'Plank_2DShork'
                B(BlockType.Stone, 5.45f, -3.65f, 1f, 1f,     artVariant: WoodArtVariant.Skew), // sprite 'Stone_Skew'
                B(BlockType.Wood, 5.58f, -3.14f, 1f, 0.492f, artVariant: WoodArtVariant.Horizontal), // sprite 'Plank_Horizontal'
                B(BlockType.Wood, 6.66f, -3.75f, 1f, 0.492f, artVariant: WoodArtVariant.Horizontal), // sprite 'Plank_Horizontal'
                B(BlockType.Barrel, 6.87f, -5.38f, 0.977f, 0.977f), // sprite 'Barrel_Dynamite'
                B(BlockType.Barrel, 4.62f, -4.75f, 0.977f, 0.977f), // sprite 'Barrel_Dynamite'
                B(BlockType.Barrel, 5.9f,  -4.05f, 0.977f, 0.977f), // sprite 'Barrel_Dynamite'
                B(BlockType.Wood, 6.14f, -2.67f, 1f, 1f,     artVariant: WoodArtVariant.Skew), // sprite 'Plank_Skew'
                B(BlockType.Wood, 6.03f, -1.49f, 1f, 0.492f, artVariant: WoodArtVariant.Horizontal), // sprite 'Plank_Horizontal'
                B(BlockType.Wood, 6.79f, -1.51f, 1f, 0.492f, artVariant: WoodArtVariant.Horizontal), // sprite 'Plank_Horizontal'
            },
            robots: new[]
            {
                R(5.498f, -2.619f, 5.962f, 7.535f,  RobotType.SemiHarvester), // sprite 'Robot_SemiHarvest'
                R(5.998f, -0.986f, 6.09f, 6.89f,    RobotType.SemiHarvester), // sprite 'Robot_SemiHarvest'
                R(6.738f, -3.158f, 6.09f, 7.406f,   RobotType.SemiHarvester), // sprite 'Robot_SemiHarvest'
                R(6.899f, -1.018f, 6.22f, 7.277f,   RobotType.SemiHarvester), // sprite 'Robot_SemiHarvest'
                R(7.82f,  -5.26f,  12.235f, 11.186f, RobotType.Harvester),    // sprite 'HarvesterRobot'
            });

        // ── W1_L07  Barrel Row ────────────────────────────────────────────────
        // Redesigned 2026-07-18 with a fresh hand-placed dump (FarmFury -> Debug -> Dump Level
        // Layout To Log, unity/Logs/level_layout_dump.txt) — user request: "see level7 new dump".
        // Old layout (2 Harvester + 4 SemiHarvester, a WoodenCart prop, 2 barrels, 2 haybale, 9
        // wood planks) fully replaced by a twin-column structure: 2 tall vertical Plank_Shork
        // columns with matching Horizontal/Skew/Horizontal tiers on each side, meeting in the
        // middle at a base row of 1 haybale + 2 Stone_Square blocks, 3 vertical Stone pieces + 2
        // Stone_Diagonal pieces near the top, 4 dynamite barrels woven through, guarded by 5
        // SemiHarvester + 2 Harvester (the densest robot count of any level so far, 7 total).
        // birds[] stays 3x Cluck, matching L05/L06 — Bessie's debut is still L10. Kept the same
        // asset filename ("L07_BarrelRow") and id/name — regenerating via "Generate All Level
        // Data" overwrites this asset in place, no separate deletion needed. Not visually
        // re-verified (no Play-mode access here).
        Make(folder, "L07_BarrelRow",
            id: "W1_L07", name: "Barrel Row", par: 2,
            birds: new[] { AnimalType.Cluck, AnimalType.Cluck, AnimalType.Cluck },
            blocks: new[]
            {
                B(BlockType.Wood, 3.82f, -5.078f, 1f, 2.037f, artVariant: WoodArtVariant.Vertical), // sprite 'Plank_Shork'
                B(BlockType.Wood, 7.08f, -5.13f,  1f, 2.037f, artVariant: WoodArtVariant.Vertical), // sprite 'Plank_Shork'
                B(BlockType.Wood, 3.756f, -4.178f, 1f, 0.492f, artVariant: WoodArtVariant.Horizontal), // sprite 'Plank_Horizontal'
                B(BlockType.Wood, 7f,     -4.21f,  1f, 0.492f, artVariant: WoodArtVariant.Horizontal), // sprite 'Plank_Horizontal'
                B(BlockType.Wood, 4.31f, -3.65f, 1f, 1f, artVariant: WoodArtVariant.Skew), // sprite 'Plank_Skew'
                B(BlockType.Wood, 7.51f, -3.67f, 1f, 1f, artVariant: WoodArtVariant.Skew), // sprite 'Plank_Skew'
                B(BlockType.Wood, 4.01f, -3.11f, 1f, 0.492f, artVariant: WoodArtVariant.Horizontal), // sprite 'Plank_Horizontal'
                B(BlockType.Wood, 4.91f, -3.13f, 1f, 0.492f, artVariant: WoodArtVariant.Horizontal), // sprite 'Plank_Horizontal'
                B(BlockType.Wood, 7.01f, -3.22f, 1f, 0.492f, artVariant: WoodArtVariant.Horizontal), // sprite 'Plank_Horizontal'
                B(BlockType.Stone, 3.71f, -2.76f, 0.48f, 1f, artVariant: WoodArtVariant.Vertical), // sprite 'Stone_Vertical'
                B(BlockType.Stone, 5.26f, -2.74f, 0.48f, 1f, artVariant: WoodArtVariant.Vertical), // sprite 'Stone_Vertical'
                B(BlockType.Stone, 7.7f,  -3.02f, 0.48f, 1f, artVariant: WoodArtVariant.Vertical), // sprite 'Stone_Vertical'
                B(BlockType.Stone, 3.62f, -2.11f, 1f, 1f, artVariant: WoodArtVariant.Diagonal), // sprite 'Stone_Diagonal'
                B(BlockType.Stone, 7.85f, -2.33f, 1f, 1f, artVariant: WoodArtVariant.Diagonal), // sprite 'Stone_Diagonal'
                B(BlockType.Barrel, 3.21f, -5.53f, 0.977f, 0.977f), // sprite 'Barrel_Dynamite'
                B(BlockType.Barrel, 4.63f, -2.7f,  0.977f, 0.977f), // sprite 'Barrel_Dynamite'
                B(BlockType.Barrel, 6.84f, -3.73f, 0.977f, 0.977f), // sprite 'Barrel_Dynamite'
                B(BlockType.Barrel, 7.69f, -5.55f, 0.977f, 0.977f), // sprite 'Barrel_Dynamite'
                B(BlockType.Wood, 4.89f, -2.15f, 1f, 0.492f, artVariant: WoodArtVariant.Horizontal), // sprite 'Plank_Horizontal'
                B(BlockType.Haybale, 4.589f, -5.477f, 0.977f, 0.977f, passThrough: true, hp: 10f, mass: 3f), // sprite 'Haybail'
                B(BlockType.Stone, 5.492f, -5.522f, 1f, 1f, artVariant: WoodArtVariant.Square), // sprite 'Stone_Square'
                B(BlockType.Stone, 6.3f,   -5.55f,  1f, 1f, artVariant: WoodArtVariant.Square), // sprite 'Stone_Square'
            },
            robots: new[]
            {
                R(4.58f, -4.81f,  5.7f,  5.785f, RobotType.SemiHarvester), // sprite 'Robot_SemiHarvest'
                R(3.62f, -3.75f,  5.7f,  5.785f, RobotType.SemiHarvester), // sprite 'Robot_SemiHarvest'
                R(3.69f, -1.52f,  5.7f,  5.785f, RobotType.SemiHarvester), // sprite 'Robot_SemiHarvest'
                R(4.97f, -1.6f,   5.7f,  5.785f, RobotType.SemiHarvester), // sprite 'Robot_SemiHarvest'
                R(6.99f, -2.7f,   5.7f,  5.898f, RobotType.SemiHarvester), // sprite 'Robot_SemiHarvest'
                R(5.932f, -4.714f, 11.339f, 9.056f, RobotType.Harvester), // sprite 'HarvesterRobot'
                R(7.78f, -1.6f,   11.339f, 9.056f, RobotType.Harvester),  // sprite 'HarvesterRobot'
            });

        // Old L07 asset filename, orphaned by the rename above (L07_BessiesDebut -> L07_BarrelRow
        // — GameManager auto-loads every LevelData asset it finds in this folder, so the stale
        // file would otherwise linger as a phantom extra level).
        DeleteStaleAsset(folder, "L07_BessiesDebut");

        // ── W1_L08  Fortress Assault ──────────────────────────────────────────
        // Redesigned 2026-07-18 with a fresh hand-placed dump (FarmFury -> Debug -> Dump Level
        // Layout To Log, unity/Logs/level_layout_dump.txt) — user request: "see level 8 design".
        // Old layout (3 Harvester + 4 SemiHarvester, a WoodenCart + 2 WoodenBarrel props, 1
        // barrel, 1 haybale, 2 stone pillars, a wood tower) fully replaced by a wider 3-column
        // structure: 1 dynamite barrel + 2 WoodenBarrel-art Wood props near the base, 3 tall
        // vertical Plank_Shork columns, 3 Stone_Square blocks mid-height, 2 Skew wood planks, 4
        // haybale woven through, a wide upper deck of 6 Horizontal wood planks + 3 vertical wood
        // planks + a second dynamite barrel near the top. Guarded by 6 SemiHarvester + 2
        // Harvester (8 robots — the heaviest count shipped so far, up from 7). birds[] stays 3x
        // Cluck, matching L05-L07 — Bessie's debut is still L10. Kept the same asset filename
        // ("L08_FortressAssault") and id/name — regenerating via "Generate All Level Data"
        // overwrites this asset in place, no separate deletion needed. Not visually re-verified
        // (no Play-mode access here).
        Make(folder, "L08_FortressAssault",
            id: "W1_L08", name: "Fortress Assault", par: 3,
            birds: new[] { AnimalType.Cluck, AnimalType.Cluck, AnimalType.Cluck },
            blocks: new[]
            {
                B(BlockType.Barrel, 3.97f, -5.45f, 0.977f, 0.977f), // sprite 'Barrel_Dynamite'
                B(BlockType.Barrel, 6.14f, -0.83f, 0.977f, 0.977f), // sprite 'Barrel_Dynamite'
                B(BlockType.Wood, 4.592f, -5.464f, 0.977f, 0.977f, artVariant: WoodArtVariant.Auto), // sprite 'WoodenBarrel'
                B(BlockType.Wood, 4.3f,   -4.84f,  0.977f, 0.977f, artVariant: WoodArtVariant.Auto), // sprite 'WoodenBarrel'
                B(BlockType.Wood, 5.13f, -5.058f, 1f, 2.037f, artVariant: WoodArtVariant.Vertical), // sprite 'Plank_Shork'
                B(BlockType.Wood, 7.7f,  -5.18f,  1f, 2.037f, artVariant: WoodArtVariant.Vertical), // sprite 'Plank_Shork'
                B(BlockType.Wood, 6.41f, -5.1f,   1f, 2.037f, artVariant: WoodArtVariant.Vertical), // sprite 'Plank_Shork'
                B(BlockType.Stone, 5.1f,  -4f,     1f, 1f, artVariant: WoodArtVariant.Square), // sprite 'Stone_Square'
                B(BlockType.Stone, 6.33f, -4.013f, 1f, 1f, artVariant: WoodArtVariant.Square), // sprite 'Stone_Square'
                B(BlockType.Stone, 7.64f, -4.12f,  1f, 1f, artVariant: WoodArtVariant.Square), // sprite 'Stone_Square'
                B(BlockType.Wood, 5.27f, -3.3f, 1f, 1f, artVariant: WoodArtVariant.Skew), // sprite 'Plank_Skew'
                B(BlockType.Wood, 7.69f, -3.4f, 1f, 1f, artVariant: WoodArtVariant.Skew), // sprite 'Plank_Skew'
                B(BlockType.Haybale, 5.74f, -5.59f, 0.977f, 0.977f, passThrough: true, hp: 10f, mass: 3f), // sprite 'Haybail'
                B(BlockType.Haybale, 5.78f, -4.99f, 0.977f, 0.977f, passThrough: true, hp: 10f, mass: 3f), // sprite 'Haybail'
                B(BlockType.Haybale, 7.01f, -5.67f, 0.977f, 0.977f, passThrough: true, hp: 10f, mass: 3f), // sprite 'Haybail'
                B(BlockType.Haybale, 7f,    -4.99f, 0.977f, 0.977f, passThrough: true, hp: 10f, mass: 3f), // sprite 'Haybail'
                B(BlockType.Wood, 5.027f, -2.867f, 1f, 1f, artVariant: WoodArtVariant.Horizontal), // sprite 'Plank_2DHorizontal'
                B(BlockType.Wood, 5.87f,  -2.88f,  1f, 1f, artVariant: WoodArtVariant.Horizontal), // sprite 'Plank_2DHorizontal'
                B(BlockType.Wood, 7.37f,  -2.93f,  1f, 1f, artVariant: WoodArtVariant.Horizontal), // sprite 'Plank_2DHorizontal'
                B(BlockType.Wood, 6.12f,  -1.25f,  1f, 1f, artVariant: WoodArtVariant.Horizontal), // sprite 'Plank_2DHorizontal'
                B(BlockType.Wood, 7.7f,   -1.99f,  1f, 1f, artVariant: WoodArtVariant.Horizontal), // sprite 'Plank_2DHorizontal'
                B(BlockType.Wood, 8.22f,  -2.93f,  1f, 1f, artVariant: WoodArtVariant.Horizontal), // sprite 'Plank_2DHorizontal'
                B(BlockType.Wood, 7.81f, -2.47f, 1f, 1.064f, artVariant: WoodArtVariant.Vertical), // sprite '2D_Block_Wood_Vertical'
                B(BlockType.Wood, 6.2f,  -2.46f, 1f, 1.064f, artVariant: WoodArtVariant.Vertical), // sprite '2D_Block_Wood_Vertical'
                B(BlockType.Wood, 6.21f, -1.69f, 1f, 1.064f, artVariant: WoodArtVariant.Vertical), // sprite '2D_Block_Wood_Vertical'
            },
            robots: new[]
            {
                R(7.25f, -2.49f,  4.442f, 5.059f, RobotType.SemiHarvester), // sprite 'Robot_SemiHarvest'
                R(6.4f,  -3.38f,  4.442f, 5.059f, RobotType.SemiHarvester), // sprite 'Robot_SemiHarvest'
                R(4.311f, -4.211f, 4.442f, 5.059f, RobotType.SemiHarvester), // sprite 'Robot_SemiHarvest'
                R(7.02f, -4.34f,  4.442f, 5.059f, RobotType.SemiHarvester), // sprite 'Robot_SemiHarvest'
                R(5.75f, -4.4f,   4.442f, 5.059f, RobotType.SemiHarvester), // sprite 'Robot_SemiHarvest'
                R(8.25f, -2.49f,  4.442f, 5.059f, RobotType.SemiHarvester), // sprite 'Robot_SemiHarvest'
                R(5.36f, -2.32f,  9.081f, 7.251f, RobotType.Harvester),     // sprite 'HarvesterRobot'
                R(7.6f,  -1.37f,  9.081f, 7.251f, RobotType.Harvester),     // sprite 'HarvesterRobot'
            });

        // ── W1_L09  Stone Bastion ─────────────────────────────────────────────
        // Redesigned 2026-07-18 with a fresh hand-placed dump (FarmFury -> Debug -> Dump Level
        // Layout To Log, unity/Logs/level_layout_dump.txt) — user request: "see new dump level9".
        // Old layout (4 Harvester + 2 SemiHarvester, 2 barrels, 4 haybale, a WoodenCart, 2
        // RuinedStoneWall pieces, dense wood scaffold) fully replaced by a wide twin-tower
        // structure: 7 Stone_Square blocks split across a left/right base and a mid-height
        // bridge, 3 haybale, 5 dynamite barrels woven through, 4 vertical Plank_2DShork columns,
        // 8 Horizontal wood planks forming two full-width decks, and a WoodenBarrel-art Wood prop
        // at the very top. Guarded by 3 SemiHarvester + 3 Harvester (6 robots total — lighter
        // robot count than L08's 8, but a taller/wider structure). birds[] stays 3x Cluck,
        // matching L05-L08 — Bessie's debut is still L10. Kept the same asset filename
        // ("L09_StoneBastion") and id/name — regenerating via "Generate All Level Data"
        // overwrites this asset in place, no separate deletion needed. Not visually re-verified
        // (no Play-mode access here).
        Make(folder, "L09_StoneBastion",
            id: "W1_L09", name: "Stone Bastion", par: 3,
            birds: new[] { AnimalType.Cluck, AnimalType.Cluck, AnimalType.Cluck },
            blocks: new[]
            {
                B(BlockType.Stone, 7.62f, -2.81f, 1f, 1f, artVariant: WoodArtVariant.Square), // sprite 'Stone_Square'
                B(BlockType.Stone, 7.64f, -2.27f, 1f, 1f, artVariant: WoodArtVariant.Square), // sprite 'Stone_Square'
                B(BlockType.Stone, 4.1f,  -5.52f, 1f, 1f, artVariant: WoodArtVariant.Square), // sprite 'Stone_Square'
                B(BlockType.Stone, 5.57f, -5.5f,  1f, 1f, artVariant: WoodArtVariant.Square), // sprite 'Stone_Square'
                B(BlockType.Stone, 7.74f, -5.56f, 1f, 1f, artVariant: WoodArtVariant.Square), // sprite 'Stone_Square'
                B(BlockType.Stone, 7.63f, -3.36f, 1f, 1f, artVariant: WoodArtVariant.Square), // sprite 'Stone_Square'
                B(BlockType.Stone, 4.36f, -3.32f, 1f, 1f, artVariant: WoodArtVariant.Square), // sprite 'Stone_Square'
                B(BlockType.Haybale, 5.7f,  -4.81f, 0.977f, 0.977f, passThrough: true, hp: 10f, mass: 3f), // sprite 'Haybail'
                B(BlockType.Haybale, 4.09f, -4.84f, 0.977f, 0.977f, passThrough: true, hp: 10f, mass: 3f), // sprite 'Haybail'
                B(BlockType.Barrel, 4.26f, -2.62f, 0.977f, 0.977f), // sprite 'Barrel_Dynamite'
                B(BlockType.Barrel, 4.68f, -1.39f, 0.977f, 0.977f), // sprite 'Barrel_Dynamite'
                B(BlockType.Barrel, 5.27f, -1.37f, 0.977f, 0.977f), // sprite 'Barrel_Dynamite'
                B(BlockType.Barrel, 4.9f,  -5.44f, 0.977f, 0.977f), // sprite 'Barrel_Dynamite'
                B(BlockType.Barrel, 7.75f, -4.91f, 0.977f, 0.977f), // sprite 'Barrel_Dynamite'
                B(BlockType.Wood, 7.59f, -4.24f,  1f, 1.064f, artVariant: WoodArtVariant.Vertical), // sprite 'Plank_2DShork'
                B(BlockType.Wood, 4.29f, -4.26f,  1f, 1.064f, artVariant: WoodArtVariant.Vertical), // sprite 'Plank_2DShork'
                B(BlockType.Wood, 4.74f, -2.509f, 1f, 1.287f, artVariant: WoodArtVariant.Vertical), // sprite 'Plank_2DShork'
                B(BlockType.Wood, 7.91f, -4.27f,  1f, 1.064f, artVariant: WoodArtVariant.Vertical), // sprite 'Plank_2DShork'
                B(BlockType.Wood, 7.211f, -3.715f, 1f, 0.492f, artVariant: WoodArtVariant.Horizontal), // sprite 'Plank_Horizontal'
                B(BlockType.Wood, 6.3f,   -3.7f,   1f, 0.492f, artVariant: WoodArtVariant.Horizontal), // sprite 'Plank_Horizontal'
                B(BlockType.Wood, 5.42f,  -3.7f,   1f, 0.492f, artVariant: WoodArtVariant.Horizontal), // sprite 'Plank_Horizontal'
                B(BlockType.Wood, 4.51f,  -3.7f,   1f, 0.492f, artVariant: WoodArtVariant.Horizontal), // sprite 'Plank_Horizontal'
                B(BlockType.Wood, 8.1f,   -3.715f, 1f, 0.492f, artVariant: WoodArtVariant.Horizontal), // sprite 'Plank_Horizontal'
                B(BlockType.Wood, 7.51f,  -1.87f,  1f, 0.492f, artVariant: WoodArtVariant.Horizontal), // sprite 'Plank_Horizontal'
                B(BlockType.Wood, 6.65f,  -1.85f,  1f, 0.492f, artVariant: WoodArtVariant.Horizontal), // sprite 'Plank_Horizontal'
                B(BlockType.Wood, 5.7f,   -1.86f,  1f, 0.492f, artVariant: WoodArtVariant.Horizontal), // sprite 'Plank_Horizontal'
                B(BlockType.Wood, 4.78f,  -1.86f,  1f, 0.492f, artVariant: WoodArtVariant.Horizontal), // sprite 'Plank_Horizontal'
                B(BlockType.Wood, 4.96f,  -0.75f,  0.977f, 0.977f, artVariant: WoodArtVariant.Auto), // sprite 'WoodenBarrel'
            },
            robots: new[]
            {
                R(5.12f, -3.2f,  4.988f, 5.913f, RobotType.SemiHarvester), // sprite 'Robot_SemiHarvest'
                R(7.53f, -1.39f, 4.988f, 5.913f, RobotType.SemiHarvester), // sprite 'Robot_SemiHarvest'
                R(4.88f, -4.64f, 4.988f, 5.913f, RobotType.SemiHarvester), // sprite 'Robot_SemiHarvest'
                R(6.7f,  -5.23f, 11.759f, 9.685f, RobotType.Harvester), // sprite 'HarvesterRobot'
                R(6.48f, -2.97f, 11.759f, 9.685f, RobotType.Harvester), // sprite 'HarvesterRobot'
                R(6.39f, -1.15f, 11.759f, 9.685f, RobotType.Harvester), // sprite 'HarvesterRobot'
            });

        // ── W1_L10  ────────────────────────────────────────────────────────────
        // Redesigned from scratch 2026-07-19, part of the full 18-level redesign against the new
        // 3-layer parallax backdrop (see the 2026-07-18 Current Status entries) — replaces the
        // previous 2026-07-13 layout entirely, pasted verbatim from unity/Logs/level_layout_dump.txt
        // (FarmFury -> Debug -> Dump Level Layout To Log). id/name/par/birds[] (Bessie's debut, 2x
        // Cluck + 1x Bessie) are unchanged — the dump only captures blocks[]/robots[]. No
        // StoneTower/indestructible structure in this rebuild. 2 SemiHarvester + 2 Basic/Robot_Pawn
        // + 2 Harvester. Not visually verified (no Play-mode access here).
        Make(folder, "L10_BessiesDebut",
            id: "W1_L10", name: "Bessie's Debut", par: 3,
            birds: new[] { AnimalType.Cluck, AnimalType.Cluck, AnimalType.Bessie },
            blocks: new[]
            {
                B(BlockType.Stone, 3.73f, -5.596f, 1f, 1f, artVariant: WoodArtVariant.Square), // sprite 'Stone_Square'
                B(BlockType.Stone, 5.94f, -5.58f, 1f, 1f, artVariant: WoodArtVariant.Square), // sprite 'Stone_Square'
                B(BlockType.Stone, 7.97f, -5.62f, 1f, 1f, artVariant: WoodArtVariant.Square), // sprite 'Stone_Square'
                B(BlockType.Stone, 5.91f, -4.19f, 1f, 1f, artVariant: WoodArtVariant.Square), // sprite 'Stone_Square'
                B(BlockType.Stone, 5.95f, -2.31f, 1f, 1f, artVariant: WoodArtVariant.Square), // sprite 'Stone_Square'
                B(BlockType.Wood, 5.96f, -5.04f, 1f, 0.81f, artVariant: WoodArtVariant.Auto), // sprite 'Plank_Short'
                B(BlockType.Wood, 7.98f, -5.01f, 1f, 0.81f, artVariant: WoodArtVariant.Auto), // sprite 'Plank_Short'
                B(BlockType.Wood, 5.97f, -2.88f, 1f, 0.81f, artVariant: WoodArtVariant.Auto), // sprite 'Plank_Short'
                B(BlockType.Wood, 3.94f, -4.87f, 1f, 1f, artVariant: WoodArtVariant.Skew), // sprite 'Plank_Skew'
                B(BlockType.Wood, 4.69f, -4.58f, 1f, 0.492f, artVariant: WoodArtVariant.Horizontal), // sprite 'Plank_Horizontal'
                B(BlockType.Wood, 6.64f, -0.07f, 1f, 0.492f, artVariant: WoodArtVariant.Horizontal), // sprite 'Plank_Horizontal'
                B(BlockType.Wood, 7.53f, -0.07f, 1f, 0.492f, artVariant: WoodArtVariant.Horizontal), // sprite 'Plank_Horizontal'
                B(BlockType.Wood, 6.41f, -1.86f, 1f, 0.492f, artVariant: WoodArtVariant.Horizontal), // sprite 'Plank_Horizontal'
                B(BlockType.Wood, 5.44f, -1.88f, 1f, 0.492f, artVariant: WoodArtVariant.Horizontal), // sprite 'Plank_Horizontal'
                B(BlockType.Wood, 5.62f, -4.6f, 1f, 0.492f, artVariant: WoodArtVariant.Horizontal), // sprite 'Plank_Horizontal'
                B(BlockType.Wood, 6.52f, -4.62f, 1f, 0.492f, artVariant: WoodArtVariant.Horizontal), // sprite 'Plank_Horizontal'
                B(BlockType.Wood, 7.43f, -4.65f, 1f, 0.492f, artVariant: WoodArtVariant.Horizontal), // sprite 'Plank_Horizontal'
                B(BlockType.Haybale, 3.022f, -5.527f, 0.977f, 0.977f, passThrough: true, hp: 10f, mass: 3f), // sprite 'Haybail'
                B(BlockType.Haybale, 4.52f, -4.17f, 0.977f, 0.977f, passThrough: true, hp: 10f, mass: 3f), // sprite 'Haybail'
                B(BlockType.Haybale, 5.31f, -4.17f, 0.977f, 0.977f, passThrough: true, hp: 10f, mass: 3f), // sprite 'Haybail'
                B(BlockType.Haybale, 4.88f, -3.58f, 0.977f, 0.977f, passThrough: true, hp: 10f, mass: 3f), // sprite 'Haybail'
                B(BlockType.Barrel, 5.94f, -3.52f, 0.977f, 0.977f), // sprite 'Barrel_Dynamite'
                B(BlockType.Barrel, 6.49f, -1.35f, 0.977f, 0.977f), // sprite 'Barrel_Dynamite'
                B(BlockType.Wood, 6.51f, -0.95f, 1f, 2.037f, artVariant: WoodArtVariant.Vertical), // sprite 'Plank_Shork'
            },
            robots: new[]
            {
                R(6.7f, -4.031f, 5.374f, 6.692f, RobotType.SemiHarvester), // sprite 'Robot_SemiHarvest'
                R(7.57f, -4.09f, 5.374f, 6.692f, RobotType.SemiHarvester), // sprite 'Robot_SemiHarvest'
                R(5.49f, -1.1f, 8.641f, 10.596f), // sprite 'Robot_Pawn'
                R(7.03f, 0.69f, 8.641f, 10.596f), // sprite 'Robot_Pawn'
                R(4.83f, -5.39f, 11.122f, 9.561f, RobotType.Harvester), // sprite 'HarvesterRobot'
                R(6.9f, -5.41f, 11.122f, 9.561f, RobotType.Harvester), // sprite 'HarvesterRobot'
            });

        // ── W1_L11  ────────────────────────────────────────────────────────────
        // Redesigned from scratch 2026-07-19, part of the full 18-level redesign against the new
        // 3-layer parallax backdrop (see the 2026-07-18 Current Status entries) — replaces the
        // previous layout entirely, pasted verbatim from unity/Logs/level_layout_dump.txt (FarmFury
        // -> Debug -> Dump Level Layout To Log). id/name/par/birds[] (2x Cluck + 1x Bessie) are
        // unchanged — the dump only captures blocks[]/robots[]. Still "Full Roster": 1 SemiHarvester
        // + 3 Basic/Robot_Pawn + 1 Harvester. Not visually verified (no Play-mode access here).
        Make(folder, "L11_FullRoster",
            id: "W1_L11", name: "Full Roster", par: 3,
            birds: new[] { AnimalType.Cluck, AnimalType.Cluck, AnimalType.Bessie },
            blocks: new[]
            {
                B(BlockType.Wood, 3.49f, -5.45f, 0.977f, 0.977f, artVariant: WoodArtVariant.Auto), // sprite 'WoodenBarrel'
                B(BlockType.Wood, 3.81f, -5.69f, 0.977f, 0.977f, artVariant: WoodArtVariant.Auto), // sprite 'WoodenBarrel'
                B(BlockType.Wood, 4.44f, -5.49f, 0.977f, 0.977f, artVariant: WoodArtVariant.Auto), // sprite 'WoodenBarrel'
                B(BlockType.Barrel, 3.69f, -4.99f, 0.977f, 0.977f), // sprite 'Barrel_Dynamite'
                B(BlockType.Barrel, 4.39f, -4.8f, 0.977f, 0.977f), // sprite 'Barrel_Dynamite'
                B(BlockType.Haybale, 7.45f, -3.26f, 0.977f, 0.977f, passThrough: true, hp: 10f, mass: 3f), // sprite 'Haybail'
                B(BlockType.Haybale, 7.45f, -3.92f, 0.977f, 0.977f, passThrough: true, hp: 10f, mass: 3f), // sprite 'Haybail'
                B(BlockType.Wood, 5.13f, -5.55f, 1f, 0.81f, artVariant: WoodArtVariant.Auto), // sprite 'Plank_Short'
                B(BlockType.Wood, 5.13f, -4.41f, 1f, 0.81f, artVariant: WoodArtVariant.Auto), // sprite 'Plank_Short'
                B(BlockType.Wood, 5.15f, -3.23f, 1f, 0.81f, artVariant: WoodArtVariant.Auto), // sprite 'Plank_Short'
                B(BlockType.Stone, 5.1f, -5.05f, 1f, 1f, artVariant: WoodArtVariant.Block), // sprite 'Stone_Block'
                B(BlockType.Stone, 5.1f, -3.91f, 1f, 1f, artVariant: WoodArtVariant.Block), // sprite 'Stone_Block'
                B(BlockType.Wood, 4.75f, -2.69f, 1f, 0.618f, artVariant: WoodArtVariant.Auto), // sprite 'Plank_VeriticalShort'
                B(BlockType.Wood, 5.54f, -2.69f, 1f, 0.618f, artVariant: WoodArtVariant.Auto), // sprite 'Plank_VeriticalShort'
                B(BlockType.Wood, 6.37f, -2.71f, 1f, 0.618f, artVariant: WoodArtVariant.Auto), // sprite 'Plank_VeriticalShort'
                B(BlockType.Wood, 7.16f, -2.69f, 1f, 0.618f, artVariant: WoodArtVariant.Auto), // sprite 'Plank_VeriticalShort'
                B(BlockType.Wood, 7.94f, -2.71f, 1f, 0.618f, artVariant: WoodArtVariant.Auto), // sprite 'Plank_VeriticalShort'
                B(BlockType.Wood, 7.55f, -5.04f, 1f, 2.037f, artVariant: WoodArtVariant.Vertical), // sprite 'Plank_Shork'
                B(BlockType.Stone, 5.01f, -2.14f, 1.545f, 1.443f, artVariant: WoodArtVariant.Auto), // sprite 'RuinedStoneWall'
                B(BlockType.Stone, 6.2f, -2.14f, 1.545f, 1.443f, artVariant: WoodArtVariant.Auto), // sprite 'RuinedStoneWall'
                B(BlockType.Stone, 7.42f, -2.12f, 1.545f, 1.443f, artVariant: WoodArtVariant.Auto), // sprite 'RuinedStoneWall'
                B(BlockType.Wood, 5.704f, -4.13f, 1.148f, 1f, artVariant: WoodArtVariant.Horizontal), // sprite 'Plank_2DHorizontal'
                B(BlockType.Wood, 6.67f, -4.11f, 1f, 1f, artVariant: WoodArtVariant.Horizontal), // sprite 'Plank_2DHorizontal'
            },
            robots: new[]
            {
                R(6.22f, -3.54f, 5.894f, 6.738f, RobotType.SemiHarvester), // sprite 'Robot_SemiHarvest'
                R(5.13f, -1.7f, 8.604f, 10.048f), // sprite 'Robot_Pawn'
                R(6.2f, -1.76f, 8.604f, 10.048f), // sprite 'Robot_Pawn'
                R(7.35f, -1.72f, 8.604f, 10.048f), // sprite 'Robot_Pawn'
                R(6.28f, -5.25f, 11.991f, 9.301f, RobotType.Harvester), // sprite 'HarvesterRobot'
            });

        // ── W1_L12  ────────────────────────────────────────────────────────────
        // Redesigned from scratch 2026-07-19, part of the full 18-level redesign against the new
        // 3-layer parallax backdrop (see the 2026-07-18 Current Status entries) — replaces the
        // previous layout entirely, pasted verbatim from unity/Logs/level_layout_dump.txt (FarmFury
        // -> Debug -> Dump Level Layout To Log). id/name/par/birds[] (2x Cluck + 1x Bessie) are
        // unchanged — the dump only captures blocks[]/robots[]. Still heaviest-yet robot count: 2
        // Harvester + 3 Basic/Robot_Pawn + 3 SemiHarvester (8 total). Not visually verified (no
        // Play-mode access here).
        Make(folder, "L12_HeavyGuard",
            id: "W1_L12", name: "Heavy Guard", par: 3,
            birds: new[] { AnimalType.Cluck, AnimalType.Cluck, AnimalType.Bessie },
            blocks: new[]
            {
                B(BlockType.Wood, 3.44f, -5.519f, 0.977f, 0.977f, artVariant: WoodArtVariant.Auto), // sprite 'WoodenBarrel'
                B(BlockType.Wood, 3.42f, -4.83f, 0.977f, 0.977f, artVariant: WoodArtVariant.Auto), // sprite 'WoodenBarrel'
                B(BlockType.Wood, 4.86f, -4.92f, 0.977f, 0.977f, artVariant: WoodArtVariant.Auto), // sprite 'WoodenBarrel'
                B(BlockType.Wood, 4.88f, -5.54f, 0.977f, 0.977f, artVariant: WoodArtVariant.Auto), // sprite 'WoodenBarrel'
                B(BlockType.Stone, 3.61f, -4.45f, 1f, 1f, artVariant: WoodArtVariant.Diagonal), // sprite 'Stone_Diagonal'
                B(BlockType.Stone, 4.59f, -4.45f, 1f, 1f, artVariant: WoodArtVariant.Diagonal), // sprite 'Stone_Diagonal'
                B(BlockType.Stone, 6.7f, -5.5f, 0.48f, 1f, artVariant: WoodArtVariant.Vertical), // sprite 'Stone_Vertical'
                B(BlockType.Stone, 7.7f, -5.54f, 0.48f, 1f, artVariant: WoodArtVariant.Vertical), // sprite 'Stone_Vertical'
                B(BlockType.Stone, 7.68f, -4.84f, 0.48f, 1f, artVariant: WoodArtVariant.Vertical), // sprite 'Stone_Vertical'
                B(BlockType.Stone, 4.9f, -1.68f, 0.48f, 1f, artVariant: WoodArtVariant.Vertical), // sprite 'Stone_Vertical'
                B(BlockType.Stone, 7.3f, -1.161f, 0.48f, 1.139f, artVariant: WoodArtVariant.Vertical), // sprite 'Stone_Vertical'
                B(BlockType.Stone, 4.88f, -0.99f, 0.48f, 1f, artVariant: WoodArtVariant.Vertical), // sprite 'Stone_Vertical'
                B(BlockType.Stone, 6.83f, -4.69f, 1f, 1f, artVariant: WoodArtVariant.Skew), // sprite 'Stone_Skew'
                B(BlockType.Wood, 7.33f, -4.33f, 1f, 1f, artVariant: WoodArtVariant.Horizontal), // sprite 'Plank_2DHorizontal'
                B(BlockType.Wood, 3.77f, -2.75f, 1f, 1f, artVariant: WoodArtVariant.Horizontal), // sprite 'Plank_2DHorizontal'
                B(BlockType.Wood, 5.33f, -2.72f, 1f, 1f, artVariant: WoodArtVariant.Horizontal), // sprite 'Plank_2DHorizontal'
                B(BlockType.Wood, 6.15f, -2.72f, 1f, 1f, artVariant: WoodArtVariant.Horizontal), // sprite 'Plank_2DHorizontal'
                B(BlockType.Wood, 5.14f, -0.6f, 1f, 1f, artVariant: WoodArtVariant.Horizontal), // sprite 'Plank_2DHorizontal'
                B(BlockType.Wood, 5.97f, -0.6f, 1f, 1f, artVariant: WoodArtVariant.Horizontal), // sprite 'Plank_2DHorizontal'
                B(BlockType.Wood, 6.77f, -0.58f, 1f, 1f, artVariant: WoodArtVariant.Horizontal), // sprite 'Plank_2DHorizontal'
                B(BlockType.Wood, 4.61f, -2.78f, 1f, 1f, artVariant: WoodArtVariant.Horizontal), // sprite 'Plank_2DHorizontal'
                B(BlockType.Barrel, 7.22f, -5.41f, 0.977f, 0.977f), // sprite 'Barrel_Dynamite'
                B(BlockType.Barrel, 6.82f, -1.47f, 0.977f, 0.977f), // sprite 'Barrel_Dynamite'
                B(BlockType.Barrel, 2.87f, -5.48f, 0.977f, 0.977f), // sprite 'Barrel_Dynamite'
                B(BlockType.Barrel, 5.8f, -0.11f, 0.977f, 0.977f), // sprite 'Barrel_Dynamite'
                B(BlockType.Haybale, 5.62f, -5.53f, 0.977f, 0.977f, passThrough: true, hp: 10f, mass: 3f), // sprite 'Haybail'
                B(BlockType.Haybale, 6.27f, -5.53f, 0.977f, 0.977f, passThrough: true, hp: 10f, mass: 3f), // sprite 'Haybail'
                B(BlockType.Haybale, 5.86f, -4.97f, 0.977f, 0.977f, passThrough: true, hp: 10f, mass: 3f), // sprite 'Haybail'
                B(BlockType.Wood, 3.36f, -3.94f, 1f, 1.064f, artVariant: WoodArtVariant.Vertical), // sprite 'Plank_2DShork'
                B(BlockType.Wood, 3.38f, -3.15f, 1f, 1.064f, artVariant: WoodArtVariant.Vertical), // sprite 'Plank_2DShork'
                B(BlockType.Wood, 4.92f, -3.91f, 1f, 1.064f, artVariant: WoodArtVariant.Vertical), // sprite 'Plank_2DShork'
                B(BlockType.Wood, 4.92f, -3.16f, 1f, 1.064f, artVariant: WoodArtVariant.Vertical), // sprite 'Plank_2DShork'
                B(BlockType.Wood, 4.94f, -2.37f, 1f, 1.064f, artVariant: WoodArtVariant.Vertical), // sprite 'Plank_2DShork'
                B(BlockType.Wood, 3.27f, -2.53f, 1f, 1f, artVariant: WoodArtVariant.Skew), // sprite 'Plank_Skew'
                B(BlockType.Wood, 5.27f, -3.9f, 1f, 1f, artVariant: WoodArtVariant.Skew), // sprite 'Plank_Skew'
                B(BlockType.Wood, 5.9f, -3.27f, 1f, 1f, artVariant: WoodArtVariant.Skew), // sprite 'Plank_Skew'
                B(BlockType.Wood, 6.54f, -2.63f, 1f, 1f, artVariant: WoodArtVariant.Skew), // sprite 'Plank_Skew'
                B(BlockType.Wood, 7.13f, -1.97f, 1f, 1f, artVariant: WoodArtVariant.Skew), // sprite 'Plank_Skew'
                B(BlockType.Wood, 5.04f, -0.15f, 1f, 1f, artVariant: WoodArtVariant.Skew), // sprite 'Plank_Skew'
            },
            robots: new[]
            {
                R(4.03f, -3.8f, 9.636f, 8.01f, RobotType.Harvester), // sprite 'HarvesterRobot'
                R(5.77f, -2.06f, 9.636f, 8.01f, RobotType.Harvester), // sprite 'HarvesterRobot'
                R(7.3f, -3.58f, 7.865f, 9.466f), // sprite 'Robot_Pawn'
                R(4.14f, -2.04f, 7.865f, 9.466f), // sprite 'Robot_Pawn'
                R(6.61f, 0.15f, 7.865f, 9.466f), // sprite 'Robot_Pawn'
                R(4.16f, -5.43f, 4.652f, 5.594f, RobotType.SemiHarvester), // sprite 'Robot_SemiHarvest'
                R(5.65f, 0.61f, 4.652f, 5.594f, RobotType.SemiHarvester), // sprite 'Robot_SemiHarvest'
                R(5.85f, -4.36f, 4.652f, 5.594f, RobotType.SemiHarvester), // sprite 'Robot_SemiHarvest'
            });

        // ── W1_L13  ────────────────────────────────────────────────────────────
        // Redesigned from scratch 2026-07-19, part of the full 18-level redesign against the new
        // 3-layer parallax backdrop (see the 2026-07-18 Current Status entries) — replaces the
        // previous layout entirely, pasted verbatim from unity/Logs/level_layout_dump.txt (FarmFury
        // -> Debug -> Dump Level Layout To Log). id/name/par/birds[] (2x Cluck + 1x Bessie) are
        // unchanged — the dump only captures blocks[]/robots[]. Still "Ten Strong": 2 SemiHarvester
        // + 3 Basic/Robot_Pawn + 2 Harvester (7 total this pass). Not visually verified (no
        // Play-mode access here).
        Make(folder, "L13_TenStrong",
            id: "W1_L13", name: "Ten Strong", par: 3,
            birds: new[] { AnimalType.Cluck, AnimalType.Cluck, AnimalType.Bessie },
            blocks: new[]
            {
                B(BlockType.Wood, 3.703f, -5.583f, 1f, 1f, artVariant: WoodArtVariant.Diagonal), // sprite 'Plank_Diagonal'
                B(BlockType.Wood, 4.4f, -5.6f, 1f, 1f, artVariant: WoodArtVariant.Diagonal), // sprite 'Plank_Diagonal'
                B(BlockType.Wood, 4.35f, -4.45f, 1f, 1f, artVariant: WoodArtVariant.Diagonal), // sprite 'Plank_Diagonal'
                B(BlockType.Wood, 3.71f, -5.02f, 1f, 1f, artVariant: WoodArtVariant.Diagonal), // sprite 'Plank_Diagonal'
                B(BlockType.Stone, 4.398f, -5.097f, 1f, 1f, artVariant: WoodArtVariant.Square), // sprite 'Stone_Square'
                B(BlockType.Stone, 4.36f, -3.97f, 1f, 1f, artVariant: WoodArtVariant.Square), // sprite 'Stone_Square'
                B(BlockType.Wood, 4.22f, -3.34f, 1f, 1.064f, artVariant: WoodArtVariant.Vertical), // sprite 'Plank_2DShork'
                B(BlockType.Wood, 4.62f, -3.34f, 1f, 1.064f, artVariant: WoodArtVariant.Vertical), // sprite 'Plank_2DShork'
                B(BlockType.Wood, 6.6f, -3.27f, 1f, 1.064f, artVariant: WoodArtVariant.Vertical), // sprite 'Plank_2DShork'
                B(BlockType.Wood, 5.04f, -5.01f, 1f, 1f, artVariant: WoodArtVariant.Skew), // sprite 'Plank_Skew'
                B(BlockType.Stone, 5.14f, -4.44f, 1f, 1f, artVariant: WoodArtVariant.Diagonal), // sprite 'Stone_Diagonal'
                B(BlockType.Wood, 5.995f, -4.42f, 1f, 0.618f, artVariant: WoodArtVariant.Auto), // sprite 'Plank_VeriticalShort'
                B(BlockType.Wood, 6.603f, -5.062f, 1f, 2.037f, artVariant: WoodArtVariant.Vertical), // sprite 'Plank_Shork'
                B(BlockType.Haybale, 3.008f, -5.549f, 0.977f, 0.977f, passThrough: true, hp: 10f, mass: 3f), // sprite 'Haybail'
                B(BlockType.Haybale, 5.39f, -2.37f, 0.977f, 0.977f, passThrough: true, hp: 10f, mass: 3f), // sprite 'Haybail'
                B(BlockType.Barrel, 3.69f, -4.33f, 0.977f, 0.977f), // sprite 'Barrel_Dynamite'
                B(BlockType.Barrel, 3.68f, -3.69f, 0.977f, 0.977f), // sprite 'Barrel_Dynamite'
                B(BlockType.Barrel, 5.24f, -5.53f, 0.977f, 0.977f), // sprite 'Barrel_Dynamite'
                B(BlockType.Barrel, 6.56f, -3.88f, 0.977f, 0.977f), // sprite 'Barrel_Dynamite'
                B(BlockType.Barrel, 5.38f, -1.69f, 0.977f, 0.977f), // sprite 'Barrel_Dynamite'
                B(BlockType.Wood, 4.382f, -2.837f, 1f, 0.492f, artVariant: WoodArtVariant.Horizontal), // sprite 'Plank_Horizontal'
                B(BlockType.Wood, 5.27f, -2.85f, 1f, 0.492f, artVariant: WoodArtVariant.Horizontal), // sprite 'Plank_Horizontal'
                B(BlockType.Wood, 6.18f, -2.84f, 1f, 0.492f, artVariant: WoodArtVariant.Horizontal), // sprite 'Plank_Horizontal'
                B(BlockType.Wood, 7.15f, -4.41f, 1f, 0.492f, artVariant: WoodArtVariant.Horizontal), // sprite 'Plank_Horizontal'
                B(BlockType.Wood, 7.99f, -4.41f, 1f, 0.492f, artVariant: WoodArtVariant.Horizontal), // sprite 'Plank_Horizontal'
                B(BlockType.Wood, 4.74f, -1.34f, 1f, 0.492f, artVariant: WoodArtVariant.Horizontal), // sprite 'Plank_Horizontal'
                B(BlockType.Wood, 5.82f, -1.31f, 1f, 0.492f, artVariant: WoodArtVariant.Horizontal), // sprite 'Plank_Horizontal'
                B(BlockType.Stone, 4.14f, -2.43f, 0.48f, 1f, artVariant: WoodArtVariant.Vertical), // sprite 'Stone_Vertical'
                B(BlockType.Stone, 4.13f, -1.71f, 0.48f, 1f, artVariant: WoodArtVariant.Vertical), // sprite 'Stone_Vertical'
                B(BlockType.Stone, 4.12f, -1.01f, 0.48f, 1f, artVariant: WoodArtVariant.Vertical), // sprite 'Stone_Vertical'
                B(BlockType.Stone, 4.34f, -0.53f, 1f, 1f, artVariant: WoodArtVariant.Skew), // sprite 'Stone_Skew'
            },
            robots: new[]
            {
                R(5.92f, -5.49f, 5.366f, 6.356f, RobotType.SemiHarvester), // sprite 'Robot_SemiHarvest'
                R(4.68f, -2.33f, 5.366f, 6.356f, RobotType.SemiHarvester), // sprite 'Robot_SemiHarvest'
                R(5.445f, -3.74f, 8.94f, 9.223f), // sprite 'Robot_Pawn'
                R(6.25f, -2.12f, 8.94f, 9.223f), // sprite 'Robot_Pawn'
                R(5.35f, -0.59f, 8.94f, 9.223f), // sprite 'Robot_Pawn'
                R(7.59f, -5.37f, 11.193f, 9.538f, RobotType.Harvester), // sprite 'HarvesterRobot'
                R(7.56f, -3.66f, 11.193f, 9.538f, RobotType.Harvester), // sprite 'HarvesterRobot'
            });

        // ── W1_L14  ────────────────────────────────────────────────────────────
        // Redesigned from scratch 2026-07-19, part of the full 18-level redesign against the new
        // 3-layer parallax backdrop (see the 2026-07-18 Current Status entries) — replaces the
        // previous layout entirely, pasted verbatim from unity/Logs/level_layout_dump.txt (FarmFury
        // -> Debug -> Dump Level Layout To Log). id/name/par/birds[] (2x Cluck + 1x Bessie) are
        // unchanged — the dump only captures blocks[]/robots[]. Now 2 Basic/Robot_Pawn + 3
        // SemiHarvester + 3 Harvester (8 total). Not visually verified (no Play-mode access here).
        Make(folder, "L14_StoneAndPawns",
            id: "W1_L14", name: "Stone and Pawns", par: 3,
            birds: new[] { AnimalType.Cluck, AnimalType.Cluck, AnimalType.Bessie },
            blocks: new[]
            {
                B(BlockType.Haybale, 6.27f, -4.98f, 0.977f, 0.977f, passThrough: true, hp: 10f, mass: 3f), // sprite 'Haybail'
                B(BlockType.Haybale, 7.8f, -2.52f, 0.977f, 0.977f, passThrough: true, hp: 10f, mass: 3f), // sprite 'Haybail'
                B(BlockType.Haybale, 7.81f, -1.91f, 0.977f, 0.977f, passThrough: true, hp: 10f, mass: 3f), // sprite 'Haybail'
                B(BlockType.Haybale, 7.8f, -1.31f, 0.977f, 0.977f, passThrough: true, hp: 10f, mass: 3f), // sprite 'Haybail'
                B(BlockType.Stone, 8.085f, -5.52f, 1.03f, 1.12f, artVariant: WoodArtVariant.Square), // sprite 'Stone_Square'
                B(BlockType.Stone, 8.04f, -4.64f, 1f, 1f, artVariant: WoodArtVariant.Square), // sprite 'Stone_Square'
                B(BlockType.Stone, 8.04f, -3.53f, 1f, 1f, artVariant: WoodArtVariant.Square), // sprite 'Stone_Square'
                B(BlockType.Stone, 4.4f, -4.65f, 1f, 1f, artVariant: WoodArtVariant.Square), // sprite 'Stone_Square'
                B(BlockType.Wood, 8.07f, -4.08f, 1f, 0.81f, artVariant: WoodArtVariant.Auto), // sprite 'Plank_Short'
                B(BlockType.Wood, 4.24f, -3.31f, 1f, 0.81f, artVariant: WoodArtVariant.Auto), // sprite 'Plank_Short'
                B(BlockType.Wood, 4.26f, -4.02f, 1f, 1.064f, artVariant: WoodArtVariant.Vertical), // sprite 'Plank_2DShork'
                B(BlockType.Wood, 4.34f, -2.58f, 1f, 1f, artVariant: WoodArtVariant.Skew), // sprite 'Plank_Skew'
                B(BlockType.Wood, 8f, -3.06f, 1f, 1f, artVariant: WoodArtVariant.Skew), // sprite 'Plank_Skew'
                B(BlockType.Wood, 4.88f, -1.98f, 1f, 1f, artVariant: WoodArtVariant.Skew), // sprite 'Plank_Skew'
                B(BlockType.Wood, 5.48f, -1.37f, 1f, 1f, artVariant: WoodArtVariant.Skew), // sprite 'Plank_Skew'
                B(BlockType.Wood, 6.05f, -0.7f, 1f, 1f, artVariant: WoodArtVariant.Skew), // sprite 'Plank_Skew'
                B(BlockType.Barrel, 3.48f, -5.46f, 0.977f, 0.977f), // sprite 'Barrel_Dynamite'
                B(BlockType.Barrel, 4.66f, -0.98f, 0.977f, 0.977f), // sprite 'Barrel_Dynamite'
                B(BlockType.Barrel, 6.8f, -0.45f, 0.977f, 0.977f), // sprite 'Barrel_Dynamite'
                B(BlockType.Barrel, 4.11f, -5.46f, 0.977f, 0.977f), // sprite 'Barrel_Dynamite'
                B(BlockType.Wood, 3.783f, -4.875f, 0.977f, 0.977f, artVariant: WoodArtVariant.Auto), // sprite 'WoodenBarrel'
                B(BlockType.Wood, 4.53f, -5.04f, 1f, 0.492f, artVariant: WoodArtVariant.Horizontal), // sprite 'Plank_Horizontal'
                B(BlockType.Wood, 4.65f, -2.91f, 1f, 0.492f, artVariant: WoodArtVariant.Horizontal), // sprite 'Plank_Horizontal'
                B(BlockType.Wood, 5.5f, -2.92f, 1f, 0.492f, artVariant: WoodArtVariant.Horizontal), // sprite 'Plank_Horizontal'
                B(BlockType.Wood, 6.42f, -2.91f, 1f, 0.492f, artVariant: WoodArtVariant.Horizontal), // sprite 'Plank_Horizontal'
                B(BlockType.Wood, 7.34f, -2.92f, 1f, 0.492f, artVariant: WoodArtVariant.Horizontal), // sprite 'Plank_Horizontal'
                B(BlockType.Wood, 5.43f, -5.05f, 1f, 0.492f, artVariant: WoodArtVariant.Horizontal), // sprite 'Plank_Horizontal'
                B(BlockType.Wood, 4.08f, -2.16f, 1f, 0.492f, artVariant: WoodArtVariant.Horizontal), // sprite 'Plank_Horizontal'
                B(BlockType.Wood, 4.71f, -1.47f, 1f, 0.492f, artVariant: WoodArtVariant.Horizontal), // sprite 'Plank_Horizontal'
                B(BlockType.Wood, 6.38f, -0.94f, 1f, 0.492f, artVariant: WoodArtVariant.Horizontal), // sprite 'Plank_Horizontal'
                B(BlockType.Wood, 7.26f, -0.93f, 1f, 0.492f, artVariant: WoodArtVariant.Horizontal), // sprite 'Plank_Horizontal'
                B(BlockType.Wood, 5.38f, -0.84f, 1f, 0.492f, artVariant: WoodArtVariant.Horizontal), // sprite 'Plank_Horizontal'
                B(BlockType.Wood, 6.97f, -5.05f, 1f, 0.492f, artVariant: WoodArtVariant.Horizontal), // sprite 'Plank_Horizontal'
                B(BlockType.Wood, 7.83f, -5.05f, 1f, 0.492f, artVariant: WoodArtVariant.Horizontal), // sprite 'Plank_Horizontal'
                B(BlockType.Haybale, 6f, -5.55f, 0.977f, 0.977f, passThrough: true, hp: 10f, mass: 3f), // sprite 'Haybail'
                B(BlockType.Haybale, 6.52f, -5.56f, 0.977f, 0.977f, passThrough: true, hp: 10f, mass: 3f), // sprite 'Haybail'
            },
            robots: new[]
            {
                R(5.57f, -2.38f, 6.822f, 7.545f), // sprite 'Robot_Pawn'
                R(7.52f, -0.34f, 6.822f, 7.545f), // sprite 'Robot_Pawn'
                R(5.34f, -0.41f, 4.204f, 5.687f, RobotType.SemiHarvester), // sprite 'Robot_SemiHarvest'
                R(4.03f, -1.65f, 4.204f, 5.687f, RobotType.SemiHarvester), // sprite 'Robot_SemiHarvest'
                R(3.79f, -4.22f, 4.204f, 5.687f, RobotType.SemiHarvester), // sprite 'Robot_SemiHarvest'
                R(5.32f, -4.46f, 9.029f, 8.225f, RobotType.Harvester), // sprite 'HarvesterRobot'
                R(7.15f, -4.48f, 9.029f, 8.225f, RobotType.Harvester), // sprite 'HarvesterRobot'
                R(6.83f, -2.27f, 9.029f, 8.225f, RobotType.Harvester), // sprite 'HarvesterRobot'
            });

        // ── W1_L15  ────────────────────────────────────────────────────────────
        // Redesigned from scratch 2026-07-19, part of the full 18-level redesign against the new
        // 3-layer parallax backdrop (see the 2026-07-18 Current Status entries) — replaces the
        // previous layout entirely, pasted verbatim from unity/Logs/level_layout_dump.txt (FarmFury
        // -> Debug -> Dump Level Layout To Log). id/name/par/birds[] (2x Cluck + 1x Bessie) are
        // unchanged — the dump only captures blocks[]/robots[]. Now 1 Harvester + 3 Basic/
        // Robot_Pawn + 3 SemiHarvester (7 total). Not visually verified (no Play-mode access here).
        Make(folder, "L15_FiveStones",
            id: "W1_L15", name: "Five Stones", par: 3,
            birds: new[] { AnimalType.Cluck, AnimalType.Cluck, AnimalType.Bessie },
            blocks: new[]
            {
                B(BlockType.Wood, 3.82f, -5.05f, 1f, 2.037f, artVariant: WoodArtVariant.Vertical), // sprite 'Plank_Shork'
                B(BlockType.Wood, 5.02f, -3.45f, 1f, 2.037f, artVariant: WoodArtVariant.Vertical), // sprite 'Plank_Shork'
                B(BlockType.Wood, 7.03f, -3.45f, 1f, 2.037f, artVariant: WoodArtVariant.Vertical), // sprite 'Plank_Shork'
                B(BlockType.Wood, 6.55f, -1.88f, 1f, 2.037f, artVariant: WoodArtVariant.Vertical), // sprite 'Plank_Shork'
                B(BlockType.Wood, 5.58f, -4.99f, 1f, 2.037f, artVariant: WoodArtVariant.Vertical), // sprite 'Plank_Shork'
                B(BlockType.Wood, 7.51f, -4.99f, 1f, 2.037f, artVariant: WoodArtVariant.Vertical), // sprite 'Plank_Shork'
                B(BlockType.Stone, 3.53f, -4.11f, 1f, 1f, artVariant: WoodArtVariant.Skew), // sprite 'Stone_Skew'
                B(BlockType.Stone, 3.63f, -3.53f, 1f, 1f, artVariant: WoodArtVariant.Skew), // sprite 'Stone_Skew'
                B(BlockType.Stone, 3.73f, -2.89f, 1f, 1f, artVariant: WoodArtVariant.Skew), // sprite 'Stone_Skew'
                B(BlockType.Stone, 4.29f, -2.33f, 1f, 1f, artVariant: WoodArtVariant.Skew), // sprite 'Stone_Skew'
                B(BlockType.Stone, 4.92f, -0.51f, 1f, 1f, artVariant: WoodArtVariant.Skew), // sprite 'Stone_Skew'
                B(BlockType.Stone, 5.45f, 0.07f, 1f, 1f, artVariant: WoodArtVariant.Skew), // sprite 'Stone_Skew'
                B(BlockType.Wood, 4.38f, -1.72f, 1f, 1f, artVariant: WoodArtVariant.Skew), // sprite 'Plank_Skew'
                B(BlockType.Wood, 4.98f, -1.08f, 1f, 1f, artVariant: WoodArtVariant.Skew), // sprite 'Plank_Skew'
                B(BlockType.Wood, 4.79f, -2.57f, 1f, 1f, artVariant: WoodArtVariant.Horizontal), // sprite 'Plank_2DHorizontal'
                B(BlockType.Wood, 5.83f, -1.06f, 1f, 1f, artVariant: WoodArtVariant.Horizontal), // sprite 'Plank_2DHorizontal'
                B(BlockType.Wood, 7.14f, -1.04f, 1f, 1f, artVariant: WoodArtVariant.Horizontal), // sprite 'Plank_2DHorizontal'
                B(BlockType.Wood, 5.75f, -2.57f, 1f, 1f, artVariant: WoodArtVariant.Horizontal), // sprite 'Plank_2DHorizontal'
                B(BlockType.Wood, 4.24f, -4.19f, 1f, 1f, artVariant: WoodArtVariant.Horizontal), // sprite 'Plank_2DHorizontal'
                B(BlockType.Wood, 6.27f, -4.19f, 1f, 1f, artVariant: WoodArtVariant.Horizontal), // sprite 'Plank_2DHorizontal'
                B(BlockType.Wood, 7.7f, -2.66f, 1f, 1f, artVariant: WoodArtVariant.Horizontal), // sprite 'Plank_2DHorizontal'
                B(BlockType.Barrel, 4.331f, -3.728f, 0.977f, 0.977f), // sprite 'Barrel_Dynamite'
                B(BlockType.Barrel, 5.08f, -2.11f, 0.977f, 0.977f), // sprite 'Barrel_Dynamite'
                B(BlockType.Barrel, 5.76f, -0.61f, 0.977f, 0.977f), // sprite 'Barrel_Dynamite'
                B(BlockType.Barrel, 7.17f, -2.2f, 0.977f, 0.977f), // sprite 'Barrel_Dynamite'
                B(BlockType.Haybale, 3.05f, -5.5f, 0.977f, 0.977f, passThrough: true, hp: 10f, mass: 3f), // sprite 'Haybail'
            },
            robots: new[]
            {
                R(6.86f, -0.3f, 11.123f, 10.984f, RobotType.Harvester), // sprite 'HarvesterRobot'
                R(4.7f, -5.09f, 8.436f, 10.394f), // sprite 'Robot_Pawn'
                R(6.57f, -5.09f, 8.436f, 10.394f), // sprite 'Robot_Pawn'
                R(5.98f, -3.47f, 8.436f, 10.394f), // sprite 'Robot_Pawn'
                R(5.81f, -2.03f, 5.416f, 6.861f, RobotType.SemiHarvester), // sprite 'Robot_SemiHarvest'
                R(7.83f, -2.13f, 5.416f, 6.861f, RobotType.SemiHarvester), // sprite 'Robot_SemiHarvest'
                R(8.19f, -5.36f, 5.416f, 6.861f, RobotType.SemiHarvester), // sprite 'Robot_SemiHarvest'
            });

        // ── W1_L16  ────────────────────────────────────────────────────────────
        // Redesigned from scratch 2026-07-19, part of the full 18-level redesign against the new
        // 3-layer parallax backdrop (see the 2026-07-18 Current Status entries) — replaces the
        // previous layout entirely, pasted verbatim from unity/Logs/level_layout_dump.txt (FarmFury
        // -> Debug -> Dump Level Layout To Log). id/name/par/birds[] (2x Cluck + 1x Bessie) are
        // unchanged — the dump only captures blocks[]/robots[]. Now 2 SemiHarvester + 3 Basic/
        // Robot_Pawn + 3 Harvester (8 total). Not visually verified (no Play-mode access here).
        Make(folder, "L16_TwinTowers",
            id: "W1_L16", name: "Twin Towers", par: 3,
            birds: new[] { AnimalType.Cluck, AnimalType.Cluck, AnimalType.Bessie },
            blocks: new[]
            {
                B(BlockType.Stone, 4.577f, -5.334f, 1.34f, 1.208f, artVariant: WoodArtVariant.Auto), // sprite 'RuinedStoneWall'
                B(BlockType.Stone, 6.016f, -5.345f, 1.307f, 1.158f, artVariant: WoodArtVariant.Auto), // sprite 'RuinedStoneWall'
                B(BlockType.Stone, 7.503f, -5.395f, 1.241f, 1.158f, artVariant: WoodArtVariant.Auto), // sprite 'RuinedStoneWall'
                B(BlockType.Wood, 5.373f, -5.381f, 1f, 0.81f, artVariant: WoodArtVariant.Auto), // sprite 'Plank_Short'
                B(BlockType.Wood, 6.81f, -5.41f, 1f, 0.81f, artVariant: WoodArtVariant.Auto), // sprite 'Plank_Short'
                B(BlockType.Stone, 5.17f, -4.79f, 0.48f, 1f, artVariant: WoodArtVariant.Vertical), // sprite 'Stone_Vertical'
                B(BlockType.Stone, 5.57f, -4.82f, 0.48f, 1f, artVariant: WoodArtVariant.Vertical), // sprite 'Stone_Vertical'
                B(BlockType.Stone, 6.1f, -3.45f, 0.48f, 1f, artVariant: WoodArtVariant.Vertical), // sprite 'Stone_Vertical'
                B(BlockType.Stone, 5.89f, -2.57f, 0.48f, 1f, artVariant: WoodArtVariant.Vertical), // sprite 'Stone_Vertical'
                B(BlockType.Stone, 6.32f, -2.58f, 0.48f, 1f, artVariant: WoodArtVariant.Vertical), // sprite 'Stone_Vertical'
                B(BlockType.Stone, 6.09f, -1.63f, 0.48f, 1f, artVariant: WoodArtVariant.Vertical), // sprite 'Stone_Vertical'
                B(BlockType.Stone, 6.6f, -4.92f, 0.48f, 1f, artVariant: WoodArtVariant.Vertical), // sprite 'Stone_Vertical'
                B(BlockType.Stone, 7f, -4.87f, 0.48f, 1f, artVariant: WoodArtVariant.Vertical), // sprite 'Stone_Vertical'
                B(BlockType.Stone, 5.31f, -4.14f, 1f, 1f, artVariant: WoodArtVariant.Square), // sprite 'Stone_Square'
                B(BlockType.Stone, 6.77f, -4.22f, 1f, 1f, artVariant: WoodArtVariant.Square), // sprite 'Stone_Square'
                B(BlockType.Haybale, 5.36f, -3.54f, 0.977f, 0.977f, passThrough: true, hp: 10f, mass: 3f), // sprite 'Haybail'
                B(BlockType.Haybale, 6.75f, -3.62f, 0.977f, 0.977f, passThrough: true, hp: 10f, mass: 3f), // sprite 'Haybail'
                B(BlockType.Barrel, 4.57f, -3.45f, 0.977f, 0.977f), // sprite 'Barrel_Dynamite'
                B(BlockType.Barrel, 7.52f, -3.38f, 0.977f, 0.977f), // sprite 'Barrel_Dynamite'
                B(BlockType.Wood, 6.04f, -3.83f, 1f, 0.492f, artVariant: WoodArtVariant.Horizontal), // sprite 'Plank_Horizontal'
                B(BlockType.Wood, 7.43f, -2.17f, 1f, 0.492f, artVariant: WoodArtVariant.Horizontal), // sprite 'Plank_Horizontal'
                B(BlockType.Wood, 6.06f, -2.07f, 1f, 0.492f, artVariant: WoodArtVariant.Horizontal), // sprite 'Plank_Horizontal'
                B(BlockType.Wood, 5.99f, -1.1f, 1f, 0.492f, artVariant: WoodArtVariant.Horizontal), // sprite 'Plank_Horizontal'
                B(BlockType.Wood, 6.1f, -2.98f, 1f, 0.492f, artVariant: WoodArtVariant.Horizontal), // sprite 'Plank_Horizontal'
                B(BlockType.Wood, 4.52f, -2.31f, 1f, 0.492f, artVariant: WoodArtVariant.Horizontal), // sprite 'Plank_Horizontal'
                B(BlockType.Wood, 4.61f, -3.83f, 1f, 0.492f, artVariant: WoodArtVariant.Horizontal), // sprite 'Plank_Horizontal'
                B(BlockType.Wood, 7.48f, -3.84f, 1f, 0.492f, artVariant: WoodArtVariant.Horizontal), // sprite 'Plank_Horizontal'
                B(BlockType.Wood, 4.46f, -2.79f, 1f, 1.064f, artVariant: WoodArtVariant.Vertical), // sprite 'Plank_2DShork'
                B(BlockType.Wood, 4.78f, -2.83f, 1f, 1.064f, artVariant: WoodArtVariant.Vertical), // sprite 'Plank_2DShork'
                B(BlockType.Wood, 7.4f, -2.67f, 1f, 1.064f, artVariant: WoodArtVariant.Vertical), // sprite 'Plank_2DShork'
                B(BlockType.Wood, 7.72f, -2.71f, 1f, 1.064f, artVariant: WoodArtVariant.Vertical), // sprite 'Plank_2DShork'
            },
            robots: new[]
            {
                R(5.4f, -2.9f, 4.615f, 5.194f, RobotType.SemiHarvester), // sprite 'Robot_SemiHarvest'
                R(6.84f, -3.01f, 4.615f, 5.194f, RobotType.SemiHarvester), // sprite 'Robot_SemiHarvest'
                R(6.07f, -4.98f, 7.139f, 7.82f), // sprite 'Robot_Pawn'
                R(7.46f, -1.6f, 7.139f, 7.82f), // sprite 'Robot_Pawn'
                R(4.54f, -1.76f, 7.139f, 7.82f), // sprite 'Robot_Pawn'
                R(4.48f, -5.03f, 8.888f, 7.461f, RobotType.Harvester), // sprite 'HarvesterRobot'
                R(5.96f, -0.57f, 8.888f, 7.461f, RobotType.Harvester), // sprite 'HarvesterRobot'
                R(7.55f, -5.08f, 8.888f, 7.461f, RobotType.Harvester), // sprite 'HarvesterRobot'
            });

        // ── W1_L17  ────────────────────────────────────────────────────────────
        // Redesigned from scratch 2026-07-19, part of the full 18-level redesign against the new
        // 3-layer parallax backdrop (see the 2026-07-18 Current Status entries) — replaces the
        // previous layout entirely, pasted verbatim from unity/Logs/level_layout_dump.txt (FarmFury
        // -> Debug -> Dump Level Layout To Log). id/name/par/birds[] (2x Cluck + 1x Bessie) are
        // unchanged — the dump only captures blocks[]/robots[]. Now 2 Basic/Robot_Pawn + 3
        // Harvester + 3 SemiHarvester (8 total) — still the last "full roster" level before L18's
        // Captain boss. Not visually verified (no Play-mode access here).
        Make(folder, "L17_EightStrong",
            id: "W1_L17", name: "Eight Strong", par: 3,
            birds: new[] { AnimalType.Cluck, AnimalType.Cluck, AnimalType.Bessie },
            blocks: new[]
            {
                B(BlockType.Haybale, 3.37f, -5.01f, 0.977f, 0.977f, passThrough: true, hp: 10f, mass: 3f), // sprite 'Haybail'
                B(BlockType.Haybale, 4.07f, -5.54f, 0.977f, 0.977f, passThrough: true, hp: 10f, mass: 3f), // sprite 'Haybail'
                B(BlockType.Haybale, 7.33f, -5.59f, 0.977f, 0.977f, passThrough: true, hp: 10f, mass: 3f), // sprite 'Haybail'
                B(BlockType.Haybale, 7.95f, -5.62f, 0.977f, 0.977f, passThrough: true, hp: 10f, mass: 3f), // sprite 'Haybail'
                B(BlockType.Stone, 4.942f, -5.396f, 1.416f, 1.403f, artVariant: WoodArtVariant.Auto), // sprite 'RuinedStoneWall'
                B(BlockType.Stone, 6.549f, -5.391f, 1.336f, 1.336f, artVariant: WoodArtVariant.Auto), // sprite 'RuinedStoneWall'
                B(BlockType.Barrel, 5.75f, -5.389f, 0.977f, 0.977f), // sprite 'Barrel_Dynamite'
                B(BlockType.Barrel, 6.19f, -1.38f, 0.977f, 0.977f), // sprite 'Barrel_Dynamite'
                B(BlockType.Barrel, 7.28f, -1.21f, 0.977f, 0.977f), // sprite 'Barrel_Dynamite'
                B(BlockType.Barrel, 7.42f, -2.25f, 0.977f, 0.977f), // sprite 'Barrel_Dynamite'
                B(BlockType.Barrel, 5.75f, -4.72f, 0.977f, 0.977f), // sprite 'Barrel_Dynamite'
                B(BlockType.Wood, 4.02f, -4.92f, 1f, 0.81f, artVariant: WoodArtVariant.Auto), // sprite 'Plank_Short'
                B(BlockType.Wood, 7.51f, -4.95f, 1f, 0.81f, artVariant: WoodArtVariant.Auto), // sprite 'Plank_Short'
                B(BlockType.Wood, 7.48f, -3.84f, 1f, 0.81f, artVariant: WoodArtVariant.Auto), // sprite 'Plank_Short'
                B(BlockType.Wood, 4.02f, -3.79f, 1f, 0.81f, artVariant: WoodArtVariant.Auto), // sprite 'Plank_Short'
                B(BlockType.Stone, 3.99f, -4.42f, 1f, 1f, artVariant: WoodArtVariant.Square), // sprite 'Stone_Square'
                B(BlockType.Stone, 7.48f, -4.45f, 1f, 1f, artVariant: WoodArtVariant.Square), // sprite 'Stone_Square'
                B(BlockType.Wood, 4.486f, -3.398f, 1f, 1f, artVariant: WoodArtVariant.Skew), // sprite 'Plank_Skew'
                B(BlockType.Wood, 5.07f, -2.78f, 1f, 1f, artVariant: WoodArtVariant.Skew), // sprite 'Plank_Skew'
                B(BlockType.Wood, 5.64f, -2.21f, 1f, 1f, artVariant: WoodArtVariant.Skew), // sprite 'Plank_Skew'
                B(BlockType.Wood, 3.81f, -3.61f, 1f, 0.618f, artVariant: WoodArtVariant.Auto), // sprite 'Plank_VeriticalShort'
                B(BlockType.Wood, 4.56f, -2.93f, 1f, 0.618f, artVariant: WoodArtVariant.Auto), // sprite 'Plank_VeriticalShort'
                B(BlockType.Wood, 5.21f, -2.3f, 1f, 0.618f, artVariant: WoodArtVariant.Auto), // sprite 'Plank_VeriticalShort'
                B(BlockType.Stone, 7.63f, -3.52f, 1f, 1f, artVariant: WoodArtVariant.Skew), // sprite 'Stone_Skew'
                B(BlockType.Stone, 7.779f, -3.023f, 0.48f, 1f, artVariant: WoodArtVariant.Vertical), // sprite 'Stone_Vertical'
                B(BlockType.Stone, 7.79f, -2.23f, 0.48f, 1f, artVariant: WoodArtVariant.Vertical), // sprite 'Stone_Vertical'
                B(BlockType.Stone, 7.79f, -1.34f, 0.48f, 1f, artVariant: WoodArtVariant.Vertical), // sprite 'Stone_Vertical'
                B(BlockType.Stone, 6.74f, -1.43f, 0.48f, 1f, artVariant: WoodArtVariant.Vertical), // sprite 'Stone_Vertical'
                B(BlockType.Stone, 6.74f, -0.71f, 0.48f, 1f, artVariant: WoodArtVariant.Vertical), // sprite 'Stone_Vertical'
                B(BlockType.Stone, 7.78f, -0.63f, 0.48f, 1f, artVariant: WoodArtVariant.Vertical), // sprite 'Stone_Vertical'
                B(BlockType.Stone, 5.68f, -1.43f, 0.48f, 1f, artVariant: WoodArtVariant.Vertical), // sprite 'Stone_Vertical'
                B(BlockType.Wood, 7.52f, -1.71f, 1f, 0.618f, artVariant: WoodArtVariant.Auto), // sprite 'Plank_VeriticalShort'
                B(BlockType.Wood, 7.52f, -2.69f, 1f, 0.618f, artVariant: WoodArtVariant.Auto), // sprite 'Plank_VeriticalShort'
                B(BlockType.Wood, 6.787f, -1.805f, 1f, 0.492f, artVariant: WoodArtVariant.Horizontal), // sprite 'Plank_Horizontal'
                B(BlockType.Wood, 5.99f, -1.82f, 1f, 0.492f, artVariant: WoodArtVariant.Horizontal), // sprite 'Plank_Horizontal'
                B(BlockType.Wood, 6.14f, -0.9f, 1f, 0.492f, artVariant: WoodArtVariant.Horizontal), // sprite 'Plank_Horizontal'
                B(BlockType.Wood, 7.21f, -0.18f, 1f, 0.492f, artVariant: WoodArtVariant.Horizontal), // sprite 'Plank_Horizontal'
                B(BlockType.Wood, 6.88f, -4.04f, 1f, 0.492f, artVariant: WoodArtVariant.Horizontal), // sprite 'Plank_Horizontal'
                B(BlockType.Wood, 6.05f, -4.03f, 1f, 0.492f, artVariant: WoodArtVariant.Horizontal), // sprite 'Plank_Horizontal'
                B(BlockType.Haybale, 3.403f, -5.553f, 0.977f, 0.977f, passThrough: true, hp: 10f, mass: 3f), // sprite 'Haybail'
            },
            robots: new[]
            {
                R(4.92f, -4.88f, 6.297f, 6.902f), // sprite 'Robot_Pawn'
                R(6.51f, -4.88f, 6.297f, 6.902f), // sprite 'Robot_Pawn'
                R(7.24f, 0.312f, 9.373f, 7.898f, RobotType.Harvester), // sprite 'HarvesterRobot'
                R(6.02f, -0.41f, 9.373f, 7.898f, RobotType.Harvester), // sprite 'HarvesterRobot'
                R(6.42f, -3.42f, 9.373f, 7.898f, RobotType.Harvester), // sprite 'HarvesterRobot'
                R(5.19f, -1.72f, 4.532f, 5.823f, RobotType.SemiHarvester), // sprite 'Robot_SemiHarvest'
                R(4.49f, -2.38f, 4.532f, 5.823f, RobotType.SemiHarvester), // sprite 'Robot_SemiHarvest'
                R(3.75f, -3.08f, 4.532f, 5.823f, RobotType.SemiHarvester), // sprite 'Robot_SemiHarvest'
            });

        // ── W1_L18  ────────────────────────────────────────────────────────────
        // Redesigned from scratch 2026-07-19, part of the full 18-level redesign against the new
        // 3-layer parallax backdrop (see the 2026-07-18 Current Status entries) — replaces the
        // previous 2026-07-14 layout entirely, pasted verbatim from unity/Logs/level_layout_dump.txt
        // (FarmFury -> Debug -> Dump Level Layout To Log). id/name/par/birds[] unchanged. Still a
        // Commander-solo boss fight — fully destructible staircase (Stone_Square/RuinedStoneWall/
        // Barrel_Dynamite/Wood_Skew/Plank_2DShork), no indestructible structure, Commander is the
        // only robot.
        // FLAG, not silently fixed: the dump captured the Commander at scale (1f, 1f) — every prior
        // dump/hand-placement of this robot (and every other robot type in this file) used a scale
        // well above 5 (the previous L18 Commander was 6.985x7.555; SemiHarvester/Harvester
        // elsewhere in this redesign pass run 4.5-11.9). A 1x1 Commander would render far smaller
        // than any other robot/boss in the game and likely wrong relative to the rescaled staircase
        // around it — this reads as the Commander not having been scaled up in the Scene view
        // before this dump was taken, not a deliberate design choice. Pasted as dumped rather than
        // guessing a replacement value; worth confirming with a fresh dump once the Commander is
        // properly scaled in-Editor.
        Make(folder, "L18_CaptainsLastStand",
            id: "W1_L18", name: "Captain's Last Stand", par: 3,
            birds: new[] { AnimalType.Cluck, AnimalType.Cluck, AnimalType.Bessie },
            blocks: new[]
            {
                B(BlockType.Stone, 5.24f, -3.79f, 1.932f, 1.454f, artVariant: WoodArtVariant.Auto), // sprite 'RuinedStoneWall'
                B(BlockType.Stone, 7.11f, -3.76f, 1.762f, 1.471f, artVariant: WoodArtVariant.Auto), // sprite 'RuinedStoneWall'
                B(BlockType.Stone, 4.83f, -5.54f, 1f, 1f, artVariant: WoodArtVariant.Square), // sprite 'Stone_Square'
                B(BlockType.Stone, 6.08f, -5.59f, 1f, 1f, artVariant: WoodArtVariant.Square), // sprite 'Stone_Square'
                B(BlockType.Stone, 7.45f, -5.64f, 1f, 1f, artVariant: WoodArtVariant.Square), // sprite 'Stone_Square'
                B(BlockType.Stone, 7.47f, -4.46f, 1f, 1f, artVariant: WoodArtVariant.Square), // sprite 'Stone_Square'
                B(BlockType.Stone, 4.84f, -4.39f, 1f, 1f, artVariant: WoodArtVariant.Square), // sprite 'Stone_Square'
                B(BlockType.Stone, 6.15f, -4.39f, 1f, 1f, artVariant: WoodArtVariant.Square), // sprite 'Stone_Square'
                B(BlockType.Wood, 4.709f, -4.974f, 1f, 1.064f, artVariant: WoodArtVariant.Vertical), // sprite 'Plank_2DShork'
                B(BlockType.Wood, 5.14f, -4.974f, 1f, 1.064f, artVariant: WoodArtVariant.Vertical), // sprite 'Plank_2DShork'
                B(BlockType.Wood, 5.98f, -5.01f, 1f, 1.064f, artVariant: WoodArtVariant.Vertical), // sprite 'Plank_2DShork'
                B(BlockType.Wood, 6.34f, -4.94f, 1f, 1.064f, artVariant: WoodArtVariant.Vertical), // sprite 'Plank_2DShork'
                B(BlockType.Wood, 7.3f, -5.03f, 1f, 1.064f, artVariant: WoodArtVariant.Vertical), // sprite 'Plank_2DShork'
                B(BlockType.Wood, 7.68f, -5.01f, 1f, 1.064f, artVariant: WoodArtVariant.Vertical), // sprite 'Plank_2DShork'
                B(BlockType.Barrel, 5.51f, -5.44f, 0.977f, 0.977f), // sprite 'Barrel_Dynamite'
                B(BlockType.Barrel, 5.54f, -4.77f, 0.977f, 0.977f), // sprite 'Barrel_Dynamite'
                B(BlockType.Barrel, 6.89f, -4.86f, 0.977f, 0.977f), // sprite 'Barrel_Dynamite'
                B(BlockType.Barrel, 6.84f, -5.49f, 0.977f, 0.977f), // sprite 'Barrel_Dynamite'
                B(BlockType.Stone, 4.73f, -3.27f, 0.48f, 1f, artVariant: WoodArtVariant.Vertical), // sprite 'Stone_Vertical'
                B(BlockType.Stone, 7.56f, -3.2f, 0.48f, 1f, artVariant: WoodArtVariant.Vertical), // sprite 'Stone_Vertical'
                B(BlockType.Wood, 4.37f, -2.97f, 1f, 1f, artVariant: WoodArtVariant.Skew), // sprite 'Plank_Skew'
                B(BlockType.Wood, 4.93f, -2.36f, 1f, 1f, artVariant: WoodArtVariant.Skew), // sprite 'Plank_Skew'
                B(BlockType.Wood, 5.48f, -1.75f, 1f, 1f, artVariant: WoodArtVariant.Skew), // sprite 'Plank_Skew'
                B(BlockType.Wood, 7.51f, -2.67f, 1f, 0.492f, artVariant: WoodArtVariant.Horizontal), // sprite 'Plank_Horizontal'
                B(BlockType.Wood, 6.16f, -3.88f, 1f, 0.492f, artVariant: WoodArtVariant.Horizontal), // sprite 'Plank_Horizontal'
                B(BlockType.Wood, 6.14f, -1.46f, 1f, 0.492f, artVariant: WoodArtVariant.Horizontal), // sprite 'Plank_Horizontal'
                B(BlockType.Wood, 7.03f, -1.46f, 1f, 0.492f, artVariant: WoodArtVariant.Horizontal), // sprite 'Plank_Horizontal'
                B(BlockType.Haybale, 7.51f, -2.28f, 0.977f, 0.977f, passThrough: true, hp: 10f, mass: 3f), // sprite 'Haybail'
                B(BlockType.Haybale, 7.51f, -1.7f, 0.977f, 0.977f, passThrough: true, hp: 10f, mass: 3f), // sprite 'Haybail'
            },
            robots: new[]
            {
                R(6.17f, -2.94f, 1f, 1f, RobotType.Commander), // sprite 'Commander' — scale as dumped, see FLAG comment above
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
        LevelData.RobotSpawnData[] robots,
        int world = 1)
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
        asset.world       = world;

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
