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
    }

    // Thrown-ability art (AbilityProjectile, 2026-11-08): Gerald's feather bombs, Woolly's wool balls, Billy's mud.
    // Sprites are made by tools/import_ability_badges.py into Assets/Sprites/Abilities.
    static void WireAbilityProjectiles()
    {
        const string art = "Assets/Sprites/Abilities/";
        WireAnimalSprites("GeraldAnimal", typeof(GeraldAnimal), ("_bombSprite", art + "FeatherBomb.png"), ("_burstSprite", art + "FeatherBurst.png"));
        WireAnimalSprites("WoollyAnimal", typeof(WoollyAnimal), ("_woolBallSprite", art + "WoolBall.png"));
        WireAnimalSprites("BillyAnimal",  typeof(BillyAnimal),  ("_mudBallSprite", art + "MudBall.png"), ("_mudSplatSprite", art + "MudSplat.png"));
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

    static Sprite LoadCornfieldSprite(string file, bool opaque)
    {
        string path = $"{CornfieldFolder}/{file}";
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
