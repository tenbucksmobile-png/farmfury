// FarmFury — Editor utility. Run via menu: FarmFury ▶ Debug ▶ Dump Level Layout To Log
//
// Lets a level be designed by drag-and-dropping real block/robot prefabs into the Scene view
// (Edit mode, NOT Play mode) instead of hand-typing coordinates into LevelDataGenerator.cs. This
// scans whatever's currently in the open scene, reads each object's actual transform, and prints
// ready-to-paste B(...)/R(...) code lines in the exact format LevelDataGenerator.Make() expects —
// pixel/unit-exact to wherever the object was dropped, no manual coordinate math needed.
//
// WORKFLOW — two supported ways to place objects, freely mixed in the same pass:
//   A) Drag real prefabs (WoodBlock.prefab / StoneBlock.prefab / HaybaleBlock.prefab /
//      Robot.prefab / HarvesterRobot.prefab from Assets/Prefabs/) into the Scene view. These are
//      found anywhere in the scene via their BlockBase/RobotEnemy component.
//   B) Drag raw sprite assets (Haybail.png, Block_Stone_Normal.png, Plank_Horizontal.png,
//      Robot_Idle.png, HarvesterRobot.png, etc. — no prefab needed) straight from the Project
//      window. These must be parented under an empty GameObject named exactly "LevelScratch"
//      (create one via GameObject -> Create Empty, rename it) so the dumper can tell "a sprite
//      I'm designing a level with" apart from ordinary scene art (background, scenery props,
//      HUD) without accidentally sweeping up the whole scene. Type is inferred from the sprite's
//      name (case-insensitive keyword match — "hay"->Haybale, "stone"->Stone, "wood"/"plank"->
//      Wood, "harvester"->Harvester robot, "robot"->Basic robot); unrecognised sprite names are
//      skipped with a warning telling you what didn't match.
//
// Either way:
//   1. Open Game.unity in Edit mode (Play mode is NOT required — positions are read from the
//      scene as authored, not from a running simulation). Position/scale by eye relative to the
//      existing Ground/Launcher.
//   2. Run this menu command. It writes unity/Logs/level_layout_dump.txt AND logs to the Console.
//   3. Paste the output into a new Make("LXX_Name", ...) call in LevelDataGenerator.cs, then run
//      FarmFury -> Generate All Level Data + Wire Scene References.
//   4. Delete the "LevelScratch" container from the scene afterward — IMPORTANT, not optional:
//      unlike a real prefab instance, it is NOT tied to LevelLoader's per-level spawn/clear
//      cycle, so if left in place it renders permanently regardless of which level is actually
//      loaded, visually bleeding into every other level ("level 1 and level 2 compiled over
//      each other" — real symptom hit 2026-07-09 after leaving L02's design sprites behind).
//      SceneSetup's "Wire Scene References" pass now auto-deletes any GameObject literally
//      named "LevelScratch" as a safety net (same list as its other placeholder cleanup), but
//      don't rely on remembering to run that — delete it yourself right after a successful dump.
//
// TYPE DETECTION (prefab path, A): identifies each object's BlockType/RobotType from which SOURCE
// PREFAB it's an instance of (via PrefabUtility), not from its component type — HaybaleBlock and
// WoodBlock both use the WoodBlock component (see CLAUDE.md), so component type alone can't tell
// them apart. Objects that aren't instances of one of the five known prefabs are skipped.
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
            // "SemiHarvesterRobot" must be checked before "HarvesterRobot" — it contains
            // "HarvesterRobot" as a substring, so the reverse order would misclassify it (same
            // class of bug as the sprite-name check above).
            RobotType? type = prefabPath switch
            {
                string p when p.Contains("SemiHarvesterRobot") => RobotType.SemiHarvester,
                string p when p.Contains("HarvesterRobot")     => RobotType.Harvester,
                string p when p.Contains("Robot")              => RobotType.Basic,
                _                                              => null,
            };
            if (type == null)
            {
                Debug.LogWarning($"[LevelLayoutDumper] Skipping '{robot.name}' — not an instance of a known robot prefab ({prefabPath}).");
                skipped++;
                continue;
            }

            Vector3 pos   = robot.transform.position;
            Vector3 scale = robot.transform.localScale;

            robotLines.Add(type switch
            {
                RobotType.Harvester     => $"R({F(pos.x)}f, {F(pos.y)}f, {F(scale.x)}f, {F(scale.y)}f, RobotType.Harvester),",
                RobotType.SemiHarvester => $"R({F(pos.x)}f, {F(pos.y)}f, {F(scale.x)}f, {F(scale.y)}f, RobotType.SemiHarvester),",
                _                       => $"R({F(pos.x)}f, {F(pos.y)}f),",
            });
        }

        // Path B — raw sprites dropped under a "LevelScratch" empty GameObject. Only scanned
        // inside that container so ordinary scene art (background, scenery, HUD) is never swept
        // up by name-matching alone.
        var scratchRoot = GameObject.Find("LevelScratch");
        if (scratchRoot != null)
        {
            foreach (var sr in scratchRoot.GetComponentsInChildren<SpriteRenderer>(includeInactive: false))
            {
                if (sr.sprite == null) continue;
                // Already handled by the BlockBase/RobotEnemy loops above if this object is a
                // real prefab instance — don't double-count it here.
                if (sr.GetComponent<BlockBase>() != null || sr.GetComponent<RobotEnemy>() != null) continue;

                string spriteName = sr.sprite.name.ToLowerInvariant();
                Vector3 pos   = sr.transform.position;
                Vector3 scale = sr.transform.localScale;

                if (spriteName.Contains("hay"))
                    blockLines.Add($"B(BlockType.Haybale, {F(pos.x)}f, {F(pos.y)}f, {F(scale.x)}f, {F(scale.y)}f, passThrough: true, hp: 10f, mass: 3f), // sprite '{sr.sprite.name}'");
                else if (spriteName.Contains("stone"))
                    blockLines.Add($"B(BlockType.Stone, {F(pos.x)}f, {F(pos.y)}f, {F(scale.x)}f, {F(scale.y)}f), // sprite '{sr.sprite.name}'");
                else if (spriteName.Contains("wood") || spriteName.Contains("plank"))
                    blockLines.Add($"B(BlockType.Wood, {F(pos.x)}f, {F(pos.y)}f, {F(scale.x)}f, {F(scale.y)}f), // sprite '{sr.sprite.name}'");
                // "semiharvest" must be checked before "harvester"/"robot" — "Robot_SemiHarvest"
                // contains both "robot" and (once the underscore is stripped by ToLowerInvariant
                // leaving "semiharvest") no "harvester" substring, but it DOES contain "robot",
                // so the generic "robot" check below would wrongly claim it as Basic if checked
                // first (this was a real bug — first found via an actual level-2 dump 2026-07-09).
                else if (spriteName.Contains("semiharvest"))
                    robotLines.Add($"R({F(pos.x)}f, {F(pos.y)}f, {F(scale.x)}f, {F(scale.y)}f, RobotType.SemiHarvester), // sprite '{sr.sprite.name}'");
                else if (spriteName.Contains("harvester"))
                    robotLines.Add($"R({F(pos.x)}f, {F(pos.y)}f, {F(scale.x)}f, {F(scale.y)}f, RobotType.Harvester), // sprite '{sr.sprite.name}'");
                else if (spriteName.Contains("robot"))
                    robotLines.Add($"R({F(pos.x)}f, {F(pos.y)}f), // sprite '{sr.sprite.name}'");
                else
                {
                    Debug.LogWarning($"[LevelLayoutDumper] Skipping '{sr.gameObject.name}' under LevelScratch — sprite name '{sr.sprite.name}' doesn't match a known keyword (hay/stone/wood/plank/robot/harvester/semiharvest).");
                    skipped++;
                }
            }
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
