using System;
using System.Collections.Generic;
using FarmFury.Shared.Monetisation;
using FarmFury.Shared.UI;
using NUnit.Framework;
using UnityEngine;

namespace FarmFury.Shared.Tests
{
    public class StoreTests
    {
        private class FakeBackend : IStoreBackend
        {
            public bool IsAvailable { get; set; } = true;
            public string RemoveAdsId => "remove_ads";
            public IReadOnlyList<string> CoinPackIds { get; } = new[] { "coins_100", "coins_500" };
            public readonly HashSet<string> Owned = new();
            public bool IsOwned(string productId) => Owned.Contains(productId);
            public int CoinBalance { get; set; }
            public bool TryGetCoinCost(string productId, out int coinCost)
            {
                coinCost = productId.StartsWith("trail_") ? 2500 : 0;
                return coinCost > 0;
            }
            public string LastPurchase;
            public void PurchaseProduct(string productId) => LastPurchase = productId;
            public bool PurchaseProductWithCoins(string productId) => TryGetCoinCost(productId, out int c) && CoinBalance >= c;
            public void RestorePurchases(Action<bool> callback) => callback(true);
            public event Action<string> OnPurchaseSucceeded;
            public event Action<string, string> OnPurchaseFailed;
            public event Action<string> OnPurchaseDeferred;
            public void Succeed(string id) => OnPurchaseSucceeded?.Invoke(id);
            public void Fail(string id, string reason) => OnPurchaseFailed?.Invoke(id, reason);
            public void Defer(string id) => OnPurchaseDeferred?.Invoke(id);
            public bool Hooked => OnPurchaseSucceeded != null || OnPurchaseFailed != null || OnPurchaseDeferred != null;
        }

        private FakeBackend _fake;

        [SetUp]
        public void SetUp() => Store.Backend = _fake = new FakeBackend();

        [TearDown]
        public void TearDown() => Store.Backend = null;

        [Test]
        public void NoBackendOrUnavailable_ReportsStoreUnavailable()
        {
            Store.Backend = null;
            string message = null;
            Store.Purchase("remove_ads", (ok, m) => message = m);
            Assert.AreEqual(Store.UnavailableMessage, message);
            Assert.IsFalse(Store.IsOwned("remove_ads"));
            Assert.AreEqual(0, Store.CoinPackIds.Count);

            Store.Backend = _fake;
            _fake.IsAvailable = false;
            bool? restored = null;
            Store.RestorePurchases((ok, m) => { restored = ok; message = m; });
            Assert.AreEqual(false, restored);
            Assert.AreEqual(Store.UnavailableMessage, message);
            Store.Purchase("remove_ads", (ok, m) => { });
            Assert.IsNull(_fake.LastPurchase);
        }

        [Test]
        public void Purchase_IgnoresOtherProducts_ThenCallsBackOnce_AndUnhooks()
        {
            int calls = 0;
            bool ok = false;
            Store.Purchase("coins_100", (o, m) => { calls++; ok = o; });
            Assert.AreEqual("coins_100", _fake.LastPurchase);

            _fake.Succeed("coins_500");
            Assert.AreEqual(0, calls);
            _fake.Succeed("coins_100");
            _fake.Succeed("coins_100");
            Assert.AreEqual(1, calls);
            Assert.IsTrue(ok);
            Assert.IsFalse(_fake.Hooked);
        }

        [Test]
        public void FailedAndDeferred_GiveTheStatusLineMessages()
        {
            string message = null;
            Store.Purchase("remove_ads", (o, m) => message = m);
            _fake.Fail("remove_ads", "UserCancelled");
            Assert.AreEqual("Purchase cancelled.", message);

            Store.Purchase("remove_ads", (o, m) => message = m);
            _fake.Fail("remove_ads", "StoreNotConnected");
            Assert.AreEqual("Store unavailable - check your connection.", message);

            Store.Purchase("remove_ads", (o, m) => message = m);
            _fake.Defer("remove_ads");
            Assert.AreEqual("Waiting for approval.", message);
            Assert.IsFalse(_fake.Hooked);
        }

        [Test]
        public void CanAffordWithCoins_NeedsACoinPriceAndTheBalance()
        {
            _fake.CoinBalance = 2499;
            Assert.IsFalse(Store.CanAffordWithCoins("trail_ember"));
            _fake.CoinBalance = 2500;
            Assert.IsTrue(Store.CanAffordWithCoins("trail_ember"));
            Assert.IsFalse(Store.CanAffordWithCoins("remove_ads"));
        }
    }

    public class SharedArtTests
    {
        [Test]
        public void At_IsSafeForMissingAndShortArrays()
        {
            Assert.IsNull(ShopArtBase.At(null, 0));
            Assert.IsNull(ShopArtBase.At(new Sprite[1], 1));
            Assert.IsNull(ShopArtBase.At(new Sprite[1], -1));
            Assert.IsNull(new LeaderboardArt().WorldName(3));
        }

        [Serializable]
        private class GameMenuArt : MenuArtBase { public Sprite gameOnly; }

        [Test]
        public void DerivedArt_SerializesInheritedFieldsByName()
        {
            // A game's MenuArt keeps scene values for fields moved into the base: JsonUtility uses the same
            // field-name serializer as scenes.
            var json = JsonUtility.ToJson(new GameMenuArt());
            StringAssert.Contains("\"backButton\"", json);
            StringAssert.Contains("\"pauseBackground\"", json);
            StringAssert.Contains("\"gameOnly\"", json);
        }
    }
}
