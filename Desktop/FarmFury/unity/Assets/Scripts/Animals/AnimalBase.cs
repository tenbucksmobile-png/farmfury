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

    [Header("Pose Sprites")]
    [SerializeField] private Sprite _sprIdle;
    [SerializeField] private Sprite _sprLoaded;
    [SerializeField] private Sprite _sprInFlight;
    [SerializeField] private Sprite _sprImpact;
    [SerializeField] private Sprite _sprAbility;

    [Header("Flight")]
    [SerializeField] private float _contactTimeout = 1f;

    public bool IsInFlight  { get; protected set; }
    public bool IsLaunched  { get; private set; }
    public bool IsDestroyed { get; protected set; }

    public event Action<AnimalBase> OnAnimalDestroyed;
    // Fires on the first REAL collision (not CluckAnimal's pass-through punches, which
    // return before calling base.OnCollisionEnter2D) — used to stop/fade the falling SFX.
    public event Action<AnimalBase> OnAnimalImpact;

    protected Rigidbody2D      _rb;
    protected CircleCollider2D _col;
    protected SpriteRenderer   _sr;

    protected bool HasRealSprites => _sprIdle != null;

    public Sprite IdleSprite => _sprIdle;

    protected bool _abilityUsed;
    private bool  _contactStarted;
    private float _contactTimer;

    protected virtual void Awake()
    {
        _rb  = GetComponent<Rigidbody2D>();
        _col = GetComponent<CircleCollider2D>();
        _sr  = GetComponent<SpriteRenderer>();
        if (_sr == null) _sr = gameObject.AddComponent<SpriteRenderer>();
        _sr.sortingOrder = 6;   // fixed 2026-07-08: was 5 — CannonSmoke needed to sit between
                                 // FarmCannon (4) and animals, but sortingOrder is integer-only,
                                 // so animals moved to 6 and smoke took 5 rather than leaving no
                                 // slot between the cannon and the bird.

        _rb.mass                   = mass;
        _rb.linearDamping          = linearDrag;
        _rb.gravityScale           = 1f;
        _rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        var mat = new PhysicsMaterial2D("AnimalMat") { bounciness = bounciness, friction = 0.3f };
        _col.sharedMaterial = mat;
        _rb.bodyType = RigidbodyType2D.Kinematic;

        if (_sprIdle != null) { _sr.sprite = _sprIdle; _sr.color = Color.white; }
        else                    _sr.sprite = MakeCircleSprite(32);
    }

    protected virtual void Update()
    {
        if (!IsLaunched || IsDestroyed) return;

        if (!_abilityUsed && Pointer.current != null && Pointer.current.press.wasPressedThisFrame)
        {
            _abilityUsed = true;
            TriggerAbility();
            if (_sprAbility != null) _sr.sprite = _sprAbility;
        }

        if (_contactStarted)
        {
            _contactTimer -= Time.deltaTime;
            if (_contactTimer <= 0f) DestroyAnimal();
        }
    }

    // Call after placing the animal in the sling cup — shows the seated pose.
    public void SetLoadedPose()
    {
        if (_sprLoaded  != null) _sr.sprite = _sprLoaded;
        else if (_sprIdle != null) _sr.sprite = _sprIdle;
    }

    // Shows the in-flight pose without actually launching (physics/IsLaunched/IsInFlight
    // untouched) — used for the bird sitting loaded at the cannon barrel mouth, where the
    // dynamic in-flight pose (wings out) reads better than the seated "loaded" pose.
    public void SetInFlightPose()
    {
        if (_sprInFlight != null) _sr.sprite = _sprInFlight;
        else if (_sprLoaded != null) _sr.sprite = _sprLoaded;
        else if (_sprIdle != null) _sr.sprite = _sprIdle;
    }

    public void Launch(Vector2 velocity)
    {
        _rb.bodyType       = RigidbodyType2D.Dynamic;
        // 2026-07-14: dropped from 0.4 to 0.18 (~55% weaker fall) per request to slow the
        // flight considerably and loop higher — paired with CatapultLauncher's higher
        // angle/lower speed and DrawTrajectory()'s matching grav constant.
        _rb.gravityScale   = 0.18f;
        _rb.linearVelocity = velocity;
        IsLaunched         = true;
        IsInFlight         = true;
        if (_sprInFlight != null) _sr.sprite = _sprInFlight;
    }

    protected abstract void TriggerAbility();

    protected virtual void OnCollisionEnter2D(Collision2D col)
    {
        IsInFlight = false;
        if (_sprImpact != null) _sr.sprite = _sprImpact;
        _sr.enabled = false;   // hide immediately — no dead bird lying on the ground
        OnAnimalImpact?.Invoke(this);
        if (!_contactStarted)
        {
            _contactStarted = true;
            _contactTimer   = _contactTimeout;
        }
    }

    protected void FireOnAnimalDestroyed() => OnAnimalDestroyed?.Invoke(this);

    protected virtual void DestroyAnimal()
    {
        if (IsDestroyed) return;
        IsDestroyed = true;
        FireOnAnimalDestroyed();
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
