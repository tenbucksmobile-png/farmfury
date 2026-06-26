// Runs automatically every time Unity compiles scripts.
// Auto-generates level data if none exist, and force-reimports launcher
// sprites if their PPU or alpha settings are stale.
// No manual menu steps required.

using System.IO;
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
        AutoWireCharacterSprites();
        AutoCopyCardSprites();
    }

    static void AutoGenerateLevels()
    {
        var guids = AssetDatabase.FindAssets("t:LevelData",
            new[] { "Assets/ScriptableObjects/Levels" });
        if (guids.Length > 0) return;   // already generated — skip

        LevelDataGenerator.GenerateAll(silent: true);
        Debug.Log("[FarmFury] Auto-generated 6 level assets.");
    }

    static void AutoWireCharacterSprites()
    {
        // Only reimport if the sentinel sprite is still on the old PPU value.
        // This prevents wiring 40+ sprites on every compile after the first fix.
        const string sentinel = "Assets/Sprites/Characters/Cluck/Loaded.png";
        const int    targetPpu = 2057;
        var imp = AssetImporter.GetAtPath(sentinel) as TextureImporter;
        if (imp == null || imp.spritePixelsPerUnit == targetPpu) return;

        SpriteWiring.WireAll();
        Debug.Log("[FarmFury] Auto-applied updated character sprite PPU values.");
    }

    static void AutoCopyCardSprites()
    {
        // Copy assets/FarmCards/*.png → Assets/Sprites/UI/Cards/ so SceneSetup can wire them.
        // Source path is two directories above the Unity project root (repo root/assets/FarmCards).
        string unityRoot = Path.GetFullPath(Application.dataPath + "/../..");
        string srcDir    = Path.Combine(unityRoot, "assets", "FarmCards");
        const string dstFolder = "Assets/Sprites/UI/Cards";

        if (!Directory.Exists(srcDir)) return;  // nothing to copy yet

        if (!AssetDatabase.IsValidFolder("Assets/Sprites/UI"))
            AssetDatabase.CreateFolder("Assets/Sprites", "UI");
        if (!AssetDatabase.IsValidFolder(dstFolder))
            AssetDatabase.CreateFolder("Assets/Sprites/UI", "Cards");

        bool anyCopied = false;
        foreach (var srcPath in Directory.GetFiles(srcDir, "*.png"))
        {
            string filename = Path.GetFileName(srcPath);
            string dstPath  = Path.Combine(Application.dataPath,
                                 "Sprites/UI/Cards", filename).Replace('\\', '/');
            if (!File.Exists(dstPath))
            {
                File.Copy(srcPath, dstPath);
                anyCopied = true;
            }
        }

        if (anyCopied)
        {
            AssetDatabase.Refresh();
            Debug.Log($"[FarmFury] Copied card sprites from assets/FarmCards/ to {dstFolder}.");
        }
    }

    static void AutoFixLauncherSprites()
    {
        // Both sprites are 2048×2048 px. PPU=768 gives 2048/768=2.667u — same physical
        // size as the 1024px spec at PPU=384. Body pivot bottom-centre so it sits on Y=0.
        bool anyFixed = false;
        anyFixed |= FixSprite("Assets/Sprites/Environment/Launchers/Trabuchet_Body.png", 768,
                              customPivot: new Vector2(0.50f, 0.00f));
        anyFixed |= FixSprite("Assets/Sprites/Environment/Launchers/Trabuchet_Arm.png",  768,
                              customPivot: new Vector2(0.55f, 0.50f));
        if (anyFixed)
            Debug.Log("[FarmFury] Auto-fixed launcher sprite import settings (PPU=768, pivots corrected).");
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
