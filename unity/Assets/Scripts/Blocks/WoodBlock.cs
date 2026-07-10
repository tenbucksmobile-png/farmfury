using UnityEngine;

public class WoodBlock : BlockBase
{
    // When true, a launched animal passes through this block at 70% of its incoming velocity.
    // Set by LevelLoader after spawn when BlockSpawnData.passThrough is true.
    [SerializeField] public bool _passThrough;

    // Fundamental gameplay rule (user-requested 2026-07-10 — "levels are too difficult... we
    // need to ease the damage requirements... what we're looking for is destruction and mayhem,
    // one hit must cause a chain reaction"): every block destruction in this family (Wood,
    // Haybale — same class, just different wired art — and ExplodingBarrelBlock below, which
    // overrides these two fields with bigger numbers) damages BOTH nearby robots AND nearby
    // blocks, not just robots. Originally (same day, first pass) this only hit robots and left
    // block-to-block chaining as an ExplodingBarrelBlock-only "exploding barrel" special case —
    // that wasn't enough to reliably clear a level within 3 birds, so the chain reaction was
    // widened to every block type, and the radius/damage themselves raised (1.3->2.0, 20->35).
    // Dialed back down (2.0->1.6, 35->25) same day, second pass — user-reported the combination
    // of this plus the lowered robot HP (see SceneSetup's HarvesterRobot/SemiHarvesterRobot
    // wiring) made robots "explode on the slightest of touches," overcorrecting the original
    // "too hard" complaint into "too easy." User expects this to need further iteration ("it
    // will take some tweaking to get the correct strength ratio") — treat these numbers as a
    // rough middle ground, not a final tuning. Lives here (not per-level data) so it
    // automatically applies to every level, current and future.
    [SerializeField] protected float _areaDamageRadius = 1.6f;
    [SerializeField] protected float _areaDamage        = 25f;

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

    // Damages every OTHER block and robot within _areaDamageRadius — one block dying can now set
    // off its neighbours (chain reaction) as well as hurt any nearby robot, not just robots.
    // ExplodingBarrelBlock overrides _areaDamageRadius/_areaDamage with bigger numbers and adds
    // camera shake on top, but reuses this exact method rather than a separate implementation.
    //
    // Robot damage changed 2026-07-10 (fourth balance pass) from _areaDamage-with-distance-
    // falloff to a flat RobotEnemy.TakeExplosionDamage() call (exactly 25% of that robot's own
    // max HP, no falloff) — see the damage-model comment at the top of RobotEnemy.cs. User
    // wanted a precisely countable "4 explosions to kill" rule, which a distance-falloff amount
    // can't guarantee (a robot at the radius edge would take much less than a robot at ground
    // zero). Block-to-block chain damage (below) still uses _areaDamage with falloff — that part
    // of the system wasn't part of this request.
    protected void DamageNearby()
    {
        var hits = Physics2D.OverlapCircleAll(transform.position, _areaDamageRadius);
        foreach (var hit in hits)
        {
            if (hit == null || hit.gameObject == gameObject) continue;

            var robot = hit.GetComponentInParent<RobotEnemy>();
            if (robot != null && !robot.IsDestroyed)
            {
                robot.TakeExplosionDamage();
                continue;
            }

            float dist    = Vector2.Distance(transform.position, hit.transform.position);
            float falloff = Mathf.Clamp01(1f - dist / _areaDamageRadius);
            if (falloff <= 0f) continue;

            var block = hit.GetComponentInParent<BlockBase>();
            if (block != null && block != (BlockBase)this && !block.IsDestroyed)
                block.TakeDamage(_areaDamage * falloff);
        }
    }
}
