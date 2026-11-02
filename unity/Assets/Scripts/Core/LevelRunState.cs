using System.Collections.Generic;

namespace FarmFury.Core
{
    /// <summary>
    /// One level attempt's outcome and per-attempt limits (franchise convention, cut down from Stampede's
    /// LevelRunState). Reset by GameManager on every attempt (a retry is a new attempt); filled in
    /// GameManager.CompleteLevel from ScoreManager's finalised result. Read by GameFlow's results screen, the
    /// New Character page and (Phase 5) the coin payout / revive rules.
    /// </summary>
    public class LevelRunState
    {
        public int starsEarned;
        public int score;
        public bool isNewBest;
        public bool isPerfect;

        /// <summary>First time this level was ever completed (coins pay in full only then — franchise economy).</summary>
        public bool firstClear;
        /// <summary>Best stars on this level before this attempt (a replay pays for stars it adds).</summary>
        public int previousBestStars;

        public bool bossCleared;
        /// <summary>The story world this boss clear opened, if any.</summary>
        public WorldType? worldUnlocked;
        /// <summary>Animals that became available with that world (the New Character page).</summary>
        public readonly List<AnimalType> newlyUnlockedAnimals = new();

        // Monetisation (franchise Phase 5).
        public int coinsEarned;
        public bool doubleCoinsClaimed;
        public bool coinReviveUsed;
        public bool freeReviveUsed;

        public void Reset()
        {
            starsEarned = 0;
            score = 0;
            isNewBest = false;
            isPerfect = false;
            firstClear = false;
            previousBestStars = 0;
            bossCleared = false;
            worldUnlocked = null;
            newlyUnlockedAnimals.Clear();
            coinsEarned = 0;
            doubleCoinsClaimed = false;
            coinReviveUsed = false;
            freeReviveUsed = false;
        }
    }
}
