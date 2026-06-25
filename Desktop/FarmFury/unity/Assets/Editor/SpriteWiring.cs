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
    // Formula: (canvas_px * fill_pct) / (collider_radius * 2)
    // All sprites: 1024px canvas, ~75% character fill = ~768px character height.
    static readonly Dictionary<string, int> CharPPU = new()
    {
        { "Cluck",   1067 },  // radius 0.36 → diam 0.72u  →  768/0.72
        { "Bessie",   740 },  // radius 0.52 → diam 1.04u  →  768/1.04
        { "Percy",   1067 },  // radius 0.36 → diam 0.72u
        { "Woolly",  1067 },  // radius 0.36 → diam 0.72u
        { "Ducky",   1280 },  // radius 0.30 → diam 0.60u  →  768/0.60
        { "Horace",   960 },  // radius 0.40 → diam 0.80u  →  768/0.80
        { "Gerald",  1010 },  // radius 0.38 → diam 0.76u  →  768/0.76
        { "Billy",   1067 },  // radius 0.36 → diam 0.72u
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

        var so = new SerializedObject(animal);
        AssignSprite(so, "_sprIdle",     spriteDir, "idle");
        AssignSprite(so, "_sprLoaded",   spriteDir, "loaded");
        AssignSprite(so, "_sprInFlight", spriteDir, "inflight", "in flight");
        AssignSprite(so, "_sprImpact",   spriteDir, "impact");
        AssignSprite(so, "_sprAbility",  spriteDir, "abilitytrigger", "trigger", "trigger1", "ability");
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
