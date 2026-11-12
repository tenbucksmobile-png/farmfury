// FarmFury — levels for Worlds 2-9, built from World 1's towers (2026-11-02, user request: "use the tower builds
// and robot placements and create all the levels for each world ... use the same building blocks to build the
// towers and the variations").
//
// Runs at the end of FarmFury ▶ Generate All Level Data (LevelDataGenerator.GenerateAll), after L01-L19 exist.
// Every level is one of World 1's hand-built towers (L01-L17; L18 for the boss), re-dressed for its world:
//   * template — levels walk through L01..L17 in order across the world, so difficulty ramps like World 1;
//   * mirror   — every other level flips the tower left-right about its own centre (art flipped to match);
//   * shift    — the whole tower slides up to ±0.35 units, kept inside World 1's reachable x 2.8..8.4;
//   * stone    — a few plank shapes stone has art for become stone, more in later worlds;
//   * robots   — some robots move up a tier (SemiHarvester -> Harvester -> Basic), more in later worlds;
//                the world's own robot art per tier comes from LevelLoader._worldRobots (World 2: its reskins);
//   * boss     — the last level is L18's fortress with RobotType.Commander = the world's boss.
// Blocks, robot scales and bird counts are World 1's, so the physics is the already-tuned physics. The world's
// look (backdrop, robots, props) is applied at runtime by LevelData.world. Nothing here has been seen in Play mode.
//
// Files: W<NN>_L<NN>_<Name>.asset — sorts after every L.. file, so GameManager's global order stays World 1, L19
// (World 2 level 1), then World 2..9. Ids W<N>_L<NN> (Level Select orders a world by levelId, boss last).
// Regenerating deletes every W*.asset first, so a removed or renamed level never lingers.

