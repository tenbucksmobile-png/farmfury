using UnityEngine;

// Ducky (duck) — Skip Shot: on tap, flattens trajectory for skimming.
// Skips off flat ground surfaces up to 3x before normal landing physics.
public class DuckyAnimal : AnimalBase
{
    [Header("Skip Shot")]
    [SerializeField] private int   _maxSkips      = 3;
    [SerializeField] private float _skipBounceY   = 4f;   // upward velocity on each skip
    [SerializeField] private float _horizontalMul = 1.2f; // speed boost on tap
    [SerializeField] private float _flattenY      = 0.05f; // downward fraction kept on tap

    private bool _skipEnabled;
    private int  _skipCount;

    protected override void Awake()
    {
        mass       = 5f;
        bounciness = 0.1f;
        linearDrag = 0.005f;
        base.Awake();
        if (!HasRealSprites) _sr.color = new Color(1f, 0.85f, 0f); // bright yellow
        _sr.sortingOrder = 4;
        _col.radius      = 0.30f;
    }

    protected override void TriggerAbility()
    {
        _skipEnabled = true;
        var vel = _rb.linearVelocity;
        // Flatten trajectory: preserve horizontal, reduce vertical
        _rb.linearVelocity = new Vector2(
            vel.x * _horizontalMul,
            -Mathf.Abs(vel.y) * _flattenY);
        if (!HasRealSprites) _sr.color = new Color(1f, 0.65f, 0f); // orange "skip mode" tint
    }

    protected override void OnCollisionEnter2D(Collision2D col)
    {
        // Skip off ground only
        if (_skipEnabled && _skipCount < _maxSkips && col.gameObject.CompareTag("Ground"))
        {
            _skipCount++;
            var vel = _rb.linearVelocity;
            // Preserve horizontal speed, add upward kick
            _rb.linearVelocity = new Vector2(vel.x * 0.95f, _skipBounceY);
            // Don't start contact timer — Ducky is still active
            return;
        }

        _skipEnabled = false;
        base.OnCollisionEnter2D(col);
    }
}
