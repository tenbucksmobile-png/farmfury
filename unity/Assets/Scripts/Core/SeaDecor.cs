using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Life in the water over World 5 (Sunken City), 2026-11-12 - the sea counterpart of <see cref="SkyDecor"/>. All
/// Stampede Sunken City art (tools/import_sunken_art.py): fish swimming across at three depths (each faces the way it
/// swims), jellyfish pulsing slowly upward, and bubbles rising and wobbling. Purely decorative: no colliders. Placed in
/// screen fractions (the far backdrop stays fixed to the screen) so it reads the same on every aspect and travels with
/// the camera from room to room. Created by <see cref="EnvironmentDepthSystem"/>, active only on World 5.
/// </summary>
public class SeaDecor : MonoBehaviour
{
    // Far items sit just behind the haze veil (-35) so it hazes them; near items in front of the far painting's layers
    // but still behind every play-layer sprite (blocks 2, robots 3).
    const int FarOrder = -36, MidOrder = -34, NearOrder = -29;
    static readonly Color FarHaze = new Color(0.55f, 0.8f, 0.85f, 1f);

    enum Kind { Fish, Jelly, Bubble }
    enum Depth { Far, Mid, Near }

    class Item
    {
        public Kind kind;
        public Depth depth;
        public SpriteRenderer sr;
        public bool facesRight;
        public float heightSh, xSh, ySh, speed, phase;
    }

    Camera _cam;
    Sprite[] _fish;
    bool[] _fishFacesRight;
    Sprite _jelly, _bubble;
    readonly List<Item> _items = new();
    Color _tint = Color.white;
    bool _glow;

    /// <param name="fishFacesRight">per fish sprite: true if its head points right in the art.</param>
    public void Init(Camera cam, Sprite[] fish, bool[] fishFacesRight, Sprite jelly, Sprite bubble)
    {
        _cam = cam; _fish = fish; _fishFacesRight = fishFacesRight; _jelly = jelly; _bubble = bubble;
    }

    /// <summary>Lay the water out for a level: the weather's tint; jellyfish glow in the deep.</summary>
    public void ResetFor(Color tint, bool glow)
    {
        _tint = tint; _glow = glow;
        foreach (var it in _items) if (it.sr != null) Destroy(it.sr.gameObject);
        _items.Clear();
        // Sizes are screen-height fractions, x/y screen fractions; speed in screen widths per second (negative swims
        // right-to-left). A school of small far fish, a few mid-water swimmers, one or two big near ones.
        //   depth, height, x, y, speed
        AddFish(Depth.Far,  0.025f, 0.10f, 0.78f,  1f / 45f);
        AddFish(Depth.Far,  0.022f, 0.14f, 0.81f,  1f / 45f);
        AddFish(Depth.Far,  0.024f, 0.18f, 0.77f,  1f / 45f);
        AddFish(Depth.Far,  0.03f,  0.70f, 0.62f, -1f / 60f);
        AddFish(Depth.Mid,  0.045f, 0.35f, 0.70f, -1f / 30f);
        AddFish(Depth.Mid,  0.05f,  0.85f, 0.86f,  1f / 34f);
        AddFish(Depth.Mid,  0.042f, 0.55f, 0.52f,  1f / 40f);
        AddFish(Depth.Near, 0.08f,  0.25f, 0.92f, -1f / 22f);
        Add(Kind.Jelly, Depth.Far, 0.05f, 0.62f, 0.55f, 1f / 90f);
        Add(Kind.Jelly, Depth.Mid, 0.08f, 0.88f, 0.40f, 1f / 70f);
        Add(Kind.Jelly, Depth.Mid, 0.07f, 0.45f, 0.30f, 1f / 80f);
        for (int i = 0; i < 9; i++)   // bubbles, small, staggered up the screen
            Add(Kind.Bubble, i % 3 == 0 ? Depth.Far : Depth.Mid, 0.012f + 0.006f * (i % 3), 0.06f + 0.105f * i,
                0.1f + 0.1f * ((i * 4) % 9), 1f / (9f + 3f * (i % 4)));
    }

