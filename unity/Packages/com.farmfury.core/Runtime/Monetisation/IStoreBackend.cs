using System;
using System.Collections.Generic;

namespace FarmFury.Shared.Monetisation
{
    /// <summary>
    /// What the shared shop screens need from a game's IAP manager. Each game implements this in a small adapter over
    /// its IAPManager (its product catalogue, save keys and store-id prefix stay in the game) and assigns it to
    /// <see cref="Store.Backend"/> from a <c>[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]</c>, so it is there
    /// before any screen is built. The member names follow the IAPManager Stampede and Cannonfire already have.
    /// </summary>
    public interface IStoreBackend
    {
        /// <summary>False while the game's IAP manager doesn't exist (purchases then report "Store unavailable").</summary>
        bool IsAvailable { get; }

        /// <summary>The game's Remove Ads product id (the game's own id, not the prefixed store id).</summary>
        string RemoveAdsId { get; }

        /// <summary>Coin-pack product ids, in ShopArtBase.coinPacks art order.</summary>
        IReadOnlyList<string> CoinPackIds { get; }

        bool IsOwned(string productId);

        /// <summary>The player's coin balance (from the game's save).</summary>
        int CoinBalance { get; }

        /// <summary>True when the product can also be bought with coins, and its price in coins.</summary>
        bool TryGetCoinCost(string productId, out int coinCost);

        /// <summary>Starts a real-money purchase; the outcome comes back through the three events.</summary>
        void PurchaseProduct(string productId);

        /// <summary>Buys with coins (no store involved). False when the player can't afford it or the grant failed.</summary>
        bool PurchaseProductWithCoins(string productId);

        void RestorePurchases(Action<bool> callback);

        event Action<string> OnPurchaseSucceeded;
        /// <summary>(product id, failure reason as the IAP package names it).</summary>
        event Action<string, string> OnPurchaseFailed;
        /// <summary>Waiting for approval (Ask to Buy, pending payment); granted later if approved.</summary>
        event Action<string> OnPurchaseDeferred;
    }
}
