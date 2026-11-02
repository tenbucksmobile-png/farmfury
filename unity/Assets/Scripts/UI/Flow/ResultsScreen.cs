using System;
using FarmFury.Core;
using UnityEngine;
using UnityEngine.UI;

namespace FarmFury.UI
{
    /// <summary>
    /// Level Complete / Level Failed — FarmFury's port of FarmFury_Stampede's ResultsScreen (franchise flow,
    /// 2026-11-02), replacing HUDController's Scoreboard panels. Shown by GameFlow once LevelCompleteManager /
    /// LevelFailedManager finish their celebration / taunt video, so the video stays the first beat.
    ///
    /// Art look: the full-screen results canvas (title, logo and three gold stars painted in; the level's world's
    /// own canvas when WorldData has one) fitted so its 1280x720 middle is always on screen (FitContent), a greyed
    /// star drawn over each painted star not earned, the points score under the stars, and the World Unlocked sign
    /// when a boss clear opened the next world. Buttons (round, safe area): complete = play (next) + X (Level
    /// Select) bottom-left, home + cog bottom-right; failed = play (retry) + X bottom-left, home bottom-right.
    /// Plain look (no art wired): a dark panel with square stars, the score and text buttons.
    /// Phase 5 adds Stampede's coin payout, Double Coins and shop card to this screen.
    /// </summary>
    public class ResultsScreen
    {
        public GameObject Root { get; private set; }
        public Button NextButton { get; private set; }
        public Button RetryButton { get; private set; }
        public Button QuitButton { get; private set; }
        public Button HomeButton { get; private set; }
        public Button SettingsButton { get; private set; }

        private readonly MenuArt _art;
        private readonly bool _artLook;

        private readonly Image _backdrop;
        private readonly Image[] _emptyStars = new Image[3];
        private readonly Image _worldUnlockedSign;
        private readonly Text _scoreText;
        private readonly Text _bestText;

        private readonly Text _plainTitle;
        private readonly Image[] _plainStars;

        // The canvases are 1280x720 (Stampede's mockup); the painted stars are 76px boxes centred on these points.
        // The score goes under them; the World Unlocked sign sits in the open hills to the right.
        public static readonly Vector2 ArtSize = new(1280f, 720f);
        public const int ArtPad = 160;   // the *_Wide.png copies add this many mirrored columns each side
        private static readonly float[] StarCentresX = { 551.5f, 639.5f, 727.5f };
        private const float StarCentreY = 369.5f, StarBox = 76f;
        private static readonly Rect ScoreTextBox = new(470f, 432f, 340f, 68f);
        private static readonly Rect BestTextBox = new(470f, 500f, 340f, 40f);
        private static readonly Rect WorldUnlockedSignBox = new(820f, 410f, 240f, 80f);
        private static readonly Color ScoreGold = new(1f, 0.84f, 0.25f, 1f);
        private static readonly Color ScoreOutline = new(0.24f, 0.11f, 0.03f, 1f);

        private const float EdgeMargin = 70f;
        private const float BottomMargin = 40f;
        private const float ButtonGap = 30f;

