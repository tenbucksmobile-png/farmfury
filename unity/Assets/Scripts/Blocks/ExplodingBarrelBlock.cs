using UnityEngine;

// "Exploding barrel" — a WoodBlock variant that deals bigger area damage to nearby blocks AND
// robots when destroyed, instead of just disappearing quietly like a normal plank. Introduced at
// L03 "The Tower" (2026-07-10, user-requested "gradually introduce the exploding barrel") using
// WoodenBarrel.png art, which existed on disk but had no dedicated gameplay behaviour before —
// levels previously using that art rendered it as a plain wood plank.
public class ExplodingBarrelBlock : WoodBlock
{
    protected override void Awake()
    {
        base.Awake();
        // Lowered 40 -> 5 (2026-07-10, user request: "wood, haybale, barrel all provide one hit
        // damage" — matches WoodBlock's own fix the same day; see that class for the exact
        // area-scaling math). A barrel should pop in one hit just as reliably as a haybale, not
        // survive multiple impacts before its explosion actually triggers.
        baseMaxHealth = 5f;

        // Bigger and more damaging than a plain wood/haybale break (WoodBlock's own defaults,
        // inherited otherwise, cover the actual chain-reaction logic via DamageNearby() — this
        // class no longer needs its own copy, just bigger numbers) — a barrel is meant to read
        // as a dramatic explosion, not a nick. Raised 1.8/60 -> 2.5/80 (2026-07-10, "ease the
        // damage requirements"), then dialed back down to 2.0/55 same day, third pass
        // (user-reported robots now "explode on the slightest of touches" — see WoodBlock's own
        // comment for the fuller context; expect more tuning passes on these numbers).
        _areaDamageRadius = 2.0f;
        _areaDamage       = 55f;
    }

    protected override void DestroyBlock()
    {
        if (IsDestroyed) return; // BlockBase also guards this, but must run exactly once
        CameraShake.Shake(0.30f, 0.28f);
        base.DestroyBlock(); // WoodBlock.DestroyBlock() -> DamageNearby() hits both blocks and robots
    }
}
