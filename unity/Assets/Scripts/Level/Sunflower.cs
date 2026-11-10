using System.Collections;
using UnityEngine;

/// <summary>
/// A giant sunflower (World 1 interactive contraption, 2026-11-09 — the farm answer to Angry Birds 2's flowers).
/// Hit it — an animal, an egg/ability projectile, or a falling block/wagon — and it whips back and sprays a fan of
/// sunflower seeds to the right, into the structure beside it. Each seed is an <see cref="AbilityProjectile"/>
/// (bounce-and-hit: chips blocks, egg-tier hit on robots), so the damage stays inside the existing damage model.
/// One shot per flower; afterwards it droops. Its collider is a trigger around the head, so shots fly through it
/// and it never props up or blocks the physics.
/// </summary>
public class Sunflower : MonoBehaviour
{
    const int   SeedCount   = 7;
    const float SeedSpeedMin = 7.5f, SeedSpeedMax = 10.5f;
    const float AngleMin = 4f, AngleMax = 46f;     // degrees above horizontal, toward +x
    const float TriggerSpeed = 1.6f;               // a falling block must be moving at least this fast
    static readonly Color Wilted = new Color(0.72f, 0.62f, 0.42f);

    Sprite _seedSprite;
    SpriteRenderer _sr;
    float _height;
    bool _fired;
    bool _crystal;   // World 2 ice crystal (2026-11-09): fires ice shards, shatters and fades instead of wilting
    // World 3 village water pump (2026-11-10): the handle jerks, the pump shudders and gushes a flat, fast jet of water
    // to the right — more droplets, lower and harder than the sunflower's fan, each one splashing (pushes + damages
    // in a small radius). Afterwards the pump stays put, run dry (darker).
    bool _pump;

