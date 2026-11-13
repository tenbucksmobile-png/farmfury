using System;
using UnityEngine;
using UnityEngine.UI;
using FarmFury.Shared.UI;

namespace FarmFury.UI
{
    /// <summary>
    /// The first screen when the game opens (GameState.MainMenu) — ported from FarmFury_Stampede's LandingScreen
    /// (franchise flow, 2026-11-02), split into two pages (2026-11-13, user: "change the landing page ... remove the
    /// buttons and exit and replace with PRESS START - navigate to second landing page with the buttons play, exit and
    /// settings"):
    ///   1. Title page (<see cref="MenuArt.landingTitle"/>, LandingTitle.png): the poster with a pulsing PRESS START;
    ///      a tap anywhere moves on. Shown when the game boots.
    ///   2. Menu page (<see cref="MenuArt.landingPoster"/>, LandingPage.png): the same scene without the characters, with
    ///      Play bottom-left, Exit beside it (not on iOS) and the settings cog bottom-right, inside the safe area.
    ///      Coming back to the landing from anywhere else in the game (World Select's back, Home) lands here.
    /// Both posters are 2400x1080 (20:9), drawn as cover: wider phones crop nothing that matters, 16:9 screens lose
    /// only the side margins. Play -> World Select, Exit -> quit, cog -> the Settings / Shop menu hub.
    /// </summary>
    public class LandingScreen
    {
        public GameObject Root { get; private set; }
        public GameObject TitlePage { get; private set; }
        public GameObject MenuPage { get; private set; }
        public Button PlayButton { get; private set; }
        public Button ExitButton { get; private set; }
        public Button SettingsButton { get; private set; }

        /// <summary>True while the PRESS START title page is showing.</summary>
        public bool OnTitlePage => TitlePage != null && TitlePage.activeSelf;

        private const float EdgeMargin = 70f;
        // Play and Exit sit closer to the left edge of the safe area than the cog does to the right.
        private const float LeftMargin = 30f;
        // Gap between the button row and the bottom of the safe area.
        private const float BottomMargin = 40f;
        // Round buttons share the game-wide size; Exit (552x256 art, same height as the 256px round art) is drawn at
        // the same scale, so all three are the same height and sit level.
        private static float ButtonSize => UIKit.RoundButtonSize;   // settable since core 0.2.0 (Cannonfire: 112)
        private static readonly Vector2 ExitSize = new(ButtonSize * 552f / 256f, ButtonSize);
        private const float ButtonGap = 30f;
        // PRESS START (323x60 art): centred low on the title page, over the grass between the hay and the tower.
        private const float PressStartWidth = 560f;
        private const float PressStartBottom = 70f;

