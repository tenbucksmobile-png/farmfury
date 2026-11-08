using System;
using System.Collections.Generic;
using System.Linq;
using FarmFury.Utilities;
using UnityEngine;
using UnityEngine.Purchasing;

namespace FarmFury.Core
{
    /// <summary>
    /// Real-money purchases through Unity IAP 5 (StoreController), ported from Farm Fury: Arcade's IAPManager with
    /// the same catalog, prices and grant rules (GDD Section 11): Remove Ads ($4.99, +100 bonus coins), four coin
    /// packs, hats and trails ($1.99, or 2500 coins), machines ($3.99) and purchase-gated worlds ($3.99). What
    /// Stampede changes: worlds are WorldTypes, and "the active character" (who a new hat is put on) is the one
    /// last played (GameManager.CurrentCharacter). Grants re-dress the player immediately via
    /// CharacterCosmeticRenderer.
    ///
    /// Store setup still to do (not code): create the Stampede products with these ids in App Store Connect and
    /// the Play Console, and link the Unity project to its Unity Gaming Services project. Until then the connect
    /// or fetch fails with a logged warning; in the Editor, Unity IAP's fake store completes purchases for testing.
    /// </summary>
    public class IAPManager : MonoSingleton<IAPManager>
    {
        public const int RemoveAdsBonusCoins = 100;
        public const int CosmeticCoinCost = 2500;

        private static readonly Dictionary<string, int> CoinPackAmounts = new()
        {
            { StoreProducts.CoinPacks[0], 100 },
            { StoreProducts.CoinPacks[1], 500 },
            { StoreProducts.CoinPacks[2], 5000 },
            { StoreProducts.CoinPacks[3], 15000 },
        };

        // Hats and trails can also be bought with coins (Arcade: 2500 each). Worlds are IAP only.
        public static bool TryGetCoinCost(string productId, out int coinCost)
        {
            bool coinBuyable = StoreProducts.AllCosmetics.Contains(productId);
            coinCost = coinBuyable ? CosmeticCoinCost : 0;
            return coinBuyable;
        }

        /// <summary>Shown until the store returns localized prices.</summary>
        private static readonly Dictionary<string, string> FallbackPrices = BuildFallbackPrices();

        private static Dictionary<string, string> BuildFallbackPrices()
        {
            var prices = new Dictionary<string, string>
            {
                { StoreProducts.RemoveAds, "$4.99" },
                { StoreProducts.CoinPacks[0], "$0.99" },
                { StoreProducts.CoinPacks[1], "$3.99" },
                { StoreProducts.CoinPacks[2], "$9.99" },
                { StoreProducts.CoinPacks[3], "$19.99" },
            };
            foreach (string id in StoreProducts.AllCosmetics) { prices[id] = "$1.99"; }
            foreach (WorldType world in Enum.GetValues(typeof(WorldType))) { prices[StoreProducts.World(world)] = "$3.99"; }
            return prices;
        }

        public bool IsInitialized { get; private set; }

        public event Action<string> OnPurchaseSucceeded;
        public event Action<string, string> OnPurchaseFailed;
        public event Action OnProductsReady;
        /// <summary>Owned purchases came back from the store (reinstall, new device, Restore Purchases).</summary>
        public event Action OnPurchasesRestored;

        private StoreController _storeController;
        private readonly Dictionary<string, Product> _products = new();
        private bool _connected, _connecting;

        /// <summary>A purchase is waiting for someone else (Ask to Buy, a pending payment method): it is granted
        /// later, through the normal pending-order path, if it is approved.</summary>
        public event Action<string> OnPurchaseDeferred;

