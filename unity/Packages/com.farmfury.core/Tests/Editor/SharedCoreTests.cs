using NUnit.Framework;
using UnityEngine;
using FarmFury.Shared.Config;
using FarmFury.Shared.Monetisation;

namespace FarmFury.Shared.Tests
{
    public class GameIdentityTests
    {
        private static GameIdentity Make(string prefix, string store)
        {
            var id = ScriptableObject.CreateInstance<GameIdentity>();
            id.gameId = "stampede";
            id.savePrefix = prefix;
            id.storeIdPrefix = store;
            id.androidPackage = "com.farmfury.stampede";
            return id;
        }

        [Test]
        public void SaveKey_UsesTheGamesExistingPrefix()
        {
            Assert.AreEqual("FFS_Stars_W1_L01", Make("FFS_", "stampede_").SaveKey("Stars_W1_L01"));
            Assert.AreEqual("FFA_CoinBalance", Make("FFA_", "").SaveKey("CoinBalance"));
        }

        [Test]
        public void StoreId_PrefixesOrLeavesBare()
        {
            Assert.AreEqual("stampede_remove_ads", Make("FFS_", "stampede_").StoreId("remove_ads"));
            Assert.AreEqual("remove_ads", Make("FFA_", "").StoreId("remove_ads"));   // Arcade's bare ids
        }

        [Test]
        public void IsValid_RequiresIdPrefixAndPackage()
        {
            Assert.IsTrue(Make("FFS_", "stampede_").IsValid(out _));
            Assert.IsFalse(Make("", "x").IsValid(out var why));
            StringAssert.Contains("savePrefix", why);
        }
    }

    public class UIKitTests
    {
        [TearDown]
        public void Reset() => FarmFury.Shared.UI.UIKit.RoundButtonSize = FarmFury.Shared.UI.UIKit.DefaultRoundButtonSize;

        [Test]
        public void RoundButtonSize_DefaultsTo160_AndIsSettablePerGame()
        {
            Assert.AreEqual(160f, FarmFury.Shared.UI.UIKit.RoundButtonSize);
            FarmFury.Shared.UI.UIKit.RoundButtonSize = 112f;   // Cannonfire
            Assert.AreEqual(112f, FarmFury.Shared.UI.UIKit.RoundButtonSize);
        }

        [Test]
        public void EnsureEventSystem_CreatesOneWithAUIInputModule()
        {
            var root = new GameObject("root");
            FarmFury.Shared.UI.UIKit.EnsureEventSystem(root.transform);
            var es = Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>();
            Assert.IsNotNull(es);
            Assert.IsNotNull(es.GetComponent<UnityEngine.EventSystems.BaseInputModule>());
            Object.DestroyImmediate(root);
        }
    }

    public class AdPolicyTests
    {
        private class FakePolicy : IAdPolicy
        {
            public bool AdsRemoved { get; set; }
            public int LevelsSinceLastInterstitial { get; set; }
            public int Shown;
            public void OnInterstitialShown() => Shown++;
        }

        [TearDown]
        public void Reset() => AdManager.Policy = null;

        [Test]
        public void NoPolicyOrAdsRemoved_CallsBackImmediately_WithoutCounting()
        {
            var go = new GameObject("ads");
            var ads = go.AddComponent<AdManager>();
            bool ready = false;

            AdManager.Policy = null;
            ads.NotifyLevelLoaded(() => ready = true);
            Assert.IsTrue(ready);

            var policy = new FakePolicy { AdsRemoved = true, LevelsSinceLastInterstitial = 3 };
            AdManager.Policy = policy;
            ready = false;
            ads.NotifyLevelLoaded(() => ready = true);
            Assert.IsTrue(ready);
            Assert.AreEqual(3, policy.LevelsSinceLastInterstitial);   // untouched with Remove Ads

            Object.DestroyImmediate(go);
        }

        [Test]
        public void CountsLevelLoads_BelowTheInterval()
        {
            var go = new GameObject("ads");
            var ads = go.AddComponent<AdManager>();
            var policy = new FakePolicy();
            AdManager.Policy = policy;
            bool ready = false;

            ads.NotifyLevelLoaded(() => ready = true);

            Assert.IsTrue(ready);
            Assert.AreEqual(1, policy.LevelsSinceLastInterstitial);
            Assert.AreEqual(0, policy.Shown);
            Object.DestroyImmediate(go);
        }
    }
}
