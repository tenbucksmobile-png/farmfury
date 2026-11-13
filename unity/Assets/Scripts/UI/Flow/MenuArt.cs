using System;
using UnityEngine;

namespace FarmFury.UI
{
    /// <summary>
    /// Shared menu art on <see cref="GameFlow"/>, wired by Wire Scene References (SceneSetup.EnsureGameFlow) —
    /// the franchise convention from Stampede's MenuArt. Stand-ins come from FarmFury_Stampede
    /// (Assets/Sprites/UI/Franchise/), FarmFury's own button set from Assets/Sprites/UI/Icon/. Every screen has a
    /// plain fallback when a field is empty. Per-world art lives on WorldData and overrides these.
    /// See docs/ART_REQUIREMENTS.md for which of these still need FarmFury-branded versions.
    /// </summary>
    [Serializable]
    public class MenuArt
    {
        [Header("Landing")]
        [Tooltip("Title page poster shown when the game opens (LandingTitle.png, 2400x1080: Cluck + Ducky by the cannon); tap anywhere to continue.")]
        public Sprite landingTitle;
        [Tooltip("The PRESS START lettering over the title page (PressStart.png).")]
        public Sprite pressStart;
        [Tooltip("Landing menu page poster, full-screen (LandingPage.png, 2400x1080: the cannon on hay, no characters) under Play / Exit / Settings.")]
        public Sprite landingPoster;
        [Tooltip("A heavily blurred copy of the poster (LandingPage_Blur.png, tools/brand_cannonfire.py): fills the side gaps on screens wider than the poster.")]
        public Sprite landingPosterBlur;
        public Sprite playButton;
        [Tooltip("Exit, beside Play (hidden on iOS). (Franchise/Menu/Exit.png)")]
        public Sprite exitButton;
        public Sprite settingsButton;

        [Header("Round buttons")]
        public Sprite homeButton;
        public Sprite backButton;
        [Tooltip("Round X: back to Level Select from Pause / results.")]
        public Sprite quitButton;
        public Sprite pauseButton;

        [Header("World Select")]
        [Tooltip("World Select backdrop (Franchise/Canvas/Canvas.png, the sunset field).")]
        public Sprite worldSelectBackground;
        [Tooltip("Header banner (World Unlocked sign). Null = 'SELECT A WORLD' text.")]
        public Sprite worldSelectBanner;

        [Header("Level Select tiles")]
        public Sprite levelTileLocked;
        public Sprite levelTileNext;
        public Sprite bossShield;
        public Sprite oneStarBoard;
        public Sprite twoStarBoard;
        public Sprite threeStarBoard;

        [Header("Character Select")]
        [Tooltip("Backdrop with 'Character' lettering. A world's WorldData.characterSelectBackground replaces it.")]
        public Sprite characterSelectBackground;
        [Tooltip("Framed animal cards (name baked in), indexed by AnimalType.")]
        public Sprite[] animalCards = new Sprite[8];
        [Tooltip("Small ability icons (egg, slam, roll, clone, skip, horseshoe, puff, horns), indexed by AnimalType — shown as a badge on each card's bottom-right.")]
        public Sprite[] abilityBadges = new Sprite[8];

        [Header("Character Story")]
        [Tooltip("Robot sprites for the Robots tab, in RobotType order (Pawn, Harvester, Semi-Harvester, Commander).")]
        public Sprite[] robotPortraits = new Sprite[4];
        [Tooltip("Robots tab, per world: 4 per world (pawn, harvester, semi-harvester, boss), Worlds 1-9 in order - the robot prefabs' own art (SceneSetup).")]
        public Sprite[] worldRobots = new Sprite[36];

        [Header("Pause / results")]
        public Sprite pauseBackground;
        public Sprite levelCompleteBackground;
        public Sprite levelFailedBackground;
        [Tooltip("Greyed star drawn over each painted star not earned.")]
        public Sprite levelCompleteStarEmpty;
        public Sprite newCharacterBackground;
        public Sprite newCharacterTitle;
        public Sprite newCharacterSign;
        public Sprite worldUnlockedSign;

        public Sprite StarBoard(int stars) => stars switch
        {
            >= 3 => threeStarBoard,
            2 => twoStarBoard,
            1 => oneStarBoard,
            _ => null,
        };

        public Sprite AbilityBadge(AnimalType animal) =>
            abilityBadges != null && (int)animal < abilityBadges.Length ? abilityBadges[(int)animal] : null;

        public Sprite AnimalCard(AnimalType animal) =>
            animalCards != null && (int)animal < animalCards.Length ? animalCards[(int)animal] : null;

        /// <summary>A world's override when set, else the shared one.</summary>
        public static Sprite Either(Sprite world, Sprite shared) => world != null ? world : shared;
    }
}
