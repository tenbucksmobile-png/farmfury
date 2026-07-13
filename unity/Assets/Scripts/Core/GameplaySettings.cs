using UnityEngine;

// Persisted preferences for the Settings > Gameplay tab (2026-07-13). Same static-property +
// PlayerPrefs pattern as AudioManager, but these two have no runtime consumers anywhere else in
// the project yet:
//   - Language: no localization framework exists anywhere in this codebase — every string is
//     hardcoded English. Selecting a non-English option here has no visible effect on any game
//     text yet; it only persists the player's preference for whenever real localization exists.
//   - LeftHanded: no mirroring logic exists yet (camera/aim-math/physics are not mirrored by this
//     flag) — per explicit user decision (2026-07-13), this pass only persists the preference.
//     The real horizontal-mirror implementation (CatapultLauncher's drag-angle math, camera
//     positioning, launch trajectory) is a separate, bigger engineering pass.
public static class GameplaySettings
{
    public static readonly string[] SupportedLanguages =
    {
        "English", "Spanish", "French", "German", "Portuguese", "Japanese"
    };

    public static string Language   { get; private set; } = "English";
    public static bool   LeftHanded { get; private set; } = false;

    static GameplaySettings()
    {
        Language   = PlayerPrefs.GetString("ff_language", "English");
        LeftHanded = PlayerPrefs.GetInt("ff_left_handed", 0) == 1;
    }

    public static void SetLanguage(string value)
    {
        Language = value;
        PlayerPrefs.SetString("ff_language", value);
        PlayerPrefs.Save();
    }

    public static void SetLeftHanded(bool value)
    {
        LeftHanded = value;
        PlayerPrefs.SetInt("ff_left_handed", value ? 1 : 0);
        PlayerPrefs.Save();
    }
}
