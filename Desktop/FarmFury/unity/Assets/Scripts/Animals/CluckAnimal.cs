using System.Collections;
using UnityEngine;

public class CluckAnimal : AnimalBase
{
    [Header("Cluster Bomb")]
    [SerializeField] private GameObject _eggPrefab;
    [SerializeField] private int        _eggCount    = 5;
    [SerializeField] private float      _minEggSpeed = 5f;
    [SerializeField] private float      _spreadDeg   = 120f;

    [Header("Flash")]
    [SerializeField] private float _flashDuration = 0.12f;

    // Cached pre-impact velocity — recorded every FixedUpdate so OnCollisionEnter2D
    // can restore the correct direction and speed after the physics response fires.
    private Vector2 _lastVelocity;

    protected override void Awake()
    {
        mass       = 8f;
        bounciness = 0.4f;
        linearDrag = 0.008f;
        base.Awake();
        if (_sr) { if (!HasRealSprites) _sr.color = Color.yellow; _sr.sortingOrder = 4; }
        if (_col) _col.radius = 0.36f; // 18px / 50
    }

    void FixedUpdate()
    {
        // Track velocity before every physics step so pass-through can restore it.
        if (IsLaunched && !IsDestroyed)
            _lastVelocity = _rb.linearVelocity;
    }

    // ── Pass-through: Cluck punches through hay bales at 70 % velocity ──────────
    // BlockBase.OnCollisionEnter2D fires in the same frame and handles damage normally.
    // We only need to restore Cluck's velocity and skip the base "stop-and-hide" path.
    protected override void OnCollisionEnter2D(Collision2D col)
    {
        if (IsInFlight)
        {
            var wood = col.gameObject.GetComponent<WoodBlock>();
            if (wood != null && wood._passThrough)
            {
                // Restore direction at 70 % pre-impact speed.
                float preSpeed = _lastVelocity.magnitude;
                if (preSpeed > 0.01f)
                    _rb.linearVelocity = _lastVelocity.normalized * (preSpeed * 0.7f);

                // Stop Physics2D from resolving further contacts with this block.
                Physics2D.IgnoreCollision(_col, col.collider, true);

                StartCoroutine(FlashPassThrough());
                AudioManager.Play(AudioManager.Sound.WoodHit, cooldown: 0f);
                return; // skip base stop-and-hide
            }
        }
        base.OnCollisionEnter2D(col);
    }

    // Brief scale-pulse gives visible feedback that Cluck punched through something.
    // Works with sprite art (color white → no visible tint change) and fallback circle.
    IEnumerator FlashPassThrough()
    {
        Vector3 orig = transform.localScale;
        transform.localScale = orig * 1.2f;
        yield return new WaitForSeconds(0.08f);
        if (!IsDestroyed && this != null) transform.localScale = orig;
    }

    protected override void TriggerAbility()
    {
        StartCoroutine(FlashWhite());
        SpawnEggs();
    }

    void SpawnEggs()
    {
        Vector2 vel       = _rb.linearVelocity;
        float   baseAngle = Mathf.Atan2(vel.y, vel.x) * Mathf.Rad2Deg;
        float   speed     = Mathf.Max(vel.magnitude * 0.6f, _minEggSpeed);

        for (int i = 0; i < _eggCount; i++)
        {
            float t      = _eggCount > 1 ? i / (float)(_eggCount - 1) : 0.5f;
            float offset = Mathf.Lerp(-_spreadDeg * 0.5f, _spreadDeg * 0.5f, t);
            float rad    = (baseAngle + offset) * Mathf.Deg2Rad;
            var   dir    = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));

            var egg = Instantiate(_eggPrefab, transform.position, Quaternion.identity);

            if (egg.TryGetComponent<Rigidbody2D>(out var rb))
                rb.linearVelocity = dir * speed;
        }
    }

    IEnumerator FlashWhite()
    {
        if (_sr && !HasRealSprites) _sr.color = Color.white;
        yield return new WaitForSeconds(_flashDuration);
        if (_sr && !HasRealSprites) _sr.color = Color.yellow;
    }
}