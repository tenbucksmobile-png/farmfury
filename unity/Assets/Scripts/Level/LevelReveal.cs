using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Structure reveal at the start of a level, matching the Angry Birds 2 reference recording (2026-11-08,
/// tools/review session ref-angrybirds-20261108-141802, 10.95-12.6 s and 29.2-31.6 s): the playfield shows empty
/// for a beat, then the whole structure appears in one frame inside a burst of dust (nothing falls — blocks stay
/// frozen until hit), then each enemy pops in with a small puff, one after another, and only then can the player aim.
/// About 0.8-1.0 s in all.
///
/// Runs on levels framed by <see cref="ReferenceFraming"/> (L01 first). Self-creating like AudioManager's fallback, so
/// it needs no scene wiring. <see cref="CatapultLauncher"/> checks <see cref="IsRevealing"/> before letting a drag start.
/// </summary>
public class LevelReveal : MonoBehaviour
{
    public static bool IsRevealing { get; private set; }

    // Timings (seconds), read off the reference frames.
    const float EmptyBeat      = 0.35f;  // island shown empty
    const float RobotsDelay    = 0.25f;  // after the structure appears, before the first robot pops
    const float RobotStagger   = 0.14f;  // between robot pops
    const float RobotPopTime   = 0.28f;  // 0 -> 1.15 -> 1 scale
    const float PuffLife       = 0.55f;
    const int   MaxBlockPuffs  = 16;

    static readonly Color DustColour  = new Color(0.94f, 0.89f, 0.78f, 0.9f);
    static readonly Color RobotPuff   = new Color(0.85f, 0.95f, 0.85f, 0.9f);
    const int PuffSortingOrder = 7;   // in front of blocks (2), robots (3), cannon (4) and animals (6)

    static Sprite _puffSprite;
    static LevelReveal _instance;
    GameManager _gm;
    Coroutine _routine;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Create()
    {
        if (FindAnyObjectByType<LevelReveal>() != null) return;
        var go = new GameObject("LevelReveal");
        DontDestroyOnLoad(go);
        go.AddComponent<LevelReveal>();
    }

    void Awake() => _instance = this;

    /// <summary>Multi-room levels (LevelLoader.NextRoom): no shots from the moment a room is cleared until the next
    /// one has been revealed.</summary>
    public static void LockAim() => IsRevealing = true;
    public static void UnlockAim() => IsRevealing = false;

    /// <summary>The reveal for a room that was just spawned (2026-11-12): hidden in the same frame, then the usual
    /// beat, dust, robot pops (and the boss drop-in on a boss level's last room).</summary>
    public static void RevealRoom(List<BlockBase> blocks, List<RobotEnemy> robots, bool bossRoom)
    {
        if (_instance == null) { IsRevealing = false; return; }
        if (_instance._routine != null) _instance.StopCoroutine(_instance._routine);
        IsRevealing = true;
        var b = new List<BlockBase>(blocks);
        var r = new List<RobotEnemy>(robots);
        var hidden = new List<Renderer>();
        foreach (var x in b) Hide(x, hidden);
        foreach (var x in r) Hide(x, hidden);
        _instance._routine = _instance.StartCoroutine(_instance.RevealObjects(b, r, hidden, bossRoom));
    }

    void Update()
    {
        if (_gm != null) return;
        _gm = GameManager.Instance;
        if (_gm != null) _gm.OnLevelStarted += HandleLevelStarted;
    }

    void OnDestroy()
    {
        if (_gm != null) _gm.OnLevelStarted -= HandleLevelStarted;
        IsRevealing = false;
    }

    void HandleLevelStarted(LevelData level)
    {
        if (_routine != null) StopCoroutine(_routine);
        IsRevealing = false;
        if (!ReferenceFraming.Active(level)) return;
        IsRevealing = true;   // lock aiming from this frame, before the loader has even spawned anything
        // A multi-room boss level drops the boss off in its last room (RevealRoom), not here.
        _routine = StartCoroutine(Reveal(level != null && level.isBossLevel && level.RoomCount <= 1));
    }

    IEnumerator Reveal(bool bossLevel)
    {
        yield return null;   // LevelLoader spawns in its own OnLevelStarted handler

        var blocks = new List<BlockBase>(FindObjectsByType<BlockBase>());
        var robots = new List<RobotEnemy>(FindObjectsByType<RobotEnemy>());
        var hidden = new List<Renderer>();
        foreach (var b in blocks) Hide(b, hidden);
        foreach (var r in robots) Hide(r, hidden);
        yield return RevealObjects(blocks, robots, hidden, bossLevel);
    }

    IEnumerator RevealObjects(List<BlockBase> blocks, List<RobotEnemy> robots, List<Renderer> hidden, bool bossLevel)
    {
        yield return new WaitForSeconds(EmptyBeat);

        // The structure appears in one frame, in dust.
        foreach (var b in blocks) Show(b, hidden);
        int puffs = 0;
        foreach (var b in blocks)
        {
            if (b == null || puffs >= MaxBlockPuffs) continue;
            var sr = b.GetComponent<SpriteRenderer>();
            // Visible art, not the padded sprite rect (2026-11-09, EffectSizing): a long slab got a 5 u puff.
            Vector2 size = EffectSizing.VisibleSize(sr, out Vector3 at);
            SpawnPuff(at, EffectSizing.BurstDiameter(size, 1.6f, 0.4f, 1.6f), DustColour, Random.Range(0f, 0.08f));
            puffs++;
        }

        yield return new WaitForSeconds(RobotsDelay);

        // Robots pop in one after another, top of the structure first. On a boss level the boss (the biggest robot)
        // doesn't pop: it is dropped off by the mothership afterwards (BossDropIn).
        robots.RemoveAll(r => r == null);
        robots.Sort((a, b) => b.transform.position.y.CompareTo(a.transform.position.y));
        RobotEnemy boss = bossLevel ? Biggest(robots) : null;
        foreach (var r in robots)
        {
            if (r == null || r == boss) continue;
            Show(r, hidden);
            var sr = r.GetComponent<SpriteRenderer>();
            Vector2 vis = EffectSizing.VisibleSize(sr, out Vector3 at);
            SpawnPuff(at, Mathf.Clamp(Mathf.Max(vis.x, vis.y) * 1.3f, 0.5f, 2.6f), RobotPuff, 0f);
            StartCoroutine(Pop(r.transform));
            yield return new WaitForSeconds(RobotStagger);
        }

        yield return new WaitForSeconds(RobotPopTime);
        if (boss != null) yield return BossDropIn(boss, hidden);
        IsRevealing = false;
        _routine = null;
    }

    // ── Boss drop-in (2026-11-09, user: "on the boss level - make it the same as angry birds and have the boss be
    // dropped off, jump onto a structure from the mothership, make the mothership fly off again") ─────────────────────
    // The robot mothership glides in from the right and hovers above the boss's spot; the boss appears under its hull,
    // leaps out on an arc and lands on the structure (squash, dust, camera shake); the ship pulls up and flies off to
    // the upper left. Aiming stays locked until the boss has landed. Ship art: LevelLoader.MothershipSprite.
    const float ShipWidthSh   = 0.30f;   // ship width, fraction of the screen height
    const float ShipInTime    = 1.1f, ShipHover = 0.35f, BossJumpTime = 0.7f, BossJumpRise = 0.9f, ShipOutTime = 1.5f;
    const int   ShipSortingOrder = 5;    // in front of the structure (blocks 2, robots 3), behind animals (6)

    static RobotEnemy Biggest(List<RobotEnemy> robots)
    {
        RobotEnemy best = null;
        float size = 0f;
        foreach (var r in robots)
        {
            if (r == null) continue;
            var v = EffectSizing.VisibleSize(r.GetComponent<SpriteRenderer>(), out _);
            if (Mathf.Max(v.x, v.y) > size) { size = Mathf.Max(v.x, v.y); best = r; }
        }
        return best;
    }

    IEnumerator BossDropIn(RobotEnemy boss, List<Renderer> hidden)
    {
        var cam = Camera.main;
        Transform bt = boss.transform;
        Vector3 land = bt.position;
        var bossCols = bt.GetComponentsInChildren<Collider2D>();
        foreach (var c in bossCols) c.enabled = false;   // LevelLoader/GoLive switch them back on

        float camH = cam != null ? cam.orthographicSize * 2f : 16f, camW = camH * (cam != null ? cam.aspect : 1.78f);
        Vector3 c0 = cam != null ? cam.transform.position : Vector3.zero;

        // The ship.
        var ship = new GameObject("BossMothership");
        var sr = ship.AddComponent<SpriteRenderer>();
        sr.sprite = LevelLoader.MothershipSprite;
        sr.sortingOrder = ShipSortingOrder;
        float shipW = ShipWidthSh * camH;
        float scale = sr.sprite != null ? shipW / sr.sprite.bounds.size.x : 1f;
        float shipH = sr.sprite != null ? sr.sprite.bounds.size.y * scale : shipW * 0.5f;
        if (sr.sprite == null) { sr.sprite = _puffSprite; sr.color = new Color(0.25f, 0.28f, 0.4f); ship.transform.localScale = new Vector3(shipW, shipW * 0.4f, 1f); }
        else ship.transform.localScale = new Vector3(scale, scale, 1f);
        // The art's bow points right; the ship enters from the right travelling left, so mirror it to fly nose-first
        // (user, 2026-11-09: "flip the mothership to fly in facing the right direction").
        sr.flipX = true;

        float bossH = EffectSizing.VisibleSize(boss.GetComponent<SpriteRenderer>(), out _).y;
        float camTop = c0.y + camH * 0.5f;
        Vector3 hover = new Vector3(land.x, Mathf.Min(land.y + bossH * 0.5f + 1.6f + shipH * 0.5f, camTop - shipH * 0.55f), 0f);
        Vector3 from = new Vector3(c0.x + camW * 0.5f + shipW, hover.y + 1.2f, 0f);

        // Fly in, easing out, nose dipping as it brakes.
        for (float e = 0f; e < ShipInTime; e += Time.deltaTime)
        {
            float k = e / ShipInTime, ease = 1f - (1f - k) * (1f - k) * (1f - k);
            ship.transform.position = Vector3.Lerp(from, hover, ease);
            ship.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(8f, 0f, ease));
            yield return null;
        }
        for (float e = 0f; e < ShipHover; e += Time.deltaTime)
        {
            ship.transform.position = hover + Vector3.up * Mathf.Sin(e * 9f) * 0.05f;
            yield return null;
        }

        // The boss leaps out from under the hull and drops onto the structure.
        Vector3 start = hover + Vector3.down * (shipH * 0.35f);
        Show(boss, hidden);
        Vector3 baseScale = bt.localScale;
        StartCoroutine(ShipLeaves(ship.transform, hover, c0, camW, camH, shipW));
        for (float e = 0f; e < BossJumpTime; e += Time.deltaTime)
        {
            float k = e / BossJumpTime;
            Vector3 p = Vector3.Lerp(start, land, k * k);                 // accelerating down
            p.y += Mathf.Sin(Mathf.PI * Mathf.Min(1f, k * 1.6f)) * BossJumpRise * (1f - k);   // the hop out of the hatch
            bt.position = p;
            bt.rotation = Quaternion.Euler(0f, 0f, Mathf.Sin(k * Mathf.PI * 2f) * 8f * (1f - k));
            yield return null;
        }
        bt.position = land;
        bt.rotation = Quaternion.identity;

        // Landing: squash, dust, shake.
        SpawnPuff(land + Vector3.down * bossH * 0.4f, Mathf.Clamp(bossH * 1.6f, 1f, 3f), DustColour, 0f);
        CameraShake.Shake(0.25f, 0.3f);
        AudioManager.Play(AudioManager.Sound.StoneHit);
        for (float e = 0f; e < 0.22f; e += Time.deltaTime)
        {
            float s = 1f + 0.2f * Mathf.Sin(Mathf.PI * (e / 0.22f));
            bt.localScale = new Vector3(baseScale.x * s, baseScale.y * (2f - s), baseScale.z);
            yield return null;
        }
        bt.localScale = baseScale;
        if (!RobotEnemy.SuppressIdleMotion) foreach (var c in bossCols) if (c != null) c.enabled = true;
    }

    // The ship pulls up and away to the upper left, accelerating, then is gone.
    IEnumerator ShipLeaves(Transform ship, Vector3 hover, Vector3 c0, float camW, float camH, float shipW)
    {
        yield return new WaitForSeconds(0.25f);
        Vector3 to = new Vector3(c0.x - camW * 0.5f - shipW, c0.y + camH * 0.5f + shipW * 0.6f, 0f);
        for (float e = 0f; e < ShipOutTime && ship != null; e += Time.deltaTime)
        {
            float k = e / ShipOutTime, ease = k * k * k;
            ship.position = Vector3.Lerp(hover, to, ease) + Vector3.up * Mathf.Sin(k * Mathf.PI) * 0.4f;
            ship.rotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(0f, 12f, k));
            yield return null;
        }
        if (ship != null) Destroy(ship.gameObject);
    }

    static void Hide(Component owner, List<Renderer> hidden)
    {
        if (owner == null) return;
        foreach (var r in owner.GetComponentsInChildren<Renderer>())
        {
            if (!r.enabled) continue;
            r.enabled = false;
            hidden.Add(r);
        }
    }

    static void Show(Component owner, List<Renderer> hidden)
    {
        if (owner == null) return;
        foreach (var r in owner.GetComponentsInChildren<Renderer>(true))
            if (hidden.Remove(r) && r != null) r.enabled = true;
    }

    // 0 -> 1.15 -> 1 of the robot's own (per-level) scale.
    static IEnumerator Pop(Transform t)
    {
        // The pop scales the whole robot, collider included: a growing (and overshooting) collider shoved the live
        // blocks around it and knocked the structure down before the player fired (found 2026-11-08 with
        // Editor/PhysicsProbe). The colliders sit out the pop.
        var cols = t.GetComponentsInChildren<Collider2D>();
        foreach (var c in cols) c.enabled = false;
        try { yield return PopScale(t); }
        // Live-physics levels: leave them off — RobotEnemy.GoLive switches them on together with the live body, and
        // LevelLoader re-sleeps the structure right after (each collider coming back during the reveal woke it).
        finally { if (!RobotEnemy.SuppressIdleMotion) foreach (var c in cols) if (c != null) c.enabled = true; }
    }

    static IEnumerator PopScale(Transform t)
    {
        Vector3 baseScale = t.localScale;
        float e = 0f;
        while (e < RobotPopTime && t != null)
        {
            e += Time.deltaTime;
            float k = Mathf.Clamp01(e / RobotPopTime);
            float s = k < 0.6f ? Mathf.Lerp(0f, 1.15f, Mathf.SmoothStep(0f, 1f, k / 0.6f))
                               : Mathf.Lerp(1.15f, 1f, Mathf.SmoothStep(0f, 1f, (k - 0.6f) / 0.4f));
            t.localScale = new Vector3(baseScale.x * s, baseScale.y * s, baseScale.z);
            yield return null;
        }
        if (t != null) t.localScale = baseScale;
    }

    // A dust puff: a handful of soft circles that swell and fade.
    void SpawnPuff(Vector3 centre, float size, Color colour, float delay)
    {
        _puffSprite ??= MakePuffSprite(64);
        for (int i = 0; i < 5; i++)
        {
            var go = new GameObject("RevealPuff");
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = _puffSprite;
            sr.sortingOrder = PuffSortingOrder;
            sr.color = new Color(colour.r, colour.g, colour.b, 0f);
            Vector2 offset = Random.insideUnitCircle * size * 0.35f;
            go.transform.position = centre + new Vector3(offset.x, offset.y, 0f);
            StartCoroutine(Puff(go.transform, sr, size * Random.Range(0.45f, 0.75f), colour, delay + i * 0.02f));
        }
    }

    static IEnumerator Puff(Transform t, SpriteRenderer sr, float size, Color colour, float delay)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);
        Vector3 drift = new Vector3(Random.Range(-0.3f, 0.3f), Random.Range(0.1f, 0.5f), 0f) * size;
        Vector3 start = t.position;
        float e = 0f;
        while (e < PuffLife && t != null)
        {
            e += Time.deltaTime;
            float k = e / PuffLife;
            float s = size * Mathf.Lerp(0.4f, 1.1f, 1f - (1f - k) * (1f - k));
            t.localScale = new Vector3(s, s, 1f);
            t.position = start + drift * k;
            sr.color = new Color(colour.r, colour.g, colour.b, colour.a * (k < 0.15f ? k / 0.15f : 1f - (k - 0.15f) / 0.85f));
            yield return null;
        }
        if (t != null) Destroy(t.gameObject);
    }

    static Sprite MakePuffSprite(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.ARGB32, false) { wrapMode = TextureWrapMode.Clamp };
        float r = size * 0.5f;
        var px = new Color[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r)) / r;
                float a = Mathf.Clamp01(1f - d);
                px[y * size + x] = new Color(1f, 1f, 1f, a * a * (3f - 2f * a));
            }
        tex.SetPixels(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }
}
