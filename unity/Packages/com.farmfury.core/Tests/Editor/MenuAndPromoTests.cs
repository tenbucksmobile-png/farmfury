using System.Collections.Generic;
using FarmFury.Shared.Monetisation;
using FarmFury.Shared.UI;
using NUnit.Framework;
using UnityEngine;

namespace FarmFury.Shared.Tests
{
    public class ShopPromoPacerTests
    {
        private class State : IPromoState
        {
            public int LevelsSincePromo { get; set; }
            public int InterstitialsSinceAdsCard { get; set; }
            public int PromoTurn { get; set; }
            public bool AdsRemoved { get; set; }
        }

        private static readonly bool[] AllUnowned = { true, true, true };

        private static int Pick(State s, bool[] unowned, bool boss = false, bool newCharacter = false) =>
            ShopPromoPacer.Pick(s, boss, newCharacter, unowned.Length, i => unowned[i]);

        [Test]
        public void ACardEveryThirdLevel_NeverOnABoss()
        {
            var s = new State();
            Assert.AreEqual(ShopPromoPacer.None, Pick(s, AllUnowned));
            Assert.AreEqual(ShopPromoPacer.None, Pick(s, AllUnowned));
            Assert.AreEqual(0, Pick(s, AllUnowned));
            Assert.AreEqual(0, s.LevelsSincePromo);
            s.LevelsSincePromo = 5;
            Assert.AreEqual(ShopPromoPacer.None, Pick(s, AllUnowned, boss: true));
            Assert.AreEqual(5, s.LevelsSincePromo);
        }

        [Test]
        public void CategoriesTakeTurns_SkippingSoldOutOnes()
        {
            var s = new State { LevelsSincePromo = 10 };
            var unowned = new[] { true, false, true };
            Assert.AreEqual(0, Pick(s, unowned));
            s.LevelsSincePromo = 10;
            Assert.AreEqual(2, Pick(s, unowned));
            s.LevelsSincePromo = 10;
            Assert.AreEqual(0, Pick(s, unowned));
            s.LevelsSincePromo = 10;
            Assert.AreEqual(ShopPromoPacer.None, Pick(s, new[] { false, false, false }));
            Assert.AreEqual(10 + 1, s.LevelsSincePromo, "no card: the counter keeps counting");
        }

        [Test]
        public void RemoveAdsCard_AfterTwoInterstitials_UnlessAlreadyBought()
        {
            var s = new State { LevelsSincePromo = 10, InterstitialsSinceAdsCard = 2 };
            Assert.AreEqual(ShopPromoPacer.RemoveAds, Pick(s, AllUnowned));
            Assert.AreEqual(0, s.InterstitialsSinceAdsCard);

            var bought = new State { LevelsSincePromo = 10, InterstitialsSinceAdsCard = 9, AdsRemoved = true };
            Assert.AreEqual(0, Pick(bought, AllUnowned));
        }

        [Test]
        public void NewCharacter_ShowsItsCategoryAtOnce()
        {
            var s = new State { PromoTurn = 1 };
            Assert.AreEqual(0, Pick(s, AllUnowned, newCharacter: true));
            Assert.AreEqual(1, s.PromoTurn, "the turn order is not used up");
        }
    }

    public class MenuSeamTests
    {
        [Test]
        public void StarBoard_And_Either()
        {
            var art = new MenuArtBase { oneStarBoard = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), Vector2.zero) };
            Assert.AreSame(art.oneStarBoard, art.StarBoard(1));
            Assert.IsNull(art.StarBoard(0));
            Assert.IsNull(art.StarBoard(3));
            Assert.AreSame(art.oneStarBoard, MenuArtBase.Either(null, art.oneStarBoard));
        }

        [Test]
        public void WideArt_Place_MapsArtPixelsIntoTheWidenedBackdrop()
        {
            var rt = new GameObject("t", typeof(RectTransform)).GetComponent<RectTransform>();
            WideArt.Place(rt, new Rect(0f, 0f, 1280f, 720f));
            float wide = 1280f + 2f * WideArt.Pad;
            Assert.AreEqual(WideArt.Pad / wide, rt.anchorMin.x, 1e-5f);
            Assert.AreEqual(1f - WideArt.Pad / wide, rt.anchorMax.x, 1e-5f);
            Assert.AreEqual(0f, rt.anchorMin.y, 1e-5f);
            Assert.AreEqual(1f, rt.anchorMax.y, 1e-5f);
            Object.DestroyImmediate(rt.gameObject);
        }

        private enum TestWorld { Meadow, Tundra, Paid }

        private class FakeData : IMenuData<TestWorld, string>
        {
            public WorldView World(TestWorld world) => new WorldView
            {
                HasData = true, DisplayName = world + " World", Unlocked = world == TestWorld.Meadow,
                PurchaseRequired = world == TestWorld.Paid,
            };
            public IReadOnlyList<string> Levels(TestWorld world) => new[] { "a", "b", "boss" };
            public LevelView Level(string level) => new LevelView
            {
                Id = level, Name = level.ToUpper(), IsBoss = level == "boss", Unlocked = level == "a", Completed = level == "a", Stars = level == "a" ? 2 : 0,
            };
        }

        [Test]
        public void LevelSelect_BuildsATilePerLevel_FromTheGamesData()
        {
            var canvas = new GameObject("c", typeof(RectTransform), typeof(Canvas)).transform;
            string picked = null;
            var screen = new LevelSelectScreen<TestWorld, string>(canvas, new FakeData(), l => picked = l, null, null);
            screen.Show(TestWorld.Tundra);
            Assert.AreEqual(3, screen.Tiles.Count);
            Assert.AreEqual(TestWorld.Tundra, screen.World);
            Assert.IsTrue(screen.Tiles[0].unlocked);
            Assert.AreEqual(2, screen.Tiles[0].stars);
            Assert.IsFalse(screen.Tiles[1].button.interactable);
            screen.Tiles[0].button.onClick.Invoke();
            Assert.AreEqual("a", picked);
            Object.DestroyImmediate(canvas.gameObject);
        }

        [Test]
        public void WorldSelect_OneCardPerWorld_PaidLast()
        {
            var canvas = new GameObject("c", typeof(RectTransform), typeof(Canvas)).transform;
            var data = new FakeData();
            var screen = new WorldSelectScreen<TestWorld>(canvas, data.World, null, null, null, null);
            screen.Refresh();
            Assert.AreEqual(3, screen.Cards.Count);
            Assert.AreEqual(TestWorld.Paid, screen.Cards[2].world);
            Assert.IsTrue(screen.Cards[0].unlocked);
            Assert.IsTrue(screen.Cards[2].forSale);
            Object.DestroyImmediate(canvas.gameObject);
        }
    }
}
