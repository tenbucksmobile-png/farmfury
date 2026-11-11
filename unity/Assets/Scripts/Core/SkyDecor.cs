using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Things floating in the sky over World 4 (Sky Islands), 2026-11-11 (user: "looks good - but a little empty.
/// incorporate some more of the artwork found in stampede, kite and other flying objects"). All Stampede Sky Island art:
/// two hot-air balloons drifting slowly across at different heights, two kites swaying on the wind, paper lanterns
/// rising slowly (glowing brighter at sunset and at night), the floating plum on its cloud bobbing, and the rooster
/// weathervane standing on the ground beside the cannon. Purely decorative: no colliders. Sky items are placed in
/// screen fractions (the far sky stays fixed to the screen, reference rule B1) so they read the same on every aspect;
/// the weathervane stands in the world. Created by <see cref="EnvironmentDepthSystem"/>, active only on World 4.
/// </summary>
public class SkyDecor : MonoBehaviour
{
    // Between the haze veil (-35) and the isles band (-30): visible, but clearly behind the play layer.
    // Far items sit just behind the haze veil (-35) so it hazes them; near items in front of the isles band (-30).
    const int FarOrder = -36, SkyOrder = -34, LanternOrder = -33, NearOrder = -29, VaneOrder = 1;
    static readonly Color FarHaze = new Color(0.86f, 0.93f, 1f, 1f);

    enum Kind { Balloon, Kite, Lantern, Plum }
    enum Depth { Far, Mid, Near }

    class Item
    {
        public Kind kind;
        public Depth depth;
        public SpriteRenderer sr;
        public float heightSh, xSh, ySh, speed, phase;
    }

    Camera _cam;
    Sprite _balloon, _kite, _lantern, _plum, _vane;
    readonly List<Item> _items = new();
    SpriteRenderer _vaneSr;
    Color _tint = Color.white;
    bool _glow;

    public void Init(Camera cam, Sprite balloon, Sprite kite, Sprite lantern, Sprite plum, Sprite vane)
    {
        _cam = cam; _balloon = balloon; _kite = kite; _lantern = lantern; _plum = plum; _vane = vane;
    }

    /// <summary>Lay the sky out for a level: the weather's sky tint, lanterns glowing at sunset / night.</summary>
    public void ResetFor(float groundY, Vector3 cannonPos, Color skyTint, bool glow)
    {
        _tint = skyTint; _glow = glow;
        foreach (var it in _items) if (it.sr != null) Destroy(it.sr.gameObject);
        _items.Clear();
        // A busy sky in three depth bands (2026-11-11, user: "visible at various sizes so the skies look busy"):
        // far = small, hazed toward the horizon, behind the haze veil's front; mid = medium; near = large, in front of
        // the isles band but still behind every play-layer sprite. Sizes are screen-height fractions, x/y screen
        // fractions; speed in screen widths per second (negative drifts right-to-left).
        //   kind, depth, height, x, y, speed
        Add(Kind.Balloon, Depth.Far,  0.045f, 0.08f, 0.66f,  1f / 140f);
        Add(Kind.Balloon, Depth.Far,  0.055f, 0.55f, 0.90f, -1f / 120f);
        Add(Kind.Balloon, Depth.Mid,  0.085f, 0.30f, 0.76f,  1f / 80f);
        Add(Kind.Balloon, Depth.Mid,  0.10f,  0.82f, 0.64f, -1f / 95f);
        Add(Kind.Balloon, Depth.Near, 0.15f,  0.66f, 0.80f,  1f / 60f);
        Add(Kind.Kite, Depth.Far,  0.045f, 0.20f, 0.88f, 0f);
        Add(Kind.Kite, Depth.Mid,  0.07f,  0.45f, 0.70f, 0f);
        Add(Kind.Kite, Depth.Mid,  0.08f,  0.93f, 0.86f, 0f);
        Add(Kind.Kite, Depth.Near, 0.12f,  0.40f, 0.90f, 0f);
        Add(Kind.Plum, Depth.Far, 0.05f,  0.95f, 0.56f, 0f);
        Add(Kind.Plum, Depth.Mid, 0.075f, 0.14f, 0.80f, 0f);
        for (int i = 0; i < 7; i++)   // lanterns, small to medium, staggered up the screen
            Add(Kind.Lantern, i % 3 == 0 ? Depth.Far : Depth.Mid, 0.02f + 0.006f * (i % 4), 0.12f + 0.125f * i,
                0.22f + 0.11f * ((i * 3) % 7), 1f / (24f + 5f * (i % 4)));

        if (_vane != null)
        {
            if (_vaneSr == null) _vaneSr = NewRenderer("Weathervane", _vane, VaneOrder);
            float h = 1.35f, s = h / _vane.bounds.size.y;
            _vaneSr.transform.localScale = new Vector3(s, s, 1f);
            // On the ground just right of the cannon's cloud-bale mount, base on the ground line.
            _vaneSr.transform.position = new Vector3(cannonPos.x + 2.55f, groundY - _vane.bounds.min.y * s, 0f);
            _vaneSr.color = Color.Lerp(Color.white, skyTint, 0.4f);
            _vaneSr.enabled = true;
        }
    }

    void Add(Kind kind, Depth depth, float heightSh, float xSh, float ySh, float speed)
    {
        Sprite sprite = kind switch { Kind.Balloon => _balloon, Kind.Kite => _kite, Kind.Lantern => _lantern, _ => _plum };
        if (sprite == null) return;
        int order = depth == Depth.Far ? FarOrder : depth == Depth.Near ? NearOrder : kind == Kind.Lantern ? LanternOrder : SkyOrder;
        var sr = NewRenderer($"{kind}_{depth}", sprite, order);
        sr.flipX = kind == Kind.Kite && (_items.Count & 1) == 1;
        _items.Add(new Item { kind = kind, depth = depth, sr = sr, heightSh = heightSh, xSh = xSh, ySh = ySh,
                              speed = speed, phase = Random.Range(0f, 10f) });
    }

    SpriteRenderer NewRenderer(string name, Sprite sprite, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = order;
        return sr;
    }

    void Update()
    {
        if (_cam == null) return;
        float dt = Time.deltaTime, time = Time.time;
        float camH = _cam.orthographicSize * 2f, camW = camH * _cam.aspect;
        Vector3 c = _cam.transform.position;
        float left = c.x - camW * 0.5f, bottom = c.y - camH * 0.5f;
        foreach (var it in _items)
        {
            Sprite s = it.sr.sprite;
            float scale = it.heightSh * camH / s.bounds.size.y;
            float bob = 0f, rot = 0f;
            Color col = _tint;
            switch (it.kind)
            {
                case Kind.Balloon:   // drifts left to right, wrapping, bobbing gently
                    it.xSh += it.speed * dt;
                    if (it.xSh > 1.15f) it.xSh = -0.15f;
                    else if (it.xSh < -0.15f) it.xSh = 1.15f;
                    bob = Mathf.Sin(time * 0.6f + it.phase) * 0.012f;
                    col.a = 0.92f;
                    break;
                case Kind.Kite:      // held in place on the wind: sways, dips and climbs
                    bob = Mathf.Sin(time * 0.9f + it.phase) * 0.02f;
                    rot = Mathf.Sin(time * 1.4f + it.phase) * 9f;
                    break;
                case Kind.Lantern:   // rises slowly, drifting a little, then starts again from below
                    it.ySh += it.speed * dt;
                    if (it.ySh > 1.05f) { it.ySh = 0.18f; it.xSh = Random.Range(0.05f, 0.95f); }
                    it.xSh += Mathf.Sin(time * 0.5f + it.phase) * 0.004f * dt;
                    float glow = _glow ? 1.15f + 0.15f * Mathf.Sin(time * 3f + it.phase) : 1f;
                    col = new Color(glow, glow * 0.95f, glow * 0.8f, _glow ? 1f : 0.85f);
                    break;
                case Kind.Plum:      // bobs on its little cloud
                    bob = Mathf.Sin(time * 0.7f + it.phase) * 0.018f;
                    rot = Mathf.Sin(time * 0.5f + it.phase) * 3f;
                    break;
            }
            if (it.depth == Depth.Far)   // far away: washed toward the horizon haze and a little see-through
            {
                col = Color.Lerp(col, FarHaze * _tint, 0.45f);
                col.a *= 0.8f;
            }
            it.sr.transform.position = new Vector3(left + it.xSh * camW, bottom + (it.ySh + bob) * camH, 0f);
            it.sr.transform.localScale = new Vector3(scale, scale, 1f);
            it.sr.transform.rotation = Quaternion.Euler(0f, 0f, rot);
            it.sr.color = col;
        }
        if (_vaneSr != null && _vaneSr.enabled)   // the rooster swings in the wind
            _vaneSr.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Sin(time * 1.1f) * 4f);
    }
}
