// FarmFury - World 1 cornfield backdrop + platform wiring (2026-11-08).
// Art is cut out by tools/import_cornfield_art.py into Assets/Sprites/Environment/Cornfield/.
// Called from SceneSetup.EnsureEnvironmentDepthSystem and WireLevelLoader (Wire Scene References).

using UnityEditor;
using UnityEngine;

public static partial class SceneSetup
{
    const string CornfieldFolder = "Assets/Sprites/Environment/Cornfield";

    static void WireCornfield(SerializedObject eds)
    {
        WireCornfieldSprite(eds, "_sprCornFar",  "CornfieldFar.png", opaque: true);
        WireCornfieldSprite(eds, "_sprCornMid",  "CornfieldMid.png", opaque: false);
        WireCornfieldSprite(eds, "_sprCornWall", "CornWall.png",     opaque: false);
        WireCornfieldSprite(eds, "_sprCornSoil", "CornSoil.png",     opaque: false);
        WireCornfieldSprite(eds, "_sprCornSilhouetteL", "CornSilhouetteL.png", opaque: false);
        WireCornfieldSprite(eds, "_sprCornSilhouetteR", "CornSilhouetteR.png", opaque: false);
        WireCornfieldSprite(eds, "_sprFlybyPlane", "FlybyPlane.png", opaque: false);
        WireCornfieldSprite(eds, "_sprFlybyDrone", "FlybyDrone.png", opaque: false);
        // Depth + weather (2026-11-09): landform layers, clouds, wind-sway material.
        WireCornfieldSprite(eds, "_sprCornRidges", "CornRidges.png", opaque: false);
        WireCornfieldSprite(eds, "_sprCornHills",  "CornHills.png",  opaque: false);
        var clouds = eds.FindProperty("_sprClouds");
        if (clouds != null)
        {
            var list = new System.Collections.Generic.List<Sprite>();
            for (int i = 0; i < 8; i++)
            {
                if (AssetImporter.GetAtPath($"{CornfieldFolder}/Cloud_{i}.png") == null) break;
                var sp = LoadCornfieldSprite($"Cloud_{i}.png", opaque: false);
                if (sp != null) list.Add(sp);
            }
            clouds.arraySize = list.Count;
            for (int i = 0; i < list.Count; i++) clouds.GetArrayElementAtIndex(i).objectReferenceValue = list[i];
            Debug.Log($"[FarmFury] Cornfield: {list.Count} clouds wired");
        }
        var sway = eds.FindProperty("_windSwayMaterial");
        if (sway != null) sway.objectReferenceValue = EnsureWindSwayMaterial();

        // World 2 Frozen Tundra framed stack (2026-11-09, tools/import_frozen_art.py).
        WireFrozenSprite(eds, "_sprFrozenRidges", "FrozenRidges.png");
        WireFrozenSprite(eds, "_sprFrozenHills",  "FrozenHills.png");
        WireFrozenSprite(eds, "_sprFrozenMid",    "FrozenMid.png");
        WireFrozenSprite(eds, "_sprFrozenWall",   "FrozenWall.png");
        WireFrozenSprite(eds, "_sprFrozenSoil",   "FrozenSoil.png");
        WireFrozenSprite(eds, "_sprFrozenSilhouetteL", "FrozenSilhouetteL.png");
        WireFrozenSprite(eds, "_sprFrozenSilhouetteR", "FrozenSilhouetteR.png");

        // World 3 Watermill Village framed stack (2026-11-10, tools/import_watermill_art.py).
        WireMillSprite(eds, "_sprMillFar",    "MillFar.png", opaque: true);
        WireMillSprite(eds, "_sprMillRidges", "MillRidges.png");
        WireMillSprite(eds, "_sprMillMid",    "MillMid.png");
        WireMillSprite(eds, "_sprMillWall",   "MillWall.png");
        WireMillSprite(eds, "_sprMillSoil",   "MillSoil.png");
        WireMillSprite(eds, "_sprMillSilhouetteL", "MillSilhouetteL.png");
        WireMillSprite(eds, "_sprMillSilhouetteR", "MillSilhouetteR.png");

        // World 4 Sky Islands framed stack (2026-11-11, tools/import_sky_art.py).
        WireSkySprite(eds, "_sprSkyFar",    "SkyFar.png", opaque: true);
        WireSkySprite(eds, "_sprSkyRidges", "SkyRidges.png");
        WireSkySprite(eds, "_sprSkyMid",    "SkyMid.png");
        WireSkySprite(eds, "_sprSkyWall",   "SkyWall.png");
        WireSkySprite(eds, "_sprSkySoil",   "SkySoil.png");
        WireSkySprite(eds, "_sprSkySilhouetteL", "SkySilhouetteL.png");
        WireSkySprite(eds, "_sprSkySilhouetteR", "SkySilhouetteR.png");
        // Sky decor (SkyDecor, 2026-11-11): Stampede Sky Island props copied by tools/import_sky_art.py.
        WireSkySprite(eds, "_sprSkyBalloon", "Decor_Balloon.png");
        WireSkySprite(eds, "_sprSkyKite",    "Decor_Kite.png");
        WireSkySprite(eds, "_sprSkyLantern", "Decor_Lantern.png");
        WireSkySprite(eds, "_sprSkyPlum",    "Decor_Plum.png");
        WireSkySprite(eds, "_sprSkyVane",    "Decor_Windvane.png");
    }

