using FarmFury.Core;
using FarmFury.Shared.Monetisation;

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
        // Category order for ShopPromoPacer (its index = position here); cannons are offered after a new animal.
        private static readonly ShopPromo[] Categories = { ShopPromo.Cannons, ShopPromo.Trails, ShopPromo.Impacts };

        public static ShopPromo PickForLevelComplete(LevelData level, LevelRunState run)
        {
            var save = SaveManager.Instance;
            if (save == null || level == null)
            {
                return ShopPromo.None;
            }

            // FarmFury: cosmetics are shared by every animal, so "unowned" doesn't depend on who's playing.
            int pick = ShopPromoPacer.Pick(save, level.isBossLevel, run.newlyUnlockedAnimals.Count > 0, Categories.Length,
                i => AnyUnowned(save, Products(Categories[i])), newCharacterCategory: 0);
            var promo = pick == ShopPromoPacer.RemoveAds ? ShopPromo.RemoveAds
                : pick >= 0 ? Categories[pick] : ShopPromo.None;
            if (promo != ShopPromo.None)
            {
                AnalyticsManager.Instance?.LogPromoShown(Id(promo));
            }
            return promo;
        }

        private static string[] Products(ShopPromo promo) => promo switch
        {
            ShopPromo.Cannons => StoreProducts.Cannons,
            ShopPromo.Trails => StoreProducts.Trails,
            _ => StoreProducts.Impacts,
        };

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
