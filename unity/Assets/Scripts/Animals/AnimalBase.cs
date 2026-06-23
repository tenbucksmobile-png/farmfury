using System;
using UnityEngine;
using UnityEngine.InputSystem;

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
        if (_sr == null) _sr = gameObject.AddComponent<SpriteRenderer>();

        _rb.mass                   = mass;
        _rb.linearDamping          = linearDrag;
        _rb.gravityScale           = 1f;
        _rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        var mat = new PhysicsMaterial2D("AnimalMat") { bounciness = bounciness, friction = 0.3f };
        _col.sharedMaterial = mat;
        _rb.bodyType = RigidbodyType2D.Kinematic;

        if (_sr.sprite == null) _sr.sprite = MakeCircleSprite(32);
    }

    protected virtual void Update()
    {
        if (!IsLaunched || IsDestroyed) return;

        if (!_abilityUsed && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
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

    protected static Sprite MakeCircleSprite(int size)
    {
        var   tex = new Texture2D(size, size, TextureFormat.ARGB32, false);
        float r   = size * 0.5f;
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float dx = x - r + 0.5f, dy = y - r + 0.5f;
            tex.SetPixel(x, y, dx * dx + dy * dy <= r * r ? Color.white : Color.clear);
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), (float)size);
    }
}
