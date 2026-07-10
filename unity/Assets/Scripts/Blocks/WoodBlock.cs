using UnityEngine;

public class WoodBlock : BlockBase
{
    // When true, a launched animal passes through this block at 70% of its incoming velocity.
    // Set by LevelLoader after spawn when BlockSpawnData.passThrough is true.
    [SerializeField] public bool _passThrough;

    // Damage-factor model v2 (2026-07-10, fifth balance pass — see the comment block at the top
    // of RobotEnemy.cs for the full rationale). Root cause of the "level 3 onward is one hit"
    // complaint: EVERY block in this family firing a flat, no-falloff robot explosion hit at a
    // wide (1.6-2.0u) radius on death let one swing chain-break a whole packed tower, each break
    // landing a "free" hit on any nearby robot. Two changes fix this:
    //   1. Plain Wood no longer damages robots on death AT ALL — it is now purely structural.
    //      Only _explodesOnRobots=true blocks (Haybale, ExplodingBarrelBlock) still call
    //      RobotEnemy.TakeExplosionDamage() when they die near a robot. A robot standing on wood
    //      that gets destroyed still suffers — via BlockBase.CheckForRobotsOnTop() ->
    //      RobotEnemy.MakeDynamicFromSupportLoss() -> TakeFallDamage(), a distinct, weaker
    //      category — but breaking wood 2 units away from a robot now does nothing to it.
    //   2. Blast radius ("explosive force") is no longer an arbitrary flat number independent of
    //      the block's own size — it's now ExplosiveForceScale (40%) of the block's own rendered
    //      footprint (see EffectiveBlastRadius below), computed fresh from the actual collider
    //      bounds at the moment of death. A ~1x1 block now has a ~0.4u blast radius instead of
    //      1.6-2.0u, so a single break can only ever reach directly-touching neighbours, not an
    //      entire tower — this is what actually "minimises the chain reaction," not the damage
    //      amount below (_areaDamage), which is unchanged and still governs block-to-block
    //      falloff damage only.
    [SerializeField] protected bool  _explodesOnRobots = false;
    protected const float ExplosiveForceScale = 0.4f;
    [SerializeField] protected float _areaDamage        = 25f;

    // Multiplies RobotEnemy.TakeExplosionDamage()'s usual fraction for THIS block type only —
    // added 2026-07-10, user request: "these barrels must explode like the haybales, only with
    // stronger strength." Haybale and plain explosive Wood both use the default 1x (identical to
    // RobotEnemy.ExplosionDamageFraction, the same "genuine explosion" tier every explosive prop
    // shares); ExplodingBarrelBlock overrides this higher so a barrel reads as a dramatically
    // bigger threat without needing a second, parallel damage category.
    [SerializeField] protected float _explosionStrengthMultiplier = 1f;

    // Radius of this block's blast, derived from its own actual world-space size (not a flat
    // constant) — see the class comment above. Falls back to a plain 1u footprint if the
    // collider somehow isn't ready yet.
    protected float EffectiveBlastRadius =>
        (_col != null ? Mathf.Max(_col.bounds.size.x, _col.bounds.size.y) : 1f) * ExplosiveForceScale;

    protected override void Awake()
    {
        // Lowered 80 -> 5 (2026-07-10, user report: "wood... currently it is not damaging" —
        // early levels are meant to be easy, single-hit destruction for all three environmental
        // block types (haybale/wood/barrel), with the actual challenge coming from robot HP and
        // the fall/contact damage mechanics instead). BlockBase.Initialise() scales this by
        // area/StdArea (StdArea=0.48) — for a standard 1x1 block that's baseMaxHealth*2.083, so
        // 5 lands right around Haybale's explicit hp:10 override (its own class default is
        // irrelevant there since every Haybale B() call already passes an explicit override) —
        // a typical Cluck impact (~15-20 damage per CLAUDE.md) now reliably one-shots any
        // standard wood block without needing a per-instance hp override in every level's data.
        baseMaxHealth = 5f;
        baseMass      = 5f;
        bounciness    = 0.2f;
        base.Awake();
        if (_sr) _sr.color = new Color(0.65f, 0.38f, 0.12f); // wood brown
    }

    protected override void PlayHitSound() =>
        AudioManager.Play(AudioManager.Sound.WoodHit, cooldown: 0.08f);

    protected override void DestroyBlock()
    {
        if (IsDestroyed) return; // BlockBase also guards this, but DamageNearby() must run exactly once
        // base.DestroyBlock() MUST run first — it sets IsDestroyed = true as literally its
        // second line. Calling DamageNearby() before that (the original order) caused a real
        // infinite-recursion stack overflow crash: if block A's blast radius reaches block B and
        // B's radius reaches back to A (any two blocks placed within roughly 2x the radius of
        // each other — common in a packed level), A wasn't marked destroyed yet when B's own
        // chain reaction looped back and called TakeDamage() on A again, which (since A's Health
        // was already <=0) called DestroyBlock() on A again, which called DamageNearby() again,
        // which called back into B... forever. Destroy(gameObject) below only marks the
        // GameObject for deferred destruction at end-of-frame, so transform.position/_col stay
        // fully valid for DamageNearby()'s Physics2D query even after base.DestroyBlock() runs —
        // safe to reorder with no loss of functionality, and IsDestroyed being true by the time
        // DamageNearby() starts means any reentrant chain back to this block now hits
        // BlockBase.TakeDamage()'s own "if (IsDestroyed) return;" guard immediately.
        base.DestroyBlock();
        DamageNearby();
    }

    // Damages every OTHER block within EffectiveBlastRadius (structural chain reaction — applies
    // to every block in this family, Wood included) and, ONLY if _explodesOnRobots is true
    // (Haybale, ExplodingBarrelBlock — plain Wood never), every robot in that same now-much-
    // smaller radius via the flat, no-falloff RobotEnemy.TakeExplosionDamage(). Wood dying near a
    // robot but NOT under it does nothing to that robot at all — see the class comment above.
    protected void DamageNearby()
    {
        float radius = EffectiveBlastRadius;
        var hits = Physics2D.OverlapCircleAll(transform.position, radius);
        foreach (var hit in hits)
        {
            if (hit == null || hit.gameObject == gameObject) continue;

            if (_explodesOnRobots)
            {
                var robot = hit.GetComponentInParent<RobotEnemy>();
                if (robot != null && !robot.IsDestroyed)
                {
                    robot.TakeExplosionDamage(_explosionStrengthMultiplier);
                    continue;
                }
            }

            float dist    = Vector2.Distance(transform.position, hit.transform.position);
            float falloff = Mathf.Clamp01(1f - dist / radius);
            if (falloff <= 0f) continue;

            var block = hit.GetComponentInParent<BlockBase>();
            if (block != null && block != (BlockBase)this && !block.IsDestroyed)
                block.TakeDamage(_areaDamage * falloff);
        }
    }
}
