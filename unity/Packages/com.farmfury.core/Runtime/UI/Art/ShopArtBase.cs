using System;
using UnityEngine;

namespace FarmFury.Shared.UI
{
    /// <summary>
    /// The Settings / Shop family art the shared overlay screens use (headers, shop icons, purchase screens, Yes/No).
    /// Each game derives its own [Serializable] <c>ShopArt</c> from this and adds its game-only sprites (cosmetic
    /// banners and price cards, HUD controls, Locker...). Inherited fields serialize by name, so values already in a
    /// scene survive the move; a derived class must not redeclare them. Null sprites fall back to text buttons.
    /// </summary>
    [Serializable]
    public class ShopArtBase
    {
        [Header("Shared: page")]
        [Tooltip("Backdrop of every Settings / Shop page, 1280x720 widened by OverlayScreen.WideArtPad each side. Null = the dimmed landing poster.")]
        public Sprite pageBackground;

        [Header("Shared: wood-sign headers")]
        public Sprite settingsSign;
        public Sprite shopSign;
        public Sprite legalSign;
        public Sprite leaderboardSign;

        [Header("Shared: shop icons")]
        [Tooltip("Cash: opens the coin packs.")]
        public Sprite cashIcon;
        public Sprite worldsIcon;
        public Sprite removeAdsIcon;
        public Sprite cosmeticsIcon;

        [Header("Shared: purchase screens")]
        [Tooltip("Wooden plaque behind text buttons (Restore Purchases, Legal, parental gate); 9-sliced.")]
        public Sprite plaque;
        [Tooltip("Coin-pack plaques, price baked in, in the order of the game's coin-pack product ids (Store.CoinPackIds).")]
        public Sprite[] coinPacks = new Sprite[0];
        [Tooltip("Tick badge over an owned item.")]
        public Sprite ownedBadge;
        [Tooltip("Banner that fades in after a purchase.")]
        public Sprite purchaseComplete;

        [Header("Shared: coins and prompts")]
        [Tooltip("Coin glyph (HUD balance, payouts).")]
        public Sprite coinIcon;
        [Tooltip("Yes / No buttons, labels baked in (Use Coins prompt, revive prompt).")]
        public Sprite yesButton;
        public Sprite noButton;
        [Tooltip("'Use Coins' sign asked before buying an item with coins.")]
        public Sprite useCoinsSign;

        /// <summary>The sprite at index, or null when the array is missing or too short.</summary>
        public static Sprite At(Sprite[] sprites, int index) =>
            sprites != null && index >= 0 && index < sprites.Length ? sprites[index] : null;
    }
}
