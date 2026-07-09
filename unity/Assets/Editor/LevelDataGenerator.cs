// FarmFury — Editor utility. Run via menu: FarmFury ▶ Generate All Level Data
// Coordinate system (current, post-rebuild): ground surface Y = -6.60, launcher X = -2.327.
// Block/robot positions are raw world-space — LevelLoader applies no offset at spawn time.
// All 6 levels now use this current system. L02-L06 were migrated from the OLD pre-rebuild
// system (ground Y=-2.5, launcher X=-5.5) via the same uniform rigid-translation delta:
// dx=+3.173, dy=-4.10 (new = old + delta) — L02-L04 on 2026-07-27, L05-L06 on 2026-07-09.
// A pure delta preserves every relative relationship the original design had (bottom rows
// still rest exactly on the ground line, stacked gaps unchanged, the L03 robot still stands
// on its tower) without needing to hand-retune anything.

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
        Make(folder, "L01_FirstContact",
            id: "W1_L01", name: "First Contact", par: 2,
            birds: new[] { AnimalType.Cluck, AnimalType.Cluck, AnimalType.Cluck },
            blocks: new[]
            {
                B(BlockType.Haybale, 3.6462f, -5.180f, 1.0f, 0.9f, passThrough: true, hp: 10f, mass: 3f),
                B(BlockType.Haybale, 4.3098f, -5.235f, 1.0f, 0.9f, passThrough: true, hp: 10f, mass: 3f),
                B(BlockType.Haybale, 3.85f,   -5.281f, 1.0f, 0.9f, passThrough: true, hp: 10f, mass: 3f),
                // Top ("cap") bale — lowered 0.35u and recentred over the base row's mean X
                // (2026-07-06, user-reported "still too high and off the mark"). Was sitting a
                // full bale-height above a single base bale (a valid but precarious "balanced on
                // one point" stack); now nests down into the row for a squatter, more obviously-
                // connected pile. Re-check against a fresh screenshot after this change.
                B(BlockType.Haybale, 3.935f,  -4.85f, 1.0f, 0.9f, passThrough: true, hp: 10f, mass: 3f),
            },
            robots: new[]
            {
                R(5.7f, -5.36f, 5.7065f, 7.009f, RobotType.Harvester), // sheltering behind the hay
            });

        // ── W1_L02  Stone Wall ────────────────────────────────────────────────
        // Migrated 2026-07-27 (dx=+3.173, dy=-4.10 from the old ground/launcher — see header
        // comment above). Original design unchanged: wooden palisade + platform, 1 robot behind it.
        Make(folder, "L02_StoneWall",
            id: "W1_L02", name: "Stone Wall", par: 2,
            birds: new[] { AnimalType.Cluck, AnimalType.Cluck, AnimalType.Cluck },
            blocks: new[]
            {
                B(BlockType.Wood, -0.727f, -6.30f, 1.2f, 0.6f),  // wooden palisade wall
                B(BlockType.Wood,  0.473f, -6.30f, 1.2f, 0.6f),
                B(BlockType.Wood,  1.673f, -6.30f, 1.2f, 0.6f),
                B(BlockType.Wood,  0.473f, -5.70f, 1.2f, 0.4f),  // platform on top
            },
            robots: new[] { R(2.873f, -6.20f) });

        // ── W1_L03  The Tower ─────────────────────────────────────────────────
        // Migrated 2026-07-27 (same delta as L02). Original design unchanged: 3-tier wood
        // tower, one robot on the ground and one perched on top of the tower.
        Make(folder, "L03_TheTower",
            id: "W1_L03", name: "The Tower", par: 3,
            birds: new[] { AnimalType.Cluck, AnimalType.Bessie, AnimalType.Cluck, AnimalType.Cluck },
            blocks: new[]
            {
                B(BlockType.Wood, 0.473f, -6.40f, 2.4f, 0.4f),  // wide wood base
                B(BlockType.Wood, 0.473f, -6.00f, 2.4f, 0.4f),  // wide wood mid
                B(BlockType.Wood, 0.473f, -5.60f, 1.6f, 0.4f),  // narrower top
            },
            robots: new[] { R(2.073f, -6.20f), R(0.473f, -5.00f) });

        // ── W1_L04  Egg Practice ──────────────────────────────────────────────
        // Migrated 2026-07-27 (same delta as L02/L03). Original design unchanged: 3-high wood
        // wall, 4 grouped robots behind it as a Cluck cluster-bomb showcase.
        Make(folder, "L04_EggPractice",
            id: "W1_L04", name: "Egg Practice", par: 2,
            birds: new[] { AnimalType.Cluck, AnimalType.Cluck, AnimalType.Cluck, AnimalType.Cluck },
            blocks: new[]
            {
                B(BlockType.Wood, -0.727f, -6.40f, 1.2f, 0.4f),  // 3-high wood wall
                B(BlockType.Wood, -0.727f, -6.00f, 1.2f, 0.4f),
                B(BlockType.Wood, -0.727f, -5.60f, 1.2f, 0.4f),
            },
            robots: new[] { R(0.473f, -6.20f), R(1.173f, -6.20f), R(1.873f, -6.20f), R(2.573f, -6.20f) });

        // ── W1_L05  The Fortress ──────────────────────────────────────────────
        // Migrated 2026-07-09 (same delta as L02-L04: dx=+3.173, dy=-4.10 from the old
        // ground/launcher — see header comment above). Original design unchanged: 3-segment
        // stone pillar guarding a 3×3 wood grid, 3 robots sheltering behind it.
        Make(folder, "L05_TheFortress",
            id: "W1_L05", name: "The Fortress", par: 3,
            birds: new[]
            {
                AnimalType.Cluck, AnimalType.Cluck, AnimalType.Cluck,
                AnimalType.Cluck, AnimalType.Cluck,
            },
            blocks: new[]
            {
                B(BlockType.Stone, -1.527f, -5.80f, 0.4f, 1.6f), // left stone pillar — 3 segments
                B(BlockType.Stone, -1.527f, -4.20f, 0.4f, 1.6f),
                B(BlockType.Stone, -1.527f, -2.60f, 0.4f, 1.6f),
                B(BlockType.Wood,  -0.127f, -6.20f, 0.8f, 0.8f), // 3×3 wood grid
                B(BlockType.Wood,   0.673f, -6.20f, 0.8f, 0.8f),
                B(BlockType.Wood,   1.473f, -6.20f, 0.8f, 0.8f),
                B(BlockType.Wood,  -0.127f, -5.40f, 0.8f, 0.8f),
                B(BlockType.Wood,   0.673f, -5.40f, 0.8f, 0.8f),
                B(BlockType.Wood,   1.473f, -5.40f, 0.8f, 0.8f),
                B(BlockType.Wood,  -0.127f, -4.60f, 0.8f, 0.8f),
                B(BlockType.Wood,   0.673f, -4.60f, 0.8f, 0.8f),
                B(BlockType.Wood,   1.473f, -4.60f, 0.8f, 0.8f),
            },
            robots: new[] { R(2.473f, -6.20f), R(3.173f, -6.20f), R(3.873f, -6.20f) });

        // ── W1_L06  Bessie's Debut ────────────────────────────────────────────
        // Migrated 2026-07-09 (same delta as L02-L05). Original design unchanged: 2 flanking
        // stone pillar pairs, 3 robots between them — a Bessie ground-slam showcase.
        Make(folder, "L06_BessiesDebut",
            id: "W1_L06", name: "Bessie's Debut", par: 2,
            birds: new[] { AnimalType.Cluck, AnimalType.Bessie, AnimalType.Bessie, AnimalType.Cluck },
            blocks: new[]
            {
                B(BlockType.Stone, -0.127f, -5.80f, 0.4f, 1.6f), // left flanking pillar
                B(BlockType.Stone, -0.127f, -4.20f, 0.4f, 1.6f),
                B(BlockType.Stone,  3.073f, -5.80f, 0.4f, 1.6f), // right flanking pillar
                B(BlockType.Stone,  3.073f, -4.20f, 0.4f, 1.6f),
            },
            robots: new[] { R(0.873f, -6.20f), R(1.573f, -6.20f), R(2.273f, -6.20f) });

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        if (!silent)
            EditorUtility.DisplayDialog("FarmFury",
                "Generated 6 LevelData assets in\nAssets/ScriptableObjects/Levels", "OK");
        Debug.Log("[FarmFury] Level data generation complete.");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

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

    // passThrough, hp, mass are optional overrides — 0 values mean "use BlockBase default"
    static LevelData.BlockSpawnData B(BlockType type, float x, float y, float w, float h,
                                      bool passThrough = false, float hp = 0f, float mass = 0f) =>
        new() { type = type, position = new Vector2(x, y), size = new Vector2(w, h),
                passThrough = passThrough, healthOverride = hp, massOverride = mass };

    static LevelData.RobotSpawnData R(float x, float y, float scaleX = 0f, float scaleY = 0f,
                                      RobotType robotType = RobotType.Basic) =>
        new() { position = new Vector2(x, y), scale = new Vector2(scaleX, scaleY), robotType = robotType };
}
