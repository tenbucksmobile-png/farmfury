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
        _routine = StartCoroutine(Reveal());
    }

    IEnumerator Reveal()
    {
        yield return null;   // LevelLoader spawns in its own OnLevelStarted handler

        var blocks = new List<BlockBase>(FindObjectsByType<BlockBase>());
        var robots = new List<RobotEnemy>(FindObjectsByType<RobotEnemy>());
        var hidden = new List<Renderer>();
        foreach (var b in blocks) Hide(b, hidden);
        foreach (var r in robots) Hide(r, hidden);

        yield return new WaitForSeconds(EmptyBeat);

        // The structure appears in one frame, in dust.
        foreach (var b in blocks) Show(b, hidden);
        int puffs = 0;
        foreach (var b in blocks)
        {
            if (b == null || puffs >= MaxBlockPuffs) continue;
            var sr = b.GetComponent<SpriteRenderer>();
            Vector2 size = sr != null ? (Vector2)sr.bounds.size : Vector2.one;
            SpawnPuff(b.transform.position, Mathf.Max(size.x, size.y) * 1.5f, DustColour, Random.Range(0f, 0.08f));
            puffs++;
        }

        yield return new WaitForSeconds(RobotsDelay);

        // Robots pop in one after another, top of the structure first.
        robots.RemoveAll(r => r == null);
        robots.Sort((a, b) => b.transform.position.y.CompareTo(a.transform.position.y));
        foreach (var r in robots)
        {
            if (r == null) continue;
            Show(r, hidden);
            var sr = r.GetComponent<SpriteRenderer>();
            float size = sr != null ? Mathf.Max(sr.bounds.size.x, sr.bounds.size.y) : 1f;
            SpawnPuff(r.transform.position, size * 1.3f, RobotPuff, 0f);
            StartCoroutine(Pop(r.transform));
            yield return new WaitForSeconds(RobotStagger);
        }

        yield return new WaitForSeconds(RobotPopTime);
        IsRevealing = false;
        _routine = null;
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
