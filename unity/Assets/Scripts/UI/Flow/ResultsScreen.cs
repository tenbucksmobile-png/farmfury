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
    /// Monetisation (Stampede's): the coin balance + this clear's payout top-right, the rewarded Double Coins button
    /// under it (only while an ad is loaded), a shop card bottom-centre on some clears (ShopPromos), and the Shop sign
    /// beside home on Level Failed.
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

        // Count-up reveal (2026-11-11, user: "have the score run up from zero - like a counter to slow down the flow"):
        // the score counts up from 0 easing out, the earned stars fill in one by one as it passes each third, the coin
        // balance ticks up by the payout, then the buttons fade in. A tap skips to the end. Unscaled time.
        private const float CountDelay = 0.45f, CountTime = 1.7f, CoinTime = 0.7f, ButtonsFade = 0.3f;
        private CanvasGroup _buttons;
        private float _revealT = -1f;          // seconds into the reveal; < 0 = not revealing
        private bool _countDone = true;
        private int _shownStars;
        private int _balanceStart, _paid;
        private string _bestLine = "";

        // The canvases are 1280x720 (Stampede's mockup); the painted stars are 76px boxes centred on these points.
        // The score goes under them; the World Unlocked sign sits in the open hills to the right.
        public static readonly Vector2 ArtSize = new(1280f, 720f);
        public const int ArtPad = 160;   // the *_Wide.png copies add this many mirrored columns each side
        private static readonly float[] StarCentresX = { 551.5f, 639.5f, 727.5f };
        private const float StarCentreY = 369.5f, StarBox = 76f;
        // Sized to sit under the three stars (2026-11-08: the score was drawing far larger than this box, see GoldText).
        private static readonly Rect ScoreTextBox = new(520f, 420f, 240f, 46f);
        private static readonly Rect BestTextBox = new(520f, 468f, 240f, 26f);
        private static readonly Rect WorldUnlockedSignBox = new(820f, 410f, 240f, 80f);
        private static readonly Color ScoreGold = new(1f, 0.84f, 0.25f, 1f);
        private static readonly Color ScoreOutline = new(0.24f, 0.11f, 0.03f, 1f);

        private const float EdgeMargin = 70f;
        private const float BottomMargin = 40f;
        private const float ButtonGap = 30f;

        private readonly ShopArt _shop;
        private readonly GameObject _payout;
        private readonly Text _payoutText;
        private readonly RectTransform _payoutCoin;
        private readonly Text _earnedText;
        private static readonly Color EarnedGreen = new(0.55f, 1f, 0.45f, 1f);
        private const float PayoutHeight = 80f, PayoutCoinSize = 72f, PayoutGap = 12f;
        private readonly GameObject _doubleCoins;
        private const float DoubleCoinsSize = 160f;
        private const float PromoCardHeight = 200f;
        private readonly Button _promoCard;
        private readonly Button _shopSign;
        private ShopPromo _promo;
        private LevelRunState _run;

        public ResultsScreen(Transform canvas, Action onNext, Action onRetry, Action onQuit, Action onHome, Action onSettings,
            Action<ShopPromo> onPromo, Action onShop, MenuArt art, ShopArt shop)
        {
            _art = art ?? new MenuArt();
            _shop = shop ?? new ShopArt();
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
            _buttons = safe.gameObject.AddComponent<CanvasGroup>();
            float size = UIKit.RoundButtonSize;
            // Same inset as Home/Settings on the right (was EdgeMargin + 60, user request 2026-11-08).
            float left = EdgeMargin, second = left + size + ButtonGap;
            NextButton = RoundButton(safe, "NextButton", _art.playButton, ">", onNext, Vector2.zero, new Vector2(left, BottomMargin));
            RetryButton = RoundButton(safe, "RetryButton", _art.playButton, ">", onRetry, Vector2.zero, new Vector2(left, BottomMargin));
            QuitButton = RoundButton(safe, "QuitButton", _art.quitButton, "X", onQuit, Vector2.zero, new Vector2(second, BottomMargin));
            SettingsButton = RoundButton(safe, "SettingsButton", _art.settingsButton, "SET", onSettings, Vector2.right, new Vector2(-EdgeMargin, BottomMargin));
            HomeButton = RoundButton(safe, "HomeButton", _art.homeButton, "HOME", onHome, Vector2.right, new Vector2(-EdgeMargin, BottomMargin));

            _promoCard = UIKit.MakeButton(safe, "ShopPromoCard", "", Color.white, () => onPromo?.Invoke(_promo));
            _promoCard.image.preserveAspect = true;
            UIKit.Place(_promoCard.image.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, BottomMargin),
                new Vector2(PromoCardHeight * 619f / 246f, PromoCardHeight));
            _promoCard.gameObject.SetActive(false);

            // Level Failed: in the corner beside home, where Level Complete has its cog; the sign is 666x375.
            _shopSign = UIKit.MakeButton(safe, "ShopSign", _shop.shopSign != null ? "" : "SHOP", _shop.shopSign != null ? Color.white : UIKit.Accent, () => onShop?.Invoke());
            if (_shop.shopSign != null)
            {
                _shopSign.image.sprite = _shop.shopSign;
                _shopSign.image.preserveAspect = true;
            }
            UIKit.Place(_shopSign.image.rectTransform, Vector2.right, Vector2.right,
                new Vector2(-EdgeMargin - UIKit.RoundButtonSize - ButtonGap, BottomMargin), new Vector2(UIKit.RoundButtonSize * 666f / 375f, UIKit.RoundButtonSize));
            _shopSign.gameObject.SetActive(false);

            // Top-right: the coin balance (as on the HUD), the coin, this clear's payout ("+20") in green before it.
            var top = UIKit.NewRect("TopSafeArea", root);
            top.gameObject.AddComponent<SafeAreaFitter>();
            _payout = UIKit.NewRect("CoinPayout", top).gameObject;
            var payoutRect = (RectTransform)_payout.transform;
            UIKit.Place(payoutRect, Vector2.one, Vector2.one, new Vector2(-EdgeMargin, -40f), new Vector2(600f, PayoutHeight));
            var coin = _shop.coinIcon != null ? UIKit.Picture(payoutRect, "CoinIcon", _shop.coinIcon) : UIKit.Panel(payoutRect, "CoinIcon", UIKit.Accent);
            UIKit.Place(coin.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(PayoutCoinSize, PayoutCoinSize));
            _payoutCoin = coin.rectTransform;
            _payoutText = UIKit.Label(payoutRect, "Count", "", 52, TextAnchor.MiddleRight, ScoreGold);
            _payoutText.fontStyle = FontStyle.Bold;
            _payoutText.horizontalOverflow = HorizontalWrapMode.Overflow;
            var payoutOutline = _payoutText.gameObject.AddComponent<Outline>();
            payoutOutline.effectColor = ScoreOutline;
            payoutOutline.effectDistance = new Vector2(3f, -3f);
            UIKit.Place(_payoutText.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(500f, 72f));
            _earnedText = UIKit.Label(payoutRect, "Earned", "", 40, TextAnchor.MiddleRight, EarnedGreen);
            _earnedText.fontStyle = FontStyle.Bold;
            _earnedText.horizontalOverflow = HorizontalWrapMode.Overflow;
            var earnedOutline = _earnedText.gameObject.AddComponent<Outline>();
            earnedOutline.effectColor = ScoreOutline;
            earnedOutline.effectDistance = new Vector2(3f, -3f);
            UIKit.Place(_earnedText.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(400f, 72f));

            float watchAdHeight = DoubleCoinsSize * 214f / 512f;
            _doubleCoins = UIKit.NewRect("DoubleCoinsAdGroup", top).gameObject;
            UIKit.Place((RectTransform)_doubleCoins.transform, Vector2.one, Vector2.one, new Vector2(-EdgeMargin, -40f - PayoutHeight - 10f),
                new Vector2(DoubleCoinsSize, DoubleCoinsSize + 6f + watchAdHeight));
            var doubleButton = UIKit.MakeButton(_doubleCoins.transform, "DoubleCoinsButton", _shop.doubleCoins != null ? "" : "x2",
                _shop.doubleCoins != null ? Color.white : UIKit.Accent, ClaimDoubleCoins, 48);
            if (_shop.doubleCoins != null)
            {
                doubleButton.image.sprite = _shop.doubleCoins;
                doubleButton.image.preserveAspect = true;
            }
            UIKit.Place(doubleButton.image.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, Vector2.one * DoubleCoinsSize);
            Graphic watchAd = _shop.watchAd != null
                ? UIKit.Picture(_doubleCoins.transform, "WatchAdLabel", _shop.watchAd)
                : UIKit.Label(_doubleCoins.transform, "WatchAdLabel", "Watch Ad", 30);
            UIKit.Place(watchAd.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -(DoubleCoinsSize + 6f)),
                new Vector2(DoubleCoinsSize, watchAdHeight));
            _doubleCoins.SetActive(false);

            Root.SetActive(false);
        }

        /// <summary>Called every frame by GameFlow while shown: Double Coins only while a rewarded ad is loaded.</summary>
        public void Tick()
        {
            var gm = GameManager.Instance;
            if (_revealT >= 0f) TickReveal();
            bool show = _countDone && gm != null && gm.State == GameState.LevelComplete && _run != null && !_run.doubleCoinsClaimed &&
                _run.coinsEarned > 0 && AdManager.Instance != null && AdManager.Instance.IsRewardedAdReady;
            if (_doubleCoins.activeSelf != show) { _doubleCoins.SetActive(show); }
        }

        private void ClaimDoubleCoins()
        {
            AdManager.Instance?.ShowRewardedAd("double_coins_level_complete", rewarded =>
            {
                if (rewarded && GameManager.Instance != null && GameManager.Instance.ClaimDoubleCoinsViaAd() && _run != null)
                {
                    ShowPayout(_run);
                }
                Tick();
            });
        }

        // The payout is already in the balance (GameManager.CompleteLevel / ClaimDoubleCoinsViaAd add it).
        private void ShowPayout(LevelRunState run)
        {
            int paid = run.coinsEarned * (run.doubleCoinsClaimed ? 2 : 1);
            int balance = SaveManager.Instance != null ? SaveManager.Instance.CoinBalance : paid;
            _payoutText.text = balance.ToString();
            _earnedText.text = paid > 0 ? (run.doubleCoinsClaimed ? $"+{paid} (x2)" : $"+{paid}") : "";
            float coinX = -(_payoutText.preferredWidth + PayoutGap);
            _payoutCoin.anchoredPosition = new Vector2(coinX, 0f);
            _earnedText.rectTransform.anchoredPosition = new Vector2(coinX - PayoutCoinSize - PayoutGap * 1.5f, 0f);
            _payout.SetActive(true);
        }

        // The existing category banners are the cards; a card without art isn't shown.
        private void ShowPromoCard(ShopPromo promo)
        {
            Sprite art = promo switch
            {
                ShopPromo.Cannons => _shop.cannonsBanner,
                ShopPromo.Trails => _shop.trailsBanner,
                ShopPromo.Impacts => _shop.impactsBanner,
                ShopPromo.RemoveAds => _shop.promoNoAds,
                _ => null,
            };
            _promo = promo;
            _promoCard.image.sprite = art;
            _promoCard.gameObject.SetActive(art != null);
        }

        public void ShowComplete(LevelRunState run, WorldData world)
        {
            _run = run;
            ShowPayout(run);
            ShowPromoCard(ShopPromos.PickForLevelComplete(GameManager.Instance != null ? GameManager.Instance.CurrentLevel : null, run));
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
            _bestLine = run.isPerfect ? "PERFECT!" : run.isNewBest ? "NEW BEST!" : "";
            LayoutButtons(complete: true);
            StartReveal(run);
            Root.SetActive(true);
        }

        private void StartReveal(LevelRunState run)
        {
            _paid = run.coinsEarned * (run.doubleCoinsClaimed ? 2 : 1);
            int balance = SaveManager.Instance != null ? SaveManager.Instance.CoinBalance : _paid;
            _balanceStart = Mathf.Max(0, balance - _paid);
            _scoreText.text = "0";
            _bestText.text = "";
            _shownStars = 0;
            SetShownStars(0);
            _payoutText.text = _balanceStart.ToString();
            _buttons.alpha = 0f;
            _buttons.interactable = _buttons.blocksRaycasts = false;
            _countDone = false;
            _revealT = 0f;
        }

        private void TickReveal()
        {
            _revealT += Time.unscaledDeltaTime;
            var pointer = UnityEngine.InputSystem.Pointer.current;
            float end = CountDelay + CountTime + CoinTime;
            if (!_countDone && _revealT > 0.25f && pointer != null && pointer.press.wasPressedThisFrame) _revealT = end;   // tap: skip

            float p = Mathf.Clamp01((_revealT - CountDelay) / CountTime);
            float eased = 1f - Mathf.Pow(1f - p, 3f);
            int target = _run != null ? _run.score : 0;
            _scoreText.text = Mathf.RoundToInt(target * eased).ToString("N0");
            int stars = _run != null ? _run.starsEarned : 0;
            int due = 0;
            for (int i = 0; i < stars; i++) if (p >= (i + 1) / 3f - 0.001f || p >= 1f) due = i + 1;
            if (due != _shownStars) { _shownStars = due; SetShownStars(due); AudioManager.Play(AudioManager.Sound.RobotHit); }

            float q = Mathf.Clamp01((_revealT - CountDelay - CountTime) / CoinTime);
            _payoutText.text = (_balanceStart + Mathf.RoundToInt(_paid * q)).ToString();

            if (!_countDone && _revealT >= end)
            {
                _countDone = true;
                _bestText.text = _bestLine;
                _buttons.interactable = _buttons.blocksRaycasts = true;
            }
            if (_countDone)
            {
                _buttons.alpha = Mathf.Clamp01((_revealT - end) / ButtonsFade);
                if (_buttons.alpha >= 1f) _revealT = -1f;
            }
        }

        // Stars filled so far: the empty-star overlay hides over each earned painted star in turn (art look), or the
        // plain bar fills (plain look).
        private void SetShownStars(int n)
        {
            if (_artLook)
            {
                for (int i = 0; i < _emptyStars.Length; i++)
                    _emptyStars[i].gameObject.SetActive(_art.levelCompleteStarEmpty != null && i >= n);
            }
            else if (_plainStars != null) UIKit.SetStars(_plainStars, n);
        }

        public void ShowFailed(WorldData world)
        {
            _run = null;
            _payout.SetActive(false);
            _doubleCoins.SetActive(false);
            ShowPromoCard(ShopPromo.None);
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
            _revealT = -1f;
            _countDone = true;
            _buttons.alpha = 1f;
            _buttons.interactable = _buttons.blocksRaycasts = true;
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
            _shopSign.gameObject.SetActive(!complete);
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
            // Best fit only shrinks text to fit edges that clip: with Overflow it sized to the box height alone and a
            // long score ("2,300") ran far outside the box, over the stars. Wrap + Truncate makes it fit both ways.
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
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
