using FarmFury.Core;

namespace FarmFury.UI
{
    public enum ShopPromo
    {
        None,
        Cannons,
        Trails,
        Impacts,
        RemoveAds,
    }

    /// <summary>
    /// Decides whether Level Complete shows a shop card, and which one (2026-11-01). This is a game for children, so
    /// the cards only show things and never press: at most one per LevelsBetweenCards clears, never on a boss clear
    /// (the New Worlds page follows those), never something already owned, and no prices, timers or "buy now"
    /// wording. Tapping one opens the normal shop page, with its parental gate. A character unlocked by this clear
    /// gets the card early (hats on the screen after the celebration); Remove Ads comes up only after
    /// InterstitialsBeforeAdsCard interstitials were actually shown; otherwise hats, trails and machines take turns.
    /// The revive prompt never advertises.
    /// </summary>
    public static class ShopPromos
    {
        public const int LevelsBetweenCards = 3;
        public const int InterstitialsBeforeAdsCard = 2;

        /// <summary>Call once per Level Complete: counts the clear and uses up the card it returns.</summary>
        public static ShopPromo PickForLevelComplete(LevelData level, LevelRunState run)
        {
            var save = SaveManager.Instance;
            if (save == null || level == null || level.isBossLevel)
            {
                return ShopPromo.None;
            }

            save.LevelsSincePromo++;
            bool newCharacter = run.newlyUnlockedAnimals.Count > 0;
            if (save.LevelsSincePromo < LevelsBetweenCards && !newCharacter)
            {
                return ShopPromo.None;
            }

            var promo = Choose(save, run, newCharacter);
            if (promo != ShopPromo.None)
            {
                save.LevelsSincePromo = 0;
                AnalyticsManager.Instance?.LogPromoShown(Id(promo));
            }
            return promo;
        }

        private static ShopPromo Choose(SaveManager save, LevelRunState run, bool newCharacter)
        {
            // FarmFury: hats and trails are shared by every animal, so "unowned" doesn't depend on who's playing.
            if (newCharacter && AnyUnowned(save, StoreProducts.Cannons)) { return ShopPromo.Cannons; }

            if (!save.AdsRemoved && save.InterstitialsSinceAdsCard >= InterstitialsBeforeAdsCard)
            {
                save.InterstitialsSinceAdsCard = 0;
                return ShopPromo.RemoveAds;
            }

            // Next category in turn that still has something to sell.
            for (int i = 0; i < 3; i++)
            {
                int turn = save.PromoTurn++ % 3;
                if (turn == 0 && AnyUnowned(save, StoreProducts.Cannons)) { return ShopPromo.Cannons; }
                if (turn == 1 && AnyUnowned(save, StoreProducts.Trails)) { return ShopPromo.Trails; }
                if (turn == 2 && AnyUnowned(save, StoreProducts.Impacts)) { return ShopPromo.Impacts; }
            }
            return ShopPromo.None;
        }

        private static bool AnyUnowned(SaveManager save, string[] products)
        {
            foreach (string product in products)
            {
                if (!save.IsCosmeticOwned(product)) { return true; }
            }
            return false;
        }

        public static string Id(ShopPromo promo) => promo.ToString().ToLowerInvariant();
    }
}
