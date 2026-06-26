// Runs automatically every time Unity compiles scripts.
// Auto-generates level data if none exist, and force-reimports launcher
// sprites if their PPU or alpha settings are stale.
// No manual menu steps required.

using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class EditorAutoSetup
{
    static EditorAutoSetup()
    {
        // delayCall defers until Unity is fully initialised (safe to use AssetDatabase).
        EditorApplication.delayCall += RunOnce;
    }

    static void RunOnce()
    {
        AutoGenerateLevels();
        AutoFixLauncherSprites();
    }

    static void AutoGenerateLevels()
    {
        var guids = AssetDatabase.FindAssets("t:LevelData",
            new[] { "Assets/ScriptableObjects/Levels" });
        if (guids.Length > 0) return;   // already generated — skip

        LevelDataGenerator.GenerateAll(silent: true);
        Debug.Log("[FarmFury] Auto-generated 6 level assets.");
    }

    static void AutoFixLauncherSprites()
    {
        bool anyFixed = false;
        anyFixed |= FixSprite("Assets/Sprites/Environment/Launchers/Trabuchet_Body.png", 384);
        anyFixed |= FixSprite("Assets/Sprites/Environment/Launchers/Trabuchet_Arm.png",  384,
                              customPivot: new Vector2(0.55f, 0.50f));
        if (anyFixed)
            Debug.Log("[FarmFury] Auto-fixed launcher sprite import settings.");
    }

    static bool FixSprite(string path, int ppu, Vector2? customPivot = null)
    {
        var imp = AssetImporter.GetAtPath(path) as TextureImporter;
        if (imp == null) return false;

        bool dirty = false;
        if (imp.textureType != TextureImporterType.Sprite)         { imp.textureType = TextureImporterType.Sprite;         dirty = true; }
        if (imp.spritePixelsPerUnit != ppu)                        { imp.spritePixelsPerUnit = ppu;                        dirty = true; }
        if (!imp.alphaIsTransparency)                              { imp.alphaIsTransparency = true;                       dirty = true; }
        if (imp.alphaSource != TextureImporterAlphaSource.FromInput){ imp.alphaSource = TextureImporterAlphaSource.FromInput; dirty = true; }

        if (customPivot.HasValue)
        {
            var s = new TextureImporterSettings();
            imp.ReadTextureSettings(s);
            if (s.spriteAlignment != (int)SpriteAlignment.Custom || s.spritePivot != customPivot.Value)
            {
                s.spriteAlignment = (int)SpriteAlignment.Custom;
                s.spritePivot     = customPivot.Value;
                imp.SetTextureSettings(s);
                dirty = true;
            }
        }

        if (!dirty) return false;
        imp.SaveAndReimport();
        return true;
    }
}
