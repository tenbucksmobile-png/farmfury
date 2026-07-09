// FarmFury — Editor utility. Run via menu: FarmFury ▶ Debug ▶ Dump Level Layout To Log
//
// Lets a level be designed by drag-and-dropping real block/robot prefabs into the Scene view
// (Edit mode, NOT Play mode) instead of hand-typing coordinates into LevelDataGenerator.cs. This
// scans whatever's currently in the open scene, reads each object's actual transform, and prints
// ready-to-paste B(...)/R(...) code lines in the exact format LevelDataGenerator.Make() expects —
// pixel/unit-exact to wherever the object was dropped, no manual coordinate math needed.
//
// WORKFLOW:
//   1. Open Game.unity in Edit mode (Play mode is NOT required — positions are read from the
//      scene as authored, not from a running simulation).
//   2. Drag WoodBlock.prefab / StoneBlock.prefab / HaybaleBlock.prefab / Robot.prefab /
//      HarvesterRobot.prefab from Assets/Prefabs/ into the Scene view, positioning and scaling
//      them by eye relative to the existing Ground/Launcher.
//   3. Run this menu command. It writes unity/Logs/level_layout_dump.txt AND logs to the Console.
//   4. Paste the output into a new Make("LXX_Name", ...) call in LevelDataGenerator.cs, then run
//      FarmFury -> Generate All Level Data + Wire Scene References.
//   5. Delete the scratch objects from the scene afterward (or leave them — Generate All Level
//      Data reads from LevelDataGenerator.cs, not from stray scene objects, and LevelLoader only
//      ever spawns its own prefab instances at runtime under BlockParent/RobotParent — hand-placed
//      scratch objects sitting elsewhere in the scene have no gameplay effect).
//
// TYPE DETECTION: identifies each object's BlockType/RobotType from which SOURCE PREFAB it's an
// instance of (via PrefabUtility), not from its component type — HaybaleBlock and WoodBlock both
// use the WoodBlock component (see CLAUDE.md), so component type alone can't tell them apart.
// Objects that aren't instances of one of the five known prefabs are skipped with a warning.
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class LevelLayoutDumper
{
    const string LogPath = "Logs/level_layout_dump.txt";

    [MenuItem("FarmFury/Debug/Dump Level Layout To Log")]
    public static void Dump()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("FarmFury",
                "Exit Play mode first — this reads hand-placed Edit-mode positions, not a running simulation.",
                "OK");
            return;
        }

        var blockLines = new List<string>();
        var robotLines = new List<string>();
        int skipped = 0;

        foreach (var block in Object.FindObjectsByType<BlockBase>(FindObjectsInactive.Exclude))
        {
            string prefabPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(block.gameObject);
            BlockType? type = prefabPath switch
            {
                string p when p.Contains("HaybaleBlock") => BlockType.Haybale,
                string p when p.Contains("StoneBlock")   => BlockType.Stone,
                string p when p.Contains("WoodBlock")    => BlockType.Wood,
                _                                        => null,
            };
            if (type == null)
            {
                Debug.LogWarning($"[LevelLayoutDumper] Skipping '{block.name}' — not an instance of a known block prefab ({prefabPath}).");
                skipped++;
                continue;
            }

            Vector3 pos   = block.transform.position;
            Vector3 scale = block.transform.localScale;

            if (type == BlockType.Haybale)
                blockLines.Add($"B(BlockType.Haybale, {F(pos.x)}f, {F(pos.y)}f, {F(scale.x)}f, {F(scale.y)}f, passThrough: true, hp: 10f, mass: 3f),");
            else
                blockLines.Add($"B(BlockType.{type}, {F(pos.x)}f, {F(pos.y)}f, {F(scale.x)}f, {F(scale.y)}f),");
        }

        foreach (var robot in Object.FindObjectsByType<RobotEnemy>(FindObjectsInactive.Exclude))
        {
            string prefabPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(robot.gameObject);
            RobotType? type = prefabPath switch
            {
                string p when p.Contains("HarvesterRobot") => RobotType.Harvester,
                string p when p.Contains("Robot")          => RobotType.Basic,
                _                                          => null,
            };
            if (type == null)
            {
                Debug.LogWarning($"[LevelLayoutDumper] Skipping '{robot.name}' — not an instance of a known robot prefab ({prefabPath}).");
                skipped++;
                continue;
            }

            Vector3 pos   = robot.transform.position;
            Vector3 scale = robot.transform.localScale;

            robotLines.Add(type == RobotType.Harvester
                ? $"R({F(pos.x)}f, {F(pos.y)}f, {F(scale.x)}f, {F(scale.y)}f, RobotType.Harvester),"
                : $"R({F(pos.x)}f, {F(pos.y)}f),");
        }

        var sb = new StringBuilder();
        sb.AppendLine("// ---- Paste below into a Make(\"LXX_Name\", ...) call in LevelDataGenerator.cs ----");
        sb.AppendLine("blocks: new[]");
        sb.AppendLine("{");
        foreach (var line in blockLines) sb.AppendLine("    " + line);
        sb.AppendLine("},");
        sb.AppendLine("robots: new[]");
        sb.AppendLine("{");
        foreach (var line in robotLines) sb.AppendLine("    " + line);
        sb.AppendLine("}");
        sb.AppendLine("// ---------------------------------------------------------------------------");

        string output = sb.ToString();
        File.WriteAllText(LogPath, output);
        Debug.Log($"[LevelLayoutDumper] Dumped {blockLines.Count} block(s), {robotLines.Count} robot(s), skipped {skipped} unrecognised object(s).\n{output}");

        EditorUtility.DisplayDialog("FarmFury",
            $"Dumped {blockLines.Count} block(s) and {robotLines.Count} robot(s) to:\n{Path.GetFullPath(LogPath)}\n\n" +
            (skipped > 0 ? $"Skipped {skipped} object(s) that weren't instances of a known prefab (see Console)." : "Also printed to the Console."),
            "OK");
    }

    // 3-decimal precision, matching the existing hand-written coordinates in LevelDataGenerator.cs.
    static string F(float v) => v.ToString("0.###", CultureInfo.InvariantCulture);
}
