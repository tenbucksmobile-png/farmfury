using System;
using System.Collections;
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

    // Shared "impact stars" VFX burst (ImpactStars1.png) shown on every real collision hit
    // (not CluckAnimal's pass-through punches, which skip base.OnCollisionEnter2D entirely) —
    // wired identically on all 8 animal prefabs via SpriteWiring.WireAll(), since it's a
    // generic "just got hit" reaction, not per-character art. User-requested 2026-07-09:
    // "appears when cluck or any further animal is damaged."
    [SerializeField] private Sprite _sprImpactStars;

    [Header("Flight")]
    // Lowered from 1f (2026-07-09, user-reported "should disappear immediately" — 1s read as
    // far too long once the animal is frozen in place on impact rather than rolling, see
    // OnCollisionEnter2D). Still non-zero so the impact pose/stars get one visible beat instead
    // of the pre-2026-07-26 instant-hide bug. NOTE the [SerializeField] stale-value trap applies
    // here — SpriteWiring.WireAll() re-syncs this on all 8 prefabs since they already had 1f
    // serialized from before this change.
    [SerializeField] private float _contactTimeout = 0.25f;

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

    // 2026-07-10, user report: "check why Bessie is pink?" Root cause: this only checked
    // _sprIdle, but Bessie's character folder has Loaded/InFlight/Impact/Trigger art and no
    // Bessie_Idle.png specifically — every subclass's Awake() uses !HasRealSprites to decide
    // whether to apply a solid placeholder tint (Bessie's was pink), and that tint, once set,
    // is never reset when a later REAL sprite (Loaded/InFlight/etc.) is assigned — so the single
    // missing Idle art poisoned the tint for her entire lifetime even though every other pose she
    // actually shows in-game is real art. Broadened to check every pose so "has real sprites" no
    // longer hinges on one specific (and, for a mid-flight animal, rarely-seen) slot.
    protected bool HasRealSprites =>
        _sprIdle != null || _sprLoaded != null || _sprInFlight != null ||
        _sprImpact != null || _sprAbility != null;

    public Sprite IdleSprite => _sprIdle;

    protected bool _abilityUsed;
    private bool  _contactStarted;
    private float _contactTimer;

    protected virtual void Awake()
    {
        // Real root cause of "eggs don't appear" (2026-07-10, second report, after the sorting-
        // order fix didn't resolve it): this class never actually set gameObject.layer, so every
        // animal instance sat on whatever layer its prefab happened to default to (Default, 0) —
        // NOT the "Animal" layer (7) the project's own layer convention assumes. That meant
        // GameManager.Awake()'s Physics2D.IgnoreLayerCollision(Animal, Egg, true) call (added for
        // the earlier "Cluck disappears on tap" bug) silently did nothing, since Cluck was never
        // actually ON the Animal layer to begin with — eggs (layer 10, spawning AT Cluck's own
        // position) kept colliding with Cluck immediately, and EggProjectile.OnCollisionEnter2D
        // unconditionally Destroy()s itself on ANY collision, including a harmless hit against
        // its own launching bird — so every egg was self-destructing before its first visible
        // frame, on every level, not just L04. Explicitly assigning the layer here fixes it for
        // every AnimalBase subclass at once.
        gameObject.layer = LayerMask.NameToLayer("Animal");

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

    // 0-based level index where animal abilities are first introduced (L04) — matches
    // MatchUpScreen.AbilityIntroLevelIndex. Added 2026-07-10, user report: "when the player taps
    // on screen cluck disappears like he's hit something, this should not happen in the first
    // levels... from Level 4 when the mouse is clicked mid flight, cluck must change to trigger
    // and shoot the eggs." Before this level, a tap mid-flight does nothing — after it, the
    // existing TriggerAbility()/_sprAbility swap below fires as normal.
    const int AbilityIntroLevelIndex = 3;

    protected virtual void Update()
    {
        if (!IsLaunched || IsDestroyed) return;

        bool abilityUnlocked = GameManager.Instance == null
            || GameManager.Instance.CurrentLevelIndex >= AbilityIntroLevelIndex;

        if (abilityUnlocked && !_abilityUsed && Pointer.current != null && Pointer.current.press.wasPressedThisFrame)
        {
            _abilityUsed = true;
            TriggerAbility();
            if (_sprAbility != null)
            {
                _sr.sprite = _sprAbility;
                StartCoroutine(RevertAbilitySprite());
            }
        }

        if (_contactStarted)
        {
            _contactTimer -= Time.deltaTime;
            if (_contactTimer <= 0f) DestroyAnimal();
        }
    }

    // How long the ability-trigger pose (e.g. Cluck_Trigger1.png) stays up before reverting —
    // 2026-07-10, user report: "cluck should not stay in trigger but once eggs have been shot to
    // change back to inflight, if not hit, otherwise to damage." Previously _sprAbility was set
    // once and never reverted, so the bird stayed in its trigger pose for the rest of the flight
    // regardless of what happened next.
    const float AbilitySpriteHoldDuration = 0.3f;

    IEnumerator RevertAbilitySprite()
    {
        yield return new WaitForSeconds(AbilitySpriteHoldDuration);
        // Only revert to the normal flight pose if nothing else has claimed the sprite since —
        // a real impact's pose (set in OnCollisionEnter2D, gated by _contactStarted) takes
        // priority and must not be stomped back to InFlight.
        if (IsLaunched && !IsDestroyed && !_contactStarted && _sprInFlight != null)
            _sr.sprite = _sprInFlight;
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
        // Show the impact pose (e.g. Cluck_Impact.png) for the remaining _contactTimeout window
        // instead of hiding instantly — fixed 2026-07-26: _sprImpact used to get assigned then
        // hidden in the very next line, so the reaction art was never actually visible before
        // DestroyAnimal() removed the whole GameObject. Only fall back to the old instant-hide
        // when no impact art is wired at all (procedural fallback circle — "no dead bird lying
        // on the ground").
        if (_sprImpact != null) _sr.sprite = _sprImpact;
        else                    _sr.enabled = false;
        // Only on the actual first impact, not every subsequent roll/bounce contact — a rolling
        // or bouncing animal fires OnCollisionEnter2D repeatedly, and re-bursting the stars each
        // time read as spammy (user-reported 2026-07-09: "should not appear everytime cluck
        // rolls — perhaps only once upon impact").
        if (!_contactStarted) SpawnImpactStars();
        OnAnimalImpact?.Invoke(this);
        if (!_contactStarted)
        {
            // Freeze in place immediately instead of letting physics carry it rolling/bouncing
            // across the screen for the rest of the _contactTimeout window — user-reported
            // 2026-07-09: "the sprite should disappear immediately - currently it rolls off
            // screen then disappears." Kinematic so nothing can push it further either. Done
            // here (not by immediately calling DestroyAnimal()) because BessieAnimal.
            // OnCollisionEnter2D calls base.OnCollisionEnter2D() THEN starts its Shockwave()
            // coroutine on this same GameObject — destroying it synchronously here would risk
            // that coroutine never running.
            _rb.linearVelocity  = Vector2.zero;
            _rb.angularVelocity = 0f;
            _rb.bodyType        = RigidbodyType2D.Kinematic;

            _contactStarted = true;
            _contactTimer   = _contactTimeout;
        }
    }

    // Quick VFX burst at the impact point — reuses FragmentFader (defined in BlockBase.cs, same
    // assembly) for the fade-out, same pattern as BlockBase.SpawnImpactFlash()/SpawnExplosion().
    // Sized off this animal's own collider diameter (world-space, accounts for both the per-
    // character collider radius set in each subclass's Awake() and any parent scale) rather than
    // a fixed scale — user-reported 2026-07-09 the burst needs to read as sized to whichever
    // animal got dazed (Bessie's collider is ~1.4x Cluck's, see SpriteWiring's CharPPU comments),
    // not the same size regardless of which animal was hit.
    void SpawnImpactStars()
    {
        if (_sprImpactStars == null) return;
        var go = new GameObject("ImpactStars");
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite       = _sprImpactStars;
        sr.sortingOrder = 10;
        go.transform.position = transform.position;
        float diameter = _col.radius * 2f * transform.lossyScale.x;
        float sz = diameter * 2f; // bigger than the character so the burst reads clearly around it
        go.transform.localScale = new Vector3(sz, sz, 1f);
        go.AddComponent<FragmentFader>();
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
