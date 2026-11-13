using System;
using UnityEngine;
using UnityEngine.UI;
using FarmFury.Shared.UI;

namespace FarmFury.UI
{
    /// <summary>
    /// Base for the Settings / Shop family of screens ported from Farm Fury: Arcade (menu hub, Settings, Shop, coin
    /// packs, cosmetics, world purchase, Leaderboards, Character Story, Legal, parental gate). Arcade's overlay
    /// convention: each opens on top of whatever is showing (SetAsLastSibling, so it always draws above the screen
    /// that opened it) and its back button just closes it, revealing that screen again - no navigation state.
    /// GameFlow keeps the list: it closes every open overlay on a game-state change, and Esc closes the top one.
    ///
    /// Shared layout, in the 1920x1080 reference canvas with Arcade's numbers: the Settings backdrop
    /// (ShopArt.pageBackground, Cluck bursting through the wooden sign - the Leaderboard's backdrop; its whole 1280x720
    /// middle always on screen, fitted like the results art) over opaque black, or without it the landing poster
    /// dimmed; a 550x310 wood-sign header 55 below the top
    /// centre, and the round back button bottom-right of the safe area (where the landing screen's cog sits).
    /// </summary>
    public abstract class OverlayScreen
    {
        public GameObject Root { get; }
        public Button BackButton { get; }
        public bool IsOpen => Root.activeSelf;

        protected readonly RectTransform Rect;
        protected readonly RectTransform Safe;
        protected readonly MenuArt Art;
        protected readonly ShopArt Shop;

        public const float IconSize = 240f;          // Arcade: StandardIconButtonSize (160) x 1.5
        public const float IconSpacing = 77f;
        public const float ItemHeight = IconSize * 1.4f;   // coin-pack and cosmetic price plaques (500x669 art)
        public const float ItemWidth = ItemHeight * 500f / 669f;
        public static readonly Vector2 HeaderSize = new(550f, 310f);
        public const float HeaderTop = -55f;
        public const float BackdropBrightness = 0.45f;
        private const float EdgeMargin = 70f;
        private const float BottomMargin = 40f;
        public static readonly Color Wood = new(0.62f, 0.38f, 0.17f, 1f);
        public static readonly Color OwnedTint = new(0.5f, 0.5f, 0.5f, 1f);

        protected OverlayScreen(Transform canvas, string name, MenuArt art, ShopArt shop, bool posterBackdrop = true)
        {
            Art = art ?? new MenuArt();
            Shop = shop ?? new ShopArt();
            Rect = UIKit.NewRect(name, canvas);
            UIKit.Stretch(Rect);
            Root = Rect.gameObject;
            Root.AddComponent<Image>().color = posterBackdrop ? Color.black : new Color(0f, 0f, 0f, 0.8f);
            if (posterBackdrop && Shop.pageBackground != null)
            {
                PageBackdrop(Rect, Shop.pageBackground);
            }
            else if (posterBackdrop)
            {
                UIKit.SetBackdrop(UIKit.Backdrop(Rect), Art.landingPoster, BackdropBrightness);
            }

            Safe = UIKit.NewRect("SafeArea", Rect);
            Safe.gameObject.AddComponent<SafeAreaFitter>();
            BackButton = IconButton(Safe, "BackButton", Art.backButton, "<", Hide);
            UIKit.Place(BackButton.image.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(-EdgeMargin, BottomMargin), new Vector2(UIKit.RoundButtonSize, UIKit.RoundButtonSize));

            Root.SetActive(false);
        }

        public void Show()
        {
            Root.transform.SetAsLastSibling();
            Root.SetActive(true);
            OnShow();
        }

        /// <summary>Raised when an open overlay closes (e.g. the Locker refreshes after a purchase page on top of it).</summary>
        public event Action Closed;

        public virtual void Hide()
        {
            bool wasOpen = Root.activeSelf;
            Root.SetActive(false);
            if (wasOpen)
            {
                Closed?.Invoke();
            }
        }

        protected virtual void OnShow() { }

        // ------------------------------------------------------------ shared building blocks

