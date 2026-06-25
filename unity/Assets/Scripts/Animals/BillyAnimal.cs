using System.Collections;
using UnityEngine;

// Billy (goat) — Headbutt Through: on tap, sets the collider to trigger mode
// for _penetrateDuration seconds. During that window Billy passes through
// obstacles, dealing damage via OnTriggerEnter2D. After the window expires
// the collider is restored and normal physics resume.
public class BillyAnimal : AnimalBase
{
    [Header("Headbutt Through")]
    [SerializeField] private float _penetrateDuration = 0.45f;
    [SerializeField] private float _penetrateDamage   = 60f;
    [SerializeField] private float _minSpeed          = 8f;    // ensure Billy keeps moving

    private bool _penetrating;

    protected override void Awake()
    {
        mass       = 12f;
        bounciness = 0.10f;
        linearDrag = 0.01f;
        base.Awake();
        _sr.color        = new Color(0.80f, 0.80f, 0.80f); // light grey
        _sr.sortingOrder = 4;
        _col.radius      = 0.36f;
    }

    protected override void TriggerAbility()
    {
        if (_penetrating) return;
        StartCoroutine(PenetrateWindow());
        // Ensure minimum speed so Billy doesn't stall inside a structure
        if (_rb.linearVelocity.magnitude < _minSpeed)
            _rb.linearVelocity = _rb.linearVelocity.normalized * _minSpeed;
        _sr.color = new Color(0.35f, 0.35f, 0.35f); // darker "charging" tint
    }

    IEnumerator PenetrateWindow()
    {
        _penetrating    = true;
        _col.isTrigger  = true;
        yield return new WaitForSeconds(_penetrateDuration);
        _col.isTrigger  = false;
        _penetrating    = false;
        _sr.color       = new Color(0.80f, 0.80f, 0.80f);
    }

    // While trigger is active, deal damage to anything Billy overlaps.
    void OnTriggerEnter2D(Collider2D other)
    {
        if (!_penetrating) return;
        other.TryGetComponent<BlockBase>(out var block);
        block?.TakeDamage(_penetrateDamage);
        other.TryGetComponent<RobotEnemy>(out var robot);
        robot?.TakeDamage(_penetrateDamage);
    }
}
