using System.Collections;
using UnityEngine;

public class CluckAnimal : AnimalBase
{
    [Header("Cluster Bomb")]
    [SerializeField] private GameObject _eggPrefab;
    [SerializeField] private int        _eggCount    = 5;
    [SerializeField] private float      _minEggSpeed = 5f;
    // Fixed "cannon blast" cone (2026-07-10, user request: eggs should "fly out of him like
    // being fired from a cannon into 5 directions forwards and down") rather than the previous
    // spread centred on Cluck's exact instantaneous flight-velocity angle — that varied wildly
    // depending on where in the arc the ability was tapped (near-horizontal at the apex, steep
    // near launch/landing), so the burst never looked like a consistent, deliberate blast.
    // _coneCenterDeg/_spreadDeg are measured from horizontal-forward (0°) with positive = up, so
    // a −45° centre with an 80° spread fans the 5 eggs from −5° (barely below horizontal) to
    // −85° (nearly straight down), always canted forward+down regardless of Cluck's actual
    // in-flight angle at the moment of the tap.
    [SerializeField] private float      _coneCenterDeg = -45f;
    [SerializeField] private float      _spreadDeg     = 80f;

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
        if (_sr) { if (!HasRealSprites) _sr.color = Color.yellow; _sr.sortingOrder = 6; } // fixed 2026-07-08: was 5, needed a slot free for CannonSmoke at 5
        if (_col) _col.radius = 0.36f; // 18px / 50
    }

    void FixedUpdate()
    {
        // Track velocity before every physics step so pass-through can restore it.
        if (IsLaunched && !IsDestroyed)
            _lastVelocity = _rb.linearVelocity;
    }

    // ── Pass-through: Cluck punches through hay bales at 70 % velocity ──────────
    // BlockBase.OnCollisionEnter2D also fires in the same frame on the block's own
    // GameObject, but its damage is physics-impulse-derived (relativeVelocity × effective
    // mass) — inconsistent across the arc (a bird arriving near the top of its lob, or with
    // drag/gravity having bled off speed, can land well under one-shot impulse), so a bale
    // could survive and need a second/third bird to finish it off. Passing through is the
    // whole point of this ability, so kill the bale outright here instead of trusting
    // impulse math (2026-07-06 fix — user reported it taking all 3 birds to clear one pile).
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

                wood.TakeDamage(wood.MaxHealth); // guaranteed one-hit kill, regardless of impact speed

                StartCoroutine(FlashPassThrough());
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
        Vector2 vel   = _rb.linearVelocity;
        // "Forward" is always the launch direction's horizontal sign (in practice always +1 —
        // the cannon only ever fires rightward — but Sign() keeps this correct if that ever
        // changes) rather than the exact current velocity angle; only the sign is used, the
        // cone's actual shape is fixed by _coneCenterDeg/_spreadDeg above.
        float forwardSign = vel.x >= 0f ? 1f : -1f;
        float speed       = Mathf.Max(vel.magnitude * 0.6f, _minEggSpeed);

        for (int i = 0; i < _eggCount; i++)
        {
            float t      = _eggCount > 1 ? i / (float)(_eggCount - 1) : 0.5f;
            float angle  = _coneCenterDeg + Mathf.Lerp(-_spreadDeg * 0.5f, _spreadDeg * 0.5f, t);
            float rad    = angle * Mathf.Deg2Rad;
            var   dir    = new Vector2(Mathf.Cos(rad) * forwardSign, Mathf.Sin(rad));

            // Spawn slightly forward along the egg's own direction rather than exactly at
            // Cluck's position (2026-07-13 user report: "eggs disappear before striking
            // anything" / "only 3 or less eggs drop"). Cluck's own collider is very often still
            // overlapping a block at the moment the ability fires — the passThrough hay-punch
            // cone above literally requires it — so an egg instantiated at that exact point
            // spawns already embedded in that same block and gets destroyed by
            // EggProjectile.OnCollisionEnter2D on the very next physics step, before it's ever
            // visibly airborne. 0.5 clears both Cluck's own 0.36 collider radius and the egg's
            // own 0.18, with margin.
            Vector3 spawnPos = transform.position + (Vector3)(dir.normalized * 0.5f);
            var egg = Instantiate(_eggPrefab, spawnPos, Quaternion.identity);

            if (egg.TryGetComponent<Rigidbody2D>(out var rb))
                rb.linearVelocity = dir * speed;

            // Belt-and-suspenders on top of the 0.5 spawn offset above (2026-07-13, user report:
            // "eggs are still random — there must be five and they all must inflict damage"): in a
            // dense enough structure the 0.5 offset can still land an egg inside a DIFFERENT
            // nearby block than the one Cluck is punching through, especially near the tightly
            // packed towers introduced from L10 onward. Explicitly ignore collision with anything
            // still overlapping the egg's own collider at the exact moment it spawns — permanently
            // for THIS egg only, so it's guaranteed to clear whatever it was born inside of and
            // fly its full course, while still colliding normally with everything else (including
            // that same block later, if it happens to fly back into it).
            if (egg.TryGetComponent<Collider2D>(out var eggCol))
            {
                var overlaps = Physics2D.OverlapCircleAll(spawnPos, eggCol.bounds.extents.x);
                foreach (var other in overlaps)
                    if (other != null && other != eggCol)
                        Physics2D.IgnoreCollision(eggCol, other, true);
            }
        }
    }

    IEnumerator FlashWhite()
    {
        if (_sr && !HasRealSprites) _sr.color = Color.white;
        yield return new WaitForSeconds(_flashDuration);
        if (_sr && !HasRealSprites) _sr.color = Color.yellow;
    }
}