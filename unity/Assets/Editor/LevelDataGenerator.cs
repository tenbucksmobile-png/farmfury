// FarmFury — Editor utility. Run via menu: FarmFury ▶ Generate All Level Data
// Coordinate system: ground surface at Y = -2.5 (world). Launcher at X = -5.5.
// Block/robot positions are world-space: X as given, Y = surface_offset - 2.5

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
        // Hay bale positions converted from Canva 1275×720 canvas:
        //   Formula: X = (CanvaX + W/2 - 637.5) / 100
        //             Y = (360 - CanvaY - H/2)   / 100
        //
        // Haybail(1): CanvaX=896 Y=606 W=67 H=67 → Unity (2.295, -2.815)
        // Haybail(2): CanvaX=870 Y=643 W=67 H=67 → Unity (2.025, -3.165)
        // Haybail(3): CanvaX=896 Y=653 W=67 H=67 → Unity (2.295, -3.265)
        // Haybail(4): CanvaX=915 Y=653 W=67 H=67 → Unity (2.450, -3.265)
        //
        // HarvesterRobot: CanvaX=973 Y=585 W=177 H=118 → Unity (4.24, -2.64)
        // Note: spawns as the generic Robot prefab until a HarvesterRobot prefab is added.
        //
        // Par=1: destroying the robot with 1 bird = 3 stars.
        Make(folder, "L01_FirstContact",
            id: "W1_L01", name: "First Contact", par: 1,
            birds: new[] { AnimalType.Cluck, AnimalType.Cluck, AnimalType.Cluck },
            blocks: new[]
            {
                // Hay bales — passThrough=true so Cluck continues at 70% velocity after impact.
                // Health 60 (weaker than standard wood 80), mass 3 (lighter than standard 5).
                B(BlockType.Wood, 2.295f, -2.815f, 0.67f, 0.67f, passThrough: true, hp: 60f, mass: 3f),
                B(BlockType.Wood, 2.025f, -3.165f, 0.67f, 0.67f, passThrough: true, hp: 60f, mass: 3f),
                B(BlockType.Wood, 2.295f, -3.265f, 0.67f, 0.67f, passThrough: true, hp: 60f, mass: 3f),
                B(BlockType.Wood, 2.450f, -3.265f, 0.67f, 0.67f, passThrough: true, hp: 60f, mass: 3f),
            },
            robots: new[]
            {
                R(4.24f, -2.64f), // HarvesterRobot sheltering behind the hay bales
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

    static LevelData.RobotSpawnData R(float x, float y) =>
        new() { position = new Vector2(x, y) };
}
