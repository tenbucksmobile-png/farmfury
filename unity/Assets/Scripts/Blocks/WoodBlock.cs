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
    // block's own size — it's ExplosiveForceScale of the block's own rendered footprint (see
    // EffectiveBlastRadius below), computed fresh from the actual collider bounds at the moment
    // of death. Lowered 0.4 -> 0.25 (2026-08-09, difficulty pass, "mechanism 1 option B" — user
    // report: a single barrel/haybale touch could chain-detonate several others across a level).
    // A ~1x1 block now has a ~0.25u blast radius, so ordinary level-authoring spacing (props
    // placed roughly half a unit or more apart, the norm across every shipped level) no longer
    // falls inside another explosive prop's blast at all — only props placed essentially
    // touching/overlapping on purpose can still chain into each other.
    protected const float ExplosiveForceScale = 0.25f;
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
    // Uses the authored footprint, not the collider: colliders are trimmed to the visible art (2026-11-08) and the
    // blast balance was tuned against the full footprint.
    protected float EffectiveBlastRadius =>
        Mathf.Max(Footprint.x, Footprint.y) * ExplosiveForceScale;

    protected override void Awake()
    {
        // Lowered 80 -> 5 on 2026-07-10 for a deliberate single-hit-destruction feel from a
        // direct animal hit. Raised 5 -> 8 (2026-08-09, "mechanism 2 option F" — user report of
        // a falling-block domino chain destroying far more than the animal itself ever touched):
        // a direct Cluck/Bessie hit (~15-20 damage) still one-shots a standard wood block just as
        // reliably as before, but a WEAKER glancing impact — specifically another block landing
        // on it after falling, which generates less force than a direct thrown-animal hit — is
        // now less likely to also destroy it outright, so a collapsing stack is more likely to
        // topple/settle a few pieces deep rather than shatter everything below it in one chain.
        // BlockBase.Initialise() scales this by area/StdArea (StdArea=0.48) — for a standard 1x1
        // block that's baseMaxHealth*2.083, so 8 lands around ~16.7 effective HP.
        baseMaxHealth = 8f;
        baseMass      = 5f;
        bounciness    = 0.2f;
        // Self-healing — 2026-08-09. HaybaleBlock.prefab shares this exact component (see the
        // class comment at the top of this file) and was found still serialized at 2.2 (over
        // 4x wood's own 0.5 death-burst size) — the pre-2026-07-10 value. Reduced to 1.0 (2x
        // wood's 0.5, not 4.4x) per explicit user request ("reduce the 4x radius to only two,
        // make it seem realistic"). Branches on _isExplosive rather than a fixed value because
        // this one Awake() covers both plain Wood (never explosive, wants 0.5) and Haybale
        // (always explosive, wants 1.0) — _isExplosive is already deserialized from the prefab
        // by the time Awake() runs, so this reads correctly for both without needing a subclass.
        _explodeSizeMultiplier = _isExplosive ? 2.0f : 1.5f;   // EffectSizing k (2026-11-09)
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
        // 2026-08-09 (user-reported: a single animal was clearing entire structures in one hit).
        // Only a genuine explosive prop's (_isExplosive — Haybale, ExplodingBarrelBlock) own
        // destruction chain-damages nearby blocks any more. A plain structural block (Wood/Stone)
        // breaking no longer directly damages its neighbours at all — this used to let one hit
        // domino through an entire wall/tower via code-driven area damage, which isn't how real
        // Angry Birds-style destruction works: there, a piece dying only affects neighbours
        // through actual physics (falling once support is lost, or a real collision impulse),
        // both of which still fully apply here (see BlockBase.CheckForRobotsOnTop /
        // SettleUnsupportedBlocksNextFrame and BlockBase's own impulse-driven TakeDamage). Every
        // remaining block in a structure now only dies from being hit directly or from a genuine
        // physics collapse, not from a neighbour's death alone.
        if (_isExplosive && !BreaksOnHit) DamageNearby();   // the barrel does its own blast (ExplodingBarrelBlock.Blast)
    }

    // Damage against another explosive prop caught in this blast is reduced by this fraction —
    // 2026-08-09, "mechanism 1 option C" (user request: a chain shouldn't be a guaranteed sure
    // thing). Combined with option B's shrunk blast radius above: two explosive props have to be
    // placed essentially touching for this to matter at all, and even then the reduced damage
    // isn't always enough to finish off a full-health neighbour outright.
    protected const float ExplosiveNeighbourDamageScale = 0.5f;

    // Damages every OTHER block within EffectiveBlastRadius. Only ever called for a genuine
    // explosive prop's own destruction (see DestroyBlock above) — never for a plain Wood/Stone
    // block. Still skips no one: an explosive prop's chain can still reach another explosive
    // prop, but at reduced strength (see ExplosiveNeighbourDamageScale above).
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

            float dmg = _areaDamage * falloff;
            if (block is WoodBlock neighbourWood && neighbourWood._isExplosive)
                dmg *= ExplosiveNeighbourDamageScale;
            block.TakeDamage(dmg);
        }
    }
}
