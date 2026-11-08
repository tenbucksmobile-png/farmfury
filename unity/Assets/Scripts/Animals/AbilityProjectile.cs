using UnityEngine;

/// <summary>
/// Runtime-built projectile for the thrown abilities (2026-11-08, user request): Gerald's feather bombs, Woolly's
/// wool balls and Billy's mud. Built in code (no prefab) from a <see cref="Spec"/>, on the Egg layer like Cluck's
/// eggs so the projectiles never collide with each other or the thrower. A projectile either bursts on its first hit
/// (feather bomb, mud — damage in a radius, a burst sprite left briefly) or bounces and keeps hitting up to
/// <see cref="Spec.hits"/> times (wool ball — physical, rolls along the structure). Robots take the egg-tier hit
/// (<see cref="RobotEnemy.TakeEggDamage"/>) per hit or burst, so balance stays in RobotEnemy's damage model.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class AbilityProjectile : MonoBehaviour
{
    [System.Serializable]
    public struct Spec
    {
        public Sprite sprite;        // the projectile
        public Sprite burstSprite;   // shown on a burst (null = none)
        public float  size;          // world units across
        public float  mass;
        public float  bounciness;
        public float  gravityScale;
        public int    hits;          // impacts before it is spent (1 = burst on first contact)
        public float  blockDamage;   // per hit, or to every block in the burst radius
        public float  burstRadius;   // 0 = direct hit only
        public int    robotHits;     // egg-tier hits dealt to a robot per hit/burst
        public float  spin;          // degrees per second
        public float  life;          // seconds before it fades out regardless
    }

    const int EggLayer = 10;
    const int BlockMask = 1 << 8, RobotMask = 1 << 9;

    Spec _spec;
    Rigidbody2D _rb;
    int _hitsLeft;
    float _age;
    bool _spent;

    public static AbilityProjectile Spawn(Spec spec, Vector3 position, Vector2 velocity, Collider2D thrower = null, int sortingOrder = 6)
    {
        var go = new GameObject("AbilityProjectile");
        go.transform.position = position;
        go.layer = EggLayer;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = spec.sprite;
        sr.sortingOrder = sortingOrder;
        float native = spec.sprite != null ? Mathf.Max(spec.sprite.bounds.size.x, spec.sprite.bounds.size.y) : 1f;
        go.transform.localScale = Vector3.one * (spec.size / Mathf.Max(0.0001f, native));
        if (spec.sprite == null) sr.color = new Color(0.55f, 0.35f, 0.2f);

        var rb = go.AddComponent<Rigidbody2D>();
        rb.mass = Mathf.Max(0.1f, spec.mass);
        rb.gravityScale = spec.gravityScale;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb.linearVelocity = velocity;
        rb.angularVelocity = -spec.spin * Mathf.Sign(velocity.x == 0 ? 1 : velocity.x);

        var col = go.AddComponent<CircleCollider2D>();
        col.radius = 0.42f * native;   // local units: ~84% of the art, it is round-ish
        col.sharedMaterial = new PhysicsMaterial2D("AbilityProjectile") { bounciness = spec.bounciness, friction = 0.4f };

        if (thrower != null) Physics2D.IgnoreCollision(col, thrower, true);

        var p = go.AddComponent<AbilityProjectile>();
        p._spec = spec;
        p._rb = rb;
        p._hitsLeft = Mathf.Max(1, spec.hits);
        return p;
    }

    void Update()
    {
        _age += Time.deltaTime;
        if (!_spent && (_age > _spec.life || transform.position.y < -20f)) { _spent = true; Destroy(gameObject); }
    }

    void OnCollisionEnter2D(Collision2D c)
    {
        if (_spent) return;
        if (_spec.burstRadius > 0f) { Burst(); return; }

        // Physical projectile (wool ball): damage what it hits, bounce on.
        if (c.gameObject.TryGetComponent<BlockBase>(out var block)) block.TakeDamage(_spec.blockDamage);
        else if (c.gameObject.TryGetComponent<RobotEnemy>(out var robot)) HitRobot(robot);
        else return;                                   // the ground doesn't use up a hit
        if (--_hitsLeft <= 0) { _spent = true; Destroy(gameObject, 0.4f); }
    }

    void HitRobot(RobotEnemy robot)
    {
        for (int i = 0; i < Mathf.Max(1, _spec.robotHits); i++) robot.TakeEggDamage();
    }

    void Burst()
    {
        _spent = true;
        Vector2 at = transform.position;
        foreach (var hit in Physics2D.OverlapCircleAll(at, _spec.burstRadius, BlockMask))
            if (hit.TryGetComponent<BlockBase>(out var block)) block.TakeDamage(_spec.blockDamage);
        var hitRobots = new System.Collections.Generic.HashSet<RobotEnemy>();
        foreach (var hit in Physics2D.OverlapCircleAll(at, _spec.burstRadius, RobotMask))
        {
            var robot = hit.GetComponentInParent<RobotEnemy>();
            if (robot != null && hitRobots.Add(robot)) HitRobot(robot);
        }
        CameraShake.Shake(0.12f, 0.12f);
        if (_spec.burstSprite != null)
        {
            var fx = new GameObject("AbilityBurst");
            fx.transform.position = at;
            var sr = fx.AddComponent<SpriteRenderer>();
            sr.sprite = _spec.burstSprite;
            sr.sortingOrder = 10;
            float native = Mathf.Max(_spec.burstSprite.bounds.size.x, _spec.burstSprite.bounds.size.y);
            fx.transform.localScale = Vector3.one * (_spec.burstRadius * 2f / Mathf.Max(0.0001f, native));
            fx.AddComponent<FragmentFader>();
        }
        Destroy(gameObject);
    }
}
