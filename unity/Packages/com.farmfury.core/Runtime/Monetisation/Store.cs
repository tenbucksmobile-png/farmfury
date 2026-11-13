using System;
using System.Collections.Generic;

namespace FarmFury.Shared.Monetisation
{
    /// <summary>
    /// What the shop screens buy through: a thin callback-style facade over the game's <see cref="IStoreBackend"/>
    /// (its IAPManager). It keeps the event wiring in one place. One purchase at a time.
    /// </summary>
    public static class Store
    {
        public const string UnavailableMessage = "Store unavailable.";

        /// <summary>The game's adapter over its IAP manager, set before the first scene loads. Null = no store.</summary>
        public static IStoreBackend Backend { get; set; }

        private static readonly string[] NoIds = new string[0];

        public static string RemoveAdsId => Backend?.RemoveAdsId ?? "remove_ads";

        public static IReadOnlyList<string> CoinPackIds => Backend?.CoinPackIds ?? NoIds;

        public static bool IsOwned(string productId) => Backend != null && Backend.IsOwned(productId);

        public static int CoinBalance => Backend?.CoinBalance ?? 0;

        /// <summary>True when the product is sold for coins and the player has enough.</summary>
        public static bool CanAffordWithCoins(string productId) =>
            Backend != null && Backend.TryGetCoinCost(productId, out int cost) && Backend.CoinBalance >= cost;

        /// <summary>Calls back once with (succeeded, message for the status line).</summary>
        public static void Purchase(string productId, Action<bool, string> onDone)
        {
            var iap = Backend;
            if (iap == null || !iap.IsAvailable)
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
                onDone?.Invoke(false, FailureMessage(reason));
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

        /// <summary>The status-line text for an IAP failure reason.</summary>
        public static string FailureMessage(string reason) =>
            reason == "StoreNotConnected" ? "Store unavailable - check your connection."
            : reason != null && reason.Contains("Cancel") ? "Purchase cancelled." : "Purchase failed.";

        /// <summary>An item bought with coins (Arcade: 2500), no store involved.</summary>
        public static bool PurchaseWithCoins(string productId) => Backend != null && Backend.IsAvailable && Backend.PurchaseProductWithCoins(productId);

        public static void RestorePurchases(Action<bool, string> onDone)
        {
            if (Backend == null || !Backend.IsAvailable)
            {
                onDone?.Invoke(false, UnavailableMessage);
                return;
            }
            Backend.RestorePurchases(ok => onDone?.Invoke(ok, ok ? "Purchases restored!" : "Restore failed."));
        }
    }
}
