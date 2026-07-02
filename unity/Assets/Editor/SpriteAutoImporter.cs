// Auto-configures launcher and character sprites when they are imported into Unity.
// Runs whenever Unity detects a file change (e.g., after remove_backgrounds.py runs).
// No need to run Wire Scene References for import settings.
// First-time or after a fresh checkout: FarmFury → Reimport Sprites to force-apply.

using UnityEditor;
using UnityEngine;

public class SpriteAutoImporter : AssetPostprocessor
{
    [MenuItem("FarmFury/Reimport Sprites")]
    public static void ForceReimportAll()
    {
        string[] folders =
        {
            "Assets/Sprites/Environment/Launchers",
            "Assets/Sprites/Environment/World1Props",
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

        // Trebuchet system removed 2026-07-02 (replaced by FarmCannon) — Trabuchet_Body/Arm/Swing
        // sprites are no longer referenced anywhere; their special-case import rules were removed
        // along with them. Cannon.png (and any other launcher sprite) uses the generic branch below.
        // ── All launcher sprites (PPU=384, alpha fix) ────────────────────────
        if (assetPath.Contains("Sprites/Environment/Launchers/"))
        {
            ConfigureSprite(imp, 384, alphaTransparency: true);
            if (imp.spriteImportMode != SpriteImportMode.Single) imp.spriteImportMode = SpriteImportMode.Single;
        }
        // ── World 1 prop sprites ──────────────────────────────────────────────
        // Scenery props: PPU=512 → 1024px canvas = 2u native size at scale 1.
        // Block sprites (Block_*, Plank_*, 2D_Block_*): need PPU=textureWidth so
        //   nativeSize=1×1, making localScale(w,h) map directly to world dimensions.
        else if (assetPath.Contains("Sprites/Environment/World1Props/"))
        {
            string fname = System.IO.Path.GetFileName(assetPath);
            bool isBlockSprite = fname.StartsWith("Block_") || fname.StartsWith("Plank_") || fname.StartsWith("2D_Block_");
            // FIXED: explicit entries for late-added scenery sprites that had no .meta
            // LEVEL1_EXACT: Windmill.png added for Level 1 exact layout
            bool isNewScenery = fname == "RuinedStoneWall.png" || fname == "StoneTower.png"
                             || fname == "OldBarn_Right.png"   || fname == "Windmill.png";
            if (isBlockSprite)
            {
                imp.GetSourceTextureWidthAndHeight(out int tw, out _);
                int ppu = tw > 0 ? tw : 1024;
                ConfigureSprite(imp, ppu, alphaTransparency: true);
            }
            else if (isNewScenery)
            {
                ConfigureSprite(imp, 512, alphaTransparency: true); // 512 matches all other World1Props scenery
            }
            else
            {
                ConfigureSprite(imp, 512, alphaTransparency: true);
            }
            if (imp.spriteImportMode != SpriteImportMode.Single)
                imp.spriteImportMode = SpriteImportMode.Single;
        }
        // ── Backdrop art (sky paintings, world reference art, StoneTower) ────
        // FIXED: StoneTower.png lives here; SceneSetup._sprStoneTower now loads from this path.
        else if (assetPath.Contains("Sprites/Environment/Backdrops/"))
        {
            ConfigureSprite(imp, 512, alphaTransparency: true);
            if (imp.spriteImportMode != SpriteImportMode.Single)
                imp.spriteImportMode = SpriteImportMode.Single;
        }
        // ── HUD animal cards + level select world cards (UI sprites, PPU=100) ──
        else if (assetPath.Contains("Sprites/UI/Cards/") ||
                 assetPath.Contains("Sprites/UI/LevelCards/"))
        {
            ConfigureSprite(imp, 100, alphaTransparency: true);
            if (imp.spriteImportMode != SpriteImportMode.Single)
                imp.spriteImportMode = SpriteImportMode.Single;
        }
        // ── General UI icons/buttons (e.g. Play.png) — same PPU=100 convention as the
        // card sprites above; RectTransform.sizeDelta controls on-screen size regardless. ──
        else if (assetPath.Contains("Sprites/UI/"))
        {
            ConfigureSprite(imp, 100, alphaTransparency: true);
            if (imp.spriteImportMode != SpriteImportMode.Single)
                imp.spriteImportMode = SpriteImportMode.Single;
        }
        // ── Robot enemy sprites (PPU=1746 → content 1746px = 1u; at scale 0.8 → 0.8u world) ──
        else if (assetPath.Contains("Sprites/Enemies/Robot/"))
        {
            ConfigureSprite(imp, 1746, alphaTransparency: true);
            if (imp.spriteImportMode != SpriteImportMode.Single)
                imp.spriteImportMode = SpriteImportMode.Single;
        }
        // ── Character sprites (PPU already managed per-character by SpriteWiring) ─
        // Only fix alpha — leave PPU alone so SpriteWiring stays authoritative.
        // Fixed 2026-07-10: this branch was the ONLY one in this file that never enforced
        // SpriteImportMode.Single — every other category does, with a comment explaining
        // Multiple mode breaks LoadAssetAtPath<Sprite>/serialized references. Character sprites
        // were missed, and it showed: ALL 8 characters' pose PNGs were sitting in Multiple mode,
        // several auto-sliced by Unity into disconnected fragments (Cluck_InFlight.png alone had
        // 9). CluckAnimal.prefab's _sprInFlight ended up wired to one of those fragments — a
        // 53×8px sliver of the original art — instead of the whole sprite, which is why the
        // in-flight pose was reported as invisible ("not appearing", "not allowing me to drop
        // into scene" — Multiple-mode textures don't drag into a scene as one usable sprite).
        else if (assetPath.Contains("Sprites/Characters/") || assetPath.Contains("Sprites/Enemies/"))
        {
            if (imp.textureType != TextureImporterType.Sprite)
                imp.textureType = TextureImporterType.Sprite;
            if (!imp.alphaIsTransparency)
                imp.alphaIsTransparency = true;
            if (imp.alphaSource != TextureImporterAlphaSource.FromInput)
                imp.alphaSource = TextureImporterAlphaSource.FromInput;
            if (imp.spriteImportMode != SpriteImportMode.Single)
                imp.spriteImportMode = SpriteImportMode.Single;
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
