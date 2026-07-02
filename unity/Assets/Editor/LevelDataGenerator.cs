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
        // Hay pile: base 3 positions match the hand-placed decorative "Haybail"/"Haybail (1-3)"
        // scene GameObjects (read from Game.unity) — delete those 4 decorative scene GOs once
        // this is live, since these gameplay blocks render the same Haybail.png art at the same
        // spots. The 4th (top) bale's Y: 2026-07-13 moved it from -4.876 to -4.7 assuming the
        // block's *nominal* size (1.0x0.9) equalled its rendered size — wrong, and it made
        // "too high" worse, not better. Haybail.png is imported at PPU=512 (the generic
        // World1Props rule — it isn't in SpriteAutoImporter's isBlockSprite/isNewScenery lists
        // that give 1-unit-native sizing), so at scale (1.0,0.9) the actual rendered content
        // (measured via PIL bbox: 500x500 canvas, trimmed content 500x419px) is only ~0.7365
        // units tall, not 0.9. Recomputed 2026-07-14 from the real pixel content (including the
        // ~0.04u pivot-offset from asymmetric top/bottom padding) so the top bale's *visible*
        // art rests on the base row's *visible* art: Y=-4.87 — landing almost exactly back on
        // the original hand-placed -4.876, confirming that value (sourced from the user's own
        // scene placement) was already correct and the 2026-07-13 "fix" was the actual bug. hp=10
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
                B(BlockType.Haybale, 4.029f,  -4.87f,  1.0f, 0.9f, passThrough: true, hp: 10f, mass: 3f),
            },
            robots: new[]
            {
                R(5.7f, -5.36f, 5.7065f, 7.009f, RobotType.Harvester), // sheltering behind the hay
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
