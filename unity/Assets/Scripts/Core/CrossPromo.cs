using UnityEngine;
using FarmFury.Shared.Monetisation;

namespace FarmFury.Core
{
    /// <summary>
    /// The other Farm Fury games, advertised on the menu hub (2026-11-13, user: "advertising the other games in farmfury
    /// universe ... use the Stampede and Arcade art - all games are live"). Plain store links behind the parental gate:
    /// no reward for installing (Apple 3.2.2 / Google Play policy forbid incentivised installs).
    /// Store ids from the sister projects: Stampede App Store 6825786519 (live 2026-11-07), Arcade App Store 6804415300;
    /// Google Play packages com.farmfury.stampede / com.farmfury.arcade.
    /// Google Play installs carry a referrer (utm_source=cannonfire, utm_medium=cross_promo) so they show up in Play
    /// Console's acquisition reports; App Store campaign links need a provider token from App Store Connect, not added.
    /// </summary>
    public static class CrossPromo
    {
        public enum Game { Stampede, Arcade }

        public static string AppStoreId(Game g) => g == Game.Stampede ? "6825786519" : "6804415300";
        public static string PlayPackage(Game g) => g == Game.Stampede ? "com.farmfury.stampede" : "com.farmfury.arcade";
        public static string PromoId(Game g) => g == Game.Stampede ? "xpromo_stampede" : "xpromo_arcade";

        const string Referrer = "utm_source%3Dcannonfire%26utm_medium%3Dcross_promo%26utm_campaign%3Dmenu_hub";

        /// <summary>The store page for this platform: the App Store app on iOS, the Play Store app on Android, the web
        /// page elsewhere (Editor / desktop).</summary>
        public static string StoreUrl(Game g)
        {
#if UNITY_IOS
            return $"itms-apps://apps.apple.com/app/id{AppStoreId(g)}";
#elif UNITY_ANDROID
            return $"market://details?id={PlayPackage(g)}&referrer={Referrer}";
#else
            return Application.platform == RuntimePlatform.OSXPlayer || Application.platform == RuntimePlatform.OSXEditor
                ? $"https://apps.apple.com/app/id{AppStoreId(g)}"
                : $"https://play.google.com/store/apps/details?id={PlayPackage(g)}&referrer={Referrer}";
#endif
        }

        /// <summary>Called once the parental gate is passed.</summary>
        public static void Open(Game g)
        {
            AnalyticsManager.Instance?.LogPromoTapped(PromoId(g));
            Application.OpenURL(StoreUrl(g));
        }
    }
}
