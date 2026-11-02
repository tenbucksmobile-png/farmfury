using System;
using UnityEngine;

namespace FarmFury.UI
{
    /// <summary>
    /// Art for the two Leaderboard screens (Settings -> Leaderboards), laid out from the Leaderboard mockups.
    /// Assigned to GameFlow by Phase 5a setup (StampedeLeaderboardArt). Any sprite left null falls back to text.
    /// </summary>
    [Serializable]
    public class LeaderboardArt
    {
        [Tooltip("Backdrop: Cluck bursting through the wooden sign (Environment/Settings_Canvas.png, widened by setup).")]
        public Sprite background;
        [Tooltip("FarmFury logo, top-left of the world list (FF_Logo.png - not made yet, see docs/ART_REQUIREMENTS.md; null = no logo).")]
        public Sprite logo;
        [Tooltip("World names as lettering, in WorldType order, trimmed to their text by setup. (UI/MeadowRuins.png ...)")]
        public Sprite[] worldNames = new Sprite[0];
        [Tooltip("Row labels on a world's page. (Best.png, HighScore.png, FastestTime.png)")]
        public Sprite bestLabel;
        public Sprite highScoreLabel;
        public Sprite fastestTimeLabel;
        [Tooltip("Coin beside the coins-earned plaque. (Collectable Coin.png)")]
        public Sprite coin;

        public Sprite WorldName(WorldType world) => ShopArt.At(worldNames, (int)world);
    }
}
