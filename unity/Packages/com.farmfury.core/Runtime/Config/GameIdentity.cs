using UnityEngine;

namespace FarmFury.Shared.Config
{
    /// <summary>
    /// What makes one Farm Fury game different from another, for shared code: one asset per game
    /// (Create > Farm Fury > Game Identity). Values must match what the game already uses in the stores and in
    /// players' saves - never change them for a live game.
    /// </summary>
    [CreateAssetMenu(menuName = "Farm Fury/Game Identity", fileName = "GameIdentity")]
    public class GameIdentity : ScriptableObject
    {
        [Tooltip("Short lowercase id, e.g. arcade, stampede, cannonfire, rampage. Used in analytics and cross-promo.")]
        public string gameId;

        [Tooltip("Name shown to players, e.g. Farm Fury Stampede.")]
        public string displayName;

        [Tooltip("PlayerPrefs key prefix already used by the game's saves: Arcade FFA_, Stampede FFS_, Cannonfire FF_.")]
        public string savePrefix;

        [Tooltip("Store product id prefix: stampede_, farmfury_ (Cannonfire), empty for Arcade's bare ids.")]
        public string storeIdPrefix;

        [Tooltip("App Store numeric id (cross-promo links), e.g. 6825786519 for Stampede. Empty if not published.")]
        public string appleAppId;

        [Tooltip("Android package / bundle id, e.g. com.farmfury.stampede.")]
        public string androidPackage;

        /// <summary>The full PlayerPrefs key for <paramref name="key"/> (savePrefix + key).</summary>
        public string SaveKey(string key) => (savePrefix ?? string.Empty) + key;

        /// <summary>The store's product id for an internal product id (storeIdPrefix + id).</summary>
        public string StoreId(string productId) => (storeIdPrefix ?? string.Empty) + productId;

        /// <summary>False (with a reason) when a required field is missing.</summary>
        public bool IsValid(out string problem)
        {
            if (string.IsNullOrWhiteSpace(gameId)) { problem = "gameId is empty"; return false; }
            if (string.IsNullOrWhiteSpace(savePrefix)) { problem = "savePrefix is empty"; return false; }
            if (string.IsNullOrWhiteSpace(androidPackage)) { problem = "androidPackage is empty"; return false; }
            problem = null;
            return true;
        }
    }
}
