using System;
using System.IO;
using FarmFury.Core;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Store build settings, ported from FarmFury_Stampede's StampedeBuildSettings (2026-11-15).
/// Pins what both stores require (Android: IL2CPP + ARM64, Play only takes 64-bit; iOS: IL2CPP) on every editor load
/// and before each build, and warns about an Android release build signed with the debug key.
/// <see cref="SetUpGooglePlay"/> switches the project to a Google Play AAB: App Bundle, no development build, public
/// symbols, LZ4HC, Split Application Binary (the art goes into an install-time asset pack; the base module may be at
/// most 200 MB) with Boot.unity as the first scene (<see cref="BootLoader"/>).
/// </summary>
[InitializeOnLoad]
public class CannonfireBuildSettings : IPreprocessBuildWithReport
{
    public const string BootScenePath = "Assets/Scenes/Boot.unity";
    public const string GameScenePath = "Assets/Scenes/Game.unity";

    // Cannonfire's upload key (outside the repo, never commit it). The passwords are never stored: Unity asks for
    // them after each restart, and batch builds read them from FF_KEYSTORE_PASS / FF_KEYALIAS_PASS.
    public const string KeystorePath = @"C:\Users\Personel\Desktop\FarmFury_Technical\Keystore\cannonfire.keystore";
    public const string KeyAlias = "cannonfire";

    static CannonfireBuildSettings()
    {
        EditorApplication.delayCall += Apply;   // after the editor has finished loading the project
    }

    public int callbackOrder => -100;

    public static void Apply()
    {
        if (PlayerSettings.GetScriptingBackend(NamedBuildTarget.Android) != ScriptingImplementation.IL2CPP)
        {
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            Debug.Log("[BuildSettings] Android scripting backend set to IL2CPP.");
        }
        if ((PlayerSettings.Android.targetArchitectures & AndroidArchitecture.ARM64) == 0)
        {
            PlayerSettings.Android.targetArchitectures |= AndroidArchitecture.ARM64;
            Debug.Log("[BuildSettings] Android ARM64 target added.");
        }
        if (PlayerSettings.GetScriptingBackend(NamedBuildTarget.iOS) != ScriptingImplementation.IL2CPP)
        {
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.iOS, ScriptingImplementation.IL2CPP);
        }
    }

    [MenuItem("FarmFury/Android/Set Up For Google Play AAB")]
    public static void SetUpGooglePlay()
    {
        EditorUserBuildSettings.buildAppBundle = true;
        EditorUserBuildSettings.development = false;
        EditorUserBuildSettings.androidCreateSymbols = AndroidCreateSymbols.Public;
        EditorUserBuildSettings.SetPlatformSettings("Android", "CompressionType", "LZ4HC");
        PlayerSettings.Android.splitApplicationBinary = true;
        EnsureBootScene();
        UseUploadKeyIfPresent();
        AssetDatabase.SaveAssets();
        Debug.Log("[BuildSettings] Android set up for a Google Play AAB. Check the keystore in Player Settings > " +
                  "Publishing Settings, then build (File > Build Profiles, or Run-Unity.ps1 build-aab).");
    }

    /// <summary>APK side-loading builds (build-android / build-android-dev): one file, no asset pack.</summary>
    public static void SetUpApk()
    {
        EditorUserBuildSettings.buildAppBundle = false;
        PlayerSettings.Android.splitApplicationBinary = false;
    }

    // Boot.unity first, then Game.unity: a black camera and the loader, nothing else.
    public static void EnsureBootScene()
    {
        if (!File.Exists(BootScenePath))
        {
            var previous = SceneManager.GetActiveScene().path;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camera = new GameObject("Camera").AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.orthographic = true;
            new GameObject("BootLoader").AddComponent<BootLoader>();
            EditorSceneManager.SaveScene(scene, BootScenePath);
            if (!string.IsNullOrEmpty(previous)) { EditorSceneManager.OpenScene(previous); }
            Debug.Log("[BuildSettings] Created " + BootScenePath);
        }
        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene(BootScenePath, true),
            new EditorBuildSettingsScene(GameScenePath, true),
        };
    }

    // Points Publishing Settings at cannonfire.keystore once it exists; passwords from the environment if given.
    static void UseUploadKeyIfPresent()
    {
        if (!File.Exists(KeystorePath))
        {
            Debug.LogWarning("[BuildSettings] " + KeystorePath + " not found: the AAB will be debug-signed (fine for a " +
                             "local test, rejected by Google Play).");
            return;
        }
        PlayerSettings.Android.useCustomKeystore = true;
        PlayerSettings.Android.keystoreName = KeystorePath;
        PlayerSettings.Android.keyaliasName = KeyAlias;
        string storePass = Environment.GetEnvironmentVariable("FF_KEYSTORE_PASS");
        string aliasPass = Environment.GetEnvironmentVariable("FF_KEYALIAS_PASS");
        if (!string.IsNullOrEmpty(storePass)) { PlayerSettings.Android.keystorePass = storePass; }
        if (!string.IsNullOrEmpty(aliasPass)) { PlayerSettings.Android.keyaliasPass = aliasPass; }
        else if (!string.IsNullOrEmpty(storePass)) { PlayerSettings.Android.keyaliasPass = storePass; }
    }

    public void OnPreprocessBuild(BuildReport report)
    {
        Apply();
        bool development = (report.summary.options & BuildOptions.Development) != 0;
        if (report.summary.platform == BuildTarget.Android && !development && !PlayerSettings.Android.useCustomKeystore)
        {
            Debug.LogWarning("[BuildSettings] Android release build without a custom keystore: it is signed with the " +
                             "debug key, which Google Play rejects. Set one in Player Settings > Publishing Settings.");
        }
    }
}
