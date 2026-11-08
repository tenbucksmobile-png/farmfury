using UnityEngine;

/// <summary>
/// Billy (goat). Ability changed 2026-11-08 (user request: "billy throws mud"): tap mid-flight to hurl one big mud
/// ball ahead and slightly down; it splats on contact, damaging everything in a wide radius and leaving a mud
/// splat. Replaces the old Headbutt Through.
/// </summary>
public class BillyAnimal : AnimalBase
{
    [Header("Mud Throw")]
    [SerializeField] private Sprite _mudBallSprite;   // MudBall.png (wired by Wire Scene References)
    [SerializeField] private Sprite _mudSplatSprite;  // MudSplat.png
    private const float ThrowDownDeg = -15f;          // relative to the flight direction
    private const float ThrowSpeed   = 1.25f;

    static AbilityProjectile.Spec MudSpec(Sprite ball, Sprite splat) => new AbilityProjectile.Spec
    {
        sprite = ball, burstSprite = splat, size = 0.65f, mass = 4f, bounciness = 0f, gravityScale = 0.6f,
        hits = 1, blockDamage = 22f, burstRadius = 1.3f, robotHits = 2, spin = 200f, life = 4f,
    };

    protected override void Awake()
    {
        mass       = 12f;
        bounciness = 0.10f;
        linearDrag = 0.01f;
        base.Awake();
        if (!HasRealSprites) _sr.color = new Color(0.80f, 0.80f, 0.80f); // light grey
        _sr.sortingOrder = 6;
        _col.radius      = 0.36f;
    }

    protected override void TriggerAbility()
    {
        Vector2 vel = _rb.linearVelocity;
        float rad = (Mathf.Atan2(vel.y, vel.x) * Mathf.Rad2Deg + ThrowDownDeg * Mathf.Sign(vel.x == 0 ? 1 : vel.x)) * Mathf.Deg2Rad;
        var dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
        AbilityProjectile.Spawn(MudSpec(_mudBallSprite, _mudSplatSprite), transform.position + (Vector3)(dir * 0.6f),
            dir * Mathf.Max(6f, vel.magnitude * ThrowSpeed), _col);
    }
}
