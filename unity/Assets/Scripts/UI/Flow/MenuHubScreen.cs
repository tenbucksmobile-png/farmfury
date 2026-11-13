using System;
using FarmFury.Core;
using UnityEngine;
using UnityEngine.UI;

namespace FarmFury.UI
{
    /// <summary>
    /// Arcade's menu hub, opened by the settings cog (landing screen, Level Complete): three stacked wood signs of
    /// the same size - SETTINGS, Shop, and the "Visit Our Store" merch link-out (behind the parental gate) - over the dimmed poster. Each sign
    /// opens its screen on top; the hub stays underneath, so their back buttons return here.
    /// Either side of the signs (2026-11-13): a card for each sister game (Farm Fury Stampede left, Farm Fury Arcade
    /// right) - its app icon on a wood rim, gently bobbing, a PLAY FREE plaque under it; a tap goes through the parental
    /// gate to that game's store page (<see cref="CrossPromo"/>).
    /// </summary>
    public class MenuHubScreen : OverlayScreen
    {
        private const string MerchUrl = "https://www.farmfurygames.com/";
        private static readonly Vector2 SignSize = new(550f, 190f);
        private const float SignGap = 30f;
        private const float TopOffset = -180f;

        // Cross-promo cards: centred this far either side of the screen centre, clear of the 550-wide signs and of the
        // backdrop's wide plank sign on a 16:9 screen (1920 reference width: the card's outer edge stays ~70 px in).
        private const float PromoCentreX = 735f;
        private const float PromoCentreY = 30f;
        private const float PromoIcon = 290f;
        private static readonly Vector2 PromoPlaque = new(270f, 78f);

        public MenuHubScreen(Transform canvas, MenuArt art, ShopArt shop, ParentalGate gate, Action onSettings, Action onShop)
            : base(canvas, "MenuHub", art, shop)
        {
            float step = SignSize.y + SignGap;
            BannerButton("SettingsSignButton", Shop.settingsSign, "SETTINGS", SignSize, TopOffset, onSettings);
            BannerButton("ShopSignButton", Shop.shopSign, "SHOP", SignSize, TopOffset - step, onShop);
            BannerButton("MerchBannerButton", Shop.merchBanner, "VISIT OUR STORE", SignSize, TopOffset - 2f * step,
                () => gate.Ask(() => Application.OpenURL(MerchUrl)));

            PromoCard(CrossPromo.Game.Stampede, Shop.crossPromoStampede, "FARM FURY\nSTAMPEDE", -1f, gate);
            PromoCard(CrossPromo.Game.Arcade, Shop.crossPromoArcade, "FARM FURY\nARCADE", 1f, gate);
        }

        protected override void OnShow()
        {
            AnalyticsManager.Instance?.LogPromoShown(CrossPromo.PromoId(CrossPromo.Game.Stampede));
            AnalyticsManager.Instance?.LogPromoShown(CrossPromo.PromoId(CrossPromo.Game.Arcade));
        }

        private void PromoCard(CrossPromo.Game game, Sprite icon, string fallback, float side, ParentalGate gate)
        {
            void Open() => gate.Ask(() => CrossPromo.Open(game));

            var card = UIKit.NewRect($"CrossPromo_{game}", Rect);
            UIKit.Place(card, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(side * PromoCentreX, PromoCentreY),
                new Vector2(PromoIcon, PromoIcon + PromoPlaque.y));

            var iconButton = IconButton(card, "Icon", icon, fallback, Open);
            UIKit.Place(iconButton.image.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero,
                new Vector2(PromoIcon, PromoIcon));
            // A slow bob, out of step between the two cards, so they catch the eye without flashing.
            var bob = iconButton.gameObject.AddComponent<PulseScale>();
            bob.amount = 0.035f;
            bob.period = side < 0f ? 2.2f : 2.6f;
            bob.minAlpha = 1f;

            var plaque = PlaqueButton(card, "PlayFree", "PLAY FREE", 34, Open);
            UIKit.Place(plaque.image.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, -6f), PromoPlaque);
        }
    }
}