        private void Start()
        {
            _storeController = UnityIAPServices.StoreController();
            _storeController.OnStoreConnected += HandleStoreConnected;
            _storeController.OnStoreDisconnected += HandleStoreDisconnected;
            _storeController.OnProductsFetched += HandleProductsFetched;
            _storeController.OnProductsFetchFailed += HandleProductsFetchFailed;
            _storeController.OnPurchasePending += HandlePurchasePending;
            _storeController.OnPurchaseFailed += HandlePurchaseFailed;
            _storeController.OnPurchasesFetched += HandlePurchasesFetched;
            _storeController.OnPurchasesFetchFailed += HandlePurchasesFetchFailed;
            _storeController.OnPurchaseDeferred += HandlePurchaseDeferred;
            Connect();
        }

        // Connects to the store; called again by a purchase or restore attempted while not connected (no network at
        // launch, Play services updating), so one failed launch connect doesn't close the shop for the session (audit).
        private async void Connect()
        {
            if (_storeController == null || _connected || _connecting)
            {
                return;
            }
            _connecting = true;
            try
            {
                await _storeController.Connect();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[IAPManager] Store connect failed (expected until the farmfury_ products exist in App Store Connect / Play Console): {e.Message}");
            }
            finally
            {
                _connecting = false;
            }
        }

        private void HandlePurchaseDeferred(DeferredOrder order)
        {
            var product = order?.CartOrdered?.Items().FirstOrDefault()?.Product;
            string productId = product?.definition.id ?? "unknown";
            Debug.Log($"[IAPManager] Purchase deferred (awaiting approval): {productId}");
            OnPurchaseDeferred?.Invoke(productId);
        }

        private void HandleStoreConnected()
        {
            _connected = true;
            // The game's ids stay the products' ids; the stores know them by StoreProducts.StoreId (prefixed).
            static ProductDefinition Define(string id, ProductType type) => new(id, StoreProducts.StoreId(id), type);
            var definitions = new List<ProductDefinition> { Define(StoreProducts.RemoveAds, ProductType.NonConsumable) };
            definitions.AddRange(StoreProducts.CoinPacks.Select(id => Define(id, ProductType.Consumable)));
            definitions.AddRange(StoreProducts.AllCosmetics.Select(id => Define(id, ProductType.NonConsumable)));
            definitions.AddRange(PurchasableWorlds().Select(w => Define(StoreProducts.World(w), ProductType.NonConsumable)));
            _storeController.FetchProducts(definitions);
        }

        /// <summary>
        /// Worlds sold in the shop: only those marked purchaseRequired. The six story worlds are all free (decided
        /// 2026-09-29, replacing the GDD's paid Worlds 4-6); the paid worlds will be new ones added after them.
        /// </summary>
        public static List<WorldType> PurchasableWorlds()
        {
            var worlds = DataManager.Instance != null ? DataManager.Instance.GetAllWorlds() : new List<WorldData>();
            return worlds.Where(w => w.purchaseRequired).Select(w => w.worldType).OrderBy(w => w).ToList();
        }

        private void HandleStoreDisconnected(StoreConnectionFailureDescription description)
        {
            _connected = false;
            Debug.LogWarning($"[IAPManager] Store disconnected: {description.message}");
        }

        private void HandleProductsFetched(List<Product> products)
        {
            foreach (var product in products)
            {
                _products[product.definition.id] = product;
            }
            IsInitialized = true;
            OnProductsReady?.Invoke();
            FetchOwnedPurchases();
        }

        // ------------------------------------------------------------ owned purchases (reinstall / new device)

        // Asks the store what this account already owns, so Remove Ads, paid worlds and cosmetics come back after a
        // reinstall without the player pressing Restore (Google Play has no restore flow of its own; on iOS it also
        // picks up purchases made on another device that the App Store already reports). Answered by
        // HandlePurchasesFetched.
        private void FetchOwnedPurchases()
        {
            try
            {
                _storeController.FetchPurchases();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[IAPManager] FetchPurchases threw: {e.Message}");
            }
        }

