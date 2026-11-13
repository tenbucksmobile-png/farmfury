using FarmFury.Core;
using UnityEngine;
using UnityEngine.UI;
using FarmFury.Shared.UI;

namespace FarmFury.UI
{
    /// <summary>
    /// The World Unlocked page after a world's boss falls, when the clear opened the next world — ported from
    /// FarmFury_Stampede's WorldClearedScreen (2026-11-02, user request: "remove the transition video ... once the boss
    /// is defeated flow to the new world unlock and badge like stampede, use the animation as well"; it replaces the
    /// L18 -> Frozen Tundra transition video). Level Complete's play button opens it; a tap goes on to World Select
    /// (via the New Worlds promo on a first clear), which opens centred on the new world.
    ///
    /// Stampede's look and animation: the beaten world's backdrop (WorldData.newCharacterBackground, else MenuArt's
    /// sunset) fitted like the results art, the World Unlocked sign popping in (EaseOutBack), the next world's badge
    /// (WorldData.selectBadge) shown as a dark silhouette over turning golden rays, then after a beat flashing to colour
    /// with a scale bump and confetti, and "Next stop: [world]!". Taps in the first MinShowSeconds are ignored so the
    /// moment isn't skipped by accident. Unscaled time throughout. A clear that opens no world (the last story world,
    /// or a paid world) shows nothing here.
    /// </summary>
    public class WorldClearedScreen : OverlayScreen
    {
        // This game's MenuArt (the shared base holds it as MenuArtBase).
        private new MenuArt Art => (MenuArt)base.Art;

        private static readonly Rect TitleBox = new(240f, 40f, 800f, 170f);
        private static readonly Rect SignBox = new(440f, 30f, 400f, 200f);
        private static readonly Rect CardBox = new(490f, 215f, 300f, 300f);    // square world badge
        private static readonly Rect RaysBox = new(265f, -10f, 750f, 750f);    // centred on the badge
        private static readonly Rect MessageBox = new(140f, 520f, 1000f, 80f);
        private static readonly Color Gold = new(1f, 0.84f, 0.25f, 1f);
        private static readonly Color OutlineColor = new(0.24f, 0.11f, 0.03f, 1f);
        private static readonly Color Silhouette = new(0.08f, 0.07f, 0.12f, 1f);
        private const float MinShowSeconds = 1f;

        private readonly Text _title;
        private readonly Image _sign, _rays, _card, _flash;
        private readonly Text _message;
        private readonly ConfettiBurst _confetti;
        private readonly Driver _anim;
        private readonly Image _backdrop;
        private System.Action _onClosed;

        public WorldClearedScreen(Transform canvas, MenuArt art, ShopArt shop) : base(canvas, "WorldCleared", art, shop, posterBackdrop: false)
        {
            BackButton.gameObject.SetActive(false);
            var rootImage = Root.GetComponent<Image>();
            var tap = Root.AddComponent<Button>();
            tap.targetGraphic = rootImage;
            tap.transition = Selectable.Transition.None;
            tap.onClick.AddListener(OnTap);

            bool hasBackdrop = Art.newCharacterBackground != null;
            rootImage.color = hasBackdrop ? Color.black : UIKit.Dark;
            Image frameImage = hasBackdrop ? UIKit.Picture(Rect, "Backdrop", Art.newCharacterBackground) : null;
            var frame = frameImage != null ? frameImage.rectTransform : UIKit.NewRect("Frame", Rect);
            if (frameImage != null) { frameImage.preserveAspect = false; }
            _backdrop = frameImage;
            frame.SetAsFirstSibling();
            var fit = frame.gameObject.AddComponent<FitContent>();
            fit.contentSize = new Vector2(1280f, 720f);
            fit.spriteSize = new Vector2(1280f + 2f * ResultsScreen.ArtPad, 720f);

            _rays = UIKit.Picture(frame, "Rays", NewCharacterPage.SunburstSprite());
            _rays.color = new Color(1f, 0.9f, 0.5f, 0.6f);
            ResultsScreen.PlaceInArt(_rays.rectTransform, RaysBox);

            _title = OutlinedText(frame, "Title", "NEW WORLD UNLOCKED!", 120, 4f);
            ResultsScreen.PlaceInArt(_title.rectTransform, TitleBox);

            _sign = UIKit.Picture(frame, "WorldUnlockedSign", Art.worldUnlockedSign);
            ResultsScreen.PlaceInArt(_sign.rectTransform, SignBox);
            _sign.gameObject.SetActive(false);

            _card = UIKit.Picture(frame, "WorldCard", null);
            ResultsScreen.PlaceInArt(_card.rectTransform, CardBox);
            _flash = UIKit.Picture(_card.transform, "Flash", null);
            UIKit.Stretch(_flash.rectTransform);

            _message = OutlinedText(frame, "Message", "", 56, 3f);
            ResultsScreen.PlaceInArt(_message.rectTransform, MessageBox);

            var particles = UIKit.NewRect("Confetti", Rect);
            UIKit.Stretch(particles);
            _confetti = particles.gameObject.AddComponent<ConfettiBurst>();
            _confetti.particlesRoot = particles;

            _anim = Root.AddComponent<Driver>();
            _anim.Screen = this;
        }

