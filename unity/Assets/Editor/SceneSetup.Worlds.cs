// FarmFury - Worlds 3-9 wiring (2026-11-02): Stampede backdrops, robots and props; plus the app icon.
// Art is copied in by tools/import_stampede_worlds.py; levels come from WorldLevelGenerator.
// Called from SceneSetup.WireAll (Wire Scene References).

using UnityEditor;
using UnityEngine;

public static partial class SceneSetup
{
    const int FirstStampedeWorld = 3;
    const int LastWorld          = 9;

    static readonly string[] RobotTiers = { "Basic", "Harvester", "SemiHarvester", "Boss" };

    // Same per-tier health as World 1 (Robot 35, HarvesterRobot 28, SemiHarvesterRobot 26); bosses copy
    // CommanderRobot's boss tuning (90 HP, 0.61 direct-hit / 0.1 structural damage resistance).
    static float TierHealth(string tier) => tier switch
    {
        "Harvester"     => 28f,
        "SemiHarvester" => 26f,
        "Boss"          => 90f,
        _               => 35f,
    };

    static string WorldRobotPrefabName(int world, string tier) => $"W{world}_{tier}Robot";

    // Structures = big background pieces, clutter = small ground dressing, sky = floating props.
    // Filenames in Assets/Sprites/Environment/World<N>Props (see tools/import_stampede_worlds.py).
    static readonly (int world, string[] structures, string[] clutter, string[] sky)[] WorldPropSets =
    {
        (3, new[] { "Cottage", "WaterWheel", "WeepingWillow", "OldWell", "StoneArch", "StoneBridge" },
            new[] { "FlowerBasket", "Millstone", "MossyLog", "RiverReed", "WoodenCrate", "Wooddock" },
            new string[0]),
        (4, new[] { "GiantMushroom", "SkyTemple", "StonePillar", "Windvane" },
            new[] { "Cloud", "AnchorCloud" },
            new[] { "AirBalloon", "Kite", "HangingLantern" }),
        (5, new[] { "SunkenArchway", "SunkenPillar", "SunkenStatue", "Anchor", "Submarine" },
            new[] { "Kelp", "CoralFormation", "Floral", "Clam", "TreasureChest", "AirVent", "ShipsWheel" },
            new[] { "Fish_1", "Fish_2", "Fish_3", "Fish_4", "Fish_5", "Fish_6" }),
        (6, new[] { "SateliteDish", "WarningBeacon", "ContainmentPod", "EnergyShield", "SteelGirder" },
            new[] { "PlasmaConduit", "LaserEmitter", "RocketBooster", "CircuitPanel" },
            new[] { "Ship", "ControlTower" }),
        (7, new[] { "RanchHouse", "WaterTower", "Windpump", "DeadTree", "Saguaro", "SignPost" },
            new[] { "Boulder", "Tumbleweed", "WaterBarrel", "MineCart", "Wagon", "Crate" },
            new string[0]),
        (8, new[] { "BigTop", "FerrisWheel", "Carousel", "HelterSkelter", "GameStall", "LampPost" },
            new[] { "PopcornCart", "Pumpkin", "PrizeCrates", "CandyApple" },
            new string[0]),
        (9, new[] { "GrainSilo", "ProcessingPlant", "CogMachine", "Hopper", "RobotArm", "SignalPost" },
            new[] { "ChemDrum", "CornCan", "CrateStack", "CropCrate", "Gearbox", "Forklift", "SteelCrate" },
            new string[0]),
    };

    // ── Robots ────────────────────────────────────────────────────────────────

    static void EnsureWorldRobotPrefabs()
    {
        int made = 0;
        for (int world = FirstStampedeWorld; world <= LastWorld; world++)
            foreach (var tier in RobotTiers)
                if (EnsureWorldRobotPrefab(world, tier)) made++;
        Debug.Log($"[FarmFury] World 3-9 robots: {made} prefabs created/updated.");
    }

