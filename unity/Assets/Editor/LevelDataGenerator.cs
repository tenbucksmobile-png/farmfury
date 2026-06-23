// FarmFury — Editor utility. Run via menu: FarmFury ▶ Generate All Level Data
// Converts Phaser prototype coordinates using: x = phaser_x/50, y = -(phaser_y-770)/50
// Ground surface sits at y=0 in Unity world space.

using UnityEngine;
using UnityEditor;

public static class LevelDataGenerator
{
    [MenuItem("FarmFury/Generate All Level Data")]
    public static void GenerateAll()
    {
        const string folder = "Assets/ScriptableObjects/Levels";
        EnsureFolder("Assets/ScriptableObjects", "Levels");

        // ── W1_L01  First Contact ─────────────────────────────────────────────
        Make(folder, "L01_FirstContact",
            id: "W1_L01", name: "First Contact", par: 1,
            birds: new[] { AnimalType.Cluck, AnimalType.Cluck, AnimalType.Cluck },
            blocks: new[]
            {
                B(BlockType.Wood,  13.6f, 0.2f, 1.2f, 0.4f),   // 4 wood stacked
                B(BlockType.Wood,  13.6f, 0.6f, 1.2f, 0.4f),
                B(BlockType.Wood,  13.6f, 1.0f, 1.2f, 0.4f),
                B(BlockType.Wood,  13.6f, 1.4f, 1.2f, 0.4f),
            },
            robots: new[] { R(14.8f, 0.4f) });

        // ── W1_L02  Stone Wall ────────────────────────────────────────────────
        Make(folder, "L02_StoneWall",
            id: "W1_L02", name: "Stone Wall", par: 2,
            birds: new[] { AnimalType.Cluck, AnimalType.Cluck, AnimalType.Cluck },
            blocks: new[]
            {
                B(BlockType.Stone, 12.8f, 0.3f, 1.2f, 0.6f),   // 3 stone wall
                B(BlockType.Stone, 14.0f, 0.3f, 1.2f, 0.6f),
                B(BlockType.Stone, 15.2f, 0.3f, 1.2f, 0.6f),
                B(BlockType.Wood,  14.0f, 0.8f, 1.2f, 0.4f),   // wood on centre stone
            },
            robots: new[] { R(16.4f, 0.4f) });

        // ── W1_L03  The Tower ─────────────────────────────────────────────────
        Make(folder, "L03_TheTower",
            id: "W1_L03", name: "The Tower", par: 3,
            birds: new[] { AnimalType.Cluck, AnimalType.Bessie, AnimalType.Cluck, AnimalType.Cluck },
            blocks: new[]
            {
                B(BlockType.Wood,  14.0f, 0.2f, 2.4f, 0.4f),   // wide wood base
                B(BlockType.Stone, 14.0f, 0.6f, 2.4f, 0.4f),   // wide stone mid
                B(BlockType.Wood,  14.0f, 1.0f, 1.6f, 0.4f),   // narrower wood top
            },
            robots: new[] { R(15.6f, 0.4f), R(14.0f, 1.6f) }); // ground + top

        // ── W1_L04  Egg Practice ──────────────────────────────────────────────
        Make(folder, "L04_EggPractice",
            id: "W1_L04", name: "Egg Practice", par: 2,
            birds: new[] { AnimalType.Cluck, AnimalType.Cluck, AnimalType.Cluck, AnimalType.Cluck },
            blocks: new[]
            {
                B(BlockType.Wood, 12.8f, 0.2f, 1.2f, 0.4f),    // 3-high wood wall
                B(BlockType.Wood, 12.8f, 0.6f, 1.2f, 0.4f),
                B(BlockType.Wood, 12.8f, 1.0f, 1.2f, 0.4f),
            },
            robots: new[] { R(14.0f, 0.4f), R(14.7f, 0.4f), R(15.4f, 0.4f), R(16.1f, 0.4f) });

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
                // Left stone pillar (3 stacked 20×80 segments)
                B(BlockType.Stone, 12.0f, 0.8f, 0.4f, 1.6f),
                B(BlockType.Stone, 12.0f, 2.4f, 0.4f, 1.6f),
                B(BlockType.Stone, 12.0f, 4.0f, 0.4f, 1.6f),
                // 3×3 wood grid inside
                B(BlockType.Wood, 13.4f, 0.4f, 0.8f, 0.8f),
                B(BlockType.Wood, 14.2f, 0.4f, 0.8f, 0.8f),
                B(BlockType.Wood, 15.0f, 0.4f, 0.8f, 0.8f),
                B(BlockType.Wood, 13.4f, 1.2f, 0.8f, 0.8f),
                B(BlockType.Wood, 14.2f, 1.2f, 0.8f, 0.8f),
                B(BlockType.Wood, 15.0f, 1.2f, 0.8f, 0.8f),
                B(BlockType.Wood, 13.4f, 2.0f, 0.8f, 0.8f),
                B(BlockType.Wood, 14.2f, 2.0f, 0.8f, 0.8f),
                B(BlockType.Wood, 15.0f, 2.0f, 0.8f, 0.8f),
            },
            robots: new[] { R(16.0f, 0.4f), R(16.7f, 0.4f), R(17.4f, 0.4f) });

        // ── W1_L06  Bessie's Debut ────────────────────────────────────────────
        Make(folder, "L06_BessiesDebut",
            id: "W1_L06", name: "Bessie's Debut", par: 2,
            birds: new[] { AnimalType.Cluck, AnimalType.Bessie, AnimalType.Bessie, AnimalType.Cluck },
            blocks: new[]
            {
                // Left flanking pillar (2 stacked 20×80)
                B(BlockType.Stone, 13.4f, 0.8f, 0.4f, 1.6f),
                B(BlockType.Stone, 13.4f, 2.4f, 0.4f, 1.6f),
                // Right flanking pillar
                B(BlockType.Stone, 16.6f, 0.8f, 0.4f, 1.6f),
                B(BlockType.Stone, 16.6f, 2.4f, 0.4f, 1.6f),
            },
            robots: new[] { R(14.4f, 0.4f), R(15.1f, 0.4f), R(15.8f, 0.4f) });

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
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

    static LevelData.BlockSpawnData B(BlockType type, float x, float y, float w, float h) =>
        new() { type = type, position = new Vector2(x, y), size = new Vector2(w, h) };

    static LevelData.RobotSpawnData R(float x, float y) =>
        new() { position = new Vector2(x, y) };
}
