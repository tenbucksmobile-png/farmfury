using System.Linq;
using FarmFury.Core;
using UnityEditor;
using UnityEngine;

/// <summary>
/// FarmFury > Generate World Data: creates/updates the nine WorldData assets in Assets/ScriptableObjects/Worlds
/// (franchise world list — six free story worlds from the GDD, then Stampede's three paid post-finale worlds,
/// $3.99 each, added per the 2026-11-02 alignment decision). Identity fields (name, level count, boss id,
/// launcher, introduced animals, paid flag) are re-stamped every run; art fields are left alone so hand-wired or
/// SceneSetup-wired art survives. Safe to re-run. Wire Scene References runs this automatically if no assets exist.
/// </summary>
public static class WorldDataGenerator
{
    public const string Folder = "Assets/ScriptableObjects/Worlds";

    private struct Spec
    {
        public WorldType type;
        public string name;
        public int levels;
        public string launcher;
        public string blurb;
        public bool paid;
    }

    // Level counts and launchers: GDD world table (CLAUDE.md "6 Worlds, 6 Launchers"). The paid worlds have no
    // FarmFury design yet — 12 levels each mirrors Stampede's paid worlds and is a placeholder until designed.
    private static readonly Spec[] Specs =
    {
        new() { type = WorldType.MeadowRuins,      name = "Meadow Ruins",       levels = 18, launcher = "Farm Cannon",      blurb = "Where it all began: drive the Harvest Robots off the old farm." },
        new() { type = WorldType.FrozenTundra,     name = "Frozen Tundra",      levels = 22, launcher = "Ice Cannon",       blurb = "Frozen fortresses and freeze zones that slow every flight." },
        new() { type = WorldType.WatermillVillage, name = "Watermill Village",  levels = 22, launcher = "Water Wheel",      blurb = "Time the wheel and let the fire spread through the timber." },
        new() { type = WorldType.SkyIslands,       name = "Sky Islands",        levels = 24, launcher = "Airdrop Biplane",  blurb = "Drop in from the biplane and ride the updrafts." },
        new() { type = WorldType.SunkenCity,       name = "Sunken City",        levels = 22, launcher = "Torpedo Tube",     blurb = "Currents, bubbles and lever switches under the waves." },
        new() { type = WorldType.RobotMothership,  name = "Robot Mothership",   levels = 16, launcher = "Gravity Sling",    blurb = "Zero-G showdown with the Robot Overlord." },
        new() { type = WorldType.DustbowlCanyon,   name = "Dustbowl Canyon",    levels = 12, launcher = "TBD",              blurb = "Bonus world.", paid = true },
        new() { type = WorldType.HarvestFairground,name = "Harvest Fairground", levels = 12, launcher = "TBD",              blurb = "Bonus world.", paid = true },
        new() { type = WorldType.CropFactory,      name = "Crop Factory",       levels = 12, launcher = "TBD",              blurb = "Bonus world.", paid = true },
    };

    [MenuItem("FarmFury/Generate World Data")]
    public static void GenerateAll()
    {
        if (!AssetDatabase.IsValidFolder("Assets/ScriptableObjects")) { AssetDatabase.CreateFolder("Assets", "ScriptableObjects"); }
        if (!AssetDatabase.IsValidFolder(Folder)) { AssetDatabase.CreateFolder("Assets/ScriptableObjects", "Worlds"); }

        foreach (var spec in Specs)
        {
            string path = $"{Folder}/W{(int)spec.type + 1:00}_{spec.type}.asset";
            var asset = AssetDatabase.LoadAssetAtPath<WorldData>(path);
            bool created = asset == null;
            if (created) { asset = ScriptableObject.CreateInstance<WorldData>(); }

            asset.worldType = spec.type;
            asset.displayName = spec.name;
            asset.levelCount = spec.levels;
            asset.bossLevelId = $"W{(int)spec.type + 1}_L{spec.levels:00}";
            asset.launcherName = spec.launcher;
            asset.blurb = spec.blurb;
            asset.purchaseRequired = spec.paid;
            asset.introducedAnimals = AnimalRoster.All.Where(a => AnimalRoster.IntroWorld(a) == spec.type && !spec.paid).ToArray();

            if (created) { AssetDatabase.CreateAsset(asset, path); }
            else { EditorUtility.SetDirty(asset); }
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[FarmFury] World Data: {Specs.Length} worlds in {Folder}.");
    }

    public static WorldData[] LoadAll() =>
        AssetDatabase.FindAssets("t:WorldData", new[] { Folder })
            .Select(g => AssetDatabase.LoadAssetAtPath<WorldData>(AssetDatabase.GUIDToAssetPath(g)))
            .Where(w => w != null)
            .OrderBy(w => (int)w.worldType)
            .ToArray();
}
