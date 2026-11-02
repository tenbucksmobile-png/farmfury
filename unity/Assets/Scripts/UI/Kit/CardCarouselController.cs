using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FarmFury.UI
{
    /// <summary>
    /// Horizontal card carousel, ported from Arcade's CardCarouselController (its Choose Character picker): cards
    /// sit along a shallow arc around a continuous offset (in item-index units); the centred card is full size, the
    /// others shrink and dip away from it. Dragging moves the offset directly and releasing snaps to the nearest
    /// card. Tapping the centred card invokes the callback; tapping any other card only re-centres on it, so a stray
    /// tap mid-flick can't commit to the wrong one. The owner creates the cards; this only positions and scales them.
    /// Stampede change: sizes follow this container's height (the cards are squares the container's height times
    /// <see cref="cardFraction"/>, spaced <see cref="spacingFactor"/> cards apart), so the carousel scales with a
    /// fitted backdrop instead of using fixed pixels. Runs on unscaled time (it is shown over a frozen level).
    /// Sits on the container's own invisible raycast Image so a drag that starts between cards still registers.
    /// </summary>
    public class CardCarouselController : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public float cardFraction = 0.9f;
        public float spacingFactor = 1.1f;
        public float sideScale = 0.72f;
        public float snapSpeed = 10f;
        /// <summary>Arc radius in card spacings (Arcade: 2800 / 380).</summary>
        public float arcRadiusInSpacings = 7.4f;

        private readonly List<RectTransform> _items = new();
        private readonly List<Button> _buttons = new();
        private readonly List<int> _drawOrder = new();

        private float _offset;
        private float _targetOffset;
        private bool _dragging;
        private Action<int> _onCenterTapped;

        /// <summary>The card nearest the centre.</summary>
        public int CenterIndex => Mathf.Clamp(Mathf.RoundToInt(_targetOffset), 0, Mathf.Max(0, _items.Count - 1));

        private float CardSize => ((RectTransform)transform).rect.height * cardFraction;
        private float Spacing => Mathf.Max(1f, CardSize * spacingFactor);

        /// <summary>Replaces the cards (does not destroy GameObjects - the owner does) and centres startIndex.</summary>
        public void SetItems(IReadOnlyList<RectTransform> items, IReadOnlyList<Button> buttons, int startIndex, Action<int> onCenterTapped)
        {
            ClearListeners();
            _items.Clear();
            _buttons.Clear();
            _onCenterTapped = onCenterTapped;

            for (int i = 0; i < items.Count; i++)
            {
                _items.Add(items[i]);
                _buttons.Add(buttons[i]);
                int capturedIndex = i;
                buttons[i].onClick.AddListener(() => OnItemTapped(capturedIndex));
            }

            _dragging = false;
            _offset = _targetOffset = items.Count > 0 ? Mathf.Clamp(startIndex, 0, items.Count - 1) : 0f;
            ApplyLayout();
        }

        /// <summary>Moves the centre by step cards (keyboard / gamepad), clamped to the ends.</summary>
        public void Step(int step)
        {
            if (_items.Count == 0) { return; }
            _targetOffset = Mathf.Clamp(Mathf.Round(_targetOffset) + step, 0, _items.Count - 1);
        }

        private void ClearListeners()
        {
            foreach (var button in _buttons)
            {
                if (button != null) { button.onClick.RemoveAllListeners(); }
            }
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (_items.Count > 0) { _dragging = true; }
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!_dragging || _items.Count == 0) { return; }
            // Screen pixels -> this rect's units, so a drag tracks the finger at any canvas scale.
            var canvas = GetComponentInParent<Canvas>();
            float scale = canvas != null ? canvas.rootCanvas.scaleFactor : 1f;
            _offset = Mathf.Clamp(_offset - eventData.delta.x / scale / Spacing, 0f, _items.Count - 1);
            _targetOffset = _offset;
            ApplyLayout();
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            _dragging = false;
            if (_items.Count > 0) { _targetOffset = Mathf.Clamp(Mathf.Round(_offset), 0, _items.Count - 1); }
        }

        private void Update()
        {
            if (_items.Count == 0) { return; }
            if (!_dragging && !Mathf.Approximately(_offset, _targetOffset))
            {
                _offset = Mathf.Lerp(_offset, _targetOffset, Time.unscaledDeltaTime * snapSpeed);
                if (Mathf.Abs(_offset - _targetOffset) < 0.001f) { _offset = _targetOffset; }
            }
            ApplyLayout();   // every frame: the container's size follows the fitted backdrop
        }

        private void ApplyLayout()
        {
            float size = CardSize, spacing = Spacing, radius = spacing * arcRadiusInSpacings;
            for (int i = 0; i < _items.Count; i++)
            {
                var rect = _items[i];
                if (rect == null) { continue; }
                float delta = i - _offset;
                float angle = delta * (spacing / radius);
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(size, size);
                rect.anchoredPosition = new Vector2(radius * Mathf.Sin(angle), -radius * (1f - Mathf.Cos(angle)));
                float scale = Mathf.Lerp(1f, sideScale, Mathf.Clamp01(Mathf.Abs(delta)));
                rect.localScale = new Vector3(scale, scale, 1f);
            }

            // Nearest-to-centre card drawn last (on top). Sorts a scratch list: _items' order is the index space.
            _drawOrder.Clear();
            for (int i = 0; i < _items.Count; i++) { _drawOrder.Add(i); }
            _drawOrder.Sort((a, b) => Mathf.Abs(b - _offset).CompareTo(Mathf.Abs(a - _offset)));
            foreach (int index in _drawOrder) { _items[index].SetAsLastSibling(); }
        }

        private void OnItemTapped(int index)
        {
            if (_items.Count == 0) { return; }
            if (Mathf.RoundToInt(_offset) == index && Mathf.Approximately(_offset, _targetOffset))
            {
                _onCenterTapped?.Invoke(index);
            }
            else
            {
                _targetOffset = index;
            }
        }
    }
}
