using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(CircleCollider2D))]
public class EggProjectile : MonoBehaviour
{
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
        col.radius = 0.15f;
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (_hit) return;
        _hit = true;

        if (collision.gameObject.TryGetComponent<BlockBase>(out var block))
            block.TakeDamage(_damage);
        else if (collision.gameObject.TryGetComponent<RobotEnemy>(out var robot))
            robot.TakeDamage(_damage);

        Destroy(gameObject);
    }
}
