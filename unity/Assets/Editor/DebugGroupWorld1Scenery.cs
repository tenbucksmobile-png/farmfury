using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// One-off scene-restructuring helper — added 2026-07-19, user request: group World 1's
// hand-placed decorative props under a single "Scenery_World1" parent so they can all be
// hidden/shown at once (e.g. via that one GameObject's Scene Visibility toggle) while building
// World 2 content against the new Preview World 2 Backdrop tool, instead of having to toggle 18
// separate top-level objects individually.
//
// Uses GameObject.transform.SetParent(..., worldPositionStays: true) — the real Unity API, not
// hand-edited scene YAML — so every prop's actual world position/rotation/scale is byte-for-byte
// preserved; this is purely a hierarchy reorganization, not a content change. Idempotent — safe
// to run more than once (SetParent onto an already-correct parent is a no-op).
//
// Deliberately a SEPARATE top-level GameObject from the existing "Scenery" GameObject (the
// SceneryBuilder-owned one) rather than nested under it — that one is script-managed (its own
// ClearProps()/rebuild cycle only ever touches props IT spawned, tracked in its own _props list),
// so mixing permanent hand-placed art into that hierarchy would be a semantic mismatch even
// though it wouldn't actually break ClearProps() itself.
//
// The exact 18 names below were read directly from the current Game.unity (grep, not guessed
// from CLAUDE.md history, which had listed a "GnarledTree"/"WoodenCart"/"Haybail" that don't
// currently exist as scene GameObjects — only as unused SceneryBuilder sprite-field references).
public static class DebugGroupWorld1Scenery
{
    static readonly string[] SceneryNames =
    {
        "Windmill", "OldBarn_Right", "OakTree",
        "WoodenFence", "WoodenFence (1)", "WoodenFence (2)",
        "Rock", "Rock (1)",
        "GrassTuft", "GrassTuft (3)", "GrassTuft (4)", "GrassTuft (5)", "GrassTuft (6)",
        "WildFlowers", "WildFlowers (1)",
        "WoodenBarrel", "WoodenBarrel (1)", "WoodenBarrel (2)",
    };

    // Batch entry point (Unity.exe -batchmode -executeMethod DebugGroupWorld1Scenery.RunBatch)
    // — opens Game.unity itself and saves at the end, unlike the interactive menu item below
    // which assumes the scene is already open (and deliberately does NOT force a reload, so it
    // never risks discarding unsaved interactive edits).
    public static void RunBatch()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/Game.unity", OpenSceneMode.Single);
        GroupWorld1Scenery();
        EditorSceneManager.SaveScene(scene);
    }

    [MenuItem("FarmFury/Debug/Group World 1 Scenery Under Scenery_World1")]
    public static void GroupWorld1Scenery()
    {
        var parentGO = GameObject.Find("Scenery_World1");
        if (parentGO == null)
        {
            parentGO = new GameObject("Scenery_World1");
            parentGO.transform.position = Vector3.zero;
            Debug.Log("[FarmFury] Created 'Scenery_World1' GameObject.");
        }

        int moved = 0, alreadyParented = 0, missing = 0;
        foreach (var name in SceneryNames)
        {
            var go = GameObject.Find(name);
            if (go == null)
            {
                Debug.LogWarning($"[FarmFury] Scenery prop not found in scene: '{name}' — skipped.");
                missing++;
                continue;
            }
            if (go.transform.parent == parentGO.transform)
            {
                alreadyParented++;
                continue;
            }
            go.transform.SetParent(parentGO.transform, worldPositionStays: true);
            moved++;
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log($"[FarmFury] Scenery_World1: moved {moved}, already parented {alreadyParented}, missing {missing} (of {SceneryNames.Length} expected). Save the scene (Ctrl+S) to keep this.");
    }
}
