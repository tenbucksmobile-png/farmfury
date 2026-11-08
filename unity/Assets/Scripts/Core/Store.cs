using System;
using System.Linq;

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

        // Cosmetics (2026-11-08, user decision): cannon skins, trails and impact effects. Hats were dropped (too small
        // to read at the launcher game's zoom and hard to fit on eight body shapes). All shared by every animal.
        public static readonly string[] Cannons =
            { "cannon_golden", "cannon_pumpkin", "cannon_corncob", "cannon_milkchurn", "cannon_tractor" };

        public static readonly string[] Trails =
            { "trail_rainbowribbon", "trail_sparkledust", "trail_cornhusk", "trail_ember", "trail_confetti", "trail_bubbles" };

        public static readonly string[] Impacts =
            { "impact_confetti", "impact_popcorn", "impact_fireworks", "impact_feathers", "impact_eggsplat" };

        /// <summary>Every cosmetic, in StoreProducts.Cosmetics / ShopArt.cosmeticIcons order.</summary>
        public static string[] AllCosmetics => Cannons.Concat(Trails).Concat(Impacts).ToArray();

        public static string World(WorldType world) => "world_" + world.ToString().ToLowerInvariant();

        /// <summary>Names and blurbs: cannons, then trails, then impacts (ShopArt.cosmeticIcons order).</summary>
        public static readonly (string name, string blurb)[] Cosmetics =
        {
            ("Golden Cannon", "Polished gold and a gem or two - fire your farmyard in style."),
            ("Pumpkin Cannon", "A carved jack-o-lantern barrel on a vine carriage. Spooky season, all season."),
            ("Corn Cob Cannon", "A giant golden cob for a barrel - the farm's favourite crop fights back."),
            ("Milk Churn Cannon", "An old dairy churn with cow spots. Fresh from the milking shed."),
            ("Tractor Cannon", "Big tyres, a chrome exhaust stack and a puff of smoke on every shot."),
            ("Rainbow Ribbon", "A trail of shimmering rainbow colour follows every flight."),
            ("Sparkle Dust", "Leaves a shimmering trail of magic sparkles behind you."),
            ("Corn Husk Trail", "A rustling trail of golden corn husks, straight off the stalk."),
            ("Ember Trail", "A trail of glowing embers - warm, cozy, and a little bit fiery."),
            ("Confetti Trail", "A burst of colourful confetti all the way to the target - instant party!"),
            ("Bubbles Trail", "Leaves a trail of soft, shimmering bubbles floating behind you."),
            ("Confetti Pop", "Every robot you knock out goes out in a shower of party confetti."),
            ("Popcorn Burst", "Pop! Robots burst into a cloud of buttery popcorn."),
            ("Fireworks", "Send every robot off with a bang and a sky full of sparks."),
            ("Feather Poof", "A flurry of farmyard feathers for every robot you topple."),
            ("Egg Splat", "Splat! Robots go down in a big messy egg splash."),
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

        /// <summary>A cosmetic bought with coins (Arcade: 2500), no store involved.</summary>
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