    static bool EnsureWorldRobotPrefab(int world, string tier)
    {
        string folder     = $"Assets/Sprites/Enemies/Robot/W{world}";
        string leftPath   = $"{folder}/{tier}_left.png";
        string rightPath  = $"{folder}/{tier}_right.png";
        string name       = WorldRobotPrefabName(world, tier);
        string prefabPath = $"Assets/Prefabs/Enemies/{name}.prefab";

        var sprite = LoadRobotSprite(leftPath);
        if (sprite == null)
        {
            Debug.LogWarning($"[FarmFury] {name}: {leftPath} missing — run tools/import_stampede_worlds.py.");
            return false;
        }

        bool exists = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null;
        GameObject go;
        if (exists)
        {
            go = PrefabUtility.LoadPrefabContents(prefabPath);
        }
        else
        {
            go       = new GameObject(name);
            go.layer = 9;
            var rb   = go.AddComponent<Rigidbody2D>();
            rb.mass  = 20f;
            var bc   = go.AddComponent<BoxCollider2D>();
            bc.size  = new Vector2(1f, 1f);
            go.AddComponent<SpriteRenderer>();
            go.AddComponent<RobotEnemy>();
        }

        var robot = go.GetComponent<RobotEnemy>();
        if (robot != null)
        {
            var so = new SerializedObject(robot);
            so.FindProperty("_robotSprite").objectReferenceValue = sprite;
            WireRobotFacingRight(so, rightPath);
            so.FindProperty("_maxHealth").floatValue          = TierHealth(tier);
            so.FindProperty("_robotContactDamage").floatValue = 18f;
            bool boss = tier == "Boss";
            so.FindProperty("_damageResistance").floatValue           = boss ? 0.61f : 1f;
            so.FindProperty("_structuralDamageResistance").floatValue = boss ? 0.1f  : 1f;
            WireRobotDeathFx(so);
            so.ApplyModifiedProperties();
        }
        var sr = go.GetComponent<SpriteRenderer>();
        if (sr != null) sr.sprite = sprite;

        PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
        if (exists) PrefabUtility.UnloadPrefabContents(go);
        else        Object.DestroyImmediate(go);
        return true;
    }

