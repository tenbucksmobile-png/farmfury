using FarmFury.Core;
using FarmFury.UI;
using UnityEditor;
using UnityEngine;

// QA shortcuts (Play mode only). Rewritten 2026-11-02 for the franchise flow: the old World 1/World 2 map and
// World 2 landing previews are gone with those screens; World Select / Level Select now cover both worlds.
public static class DebugJumpToLevel
{
    // Jumps straight to L18 loaded and ready to play — clear it to test the boss clear -> transition video ->
    // World Select (Frozen Tundra unlocked) path for real.
    // Purchases (2026-11-13, user: "a function in debug that gives me all the purchases of trails, cannons etc").
    // Work in or out of Play mode (they write the save directly); in Play mode the menus showing are refreshed, and the
    // Locker / shop pages read ownership each time they open.
    [MenuItem("FarmFury/Debug/Purchases: Own All Cannons, Trails and Impacts")]
    public static void OwnAllCosmetics() { SaveManager.DebugSetAllCosmeticsOwned(true); RefreshMenus(); }

    [MenuItem("FarmFury/Debug/Purchases: Own Everything (cosmetics, paid worlds, Remove Ads)")]
    public static void OwnEverything() { SaveManager.DebugOwnEverything(); RefreshMenus(); }

    [MenuItem("FarmFury/Debug/Purchases: Remove All Cannons, Trails and Impacts")]
    public static void RemoveAllCosmetics() { SaveManager.DebugSetAllCosmeticsOwned(false); RefreshMenus(); }

    static void RefreshMenus()
    {
        if (Application.isPlaying && GameFlow.Instance != null) GameFlow.Instance.RefreshCurrentScreen();
    }

    [MenuItem("FarmFury/Debug/Jump To Level 18 (test World 2 transition)")]
    public static void JumpToLevel18()
    {
        if (!RequirePlayMode()) return;
        GameManager.Instance.ForceStartLevel(17); // 0-based — L18
        Debug.Log("[FarmFury] Jumped to L18 (Captain's Last Stand). Clear it to test the World 2 transition.");
    }

    [MenuItem("FarmFury/Debug/Open World Select")]
    public static void OpenWorldSelect()
    {
        if (!RequirePlayMode() || GameFlow.Instance == null) return;
        GameFlow.Instance.EnterWorldSelect();
    }

    [MenuItem("FarmFury/Debug/Open Level Select: Meadow Ruins")]
    public static void OpenLevelSelectWorld1()
    {
        if (!RequirePlayMode() || GameFlow.Instance == null) return;
        GameFlow.Instance.EnterLevelSelect(WorldType.MeadowRuins);
    }

    [MenuItem("FarmFury/Debug/Open Level Select: Frozen Tundra")]
    public static void OpenLevelSelectWorld2()
    {
        if (!RequirePlayMode() || GameFlow.Instance == null) return;
        GameFlow.Instance.EnterLevelSelect(WorldType.FrozenTundra);
    }

    // Stampede's "Unlock All Levels" switch: every world and level opens without marking anything completed
    // (SaveManager.DebugAllLevelsOpen; Editor/development builds only). Works in or out of Play mode — it's a pref.
    const string UnlockAllMenu = "FarmFury/Debug/Unlock All Levels";

    [MenuItem(UnlockAllMenu)]
    public static void ToggleUnlockAll()
    {
        bool on = PlayerPrefs.GetInt(SaveManager.DebugAllLevelsOpenKey, 0) == 1;
        PlayerPrefs.SetInt(SaveManager.DebugAllLevelsOpenKey, on ? 0 : 1);
        PlayerPrefs.Save();
        Debug.Log($"[FarmFury] Unlock All Levels: {(on ? "OFF" : "ON")}.");
        if (Application.isPlaying && GameFlow.Instance != null) GameFlow.Instance.RefreshCurrentScreen();
    }

    [MenuItem(UnlockAllMenu, true)]
    static bool ToggleUnlockAllValidate()
    {
        Menu.SetChecked(UnlockAllMenu, PlayerPrefs.GetInt(SaveManager.DebugAllLevelsOpenKey, 0) == 1);
        return true;
    }

    static bool RequirePlayMode()
    {
        if (!Application.isPlaying || GameManager.Instance == null)
        {
            Debug.LogWarning("[FarmFury] This only works in Play mode — press Play first.");
            return false;
        }
        return true;
    }
}
