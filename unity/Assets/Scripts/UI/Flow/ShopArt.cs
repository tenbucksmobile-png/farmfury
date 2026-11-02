using System;
using UnityEngine;

namespace FarmFury.UI
{
    /// <summary>
    /// Art for the Settings / Shop family of overlay screens, ported from Farm Fury: Arcade's menu hub, Settings,
    /// Shop, coin-pack, cosmetics, world-purchase, Leaderboards, Character Story and Legal screens. Assigned to
    /// GameFlow by Phase 5a setup (StampedeShopArt) from Sprites/UI and Sprites/UI/Shop (the files Stampede didn't
    /// have, copied from Arcade). Any sprite left null falls back to a plain labelled button or text.
    /// </summary>
    [Serializable]
    public class ShopArt
    {
        [Tooltip("Backdrop of every Settings / Shop page: Cluck bursting through the wooden sign (Environment/Settings_Canvas.png, " +
            "widened by setup to Settings_Canvas_Wide.png, the Leaderboard's backdrop). Null = the dimmed landing poster.")]
        public Sprite pageBackground;

        [Header("Wood-sign headers")]
        [Tooltip("Menu hub + Settings header. (Shop/SettingsSign.png)")]
        public Sprite settingsSign;
        [Tooltip("Menu hub + Shop / coin-pack header. (ShopBanner.png)")]
        public Sprite shopSign;
        [Tooltip("Menu hub 'Visit Our Store' link-out. (MerchBanner.png)")]
        public Sprite merchBanner;
        public Sprite legalSign;
        public Sprite leaderboardSign;

        [Header("Settings icons")]
        [Tooltip("Music on/off, dimmed while muted. (Btn_music-remove.png)")]
        public Sprite musicIcon;
        public Sprite leaderboardIcon;
        public Sprite characterStoryIcon;
        public Sprite policiesIcon;

        [Header("Shop icons")]
        [Tooltip("Cash: opens the coin packs. (Shop/Shop.png)")]
        public Sprite cashIcon;
        public Sprite worldsIcon;
        public Sprite removeAdsIcon;
        public Sprite cosmeticsIcon;

        [Header("Purchase screens")]
        [Tooltip("Wooden plaque behind text buttons (Restore Purchases, Legal, parental gate); 9-sliced. (Shop/Btn_plaque.png)")]
        public Sprite plaque;
        [Tooltip("Coin-pack plaques, price baked in: 100 / 500 / 5000 / 15000.")]
        public Sprite[] coinPacks = new Sprite[0];
        [Tooltip("World purchase price sign. (Shop/3.99.png)")]
        public Sprite worldPrice;
        public Sprite hatsBanner;
        public Sprite trailsBanner;
        public Sprite machinesBanner;
        [Tooltip("Price plaques, in StoreProducts.Hats / Trails / Machines order.")]
        public Sprite[] hatItems = new Sprite[0];
        public Sprite[] trailItems = new Sprite[0];
        public Sprite[] machineItems = new Sprite[0];
        [Tooltip("Tick badge over an owned item. (Shop/EquippedBadge_Icon.png)")]
        public Sprite ownedBadge;
        [Tooltip("Banner that fades in after a purchase. (PurchaseComplete.png)")]
        public Sprite purchaseComplete;

        [Header("Coins, ads and the Locker (Phase 6)")]
        [Tooltip("Coin glyph: HUD balance, Level Complete payout. (Shop/Coin_UI.png)")]
        public Sprite coinIcon;
        [Tooltip("Yes / No buttons, labels baked in: revive prompt, Use Coins prompt. (Shop/Yes.png, Shop/No.png)")]
        public Sprite yesButton;
        public Sprite noButton;
        [Tooltip("Revive prompt sign, 'Revive for 5 coins' baked in. (Shop/Revive Prompt panel background.png)")]
        public Sprite revivePanel;
        [Tooltip("'Watch Ad' banner: the revive prompt's ad button and the label under Double Coins. (WatchAd.png)")]
        public Sprite watchAd;
        [Tooltip("Level Complete's rewarded Double Coins button. (DoubleCoins.png)")]
        public Sprite doubleCoins;
        [Tooltip("'Use Coins x2500' sign, asked before buying a hat or trail with coins. (UseCoins.png)")]
        public Sprite useCoinsSign;
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

        public Sprite AbilityIcon(AnimalType type) => At(abilityIcons, (int)type);

        public static Sprite At(Sprite[] sprites, int index) =>
            sprites != null && index >= 0 && index < sprites.Length ? sprites[index] : null;
    }
}
