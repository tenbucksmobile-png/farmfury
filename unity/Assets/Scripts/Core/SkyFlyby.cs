using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Ambient life in the sky over the World 1 cornfield (2026-11-08): every so often a biplane crosses left to right
/// trailing smoke puffs, and now and then a robot scout drone patrols right to left. Purely decorative — no
/// colliders, drawn between the hazy far painting and the mid corn, sized and placed in screen fractions so it
/// reads the same on every aspect ratio. Created and enabled by <see cref="EnvironmentDepthSystem"/> only while
/// its cornfield layers are showing. Update-driven (not a coroutine) so it survives being disabled/re-enabled.
/// </summary>
public class SkyFlyby : MonoBehaviour
{
    const int   SortingOrder   = -31;            // haze veil -35 < mist -32 < fly-bys < mid corn -30
    const float PlaneHeightSh  = 0.075f;         // plane drawn this tall (fraction of screen height)
    const float DroneHeightSh  = 0.05f;
    const float PlaneCrossSec  = 11f;            // time to cross the screen
    const float DroneCrossSec  = 15f;
    const float FirstDelay     = 2.5f;           // first fly-by shortly after the level appears
    const float MinGap = 9f, MaxGap = 18f;       // pause between fly-bys
    const float DroneChance    = 0.3f;
    const float PuffEvery      = 0.12f;          // smoke trail (plane only)
    const float PuffLife       = 1.6f;

    Camera _cam;
    Sprite _plane, _drone, _puff;
    SpriteRenderer _flyer;
    float _timer = FirstDelay, _t, _crossSec, _ySh, _puffTimer, _phase;
    bool _flying, _isDrone;

    struct Puff { public SpriteRenderer sr; public Vector2 screenOffset; public float age; public float size; }
    readonly List<Puff> _puffs = new();
    readonly Stack<SpriteRenderer> _puffPool = new();

    public void Init(Camera cam, Sprite plane, Sprite drone)
    {
        _cam = cam; _plane = plane; _drone = drone;
        _flyer = NewRenderer("Flyer");
        _flyer.enabled = false;
        _puff = MakePuffSprite();
    }

    /// <summary>Restart the schedule (called on every level start).</summary>
    public void ResetSchedule()
    {
        _flying = false;
        if (_flyer != null) _flyer.enabled = false;
        _timer = FirstDelay;
        foreach (var p in _puffs) Recycle(p.sr);
        _puffs.Clear();
    }

    void Update()
    {
        if (_cam == null || _flyer == null) return;
        float dt = Time.deltaTime;
        float camH = _cam.orthographicSize * 2f, camW = camH * _cam.aspect;
        Vector3 c = _cam.transform.position;

        if (!_flying)
        {
            _timer -= dt;
            if (_timer <= 0f) Launch();
        }
        else
        {
            _t += dt / _crossSec;
            float heightSh = _isDrone ? DroneHeightSh : PlaneHeightSh;
            Sprite s = _flyer.sprite;
            float scale = heightSh * camH / s.bounds.size.y;
            float w = s.bounds.size.x * scale;
            // Screen x from just off one edge to just off the other.
            float u = _isDrone ? 1f - _t : _t;
            float x = c.x - camW * 0.5f - w + u * (camW + 2f * w);
            float bob = Mathf.Sin((_t * _crossSec + _phase) * (_isDrone ? 3.1f : 1.3f)) * (_isDrone ? 0.008f : 0.012f) * camH;
            float y = c.y - camH * 0.5f + _ySh * camH + bob;
            _flyer.transform.position = new Vector3(x, y, 0f);
            _flyer.transform.localScale = new Vector3(scale, scale, 1f);
            _flyer.transform.rotation = Quaternion.Euler(0f, 0f,
                _isDrone ? Mathf.Sin(_t * 40f) * 3f : Mathf.Cos((_t * _crossSec + _phase) * 1.3f) * 4f);

            if (!_isDrone)
            {
                _puffTimer -= dt;
                if (_puffTimer <= 0f)
                {
                    _puffTimer = PuffEvery;
                    // From the tail (left end of a right-facing plane), stored relative to the camera.
                    Vector2 tail = new Vector2(x - w * 0.45f, y - 0.1f * heightSh * camH);
                    SpawnPuff(tail - (Vector2)c, heightSh * camH * 0.35f);
                }
            }
            if (_t >= 1f) { _flying = false; _flyer.enabled = false; _timer = Random.Range(MinGap, MaxGap); }
        }

        for (int i = _puffs.Count - 1; i >= 0; i--)
        {
            var p = _puffs[i];
            p.age += dt;
            if (p.age >= PuffLife) { Recycle(p.sr); _puffs.RemoveAt(i); continue; }
            float k = p.age / PuffLife;
            float size = p.size * (1f + 1.2f * k);
            p.sr.transform.position = new Vector3(c.x + p.screenOffset.x, c.y + p.screenOffset.y + k * 0.01f * camH, 0f);
            p.sr.transform.localScale = new Vector3(size, size, 1f);
            p.sr.color = new Color(1f, 1f, 1f, 0.55f * (1f - k));
            _puffs[i] = p;
        }
    }

    void Launch()
    {
        _isDrone = _drone != null && (_plane == null || Random.value < DroneChance);
        Sprite s = _isDrone ? _drone : _plane;
        if (s == null) { _timer = MaxGap; return; }
        _flyer.sprite = s;
        _flyer.enabled = true;
        _flying = true;
        _t = 0f;
        _crossSec = _isDrone ? DroneCrossSec : PlaneCrossSec;
        _ySh = _isDrone ? Random.Range(0.62f, 0.72f) : Random.Range(0.72f, 0.84f);   // well clear of the HUD row
        _phase = Random.Range(0f, 10f);
        _puffTimer = 0f;
    }

    void SpawnPuff(Vector2 screenOffset, float size)
    {
        var sr = _puffPool.Count > 0 ? _puffPool.Pop() : NewRenderer("Puff");
        sr.sprite = _puff;
        sr.sortingOrder = SortingOrder - 1;
        sr.enabled = true;
        _puffs.Add(new Puff { sr = sr, screenOffset = screenOffset, age = 0f, size = size });
    }

    void Recycle(SpriteRenderer sr)
    {
        if (sr == null) return;
        sr.enabled = false;
        _puffPool.Push(sr);
    }

    SpriteRenderer NewRenderer(string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sortingOrder = SortingOrder;
        return sr;
    }

    // 1-unit soft white disc.
    static Sprite MakePuffSprite()
    {
        const int N = 32;
        var tex = new Texture2D(N, N, TextureFormat.ARGB32, false) { wrapMode = TextureWrapMode.Clamp };
        var px = new Color[N * N];
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float dx = (x + 0.5f) / N - 0.5f, dy = (y + 0.5f) / N - 0.5f;
                float d = Mathf.Sqrt(dx * dx + dy * dy) * 2f;
                px[y * N + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(1f - d) * Mathf.Clamp01(1f - d));
            }
        tex.SetPixels(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), N);
    }
}
