using System;
using UnityEngine;
using FarmFury.Shared.UI;

namespace FarmFury.UI
{
    /// <summary>
    /// Art for the Settings / Shop family of overlay screens, ported from Farm Fury: Arcade's menu hub, Settings,
    /// Shop, coin-pack, cosmetics, world-purchase, Leaderboards, Character Story and Legal screens. Assigned to
    /// GameFlow by Phase 5a setup (StampedeShopArt) from Sprites/UI and Sprites/UI/Shop (the files Stampede didn't
    /// have, copied from Arcade). Any sprite left null falls back to a plain labelled button or text.
    /// The fields the shared overlay screens use (page backdrop, signs, shop icons, plaque, coin packs, Yes/No...) live
    /// on com.farmfury.core's ShopArtBase; only Cannonfire's own sprites are declared here (never redeclare a base field).
    /// </summary>
    [Serializable]
    public class ShopArt : ShopArtBase
    {
        [Header("Wood-sign headers")]
        [Tooltip("Menu hub 'Visit Our Store' link-out. (MerchBanner.png)")]
        public Sprite merchBanner;
        [Tooltip("Menu hub cross-promo cards for the sister games: each game's app icon on a wood rim (Franchise/CrossPromo/Stampede.png, Arcade.png).")]
        public Sprite crossPromoStampede;
        public Sprite crossPromoArcade;

        [Header("Settings icons")]
        [Tooltip("Music on/off, dimmed while muted. (Btn_music-remove.png)")]
        public Sprite musicIcon;
        public Sprite leaderboardIcon;
        public Sprite characterStoryIcon;
        public Sprite policiesIcon;

        [Header("Purchase screens")]
        [Tooltip("World purchase price sign. (Shop/3.99.png)")]
        public Sprite worldPrice;
        public Sprite cannonsBanner;
        public Sprite trailsBanner;
        public Sprite impactsBanner;
        [Tooltip("Price cards, in StoreProducts.Cannons / Trails / Impacts order.")]
        public Sprite[] cannonItems = new Sprite[0];
        public Sprite[] trailItems = new Sprite[0];
        public Sprite[] impactItems = new Sprite[0];

        [Header("Coins, ads and the Locker (Phase 6)")]
        [Tooltip("Revive prompt sign, 'Revive for 5 coins' baked in. (Shop/Revive Prompt panel background.png)")]
        public Sprite revivePanel;
        [Tooltip("'Watch Ad' banner: the revive prompt's ad button and the label under Double Coins. (WatchAd.png)")]
        public Sprite watchAd;
        [Tooltip("Level Complete's rewarded Double Coins button. (DoubleCoins.png)")]
        public Sprite doubleCoins;
        [Tooltip("HUD Locker button. (Locker.png)")]
        public Sprite lockerIcon;
        [Tooltip("Locker header sign. (LockerBanner.png)")]
        public Sprite lockerBanner;
        [Tooltip("Locker's 'You may like...' card, Cluck pointing at a blank board. (LockerAd.png)")]
        public Sprite lockerSuggestion;
        [Tooltip("Frame behind each owned item in the Locker. (Shop/PurchaseCardFrame.png)")]
        public Sprite cardFrame;

        [Header("Shop promotion")]
        [Tooltip("Header of the page after a first boss clear: Cluck's board, 'New Worlds'. (Shop/Promo_NewWorlds.png)")]
        public Sprite promoNewWorlds;
        [Tooltip("Level Complete's Remove Ads card: Cluck's board, crossed-out 'Ads'. (Shop/Promo_NoAds.png)")]
        public Sprite promoNoAds;

        [Header("On-screen controls (HUD)")]
        [Tooltip("Hold to run left / right. (left.png, right.png)")]
        public Sprite moveLeftButton;
        public Sprite moveRightButton;
        [Tooltip("Jump. (up.png)")]
        public Sprite jumpButton;

        [Header("Character Story")]
        [Tooltip("One per animal, in AnimalType order. (<Name>_ability.png) Currently unused: the Characters tab shows only the card and text.")]
        public Sprite[] abilityIcons = new Sprite[0];
        [Tooltip("Plain cosmetic art for the Cosmetics tab, in StoreProducts.CosmeticNames order.")]
        public Sprite[] cosmeticIcons = new Sprite[0];

        [Header("Cosmetics drawn on the animals (StoreProducts order)")]
        [Tooltip("Cannon skins (same pose and size as Cannon.png), in StoreProducts.Cannons order.")]
        public Sprite[] cannonArt = new Sprite[0];
        [Tooltip("Robot-defeat burst art, in StoreProducts.Impacts order.")]
        public Sprite[] impactArt = new Sprite[0];
        [Tooltip("Trail art stamped behind a flying animal, in StoreProducts.Trails order.")]
        public Sprite[] trailArt = new Sprite[0];

        public Sprite AbilityIcon(AnimalType type) => At(abilityIcons, (int)type);
    }
}
