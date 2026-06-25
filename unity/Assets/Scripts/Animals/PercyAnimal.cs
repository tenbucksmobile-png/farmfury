using UnityEngine;

// Percy (pig) — Bounce Roll: on tap, curls into a ball that bounces up to 3x
// gaining speed with each bounce.
public class PercyAnimal : AnimalBase
{
    [Header("Bounce Roll")]
    [SerializeField] private int   _maxBounces   = 3;
    [SerializeField] private float _bounceBoost  = 1.35f;  // speed multiplier per bounce
    [SerializeField] private float _rollDrag      = 0f;
    [SerializeField] private float _rollBounciness = 1.1f;

    private bool _rolling;
    private int  _bouncesLeft;
    private PhysicsMaterial2D _rollMat;

    protected override void Awake()
    {
        mass       = 8f;
        bounciness = 0.4f;
        linearDrag = 0.008f;
        base.Awake();
        _sr.color        = new Color(1f, 0.6f, 0.65f); // salmon pink
        _sr.sortingOrder = 4;
        _col.radius      = 0.36f;

        _rollMat = new PhysicsMaterial2D("PercyRoll") { bounciness = _rollBounciness, friction = 0f };
    }

    protected override void TriggerAbility()
    {
        _rolling      = true;
        _bouncesLeft  = _maxBounces;
        _rb.linearDamping = _rollDrag;
        _col.sharedMaterial = _rollMat;
        _sr.color = new Color(1f, 0.85f, 0.2f); // golden "rolled up" tint
    }

    protected override void OnCollisionEnter2D(Collision2D col)
    {
        if (_rolling && _bouncesLeft > 0)
        {
            _bouncesLeft--;
            IsInFlight = false;
            return; // skip contact timer — Percy is still active during bounces
        }

        _rolling = false;
        base.OnCollisionEnter2D(col);
    }

    void OnCollisionExit2D(Collision2D col)
    {
        if (!_rolling || _bouncesLeft < 0) return;
        // Boost speed after each bounce exit
        _rb.linearVelocity *= _bounceBoost;
        if (_bouncesLeft > 0) IsInFlight = true;
    }
}
