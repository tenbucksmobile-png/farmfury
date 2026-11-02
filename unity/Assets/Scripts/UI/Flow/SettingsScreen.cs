using System;
using UnityEngine;
using UnityEngine.UI;

namespace FarmFury.UI
{
    /// <summary>
    /// Settings (franchise layout, ported from Stampede's SettingsScreen): the SETTINGS wood sign over one row of four
    /// large icons — Music, Leaderboards, Character Story, Policies. Opened from the menu hub (landing / Level
    /// Complete cog) and straight from Pause's cog. Replaces MainMenuController's 7-tab settings popup (2026-11-02).
    /// FarmFury difference: the Music icon mutes music AND sound effects together — it takes over the old in-level
    /// Mute button's job (AudioManager's persisted MusicEnabled/SfxEnabled), since the HUD no longer has one.
    /// </summary>
    public class SettingsScreen : OverlayScreen
    {
        private readonly Button _music;

        public SettingsScreen(Transform canvas, MenuArt art, ShopArt shop, Action onLeaderboards, Action onCharacterStory, Action onPolicies)
            : base(canvas, "Settings", art, shop)
        {
            HeaderSign(Shop.settingsSign, "SETTINGS");

            var row = Row("SettingsGrid", 4, new Vector2(IconSize, IconSize), IconSpacing, -183f);
            _music = IconButton(row, "MusicCell", Shop.musicIcon, "SOUND", ToggleSound);
            IconButton(row, "LeaderboardCell", Shop.leaderboardIcon, "LEADERBOARDS", onLeaderboards);
            IconButton(row, "CharacterStoryCell", Shop.characterStoryIcon, "STORY", onCharacterStory);
            IconButton(row, "PoliciesCell", Shop.policiesIcon, "POLICIES", onPolicies);
        }

        protected override void OnShow() => RefreshMusicIcon();

        private static bool SoundOn => AudioManager.MusicEnabled || AudioManager.SfxEnabled;

        private void ToggleSound()
        {
            bool on = !SoundOn;
            AudioManager.SetMusicEnabled(on);
            AudioManager.SetSfxEnabled(on);
            RefreshMusicIcon();
        }

        private void RefreshMusicIcon()
        {
            bool on = SoundOn;
            _music.image.color = on ? (Shop.musicIcon != null ? Color.white : Wood) : OwnedTint;
            if (Shop.musicIcon == null)
            {
                _music.GetComponentInChildren<Text>().text = on ? "SOUND ON" : "SOUND OFF";
            }
        }
    }
}
