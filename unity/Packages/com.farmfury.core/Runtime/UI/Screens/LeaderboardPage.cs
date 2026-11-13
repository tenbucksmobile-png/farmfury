using UnityEngine;
using UnityEngine.UI;

namespace FarmFury.Shared.UI
{
    /// <summary>
    /// Shared look of the two Leaderboard screens, copied from the Leaderboard mockups (1280x720): the Settings
    /// backdrop every Settings / Shop page shares (OverlayScreen, ShopArtBase.pageBackground), the LeaderBoard sign top-right and the round back button bottom-right.
    /// Everything on top is placed in the mockup's pixel coordinates on a 16:9 board fitted inside the device safe
    /// area, so it scales as one piece and never leaves the safe area on any aspect.
    /// </summary>
    public abstract class LeaderboardPage : OverlayScreen
    {
        public static readonly Vector2 ArtSize = new(1280f, 720f);
        private static readonly Rect SignBox = new(862f, 44f, 326f, 182f);
        private static readonly Rect BackBox = new(1120f, 576f, 88f, 88f);
        protected static readonly Color ValueGold = new(1f, 0.84f, 0.25f, 1f);
        protected static readonly Color ValueOutline = new(0.24f, 0.11f, 0.03f, 1f);

        protected readonly LeaderboardArt Boards;
        /// <summary>The mockup's 1280x720 frame, fitted inside the safe area; place children with <see cref="PlaceInArt"/>.</summary>
        protected readonly RectTransform Board;

        protected LeaderboardPage(Transform canvas, string name, MenuArtBase art, ShopArtBase shop, LeaderboardArt boards)
            : base(canvas, name, art, shop)
        {
            Boards = boards ?? new LeaderboardArt();

            Board = UIKit.NewRect("Board", Safe);
            var boardFit = Board.gameObject.AddComponent<FitContent>();
            boardFit.contentSize = ArtSize;
            boardFit.spriteSize = ArtSize;

            BackButton.transform.SetParent(Board, false);
            PlaceInArt(BackButton.image.rectTransform, BackBox);

            if (Shop.leaderboardSign != null)
            {
                PlaceInArt(UIKit.Picture(Board, "LeaderboardSign", Shop.leaderboardSign).rectTransform, SignBox);
            }
            else
            {
                PlaceInArt(OutlinedText(Board, "LeaderboardTitle", "LEADERBOARD", UIKit.Accent).rectTransform, SignBox);
            }
        }

        /// <summary>Stretches rt over a box given in the mockup's pixels (top-left origin, 1280x720).</summary>
        protected static void PlaceInArt(RectTransform rt, Rect box)
        {
            rt.anchorMin = new Vector2(box.xMin / ArtSize.x, 1f - box.yMax / ArtSize.y);
            rt.anchorMax = new Vector2(box.xMax / ArtSize.x, 1f - box.yMin / ArtSize.y);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        /// <summary>A box of the given size centred on a mockup point.</summary>
        protected static Rect Centred(float x, float y, float width, float height) =>
            new(x - width * 0.5f, y - height * 0.5f, width, height);

        /// <summary>Bold outlined text that sizes itself to its box (Best Fit), so it scales with the board.</summary>
        protected static Text OutlinedText(Transform parent, string name, string text, Color color)
        {
            var label = UIKit.Label(parent, name, text, 120, TextAnchor.MiddleCenter, color);
            label.fontStyle = FontStyle.Bold;
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 8;
            label.resizeTextMaxSize = 120;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            label.gameObject.AddComponent<Outline>().effectDistance = new Vector2(3f, -3f);
            label.GetComponent<Outline>().effectColor = ValueOutline;
            return label;
        }
    }
}
