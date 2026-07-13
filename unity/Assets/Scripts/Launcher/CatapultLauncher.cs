using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

// Farm Cannon mechanic: click the bird loaded in the cannon barrel, drag to aim, release to
// fire. Direction and power are two independent axes ("Angry Birds" style, added 2026-07-26):
// drag ANGLE (relative to the pivot) sets the launch angle, drag DISTANCE sets the power. This
// replaced the original trebuchet-arm aiming math, which was carried over unchanged through the
// 2026-07-02 cannon visual swap — that scheme clamped the drag angle into a narrow arc and only
// ever used it to derive a single 0-1 "how far pulled" value driving both speed and angle
// together, so the actual drag direction was thrown away and every shot looked nearly identical
// regardless of where the player dragged (user-reported bug, see docs/HISTORY.md).
// Place on a GameObject at world position (11.2, 0, 0).
public class CatapultLauncher : MonoBehaviour
{
    [Header("Scene References")]
    [SerializeField] private LevelLoader _levelLoader;
    [SerializeField] private Camera      _camera;

    [Header("Launch Physics")]
    // Pull DISTANCE from the pivot drives power (0 at rest, full power at/past this distance).
    // Was declared but never actually read before 2026-07-26 — the trebuchet-arm scheme derived
    // power from the clamped drag ANGLE instead. Revived here as the real power axis.
    [SerializeField] private float _maxDragDistance = 2.4f;   // 120 px / 50
    // Raised 3.0-6.0 -> 3.5-8.5 (2026-07-11, user report: "with structures beginning to stack it
    // is impossible to have the animal fly over... allow for high and strong firing"). At the old
    // 6.0 max, the highest-arcing shot the aim cone allowed (55 deg) could only just barely reach
    // this level set's far edge (~X 9.9 from the launch point) at roughly LAUNCH HEIGHT — solved
    // from the standard projectile-height formula y(x) = x*tanθ - g*x^2/(2v^2*cos^2θ) with
    // g=3.6 (Physics2D.gravity.y * AnimalBase's 0.18 gravityScale), it landed at y≈0.1 above the
    // barrel, i.e. it could barely REACH that far, let alone clear a robot/block stack sitting
    // several units above ground there. Re-solved for v at the same angle/distance targeting a
    // generous ~4.5-unit clearance above launch height gives v≈7.5 — 8.5 keeps a real margin on
    // top of that (verified: at 55 deg it clears ~6.7 units at that same distance).
    //
    // Raised again 8.5 -> 9.3 (2026-07-13, same day as the FarmCannon reposition — user report:
    // "did we also adjust the trajectory and power of the cannon to adjust for the distance").
    // Moving the visual FarmCannon ~4.5 units further left (X-3.0 -> X-7.54, see the Coordinate
    // System docs in CLAUDE.md) increased every level's actual launch-to-target distance by the
    // same amount, since block/robot X coordinates in LevelData are untouched absolute positions.
    // Re-checked the same projectile-height formula against every block/robot in every one of the
    // 18 built levels (script, not eyeballed) — the worst case was L18's own Commander boss, whose
    // required clearance at the new distance (dist=13.2, reqH=6.70 above launch height) exceeded
    // what 8.5 m/s could reach at ANY angle by a full unit (margin -1.00, down from +1.38 under
    // the old cannon position) — the World 1 boss fight was no longer winnable at max power.
    // Several other levels (L17, L13, L16, L08/L09, L07, L11) had their margins roughly halved
    // too. 9.3 restores a same-or-better margin than the original 2026-07-11 tuning at every one
    // of them (L18's boss: +1.69). Min speed raised proportionally alongside it (3.5 -> 3.8, same
    // ratio as before) so the softest pull still meaningfully reaches nearby targets.
    [SerializeField] private float _minLaunchSpeed  = 3.8f;
    [SerializeField] private float _maxLaunchSpeed  = 9.3f;

    [Header("Aim Geometry")]
    // Abstract aiming-math anchor. NOT tied to any visual GameObject — drag angle is measured
    // relative to this point.
    private const float _pivotHeight  = 1.914f;

    // Pull-angle cone (world-space, standard atan2 convention: 0°=+X, 90°=+Y). The player drags
    // the loaded bird backward/away from the target, mirroring it through the pivot (-180°) to
    // get the actual launch angle — same convention the old trebuchet-arm code used, just no
    // longer discarded. 200°-265° pull mirrors to a 20°-85° launch angle: pulling toward
    // horizontal-left (200°) gives a flat 20° shot, pulling toward near-straight-down (265°)
    // gives an 85° near-vertical lob. Widened from the trebuchet's old 218°-268° (which only ever
    // mirrored to a barely-varying 38°-88°, and wasn't even used that way — see class comment) so
    // direction changes are actually visible on release, per explicit user request. Upper bound
    // raised again 80° -> 85° (2026-07-11, alongside the speed increase above, same "allow for
    // high and strong firing" request) — a near-vertical lob is the only way to clear a tall
    // structure sitting close to the cannon, where there isn't enough horizontal room for a
    // shallower high-arcing shot to gain height before reaching it.
    private const float _armRestAngle = 200f;  // drag-angle lower bound
    private const float MaxLoadAngle  = 65f;   // drag-angle range above _armRestAngle

    // Deadzone radius (world units) around the pivot within which the drag angle is not
    // updated — see HandleInput()'s "Hold" branch. ~11x the pivot-to-loaded-bird distance
    // (~0.055u), chosen to comfortably absorb touchscreen contact-point jitter while still
    // being well inside a normal drag gesture's travel distance. Only gates the ANGLE; pull
    // distance (power) tracks the pointer continuously from the first frame of the drag.
    private const float MinAimRadius = 0.6f;