        private void HandlePurchasesFetched(Orders orders)
        {
            int restored = 0;
            foreach (var order in orders.ConfirmedOrders)
            {
                foreach (var item in order.CartOrdered.Items())
                {
                    if (item?.Product != null && RestoreOwnership(item.Product.definition.id)) { restored++; }
                }
            }
            if (restored > 0)
            {
                SaveManager.Instance?.SaveProgress();
                Debug.Log($"[IAPManager] Restored {restored} owned purchase(s).");
                OnPurchasesRestored?.Invoke();
            }
        }

        private void HandlePurchasesFetchFailed(PurchasesFetchFailureDescription failure)
        {
            Debug.LogWarning($"[IAPManager] Fetching owned purchases failed: {failure.FailureReason} {failure.Message}");
        }

        /// <summary>
        /// Marks a non-consumable the store says is owned as owned here, without the first-purchase extras (no Remove
        /// Ads bonus coins, nothing equipped). Coin packs are consumables and never come back. True if anything changed.
        /// </summary>
        private static bool RestoreOwnership(string productId)
        {
            var save = SaveManager.Instance;
            if (save == null) { return false; }

            if (productId == StoreProducts.RemoveAds)
            {
                if (save.AdsRemoved) { return false; }
                save.AdsRemoved = true;
                AdManager.Instance?.HideBanner();
                return true;
            }
            if (TryGetWorld(productId, out var world))
            {
                if (save.IsWorldPurchased(world)) { return false; }
                save.SetWorldPurchased(world);
                return true;
            }
            if (!StoreProducts.AllCosmetics.Contains(productId)) { return false; }
            if (save.IsCosmeticOwned(productId)) { return false; }
            save.SetCosmeticOwned(productId);
            return true;
        }

        private void HandleProductsFetchFailed(ProductFetchFailed failure)
        {
            Debug.LogWarning($"[IAPManager] Product fetch failed for {failure.FailedFetchProducts.Count} products: {failure.FailureReason}");
        }

        public string GetPriceString(string productId)
        {
            if (_products.TryGetValue(productId, out var product) && product.metadata != null &&
                !string.IsNullOrEmpty(product.metadata.localizedPriceString))
            {
                return product.metadata.localizedPriceString;
            }
            return FallbackPrices.TryGetValue(productId, out var fallback) ? fallback : string.Empty;
        }

        public void PurchaseProduct(string productId)
        {
            // Not connected, or the store never returned this product: fail now (the caller's UI says so) and try
            // connecting again for the next attempt, rather than handing the store an order it can't take.
            if (_storeController == null || !_connected || !_products.TryGetValue(productId, out var product))
            {
                Debug.LogWarning($"[IAPManager] Store not ready - can't purchase {productId} yet.");
                Connect();
                OnPurchaseFailed?.Invoke(productId, "StoreNotConnected");
                return;
            }

            try
            {
                _storeController.PurchaseProduct(product);
            }
            catch (Exception e)
            {
                Debug.LogError($"[IAPManager] PurchaseProduct({productId}) threw: {e}");
                OnPurchaseFailed?.Invoke(productId, e.Message);
            }
        }

        public void RestorePurchases(Action<bool> callback)
        {
            if (_storeController == null || !_connected)
            {
                Connect();
                callback?.Invoke(false);
                return;
            }

            try
            {
                _storeController.RestoreTransactions((success, error) =>
                {
                    if (!success && !string.IsNullOrEmpty(error))
                    {
                        Debug.LogWarning($"[IAPManager] Restore failed: {error}");
                    }
                    FetchOwnedPurchases();   // grants whatever the restore brought back (HandlePurchasesFetched)
                    callback?.Invoke(success);
                });
            }
            catch (Exception e)
            {
                Debug.LogError($"[IAPManager] RestoreTransactions threw: {e}");
                callback?.Invoke(false);
            }
        }

        private void HandlePurchasePending(PendingOrder order)
        {
            try
            {
                HandlePurchasePendingInner(order);
            }
            catch (Exception e)
            {
                Debug.LogError($"[IAPManager] HandlePurchasePending threw: {e}");
            }
        }

