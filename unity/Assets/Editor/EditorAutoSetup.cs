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
        AutoFixCannonSprite();
        AutoWireCharacterSprites();
        AutoCopyCardSprites();
        AutoFixWorld1Props();
        // Scan for any PNGs modified outside Unity (e.g., by remove_backgrounds.py)
        // and reimport them so the latest content is visible in-game.
        AssetDatabase.Refresh();
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
        // Fixed 2026-07-10: sentinel path was stale — "Loaded.png" never existed post-rename to
        // "Cluck_Loaded.png", so `imp` was always null and this silently never fired, for any
        // reason, ever. Separately broadened the trigger to also catch wrong sprite import mode,
        // not just stale PPU: ALL character sprites (all 8 characters) were stuck in Multiple
        // mode until SpriteAutoImporter.cs's character branch was fixed the same day to enforce
        // Single (several got auto-sliced into disconnected fragments — CluckAnimal's in-flight
        // pose ended up wired to a 53x8px sliver instead of the actual art). Now self-heals a
        // future regression on next compile instead of relying on someone noticing and running
        // the menu items by hand.
        const string sentinel  = "Assets/Sprites/Characters/Cluck/Cluck_Loaded.png";
        const int    targetPpu = 2057;
        var imp = AssetImporter.GetAtPath(sentinel) as TextureImporter;
        if (imp == null) return;
        if (imp.spritePixelsPerUnit == targetPpu && imp.spriteImportMode == SpriteImportMode.Single) return;

        SpriteAutoImporter.ForceReimportAll();
        SpriteWiring.WireAll();
        Debug.Log("[FarmFury] Auto-applied updated character sprite PPU/import-mode values.");
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

    // Force-reimport World1Props if PPU is not yet 512 (happens after first compile with new SpriteAutoImporter rule).
    static void AutoFixWorld1Props()
    {
        const string sentinel = "Assets/Sprites/Environment/World1Props/Grass Tuft.png";
        var imp = AssetImporter.GetAtPath(sentinel) as TextureImporter;
        if (imp == null || imp.spritePixelsPerUnit == 512) return;

        var guids = AssetDatabase.FindAssets("t:Texture2D",
            new[] { "Assets/Sprites/Environment/World1Props" });
        foreach (var g in guids)
            AssetDatabase.ImportAsset(AssetDatabase.GUIDToAssetPath(g), ImportAssetOptions.ForceUpdate);
        Debug.Log("[FarmFury] Auto-reimported World1Props at PPU=512.");
    }

    // Trebuchet system removed 2026-07-02 (replaced by FarmCannon) — was AutoFixLauncherSprites(),
    // force-fixing Trabuchet_Body/Arm.png. Cannon.png needs no custom pivot (static, non-rotating
    // prop — default centre 0.5/0.5 is correct) and PPU=384 is already enforced by the generic
    // "Sprites/Environment/Launchers/" branch in SpriteAutoImporter.OnPreprocessTexture(), so this
    // just double-checks Single mode/alpha in case the file was ever re-imported as Multiple.
    static void AutoFixCannonSprite()
    {
        if (FixSprite("Assets/Sprites/Environment/Launchers/Cannon.png", 384,
                       mode: SpriteImportMode.Single))
            Debug.Log("[FarmFury] Auto-fixed Cannon.png (Single mode, PPU=384).");
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