    [Header("Camera")]
    [SerializeField] private float   _returnDelay          = 2.5f;   // seconds after landing before pan-back starts
    [SerializeField] private float   _cameraFollowSpeed    = 6f;     // exponential follow rate (units/s)
    [SerializeField] private float   _cameraReturnDuration = 1.2f;   // seconds for the pan-back animation
    [SerializeField] private Vector2 _cameraRestOffset     = new Vector2(5.5f, 2.5f);

    // Per-level auto-zoom (2026-07-11, user report: "as the scenes progress it appears as if the
    // cannon is very close to the robot structures, and the structures are only going to grow all
    // the way to level 18... zoom out the camera"). Rather than hand-tuning a fixed zoom value per
    // level (something to remember on every future level build, easy to forget), orthoSize is
    // recomputed each level load from that level's actual block/robot bounding box — see
    // ComputeOrthoSizeForLevel(). _minOrthoSize floors it at the original fixed framing so smaller/
    // earlier levels (L01 in particular, whose camera framing is explicit user-verified ground
    // truth — see CLAUDE.md) render exactly as before; _maxOrthoSize caps how far it can zoom out
    // so a very sprawling future level doesn't shrink everything to illegibility.
    [SerializeField] private float _minOrthoSize = 4.5f;
    [SerializeField] private float _maxOrthoSize = 8.0f;
    private const float ZoomPadding = 1.2f; // extra world-units of breathing room around content bounds

    [Header("Farm Cannon")]
    [SerializeField] private GameObject _cannonGO;      // wired to "FarmCannon" scene GO
    [SerializeField] private Sprite     _cannonSprite;   // single "Cannon" sprite — never swapped

    // Cannon-relative offsets (world-space, added to the cannon's REST position — recoil never
    // affects the launch point or the loaded-bird position, only the cannon's own sprite).
    private static readonly Vector2 CannonBarrelOffset     = new Vector2(1.1f, 0.4f);  // barrel tip = LaunchPoint / trajectory-arc origin
    // User-verified 2026-07-11, replacing the original (0.9, 0.3) guess: now that
    // Cluck_InFlight renders as the actual whole sprite (not the pre-fix 53x8px sliver
    // fragment), the visually-correct offset for where it sits in the barrel is different.
    private static readonly Vector2 CannonLoadedBirdOffset = new Vector2(0.6212f, 0.4223f);

    private const float RecoilDistance       = 0.3f;   // rest X -> rest X − 0.3 (e.g. -4.5 -> -4.8)
    private const float RecoilOutDuration    = 0.08f;
    private const float RecoilReturnDuration = 0.4f;
    private const float CannonResetDelay     = 1.80f;  // total time from fire until "ready for next bird"

    // Recalibrated 2026-07-13: the previous (0.274099, 0.251007) was tuned before the
    // Assets/Sprites/Characters/Cluck_Chicken -> Cluck folder rename (round 9), so it was
    // very likely eyeballed against the still-unwired "yellow dot" fallback circle, not the
    // real Cluck_InFlight sprite — that would explain why the bird stayed invisible after
    // this value was set. Re-derived directly from measured pixel data instead of a visual
    // guess: Cluck_InFlight.png is a 500x500 canvas (PPU=2057) whose actual non-transparent
    // content is a 444x301px region (measured via PIL bbox), i.e. 0.2159 x 0.1463 world
    // units unscaled. SpriteWiring.cs's own PPU comment states the design intent as
    // "visual diameter ≈ physics collider diameter" (collider diameter = 0.36*2 = 0.72u).
    // Scale solved so the sprite's real content height hits that target: 0.72 / 0.1463 ≈
    // 4.9204 (uniform — InFlight is the only pose ever shown on this GameObject, so no
    // cross-pose aspect compromise is needed). Final on-screen size ≈ 1.06 x 0.72 units —
    // comparable to a haybale block, clearly visible against the 9-unit-tall camera viewport.
    private static readonly Vector3 BirdScale = new Vector3(4.9204f, 4.9204f, 1f);

    [Header("Trajectory")]
    [SerializeField] private int   _trajectoryDots      = 20;
    [SerializeField] private int   _trajectorySubsteps  = 3;
    [SerializeField] private float _trajectoryDotRadius = 0.08f;

    // Not serialized — value must come from code so Unity can't freeze a stale Inspector value
    private const float BirdClickRadius = 1.2f;

    // Runtime state
    private float      _dragAngle;      // pull angle while dragging — sets launch direction
    private float      _pullDistance;   // pull distance from the pivot, clamped — sets launch power
    private bool       _isDragging;
    private Vector3    _pocketPos;      // unused — kept to avoid serialisation churn
    private Vector3    _launchPoint;    // cannon barrel tip — the physical fire origin
    private Vector3    _cannonRestPos;  // cached FarmCannon rest position; recoil animates around this
    private AnimalBase _activeAnimal;
    private AnimalBase _readyBird;
    private bool       _cameraFollowing;
    private bool       _returnPending;    // true once ReturnCamera() has been started for this shot
    private Coroutine  _returnRoutine;
    private Coroutine  _fireRoutine;

    // Renderers
    private LineRenderer     _rubberBandLine;
    private SpriteRenderer[] _trajDotRenderers;
    private static Sprite    _dotSprite;
    private SpriteRenderer   _cannonSR;
    private ParticleSystem   _smokePS;

