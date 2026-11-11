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
    const int SkyOrder = -34, LanternOrder = -33, VaneOrder = 1;

    enum Kind { Balloon, Kite, Lantern, Plum }

    class Item
    {
        public Kind kind;
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
        Add(Kind.Balloon, _balloon, 0.11f, 0.18f, 0.70f, 1f / 55f);
        Add(Kind.Balloon, _balloon, 0.075f, 0.62f, 0.80f, 1f / 75f);
        Add(Kind.Kite, _kite, 0.09f, 0.42f, 0.84f, 0f);
        Add(Kind.Kite, _kite, 0.065f, 0.86f, 0.74f, 0f);
        Add(Kind.Plum, _plum, 0.07f, 0.74f, 0.58f, 0f);
        for (int i = 0; i < 3; i++)
            Add(Kind.Lantern, _lantern, 0.035f + 0.01f * i, 0.30f + 0.22f * i, 0.30f + 0.18f * i, 1f / (26f + 6f * i));

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

    void Add(Kind kind, Sprite sprite, float heightSh, float xSh, float ySh, float speed)
    {
        if (sprite == null) return;
        var sr = NewRenderer(kind.ToString(), sprite, kind == Kind.Lantern ? LanternOrder : SkyOrder);
        _items.Add(new Item { kind = kind, sr = sr, heightSh = heightSh, xSh = xSh, ySh = ySh, speed = speed,
                              phase = Random.Range(0f, 10f) });
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
                    bob = Mathf.Sin(time * 0.6f + it.phase) * 0.012f;
                    col.a = 0.92f;
                    break;
                case Kind.Kite:      // held in place on the wind: sways, dips and climbs
                    bob = Mathf.Sin(time * 0.9f + it.phase) * 0.02f;
                    rot = Mathf.Sin(time * 1.4f + it.phase) * 9f;
                    break;
                case Kind.Lantern:   // rises slowly, drifting a little, then starts again from below
                    it.ySh += it.speed * dt;
                    if (it.ySh > 1.05f) { it.ySh = 0.18f; it.xSh = Random.Range(0.15f, 0.9f); }
                    it.xSh += Mathf.Sin(time * 0.5f + it.phase) * 0.004f * dt;
                    float glow = _glow ? 1.15f + 0.15f * Mathf.Sin(time * 3f + it.phase) : 1f;
                    col = new Color(glow, glow * 0.95f, glow * 0.8f, _glow ? 1f : 0.85f);
                    break;
                case Kind.Plum:      // bobs on its little cloud
                    bob = Mathf.Sin(time * 0.7f + it.phase) * 0.018f;
                    rot = Mathf.Sin(time * 0.5f + it.phase) * 3f;
                    break;
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