    const string SkyFolder = "Assets/Sprites/Environment/Sky";

    static void WireSkySprite(SerializedObject so, string field, string file, bool opaque = false)
    {
        var sp = LoadSpriteAt($"{SkyFolder}/{file}", opaque);
        var prop = so.FindProperty(field);
        if (sp != null && prop != null) { prop.objectReferenceValue = sp; Debug.Log($"[FarmFury] Sky: {field} -> {file}"); }
        else Debug.LogWarning($"[FarmFury] Sky: {field} not wired ({SkyFolder}/{file} missing?)");
    }

    const string MillFolder = "Assets/Sprites/Environment/Watermill";

    static void WireMillSprite(SerializedObject so, string field, string file, bool opaque = false)
    {
        var sp = LoadSpriteAt($"{MillFolder}/{file}", opaque);
        var prop = so.FindProperty(field);
        if (sp != null && prop != null) { prop.objectReferenceValue = sp; Debug.Log($"[FarmFury] Watermill: {field} -> {file}"); }
    }

    const string FrozenFolder = "Assets/Sprites/Environment/Frozen";

    static void WireFrozenSprite(SerializedObject so, string field, string file)
    {
        var sp = LoadSpriteAt($"{FrozenFolder}/{file}", opaque: false);
        var prop = so.FindProperty(field);
        if (sp != null && prop != null) { prop.objectReferenceValue = sp; Debug.Log($"[FarmFury] Frozen: {field} -> {file}"); }
    }

    // Assets/Materials/WindSway.mat (FarmFury/WindSway shader) — an asset so the shader ships in builds;
    // EnvironmentDepthSystem copies it per layer with its own sway amount.
    static Material EnsureWindSwayMaterial()
    {
        const string path = "Assets/Materials/WindSway.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat != null) return mat;
        var shader = Shader.Find("FarmFury/WindSway");
        if (shader == null) { Debug.LogWarning("[FarmFury] FarmFury/WindSway shader missing"); return null; }
        mat = new Material(shader);
        AssetDatabase.CreateAsset(mat, path);
        Debug.Log($"[FarmFury] Created {path}");
        return mat;
    }

