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
        string unityRoot = Path.GetFullPath(Application.dataPath + "/../..");
        CopySpritesFolder(
            srcRelative: "assets/FarmCards",
            dstParent:   "Assets/Sprites/UI",
            dstFolder:   "Assets/Sprites/UI/Cards",
            folderName:  "Cards",
            logLabel:    "HUD animal card");
        CopySpritesFolder(
            srcRelative: "assets/LevelCards",
            dstParent:   "Assets/Sprites/UI",
            dstFolder:   "Assets/Sprites/UI/LevelCards",
            folderName:  "LevelCards",
            logLabel:    "level select world card");
    }

    static void CopySpritesFolder(string srcRelative, string dstParent,
                                   string dstFolder, string folderName, string logLabel)
    {
        string unityRoot = Path.GetFullPath(Application.dataPath + "/../..");
        string srcDir    = Path.Combine(unityRoot, srcRelative);
        if (!Directory.Exists(srcDir)) return;

        if (!AssetDatabase.IsValidFolder(dstParent))
        {
            var parts = dstParent.Split('/');
            AssetDatabase.CreateFolder(string.Join("/", parts[..^1]), parts[^1]);
        }
        if (!AssetDatabase.IsValidFolder(dstFolder))
            AssetDatabase.CreateFolder(dstParent, folderName);

        bool anyCopied = false;
        foreach (var srcPath in Directory.GetFiles(srcDir, "*.png"))
        {
            string filename = Path.GetFileName(srcPath);
            string dstPath  = Path.Combine(Application.dataPath,
                                 dstFolder.Replace("Assets/", ""), filename);
            if (!File.Exists(dstPath))
            {
                File.Copy(srcPath, dstPath);
                anyCopied = true;
            }
        }

        if (anyCopied)
        {
            AssetDatabase.Refresh();
            Debug.Log($"[FarmFury] Copied {logLabel} sprites from {srcRelative}/ to {dstFolder}.");
        }
    }

    static void AutoFixLauncherSprites()
    {
        // Both sprites are 2048×2048 px. PPU=768 gives 2048/768=2.667u.
        // MUST be Single mode — Multiple mode (spriteMode:2) splits the canvas into sub-sprites
        // breaking LoadAssetAtPath<Sprite> and causing arm/body to appear disconnected in-game.
        bool anyFixed = false;
        anyFixed |= FixSprite("Assets/Sprites/Environment/Launchers/Trabuchet_Body.png", 768,
                              customPivot: new Vector2(0.50f, 0.00f),
                              mode: SpriteImportMode.Single);
        // Arm pivot bolt pixel-measured: ~40% from left, ~56% from bottom of 2048px canvas
        anyFixed |= FixSprite("Assets/Sprites/Environment/Launchers/Trabuchet_Arm.png", 768,
                              customPivot: new Vector2(0.40f, 0.56f),
                              mode: SpriteImportMode.Single);
        if (anyFixed)
            Debug.Log("[FarmFury] Auto-fixed launcher sprites (Single mode, PPU=768, pivots).");
    }

    static bool FixSprite(string path, int ppu, Vector2? customPivot = null,
                          SpriteImportMode mode = SpriteImportMode.Single)
    {
        var imp = AssetImporter.GetAtPath(path) as TextureImporter;
        if (imp == null) return false;

        bool dirty = false;
        if (imp.textureType != TextureImporterType.Sprite)           { imp.textureType = TextureImporterType.Sprite;           dirty = true; }
        if (imp.spritePixelsPerUnit != ppu)                          { imp.spritePixelsPerUnit = ppu;                          dirty = true; }
        if (!imp.alphaIsTransparency)                                { imp.alphaIsTransparency = true;                         dirty = true; }
        if (imp.alphaSource != TextureImporterAlphaSource.FromInput) { imp.alphaSource = TextureImporterAlphaSource.FromInput; dirty = true; }
        if (imp.spriteImportMode != mode)                            { imp.spriteImportMode = mode;                            dirty = true; }

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
