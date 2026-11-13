using FarmFury.Shared.Monetisation;
using UnityEngine;
using UnityEngine.UI;

namespace FarmFury.Shared.UI
{
    /// <summary>
    /// Arcade's CosmeticPurchaseScreen: one centred row of purchasable items under a header, used for the Hats &amp;
    /// Caps, Trails and Machines pages (price plaques under the same banner the chooser showed) and the Worlds page
    /// (world cards under the World Unlocked sign, with a price sign below). Owned items get the tick badge and stop
    /// being tappable. A hat or trail the player can afford in coins first asks "Use Coins?" (yes = pay 2500 coins,
    /// no = buy with money); a money purchase passes the parental gate, then goes through <see cref="Store"/>. A
    /// successful purchase shows the Purchase Complete banner (4s, then fades).
    /// </summary>
    public class ItemPurchaseScreen : OverlayScreen
    {
        public struct Item
        {
            public string productId;
            public Sprite sprite;
            public string label;   // shown only when there's no art

            public Item(string productId, Sprite sprite, string label)
            {
                this.productId = productId;
                this.sprite = sprite;
                this.label = label;
            }
        }

        // Arcade: the cosmetics banners (619x246 art) are 700 wide, 55 below the top centre like every header.
        public static readonly Vector2 BannerSize = new(700f, 700f * 246f / 619f);

        private readonly ParentalGate _gate;
        private readonly UseCoinsPrompt _useCoins;
        private readonly Item[] _items;
        private readonly Button[] _buttons;
        private readonly Image[] _badges;
        private readonly Text _status;
        private readonly HoldThenFade _complete;

        /// <param name="banner">Cosmetics-style 700-wide banner; null uses <paramref name="headerSign"/> as a standard sign instead.</param>
        public ItemPurchaseScreen(Transform canvas, string name, MenuArtBase art, ShopArtBase shop, ParentalGate gate, UseCoinsPrompt useCoins,
            Sprite banner, Sprite headerSign, string title, Item[] items, Vector2 cell, float spacing, Sprite priceSign = null)
            : base(canvas, name, art, shop)
        {
            _gate = gate;
            _useCoins = useCoins;
            _items = items;

            if (banner != null)
            {
                var header = UIKit.Picture(Rect, "TitleImage", banner);
                UIKit.Place(header.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, HeaderTop), BannerSize);
            }
            else
            {
                HeaderSign(headerSign, title);
            }

            var row = Row(name + "Row", items.Length, cell, spacing, -60f);
            _buttons = new Button[items.Length];
            _badges = new Image[items.Length];
            for (int i = 0; i < items.Length; i++)
            {
                string productId = items[i].productId;
                _buttons[i] = IconButton(row, $"{name}Item{i}", items[i].sprite, items[i].label, () => Tapped(productId));
                _badges[i] = UIKit.Picture(_buttons[i].transform, "OwnedBadge", Shop.ownedBadge);
                UIKit.Place(_badges[i].rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(10f, 10f), new Vector2(100f, 100f));
                _badges[i].gameObject.SetActive(false);
            }

            if (items.Length == 0)
            {
                // Nothing on sale yet (the Worlds page before any paid world exists).
                var soon = UIKit.Label(Rect, "ComingSoon", "Coming soon!", 72, TextAnchor.MiddleCenter, UIKit.Accent);
                soon.fontStyle = FontStyle.Bold;
                soon.gameObject.AddComponent<Outline>().effectDistance = new Vector2(3f, -3f);
                UIKit.Place(soon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -60f), new Vector2(1200f, 120f));
            }
            else if (priceSign != null)
            {
                var price = UIKit.Picture(Rect, "PriceSign", priceSign);
                UIKit.Place(price.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 65f), new Vector2(340f, 180f));
            }

            _status = StatusText(priceSign != null ? 250f : 30f);

            var complete = UIKit.Picture(Rect, "PurchaseCompleteBanner", Shop.purchaseComplete);
            UIKit.Place(complete.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(560f, 294f));
            complete.gameObject.AddComponent<CanvasGroup>().blocksRaycasts = false;
            _complete = complete.gameObject.AddComponent<HoldThenFade>();
            complete.gameObject.SetActive(false);
        }

        protected override void OnShow()
        {
            _status.text = "";
            _complete.gameObject.SetActive(false);
            RefreshOwned();
        }

        private void Tapped(string productId)
        {
            if (Store.IsOwned(productId))
            {
                return;
            }

            _complete.gameObject.SetActive(false);
            bool canPayInCoins = Store.CanAffordWithCoins(productId);
            if (canPayInCoins && _useCoins != null)
            {
                _useCoins.Ask(() => BuyWithCoins(productId), () => BuyWithMoney(productId));
            }
            else
            {
                BuyWithMoney(productId);
            }
        }

        private void BuyWithCoins(string productId)
        {
            if (Store.PurchaseWithCoins(productId))
            {
                Purchased();
            }
            else
            {
                _status.text = "Not enough coins.";
            }
        }

        private void BuyWithMoney(string productId)
        {
            _gate.Ask(() =>
            {
                _status.text = "Processing...";
                Store.Purchase(productId, (ok, message) =>
                {
                    if (ok) { Purchased(); } else { _status.text = message; }
                });
            });
        }

        private void Purchased()
        {
            _status.text = Shop.purchaseComplete == null ? "Purchase complete!" : "";
            if (Shop.purchaseComplete != null)
            {
                _complete.transform.SetAsLastSibling();
                _complete.Play();
            }
            RefreshOwned();
        }

        private void RefreshOwned()
        {
            for (int i = 0; i < _items.Length; i++)
            {
                bool owned = Store.IsOwned(_items[i].productId);
                _buttons[i].interactable = !owned;
                _badges[i].gameObject.SetActive(owned && Shop.ownedBadge != null);
            }
        }
    }
}
