#if UNITY_IOS
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.iOS.Xcode;
using UnityEngine;

/// <summary>
/// iOS post-build (ported from FarmFury_Stampede's StampedeIosBuild, 2026-11-02; AdMob + privacy manifest 2026-11-13):
///  - CFBundleDisplayName "Cannonfire" — the name under the home screen icon (productName is left alone; the App
///    Store name is set in App Store Connect).
///  - ITSAppUsesNonExemptEncryption = false, so every TestFlight upload doesn't stop at export compliance.
///  - GADApplicationIdentifier, Cannonfire's AdMob iOS App ID: Google Mobile Ads (in the build as LevelPlay's AdMob
///    adapter, LevelPlay/Editor/ISAdMobAdapterDependencies.xml) throws at launch without it, before Unity even starts.
///  - strips any "-ld64" from OTHER_LDFLAGS (the classic-linker flag an SDK may add; Xcode 26 rejects it — the
///    franchise's Xcode 26 gotcha, same call as Stampede's).
///  - adds the app's own privacy manifest (Assets/iOS/PrivacyInfo.xcprivacy, Stampede's: UserDefaults, reason CA92.1,
///    for PlayerPrefs), without which Apple rejects the upload. The SDKs bring their own manifests.
/// </summary>
public class FarmFuryIosBuild : IPostprocessBuildWithReport
{
    public const string HomeScreenName = "Cannonfire";   // franchise: the subtitle only, like "Stampede" / "Arcade" (2026-11-10)

    /// <summary>AdMob App ID for Farm Fury Cannonfire (iOS), from admob.google.com > Apps > App settings (2026-11-13).
    /// Not the LevelPlay app key or the ad unit ids, which live on AdManager in the scene / in the LevelPlay dashboard.
    /// (Android's, ~6913458246, is in Assets/Plugins/Android/AndroidManifest.xml.)</summary>
    private const string AdMobAppId = "ca-app-pub-1264425755955045~9969711472";

    public int callbackOrder => 100;

    public void OnPostprocessBuild(BuildReport report)
    {
        if (report.summary.platform != BuildTarget.iOS) return;
        string outputPath = report.summary.outputPath;

        string projectPath = PBXProject.GetPBXProjectPath(outputPath);
        var project = new PBXProject();
        project.ReadFromFile(projectPath);
        string mainTarget = project.GetUnityMainTargetGuid();
        foreach (string target in new[] { mainTarget, project.GetUnityFrameworkTargetGuid() })
        {
            project.UpdateBuildProperty(target, "OTHER_LDFLAGS", new string[0], new[] { "-ld64" });
        }
        AddPrivacyManifest(project, mainTarget, outputPath);
        File.WriteAllText(projectPath, project.WriteToString());

        string plistPath = Path.Combine(outputPath, "Info.plist");
        var plist = new PlistDocument();
        plist.ReadFromFile(plistPath);
        plist.root.SetString("CFBundleDisplayName", HomeScreenName);
        plist.root.SetBoolean("ITSAppUsesNonExemptEncryption", false);
        plist.root.SetString("GADApplicationIdentifier", AdMobAppId);
        plist.WriteToFile(plistPath);
    }

    static void AddPrivacyManifest(PBXProject project, string mainTarget, string outputPath)
    {
        string source = Path.Combine(Application.dataPath, "iOS", "PrivacyInfo.xcprivacy");
        if (!File.Exists(source))
        {
            Debug.LogWarning("[FarmFuryIosBuild] " + source + " is missing; the build has no app privacy manifest.");
            return;
        }
        const string fileName = "PrivacyInfo.xcprivacy";
        File.Copy(source, Path.Combine(outputPath, fileName), true);
        project.AddFileToBuild(mainTarget, project.AddFile(fileName, fileName, PBXSourceTree.Source));
    }
}
#endif
