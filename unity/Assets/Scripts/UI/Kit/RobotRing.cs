using System.Collections.Generic;
using UnityEngine;

namespace FarmFury.UI
{
    /// <summary>
    /// Keeps a set of UI items (the Robots page's robots, CharacterStoryScreen; ported from FarmFury_Stampede 2026-11-13) evenly spaced on an ellipse round a
    /// centre and turns the ring slowly, on unscaled time so it moves in menus. Items nearer the bottom are drawn a
    /// little bigger and in front, so the ring reads as a circle seen from slightly above.
    /// </summary>
    public class RobotRing : MonoBehaviour
    {
        public Vector2 centre;
        public Vector2 radius = new(300f, 190f);
        [Tooltip("Degrees per second.")]
        public float speed = 7f;

        private readonly List<RectTransform> _items = new();
        private float _angle;

        public void SetItems(IEnumerable<RectTransform> items)
        {
            _items.Clear();
            _items.AddRange(items);
            _angle = 0f;
            Place();
        }

        private void Update()
        {
            if (_items.Count == 0) { return; }
            _angle = Mathf.Repeat(_angle + speed * Time.unscaledDeltaTime, 360f);
            Place();
        }

        private void Place()
        {
            int n = _items.Count;
            for (int i = 0; i < n; i++)
            {
                if (_items[i] == null) { continue; }
                // Start at the bottom (-90 degrees) and go round evenly.
                float a = (-90f + _angle + 360f * i / n) * Mathf.Deg2Rad;
                _items[i].anchoredPosition = centre + new Vector2(Mathf.Cos(a) * radius.x, Mathf.Sin(a) * radius.y);
                float depth = 0.5f - 0.5f * Mathf.Sin(a);   // 1 at the bottom, 0 at the top
                _items[i].localScale = Vector3.one * Mathf.Lerp(0.82f, 1.08f, depth);
            }
            // Front-most (lowest) last, so it draws over the others; the ring's own order (the angles) is left alone.
            _drawOrder.Clear();
            _drawOrder.AddRange(_items);
            _drawOrder.Sort((x, y) => y.anchoredPosition.y.CompareTo(x.anchoredPosition.y));
            foreach (var item in _drawOrder) { if (item != null) { item.SetAsLastSibling(); } }
        }

        private readonly List<RectTransform> _drawOrder = new();
    }
}