    static Sprite LoadRobotSprite(string path)
    {
        var imp = AssetImporter.GetAtPath(path) as TextureImporter;
        if (imp == null) return null;
        bool dirty = false;
        if (imp.textureType         != TextureImporterType.Sprite) { imp.textureType         = TextureImporterType.Sprite; dirty = true; }
        if (imp.spritePixelsPerUnit != 1746)                        { imp.spritePixelsPerUnit = 1746;                       dirty = true; }
        if (!imp.alphaIsTransparency)                               { imp.alphaIsTransparency = true;                       dirty = true; }
        if (imp.spriteImportMode    != SpriteImportMode.Single)    { imp.spriteImportMode    = SpriteImportMode.Single;    dirty = true; }
        if (dirty) imp.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    static void WireWorldRobotSets(SerializedObject levelLoader)
    {
        var sets = levelLoader.FindProperty("_worldRobots");
        if (sets == null) return;
        if (sets.arraySize < LastWorld + 1) sets.arraySize = LastWorld + 1;
        for (int world = FirstStampedeWorld; world <= LastWorld; world++)
        {
            var set = sets.GetArrayElementAtIndex(world);
            set.FindPropertyRelative("basic").objectReferenceValue         = LoadRobotPrefab(world, "Basic");
            set.FindPropertyRelative("harvester").objectReferenceValue     = LoadRobotPrefab(world, "Harvester");
            set.FindPropertyRelative("semiHarvester").objectReferenceValue = LoadRobotPrefab(world, "SemiHarvester");
            set.FindPropertyRelative("boss").objectReferenceValue          = LoadRobotPrefab(world, "Boss");
            // World 3's framed boss levels use the Mill Stone Mauler (taunt poses + the mothership drop-in), 2026-11-10.
            if (world == 3)
            {
                var mauler = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/MillMaulerRobot.prefab");
                if (mauler != null) set.FindPropertyRelative("boss").objectReferenceValue = mauler.GetComponent<RobotEnemy>();
            }
        }
    }

    static RobotEnemy LoadRobotPrefab(int world, string tier)
    {
        var go = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Prefabs/Enemies/{WorldRobotPrefabName(world, tier)}.prefab");
        return go != null ? go.GetComponent<RobotEnemy>() : null;
    }

    // ── Backdrops ─────────────────────────────────────────────────────────────

    static void WireWorldBackdrops(SerializedObject eds)
    {
        var arr = eds.FindProperty("_worldBackdrops");
        if (arr == null) return;
        if (arr.arraySize < LastWorld + 1) arr.arraySize = LastWorld + 1;
        for (int world = FirstStampedeWorld; world <= LastWorld; world++)
        {
            // Same opaque-painting import settings as WireParallaxPainting (which only writes named fields).
            string path = $"Assets/Sprites/Environment/Skies/World{world}_Backdrop.png";
            ConfigureBackdropImport(path);
            arr.GetArrayElementAtIndex(world).objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
    }

    static void ConfigureBackdropImport(string path)
    {
        var imp = AssetImporter.GetAtPath(path) as TextureImporter;
        if (imp == null) { Debug.LogWarning($"[FarmFury] Backdrop not found at {path} — run tools/import_stampede_worlds.py."); return; }
        bool dirty = false;
        if (imp.textureType         != TextureImporterType.Sprite)             { imp.textureType         = TextureImporterType.Sprite;             dirty = true; }
        if (imp.spritePixelsPerUnit != 100)                                     { imp.spritePixelsPerUnit = 100;                                    dirty = true; }
        if (imp.spriteImportMode    != SpriteImportMode.Single)                 { imp.spriteImportMode    = SpriteImportMode.Single;                dirty = true; }
        if (imp.alphaIsTransparency)                                            { imp.alphaIsTransparency = false;                                  dirty = true; }
        if (imp.textureCompression  != TextureImporterCompression.CompressedHQ) { imp.textureCompression  = TextureImporterCompression.CompressedHQ; dirty = true; }
        if (imp.crunchedCompression)                                            { imp.crunchedCompression = false;                                  dirty = true; }
        if (imp.maxTextureSize      < 4096)                                     { imp.maxTextureSize      = 4096;                                   dirty = true; }
        if (imp.mipmapEnabled)                                                  { imp.mipmapEnabled       = false;                                  dirty = true; }
        if (dirty) imp.SaveAndReimport();
    }

    // ── App icon ──────────────────────────────────────────────────────────────
    // Sprites/AppIcon.png (1024x1024: the Farm Fury Cannonfire logo on orange sunburst rings, Kling 2026-11-10 in
    // Stampede's icon style; tools/brand_cannonfire.py) as the default icon for every platform. A platform-specific icon set in Player Settings wins.
    static void EnsureAppIcon()
    {
        const string path = "Assets/Sprites/AppIcon.png";
        var imp = AssetImporter.GetAtPath(path) as TextureImporter;
        if (imp == null) { Debug.LogWarning($"[FarmFury] App icon not found at {path}."); return; }
        bool dirty = false;
        if (imp.textureType        != TextureImporterType.Default)        { imp.textureType        = TextureImporterType.Default;        dirty = true; }
        if (imp.textureCompression != TextureImporterCompression.Uncompressed) { imp.textureCompression = TextureImporterCompression.Uncompressed; dirty = true; }
        if (imp.mipmapEnabled)                                             { imp.mipmapEnabled      = false;                              dirty = true; }
        if (dirty) imp.SaveAndReimport();
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (tex == null) return;
        PlayerSettings.SetIcons(UnityEditor.Build.NamedBuildTarget.Unknown, new[] { tex }, IconKind.Any);
        Debug.Log("[FarmFury] Default app icon <- " + path);
    }

    // ── Props ─────────────────────────────────────────────────────────────────

    static void WireWorldProps(SerializedObject sceneryBuilder)
    {
        var arr = sceneryBuilder.FindProperty("_worldProps");
        if (arr == null) return;
        if (arr.arraySize < LastWorld + 1) arr.arraySize = LastWorld + 1;
        int wired = 0;
        foreach (var (world, structures, clutter, sky) in WorldPropSets)
        {
            var set = arr.GetArrayElementAtIndex(world);
            wired += WirePropList(set.FindPropertyRelative("structures"), world, structures);
            wired += WirePropList(set.FindPropertyRelative("clutter"),    world, clutter);
            wired += WirePropList(set.FindPropertyRelative("sky"),        world, sky);
        }
        Debug.Log($"[FarmFury] Scenery: wired {wired} World 3-9 props.");
    }

    static int WirePropList(SerializedProperty list, int world, string[] names)
    {
        list.arraySize = 0;
        foreach (var name in names)
        {
            string path = $"Assets/Sprites/Environment/World{world}Props/{name}.png";
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp == null) { Debug.LogWarning($"[FarmFury] Prop not found at {path} — run tools/import_stampede_worlds.py."); continue; }
            bool dirty = false;
            if (imp.textureType         != TextureImporterType.Sprite) { imp.textureType         = TextureImporterType.Sprite; dirty = true; }
            if (imp.spritePixelsPerUnit != 512)                         { imp.spritePixelsPerUnit = 512;                        dirty = true; }
            if (!imp.alphaIsTransparency)                               { imp.alphaIsTransparency = true;                       dirty = true; }
            if (imp.spriteImportMode    != SpriteImportMode.Single)    { imp.spriteImportMode    = SpriteImportMode.Single;    dirty = true; }
            if (dirty) imp.SaveAndReimport();
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null) continue;
            list.arraySize++;
            list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = sprite;
        }
        return list.arraySize;
    }
}
