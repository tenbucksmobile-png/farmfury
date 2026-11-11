using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FarmFury.UI
{
    /// <summary>
    /// Tiny uGUI construction helpers so every screen is built in code (placeholder look, no prefabs to keep in
    /// sync). Phase-5b+ art passes can restyle here in one place. Uses the built-in legacy font.
    /// </summary>
    public static class UIKit
    {
        public static readonly Color Dark = new Color(0.07f, 0.09f, 0.15f, 0.96f);
        public static readonly Color Card = new Color(0.16f, 0.20f, 0.30f, 1f);
        public static readonly Color CardLocked = new Color(0.12f, 0.12f, 0.14f, 1f);
        public static readonly Color Accent = new Color(0.95f, 0.72f, 0.2f, 1f);
        public static readonly Color Good = new Color(0.3f, 0.75f, 0.4f, 1f);
        public static readonly Color Muted = new Color(0.55f, 0.58f, 0.65f, 1f);

        /// <summary>Every round menu button (play, settings, home, back) is this size; Exit is scaled to the same height.</summary>
        public const float RoundButtonSize = 136f;   // 160 (Arcade StandardIconButtonSize) -> 136 2026-11-11, user: "slightly shrink the button sizes"; was 120 until 2026-10-30 (too small on an iPhone)

        private static Font _font;

        public static Font DefaultFont
        {
            get
            {
                if (_font == null)
                {
                    _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                }
                return _font;
            }
        }

        public static Canvas CreateCanvas(string canvasName, int sortingOrder, Transform parent)
        {
            var go = new GameObject(canvasName, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;

            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            go.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        /// <summary>Makes sure clicks can reach the buttons: an EventSystem with a UI input module.</summary>
        public static void EnsureEventSystem(Transform parent)
        {
            if (Object.FindAnyObjectByType<EventSystem>() != null)
            {
                return;
            }

            // FarmFury runs the Input System only (activeInputHandler = 1), unlike Stampede (both), so the legacy
            // StandaloneInputModule would throw every frame here.
            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
            go.transform.SetParent(parent, false);
        }

        public static RectTransform NewRect(string rectName, Transform parent)
        {
            var go = new GameObject(rectName, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static void Stretch(RectTransform rt, float left = 0f, float bottom = 0f, float right = 0f, float top = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
        }

        /// <summary>Anchors to a point on the parent (0-1 each axis) with a pivot, an offset and a fixed size.</summary>
        public static void Place(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
        }

        public static Image Panel(Transform parent, string panelName, Color color)
        {
            var rt = NewRect(panelName, parent);
            var image = rt.gameObject.AddComponent<Image>();
            image.color = color;
            return image;
        }

        public static Text Label(Transform parent, string labelName, string text, int size,
            TextAnchor anchor = TextAnchor.MiddleCenter, Color? color = null)
        {
            var rt = NewRect(labelName, parent);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = DefaultFont;
            t.text = text;
            t.fontSize = size;
            t.alignment = anchor;
            t.color = color ?? Color.white;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        public static Button MakeButton(Transform parent, string buttonName, string label, Color color, UnityAction onClick, int fontSize = 34)
        {
            var image = Panel(parent, buttonName, color);
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;

            var colors = button.colors;
            colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            colors.disabledColor = new Color(0.35f, 0.35f, 0.38f, 1f);
            button.colors = colors;

            var text = Label(image.transform, "Label", label, fontSize);
            Stretch(text.rectTransform);
            if (onClick != null)
            {
                button.onClick.AddListener(onClick);
            }
            return button;
        }

        /// <summary>
        /// A full-screen image behind a screen's content, scaled to cover the parent (cropping the overflow rather
        /// than stretching) whatever the device aspect. Inserted as the first child so everything draws over it.
        /// Hidden while it has no sprite.
        /// </summary>
        public static Image Backdrop(Transform parent)
        {
            var image = Panel(parent, "Backdrop", Color.white);
            image.raycastTarget = false;
            image.transform.SetAsFirstSibling();
            Stretch(image.rectTransform);
            var fitter = image.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            image.gameObject.SetActive(false);
            return image;
        }

        /// <summary>Shows the sprite in a backdrop made by <see cref="Backdrop"/> (null hides it), tinted by brightness.</summary>
        public static void SetBackdrop(Image backdrop, Sprite sprite, float brightness = 1f)
        {
            backdrop.sprite = sprite;
            backdrop.color = new Color(brightness, brightness, brightness, 1f);
            backdrop.gameObject.SetActive(sprite != null);
            if (sprite != null)
            {
                backdrop.GetComponent<AspectRatioFitter>().aspectRatio = sprite.rect.width / sprite.rect.height;
            }
        }

        /// <summary>A non-interactive image that keeps the sprite's aspect inside its rect. Hidden when the sprite is null.</summary>
        public static Image Picture(Transform parent, string pictureName, Sprite sprite)
        {
            var image = Panel(parent, pictureName, Color.white);
            image.sprite = sprite;
            image.preserveAspect = true;
            image.raycastTarget = false;
            image.gameObject.SetActive(sprite != null);
            return image;
        }

        /// <summary>Three square "stars": gold when earned, dim when not. (Avoids glyphs the built-in font lacks.)</summary>
        public static Image[] StarBar(Transform parent, Vector2 anchor, Vector2 pivot, Vector2 position, float starSize)
        {
            var bar = NewRect("Stars", parent);
            Place(bar, anchor, pivot, position, new Vector2(starSize * 3.6f, starSize));

            var stars = new Image[3];
            for (int i = 0; i < 3; i++)
            {
                stars[i] = Panel(bar, $"Star{i + 1}", Muted);
                Place(stars[i].rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(i * starSize * 1.3f, 0f), new Vector2(starSize, starSize));
            }
            return stars;
        }

        public static void SetStars(Image[] bar, int earned)
        {
            for (int i = 0; i < bar.Length; i++)
            {
                bar[i].color = i < earned ? Accent : new Color(0.3f, 0.3f, 0.35f, 1f);
            }
        }
    }
}