    // Type of the most recently fired bird — read by LevelCompleteManager to pick which
    // animal's celebration video plays over the Level Complete freeze-frame. Static rather
    // than an instance lookup since there's exactly one cannon per scene and this outlives
    // any single AnimalBase instance (which gets destroyed on landing).
    public static AnimalType LastAnimalUsed { get; private set; } = AnimalType.Cluck;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Awake()
    {
        if (_camera == null) _camera = Camera.main;
        if (_levelLoader == null) _levelLoader = FindAnyObjectByType<LevelLoader>();
        if (GetComponent<CameraShake>()   == null) gameObject.AddComponent<CameraShake>();
        // Checked against the static singleton, not GetComponent(this GO) — AudioManager now
        // normally lives on its own dedicated scene GO (wired with external clips via
        // SceneSetup.EnsureAudioManager()); AudioManager's [DefaultExecutionOrder(-90)]
        // guarantees that instance claims Instance first, so this is a null-safety fallback
        // only (e.g. a scene that hasn't run Wire Scene References yet), never a duplicate.
        if (AudioManager.Instance == null) gameObject.AddComponent<AudioManager>();

        // Same null-safety fallback as AudioManager above — LevelCompleteManager/LevelFailedManager
        // normally live on their own dedicated scene GOs, but a scene that hasn't been re-wired
        // since these systems were added still needs the celebration/taunt sequences to run.
        if (FindAnyObjectByType<LevelCompleteManager>() == null)
            new GameObject("LevelCompleteManager").AddComponent<LevelCompleteManager>();
        if (FindAnyObjectByType<LevelFailedManager>() == null)
            new GameObject("LevelFailedManager").AddComponent<LevelFailedManager>();
        if (PlayerStatsTracker.Instance == null)
            new GameObject("PlayerStatsTracker").AddComponent<PlayerStatsTracker>();

        // Ensure 2D orthographic view regardless of scene camera settings — orthoSize starts at
        // the floor value here; OnLevelStarted() recomputes it per-level once a level actually
        // loads (see ComputeOrthoSizeForLevel).
        if (_camera != null)
        {
            _camera.orthographic     = true;
            _camera.orthographicSize = _minOrthoSize;
        }

        _rubberBandLine   = MakeLine("RubberBandRenderer", 0.05f, new Color(0.9f, 0.7f, 0.1f, 0.9f));
        _trajDotRenderers = CreateDotPool(_trajectoryDots);

        BuildCannon();
        BuildSmokeParticles();
    }

