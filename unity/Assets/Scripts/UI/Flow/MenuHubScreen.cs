using System;
using UnityEngine;

namespace FarmFury.UI
{
    /// <summary>
    /// Arcade's menu hub, opened by the settings cog (landing screen, Level Complete): three stacked wood signs of
    /// the same size - SETTINGS, Shop, and the "Visit Our Store" merch link-out (behind the parental gate) - over the dimmed poster. Each sign
    /// opens its screen on top; the hub stays underneath, so their back buttons return here.
    /// </summary>
    public class MenuHubScreen : OverlayScreen
    {
        private const string MerchUrl = "https://www.farmfurygames.com/";
        private static readonly Vector2 SignSize = new(550f, 190f);
        private const float SignGap = 30f;
        private const float TopOffset = -180f;

        public MenuHubScreen(Transform canvas, MenuArt art, ShopArt shop, ParentalGate gate, Action onSettings, Action onShop)
            : base(canvas, "MenuHub", art, shop)
        {
            float step = SignSize.y + SignGap;
            BannerButton("SettingsSignButton", Shop.settingsSign, "SETTINGS", SignSize, TopOffset, onSettings);
            BannerButton("ShopSignButton", Shop.shopSign, "SHOP", SignSize, TopOffset - step, onShop);
            BannerButton("MerchBannerButton", Shop.merchBanner, "VISIT OUR STORE", SignSize, TopOffset - 2f * step,
                () => gate.Ask(() => Application.OpenURL(MerchUrl)));
        }
    }
}