    // Contraption art on the LevelLoader (2026-11-09): sunflower + seed, hay hoist crane + bucket.
    static void WireGadgetSprites(SerializedObject loader)
    {
        (string field, string file)[] map =
        {
            ("_sunflowerSprite", "Sunflower.png"), ("_sunflowerSeedSprite", "SunflowerSeed.png"),
            ("_hoistCraneSprite", "HoistCrane.png"), ("_hoistBucketSprite", "HoistBucket.png"),
        };
        foreach (var (field, file) in map)
        {
            var prop = loader.FindProperty(field);
            if (prop != null) prop.objectReferenceValue = LoadCornfieldSprite(file, opaque: false);
        }
        // World 2 contraptions (2026-11-09).
        (string field, string path)[] w2 =
        {
            ("_iceCrystalSprite",   "Assets/Sprites/Environment/World2Props/IceCrystals.png"),
            ("_iceShardSprite",     "Assets/Sprites/Environment/World2Props/IceSpire.png"),
            ("_frozenCraneSprite",  $"{FrozenFolder}/FrozenCrane.png"),
            ("_iceBlockLoadSprite", "Assets/Sprites/Environment/World2Props/IceBlock.png"),
            ("_iceBoulderSprite",   "Assets/Sprites/Environment/World2Props/IceBoulder.png"),
        };
        foreach (var (field, path) in w2)
        {
            var prop = loader.FindProperty(field);
            // World2Props art is also hand-placed in Scenery_World2: load it as imported (re-importing at another PPU
            // would resize those scene props). Only our own Frozen/ art gets the strip import settings.
            if (prop != null) prop.objectReferenceValue = path.StartsWith(FrozenFolder)
                ? LoadSpriteAt(path, opaque: false) : AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
        // World 3 contraptions (2026-11-10): water pump + droplet, mill hoist + millstone (also the rolling millstone).
        (string field, string path)[] w3 =
        {
            ("_waterPumpSprite", $"{MillFolder}/WaterPump.png"),
            ("_millCraneSprite", $"{MillFolder}/MillCrane.png"),
            ("_millstoneSprite", $"{MillFolder}/Millstone.png"),
        };
        foreach (var (field, path) in w3)
        {
            var prop = loader.FindProperty(field);
            if (prop != null) prop.objectReferenceValue = LoadSpriteAt(path, opaque: false);
        }
        // World 4 contraptions (2026-11-11): thundercloud + bolt, sky crane + thunder wheel (also the rolling wheel).
        (string field, string path)[] w4 =
        {
            ("_stormCloudSprite",   $"{SkyFolder}/StormCloud.png"),
            ("_boltSprite",         $"{SkyFolder}/Bolt.png"),
            ("_skyCraneSprite",     $"{SkyFolder}/SkyCrane.png"),
            ("_thunderWheelSprite", $"{SkyFolder}/ThunderWheel.png"),
        };
        foreach (var (field, path) in w4)
        {
            var prop = loader.FindProperty(field);
            if (prop != null) prop.objectReferenceValue = LoadSpriteAt(path, opaque: false);
        }
        var drop = loader.FindProperty("_waterDropSprite");
        if (drop != null) drop.objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Abilities/WaterSwirl.png");

        // Boss drop-in ship (every world's boss level).
        const string ship = "Assets/Sprites/Environment/BossMothership.png";
        EnsureSingleSprite(ship, 100);
        var shipProp = loader.FindProperty("_mothershipSprite");
        if (shipProp != null) shipProp.objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(ship);
    }

    static void WirePlatformSprites(SerializedObject loader)
    {
        var arr = loader.FindProperty("_platformSprites");
        if (arr == null) return;
        // LevelLoader mount kinds, in order: IslandA, IslandB, Hill, Mesa, Scaffold, Wagon, Ramp.
        string[] files = { "PlatformWide_A.png", "PlatformWide_B.png", "Mount_Hill.png", "Mount_Mesa.png", "Mount_Scaffold.png", "Mount_Wagon.png", "Mount_Ramp.png" };
        arr.arraySize = files.Length;
        for (int i = 0; i < files.Length; i++)
            arr.GetArrayElementAtIndex(i).objectReferenceValue = LoadCornfieldSprite(files[i], opaque: false);
        // World 2 mounts (2026-11-09), same slots: floe, floe, floe, ice pillar, frosted scaffold, sled, snow ramp.
        var w2 = loader.FindProperty("_platformSpritesW2");
        if (w2 == null) return;
        string[] filesW2 = { "Mount_Floe.png", "Mount_Floe.png", "Mount_Floe.png", "Mount_IcePillar.png", "Mount_FrostScaffold.png", "Mount_Sled.png", "Mount_SnowRamp.png" };
        w2.arraySize = filesW2.Length;
        for (int i = 0; i < filesW2.Length; i++)
            w2.GetArrayElementAtIndex(i).objectReferenceValue = LoadSpriteAt($"{FrozenFolder}/{filesW2[i]}", opaque: false);
        // World 3 mounts (2026-11-10): islet, islet, islet, bridge pier, fishing jetty, rowing boat, boat slipway.
        var w3 = loader.FindProperty("_platformSpritesW3");
        if (w3 == null) return;
        string[] filesW3 = { "Mount_Islet.png", "Mount_Islet.png", "Mount_Islet.png", "Mount_Pier.png", "Mount_Jetty.png", "Mount_Boat.png", "Mount_Slipway.png" };
        w3.arraySize = filesW3.Length;
        for (int i = 0; i < filesW3.Length; i++)
            w3.GetArrayElementAtIndex(i).objectReferenceValue = LoadSpriteAt($"{MillFolder}/{filesW3[i]}", opaque: false);
        // World 4 mounts (2026-11-11): isle, isle, floating rock, stone pillar, sky dock, (no wagon), slide ramp.
        var w4 = loader.FindProperty("_platformSpritesW4");
        if (w4 == null) return;
        string[] filesW4 = { "Mount_Isle.png", "Mount_Isle.png", "Mount_Rock.png", "Mount_Pillar.png", "Mount_Dock.png", null, "Mount_Ramp.png" };
        w4.arraySize = filesW4.Length;
        for (int i = 0; i < filesW4.Length; i++)
            w4.GetArrayElementAtIndex(i).objectReferenceValue = filesW4[i] == null ? null : LoadSpriteAt($"{SkyFolder}/{filesW4[i]}", opaque: false);
    }

    // Thrown-ability art (AbilityProjectile, 2026-11-08): Gerald's feather bombs, Woolly's wool balls, Billy's mud,
    // Ducky's water swirls (2026-11-09).
    // Sprites are made by tools/import_ability_badges.py into Assets/Sprites/Abilities.
    static void WireAbilityProjectiles()
    {
        const string art = "Assets/Sprites/Abilities/";
        WireAnimalSprites("GeraldAnimal", typeof(GeraldAnimal), ("_bombSprite", art + "FeatherBomb.png"), ("_burstSprite", art + "FeatherBurst.png"));
        WireAnimalSprites("WoollyAnimal", typeof(WoollyAnimal), ("_woolBallSprite", art + "WoolBall.png"));
        WireAnimalSprites("BillyAnimal",  typeof(BillyAnimal),  ("_mudBallSprite", art + "MudBall.png"), ("_mudSplatSprite", art + "MudSplat.png"));
        WireAnimalSprites("DuckyAnimal",  typeof(DuckyAnimal),  ("_swirlSprite", art + "WaterSwirl.png"));   // 2026-11-09
        WireAnimalSprites("HoraceAnimal", typeof(HoraceAnimal), ("_horseshoeSprite", art + "Horseshoe.png")); // 2026-11-09
    }

    static void WireAnimalSprites(string prefabName, System.Type type, params (string field, string path)[] sprites)
    {
        string prefabPath = $"Assets/Prefabs/Animals/{prefabName}.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null) { Debug.LogWarning($"[FarmFury] {prefabPath} missing"); return; }
        var contents = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            var comp = contents.GetComponent(type);
            if (comp == null) return;
            var so = new SerializedObject(comp);
            int found = 0;
            foreach (var (field, path) in sprites)
            {
                EnsureSingleSprite(path);
                var sp = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                var prop = so.FindProperty(field);
                if (prop == null) continue;
                prop.objectReferenceValue = sp;
                if (sp != null) found++;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(contents, prefabPath);
            Debug.Log($"[FarmFury] {prefabName}: ability art {found}/{sprites.Length} wired.");
        }
        finally { PrefabUtility.UnloadPrefabContents(contents); }
    }

    // Single sprite mode (Multiple mode auto-slices the art and LoadAssetAtPath<Sprite> returns a fragment — see
    // CLAUDE.md's Auto-Compile Pipeline note).
    static void EnsureSingleSprite(string path, int ppu = 0)
    {
        if (!(AssetImporter.GetAtPath(path) is TextureImporter imp)) return;
        bool dirty = false;
        if (ppu > 0 && !Mathf.Approximately(imp.spritePixelsPerUnit, ppu)) { imp.spritePixelsPerUnit = ppu; dirty = true; }
        if (imp.textureType != TextureImporterType.Sprite)   { imp.textureType = TextureImporterType.Sprite; dirty = true; }
        if (imp.spriteImportMode != SpriteImportMode.Single) { imp.spriteImportMode = SpriteImportMode.Single; dirty = true; }
        if (!imp.alphaIsTransparency)                        { imp.alphaIsTransparency = true; dirty = true; }
        if (dirty) imp.SaveAndReimport();
    }

    static void WireCornfieldSprite(SerializedObject so, string field, string file, bool opaque)
    {
        var sp = LoadCornfieldSprite(file, opaque);
        if (sp == null) return;
        so.FindProperty(field).objectReferenceValue = sp;
        Debug.Log($"[FarmFury] Cornfield: {field} -> {file}");
    }

    static Sprite LoadCornfieldSprite(string file, bool opaque) => LoadSpriteAt($"{CornfieldFolder}/{file}", opaque);

    static Sprite LoadSpriteAt(string path, bool opaque)
    {
        if (AssetImporter.GetAtPath(path) is TextureImporter imp)
        {
            bool dirty = false;
            if (imp.textureType != TextureImporterType.Sprite)            { imp.textureType = TextureImporterType.Sprite; dirty = true; }
            if (imp.spriteImportMode != SpriteImportMode.Single)          { imp.spriteImportMode = SpriteImportMode.Single; dirty = true; }
            if (imp.spritePixelsPerUnit != 100)                           { imp.spritePixelsPerUnit = 100; dirty = true; }
            if (imp.alphaIsTransparency == opaque)                        { imp.alphaIsTransparency = !opaque; dirty = true; }
            if (imp.maxTextureSize < 4096)                                { imp.maxTextureSize = 4096; dirty = true; }
            if (imp.textureCompression != TextureImporterCompression.CompressedHQ) { imp.textureCompression = TextureImporterCompression.CompressedHQ; dirty = true; }
            if (imp.crunchedCompression)                                  { imp.crunchedCompression = false; dirty = true; }
            // Tiled strips and islands are drawn several times smaller than the source: keep mipmaps. The cover-fit
            // far painting is drawn near full size: none.
            if (imp.mipmapEnabled == opaque)                              { imp.mipmapEnabled = !opaque; dirty = true; }
            if (imp.wrapMode != TextureWrapMode.Clamp)                    { imp.wrapMode = TextureWrapMode.Clamp; dirty = true; }
            // Full-rect meshes: the clump sprites EnvironmentDepthSystem cuts from CornWall need the whole texture.
            var settings = new TextureImporterSettings();
            imp.ReadTextureSettings(settings);
            if (settings.spriteMeshType != SpriteMeshType.FullRect)
            {
                settings.spriteMeshType = SpriteMeshType.FullRect;
                imp.SetTextureSettings(settings);
                dirty = true;
            }
            if (dirty) imp.SaveAndReimport();
        }
        var sp = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sp == null)
            Debug.LogWarning($"[FarmFury] Cornfield art missing: {path} — run tools/import_cornfield_art.py");
        return sp;
    }
}