    void OnEnable()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.OnLevelStarted += OnLevelStarted;
    }

    void OnDisable()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.OnLevelStarted -= OnLevelStarted;
    }

    void Start()
    {
        EnsureGroundExists();
        RefreshRestPoint();
        SnapCameraToRest();

        // Defer one frame so all Start() methods (including MainMenuController) have run.
        StartCoroutine(DelayedAutoStart());
    }

    IEnumerator DelayedAutoStart()
    {
        yield return null;
        // Skip auto-start when the main menu is visible — player navigates via UI.
        if (MainMenuController.Instance != null && MainMenuController.Instance.IsVisible)
            yield break;
        if (GameManager.Instance != null && GameManager.Instance.State == GameState.Idle)
            GameManager.Instance.ForceStartLevel(0);
    }

    // Creates a ground plane at runtime if none exists or if the existing one is stale.
    // Ground surface level = Launcher's Y (Launcher sits at ground level).
    void EnsureGroundExists()
    {
        float groundY = transform.position.y;
        var existing = GameObject.Find("Ground");
        if (existing != null)
        {
            var check = existing.GetComponent<BoxCollider2D>();
            if (check != null && Mathf.Abs(check.bounds.max.y - groundY) < 0.5f && check.bounds.size.x > 5f)
                return;
            Object.Destroy(existing);
        }

        var go = new GameObject("Ground");
        go.tag   = "Ground";
        go.layer = 6;
        go.transform.position   = new Vector3(0f, groundY - 0.25f, 0f);
        go.transform.localScale = new Vector3(60f, 0.5f, 1f);

        // 1×1 local col × (60,0.5) scale = 60×0.5 world, top edge at Y=-2.5
        var col  = go.AddComponent<BoxCollider2D>();
        col.size = new Vector2(1f, 1f);

        var tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        var sr        = go.AddComponent<SpriteRenderer>();
        sr.sprite     = Sprite.Create(tex, new Rect(0, 0, 1, 1), Vector2.one * 0.5f, 1f);
        sr.color      = new Color(0.25f, 0.65f, 0.15f);
        sr.sortingOrder = 0;

        var rb      = go.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Static;
    }

    void Update()
    {
        HandleInput();

        if (_cameraFollowing && _activeAnimal != null && !_activeAnimal.IsDestroyed)
        {
            if (_activeAnimal.IsInFlight)
            {
                SmoothFollowAnimal();
            }
            else if (!_returnPending)
            {
                // Bird has landed — begin the pan-back countdown
                _returnPending = true;
                if (_returnRoutine != null) StopCoroutine(_returnRoutine);
                _returnRoutine = StartCoroutine(ReturnCamera());
            }
        }
    }

    // ── Input ─────────────────────────────────────────────────────────────────

    void HandleInput()
    {
        // Pointer.current covers both Mouse (Editor/desktop) and Touchscreen (mobile).
        var ptr = Pointer.current;
        if (ptr == null) return;

        bool canFire = _activeAnimal == null
                    && _readyBird   != null
                    && _levelLoader != null
                    && _levelLoader.HasBirdsRemaining
                    && GameManager.Instance?.State == GameState.Playing;

        // ── Press: must click/tap ON the bird (loaded in the cannon barrel) ──
        if (ptr.press.wasPressedThisFrame && canFire && !_isDragging)
        {
            Vector3 world = ScreenToWorld(ptr.position.ReadValue());
            if (Vector3.Distance(world, _readyBird.transform.position) < BirdClickRadius)
            {
                _isDragging   = true;
                _dragAngle    = _armRestAngle;
                _pullDistance = 0f;
            }
        }

        // ── Hold: angle + distance both follow the pointer from the abstract pivot ──
        if (_isDragging && ptr.press.isPressed)
        {
            Vector3 world = ScreenToWorld(ptr.position.ReadValue());
            Vector3 pivot = PivotPos();
            Vector2 toPointer = new Vector2(world.x - pivot.x, world.y - pivot.y);

            // Power tracks pull distance continuously from the first frame — no deadzone here,
            // unlike the angle below, since a plain magnitude has no small-vector instability.
            _pullDistance = Mathf.Clamp(toPointer.magnitude, 0f, _maxDragDistance);

            // The loaded bird sits only ~0.055 world units from the pivot (measured 2026-07-18),
            // so the very start of every drag is an angle computed from a near-zero-length
            // vector — a fraction of a millimetre of finger jitter there swings the aim across
            // most of the pull range. That reads as "twitchy"/uncontrollable on a mobile
            // touchscreen, where contact-point noise is larger than a mouse's. Freezing the angle
            // until the pointer has moved MinAimRadius away from the pivot gives the drag a
            // stable direction before it starts steering.
            if (toPointer.magnitude >= MinAimRadius)
            {
                float angle = Mathf.Atan2(toPointer.y, toPointer.x) * Mathf.Rad2Deg;
                if (angle < 0f) angle += 360f;
                _dragAngle = Mathf.Clamp(angle, _armRestAngle, _armRestAngle + MaxLoadAngle);
            }

            _rubberBandLine.positionCount = 0;
            DrawTrajectory();
        }

        // ── Release: fire. The cannon has no drag-follow visual, so nothing to snap back. ──
        if (_isDragging && ptr.press.wasReleasedThisFrame)
        {
            _isDragging = false;
            HideTrajDots();
            _rubberBandLine.positionCount = 0;
            Fire();
        }
    }

    // ── Fire ──────────────────────────────────────────────────────────────────

    void PrepareNextBird()
    {
        if (_readyBird != null) { Destroy(_readyBird.gameObject); _readyBird = null; }
        if (_levelLoader == null || !_levelLoader.HasBirdsRemaining) return;
        Vector3 loadedPos = _cannonRestPos + (Vector3)CannonLoadedBirdOffset;
        _readyBird = _levelLoader.CreateNextAnimal(_levelLoader.PeekNextBird(), loadedPos);
        if (_readyBird != null) ApplyBirdScale(_readyBird);
        _readyBird?.SetInFlightPose(); // wings-out pose reads better poking out of a cannon muzzle than the seated "loaded" pose
    }

    // Sets the visual scale AND re-derives the CircleCollider2D radius to counteract it — same
    // pattern as LevelLoader.SpawnRobot()'s BoxCollider2D re-derivation for the robot's custom
    // scale. Unity's CircleCollider2D radius scales with transform.localScale, so after
    // BirdScale grew from ~0.27 to ~4.92 (fixing Cluck_InFlight's near-invisible render size —
    // see CLAUDE.md), the hitbox grew right along with it to ~18x its designed size, extending
    // far outside the visible sprite. That's why Cluck was destroying haybails well before any
    // visible contact ("destroys the haybails before actually hitting anything"). Divides by
    // BirdScale.x (uniform, x==y) to restore whichever world-space radius each character's own
    // Awake() set (e.g. CluckAnimal: 0.36) as if scale were still 1.
    static void ApplyBirdScale(AnimalBase animal)
    {
        animal.transform.localScale = BirdScale;
        if (animal.TryGetComponent<CircleCollider2D>(out var col))
            col.radius /= BirdScale.x;
    }

    void Fire()
    {
        Vector2 velocity = LaunchVelocity();
        if (velocity.magnitude < 0.1f) return; // not enough pull — nothing fires
        if (!_levelLoader.TryConsumeBird(out AnimalType birdType)) return;
        LastAnimalUsed = birdType;
        PlayerStatsTracker.RecordCannonballFired(birdType);

        if (_readyBird != null) { Destroy(_readyBird.gameObject); _readyBird = null; }

        ScoreManager.Instance?.OnBirdFired();

        _activeAnimal = _levelLoader.CreateNextAnimal(birdType, _launchPoint);
        ApplyBirdScale(_activeAnimal);
        _activeAnimal.OnAnimalDestroyed += OnAnimalLanded;
        _activeAnimal.OnAnimalImpact    += HandleAnimalImpact;
        _activeAnimal.Launch(velocity);
        AudioManager.Play(AudioManager.Sound.Launch);
        if (birdType == AnimalType.Cluck || birdType == AnimalType.Bessie)
            AudioManager.Instance?.PlayFalling(birdType);

        if (_fireRoutine != null) StopCoroutine(_fireRoutine);
        _fireRoutine = StartCoroutine(CannonFireSequence());

        // Camera stays fixed — the bird flies across the static scene.
        if (_returnRoutine != null) StopCoroutine(_returnRoutine);
        _cameraFollowing = false;
        _returnPending   = false;
    }

    // Fires on the real hit (ground/robot/non-passthrough block) — not on Cluck's hay
    // pass-through punches, which skip base.OnCollisionEnter2D entirely.
    void HandleAnimalImpact(AnimalBase _) => AudioManager.Instance?.StopFallingFade();

    void OnAnimalLanded(AnimalBase _)
    {
        _activeAnimal = null;

        // If the bird was destroyed before a landing was detected (rare edge case),
        // start the pan-back now so the camera doesn't stay locked forever.
        if (_cameraFollowing && !_returnPending)
        {
            _returnPending = true;
            if (_returnRoutine != null) StopCoroutine(_returnRoutine);
            _returnRoutine = StartCoroutine(ReturnCamera());
        }

        if (_levelLoader != null && !_levelLoader.HasBirdsRemaining)
            _levelLoader.NotifyBirdsExhausted();
        else
            PrepareNextBird();
    }

    void OnLevelStarted(LevelData data)
    {
        if (_readyBird != null) { Destroy(_readyBird.gameObject); _readyBird = null; }
        _activeAnimal    = null;
        _isDragging      = false;
        _cameraFollowing = false;
        _returnPending   = false;
        HideTrajDots();
        _rubberBandLine.positionCount = 0;
        if (_returnRoutine != null) StopCoroutine(_returnRoutine);
        if (_fireRoutine   != null) StopCoroutine(_fireRoutine);
        if (_cannonGO != null) _cannonGO.transform.position = _cannonRestPos;
        if (_camera != null) _camera.orthographicSize = ComputeOrthoSizeForLevel(data);
        RefreshRestPoint();
        // Restart/Replay now reuses this same handler (GameManager.RestartLevel ->
        // ForceStartLevel, no scene reload — see GameManager.cs), so a leftover mid-pan-back
        // camera position from the previous attempt must be snapped back here explicitly;
        // previously this only ever ran once at Start(), which a same-scene restart skips.
        SnapCameraToRest();
        PrepareNextBird();
    }

    // Grows orthoSize to fit this level's actual block/robot bounding box (plus the cannon's own
    // position, so it's always still in frame) — see the field comment above for why this is
    // computed rather than hand-tuned per level. Block bounds use BlockSpawnData.size directly
    // (BlockBase.Initialise() scales the sprite to exactly that world-space footprint).
    //
    // Robot bounds used the fixed (0.6, 0.9) footprint, never RobotSpawnData.scale — fixed
    // 2026-07-12 (user report: L04/L05's auto-zoom was "so extreme" the sky backdrop fell short
    // of the safe area). Root cause at the time: RobotSpawnData.scale (as dumped by
    // LevelLayoutDumper, values like 5.7/5.4) is a VISUAL transform.localScale multiplier that
    // inflates this project's tiny native robot sprites up to roughly gameplay size —
    // LevelLoader.SpawnRobot's own comment confirms the real world-space PHYSICS footprint is
    // deliberately re-derived back to a pinned 0.6×0.9 (so the collider doesn't balloon and clip
    // through the ground) — but this method was using that raw scale number directly as a
    // bounding-box SIZE with no regard for how small the underlying sprite actually is, wildly
    // overestimating every robot's VISUAL footprint by ~5-9x.
    //
    // REPLACED 2026-07-13 (user report: "levels 12 upward... structure and robots too close") —
    // the flat (0.6, 0.9) fallback swung too far the other way: verified directly against the
    // actual imported art (not guessed) — HarvesterRobot.png is 612x408px, Robot_SemiHarvest.png/
    // Robot_Pawn.png/Commander.png are all 500x500px, and every robot sprite in this project
    // imports at PPU=1746 (confirmed via the Wire Scene References log) — so a robot's TRUE
    // rendered world-space footprint is (pixelSize / 1746) * RobotSpawnData.scale, not either
    // extreme. For a typical L10+ robot (scale ~6-8), that comes out to roughly 1.7-2.2 world
    // units per side — 2-3x bigger than the old flat 0.6x0.9 assumption — which is exactly why the
    // auto-zoom was computing a bounding box, and therefore a camera framing, tighter than what
    // actually renders on screen for every level using large-scale robots (L10 onward).
    static readonly System.Collections.Generic.Dictionary<RobotType, Vector2> RobotNativePixelSize = new()
    {
        { RobotType.Basic,          new Vector2(500f, 500f) }, // Robot_Pawn.png
        { RobotType.Harvester,      new Vector2(612f, 408f) }, // HarvesterRobot.png
        { RobotType.SemiHarvester,  new Vector2(500f, 500f) }, // Robot_SemiHarvest.png
        { RobotType.Commander,      new Vector2(500f, 500f) }, // Commander.png
    };
    const float RobotSpritePPU = 1746f;

    float ComputeOrthoSizeForLevel(LevelData data)
    {
        if (data == null) return _minOrthoSize;

        // Seed the bounding box from the CANNON'S REAL visual footprint, not this script's own
        // transform (the abstract "Launcher" aim-math anchor — see the Coordinate System docs in
        // CLAUDE.md — which sits wherever it was originally placed and is independent of the
        // visual FarmCannon GO). Fixed 2026-07-13, same day as the cannon's own reposition further
        // left ("cannon too close") pushed FarmCannon from ~X-3.0 to X-7.54 — ~5 units further
        // from the Launcher anchor than before, big enough that the old anchor-based seed no
        // longer has any relationship to where the cannon actually renders, risking it clipping
        // off the left edge of frame (worst case on a narrower-than-16:9 aspect) with the auto-zoom
        // never compensating because it didn't know the cannon reached that far.
        Vector2 cannonCenter = _cannonGO != null ? (Vector2)_cannonGO.transform.position : (Vector2)transform.position;
        Vector2 cannonSize   = _cannonSR != null ? (Vector2)_cannonSR.bounds.size : Vector2.one;

        Vector2 min = cannonCenter - cannonSize * 0.5f;
        Vector2 max = cannonCenter + cannonSize * 0.5f;
        void Expand(Vector2 center, Vector2 size)
        {
            Vector2 half = size * 0.5f;
            min = Vector2.Min(min, center - half);
            max = Vector2.Max(max, center + half);
        }

        if (data.blocks != null)
            foreach (var b in data.blocks) Expand(b.position, b.size);
        if (data.robots != null)
            foreach (var r in data.robots)
            {
                Vector2 footprint;
                if (r.scale != Vector2.zero && RobotNativePixelSize.TryGetValue(r.robotType, out var px))
                    footprint = new Vector2(px.x / RobotSpritePPU, px.y / RobotSpritePPU) * r.scale;
                else
                    footprint = new Vector2(0.6f, 0.9f); // no scale override — prefab default footprint
                Expand(r.position, footprint);
            }

        // The camera's rest position is FIXED (SnapCameraToRest never re-centers on content — see
        // _cameraRestOffset), so the required zoom isn't "half the total content span," it's
        // whichever side of the fixed camera center needs to reach furthest — an asymmetric
        // bounding box (like this one, cannon far left / structures far right) needs the bigger of
        // the two one-sided distances doubled, not the two-sided span halved.
        Vector2 camCenter = new Vector2(
            transform.position.x + _cameraRestOffset.x,
            transform.position.y + _cameraRestOffset.y);

        float halfWidthNeeded  = Mathf.Max(camCenter.x - min.x, max.x - camCenter.x) + ZoomPadding;
        float halfHeightNeeded = Mathf.Max(camCenter.y - min.y, max.y - camCenter.y) + ZoomPadding;

        float aspect = _camera != null && _camera.aspect > 0f ? _camera.aspect : 16f / 9f;

        // orthoSize is HALF the visible height; visible width = orthoSize * 2 * aspect — solve
        // both ways and take whichever axis needs the bigger zoom-out to fit.
        float sizeForHeight = halfHeightNeeded;
        float sizeForWidth  = halfWidthNeeded / aspect;

        return Mathf.Clamp(Mathf.Max(sizeForHeight, sizeForWidth), _minOrthoSize, _maxOrthoSize);
    }

    // ── Cannon visual ─────────────────────────────────────────────────────────

    // Creates/finds the FarmCannon GO and its single SpriteRenderer. No rotation, no arm, no
    // counterweight — a cannon is a static prop; only its position recoils on fire.
    void BuildCannon()
    {
        if (_cannonGO == null) _cannonGO = GameObject.Find("FarmCannon");

        if (_cannonGO == null)
        {
            // Position/scale user-verified 2026-07-03 (was -4.5,-2.5,2 / 2.2,1.8,1 — a stray
            // manually-placed "Cannon" GO at these exact values was found duplicating this one
            // and has been deleted from the scene; this is now the single source of truth).
            _cannonGO = new GameObject("FarmCannon");
            _cannonGO.transform.position   = new Vector3(-3.0012f, -5.1223f, 0f);
            _cannonGO.transform.localScale = new Vector3(1.4711188f, 1.3868444f, 1f);
        }

        _cannonSR = _cannonGO.GetComponent<SpriteRenderer>();
        if (_cannonSR == null) _cannonSR = _cannonGO.AddComponent<SpriteRenderer>();
        if (_cannonSprite != null) _cannonSR.sprite = _cannonSprite;
        _cannonSR.sortingOrder = 4; // explicit order wins over Z-depth ties

        _cannonRestPos = _cannonGO.transform.position;
    }

    // Procedural smoke-puff particle system: cone burst, fades out, drifts slightly upward.
    void BuildSmokeParticles()
    {
        var go = new GameObject("CannonSmoke");
        go.transform.SetParent(transform);
        _smokePS = go.AddComponent<ParticleSystem>();

        // AddComponent<ParticleSystem>() starts it playing immediately (default
        // playOnAwake=true) — configuring `main` (e.g. duration) while it's already
        // playing throws "Setting the duration while system is still playing is not
        // supported". Force it into a stopped/cleared state first.
        _smokePS.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main             = _smokePS.main;
        main.loop            = false;
        main.playOnAwake     = false;
        main.duration        = 1.2f;
        main.startLifetime   = 1.2f;
        main.startSpeed      = new ParticleSystem.MinMaxCurve(1.5f, 2.5f);
        main.startSize       = new ParticleSystem.MinMaxCurve(0.3f, 0.8f);
        main.startColor      = Color.white;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = _smokePS.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 15) });

        var shape = _smokePS.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle     = 25f;

        var colorOverLifetime = _smokePS.colorOverLifetime;
        colorOverLifetime.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f),           new GradientAlphaKey(0f, 1f) });
        colorOverLifetime.color = grad;

        // "slight upward drift Y=+0.5" — a constant upward force over the particle's life,
        // not Unity's gravityModifier (which only pulls down).
        var forceOverLifetime = _smokePS.forceOverLifetime;
        forceOverLifetime.enabled = true;
        forceOverLifetime.y = 0.5f;

        // Renders as a plain white square without an explicit texture — a bare quad has no
        // shape of its own. Reuses the same soft-radial-falloff generation as MakeDotSprite().
        var mat = new Material(Shader.Find("Sprites/Default"));
        mat.mainTexture = MakeSoftCircleTexture(32);
        var psRenderer = go.GetComponent<ParticleSystemRenderer>();
        psRenderer.material     = mat;
        // Fixed 2026-07-08: was 1 (2026-07-06 fix), which put smoke BEHIND the opaque FarmCannon
        // sprite (order 4) — the cannon fully occluded it, reported as "no smoke comes out of the
        // cannon anymore". There's no integer between 4 (cannon) and the old animal order of 5,
        // so animals moved to 6 (see AnimalBase.cs) and smoke takes 5: in front of the cannon
        // (visible puffing out of the barrel) but still behind the bird (won't cover the ~1.3s
        // flight the way the original order=6 did).
        psRenderer.sortingOrder = 5;
    }

    // Soft circular blob with a feathered edge (alpha falls off toward the rim) — smoke/puff
    // texture. Unlike MakeDotSprite() (hard edge, used for the trajectory dots), this fades
    // gradually so particles blend instead of showing a visible disc outline.
    static Texture2D MakeSoftCircleTexture(int size)
    {
        var   tex = new Texture2D(size, size, TextureFormat.ARGB32, false);
        float r   = size * 0.5f;
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float dx   = x - r + 0.5f, dy = y - r + 0.5f;
            float dist = Mathf.Sqrt(dx * dx + dy * dy) / r;
            float a    = Mathf.Clamp01(1f - dist); // 1 at centre, 0 at/past the rim
            tex.SetPixel(x, y, new Color(1f, 1f, 1f, a * a)); // squared falloff — softer edge
        }
        tex.Apply();
        return tex;
    }

    void SpawnSmoke()
    {
        if (_smokePS == null) return;
        _smokePS.transform.position = _launchPoint; // barrel mouth
        _smokePS.Play();
    }

    // Fire-time sequence: recoil out (0.08s) -> smoke burst -> recoil return (0.4s) -> wait out
    // the remainder of CannonResetDelay (1.80s total). The cannon sprite itself never swaps
    // (single "Cannon" sprite throughout) — only position (recoil) and particles animate.
    IEnumerator CannonFireSequence()
    {
        yield return StartCoroutine(RecoilTo(_cannonRestPos.x - RecoilDistance, RecoilOutDuration));

        SpawnSmoke(); // t = RecoilOutDuration (0.08s)

        yield return StartCoroutine(RecoilTo(_cannonRestPos.x, RecoilReturnDuration));

        float remaining = CannonResetDelay - RecoilOutDuration - RecoilReturnDuration;
        if (remaining > 0f) yield return new WaitForSeconds(remaining);
        // t = CannonResetDelay (1.80s) — cannon considered "reset"/ready for next bird.
    }

    // Simple coroutine tween (no DOTween in this project — checked Packages/manifest.json).
    IEnumerator RecoilTo(float targetX, float duration)
    {
        if (_cannonGO == null) yield break;
        float startX  = _cannonGO.transform.position.x;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float x = Mathf.Lerp(startX, targetX, Mathf.Clamp01(elapsed / duration));
            Vector3 p = _cannonGO.transform.position;
            _cannonGO.transform.position = new Vector3(x, p.y, p.z);
            yield return null;
        }
        Vector3 final = _cannonGO.transform.position;
        _cannonGO.transform.position = new Vector3(targetX, final.y, final.z);
    }

    // ── Trajectory preview (dotted arc) ──────────────────────────────────────
    // Physics/shape UNCHANGED — only the origin point changed (barrel mouth, fixed, instead of
    // a drag-varying bucket position, since the cannon body doesn't rotate with the pull).

    void DrawTrajectory()
    {
        Vector2 vel = LaunchVelocity();
        if (vel.magnitude < 0.1f) { HideTrajDots(); return; }

        float   grav    = Physics2D.gravity.y * 0.18f; // bird uses gravityScale=0.18 (see AnimalBase.Launch)
        float   fa      = NextBirdFA();
        float   dt      = Time.fixedDeltaTime;
        float   visible = TrajectoryVisibleFraction();
        Vector2 pos     = _launchPoint; // fixed cannon barrel mouth — arc origin never moves
        Vector2 v       = vel;

        for (int i = 0; i < _trajectoryDots; i++)
        {
            _trajDotRenderers[i].transform.position = new Vector3(pos.x, pos.y, 0f);

            float t     = i / (float)(_trajectoryDots - 1);
            float alpha = DotAlpha(t, visible);
            _trajDotRenderers[i].color = new Color(1f, 1f, 0f, alpha);

            for (int s = 0; s < _trajectorySubsteps; s++)
            {
                v.y += grav * dt;
                v   *= Mathf.Max(0f, 1f - fa * dt);
                pos += v * dt;
            }
        }
    }

    void HideTrajDots()
    {
        if (_trajDotRenderers == null) return;
        var hidden = new Color(1f, 1f, 0f, 0f);
        foreach (var sr in _trajDotRenderers) sr.color = hidden;
    }

    // Dots fade out over the last 20% of the visible window; beyond that, invisible.
    static float DotAlpha(float t, float visibleFraction)
    {
        const float FadeWindow = 0.2f;
        float fadeStart = visibleFraction - FadeWindow;
        if (t <= fadeStart)       return 0.85f;
        if (t <= visibleFraction) return Mathf.InverseLerp(visibleFraction, fadeStart, t) * 0.85f;
        return 0f;
    }

    // Earlier levels show the full arc; later levels fade out past the midpoint.
    // Level 0 → 1.0, Level 17 → 0.5 (18 World-1 levels).
    static float TrajectoryVisibleFraction()
    {
        int idx = GameManager.Instance?.CurrentLevelIndex ?? 0;
        return Mathf.Lerp(1f, 0.5f, Mathf.Clamp01(idx / 17f));
    }

    // ── Camera ────────────────────────────────────────────────────────────────

    void SmoothFollowAnimal()
    {
        Vector3 target = _activeAnimal.transform.position;
        target.z = _camera.transform.position.z;
        // Exponential decay: frame-rate independent, same feel at 30 fps and 120 fps
        float alpha = 1f - Mathf.Exp(-_cameraFollowSpeed * Time.deltaTime);
        _camera.transform.position = Vector3.Lerp(_camera.transform.position, target, alpha);
    }

    IEnumerator ReturnCamera()
    {
        // Brief pause so the player can see where the bird landed before the camera moves
        yield return new WaitForSeconds(_returnDelay);
        _cameraFollowing = false;

        Vector3 rest = new Vector3(
            transform.position.x + _cameraRestOffset.x,
            transform.position.y + _cameraRestOffset.y,
            _camera.transform.position.z);

        // SmoothStep pan: ease-in/out over _cameraReturnDuration seconds
        Vector3 from    = _camera.transform.position;
        float   elapsed = 0f;
        while (elapsed < _cameraReturnDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / _cameraReturnDuration);
            _camera.transform.position = Vector3.Lerp(from, rest, t);
            yield return null;
        }
        _camera.transform.position = rest;
    }

    void SnapCameraToRest()
    {
        _camera.transform.position = new Vector3(
            transform.position.x + _cameraRestOffset.x,
            transform.position.y + _cameraRestOffset.y,
            _camera.transform.position.z);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    // Refresh _launchPoint from the cannon's rest position (called once on start + level reset).
    void RefreshRestPoint()
    {
        _launchPoint = _cannonRestPos + (Vector3)CannonBarrelOffset;
    }

    Vector3 PivotPos() =>
        transform.position + new Vector3(0f, _pivotHeight, 0f);

    Vector2 LaunchVelocity()
    {
        // Power fraction: how far the pointer was pulled from the pivot (0 = rest, 1 = at/past
        // _maxDragDistance) — independent of angle. Replaces the old scheme where a single
        // "how far into the clamped angle range" fraction drove both speed AND angle together,
        // which is why direction never used to matter (see class comment).
        float powerFrac = Mathf.Clamp01(_pullDistance / _maxDragDistance);
        if (powerFrac < 0.05f) return Vector2.zero;

        // Direction: the pull angle mirrored through the pivot (-180°) gives the launch angle.
        // _armRestAngle=200/MaxLoadAngle=65 means _dragAngle ranges 200°-265°, mirroring to a
        // 20°-85° launch angle — dragging toward horizontal-left gives a flat 20° shot, dragging
        // toward near-straight-down gives an 85° near-vertical lob. _minLaunchSpeed/
        // _maxLaunchSpeed (3.5-8.5, raised 2026-07-11 — see that field's comment) sized against
        // the projectile-height formula y(x) = x*tanθ - g*x^2/(2v^2*cos^2θ) (g=-20*0.18=3.6) to
        // comfortably clear the tallest/farthest structures seen so far (L08/L09's stacked
        // towers, obstacles up to ~X 8 and several units above launch height) at a real angle/
        // power combination, not just barely reach that far at ground level.
        float speed    = Mathf.Lerp(_minLaunchSpeed, _maxLaunchSpeed, powerFrac);
        float angleDeg = _dragAngle - 180f;
        float rad      = angleDeg * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * speed;
    }

    float NextBirdFA() =>
        (_levelLoader != null && _levelLoader.HasBirdsRemaining &&
         _levelLoader.PeekNextBird() == AnimalType.Bessie) ? 0.016f : 0.008f;

    Vector3 ScreenToWorld(Vector2 screen)
    {
        Vector3 s = new Vector3(screen.x, screen.y, Mathf.Abs(_camera.transform.position.z));
        return _camera.ScreenToWorldPoint(s);
    }

    LineRenderer MakeLine(string goName, float width, Color color)
    {
        var go = new GameObject(goName);
        go.transform.SetParent(transform);
        var lr = go.AddComponent<LineRenderer>();
        lr.material      = new Material(Shader.Find("Sprites/Default"));
        lr.startColor    = color;
        lr.endColor      = color;
        lr.startWidth    = width;
        lr.endWidth      = width;
        lr.useWorldSpace = true;
        lr.positionCount = 0;
        lr.sortingOrder  = 5;
        return lr;
    }

    SpriteRenderer[] CreateDotPool(int count)
    {
        _dotSprite ??= MakeDotSprite(16);
        float diameter = _trajectoryDotRadius * 2f;
        var pool = new SpriteRenderer[count];
        for (int i = 0; i < count; i++)
        {
            var go = new GameObject($"TrajDot_{i}");
            go.transform.SetParent(transform);
            go.transform.localScale = new Vector3(diameter, diameter, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite       = _dotSprite;
            sr.color        = new Color(1f, 1f, 0f, 0f);
            sr.sortingOrder = 5;
            pool[i] = sr;
        }
        return pool;
    }

    static Sprite MakeDotSprite(int size)
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
