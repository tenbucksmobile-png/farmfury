using UnityEngine;

// Horace (horse) — Rear Kick: on tap, arms a kick. On first contact with a
// structure, nearby objects are blasted backward (opposite to travel direction).
public class HoraceAnimal : AnimalBase
{
    [Header("Rear Kick")]
    [SerializeField] private float _kickRadius  = 2.8f;
    [SerializeField] private float _kickForce   = 18f;
    [SerializeField] private float _kickDamage  = 25f;

    private bool    _kickArmed;
    private bool    _kicked;
    private Vector2 _kickDir;

    protected override void Awake()
    {
        mass       = 18f;
        bounciness = 0.15f;
        linearDrag = 0.012f;
        base.Awake();
        if (!HasRealSprites) _sr.color = new Color(0.55f, 0.32f, 0.10f); // chestnut brown
        _sr.sortingOrder = 6; // fixed 2026-07-08: was 5, needed a slot free for CannonSmoke at 5
        _col.radius      = 0.40f;
    }

    protected override void TriggerAbility()
    {
        _kickArmed = true;
        // Record backward direction from current travel for the eventual kick
        _kickDir   = -_rb.linearVelocity.normalized;
        if (!HasRealSprites) _sr.color = new Color(0.80f, 0.55f, 0.10f); // lighter "armed" tint
    }

    protected override void OnCollisionEnter2D(Collision2D col)
    {
        base.OnCollisionEnter2D(col);

        if (_kickArmed && !_kicked && !col.gameObject.CompareTag("Ground"))
        {
            _kicked = true;
            PerformKick();
        }
    }

    void PerformKick()
    {
        var hits = Physics2D.OverlapCircleAll(transform.position, _kickRadius);
        foreach (var hit in hits)
        {
            if (hit.gameObject == gameObject) continue;

            float   dist    = Vector2.Distance(hit.transform.position, transform.position);
            float   falloff = 1f - Mathf.Clamp01(dist / _kickRadius);

            if (hit.TryGetComponent<Rigidbody2D>(out var rb))
                rb.AddForce(_kickDir * _kickForce * falloff, ForceMode2D.Impulse);

            if (hit.TryGetComponent<BlockBase>(out var block))
                block.TakeDamage(_kickDamage * falloff);

            if (hit.TryGetComponent<RobotEnemy>(out var robot))
                robot.TakeDamage(_kickDamage * falloff);
        }
    }
}
