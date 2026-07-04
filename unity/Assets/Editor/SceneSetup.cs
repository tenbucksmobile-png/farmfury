// FarmFury - Wires all Inspector references in Game.unity automatically.
// Menu: FarmFury > Wire Scene References
// Batch: BuildScript.WireScene

using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class SceneSetup
{
    [MenuItem("FarmFury/Wire Scene References")]
    public static void WireAll()
    {
        // Deselect everything before EnsureGround() destroys scene GOs — prevents
        // SerializedObjectNotCreatableException in TransformInspector.OnEnable().
        UnityEditor.Selection.activeObject = null;

        var scene = EditorSceneManager.OpenScene("Assets/Scenes/Game.unity", OpenSceneMode.Single);

        EnsureParents();        // BlockParent, RobotParent GameObjects
        EnsureGround();         // Static ground plane with collider + renderer
        EnsureBackground();     // Sky painting backdrop behind all game objects
        EnsureScenery();        // SceneryBuilder GO + World1Props sprite refs
        EnsureEggPrefab();      // Create Egg prefab + wire into CluckAnimal prefab
        EnsureAnimalPrefabs();  // Create Percy/Woolly/Ducky/Horace/Gerald/Billy prefabs
        EnsureHUD();            // HUDController GO (builds Canvas at runtime)
        EnsureLevelSelect();    // LevelSelectController GO (Canvas sortingOrder 300)
        EnsureMainMenu();       // MainMenuController GO (Canvas sortingOrder 400)
        EnsureWorldMap();       // WorldMapController GO (Sunrise Meadows level-map, replaces LevelSelectController for PLAY)
        EnsureAudioManager();   // AudioManager GO wired with music/cannon/falling clips
        WireGameManager();      // _levels array
        WireLevelLoader();      // 8 animal prefab refs + block/robot + 2 parent transforms
        WireLauncher();         // CatapultLauncher + LevelLoader ref + counterweight sprite
        WireRobotSprite();                // Robot_Idle.png → Robot prefab SpriteRenderer
        EnsureHarvesterRobotPrefab();  // Create/update HarvesterRobot.prefab (separate from Robot)
        EnsureHaybaleBlockPrefab();    // Create/update HaybaleBlock.prefab (WoodBlock + Haybail.png art)
        WireBlockSprites();     // Art sprites into WoodBlock + StoneBlock prefabs
        PositionCamera();       // Move camera to see the play area
        SpriteWiring.WireAll(); // Wire character pose sprites into all 8 animal prefabs

        // Delete visual-only placeholder GOs that duplicate code-spawned gameplay objects.
        // These are scene GOs pasted in for reference during layout — they must be gone
        // before Play mode so the code-spawned prefabs are the only instances.
        foreach (var placeholderName in new[] { "Cluck_Loaded_0", "HarvesterRobot" })
        {
            var ph = GameObject.Find(placeholderName);
            if (ph != null)
            {
                Object.DestroyImmediate(ph);
                Debug.Log($"[FarmFury] Deleted visual placeholder '{placeholderName}'.");
            }
        }

        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[FarmFury] Scene wiring complete. Game.unity saved.");
    }

    // ── Scenery: World 1 decorative prop sprites ──────────────────────────────────

    static void EnsureScenery()
    {
        var go = GameObject.Find("Scenery");
        if (go == null)
        {
            go = new GameObject("Scenery");
            Debug.Log("[FarmFury] Created 'Scenery' GameObject.");
        }
        var sb = go.GetComponent<SceneryBuilder>();
        if (sb == null) sb = go.AddComponent<SceneryBuilder>();

        const string propsFolder = "Assets/Sprites/Environment/World1Props";

        var so = new SerializedObject(sb);
        WireProp(so, "_sprGrassTuft",     "GrassTuft.png",     propsFolder);
        WireProp(so, "_sprWildFlowers",   "WildFlowers.png",   propsFolder);
        WireProp(so, "_sprRock",          "Rock.png",          propsFolder);
        WireProp(so, "_sprWoodenFence",   "WoodenFence.png",   propsFolder);
        WireProp(so, "_sprHaybail",       "Haybail.png",       propsFolder);
        WireProp(so, "_sprWoodenBarrel",  "WoodenBarrel.png",  propsFolder);
        WireProp(so, "_sprWoodenCart",    "WoodenCart.png",    propsFolder);
        // FarmSilo intentionally not wired — excluded from all World 1 level designs
        WireProp(so, "_sprWindmill",         "Windmill.png",         propsFolder); // LEVEL1_EXACT
        WireProp(so, "_sprOakTree",          "OakTree.png",          propsFolder);
        WireProp(so, "_sprGnarledTree",      "GnarledTree.png",      propsFolder);
        // Ruins & barns (Phase 4 additions)
        WireProp(so, "_sprRuinedStoneWall",  "RuinedStoneWall.png",  propsFolder);
        WireProp(so, "_sprStoneTower",       "StoneTower.png",       propsFolder); // FIXED: StoneWall_Tall.png renamed to StoneTower.png, lives in World1Props
        // _sprStoneWallTall intentionally not wired — StoneWall_Tall.png was renamed to StoneTower.png above
        WireProp(so, "_sprOldBarn",          "OldBarn_Right.png",    propsFolder); // FIXED: actual filename
        WireProp(so, "_sprDamagedBarn",      "DamagedBarn.png",      propsFolder);
        // Level 1 scenery is hand-authored as scene GOs — skip all code-spawning for level 0
        so.FindProperty("_useExactPlacement").boolValue = true;
        so.ApplyModifiedProperties();
        Debug.Log("[FarmFury] Scenery: _useExactPlacement = true (Level 1 props are scene-authored, not code-generated).");
    }

    static void WireProp(SerializedObject so, string field, string filename, string folder)
    {
        string path = $"{folder}/{filename}";
        // Force PPU=512 / Single mode on import so bottom-anchor maths in SceneryBuilder is correct.
        var imp = AssetImporter.GetAtPath(path) as TextureImporter;
        if (imp != null)
        {
            bool dirty = false;
            if (imp.textureType         != TextureImporterType.Sprite)          { imp.textureType         = TextureImporterType.Sprite;          dirty = true; }
            if (imp.spritePixelsPerUnit != 512)                                  { imp.spritePixelsPerUnit = 512;                                 dirty = true; }
            if (!imp.alphaIsTransparency)                                        { imp.alphaIsTransparency = true;                                dirty = true; }
            if (imp.alphaSource         != TextureImporterAlphaSource.FromInput) { imp.alphaSource         = TextureImporterAlphaSource.FromInput; dirty = true; }
            if (imp.spriteImportMode    != SpriteImportMode.Single)              { imp.spriteImportMode    = SpriteImportMode.Single;             dirty = true; }
            if (dirty) imp.SaveAndReimport();
        }

        var sp = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sp == null)
        {
            Debug.LogWarning($"[FarmFury] Scenery: prop not found at {path}");
            return;
        }
        so.FindProperty(field).objectReferenceValue = sp;
        Debug.Log($"[FarmFury] Scenery: {field} → {filename}");
    }

    // ── Sky backdrop ─────────────────────────────────────────────────────────────

    static void EnsureBackground()
    {
        // Prefer the user's authored sky GO; delete any stale code-created duplicate.
        var skyVariant = GameObject.Find("Background_SkyV1")
                      ?? GameObject.Find("Background_Sky");
        var codeBg     = GameObject.Find("Background");

        GameObject go;
        if (skyVariant != null)
        {
            if (codeBg != null && codeBg != skyVariant) Object.DestroyImmediate(codeBg);
            go      = skyVariant;
            go.name = "Background";
            Debug.Log("[FarmFury] Background: merged Background_SkyV1 → 'Background'.");
        }
        else if (codeBg != null)
        {
            go = codeBg;
        }
        else
        {
            go = new GameObject("Background");
            Debug.Log("[FarmFury] Created 'Background' GO — no existing sky sprite found.");
        }

        var bc = go.GetComponent<BackgroundController>();
        if (bc == null) bc = go.AddComponent<BackgroundController>();

        const string skyPath = "Assets/Sprites/Environment/Skies/SkyPainting.png";

        // SkyPainting is 1920×1080 art — ensure it is imported as a Sprite before loading.
        // If it was imported as the default Texture2D, LoadAssetAtPath<Sprite> returns null.
        var skyImp = AssetImporter.GetAtPath(skyPath) as TextureImporter;
        if (skyImp != null)
        {
            bool dirty = false;
            if (skyImp.textureType         != TextureImporterType.Sprite)          { skyImp.textureType         = TextureImporterType.Sprite;          dirty = true; }
            if (skyImp.spritePixelsPerUnit != 100)                                   { skyImp.spritePixelsPerUnit = 100;                                  dirty = true; }
            if (!skyImp.alphaIsTransparency)                                         { skyImp.alphaIsTransparency = true;                                 dirty = true; }
            if (skyImp.spriteImportMode    != SpriteImportMode.Single)              { skyImp.spriteImportMode    = SpriteImportMode.Single;              dirty = true; }
            if (dirty) { skyImp.SaveAndReimport(); Debug.Log("[FarmFury] SkyPainting.png re-imported as Sprite (PPU=100)."); }
        }
        else
            Debug.LogWarning($"[FarmFury] SkyPainting.png not found at {skyPath} — copy it there and re-run Wire Scene References.");

        var skySprite = AssetDatabase.LoadAssetAtPath<Sprite>(skyPath);
        if (skySprite != null)
        {
            var so = new SerializedObject(bc);
            so.FindProperty("_skySprite").objectReferenceValue = skySprite;
            so.ApplyModifiedProperties();
            Debug.Log("[FarmFury] Background: SkyPainting wired.");
        }
        else
        {
            Debug.LogWarning($"[FarmFury] SkyPainting.png not found at {skyPath}. " +
                             "Copy assets/Backdrops/SkyPainting.png there and re-run Wire Scene References.");
        }
    }

    // ── New animal prefabs (Percy, Woolly, Ducky, Horace, Gerald, Billy) ────────

    static void EnsureAnimalPrefabs()
    {
        CreateAnimalPrefab("PercyAnimal",  typeof(PercyAnimal));
        CreateAnimalPrefab("WoollyAnimal", typeof(WoollyAnimal));
        CreateAnimalPrefab("DuckyAnimal",  typeof(DuckyAnimal));
        CreateAnimalPrefab("HoraceAnimal", typeof(HoraceAnimal));
        CreateAnimalPrefab("GeraldAnimal", typeof(GeraldAnimal));
        CreateAnimalPrefab("BillyAnimal",  typeof(BillyAnimal));
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    static void CreateAnimalPrefab(string name, System.Type scriptType)
    {
        const string folder = "Assets/Prefabs/Animals";
        string path = $"{folder}/{name}.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
        {
            Debug.Log($"[FarmFury] {name} prefab already exists — skipped.");
            return;
        }

        var go = new GameObject(name);
        go.layer = 7; // Animal layer
        go.AddComponent<Rigidbody2D>();
        go.AddComponent<CircleCollider2D>();
        go.AddComponent<SpriteRenderer>();
        go.AddComponent(scriptType);

        // Ensure destination folder exists
        if (!AssetDatabase.IsValidFolder(folder))
            AssetDatabase.CreateFolder("Assets/Prefabs", "Animals");

        PrefabUtility.SaveAsPrefabAsset(go, path);
        Object.DestroyImmediate(go);
        Debug.Log($"[FarmFury] Created {path}");
    }

    // ── HUD: create GO + attach HUDController + wire card sprites ────────────

    // Filename keywords that uniquely identify each card, indexed by AnimalType.
    static readonly string[] CardKeywords =
    {
        "Cluck",   // 0 Cluck   → Cluck_Chicken.png
        "Bessie",  // 1 Bessie  → Bessie_Cow.png
        "Percy",   // 2 Percy   → Percy_Pig.png
        "Woolly",  // 3 Woolly  → Woolly_Sheep.png
        "Ducky",   // 4 Ducky   → Ducky_Duck.png
        "Horace",  // 5 Horace  → Horace_Horse.png
        "Gerald",  // 6 Gerald  → Gerald_Turkey.png
        "Goat",    // 7 Billy   → Billy_Goat.png
    };

    static void EnsureHUD()
    {
        var go = GameObject.Find("HUD");
        if (go == null)
        {
            go = new GameObject("HUD");
            Debug.Log("[FarmFury] Created 'HUD' GameObject.");
        }
        if (go.GetComponent<HUDController>() == null)
            go.AddComponent<HUDController>();

        // Wire card sprites into _cardSprites[]
        const string cardsFolder = "Assets/Sprites/UI/Cards";
        var hud = go.GetComponent<HUDController>();
        var so  = new SerializedObject(hud);
        var arr = so.FindProperty("_cardSprites");
        arr.arraySize = CardKeywords.Length;

        var guids = AssetDatabase.FindAssets("t:Sprite", new[] { cardsFolder });
        int wired = 0;
        for (int i = 0; i < CardKeywords.Length; i++)
        {
            string kw = CardKeywords[i].ToLower();
            foreach (var g in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(g);
                if (path.ToLower().Contains(kw))
                {
                    var spr = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                    if (spr != null)
                    {
                        arr.GetArrayElementAtIndex(i).objectReferenceValue = spr;
                        wired++;
                        break;
                    }
                }
            }
        }
        // Level Complete / Level Failed panel art — Scoreboard/LevelComplete/LevelFailed/
        // ScoreStars live in the MatchUp folder (already used elsewhere for the pre-match
        // sequence); Btn_play/Btn_home are the shared Icon set also used by
        // MainMenuController/WorldMapController. Scoreboard + the two buttons are shared
        // between both end-of-level panels (same assets, same field).
        WireSprite(so, "_lcTitleSprite",       "Assets/Sprites/UI/MatchUp/LevelComplete.png");
        WireSprite(so, "_starSprite",          "Assets/Sprites/UI/MatchUp/ScoreStars.png");
        WireSprite(so, "_lfTitleSprite",       "Assets/Sprites/UI/MatchUp/LevelFailed.png");
        WireSprite(so, "_scoreboardSprite",    "Assets/Sprites/UI/MatchUp/Scoreboard.png");
        WireSprite(so, "_playButtonSprite",    "Assets/Sprites/UI/Icon/Btn_play.png");
        WireSprite(so, "_homeButtonSprite",    "Assets/Sprites/UI/Icon/Btn_home.png");
        WireSprite(so, "_quitButtonSprite",    "Assets/Sprites/UI/Icon/Btn_quite.png");

        so.ApplyModifiedProperties();

        if (wired > 0)
            Debug.Log($"[FarmFury] HUD: wired {wired}/8 card sprites from {cardsFolder}.");
        else
            Debug.LogWarning($"[FarmFury] HUD: no card sprites found in {cardsFolder}. " +
                             "Copy assets/FarmCards/*.png there first.");
    }

    // ── MainMenuController ────────────────────────────────────────────────────────

    static void EnsureMainMenu()
    {
        var go = GameObject.Find("MainMenu");
        if (go == null)
        {
            go = new GameObject("MainMenu");
            Debug.Log("[FarmFury] Created 'MainMenu' GameObject.");
        }
        if (go.GetComponent<MainMenuController>() == null)
            go.AddComponent<MainMenuController>();

        // Wire landing page sprite (1920×1080 full-scene art, no bg removal needed)
        const string landingPath = "Assets/Sprites/UI/LandingPage.png";
        if (AssetDatabase.LoadAssetAtPath<Texture2D>(landingPath) != null)
        {
            var importer = AssetImporter.GetAtPath(landingPath) as TextureImporter;
            if (importer != null)
            {
                bool changed = false;
                if (importer.textureType != TextureImporterType.Sprite)
                { importer.textureType = TextureImporterType.Sprite; changed = true; }
                if (importer.spritePixelsPerUnit != 100)
                { importer.spritePixelsPerUnit = 100; changed = true; }
                if (changed) importer.SaveAndReimport();
            }
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(landingPath);
            var mc = go.GetComponent<MainMenuController>();
            var so = new SerializedObject(mc);
            so.FindProperty("_landingSprite").objectReferenceValue = sprite;
            so.ApplyModifiedProperties();
            Debug.Log("[FarmFury] MainMenu: landing page sprite wired.");
        }
        else
        {
            Debug.LogWarning("[FarmFury] LandingPage.png not found at Assets/Sprites/UI/LandingPage.png.");
        }

        // Wire PLAY and SETTINGS button sprites (bottom-left/bottom-right corner icons, per the
        // 2026-07-16 LandingPage_New.png mockup). These live at Assets/Sprites/UI/Icon/ and are
        // already imported (Single mode, PPU=100) by SpriteAutoImporter's generic "Sprites/UI/"
        // rule — no reimport step needed, unlike the old Assets/Sprites/UI/Play.png path this
        // replaces (that file never actually existed, so _playButtonSprite silently fell back
        // to a plain orange square at runtime — see MainMenuController's fallback color).
        var mainMenu = go.GetComponent<MainMenuController>();
        var mmSo = new SerializedObject(mainMenu);
        WireSprite(mmSo, "_playButtonSprite",     "Assets/Sprites/UI/Icon/Btn_play.png");
        WireSprite(mmSo, "_settingsButtonSprite", "Assets/Sprites/UI/Icon/Btn_settings.png");
        mmSo.ApplyModifiedProperties();
    }

    // ── WorldMapController (Sunrise Meadows) ──────────────────────────────────
    // Art lives at Assets/Sprites/UI/LevelCards/World1/ — already imported (Single mode,
    // PPU=100) by SpriteAutoImporter's existing "Sprites/UI/" rule, so no reimport step needed
    // here, just AssetDatabase lookups + SerializedObject wiring (same pattern as
    // EnsureMainMenu above). Also wires the MatchUpScreen art fields that live directly on
    // WorldMapController (animal/robot cards, VS graphic, matchup background) — NOT on the
    // nested MatchUpScreen instance itself. That nested object is a child GameObject created
    // inside BuildUI(), which only runs when Awake() actually fires (Play mode, or immediately
    // after a fresh AddComponent) — this batch pass opens the scene without entering Play mode,
    // so GetComponentInChildren<MatchUpScreen>() here would find nothing (confirmed empirically
    // 2026-07-16 — m_Children: [] on WorldMap's Transform in the saved scene even right after a
    // fresh AddComponent<WorldMapController>() in this same method — Awake() genuinely does not
    // fire synchronously in this batch context, unlike normal interactive-Editor AddComponent
    // calls). The old LevelPreviewCard had this exact same silent gap. Keeping the fields on
    // WorldMapController (which DOES persist — proven by every other field below already
    // working) and threading them into MatchUpScreen.Init() at BuildUI() time sidesteps the
    // whole issue: it doesn't matter whether BuildUI() ran during this edit-time pass, only that
    // these fields are correctly saved for the NEXT real Awake() (Play mode / the actual game).
    static void EnsureWorldMap()
    {
        const string artFolder  = "Assets/Sprites/UI/LevelCards/World1";
        const string iconFolder = "Assets/Sprites/UI/Icon";

        var go = GameObject.Find("WorldMap");
        if (go == null)
        {
            go = new GameObject("WorldMap");
            Debug.Log("[FarmFury] Created 'WorldMap' GameObject.");
        }
        if (go.GetComponent<WorldMapController>() == null)
            go.AddComponent<WorldMapController>();

        var mapSo = new SerializedObject(go.GetComponent<WorldMapController>());
        WireSprite(mapSo, "_backgroundSprite",      $"{artFolder}/SunriseMeadows.png");
        WireSprite(mapSo, "_lockedSprite",          $"{artFolder}/LevelMarker_Locked.png");
        WireSprite(mapSo, "_unlockedSprite",        $"{artFolder}/LevelMarker_Unlocked.png");
        WireSprite(mapSo, "_star1Sprite",           $"{artFolder}/LevelMarker_1star.png");
        WireSprite(mapSo, "_star3Sprite",           $"{artFolder}/LevelMarker_3stars.png");
        WireSprite(mapSo, "_playerPositionSprite",  $"{artFolder}/PlayerPosition.png");
        WireSprite(mapSo, "_nextLevelButtonSprite", $"{iconFolder}/Btn_nextlevel.png");
        WireSprite(mapSo, "_homeButtonSprite",      $"{iconFolder}/Btn_home.png");
        // _star2Sprite intentionally left unwired — no LevelMarker_2stars.png exists yet;
        // LevelMarker.Refresh() falls back to the 3-star sprite until one is added.

        WireMatchUpCards(mapSo);

        mapSo.ApplyModifiedProperties();
    }

    // MatchUpScreen art — dedicated folder, distinct from the HUD's Assets/Sprites/UI/Cards/
    // (see CardKeywords above, still used by EnsureHUD). Only Cluck/Bessie (animal) and
    // Basic/Harvester (robot) have art here today, matching what L01-L06 actually use — other
    // AnimalType slots are left null and MatchUpScreen already tolerates that (hides the card
    // image rather than crashing).
    static void WireMatchUpCards(SerializedObject mapSo)
    {
        const string matchUpFolder = "Assets/Sprites/UI/MatchUp";

        WireSprite(mapSo, "_matchUpBackgroundSprite", $"{matchUpFolder}/MatchUpBackground.png");
        WireSprite(mapSo, "_vsSprite",                $"{matchUpFolder}/VS.png");
        WireSprite(mapSo, "_levelHeaderSprite",       $"{matchUpFolder}/LevelHeader1.png");
        WireSprite(mapSo, "_countdown3Sprite",        $"{matchUpFolder}/countdown3.png");
        WireSprite(mapSo, "_countdown2Sprite",        $"{matchUpFolder}/countdown2.png");
        WireSprite(mapSo, "_countdown1Sprite",        $"{matchUpFolder}/countdown1.png");
        WireSprite(mapSo, "_countdownReadySprite",    $"{matchUpFolder}/Countdown_Ready.png");

        WireArrayByKeyword(mapSo, "_animalCardSprites", matchUpFolder, CardKeywords, "animal");

        // Robot cards need exact-filename matching, not substring: "Robot.png" (Basic) has no
        // distinguishing prefix, but "Harvestor_Robot.png"/"Harvestor_Robot1.png" both also
        // contain "robot" — a naive keyword search (like WireArrayByKeyword uses for animals)
        // would ambiguously match all three off either keyword.
        var robotArr = mapSo.FindProperty("_robotCardSprites");
        robotArr.arraySize = 2;
        robotArr.GetArrayElementAtIndex(0).objectReferenceValue = // Basic
            AssetDatabase.LoadAssetAtPath<Sprite>($"{matchUpFolder}/Robot.png");
        // Prefer the newer Harvestor_Robot1.png (revised art, added a day after the original)
        // over Harvestor_Robot.png if both exist.
        Sprite harvester = AssetDatabase.LoadAssetAtPath<Sprite>($"{matchUpFolder}/Harvestor_Robot1.png")
                         ?? AssetDatabase.LoadAssetAtPath<Sprite>($"{matchUpFolder}/Harvestor_Robot.png");
        robotArr.GetArrayElementAtIndex(1).objectReferenceValue = harvester; // Harvester
    }

    // Added 2026-07-24 — a Sunrise Meadows screenshot showed level markers 1-2 rendering as
    // LevelMarker_3stars.png ("3" badge) and marker 3 as LevelMarker_Unlocked.png ("1" badge),
    // which looked like a bug at first glance. It isn't one: WorldMapController.IsUnlocked()/
    // ScoreManager.GetBestStars() were both already correct — that screenshot's PlayerPrefs
    // simply had 2 levels previously completed with 3 stars each from earlier testing sessions,
    // which is exactly what ff_stars_0/ff_stars_1 = 3 should render as. On a genuinely fresh
    // save (no ff_stars_N keys set), level 1 shows LevelMarker_Unlocked.png and every other
    // level shows LevelMarker_Locked.png, per the existing (unchanged) logic. This menu item
    // just clears that leftover test data on demand, standalone from Wire Scene References
    // (which is scene/asset wiring, not runtime save data — bundling this into it would be a
    // surprising side effect of an unrelated action).
    [MenuItem("FarmFury/Reset World Map Progress")]
    public static void ResetWorldMapProgress()
    {
        for (int i = 0; i < WorldMapController.LevelCount; i++)
        {
            PlayerPrefs.DeleteKey($"ff_score_{i}");
            PlayerPrefs.DeleteKey($"ff_stars_{i}");
        }
        PlayerPrefs.Save();
        Debug.Log($"[FarmFury] Cleared ff_score_N/ff_stars_N for all {WorldMapController.LevelCount} levels — " +
                   "world map will show a fresh save (level 1 unlocked, rest locked) next time it opens.");
    }

    static void WireArrayByKeyword(SerializedObject so, string fieldName, string folder, string[] keywords, string label)
    {
        var arr = so.FindProperty(fieldName);
        arr.arraySize = keywords.Length;
        var guids = AssetDatabase.FindAssets("t:Sprite", new[] { folder });
        int wired = 0;
        for (int i = 0; i < keywords.Length; i++)
        {
            string kw = keywords[i].ToLower();
            if (string.IsNullOrEmpty(kw)) continue;
            foreach (var g in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(g);
                if (!path.ToLower().Contains(kw)) continue;
                var spr = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (spr == null) continue;
                arr.GetArrayElementAtIndex(i).objectReferenceValue = spr;
                wired++;
                break;
            }
        }
        Debug.Log($"[FarmFury] WorldMap: wired {wired}/{keywords.Length} {label} card sprites into MatchUpScreen.");
    }

    static void WireSprite(SerializedObject so, string fieldName, string path)
    {
        var spr = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (spr == null)
        {
            Debug.LogWarning($"[FarmFury] Sprite not found at {path} (target field: {fieldName}).");
            return;
        }
        so.FindProperty(fieldName).objectReferenceValue = spr;
        Debug.Log($"[FarmFury] Wired {fieldName} <- {path}");
    }

    // ── AudioManager ──────────────────────────────────────────────────────────
    // Dedicated scene GameObject (not runtime-AddComponent'd like before) so external
    // clips can be serialized into Game.unity and survive into real builds — AssetDatabase
    // only runs here at edit-time to capture the references. AudioManager's own
    // [DefaultExecutionOrder(-90)] guarantees this instance's Awake() wins the singleton
    // race against CatapultLauncher's fallback AddComponent (see CatapultLauncher.Awake()).
    static void EnsureAudioManager()
    {
        var go = GameObject.Find("AudioManager");
        if (go == null)
        {
            go = new GameObject("AudioManager");
            Debug.Log("[FarmFury] Created 'AudioManager' GameObject.");
        }
        if (go.GetComponent<AudioManager>() == null)
            go.AddComponent<AudioManager>();

        WireAudioClip(go, "_musicClip",      "Assets/Audio/SunriseMeadows_Background.mp3");
        WireAudioClip(go, "_cannonShotClip", "Assets/Audio/CannonShot.mp3");
        WireAudioClip(go, "_fallingClip",    "Assets/Audio/Cluck_falling.mp3");
    }

    static void WireAudioClip(GameObject go, string fieldName, string path)
    {
        var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        if (clip == null)
        {
            Debug.LogWarning($"[FarmFury] Audio clip not found at {path}.");
            return;
        }
        var so = new SerializedObject(go.GetComponent<AudioManager>());
        so.FindProperty(fieldName).objectReferenceValue = clip;
        so.ApplyModifiedProperties();
        Debug.Log($"[FarmFury] AudioManager: wired {fieldName} <- {path}");
    }

    // ── LevelSelectController + world card sprite wiring ─────────────────────────

    // World card filenames (case-insensitive keyword, index = world number 0-5).
    static readonly string[] WorldCardKeywords =
    {
        "Meadow",    // 0 — World 1 Meadow Ruins
        "Frozen",    // 1 — World 2 Frozen Tundra
        "Watermill", // 2 — World 3 Watermill Village
        "Sky",       // 3 — World 4 Sky Islands
        "Sunken",    // 4 — World 5 Sunken City
        "Mothership",// 5 — World 6 Robot Mothership
    };

    static void EnsureLevelSelect()
    {
        var go = GameObject.Find("LevelSelect");
        if (go == null)
        {
            go = new GameObject("LevelSelect");
            Debug.Log("[FarmFury] Created 'LevelSelect' GameObject.");
        }
        if (go.GetComponent<LevelSelectController>() == null)
            go.AddComponent<LevelSelectController>();

        // Wire world card sprites into _worldCardSprites[]
        const string cardsFolder = "Assets/Sprites/UI/LevelCards";
        var lsc = go.GetComponent<LevelSelectController>();
        var so  = new SerializedObject(lsc);
        var arr = so.FindProperty("_worldCardSprites");
        arr.arraySize = WorldCardKeywords.Length;

        var guids = AssetDatabase.FindAssets("t:Sprite", new[] { cardsFolder });
        int wired = 0;
        for (int i = 0; i < WorldCardKeywords.Length; i++)
        {
            string kw = WorldCardKeywords[i].ToLower();
            foreach (var g in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(g);
                if (path.ToLower().Contains(kw))
                {
                    var spr = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                    if (spr != null)
                    {
                        arr.GetArrayElementAtIndex(i).objectReferenceValue = spr;
                        wired++;
                        break;
                    }
                }
            }
        }
        so.ApplyModifiedProperties();

        if (wired > 0)
            Debug.Log($"[FarmFury] LevelSelect: wired {wired}/6 world card sprites from {cardsFolder}.");
        else
            Debug.LogWarning($"[FarmFury] LevelSelect: no world card art found in {cardsFolder}. " +
                             "Add Kling AI card art there and re-run Wire Scene References.");
    }

    // ── GameManager: wire the levels array ────────────────────────────────────

    static void WireGameManager()
    {
        var gm = Object.FindAnyObjectByType<GameManager>();
        if (gm == null) { Debug.LogError("[FarmFury] GameManager not found in scene."); return; }

        var levels = AssetDatabase
            .FindAssets("t:LevelData", new[] { "Assets/ScriptableObjects/Levels" })
            .Select(g  => AssetDatabase.GUIDToAssetPath(g))
            .OrderBy(p => p)                                    // L01 < L02 < ... alphabetical
            .Select(p  => AssetDatabase.LoadAssetAtPath<LevelData>(p))
            .Where(l   => l != null)
            .ToArray();

        var so   = new SerializedObject(gm);
        var prop = so.FindProperty("_levels");
        prop.arraySize = levels.Length;
        for (int i = 0; i < levels.Length; i++)
            prop.GetArrayElementAtIndex(i).objectReferenceValue = levels[i];
        so.ApplyModifiedProperties();

        Debug.Log($"[FarmFury] GameManager: wired {levels.Length} LevelData assets.");
    }

    // ── LevelLoader: wire 5 prefabs + 2 parent transforms ────────────────────

    static void WireLevelLoader()
    {
        var ll = Object.FindAnyObjectByType<LevelLoader>();
        if (ll == null) { Debug.LogError("[FarmFury] LevelLoader not found in scene."); return; }

        var so = new SerializedObject(ll);

        SetPrefab(so, "_woodPrefab",   "WoodBlock",    "Assets/Prefabs/Blocks",   typeof(WoodBlock));
        SetPrefab(so, "_stonePrefab",  "StoneBlock",   "Assets/Prefabs/Blocks",   typeof(StoneBlock));
        SetPrefab(so, "_cluckPrefab",  "CluckAnimal",  "Assets/Prefabs/Animals",  typeof(CluckAnimal));
        SetPrefab(so, "_bessiePrefab", "BessieAnimal", "Assets/Prefabs/Animals",  typeof(BessieAnimal));
        SetPrefab(so, "_percyPrefab",  "PercyAnimal",  "Assets/Prefabs/Animals",  typeof(PercyAnimal));
        SetPrefab(so, "_woollyPrefab", "WoollyAnimal", "Assets/Prefabs/Animals",  typeof(WoollyAnimal));
        SetPrefab(so, "_duckyPrefab",  "DuckyAnimal",  "Assets/Prefabs/Animals",  typeof(DuckyAnimal));
        SetPrefab(so, "_horacePrefab", "HoraceAnimal", "Assets/Prefabs/Animals",  typeof(HoraceAnimal));
        SetPrefab(so, "_geraldPrefab", "GeraldAnimal", "Assets/Prefabs/Animals",  typeof(GeraldAnimal));
        SetPrefab(so, "_billyPrefab",  "BillyAnimal",  "Assets/Prefabs/Animals",  typeof(BillyAnimal));
        SetPrefab(so, "_robotPrefab",      "Robot",           "Assets/Prefabs/Enemies", typeof(RobotEnemy));
        SetPrefab(so, "_harvesterPrefab",  "HarvesterRobot",  "Assets/Prefabs/Enemies", typeof(RobotEnemy));
        SetPrefab(so, "_haybalePrefab",    "HaybaleBlock",    "Assets/Prefabs/Blocks",  typeof(WoodBlock));

        so.FindProperty("_blockParent").objectReferenceValue =
            GameObject.Find("BlockParent")?.transform;
        so.FindProperty("_robotParent").objectReferenceValue =
            GameObject.Find("RobotParent")?.transform;

        so.ApplyModifiedProperties();
        Debug.Log("[FarmFury] LevelLoader: all refs wired.");
    }

    // ── CatapultLauncher: create if missing, wire LevelLoader ref ────────────

    static void WireLauncher()
    {
        var go = GameObject.Find("Launcher");
        if (go == null)
        {
            go = new GameObject("Launcher");
            Debug.Log("[FarmFury] Created 'Launcher' GameObject.");
        }
        // Launcher itself is just an aiming-math anchor (PivotPos() = this position + pivotHeight
        // 1.914) — unrelated to the visual launcher. Unchanged by the trebuchet->cannon swap.
        go.transform.position = new Vector3(-2.327f, -6.60f, 0f);

        var launcher = go.GetComponent<CatapultLauncher>();
        if (launcher == null) launcher = go.AddComponent<CatapultLauncher>();

        var so = new SerializedObject(launcher);
        var ll = Object.FindAnyObjectByType<LevelLoader>();
        so.FindProperty("_levelLoader").objectReferenceValue = ll;
        so.FindProperty("_cameraRestOffset").vector2Value = new Vector2(2.327f, 4.60f);
        so.FindProperty("_returnDelay").floatValue        = 2.5f;

        // ── Farm Cannon (2026-07-02, replaces the trebuchet visual system) ──────
        // Delete the old trebuchet scene GOs — CatapultLauncher no longer references them
        // (no _armSpriteGO/_swingSpriteGO/_trebuchetXSprite fields exist any more).
        foreach (var n in new[] { "Trabuchet_Body", "Trabuchet_Arm", "Trabuchet_Swing" })
        {
            var old = GameObject.Find(n);
            if (old != null) { Object.DestroyImmediate(old); Debug.Log($"[FarmFury] Deleted trebuchet GO '{n}'."); }
        }

        var cannonGO = GameObject.Find("FarmCannon");
        if (cannonGO == null)
        {
            cannonGO = new GameObject("FarmCannon");
            Debug.Log("[FarmFury] Created 'FarmCannon' GameObject.");
        }
        // User-verified 2026-07-03 (was -4.5,-2.5,2 / 2.2,1.8,1 — see CatapultLauncher.BuildCannon()).
        cannonGO.transform.position   = new Vector3(-3.0012f, -5.1223f, 0f);
        cannonGO.transform.localScale = new Vector3(1.4711188f, 1.3868444f, 1f);

        var cannonSR = cannonGO.GetComponent<SpriteRenderer>();
        if (cannonSR == null) cannonSR = cannonGO.AddComponent<SpriteRenderer>();
        cannonSR.sortingOrder = 4; // explicit order beats Z-depth ties regardless of Z=2

        const string cannonPath = "Assets/Sprites/Environment/Launchers/Cannon.png";
        if (AssetDatabase.LoadAssetAtPath<Texture2D>(cannonPath) != null)
        {
            var imp = AssetImporter.GetAtPath(cannonPath) as TextureImporter;
            if (imp != null)
            {
                bool dirty = false;
                if (imp.textureType         != TextureImporterType.Sprite) { imp.textureType         = TextureImporterType.Sprite; dirty = true; }
                if (!imp.alphaIsTransparency)                               { imp.alphaIsTransparency = true;                       dirty = true; }
                if (imp.spriteImportMode    != SpriteImportMode.Single)    { imp.spriteImportMode    = SpriteImportMode.Single;    dirty = true; }
                if (dirty) imp.SaveAndReimport();
            }
            var cannonSprite = AssetDatabase.LoadAssetAtPath<Sprite>(cannonPath);
            cannonSR.sprite = cannonSprite;
            so.FindProperty("_cannonSprite").objectReferenceValue = cannonSprite;
            Debug.Log("[FarmFury] Launcher: Cannon.png wired (Single mode).");
        }
        else
            Debug.LogWarning("[FarmFury] Cannon.png not found at " + cannonPath);

        so.FindProperty("_cannonGO").objectReferenceValue = cannonGO;

        so.ApplyModifiedProperties();

        Debug.Log("[FarmFury] Launcher: wired LevelLoader reference.");
    }

    // ── Block art sprites: wire World1Props textures into WoodBlock + StoneBlock prefabs ──
    // Sets PPU = texture width so native sprite size is 1×1, making localScale(w,h) map
    // directly to world dimensions in BlockBase.Initialise().

    static void WireBlockSprites()
    {
        const string folder = "Assets/Sprites/Environment/World1Props";

        WireBlockPrefab("Assets/Prefabs/Blocks/WoodBlock.prefab", folder, new[]
        {
            ("_sprNormal",      "Block_Wood_Normal.png"),
            ("_sprHorizontal",  "Plank_Horizontal.png"),
            ("_sprVertical",    "2D_Block_Wood_Vertical.png"),
        });

        WireBlockPrefab("Assets/Prefabs/Blocks/StoneBlock.prefab", folder, new[]
        {
            ("_sprNormal",      "Block_Stone_Normal.png"),
            ("_sprHorizontal",  "Block_Stone_Normal.png"),   // reuse until dedicated art exists
            ("_sprVertical",    "Block_Stone_Normal.png"),
        });
    }

    // ── HaybaleBlock: a WoodBlock variant rendered with Haybail.png ─────────────
    // Same mechanics as WoodBlock — health/mass/passThrough come entirely from
    // BlockSpawnData overrides at the level-data level. Only the default art differs.
    // LevelLoader picks this prefab when BlockSpawnData.type == BlockType.Haybale.
    static void EnsureHaybaleBlockPrefab()
    {
        const string prefabPath = "Assets/Prefabs/Blocks/HaybaleBlock.prefab";

        if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null)
        {
            var go   = new GameObject("HaybaleBlock");
            go.layer = 8; // Block layer
            go.AddComponent<Rigidbody2D>();
            var bc   = go.AddComponent<BoxCollider2D>();
            bc.size  = new Vector2(1f, 1f);
            go.AddComponent<SpriteRenderer>();
            go.AddComponent<WoodBlock>();
            PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
            Object.DestroyImmediate(go);
            Debug.Log("[FarmFury] Created HaybaleBlock.prefab.");
        }

        WireBlockPrefab(prefabPath, "Assets/Sprites/Environment/World1Props", new[]
        {
            ("_sprNormal",     "Haybail.png"),
            ("_sprHorizontal", "Haybail.png"),
            ("_sprVertical",   "Haybail.png"),
            // Both PNGs are the same 500x500 canvas, so WireBlockPrefab's per-sprite
            // "PPU = texture width" auto-derivation gives both an identical native 1x1 size —
            // the hit-flash swap in BlockBase.PlayDamageFlash() can't visibly change size/shape.
            ("_sprDamaged",    "Haybail_Damaged.png"),
        });
    }

    static void WireBlockPrefab(string prefabPath, string folder,
                                 (string field, string file)[] sprites)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null)
        {
            Debug.LogWarning($"[FarmFury] Block prefab not found: {prefabPath}");
            return;
        }

        var contents = PrefabUtility.LoadPrefabContents(prefabPath);
        var block    = contents.GetComponent<BlockBase>();
        if (block == null)
        {
            PrefabUtility.UnloadPrefabContents(contents);
            Debug.LogWarning($"[FarmFury] No BlockBase component on {prefabPath}");
            return;
        }

        var so = new SerializedObject(block);
        foreach (var (field, file) in sprites)
        {
            string spPath = $"{folder}/{file}";
            // PPU = texture width → native size = 1×1 so localScale(w,h) = exact world size
            var imp = AssetImporter.GetAtPath(spPath) as TextureImporter;
            if (imp != null)
            {
                imp.GetSourceTextureWidthAndHeight(out int tw, out _);
                int targetPpu = tw > 0 ? tw : 1024;
                bool dirty = false;
                if (imp.textureType         != TextureImporterType.Sprite)          { imp.textureType         = TextureImporterType.Sprite;          dirty = true; }
                if (imp.spritePixelsPerUnit != targetPpu)                            { imp.spritePixelsPerUnit = targetPpu;                           dirty = true; }
                if (!imp.alphaIsTransparency)                                        { imp.alphaIsTransparency = true;                                dirty = true; }
                if (imp.spriteImportMode    != SpriteImportMode.Single)             { imp.spriteImportMode    = SpriteImportMode.Single;             dirty = true; }
                if (dirty) imp.SaveAndReimport();
            }
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spPath);
            if (sprite != null)
            {
                so.FindProperty(field).objectReferenceValue = sprite;
                Debug.Log($"[FarmFury] {System.IO.Path.GetFileNameWithoutExtension(prefabPath)}.{field} → {file}");
            }
            else
                Debug.LogWarning($"[FarmFury] Sprite not found: {spPath}");
        }
        so.ApplyModifiedProperties();
        PrefabUtility.SaveAsPrefabAsset(contents, prefabPath);
        PrefabUtility.UnloadPrefabContents(contents);
    }

    // ── Robot sprite: wire Robot_Idle into Robot prefab SpriteRenderer ──────────

    static void WireRobotSprite()
    {
        const string prefabPath = "Assets/Prefabs/Enemies/Robot.prefab";
        const string spritePath = "Assets/Sprites/Enemies/Robot/Robot_Idle.png";

        if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null)
        {
            Debug.LogWarning("[FarmFury] Robot prefab not found — skipping robot sprite wiring.");
            return;
        }

        var spriteAsset = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
        if (spriteAsset == null)
        {
            Debug.LogWarning("[FarmFury] Robot_Idle.png not found. Run tools/remove_backgrounds.py first.");
            return;
        }

        // Ensure PPU=1746 on the robot sprite
        var imp = AssetImporter.GetAtPath(spritePath) as TextureImporter;
        if (imp != null)
        {
            bool dirty = false;
            if (imp.textureType         != TextureImporterType.Sprite)         { imp.textureType         = TextureImporterType.Sprite;         dirty = true; }
            if (imp.spritePixelsPerUnit != 1746)                                { imp.spritePixelsPerUnit = 1746;                               dirty = true; }
            if (!imp.alphaIsTransparency)                                        { imp.alphaIsTransparency = true;                               dirty = true; }
            if (imp.spriteImportMode    != SpriteImportMode.Single)             { imp.spriteImportMode    = SpriteImportMode.Single;            dirty = true; }
            if (dirty) { imp.SaveAndReimport(); spriteAsset = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath); }
        }

        // Wire into RobotEnemy._robotSprite via LoadPrefabContents (no EditPrefabContentsScope)
        var go = PrefabUtility.LoadPrefabContents(prefabPath);
        var robot = go.GetComponent<RobotEnemy>();
        if (robot != null)
        {
            var so = new SerializedObject(robot);
            so.FindProperty("_robotSprite").objectReferenceValue = spriteAsset;
            so.ApplyModifiedProperties();
        }
        PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
        PrefabUtility.UnloadPrefabContents(go);
        Debug.Log("[FarmFury] Robot: wired Robot_Idle.png into _robotSprite (PPU=1746).");
    }

    // ── HarvesterRobot: create a SEPARATE prefab (distinct from Robot.prefab) ───
    // LevelLoader picks this prefab when RobotSpawnData.robotType == RobotType.Harvester.

    static void EnsureHarvesterRobotPrefab()
    {
        const string prefabPath = "Assets/Prefabs/Enemies/HarvesterRobot.prefab";
        const string spritePath = "Assets/Sprites/Enemies/Robot/HarvesterRobot.png";
        const string damagedSpritePath = "Assets/Sprites/Enemies/Robot/HarvesterRobot_Damaged.png";

        var imp = AssetImporter.GetAtPath(spritePath) as TextureImporter;
        if (imp == null)
        {
            Debug.LogWarning($"[FarmFury] HarvesterRobot.png not found at {spritePath}.");
            return;
        }
        bool dirty = false;
        if (imp.textureType         != TextureImporterType.Sprite)         { imp.textureType         = TextureImporterType.Sprite;         dirty = true; }
        if (imp.spritePixelsPerUnit != 1746)                                { imp.spritePixelsPerUnit = 1746;                               dirty = true; }
        if (!imp.alphaIsTransparency)                                       { imp.alphaIsTransparency = true;                               dirty = true; }
        if (imp.spriteImportMode    != SpriteImportMode.Single)            { imp.spriteImportMode    = SpriteImportMode.Single;            dirty = true; }
        if (dirty) imp.SaveAndReimport();

        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
        if (sprite == null) { Debug.LogWarning("[FarmFury] HarvesterRobot.png failed to load as Sprite."); return; }

        // HarvesterRobot_Damaged.png is a 500x500 canvas, unlike the normal art's 612x408 —
        // PPU=1746 would render it a different apparent size when PlayDamageFlash() swaps sprites
        // on the same Transform. Computed PPU so the damaged canvas's height (500px) renders at
        // the same world height as the normal sprite's height (408px / 1746 = 0.2337u):
        // 500 / 0.2337 ≈ 2140. Only needs to be close for a 0.15s flash, not pixel-exact.
        var dImp = AssetImporter.GetAtPath(damagedSpritePath) as TextureImporter;
        Sprite damagedSprite = null;
        if (dImp == null)
        {
            Debug.LogWarning($"[FarmFury] HarvesterRobot_Damaged.png not found at {damagedSpritePath} — hit-flash will fall back to the plain white tint.");
        }
        else
        {
            bool dDirty = false;
            if (dImp.textureType         != TextureImporterType.Sprite)  { dImp.textureType         = TextureImporterType.Sprite;  dDirty = true; }
            if (dImp.spritePixelsPerUnit != 2140)                         { dImp.spritePixelsPerUnit = 2140;                        dDirty = true; }
            if (!dImp.alphaIsTransparency)                                { dImp.alphaIsTransparency = true;                        dDirty = true; }
            if (dImp.spriteImportMode    != SpriteImportMode.Single)     { dImp.spriteImportMode    = SpriteImportMode.Single;     dDirty = true; }
            if (dDirty) dImp.SaveAndReimport();
            damagedSprite = AssetDatabase.LoadAssetAtPath<Sprite>(damagedSpritePath);
        }

        bool exists = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null;
        GameObject go;
        if (exists)
        {
            go = PrefabUtility.LoadPrefabContents(prefabPath);
        }
        else
        {
            go       = new GameObject("HarvesterRobot");
            go.layer = 9;
            var rb   = go.AddComponent<Rigidbody2D>();
            rb.mass  = 20f;
            var bc   = go.AddComponent<BoxCollider2D>();
            bc.size  = new Vector2(1f, 1f);
            go.AddComponent<SpriteRenderer>();
            go.AddComponent<RobotEnemy>();
        }

        var robot = go.GetComponent<RobotEnemy>();
        if (robot != null)
        {
            var so = new SerializedObject(robot);
            so.FindProperty("_robotSprite").objectReferenceValue = sprite;
            if (damagedSprite != null)
                so.FindProperty("_robotDamagedSprite").objectReferenceValue = damagedSprite;
            // _maxHealth raised from the class default (35) to 40 (fixed 2026-07-01) — with the
            // recalibrated trajectory (LaunchVelocity(), max ~7m/s), a Cluck shot that punches
            // through the hay pile at 70% speed deals ~24-28 impulse damage. At 35 HP the robot
            // would survive with only ~7-11 HP — technically not one-shot, but fragile enough to
            // read as "instant" (any stray contact finishes it). 40 HP leaves a safer ~12-16 HP
            // margin after the hay-clearing hit, while a second solid hit (~34-40 direct, or
            // another ~24-28 pass-through) still reliably finishes it — matching L01's par=2.
            so.FindProperty("_maxHealth").floatValue = 40f;
            so.ApplyModifiedProperties();
        }

        if (exists)
        {
            PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
            PrefabUtility.UnloadPrefabContents(go);
        }
        else
        {
            PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
            Object.DestroyImmediate(go);
        }
        AssetDatabase.Refresh();
        Debug.Log("[FarmFury] HarvesterRobot.prefab created with HarvesterRobot.png (PPU=1746).");
    }

    // ── Egg prefab: create if missing, wire into CluckAnimal prefab ─────────────

    static void EnsureEggPrefab()
    {
        const string eggPath   = "Assets/Prefabs/Animals/Egg.prefab";
        const string cluckPath = "Assets/Prefabs/Animals/CluckAnimal.prefab";

        // Always recreate — guards against a stale prefab from an earlier compile-error run
        if (AssetDatabase.LoadAssetAtPath<GameObject>(eggPath) != null)
            AssetDatabase.DeleteAsset(eggPath);

        var go = new GameObject("Egg");
        go.layer = 10; // Egg layer

        var rb = go.AddComponent<Rigidbody2D>();
        rb.mass                   = 1f;
        rb.gravityScale           = 1f;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        var col    = go.AddComponent<CircleCollider2D>();
        col.radius = 0.15f;

        var sr = go.AddComponent<SpriteRenderer>();
        sr.color        = new Color(1f, 0.95f, 0.7f);
        sr.sortingOrder = 4;

        go.AddComponent<EggProjectile>();

        PrefabUtility.SaveAsPrefabAsset(go, eggPath);
        Object.DestroyImmediate(go);
        AssetDatabase.Refresh();
        Debug.Log($"[FarmFury] Created {eggPath}");

        // Wire _eggPrefab inside the CluckAnimal prefab
        if (AssetDatabase.LoadAssetAtPath<GameObject>(cluckPath) == null)
        {
            Debug.LogWarning("[FarmFury] CluckAnimal prefab not found.");
            return;
        }

        var contents  = PrefabUtility.LoadPrefabContents(cluckPath);
        var cluckComp = contents.GetComponent<CluckAnimal>();
        if (cluckComp != null)
        {
            var so    = new SerializedObject(cluckComp);
            var eggGO = AssetDatabase.LoadAssetAtPath<GameObject>(eggPath);
            // _eggPrefab on CluckAnimal is typed GameObject (Instantiate(_eggPrefab, ...) in
            // SpawnEggs() expects a GameObject) — assigning eggGO.GetComponent<EggProjectile>()
            // here was a type mismatch that SerializedProperty silently rejected, leaving the
            // field null (UnassignedReferenceException at runtime on ability trigger). Fixed
            // 2026-07-05: assign the GameObject itself, not a component on it.
            so.FindProperty("_eggPrefab").objectReferenceValue = eggGO;
            so.ApplyModifiedProperties();
            PrefabUtility.SaveAsPrefabAsset(contents, cluckPath);
            Debug.Log("[FarmFury] CluckAnimal._eggPrefab wired.");
        }
        PrefabUtility.UnloadPrefabContents(contents);
    }

    // ── Ground: static collider + layered terrain visual ─────────────────────

    static void EnsureGround()
    {
        // Delete old code-generated visual layers from previous Wire Scene References runs.
        // The user authors ground/grass visuals directly in the scene — we don't create them.
        foreach (var n in new[] { "GroundFill", "GrassBase", "GrassTips", "SoilEdge", "GrassTop" })
        {
            var old = GameObject.Find(n);
            if (old != null) { Object.DestroyImmediate(old); Debug.Log($"[FarmFury] Deleted old visual layer '{n}'."); }
        }

        // Physics collider only — invisible, no SpriteRenderer.
        // Surface Y = -6.60; centre at (0, -6.85); scale (60,0.5,1) → collider top edge at -6.60.
        const float GroundSurface = -6.60f;
        var go = GameObject.Find("Ground");
        if (go == null) { go = new GameObject("Ground"); Debug.Log("[FarmFury] Created 'Ground' physics collider."); }
        go.tag   = "Ground";
        go.layer = 6;
        go.transform.position   = new Vector3(0f, GroundSurface - 0.25f, 0f);
        go.transform.localScale = new Vector3(60f, 0.5f, 1f);

        // Remove any old SpriteRenderer — ground is physics-only from now on
        var oldSr = go.GetComponent<SpriteRenderer>();
        if (oldSr != null) Object.DestroyImmediate(oldSr);

        if (go.GetComponent<BoxCollider2D>() == null)
        {
            var col  = go.AddComponent<BoxCollider2D>();
            col.size = new Vector2(1f, 1f);
        }
        if (go.GetComponent<Rigidbody2D>() == null)
        {
            var rb   = go.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Static;
        }
        Debug.Log("[FarmFury] Ground physics collider at surface Y=-6.60 (ground/grass visuals are scene-authored, not code-generated).");
    }

    // ── Camera: position to see launcher + structures ─────────────────────────

    static void PositionCamera()
    {
        var cam = Object.FindAnyObjectByType<Camera>();
        if (cam == null) return;
        cam.orthographic     = true;
        cam.orthographicSize = 4.5f;
        cam.transform.position = new Vector3(0f, -2f, -10f);  // rest pos = launcher(-2.327,-6.60) + offset(2.327,4.60)
        cam.backgroundColor    = new Color(0.38f, 0.65f, 0.90f);
        Debug.Log("[FarmFury] Camera positioned at (0, -2, -10), orthoSize=4.5.");
    }

    // ── Ensure parent holder GameObjects exist in scene ───────────────────────

    static void EnsureParents()
    {
        EnsureGO("BlockParent");
        EnsureGO("RobotParent");
    }

    static void EnsureGO(string name)
    {
        if (GameObject.Find(name) == null)
        {
            new GameObject(name);
            Debug.Log($"[FarmFury] Created '{name}' GameObject.");
        }
    }

    // ── Helper: find a prefab by name in a folder and assign the correct component ──
    // componentType: the MonoBehaviour type the field expects (null → assign the GO itself)

    static void SetPrefab(SerializedObject so, string fieldName, string prefabName,
                          string folder, System.Type componentType = null)
    {
        var guids = AssetDatabase.FindAssets(prefabName + " t:Prefab", new[] { folder });
        if (guids.Length == 0)
        {
            Debug.LogWarning($"[FarmFury] Prefab '{prefabName}' not found in {folder}");
            return;
        }
        var path   = AssetDatabase.GUIDToAssetPath(guids[0]);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

        // Fields typed as a Component subclass must receive the component, not the GO.
        // Assigning a GameObject to a component-typed SerializedProperty serialises as null.
        UnityEngine.Object target = componentType != null
            ? prefab.GetComponent(componentType)
            : prefab;

        if (target == null && componentType != null)
        {
            Debug.LogWarning($"[FarmFury] {componentType.Name} not found on prefab '{prefabName}'");
            target = prefab; // fallback
        }

        so.FindProperty(fieldName).objectReferenceValue = target;
        Debug.Log($"[FarmFury]   {fieldName} -> {path}");
    }
}
