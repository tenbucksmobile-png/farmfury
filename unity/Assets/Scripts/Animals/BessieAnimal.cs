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

    protected override void TriggerAbility()
    {
        if (_slammed) return;
        _slammed = true;
        _rb.linearVelocity += Vector2.down * _slamImpulse;
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

    IEnumerator Shockwave()
    {
        if (_shockwaveRingPrefab)
            Instantiate(_shockwaveRingPrefab, transform.position, Quaternion.identity);

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