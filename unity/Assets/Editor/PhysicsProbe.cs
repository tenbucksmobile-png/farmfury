// FarmFury - headless physics probe (2026-11-08): plays World 1 levels in Play mode with no input and reports what
// moves, wakes and breaks on its own. Batch: Unity.exe -batchmode -projectPath unity -executeMethod PhysicsProbe.Run
// (no -quit; the probe exits when done). Output lines start with "[Probe]" in the log file.
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class PhysicsProbe
{
    const string Key = "FF_PhysicsProbe_Levels";   // "first,last" level indices; deleted when done

    [MenuItem("FarmFury/Debug/Physics Probe (World 1, no input)")]
    public static void Run()
    {
        string levels = System.Environment.GetEnvironmentVariable("FF_PROBE_LEVELS");
        EditorPrefs.SetString(Key, string.IsNullOrEmpty(levels) ? "0,17" : levels);
        EditorSceneManager.OpenScene("Assets/Scenes/Game.unity");
        EditorApplication.EnterPlaymode();
    }

    [InitializeOnLoadMethod]
    static void Hook()
    {
        if (!EditorPrefs.HasKey(Key)) return;
        EditorApplication.playModeStateChanged += s => { if (s == PlayModeStateChange.EnteredPlayMode) Begin(); };
        if (EditorApplication.isPlaying) Begin();
    }

    static bool _begun;
    static void Begin()
    {
        if (_begun || !EditorPrefs.HasKey(Key)) return;
        _begun = true;
        var parts = EditorPrefs.GetString(Key).Split(',');
        new GameObject("PhysicsProbeRunner").AddComponent<PhysicsProbeRunner>()
            .Init(int.Parse(parts[0]), int.Parse(parts[1]), () =>
            {
                EditorPrefs.DeleteKey(Key);
                _begun = false;
                if (Application.isBatchMode) EditorApplication.Exit(0); else EditorApplication.ExitPlaymode();
            });
    }
}

public class PhysicsProbeRunner : MonoBehaviour
{
    int _first, _last;
    System.Action _done;

    public void Init(int first, int last, System.Action done) { _first = first; _last = last; _done = done; }

