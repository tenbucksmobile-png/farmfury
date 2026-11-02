using System;
using UnityEngine;

namespace FarmFury.UI
{
    /// <summary>
    /// Keeps its RectTransform inside the device safe area (Screen.safeArea: clear of notches, rounded corners and
    /// the home indicator) by setting its anchors. Put content that must stay tappable/visible under it; full-screen
    /// backdrops stay outside so they still reach the screen edges. Re-fits when the safe area or resolution changes
    /// (rotation, Device Simulator).
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class SafeAreaFitter : MonoBehaviour
    {
        private Rect _applied;
        private Vector2Int _screen;

        /// <summary>Raised after the anchors change, so a screen can re-lay out children that depend on the size.</summary>
        public event Action Changed;

        /// <summary>The safe area as 0-1 fractions of the screen (min, max).</summary>
        public static (Vector2 min, Vector2 max) Normalized()
        {
            var safe = Screen.safeArea;
            float w = Mathf.Max(Screen.width, 1), h = Mathf.Max(Screen.height, 1);
            return (new Vector2(safe.xMin / w, safe.yMin / h), new Vector2(safe.xMax / w, safe.yMax / h));
        }

        private void OnEnable() => Apply(true);

        private void Update() => Apply(false);

        private void Apply(bool force)
        {
            var screen = new Vector2Int(Screen.width, Screen.height);
            if (!force && Screen.safeArea == _applied && screen == _screen)
            {
                return;
            }

            _applied = Screen.safeArea;
            _screen = screen;
            var (min, max) = Normalized();
            var rt = (RectTransform)transform;
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            Changed?.Invoke();
        }
    }
}
