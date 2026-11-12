using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Ambient life over the three paid worlds (2026-11-12, built on the World 1-6 template; SpaceDecor / SeaDecor are the
/// models). One component, three moods, chosen per level by <see cref="EnvironmentDepthSystem"/>:
///   World 7 Dustbowl Canyon   - tumbleweeds bouncing along the ground, sandstorm dust banks drifting across, a hawk
///                               circling high up (Stampede Tumbleweed / SandstormDust, the hawk is a drawn chevron).
///   World 8 Harvest Fairground - hot-air balloons drifting over the fair, fireworks bursting at night (drawn sparks).
///   World 9 Crop Factory       - steam plumes rising off distant chimneys, sparks drifting, a crate-carrying drone.
/// Purely decorative (no colliders), placed in screen fractions so it reads the same on every aspect and travels with
/// the camera from room to room. Everything sits behind the play layer.
/// </summary>
public class PaidWorldDecor : MonoBehaviour
{
    public enum Mood { Canyon, Fair, Factory }

    const int FarOrder = -37, MidOrder = -34, NearOrder = -29;

    enum Kind { Tumbleweed, Dust, Hawk, Balloon, Spark, Steam, Drone }

    class Item
    {
        public Kind kind;
        public SpriteRenderer sr;
        public float heightSh, xSh, ySh, vx, vy, rot, spin, alpha, phase, wait, life, age;
        public Color colour = Color.white;
    }

    Camera _cam;
    Sprite _tumbleweed, _dust, _balloon, _steam, _drone, _dot, _hawk;
    readonly List<Item> _items = new();
    Color _tint = Color.white;
    Mood _mood;
    float _groundY;
    bool _night;
    float _fireworkWait;

    public void Init(Camera cam, Sprite tumbleweed, Sprite dust, Sprite balloon, Sprite steam, Sprite drone)
    {
        _cam = cam; _tumbleweed = tumbleweed; _dust = dust; _balloon = balloon; _steam = steam; _drone = drone;
        _dot = MakeDot();
        _hawk = MakeHawk();
    }

    /// <summary>night: fireworks over the fair (CarnivalNight / HarvestMoon), sparks glow brighter in the factory.</summary>
    public void ResetFor(Mood mood, float groundY, Color tint, bool night)
    {
        _mood = mood; _groundY = groundY; _tint = tint; _night = night;
        foreach (var it in _items) if (it.sr != null) Destroy(it.sr.gameObject);
        _items.Clear();
        _fireworkWait = 1.2f;
        switch (mood)
        {
            case Mood.Canyon:
                //   kind, order, height (sh), x, y, vx, vy (screens / s), rotation, spin, alpha
                Add(Kind.Dust, FarOrder, 0.22f, 0.20f, 0.30f, 1f / 70f, 0f, 0f, 0f, 0.35f);
                Add(Kind.Dust, MidOrder, 0.28f, 0.80f, 0.22f, 1f / 55f, 0f, 0f, 0f, 0.3f);
                Add(Kind.Hawk, FarOrder, 0.025f, 0.6f, 0.82f, 0f, 0f, 0f, 0f, 0.8f);
                Add(Kind.Hawk, FarOrder, 0.018f, 0.3f, 0.76f, 0f, 0f, 0f, 0f, 0.65f);
                for (int i = 0; i < 3; i++)
                {
                    var t = Add(Kind.Tumbleweed, NearOrder, Random.Range(0.045f, 0.075f), -0.2f, 0f, 0f, 0f, 0f, 0f, 1f);
                    if (t != null) t.wait = Random.Range(0.5f, 9f) + i * 3f;
                }
                break;
            case Mood.Fair:
                Add(Kind.Balloon, FarOrder, 0.06f, 0.25f, 0.78f, 1f / 220f, 0.0008f, 0f, 0f, 0.8f).colour = new Color(1f, 0.85f, 0.85f);
                Add(Kind.Balloon, MidOrder, 0.09f, 0.70f, 0.70f, -1f / 180f, 0.001f, 0f, 0f, 0.95f);
                Add(Kind.Balloon, FarOrder, 0.045f, 0.92f, 0.86f, 1f / 260f, 0f, 0f, 0f, 0.7f).colour = new Color(0.85f, 0.9f, 1f);
                break;
            case Mood.Factory:
                for (int i = 0; i < 5; i++)
                {
                    float x = 0.12f + i * 0.2f + Random.Range(-0.04f, 0.04f);
                    for (int k = 0; k < 4; k++)
                    {
                        var s = Add(Kind.Steam, FarOrder, 0.06f, x, 0.42f, 0f, 0f, Random.Range(0f, 360f), Random.Range(-20f, 20f), 0.5f);
                        if (s != null) { s.phase = x; s.age = k * 1.1f; s.life = 4.4f; }
                    }
                }
                for (int i = 0; i < 18; i++)
                {
                    var sp = Add(Kind.Spark, MidOrder, 0.006f, Random.value, Random.value, 0f, 0f, 0f, 0f, 0.8f);
                    if (sp != null) sp.colour = new Color(1f, 0.75f, 0.35f);
                }
                var dr = Add(Kind.Drone, MidOrder, 0.07f, -0.2f, 0.78f, 1f / 30f, 0f, 0f, 0f, 0.9f);
                if (dr != null) dr.wait = Random.Range(3f, 8f);
                break;
        }
    }

    Item Add(Kind kind, int order, float heightSh, float x, float y, float vx, float vy, float rot, float spin, float alpha)
    {
        Sprite s = kind switch
        {
            Kind.Tumbleweed => _tumbleweed, Kind.Dust => _dust, Kind.Hawk => _hawk, Kind.Balloon => _balloon,
            Kind.Steam => _steam, Kind.Drone => _drone, _ => _dot,
        };
        if (s == null) return null;
        var go = new GameObject($"{kind}");
        go.transform.SetParent(transform, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = s;
        sr.sortingOrder = order;
        var it = new Item { kind = kind, sr = sr, heightSh = heightSh, xSh = x, ySh = y, vx = vx, vy = vy, rot = rot,
                            spin = spin, alpha = alpha, phase = Random.Range(0f, 10f) };
        _items.Add(it);
        return it;
    }

    // A firework: a ring of sparks bursting from one point, falling a little and fading.
    void Firework(float x, float y)
    {
        Color c = Color.HSVToRGB(Random.value, 0.65f, 1f);
        int n = 22;
        float speed = Random.Range(0.10f, 0.16f);
        for (int i = 0; i < n; i++)
        {
            float a = i * Mathf.PI * 2f / n;
            var it = Add(Kind.Spark, FarOrder, 0.008f, x, y, Mathf.Cos(a) * speed, Mathf.Sin(a) * speed, 0f, 0f, 1f);
            if (it == null) return;
            it.colour = c; it.life = 1.4f; it.age = 0f;
        }
    }

    void Update()
    {
        if (_cam == null) return;
        float dt = Time.deltaTime, time = Time.time;
        float camH = _cam.orthographicSize * 2f, camW = camH * _cam.aspect;
        Vector3 c = _cam.transform.position;
        float left = c.x - camW * 0.5f, bottom = c.y - camH * 0.5f;
        float groundSh = (_groundY - bottom) / camH;

        if (_mood == Mood.Fair && _night)
        {
            _fireworkWait -= dt;
            if (_fireworkWait <= 0f)
            {
                Firework(Random.Range(0.25f, 0.95f), Random.Range(0.62f, 0.88f));
                _fireworkWait = Random.Range(1.4f, 3.2f);
            }
        }

        for (int i = _items.Count - 1; i >= 0; i--)
        {
            var it = _items[i];
            if (it.sr == null) { _items.RemoveAt(i); continue; }
            float a = it.alpha;
            switch (it.kind)
            {
                case Kind.Tumbleweed:   // waits, then bounces along the ground left to right, spinning
                    if (it.wait > 0f)
                    {
                        it.wait -= dt;
                        it.sr.enabled = false;
                        if (it.wait > 0f) continue;
                        it.xSh = -0.1f; it.vx = Random.Range(0.08f, 0.14f);
                    }
                    it.sr.enabled = true;
                    it.xSh += it.vx * dt;
                    it.rot -= it.vx * camW / (it.heightSh * camH * 0.5f) * Mathf.Rad2Deg * dt;
                    float hop = Mathf.Abs(Mathf.Sin(time * 3.2f + it.phase)) * 0.035f;
                    it.ySh = groundSh + it.heightSh * 0.45f + hop;
                    if (it.xSh > 1.15f) it.wait = Random.Range(4f, 12f);
                    break;
                case Kind.Dust:         // a sandstorm bank drifting across, breathing in and out
                    it.xSh += it.vx * dt;
                    if (it.xSh > 1.3f) it.xSh = -0.3f;
                    a *= 0.75f + 0.25f * Mathf.Sin(time * 0.5f + it.phase);
                    it.ySh = groundSh + 0.12f;
                    break;
                case Kind.Hawk:         // circling high up
                    {
                        float r = 0.06f, w = 0.35f;
                        float px = it.xSh + Mathf.Cos(time * w + it.phase) * r;
                        float py = it.ySh + Mathf.Sin(time * w + it.phase) * r * 0.35f;
                        Place(it, left + px * camW, bottom + py * camH, camH, a);
                        it.sr.flipX = Mathf.Sin(time * w + it.phase) > 0f;
                        continue;
                    }
                case Kind.Balloon:      // drifts and bobs, wrapping round
                    it.xSh += it.vx * dt; it.ySh += it.vy * Mathf.Sin(time * 0.3f + it.phase) * dt;
                    if (it.xSh > 1.15f) it.xSh = -0.15f; else if (it.xSh < -0.15f) it.xSh = 1.15f;
                    break;
                case Kind.Spark:
                    if (it.life > 0f)   // a firework spark: slows, falls, fades, then goes
                    {
                        it.age += dt;
                        if (it.age >= it.life) { Destroy(it.sr.gameObject); _items.RemoveAt(i); continue; }
                        it.vx *= 1f - 1.6f * dt; it.vy = it.vy * (1f - 1.6f * dt) - 0.05f * dt;
                        it.xSh += it.vx * dt; it.ySh += it.vy * dt * camW / camH;
                        a *= 1f - it.age / it.life;
                    }
                    else                // a drifting factory spark: rises and flickers, wrapping round
                    {
                        it.ySh += 0.02f * dt; it.xSh += 0.01f * Mathf.Sin(time + it.phase) * dt;
                        if (it.ySh > 1.05f) { it.ySh = -0.02f; it.xSh = Random.value; }
                        a *= (_night ? 1f : 0.6f) * (0.5f + 0.5f * Mathf.Abs(Mathf.Sin(time * 4f + it.phase * 3f)));
                    }
                    break;
                case Kind.Steam:        // puffs rise from a distant chimney, growing and fading, then start again
                    it.age += dt;
                    if (it.age > it.life) it.age -= it.life;
                    {
                        float t = it.age / it.life;
                        it.xSh = it.phase + t * 0.04f;
                        it.ySh = 0.42f + t * 0.35f;
                        it.rot += it.spin * dt;
                        float grow = 0.6f + t * 1.4f;
                        Place(it, left + it.xSh * camW, bottom + it.ySh * camH, camH, a * Mathf.Sin(t * Mathf.PI), grow);
                        continue;
                    }
                case Kind.Drone:        // a crate drone crossing now and then
                    if (it.wait > 0f)
                    {
                        it.wait -= dt;
                        it.sr.enabled = false;
                        if (it.wait > 0f) continue;
                        it.xSh = -0.15f; it.ySh = Random.Range(0.7f, 0.86f);
                    }
                    it.sr.enabled = true;
                    it.xSh += it.vx * dt;
                    it.ySh += Mathf.Sin(time * 1.5f + it.phase) * 0.01f * dt;
                    if (it.xSh > 1.2f) it.wait = Random.Range(8f, 16f);
                    break;
            }
            Place(it, left + it.xSh * camW, bottom + it.ySh * camH, camH, a);
        }
    }

    void Place(Item it, float x, float y, float camH, float alpha, float grow = 1f)
    {
        Sprite s = it.sr.sprite;
        float scale = it.heightSh * camH / Mathf.Max(0.0001f, s.bounds.size.y) * grow;
        it.sr.transform.position = new Vector3(x, y, 0f);
        it.sr.transform.localScale = new Vector3(scale, scale, 1f);
        it.sr.transform.rotation = Quaternion.Euler(0f, 0f, it.rot);
        Color col = it.kind == Kind.Spark ? it.colour : it.colour * _tint;
        it.sr.color = new Color(col.r, col.g, col.b, alpha);
    }

    static Sprite MakeDot()
    {
        const int N = 32;
        var tex = new Texture2D(N, N, TextureFormat.ARGB32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(N / 2f, N / 2f)) / (N / 2f);
                float a = Mathf.Clamp01(1f - d);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
            }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), N);
    }

    // A dark bird silhouette (a soft chevron) for the circling hawk.
    static Sprite MakeHawk()
    {
        const int W = 64, H = 24;
        var tex = new Texture2D(W, H, TextureFormat.ARGB32, false) { wrapMode = TextureWrapMode.Clamp };
        var clear = new Color(0, 0, 0, 0);
        for (int y = 0; y < H; y++) for (int x = 0; x < W; x++) tex.SetPixel(x, y, clear);
        for (int x = 0; x < W; x++)
        {
            float u = (x - W / 2f) / (W / 2f);                    // -1..1
            float wing = 14f - Mathf.Abs(u) * 6f + Mathf.Sin(Mathf.Abs(u) * Mathf.PI) * 4f;   // the wing curve
            float th = 3.2f * (1f - Mathf.Abs(u)) + 1.2f;
            for (int y = 0; y < H; y++)
            {
                float dd = Mathf.Abs(y - wing);
                if (dd < th) tex.SetPixel(x, y, new Color(0.16f, 0.1f, 0.08f, Mathf.Clamp01(th - dd)));
            }
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), H);
    }
}
