// FarmFury — Editor utility. Run via menu: FarmFury ▶ Generate All Level Data
// Coordinate system (current, post-rebuild): ground surface Y = -6.60, launcher X = -2.327.
// Block/robot positions are raw world-space — LevelLoader applies no offset at spawn time.
// L01 uses this current system (see its Make() call below). L02-L06 below still use the
// OLD pre-rebuild system (ground Y=-2.5, launcher X=-5.5) and are NOT yet migrated — they
// spawn floating above the current ground. See CLAUDE.md Audit Findings before touching them.

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
        // HarvesterRobot position (5.6, -5.25) and scale (5.7065, 7.009) — fixed 2026-07-09,
        // USER-PROVIDED exact values from their own Editor after three rounds of Claude's own
        // position/scale calculations all came back "still wrong" (X=5.15509 too far right/edge
        // of frame; Y=-6.25 clipped at the bottom; Y=-5.8215 still too low; halving the scale to
        // "fix" framing was the wrong instinct — the user wanted it bigger, not smaller). Lesson:
        // once numeric recalculation has failed twice, ask for the measured value instead of
        // computing a third guess. Do not re-derive this from camera/sprite-padding math again —
        // treat it as ground truth unless the user reports it's wrong.
        // BoxCollider2D is re-derived in LevelLoader.SpawnRobot() to stay pinned to the default
        // 0.6×0.9 world-space hitbox regardless of visual scale (fixed 2026-07-01 — previously
        // the collider inherited the full scale, deeply overlapping the ground at spawn and
        // getting launched into the air by physics separation) — this formula is scale-agnostic,
        // so this scale change needs no corresponding code change.
        //
        // Hay pile: positions match the 4 hand-placed decorative "Haybail"/"Haybail (1-3)"
        // scene GameObjects exactly (read from Game.unity), so this replaces them rather
        // than duplicating them — delete the 4 decorative scene GOs once this is live, since
        // these gameplay blocks render the same Haybail.png art at the same spots. hp=10
        // (fixed 2026-07-01, was 60 — that survived multiple hits instead of exploding in one;
        // a typical Cluck impact does ~15-20 impulse damage, so 10 reliably one-shots it).
        // passThrough=true lets Cluck punch through at 70% speed and continue to the robot.
        Make(folder, "L01_FirstContact",
            id: "W1_L01", name: "First Contact", par: 2,
            birds: new[] { AnimalType.Cluck, AnimalType.Cluck, AnimalType.Cluck },
            blocks: new[]
            {
                B(BlockType.Haybale, 3.6462f, -5.553f, 1.0f, 0.9f, passThrough: true, hp: 10f, mass: 3f),
                B(BlockType.Haybale, 4.3098f, -5.608f, 1.0f, 0.9f, passThrough: true, hp: 10f, mass: 3f),
                B(BlockType.Haybale, 3.85f,   -5.654f, 1.0f, 0.9f, passThrough: true, hp: 10f, mass: 3f),
                B(BlockType.Haybale, 4.029f,  -4.876f, 1.0f, 0.9f, passThrough: true, hp: 10f, mass: 3f),
            },
            robots: new[]
            {
                R(5.6f, -5.25f, 5.7065f, 7.009f, RobotType.Harvester), // sheltering behind the hay
            });

        // ── W1_L02  Stone Wall ────────────────────────────────────────────────
        Make(folder, "L02_StoneWall",
            id: "W1_L02", name: "Stone Wall", par: 2,
            birds: new[] { AnimalType.Cluck, AnimalType.Cluck, AnimalType.Cluck },
            blocks: new[]
            {
                B(BlockType.Wood, -3.9f, -2.2f, 1.2f, 0.6f),  // wooden palisade wall
                B(BlockType.Wood, -2.7f, -2.2f, 1.2f, 0.6f),
                B(BlockType.Wood, -1.5f, -2.2f, 1.2f, 0.6f),
                B(BlockType.Wood, -2.7f, -1.6f, 1.2f, 0.4f),  // platform on top
            },
            robots: new[] { R(-0.3f, -2.1f) });

        // ── W1_L03  The Tower ─────────────────────────────────────────────────
        Make(folder, "L03_TheTower",
            id: "W1_L03", name: "The Tower", par: 3,
            birds: new[] { AnimalType.Cluck, AnimalType.Bessie, AnimalType.Cluck, AnimalType.Cluck },
            blocks: new[]
            {
                B(BlockType.Wood, -2.7f, -2.3f, 2.4f, 0.4f),  // wide wood base
                B(BlockType.Wood, -2.7f, -1.9f, 2.4f, 0.4f),  // wide wood mid
                B(BlockType.Wood, -2.7f, -1.5f, 1.6f, 0.4f),  // narrower top
            },
            robots: new[] { R(-1.1f, -2.1f), R(-2.7f, -0.9f) });

        // ── W1_L04  Egg Practice ──────────────────────────────────────────────
        Make(folder, "L04_EggPractice",
            id: "W1_L04", name: "Egg Practice", par: 2,
            birds: new[] { AnimalType.Cluck, AnimalType.Cluck, AnimalType.Cluck, AnimalType.Cluck },
            blocks: new[]
            {
                B(BlockType.Wood, -3.9f, -2.3f, 1.2f, 0.4f),  // 3-high wood wall
                B(BlockType.Wood, -3.9f, -1.9f, 1.2f, 0.4f),
                B(BlockType.Wood, -3.9f, -1.5f, 1.2f, 0.4f),
            },
            robots: new[] { R(-2.7f, -2.1f), R(-2.0f, -2.1f), R(-1.3f, -2.1f), R(-0.6f, -2.1f) });

        // ── W1_L05  The Fortress ──────────────────────────────────────────────
        Make(folder, "L05_TheFortress",
            id: "W1_L05", name: "The Fortress", par: 3,
            birds: new[]
            {
                AnimalType.Cluck, AnimalType.Cluck, AnimalType.Cluck,
                AnimalType.Cluck, AnimalType.Cluck,
            },
            blocks: new[]
            {
                B(BlockType.Stone, -4.7f, -1.7f, 0.4f, 1.6f), // left stone pillar — 3 segments
                B(BlockType.Stone, -4.7f, -0.1f, 0.4f, 1.6f),
                B(BlockType.Stone, -4.7f,  1.5f, 0.4f, 1.6f),
                B(BlockType.Wood,  -3.3f, -2.1f, 0.8f, 0.8f), // 3×3 wood grid
                B(BlockType.Wood,  -2.5f, -2.1f, 0.8f, 0.8f),
                B(BlockType.Wood,  -1.7f, -2.1f, 0.8f, 0.8f),
                B(BlockType.Wood,  -3.3f, -1.3f, 0.8f, 0.8f),
                B(BlockType.Wood,  -2.5f, -1.3f, 0.8f, 0.8f),
                B(BlockType.Wood,  -1.7f, -1.3f, 0.8f, 0.8f),
                B(BlockType.Wood,  -3.3f, -0.5f, 0.8f, 0.8f),
                B(BlockType.Wood,  -2.5f, -0.5f, 0.8f, 0.8f),
                B(BlockType.Wood,  -1.7f, -0.5f, 0.8f, 0.8f),
            },
            robots: new[] { R(-0.7f, -2.1f), R(0.0f, -2.1f), R(0.7f, -2.1f) });

        // ── W1_L06  Bessie's Debut ────────────────────────────────────────────
        Make(folder, "L06_BessiesDebut",
            id: "W1_L06", name: "Bessie's Debut", par: 2,
            birds: new[] { AnimalType.Cluck, AnimalType.Bessie, AnimalType.Bessie, AnimalType.Cluck },
            blocks: new[]
            {
                B(BlockType.Stone, -3.3f, -1.7f, 0.4f, 1.6f), // left flanking pillar
                B(BlockType.Stone, -3.3f, -0.1f, 0.4f, 1.6f),
                B(BlockType.Stone, -0.1f, -1.7f, 0.4f, 1.6f), // right flanking pillar
                B(BlockType.Stone, -0.1f, -0.1f, 0.4f, 1.6f),
            },
            robots: new[] { R(-2.3f, -2.1f), R(-1.6f, -2.1f), R(-0.9f, -2.1f) });

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
