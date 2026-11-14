using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(CircleCollider2D))]
public abstract class AnimalBase : MonoBehaviour
{
    [Header("Physics")]
    // Share of an animal's design mass its body carries into a hit (2026-11-11, user: impacts "too high and shoot the
    // structure apart"). Cluck 8 -> 3.2, Bessie 28 -> 11.2: about a block's weight, enough to tip a well-struck piece,
    // not to blast a tower apart. Flight is unaffected (gravity doesn't depend on mass). Ability projectiles share it.
    // 0.4 -> 0.6 (2026-11-14, user on device: "the character hardly makes a dent ... almost feels like the character has
    // no impact at all on fallen structure"): Cluck 4.8, Bessie 16.8. See also BlockBase.CrushRobotsUnderWreckage.
    public const float ImpactMassScale = 0.6f;
    /// <summary>The level's flight pace (ReferenceFraming.FlightPace on framed levels, else 1), set by
    /// CatapultLauncher.ApplyFlightTuning. Slower shots carry 1/Pace the mass so their momentum - and their hits - stay
    /// the same; abilities fly and fall at the same pace.</summary>
    public static float Pace = 1f;
    /// <summary>Raised when a flying animal's ability is triggered (TutorialHints).</summary>
    public static event System.Action AbilityUsed;
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
    // of the pre-2026-07-26 instant-hide bug. Raised 0.25 -> 0.45 (2026-07-13, user report: "the
    // sprite disappears before anything is hit... delay the disappearance so the impact and
    // damage is seen") — 0.25s wasn't enough time to actually register an impact pose plus, for
    // Bessie specifically, the Ground Slam shockwave/earthquake landing before she vanished; 0.45s
    // gives a clearly longer hold without reintroducing the original "rolls off screen" complaint
    // this value was first tuned against. Applies to every animal on every level automatically —
    // this is the shared base class field, not per-level data. NOTE the [SerializeField]
    // stale-value trap applies here — SpriteWiring.WireAll() re-syncs this on all 8 prefabs since
    // they already had an older value serialized from before this change.
    [SerializeField] private float _contactTimeout = 0.45f;

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

    // Safety net added 2026-07-19, user report: "from level 8 the animal no longer fires (as if
    // the game is stuck)." Root cause: CatapultLauncher only ever clears its "cannon busy" state
    // (_activeAnimal) from OnAnimalDestroyed, which — before this fix — only ever fired from
    // OnCollisionEnter2D below, i.e. a genuine Collision2D. If a shot's arc ever clears every
    // collider in the level (a real possibility once levels got tall/dense — L08's redesigned
    // 3-column fortress plus pass-through haybales that intentionally skip
    // base.OnCollisionEnter2D, see CluckAnimal.OnCollisionEnter2D) and also somehow clears the
    // ground plane, the animal just keeps falling forever, never collides, never destroys itself,
    // and every subsequent tap silently no-ops in CatapultLauncher.HandleInput()'s
    // "_activeAnimal == null" gate — permanently stuck. This is a level-agnostic latent bug, not
    // something specific to L08's data; L08 just raised the odds of triggering it. Two independent
    // fallbacks, checked every frame while in flight: a hard flight-time cap, and a "fell way below
    // any plausible ground" check (Ground sits at Y=-6.60 per the project's coordinate system —
    // MaxFallY gives a wide safety margin below that for any current or future level).
    const float MaxFlightSeconds = 10f;
    const float MaxFallY      = -30f;
    private float _flightTimer;

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