    void AddFish(Depth depth, float heightSh, float xSh, float ySh, float speed)
    {
        if (_fish == null || _fish.Length == 0) return;
        int k = (_items.Count * 7 + 3) % _fish.Length;
        var it = Add(Kind.Fish, depth, heightSh, xSh, ySh, speed, _fish[k]);
        if (it != null) it.facesRight = _fishFacesRight != null && k < _fishFacesRight.Length && _fishFacesRight[k];
    }

    Item Add(Kind kind, Depth depth, float heightSh, float xSh, float ySh, float speed, Sprite sprite = null)
    {
        sprite ??= kind == Kind.Jelly ? _jelly : kind == Kind.Bubble ? _bubble : null;
        if (sprite == null) return null;
        int order = depth == Depth.Far ? FarOrder : depth == Depth.Near ? NearOrder : MidOrder;
        var go = new GameObject($"{kind}_{depth}");
        go.transform.SetParent(transform, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = order;
        var it = new Item { kind = kind, depth = depth, sr = sr, heightSh = heightSh, xSh = xSh, ySh = ySh,
                            speed = speed, phase = Random.Range(0f, 10f) };
        _items.Add(it);
        return it;
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
            float bob = 0f, rot = 0f, sx = 1f;
            Color col = _tint;
            switch (it.kind)
            {
                case Kind.Fish:     // swims across, wrapping; tail-wiggle tilt; faces the way it swims
                    it.xSh += it.speed * dt;
                    if (it.xSh > 1.12f) { it.xSh = -0.12f; it.ySh = Mathf.Clamp(it.ySh + Random.Range(-0.08f, 0.08f), 0.3f, 0.95f); }
                    else if (it.xSh < -0.12f) { it.xSh = 1.12f; it.ySh = Mathf.Clamp(it.ySh + Random.Range(-0.08f, 0.08f), 0.3f, 0.95f); }
                    bob = Mathf.Sin(time * 1.3f + it.phase) * 0.01f;
                    rot = Mathf.Sin(time * 6f + it.phase) * 4f;
                    it.sr.flipX = (it.speed > 0f) != it.facesRight;
                    break;
                case Kind.Jelly:    // pulses: drifts up in little pushes, bell squeezing, then starts again below
                    float pulse = Mathf.Max(0f, Mathf.Sin(time * 1.6f + it.phase));
                    it.ySh += it.speed * dt * (0.3f + 1.7f * pulse);
                    if (it.ySh > 1.1f) { it.ySh = -0.08f; it.xSh = Random.Range(0.1f, 0.95f); }
                    sx = 1f + 0.08f * pulse;
                    if (_glow) { float g = 1.15f + 0.2f * Mathf.Sin(time * 2f + it.phase); col = new Color(g, g, g * 1.1f, 1f); }
                    else col.a = 0.85f;
                    break;
                case Kind.Bubble:   // rises, wobbling side to side, then starts again near the floor
                    it.ySh += it.speed * dt;
                    if (it.ySh > 1.05f) { it.ySh = 0.12f; it.xSh = Random.Range(0.05f, 0.95f); }
                    it.xSh += Mathf.Sin(time * 3f + it.phase) * 0.012f * dt;
                    col = new Color(1f, 1f, 1f, 0.7f);
                    break;
            }
            if (it.depth == Depth.Far)   // far away: washed toward the water's haze and a little see-through
            {
                col = Color.Lerp(col, FarHaze * _tint, 0.5f);
                col.a *= 0.75f;
            }
            it.sr.transform.position = new Vector3(left + it.xSh * camW, bottom + (it.ySh + bob) * camH, 0f);
            it.sr.transform.localScale = new Vector3(scale * sx, scale / sx, 1f);
            it.sr.transform.rotation = Quaternion.Euler(0f, 0f, rot);
            it.sr.color = col;
        }
    }
}
