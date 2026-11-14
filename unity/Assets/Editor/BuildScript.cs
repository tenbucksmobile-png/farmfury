// FarmFury — Batch-mode build + setup automation.
// All public static methods are callable via:
//   Unity.exe -batchmode -projectPath <path> -executeMethod BuildScript.<Method> -quit

using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class BuildScript
{
    static readonly string ProjectRoot = Path.GetFullPath(
        Path.Combine(Application.dataPath, ".."));

    // ── Entry points ──────────────────────────────────────────────────────────

    // Generates all LevelData ScriptableObjects (same logic as menu item)
    public static void GenerateLevels()
    {
        Log("Generating level data...");
        LevelDataGenerator.GenerateAll();
        AssetDatabase.SaveAssets();
        Log("Level data generation complete.");
    }

    // Wires all Inspector references in Game.unity
    public static void WireScene()
    {
        Log("Wiring scene references...");
        SceneSetup.WireAll();
        Log("Scene wiring complete.");
    }

    // Full Windows build
    public static void BuildWindows()
    {
        GenerateLevels();
        Build(BuildTarget.StandaloneWindows64,
              Path.Combine(ProjectRoot, "Builds", "Windows", "FarmFury.exe"));
    }

    // WebGL build (for itch.io / browser play)
    public static void BuildWebGL()
    {
        GenerateLevels();
        Build(BuildTarget.WebGL,
              Path.Combine(ProjectRoot, "Builds", "WebGL"));
    }

    // Android build
    public static void BuildAndroid()
    {
        GenerateLevels();
        Build(BuildTarget.Android,
              Path.Combine(ProjectRoot, "Builds", "Android", "FarmFury.apk"));
    }

    // iOS Xcode project export (no Mac needed): the same Unity step Build Automation runs before Xcode, so it
    // catches iOS-only compile errors and runs FarmFuryIosBuild's post-process. Uses the committed LevelData as
    // the cloud does (no GenerateLevels). Run-Unity.ps1 passes -buildTarget iOS; switch back with -buildTarget Android.
    public static void BuildIOS()
    {
        Build(BuildTarget.iOS, Path.Combine(ProjectRoot, "Builds", "iOS"));
    }

    // Development APK for the video review pipeline (tools/review): keeps HighlightMarkers' log lines,
    // which are compiled out of release builds. Install with `adb install -r`.
    public static void BuildAndroidDev()
    {
        GenerateLevels();
        Build(BuildTarget.Android,
              Path.Combine(ProjectRoot, "Builds", "Android", "FarmFury_dev.apk"),
              BuildOptions.Development);
    }

    // Compile check only — exits with code 1 on error
    public static void CompileCheck()
    {
        Log("Compile check passed — no errors.");
    }

    // ── Core build helper ─────────────────────────────────────────────────────

    static void Build(BuildTarget target, string outputPath, BuildOptions options = BuildOptions.None)
    {
        Log($"Building {target} → {outputPath}");

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes           = GetScenes(),
            locationPathName = outputPath,
            target           = target,
            options          = options,
        });

        if (report.summary.result == BuildResult.Succeeded)
        {
            Log($"Build succeeded  ({report.summary.totalSize / 1024 / 1024} MB)");
        }
        else
        {
            LogError($"Build FAILED: {report.summary.result}");
            EditorApplication.Exit(1);
        }
    }

    static string[] GetScenes()
    {
        // Returns scenes in Build Settings order; fall back to explicit list if none set
        var settingsScenes = EditorBuildSettings.scenes;
        if (settingsScenes.Length > 0)
        {
            var paths = new System.Collections.Generic.List<string>();
            foreach (var s in settingsScenes)
                if (s.enabled) paths.Add(s.path);
            if (paths.Count > 0) return paths.ToArray();
        }

        // Fallback: known scene paths
        return new[]
        {
            "Assets/Scenes/Bootstrap.unity",
            "Assets/Scenes/MainMenu.unity",
            "Assets/Scenes/Game.unity",
        };
    }

    static void Log(string msg)      => Debug.Log($"[FarmFury Build] {msg}");
    static void LogError(string msg) => Debug.LogError($"[FarmFury Build] {msg}");
}
