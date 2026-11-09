using UnityEngine;

// "Exploding barrel" — a WoodBlock variant that deals bigger area damage to nearby blocks when
// destroyed, instead of just disappearing quietly like a normal plank. Introduced at L03 "The
// Tower" (2026-07-10, user-requested "gradually introduce the exploding barrel") using
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

        // A barrel is a genuine explosive prop — unlike plain Wood (see WoodBlock's
        // _isExplosive comment), it's marked explosive so its own chain can freely detonate any
        // nearby block (including other explosive props), while plain Wood/Stone breaking near
        // IT can never trigger its explosion — only a direct hit from a character/ability can
        // (2026-08-03). Block-to-block chain damage stays bigger than plain Wood's default too,
        // matching a barrel's "dramatic explosion, not a nick" identity. Never damages robots
        // directly — see RobotEnemy.cs's damage-model comment.
        _isExplosive = true;
        _areaDamage  = 55f;
        // Explicit, not just inherited from WoodBlock.Awake()'s _isExplosive branch — 2026-08-09,
        // same reduction (2.2 -> 1.0, "reduce the 4x radius to only two") as StoneBlock/Haybale.
        _explodeSizeMultiplier = 2.2f;   // EffectSizing k (2026-11-09): the barrel's blast reads bigger than its body
    }

    protected override void DestroyBlock()
    {
        if (IsDestroyed) return; // BlockBase also guards this, but must run exactly once
        CameraShake.Shake(0.30f, 0.28f);
        base.DestroyBlock(); // WoodBlock.DestroyBlock() -> DamageNearby() chains into nearby blocks
    }
}