using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class WorldLevelGenerator
{
    const string Folder = "Assets/ScriptableObjects/Levels";

    // World 1 towers in difficulty order; L18 is the boss fortress.
    static readonly string[] Templates =
    {
        "L01_FirstContact", "L02_StoneWall", "L03_TheTower", "L04_EggPractice", "L05_TheGauntlet",
        "L06_DoubleBarrel", "L07_BarrelRow", "L08_FortressAssault", "L09_StoneBastion", "L10_BessiesDebut",
        "L11_FullRoster", "L12_HeavyGuard", "L13_TenStrong", "L14_StoneAndPawns", "L15_FiveStones",
        "L16_TwinTowers", "L17_EightStrong",
    };
    const string BossTemplate = "L18_CaptainsLastStand";

    const float MinX = 2.8f, MaxX = 8.4f;

    class WorldSpec
    {
        public int world, levels, difficulty, firstLevel = 1;
        public AnimalType[] newAnimals, oldAnimals;
        public string bossName;
        public string[] adjectives, nouns;
    }

    // Level counts match WorldDataGenerator. difficulty 1-5 scales the stone and robot-tier variations
    // (story order; the paid worlds sit mid-curve since they can be bought early). Paid worlds use only
    // Cluck and Bessie, the two animals every player has.
    static readonly AnimalType[] W1 = { AnimalType.Cluck, AnimalType.Bessie };
    static readonly WorldSpec[] Worlds =
    {
        new() { world = 2, levels = 22, difficulty = 1, firstLevel = 2,   // level 1 is L19 Frostbound Watch
                newAnimals = new[] { AnimalType.Percy, AnimalType.Woolly }, oldAnimals = W1,
                bossName = "The Ice Commander",
                adjectives = new[] { "Frost", "Glacier", "Snowdrift", "Icicle", "Polar", "Blizzard" },
                nouns = new[] { "Watch", "Keep", "Outpost", "Ridge", "Hollow" } },
        new() { world = 3, levels = 22, difficulty = 2,
                newAnimals = new[] { AnimalType.Ducky }, oldAnimals = new[] { AnimalType.Percy, AnimalType.Woolly, AnimalType.Cluck, AnimalType.Bessie },
                bossName = "The Milestone Mauler",
                adjectives = new[] { "Millpond", "Mossy", "Willow", "Riverside", "Cobble", "Waterwheel" },
                nouns = new[] { "Crossing", "Yard", "Lane", "Dock", "Green" } },
        new() { world = 4, levels = 24, difficulty = 3,
                newAnimals = new[] { AnimalType.Horace }, oldAnimals = new[] { AnimalType.Ducky, AnimalType.Percy, AnimalType.Woolly, AnimalType.Bessie },
                bossName = "The Storm Baron",
                adjectives = new[] { "Cloudtop", "Updraft", "Skyward", "Thunder", "Windswept", "Lofty" },
                nouns = new[] { "Perch", "Temple", "Spire", "Landing", "Isle" } },
        new() { world = 5, levels = 22, difficulty = 4,
                newAnimals = new[] { AnimalType.Gerald }, oldAnimals = new[] { AnimalType.Horace, AnimalType.Ducky, AnimalType.Woolly, AnimalType.Bessie },
                bossName = "The Deep Dredger",
                adjectives = new[] { "Coral", "Sunken", "Kelp", "Tidal", "Abyssal", "Barnacle" },
                nouns = new[] { "Arch", "Reef", "Vault", "Trench", "Ruins" } },
        new() { world = 6, levels = 16, difficulty = 5,
                newAnimals = new[] { AnimalType.Billy }, oldAnimals = new[] { AnimalType.Gerald, AnimalType.Horace, AnimalType.Ducky, AnimalType.Bessie },
                bossName = "The Factory Machine",
                adjectives = new[] { "Hangar", "Reactor", "Orbital", "Plasma", "Command", "Gravity" },
                nouns = new[] { "Deck", "Bay", "Core", "Bridge", "Hold" } },
        new() { world = 7, levels = 12, difficulty = 3, newAnimals = new AnimalType[0], oldAnimals = W1,
                bossName = "The Drill Rig's Quarry",
                adjectives = new[] { "Dusty", "Tumbleweed", "Canyon", "Sunbaked", "Mesa", "Rattlesnake" },
                nouns = new[] { "Ranch", "Gulch", "Pass", "Mine", "Corral" } },
        new() { world = 8, levels = 12, difficulty = 3, newAnimals = new AnimalType[0], oldAnimals = W1,
                bossName = "The Ringmaster's Ring",
                adjectives = new[] { "Carousel", "Big Top", "Popcorn", "Ferris", "Midway", "Candy" },
                nouns = new[] { "Stand", "Alley", "Fair", "Stage", "Row" } },
        new() { world = 9, levels = 12, difficulty = 3, newAnimals = new AnimalType[0], oldAnimals = W1,
                bossName = "Harvester Prime",
                adjectives = new[] { "Conveyor", "Silo", "Assembly", "Hopper", "Gearworks", "Cannery" },
                nouns = new[] { "Line", "Floor", "Shed", "Works", "Depot" } },
    };

    public static void GenerateAll()
    {
        var templates = Templates.Select(LoadTemplate).ToArray();
        var boss = LoadTemplate(BossTemplate);
        if (templates.Any(t => t == null) || boss == null)
        {
            Debug.LogError("[FarmFury] WorldLevelGenerator: World 1 templates missing — generate L01-L18 first.");
            return;
        }

        foreach (var guid in AssetDatabase.FindAssets("t:LevelData", new[] { Folder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            // Its own files only: World 2 ("W02_") is designed by tools/design_world2.py (2026-11-09).
            string fn = System.IO.Path.GetFileName(path);
            if (fn.StartsWith("W") && !fn.StartsWith("W02_") && !fn.StartsWith("W03_") && !fn.StartsWith("W04_") && !fn.StartsWith("W05_") && !fn.StartsWith("W06_")
                && !fn.StartsWith("W07_") && !fn.StartsWith("W08_") && !fn.StartsWith("W09_")) AssetDatabase.DeleteAsset(path);
        }

        int made = 0;
        foreach (var spec in Worlds)
        {
            // World 2 is designed by hand on the framed template (tools/design_world2.py, 2026-11-09).
            // World 3 likewise (tools/design_world3.py, 2026-11-10).
            // World 4 likewise (tools/design_world4.py, 2026-11-11).
            // World 5 likewise (tools/design_world5.py, 2026-11-12), World 6 too (tools/design_world6.py, 2026-11-12).
            // The paid worlds 7-9 likewise (tools/design_world7.py / 8 / 9, 2026-11-12): nothing is generated any more.
            if (spec.world >= 2 && spec.world <= 9) continue;
            var names = LevelNames(spec);
            int regular = spec.levels - 1;
            for (int k = spec.firstLevel; k <= regular; k++)
            {
                int t = regular <= 1 ? 0 : Mathf.RoundToInt((k - 1) * (Templates.Length - 1f) / (regular - 1f));
                Create(spec, k, names[k - 1], templates[t], boss: false);
                made++;
            }
            Create(spec, spec.levels, spec.bossName, boss, boss: true);
            made++;
        }
        Debug.Log($"[FarmFury] WorldLevelGenerator: created {made} levels for Worlds 7-9.");
    }

    static LevelData LoadTemplate(string file) =>
        AssetDatabase.LoadAssetAtPath<LevelData>($"{Folder}/{file}.asset");

    static void Create(WorldSpec spec, int k, string name, LevelData template, bool boss)
    {
        var rng = new System.Random(spec.world * 1000 + k);
        float stoneChance = 0.03f * spec.difficulty;
        float tierChance  = 0.07f * spec.difficulty;

        // Multi-room World 1 templates (2026-11-12): these worlds copy one room only, as before the rooms existed - the
        // first room, or for the boss template its last room (the keep moved there), shifted back to room 0's frame.
        int useRoom = boss ? template.RoomCount - 1 : 0;
        float roomDx = -useRoom * LevelData.RoomStride;
        var blocks = template.blocks.Where(b => b.room == useRoom).Select(b => { b.position.x += roomDx; b.room = 0; return b; }).ToArray();
        var robots = template.robots.Where(r => r.room == useRoom).Select(r => { r.position.x += roomDx; r.room = 0; return r; }).ToArray();
        var platforms = (template.platforms ?? new LevelData.PlatformSpawnData[0])
            .Where(p => p.room == useRoom).Select(p => { p.x += roomDx; return p; }).ToArray();

        // Bounds of the whole tower (blocks and robots), for mirroring and shifting.
        var xs = blocks.Select(b => b.position.x).Concat(robots.Select(r => r.position.x)).ToList();
        float minX = xs.Min(), maxX = xs.Max(), centre = (minX + maxX) * 0.5f;

        bool mirror = !boss && (k + spec.world) % 2 == 1;
        float shift = boss ? 0f : (float)(rng.NextDouble() * 0.7 - 0.35);
        shift = Mathf.Clamp(shift, MinX - minX, MaxX - maxX);

        float MapX(float x) => (mirror ? 2f * centre - x : x) + shift;
        // A template framed by ReferenceFraming stands on World 1's shared ground line (y=-5.6); these worlds
        // still use the original physics ground (-6.6, the Launcher anchor), so drop it back onto that.
        float dy = template.referenceFraming ? -6.6f - ReferenceFraming.World1GroundY : 0f;
        // Generated levels don't carry the template's raised islands (cornfield art, 2026-11-08): anything standing
        // on one drops by the island's height back onto the template's ground line, as before the islands existed.
        float IslandLift(float x, float y)
        {
            foreach (var p in platforms)
                if (Mathf.Abs(x - p.x) <= p.width * 0.5f && y > p.surfaceY - 0.1f)
                    return p.surfaceY - ReferenceFraming.World1GroundY;
            return 0f;
        }

        for (int i = 0; i < blocks.Length; i++)
        {
            var b = blocks[i];
            b.position.y -= IslandLift(b.position.x, b.position.y);
            b.position.x = MapX(b.position.x);
            b.position.y += dy;
            b.flipX = mirror;
            if (mirror) b.rotation = -b.rotation;   // a mirrored tower leans the other way
            if (!boss && b.type == BlockType.Wood && StoneHasShape(b.artVariant) && rng.NextDouble() < stoneChance)
                b.type = BlockType.Stone;
            blocks[i] = b;
        }
        for (int i = 0; i < robots.Length; i++)
        {
            var r = robots[i];
            r.position.y -= IslandLift(r.position.x, r.position.y);
            r.position.x = MapX(r.position.x);
            r.position.y += dy;
            if (!boss && rng.NextDouble() < tierChance)
                r.robotType = r.robotType switch
                {
                    RobotType.SemiHarvester => RobotType.Harvester,
                    RobotType.Harvester     => RobotType.Basic,
                    _                       => r.robotType,
                };
            robots[i] = r;
        }

        string file = $"W{spec.world:00}_L{k:00}_{Sanitize(name)}";
        var asset = ScriptableObject.CreateInstance<LevelData>();
        asset.levelId     = $"W{spec.world}_L{k:00}";
        asset.levelName   = name;
        asset.parBirds    = template.parBirds;
        asset.birds       = Lineup(spec, k, template.birds, boss);
        asset.blocks      = blocks;
        asset.robots      = robots;
        asset.world       = spec.world;
        asset.isBossLevel = boss;
        // Framed templates are laid out by the blocks' visible edges: keep the colliders trimmed to the art. Not World 2,
        // whose ice reskin has different padding (its blocks would sit over gaps and drop at load).
        asset.trimColliders = template.referenceFraming && spec.world != 2;
        AssetDatabase.CreateAsset(asset, $"{Folder}/{file}.asset");
    }

    static bool StoneHasShape(WoodArtVariant v) =>
        v == WoodArtVariant.Vertical || v == WoodArtVariant.Horizontal ||
        v == WoodArtVariant.Skew     || v == WoodArtVariant.Diagonal;

    // Same number of animals as the World 1 tower (the boss gets one more). The world's new animals lead;
    // where World 1 used Bessie (the heavy hitter) the slot stays a heavy animal from the world's pool.
    static AnimalType[] Lineup(WorldSpec spec, int k, AnimalType[] templateBirds, bool boss)
    {
        // Never more than three (LevelData.MaxLineup, 2026-11-09): boss levels used to get a fourth animal.
        int count = Mathf.Min(LevelData.MaxLineup, templateBirds.Length + (boss ? 1 : 0));
        var lineup = new List<AnimalType>();
        for (int i = 0; i < count; i++)
        {
            bool heavy = i < templateBirds.Length && templateBirds[i] == AnimalType.Bessie;
            if (spec.newAnimals.Length == 0)
                lineup.Add(i < templateBirds.Length ? templateBirds[i] : AnimalType.Bessie);
            else if (heavy || (boss && i == count - 1))
                lineup.Add(spec.oldAnimals[(k + i) % spec.oldAnimals.Length]);
            else
                lineup.Add(spec.newAnimals[(k + i) % spec.newAnimals.Length]);
        }
        return lineup.ToArray();
    }

    // Unique "<adjective> <noun>" names, in a fixed shuffled order per world.
    static List<string> LevelNames(WorldSpec spec)
    {
        var all = new List<string>();
        foreach (var a in spec.adjectives)
            foreach (var n in spec.nouns)
                all.Add($"{a} {n}");
        var rng = new System.Random(spec.world * 7919);
        for (int i = all.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (all[i], all[j]) = (all[j], all[i]);
        }
        return all;
    }

    static string Sanitize(string name) =>
        new string(name.Where(char.IsLetterOrDigit).ToArray());
}
