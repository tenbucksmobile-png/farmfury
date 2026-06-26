// Auto-configures launcher and character sprites when they are imported into Unity.
// Runs whenever Unity detects a file change (e.g., after remove_backgrounds.py runs).
// No need to run Wire Scene References for import settings.
// First-time or after a fresh checkout: FarmFury → Reimport Sprites to force-apply.

using UnityEditor;
using UnityEngine;

public class SpriteAutoImporter : AssetPostprocessor
{
    [MenuItem("FarmFury/Reimport Sprites")]
    static void ForceReimportAll()
    {
        string[] folders =
        {
            "Assets/Sprites/Environment/Launchers",
            "Assets/Sprites/Characters",
            "Assets/Sprites/Enemies",
        };
        foreach (var folder in folders)
        {
            var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { folder });
            foreach (var g in guids)
                AssetDatabase.ImportAsset(AssetDatabase.GUIDToAssetPath(g), ImportAssetOptions.ForceUpdate);
        }
        Debug.Log("[FarmFury] Forced reimport of all sprite folders.");
    }


    void OnPreprocessTexture()
    {
        var imp = assetImporter as TextureImporter;
        if (imp == null) return;

        // ── Trebuchet body (static frame, PPU=384, alpha from PNG) ──────────
        if (assetPath.Contains("Launchers/Trabuchet_Body"))
        {
            ConfigureSprite(imp, 384, alphaTransparency: true);
        }
        // ── Trebuchet arm (rotating, PPU=384, custom pivot at fulcrum) ──────
        else if (assetPath.Contains("Launchers/Trabuchet_Arm"))
        {
            ConfigureSprite(imp, 384, alphaTransparency: true);
            SetCustomPivot(imp, new Vector2(0.55f, 0.50f));
        }
        // ── All other launcher sprites (same alpha fix, PPU=384) ─────────────
        else if (assetPath.Contains("Sprites/Environment/Launchers/"))
        {
            ConfigureSprite(imp, 384, alphaTransparency: true);
        }
        // ── Character sprites (PPU already managed per-character by SpriteWiring) ─
        // Only fix alpha — leave PPU alone so SpriteWiring stays authoritative.
        else if (assetPath.Contains("Sprites/Characters/") || assetPath.Contains("Sprites/Enemies/"))
        {
            if (imp.textureType != TextureImporterType.Sprite)
                imp.textureType = TextureImporterType.Sprite;
            if (!imp.alphaIsTransparency)
                imp.alphaIsTransparency = true;
            if (imp.alphaSource != TextureImporterAlphaSource.FromInput)
                imp.alphaSource = TextureImporterAlphaSource.FromInput;
        }
    }

    static void ConfigureSprite(TextureImporter imp, int ppu, bool alphaTransparency)
    {
        if (imp.textureType != TextureImporterType.Sprite)
            imp.textureType = TextureImporterType.Sprite;
        if (imp.spritePixelsPerUnit != ppu)
            imp.spritePixelsPerUnit = ppu;
        if (alphaTransparency && !imp.alphaIsTransparency)
            imp.alphaIsTransparency = true;
        if (alphaTransparency && imp.alphaSource != TextureImporterAlphaSource.FromInput)
            imp.alphaSource = TextureImporterAlphaSource.FromInput;
    }

    static void SetCustomPivot(TextureImporter imp, Vector2 pivot)
    {
        var settings = new TextureImporterSettings();
        imp.ReadTextureSettings(settings);
        if (settings.spriteAlignment == (int)SpriteAlignment.Custom && settings.spritePivot == pivot)
            return;
        settings.spriteAlignment = (int)SpriteAlignment.Custom;
        settings.spritePivot     = pivot;
        imp.SetTextureSettings(settings);
    }
}