    IEnumerator Start()
    {
        BlockBase.ProbeLog = true;
        yield return new WaitForSeconds(1f);
        var gm = GameManager.Instance;
        if (gm == null) { Debug.Log("[Probe] no GameManager"); _done(); yield break; }
        for (int i = _first; i <= _last && i < gm.TotalLevels; i++)
        {
            // FF_PROBE_ROOM=1 (2026-11-12): probe a multi-room level's second room instead of its first.
            LevelLoader.DebugStartRoom = int.TryParse(System.Environment.GetEnvironmentVariable("FF_PROBE_ROOM"), out var pr) ? pr : 0;
            gm.ForceStartLevel(i);
            // Timeline: awake bodies over the first 3 s, and collider overlaps once the blocks have gone live.
            float ts = Time.time; bool overlapsLogged = false;
            while (Time.time - ts < 3f)
            {
                yield return new WaitForFixedUpdate();
                var bs = FindObjectsByType<BlockBase>(FindObjectsSortMode.None);
                int live = 0, awakeN = 0, dyn = 0;
                foreach (var b in bs)
                {
                    var rb = b.GetComponent<Rigidbody2D>();
                    if (b.IsLive) live++;
                    if (rb.bodyType == RigidbodyType2D.Dynamic) { dyn++; if (rb.IsAwake()) awakeN++; }
                }
                if (Mathf.Repeat(Time.time - ts, 0.25f) < Time.fixedDeltaTime * 1.01f || awakeN > 0 && Time.time - ts < 0.5f)
                    Debug.Log($"[Probe] t+{Time.time - ts:F2} blocks={bs.Length} live={live} dynamic={dyn} awake={awakeN} reveal={LevelReveal.IsRevealing}");
                if (!overlapsLogged && live > 0)
                {
                    overlapsLogged = true;
                    for (int a1 = 0; a1 < bs.Length; a1++)
                        for (int b1 = a1 + 1; b1 < bs.Length; b1++)
                        {
                            var ca = bs[a1].GetComponent<Collider2D>(); var cb = bs[b1].GetComponent<Collider2D>();
                            var dist = Physics2D.Distance(ca, cb);
                            if (dist.isValid && dist.distance < -0.02f)
                                Debug.Log($"[Probe] OVERLAP {bs[a1].name}@{bs[a1].transform.position} x {bs[b1].name}@{bs[b1].transform.position} depth={-dist.distance:F3} " +
                                          $"sizeA={ca.bounds.size} sizeB={cb.bounds.size}");
                        }
                }
            }
            float t0 = Time.time;
            while (LevelReveal.IsRevealing && Time.time - t0 < 6f) yield return null;
            yield return new WaitForSeconds(0.5f);

            var blocks = FindObjectsByType<BlockBase>(FindObjectsSortMode.None);
            var robots = FindObjectsByType<RobotEnemy>(FindObjectsSortMode.None);
            if (System.Environment.GetEnvironmentVariable("FF_PROBE_WAKE") == "1")
            {
                foreach (var b in blocks) { var rb = b.GetComponent<Rigidbody2D>(); if (rb.bodyType == RigidbodyType2D.Dynamic) rb.WakeUp(); }
                foreach (var r in robots) { var rb = r.GetComponent<Rigidbody2D>(); if (rb.bodyType == RigidbodyType2D.Dynamic) rb.WakeUp(); }
                Debug.Log("[Probe] woke everything (stress mode)");
            }
            var start = new Dictionary<Object, Vector3>();
            foreach (var b in blocks) start[b] = b.transform.position;
            foreach (var r in robots) start[r] = r.transform.position;
            int awakeAtStart = CountAwake(blocks, robots);
            Debug.Log($"[Probe] LEVEL {i} {gm.CurrentLevel?.levelId} blocks={blocks.Length} robots={robots.Length} awakeAfterReveal={awakeAtStart}");

            // Test shot (2026-11-11): FF_PROBE_SHOT=high|low fires one animal (FF_PROBE_ANIMAL, default Cluck) straight
            // at the front piece of the structure (high or low on it) at FF_PROBE_SPEED (default 10 u/s, a typical
            // impact speed) and reports what it did: pieces toppled, pieces thrown > 3 u, top piece speed, robots lost.
            string shot = System.Environment.GetEnvironmentVariable("FF_PROBE_SHOT");
            float topSpeed = 0f;
            if (!string.IsNullOrEmpty(shot)) yield return Shoot(blocks, shot, v => topSpeed = Mathf.Max(topSpeed, v));
            yield return new WaitForSeconds(6f);
            if (!string.IsNullOrEmpty(shot))
            {
                int toppled = 0, thrown = 0, robotsLost = 0;
                foreach (var b in blocks)
                {
                    if (b == null) { toppled++; continue; }
                    float d = Vector3.Distance(start[b], b.transform.position);
                    float turned = Mathf.Abs(Mathf.DeltaAngle(b.transform.eulerAngles.z, _startAngle.TryGetValue(b, out var a0) ? a0 : 0f));
                    if (d > 0.6f || turned > 40f) toppled++;
                    if (d > 3f) thrown++;
                }
                foreach (var r in robots) if (r == null || r.IsDestroyed) robotsLost++;
                Debug.Log($"[Probe] SHOT {i} {gm.CurrentLevel?.levelId} {shot} target={_shotTarget}: toppled {toppled}/{blocks.Length}, " +
                          $"thrown>3u {thrown}, topSpeed {topSpeed:F1} u/s, robots lost {robotsLost}/{robots.Length}");
            }

            int blocksLeft = 0, robotsLeft = 0, awake = 0;
            float maxMove = 0f; string mover = "";
            foreach (var b in blocks)
            {
                if (b == null || b.IsDestroyed) continue;
                blocksLeft++;
                float d = Vector3.Distance(start[b], b.transform.position);
                if (d > maxMove) { maxMove = d; mover = $"{b.name}@{start[b]}"; }
                var rb = b.GetComponent<Rigidbody2D>();
                if (rb != null && rb.IsAwake()) awake++;
            }
            foreach (var r in robots)
            {
                if (r == null || r.IsDestroyed) continue;
                robotsLeft++;
                float d = Vector3.Distance(start[r], r.transform.position);
                if (d > maxMove) { maxMove = d; mover = $"{r.name}@{start[r]}"; }
            }
            Debug.Log($"[Probe] RESULT {i}: blocks {blocksLeft}/{blocks.Length}, robots {robotsLeft}/{robots.Length}, " +
                      $"stillAwake={awake}, maxMove={maxMove:F2} ({mover})");
        }
        _done();
    }

