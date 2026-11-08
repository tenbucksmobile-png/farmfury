using UnityEngine;

/// <summary>
/// Woolly (sheep). Ability changed 2026-11-08 (user request: "the sheep throws wool balls"): tap mid-flight to
/// throw three heavy, bouncy wool balls ahead in a narrow fan. They bounce through the structure, each damaging up
/// to three blocks/robots it hits. Replaces the old Triple Clone.
/// </summary>
public class WoollyAnimal : AnimalBase
{
    [Header("Wool Balls")]
    [SerializeField] private Sprite _woolBallSprite;   // WoolBall.png (wired by Wire Scene References)
    private const int   BallCount  = 3;
    private const float FanSpread  = 20f;    // degrees around the flight direction
    private const float SpeedBoost = 1.15f;  // balls leave a little faster than Woolly

    static AbilityProjectile.Spec BallSpec(Sprite s) => new AbilityProjectile.Spec
    {
        sprite = s, size = 0.5f, mass = 3f, bounciness = 0.55f, gravityScale = 0.5f,
        hits = 3, blockDamage = 14f, burstRadius = 0f, robotHits = 1, spin = 540f, life = 6f,
    };

    protected override void Awake()
    {
        mass       = 6f;
        bounciness = 0.3f;
        linearDrag = 0.01f;
        base.Awake();
        _sr.color        = Color.white;
        _sr.sortingOrder = 6;
        _col.radius      = 0.36f;
    }

    protected override void TriggerAbility()
    {
        Vector2 vel = _rb.linearVelocity;
        float angle = Mathf.Atan2(vel.y, vel.x) * Mathf.Rad2Deg;
        float speed = Mathf.Max(5f, vel.magnitude * SpeedBoost);
        var spec = BallSpec(_woolBallSprite);
        for (int i = 0; i < BallCount; i++)
        {
            float t = BallCount > 1 ? i / (float)(BallCount - 1) : 0.5f;
            float rad = (angle + Mathf.Lerp(-FanSpread * 0.5f, FanSpread * 0.5f, t)) * Mathf.Deg2Rad;
            var dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
            AbilityProjectile.Spawn(spec, transform.position + (Vector3)(dir * 0.55f), dir * speed, _col);
        }
    }
}
