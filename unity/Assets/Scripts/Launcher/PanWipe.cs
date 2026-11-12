using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A world-themed wipe across the whole screen while the camera pans from one room to the next (2026-11-12, user: "a
/// panning effect that is specific to a world - example - cornfield (hurricane wind), sunken world (bubbles) - that
/// cover the whole screen while it pans"). Built by <see cref="CatapultLauncher.PanToRoom"/> from a list of
/// <see cref="Layer"/>s (each a swarm of one kind of thing: dust clouds, wind streaks, leaves, bubbles, fish...).
///
/// Everything lives in screen space (fractions of the camera view), so it covers the screen however far the camera
/// travels. A coverage envelope builds up over the first ~40% of the pan, holds near full cover around mid-pan (when
/// the camera moves fastest, the swoosh peaks, and the old room drops away), then clears as the new room settles -
/// about 85-90% opaque at its thickest, so room 2's outline still shows through. Purely visual: no colliders. Sorted
/// above the play layer and below the HUD canvas (screen-space overlay), so the room dots and lineup stay visible.
/// Destroys itself when done.
/// </summary>
public class PanWipe : MonoBehaviour
{
    public struct Layer
    {
        public Sprite[] sprites;   // picked at random per particle (null entries skipped)
        public int count;
        public Vector2 size;       // particle height range, in screen heights
        public Vector2 start;      // where particles start: (u, v) screen fractions, spread by `spread`
        public Vector2 spread;     // random extra start offset range (u, v), added to `start`
        public Vector2 travel;     // how far each one moves over the whole wipe (u, v), +-25% random
        public float spin;         // max degrees per second (random sign)
        public float wobble;       // side-to-side sway amplitude (screen fractions, across the travel)
        public Color tint;
        public float alpha;        // peak opacity
        public int order;          // sorting order
        public bool faceTravel;    // flip so the art faces the way it moves (fish)
        public bool[] facesRight;  // per sprite: the art's head points right (with faceTravel)
        public bool veil;          // one full-screen quad of `tint` instead of a swarm (sprites ignored)
    }

    class P
    {
        public SpriteRenderer sr;
        public Layer layer;
        public Vector2 u0, travel;
        public float size, rot, spin, phase, flip;
    }

    readonly List<P> _ps = new();
    Camera _cam;
    float _duration, _age;
    static Sprite _white;

    public static PanWipe Play(Camera cam, float duration, IList<Layer> layers)
    {
        var w = new GameObject("PanWipe").AddComponent<PanWipe>();
        w._cam = cam;
        w._duration = Mathf.Max(0.1f, duration);
        foreach (var l in layers) w.Build(l);
        w.LateUpdate();   // placed and faded-out from the first frame
        return w;
    }

    void Build(Layer l)
    {
        if (l.veil)
        {
            _white ??= MakeWhite();
            _ps.Add(new P { sr = NewRenderer(_white, l.order), layer = l });
            return;
        }
        if (l.sprites == null || l.sprites.Length == 0) return;
        for (int i = 0; i < l.count; i++)
        {
            int k = Random.Range(0, l.sprites.Length);
            var s = l.sprites[k];
            if (s == null) continue;
            var p = new P
            {
                sr = NewRenderer(s, l.order),
                layer = l,
                u0 = l.start + new Vector2(Random.value * l.spread.x, Random.value * l.spread.y),
                travel = l.travel * Random.Range(0.75f, 1.25f),
                size = Random.Range(l.size.x, l.size.y),
                rot = Random.Range(0f, 360f) * (l.spin > 0f ? 1f : 0f),
                spin = l.spin * Random.Range(0.4f, 1f) * (Random.value < 0.5f ? -1f : 1f),
                phase = Random.Range(0f, 6.283f),
            };
            if (l.faceTravel)
            {
                bool right = l.facesRight != null && k < l.facesRight.Length && l.facesRight[k];
                p.flip = (p.travel.x > 0f) != right ? -1f : 1f;
            }
            else p.flip = Random.value < 0.5f ? -1f : 1f;
            _ps.Add(p);
        }
    }

    SpriteRenderer NewRenderer(Sprite s, int order)
    {
        var go = new GameObject("WipeBit");
        go.transform.SetParent(transform, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = s;
        sr.sortingOrder = order;
        return sr;
    }

    // Coverage over the wipe: builds over the first 38%, holds 40-60%, clears from 62%.
    static float Envelope(float t)
    {
        float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.02f, 0.38f, t));
        float b = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.62f, 0.98f, t));
        return Mathf.Min(a, b);
    }

    void LateUpdate()
    {
        if (_cam == null) { Destroy(gameObject); return; }
        _age += Time.deltaTime;
        float t = Mathf.Clamp01(_age / _duration);
        float env = Envelope(t);
        float camH = _cam.orthographicSize * 2f, camW = camH * _cam.aspect;
        Vector3 c = _cam.transform.position;
        float left = c.x - camW * 0.5f, bottom = c.y - camH * 0.5f;
        foreach (var p in _ps)
        {
            var l = p.layer;
            var col = l.tint;
            col.a = l.alpha * env;
            p.sr.color = col;
            if (l.veil)
            {
                Vector2 n = p.sr.sprite.bounds.size;
                p.sr.transform.position = new Vector3(c.x, c.y, 0f);
                p.sr.transform.localScale = new Vector3(camW * 1.1f / n.x, camH * 1.1f / n.y, 1f);
                continue;
            }
            Vector2 uv = p.u0 + p.travel * t;
            uv.x += Mathf.Sin(t * 9f + p.phase) * l.wobble;
            float sc = p.size * camH / Mathf.Max(0.0001f, p.sr.sprite.bounds.size.y);
            p.sr.transform.position = new Vector3(left + uv.x * camW, bottom + uv.y * camH, 0f);
            p.sr.transform.localScale = new Vector3(sc * p.flip, sc, 1f);
            p.sr.transform.rotation = Quaternion.Euler(0f, 0f, p.rot + p.spin * _age);
        }
        if (t >= 1f) Destroy(gameObject);
    }

    static Sprite MakeWhite()
    {
        var tex = new Texture2D(4, 4, TextureFormat.ARGB32, false);
        var px = new Color[16];
        for (int i = 0; i < 16; i++) px[i] = Color.white;
        tex.SetPixels(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4);
    }
}
