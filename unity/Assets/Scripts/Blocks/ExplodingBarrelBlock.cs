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

        // A barrel is a genuine explosive prop — unlike plain Wood (see WoodBlock's
        // _explodesOnRobots comment, 2026-07-10 fifth balance pass), its death always fires
        // RobotEnemy.TakeExplosionDamage() at any robot caught in its (now sprite-size-relative,
        // see EffectiveBlastRadius) blast. Block-to-block chain damage stays bigger than plain
        // Wood's default too, matching a barrel's "dramatic explosion, not a nick" identity.
        _explodesOnRobots = true;
        _areaDamage       = 55f;

        // "These barrels must explode like the haybales, only with stronger strength" (2026-07-10,
        // user request, after L07's barrel-heavy layout) — a barrel now deals 2x the robot-facing
        // explosion fraction Haybale uses (RobotEnemy.ExplosionDamageFraction * 2, i.e. always
        // guarantees a kill on any robot caught in its blast regardless of current HP), rather
        // than sharing an identical fraction with Haybale despite reading as the bigger, more
        // dramatic explosion of the two. Lives here (not per-level data), so every level that
        // already uses ExplodingBarrelBlock — L03, L04, L05, L07 — gets this automatically.
        _explosionStrengthMultiplier = 2f;
    }

    protected override void DestroyBlock()
    {
        if (IsDestroyed) return; // BlockBase also guards this, but must run exactly once
        CameraShake.Shake(0.30f, 0.28f);
        base.DestroyBlock(); // WoodBlock.DestroyBlock() -> DamageNearby() hits both blocks and robots
    }
}
