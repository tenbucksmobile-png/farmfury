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

        [Header("Shared: pause")]
        [Tooltip("Pause backdrop, 1280x720 widened by OverlayScreen.WideArtPad each side. Null = the plain panel menu.")]
        public Sprite pauseBackground;
    }
}
