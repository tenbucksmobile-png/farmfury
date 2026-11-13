using System;
using UnityEngine;
using UnityEngine.UI;

namespace FarmFury.Shared.UI
{
    /// <summary>
    /// The New Character page's look (the 2026-09-29 mockup), shared by the rare-pellet reveal in a level
    /// (RarePelletCelebration) and the unlock page after Level Complete (NewCharacterScreen): the sunset backdrop with
    /// the logo painted in (MenuArt.newCharacterBackground), the "New Character" lettering (MenuArt.newCharacterTitle)
    /// across the top, and the character's framed card (name baked in) in the middle over the sun, with golden rays
    /// turning behind it and confetti. Everything is placed in the backdrop's 1280x720 pixels, fitted whole inside the
    /// screen like the results art, so it scales as one piece. Without the backdrop: a dark veil and the same layout.
    /// Callers own the animation and the tap handling.
    /// </summary>
    public class NewCharacterPage
    {
        private static readonly Rect TitleBox = new(380f, 48f, 520f, 218f);    // the lettering is 438x184
        private static readonly Rect CardBox = new(500f, 330f, 280f, 280f);
        private static readonly Rect RaysBox = new(265f, 95f, 750f, 750f);     // centred on the card
        private static readonly Rect MessageBox = new(140f, 620f, 1000f, 70f);
        private static readonly Color Gold = new(1f, 0.84f, 0.25f, 1f);
        private static readonly Color OutlineColor = new(0.24f, 0.11f, 0.03f, 1f);

        /// <summary>The backdrop (or, without art, a transparent frame): parent of everything else.</summary>
        public readonly RectTransform Frame;
        public readonly Graphic Title;
        public readonly Image Rays, Card, Flash;
        /// <summary>Shown only for a character without a framed card, and for "every character is unlocked".</summary>
        public readonly Text Message;
        public readonly ConfettiBurst Confetti;
        public readonly bool HasBackdrop;
        private readonly Image _backdrop;
        private readonly Sprite _defaultBackdrop;
        private readonly Func<Sprite> _worldBackdrop;

        /// <param name="worldBackdrop">The current world's own New Character backdrop, or null for the shared one.</param>
        public NewCharacterPage(RectTransform parent, MenuArtBase art, Func<Sprite> worldBackdrop = null)
        {
            art ??= new MenuArtBase();
            _worldBackdrop = worldBackdrop;
            HasBackdrop = art.newCharacterBackground != null;

            Image frameImage = HasBackdrop ? UIKit.Picture(parent, "Backdrop", art.newCharacterBackground) : null;
            Frame = frameImage != null ? frameImage.rectTransform : UIKit.NewRect("Frame", parent);
            if (frameImage != null) { frameImage.preserveAspect = false; }
            _backdrop = frameImage;
            _defaultBackdrop = art.newCharacterBackground;
            var fit = Frame.gameObject.AddComponent<FitContent>();
            fit.contentSize = new Vector2(1280f, 720f);
            fit.spriteSize = new Vector2(1280f + 2f * WideArt.Pad, 720f);

            Rays = UIKit.Picture(Frame, "Rays", SunburstSprite());
            Rays.color = new Color(1f, 0.9f, 0.5f, 0.6f);
            WideArt.Place(Rays.rectTransform, RaysBox);

            if (art.newCharacterTitle != null)
            {
                Title = UIKit.Picture(Frame, "Title", art.newCharacterTitle);
            }
            else
            {
                var text = UIKit.Label(Frame, "Title", "New Character", 110, TextAnchor.MiddleCenter, Gold);
                text.fontStyle = FontStyle.Bold;
                text.resizeTextForBestFit = true;
                text.resizeTextMaxSize = 140;
                var outline = text.gameObject.AddComponent<Outline>();
                outline.effectDistance = new Vector2(4f, -4f);
                outline.effectColor = OutlineColor;
                Title = text;
            }
            WideArt.Place(Title.rectTransform, TitleBox);

            Card = UIKit.Picture(Frame, "CharacterCard", null);
            WideArt.Place(Card.rectTransform, CardBox);
            Flash = UIKit.Picture(Card.transform, "Flash", null);
            UIKit.Stretch(Flash.rectTransform);

            Message = UIKit.Label(Frame, "Message", "", 56, TextAnchor.MiddleCenter, Gold);
            Message.fontStyle = FontStyle.Bold;
            Message.horizontalOverflow = HorizontalWrapMode.Overflow;
            var messageOutline = Message.gameObject.AddComponent<Outline>();
            messageOutline.effectDistance = new Vector2(3f, -3f);
            messageOutline.effectColor = OutlineColor;
            WideArt.Place(Message.rectTransform, MessageBox);

            var particles = UIKit.NewRect("Confetti", parent);
            UIKit.Stretch(particles);
            Confetti = particles.gameObject.AddComponent<ConfettiBurst>();
            Confetti.particlesRoot = particles;
        }

        /// <summary>
        /// Shows the current level's world's own backdrop (Frozen Tundra's aurora) when it has one, otherwise the
        /// shared sunset. Call each time the page opens.
        /// </summary>
        /// <summary>The current level's world's New Character backdrop when it has one, else the shared one.</summary>
        public void UseWorldBackdrop()
        {
            if (_backdrop == null) { return; }
            _backdrop.sprite = MenuArtBase.Either(_worldBackdrop?.Invoke(), _defaultBackdrop);
        }

        /// <summary>Shows the animal's framed card (name baked in); without a card, its name as text.</summary>
        /// <summary>The unlocked character's card (name baked in); without art, fallbackName as text.</summary>
        public void SetCharacter(Sprite card, string fallbackName)
        {
            Card.sprite = card;
            Card.gameObject.SetActive(true);
            Card.color = card != null ? Color.white : new Color(0f, 0f, 0f, 0f);
            Flash.sprite = card;
            Flash.preserveAspect = true;
            Flash.gameObject.SetActive(card != null);
            Flash.color = new Color(1f, 1f, 1f, 0f);
            Message.text = card == null ? fallbackName : "";
        }

        public void Spin()
        {
            Rays.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -Time.unscaledTime * 25f);
        }

        /// <summary>Throws the confetti over everything.</summary>
        public void Burst()
        {
            Confetti.transform.SetAsLastSibling();
            Confetti.Burst(110, 3f);
        }

        // Soft golden rays, made once.
        private static Sprite _sunburst;

        public static Sprite SunburstSprite()
        {
            if (_sunburst != null) { return _sunburst; }
            const int size = 256, rays = 14;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - size * 0.5f, dy = y + 0.5f - size * 0.5f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy) / (size * 0.5f);
                    float angle = Mathf.Atan2(dy, dx);
                    float ray = Mathf.Pow(Mathf.Abs(Mathf.Cos(angle * rays * 0.5f)), 6f);
                    float fade = Mathf.Clamp01(1f - r);
                    float glow = Mathf.Clamp01(1f - r * 2.2f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(ray * fade * 0.9f + glow * 0.8f)));
                }
            }
            tex.Apply();
            _sunburst = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            return _sunburst;
        }
    }
}
