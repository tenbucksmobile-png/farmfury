using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using FarmFury.Core;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Website demo (Meadow Ruins only) build tooling, after Stampede's / Arcade's WebDemoBuilder. See WEB_DEMO_PLAN.md at
/// the repo root. Branch web-demo only.
///
/// Unity only ships an asset the build's scene, a prefab it references or a Resources folder points at, so the other
/// eight worlds are kept out of the download by removing references, not by deleting files:
///  - GameManager's level list keeps Meadow Ruins' levels only;
///  - the IAP / Ads / Analytics components are removed from the scene;
///  - every reference (scene components, prefabs, the other worlds' WorldData menu art except the World Select badge)
///    to an asset whose path is another world's (<see cref="OtherWorldArt"/>) or a cosmetic is cleared.
/// Re-running Wire Scene References / Generate All Level Data puts them back: run "Trim To Meadow Ruins" again after
/// (Build Website does it anyway).
/// </summary>
public static class WebDemoBuilder
{
    private const string ScenePath = "Assets/Scenes/Game.unity";
    private const string OutputPath = "Builds/Website";

    // Asset paths that belong to Worlds 2-9 (or to cosmetics, which the demo doesn't sell). Case-insensitive.
    private static readonly Regex OtherWorldArt = new(
        @"/Sprites/Environment/(Canyon|Factory|Fair|Frozen|Mothership|Sky|Sunken|Watermill|World[2-9]Props)/" +
        @"|/Sprites/Environment/Skies/World[2-9]_" +
        @"|/Launchers/(Circus|Gravity|Ice|Mill|Pneumatic|Prospector|Sea|Sky)Cannon" +
        @"|/Enemies/Robot/W[2-9]/" +
        @"|/Enemies/Robot/(DeepDredger|DrillRig|FactoryMachine|Frost|Glacier|HarvesterPrime|IceHarvestor|MillMauler|Ringmaster|StormBaron)" +
        @"|/Prefabs/Enemies/(W[2-9]_|DeepDredger|DrillRig|FactoryMachine|Frost|Glacier|HarvesterPrime|IceHarvestor|MillMauler|Ringmaster|StormBaron)" +
        @"|/Audio/Worlds/" +
        @"|/Franchise/Canvas/(CF|DC|FT|HF|RM|SC|SI|WV)_" +
        @"|/Franchise/LevelSelect/(?!MeadowRuins)" +
        @"|/Sprites/UI/Matchup_W2/" +
        @"|/Sprites/Cosmetics/|/Franchise/Cosmetics/" +
        @"|_W[2-9]\.png$" +
        // Shop-only art (the demo sells nothing): cosmetic tiles and price cards, coin packs, shop / locker icons.
        @"|/Franchise/Shop/(Impact|Cannon|Trail|SparkleDust|CornHusk|Confetti|Ember|RainbowRibbon|Ribbon|Bubble|bubbles" +
        @"|100\.|500\.|5000\.|15000\.|Cosmetics_Icon|WorldMaze|Locker|PurchaseCardFrame|Ads\.|Shop\.png|Btn_LeaderBoard|Promo_)",
        RegexOptions.IgnoreCase);

    public static bool IsOtherWorldArt(Object asset)
    {
        if (asset == null) { return false; }
        string path = AssetDatabase.GetAssetPath(asset);
        return !string.IsNullOrEmpty(path) && OtherWorldArt.IsMatch("/" + path);
    }

