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
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/Game.unity", OpenSceneMode.Single);

        EnsureParents();     // BlockParent, RobotParent GameObjects
        EnsureGround();      // Static ground plane with collider + renderer
        EnsureEggPrefab();   // Create Egg prefab + wire into CluckAnimal prefab
        WireGameManager();   // _levels array
        WireLevelLoader();   // 5 prefab refs + 2 parent transforms
        WireLauncher();      // CatapultLauncher + LevelLoader ref
        PositionCamera();    // Move camera to see the play area

        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[FarmFury] Scene wiring complete. Game.unity saved.");
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

        SetPrefab(so, "_woodPrefab",   "WoodBlock",    "Assets/Prefabs/Blocks");
        SetPrefab(so, "_stonePrefab",  "StoneBlock",   "Assets/Prefabs/Blocks");
        SetPrefab(so, "_cluckPrefab",  "CluckAnimal",  "Assets/Prefabs/Animals");
        SetPrefab(so, "_bessiePrefab", "BessieAnimal", "Assets/Prefabs/Animals");
        SetPrefab(so, "_robotPrefab",  "Robot",        "Assets/Prefabs/Enemies");

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
            go.transform.position = new Vector3(11.2f, 0f, 0f); // 560px / 50
            Debug.Log("[FarmFury] Created 'Launcher' GameObject at (11.2, 0, 0).");
        }

        var launcher = go.GetComponent<CatapultLauncher>();
        if (launcher == null) launcher = go.AddComponent<CatapultLauncher>();

        var so = new SerializedObject(launcher);
        var ll = Object.FindAnyObjectByType<LevelLoader>();
        so.FindProperty("_levelLoader").objectReferenceValue = ll;
        so.ApplyModifiedProperties();

        Debug.Log("[FarmFury] Launcher: wired LevelLoader reference.");
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
            var so  = new SerializedObject(cluckComp);
            var egg = AssetDatabase.LoadAssetAtPath<GameObject>(eggPath);
            so.FindProperty("_eggPrefab").objectReferenceValue = egg;
            so.ApplyModifiedProperties();
            PrefabUtility.SaveAsPrefabAsset(contents, cluckPath);
            Debug.Log("[FarmFury] CluckAnimal._eggPrefab wired.");
        }
        PrefabUtility.UnloadPrefabContents(contents);
    }

    // ── Ground: static collider + green visual ────────────────────────────────

    static void EnsureGround()
    {
        // Always destroy-and-recreate to wipe any previously-broken ground object.
        var existing = GameObject.Find("Ground");
        if (existing != null) Object.DestroyImmediate(existing);

        var go = new GameObject("Ground");
        go.tag   = "Ground";
        go.layer = 6;

        // Key maths: localScale drives BOTH collider and sprite.
        // scale=(60,1,1), col.size=(1,1) → world collider = 60×1.
        // GO center at (14,-0.5) → top edge at Y=0  (the ground surface).
        go.transform.position   = new Vector3(14f, -0.5f, 0f);
        go.transform.localScale = new Vector3(60f,  1f,   1f);

        var col  = go.AddComponent<BoxCollider2D>();
        col.size = new Vector2(1f, 1f);   // 1×1 local → 60×1 world

        var sr        = go.AddComponent<SpriteRenderer>();
        sr.sprite     = MakeGroundSprite();
        sr.color      = new Color(0.25f, 0.65f, 0.15f);
        sr.sortingOrder = 0;

        var rb      = go.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Static;

        Debug.Log("[FarmFury] Ground created: centre (14,-0.5), surface at Y=0.");
    }

    static Sprite MakeGroundSprite()
    {
        var tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
    }

    // ── Camera: position to see launcher + structures ─────────────────────────

    static void PositionCamera()
    {
        var cam = Object.FindAnyObjectByType<Camera>();
        if (cam == null) return;
        cam.orthographic     = true;
        cam.orthographicSize = 5f;   // 10 Unity units tall — covers the full play area
        cam.transform.position = new Vector3(13f, 3f, -10f);
        Debug.Log("[FarmFury] Camera positioned at (13, 3, -10), orthoSize=5.");
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

    // ── Helper: find a prefab by name in a folder and assign it ──────────────

    static void SetPrefab(SerializedObject so, string fieldName, string prefabName, string folder)
    {
        var guids = AssetDatabase.FindAssets(prefabName + " t:Prefab", new[] { folder });
        if (guids.Length == 0)
        {
            Debug.LogWarning($"[FarmFury] Prefab '{prefabName}' not found in {folder}");
            return;
        }
        var path   = AssetDatabase.GUIDToAssetPath(guids[0]);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        so.FindProperty(fieldName).objectReferenceValue = prefab;
        Debug.Log($"[FarmFury]   {fieldName} -> {path}");
    }
}
