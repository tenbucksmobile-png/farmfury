using FarmFury.Shared.UI;
using UnityEngine;

namespace FarmFury.UI
{
    /// <summary>
    /// Cannonfire's settings for the shared UI kit (com.farmfury.core), applied before any scene loads and, in the
    /// Editor, on every script reload (so Editor tools that build screens see the same values).
    /// </summary>
    public static class SharedUISettings
    {
        /// <summary>Cannonfire's round menu buttons: 112 (user, 2026-11-11: 160 -> 136 -> 112, "buttons too big").</summary>
        public const float RoundButtonSize = 112f;

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
#endif
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Apply()
        {
            UIKit.RoundButtonSize = RoundButtonSize;
        }
    }
}