    [MenuItem("FarmFury/Web Demo/Trim To Meadow Ruins")]
    public static void TrimToDemoContent()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath);
        int levels = 0, managers = 0, sceneRefs = 0, prefabRefs = 0, worldArt = 0;

        // Meadow Ruins' levels only.
        var gm = Object.FindAnyObjectByType<GameManager>(FindObjectsInactive.Include);
        if (gm != null)
        {
            var so = new SerializedObject(gm);
            var list = so.FindProperty("_levels");
            for (int i = list.arraySize - 1; i >= 0; i--)
            {
                var element = list.GetArrayElementAtIndex(i);
                if (element.objectReferenceValue is LevelData l && WebDemo.IsPlayable(l.WorldType)) { continue; }
                element.objectReferenceValue = null;
                list.DeleteArrayElementAtIndex(i);
                levels++;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        else
        {
            Debug.LogError("[WebDemoBuilder] No GameManager in the scene.");
        }

        // No store, ads or analytics.
        foreach (var type in new[] { typeof(IAPManager), typeof(FarmFury.Shared.Monetisation.AdManager),
                     typeof(FarmFury.Shared.Monetisation.AnalyticsManager) })
        {
            foreach (var c in Object.FindObjectsByType(type, FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                Object.DestroyImmediate(c);
                managers++;
            }
        }

        // Other worlds' references in the scene.
        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var c in root.GetComponentsInChildren<Component>(true))
            {
                if (c != null) { sceneRefs += ClearOtherWorldRefs(c); }
            }
        }

        // ... and in every prefab the demo can still reach.
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (OtherWorldArt.IsMatch("/" + path)) { continue; }
            var root = PrefabUtility.LoadPrefabContents(path);
            int n = root.GetComponentsInChildren<Component>(true).Where(c => c != null).Sum(ClearOtherWorldRefs);
            if (n > 0) { PrefabUtility.SaveAsPrefabAsset(root, path); prefabRefs += n; }
            PrefabUtility.UnloadPrefabContents(root);
        }

        // The other worlds stay on World Select as badges (the Full Game panel shows them too): only their badge stays.
        foreach (string guid in AssetDatabase.FindAssets("t:WorldData"))
        {
            var world = AssetDatabase.LoadAssetAtPath<WorldData>(AssetDatabase.GUIDToAssetPath(guid));
            if (world == null || WebDemo.IsPlayable(world.worldType)) { continue; }
            var so = new SerializedObject(world);
            var it = so.GetIterator();
            bool changed = false;
            while (it.Next(true))
            {
                if (it.propertyType == SerializedPropertyType.ObjectReference && it.objectReferenceValue != null
                    && it.name != "selectBadge" && it.name != "m_Script")
                {
                    it.objectReferenceValue = null;
                    changed = true;
                }
            }
            if (changed) { so.ApplyModifiedPropertiesWithoutUndo(); worldArt++; }
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log($"[WebDemoBuilder] Trimmed to Meadow Ruins: removed {levels} level(s), {managers} store/ads/analytics " +
                  $"component(s); cleared {sceneRefs} scene and {prefabRefs} prefab reference(s) to other worlds' art; " +
                  $"cleared the menu art of {worldArt} world(s).");
    }

    // Nulls every object reference on the component (arrays and nested serializable classes too) that points at
    // another world's art. Returns how many it cleared.
    private static int ClearOtherWorldRefs(Component c)
    {
        var so = new SerializedObject(c);
        var it = so.GetIterator();
        int n = 0;
        while (it.Next(true))
        {
            if (it.propertyType == SerializedPropertyType.ObjectReference && IsOtherWorldArt(it.objectReferenceValue))
            {
                it.objectReferenceValue = null;
                n++;
            }
        }
        if (n > 0) { so.ApplyModifiedPropertiesWithoutUndo(); }
        return n;
    }

    /// <summary>Website build into Builds/Website, for
    /// farmfurygames.com/play/cannonfire/: gzip with Unity's decompression fallback (the host needs no headers).</summary>
    [MenuItem("FarmFury/Web Demo/Build Website")]
    public static void BuildWebsite()
    {
        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL)
        {
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.WebGL, BuildTarget.WebGL);
        }

        TrimToDemoContent();
        ApplyWebSizeSettings();

        PlayerSettings.productName = "Farm Fury Cannonfire";
        PlayerSettings.companyName = "Tenbucks Mobile";
        PlayerSettings.WebGL.template = "PROJECT:CannonfireEmbed";
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
        PlayerSettings.WebGL.decompressionFallback = true;
        PlayerSettings.WebGL.nameFilesAsHashes = false;   // true loops "Backend has requested a buildprogram run 6 times" on 6000.5.6f1 (Stampede)
        PlayerSettings.runInBackground = false;

        if (Directory.Exists(OutputPath)) { Directory.Delete(OutputPath, true); }
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { ScenePath },
            locationPathName = OutputPath,
            target = BuildTarget.WebGL,
            options = BuildOptions.None,
        });
        var summary = report.summary;
        if (summary.result == BuildResult.Succeeded)
        {
            StampVersion();
            foreach (string dir in Directory.GetDirectories(OutputPath, "*_DoNotShip")) { Directory.Delete(dir, true); }
        }
        Debug.Log($"[WebDemoBuilder] Build {summary.result} -> {OutputPath}: {summary.totalSize / (1024f * 1024f):F1} MB, " +
                  $"{summary.totalErrors} error(s), {summary.totalTime}.");
        if (summary.result != BuildResult.Succeeded && Application.isBatchMode)
        {
            EditorApplication.Exit(1);
        }
    }

    // Fixed file names + index.html asking for them with ?v=<build time>: the host caches files for a day, and a new
    // query string makes browsers fetch a new upload (hashed names loop the 6000.5.6f1 WebGL build).
    private static void StampVersion()
    {
        string index = Path.Combine(OutputPath, "index.html");
        string version = System.DateTime.UtcNow.ToString("yyyyMMddHHmm");
        string html = File.ReadAllText(index);
        html = Regex.Replace(html, @"(Build/[^""?]+\.(?:js|unityweb|wasm|data))""", $"$1?v={version}\"");
        File.WriteAllText(index, html);
        Debug.Log($"[WebDemoBuilder] index.html file links stamped ?v={version}.");
    }

    // ---- download size (Arcade's / Stampede's numbers) ------------------------------------------------------------

    // Above this source size (longest side, px) a texture is a full-screen backdrop or strip and keeps up to 2048.
    private const int BackdropThreshold = 1000;
    private const int BackdropMaxSize = 2048;
    private const int SpriteMaxSize = 512;
    // Animals and robots are drawn small on the framed levels (an animal is ~5% of the screen height): 256 is plenty.
    private const int ActorMaxSize = 256;
    private const long MusicBytesThreshold = 300 * 1024;

    private static bool IsActor(string path) => path.Contains("/Sprites/Characters/") || path.Contains("/Sprites/Enemies/");

    // DXT only compresses textures whose (downscaled) sides are multiples of 4; the rest use ASTC (Unity would
    // otherwise ship them uncompressed). Browsers decode on the CPU where the GPU lacks the format.
    private static TextureImporterFormat ChooseWebFormat(int w, int h, int maxSize, bool isBackdrop)
    {
        float scale = Mathf.Max(w, h) > maxSize ? (float)maxSize / Mathf.Max(w, h) : 1f;
        int rw = Mathf.Max(1, Mathf.RoundToInt(w * scale));
        int rh = Mathf.Max(1, Mathf.RoundToInt(h * scale));
        if (rw % 4 == 0 && rh % 4 == 0) { return TextureImporterFormat.DXT5Crunched; }
        return isBackdrop ? TextureImporterFormat.ASTC_8x8 : TextureImporterFormat.ASTC_6x6;
    }

    /// <summary>WebGL-only texture/audio overrides and player settings; never touches the mobile platforms. Safe to re-run.</summary>
    [MenuItem("FarmFury/Web Demo/Apply Web Size Settings")]
    public static void ApplyWebSizeSettings()
    {
        int textures = 0, audio = 0;

        AssetDatabase.StartAssetEditing();
        try
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Sprites" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetImporter.GetAtPath(path) is not TextureImporter importer) { continue; }
                importer.GetSourceTextureWidthAndHeight(out int w, out int h);
                bool isBackdrop = Mathf.Max(w, h) > BackdropThreshold && !IsActor(path);
                int maxSize = isBackdrop ? BackdropMaxSize : IsActor(path) ? ActorMaxSize : SpriteMaxSize;
                var format = ChooseWebFormat(w, h, maxSize, isBackdrop);

                var settings = importer.GetPlatformTextureSettings("WebGL");
                if (settings.overridden && settings.maxTextureSize == maxSize && settings.format == format && settings.compressionQuality == 50)
                {
                    continue;
                }
                settings.overridden = true;
                settings.maxTextureSize = maxSize;
                settings.format = format;
                settings.crunchedCompression = format == TextureImporterFormat.DXT5Crunched;
                settings.compressionQuality = 50;
                importer.SetPlatformTextureSettings(settings);
                importer.SaveAndReimport();
                textures++;
            }

            foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets/Audio" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetImporter.GetAtPath(path) is not AudioImporter importer) { continue; }
                bool isLong = new FileInfo(path).Length > MusicBytesThreshold;
                var current = importer.GetOverrideSampleSettings("WebGL");
                var wanted = current;
                wanted.compressionFormat = AudioCompressionFormat.AAC;
                wanted.quality = isLong ? 0.4f : 0.6f;
                wanted.loadType = isLong ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad;
                if (importer.ContainsSampleSettingsOverride("WebGL") && current.compressionFormat == wanted.compressionFormat
                    && Mathf.Approximately(current.quality, wanted.quality) && current.loadType == wanted.loadType
                    && importer.loadInBackground == isLong)
                {
                    continue;
                }
                importer.SetOverrideSampleSettings("WebGL", wanted);
                importer.loadInBackground = isLong;
                importer.SaveAndReimport();
                audio++;
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
        }

        var target = NamedBuildTarget.WebGL;
        PlayerSettings.SplashScreen.show = false;
        PlayerSettings.SplashScreen.showUnityLogo = false;
        PlayerSettings.SetManagedStrippingLevel(target, ManagedStrippingLevel.High);
        PlayerSettings.SetIl2CppCodeGeneration(target, Il2CppCodeGeneration.OptimizeSize);
        PlayerSettings.stripEngineCode = true;
        PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;

        AssetDatabase.SaveAssets();
        Debug.Log($"[WebDemoBuilder] Web size settings: {textures} texture(s) and {audio} audio clip(s) updated.");
    }
}
