#if UNITY_IOS
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.iOS.Xcode;

/// <summary>
/// iOS post-build (ported from FarmFury_Stampede's StampedeIosBuild, 2026-11-02):
///  - CFBundleDisplayName "Cannonfire" — the name under the home screen icon (productName is left alone; the App
///    Store name is set in App Store Connect).
///  - ITSAppUsesNonExemptEncryption = false, so every TestFlight upload doesn't stop at export compliance.
///  - strips any "-ld64" from OTHER_LDFLAGS (the classic-linker flag an SDK may add; Xcode 26 rejects it — the
///    franchise's Xcode 26 gotcha, same call as Stampede's).
/// Not added yet: GADApplicationIdentifier (FarmFury has no AdMob adapter until its own AdMob app exists — add the
/// id here when it does, or the AdMob SDK crashes at launch) and the privacy manifest Stampede ships.
/// </summary>
public class FarmFuryIosBuild : IPostprocessBuildWithReport
{
    public const string HomeScreenName = "Cannonfire";   // franchise: the subtitle only, like "Stampede" / "Arcade" (2026-11-10)

    public int callbackOrder => 100;

    public void OnPostprocessBuild(BuildReport report)
    {
        if (report.summary.platform != BuildTarget.iOS) return;
        string outputPath = report.summary.outputPath;

        string projectPath = PBXProject.GetPBXProjectPath(outputPath);
        var project = new PBXProject();
        project.ReadFromFile(projectPath);
        foreach (string target in new[] { project.GetUnityMainTargetGuid(), project.GetUnityFrameworkTargetGuid() })
        {
            project.UpdateBuildProperty(target, "OTHER_LDFLAGS", new string[0], new[] { "-ld64" });
        }
        File.WriteAllText(projectPath, project.WriteToString());

        string plistPath = Path.Combine(outputPath, "Info.plist");
        var plist = new PlistDocument();
        plist.ReadFromFile(plistPath);
        plist.root.SetString("CFBundleDisplayName", HomeScreenName);
        plist.root.SetBoolean("ITSAppUsesNonExemptEncryption", false);
        plist.WriteToFile(plistPath);
    }
}
#endif