        /// <summary>
        /// The Settings backdrop: the widened 1280x720 art (ResultsScreen.ArtPad mirrored columns each side), fitted so
        /// the middle 1280x720 is always whole and wider screens see the padding instead of bars.
        /// </summary>
        private static void PageBackdrop(RectTransform parent, Sprite sprite)
        {
            var backdrop = UIKit.Picture(parent, "Backdrop", sprite);
            backdrop.preserveAspect = false;
            var fit = backdrop.gameObject.AddComponent<FitContent>();
            fit.contentSize = new Vector2(1280f, 720f);
            fit.spriteSize = new Vector2(1280f + 2f * ResultsScreen.ArtPad, 720f);
            backdrop.transform.SetAsFirstSibling();
        }

        /// <summary>The standard top-centre wood sign; without art, the fallback text in its place.</summary>
        protected void HeaderSign(Sprite sprite, string fallback)
        {
            if (sprite != null)
            {
                var sign = UIKit.Picture(Rect, "TitleImage", sprite);
                UIKit.Place(sign.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, HeaderTop), HeaderSize);
                return;
            }

            var title = UIKit.Label(Rect, "Title", fallback, 80, TextAnchor.MiddleCenter, UIKit.Accent);
            title.fontStyle = FontStyle.Bold;
            title.gameObject.AddComponent<Outline>().effectDistance = new Vector2(4f, -4f);
            UIKit.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -110f), new Vector2(1300f, 120f));
        }

        /// <summary>An art-only button (aspect kept); without art, a plain wooden button with the fallback label.</summary>
        protected static Button IconButton(Transform parent, string name, Sprite sprite, string fallback, Action onClick)
        {
            var button = UIKit.MakeButton(parent, name, sprite != null ? "" : fallback, sprite != null ? Color.white : Wood,
                () => onClick?.Invoke(), 40);
            if (sprite != null)
            {
                button.image.sprite = sprite;
                button.image.preserveAspect = true;
            }
            else
            {
                button.GetComponentInChildren<Text>().resizeTextForBestFit = true;
            }
            return button;
        }

        /// <summary>A text button on the wooden plaque art (9-sliced, so any width keeps its rounded ends).</summary>
        protected Button PlaqueButton(Transform parent, string name, string label, int fontSize, Action onClick)
        {
            var button = UIKit.MakeButton(parent, name, label, Shop.plaque != null ? Color.white : Wood, () => onClick?.Invoke(), fontSize);
            if (Shop.plaque != null)
            {
                button.image.sprite = Shop.plaque;
                button.image.type = Shop.plaque.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
                // The art's border is ~90px at source size; draw it at the button's scale, not full size.
                button.image.pixelsPerUnitMultiplier = Shop.plaque.rect.height / Mathf.Max(1f, 110f);
            }
            var text = button.GetComponentInChildren<Text>();
            text.fontStyle = FontStyle.Bold;
            text.color = Color.white;
            text.gameObject.AddComponent<Outline>().effectDistance = new Vector2(2f, -2f);
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            return button;
        }

        /// <summary>A centred single row of equal cells (a GridLayoutGroup, like Arcade's icon rows), centre at y.</summary>
        protected RectTransform Row(string name, int count, Vector2 cell, float spacing, float y)
        {
            var row = UIKit.NewRect(name, Rect);
            UIKit.Place(row, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, y),
                new Vector2(count * cell.x + (count - 1) * spacing + 40f, cell.y + 40f));
            var grid = row.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = cell;
            grid.spacing = new Vector2(spacing, 0f);
            grid.childAlignment = TextAnchor.MiddleCenter;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = Mathf.Max(1, count);
            return row;
        }

        /// <summary>A one-line message centred along the bottom (purchase feedback), empty until something happens.</summary>
        protected Text StatusText(float bottom)
        {
            var status = UIKit.Label(Rect, "StatusText", "", 34, TextAnchor.MiddleCenter, Color.white);
            status.gameObject.AddComponent<Outline>().effectDistance = new Vector2(2f, -2f);
            UIKit.Place(status.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, bottom), new Vector2(1100f, 50f));
            return status;
        }

        /// <summary>Wood-sign banner button used on the menu hub and cosmetics chooser: top-centre, offset down from the top.</summary>
        protected Button BannerButton(string name, Sprite sprite, string fallback, Vector2 size, float top, Action onClick)
        {
            var button = IconButton(Rect, name, sprite, fallback, onClick);
            UIKit.Place(button.image.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, top), size);
            return button;
        }
    }
}
