using UnityEngine;

/// <summary>
/// Horace (horse). Ability changed 2026-11-09 (user: "horace horseshoe throw ability is not working"): the old Rear
/// Kick armed on tap and, on Horace's next contact, blasted nearby debris BACKWARD — toward the cannon, away from the
/// structure — so in play it looked like nothing happened, while his ability badge already showed a horseshoe. Now a
/// tap hurls two heavy iron horseshoes ahead on a flat, fast line. Each is a physical <see cref="AbilityProjectile"/>
/// (bounce-and-hit): heavy enough to shove and break what it strikes, ricocheting on through the structure for up to
/// three hits; robots take two egg-tier hits per strike.
/// </summary>
public class HoraceAnimal : AnimalBase
{
    [Header("Horseshoe Throw")]
    [SerializeField] private Sprite _horseshoeSprite;   // Horseshoe.png (Stampede horseshoe, wired by Wire Scene References)
    private const int   ShoeCount  = 2;
    private const float ShoeSpread = 10f;    // degrees between the two
    private const float ShoeSpeed  = 13f;

    static AbilityProjectile.Spec ShoeSpec(Sprite shoe) => new AbilityProjectile.Spec
    {
        sprite = shoe, size = 0.5f, mass = 6f, bounciness = 0.35f, gravityScale = 0.35f,
        hits = 3, blockDamage = 16f, burstRadius = 0f, robotHits = 2, spin = 900f, life = 4f,
    };

    protected override void Awake()
    {
        mass       = 18f;
        bounciness = 0.15f;
        linearDrag = 0.012f;
        base.Awake();
        if (!HasRealSprites) _sr.color = new Color(0.55f, 0.32f, 0.10f); // chestnut brown
        _sr.sortingOrder = 6; // fixed 2026-07-08: was 5, needed a slot free for CannonSmoke at 5
        _col.radius      = 0.40f;
    }

    protected override void TriggerAbility()
    {
        Vector2 vel = _rb.linearVelocity;
        float sign = vel.x >= 0f ? 1f : -1f;
        // Thrown level-ish whatever Horace is doing: a flat line into the structure, not into the ground.
        Vector2 fwd = vel.sqrMagnitude > 0.01f ? vel.normalized : Vector2.right;
        float angle = Mathf.Clamp(Mathf.Atan2(fwd.y, Mathf.Abs(fwd.x)) * Mathf.Rad2Deg, -15f, 20f);
        var spec = ShoeSpec(_horseshoeSprite);
        for (int i = 0; i < ShoeCount; i++)
        {
            float a = (angle + (i - (ShoeCount - 1) * 0.5f) * ShoeSpread) * Mathf.Deg2Rad;
            var dir = new Vector2(Mathf.Cos(a) * sign, Mathf.Sin(a));
            AbilityProjectile.Spawn(spec, transform.position + (Vector3)(dir * 0.5f), dir * ShoeSpeed, _col);
        }
        AudioManager.Play(AudioManager.Sound.Launch);
    }
}
