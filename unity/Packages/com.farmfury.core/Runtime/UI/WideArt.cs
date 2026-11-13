using UnityEngine;

namespace FarmFury.Shared.UI
{
    /// <summary>
    /// The franchise's full-screen art convention: pages are painted at 1280x720, and setup writes *_Wide.png copies
    /// with <see cref="Pad"/> mirrored columns each side, so a phone wider than 16:9 sees art instead of bars. A
    /// FitContent with contentSize 1280x720 and spriteSize (1280 + 2 Pad) x 720 keeps the middle whole.
    /// </summary>
    public static class WideArt
    {
        public const int Pad = 160;
        public static readonly Vector2 Size = new(1280f, 720f);

        /// <summary>Sets the FitContent for a widened page backdrop.</summary>
        public static FitContent Fit(GameObject backdrop)
        {
            var fit = backdrop.AddComponent<FitContent>();
            fit.contentSize = Size;
            fit.spriteSize = new Vector2(Size.x + 2f * Pad, Size.y);
            return fit;
        }

        /// <summary>Anchors rt to box, given in pixels of the 1280x720 art (y down), inside a widened backdrop. The pivot
        /// is left as it is (same as the games' ResultsScreen.PlaceInArt).</summary>
        public static void Place(RectTransform rt, Rect box)
        {
            float wide = Size.x + 2f * Pad;
            rt.anchorMin = new Vector2((box.xMin + Pad) / wide, 1f - box.yMax / Size.y);
            rt.anchorMax = new Vector2((box.xMax + Pad) / wide, 1f - box.yMin / Size.y);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