        public LandingScreen(Transform canvas, Action onPlay, Action onExit, Action onSettings, MenuArt art)
        {
            art ??= new MenuArt();
            var root = UIKit.NewRect("Landing", canvas);
            UIKit.Stretch(root);
            Root = root.gameObject;
            var bg = Root.AddComponent<Image>();
            bg.color = UIKit.Dark;

            // ---- page 1: title + PRESS START
            var title = Page(root, "TitlePage");
            TitlePage = title.gameObject;
            Poster(title, art.landingTitle != null ? art.landingTitle : art.landingPoster);
            var titleSafe = UIKit.NewRect("SafeArea", title);
            titleSafe.gameObject.AddComponent<SafeAreaFitter>();
            if (art.pressStart != null)
            {
                var press = UIKit.Picture(titleSafe, "PressStart", art.pressStart);
                float aspect = art.pressStart.rect.width / art.pressStart.rect.height;
                UIKit.Place(press.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                    new Vector2(0f, PressStartBottom), new Vector2(PressStartWidth, PressStartWidth / aspect));
                press.gameObject.AddComponent<CanvasGroup>();
                press.gameObject.AddComponent<PulseScale>();
            }
            else
            {
                var press = UIKit.Label(titleSafe, "PressStart", "PRESS START", 72, TextAnchor.MiddleCenter, UIKit.Accent);
                press.fontStyle = FontStyle.Bold;
                UIKit.Place(press.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                    new Vector2(0f, PressStartBottom), new Vector2(900f, 110f));
                press.gameObject.AddComponent<CanvasGroup>();
                press.gameObject.AddComponent<PulseScale>();
            }
            // A full-screen invisible button over the title page: a tap anywhere moves on to the menu page.
            var tap = UIKit.MakeButton(title, "TapToStart", "", new Color(0f, 0f, 0f, 0f), ShowMenu);
            UIKit.Stretch(tap.image.rectTransform);
            tap.transition = Selectable.Transition.None;
            tap.navigation = new Navigation { mode = Navigation.Mode.None };

            // ---- page 2: Play / Exit / Settings
            var menu = Page(root, "MenuPage");
            MenuPage = menu.gameObject;
            Poster(menu, art.landingPoster);
            var safe = UIKit.NewRect("SafeArea", menu);
            safe.gameObject.AddComponent<SafeAreaFitter>();

            PlayButton = ArtButton(safe, "PlayButton", art.playButton, "PLAY", onPlay,
                new Vector2(0f, 0f), new Vector2(LeftMargin, BottomMargin), new Vector2(ButtonSize, ButtonSize));
            SettingsButton = ArtButton(safe, "SettingsButton", art.settingsButton, "SET", onSettings,
                new Vector2(1f, 0f), new Vector2(-EdgeMargin, BottomMargin), new Vector2(ButtonSize, ButtonSize));
            ExitButton = ArtButton(safe, "ExitButton", art.exitButton, "EXIT", onExit,
                new Vector2(0f, 0f), new Vector2(LeftMargin + ButtonSize + ButtonGap, BottomMargin), ExitSize);
            // No Exit on iOS: Apple's guidelines say an app never quits itself (Application.Quit there reads as a crash
            // to the player and to App Review); players leave with the home gesture.
            ExitButton.gameObject.SetActive(Application.platform != RuntimePlatform.IPhonePlayer);

            ShowTitle();
            Root.SetActive(false);
        }

        /// <summary>Page 1: the poster with PRESS START.</summary>
        public void ShowTitle()
        {
            MenuPage.SetActive(false);
            TitlePage.SetActive(true);
        }

        /// <summary>Page 2: Play / Exit / Settings.</summary>
        public void ShowMenu()
        {
            TitlePage.SetActive(false);
            MenuPage.SetActive(true);
        }

        // A full-screen page that fades in each time it is shown.
        private static RectTransform Page(RectTransform root, string name)
        {
            var page = UIKit.NewRect(name, root);
            UIKit.Stretch(page);
            ScreenFadeIn.AddTo(page.gameObject);
            return page;
        }

        // The page's poster, covering the screen (aspect kept, overflow cropped evenly); without art, a plain title.
        private static void Poster(RectTransform page, Sprite sprite)
        {
            if (sprite != null)
            {
                var poster = UIKit.Picture(page, "Poster", sprite);
                poster.preserveAspect = false;
                var fit = poster.gameObject.AddComponent<ZoomedCover>();
                fit.aspect = sprite.rect.width / sprite.rect.height;
                fit.zoom = 1f;
                fit.pivotY = 0.5f;
                return;
            }
            var title = UIKit.Label(page, "Title", "FARM FURY", 96, TextAnchor.MiddleCenter, UIKit.Accent);
            title.fontStyle = FontStyle.Bold;
            UIKit.Place(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 80f), new Vector2(1600f, 140f));
        }

        // A button showing its art (sized to the rect, aspect kept); without art, a plain labelled button.
        private static Button ArtButton(Transform parent, string name, Sprite sprite, string fallbackLabel, Action onClick,
            Vector2 corner, Vector2 offset, Vector2 size)
        {
            var button = UIKit.MakeButton(parent, name, sprite != null ? "" : fallbackLabel,
                sprite != null ? Color.white : new Color(0.85f, 0.55f, 0.15f, 1f), () => onClick?.Invoke(), 40);
            if (sprite != null)
            {
                button.image.sprite = sprite;
                button.image.preserveAspect = true;
            }
            UIKit.Place(button.image.rectTransform, corner, corner, offset, size);
            return button;
        }
    }
}
