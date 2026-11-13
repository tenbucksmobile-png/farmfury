using System;
using UnityEngine;
using UnityEngine.UI;

namespace FarmFury.Shared.UI
{
    /// <summary>
    /// Settings (franchise layout, ported from Stampede's SettingsScreen): the SETTINGS wood sign over one row of four
    /// large icons — Music, Leaderboards, Character Story, Policies. Opened from the menu hub (landing / Level
    /// Complete cog) and straight from Pause's cog. Replaces MainMenuController's 7-tab settings popup (2026-11-02).
    /// The first icon is the game's sound toggle: the game says what it switches (Stampede: music; Cannonfire: music
    /// and effects together) through the label, isOn and setOn it passes in.
    /// </summary>
    public class SettingsScreen : OverlayScreen
    {
        private readonly Button _music;
        private readonly string _soundLabel;
        private readonly Func<bool> _isSoundOn;
        private readonly Action<bool> _setSoundOn;

        /// <param name="soundLabel">Text-fallback label of the sound icon, e.g. "MUSIC" or "SOUND" ("... ON/OFF").</param>
        /// <param name="isSoundOn">Read from the game's audio settings.</param>
        /// <param name="setSoundOn">Persist and apply (the game's AudioManager / save).</param>
        public SettingsScreen(Transform canvas, MenuArtBase art, ShopArtBase shop, Action onLeaderboards, Action onCharacterStory,
            Action onPolicies, string soundLabel, Func<bool> isSoundOn, Action<bool> setSoundOn)
            : base(canvas, "Settings", art, shop)
        {
            _soundLabel = soundLabel;
            _isSoundOn = isSoundOn;
            _setSoundOn = setSoundOn;
            HeaderSign(Shop.settingsSign, "SETTINGS");

            var row = Row("SettingsGrid", 4, new Vector2(IconSize, IconSize), IconSpacing, -183f);
            _music = IconButton(row, "MusicCell", Shop.musicIcon, soundLabel, ToggleSound);
            IconButton(row, "LeaderboardCell", Shop.leaderboardIcon, "LEADERBOARDS", onLeaderboards);
            IconButton(row, "CharacterStoryCell", Shop.characterStoryIcon, "STORY", onCharacterStory);
            IconButton(row, "PoliciesCell", Shop.policiesIcon, "POLICIES", onPolicies);
        }

        protected override void OnShow() => RefreshMusicIcon();

        private bool SoundOn => _isSoundOn == null || _isSoundOn();

        private void ToggleSound()
        {
            _setSoundOn?.Invoke(!SoundOn);
            RefreshMusicIcon();
        }

        private void RefreshMusicIcon()
        {
            bool on = SoundOn;
            _music.image.color = on ? (Shop.musicIcon != null ? Color.white : Wood) : OwnedTint;
            if (Shop.musicIcon == null)
            {
                _music.GetComponentInChildren<Text>().text = _soundLabel + (on ? " ON" : " OFF");
            }
        }
    }
}