        _rb.mass                   = mass * ImpactMassScale;
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
            AbilityUsed?.Invoke();   // TutorialHints (2026-11-12)
            AudioManager.PlayAbility(Kind);   // the franchise ability sound (2026-11-10)
            if (_sprAbility != null)
            {
                _sr.sprite = _sprAbility;
                StartCoroutine(RevertAbilitySprite());
            }
        }

        if (_contactStarted)
        {
            if (_fading) return;
            _contactTimer -= Time.deltaTime;
            bool still = _rb == null || _rb.linearVelocity.sqrMagnitude < RestSpeedSqr * 4f;
            _settledTimer = still ? _settledTimer + Time.deltaTime : 0f;
            Vector2 cp = transform.position;
            if (_contactTimer <= 0f || _settledTimer >= SettledSeconds || cp.y <= MaxFallY || Mathf.Abs(cp.x - _launchX) > MaxAbsX)
                StartCoroutine(FadeOutAndDestroy());
            return;
        }

        // See the _flightTimer field comment above — stuck-cannon safety net. Counted for the whole time since
        // launch, not only while IsInFlight (2026-11-10, user report: the next animal never loaded): Percy's Bounce
        // Roll clears IsInFlight on each bounce and, once rolling along the ground or a floe, never gets a new
        // collision-enter or exit — so the timer stopped, Percy rolled off-screen (friction 0) and the cannon waited
        // forever. Also ends a launched animal that has left the play area or has come to rest without a "real" hit.
        _flightTimer += Time.deltaTime;
        Vector2 p = transform.position;
        if (_flightTimer >= MaxFlightSeconds || p.y <= MaxFallY || Mathf.Abs(p.x - _launchX) > MaxAbsX)
        {
            DestroyAnimal();
            return;
        }
        if (_rb != null && _rb.bodyType == RigidbodyType2D.Dynamic && _rb.linearVelocity.sqrMagnitude < RestSpeedSqr)
        {
            _restTimer += Time.deltaTime;
            if (_restTimer >= RestSeconds) DestroyAnimal();
        }
        else _restTimer = 0f;
    }

    // Left the play area: measured from where the animal was fired (2026-11-12). It was |x| > 40 from the world origin,
    // and on a multi-room level's second room the cannon stands at x ~ 27, so a shot vanished mid-air at x = 40 before
    // reaching the structure (user: "the character sometimes disappears mid air").
    const float MaxAbsX = 40f;
    float _launchX;
    const float RestSpeedSqr = 0.15f * 0.15f, RestSeconds = 1.2f;
    float _restTimer;

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
        if (IsLaunched && !IsDestroyed && !_contactStarted && !KeepAbilityPose && _sprInFlight != null)
            _sr.sprite = _sprInFlight;
    }

    /// <summary>True while the ability pose should stay up for the rest of the flight (Percy's curled-up roll).</summary>
    protected virtual bool KeepAbilityPose => false;

    /// <summary>Shows the ability pose (if there is one).</summary>
    protected void ShowAbilityPose() { if (_sprAbility != null) _sr.sprite = _sprAbility; }

    /// <summary>After a hit: fade out within `seconds` instead of the usual settle / 3 s (Percy's single bounce).</summary>
    protected void EndSoon(float seconds) { if (_contactStarted) _contactTimer = Mathf.Min(_contactTimer, seconds); }

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

    /// <summary>Flight gravityScale for levels without ReferenceFraming (see Launch).</summary>
    public const float DefaultGravityScale = 0.18f;

    public void Launch(Vector2 velocity) => Launch(velocity, DefaultGravityScale);

    // gravityScale: CatapultLauncher passes ReferenceFraming.ProjectileGravityScale on framed levels.
    public void Launch(Vector2 velocity, float gravityScale)
    {
        _launchX = transform.position.x;
        _rb.mass = mass * ImpactMassScale / Mathf.Max(0.1f, Pace);   // slower, heavier: the same momentum into the hit
        _rb.bodyType       = RigidbodyType2D.Dynamic;
        // 2026-07-14: dropped from 0.4 to 0.18 (~55% weaker fall) per request to slow the
        // flight considerably and loop higher — paired with CatapultLauncher's higher
        // angle/lower speed and DrawTrajectory()'s matching grav constant.
        _rb.gravityScale   = gravityScale;
        _rb.linearVelocity = velocity;
        IsLaunched         = true;
        IsInFlight         = true;
        if (_sprInFlight != null) _sr.sprite = _sprInFlight;
    }

    protected abstract void TriggerAbility();

    // -- Ability aim (2026-11-11, user: "there ability should not be slower than the flight"; "ability should be angled
    // down and not ahead, otherwise it shoots over the structure") ----------------------------------------------------
    // Every thrown ability leaves forward (the way the animal is travelling) and DOWN: at least minDownDeg below the
    // horizon and always at least 12 deg steeper than the animal's own path, so it drops into the structure instead of
    // sailing over it; offsetDeg fans several projectiles around that line. Speed: 1.25x the animal's, never below min.
    // 1.25 -> 1.6 (2026-11-12, user: abilities "seem slow to appear"): at 1.25x the projectiles drifted out from behind
    // the animal at about a quarter of its speed, hidden by its sprite for ~0.3 s; now they burst clear at once.
    const float AbilitySpeedFactor = 1.6f, AbilitySteeperBy = 12f, AbilityMaxDownDeg = 80f;

    protected Vector2 AbilityAim(float minDownDeg, float offsetDeg = 0f)
    {
        Vector2 v = _rb != null ? _rb.linearVelocity : Vector2.right;
        float sign = v.x >= 0f ? 1f : -1f;
        float flight = Mathf.Atan2(v.y, Mathf.Abs(v.x) + 0.0001f) * Mathf.Rad2Deg;   // + up, - down
        float centre = Mathf.Clamp(Mathf.Min(flight - AbilitySteeperBy, -minDownDeg), -AbilityMaxDownDeg, -minDownDeg);
        float a = Mathf.Clamp(centre + offsetDeg, -88f, -5f) * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(a) * sign, Mathf.Sin(a));
    }

    protected float AbilitySpeed(float min) =>
        Mathf.Max(min * Pace, (_rb != null ? _rb.linearVelocity.magnitude : 0f) * AbilitySpeedFactor);

    /// <summary>Which animal this is (picks its ability sound).</summary>
    public AnimalType Kind => this switch
    {
        BessieAnimal => AnimalType.Bessie,
        PercyAnimal  => AnimalType.Percy,
        WoollyAnimal => AnimalType.Woolly,
        DuckyAnimal  => AnimalType.Ducky,
        HoraceAnimal => AnimalType.Horace,
        GeraldAnimal => AnimalType.Gerald,
        BillyAnimal  => AnimalType.Billy,
        _            => AnimalType.Cluck,
    };

    protected virtual void OnCollisionEnter2D(Collision2D col)
    {
        IsInFlight = false;
        // No burst, no freeze (2026-11-11, user: "the farm character should also not explode upon impact"): the animal
        // stays a physical body, shows its impact pose and keeps tumbling through the structure — its momentum is what
        // shoves the blocks over (BlockBase.ShoveOnHit) — then fades out once it settles (Update, AfterImpactSeconds cap).
        if (_contactStarted) return;
        _contactStarted = true;
        _contactTimer   = AfterImpactSeconds;
        if (_sprImpact != null) _sr.sprite = _sprImpact;
        OnAnimalImpact?.Invoke(this);
    }

    const float AfterImpactSeconds = 3f, SettledSeconds = 0.5f, FadeSeconds = 0.35f;
    float _settledTimer;
    bool _fading;

    // Once settled (or after AfterImpactSeconds) the animal shrinks a little and fades away, then the next one loads.
    System.Collections.IEnumerator FadeOutAndDestroy()
    {
        _fading = true;
        Vector3 s0 = transform.localScale;
        Color c0 = _sr != null ? _sr.color : Color.white;
        for (float t = 0f; t < FadeSeconds; t += Time.deltaTime)
        {
            float p = t / FadeSeconds;
            transform.localScale = s0 * Mathf.Lerp(1f, 0.75f, p);
            if (_sr != null) _sr.color = new Color(c0.r, c0.g, c0.b, c0.a * (1f - p));
            yield return null;
        }
        DestroyAnimal();
    }

    void SpawnImpactStars()
    {
        if (_sprImpactStars == null) return;
        var go = new GameObject("ImpactStars");
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite       = _sprImpactStars;
        sr.sortingOrder = 10;
        go.transform.position = transform.position;
        float diameter = _col.radius * 2f * transform.lossyScale.x;
        // 1.5x the animal, measured against the stars sprite's own size (2026-11-09, EffectSizing).
        float sz = EffectSizing.ScaleFor(_sprImpactStars, diameter * 1.5f);
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
