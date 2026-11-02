#if UNITY_EDITOR || DEVELOPMENT_BUILD
using FarmFury.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FarmFury.UI
{
    /// <summary>
    /// F1 test panel (franchise convention, a small IMGUI take on Stampede's DebugPanel). Editor and development
    /// builds only — compiled out of release builds. Coins, Unlock All Levels, completing / clearing the current
    /// world, Remove Ads and paid-world ownership toggles, and Reset Progress, then refreshes whichever menu shows.
    /// </summary>
    public class DebugPanel : MonoBehaviour
    {
        private bool _open;
        private Rect _window = new(20f, 20f, 420f, 520f);

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.f1Key.wasPressedThisFrame) { _open = !_open; }
        }

        private void OnGUI()
        {
            if (!_open) { return; }
            float scale = Screen.height / 720f;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            _window = GUILayout.Window(4242, _window, Draw, "FarmFury debug (F1)");
        }

        private void Draw(int id)
        {
            var save = SaveManager.Instance;
            var flow = GameFlow.Instance;
            if (save == null || flow == null) { GUILayout.Label("No SaveManager / GameFlow."); return; }
            var world = flow.CurrentWorld;

            GUILayout.Label($"Coins: {save.CoinBalance}   World: {world}");
            if (GUILayout.Button("Coins +1000")) { save.DebugAddCoins(1000); }
            if (GUILayout.Button($"Unlock All Levels: {(save.DebugAllLevelsOpen ? "ON" : "OFF")}"))
            {
                save.DebugAllLevelsOpen = !save.DebugAllLevelsOpen;
            }
            if (GUILayout.Button($"Complete {world}'s levels (1 star)")) { save.DebugCompleteWorldLevels(world); }
            if (GUILayout.Button($"{world} boss cleared: {save.IsWorldBossCleared(world)}"))
            {
                save.SetWorldBossCleared(world, !save.IsWorldBossCleared(world));
                save.SaveProgress();
            }
            if (GUILayout.Button($"Remove Ads owned: {save.AdsRemoved}")) { save.AdsRemoved = !save.AdsRemoved; }
            foreach (var paid in IAPManager.PurchasableWorlds())
            {
                bool bought = save.IsWorldPurchased(paid);
                if (GUILayout.Button($"{paid} bought: {bought}")) { save.DebugSetWorldPurchased(paid, !bought); }
            }
            GUILayout.Space(10f);
            if (GUILayout.Button("Reset progress (keeps coins/purchases)")) { save.DebugResetProgress(); }
            if (GUI.changed) { flow.RefreshCurrentScreen(); }
            GUI.DragWindow();
        }
    }
}
#endif
