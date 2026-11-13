namespace FarmFury.Shared.Monetisation
{
    /// <summary>The persisted counters <see cref="ShopPromoPacer"/> paces with (the game's SaveManager, its own keys).</summary>
    public interface IPromoState
    {
        int LevelsSincePromo { get; set; }
        int InterstitialsSinceAdsCard { get; set; }
        int PromoTurn { get; set; }
        bool AdsRemoved { get; }
    }

    /// <summary>
    /// The franchise's Level Complete shop-card pacing (Stampede's ShopPromos): a card at most every
    /// <see cref="LevelsBetweenCards"/> levels (or straight after a new character), never on a boss level; the Remove
    /// Ads card once <see cref="InterstitialsBeforeAdsCard"/> interstitials have played since the last one; otherwise
    /// the game's cosmetic categories in turn, skipping any with nothing left to sell. The game maps the result to
    /// its own promo enum, art and analytics.
    /// </summary>
    public static class ShopPromoPacer
    {
        public const int LevelsBetweenCards = 3;
        public const int InterstitialsBeforeAdsCard = 2;

        /// <summary>No card this time.</summary>
        public const int None = -1;
        /// <summary>The Remove Ads card.</summary>
        public const int RemoveAds = -2;

        /// <summary>
        /// Called once per Level Complete. Returns a category index (0..categoryCount-1), <see cref="RemoveAds"/> or
        /// <see cref="None"/>, and updates the counters.
        /// </summary>
        /// <param name="hasUnowned">True when category i still has something the player doesn't own.</param>
        /// <param name="newCharacterCategory">Category offered straight after a new character unlocks (Stampede: hats
        /// for that character; Cannonfire: cannons), or -1 for none.</param>
        public static int Pick(IPromoState state, bool bossLevel, bool newCharacter, int categoryCount,
            System.Func<int, bool> hasUnowned, int newCharacterCategory = 0)
        {
            if (state == null || bossLevel)
            {
                return None;
            }

            state.LevelsSincePromo++;
            if (state.LevelsSincePromo < LevelsBetweenCards && !newCharacter)
            {
                return None;
            }

            int pick = Choose(state, newCharacter, categoryCount, hasUnowned, newCharacterCategory);
            if (pick != None)
            {
                state.LevelsSincePromo = 0;
            }
            return pick;
        }

        private static int Choose(IPromoState state, bool newCharacter, int categoryCount, System.Func<int, bool> hasUnowned,
            int newCharacterCategory)
        {
            if (newCharacter && newCharacterCategory >= 0 && newCharacterCategory < categoryCount && hasUnowned(newCharacterCategory))
            {
                return newCharacterCategory;
            }

            if (!state.AdsRemoved && state.InterstitialsSinceAdsCard >= InterstitialsBeforeAdsCard)
            {
                state.InterstitialsSinceAdsCard = 0;
                return RemoveAds;
            }

            // Next category in turn that still has something to sell.
            for (int i = 0; i < categoryCount; i++)
            {
                int turn = state.PromoTurn++ % categoryCount;
                if (hasUnowned(turn))
                {
                    return turn;
                }
            }
            return None;
        }
    }
}
