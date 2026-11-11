using UnityEngine;

/// <summary>
/// Ducky (duck). Ability changed 2026-11-09 (user request: "ducky ability should shoot swirls of water out that
/// enlarge and splash over the structures"): tap mid-flight and Ducky fires three spinning water swirls ahead in a
/// narrow fan. Each swirl flies almost straight, grows as it travels (small at the beak, about two animals wide by
/// the time it reaches a structure; reduced 2026-11-09, "too oversized") and bursts on its first contact, splashing everything in a radius that grows
/// with it. Built on <see cref="AbilityProjectile"/> (burst mode + growth), so block damage and the egg-tier robot
/// hits stay inside the existing damage model. Replaces Skip Shot.
/// </summary>
public class DuckyAnimal : AnimalBase
{
    [Header("Water Swirls")]
    [SerializeField] private Sprite _swirlSprite;   // WaterSwirl.png (Stampede WaterSpout, wired by Wire Scene References)
    private const int   SwirlCount  = 3;
    private const float FanSpread   = 24f;    // degrees across the three, centred on the flight direction
    private const float SwirlSpeed  = 9f;

    static AbilityProjectile.Spec SwirlSpec(Sprite swirl) => new AbilityProjectile.Spec
    {
        sprite = swirl, burstSprite = swirl, size = 0.35f, growTo = 1.05f, growTime = 0.9f,   // was 0.4 -> 1.5 (too big)
        mass = 1f, bounciness = 0f, gravityScale = 0.12f, hits = 1,
        // A wide splash that shoves more than it smashes: modest damage (posts and planks survive a glancing
        // splash), an outward push that topples what it soaks, and one egg-tier hit per robot per swirl.
        blockDamage = 5f, burstRadius = 0.38f,    // x growth: 0.38 at launch, ~1.1 u (2.3 u across) fully grown
        burstPush = 6f, robotHits = 1, spin = 540f, life = 3.5f,
    };

    protected override void Awake()
    {
        mass       = 5f;
        bounciness = 0.1f;
        linearDrag = 0.005f;
        base.Awake();
        if (!HasRealSprites) _sr.color = new Color(1f, 0.85f, 0f); // bright yellow
        _sr.sortingOrder = 6; // fixed 2026-07-08: was 5, needed a slot free for CannonSmoke at 5
        _col.radius      = 0.30f;
    }

    protected override void TriggerAbility()
    {
        float speed = AbilitySpeed(SwirlSpeed);   // forward-and-down, faster than Ducky (2026-11-11)
        var spec = SwirlSpec(_swirlSprite);
        for (int i = 0; i < SwirlCount; i++)
        {
            float t = SwirlCount > 1 ? i / (float)(SwirlCount - 1) : 0.5f;
            var dir = AbilityAim(35f, Mathf.Lerp(-FanSpread * 0.5f, FanSpread * 0.5f, t));
            AbilityProjectile.Spawn(spec, transform.position + (Vector3)(dir * 0.45f), dir * speed, _col);
        }
        AudioManager.Play(AudioManager.Sound.Launch);
    }
}