        public ResultsScreen(Transform canvas, Action onNext, Action onRetry, Action onQuit, Action onHome, Action onSettings, MenuArt art)
        {
            _art = art ?? new MenuArt();
            _artLook = _art.levelCompleteBackground != null && _art.levelFailedBackground != null;

            var root = UIKit.NewRect("Results", canvas);
            UIKit.Stretch(root);
            Root = root.gameObject;
            Root.AddComponent<Image>().color = _artLook ? UIKit.Dark : new Color(0f, 0f, 0f, 0.6f);

            if (_artLook)
            {
                _backdrop = UIKit.Picture(root, "Backdrop", null);
                _backdrop.gameObject.SetActive(true);
                var fit = _backdrop.gameObject.AddComponent<FitContent>();
                fit.contentSize = ArtSize;
                fit.spriteSize = new Vector2(ArtSize.x + 2f * ArtPad, ArtSize.y);

                for (int i = 0; i < _emptyStars.Length; i++)
                {
                    _emptyStars[i] = UIKit.Picture(_backdrop.transform, $"EmptyStar{i + 1}", _art.levelCompleteStarEmpty);
                    PlaceInArt(_emptyStars[i].rectTransform,
                        new Rect(StarCentresX[i] - StarBox * 0.5f, StarCentreY - StarBox * 0.5f, StarBox, StarBox));
                }
                _worldUnlockedSign = UIKit.Picture(_backdrop.transform, "WorldUnlockedSign", _art.worldUnlockedSign);
                PlaceInArt(_worldUnlockedSign.rectTransform, WorldUnlockedSignBox);

                _scoreText = GoldText(_backdrop.transform, "Score", 300);
                PlaceInArt(_scoreText.rectTransform, ScoreTextBox);
                _bestText = GoldText(_backdrop.transform, "NewBest", 120);
                PlaceInArt(_bestText.rectTransform, BestTextBox);
            }
            else
            {
                var panel = UIKit.Panel(root, "Panel", UIKit.Dark);
                UIKit.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1000f, 600f));
                _plainTitle = UIKit.Label(panel.transform, "Title", "", 64, TextAnchor.MiddleCenter, UIKit.Accent);
                UIKit.Place(_plainTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(840f, 90f));
                _plainStars = UIKit.StarBar(panel.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-115f, -150f), 64f);
                _scoreText = GoldText(panel.transform, "Score", 80);
                UIKit.Place(_scoreText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -260f), new Vector2(700f, 90f));
                _bestText = GoldText(panel.transform, "NewBest", 40);
                UIKit.Place(_bestText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -350f), new Vector2(700f, 50f));
            }

            var safe = UIKit.NewRect("SafeArea", root);
            safe.gameObject.AddComponent<SafeAreaFitter>();
            float size = UIKit.RoundButtonSize;
            float left = EdgeMargin + 60f, second = left + size + ButtonGap;
            NextButton = RoundButton(safe, "NextButton", _art.playButton, ">", onNext, Vector2.zero, new Vector2(left, BottomMargin));
            RetryButton = RoundButton(safe, "RetryButton", _art.playButton, ">", onRetry, Vector2.zero, new Vector2(left, BottomMargin));
            QuitButton = RoundButton(safe, "QuitButton", _art.quitButton, "X", onQuit, Vector2.zero, new Vector2(second, BottomMargin));
            SettingsButton = RoundButton(safe, "SettingsButton", _art.settingsButton, "SET", onSettings, Vector2.right, new Vector2(-EdgeMargin, BottomMargin));
            HomeButton = RoundButton(safe, "HomeButton", _art.homeButton, "HOME", onHome, Vector2.right, new Vector2(-EdgeMargin, BottomMargin));

            Root.SetActive(false);
        }

        public void ShowComplete(LevelRunState run, WorldData world)
        {
            if (_artLook)
            {
                SetArt(MenuArt.Either(world != null ? world.levelCompleteBackground : null, _art.levelCompleteBackground));
                for (int i = 0; i < _emptyStars.Length; i++)
                {
                    _emptyStars[i].gameObject.SetActive(_art.levelCompleteStarEmpty != null && i >= run.starsEarned);
                }
                _worldUnlockedSign.gameObject.SetActive(run.worldUnlocked.HasValue && _art.worldUnlockedSign != null);
            }
            else
            {
                _plainTitle.text = run.bossCleared ? "BOSS DEFEATED!" : "LEVEL COMPLETE!";
                foreach (var s in _plainStars) { s.gameObject.SetActive(true); }
                UIKit.SetStars(_plainStars, run.starsEarned);
            }
            _scoreText.gameObject.SetActive(true);
            _scoreText.text = run.score.ToString("N0");
            _bestText.text = run.isPerfect ? "PERFECT!" : run.isNewBest ? "NEW BEST!" : "";
            LayoutButtons(complete: true);
            Root.SetActive(true);
        }

        public void ShowFailed(WorldData world)
        {
            if (_artLook)
            {
                SetArt(MenuArt.Either(world != null ? world.levelFailedBackground : null, _art.levelFailedBackground));
                foreach (var star in _emptyStars) { star.gameObject.SetActive(false); }
                _worldUnlockedSign.gameObject.SetActive(false);
            }
            else
            {
                _plainTitle.text = "LEVEL FAILED";
                foreach (var s in _plainStars) { s.gameObject.SetActive(false); }
            }
            _scoreText.gameObject.SetActive(false);
            _bestText.text = "";
            LayoutButtons(complete: false);
            Root.SetActive(true);
        }

        public void Hide() => Root.SetActive(false);

        private void SetArt(Sprite sprite)
        {
            _backdrop.sprite = sprite;
            _backdrop.preserveAspect = true;
        }

        // Complete: play + X bottom-left, home + cog bottom-right. Failed: retry + X bottom-left, home alone bottom-right.
        private void LayoutButtons(bool complete)
        {
            NextButton.gameObject.SetActive(complete);
            RetryButton.gameObject.SetActive(!complete);
            SettingsButton.gameObject.SetActive(complete);
            float homeX = complete ? -EdgeMargin - UIKit.RoundButtonSize - ButtonGap : -EdgeMargin;
            HomeButton.image.rectTransform.anchoredPosition = new Vector2(homeX, BottomMargin);
        }

        private static Text GoldText(Transform parent, string name, int maxSize)
        {
            var text = UIKit.Label(parent, name, "", maxSize, TextAnchor.MiddleCenter, ScoreGold);
            text.fontStyle = FontStyle.Bold;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 8;
            text.resizeTextMaxSize = maxSize;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            var outline = text.gameObject.AddComponent<Outline>();
            outline.effectColor = ScoreOutline;
            outline.effectDistance = new Vector2(3f, -3f);
            return text;
        }

        /// <summary>
        /// Places a child of the backdrop over a box given in the original 1280x720 art's pixels (origin top-left),
        /// by anchors on the widened sprite, so it scales and moves with the backdrop. Shared by the overlays.
        /// </summary>
        public static void PlaceInArt(RectTransform rt, Rect box)
        {
            float wide = ArtSize.x + 2f * ArtPad;
            rt.anchorMin = new Vector2((box.xMin + ArtPad) / wide, 1f - box.yMax / ArtSize.y);
            rt.anchorMax = new Vector2((box.xMax + ArtPad) / wide, 1f - box.yMin / ArtSize.y);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

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
    }
}
