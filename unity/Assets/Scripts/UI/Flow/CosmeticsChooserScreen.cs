using System;
using UnityEngine;

namespace FarmFury.UI
{
    /// <summary>
    /// Arcade's cosmetics chooser (Shop -> Cosmetics): the Hats &amp; Caps, Trails and Machines banners stacked
    /// top-centre with a fixed gap, each opening its purchase page on top (which shows the same banner as its
    /// header, so the player sees where they are).
    /// </summary>
    public class CosmeticsChooserScreen : OverlayScreen
    {
        private const float BannerGap = 30f;

        public CosmeticsChooserScreen(Transform canvas, MenuArt art, ShopArt shop, Action onHats, Action onTrails)
            : base(canvas, "CosmeticsChooser", art, shop)
        {
            var size = ItemPurchaseScreen.BannerSize;
            float step = size.y + BannerGap;
            BannerButton("HatsBannerButton", Shop.hatsBanner, "HATS & CAPS", size, HeaderTop, onHats);
            BannerButton("TrailsBannerButton", Shop.trailsBanner, "TRAILS", size, HeaderTop - step, onTrails);
        }
    }
}
