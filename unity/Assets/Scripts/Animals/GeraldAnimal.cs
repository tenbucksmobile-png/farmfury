using UnityEngine;

/// <summary>
/// Gerald (turkey). Ability changed 2026-11-08 (user request: "the turkey shoots feather bombs"): tap mid-flight to
/// drop three feather bombs in a forward-down fan; each bursts on contact in a small radius with a feather burst.
/// Replaces the old Puff Up.
/// </summary>
public class GeraldAnimal : AnimalBase
{
    [Header("Feather Bombs")]
    [SerializeField] private Sprite _bombSprite;    // FeatherBomb.png (wired by Wire Scene References)
    [SerializeField] private Sprite _burstSprite;   // FeatherBurst.png
    private const int   BombCount   = 3;
    private const float FanCentre   = -60f, FanSpread = 40f;   // degrees, relative to straight ahead
    private const float BombSpeed   = 6f;

    static AbilityProjectile.Spec BombSpec(Sprite bomb, Sprite burst) => new AbilityProjectile.Spec
    {
        sprite = bomb, burstSprite = burst, size = 0.55f, mass = 1.5f, bounciness = 0f, gravityScale = 0.6f,
        hits = 1, blockDamage = 30f, burstRadius = 0.9f, robotHits = 2, spin = 360f, life = 4f,
    };

    protected override void Awake()
    {
        mass       = 10f;
        bounciness = 0.2f;
        linearDrag = 0.008f;
        base.Awake();
        if (!HasRealSprites) _sr.color = new Color(0.50f, 0.28f, 0.08f); // dark turkey brown
        _sr.sortingOrder = 6;
        _col.radius      = 0.38f;
    }

    protected override void TriggerAbility()
    {
        Vector2 vel = _rb.linearVelocity;
        float sign = vel.x >= 0f ? 1f : -1f;
        var spec = BombSpec(_bombSprite, _burstSprite);
        for (int i = 0; i < BombCount; i++)
        {
            float t = BombCount > 1 ? i / (float)(BombCount - 1) : 0.5f;
            float rad = (FanCentre + Mathf.Lerp(-FanSpread * 0.5f, FanSpread * 0.5f, t)) * Mathf.Deg2Rad;
            var dir = new Vector2(Mathf.Cos(rad) * sign, Mathf.Sin(rad));
            AbilityProjectile.Spawn(spec, transform.position + (Vector3)(dir * 0.5f), vel * 0.5f + dir * BombSpeed, _col);
        }
    }
}
