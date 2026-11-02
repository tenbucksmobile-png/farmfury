using System;

namespace FarmFury.Core
{
    /// <summary>
    /// Store product ids, the same catalog as Farm Fury: Arcade's IAPManager (GDD Section 11: Arcade's tested tiers).
    /// The order of each array matches the art order in ShopArt.
    /// </summary>
    public static class StoreProducts
    {
        /// <summary>
        /// Prefix of every product's id in App Store Connect and Play Console (since 2026-10-25): Apple product ids
        /// must be unique across the whole developer account, and Arcade's (same account) already use the bare
        /// names. The game itself keeps the bare ids below (save keys, cosmetic ids, analytics); IAPManager maps
        /// them to store ids in its ProductDefinitions, so create the products as e.g. "farmfury_remove_ads".
        /// </summary>
        public const string StoreIdPrefix = "farmfury_";

        /// <summary>The id a product has in the stores (StoreIdPrefix + the game's id).</summary>
        public static string StoreId(string productId) => StoreIdPrefix + productId;

        public const string RemoveAds = "remove_ads";

        public static readonly string[] CoinPacks = { "coins_100", "coins_500", "coins_5000", "coins_15000" };

        public static readonly string[] Hats =
            { "hat_sombrero", "hat_baseball_cap", "hat_cowboy_hat", "hat_chef_hat", "hat_crown" };

        public static readonly string[] Trails =
            { "trail_rainbowribbon", "trail_sparkledust", "trail_cornhusk", "trail_ember", "trail_confetti", "trail_bubbles" };

        public static string World(WorldType world) => "world_" + world.ToString().ToLowerInvariant();

        /// <summary>Character Story's Cosmetics tab: every hat, trail and machine, in ShopArt.cosmeticIcons order.</summary>
        public static readonly (string name, string blurb)[] Cosmetics =
        {
            ("Sombrero", "Ole! A wide, sun-shading hat with a ton of farm-fiesta flair."),
            ("Baseball Cap", "Sporty and snug - every animal's got a favourite colour."),
            ("Cowboy Hat", "Yeehaw! Perfect for rounding up robots instead of cattle."),
            ("Chef Hat", "A crisp toque fit for a top chef - bring farm-fresh flavour to the kitchen."),
            ("Crown", "Fit for royalty - every animal deserves to feel like king (or queen) of the farm."),
            ("Rainbow Ribbon", "A trail of shimmering rainbow colour follows every flight."),
            ("Sparkle Dust", "Leaves a shimmering trail of magic sparkles behind you."),
            ("Corn Husk Trail", "A rustling trail of golden corn husks, straight off the stalk."),
            ("Ember Trail", "A trail of glowing embers - warm, cozy, and a little bit fiery."),
            ("Confetti Trail", "A burst of colourful confetti all the way to the target - instant party!"),
            ("Bubbles Trail", "Leaves a trail of soft, shimmering bubbles floating behind you."),
        };
    }

    /// <summary>
    /// What the shop screens buy through: a thin callback-style facade over <see cref="IAPManager"/> (Arcade's screens
    /// subscribe to IAPManager's events directly; this keeps that in one place). One purchase at a time.
    /// </summary>
    public static class Store
    {
        public const string UnavailableMessage = "Store unavailable.";

        public static bool IsOwned(string productId) => IAPManager.IsProductOwned(productId);

        /// <summary>Calls back once with (succeeded, message for the status line).</summary>
        public static void Purchase(string productId, Action<bool, string> onDone)
        {
            var iap = IAPManager.Instance;
            if (iap == null)
            {
                onDone?.Invoke(false, UnavailableMessage);
                return;
            }

            void Succeeded(string id)
            {
                if (id != productId) { return; }
                Unhook();
                onDone?.Invoke(true, "Purchase complete!");
            }

            void Failed(string id, string reason)
            {
                if (id != productId) { return; }
                Unhook();
                onDone?.Invoke(false, reason == "StoreNotConnected" ? "Store unavailable - check your connection."
                    : reason != null && reason.Contains("Cancel") ? "Purchase cancelled." : "Purchase failed.");
            }

            void Deferred(string id)
            {
                if (id != productId) { return; }
                Unhook();
                onDone?.Invoke(false, "Waiting for approval.");   // granted later if approved
            }

            void Unhook()
            {
                iap.OnPurchaseSucceeded -= Succeeded;
                iap.OnPurchaseFailed -= Failed;
                iap.OnPurchaseDeferred -= Deferred;
            }

            iap.OnPurchaseSucceeded += Succeeded;
            iap.OnPurchaseFailed += Failed;
            iap.OnPurchaseDeferred += Deferred;
            iap.PurchaseProduct(productId);
        }

        /// <summary>A hat or trail bought with coins (Arcade: 2500), no store involved.</summary>
        public static bool PurchaseWithCoins(string productId) =>
            IAPManager.Instance != null && IAPManager.Instance.PurchaseProductWithCoins(productId);

        public static void RestorePurchases(Action<bool, string> onDone)
        {
            if (IAPManager.Instance == null)
            {
                onDone?.Invoke(false, UnavailableMessage);
                return;
            }
            IAPManager.Instance.RestorePurchases(ok => onDone?.Invoke(ok, ok ? "Purchases restored!" : "Restore failed."));
        }
    }
}
