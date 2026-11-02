using System;
using FarmFury.Core;
using UnityEngine;
using UnityEngine.UI;

namespace FarmFury.UI
{
    /// <summary>
    /// Arcade's Shop hub: the Shop sign and one row of four icons - Cash (the coin packs), Worlds (world purchase),
    /// Ads (a direct Remove Ads purchase behind the parental gate, dimmed once owned) and Cosmetics (the hats /
    /// trails / machines chooser). Purchases go through <see cref="Store"/>, a stub until Phase 6's IAP.
    /// </summary>
    public class ShopScreen : OverlayScreen
    {
        private readonly ParentalGate _gate;
        private readonly Button _removeAds;
        private readonly Text _status;

        public ShopScreen(Transform canvas, MenuArt art, ShopArt shop, ParentalGate gate, Action onCash, Action onWorlds, Action onCosmetics)
            : base(canvas, "Shop", art, shop)
        {
            _gate = gate;
            HeaderSign(Shop.shopSign, "SHOP");

            var row = Row("ShopGrid", 4, new Vector2(IconSize, IconSize), IconSpacing, -183f);
            IconButton(row, "CashCell", Shop.cashIcon, "COINS", onCash);
            IconButton(row, "WorldsCell", Shop.worldsIcon, "WORLDS", onWorlds);
            _removeAds = IconButton(row, "RemoveAdsCell", Shop.removeAdsIcon, "NO ADS", () => _gate.Ask(BuyRemoveAds));
            IconButton(row, "CosmeticsCell", Shop.cosmeticsIcon, "COSMETICS", onCosmetics);

            _status = StatusText(170f);
        }

        protected override void OnShow()
        {
            _status.text = "";
            RefreshRemoveAds();
        }

        private void BuyRemoveAds()
        {
            Store.Purchase(StoreProducts.RemoveAds, (ok, message) =>
            {
                _status.text = ok ? "Ads removed!" : message;
                RefreshRemoveAds();
            });
        }

        /// <summary>A non-consumable can't be bought twice: dim and disable the icon once owned.</summary>
        private void RefreshRemoveAds()
        {
            bool owned = Store.IsOwned(StoreProducts.RemoveAds);
            _removeAds.interactable = !owned;
            if (Shop.removeAdsIcon != null)
            {
                _removeAds.image.color = owned ? OwnedTint : Color.white;
            }
        }
    }
}
