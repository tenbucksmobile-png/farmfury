using System;
using UnityEngine;

namespace FarmFury.Shared.UI
{
    /// <summary>
    /// Art for the two Leaderboard screens (Settings -> Leaderboards), laid out from the Leaderboard mockups.
    /// Each game keeps one on its GameFlow, wired by its own setup script. Any sprite left null falls back to text.
    /// </summary>
    [Serializable]
    public class LeaderboardArt
    {
        [Tooltip("Backdrop: Cluck bursting through the wooden sign (Environment/Settings_Canvas.png, widened by setup).")]
        public Sprite background;
        [Tooltip("The game's logo, top-left of the world list (null = no logo).")]
        public Sprite logo;
        [Tooltip("World names as lettering, in the game's world order (its WorldType), trimmed to their text by setup. (UI/MeadowRuins.png ...)")]
        public Sprite[] worldNames = new Sprite[0];
        [Tooltip("Row labels on a world's page. (Best.png, HighScore.png, FastestTime.png)")]
        public Sprite bestLabel;
        public Sprite highScoreLabel;
        public Sprite fastestTimeLabel;
        [Tooltip("Coin beside the coins-earned plaque. (Collectable Coin.png)")]
        public Sprite coin;

        /// <summary>The lettering for the world at this index of the game's world order (e.g. <c>(int)WorldType</c>).</summary>
        public Sprite WorldName(int worldIndex) => ShopArtBase.At(worldNames, worldIndex);
    }
}
