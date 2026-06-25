using UnityEngine;

// Gerald (turkey) — Puff Up: on tap, inflates to 3x size. The CircleCollider2D
// scales with the transform, so no explicit radius change is needed.
public class GeraldAnimal : AnimalBase
{
    [Header("Puff Up")]
    [SerializeField] private float _puffScale     = 3f;
    [SerializeField] private float _puffMassMul   = 4f;
    [SerializeField] private float _puffImpulse   = 6f;   // forward boost on puff

    private bool  _puffed;
    private float _originalRadius;

    protected override void Awake()
    {
        mass       = 10f;
        bounciness = 0.2f;
        linearDrag = 0.008f;
        base.Awake();
        if (!HasRealSprites) _sr.color = new Color(0.50f, 0.28f, 0.08f); // dark turkey brown
        _sr.sortingOrder = 4;
        _col.radius      = 0.38f;
        _originalRadius  = _col.radius;
    }

    protected override void TriggerAbility()
    {
        if (_puffed) return;
        _puffed = true;

        transform.localScale = Vector3.one * _puffScale;
        _rb.mass             = mass * _puffMassMul;
        _rb.AddForce(_rb.linearVelocity.normalized * _puffImpulse, ForceMode2D.Impulse);
        if (!HasRealSprites) _sr.color = new Color(0.85f, 0.45f, 0.10f); // bright orange puffed tint
    }
}