        /// <summary>
        /// Shows the world this boss clear opened (LevelRunState.worldUnlocked) over the beaten world's backdrop and
        /// calls onClosed when tapped away; returns false (showing nothing) when the clear opened no world.
        /// </summary>
        public bool ShowFor(WorldType cleared, WorldType? unlocked, System.Action onClosed = null)
        {
            var data = DataManager.Instance;
            var next = unlocked.HasValue && data != null ? data.GetWorldData(unlocked.Value) : null;
            if (next == null) { return false; }
            _onClosed = onClosed;
            var world = data.GetWorldData(cleared);
            if (_backdrop != null)   // the beaten world's own backdrop, else the shared sunset
            {
                _backdrop.sprite = world != null && world.newCharacterBackground != null ? world.newCharacterBackground : Art.newCharacterBackground;
            }

            Show();
            bool sign = Art.worldUnlockedSign != null;
            _sign.gameObject.SetActive(sign);
            _title.gameObject.SetActive(!sign);
            _message.text = $"Next stop: {next.displayName}!";
            SetCard(next.selectBadge, next.displayName);
            _anim.Play(silhouette: true);
            return true;
        }

        private void OnTap()
        {
            if (Time.unscaledTime - _anim.ShownAt < MinShowSeconds) { return; }
            var then = _onClosed;
            _onClosed = null;
            Hide();
            then?.Invoke();
        }

        // The world's badge (or, without art, its name in the badge's place).
        private void SetCard(Sprite sprite, string worldName)
        {
            _card.sprite = sprite;
            _card.preserveAspect = true;
            _card.gameObject.SetActive(true);
            _card.color = sprite != null ? Color.white : new Color(0f, 0f, 0f, 0f);
            _flash.sprite = sprite;
            _flash.preserveAspect = true;
            _flash.gameObject.SetActive(sprite != null);
            _flash.color = new Color(1f, 1f, 1f, 0f);
            if (sprite == null) { _message.text = worldName; }
        }

        private static Text OutlinedText(Transform parent, string name, string text, int maxSize, float outlineDistance)
        {
            var label = UIKit.Label(parent, name, text, maxSize, TextAnchor.MiddleCenter, Gold);
            label.fontStyle = FontStyle.Bold;
            label.resizeTextForBestFit = true;
            label.resizeTextMaxSize = maxSize;
            var outline = label.gameObject.AddComponent<Outline>();
            outline.effectDistance = new Vector2(outlineDistance, -outlineDistance);
            outline.effectColor = OutlineColor;
            return label;
        }

        private static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
        }

        // Drives the pop-in, the silhouette reveal and the turning rays (OverlayScreen isn't a MonoBehaviour).
        private class Driver : MonoBehaviour
        {
            private const float PopSeconds = 0.45f;
            private const float HoldSeconds = 0.4f;     // the silhouette waits this long before it flashes to colour
            private const float FlashSeconds = 0.55f;

            [System.NonSerialized] public WorldClearedScreen Screen;
            public float ShownAt { get; private set; }
            private bool _silhouette, _revealed;

            public void Play(bool silhouette)
            {
                ShownAt = Time.unscaledTime;
                _silhouette = silhouette;
                _revealed = !silhouette;
                if (silhouette && Screen._card.sprite != null) { Screen._card.color = Silhouette; }
                if (!silhouette) { Burst(); }
            }

            private void Burst()
            {
                Screen._confetti.transform.SetAsLastSibling();
                Screen._confetti.Burst(110, 3f);
            }

            private void LateUpdate()
            {
                if (Screen == null) { return; }
                float age = Time.unscaledTime - ShownAt;

                Screen._rays.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -Time.unscaledTime * 25f);
                Screen._rays.canvasRenderer.SetAlpha(Mathf.Clamp01(age / PopSeconds));

                float pop = Mathf.Clamp01(age / PopSeconds);
                var heading = Screen._sign.gameObject.activeSelf ? Screen._sign.rectTransform : Screen._title.rectTransform;
                heading.localScale = Vector3.one * EaseOutBack(pop);

                float revealAt = _silhouette ? PopSeconds + HoldSeconds : 0f;
                if (!_revealed && age >= revealAt)
                {
                    _revealed = true;
                    if (Screen._card.sprite != null) { Screen._card.color = Color.white; }
                    Screen._flash.color = Color.white;
                    Burst();
                }

                float cardScale = Mathf.Lerp(0.5f, 1f, EaseOutBack(pop));
                if (_revealed)
                {
                    float f = Mathf.Clamp01((age - revealAt) / FlashSeconds);
                    if (_silhouette)
                    {
                        Screen._flash.color = new Color(1f, 1f, 1f, 1f - f);
                        cardScale = Mathf.Lerp(1.2f, 1f, EaseOutBack(f));
                    }
                }
                Screen._card.rectTransform.localScale = Vector3.one * cardScale;
            }
        }
    }
}
