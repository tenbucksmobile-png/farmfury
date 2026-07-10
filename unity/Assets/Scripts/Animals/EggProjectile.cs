using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(CircleCollider2D))]
public class EggProjectile : MonoBehaviour
{
    // Flat damage vs blocks only — robot damage uses RobotEnemy.TakeEggDamage()'s own
    // fraction-of-max-HP factor instead (2026-07-10 fifth balance pass, damage-factor model v2:
    // "eggs... apply your determined damage factor" — see RobotEnemy.cs's class comment), so egg
    // damage scales consistently across every robot type rather than being a flat number that
    // happened to be ~half of some robots' HP and a third of others'.
    [SerializeField] private float _damage = 15f;

    private bool _hit;

    void Awake()
    {
        gameObject.layer = 10; // Egg layer

        var rb = GetComponent<Rigidbody2D>();
        rb.mass                    = 1f;
        rb.gravityScale            = 1f;
        rb.linearDamping           = 0f;
        rb.collisionDetectionMode  = CollisionDetectionMode2D.Continuous;

        var col = GetComponent<CircleCollider2D>();
        col.radius = 0.18f; // matches SceneSetup.EnsureEggPrefab()'s prefab value — keep in sync
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (_hit) return;
        _hit = true;

        if (collision.gameObject.TryGetComponent<BlockBase>(out var block))
            block.TakeDamage(_damage);
        else if (collision.gameObject.TryGetComponent<RobotEnemy>(out var robot))
            robot.TakeEggDamage();

        Destroy(gameObject);
    }
}
