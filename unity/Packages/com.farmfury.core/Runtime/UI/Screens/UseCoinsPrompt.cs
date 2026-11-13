using System;
using UnityEngine;
using UnityEngine.UI;

namespace FarmFury.Shared.UI
{
    /// <summary>
    /// Arcade's Use Coins prompt: tapping a hat or trail the player can afford with coins asks first - the "Use
    /// Coins x2500" sign with Yes (pay in coins) and No (go on to the real-money purchase) below it.
    /// </summary>
    public class UseCoinsPrompt : OverlayScreen
    {
        private const float SignWidth = 700f;
        private const float ButtonWidth = 220f;
        private const float ButtonGap = 40f;

        private Action _onYes, _onNo;

        /// <param name="coinCost">Shown only in the text fallback when there is no useCoinsSign art.</param>
        public UseCoinsPrompt(Transform canvas, MenuArtBase art, ShopArtBase shop, int coinCost) : base(canvas, "UseCoinsPrompt", art, shop, posterBackdrop: false)
        {
            Root.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f);
            BackButton.gameObject.SetActive(false);

            float signHeight = Shop.useCoinsSign != null ? SignWidth * Shop.useCoinsSign.rect.height / Shop.useCoinsSign.rect.width : 280f;
            if (Shop.useCoinsSign != null)
            {
                var sign = UIKit.Picture(Rect, "Sign", Shop.useCoinsSign);
                UIKit.Place(sign.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 80f), new Vector2(SignWidth, signHeight));
            }
            else
            {
                var text = UIKit.Label(Rect, "Sign", $"Use {coinCost} coins?", 64, TextAnchor.MiddleCenter, UIKit.Accent);
                UIKit.Place(text.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 80f), new Vector2(SignWidth, signHeight));
            }

            float buttonHeight = ButtonWidth * 214f / 512f;
            float y = 80f - signHeight * 0.5f - 30f - buttonHeight * 0.5f;
            var yes = IconButton(Rect, "YesButton", Shop.yesButton, "YES", () => Answer(true));
            UIKit.Place(yes.image.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-(ButtonWidth + ButtonGap) * 0.5f, y), new Vector2(ButtonWidth, buttonHeight));
            var no = IconButton(Rect, "NoButton", Shop.noButton, "NO", () => Answer(false));
            UIKit.Place(no.image.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2((ButtonWidth + ButtonGap) * 0.5f, y), new Vector2(ButtonWidth, buttonHeight));
        }

        public void Ask(Action onYes, Action onNo)
        {
            _onYes = onYes;
            _onNo = onNo;
            Show();
        }

        private void Answer(bool yes)
        {
            var callback = yes ? _onYes : _onNo;
            _onYes = _onNo = null;
            Hide();
            callback?.Invoke();
        }
    }
}
