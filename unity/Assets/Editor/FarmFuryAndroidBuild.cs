#if UNITY_ANDROID
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor.Android;
using UnityEngine;

/// <summary>
/// Sets the name under the Android home screen icon to "Cannonfire" (2026-11-10: the game is Farm Fury Cannonfire;
/// franchise convention is the subtitle only under the icon — Arcade shows "Arcade", Stampede "Stampede"). Only the
/// label changes here; productName is "Farm Fury Cannonfire".
/// Ported from FarmFury_Stampede's StampedeAndroidBuild.
/// </summary>
public class FarmFuryAndroidBuild : IPostGenerateGradleAndroidProject
{
    public const string HomeScreenName = "Cannonfire";   // franchise: the subtitle only, like "Stampede" / "Arcade" (2026-11-10)

    public int callbackOrder => 100;

    /// <param name="path">The generated unityLibrary folder; the launcher module sits beside it.</param>
    public void OnPostGenerateGradleAndroidProject(string path)
    {
        string strings = Path.Combine(path, "..", "launcher", "src", "main", "res", "values", "strings.xml");
        if (!File.Exists(strings))
        {
            Debug.LogError("[FarmFuryAndroidBuild] " + strings + " is missing; the home screen name was not set.");
            return;
        }

        string xml = File.ReadAllText(strings);
        string updated = Regex.Replace(xml, "(<string name=\"app_name\">).*?(</string>)", "${1}" + HomeScreenName + "${2}");
        if (updated == xml && !xml.Contains(">" + HomeScreenName + "<"))
        {
            Debug.LogError("[FarmFuryAndroidBuild] No app_name in " + strings + "; the home screen name was not set.");
            return;
        }
        File.WriteAllText(strings, updated);
    }
}
#endif
