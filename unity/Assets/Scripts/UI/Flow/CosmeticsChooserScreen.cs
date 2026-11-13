using System;
using UnityEngine;
using FarmFury.Shared.UI;

namespace FarmFury.UI
{
    /// <summary>
    /// Arcade's cosmetics chooser (Shop -> Cosmetics): the Hats &amp; Caps, Trails and Machines banners stacked
    /// top-centre with a fixed gap, each opening its purchase page on top (which shows the same banner as its
    /// header, so the player sees where they are).
    /// </summary>
    public class CosmeticsChooserScreen : OverlayScreen
    {
        // This game's ShopArt (the shared base holds it as ShopArtBase).
        private new ShopArt Shop => (ShopArt)base.Shop;

        private const float BannerGap = 30f;

        public CosmeticsChooserScreen(Transform canvas, MenuArt art, ShopArt shop, Action onCannons, Action onTrails, Action onImpacts)
            : base(canvas, "CosmeticsChooser", art, shop)
        {
            // Three banners: slightly smaller than a page header so all three fit under the top edge.
            var size = ItemPurchaseScreen.BannerSize * 0.82f;
            float step = size.y + BannerGap;
            BannerButton("CannonsBannerButton", Shop.cannonsBanner, "CANNONS", size, HeaderTop, onCannons);
            BannerButton("TrailsBannerButton", Shop.trailsBanner, "TRAILS", size, HeaderTop - step, onTrails);
            BannerButton("ImpactsBannerButton", Shop.impactsBanner, "IMPACTS", size, HeaderTop - 2f * step, onImpacts);
        }
    }
}
