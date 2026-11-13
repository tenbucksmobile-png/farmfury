using FarmFury.Core;
using UnityEngine;
using UnityEngine.UI;
using FarmFury.Shared.Monetisation;
using FarmFury.Shared.UI;

namespace FarmFury.UI
{
    /// <summary>
    /// Arcade's revive prompt, shown when the last life is lost (GameManager.ReviveOffered): the hanging sign with
    /// "Revive for 5 coins" painted in drops in from the top of the screen and sways to a stop mid-screen
    /// (<see cref="HangingSignDrop"/>). On its planks, three stacked buttons: Yes (spend the coins for one life, once
    /// per attempt; greyed when used or they can't be afforded), Watch Ad (a rewarded ad for three lives, any number of
    /// times; always shown, dimmed while no ad is loaded) and No (Level Failed). A Remove Ads owner sees a Free
    /// button instead of Watch Ad (three lives, once per attempt). The revive lands at the last checkpoint; what each
    /// gives is written beside its button. The player's coins sit in the top-right corner of the safe area: a slowly
    /// spinning coin with the count beside it. Closing it any other way (Esc) counts as No.
    /// </summary>
    public class RevivePromptScreen : OverlayScreen
    {
        // This game's ShopArt (the shared base holds it as ShopArtBase).
        private new ShopArt Shop => (ShopArt)base.Shop;

        private static readonly Vector2 PanelSize = new(1300f, 731f);    // the 666x375 sign, enlarged
        private const float ButtonHeight = 84f;
        private const float ButtonGap = 16f;
        private const float PlankCentreY = 15f;
        private const float CoinSize = 90f;
        private const float CornerMargin = 40f;
        // The button column sits left of centre so the button + "to add a Farm Fury" read as one centred group on the
        // sign (2026-11-13, user: "change text to 'to add a Farm Fury' - perhaps we could shift the buttons left").
        private const float ButtonsX = -170f;
        private const string RewardText = "to add a Farm Fury";
        private static readonly Color CountGold = new(1f, 0.84f, 0.25f, 1f);
        private static readonly Color CountOutline = new(0.24f, 0.11f, 0.03f, 1f);
        private static readonly Color AdNotReadyTint = new(1f, 1f, 1f, 0.45f);

        private readonly RectTransform _sign;
        private readonly HangingSignDrop _drop;
        private readonly Button _yes, _watchAd, _free;
        private readonly Text _coins;
        private readonly RectTransform _coin;
        private readonly ConfettiBurst _confetti;
        private bool _deciding;

        public RevivePromptScreen(Transform canvas, MenuArt art, ShopArt shop) : base(canvas, "RevivePrompt", art, shop, posterBackdrop: false)
        {
            BackButton.gameObject.SetActive(false);

            // Everything that hangs (sign, buttons, coin) moves together; the top pivot makes the sway swing from the ropes.
            _sign = UIKit.NewRect("HangingSign", Rect);
            UIKit.Place(_sign, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 1f), new Vector2(0f, PanelSize.y * 0.5f), PanelSize);
            _drop = _sign.gameObject.AddComponent<HangingSignDrop>();

