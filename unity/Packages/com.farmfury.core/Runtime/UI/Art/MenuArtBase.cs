using System;
using UnityEngine;

namespace FarmFury.Shared.UI
{
    /// <summary>
    /// The menu art the shared screens use (overlay back button, Pause, the landing poster behind overlays). Each game
    /// derives its own [Serializable] <c>MenuArt</c> from this, adds its game-only sprites, and keeps it on its GameFlow
    /// as before: Unity serializes inherited fields by name, so moving a field into this base keeps the value already
    /// in the scene. A derived class must not redeclare these fields. Any sprite left null falls back to the plain
    /// code-built look.
    /// </summary>
    [Serializable]
    public class MenuArtBase
    {
        [Header("Shared: landing")]
        [Tooltip("Full-screen landing poster; also the dimmed backdrop of an overlay page with no ShopArtBase.pageBackground.")]
        public Sprite landingPoster;
        [Tooltip("Round play button (landing, Pause). Null = a plain button.")]
        public Sprite playButton;
        [Tooltip("Exit button on the landing screen (hidden on iOS).")]
        public Sprite exitButton;
        [Tooltip("Settings cog (landing, Pause).")]
        public Sprite settingsButton;

        [Header("Shared: round buttons")]
        [Tooltip("Round home button: back to the landing screen.")]
        public Sprite homeButton;
        [Tooltip("Round back button: every overlay page's close button, bottom-right.")]
        public Sprite backButton;
        [Tooltip("Round X: back to Level Select from Pause / results.")]
        public Sprite quitButton;
        [Tooltip("Round pause button on the gameplay HUD.")]
        public Sprite pauseButton;

        [Header("Shared: World Select")]
        [Tooltip("World Select backdrop (the sunset farm). Null = the plain dark background.")]
        public Sprite worldSelectBackground;
        [Tooltip("World Select header banner (the wooden World Unlocked sign). Null = 'SELECT A WORLD' text.")]
        public Sprite worldSelectBanner;

        [Header("Shared: Level Select tiles")]
        [Tooltip("A locked level: plank with a padlock. Null (or no world backdrop) = the plain look.")]
        public Sprite levelTileLocked;
        [Tooltip("The next level to play: plank with a question mark.")]
        public Sprite levelTileNext;
        [Tooltip("The boss's tile: shield reading BOSS.")]
        public Sprite bossShield;
        [Tooltip("Cluck on a board with 1 / 2 / 3 gold stars: results and completed Level Select tiles.")]
        public Sprite oneStarBoard;
        public Sprite twoStarBoard;
        public Sprite threeStarBoard;

        [Header("Shared: New Character / World Unlocked")]
        [Tooltip("Sunset hills with the logo painted in (1280x720 widened). A world's own replaces it.")]
        public Sprite newCharacterBackground;
        [Tooltip("'New Character' lettering over the backdrop.")]
        public Sprite newCharacterTitle;
        [Tooltip("The wooden World Unlocked sign (results, World Unlocked page).")]
        public Sprite worldUnlockedSign;

        [Header("Shared: pause")]
        [Tooltip("Pause backdrop, 1280x720 widened by OverlayScreen.WideArtPad each side. Null = the plain panel menu.")]
        public Sprite pauseBackground;

        /// <summary>The 1 / 2 / 3-star board for a beaten level; null for 0 stars.</summary>
        public Sprite StarBoard(int stars) => stars switch
        {
            >= 3 => threeStarBoard,
            2 => twoStarBoard,
            1 => oneStarBoard,
            _ => null,
        };

        /// <summary>A world's override when set, else the shared one.</summary>
        public static Sprite Either(Sprite world, Sprite shared) => world != null ? world : shared;
    }
}
