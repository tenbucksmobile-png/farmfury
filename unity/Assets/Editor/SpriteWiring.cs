// FarmFury - Batch-imports character sprites and wires them into animal prefabs.
// Menu: FarmFury > Wire Sprites
// Run once after background removal output lands in Assets/Sprites/Characters/.

using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class SpriteWiring
{
    // PPU computed so visual diameter ≈ physics collider diameter.
    // Sprites are Kling AI 2720×1536 (landscape), Unity smart-trims them to ~1944×1481.
    // Formula: trimmed_height_px / (collider_radius * 2)  e.g. 1481 / 0.72 = 2057
    static readonly Dictionary<string, int> CharPPU = new()
    {
        { "Cluck",   2057 },  // radius 0.36 → diam 0.72u  →  1481/0.72
        { "Bessie",  1424 },  // radius 0.52 → diam 1.04u  →  1481/1.04
        { "Percy",   2057 },  // radius 0.36 → diam 0.72u
        { "Woolly",  2057 },  // radius 0.36 → diam 0.72u
        { "Ducky",   2468 },  // radius 0.30 → diam 0.60u  →  1481/0.60
        { "Horace",  1851 },  // radius 0.40 → diam 0.80u  →  1481/0.80
        { "Gerald",  1949 },  // radius 0.38 → diam 0.76u  →  1481/0.76
        { "Billy",   2057 },  // radius 0.36 → diam 0.72u
    };

    static readonly Dictionary<string, string> CharPrefab = new()
    {
        { "Cluck",   "CluckAnimal"  },
        { "Bessie",  "BessieAnimal" },
        { "Percy",   "PercyAnimal"  },
        { "Woolly",  "WoollyAnimal" },
        { "Ducky",   "DuckyAnimal"  },
        { "Horace",  "HoraceAnimal" },
        { "Gerald",  "GeraldAnimal" },
        { "Billy",   "BillyAnimal"  },
    };

    [MenuItem("FarmFury/Wire Sprites")]
    public static void WireAll()
    {
        AssetDatabase.StartAssetEditing();
        try
        {
            foreach (var kvp in CharPPU)
            {
                string charName  = kvp.Key;
                int    ppu       = kvp.Value;
                string spriteDir = $"Assets/Sprites/Characters/{charName}";
                SetFolderPPU(spriteDir, ppu);
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
            AssetDatabase.Refresh();
        }

        // Wire sprites into prefabs after import is complete
        foreach (var kvp in CharPrefab)
        {
            string charName  = kvp.Key;
            string prefabKey = kvp.Value;
            string spriteDir = $"Assets/Sprites/Characters/{charName}";
            WirePrefab(prefabKey, spriteDir);
        }

        AssetDatabase.SaveAssets();
        Debug.Log("[FarmFury] Sprite wiring complete.");
    }

    // ── Import settings ───────────────────────────────────────────────────────

    static void SetFolderPPU(string folder, int ppu)
    {
        var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { folder });
        foreach (var g in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(g);
            if (!path.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase)) continue;

            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp == null) continue;

            bool dirty = false;
            if (imp.textureType         != TextureImporterType.Sprite) { imp.textureType         = TextureImporterType.Sprite; dirty = true; }
            if (imp.spritePixelsPerUnit != ppu)                         { imp.spritePixelsPerUnit = ppu;                        dirty = true; }
            if (imp.filterMode          != FilterMode.Bilinear)         { imp.filterMode          = FilterMode.Bilinear;        dirty = true; }
            if (!imp.alphaIsTransparency)                               { imp.alphaIsTransparency = true;                      dirty = true; }

            if (dirty)
            {
                imp.SaveAndReimport();
                Debug.Log($"[FarmFury]   PPU={ppu}  {path}");
            }
        }
    }

    // ── Prefab wiring ─────────────────────────────────────────────────────────

    static void WirePrefab(string prefabName, string spriteDir)
    {
        var guids = AssetDatabase.FindAssets(prefabName + " t:Prefab", new[] { "Assets/Prefabs/Animals" });
        if (guids.Length == 0) { Debug.LogWarning($"[FarmFury] Prefab '{prefabName}' not found."); return; }

        string path     = AssetDatabase.GUIDToAssetPath(guids[0]);
        var    contents = PrefabUtility.LoadPrefabContents(path);
        var    animal   = contents.GetComponent<AnimalBase>();
        if (animal == null) { PrefabUtility.UnloadPrefabContents(contents); return; }

        // Derive "cluck", "bessie", etc. from "CluckAnimal", "BessieAnimal"
        string charKey = prefabName.Replace("Animal", "").ToLower();

        var so = new SerializedObject(animal);
        // Try charKey-prefixed filename first (e.g. "cluck_idle") then generic ("idle").
        // Kling AI output uses <Name>_<Pose>.png convention throughout.
        AssignSprite(so, "_sprIdle",     spriteDir, charKey + "_idle",    "idle");
        AssignSprite(so, "_sprLoaded",   spriteDir, charKey + "_loaded",  "loaded");
        AssignSprite(so, "_sprInFlight", spriteDir, charKey + "_inflight","inflight", "in flight");
        AssignSprite(so, "_sprImpact",   spriteDir, charKey + "_impact",  "impact");
        AssignSprite(so, "_sprAbility",  spriteDir,
            charKey + "_abilitytrigger", charKey + "_trigger1", charKey + "_trigger",
            "abilitytrigger", "trigger", "trigger1", "ability");

        // Shared "impact stars" VFX (ImpactStars1.png) — same asset on every animal, not a
        // per-character keyword match like the poses above.
        var impactStars = AssetDatabase.LoadAssetAtPath<Sprite>(
            "Assets/Sprites/Environment/World1Props/ImpactStars1.png");
        if (impactStars != null)
            so.FindProperty("_sprImpactStars").objectReferenceValue = impactStars;

        so.ApplyModifiedProperties();

        PrefabUtility.SaveAsPrefabAsset(contents, path);
        PrefabUtility.UnloadPrefabContents(contents);
        Debug.Log($"[FarmFury]   Wired sprites into {prefabName}");
    }

    static void AssignSprite(SerializedObject so, string fieldName, string folder, params string[] candidates)
    {
        var sprite = FindSprite(folder, candidates);
        so.FindProperty(fieldName).objectReferenceValue = sprite;
        if (sprite == null)
            Debug.LogWarning($"[FarmFury]     {fieldName}: no match in {folder} for [{string.Join(", ", candidates)}]");
    }

    // Case-insensitive, space-ignored match against the file's base name.
    static Sprite FindSprite(string folder, params string[] candidates)
    {
        var guids = AssetDatabase.FindAssets("t:Sprite", new[] { folder });
        foreach (var candidate in candidates)
        {
            string norm = candidate.ToLower().Replace(" ", "");
            foreach (var g in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(g);
                string name = Path.GetFileNameWithoutExtension(path).ToLower().Replace(" ", "");
                if (name == norm)
                    return AssetDatabase.LoadAssetAtPath<Sprite>(path);
            }
        }
        return null;
    }
}
