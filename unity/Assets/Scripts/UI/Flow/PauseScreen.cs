using System;
using UnityEngine;
using UnityEngine.UI;
using FarmFury.Shared.UI;

namespace FarmFury.UI
{
    /// <summary>
    /// Pause. Art look (per the Pause mockup, when MenuArt.pauseBackground is set): the full-screen Pause canvas
    /// ("Pause" and the logo painted in), fitted like the results screens (the whole 1280x720 art always on screen,
    /// the widened copy's extra sides filling wider phones), with round buttons in the safe area at the results
    /// screens' positions: play (resume) + X (quit to Level Select) bottom-left, home (landing) + cog (Settings,
    /// opened on top) bottom-right. Without the art: the old panel menu - Play, Settings, Restart Level, Quit.
    /// </summary>
    public class PauseScreen
    {
        public GameObject Root { get; private set; }
        public Button PlayButton { get; private set; }
        public Button SettingsButton { get; private set; }
        public Button RestartButton { get; private set; }
        public Button QuitButton { get; private set; }
        public Button HomeButton { get; private set; }

        private const float EdgeMargin = 70f;
        private const float BottomMargin = 40f;
        private const float ButtonGap = 30f;

        public PauseScreen(Transform canvas, Action onPlay, Action onSettings, Action onRestart, Action onQuit, Action onHome, MenuArt art)
        {
            art ??= new MenuArt();
            var root = UIKit.NewRect("Pause", canvas);
            UIKit.Stretch(root);
            Root = root.gameObject;

            if (art.pauseBackground != null)
            {
                BuildArtLook(root, art, onPlay, onSettings, onQuit, onHome);
            }
            else
            {
                BuildPanelLook(root, onPlay, onSettings, onRestart, onQuit);
            }

            Root.SetActive(false);
        }

        private void BuildArtLook(RectTransform root, MenuArt art, Action onPlay, Action onSettings, Action onQuit, Action onHome)
        {
            Root.AddComponent<Image>().color = UIKit.Dark;
            var backdrop = UIKit.Picture(root, "Backdrop", art.pauseBackground);
            backdrop.gameObject.SetActive(true);
            var fit = backdrop.gameObject.AddComponent<FitContent>();
            fit.contentSize = new Vector2(1280f, 720f);
            fit.spriteSize = new Vector2(1280f + 2f * ResultsScreen.ArtPad, 720f);

            var safe = UIKit.NewRect("SafeArea", root);
            safe.gameObject.AddComponent<SafeAreaFitter>();
            float size = UIKit.RoundButtonSize;
            float left = EdgeMargin + 60f, second = left + size + ButtonGap;
            PlayButton = RoundButton(safe, "PlayButton", art.playButton, ">", onPlay, Vector2.zero, new Vector2(left, BottomMargin));
            QuitButton = RoundButton(safe, "QuitButton", art.quitButton, "X", onQuit, Vector2.zero, new Vector2(second, BottomMargin));
            SettingsButton = RoundButton(safe, "SettingsButton", art.settingsButton, "SET", onSettings, Vector2.right, new Vector2(-EdgeMargin, BottomMargin));
            HomeButton = RoundButton(safe, "HomeButton", art.homeButton, "HOME", onHome, Vector2.right,
                new Vector2(-EdgeMargin - size - ButtonGap, BottomMargin));
        }

        private void BuildPanelLook(RectTransform root, Action onPlay, Action onSettings, Action onRestart, Action onQuit)
        {
            Root.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f);

            var menu = UIKit.Panel(root, "Menu", UIKit.Dark);
            UIKit.Place(menu.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(620f, 640f));

            var title = UIKit.Label(menu.transform, "Title", "PAUSED", 60, TextAnchor.MiddleCenter, UIKit.Accent);
            UIKit.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(560f, 80f));

            PlayButton = MenuButton(menu.transform, "PlayButton", "Play", 0, UIKit.Good, () => onPlay?.Invoke());
            SettingsButton = MenuButton(menu.transform, "SettingsButton", "Settings", 1, UIKit.Card, () => onSettings?.Invoke());
            RestartButton = MenuButton(menu.transform, "RestartButton", "Restart Level", 2, UIKit.Card, () => onRestart?.Invoke());
            QuitButton = MenuButton(menu.transform, "QuitButton", "Quit", 3, new Color(0.6f, 0.25f, 0.25f, 1f), () => onQuit?.Invoke());
        }

        private static Button MenuButton(Transform parent, string name, string label, int index, Color color, UnityEngine.Events.UnityAction onClick)
        {
            var button = UIKit.MakeButton(parent, name, label, color, onClick, 40);
            UIKit.Place(button.image.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -150f - index * 110f), new Vector2(440f, 84f));
            return button;
        }

        // A round art button of the shared size in a corner of the safe area; without art, a plain labelled square.
        private static Button RoundButton(Transform parent, string name, Sprite sprite, string fallbackLabel, Action onClick,
            Vector2 corner, Vector2 offset)
        {
            var button = UIKit.MakeButton(parent, name, sprite != null ? "" : fallbackLabel,
                sprite != null ? Color.white : new Color(0.85f, 0.55f, 0.15f, 1f), () => onClick?.Invoke(), 34);
            if (sprite != null)
            {
                button.image.sprite = sprite;
                button.image.preserveAspect = true;
            }
            UIKit.Place(button.image.rectTransform, corner, corner, offset, Vector2.one * UIKit.RoundButtonSize);
            return button;
        }

        public void Show()
        {
            Root.SetActive(true);
        }

        public void Hide()
        {
            Root.SetActive(false);
        }
    }
}
