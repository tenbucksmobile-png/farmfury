using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The sky over World 6 (Robot Mothership), 2026-11-12 (user: "remove the flying purple drones ... insert things like
/// asteroids, galaxy milky ways, comets to make the sky look busy ... remove the smaller motherships, only keep the big
/// one as a backdrop"). Kling art (tools/import_mothership_art.py, W6SpaceDecor.png): a faint milky-way band and spiral
/// galaxies far back, asteroids tumbling slowly at three depths, and now and then a comet streaking across. Purely
/// decorative: no colliders. Placed in screen fractions (the far backdrop stays fixed to the screen) so it reads the
/// same on every aspect and travels with the camera from room to room. Created by <see cref="EnvironmentDepthSystem"/>.
/// </summary>
public class SpaceDecor : MonoBehaviour
{
    // Just over the far painting (-40) and behind the haze veil (-35) for the far things; near asteroids in front of the
    // backdrop layers but behind every play-layer sprite.
    const int MilkyOrder = -39, GalaxyOrder = -38, FarOrder = -37, CometOrder = -36, MidOrder = -34, NearOrder = -29;

    enum Kind { Milky, Galaxy, Asteroid, Comet }

    class Item
    {
        public Kind kind;
        public SpriteRenderer sr;
        public float heightSh, xSh, ySh, vx, vy, rot, spin, alpha, phase, wait;
    }

    Camera _cam;
    Sprite _asteroid, _comet, _galaxy, _milky;
    readonly List<Item> _items = new();
    Color _tint = Color.white;

    public void Init(Camera cam, Sprite asteroid, Sprite comet, Sprite galaxy, Sprite milky)
    {
        _cam = cam; _asteroid = asteroid; _comet = comet; _galaxy = galaxy; _milky = milky;
    }

    public void ResetFor(Color tint)
    {
        _tint = tint;
        foreach (var it in _items) if (it.sr != null) Destroy(it.sr.gameObject);
        _items.Clear();
        //   kind, order, height (sh), x, y, vx, vy (screen widths / heights per second), rotation, spin (deg/s), alpha
        Add(Kind.Milky, MilkyOrder, 0.16f, 0.42f, 0.80f, 0f, 0f, -12f, 0f, 0.55f);
        Add(Kind.Milky, MilkyOrder, 0.10f, 0.86f, 0.60f, 0f, 0f, 18f, 0f, 0.35f);
        Add(Kind.Galaxy, GalaxyOrder, 0.12f, 0.72f, 0.86f, 0f, 0f, 0f, -4f, 0.75f);
        Add(Kind.Galaxy, GalaxyOrder, 0.07f, 0.16f, 0.66f, 0f, 0f, 30f, 6f, 0.55f);
        Add(Kind.Asteroid, FarOrder, 0.025f, 0.30f, 0.70f, 1f / 160f, 0.001f, 0f, 14f, 0.7f);
        Add(Kind.Asteroid, FarOrder, 0.02f, 0.58f, 0.74f, -1f / 140f, 0f, 40f, -20f, 0.7f);
        Add(Kind.Asteroid, FarOrder, 0.03f, 0.92f, 0.52f, -1f / 180f, -0.001f, 100f, 10f, 0.7f);
        Add(Kind.Asteroid, MidOrder, 0.05f, 0.48f, 0.90f, 1f / 110f, 0f, 20f, -16f, 0.95f);
        Add(Kind.Asteroid, MidOrder, 0.06f, 0.08f, 0.88f, -1f / 120f, 0.0015f, 70f, 12f, 0.95f);
        Add(Kind.Asteroid, NearOrder, 0.09f, 0.97f, 0.93f, -1f / 90f, 0f, 150f, -9f, 1f);
        var c1 = Add(Kind.Comet, CometOrder, 0.09f, 1.3f, 1.2f, 0f, 0f, 0f, 0f, 1f);
        var c2 = Add(Kind.Comet, CometOrder, 0.06f, 1.3f, 1.2f, 0f, 0f, 0f, 0f, 0.8f);
        if (c1 != null) c1.wait = Random.Range(1.5f, 4f);
        if (c2 != null) c2.wait = Random.Range(6f, 11f);
    }

    Item Add(Kind kind, int order, float heightSh, float x, float y, float vx, float vy, float rot, float spin, float alpha)
    {
        Sprite s = kind switch { Kind.Milky => _milky, Kind.Galaxy => _galaxy, Kind.Comet => _comet, _ => _asteroid };
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

    // A comet starts above the top-right corner and streaks down-left (its head is at the art's lower left).
    static void LaunchComet(Item it)
    {
        it.xSh = Random.Range(0.6f, 1.2f);
        it.ySh = 1.12f;
        float speed = Random.Range(0.35f, 0.5f);           // screen widths per second, along its path
        it.vx = -speed; it.vy = -speed * Random.Range(0.35f, 0.5f);
        it.wait = 0f;
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
            float a = it.alpha;
            switch (it.kind)
            {
                case Kind.Milky:     // barely moving, softly shimmering
                    a *= 0.85f + 0.15f * Mathf.Sin(time * 0.6f + it.phase);
                    break;
                case Kind.Galaxy:    // turns very slowly
                    it.rot += it.spin * dt;
                    break;
                case Kind.Asteroid:  // drifts and tumbles, wrapping round
                    it.xSh += it.vx * dt; it.ySh += it.vy * dt; it.rot += it.spin * dt;
                    if (it.xSh > 1.1f) it.xSh = -0.1f; else if (it.xSh < -0.1f) it.xSh = 1.1f;
                    break;
                case Kind.Comet:     // waits off screen, then streaks across
                    if (it.wait > 0f)
                    {
                        it.wait -= dt;
                        if (it.wait <= 0f) LaunchComet(it);
                        else { it.sr.enabled = false; continue; }
                    }
                    it.sr.enabled = true;
                    it.xSh += it.vx * dt; it.ySh += it.vy * dt * camW / camH;
                    it.rot = 0f;   // the art already lies along its path: head lower left, tail streaming up-right
                    if (it.xSh < -0.3f || it.ySh < -0.3f) it.wait = Random.Range(5f, 11f);
                    break;
            }
            Sprite s = it.sr.sprite;
            float scale = it.heightSh * camH / s.bounds.size.y;
            it.sr.transform.position = new Vector3(left + it.xSh * camW, bottom + it.ySh * camH, 0f);
            it.sr.transform.localScale = new Vector3(scale, scale, 1f);
            it.sr.transform.rotation = Quaternion.Euler(0f, 0f, it.rot);
            it.sr.color = new Color(_tint.r, _tint.g, _tint.b, a);
        }
    }
}