    public static Sunflower Spawn(Transform parent, Sprite flower, Sprite seed, Vector2 basePos, float height, bool crystal = false,
                                  bool pump = false)
    {
        var go = new GameObject("Sunflower");
        go.transform.SetParent(parent, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = flower;
        sr.sortingOrder = 2;   // with the blocks: in front of the corn and mounts, behind robots/animals
        float s = 1f;
        Vector2 pivotOffset = Vector2.zero;
        if (flower != null)
        {
            Bounds b = flower.bounds;
            s = height / b.size.y;
            pivotOffset = new Vector2(-b.center.x, -b.min.y) * s;   // base of the stalk sits on basePos
        }
        else
        {
            sr.sprite = MakeFallbackSprite();
            s = height;
            pivotOffset = new Vector2(0f, height * 0.5f);
        }
        go.transform.localScale = new Vector3(s, s, 1f);
        go.transform.position = new Vector3(basePos.x + pivotOffset.x, basePos.y + pivotOffset.y, 0f);

        // Trigger around the head (upper third of the plant).
        var rb = go.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        var col = go.AddComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius = height * 0.24f / s;
        col.offset = new Vector2(0f, (basePos.y + height * 0.78f - go.transform.position.y) / s);

        var f = go.AddComponent<Sunflower>();
        f._sr = sr;
        f._seedSprite = seed;
        f._height = height;
        f._crystal = crystal;
        f._pump = pump;
        if (crystal) go.name = "IceCrystal";
        if (pump) go.name = "WaterPump";
        return f;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (_fired || other.attachedRigidbody == null) return;
        var rb = other.attachedRigidbody;
        if (rb.GetComponent<Sunflower>() != null) return;
        bool shot = rb.GetComponent<AnimalBase>() != null || rb.GetComponent<AbilityProjectile>() != null ||
                    rb.GetComponent<EggProjectile>() != null;
        if (!shot && (rb.bodyType != RigidbodyType2D.Dynamic || rb.linearVelocity.magnitude < TriggerSpeed)) return;
        Fire();
    }

    public void Fire()
    {
        if (_fired) return;
        _fired = true;
        StartCoroutine(FireRoutine());
    }

    IEnumerator FireRoutine()
    {
        if (_pump) { yield return PumpRoutine(); yield break; }
        // Wind-up: the head whips back, then snaps forward and spits the seeds.
        Quaternion rest = transform.rotation;
        yield return Rotate(rest, Quaternion.Euler(0, 0, 14f), 0.10f);
        Vector3 head = transform.position + transform.up * (_height * 0.25f);
        var spec = new AbilityProjectile.Spec
        {
            sprite = _seedSprite, burstSprite = null, size = _crystal ? 0.34f : 0.2f, mass = _crystal ? 0.8f : 0.5f,
            bounciness = _crystal ? 0.15f : 0.35f, gravityScale = 0.45f, hits = 2, blockDamage = _crystal ? 18f : 14f,
            burstRadius = 0f, robotHits = 1, spin = _crystal ? 200f : 720f, life = 4f,
        };
        for (int i = 0; i < SeedCount; i++)
        {
            float a = Mathf.Lerp(AngleMin, AngleMax, (i + Random.Range(-0.3f, 0.3f)) / (SeedCount - 1)) * Mathf.Deg2Rad;
            float v = Random.Range(SeedSpeedMin, SeedSpeedMax);
            var p = AbilityProjectile.Spawn(spec, head + new Vector3(0.15f, Random.Range(-0.1f, 0.1f), 0f),
                                            new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * v, null, 7);
            if (_seedSprite == null) p.GetComponent<SpriteRenderer>().sprite = MakeFallbackSprite();
        }
        AudioManager.Play(AudioManager.Sound.WoodHit);
        CameraShake.Shake(0.08f, 0.1f);
        yield return Rotate(transform.rotation, Quaternion.Euler(0, 0, -10f), 0.08f);
        if (_crystal)
        {
            // Spent crystal: it shatters — a flash, then it fades away.
            float e = 0f;
            Color c1 = _sr.color;
            while (e < 0.5f) { e += Time.deltaTime; _sr.color = new Color(c1.r, c1.g, c1.b, 1f - e / 0.5f); yield return null; }
            _sr.enabled = false;
            yield break;
        }
        // Spent: droop and fade to straw colour.
        yield return Rotate(transform.rotation, Quaternion.Euler(0, 0, -22f), 0.6f);
        float t = 0f;
        Color c0 = _sr.color;
        while (t < 0.6f) { t += Time.deltaTime; _sr.color = Color.Lerp(c0, Wilted, t / 0.6f); yield return null; }
    }

    const int   PumpDrops = 10;
    const float PumpAngleMin = 2f, PumpAngleMax = 30f, PumpSpeedMin = 9f, PumpSpeedMax = 12.5f;

    IEnumerator PumpRoutine()
    {
        // Shudder (a quick squash-and-stretch about the base), then the gush: droplets in two quick pulses.
        Vector3 s0 = transform.localScale, p0 = transform.position;
        Vector3 basePos = p0 - transform.up * (_height * 0.5f);
        for (float t = 0f; t < 0.12f; t += Time.deltaTime)
        {
            float k = Mathf.Sin(t / 0.12f * Mathf.PI);
            transform.localScale = new Vector3(s0.x * (1f + 0.08f * k), s0.y * (1f - 0.1f * k), 1f);
            transform.position = basePos + transform.up * (_height * 0.5f * (1f - 0.1f * k));
            yield return null;
        }
        transform.localScale = s0; transform.position = p0;
        // The spout: upper right of the pump (~0.7 of its height, a little right of centre).
        Vector3 spout = basePos + transform.up * (_height * 0.7f) + transform.right * (_height * 0.18f);
        var spec = new AbilityProjectile.Spec
        {
            sprite = _seedSprite, burstSprite = _seedSprite, size = 0.26f, mass = 0.9f, bounciness = 0.05f,
            gravityScale = 0.5f, hits = 1, blockDamage = 16f, burstRadius = 0.45f, burstPush = 2.5f, robotHits = 1,
            spin = 360f, life = 3f,
        };
        for (int pulse = 0; pulse < 2; pulse++)
        {
            for (int i = 0; i < PumpDrops / 2; i++)
            {
                float a = Mathf.Lerp(PumpAngleMin, PumpAngleMax, (i + Random.Range(-0.3f, 0.3f)) / (PumpDrops / 2 - 1)) * Mathf.Deg2Rad;
                float v = Random.Range(PumpSpeedMin, PumpSpeedMax);
                var p = AbilityProjectile.Spawn(spec, spout + new Vector3(0.1f, Random.Range(-0.06f, 0.06f), 0f),
                                                new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * v, null, 7);
                if (_seedSprite == null) p.GetComponent<SpriteRenderer>().sprite = MakeFallbackSprite();
            }
            AudioManager.Play(AudioManager.Sound.WoodHit);
            CameraShake.Shake(0.07f, 0.1f);
            yield return new WaitForSeconds(0.14f);
        }
        // Run dry: a little darker, as if the wet sheen is gone.
        Color c0 = _sr.color, dry = new Color(0.72f, 0.72f, 0.76f);
        for (float t = 0f; t < 0.5f; t += Time.deltaTime) { _sr.color = Color.Lerp(c0, dry, t / 0.5f); yield return null; }
    }

    IEnumerator Rotate(Quaternion from, Quaternion to, float seconds)
    {
        // Rotates about the stalk's base (the transform sits at the sprite's middle, so pivot manually).
        Vector3 basePos = transform.position - transform.up * (_height * 0.5f);
        float t = 0f;
        while (t < seconds)
        {
            t += Time.deltaTime;
            transform.rotation = Quaternion.Slerp(from, to, Mathf.Clamp01(t / seconds));
            transform.position = basePos + transform.up * (_height * 0.5f);
            yield return null;
        }
    }

    static Sprite _fallback;
    static Sprite MakeFallbackSprite()
    {
        if (_fallback != null) return _fallback;
        var tex = new Texture2D(8, 8);
        var px = new Color[64];
        for (int i = 0; i < 64; i++) px[i] = new Color(0.95f, 0.8f, 0.15f);
        tex.SetPixels(px); tex.Apply();
        return _fallback = Sprite.Create(tex, new Rect(0, 0, 8, 8), new Vector2(0.5f, 0.5f), 8);
    }
}
