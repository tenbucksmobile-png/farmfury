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
        string[] files = { "PlatformWide_A.png", "PlatformWide_B.png" };
        arr.arraySize = files.Length;
        for (int i = 0; i < files.Length; i++)
            arr.GetArrayElementAtIndex(i).objectReferenceValue = LoadCornfieldSprite(files[i], opaque: false);
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
