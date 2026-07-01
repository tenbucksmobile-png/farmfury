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

    protected override void OnCollisionEnter2D(Collision2D col)
    {
        base.OnCollisionEnter2D(col);

        if (_slammed && col.gameObject.CompareTag("Ground"))
            StartCoroutine(Shockwave());
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