    readonly Dictionary<Object, float> _startAngle = new();
    string _shotTarget = "";

    IEnumerator Shoot(BlockBase[] blocks, string mode, System.Action<float> speedSample)
    {
        _startAngle.Clear();
        foreach (var b in blocks) _startAngle[b] = b.transform.eulerAngles.z;
        BlockBase front = null;
        foreach (var b in blocks) if (b.IsLive && (front == null || b.GetComponent<Collider2D>().bounds.min.x < front.GetComponent<Collider2D>().bounds.min.x)) front = b;
        if (front == null) { _shotTarget = "none"; yield break; }
        // Front stack: pieces within 1 u of the front piece; "high" = its top piece, "low" = its bottom piece.
        float fx = front.GetComponent<Collider2D>().bounds.min.x;
        BlockBase target = front;
        foreach (var b in blocks)
        {
            if (!b.IsLive || b.GetComponent<Collider2D>().bounds.min.x > fx + 1f) continue;
            float y = b.GetComponent<Collider2D>().bounds.center.y, ty = target.GetComponent<Collider2D>().bounds.center.y;
            if (mode == "high" ? y > ty : y < ty) target = b;
        }
        var bb = target.GetComponent<Collider2D>().bounds;
        float aimY = bb.center.y + (mode == "high" ? 0.3f : -0.3f) * bb.extents.y;
        string animal = System.Environment.GetEnvironmentVariable("FF_PROBE_ANIMAL");
        if (string.IsNullOrEmpty(animal)) animal = "Cluck";
        float speed = float.TryParse(System.Environment.GetEnvironmentVariable("FF_PROBE_SPEED"), out var sp) ? sp : 10f;
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Prefabs/Animals/{animal}Animal.prefab");
        _shotTarget = $"{target.GetType().Name} {target.name}@({bb.center.x:F1},{bb.center.y:F1}) by {animal}";
        if (prefab == null) { _shotTarget += " (no prefab)"; yield break; }
        var go = Object.Instantiate(prefab, new Vector3(bb.min.x - 0.9f, aimY, 0f), Quaternion.identity);
        var ab = go.GetComponent<AnimalBase>();
        yield return null;
        ab.Launch(new Vector2(speed, 0f), 0f);   // straight in, no drop, so the contact point is the aimed one
        float t0 = Time.time;
        while (Time.time - t0 < 1.5f)
        {
            yield return new WaitForFixedUpdate();
            foreach (var b in blocks)
                if (b != null && b.TryGetComponent<Rigidbody2D>(out var rb)) speedSample(rb.linearVelocity.magnitude);
        }
    }

    static int CountAwake(BlockBase[] blocks, RobotEnemy[] robots)
    {
        int n = 0;
        foreach (var b in blocks) { var rb = b != null ? b.GetComponent<Rigidbody2D>() : null; if (rb != null && rb.bodyType == RigidbodyType2D.Dynamic && rb.IsAwake()) n++; }
        foreach (var r in robots) { var rb = r != null ? r.GetComponent<Rigidbody2D>() : null; if (rb != null && rb.bodyType == RigidbodyType2D.Dynamic && rb.IsAwake()) n++; }
        return n;
    }
}
