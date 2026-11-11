using UnityEngine;

// Dynamite barrel — the ONLY thing in a structure that explodes (2026-11-11, user: "the only explosion is that of the
// dynamite barrel - on first hit - and even then exploding topples structure not explode"). It goes off on its first
// real hit (an animal, an ability, an egg, or debris landing on it hard — BlockBase.BreaksOnHit), shows the blast, and
// the blast SHOVES everything around it outward and up: blocks are woken and pushed over (never destroyed), other
// dynamite in reach goes off a beat later, and robots right beside it are blown up; robots further out are thrown and
// usually finished by the fall or the debris (RobotEnemy's crush rule).
public class ExplodingBarrelBlock : WoodBlock
{
    const float BlastRadius = 3.0f;      // world units: everything in here is shoved
    const float KillRadius  = 1.15f;     // robots this close are blown up outright
    const float BlastImpulse = 26f;      // at the centre, falling off to ~25% at the edge
    const float ChainDelay  = 0.15f;

    protected override bool BreaksOnHit => true;

    protected override void Awake()
    {
        base.Awake();
        baseMaxHealth = 5f;

        _isExplosive = true;
        _areaDamage  = 55f;
        _explodeSizeMultiplier = 2.2f;   // EffectSizing k (2026-11-09): the barrel's blast reads bigger than its body
    }

    protected override void DestroyBlock()
    {
        if (IsDestroyed) return; // BlockBase also guards this, but must run exactly once
        CameraShake.Shake(0.30f, 0.28f);
        Vector2 at = transform.position;
        base.DestroyBlock();     // the blast art + sound; WoodBlock no longer chain-damages (see Blast)
        Blast(at);
    }

    void Blast(Vector2 at)
    {
        var shoved = new System.Collections.Generic.HashSet<Rigidbody2D>();
        foreach (var hit in Physics2D.OverlapCircleAll(at, BlastRadius))
        {
            if (hit == null || hit.isTrigger || hit.gameObject == gameObject) continue;
            var rb = hit.attachedRigidbody;
            Vector2 away = (Vector2)hit.bounds.center - at;
            float falloff = Mathf.Lerp(1f, 0.25f, Mathf.Clamp01(away.magnitude / BlastRadius));
            Vector2 dir = (away.sqrMagnitude < 0.0001f ? Vector2.up : away.normalized) + Vector2.up * 0.35f;

            var block = hit.GetComponentInParent<BlockBase>();
            if (block != null && !block.IsDestroyed)
            {
                if (block is ExplodingBarrelBlock other) { other.Invoke(nameof(GoOff), ChainDelay); continue; }
                block.Disturb();
            }
            var robot = hit.GetComponentInParent<RobotEnemy>();
            if (robot != null && !robot.IsDestroyed)
            {
                if (away.magnitude <= KillRadius) { robot.TakeFallDamage(); continue; }
                robot.MakeDynamicFromSupportLoss();
            }
            if (rb == null || rb.bodyType != RigidbodyType2D.Dynamic || !shoved.Add(rb)) continue;
            rb.WakeUp();
            // Pushed at a point above the centre so tall pieces tip over rather than slide.
            Vector2 point = (Vector2)hit.bounds.center + Vector2.up * hit.bounds.extents.y * 0.5f;
            rb.AddForceAtPosition(dir.normalized * BlastImpulse * falloff * Mathf.Clamp(rb.mass / 4f, 0.4f, 2.5f), point, ForceMode2D.Impulse);
        }
    }

    void GoOff()
    {
        if (this != null && !IsDestroyed) TakeDamage(MaxHealth);
    }
}
