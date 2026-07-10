// FarmFury - Wires all Inspector references in Game.unity automatically.
// Menu: FarmFury > Wire Scene References
// Batch: BuildScript.WireScene

using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Video;

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
        EnsureGroundVisual();   // Placeholder tinted ground/grass strip (see method comment)
        EnsureBackground();     // Sky painting backdrop behind all game objects
        EnsureScenery();        // SceneryBuilder GO + World1Props sprite refs
        EnsureEggPrefab();      // Create Egg prefab + wire into CluckAnimal prefab
        EnsureAnimalPrefabs();  // Create Percy/Woolly/Ducky/Horace/Gerald/Billy prefabs
        EnsureHUD();            // HUDController GO (builds Canvas at runtime)
        EnsureMainMenu();       // MainMenuController GO (Canvas sortingOrder 400)
        EnsureWorldMap();       // WorldMapController GO (Sunrise Meadows level-map — PLAY's destination)
        EnsureAudioManager();   // AudioManager GO wired with music/cannon/falling clips
        EnsureLevelCompleteManager();  // Cluck celebration video + laugh audio
        EnsureLevelFailedManager();    // Robot taunt video + taunt audio
        EnsureCelebrationVideoBackground(); // Sky backdrop behind both of the above (shared overlay)
        WireGameManager();      // _levels array
        WireLevelLoader();      // 8 animal prefab refs + block/robot + 2 parent transforms
        WireLauncher();         // CatapultLauncher + LevelLoader ref + counterweight sprite
        WireRobotSprite();                // Robot_Idle.png → Robot prefab SpriteRenderer
        EnsureHarvesterRobotPrefab();  // Create/update HarvesterRobot.prefab (separate from Robot)
        EnsureSemiHarvesterRobotPrefab();  // Create/update SemiHarvesterRobot.prefab (separate from Robot/HarvesterRobot)
        EnsureHaybaleBlockPrefab();    // Create/update HaybaleBlock.prefab (WoodBlock + Haybail.png art)
        EnsureExplodingBarrelPrefab(); // Create/update ExplodingBarrelBlock.prefab (WoodBlock + WoodenBarrel.png art, area damage on death)
        WireBlockSprites();     // Art sprites into WoodBlock + StoneBlock prefabs
        PositionCamera();       // Move camera to see the play area
        SpriteWiring.WireAll(); // Wire character pose sprites into all 8 animal prefabs

        // Delete visual-only placeholder GOs that duplicate code-spawned gameplay objects.
        // These are scene GOs pasted in for reference during layout — they must be gone
        // before Play mode so the code-spawned prefabs are the only instances.
        // "LevelScratch" is LevelLayoutDumper's raw-sprite design container (see that file) —
        // NOT tied to LevelLoader's per-level spawn/clear cycle, so if left in the scene it
        // renders permanently regardless of which level is actually loaded, visually bleeding
        // into every other level ("level 1 and level 2 compiled over each other" — real user
        // report, 2026-07-09, after L02's design sprites were left behind following a dump).
        // Always deleted here once its data has presumably already been dumped/pasted into
        // LevelDataGenerator.cs.
        foreach (var placeholderName in new[] { "Cluck_Loaded_0", "Cluck_InFlight", "HarvesterRobot", "SemiHarvesterRobot", "LevelScratch" })
        {
            var ph = GameObject.Find(placeholderName);
            if (ph != null)
            {
                Object.DestroyImmediate(ph);
                Debug.Log($"[FarmFury] Deleted visual placeholder '{placeholderName}'.");
            }
        }

        // LevelSelectController removed 2026-07-26 (user-reported: an old "SELECT LEVEL" grid
        // screen kept appearing behind/instead of the Sunrise Meadows world map). Root cause:
        // LevelSelectController.Start() subscribed to GameManager.OnStateChanged and showed
        // itself on GameState.Idle — the exact same event and Canvas sortingOrder (300)
        // WorldMapController uses — so the two were silently racing to show themselves on every
        // Idle transition, with whichever GameObject came later in scene order winning. It was
        // never actually disabled after WorldMapController superseded it for World 1 (2026-07-15),
        // just left running unused in the background until this. Deletes the leftover scene
        // GameObject from any project that already ran an older Wire Scene References pass.
        var staleLevelSelect = GameObject.Find("LevelSelect");
        if (staleLevelSelect != null)
        {
            Object.DestroyImmediate(staleLevelSelect);
            Debug.Log("[FarmFury] Deleted obsolete 'LevelSelect' GameObject (superseded by WorldMapController).");
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

        // "SkyPainting.png" doesn't exist on disk — the actual sky art committed to the project
        // is Background_SkyV1.png (see Assets/Sprites/Environment/Skies/). This path pointed at
        // a nonexistent file, so this whole wiring step silently no-op'd (LogWarning only) and
        // relied entirely on the hand-authored Background_SkyV1 scene GO already having its own
        // sprite set — which is why the live in-game sky always looked correct despite this.
        const string skyPath = "Assets/Sprites/Environment/Skies/Background_SkyV1.png";

        // Background_SkyV1 is 1920×1080 art — ensure it is imported as a Sprite before loading.
        // If it was imported as the default Texture2D, LoadAssetAtPath<Sprite> returns null.
        var skyImp = AssetImporter.GetAtPath(skyPath) as TextureImporter;
        if (skyImp != null)
        {
            bool dirty = false;
            if (skyImp.textureType         != TextureImporterType.Sprite)          { skyImp.textureType         = TextureImporterType.Sprite;          dirty = true; }
            if (skyImp.spritePixelsPerUnit != 100)                                   { skyImp.spritePixelsPerUnit = 100;                                  dirty = true; }
            if (!skyImp.alphaIsTransparency)                                         { skyImp.alphaIsTransparency = true;                                 dirty = true; }
            if (skyImp.spriteImportMode    != SpriteImportMode.Single)              { skyImp.spriteImportMode    = SpriteImportMode.Single;              dirty = true; }
            if (dirty) { skyImp.SaveAndReimport(); Debug.Log("[FarmFury] Background_SkyV1.png re-imported as Sprite (PPU=100)."); }
        }
        else
            Debug.LogWarning($"[FarmFury] Background_SkyV1.png not found at {skyPath} — copy it there and re-run Wire Scene References.");

        var skySprite = AssetDatabase.LoadAssetAtPath<Sprite>(skyPath);
        if (skySprite != null)
        {
            var so = new SerializedObject(bc);
            so.FindProperty("_skySprite").objectReferenceValue = skySprite;
            so.ApplyModifiedProperties();
            Debug.Log("[FarmFury] Background: Background_SkyV1 wired.");
        }
        else
        {
            Debug.LogWarning($"[FarmFury] Background_SkyV1.png not found at {skyPath}.");
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

    static Sprite FindSpriteByKeyword(string[] guids, string keywordLower)
    {
        foreach (var g in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(g);
            if (path.ToLower().Contains(keywordLower))
            {
                var spr = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (spr != null) return spr;
            }
        }
        return null;
    }

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

        // Wire card sprites into _cardSprites[]. Primary source is Assets/Sprites/UI/Cards
        // (populated from assets/FarmCards/*.png by EditorAutoSetup) — but assets/ (the raw art
        // source) is currently deleted from disk, so that folder is empty for now. Assets/Sprites/UI/
        // MatchUp/ already has real per-animal art (Cluck_Chicken.png, Bessie_Cow.png, wired for the
        // pre-match screen) that happens to match the same filename keywords, so it's searched as a
        // fallback for any animal the Cards folder doesn't cover, rather than leaving the HUD cards
        // on the plain tinted-square fallback.
        const string cardsFolder   = "Assets/Sprites/UI/Cards";
        const string matchUpFolder = "Assets/Sprites/UI/MatchUp";
        var hud = go.GetComponent<HUDController>();
        var so  = new SerializedObject(hud);
        var arr = so.FindProperty("_cardSprites");
        arr.arraySize = CardKeywords.Length;

        var guids       = AssetDatabase.FindAssets("t:Sprite", new[] { cardsFolder });
        var matchUpGuids = AssetDatabase.FindAssets("t:Sprite", new[] { matchUpFolder });
        int wired = 0;
        for (int i = 0; i < CardKeywords.Length; i++)
        {
            string kw = CardKeywords[i].ToLower();
            Sprite spr = FindSpriteByKeyword(guids, kw) ?? FindSpriteByKeyword(matchUpGuids, kw);
            if (spr != null)
            {
                arr.GetArrayElementAtIndex(i).objectReferenceValue = spr;
                wired++;
            }
        }
        // Level Complete / Level Failed panel art — Scoreboard/LevelComplete/LevelFailed/
        // ScoreStars live in the MatchUp folder (already used elsewhere for the pre-match
        // sequence); Btn_play/Btn_home are the shared Icon set also used by
        // MainMenuController/WorldMapController. Scoreboard + the two buttons are shared
        // between both end-of-level panels (same assets, same field).
        WireSprite(so, "_lcTitleSprite",       "Assets/Sprites/UI/MatchUp/LevelComplete.png");
        WireSprite(so, "_starSprite",          "Assets/Sprites/UI/MatchUp/ScoreStars.png");
        WireSprite(so, "_levelUpStarSprite",   "Assets/Sprites/UI/MatchUp/Levelup.png");
        WireSprite(so, "_lfTitleSprite",       "Assets/Sprites/UI/MatchUp/LevelFailed.png");
        WireSprite(so, "_scoreboardSprite",    "Assets/Sprites/UI/MatchUp/Scoreboard.png");
        WireSprite(so, "_playButtonSprite",    "Assets/Sprites/UI/Icon/Btn_play.png");
        WireSprite(so, "_homeButtonSprite",    "Assets/Sprites/UI/Icon/Btn_home.png");
        WireSprite(so, "_quitButtonSprite",    "Assets/Sprites/UI/Icon/Btn_quite.png");
        // Top-right Quit/Mute/Pause row (2026-07-26) — Quit reuses _quitButtonSprite above.
        WireSprite(so, "_pauseButtonSprite",   "Assets/Sprites/UI/Icon/Btn_pause.png");
        WireSprite(so, "_musicOnSprite",       "Assets/Sprites/UI/Icon/Btn_music.png");
        WireSprite(so, "_musicOffSprite",      "Assets/Sprites/UI/Icon/NoSound.png");

        so.ApplyModifiedProperties();

        if (wired > 0)
            Debug.Log($"[FarmFury] HUD: wired {wired}/8 card sprites from {cardsFolder} / {matchUpFolder}.");
        else
            Debug.LogWarning($"[FarmFury] HUD: no card sprites found in {cardsFolder} or {matchUpFolder}. " +
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

        // Wire the PLAY button sprite (bottom-left corner icon, per the 2026-07-16
        // LandingPage_New.png mockup). Lives at Assets/Sprites/UI/Icon/ and is already imported
        // (Single mode, PPU=100) by SpriteAutoImporter's generic "Sprites/UI/" rule — no reimport
        // step needed, unlike the old Assets/Sprites/UI/Play.png path this replaces (that file
        // never actually existed, so _playButtonSprite silently fell back to a plain orange
        // square at runtime — see MainMenuController's fallback color). The SETTINGS icon/popup
        // was removed 2026-07-07, then re-added 2026-07-10 (both user requests) — Btn_settings.png
        // already existed on disk the whole time, just unwired while the button was gone.
        var mainMenu = go.GetComponent<MainMenuController>();
        var mmSo = new SerializedObject(mainMenu);
        WireSprite(mmSo, "_playButtonSprite", "Assets/Sprites/UI/Icon/Btn_play.png");
        WireSprite(mmSo, "_settingsButtonSprite", "Assets/Sprites/UI/Icon/Btn_settings.png");
        // Settings popup backdrop (2026-07-10) — same Scoreboard.png HUDController's Level
        // Complete/Failed panels already use.
        WireSprite(mmSo, "_scoreboardSprite", "Assets/Sprites/UI/MatchUp/Scoreboard.png");
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
        WireSprite(mapSo, "_unlockedSprite",        $"{artFolder}/LevelMarker_tick.png");
        WireSprite(mapSo, "_playerPositionSprite",  $"{artFolder}/PlayerPosition.png");
        WireSprite(mapSo, "_playButtonSprite",      $"{iconFolder}/Btn_play.png");
        WireSprite(mapSo, "_homeButtonSprite",      $"{iconFolder}/Btn_home.png");

        WireMatchUpCards(mapSo);

        mapSo.ApplyModifiedProperties();
    }

    // MatchUpScreen art — dedicated folder, distinct from the HUD's Assets/Sprites/UI/Cards/
    // (see CardKeywords above, still used by EnsureHUD). Only Cluck/Bessie (animal) and
    // Basic/Harvester/SemiHarvester (robot) have art here today, matching what L01-L06 actually
    // use — other AnimalType slots are left null and MatchUpScreen already tolerates that (hides
    // the card image rather than crashing).
    static void WireMatchUpCards(SerializedObject mapSo)
    {
        const string matchUpFolder = "Assets/Sprites/UI/MatchUp";

        WireSprite(mapSo, "_matchUpBackgroundSprite", $"{matchUpFolder}/MatchUpBackground.png");
        WireSprite(mapSo, "_vsSprite",                $"{matchUpFolder}/VS.png");

        // Level header art — index 0 (LevelHeader1.png) is the fallback MatchUpScreen.Show()
        // uses for any level whose own slot is empty. Levels 2-5 already have their own header
        // art (different naming, since they were added individually rather than following
        // LevelHeader1.png's naming pattern) — wired directly by filename.
        var headerArr = mapSo.FindProperty("_levelHeaderSprites");
        headerArr.arraySize = WorldMapController.LevelCount;
        headerArr.GetArrayElementAtIndex(0).objectReferenceValue =
            AssetDatabase.LoadAssetAtPath<Sprite>($"{matchUpFolder}/LevelHeader1.png");
        headerArr.GetArrayElementAtIndex(1).objectReferenceValue =
            AssetDatabase.LoadAssetAtPath<Sprite>($"{matchUpFolder}/level2.png");
        headerArr.GetArrayElementAtIndex(2).objectReferenceValue =
            AssetDatabase.LoadAssetAtPath<Sprite>($"{matchUpFolder}/level3-removebg-preview.png");
        headerArr.GetArrayElementAtIndex(3).objectReferenceValue =
            AssetDatabase.LoadAssetAtPath<Sprite>($"{matchUpFolder}/level4.png");
        headerArr.GetArrayElementAtIndex(4).objectReferenceValue =
            AssetDatabase.LoadAssetAtPath<Sprite>($"{matchUpFolder}/level5.png");

        WireSprite(mapSo, "_countdown3Sprite",        $"{matchUpFolder}/countdown3.png");
        WireSprite(mapSo, "_countdown2Sprite",        $"{matchUpFolder}/countdown2.png");
        WireSprite(mapSo, "_countdown1Sprite",        $"{matchUpFolder}/countdown1.png");
        WireSprite(mapSo, "_countdownReadySprite",    $"{matchUpFolder}/Countdown_Ready.png");
        WireAudioClip(mapSo, "_countdownClip",        "Assets/Audio/Countdown.mp3");
        WireSprite(mapSo, "_cluckFlySprite",           "Assets/Sprites/Characters/Cluck/Cluck_InFlight.png");
        WireAudioClip(mapSo, "_cluckFallingClip",      "Assets/Audio/Cluck_falling.mp3");
        WireSprite(mapSo, "_eggSprite",                "Assets/Sprites/Characters/Cluck/Egg.png");

        WireArrayByKeyword(mapSo, "_animalCardSprites", matchUpFolder, CardKeywords, "animal");

        // Robot cards need exact-filename matching, not substring: "Robot.png" (Basic) has no
        // distinguishing prefix, but "Harvestor_Robot.png"/"Harvestor_Robot1.png" both also
        // contain "robot" — a naive keyword search (like WireArrayByKeyword uses for animals)
        // would ambiguously match all three off either keyword.
        var robotArr = mapSo.FindProperty("_robotCardSprites");
        robotArr.arraySize = 3;
        robotArr.GetArrayElementAtIndex(0).objectReferenceValue = // Basic
            AssetDatabase.LoadAssetAtPath<Sprite>($"{matchUpFolder}/Robot.png");
        // Prefer the newer Harvestor_Robot1.png (revised art, added a day after the original)
        // over Harvestor_Robot.png if both exist.
        Sprite harvester = AssetDatabase.LoadAssetAtPath<Sprite>($"{matchUpFolder}/Harvestor_Robot1.png")
                         ?? AssetDatabase.LoadAssetAtPath<Sprite>($"{matchUpFolder}/Harvestor_Robot.png");
        robotArr.GetArrayElementAtIndex(1).objectReferenceValue = harvester; // Harvester

        // SemiHarvester — dedicated framed MatchUp card art (Semi_Harvestor.png, matches
        // Harvestor_Robot.png's style), replacing the earlier fallback to the plain enemy
        // sprite (Assets/Sprites/Enemies/Robot/Robot_SemiHarvest.png — 2026-07-09, user-reported
        // "that's not the right card").
        robotArr.GetArrayElementAtIndex(2).objectReferenceValue =
            AssetDatabase.LoadAssetAtPath<Sprite>($"{matchUpFolder}/Semi_Harvestor.png");
    }

    // Added 2026-07-24 — a Sunrise Meadows screenshot showed level markers rendering with
    // leftover star-tier art from earlier testing sessions, which looked like a bug at first
    // glance. It isn't one: WorldMapController.IsUnlocked()/ScoreManager.GetBestStars() were
    // both already correct — that screenshot's PlayerPrefs simply had levels previously
    // completed from earlier testing. (Per-star-tier marker art no longer exists as of
    // 2026-07-27 — every unlocked level now renders LevelMarker_tick.png regardless of star
    // count, so this scenario can no longer visibly recur, but the underlying leftover-
    // PlayerPrefs issue this menu item clears is unrelated to marker art and still applies.) On
    // a genuinely fresh save (no ff_stars_N keys set), level 1 shows LevelMarker_tick.png and
    // every other level shows LevelMarker_Locked.png, per the existing (unchanged) logic. This
    // menu item just clears that leftover test data on demand, standalone from Wire Scene
    // References (which is scene/asset wiring, not runtime save data — bundling this into it
    // would be a surprising side effect of an unrelated action).
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

    // SerializedObject-based overload of WireAudioClip(GameObject, ...) below, for components
    // other than AudioManager (e.g. WorldMapController's _countdownClip) — same shape as
    // WireSprite above, just for AudioClip.
    static void WireAudioClip(SerializedObject so, string fieldName, string path)
    {
        var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        if (clip == null)
        {
            Debug.LogWarning($"[FarmFury] Audio clip not found at {path} (target field: {fieldName}).");
            return;
        }
        so.FindProperty(fieldName).objectReferenceValue = clip;
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
        // Landing page + Sunrise Meadows world map (GameState.Idle) — separate track/AudioSource
        // from the gameplay loop above, see AudioManager.OnStateChanged.
        WireAudioClip(go, "_menuMusicClip",  "Assets/Audio/SunriseMeadows_TransitionMusic.mp3");
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

    // ── LevelCompleteManager / LevelFailedManager: celebration + taunt clips ──────

    // Both managers otherwise self-bootstrap at runtime (CatapultLauncher.Awake() adds one if
    // missing — see LevelCompleteManager/LevelFailedManager class comments), but that fallback
    // can't reach into Assets to assign clips, so Wire Scene References does it here instead,
    // the same division of labour as EnsureAudioManager()/WireAudioClip() above.
    static void EnsureLevelCompleteManager()
    {
        var go = GameObject.Find("LevelCompleteManager");
        if (go == null)
        {
            go = new GameObject("LevelCompleteManager");
            Debug.Log("[FarmFury] Created 'LevelCompleteManager' GameObject.");
        }
        if (go.GetComponent<LevelCompleteManager>() == null)
            go.AddComponent<LevelCompleteManager>();

        var so = new SerializedObject(go.GetComponent<LevelCompleteManager>());
        WireArrayElement<VideoClip>(so, "_celebrationClips", (int)AnimalType.Cluck, 8,
            "Assets/Video/Cluck_Celebration.mp4");
        WireArrayElement<AudioClip>(so, "_celebrationAudioClips", (int)AnimalType.Cluck, 8,
            "Assets/Audio/Cluck_CelebratingLaugh.mp3");
        // 2026-07-08: Cluck_Celebration.mp4 was re-exported with its sky backdrop composited in
        // at generation time instead of a green screen (see the "future clips skip green screen"
        // decision in Known Issues) — skip VideoChromaKey's chroma-key shader for it entirely.
        WireBoolArrayElement(so, "_celebrationPlainRender", (int)AnimalType.Cluck, 8, true);
        so.ApplyModifiedProperties();
    }

    static void EnsureLevelFailedManager()
    {
        var go = GameObject.Find("LevelFailedManager");
        if (go == null)
        {
            go = new GameObject("LevelFailedManager");
            Debug.Log("[FarmFury] Created 'LevelFailedManager' GameObject.");
        }
        if (go.GetComponent<LevelFailedManager>() == null)
            go.AddComponent<LevelFailedManager>();

        var so = new SerializedObject(go.GetComponent<LevelFailedManager>());
        // Index 0 = L01 — the only robot taunt clips that exist today; every other level falls
        // back to index 0 at runtime (see LevelFailedManager.GetTauntClip/GetTauntAudioClip).
        WireArrayElement<VideoClip>(so, "_robotTauntClips", 0, 1,
            "Assets/Video/Robot_Celebration.mp4");
        WireArrayElement<AudioClip>(so, "_robotTauntAudioClips", 0, 1,
            "Assets/Audio/Robot_CelebrateSound.mp3");
        so.ApplyModifiedProperties();
    }

    // The chroma-keyed video's transparent areas used to show whatever the frozen gameplay
    // camera happened to be rendering behind it (busy level art) — user-reported as making the
    // character itself look like a translucent "ghost" hidden in the background. A plain sky
    // backdrop behind the video (same asset EnsureBackground() already wires for the live scene,
    // reimported as a Sprite there) gives Cluck/the robot a clean, consistent surface to stand
    // on regardless of which level is paused. Must run after EnsureBackground() so
    // Background_SkyV1's Sprite import settings are already correct by the time this loads it.
    static void EnsureCelebrationVideoBackground()
    {
        var vck = VideoChromaKey.FindOrCreate();
        var so  = new SerializedObject(vck);
        // Was "SkyPainting.png", which doesn't exist on disk — see the identical fix/comment in
        // EnsureBackground() above. This meant _backgroundSprite was never actually wired, so the
        // celebration overlay's transparent regions fell through to the frozen gameplay scene
        // instead of a clean sky, unlike what the class comment on VideoChromaKey describes.
        const string skyPath = "Assets/Sprites/Environment/Skies/Background_SkyV1.png";
        var sky = AssetDatabase.LoadAssetAtPath<Sprite>(skyPath);
        if (sky == null)
        {
            Debug.LogWarning($"[FarmFury] Background_SkyV1.png not found at {skyPath} for CelebrationVideo backdrop.");
            return;
        }
        so.FindProperty("_backgroundSprite").objectReferenceValue = sky;
        // Explicitly re-synced every pass rather than left to the C# field default — `tolerance`
        // is a public (not [SerializeField]-only-default) field, so a value tweaked once in the
        // Inspector during earlier debugging would otherwise silently survive in Game.unity
        // forever regardless of code changes (the project's own documented stale-serialized-value
        // trap). See the comment on VideoChromaKey.tolerance for why 0.20 was chosen.
        so.FindProperty("tolerance").floatValue = 0.20f;
        so.ApplyModifiedProperties();
        Debug.Log("[FarmFury] CelebrationVideo: wired sky backdrop, tolerance=0.20.");
    }

    // Loads an asset of type T and drops it into arrayField[index], growing the array to
    // minSize first if it's smaller (SerializedProperty arrays don't auto-grow). Shared by both
    // Ensure*Manager methods above since they wire the exact same shape (video + audio array,
    // indexed, Inspector-resizable) for two different components.
    static void WireArrayElement<T>(SerializedObject so, string arrayField, int index, int minSize, string path)
        where T : Object
    {
        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset == null)
        {
            Debug.LogWarning($"[FarmFury] {typeof(T).Name} not found at {path}.");
            return;
        }
        var arr = so.FindProperty(arrayField);
        if (arr.arraySize < minSize) arr.arraySize = minSize;
        arr.GetArrayElementAtIndex(index).objectReferenceValue = asset;
        Debug.Log($"[FarmFury] {so.targetObject.GetType().Name}: wired {arrayField}[{index}] <- {path}");
    }

    static void WireBoolArrayElement(SerializedObject so, string arrayField, int index, int minSize, bool value)
    {
        var arr = so.FindProperty(arrayField);
        if (arr.arraySize < minSize) arr.arraySize = minSize;
        arr.GetArrayElementAtIndex(index).boolValue = value;
        Debug.Log($"[FarmFury] {so.targetObject.GetType().Name}: wired {arrayField}[{index}] <- {value}");
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
        SetPrefab(so, "_semiHarvesterPrefab", "SemiHarvesterRobot", "Assets/Prefabs/Enemies", typeof(RobotEnemy));
        SetPrefab(so, "_haybalePrefab",    "HaybaleBlock",    "Assets/Prefabs/Blocks",  typeof(WoodBlock));
        SetPrefab(so, "_barrelPrefab",     "ExplodingBarrelBlock", "Assets/Prefabs/Blocks", typeof(ExplodingBarrelBlock));

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
            // "Block_Wood_Normal.png" never existed on disk, so this was originally pointed at
            // 2D_Block_Wood_Flat.png (2026-07-10) as "the closest existing square wood sprite" —
            // but its actual drawn plank content only fills 86%x21% of its own square canvas
            // (measured via PIL alpha bbox), rendering as a near-invisible thin sliver at a
            // normal 1x1 block size (user-reported "wood... not active when hit", "not rendering
            // as desired"). Re-pointed to Plank_Horizontal.png instead (90%x50% fill — much more
            // substantial) same day. Its native aspect (1.0 : 0.492, i.e. flatter than square)
            // means a plain 1x1 square block request stretches it ~2x vertically via
            // BlockBase.Initialise()'s native-bounds scaling — a mild, acceptable distortion
            // (reads as a slightly thicker plank) traded for actually being visible, and the
            // collider now correctly re-fits to the requested 1x1 regardless (see BlockBase.cs's
            // _col.size fix same day).
            ("_sprNormal",      "Plank_Horizontal.png"),
            ("_sprHorizontal",  "Plank_Horizontal.png"),
            ("_sprVertical",    "2D_Block_Wood_Vertical.png"),
            // WoodDebris.png (broken-splinter burst art) — added 2026-07-10, user-supplied —
            // wired to both the brief pre-death hit-reaction flash (_sprDamaged) and the actual
            // death-burst shown by DestroyBlock() (_sprExplode), same dual-use pattern
            // HaybaleBlock/ExplodingBarrelBlock already use. Previously WoodBlock had neither
            // wired, so every wood plank died via the generic 4-fragment fade instead of a
            // dedicated reaction.
            ("_sprDamaged",     "WoodDebris.png"),
            ("_sprExplode",     "WoodDebris.png"),
        });

        // Was 1.4 (WoodDebris.png is full-bleed, unlike Haybail_Damaged.png's ~92%x83% fill, so
        // it read bigger/more dominant than haybale's burst at the old shared 2.2x default).
        // BlockBase's own default is now a flat 0.5 for every block type (2026-07-10, settled
        // after a brief 1.5 correction was reverted) — WoodBlock matches that same value.
        SetFloatField("Assets/Prefabs/Blocks/WoodBlock.prefab", "_explodeSizeMultiplier", 0.5f);

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
            // _sprExplode reuses the same starburst art as the death-burst VFX (2026-07-26) —
            // at hp=10 a haybale always dies on the hit that damages it, so this is the reaction
            // players actually see, not the brief pre-death _sprDamaged flash above.
            ("_sprExplode",    "Haybail_Damaged.png"),
        });

        // _stayKinematic (2026-07-26): user-reported bug — hitting one haybale in the L01 pile
        // woke ALL FOUR via BlockBase.WakeAllStaticBlocks() (global, not per-instance), so the
        // three untouched bales visibly shifted/tumbled even though nothing hit them directly.
        // Setting this true on the prefab keeps every haybale Static until it's the one actually
        // destroyed — no physical shift, ever.
        //
        // _silentHit + _destroyClipOverride (2026-07-07): every haybail hit is a guaranteed
        // same-frame one-shot kill, so the generic WoodHit + BlockDestroy sounds plus the
        // chicken's own pass-through punch sound (removed from CluckAnimal.cs) all firing at once
        // for one "pop" read as cluttered — user-requested a single dedicated explosion sound.
        var contents = PrefabUtility.LoadPrefabContents(prefabPath);
        var block    = contents.GetComponent<BlockBase>();
        if (block != null)
        {
            var so = new SerializedObject(block);
            so.FindProperty("_stayKinematic").boolValue = true;
            so.FindProperty("_silentHit").boolValue      = true;
            var explodeClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Haybail_Exploding.mp3");
            if (explodeClip != null)
                so.FindProperty("_destroyClipOverride").objectReferenceValue = explodeClip;
            else
                Debug.LogWarning("[FarmFury] Haybail_Exploding.mp3 not found at Assets/Audio/Haybail_Exploding.mp3.");
            so.ApplyModifiedProperties();
            PrefabUtility.SaveAsPrefabAsset(contents, prefabPath);
        }
        PrefabUtility.UnloadPrefabContents(contents);
    }

    // ── ExplodingBarrelBlock: a WoodBlock variant that deals area damage on death ────────
    // WoodenBarrel.png existed in Assets/Sprites/Environment/World1Props/ but had no dedicated
    // gameplay behaviour before — introduced 2026-07-10 at L03 "The Tower" to "gradually
    // introduce the exploding barrel" per user request. LevelLoader picks this prefab when
    // BlockSpawnData.type == BlockType.Barrel.
    static void EnsureExplodingBarrelPrefab()
    {
        const string prefabPath = "Assets/Prefabs/Blocks/ExplodingBarrelBlock.prefab";

        if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null)
        {
            var go   = new GameObject("ExplodingBarrelBlock");
            go.layer = 8; // Block layer
            go.AddComponent<Rigidbody2D>();
            var bc   = go.AddComponent<BoxCollider2D>();
            bc.size  = new Vector2(1f, 1f);
            go.AddComponent<SpriteRenderer>();
            go.AddComponent<ExplodingBarrelBlock>();
            PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
            Object.DestroyImmediate(go);
            Debug.Log("[FarmFury] Created ExplodingBarrelBlock.prefab.");
        }

        WireBlockPrefab(prefabPath, "Assets/Sprites/Environment/World1Props", new[]
        {
            ("_sprNormal",     "WoodenBarrel.png"),
            ("_sprHorizontal", "WoodenBarrel.png"),
            ("_sprVertical",   "WoodenBarrel.png"),
        });

        // Reuses the same comic-burst art/sound RobotEnemy's death explosion uses (Explosion.png
        // + Explosion_Robot.mp3) — a barrel popping should read as an explosion, not the generic
        // flying-fragment default every other WoodBlock uses.
        var contents = PrefabUtility.LoadPrefabContents(prefabPath);
        var block    = contents.GetComponent<BlockBase>();
        if (block != null)
        {
            var so = new SerializedObject(block);
            var explosionSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Environment/World1Props/Explosion.png");
            if (explosionSprite != null)
                so.FindProperty("_sprExplode").objectReferenceValue = explosionSprite;
            var explodeClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Explosion_Robot.mp3");
            if (explodeClip != null)
                so.FindProperty("_destroyClipOverride").objectReferenceValue = explodeClip;
            so.ApplyModifiedProperties();
            PrefabUtility.SaveAsPrefabAsset(contents, prefabPath);
        }
        PrefabUtility.UnloadPrefabContents(contents);
    }

    // Generic float-field override on a prefab's BlockBase (or any Component) — same
    // LoadPrefabContents/SaveAsPrefabAsset shape as EnsureExplodingBarrelPrefab's death-FX
    // wiring above, factored out since WireBlockSprites needs the same pattern for a plain
    // numeric field rather than an asset reference.
    static void SetFloatField(string prefabPath, string fieldName, float value)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null)
        {
            Debug.LogWarning($"[FarmFury] Block prefab not found: {prefabPath}");
            return;
        }
        var contents = PrefabUtility.LoadPrefabContents(prefabPath);
        var block    = contents.GetComponent<BlockBase>();
        if (block != null)
        {
            var so = new SerializedObject(block);
            so.FindProperty(fieldName).floatValue = value;
            so.ApplyModifiedProperties();
            PrefabUtility.SaveAsPrefabAsset(contents, prefabPath);
        }
        PrefabUtility.UnloadPrefabContents(contents);
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
            WireRobotDeathFx(so);
            so.ApplyModifiedProperties();
        }
        PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
        PrefabUtility.UnloadPrefabContents(go);
        Debug.Log("[FarmFury] Robot: wired Robot_Idle.png into _robotSprite (PPU=1746).");
    }

    // Shared death VFX/SFX wiring for all 3 robot prefabs (Robot/HarvesterRobot/
    // SemiHarvesterRobot) — Explosion.png comic burst + Explosion_Robot.mp3, replacing the
    // procedural fragments/RobotDeath jingle fallback in RobotEnemy.DeathSequence(). User-
    // requested 2026-07-09.
    static void WireRobotDeathFx(SerializedObject so)
    {
        var explosion = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Environment/World1Props/Explosion.png");
        if (explosion != null) so.FindProperty("_deathExplosionSprite").objectReferenceValue = explosion;

        var deathClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Explosion_Robot.mp3");
        if (deathClip != null) so.FindProperty("_deathSoundOverride").objectReferenceValue = deathClip;

        // Hit SFX (2026-07-10, user request) — replaces the procedural RobotHit DSP ping
        // entirely (see RobotEnemy.TakeDamage()), same shared wiring across all 3 robot prefabs
        // as the death FX above.
        var hitClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Robot_Hit.mp3");
        if (hitClip != null) so.FindProperty("_hitSoundOverride").objectReferenceValue = hitClip;
        else Debug.LogWarning("[FarmFury] Robot_Hit.mp3 not found at Assets/Audio/Robot_Hit.mp3.");
    }

    // Wires a robot's "damaged" art to BOTH its brief per-hit flash (_robotDamagedSprite) AND
    // its persistent "one hit away from exploding" critical pose (_criticalSprite) in one call —
    // added 2026-07-10 after SemiHarvesterRobot's critical pose was wired by hand and the user
    // asked to "keep it consistent across all robots, also that are still to come". Every robot
    // prefab's damaged-art wiring should call this instead of setting _robotDamagedSprite
    // directly, so a future robot with dedicated damaged art automatically gets the critical
    // pose too with no extra step to remember. No-op (does nothing) if damagedSprite is null —
    // callers already handle that case (e.g. the plain Robot.prefab, which has no damaged art at
    // all yet and falls back to RobotEnemy's plain white-tint flash / no critical pose).
    static void WireRobotDamagedArt(SerializedObject so, Sprite damagedSprite)
    {
        if (damagedSprite == null) return;
        so.FindProperty("_robotDamagedSprite").objectReferenceValue = damagedSprite;
        so.FindProperty("_criticalSprite").objectReferenceValue     = damagedSprite;
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
            WireRobotDamagedArt(so, damagedSprite);
            // _maxHealth: raised 35->40 originally (2026-07-01), LOWERED 40->22 same day
            // 2026-07-10 (user report: "I cannot get past level 2... ease the damage
            // requirements... one hit must cause a chain reaction"), then RAISED back up 22->28,
            // same day third pass (user-reported the combination of that plus the widened
            // chain-reaction blast radius/damage made robots "explode on the slightest of
            // touches" — overcorrected). Expect further tuning passes on this number — the user
            // has explicitly flagged it'll take iteration to land the right strength ratio.
            so.FindProperty("_maxHealth").floatValue = 28f;
            WireRobotDeathFx(so);
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

    // ── SemiHarvesterRobot: create a SEPARATE prefab (distinct from Robot/HarvesterRobot) ───
    // LevelLoader picks this prefab when RobotSpawnData.robotType == RobotType.SemiHarvester.
    // Introduced for L02 (2026-07-09). Both Robot_SemiHarvest.png and its damaged counterpart
    // are the same 500x500 canvas (unlike HarvesterRobot's mismatched normal/damaged sizes), so
    // both use the same PPU=1746 already established for Robot_Idle.png/HarvesterRobot.png —
    // no cross-sprite scale compensation needed. HP=38, between Basic's 35 and Harvester's 40
    // (a mid-tier variant, matching the "Semi-Harvester" name).
    static void EnsureSemiHarvesterRobotPrefab()
    {
        const string prefabPath        = "Assets/Prefabs/Enemies/SemiHarvesterRobot.prefab";
        const string spritePath        = "Assets/Sprites/Enemies/Robot/Robot_SemiHarvest.png";
        const string damagedSpritePath = "Assets/Sprites/Enemies/Robot/Robot_SemiHarvest_Damage.png";

        var imp = AssetImporter.GetAtPath(spritePath) as TextureImporter;
        if (imp == null)
        {
            Debug.LogWarning($"[FarmFury] Robot_SemiHarvest.png not found at {spritePath}.");
            return;
        }
        bool dirty = false;
        if (imp.textureType         != TextureImporterType.Sprite)         { imp.textureType         = TextureImporterType.Sprite;         dirty = true; }
        if (imp.spritePixelsPerUnit != 1746)                                { imp.spritePixelsPerUnit = 1746;                               dirty = true; }
        if (!imp.alphaIsTransparency)                                       { imp.alphaIsTransparency = true;                               dirty = true; }
        if (imp.spriteImportMode    != SpriteImportMode.Single)            { imp.spriteImportMode    = SpriteImportMode.Single;            dirty = true; }
        if (dirty) imp.SaveAndReimport();

        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
        if (sprite == null) { Debug.LogWarning("[FarmFury] Robot_SemiHarvest.png failed to load as Sprite."); return; }

        var dImp = AssetImporter.GetAtPath(damagedSpritePath) as TextureImporter;
        Sprite damagedSprite = null;
        if (dImp == null)
        {
            Debug.LogWarning($"[FarmFury] Robot_SemiHarvest_Damage.png not found at {damagedSpritePath} — hit-flash will fall back to the plain white tint.");
        }
        else
        {
            bool dDirty = false;
            if (dImp.textureType         != TextureImporterType.Sprite)  { dImp.textureType         = TextureImporterType.Sprite;  dDirty = true; }
            if (dImp.spritePixelsPerUnit != 1746)                         { dImp.spritePixelsPerUnit = 1746;                        dDirty = true; }
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
            go       = new GameObject("SemiHarvesterRobot");
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
            WireRobotDamagedArt(so, damagedSprite);
            // 38->20->26 2026-07-10 — see HarvesterRobot's own _maxHealth comment for the full
            // reasoning (same day, same reports, both floors moved together).
            so.FindProperty("_maxHealth").floatValue = 26f;
            WireRobotDeathFx(so);
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
        Debug.Log("[FarmFury] SemiHarvesterRobot.prefab created with Robot_SemiHarvest.png (PPU=1746).");
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
        col.radius = 0.18f;

        var sr = go.AddComponent<SpriteRenderer>();
        // 4 -> 7 (2026-07-10, user-reported "no eggs are firing" — Cluck's sprite pose visibly
        // changed on tap so the ability WAS triggering, but every spawned egg was invisible).
        // Root cause: eggs spawn AT the firing Cluck's own transform.position (SpawnEggs()), and
        // the project's sortingOrder convention (see CLAUDE.md) has animals at 6 but eggs were
        // still at 4 — Cluck's own sprite drew directly OVER every egg at the exact moment they
        // appeared, since they occupied the same screen position. 7 (above every other layer in
        // the convention) guarantees eggs are always visible in front of the bird that fired
        // them, not hidden behind it.
        sr.sortingOrder = 7;

        // Egg.png art + scale (2026-07-10, user-supplied — previously no sprite was ever wired
        // here at all, so eggs rendered invisible). Cluck's PPU convention (2057, set by
        // SpriteWiring for the whole Characters/Cluck folder) makes Egg.png's native size a tiny
        // 0.14x0.19 world units at scale 1 — nowhere near "scaled with Cluck" (the launched bird
        // itself renders at ~4.92x via CatapultLauncher.BirdScale). 2.3x lands the egg at a
        // visible ~0.32x0.44 world-unit footprint — a proportionate "small egg relative to a
        // full-size chicken" rather than same-size or near-invisible.
        const string eggSpritePath = "Assets/Sprites/Characters/Cluck/Egg.png";
        var eggSprite = AssetDatabase.LoadAssetAtPath<Sprite>(eggSpritePath);
        if (eggSprite != null)
        {
            sr.sprite = eggSprite;
            sr.color  = Color.white;
            go.transform.localScale = new Vector3(2.3f, 2.3f, 1f);
        }
        else
        {
            sr.color = new Color(1f, 0.95f, 0.7f); // fallback tint if Egg.png isn't imported yet
            Debug.LogWarning($"[FarmFury] {eggSpritePath} not found — egg will render as a plain circle.");
        }

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

    // Placeholder ground/grass visual (2026-07-26) — L01 shipped with NO ground art at all:
    // EnsureGround()'s collider is deliberately invisible (see above), and no one ever
    // hand-authored a replacement, so the sky backdrop ran straight to the bottom of the
    // screen with nothing showing where solid ground is. User-reported symptom: haybails/
    // HarvesterRobot/a landing Cluck all looked like they were "sinking" or "falling through
    // the floor" near the bottom of the screen. Root cause confirmed NOT a physics bug —
    // Ground's collider/Rigidbody2D/layer setup are all correct — it's that the camera's
    // visible range at rest (Y -6.5 to +2.5, see PositionCamera()) already clips 0.1 units
    // above the true ground surface (-6.60), so anything settling near true ground level
    // visually vanishes off the bottom edge with no ground graphic to anchor it.
    // This is a stand-in tinted strip, not final art: its rendered TOP edge sits at Y=-5.3
    // (matching where hand-placed props like OldBarn_Right/GnarledTree/WoodenFence/the
    // robot already visually rest, per their scene transforms) and extends down to Y=-12,
    // well past the visible frame in any orientation. sortingOrder=-1 keeps it behind every
    // gameplay object (decorative props use <=1, blocks=2, robots=3, cannon=4, animals=6 —
    // see the sortingOrder rule in CLAUDE.md) while still in front of the sky (-100).
    // DELETE this GameObject once real ground/grass art is authored — this method only
    // creates it if missing, so removing "GroundVisual_Placeholder" from the scene once
    // real art exists is enough to stop it coming back.
    static void EnsureGroundVisual()
    {
        const float VisualTop    = -5.3f;
        const float VisualBottom = -12f;
        float height = VisualTop - VisualBottom;
        float centerY = (VisualTop + VisualBottom) / 2f;

        var go = GameObject.Find("GroundVisual_Placeholder");
        if (go == null)
        {
            go = new GameObject("GroundVisual_Placeholder");
            Debug.Log("[FarmFury] Created 'GroundVisual_Placeholder' ground/grass stand-in — replace with real art, then delete this GameObject.");
        }
        go.transform.position   = new Vector3(0f, centerY, 0f);
        go.transform.localScale = new Vector3(40f, height, 1f);

        var sr = go.GetComponent<SpriteRenderer>();
        if (sr == null) sr = go.AddComponent<SpriteRenderer>();
        if (sr.sprite == null)
        {
            var tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            sr.sprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), Vector2.one * 0.5f, 1f);
        }
        sr.color         = new Color(0.36f, 0.56f, 0.24f);
        sr.sortingOrder  = -1;
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
