using UnityEngine;

public class WoodBlock : BlockBase
{
    // When true, a launched animal passes through this block at 70% of its incoming velocity.
    // Set by LevelLoader after spawn when BlockSpawnData.passThrough is true.
    [SerializeField] public bool _passThrough;

    // Damage-factor model v2 (2026-07-10, fifth balance pass — see the comment block at the top
    // of RobotEnemy.cs for the full rationale). Wood/Stone breaking is purely structural — it
    // never damages a robot directly (2026-08-03: explosion damage to robots was removed
    // entirely). A robot standing on a block that gets destroyed still suffers — via
    // BlockBase.CheckForRobotsOnTop() -> RobotEnemy.MakeDynamicFromSupportLoss() ->
    // TakeFallDamage(), a distinct, weaker category driven by the fall itself, not a blast.
    // Blast radius ("explosive force") is not an arbitrary flat number independent of the
    // block's own size — it's ExplosiveForceScale (40%) of the block's own rendered footprint
    // (see EffectiveBlastRadius below), computed fresh from the actual collider bounds at the
    // moment of death. A ~1x1 block has a ~0.4u blast radius, so a single break can only ever
    // reach directly-touching neighbours, not an entire tower.
    protected const float ExplosiveForceScale = 0.4f;
    [SerializeField] protected float _areaDamage        = 25f;

    // Marks a genuine explosive prop (Haybale, ExplodingBarrelBlock) as opposed to a plain
    // structural block (Wood, Stone). Only used for the chain-reaction gate below — 2026-08-03,
    // user request: "an exploding wood or stone should also not affect an explosion of a haybale
    // or dynamite barrel — only a direct hit of a character or ability should affect an
    // explosion; however an explosion of haybale and dynamite barrel will cause the explosion of
    // wood or stone." A plain block's own falloff-damage chain skips any explosive neighbour it
    // reaches; an explosive prop's own chain still freely damages everything nearby (including
    // other explosive props), same as always.
    [SerializeField] protected bool  _isExplosive = false;

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

    // 2026-07-11 fundamental gameplay rule, user request: "if a robot falls and lands on a
    // haybale or dynamite barrel this will automatically explode." Only genuine explosive props
    // (_isExplosive — Haybale, ExplodingBarrelBlock) are guaranteed to detonate on ANY robot
    // landing, bypassing BlockBase's normal impulse-threshold math entirely so even a gentle
    // settle (e.g. a robot dropped by MakeDynamicFromSupportLoss coming to rest on top, not a
    // hard crash) still always triggers the explosion — a plain structural Wood block still only
    // reacts to real impact force via the inherited base behaviour below. This still only
    // detonates the block itself (no robot damage results, per the 2026-08-03 change above) — it
    // exists purely so a robot's weight can still trigger the prop's own chain reaction into
    // nearby blocks.
    protected override void OnCollisionEnter2D(Collision2D col)
    {
        if (_isExplosive && !IsDestroyed && col.gameObject.GetComponentInParent<RobotEnemy>() != null)
        {
            TakeDamage(MaxHealth);
            return;
        }
        base.OnCollisionEnter2D(col);
    }

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
    // to every block in this family, Wood included). Never touches robots — see the class
    // comment above. Gated so a plain block's own destruction can't detonate a nearby explosive
    // prop (only a direct hit can do that); an explosive prop's own chain is ungated and still
    // freely damages/detonates anything nearby, explosive or not.
    protected void DamageNearby()
    {
        float radius = EffectiveBlastRadius;
        var hits = Physics2D.OverlapCircleAll(transform.position, radius);
        foreach (var hit in hits)
        {
            if (hit == null || hit.gameObject == gameObject) continue;

            float dist    = Vector2.Distance(transform.position, hit.transform.position);
            float falloff = Mathf.Clamp01(1f - dist / radius);
            if (falloff <= 0f) continue;

            var block = hit.GetComponentInParent<BlockBase>();
            if (block == null || block == (BlockBase)this || block.IsDestroyed) continue;

            if (!_isExplosive && block is WoodBlock nearbyWood && nearbyWood._isExplosive)
                continue;

            block.TakeDamage(_areaDamage * falloff);
        }
    }
}
