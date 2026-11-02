using System;
using UnityEngine;
using UnityEngine.UI;

namespace FarmFury.UI
{
    /// <summary>
    /// The first screen when the game opens (GameState.MainMenu) — ported from FarmFury_Stampede's LandingScreen
    /// (franchise flow, 2026-11-02): the FarmFury poster (LandingPage.png: title + the eight animals) covering the
    /// screen (<see cref="ZoomedCover"/>, aspect kept, overflow cropped evenly top/bottom), a round play button
    /// bottom-left with Exit beside it (not on iOS), the settings cog bottom-right — all inside the safe area.
    /// Play -> World Select, Exit -> quit, cog -> the Settings / Shop menu hub. Without the poster: a plain title.
    /// </summary>
    public class LandingScreen
    {
        public GameObject Root { get; private set; }
        public Button PlayButton { get; private set; }
        public Button ExitButton { get; private set; }
        public Button SettingsButton { get; private set; }

        private const float EdgeMargin = 70f;
        // Play and Exit sit closer to the left edge of the safe area than the cog does to the right.
        private const float LeftMargin = 30f;
        // Gap between the button row and the bottom of the safe area.
        private const float BottomMargin = 40f;
        // Round buttons share the game-wide size; Exit (552x256 art, same height as the 256px round art) is drawn at
        // the same scale, so all three are the same height and sit level.
        private const float ButtonSize = UIKit.RoundButtonSize;
        private static readonly Vector2 ExitSize = new(ButtonSize * 552f / 256f, ButtonSize);
        private const float ButtonGap = 30f;
        private const float BackdropPivotY = 0.5f;   // FarmFury's poster has the title on top and the animals at the bottom: crop evenly

        public LandingScreen(Transform canvas, Action onPlay, Action onExit, Action onSettings, MenuArt art)
        {
            art ??= new MenuArt();
            var root = UIKit.NewRect("Landing", canvas);
            UIKit.Stretch(root);
            Root = root.gameObject;
            var bg = Root.AddComponent<Image>();
            bg.color = UIKit.Dark;
            var backdrop = UIKit.Backdrop(root);
            backdrop.rectTransform.pivot = new Vector2(0.5f, BackdropPivotY);
            UIKit.SetBackdrop(backdrop, null);

            if (art.landingPoster != null)
            {
                var poster = UIKit.Picture(root, "Poster", art.landingPoster);
                poster.preserveAspect = false;
                var fit = poster.gameObject.AddComponent<ZoomedCover>();
                fit.aspect = art.landingPoster.rect.width / art.landingPoster.rect.height;
                fit.zoom = 1f;   // nothing behind it: keep covering
                fit.pivotY = BackdropPivotY;
            }
            else
            {
                var title = UIKit.Label(root, "Title", "FARM FURY", 96, TextAnchor.MiddleCenter, UIKit.Accent);
                title.fontStyle = FontStyle.Bold;
                UIKit.Place(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 80f), new Vector2(1600f, 140f));
            }

            var safe = UIKit.NewRect("SafeArea", root);
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

            Root.SetActive(false);
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
