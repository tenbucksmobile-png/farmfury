using System.Collections;
using UnityEngine;

public class BessieAnimal : AnimalBase
{
    [Header("Ground Slam")]
    [SerializeField] private float      _slamImpulse     = 18f;
    [SerializeField] private float      _shockwaveRadius = 3.6f;
    [SerializeField] private float      _shockwaveForce  = 12f;
    [SerializeField] private float      _shockwaveDamage = 30f;

    [Header("VFX")]
    [SerializeField] private GameObject _shockwaveRingPrefab;

    [Header("SFX")]
    [SerializeField] private AudioClip _earthquakeClip; // Bessie_Earthquake.mp3 — see Shockwave()

    // Guaranteed-kill amount for a triggered direct robot hit (2026-07-10, user request: "if
    // trigger hits a robot it destroys"). TakeDamage() clamps Health at 0 internally, so any
    // sufficiently large value is a safe, deterministic kill regardless of the robot's current
    // HP — simpler than adding a separate "force kill" method to RobotEnemy's public API.
    private const float InstantKillDamage = 999999f;

    private bool _slammed;

    protected override void Awake()
    {
        mass       = 28f;
        bounciness = 0.15f;
        linearDrag = 0.016f;
        base.Awake();
        if (_sr) { if (!HasRealSprites) _sr.color = new Color(1f, 0.4f, 0.7f); _sr.sortingOrder = 6; } // pink; fixed 2026-07-08: was 5, needed a slot free for CannonSmoke at 5
        if (_col) _col.radius = 0.52f; // 26px / 50
    }

    // Ceiling on total downward speed after the slam impulse is added — 2026-07-13, user report:
    // "when bessie is triggered she falls through the floor (so no tremor/earthquake happens)".
    // TriggerAbility() used to add _slamImpulse on top of whatever fall speed Bessie already had
    // with no upper bound; stacked on an already-fast fall, the resulting velocity could out-run
    // how far Physics2D's continuous collision detection reliably sweeps in one fixed timestep
    // against the Ground collider's thin profile, letting her tunnel straight through before
    // OnCollisionEnter2D ever fires — so the tremor/shockwave (which only fires FROM that
    // collision) silently never happens. Capping the resulting speed keeps the dramatic slam feel
    // while staying inside a range physics can reliably catch.
    private const float MaxSlammedDownwardSpeed = 22f;

    protected override void TriggerAbility()
    {
        if (_slammed) return;
        _slammed = true;
        _rb.linearVelocity += Vector2.down * _slamImpulse;
        if (_rb.linearVelocity.y < -MaxSlammedDownwardSpeed)
            _rb.linearVelocity = new Vector2(_rb.linearVelocity.x, -MaxSlammedDownwardSpeed);
    }

    // Ground Slam ability rework (2026-07-10, user request): "bessie_trigger... causes a tremor
    // earthquake when hitting the ground, or crashes through a structure. Also if trigger hits a
    // robot it destroys. But if trigger is not activated apply normal damage directly to robot —
    // bessie_impact." Three distinct outcomes once the ability is active (_slammed):
    //   - Ground or ANY structure (BlockBase) impact -> the tremor/earthquake shockwave below.
    //     Previously gated on Ground-tag only — "crashes through a structure" broadens it to any
    //     block, not just the ground surface itself.
    //   - Direct robot impact -> outright kill (InstantKillDamage), not the usual fractional
    //     direct-hit amount.
    // If the ability was never triggered, none of the above fires — RobotEnemy's own generic
    // OnCollisionEnter2D already applies the standard TakeDirectHitDamage() fraction to any
    // AnimalBase collision (Bessie included), and base.OnCollisionEnter2D() above already shows
    // the normal Bessie_Impact.png pose via _sprImpact. That existing default IS the "trigger not
    // activated" case — no extra code needed for it here.
    protected override void OnCollisionEnter2D(Collision2D col)
    {
        base.OnCollisionEnter2D(col);

        if (!_slammed) return;

        bool hitGround    = col.gameObject.CompareTag("Ground");
        bool hitStructure  = col.gameObject.TryGetComponent<BlockBase>(out _);
        if (hitGround || hitStructure)
            StartCoroutine(Shockwave());

        if (col.gameObject.TryGetComponent<RobotEnemy>(out var robot))
            robot.TakeDamage(InstantKillDamage);
    }

    // Small beat between the landing impact and the tremor's ring/SFX/damage actually firing —
    // 2026-07-13, user report: "review Bessie trigger and earthquake — the sprite disappears
    // before anything is hit — delay the disappearance so the impact and damage is seen, slightly
    // delay the explosions if any." Previously this all fired the same frame as the collision
    // (OnCollisionEnter2D starts this coroutine, and everything below ran before its single
    // `yield return null`), landing on top of AnimalBase's own impact-freeze in the same instant —
    // paired with AnimalBase._contactTimeout's own increase (0.25 -> 0.45s), Bessie now visibly
    // lands, THEN the ground shakes, giving the impact and the shockwave two distinct beats
    // instead of one simultaneous blur.
    const float ShockwaveDelay = 0.12f;

    IEnumerator Shockwave()
    {
        yield return new WaitForSeconds(ShockwaveDelay);
        if (IsDestroyed) yield break; // safety net — shouldn't happen given the contact timeout above, but don't act on a bird that's already gone

        if (_shockwaveRingPrefab)
            Instantiate(_shockwaveRingPrefab, transform.position, Quaternion.identity);

        // Ground Slam earthquake SFX (2026-07-11, user request: "wire up Bessie_Earthquake.mp3
        // every time Bessie power is triggered and they hit the floor for earthquake") — fires
        // exactly once per real tremor, i.e. only when the ability was triggered AND she actually
        // hit the ground/a structure (this coroutine is only ever started from that branch in
        // OnCollisionEnter2D above), not on every tap or every collision.
        if (_earthquakeClip != null)
            AudioManager.PlayClip(_earthquakeClip);

        var hits = Physics2D.OverlapCircleAll(transform.position, _shockwaveRadius);
        foreach (var hit in hits)
        {
            float   dist    = Vector2.Distance(hit.transform.position, transform.position);
            float   falloff = 1f - Mathf.Clamp01(dist / _shockwaveRadius);
            Vector2 dir     = ((Vector2)hit.transform.position
                              - (Vector2)transform.position).normalized;

            if (hit.TryGetComponent<Rigidbody2D>(out var rb))
                rb.AddForce(dir * _shockwaveForce * falloff, ForceMode2D.Impulse);

            if (hit.TryGetComponent<BlockBase>(out var block))
                block.TakeDamage(_shockwaveDamage * falloff);

            if (hit.TryGetComponent<RobotEnemy>(out var robot))
                robot.TakeDamage(_shockwaveDamage * falloff);
        }

        yield return null;
    }
}