        private void HandlePurchasePendingInner(PendingOrder order)
        {
            var product = order.CartOrdered.Items().FirstOrDefault()?.Product;
            if (product == null)
            {
                Debug.LogWarning("[IAPManager] Could not find product in pending order.");
                return;
            }

            string productId = product.definition.id;
            var save = SaveManager.Instance;

            if (productId == StoreProducts.RemoveAds)
            {
                if (save != null)
                {
                    // The bonus only once: a store can re-deliver an owned non-consumable (restore, new device).
                    if (!save.AdsRemoved) { save.AddCoins(RemoveAdsBonusCoins); }
                    save.AdsRemoved = true;
                    save.SaveProgress();
                }
                AdManager.Instance?.HideBanner();
            }
            else if (CoinPackAmounts.TryGetValue(productId, out int amount))
            {
                save?.AddCoins(amount);
                save?.SaveProgress();
            }
            else if (TryGetWorld(productId, out var world))
            {
                save?.SetWorldPurchased(world);
            }
            else if (!GrantCosmeticEffect(productId))
            {
                Debug.LogWarning($"[IAPManager] Unrecognized product id in pending order: {productId}");
            }

            _storeController.ConfirmPurchase(order);
            AnalyticsManager.Instance?.LogPurchase(productId, GetPriceString(productId));
            OnPurchaseSucceeded?.Invoke(productId);
        }

        private static bool TryGetWorld(string productId, out WorldType world)
        {
            foreach (WorldType candidate in Enum.GetValues(typeof(WorldType)))
            {
                if (StoreProducts.World(candidate) == productId)
                {
                    world = candidate;
                    return true;
                }
            }
            world = default;
            return false;
        }

        /// <summary>Hats and trails bought with coins instead of money. Refunds if the grant fails.</summary>
        public bool PurchaseProductWithCoins(string productId)
        {
            if (!TryGetCoinCost(productId, out int cost) || SaveManager.Instance == null || !SaveManager.Instance.SpendCoins(cost))
            {
                return false;
            }

            if (!GrantCosmeticEffect(productId))
            {
                SaveManager.Instance.AddCoins(cost);
                return false;
            }

            SaveManager.Instance.SaveProgress();
            OnPurchaseSucceeded?.Invoke(productId);
            return true;
        }

        // ------------------------------------------------------------ cosmetic grants
        // FarmFury: a hat or trail is one cosmetic shared by every animal (cosmetic id = product id) — Stampede's
        // per-character cap/cowboy variants and its machine skins don't apply to a launcher game. Buying one owns
        // it and puts it on: a hat on every animal, the trail behind all of them (SaveManager equips).

        public static bool IsProductOwned(string productId)
        {
            var save = SaveManager.Instance;
            if (save == null) { return false; }
            if (productId == StoreProducts.RemoveAds) { return save.AdsRemoved; }
            if (TryGetWorld(productId, out var world)) { return save.IsWorldPurchased(world); }
            if (CoinPackAmounts.ContainsKey(productId)) { return false; }
            return save.IsCosmeticOwned(productId);
        }

        private static bool GrantCosmeticEffect(string productId)
        {
            var save = SaveManager.Instance;
            if (save == null) { return false; }
            // Buying a cosmetic owns it and puts it on (cannon skin, trail or impact effect).
            foreach (var category in new[] { StoreProducts.Cannons, StoreProducts.Trails, StoreProducts.Impacts })
            {
                if (!category.Contains(productId)) continue;
                save.SetCosmeticOwned(productId);
                save.SetEquipped(category, productId);
                return true;
            }
            return false;
        }

        private void HandlePurchaseFailed(FailedOrder order)
        {
            var product = order.CartOrdered.Items().FirstOrDefault()?.Product;
            string productId = product?.definition.id ?? "unknown";
            Debug.LogWarning($"[IAPManager] Purchase failed - Product: '{productId}', Reason: {order.FailureReason}, Details: {order.Details}");
            OnPurchaseFailed?.Invoke(productId, order.FailureReason.ToString());
        }
    }
}
