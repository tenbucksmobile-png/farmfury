using System;
using System.Collections.Generic;
using System.Linq;
using FarmFury.Shared.Monetisation;
using UnityEngine;

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
    /// Cannonfire's <see cref="IStoreBackend"/> for com.farmfury.core's <see cref="Store"/> facade, which the shop
    /// screens buy through: a stateless adapter over <see cref="IAPManager"/> (static ownership checks work before
    /// the IAPManager exists, as before). Registered before the first scene loads, so it is there when GameFlow
    /// builds its screens.
    /// </summary>
    public sealed class CannonfireStoreBackend : IStoreBackend
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register() => Store.Backend = new CannonfireStoreBackend();

        public bool IsAvailable => IAPManager.Instance != null;
        public string RemoveAdsId => StoreProducts.RemoveAds;
        public IReadOnlyList<string> CoinPackIds => StoreProducts.CoinPacks;
        public bool IsOwned(string productId) => IAPManager.IsProductOwned(productId);
        public int CoinBalance => SaveManager.Instance != null ? SaveManager.Instance.CoinBalance : 0;
        public bool TryGetCoinCost(string productId, out int coinCost) => IAPManager.TryGetCoinCost(productId, out coinCost);
        public void PurchaseProduct(string productId) => IAPManager.Instance.PurchaseProduct(productId);
        public bool PurchaseProductWithCoins(string productId) => IAPManager.Instance.PurchaseProductWithCoins(productId);
        public void RestorePurchases(Action<bool> callback) => IAPManager.Instance.RestorePurchases(callback);

        // Store hooks these only while IsAvailable, for the length of one purchase.
        public event Action<string> OnPurchaseSucceeded
        {
            add { if (IAPManager.Instance != null) IAPManager.Instance.OnPurchaseSucceeded += value; }
            remove { if (IAPManager.Instance != null) IAPManager.Instance.OnPurchaseSucceeded -= value; }
        }

        public event Action<string, string> OnPurchaseFailed
        {
            add { if (IAPManager.Instance != null) IAPManager.Instance.OnPurchaseFailed += value; }
            remove { if (IAPManager.Instance != null) IAPManager.Instance.OnPurchaseFailed -= value; }
        }

        public event Action<string> OnPurchaseDeferred
        {
            add { if (IAPManager.Instance != null) IAPManager.Instance.OnPurchaseDeferred += value; }
            remove { if (IAPManager.Instance != null) IAPManager.Instance.OnPurchaseDeferred -= value; }
        }
    }
}
