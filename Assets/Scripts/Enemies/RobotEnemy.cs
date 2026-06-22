using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(BoxCollider2D))]
public class RobotEnemy : MonoBehaviour
{
    [SerializeField] private float _maxHealth               = 35f;
    [SerializeField] private float _impulseDamageMultiplier = 2.5f;
    [SerializeField] private float _minDamageImpulse        = 2f;

    public float Health      { get; private set; }
    public bool  IsDestroyed { get; private set; }

    private Rigidbody2D _rb;
    private LevelLoader _loader;

    public void Initialise(LevelLoader loader)
    {
        _loader = loader;
        _rb     = GetComponent<Rigidbody2D>();
        Health  = _maxHealth;
    }

    public void TakeDamage(float amount)
    {
        if (IsDestroyed) return;
        Health = Mathf.Max(0f, Health - amount);
        if (Health <= 0f) Die();
    }

    void OnCollisionEnter2D(Collision2D col)
    {
        if (IsDestroyed) return;
        if (col.rigidbody == null) return;

        float effMass = col.rigidbody.bodyType == RigidbodyType2D.Static
            ? _rb.mass * 0.6f
            : (_rb.mass * col.rigidbody.mass) / (_rb.mass + col.rigidbody.mass);

        float impulse = col.relativeVelocity.magnitude * effMass;
        if (impulse >= _minDamageImpulse)
            TakeDamage(impulse * _impulseDamageMultiplier);
    }

    void Die()
    {
        IsDestroyed = true;
        _loader?.NotifyRobotDestroyed(this);
        Destroy(gameObject);
    }
}