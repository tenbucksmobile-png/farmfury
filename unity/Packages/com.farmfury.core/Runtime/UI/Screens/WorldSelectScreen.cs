using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace FarmFury.Shared.UI
{
    /// <summary>
    /// World Select: a carousel of world badges, laid out like Arcade's world select (its CardCarouselController,
    /// already ported for the in-level character swap) - the six free story worlds (GDD Section 7), then the three
    /// paid post-finale worlds (WorldView.PurchaseRequired), always last (OrderPaidLast). Each badge is the world's wooden shield
    /// (WorldData.selectBadge, name baked in); a world without one yet shows its World Select card art instead, and
    /// without either a coloured panel with the name as text. Drag, or tap a side badge, to bring it to the centre;
    /// tapping the centred badge opens the world's Level Select. A story world unlocks once the previous world's boss
    /// level has been completed (only Meadow Ruins is open on a fresh save); a locked one is greyed and can be browsed
    /// but not entered. A paid world not yet bought stays in full colour (no price sign on it since 2026-10-17), and
    /// tapping it opens the world shop. The carousel opens centred on the furthest world the
    /// player has unlocked. Left/Right (A/D) and Enter/Space work on a keyboard. The carousel fills the safe area
    /// below the wooden banner header; the screen sits on the sunset-farm backdrop (Canvas.png), cover-cropped to the
    /// device. The round home button (Btn_home.png) top-left of the safe area returns to the landing screen.
    /// </summary>
    /// <typeparam name="TWorld">The game's world enum: one badge per value.</typeparam>
    public class WorldSelectScreen<TWorld> where TWorld : struct, Enum
    {
        public class Card
        {
            public TWorld world;
            public GameObject root;
            public Button button;
            public bool unlocked;
            public bool forSale;
        }

        public GameObject Root { get; private set; }
        public Button HomeButton { get; private set; }
        public IReadOnlyList<Card> Cards => _cards;

        private readonly List<Card> _cards = new();
        private readonly List<RectTransform> _items = new();
        private readonly List<Button> _buttons = new();
        private readonly CardCarouselController _carousel;
        private static readonly Color LockedTint = new(0.4f, 0.4f, 0.42f, 1f);
        // Same size as every other round menu button.
        private static float HomeButtonSize => UIKit.RoundButtonSize;   // settable since core 0.2.0
        private const float EdgeMargin = 40f;
        private const float HomeMargin = 16f;   // Level Select's back-button margin
        // Space kept for the banner above the carousel (from the top of the safe area), and below it.
        private const float CarouselTop = 215f, CarouselBottom = 24f;
        // Arcade's world carousel proportions: 580-wide badges 430 apart on a 2800 arc, side badges at 72%.
        private const float BadgeFraction = 0.8f;
        private const float BadgeSpacing = 430f / 580f;
        private const float ArcRadiusInSpacings = 2800f / 430f;

        private readonly Action<TWorld> _onEnter;
        private readonly Func<TWorld, WorldView> _world;
        private readonly Action _onBuyWorld;

        /// <param name="worldData">The game's world data (IMenuData.World).</param>
        public WorldSelectScreen(Transform canvas, Func<TWorld, WorldView> worldData, Action<TWorld> onEnter, Action onBuyWorld, Action onHome, MenuArtBase art)
        {
            art ??= new MenuArtBase();
            _world = worldData;
            _onEnter = onEnter;
            _onBuyWorld = onBuyWorld;
            var root = UIKit.NewRect("WorldSelect", canvas);
            UIKit.Stretch(root);
            Root = root.gameObject;
            var bg = Root.AddComponent<Image>();
            bg.color = UIKit.Dark;
            UIKit.SetBackdrop(UIKit.Backdrop(root), art.worldSelectBackground);

            var safe = UIKit.NewRect("SafeArea", root);
            safe.gameObject.AddComponent<SafeAreaFitter>();

            if (art.worldSelectBanner != null)
            {
                var banner = UIKit.Picture(safe, "HeaderBanner", art.worldSelectBanner);
                UIKit.Place(banner.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -22f), new Vector2(760f, 186f));
            }
            else
            {
                var title = UIKit.Label(safe, "Title", "SELECT A WORLD", 64, TextAnchor.MiddleCenter, UIKit.Accent);
                UIKit.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(1200f, 90f));
            }

            // The carousel below the banner, full width so the side badges fan out to the screen edges. Its invisible
            // raycast Image catches drags that start between badges.
            var area = UIKit.NewRect("Carousel", safe);
            UIKit.Stretch(area, 0f, CarouselBottom, 0f, CarouselTop);
            var dragArea = area.gameObject.AddComponent<Image>();
            dragArea.color = new Color(0f, 0f, 0f, 0f);
            _carousel = area.gameObject.AddComponent<CardCarouselController>();
            _carousel.cardFraction = BadgeFraction;
            _carousel.spacingFactor = BadgeSpacing;
            _carousel.arcRadiusInSpacings = ArcRadiusInSpacings;

            foreach (TWorld world in Enum.GetValues(typeof(TWorld)))
            {
                // The whole badge is the button. Every badge stays interactable so a locked one can still be tapped
                // to the centre; Tapped() decides what the centred one does.
                var button = UIKit.MakeButton(area, $"Card_{world}", "", UIKit.Card, null);
                var card = button.image;
                card.preserveAspect = true;

                var name = UIKit.Label(card.transform, "Name", world.ToString(), 40, TextAnchor.MiddleCenter);
                name.raycastTarget = false;
                name.resizeTextForBestFit = true;
                name.resizeTextMinSize = 10;
                UIKit.Stretch(name.rectTransform, 12f, 12f, 12f, 12f);

                _cards.Add(new Card { world = world, root = card.gameObject, button = button });
                _items.Add(card.rectTransform);
                _buttons.Add(button);
            }

            HomeButton = UIKit.MakeButton(safe, "HomeButton", art.homeButton != null ? "" : "Home",
                new Color(0.62f, 0.38f, 0.17f, 1f), () => onHome?.Invoke(), 34);
            if (art.homeButton != null)
            {
                HomeButton.image.sprite = art.homeButton;
                HomeButton.image.color = Color.white;
                HomeButton.image.preserveAspect = true;
            }
            // Bottom-left of the safe area, where Level Select keeps its back button (was top-left until 2026-10-30).
            UIKit.Place(HomeButton.image.rectTransform, Vector2.zero, Vector2.zero, new Vector2(HomeMargin, HomeMargin),
                new Vector2(HomeButtonSize, HomeButtonSize));

            Root.SetActive(false);
        }

        private void Tapped(int index)
        {
            if (index < 0 || index >= _cards.Count) { return; }
            var card = _cards[index];
            if (card.unlocked) { _onEnter?.Invoke(card.world); }
            else if (card.forSale) { _onBuyWorld?.Invoke(); }
        }

        /// <summary>Keyboard while showing: Left/Right (or A/D) move the carousel, Enter/Space opens the centred world.</summary>
        public void HandleKeys(Keyboard keyboard)
        {
            if (!Root.activeSelf || keyboard == null || _cards.Count == 0) { return; }
            if (keyboard.leftArrowKey.wasPressedThisFrame || keyboard.aKey.wasPressedThisFrame) { _carousel.Step(-1); }
            else if (keyboard.rightArrowKey.wasPressedThisFrame || keyboard.dKey.wasPressedThisFrame) { _carousel.Step(1); }
            else if (keyboard.enterKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame) { Tapped(_carousel.CenterIndex); }
        }

        /// <summary>
        /// Re-reads the save into the badges. Centres the furthest unlocked world, or with keepPosition (the world
        /// shop closing over this screen) stays on the badge already centred.
        /// </summary>
        public void Refresh(bool keepPosition = false)
        {
            OrderPaidLast();
            int furthest = 0;
            for (int i = 0; i < _cards.Count; i++)
            {
                var card = _cards[i];
                var data = _world(card.world);
                card.unlocked = data.Unlocked;
                card.forSale = !card.unlocked && data.PurchaseRequired;
                if (card.unlocked && !data.PurchaseRequired) { furthest = i; }

                // The badge has the world name baked in; a locked story world's is greyed out, a paid world on sale
                // stays bright under its price sign.
                var cardImage = card.root.GetComponent<Image>();
                var art = data.Badge;
                bool bright = card.unlocked || card.forSale;
                cardImage.sprite = art;
                cardImage.color = art != null
                    ? (bright ? Color.white : LockedTint)
                    : (bright ? UIKit.Card : UIKit.CardLocked);

                var nameLabel = card.root.transform.Find("Name").GetComponent<Text>();
                nameLabel.text = data.HasData && !string.IsNullOrEmpty(data.DisplayName) ? data.DisplayName : card.world.ToString();
                nameLabel.gameObject.SetActive(art == null);

            }

            int start = keepPosition ? _carousel.CenterIndex : furthest;
            _carousel.SetItems(_items, _buttons, start, Tapped);
        }

        // The free story worlds first, the paid worlds (WorldData.purchaseRequired) last, each group in enum
        // order, whatever order the enum lists them in. _items/_buttons follow _cards: they are the carousel's indices.
        private void OrderPaidLast()
        {
            bool Paid(Card card) => _world(card.world).PurchaseRequired;
            var ordered = new List<Card>(_cards);
            ordered.Sort((a, b) => Paid(a) != Paid(b) ? (Paid(a) ? 1 : -1) : a.world.CompareTo(b.world));
            _cards.Clear();
            _cards.AddRange(ordered);
            _items.Clear();
            _buttons.Clear();
            foreach (var card in _cards)
            {
                _items.Add((RectTransform)card.root.transform);
                _buttons.Add(card.button);
            }
        }
    }
}
