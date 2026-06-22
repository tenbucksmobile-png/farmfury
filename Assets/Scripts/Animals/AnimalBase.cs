using System;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(CircleCollider2D))]
public abstract class AnimalBase : MonoBehaviour
{
    [Header("Physics")]
    [SerializeField] protected float mass       = 8f;
    [SerializeField] protected float bounciness = 0.4f;
    [SerializeField] protected float linearDrag = 0.008f;

    [Header("Flight")]
    [SerializeField] private float _contactTimeout = 3f;

    public bool IsInFlight  { get; private set; }
    public bool IsLaunched  { get; private set; }
    public bool IsDestroyed { get; private set; }

    public event Action<AnimalBase> OnAnimalDestroyed;

    protected Rigidbody2D      _rb;
    protected CircleCollider2D _col;
    protected SpriteRenderer   _sr;

    private bool  _abilityUsed;
    private bool  _contactStarted;
    private float _contactTimer;

    protected virtual void Awake()
    {
        _rb  = GetComponent<Rigidbody2D>();
        _col = GetComponent<CircleCollider2D>();
        _sr  = GetComponent<SpriteRenderer>();

        _rb.mass           = mass;
        _rb.linearDamping  = linearDrag;
        _rb.gravityScale   = 1f;
        _rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        var mat = new PhysicsMaterial2D("AnimalMat")
        {
            bounciness = this.bounciness,
            friction   = 0.3f,
        };
        _col.sharedMaterial = mat;
        _rb.bodyType = RigidbodyType2D.Kinematic;
    }

    protected virtual void Update()
    {
        if (!IsLaunched || IsDestroyed) return;

        if (!_abilityUsed && Input.GetMouseButtonDown(0))
        {
            _abilityUsed = true;
            TriggerAbility();
        }

        if (_contactStarted)
        {
            _contactTimer -= Time.deltaTime;
            if (_contactTimer <= 0f) DestroyAnimal();
        }
    }

    public void Launch(Vector2 velocity)
    {
        _rb.bodyType       = RigidbodyType2D.Dynamic;
        _rb.linearVelocity = velocity;
        IsLaunched         = true;
        IsInFlight         = true;
    }

    protected abstract void TriggerAbility();

    protected virtual void OnCollisionEnter2D(Collision2D col)
    {
        IsInFlight = false;
        if (!_contactStarted)
        {
            _contactStarted = true;
            _contactTimer   = _contactTimeout;
        }
    }

    protected virtual void DestroyAnimal()
    {
        if (IsDestroyed) return;
        IsDestroyed = true;
        OnAnimalDestroyed?.Invoke(this);
        Destroy(gameObject);
    }
}