            var panel = UIKit.Picture(_sign, "PanelArt", Shop.revivePanel);
            UIKit.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, PanelSize);
            if (Shop.revivePanel == null)
            {
                var question = UIKit.Label(_sign, "Question", $"Revive for {GameManager.ReviveCoinsCost} coins?", 60, TextAnchor.MiddleCenter, UIKit.Accent);
                UIKit.Place(question.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 230f), new Vector2(1200f, 90f));
            }

            float width = ButtonHeight * 512f / 214f;   // Yes / No / WatchAd art are 512x214
            _yes = StackedButton("ReviveButton", Shop.yesButton, "YES", 0, width, Accept);
            _watchAd = StackedButton("WatchAdButton", Shop.watchAd, "WATCH AD", 1, width, WatchAd);
            StackedButton("DeclineButton", Shop.noButton, "NO", 2, width, Hide);
            var colors = _watchAd.colors;
            colors.disabledColor = AdNotReadyTint;
            _watchAd.colors = colors;

            // Remove Ads owners: the same slot, on the wooden plaque (the Watch Ad art has its label baked in).
            _free = PlaqueButton(_sign, "FreeReviveButton", "FREE", 40, AcceptFree);
            UIKit.Place(_free.image.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(ButtonsX, RowY(1)), new Vector2(width, ButtonHeight));
            _free.colors = colors;

            RewardLabel("YesReward", 0, width);
            RewardLabel("AdReward", 1, width);

            // The player's coins, top-right of the safe area: the count right-aligned, the spinning coin just left of it.
            var coin = Shop.coinIcon != null ? UIKit.Picture(Safe, "Coin", Shop.coinIcon) : UIKit.Panel(Safe, "Coin", UIKit.Accent);
            coin.raycastTarget = false;
            UIKit.Place(coin.rectTransform, Vector2.one, new Vector2(1f, 1f), new Vector2(-CornerMargin, -CornerMargin), Vector2.one * CoinSize);
            coin.gameObject.AddComponent<SpinAroundY>();
            _coin = coin.rectTransform;
            _coins = UIKit.Label(Safe, "CoinCount", "", 60, TextAnchor.MiddleRight, CountGold);
            _coins.fontStyle = FontStyle.Bold;
            _coins.horizontalOverflow = HorizontalWrapMode.Overflow;
            var outline = _coins.gameObject.AddComponent<Outline>();
            outline.effectColor = CountOutline;
            outline.effectDistance = new Vector2(3f, -3f);
            UIKit.Place(_coins.rectTransform, Vector2.one, Vector2.one, new Vector2(-CornerMargin, -CornerMargin), new Vector2(300f, CoinSize));

            // Confetti over the cannon once the extra animal is loaded. Its own child of the canvas, not of the prompt,
            // so it keeps going after the prompt closes.
            var particles = UIKit.NewRect("ReviveConfetti", canvas);
            particles.anchorMin = particles.anchorMax = particles.pivot = new Vector2(0.5f, 0.5f);
            particles.sizeDelta = Vector2.zero;
            _confetti = particles.gameObject.AddComponent<ConfettiBurst>();
            _confetti.particlesRoot = particles;
        }

        /// <summary>
        /// Keeps the revive confetti over the character: waits for the respawn (the defeat pose, then the move to the
        /// checkpoint) to finish, pops the confetti there, and tracks the character's screen position while it falls.
        /// </summary>
        private Button StackedButton(string name, Sprite sprite, string fallback, int row, float width, System.Action onClick)
        {
            var button = IconButton(_sign, name, sprite, fallback, onClick);
            UIKit.Place(button.image.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(ButtonsX, RowY(row)), new Vector2(width, ButtonHeight));
            return button;
        }

        private static float RowY(int row)
        {
            return PlankCentreY + (1 - row) * (ButtonHeight + ButtonGap);
        }

        // "to add a Farm Fury" just right of a button, in the coin count's gold lettering (was "+1 animal", then "Adding
        // FarmFury"; 2026-11-13 the wording and the buttons moved left to fit).
        private void RewardLabel(string name, int row, float buttonWidth)
        {
            var label = UIKit.Label(_sign, name, RewardText, 36, TextAnchor.MiddleLeft, CountGold);
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.fontStyle = FontStyle.Bold;
            var outline = label.gameObject.AddComponent<Outline>();
            outline.effectColor = CountOutline;
            outline.effectDistance = new Vector2(2f, -2f);
            UIKit.Place(label.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f), new Vector2(ButtonsX + buttonWidth * 0.5f + 16f, RowY(row)), new Vector2(380f, ButtonHeight));
        }

        protected override void OnShow()
        {
            _deciding = true;
            var save = SaveManager.Instance;
            var run = GameManager.Instance != null ? GameManager.Instance.RunState : null;
            _yes.interactable = save != null && run != null && !run.coinReviveUsed && save.CoinBalance >= GameManager.ReviveCoinsCost;
            bool adsRemoved = save != null && save.AdsRemoved;
            _free.gameObject.SetActive(adsRemoved);
            _free.interactable = run != null && !run.freeReviveUsed;
            _watchAd.gameObject.SetActive(!adsRemoved);
            _coins.text = save != null ? save.CoinBalance.ToString() : "0";
            // The spinning coin's pivot is its centre, so it turns in place.
            _coin.pivot = new Vector2(0.5f, 0.5f);
            _coin.anchoredPosition = new Vector2(-CornerMargin - _coins.preferredWidth - 12f - CoinSize * 0.5f, -CornerMargin - CoinSize * 0.5f);
            _drop.Play(new Vector2(0f, PanelSize.y * 0.5f));
            Tick();
        }

        /// <summary>Called every frame by GameFlow while open: Watch Ad is tappable only while an ad is loaded.</summary>
        public void Tick()
        {
            _watchAd.interactable = AdManager.Instance != null && AdManager.Instance.IsRewardedAdReady;
        }

        private void Accept()
        {
            if (GameManager.Instance != null && GameManager.Instance.AcceptRevive())
            {
                Close();
            }
        }

        private void AcceptFree()
        {
            if (GameManager.Instance != null && GameManager.Instance.AcceptFreeRevive())
            {
                Close();
            }
        }

        private void WatchAd()
        {
            AdManager.Instance?.ShowRewardedAd("continue_after_death", rewarded =>
            {
                if (rewarded && GameManager.Instance != null && GameManager.Instance.AcceptReviveViaAd())
                {
                    Close();
                }
            });
        }

        // Closed after a revive: no decline, and confetti pops over the cannon (the extra animal is in it).
        private void Close()
        {
            _deciding = false;
            base.Hide();
            var cannon = GameObject.Find("FarmCannon");
            if (cannon != null && Camera.main != null)
            {
                // Screen Space Overlay: a RectTransform's position is in screen pixels.
                _confetti.transform.position = Camera.main.WorldToScreenPoint(cannon.transform.position + Vector3.up * 1.5f);
            }
            _confetti.transform.SetAsLastSibling();
            _confetti.Pop();
        }

        /// <summary>No / Esc: the attempt ends.</summary>
        public override void Hide()
        {
            bool declined = _deciding && IsOpen;
            _deciding = false;
            base.Hide();
            if (declined)
            {
                GameManager.Instance?.DeclineRevive();
            }
        }
    }
}
