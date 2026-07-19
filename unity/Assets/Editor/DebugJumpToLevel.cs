using UnityEditor;
using UnityEngine;

// Quick QA helper — added 2026-07-19 so the L18 -> Frozen Tundra transition flow (World2Landing
// -> World2Map) can be tested without first grinding through L01-L17 on the World 1 map to
// naturally unlock L18. Only works in Play mode (GameManager.Instance is null otherwise).
// Jumps straight to L18 loaded and ready to play — you still need to actually clear it (destroy
// all robots) to trigger the real completion sequence (celebration -> transition video ->
// World2LandingController), since that's the thing being tested, not skipped.
public static class DebugJumpToLevel
{
    [MenuItem("FarmFury/Debug/Jump To Level 18 (test World 2 transition)")]
    public static void JumpToLevel18()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[FarmFury] Jump To Level 18 only works in Play mode — press Play first.");
            return;
        }
        if (GameManager.Instance == null)
        {
            Debug.LogWarning("[FarmFury] GameManager.Instance is null — is the Game scene running?");
            return;
        }
        GameManager.Instance.ForceStartLevel(17); // 0-based — L18
        Debug.Log("[FarmFury] Jumped to L18 (Captain's Last Stand). Clear it to test the World 2 transition.");
    }

    // Instant preview of World2LandingController (FarmFury_W2.png) without playing/clearing L18
    // — skips the transition video entirely, just shows the destination screen directly.
    [MenuItem("FarmFury/Debug/Show World 2 Landing")]
    public static void ShowWorld2Landing()
    {
        if (!RequirePlayMode()) return;
        World2LandingController.Instance?.Show();
        Debug.Log("[FarmFury] Showing World2LandingController directly.");
    }

    // Instant preview of World2MapController (FrozenTundra.png + 22 markers) — same shortcut,
    // one level deeper. Tap marker 1 (the only unlocked one by default) to see the new
    // Matchup_W2.png background + FrostRobot/GlacierRobot/IceHarvestor/Percy/Woolly cards.
    [MenuItem("FarmFury/Debug/Show World 2 Map")]
    public static void ShowWorld2Map()
    {
        if (!RequirePlayMode()) return;
        World2MapController.Instance?.Show();
        Debug.Log("[FarmFury] Showing World2MapController directly — tap marker 1 to preview the match-up screen.");
    }

    static bool RequirePlayMode()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[FarmFury] This only works in Play mode — press Play first.");
            return false;
        }
        return true;
    }
}
