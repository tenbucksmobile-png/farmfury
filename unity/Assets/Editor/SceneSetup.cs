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
    // GameObject.Find() ONLY searches ACTIVE objects — a real bug found 2026-07-26 (user report:
    // "we have erased the barn and all the scenery from world 1, and nothing has rendered
    // correctly from world 2"). Root cause: PreviewCannonAndScenery (below) sets
    // Scenery_World1/Scenery_World2 inactive via SetActive() as part of previewing the other
    // world, and if a scene save happened while one of them was inactive, every SUBSEQUENT Wire
    // Scene References pass's `GameObject.Find("Scenery_World2")` call in EnsureWorld2Scenery
    // silently returned null (object exists, just inactive) — which that method then "fixed" by
    // creating a whole NEW "Scenery_World2" parent + a fresh set of default-positioned children,
    // leaving the ORIGINAL hand-built one (with everything the user had actually placed) sitting
    // inactive and orphaned. This happened THREE times before being caught, hence three duplicate
    // Scenery_World2 hierarchies existing at once. This helper searches the whole scene hierarchy
    // (including inactive objects) by name instead, so a toggled-off group is still found.
    static GameObject FindIncludingInactive(string name)
    {
        foreach (var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (root.name == name) return root;
            var t = root.transform.Find(name); // Transform.Find DOES include inactive children
            if (t != null) return t.gameObject;
            // Find() only checks direct children by that exact path — recurse for safety in case
            // a matching name is nested deeper than one level under some root.
            foreach (var child in root.GetComponentsInChildren<Transform>(includeInactive: true))
                if (child.name == name) return child.gameObject;
        }
        return null;
    }

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
        EnsureWorld2Scenery();  // Scenery_World2 GO: hand-authored igloo/ice-tree background props
        EnsureEggPrefab();      // Create Egg prefab + wire into CluckAnimal prefab
        EnsureAnimalPrefabs();  // Create Percy/Woolly/Ducky/Horace/Gerald/Billy prefabs
        EnsureHUD();            // HUDController GO (builds Canvas at runtime)
        EnsureGameFlow();       // GameFlow GO: franchise menus (Landing/World/Level/Character Select), MenuArt wired
        EnsureAudioManager();   // AudioManager GO wired with music/cannon/falling clips
        EnsureLevelCompleteManager();  // Cluck celebration video + laugh audio
        EnsureLevelFailedManager();    // Robot taunt video + taunt audio
        EnsureWorldTransitionManager(); // L18 -> Frozen Tundra transition video
        EnsureCelebrationVideoBackground(); // Sky backdrop behind both of the above (shared overlay)
        WireGameManager();      // _levels array
        WireLevelLoader();      // 8 animal prefab refs + block/robot + 2 parent transforms
        WireBessieAudio();      // BessieAnimal.prefab: _earthquakeClip <- Bessie_Earthquake.mp3
        WireLauncher();         // CatapultLauncher + LevelLoader ref + counterweight sprite
        WireRobotSprite();                // Robot_Idle.png → Robot prefab SpriteRenderer
        EnsureHarvesterRobotPrefab();  // Create/update HarvesterRobot.prefab (separate from Robot)
        EnsureSemiHarvesterRobotPrefab();  // Create/update SemiHarvesterRobot.prefab (separate from Robot/HarvesterRobot)
        EnsureCommanderRobotPrefab();  // Create/update CommanderRobot.prefab (L18 boss)
        EnsureFrostRobotPrefab();            // World 2 reskin of RobotType.Basic
        EnsureIceHarvestorRobotPrefab();     // World 2 reskin of RobotType.Harvester
        EnsureGlacierHarvestorRobotPrefab(); // World 2 reskin of RobotType.SemiHarvester
        EnsureHaybaleBlockPrefab();    // Create/update HaybaleBlock.prefab (WoodBlock + Haybail.png art)
        EnsureExplodingBarrelPrefab(); // Create/update ExplodingBarrelBlock.prefab (WoodBlock + Barrel_Dynamite.png art, area damage on death)
        WireBlockSprites();     // Art sprites into WoodBlock + StoneBlock prefabs
        PositionCamera();       // Move camera to see the play area
        // Runs AFTER PositionCamera() so the Edit-mode preview (RescaleToCamera(), also called at
        // runtime on every OnLevelStarted) sizes against the camera's known rest framing.
        EnsureEnvironmentDepthSystem(); // 3-layer FarHills/Midground/Foreground parallax stack, clipped via FarmFury/ParallaxBandClip
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
        // "GroundVisual_Placeholder" was a procedurally-generated flat grass-strip stand-in
        // (EnsureGroundVisual(), removed 2026-07-17) — the user deleted it by hand once the real
        // ParallaxMidground/Foreground art took over showing ground-level detail, but the old
        // Ensure* method unconditionally recreated it on every Wire Scene References pass since it
        // had no way to know the deletion was deliberate. The method itself is gone now; this just
        // mops up one already-existing leftover instance so it doesn't linger from a scene saved
        // before this fix.
        foreach (var placeholderName in new[] { "Cluck_Loaded_0", "Cluck_InFlight", "HarvesterRobot", "SemiHarvesterRobot", "CommanderRobot", "LevelScratch", "GroundVisual_Placeholder" })
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
            Debug.Log("[FarmFury] Deleted obsolete 'LevelSelect' GameObject (superseded by GameFlow).");
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

        // World 2 (Frozen Tundra) ground-seam ice dressing — added 2026-07-19. Mapped from the
        // spec's generic names ("Ice Block / Packed Snow / Icicle Spike") to the closest actual
        // filenames under World2Props/ — several similarly-named files exist there
        // (IceBlock.png vs Ice_block.png, Snowdrift.png vs Snow_block.png/Snow_short.png) that
        // may be gameplay block art rather than decorative scatter props; this mapping is a best
        // guess, not confirmed with the user — flag/correct if the wrong asset got picked.
        const string ice2PropsFolder = "Assets/Sprites/Environment/World2Props";
        WireProp(so, "_sprIceBlock",    "IceBlock.png",   ice2PropsFolder);
        WireProp(so, "_sprPackedSnow",  "Snowdrift.png",  ice2PropsFolder);
        WireProp(so, "_sprIcicleSpike", "IceSpire.png",   ice2PropsFolder);

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

    // ── World 2 (Frozen Tundra) hand-authored background scenery ────────────────
    // Added 2026-07-26, user request: "remove the sprites and add in the igloo, ice cannon and
    // other built sprites" — mirrors World 1's Scenery_World1 group (OldBarn_Right/OakTree/
    // WoodenFence etc., hand-placed by the user directly in-Editor) with a World-2-themed
    // equivalent, EXCEPT this one is created by code, not hand-placed, since no Play-mode/
    // Scene-view access exists in this environment to place it visually. IceCannon.png is NOT
    // duplicated here — it's the real gameplay launcher, already reskinned automatically by
    // CatapultLauncher._cannonSpriteW2 (see that class), not a decorative prop.
    //
    // POSITIONS WERE A FIRST DRAFT (computed, not visually placed) — the user has since opened
    // this in-Editor and hand-tuned it directly (2026-07-26): repositioned/rescaled Igloo,
    // IceTree, IceCrystals, IceSpire, and BuriedRuin, and DELETED "IceTree (1)" and "IceBoulder"
    // entirely (didn't want a second tree or the boulder). Both removed from the list below to
    // match — leaving them in would have silently recreated them on the next Wire Scene
    // References pass, since MakeWorld2Prop's find-or-create-by-name has no memory of a
    // deliberate deletion. The 5 remaining props' hand-tuned positions in Game.unity are now the
    // real ground truth; the values below only matter the first time this group doesn't exist at
    // all (e.g. a fresh checkout) — MakeWorld2Prop never touches an already-existing GameObject's
    // transform (see its own comment).
    //
    // ToggleHandAuthoredScenery (SceneryBuilder.cs) shows/hides this group vs. Scenery_World1
    // based on LevelData.world on every level load — both groups share the same world-space
    // region (World 2 levels reuse World 1's camera/launcher coordinates), so without that toggle
    // they'd render on top of each other regardless of which level is active.
    static void EnsureWorld2Scenery()
    {
        var parent = FindIncludingInactive("Scenery_World2");
        bool isNew = parent == null;
        if (isNew)
        {
            parent = new GameObject("Scenery_World2");
            Debug.Log("[FarmFury] Created 'Scenery_World2' GameObject.");
        }

        const string folder = "Assets/Sprites/Environment/World2Props";

        // (name, file, worldX, targetHeight) — targetHeight is the desired WORLD-SPACE height in
        // units; width follows automatically from the source art's own aspect ratio (no stretch).
        // Y is derived per-prop below: the two background structures (Igloo/IceTree) float at a
        // fixed Y like Scenery_World1's barn/tree do (artistic background placement, not physically
        // grounded); the smaller clutter pieces are bottom-anchored to GroundSurface instead
        // (SceneryBuilder.GroundSurface = -6.60), matching how actual ground-level props read.
        (string name, string file, float x, float targetHeight, bool groundAnchored)[] props =
        {
            ("Igloo",        "Igloo.png",        -5.5f, 5.5f, false), // background structure, mirrors OldBarn_Right's role/position
            ("IceTree",      "IceTree.png",       -3.3f, 6.0f, false), // mirrors OakTree's role/position
            ("BuriedRuin",    "BuriedRuin.png",     5.6f, 3.0f, true),  // ground-level ruin accent near the level's typical content span
            ("IceSpire",      "IceSpire.png",      -1.2f, 1.8f, true),  // ground clutter near the launcher, mirrors WoodenBarrel's scale/role
            ("IceCrystals",   "IceCrystals.png",   -0.4f, 1.3f, true),
        };

        foreach (var (name, file, x, targetHeight, groundAnchored) in props)
            MakeWorld2Prop(parent.transform, name, $"{folder}/{file}", x, targetHeight, groundAnchored);
    }

    const float World2SceneryGroundSurface = -6.60f; // matches SceneryBuilder.GroundSurface

    // Find-or-create a single decorative prop GameObject under `parent`. Only sets position/scale
    // ON FIRST CREATION (isNew) — same "don't fight the user's own later hand-tuning" convention
    // used everywhere else in this file (see FarmCannon's own comment) — once someone has dragged
    // this prop to a better spot in-Editor, re-running Wire Scene References must never snap it
    // back. The sprite reference itself IS always re-wired (cheap, and picks up art replacements).
    static void MakeWorld2Prop(Transform parent, string name, string spritePath, float x, float targetHeight, bool groundAnchored)
    {
        var imp = AssetImporter.GetAtPath(spritePath) as TextureImporter;
        if (imp == null)
        {
            Debug.LogWarning($"[FarmFury] World2 scenery: sprite not found at {spritePath}.");
            return;
        }
        bool dirty = false;
        if (imp.textureType         != TextureImporterType.Sprite)          { imp.textureType         = TextureImporterType.Sprite;          dirty = true; }
        if (imp.spritePixelsPerUnit != 512)                                  { imp.spritePixelsPerUnit = 512;                                 dirty = true; }
        if (!imp.alphaIsTransparency)                                        { imp.alphaIsTransparency = true;                                dirty = true; }
        if (imp.spriteImportMode    != SpriteImportMode.Single)             { imp.spriteImportMode    = SpriteImportMode.Single;             dirty = true; }
        if (dirty) imp.SaveAndReimport();

        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
        if (sprite == null) { Debug.LogWarning($"[FarmFury] {spritePath} failed to load as Sprite."); return; }

        var existing = parent.Find(name);
        bool isNew = existing == null;
        var go = isNew ? new GameObject(name) : existing.gameObject;
        if (isNew) go.transform.SetParent(parent, false);

        var sr = go.GetComponent<SpriteRenderer>();
        if (sr == null) sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = 0; // matches Scenery_World1's own hand-placed props (OldBarn_Right/OakTree)

        if (isNew)
        {
            // native size at PPU=512, then scaled so the rendered sprite's height == targetHeight
            // (width follows the same uniform scale, preserving the source art's own aspect).
            Vector2 native = sprite.bounds.size;
            float   scale  = native.y > 0.0001f ? targetHeight / native.y : 1f;
            float   worldHeight = native.y * scale;
            float   y = groundAnchored ? World2SceneryGroundSurface + worldHeight * 0.5f
                                        : -3.2f; // rough background-structure height, mirrors Scenery_World1's barn/tree Y range (-2.07 to -2.8)
            go.transform.localPosition = new Vector3(x, y, 0f);
            go.transform.localScale    = new Vector3(scale, scale, 1f);
        }
    }

    // ── Environment depth system: 3-layer parallax stack (rebuilt 2026-07-18) ────────
    // History: a single frozen Layer_Midground (hand-placed/computed once, never touched again)
    // repeatedly failed to cover the camera at every per-level zoom — see CLAUDE.md's earlier
    // 2026-07-18 entries — so it was deleted and `EnsureBackground()` briefly took over showing
    // ParallaxMidground.png full-screen via BackgroundController's proven per-frame cover-scale.
    // Rebuilt AGAIN the same day per explicit user spec: three full-bleed opaque paintings
    // (ParallaxFarHills/Midground/Foreground.png, each a complete scene including its own sky),
    // stacked like theatre flats via the existing `FarmFury/ParallaxBandClip` shader — each layer
    // only reveals its own bottom [0, ClipAbove] UV band (see EnvironmentDepthSystem.cs and the
    // shader source for the exact math), so a nearer layer's transparent upper region lets the
    // layer behind it show through. Unlike the earlier frozen version, THIS layer's transform is
    // recomputed fresh every single Wire Scene References pass (Editor-time preview) AND every
    // runtime OnLevelStarted (EnvironmentDepthSystem.RescaleToCamera(), see that class) — no
    // isNew guard, no hand-placed literal to go stale, because the whole point this time is to
    // track the camera's REAL current orthoSize/aspect (CatapultLauncher clamps orthoSize
    // 4.5-8.0 per level) rather than freeze at whatever it happened to be once. `Background`
    // reverted to plain Background_SkyV1.png (see EnsureBackground() below) as a defensive
    // fallback sky BEHIND this stack — normally fully hidden, since FarHillsClipAbove defaults to
    // 1.0 (fully uncropped, its own painted sky fills whatever's left at the top), but still there
    // if any of the 3 new sprites ever fails to load.
    static void EnsureEnvironmentDepthSystem()
    {
        var go = GameObject.Find("EnvironmentDepthSystem");
        bool isNewComponent = false;
        if (go == null)
        {
            go = new GameObject("EnvironmentDepthSystem");
            Debug.Log("[FarmFury] Created 'EnvironmentDepthSystem' GameObject.");
        }
        var eds = go.GetComponent<EnvironmentDepthSystem>();
        if (eds == null)
        {
            eds = go.AddComponent<EnvironmentDepthSystem>();
            isNewComponent = true;
        }

        const string skiesFolder = "Assets/Sprites/Environment/Skies";
        var so = new SerializedObject(eds);

        WireParallaxPainting(so, "_sprFarHills",   "ParallaxFarHills.png",   skiesFolder);
        WireParallaxPainting(so, "_sprMidground",  "ParallaxMidground.png", skiesFolder);
        WireParallaxPainting(so, "_sprForeground", "ParallaxForeground.png", skiesFolder);

        // World 2 (Frozen Tundra) backdrop — added 2026-07-19. Note the actual filename is
        // "ParallaxMidGrounds_W2.png" (capital G, trailing 's') per the user-supplied asset, not
        // "ParallaxMidground_W2.png" — matched exactly here. No World-2 Foreground painting exists
        // or is wired (see EnvironmentDepthSystem.ApplyWorldLayers — Layer_Foreground is simply
        // deactivated for World 2, not given art).
        WireParallaxPainting(so, "_sprFarHillsW2",  "ParallaxFarHills_W2.png",   skiesFolder);
        WireParallaxPainting(so, "_sprMidgroundW2", "ParallaxMidGrounds_W2.png", skiesFolder);

        // ClipAbove defaults are only stamped once, on first creation — same "don't fight the
        // user's own later tuning" convention used everywhere else in this file (see FarmCannon's
        // fallback stamp above) — these 3 fields are deliberately exposed as live Inspector
        // sliders specifically so the split points can be re-tuned without re-exporting art;
        // stomping them back to these defaults on every Wire Scene References pass would defeat
        // that. FarHills=1.0 (fully uncropped — its own sky fills the top); Midground=0.23 is
        // measured, not guessed (see EnvironmentDepthSystem.cs's field comment — pixel-sampled
        // against the source art; the uniform clip line must clear the LOWEST dip in the hill
        // ridge, not the highest peak, which is lower than the windmill's own blade-tip height —
        // a real, confirmed tradeoff, not a bug); Foreground=0.30 is a starting band, not yet
        // similarly measured — tune live in Play mode or the Inspector, no re-export needed.
        if (isNewComponent)
        {
            so.FindProperty("_farHillsClipAbove").floatValue   = 1.0f;
            so.FindProperty("_midgroundClipAbove").floatValue  = 0.23f;
            so.FindProperty("_foregroundClipAbove").floatValue = 0.30f;
            // World 2 defaults — see EnvironmentDepthSystem.cs's field comment for the pixel
            // measurement behind these values (ParallaxMidGrounds_W2.png's own baked-in aurora sky
            // must never be revealed; FarHills_W2 supplies all sky content instead, same division
            // of responsibility as World 1's FarHills/Midground).
            so.FindProperty("_farHillsClipAboveW2").floatValue  = 1.0f;
        }
        // _midgroundClipAboveW2 is deliberately UNCONDITIONAL (not gated by isNewComponent like
        // its siblings above) — 2026-07-26, user request: "too much night sky in the parallax."
        // This is a real, one-time re-tune (0.21 -> 0.60, see the field's own comment in
        // EnvironmentDepthSystem.cs for the seam tradeoff this knowingly accepts), not a live-
        // tunable Inspector default any more — re-stamping it here every Wire Scene References
        // pass keeps it from silently reverting if the scene's serialized component ever predates
        // this change. Safe to hand-tune further in the Inspector; only this specific historical
        // value is force-applied.
        so.FindProperty("_midgroundClipAboveW2").floatValue = 0.60f;

        so.ApplyModifiedProperties();

        // Editor-time preview (Awake()/RescaleToCamera() only run in Play mode) — mirrors
        // EnvironmentDepthSystem.CoverFit() exactly so the Scene/Game view shows the correctly
        // sized stack even before pressing Play, and after every Wire Scene References run, not
        // just once. PositionCamera() must run before this in WireAll()'s call order.
        var cam = Object.FindAnyObjectByType<Camera>();
        if (cam != null)
        {
            CoverFitLayer(go.transform, "Layer_FarHills",   (Sprite)so.FindProperty("_sprFarHills").objectReferenceValue,   -40, cam);
            CoverFitLayer(go.transform, "Layer_Midground",  (Sprite)so.FindProperty("_sprMidground").objectReferenceValue,  -30, cam);
            CoverFitLayer(go.transform, "Layer_Foreground", (Sprite)so.FindProperty("_sprForeground").objectReferenceValue, -20, cam);
        }

        ApplyClipEditor(go.transform, "Layer_FarHills",   so.FindProperty("_farHillsClipAbove").floatValue);
        ApplyClipEditor(go.transform, "Layer_Midground",  so.FindProperty("_midgroundClipAbove").floatValue);
        ApplyClipEditor(go.transform, "Layer_Foreground", so.FindProperty("_foregroundClipAbove").floatValue);

        // Always resets the cannon + hand-authored scenery back to World 1 alongside the backdrop
        // above, same "every Wire Scene References pass re-forces World 1" convention that block
        // already followed — added 2026-07-26 (user report: the World 1 cannon was still visibly
        // showing while looking at World 2 in the Scene view). Without this, running Wire Scene
        // References after a "Preview World 2 Backdrop" pass reset the BACKDROP to World 1 but
        // left the cannon/scenery stuck showing whichever world was last manually previewed —
        // exactly the inconsistent state that caused the report. Use the Preview World 2 Backdrop
        // menu item to see World 2's version of all three together.
        PreviewCannonAndScenery(isWorld2: false);

        Debug.Log("[FarmFury] EnvironmentDepthSystem wired (3-layer FarHills/Midground/Foreground stack).");
    }

    // Lets the World 2 backdrop be seen and worked against directly in the Scene view WITHOUT
    // Play mode and without any real World 2 LevelData existing yet — added 2026-07-19, user
    // question: "how do i see the background layout for world 2 so I can build sprites - like
    // the barn, cannon etc - currently I only see world1?" EnvironmentDepthSystem only ever shows
    // World 2's art at runtime when a level with world=2 actually loads (HandleLevelStarted), and
    // no such LevelData exists yet, so there was previously no way to see it at all. Reuses
    // CoverFitLayer/ApplyClipEditor directly (same "Awake() doesn't run in Edit mode" reason
    // EnsureEnvironmentDepthSystem's own preview above has to bypass the runtime component's
    // private fields) rather than calling EnvironmentDepthSystem.PreviewWorld() — that method
    // only actually does anything once Awake() has populated its private layer Transform
    // references, i.e. in Play mode. Purely visual: touches no level data, camera rest position,
    // or anything "Wire Scene References" itself tracks — running that command again always
    // re-forces World 1's sprites back via the preview above, so there's nothing to remember to
    // reset by hand after using this.
    [MenuItem("FarmFury/Debug/Preview World 2 Backdrop (Editor)")]
    public static void PreviewWorld2Backdrop() => PreviewWorldBackdrop(2);

    [MenuItem("FarmFury/Debug/Preview World 1 Backdrop (Editor)")]
    public static void PreviewWorld1Backdrop() => PreviewWorldBackdrop(1);

    static void PreviewWorldBackdrop(int world)
    {
        var go = GameObject.Find("EnvironmentDepthSystem");
        if (go == null)
        {
            Debug.LogWarning("[FarmFury] 'EnvironmentDepthSystem' not found in the open scene — run Wire Scene References first.");
            return;
        }
        var eds = go.GetComponent<EnvironmentDepthSystem>();
        if (eds == null) return;
        var so = new SerializedObject(eds);

        bool isWorld2 = world == 2;
        string farHillsField  = isWorld2 ? "_sprFarHillsW2"  : "_sprFarHills";
        string midgroundField = isWorld2 ? "_sprMidgroundW2" : "_sprMidground";
        float farHillsClip    = so.FindProperty(isWorld2 ? "_farHillsClipAboveW2"  : "_farHillsClipAbove").floatValue;
        float midgroundClip   = so.FindProperty(isWorld2 ? "_midgroundClipAboveW2" : "_midgroundClipAbove").floatValue;
        var farHillsSprite    = (Sprite)so.FindProperty(farHillsField).objectReferenceValue;
        var midgroundSprite   = (Sprite)so.FindProperty(midgroundField).objectReferenceValue;

        var cam = Object.FindAnyObjectByType<Camera>();
        if (cam != null)
        {
            CoverFitLayer(go.transform, "Layer_FarHills",  farHillsSprite,  -40, cam);
            CoverFitLayer(go.transform, "Layer_Midground", midgroundSprite, -30, cam);
        }
        ApplyClipEditor(go.transform, "Layer_FarHills",  farHillsClip);
        ApplyClipEditor(go.transform, "Layer_Midground", midgroundClip);

        // World 2 has no opaque Foreground painting by design (see EnvironmentDepthSystem's
        // class comment) — hide the Foreground layer for this preview so it doesn't sit in front
        // of the World 2 art showing World 1's foreground painting; restore it for World 1.
        var foregroundGO = go.transform.Find("Layer_Foreground");
        if (foregroundGO != null) foregroundGO.gameObject.SetActive(!isWorld2);

        PreviewCannonAndScenery(isWorld2);

        Debug.Log($"[FarmFury] Previewing World {world} backdrop (+ cannon, scenery) in the Scene view.");
    }

    // Edit-mode preview for the two other world-swapped visuals besides the backdrop: FarmCannon's
    // sprite (CatapultLauncher._cannonSR only swaps this at runtime, from OnLevelStarted — an
    // event that never fires in Edit mode) and Scenery_World1/Scenery_World2's visibility
    // (SceneryBuilder.ToggleHandAuthoredScenery has the same runtime-only gap). Added 2026-07-26
    // (user report: the World 1 cannon/barn were still visibly showing while building/looking at
    // World 2 in the Scene view) — called both from the manual Preview World N Backdrop menu
    // items and unconditionally at the end of EnsureEnvironmentDepthSystem's own always-resets-
    // to-World-1 wiring pass, so the cannon/scenery never drift out of sync with the backdrop.
    static void PreviewCannonAndScenery(bool isWorld2)
    {
        var cannonGO = FindIncludingInactive("FarmCannon");
        if (cannonGO != null)
        {
            var cannonSR = cannonGO.GetComponent<SpriteRenderer>();
            if (cannonSR != null)
            {
                var w1Cannon = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Environment/Launchers/Cannon.png");
                var w2Cannon = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Environment/Launchers/IceCannon.png");
                cannonSR.sprite = (isWorld2 && w2Cannon != null) ? w2Cannon : w1Cannon;
            }
        }
        var scenery1 = FindIncludingInactive("Scenery_World1");
        if (scenery1 != null) scenery1.SetActive(!isWorld2);
        var scenery2 = FindIncludingInactive("Scenery_World2");
        if (scenery2 != null) scenery2.SetActive(isWorld2);
    }

    // Finds-or-creates one parallax layer child, wires its sprite + the shared ParallaxBandClip
    // material, and cover-fits it to the camera's CURRENT orthoSize/aspect/position — called
    // unconditionally every pass (see EnsureEnvironmentDepthSystem's comment on why this one is
    // deliberately NOT an isNew-guarded hand-placement fallback like FarmCannon/the old
    // Layer_Midground).
    static void CoverFitLayer(Transform parent, string name, Sprite sprite, int sortingOrder, Camera cam)
    {
        var existing = parent.Find(name);
        GameObject layerGO = existing != null ? existing.gameObject : new GameObject(name);
        if (existing == null) layerGO.transform.SetParent(parent, false);

        var sr = layerGO.GetComponent<SpriteRenderer>();
        if (sr == null) sr = layerGO.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = sortingOrder;
        var mat = EnsureParallaxBandClipMaterial();
        if (mat != null) sr.sharedMaterial = mat;

        if (sprite == null) return;
        Vector2 native = sprite.bounds.size; // world-space size at localScale = 1
        if (native.x <= 0f || native.y <= 0f) return;

        float camH = cam.orthographicSize * 2f;
        float camW = camH * (cam.aspect > 0f ? cam.aspect : 16f / 9f);
        // 'Cover' scale — fills the camera with no gaps at any aspect ratio. At the widest
        // per-level zoom (orthoSize 8.0, e.g. L18) this scales the source paintings up somewhat
        // past their native resolution — an accepted tradeoff for a single static painting (not a
        // seamless tile), not a bug, unless it visibly softens.
        float scale = Mathf.Max(camW / native.x, camH / native.y);
        var camPos = cam.transform.position;
        layerGO.transform.position   = new Vector3(camPos.x, camPos.y, layerGO.transform.position.z);
        layerGO.transform.localScale = new Vector3(scale, scale, 1f);
    }

    static void ApplyClipEditor(Transform parent, string name, float clipAbove)
    {
        var existing = parent.Find(name);
        if (existing == null) return;
        var sr = existing.GetComponent<SpriteRenderer>();
        if (sr == null) return;
        var mpb = new MaterialPropertyBlock();
        sr.GetPropertyBlock(mpb);
        mpb.SetFloat("_ClipAbove", clipAbove);
        sr.SetPropertyBlock(mpb);
    }

    // Single shared Material asset (not an ephemeral runtime-only instance) so the layers render
    // correctly while just editing the scene, not only during Play. Created once at
    // Assets/Materials/ParallaxBandClip.mat; the per-layer band cutoff is NOT baked into this
    // shared asset (that would make every layer clip identically) — it's applied per-renderer via
    // a MaterialPropertyBlock (ApplyClipEditor above / EnvironmentDepthSystem's runtime
    // equivalent), which is safe to set at Editor time (persists with the scene) and safe to
    // overwrite every frame at runtime (never dirties the shared asset itself, unlike mutating
    // sharedMaterial directly).
    static Material EnsureParallaxBandClipMaterial()
    {
        const string matFolder = "Assets/Materials";
        const string matPath   = matFolder + "/ParallaxBandClip.mat";
        if (!AssetDatabase.IsValidFolder(matFolder))
            AssetDatabase.CreateFolder("Assets", "Materials");

        var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (mat == null)
        {
            var shader = Shader.Find("FarmFury/ParallaxBandClip");
            if (shader == null)
            {
                Debug.LogWarning("[FarmFury] Shader 'FarmFury/ParallaxBandClip' not found — EnvironmentDepthSystem layers will render untextured in Edit mode.");
                return null;
            }
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, matPath);
            Debug.Log("[FarmFury] Created shared material 'ParallaxBandClip.mat'.");
        }
        return mat;
    }

    // Same PPU=100 convention as Background_SkyV1 (both are full-camera-cover backdrops, not
    // PPU=512 world props) — but with quality import settings tuned for these specifically:
    // painted sky gradients band visibly under default/crunched compression, so this forces
    // CompressedHQ with crunch off and a max size comfortably above the source art's native
    // resolution (so Unity never downsamples before the cover-scale enlarges it per-level).
    static void WireParallaxPainting(SerializedObject so, string field, string filename, string folder)
    {
        string path = $"{folder}/{filename}";
        var imp = AssetImporter.GetAtPath(path) as TextureImporter;
        if (imp != null)
        {
            bool dirty = false;
            if (imp.textureType         != TextureImporterType.Sprite)          { imp.textureType         = TextureImporterType.Sprite;          dirty = true; }
            if (imp.spritePixelsPerUnit != 100)                                  { imp.spritePixelsPerUnit = 100;                                 dirty = true; }
            if (imp.spriteImportMode    != SpriteImportMode.Single)              { imp.spriteImportMode    = SpriteImportMode.Single;             dirty = true; }
            if (imp.alphaIsTransparency)                                         { imp.alphaIsTransparency = false;                               dirty = true; } // opaque paintings, no alpha channel to preserve
            if (imp.textureCompression  != TextureImporterCompression.CompressedHQ) { imp.textureCompression = TextureImporterCompression.CompressedHQ; dirty = true; }
            if (imp.crunchedCompression)                                         { imp.crunchedCompression = false;                               dirty = true; } // crunch reintroduces banding on gradient skies
            if (imp.maxTextureSize      < 4096)                                  { imp.maxTextureSize      = 4096;                                dirty = true; } // never downsample before the cover-scale enlarges it per-level
            if (imp.mipmapEnabled)                                               { imp.mipmapEnabled       = false;                               dirty = true; } // full-screen cover backdrop, never viewed minified
            if (dirty) imp.SaveAndReimport();
        }

        var sp = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sp == null)
        {
            Debug.LogWarning($"[FarmFury] EnvironmentDepthSystem: painting not found at {path}");
            return;
        }
        so.FindProperty(field).objectReferenceValue = sp;
        Debug.Log($"[FarmFury] EnvironmentDepthSystem: {field} → {filename}");
    }

    // ── Sky backdrop ─────────────────────────────────────────────────────────────

    static void EnsureBackground()
    {
        // Prefer the user's authored sky GO; delete any stale code-created duplicate.
        var skyVariant = GameObject.Find("Background_SkyV1")
                      ?? GameObject.Find("Background_Sky");
        // NOT GameObject.Find("Background") — VideoChromaKey.FindOrCreate() builds its own child
        // GameObject also literally named "Background" (its celebration-overlay backdrop Image),
        // and once this method's own rename below ("Background_SkyV1" -> "Background") has run
        // once, Find("Background") becomes genuinely ambiguous between the two on every
        // subsequent Wire Scene References pass — whichever Unity's internal traversal order
        // happens to return could silently attach BackgroundController (a SpriteRenderer-cover-
        // scale script) to VideoChromaKey's UI Image instead of the real world sky. Root cause
        // of a real bug found 2026-07-17: that UI child ended up with a stray BackgroundController
        // and its Image.sprite never got wired (VideoChromaKey wires that separately, at runtime),
        // so it rendered as a plain white box until Play-mode script explicitly hid it. Scoped
        // instead to a root-level (no parent) Transform with no RectTransform — the real world
        // sky is always a scene-root SpriteRenderer object, never a UI child.
        GameObject codeBg = null;
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
        {
            if (t.name != "Background" || t.parent != null) continue;
            if (t.GetComponent<RectTransform>() != null) continue; // UI object — not the real sky
            codeBg = t.gameObject;
            break;
        }

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

        // Points at ParallaxFarHills.png — was Background_SkyV1.png until 2026-07-18, when that
        // file turned out to have been deleted from disk at some point during the same session
        // (confirmed via a direct folder listing, not assumed — Assets/Sprites/Environment/Skies/
        // now only contains the 3 new Parallax paintings). Since EnvironmentDepthSystem's 3-layer
        // stack renders in front of this GameObject and (with FarHillsClipAbove defaulting to 1.0,
        // fully uncropped) normally fully covers it anyway, this is purely a defensive fallback —
        // still auto-covered every frame by BackgroundController's proven ScaleToFillCamera()/
        // LateUpdate() — so pointing it at the same painting as the top parallax layer means that
        // IF it ever becomes visible (a load failure, or the razor-thin soft-edge fade at exactly
        // UV.y=1.0 the ParallaxBandClip shader applies even at ClipAbove=1 — see that shader's own
        // comment), it matches rather than seams against what's supposed to be showing anyway.
        const string skyPath = "Assets/Sprites/Environment/Skies/ParallaxFarHills.png";

        // Quality import settings tuned for this painting (opaque, no alpha channel; painted sky
        // gradients band visibly under default/crunched compression) — same settings
        // WireParallaxPainting() uses for this exact file when EnsureEnvironmentDepthSystem wires
        // it as the top parallax layer.
        var skyImp = AssetImporter.GetAtPath(skyPath) as TextureImporter;
        if (skyImp != null)
        {
            bool dirty = false;
            if (skyImp.textureType         != TextureImporterType.Sprite)             { skyImp.textureType         = TextureImporterType.Sprite;             dirty = true; }
            if (skyImp.spritePixelsPerUnit != 100)                                     { skyImp.spritePixelsPerUnit = 100;                                     dirty = true; }
            if (skyImp.spriteImportMode    != SpriteImportMode.Single)                 { skyImp.spriteImportMode    = SpriteImportMode.Single;                 dirty = true; }
            if (skyImp.alphaIsTransparency)                                            { skyImp.alphaIsTransparency = false;                                   dirty = true; } // opaque painting, no alpha channel to preserve
            if (skyImp.textureCompression  != TextureImporterCompression.CompressedHQ) { skyImp.textureCompression  = TextureImporterCompression.CompressedHQ; dirty = true; }
            if (skyImp.crunchedCompression)                                           { skyImp.crunchedCompression = false;                                   dirty = true; } // crunch reintroduces banding on gradient skies
            if (skyImp.maxTextureSize      < 4096)                                     { skyImp.maxTextureSize      = 4096;                                    dirty = true; }
            if (skyImp.mipmapEnabled)                                                  { skyImp.mipmapEnabled       = false;                                   dirty = true; } // full-screen cover backdrop, never viewed minified
            if (dirty) { skyImp.SaveAndReimport(); Debug.Log("[FarmFury] ParallaxFarHills.png re-imported as Sprite (PPU=100, CompressedHQ)."); }
        }
        else
            Debug.LogWarning($"[FarmFury] ParallaxFarHills.png not found at {skyPath}.");

        var skySprite = AssetDatabase.LoadAssetAtPath<Sprite>(skyPath);
        if (skySprite != null)
        {
            var so = new SerializedObject(bc);
            so.FindProperty("_skySprite").objectReferenceValue = skySprite;
            so.ApplyModifiedProperties();
            Debug.Log("[FarmFury] Background: ParallaxFarHills.png wired (defensive fallback).");
        }
        else
        {
            Debug.LogWarning($"[FarmFury] ParallaxFarHills.png not found at {skyPath}.");
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
        // Third source (2026-11-02): the franchise animal cards (Stampede stand-ins), which cover all eight.
        var franchiseGuids = AssetDatabase.FindAssets("t:Sprite", new[] { FranchiseArt + "/Cards" });
        int wired = 0;
        for (int i = 0; i < CardKeywords.Length; i++)
        {
            string kw = CardKeywords[i].ToLower();
            Sprite spr = FindSpriteByKeyword(guids, kw) ?? FindSpriteByKeyword(matchUpGuids, kw) ?? FindSpriteByKeyword(franchiseGuids, kw);
            if (spr != null)
            {
                arr.GetArrayElementAtIndex(i).objectReferenceValue = spr;
                wired++;
            }
        }
        // Franchise flow (2026-11-02): the end-of-level panels and Quit/Mute moved to GameFlow — only the pause
        // button's art is left on the HUD.
        WireSprite(so, "_pauseButtonSprite",   "Assets/Sprites/UI/Icon/Btn_pause.png");
        WireSprite(so, "_coinSprite",          FranchiseArt + "/Shop/Coin_UI.png");

        so.ApplyModifiedProperties();

        if (wired > 0)
            Debug.Log($"[FarmFury] HUD: wired {wired}/8 card sprites from {cardsFolder} / {matchUpFolder}.");
        else
            Debug.LogWarning($"[FarmFury] HUD: no card sprites found in {cardsFolder} or {matchUpFolder}. " +
                             "Copy assets/FarmCards/*.png there first.");
    }

    // ── MainMenuController ────────────────────────────────────────────────────────

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
    // Since 2026-11-02 (franchise save model) progress lives in SaveManager's FF_ keys by levelId; this clears
    // those plus the pre-port ff_*_N keys. Coins and purchases are kept.
    [MenuItem("FarmFury/Reset Progress")]
    public static void ResetWorldMapProgress()
    {
        var levels = AssetDatabase.FindAssets("t:LevelData", new[] { "Assets/ScriptableObjects/Levels" })
            .Select(g => AssetDatabase.LoadAssetAtPath<LevelData>(AssetDatabase.GUIDToAssetPath(g)))
            .Where(l => l != null)
            .OrderBy(l => l.name, System.StringComparer.Ordinal)
            .ToList();
        FarmFury.Core.SaveManager.ClearProgressKeys(levels);
        Debug.Log($"[FarmFury] Cleared progress for {levels.Count} levels — next launch starts from a fresh save (coins/purchases kept).");
    }

    // ── GameFlow: franchise menus (2026-11-02) ───────────────────────────────
    // Replaces the MainMenu / WorldMap / World2Landing / World2Map GameObjects (deleted here if a scene still has
    // them). GameFlow builds its screens in code at runtime; this only creates the GO and wires MenuArt — FarmFury's
    // own buttons/poster where they exist, FarmFury_Stampede stand-ins (Assets/Sprites/UI/Franchise/) otherwise —
    // and each WorldData's badge / Level Select / Character Select art. Re-running re-stamps all of it.
    const string FranchiseArt = "Assets/Sprites/UI/Franchise";

    static void EnsureGameFlow()
    {
        foreach (var legacy in new[] { "MainMenu", "WorldMap", "World2Landing", "World2Map" })
        {
            var old = FindIncludingInactive(legacy);
            if (old != null)
            {
                Object.DestroyImmediate(old);
                Debug.Log($"[FarmFury] Deleted legacy menu GameObject '{legacy}' (replaced by GameFlow).");
            }
        }

        var go = FindIncludingInactive("GameFlow");
        if (go == null)
        {
            go = new GameObject("GameFlow");
            Debug.Log("[FarmFury] Created 'GameFlow' GameObject.");
        }
        var flow = go.GetComponent<FarmFury.UI.GameFlow>();
        if (flow == null) flow = go.AddComponent<FarmFury.UI.GameFlow>();

        var so = new SerializedObject(flow);
        const string icon = "Assets/Sprites/UI/Icon";
        const string fa = FranchiseArt;
        WireSprite(so, "_menuArt.landingPoster",   "Assets/Sprites/UI/LandingPage.png");
        WireSprite(so, "_menuArt.playButton",      icon + "/Btn_play.png");
        WireSprite(so, "_menuArt.exitButton",      fa + "/Menu/Exit.png");
        WireSprite(so, "_menuArt.settingsButton",  icon + "/Btn_settings.png");
        WireSprite(so, "_menuArt.homeButton",      icon + "/Btn_home.png");
        WireSprite(so, "_menuArt.backButton",      icon + "/Btn_back.png");
        WireSprite(so, "_menuArt.quitButton",      icon + "/Btn_quit.png");
        WireSprite(so, "_menuArt.pauseButton",     icon + "/Btn_pause.png");
        WireSprite(so, "_menuArt.worldSelectBackground",     fa + "/Canvas/Canvas.png");
        WireSprite(so, "_menuArt.worldSelectBanner",         fa + "/Menu/WorldUnlocked.png");
        WireSprite(so, "_menuArt.levelTileLocked",           fa + "/Menu/LevelTile_Locked.png");
        WireSprite(so, "_menuArt.levelTileNext",             fa + "/Menu/LevelTile_question.png");
        WireSprite(so, "_menuArt.bossShield",                fa + "/Menu/Boss_Shield.png");
        WireSprite(so, "_menuArt.oneStarBoard",              fa + "/Menu/LevelWin_1.png");
        WireSprite(so, "_menuArt.twoStarBoard",              fa + "/Menu/LevelComplete_2.png");
        WireSprite(so, "_menuArt.threeStarBoard",            fa + "/Menu/LevelComplete_3.png");
        WireSprite(so, "_menuArt.characterSelectBackground", fa + "/Canvas/CharacterCanvas_Wide.png");
        WireSprite(so, "_menuArt.pauseBackground",           fa + "/Canvas/Pause_Canvas_Wide.png");
        WireSprite(so, "_menuArt.levelCompleteBackground",   fa + "/Canvas/LevelComplete_Canvas_Wide.png");
        WireSprite(so, "_menuArt.levelFailedBackground",     fa + "/Canvas/LevelFailed_Canvas_Wide.png");
        WireSprite(so, "_menuArt.levelCompleteStarEmpty",    fa + "/Menu/LevelComplete_StarEmpty.png");
        WireSprite(so, "_menuArt.newCharacterBackground",    fa + "/Canvas/NewCharacter_Plain_Wide.png");
        WireSprite(so, "_menuArt.newCharacterTitle",         fa + "/Menu/NewCharacterTitle.png");
        WireSprite(so, "_menuArt.newCharacterSign",          fa + "/Menu/NewCharacter.png");
        WireSprite(so, "_menuArt.worldUnlockedSign",         fa + "/Menu/WorldUnlocked.png");

        // Framed animal cards, indexed by AnimalType (Stampede's file names).
        var cards = new[] { "Cluck_Chicken", "Bessie_Cow", "Percy_Pig", "Wooly_Sheep", "Ducky_Duck", "Horace_Horse", "Gerald_Turkey", "Billy_Goat" };
        var cardsProp = so.FindProperty("_menuArt.animalCards");
        cardsProp.arraySize = cards.Length;
        for (int i = 0; i < cards.Length; i++)
            cardsProp.GetArrayElementAtIndex(i).objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(fa + "/Cards/" + cards[i] + ".png");
        so.ApplyModifiedProperties();

        // Robots tab of Character Story: FarmFury's own robot sprites, RobotType order.
        var robots = new[] { "Robot_Pawn", "HarvesterRobot", "Robot_SemiHarvest", "Commander" };
        var robotsProp = so.FindProperty("_menuArt.robotPortraits");
        robotsProp.arraySize = robots.Length;
        for (int i = 0; i < robots.Length; i++)
            robotsProp.GetArrayElementAtIndex(i).objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Enemies/Robot/" + robots[i] + ".png");

        // ShopArt (Settings / Shop overlays) and LeaderboardArt — Stampede's file table (StampedeShopArt /
        // StampedeLeaderboardArt), copied into Franchise/Shop, Franchise/Leaderboard, Franchise/WorldNames.
        string shop = fa + "/Shop/";
        WireSprite(so, "_shopArt.pageBackground",     fa + "/Canvas/Settings_Canvas_Wide.png");
        var shopFields = new System.Collections.Generic.Dictionary<string, string>
        {
            { "settingsSign", "SettingsSign" }, { "shopSign", "ShopBanner" }, { "merchBanner", "MerchBanner" },
            { "legalSign", "Legal" }, { "leaderboardSign", "Leaderboard" },
            { "musicIcon", "Btn_music-remove" }, { "leaderboardIcon", "Btn_LeaderBoard" },
            { "characterStoryIcon", "Btn_CharacterStory" }, { "policiesIcon", "Policies" },
            { "cashIcon", "Shop" }, { "worldsIcon", "WorldMaze" }, { "removeAdsIcon", "Ads" }, { "cosmeticsIcon", "Cosmetics_Icon" },
            { "plaque", "Btn_plaque" }, { "worldPrice", "3.99" }, { "hatsBanner", "Hats&Caps" }, { "trailsBanner", "Trails" },
            { "machinesBanner", "machine" }, { "ownedBadge", "EquippedBadge_Icon" }, { "purchaseComplete", "PurchaseComplete" },
            { "coinIcon", "Coin_UI" }, { "yesButton", "Yes" }, { "noButton", "No" }, { "revivePanel", "Revive Prompt panel background" },
            { "watchAd", "WatchAd" }, { "doubleCoins", "DoubleCoins" }, { "useCoinsSign", "UseCoins" },
            { "lockerIcon", "Locker" }, { "lockerBanner", "LockerBanner" }, { "lockerSuggestion", "LockerAd" },
            { "cardFrame", "PurchaseCardFrame" }, { "promoNewWorlds", "Promo_NewWorlds" }, { "promoNoAds", "Promo_NoAds" },
        };
        foreach (var kv in shopFields)
            WireSprite(so, "_shopArt." + kv.Key, shop + kv.Value + ".png");
        WireSpriteArray(so, "_shopArt.coinPacks", shop, new[] { "100", "500", "5000", "15000" });
        WireSpriteArray(so, "_shopArt.hatItems", shop, new[] { "sombrero_price", "baseball_price", "cowboy_price", "ChefHat_price", "Crown_price" });
        WireSpriteArray(so, "_shopArt.trailItems", shop, new[] { "RainbowRibbon_price", "SparkleDust_Price", "CornHusk_price", "EmberTrail_price", "Confetti_price", "Bubble_price" });
        WireSpriteArray(so, "_shopArt.machineItems", shop, new[] { "Price_Clucky_truck", "Price_Bessie_truck", "Price_Horace_truck" });
        WireSpriteArray(so, "_shopArt.abilityIcons", shop, new[] { "Cluck_ability", "Bessie_ability", "Percy_ability", "Woolly_ability", "Ducky_ability", "Horace_ability", "Gerald_ability", "Billy_ability" });
        WireSpriteArray(so, "_shopArt.cosmeticIcons", shop, new[] { "Sombrero", "BaseballHat", "CowboyHat", "ChefHat", "CrownHat", "Ribbon", "SparkleDust", "CornHusk", "Ember", "Confetti", "bubbles", "CluckyTruck", "BessieTruck", "HoraceTruck" });

        WireSprite(so, "_leaderboardArt.background",       fa + "/Canvas/Settings_Canvas_Wide.png");
        WireSprite(so, "_leaderboardArt.bestLabel",        fa + "/Leaderboard/Best.png");
        WireSprite(so, "_leaderboardArt.highScoreLabel",   fa + "/Leaderboard/HighScore.png");
        WireSprite(so, "_leaderboardArt.fastestTimeLabel", fa + "/Leaderboard/FastestTime.png");
        WireSprite(so, "_leaderboardArt.coin",             fa + "/Leaderboard/Collectable Coin.png");
        var logo = AssetDatabase.LoadAssetAtPath<Sprite>(fa + "/Menu/FF_Logo.png");   // FarmFury logo: not made yet
        so.FindProperty("_leaderboardArt.logo").objectReferenceValue = logo;
        WireSpriteArray(so, "_leaderboardArt.worldNames", fa + "/WorldNames/",
            new[] { "MeadowRuins", "FrozenTundra", "WatermillVillage", "SkyIslands", "SunkenCity", "MotherShip", "DustbowlCanyon", "HarvestFairground", "CropFactory" });
        so.ApplyModifiedProperties();

        WireWorldMenuArt();
    }

    static void WireSpriteArray(SerializedObject so, string field, string folder, string[] files)
    {
        var prop = so.FindProperty(field);
        prop.arraySize = files.Length;
        int found = 0;
        for (int i = 0; i < files.Length; i++)
        {
            var spr = AssetDatabase.LoadAssetAtPath<Sprite>(folder + files[i] + ".png");
            prop.GetArrayElementAtIndex(i).objectReferenceValue = spr;
            if (spr != null) found++;
        }
        Debug.Log($"[FarmFury] Wired {field}: {found}/{files.Length} sprites from {folder}");
    }

    // Per-world menu art on each WorldData asset: badge, Level Select backdrop, and (non-Meadow) Stampede's
    // per-world results / New Character / Character canvases. A slot is only stamped when its file exists.
    static void WireWorldMenuArt()
    {
        var names = new System.Collections.Generic.Dictionary<WorldType, string[]>
        {
            // badge file, Level Select backdrop file, per-world canvas prefix (null = use the shared MenuArt ones)
            { WorldType.MeadowRuins,       new[] { "Badge_Meadowruins",       "MeadowRuins",       null } },
            { WorldType.FrozenTundra,      new[] { "Badge_FrozenTundra",      "FrozenTundra",      "FT" } },
            { WorldType.WatermillVillage,  new[] { "Badge_WatermillVillage",  "WatermillVillage",  "WV" } },
            { WorldType.SkyIslands,        new[] { "Badge_SkyWorld",          "SkyIsland",         "SI" } },
            { WorldType.SunkenCity,        new[] { "Badge_SunkenCity",        "SunkenCity",        "SC" } },
            { WorldType.RobotMothership,   new[] { "Badge_Mothership",        "Mothership",        "RM" } },
            { WorldType.DustbowlCanyon,    new[] { "Badge_DustbowlCanyon",    "DustbowlCanyon",    "DC" } },
            { WorldType.HarvestFairground, new[] { "Badge_HarvestFairground", "HarvestFairground", "HF" } },
            { WorldType.CropFactory,       new[] { "Badge_CropFactory",       "CropFactory",       "CF" } },
        };
        if (WorldDataGenerator.LoadAll().Length == 0) WorldDataGenerator.GenerateAll();
        int wired = 0;
        foreach (var world in WorldDataGenerator.LoadAll())
        {
            if (!names.TryGetValue(world.worldType, out var n)) continue;
            var wso = new SerializedObject(world);
            SetWorldSprite(wso, "selectBadge",           FranchiseArt + "/Badges/" + n[0] + ".png");
            SetWorldSprite(wso, "levelSelectBackground", FranchiseArt + "/LevelSelect/" + n[1] + ".png");
            if (n[2] != null)
            {
                SetWorldSprite(wso, "levelCompleteBackground",   FranchiseArt + "/Canvas/" + n[2] + "_LevelComplete_Canvas_Wide.png");
                SetWorldSprite(wso, "levelFailedBackground",     FranchiseArt + "/Canvas/" + n[2] + "_LevelFailed_Canvas_Wide.png");
                SetWorldSprite(wso, "newCharacterBackground",    FranchiseArt + "/Canvas/" + n[2] + "_NewCharacter_Canvas_Wide.png");
                SetWorldSprite(wso, "characterSelectBackground", FranchiseArt + "/Canvas/" + n[2] + "_Character_Canvas_Wide.png");
            }
            wso.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(world);
            wired++;
        }
        AssetDatabase.SaveAssets();
        Debug.Log($"[FarmFury] World menu art wired on {wired} WorldData assets.");
    }

    static void SetWorldSprite(SerializedObject wso, string field, string path)
    {
        var spr = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (spr != null) wso.FindProperty(field).objectReferenceValue = spr;
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
        WireAudioClip(go, "_bessieFallingClip", "Assets/Audio/Bessie_falling.mp3");
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

    // World-ending transition video (currently only L18 -> "Frozen Tundra", see
    // WorldTransitionManager) — level index 17 (0-based, L18) -> TransitionVideo_Draft.mp4.
    static void EnsureWorldTransitionManager()
    {
        var go = GameObject.Find("WorldTransitionManager");
        if (go == null)
        {
            go = new GameObject("WorldTransitionManager");
            Debug.Log("[FarmFury] Created 'WorldTransitionManager' GameObject.");
        }
        if (go.GetComponent<WorldTransitionManager>() == null)
            go.AddComponent<WorldTransitionManager>();

        var so = new SerializedObject(go.GetComponent<WorldTransitionManager>());
        WireIntArrayElement(so, "_triggerLevelIndices", 0, 1, 17);
        WireArrayElement<VideoClip>(so, "_transitionClips", 0, 1,
            "Assets/Video/TransitionVideo_Draft.mp4");
        so.ApplyModifiedProperties();
    }

    // The chroma-keyed video's transparent areas used to show whatever the frozen gameplay
    // camera happened to be rendering behind it (busy level art) — user-reported as making the
    // character itself look like a translucent "ghost" hidden in the background. A plain sky
    // backdrop behind the video gives Cluck/the robot a clean, consistent surface to stand on
    // regardless of which level is paused — same ParallaxFarHills.png `EnsureBackground()` wires
    // as its own defensive fallback sky (reimported as a Sprite there first).
    static void EnsureCelebrationVideoBackground()
    {
        var vck = VideoChromaKey.FindOrCreate();
        var so  = new SerializedObject(vck);
        // Was Background_SkyV1.png until 2026-07-18, when that file turned out to have been
        // deleted from disk at some point during the same session — see EnsureBackground()'s own
        // comment for the full explanation. Switched to ParallaxFarHills.png for the same reason.
        const string skyPath = "Assets/Sprites/Environment/Skies/ParallaxFarHills.png";
        var sky = AssetDatabase.LoadAssetAtPath<Sprite>(skyPath);
        if (sky == null)
        {
            Debug.LogWarning($"[FarmFury] ParallaxFarHills.png not found at {skyPath} for CelebrationVideo backdrop.");
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

        // VideoChromaKey.Play()/Stop() explicitly SetActive(false) on both the "Background" and
        // "RawImage" children at runtime — but that only ever runs once a script actually
        // executes (Play mode). The Editor-saved scene had both left active-by-default with a
        // blank Image.sprite, which rendered as a plain white box any time the scene was viewed
        // without pressing Play (real user report, 2026-07-17 — see EnsureBackground()'s comment
        // for the related root-cause bug that let BackgroundController attach to this same
        // object). Force both inactive here too so the saved state already matches what Play mode
        // would immediately do anyway.
        foreach (var childName in new[] { "Background", "RawImage" })
        {
            var child = vck.transform.Find(childName);
            if (child != null && child.gameObject.activeSelf)
            {
                child.gameObject.SetActive(false);
                Debug.Log($"[FarmFury] CelebrationVideo: '{childName}' set inactive (idle state, shown only during Play()).");
            }
        }

        // One-time cleanup: before EnsureBackground()'s GameObject.Find("Background") ambiguity
        // fix (2026-07-17), this exact "Background" child could get mistaken for the real world
        // sky and have a BackgroundController (a SpriteRenderer-cover-scale script — meaningless
        // on a UI Image) erroneously attached to it. Removes any leftover instance of that mistake
        // from a scene saved before the fix; harmless no-op once already clean.
        var celebrationBg = vck.transform.Find("Background");
        if (celebrationBg != null)
        {
            var strayBc = celebrationBg.GetComponent<BackgroundController>();
            if (strayBc != null)
            {
                Object.DestroyImmediate(strayBc);
                Debug.Log("[FarmFury] CelebrationVideo: removed stray BackgroundController from 'Background' child (leftover from the Find(\"Background\") ambiguity bug).");
            }
        }

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

    static void WireIntArrayElement(SerializedObject so, string arrayField, int index, int minSize, int value)
    {
        var arr = so.FindProperty(arrayField);
        if (arr.arraySize < minSize) arr.arraySize = minSize;
        arr.GetArrayElementAtIndex(index).intValue = value;
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

        // Franchise save/data layer (2026-11-02): DataManager + SaveManager live on the GameManager GO
        // (DontDestroyOnLoad). World list from WorldDataGenerator — generated on first run.
        var worlds = WorldDataGenerator.LoadAll();
        if (worlds.Length == 0)
        {
            WorldDataGenerator.GenerateAll();
            worlds = WorldDataGenerator.LoadAll();
        }
        var dm = gm.GetComponent<FarmFury.Core.DataManager>();   // not ?? — Unity's fake-null defeats it
        if (dm == null) dm = gm.gameObject.AddComponent<FarmFury.Core.DataManager>();
        if (gm.GetComponent<FarmFury.Core.SaveManager>() == null) gm.gameObject.AddComponent<FarmFury.Core.SaveManager>();
        if (gm.GetComponent<FarmFury.Core.IAPManager>() == null) gm.gameObject.AddComponent<FarmFury.Core.IAPManager>();
        if (gm.GetComponent<FarmFury.Core.AdManager>() == null) gm.gameObject.AddComponent<FarmFury.Core.AdManager>();
        if (gm.GetComponent<FarmFury.Core.AnalyticsManager>() == null) gm.gameObject.AddComponent<FarmFury.Core.AnalyticsManager>();
        var dso = new SerializedObject(dm);
        var wprop = dso.FindProperty("_worlds");
        wprop.arraySize = worlds.Length;
        for (int i = 0; i < worlds.Length; i++)
            wprop.GetArrayElementAtIndex(i).objectReferenceValue = worlds[i];
        dso.ApplyModifiedProperties();
        Debug.Log($"[FarmFury] DataManager: wired {worlds.Length} WorldData assets.");
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
        SetPrefab(so, "_commanderPrefab",     "CommanderRobot",     "Assets/Prefabs/Enemies", typeof(RobotEnemy));
        SetPrefab(so, "_robotPrefabW2",         "FrostRobot",            "Assets/Prefabs/Enemies", typeof(RobotEnemy));
        SetPrefab(so, "_harvesterPrefabW2",     "IceHarvestorRobot",     "Assets/Prefabs/Enemies", typeof(RobotEnemy));
        SetPrefab(so, "_semiHarvesterPrefabW2", "GlacierHarvestorRobot", "Assets/Prefabs/Enemies", typeof(RobotEnemy));
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

        // [SerializeField] stale-value trap (see CLAUDE.md) — the Game.unity scene GO already had
        // its own serialized launch-speed values baked in from before, which changing the C# class
        // defaults alone would never retroactively update. Re-synced explicitly here so each
        // launch-dynamics change actually takes effect in the live scene, not just for freshly-
        // created GameObjects. Raised 3.5/8.5 -> 3.8/9.3 (2026-07-13, alongside the FarmCannon
        // reposition — see the field comment in CatapultLauncher.cs) to keep max-power shots able
        // to reach and clear the tallest/farthest structures (including L18's boss) from the
        // cannon's new, further-back position.
        so.FindProperty("_minLaunchSpeed").floatValue = 3.8f;
        so.FindProperty("_maxLaunchSpeed").floatValue = 9.3f;

        // ── Farm Cannon (2026-07-02, replaces the trebuchet visual system) ──────
        // Delete the old trebuchet scene GOs — CatapultLauncher no longer references them
        // (no _armSpriteGO/_swingSpriteGO/_trebuchetXSprite fields exist any more).
        foreach (var n in new[] { "Trabuchet_Body", "Trabuchet_Arm", "Trabuchet_Swing" })
        {
            var old = GameObject.Find(n);
            if (old != null) { Object.DestroyImmediate(old); Debug.Log($"[FarmFury] Deleted trebuchet GO '{n}'."); }
        }

        var cannonGO = FindIncludingInactive("FarmCannon");
        if (cannonGO == null)
        {
            cannonGO = new GameObject("FarmCannon");
            Debug.Log("[FarmFury] Created 'FarmCannon' GameObject.");
            // Only stamped on first creation — a real bug hit live 2026-07-13 (and again
            // 2026-07-17, while hand-composing the scene against the new backdrop) where this ran
            // UNCONDITIONALLY on every Wire Scene References pass, silently reverting any manual
            // reposition back to this fallback the instant setup ran again. Matches
            // CatapultLauncher.BuildCannon()'s own runtime creation logic, which was already
            // correctly conditional — SceneSetup just hadn't matched it. If the cannon needs a new
            // DEFAULT (for a brand-new scene with no FarmCannon yet), change the values below; an
            // already-existing, hand-placed FarmCannon is never touched here again.
            // Re-synced 2026-07-18 to the barn-door-aligned position from the 2026-07-17 session
            // (previously this literal was still the pre-alignment -7.54/-5.03 value, stale ever
            // since the "only stamp on creation" fix stopped this line from ever running again on
            // the live scene's already-existing FarmCannon — so it silently drifted out of sync
            // with reality and would have recreated the WRONG cannon position had the GameObject
            // ever been deleted).
            cannonGO.transform.position   = new Vector3(-3.72f, -4.7032f, 0f);
            cannonGO.transform.localScale = new Vector3(1.4711188f, 1.3316845f, 1f);
        }

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

        // World 2 (Frozen Tundra) reskin — visual sprite swap only, see CatapultLauncher's own
        // _cannonSpriteW2 field comment. Same import settings as World 1's Cannon.png above.
        const string iceCannonPath = "Assets/Sprites/Environment/Launchers/IceCannon.png";
        if (AssetDatabase.LoadAssetAtPath<Texture2D>(iceCannonPath) != null)
        {
            var imp = AssetImporter.GetAtPath(iceCannonPath) as TextureImporter;
            if (imp != null)
            {
                bool dirty = false;
                if (imp.textureType         != TextureImporterType.Sprite) { imp.textureType         = TextureImporterType.Sprite; dirty = true; }
                if (!imp.alphaIsTransparency)                               { imp.alphaIsTransparency = true;                       dirty = true; }
                if (imp.spriteImportMode    != SpriteImportMode.Single)    { imp.spriteImportMode    = SpriteImportMode.Single;    dirty = true; }
                if (dirty) imp.SaveAndReimport();
            }
            so.FindProperty("_cannonSpriteW2").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<Sprite>(iceCannonPath);
            Debug.Log("[FarmFury] Launcher: IceCannon.png wired (World 2, Single mode).");
        }
        else
            Debug.LogWarning("[FarmFury] IceCannon.png not found at " + iceCannonPath);

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
            // Named-shape slots — added 2026-07-12 (user report: "the sprites that I made the
            // scene with are not the correct ones... too many gaps"). Every one of these shapes
            // was previously collapsing into one of the 3 generic sprites above regardless of
            // which was actually placed in the Scene view — see WoodArtVariant's expansion
            // comment in LevelData.cs for the full root-cause explanation.
            ("_sprShort",         "Plank_Short.png"),
            ("_sprSkew",          "Plank_Skew.png"),
            ("_sprDiagonal",      "Plank_Diagonal.png"),
            ("_sprVerticalShort", "Plank_VeriticalShort.png"),
            ("_sprHorizontal2D",  "Plank_2DHorizontal.png"),
            ("_sprShork2D",       "Plank_2DShork.png"),
            ("_sprShork",         "Plank_Shork.png"),
            ("_sprCart",          "WoodenCart.png"),
            ("_sprBarrelProp",    "WoodenBarrel.png"),
            ("_sprFlat",          "2D_Block_Wood_Flat.png"),
            // WoodDebris.png (broken-splinter burst art) — added 2026-07-10, user-supplied —
            // wired to both the brief pre-death hit-reaction flash (_sprDamaged) and the actual
            // death-burst shown by DestroyBlock() (_sprExplode), same dual-use pattern
            // HaybaleBlock/ExplodingBarrelBlock already use. Previously WoodBlock had neither
            // wired, so every wood plank died via the generic 4-fragment fade instead of a
            // dedicated reaction.
            ("_sprDamaged",     "WoodDebris.png"),
            ("_sprExplode",     "WoodDebris.png"),
        });

        // World 2 (Frozen Tundra) reskin — see BlockBase's _sprNormalW2 etc. field comment for
        // why only these 5 shapes are wired (the only ones with real ice/snow art today).
        // Different source folder than the W1 wiring above, so a separate WireBlockPrefab call.
        const string folderW2 = "Assets/Sprites/Environment/World2Props";
        WireBlockPrefab("Assets/Prefabs/Blocks/WoodBlock.prefab", folderW2, new[]
        {
            ("_sprNormalW2",     "Ice_block.png"),
            ("_sprVerticalW2",   "Ice_vertical.png"),
            ("_sprHorizontalW2", "Snow_block.png"),
            ("_sprDiagonalW2",   "Ice_diagonal.png"),
            ("_sprShortW2",      "Snow_short.png"),
        });

        // Was 1.4 (WoodDebris.png is full-bleed, unlike Haybail_Damaged.png's ~92%x83% fill, so
        // it read bigger/more dominant than haybale's burst at the old shared 2.2x default).
        // BlockBase's own default is now a flat 0.5 for every block type (2026-07-10, settled
        // after a brief 1.5 correction was reverted) — WoodBlock matches that same value.
        SetFloatField("Assets/Prefabs/Blocks/WoodBlock.prefab", "_explodeSizeMultiplier", 0.5f);

        // Re-sync _areaDamage to the CURRENT WoodBlock class default (25f) — 2026-07-10, found
        // during a "review all our damage changes" audit: [SerializeField] fields on an
        // ALREADY-EXISTING prefab keep whatever value was serialized the first time the prefab
        // was created, even after the C# field's own default= changes (the documented
        // "[SerializeField] stale value trap"). WoodBlock.prefab had been sitting at a stale 20
        // from an early balance pass, silently un-synced through every later "20->35->25" chain-
        // damage retune — every spawned Wood block was using the OLD 20, not the intended 25.
        // Explicitly re-writing it here every "Wire Scene References" pass means future code
        // default changes can never silently drift out of sync with the live prefab again.
        SetFloatField("Assets/Prefabs/Blocks/WoodBlock.prefab", "_areaDamage", 25f);

        // Switched from Block_Stone_Normal.png 2026-07-12 (user report: "the stone_block is
        // rendering as the old 3d block... this seems to be for all levels"). Root cause:
        // Block_Stone_Normal.png doesn't exist anywhere in the project any more (confirmed via a
        // full search) — WireBlockPrefab's per-field lookup silently skips (warns, doesn't clear)
        // any field whose target file can't be found, so every "Wire Scene References" pass left
        // StoneBlock.prefab's stale, previously-serialized sprite untouched instead of failing
        // loudly. Now wired to Stone_Block.png/Stone_Vertical.png, the actual flat 2D art the
        // user has been placing in every level's Scene view dump since the L01-L18 overhaul
        // (matches the WoodBlock convention: _sprVertical picked automatically for tall/thin
        // aspect ratios via BlockBase.Initialise()'s Auto variant).
        WireBlockPrefab("Assets/Prefabs/Blocks/StoneBlock.prefab", folder, new[]
        {
            ("_sprNormal",      "Stone_Block.png"),
            ("_sprHorizontal",  "Stone_Block.png"),   // reuse until a dedicated wide/flat stone asset exists
            ("_sprVertical",    "Stone_Vertical.png"),
            // Named-shape slots — same 2026-07-12 fix as WoodBlock above. Skew/Diagonal share an
            // enum case with WoodBlock's own fields of the same name, but resolve to Stone's own
            // sprites here since each class has its own separate serialized field.
            ("_sprSkew",        "Stone_Skew.png"),
            ("_sprDiagonal",    "Stone_Diagonal.png"),
            ("_sprSquare",      "Stone_Square.png"),
            ("_sprBlock",       "Stone_Block.png"),
            ("_sprRuinedWall",  "RuinedStoneWall.png"),
            ("_sprTower",       "StoneTower.png"),
            // StoneDebris.png (broken-rubble burst art, user-supplied 2026-08-03) — wired to both
            // the brief pre-death hit-reaction flash (_sprDamaged) and the actual death-burst
            // shown by DestroyBlock() (_sprExplode), same dual-use pattern WoodBlock uses with
            // WoodDebris.png. Deliberately a separate asset, not a reuse of WoodDebris.png — a
            // shattering stone chunk should not look like broken wood splinters.
            ("_sprDamaged",     "StoneDebris.png"),
            ("_sprExplode",     "StoneDebris.png"),
        });

        // World 2 reskin — same 5 shapes/source as WoodBlock's own W2 wiring above.
        WireBlockPrefab("Assets/Prefabs/Blocks/StoneBlock.prefab", folderW2, new[]
        {
            ("_sprNormalW2",     "Ice_block.png"),
            ("_sprVerticalW2",   "Ice_vertical.png"),
            ("_sprHorizontalW2", "Snow_block.png"),
            ("_sprDiagonalW2",   "Ice_diagonal.png"),
            ("_sprShortW2",      "Snow_short.png"),
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

        // _stayKinematic — FLIPPED to false 2026-07-16 (was true since 2026-07-26). It was
        // originally set true prefab-wide to fix "hitting one haybale in the L01 pile woke all
        // four" (back when BlockBase used a level-wide WakeAllStaticBlocks() sweep). That sweep
        // was removed entirely on 2026-07-10 — TakeDamage() has wakened only the specific block
        // actually hit ever since, so the ORIGINAL bug no longer needs this flag at all. Left
        // true anyway, it became a NEW bug: every Haybale placement in every level (not just
        // L01's ground pile) was permanently exempt from BlockBase's collapse cascade
        // (CheckForBlocksOnTop/SettleIfUnsupported), so a haybale stacked on top of a taller
        // structure (e.g. L12/L14) just stayed Static and floating in mid-air even after
        // whatever supported it was destroyed — user report 2026-07-16: "structure sprites
        // remain mid-air." Haybale now falls like any other block by default; only L01's
        // specific decorative ground pile opts back into staying fixed, via the new per-instance
        // LevelData.BlockSpawnData.forceStayKinematic override (see LevelDataGenerator.cs).
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
            so.FindProperty("_stayKinematic").boolValue = false;
            so.FindProperty("_silentHit").boolValue      = true;
            // Haybale is a genuine explosive prop (unlike plain Wood, which uses the same
            // WoodBlock component but stays false here) — 2026-07-10 fifth balance pass, see
            // WoodBlock._isExplosive. Only used for the chain-reaction gate (never damages
            // robots directly) — see RobotEnemy.cs's damage-model comment.
            so.FindProperty("_isExplosive").boolValue = true;
            // Re-sync _areaDamage to WoodBlock's current class default (25f) — same
            // [SerializeField] stale-value issue as WoodBlock.prefab itself (see
            // WireBlockSprites' own comment on this); Haybale shares WoodBlock's block-to-block
            // chain-damage baseline by design, so it must be kept in sync the same way.
            so.FindProperty("_areaDamage").floatValue = 25f;
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
    // Introduced 2026-07-10 at L03 "The Tower" to "gradually introduce the exploding barrel" per
    // user request. LevelLoader picks this prefab when BlockSpawnData.type == BlockType.Barrel.
    // Art is Barrel_Dynamite.png (Assets/Sprites/Environment/World1Props/), NOT WoodenBarrel.png —
    // the latter is the plain decorative prop used elsewhere as scenery, not this explosive block.
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

        // Wired to Barrel_Dynamite.png, not WoodenBarrel.png — fixed 2026-07-12 (user report:
        // "the gameplay is showing the wrong barrel, the normal barrel doesn't explode"). Every
        // level authored via LevelLayoutDumper since the L01-L18 overhaul actually placed the
        // 'Barrel_Dynamite' sprite in the Scene view for BlockType.Barrel entries (see the dump
        // comments in LevelDataGenerator.cs), but this prefab was still wired to the plain
        // WoodenBarrel.png art from L03's original introduction — so every dynamite barrel in
        // gameplay rendered as the inert decorative barrel prop instead of looking like something
        // that explodes.
        WireBlockPrefab(prefabPath, "Assets/Sprites/Environment/World1Props", new[]
        {
            ("_sprNormal",     "Barrel_Dynamite.png"),
            ("_sprHorizontal", "Barrel_Dynamite.png"),
            ("_sprVertical",   "Barrel_Dynamite.png"),
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

    // Wires BessieAnimal.prefab's _earthquakeClip field — same LoadPrefabContents/
    // SaveAsPrefabAsset shape as SetFloatField above, just for an AudioClip reference on a
    // different component (BessieAnimal, not BlockBase).
    static void WireBessieAudio()
    {
        const string prefabPath = "Assets/Prefabs/Animals/BessieAnimal.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null)
        {
            Debug.LogWarning($"[FarmFury] Prefab not found: {prefabPath}");
            return;
        }
        var clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Bessie_Earthquake.mp3");
        if (clip == null)
        {
            Debug.LogWarning("[FarmFury] Audio clip not found at Assets/Audio/Bessie_Earthquake.mp3.");
            return;
        }
        var contents = PrefabUtility.LoadPrefabContents(prefabPath);
        var bessie   = contents.GetComponent<BessieAnimal>();
        if (bessie != null)
        {
            var so = new SerializedObject(bessie);
            so.FindProperty("_earthquakeClip").objectReferenceValue = clip;
            so.ApplyModifiedProperties();
            PrefabUtility.SaveAsPrefabAsset(contents, prefabPath);
            Debug.Log("[FarmFury] BessieAnimal: wired _earthquakeClip <- Bessie_Earthquake.mp3");
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

    // ── Robot sprite: wire Robot_Pawn into Robot (RobotType.Basic) prefab SpriteRenderer ──

    static void WireRobotSprite()
    {
        const string prefabPath = "Assets/Prefabs/Enemies/Robot.prefab";
        // Switched from Robot_Idle.png to Robot_Pawn.png 2026-07-12 — RobotType.Basic's actual
        // debut is L11 (its "third robot card" alongside Harvester/SemiHarvester), and the level
        // was hand-placed in the Scene view using the 'Robot_Pawn' sprite specifically, not the
        // old placeholder. Robot_Idle.png was never actually used in any shipped level before this.
        const string spritePath = "Assets/Sprites/Enemies/Robot/Robot_Pawn.png";
        const string damagedSpritePath = "Assets/Sprites/Enemies/Robot/Robot_Pawn_Damage(1).png";

        if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null)
        {
            Debug.LogWarning("[FarmFury] Robot prefab not found — skipping robot sprite wiring.");
            return;
        }

        var spriteAsset = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
        if (spriteAsset == null)
        {
            Debug.LogWarning("[FarmFury] Robot_Pawn.png not found. Run tools/remove_backgrounds.py first.");
            return;
        }
        var damagedSprite = AssetDatabase.LoadAssetAtPath<Sprite>(damagedSpritePath);

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
            WireRobotFacingRight(so, "Assets/Sprites/Enemies/Robot/Robot_Pawn_right.png");
            WireRobotDeathFx(so);
            WireRobotDamagedArt(so, damagedSprite);
            // Re-sync _robotContactDamage to RobotEnemy's current class default (18f) — 2026-07-10,
            // found during a "review all our damage changes" audit: every robot prefab was stuck
            // at a stale 15 (the value serialized before an earlier balance pass raised the class
            // default 15->18 — see the "Robot-vs-robot contact damage raised" history entry), and
            // nothing had ever re-written it since (same [SerializeField] stale-value trap as
            // WoodBlock/HaybaleBlock._areaDamage, fixed in the same pass — see WireBlockSprites'
            // comment on that one).
            so.FindProperty("_robotContactDamage").floatValue = 18f;
            so.ApplyModifiedProperties();
        }
        PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
        PrefabUtility.UnloadPrefabContents(go);
        Debug.Log("[FarmFury] Robot: wired Robot_Pawn.png into _robotSprite (PPU=1746).");
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

    // Right-facing sprite for the new left/right patrol animation (2026-07-18, user-supplied
    // Robot_Pawn_right.png / Robot_Harvestor_right.png / Robot_SemiHarvestor_right.png —
    // hand-mirrored art, not a runtime flip). Same PPU=1746 import convention as every other
    // robot sprite in this project. No Commander_right art exists, so EnsureCommanderRobotPrefab()
    // never calls this — Commander keeps its old in-place idle-turn animation unchanged (see
    // RobotEnemy.Awake()'s branch on _sprFacingRight being null).
    static void WireRobotFacingRight(SerializedObject so, string spritePath)
    {
        var imp = AssetImporter.GetAtPath(spritePath) as TextureImporter;
        if (imp == null)
        {
            Debug.LogWarning($"[FarmFury] Right-facing robot sprite not found at {spritePath} — patrol animation will fall back to the old in-place idle-turn.");
            return;
        }
        bool dirty = false;
        if (imp.textureType         != TextureImporterType.Sprite)  { imp.textureType         = TextureImporterType.Sprite;  dirty = true; }
        if (imp.spritePixelsPerUnit != 1746)                          { imp.spritePixelsPerUnit = 1746;                        dirty = true; }
        if (!imp.alphaIsTransparency)                                 { imp.alphaIsTransparency = true;                        dirty = true; }
        if (imp.spriteImportMode    != SpriteImportMode.Single)      { imp.spriteImportMode    = SpriteImportMode.Single;    dirty = true; }
        if (dirty) imp.SaveAndReimport();

        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
        if (sprite == null) { Debug.LogWarning($"[FarmFury] {spritePath} failed to load as Sprite."); return; }
        so.FindProperty("_sprFacingRight").objectReferenceValue = sprite;
        Debug.Log($"[FarmFury] Wired _sprFacingRight <- {spritePath}");
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
            WireRobotFacingRight(so, "Assets/Sprites/Enemies/Robot/Robot_Harvestor_right.png");
            WireRobotDamagedArt(so, damagedSprite);
            // _maxHealth: raised 35->40 originally (2026-07-01), LOWERED 40->22 same day
            // 2026-07-10 (user report: "I cannot get past level 2... ease the damage
            // requirements... one hit must cause a chain reaction"), then RAISED back up 22->28,
            // same day third pass (user-reported the combination of that plus the widened
            // chain-reaction blast radius/damage made robots "explode on the slightest of
            // touches" — overcorrected). Expect further tuning passes on this number — the user
            // has explicitly flagged it'll take iteration to land the right strength ratio.
            so.FindProperty("_maxHealth").floatValue = 28f;
            // Re-sync to RobotEnemy's current class default (18f) — see WireRobotSprite's own
            // comment on this same [SerializeField] stale-value fix for the full history.
            so.FindProperty("_robotContactDamage").floatValue = 18f;
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
            WireRobotFacingRight(so, "Assets/Sprites/Enemies/Robot/Robot_SemiHarvestor_right.png");
            WireRobotDamagedArt(so, damagedSprite);
            // 38->20->26 2026-07-10 — see HarvesterRobot's own _maxHealth comment for the full
            // reasoning (same day, same reports, both floors moved together).
            so.FindProperty("_maxHealth").floatValue = 26f;
            // Re-sync to RobotEnemy's current class default (18f) — see WireRobotSprite's own
            // comment on this same [SerializeField] stale-value fix for the full history.
            so.FindProperty("_robotContactDamage").floatValue = 18f;
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

    // ── World 2 (Frozen Tundra) robot reskins — 2026-07-19 ──────────────────────────────
    // Same 3-tier structure as Robot/HarvesterRobot/SemiHarvesterRobot above, reusing the SAME
    // RobotType.Basic/Harvester/SemiHarvester values (not new enum members — see
    // LevelLoader.SpawnRobot's own comment on why). Factored into one shared helper instead of
    // copy-pasting the Ensure*RobotPrefab shape a third time, since these three are otherwise
    // identical to each other (and to their W1 counterparts) except for name/sprite paths/HP.
    // No damaged-flash art exists for any of these yet (unlike HarvesterRobot/SemiHarvesterRobot,
    // which have dedicated _Damaged art) — falls back to RobotEnemy's plain white-tint flash,
    // same graceful-degradation behaviour as the base Robot.prefab (RobotType.Basic) already has.
    static void EnsureWorld2RobotPrefab(string prefabName, string leftSpritePath, string rightSpritePath, float maxHealth)
    {
        string prefabPath = $"Assets/Prefabs/Enemies/{prefabName}.prefab";

        var imp = AssetImporter.GetAtPath(leftSpritePath) as TextureImporter;
        if (imp == null)
        {
            Debug.LogWarning($"[FarmFury] {prefabName}: sprite not found at {leftSpritePath}.");
            return;
        }
        bool dirty = false;
        if (imp.textureType         != TextureImporterType.Sprite)  { imp.textureType         = TextureImporterType.Sprite;  dirty = true; }
        if (imp.spritePixelsPerUnit != 1746)                          { imp.spritePixelsPerUnit = 1746;                        dirty = true; }
        if (!imp.alphaIsTransparency)                                 { imp.alphaIsTransparency = true;                        dirty = true; }
        if (imp.spriteImportMode    != SpriteImportMode.Single)      { imp.spriteImportMode    = SpriteImportMode.Single;    dirty = true; }
        if (dirty) imp.SaveAndReimport();

        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(leftSpritePath);
        if (sprite == null) { Debug.LogWarning($"[FarmFury] {leftSpritePath} failed to load as Sprite."); return; }

        bool exists = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null;
        GameObject go;
        if (exists)
        {
            go = PrefabUtility.LoadPrefabContents(prefabPath);
        }
        else
        {
            go       = new GameObject(prefabName);
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
            WireRobotFacingRight(so, rightSpritePath);
            so.FindProperty("_maxHealth").floatValue = maxHealth;
            so.FindProperty("_robotContactDamage").floatValue = 18f;
            WireRobotDeathFx(so);
            so.ApplyModifiedProperties();
        }

        PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
        if (exists) PrefabUtility.UnloadPrefabContents(go);
        else        Object.DestroyImmediate(go);
        AssetDatabase.Refresh();
        Debug.Log($"[FarmFury] {prefabName}.prefab created with {leftSpritePath} (PPU=1746).");
    }

    // RobotType.Basic tier — matches Robot.prefab's own _maxHealth (35).
    static void EnsureFrostRobotPrefab() =>
        EnsureWorld2RobotPrefab("FrostRobot",
            "Assets/Sprites/Enemies/Robot/FrostRobot_left.png",
            "Assets/Sprites/Enemies/Robot/FrostRobot_right.png",
            35f);

    // RobotType.Harvester tier — matches HarvesterRobot.prefab's own _maxHealth (28). Note the
    // real on-disk filename is "Iceharvestor_right.png" (lowercase h) — matched exactly, not
    // "corrected", since AssetDatabase paths are effectively case-sensitive on iOS/Android builds
    // even though Windows tolerates the mismatch (same class of gotcha documented for L13's
    // "Level13.png" match-up header elsewhere in this file).
    static void EnsureIceHarvestorRobotPrefab() =>
        EnsureWorld2RobotPrefab("IceHarvestorRobot",
            "Assets/Sprites/Enemies/Robot/IceHarvestor_left.png",
            "Assets/Sprites/Enemies/Robot/Iceharvestor_right.png",
            28f);

    // RobotType.SemiHarvester tier — matches SemiHarvesterRobot.prefab's own _maxHealth (26).
    // Real filename is "GlacierHarvestor_Right.png" (capital R) — matched exactly, same
    // case-sensitivity reasoning as IceHarvestor above.
    static void EnsureGlacierHarvestorRobotPrefab() =>
        EnsureWorld2RobotPrefab("GlacierHarvestorRobot",
            "Assets/Sprites/Enemies/Robot/GlacierHarvestor_left.png",
            "Assets/Sprites/Enemies/Robot/GlacierHarvestor_Right.png",
            26f);

    // ── CommanderRobot: create a SEPARATE prefab (distinct from all other robot types) ───
    // LevelLoader picks this prefab when RobotSpawnData.robotType == RobotType.Commander.
    // Added 2026-07-12 for L18's boss level — user dropped Commander.png/Commander_Hit.png/
    // Commander_Explode.png into Assets/Sprites/Enemies/Robot/. HP set well above every other
    // robot type (90, vs Harvester's 28/SemiHarvester's 26/Basic's 35 class default) since this
    // is the single boss enemy of the level, not one of several. Uses its own dedicated
    // Commander_Explode.png death-burst art instead of the generic Explosion.png every other
    // robot type shares (WireRobotDeathFx sets that as a fallback default; overridden here).
    static void EnsureCommanderRobotPrefab()
    {
        const string prefabPath         = "Assets/Prefabs/Enemies/CommanderRobot.prefab";
        const string spritePath         = "Assets/Sprites/Enemies/Robot/Commander.png";
        const string alertSpritePath    = "Assets/Sprites/Enemies/Robot/Commander_Alert.png";
        const string damagedSpritePath  = "Assets/Sprites/Enemies/Robot/Commander_Hit.png";
        const string explodeSpritePath  = "Assets/Sprites/Enemies/Robot/Commander_Explode.png";

        var imp = AssetImporter.GetAtPath(spritePath) as TextureImporter;
        if (imp == null)
        {
            Debug.LogWarning($"[FarmFury] Commander.png not found at {spritePath}.");
            return;
        }
        bool dirty = false;
        if (imp.textureType         != TextureImporterType.Sprite)         { imp.textureType         = TextureImporterType.Sprite;         dirty = true; }
        if (imp.spritePixelsPerUnit != 1746)                                { imp.spritePixelsPerUnit = 1746;                               dirty = true; }
        if (!imp.alphaIsTransparency)                                       { imp.alphaIsTransparency = true;                               dirty = true; }
        if (imp.spriteImportMode    != SpriteImportMode.Single)            { imp.spriteImportMode    = SpriteImportMode.Single;            dirty = true; }
        if (dirty) imp.SaveAndReimport();

        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
        if (sprite == null) { Debug.LogWarning("[FarmFury] Commander.png failed to load as Sprite."); return; }

        var aImp = AssetImporter.GetAtPath(alertSpritePath) as TextureImporter;
        Sprite alertSprite = null;
        if (aImp == null)
        {
            Debug.LogWarning($"[FarmFury] Commander_Alert.png not found at {alertSpritePath} — the mid-tier 'alert' pose will be skipped (straight to critical).");
        }
        else
        {
            bool aDirty = false;
            if (aImp.textureType         != TextureImporterType.Sprite)  { aImp.textureType         = TextureImporterType.Sprite;  aDirty = true; }
            if (aImp.spritePixelsPerUnit != 1746)                         { aImp.spritePixelsPerUnit = 1746;                        aDirty = true; }
            if (!aImp.alphaIsTransparency)                                { aImp.alphaIsTransparency = true;                        aDirty = true; }
            if (aImp.spriteImportMode    != SpriteImportMode.Single)     { aImp.spriteImportMode    = SpriteImportMode.Single;     aDirty = true; }
            if (aDirty) aImp.SaveAndReimport();
            alertSprite = AssetDatabase.LoadAssetAtPath<Sprite>(alertSpritePath);
        }

        var dImp = AssetImporter.GetAtPath(damagedSpritePath) as TextureImporter;
        Sprite damagedSprite = null;
        if (dImp == null)
        {
            Debug.LogWarning($"[FarmFury] Commander_Hit.png not found at {damagedSpritePath} — hit-flash will fall back to the plain white tint.");
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

        var eImp = AssetImporter.GetAtPath(explodeSpritePath) as TextureImporter;
        Sprite explodeSprite = null;
        if (eImp == null)
        {
            Debug.LogWarning($"[FarmFury] Commander_Explode.png not found at {explodeSpritePath} — death burst will fall back to the generic Explosion.png.");
        }
        else
        {
            bool eDirty = false;
            if (eImp.textureType         != TextureImporterType.Sprite)  { eImp.textureType         = TextureImporterType.Sprite;  eDirty = true; }
            if (eImp.spritePixelsPerUnit != 1746)                         { eImp.spritePixelsPerUnit = 1746;                        eDirty = true; }
            if (!eImp.alphaIsTransparency)                                { eImp.alphaIsTransparency = true;                        eDirty = true; }
            if (eImp.spriteImportMode    != SpriteImportMode.Single)     { eImp.spriteImportMode    = SpriteImportMode.Single;     eDirty = true; }
            if (eDirty) eImp.SaveAndReimport();
            explodeSprite = AssetDatabase.LoadAssetAtPath<Sprite>(explodeSpritePath);
        }

        bool exists = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null;
        GameObject go;
        if (exists)
        {
            go = PrefabUtility.LoadPrefabContents(prefabPath);
        }
        else
        {
            go       = new GameObject("CommanderRobot");
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
            // 3-pose progression: Commander.png (normal) -> Commander_Alert.png (_alertSprite,
            // ~66% HP) -> Commander_Hit.png (_criticalSprite/_robotDamagedSprite via
            // WireRobotDamagedArt, ~40% HP) — see RobotEnemy's AlertHealthFraction/
            // CriticalHealthFraction comments. User request: "the commander has three poses.
            // when first hit change to Commander_Alert; when hit again change to Commander_hit".
            if (alertSprite != null)
                so.FindProperty("_alertSprite").objectReferenceValue = alertSprite;
            WireRobotDamagedArt(so, damagedSprite);
            so.FindProperty("_maxHealth").floatValue = 90f;
            so.FindProperty("_robotContactDamage").floatValue = 18f;
            // 2026-07-14, user report: "the commander must be stronger than it is" — RobotEnemy's
            // damage model computes every hit as `_maxHealth * fraction`, so _maxHealth=90 alone
            // never actually made Commander take more HITS to kill than a 26-35 HP grunt (the
            // ratio cancels out — see _damageResistance's field comment in RobotEnemy.cs for the
            // full explanation). 0.61 makes a direct hit land at 90*0.55*0.61=30.2 — exactly 3
            // solid direct hits (90.6 total) to kill, matching this level's 3-bird budget.
            so.FindProperty("_damageResistance").floatValue = 0.61f;
            // Same-session follow-up, user: "it should take all three sprites to destroy - even
            // with falling structure." L18's staircase is built from destructible blocks plus 2
            // dynamite barrels around the Commander — without a separate multiplier here,
            // Explosion/Fall damage share the SAME fraction/resistance as a direct hit (see
            // _structuralDamageResistance's field comment), so a barrel catching him in its blast
            // could finish him early, undermining "all three." 0.1 means structural collapse and
            // nearby explosions chip only token damage (~4.95 per explosion, ~1.35 per fall) —
            // negligible against his 90 HP, so only genuine thrown-animal hits can meaningfully
            // kill him. Both values are tunable — revisit once actually played.
            so.FindProperty("_structuralDamageResistance").floatValue = 0.1f;
            WireRobotDeathFx(so);
            if (explodeSprite != null)
                so.FindProperty("_deathExplosionSprite").objectReferenceValue = explodeSprite;
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
        Debug.Log("[FarmFury] CommanderRobot.prefab created with Commander.png (PPU=1746).");
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
        // Surface Y = -6.60; scale (60, GroundThickness, 1), centred so the TOP edge stays pinned
        // at -6.60 regardless of thickness. Thickness raised 0.5 -> 4 (2026-07-13, defense-in-
        // depth alongside BessieAnimal's new downward-speed clamp — user report: "when bessie is
        // triggered she falls through the floor"): a thin 0.5u collider left very little margin
        // for a fast-moving body's single-fixed-timestep travel distance to still land a real
        // collision before passing clean through, especially for Bessie's slam-triggered velocity
        // spike. Everything below the visible play area anyway, so thickening it downward (not
        // upward, which would move the surface) has no visual or gameplay side effects.
        const float GroundSurface   = -6.60f;
        const float GroundThickness = 4f;
        var go = GameObject.Find("Ground");
        if (go == null) { go = new GameObject("Ground"); Debug.Log("[FarmFury] Created 'Ground' physics collider."); }
        go.tag   = "Ground";
        go.layer = 6;
        go.transform.position   = new Vector3(0f, GroundSurface - GroundThickness * 0.5f, 0f);
        go.transform.localScale = new Vector3(60f, GroundThickness, 1f);

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

    // GroundVisual_Placeholder (procedural flat grass strip) REMOVED 2026-07-17 — user deleted it
    // by hand once ParallaxForeground/Midground took over showing ground-level detail, but this
    // method unconditionally recreated it on every Wire Scene References pass since it had no way
    // to know the deletion was deliberate (same class of bug as the FarmCannon stamp above). See
    // the placeholder-cleanup loop in WireAll() for the one-time removal of any leftover instance.

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
        // AssetDatabase.FindAssets does a FUZZY name search, not an exact match — searching
        // "Robot" also matches "CommanderRobot.prefab"/"HarvesterRobot.prefab"/
        // "SemiHarvesterRobot.prefab" (all contain "Robot"), and guids[0] is whichever sorts
        // first alphabetically among the matches, NOT necessarily "Robot.prefab" itself. Real
        // bug found 2026-07-12 while adding CommanderRobot: _robotPrefab (RobotType.Basic's
        // prefab) had been silently wired to HarvesterRobot.prefab this entire session (Harvester
        // < Robot alphabetically) — every RobotType.Basic/"Robot_Pawn" spawn since L11 has
        // actually been a HarvesterRobot (wrong art, wrong HP), and after adding Commander it
        // would have silently flipped to CommanderRobot instead (Commander < Robot too). Fixed by
        // requiring an EXACT filename match among the fuzzy results, falling back to the first
        // fuzzy hit only if no exact match exists (preserves old behaviour for any prefab name
        // that never had this ambiguity).
        var guids = AssetDatabase.FindAssets(prefabName + " t:Prefab", new[] { folder });
        if (guids.Length == 0)
        {
            Debug.LogWarning($"[FarmFury] Prefab '{prefabName}' not found in {folder}");
            return;
        }
        string path = null;
        foreach (var guid in guids)
        {
            var p = AssetDatabase.GUIDToAssetPath(guid);
            if (System.IO.Path.GetFileNameWithoutExtension(p) == prefabName) { path = p; break; }
        }
        path ??= AssetDatabase.GUIDToAssetPath(guids[0]);
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
