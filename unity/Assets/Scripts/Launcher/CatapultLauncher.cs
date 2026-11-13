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
    [SerializeField] private Sprite     _cannonSprite;   // World 1 "Cannon" sprite (default/fallback)
    // World 2 (Frozen Tundra) cannon reskin — added 2026-07-26 (user: "how do i build level 19
    // with new sprites, cannon"). Swapped purely by LevelData.world in OnLevelStarted(), same
    // "swap the sprite, not the GameObject/position" pattern EnvironmentDepthSystem uses for the
    // backdrop and LevelLoader.SpawnRobot uses for robot prefabs — the GDD's actual "Ice Cannon"
    // mechanic (angle + ricochet) is NOT implemented here, only the visual reskin; CatapultLauncher
    // is still a single static, non-rotating prop regardless of which sprite is showing.
    [SerializeField] private Sprite     _cannonSpriteW2;
    // World 3 (Watermill Village, 2026-11-10): the Mill Cannon - oak-stave barrel in copper hoops, a little water-wheel
    // for a rear wheel, a dripping copper pipe (Kling, tools/import_watermill_art.py). Drawn on Cannon.png's 566x512
    // canvas in the same pose (like the cosmetic skins), so the barrel/loaded-animal offsets still fit.
    [SerializeField] private Sprite     _cannonSpriteW3;
    // World 4 (Sky Islands, 2026-11-11): the Sky Cannon - striped brass barrel under a little cloud balloon, propeller,
    // sky-blue carriage (Kling, fitted onto Cannon.png's canvas at PPU 384 so every offset carries over).
    [SerializeField] private Sprite     _cannonSpriteW4;
    // World 5 (Sunken City, 2026-11-12): the Sea Cannon - a brass diving-helmet torpedo cannon on a coral-crusted cart
    // (Kling, fitted onto Cannon.png's canvas at PPU 384 by tools/import_sunken_art.py).
    [SerializeField] private Sprite     _cannonSpriteW5;
    // World 6 (Robot Mothership, 2026-11-12): the Gravity Cannon - a steel barrel ringed with glowing energy coils (Kling,
    // fitted onto Cannon.png's canvas at PPU 384 by tools/import_mothership_art.py).
    [SerializeField] private Sprite     _cannonSpriteW6;
    // Paid worlds (2026-11-12, Kling, fitted onto Cannon.png's canvas at PPU 384 by tools/import_paid_worlds_art.py):
    // World 7 the Prospector Cannon (rusty barrel on a mine-cart), World 8 the Circus Cannon (striped barrel on a circus
    // wagon), World 9 the Pneumatic Cannon (teal steel barrel on a forklift carriage).
    [SerializeField] private Sprite     _cannonSpriteW7;
    [SerializeField] private Sprite     _cannonSpriteW8;
    [SerializeField] private Sprite     _cannonSpriteW9;

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
    /// <summary>Raised when an animal leaves the cannon (TutorialHints, 2026-11-12).</summary>
    public static event System.Action Fired;
    /// <summary>Where the loaded animal sits in the cannon (TutorialHints' aiming hand).</summary>
    public Vector3 LoadedPosition => _cannonRestPos + (Vector3)CannonLoadedBirdOffset;

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
        if (EnvironmentDepthSystem.Instance == null)
            new GameObject("EnvironmentDepthSystem").AddComponent<EnvironmentDepthSystem>();

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
        FarmFury.Core.SaveManager.EquippedChanged += OnEquippedChanged;
    }

    void OnDisable()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.OnLevelStarted -= OnLevelStarted;
        FarmFury.Core.SaveManager.EquippedChanged -= OnEquippedChanged;
    }

    void Start()
    {
        EnsureGroundExists();
        RefreshRestPoint();
        SnapCameraToRest();
        // No auto-start: the game opens on GameFlow's landing screen and a level starts from Character Select
        // (franchise flow, 2026-11-02). Editor tools start one directly with GameManager.ForceStartLevel.
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
                    && !LevelReveal.IsRevealing   // structure still appearing (framed levels)
                    && Time.timeScale > 0f         // frozen behind an in-level screen (Locker, character switch)
                    && GameManager.Instance?.State == GameState.Playing;

        // ── Press: must click/tap ON the bird (loaded in the cannon barrel) ──
        if (ptr.press.wasPressedThisFrame && canFire && !_isDragging)
        {
            Vector3 world = ScreenToWorld(ptr.position.ReadValue());
            if (Vector3.Distance(world, _readyBird.transform.position) < _grabRadiusNow)
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
            _pullDistance = Mathf.Clamp(toPointer.magnitude, 0f, _maxDragNow);

            // The loaded bird sits only ~0.055 world units from the pivot (measured 2026-07-18),
            // so the very start of every drag is an angle computed from a near-zero-length
            // vector — a fraction of a millimetre of finger jitter there swings the aim across
            // most of the pull range. That reads as "twitchy"/uncontrollable on a mobile
            // touchscreen, where contact-point noise is larger than a mouse's. Freezing the angle
            // until the pointer has moved MinAimRadius away from the pivot gives the drag a
            // stable direction before it starts steering.
            if (toPointer.magnitude >= _aimDeadzoneNow)
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

    /// <summary>After a revive added an animal: load it into the cannon.</summary>
    public void ReloadAfterRevive() => PrepareNextBird();

    /// <summary>
    /// The in-level character switch (HUD swap button, 2026-11-09) is open only between shots: nothing in flight,
    /// not mid-drag, animals left, the structure fully revealed.
    /// </summary>
    public bool CanSwapAnimals =>
        _activeAnimal == null && !_isDragging && _levelLoader != null && _levelLoader.HasBirdsRemaining &&
        !LevelReveal.IsRevealing && GameManager.Instance != null && GameManager.Instance.State == GameState.Playing;

    /// <summary>After a switch replaced the queue: put the new first animal in the cannon.</summary>
    public void ReloadAfterSwap() => PrepareNextBird();

    /// <summary>An animal (or its ability) is still out there: fired and not yet gone.</summary>
    public bool ShotInProgress => _activeAnimal != null && !_activeAnimal.IsDestroyed;

    /// <summary>Multi-room levels: after a room change / bonus animal, make sure one is sitting in the cannon.</summary>
    public void EnsureLoaded()
    {
        if (_readyBird == null && !ShotInProgress) PrepareNextBird();
    }

    // ── Rooms (2026-11-12, Angry Birds 2 multi-room levels; World 4 pilot) ──────────────────────────────────────────
    // The cannon, its mount, the loaded animal and the camera framing all move room-by-room: room r sits
    // r * LevelData.RoomStride to the right. The move is AB2's swoosh: 0.6 s ease-in-out with speed streaks
    // (reference: 0.3-0.7 s, peak 3-4.5 screen widths/s, a streak wipe; the far sky stays put).
    // 1.0 s rather than AB2's 0.6 so the user's Swoosh_panning.mp3 fits: its whoosh peaks mid-pan, where the camera moves
    // fastest. 1.0 -> 1.8 s (2026-11-12, user: "slow the transition down - it feels rushed"); the swoosh still peaks mid-pan.
    const float RoomPanDuration = 1.8f;
    float _roomOffsetX;
    Coroutine _panRoutine;

    public Coroutine PanToRoom(float targetOffsetX)
    {
        if (_panRoutine != null) StopCoroutine(_panRoutine);
        _panRoutine = StartCoroutine(PanRoutine(targetOffsetX));
        return _panRoutine;
    }

    IEnumerator PanRoutine(float target)
    {
        float from = _roomOffsetX;
        // A world-themed wipe over the whole screen (PanWipe, 2026-11-12) where there is one; the plain speed streaks
        // elsewhere.
        var wipe = WipeLayers(_mountWorld);
        if (wipe != null && _camera != null) PanWipe.Play(_camera, RoomPanDuration, wipe);
        var streaks = wipe == null ? MakeStreaks() : new SpriteRenderer[0];
        // The swoosh starts with the camera and peaks at mid-pan (the ease's fastest point); its tail fades soon after.
        AudioManager.PlayRoomPan(RoomPanDuration * 0.5f, RoomPanDuration + 0.4f);
        for (float e = 0f; e < RoomPanDuration; e += Time.deltaTime)
        {
            float k = e / RoomPanDuration;
            float ease = k < 0.5f ? 4f * k * k * k : 1f - Mathf.Pow(-2f * k + 2f, 3f) / 2f;
            SetRoomOffset(Mathf.Lerp(from, target, ease));
            UpdateStreaks(streaks, k);
            yield return null;
        }
        SetRoomOffset(target);
        foreach (var sr in streaks) if (sr != null) Destroy(sr.gameObject);
        _panRoutine = null;
    }

    /// <summary>Jump straight to a room (level start / QA), no pan.</summary>
    public void SnapToRoom(int room) => SetRoomOffset(room * LevelData.RoomStride);

    void SetRoomOffset(float x)
    {
        var d = new Vector3(x - _roomOffsetX, 0f, 0f);
        _roomOffsetX = x;
        _cannonRestPos += d;
        _floatBase += d;
        _mountBase += d;
        if (_cannonGO != null) _cannonGO.transform.position += d;
        if (_mount != null) _mount.transform.position += d;
        if (_silo != null) _silo.transform.position += d;
        if (_readyBird != null && !_readyBird.IsLaunched) _readyBird.transform.position += d;
        RefreshRestPoint();
        if (EnvironmentDepthSystem.Instance != null) EnvironmentDepthSystem.Instance.RoomPanX = x;
        if (_camera != null) _camera.transform.position = RestCameraPosition();
    }

    // ── World-themed room-change wipes (PanWipe, 2026-11-12, user: "a panning effect that is specific to a world -
    // example - cornfield (hurricane wind), sunken world (bubbles) - that cover the whole screen while it pans"). Worlds
    // 1 and 5 first; the others keep the speed streaks. Layers in screen fractions (u across, v up); travel = how far a
    // piece moves over the whole wipe. Art: Effects/PanWipe/ (tools/import_pan_wipes.py: leaves and feathers cut from
    // existing art), the cornfield clouds, the haybale, World 5's bubble and fish (wired by SceneSetup).
    [Header("Room-change wipes")]
    [SerializeField] private Sprite[] _wipeDust;      // World 1: soft clouds, tinted to blown dust
    [SerializeField] private Sprite[] _wipeLeaves;    // World 1: golden leaves
    [SerializeField] private Sprite[] _wipeFeathers;  // World 1: feathers
    [SerializeField] private Sprite   _wipeHay;       // World 1: a haybale tumbling past
    [SerializeField] private Sprite   _wipeBubble;    // World 5
    [SerializeField] private Sprite[] _wipeFish;      // World 5 (Fish_1-6; only Fish_5 has its head to the right)
    [SerializeField] private Sprite   _wipeIceShards; // World 2: ice splinters (World2Props/IceSplinters.png)
    [SerializeField] private Sprite   _wipeFrozenBale;// World 2: a snow-capped bale tumbling past
    [SerializeField] private Sprite   _wipeWaterDrop; // World 3: water (Abilities/WaterSwirl.png)
    [SerializeField] private Sprite   _wipeFoam;      // World 3: spray / flour puffs (Effects/DustCloud.png)
    [SerializeField] private Sprite   _wipeFlourSack; // World 3: a flour sack tumbling past
    [SerializeField] private Sprite[] _wipeSkyClouds; // World 4: big white clouds
    [SerializeField] private Sprite   _wipeBalloon;   // World 4
    [SerializeField] private Sprite   _wipeKite;      // World 4
    [SerializeField] private Sprite   _wipeAsteroid;  // World 6: asteroids tumbling past the warp (no drones, 2026-11-12)
    [SerializeField] private Sprite   _wipeTumbleweed; // World 7: tumbleweeds bowling through the sandstorm
    [SerializeField] private Sprite   _wipePopcorn;    // World 8: popcorn
    [SerializeField] private Sprite   _wipePumpkin;    // World 8: a pumpkin rolling past
    [SerializeField] private Sprite   _wipeKernel;     // World 9: corn kernels
    [SerializeField] private Sprite   _wipeCrate;      // World 9: a produce crate tumbling past
    static Sprite _softDot;
    static readonly bool[] WipeFishFacesRight = { false, false, false, false, true, false };
    static Sprite _windStreak;

    System.Collections.Generic.List<PanWipe.Layer> WipeLayers(int world)
    {
        var L = new System.Collections.Generic.List<PanWipe.Layer>();
        if (world == 1 && _wipeDust != null && _wipeDust.Length > 0)
        {
            // Hurricane gust: a dusty veil, a wall of dust clouds, wind streaks, leaves and feathers whipping past,
            // a haybale or two tumbling along low - all blasting right to left, against the pan.
            _windStreak ??= MakeWindStreak();
            L.Add(new PanWipe.Layer { veil = true, tint = new Color(0.84f, 0.74f, 0.54f), alpha = 0.5f, order = 35 });
            L.Add(new PanWipe.Layer { sprites = _wipeDust, count = 26, size = new Vector2(0.32f, 0.6f),
                start = new Vector2(0.6f, -0.25f), spread = new Vector2(2.0f, 1.35f), travel = new Vector2(-2.2f, 0.05f),
                tint = new Color(0.9f, 0.8f, 0.62f), alpha = 0.85f, order = 36 });
            L.Add(new PanWipe.Layer { sprites = new[] { _windStreak }, count = 50, size = new Vector2(0.005f, 0.01f),
                start = new Vector2(0.8f, 0f), spread = new Vector2(2.4f, 1f), travel = new Vector2(-3.4f, -0.05f),
                tint = Color.white, alpha = 0.7f, order = 37 });
            L.Add(new PanWipe.Layer { sprites = _wipeLeaves, count = 50, size = new Vector2(0.03f, 0.07f),
                start = new Vector2(0.9f, 0f), spread = new Vector2(2.0f, 1f), travel = new Vector2(-2.8f, -0.15f),
                spin = 720f, wobble = 0.04f, tint = Color.white, alpha = 1f, order = 37 });
            L.Add(new PanWipe.Layer { sprites = _wipeFeathers, count = 10, size = new Vector2(0.05f, 0.09f),
                start = new Vector2(0.9f, 0.1f), spread = new Vector2(1.8f, 0.8f), travel = new Vector2(-2.4f, 0.1f),
                spin = 300f, wobble = 0.05f, tint = Color.white, alpha = 1f, order = 37 });
            if (_wipeHay != null)
                L.Add(new PanWipe.Layer { sprites = new[] { _wipeHay }, count = 2, size = new Vector2(0.13f, 0.17f),
                    start = new Vector2(1.1f, 0.12f), spread = new Vector2(0.8f, 0.25f), travel = new Vector2(-2.0f, 0.05f),
                    spin = 400f, tint = Color.white, alpha = 1f, order = 37 });
            return L;
        }
        if (world == 2 && _wipeDust != null && _wipeDust.Length > 0)
        {
            // Blizzard whiteout: a white veil, snow clouds, near-horizontal driving snow, ice splinters glinting, a
            // frozen bale tumbling past - right to left, a little downward.
            _windStreak ??= MakeWindStreak();
            _softDot ??= MakeSoftDot();
            L.Add(new PanWipe.Layer { veil = true, tint = new Color(0.9f, 0.95f, 1f), alpha = 0.55f, order = 35 });
            L.Add(new PanWipe.Layer { sprites = _wipeDust, count = 24, size = new Vector2(0.32f, 0.6f),
                start = new Vector2(0.6f, -0.25f), spread = new Vector2(2.0f, 1.35f), travel = new Vector2(-2.2f, -0.1f),
                tint = new Color(0.96f, 0.98f, 1f), alpha = 0.85f, order = 36 });
            L.Add(new PanWipe.Layer { sprites = new[] { _softDot }, count = 160, size = new Vector2(0.008f, 0.022f),
                start = new Vector2(0.7f, 0f), spread = new Vector2(2.4f, 1.3f), travel = new Vector2(-2.9f, -0.35f),
                wobble = 0.01f, tint = Color.white, alpha = 0.95f, order = 37 });
            L.Add(new PanWipe.Layer { sprites = new[] { _windStreak }, count = 40, size = new Vector2(0.004f, 0.008f),
                start = new Vector2(0.8f, 0f), spread = new Vector2(2.4f, 1f), travel = new Vector2(-3.4f, -0.3f),
                tint = new Color(0.92f, 0.96f, 1f), alpha = 0.7f, order = 37 });
            if (_wipeIceShards != null)
                L.Add(new PanWipe.Layer { sprites = new[] { _wipeIceShards }, count = 14, size = new Vector2(0.03f, 0.06f),
                    start = new Vector2(0.9f, 0.05f), spread = new Vector2(1.9f, 0.9f), travel = new Vector2(-2.6f, -0.2f),
                    spin = 540f, tint = Color.white, alpha = 1f, order = 37 });
            if (_wipeFrozenBale != null)
                L.Add(new PanWipe.Layer { sprites = new[] { _wipeFrozenBale }, count = 1, size = new Vector2(0.14f, 0.16f),
                    start = new Vector2(1.2f, 0.12f), spread = new Vector2(0.6f, 0.2f), travel = new Vector2(-2.0f, 0.02f),
                    spin = 380f, tint = Color.white, alpha = 1f, order = 37 });
            return L;
        }
        if (world == 3 && _wipeWaterDrop != null)
        {
            // River splash: a blue-green veil, a wave of spray rolling across, water droplets flung in arcs, flour
            // puffs and a flour sack caught in it - right to left.
            L.Add(new PanWipe.Layer { veil = true, tint = new Color(0.55f, 0.8f, 0.85f), alpha = 0.45f, order = 35 });
            if (_wipeFoam != null)
            {
                L.Add(new PanWipe.Layer { sprites = new[] { _wipeFoam }, count = 22, size = new Vector2(0.3f, 0.55f),
                    start = new Vector2(0.6f, -0.25f), spread = new Vector2(2.0f, 1.3f), travel = new Vector2(-2.2f, 0.1f),
                    tint = new Color(0.78f, 0.92f, 0.98f), alpha = 0.8f, order = 36 });
                L.Add(new PanWipe.Layer { sprites = new[] { _wipeFoam }, count = 8, size = new Vector2(0.08f, 0.16f),
                    start = new Vector2(0.9f, 0f), spread = new Vector2(1.8f, 1f), travel = new Vector2(-2.6f, 0.15f),
                    tint = new Color(1f, 0.98f, 0.92f), alpha = 0.9f, order = 37 });   // flour puffs
            }
            L.Add(new PanWipe.Layer { sprites = new[] { _wipeWaterDrop }, count = 70, size = new Vector2(0.025f, 0.07f),
                start = new Vector2(0.8f, -0.1f), spread = new Vector2(2.2f, 1.2f), travel = new Vector2(-2.8f, 0.25f),
                spin = 600f, wobble = 0.02f, tint = Color.white, alpha = 0.95f, order = 37 });
            if (_wipeFlourSack != null)
                L.Add(new PanWipe.Layer { sprites = new[] { _wipeFlourSack }, count = 1, size = new Vector2(0.14f, 0.16f),
                    start = new Vector2(1.2f, 0.14f), spread = new Vector2(0.6f, 0.2f), travel = new Vector2(-2.0f, 0.05f),
                    spin = 320f, tint = Color.white, alpha = 1f, order = 37 });
            return L;
        }
        if (world == 4 && _wipeSkyClouds != null && _wipeSkyClouds.Length > 0)
        {
            // Cloud bank: big white clouds roll across and cover the screen, then part; a balloon and a kite drift
            // through - slower and softer than the gusts, right to left.
            L.Add(new PanWipe.Layer { veil = true, tint = new Color(0.93f, 0.97f, 1f), alpha = 0.5f, order = 35 });
            L.Add(new PanWipe.Layer { sprites = _wipeSkyClouds, count = 26, size = new Vector2(0.35f, 0.7f),
                start = new Vector2(0.55f, -0.3f), spread = new Vector2(2.1f, 1.4f), travel = new Vector2(-2.1f, 0f),
                tint = Color.white, alpha = 0.92f, order = 36 });
            L.Add(new PanWipe.Layer { sprites = _wipeSkyClouds, count = 12, size = new Vector2(0.12f, 0.25f),
                start = new Vector2(0.8f, 0f), spread = new Vector2(2f, 1f), travel = new Vector2(-2.6f, 0f),
                tint = Color.white, alpha = 0.85f, order = 37 });
            if (_wipeBalloon != null)
                L.Add(new PanWipe.Layer { sprites = new[] { _wipeBalloon }, count = 1, size = new Vector2(0.16f, 0.18f),
                    start = new Vector2(1.1f, 0.55f), spread = new Vector2(0.3f, 0.2f), travel = new Vector2(-1.6f, 0.08f),
                    wobble = 0.01f, tint = Color.white, alpha = 1f, order = 37 });
            if (_wipeKite != null)
                L.Add(new PanWipe.Layer { sprites = new[] { _wipeKite }, count = 1, size = new Vector2(0.1f, 0.12f),
                    start = new Vector2(1.3f, 0.75f), spread = new Vector2(0.3f, 0.1f), travel = new Vector2(-1.9f, -0.05f),
                    spin = 25f, wobble = 0.02f, tint = Color.white, alpha = 1f, order = 37 });
            return L;
        }
        if (world == 6)
        {
            // Warp jump: a deep-blue veil, star streaks stretching into hyperspace lines (two speeds), a field of soft
            // star points, asteroids tumbling past - right to left. A soft glow, no flashing.
            _windStreak ??= MakeWindStreak();
            _softDot ??= MakeSoftDot();
            L.Add(new PanWipe.Layer { veil = true, tint = new Color(0.1f, 0.14f, 0.36f), alpha = 0.6f, order = 35 });
            L.Add(new PanWipe.Layer { veil = true, tint = new Color(0.45f, 0.75f, 1f), alpha = 0.12f, order = 36 });
            L.Add(new PanWipe.Layer { sprites = new[] { _windStreak }, count = 90, size = new Vector2(0.003f, 0.008f),
                start = new Vector2(0.6f, 0f), spread = new Vector2(2.8f, 1f), travel = new Vector2(-4.2f, 0f),
                tint = new Color(0.75f, 0.88f, 1f), alpha = 0.9f, order = 37 });
            L.Add(new PanWipe.Layer { sprites = new[] { _windStreak }, count = 40, size = new Vector2(0.006f, 0.012f),
                start = new Vector2(0.7f, 0f), spread = new Vector2(2.4f, 1f), travel = new Vector2(-3.2f, 0f),
                tint = new Color(0.55f, 1f, 1f), alpha = 0.7f, order = 37 });
            L.Add(new PanWipe.Layer { sprites = new[] { _softDot }, count = 120, size = new Vector2(0.004f, 0.012f),
                start = new Vector2(0f, 0f), spread = new Vector2(2.2f, 1f), travel = new Vector2(-1.2f, 0f),
                tint = new Color(0.85f, 0.92f, 1f), alpha = 1f, order = 36 });
            if (_wipeAsteroid != null)
                L.Add(new PanWipe.Layer { sprites = new[] { _wipeAsteroid }, count = 4, size = new Vector2(0.05f, 0.12f),
                    start = new Vector2(1.1f, 0.2f), spread = new Vector2(0.8f, 0.6f), travel = new Vector2(-2.4f, -0.05f),
                    spin = 90f, tint = Color.white, alpha = 1f, order = 37 });
            return L;
        }
        if (world == 7 && _wipeDust != null && _wipeDust.Length > 0)
        {
            // Sandstorm: a rust-orange veil, a wall of red dust rolling across, grit streaks, tumbleweeds bowling
            // through low - right to left, against the pan.
            _windStreak ??= MakeWindStreak();
            _softDot ??= MakeSoftDot();
            L.Add(new PanWipe.Layer { veil = true, tint = new Color(0.86f, 0.5f, 0.28f), alpha = 0.55f, order = 35 });
            L.Add(new PanWipe.Layer { sprites = _wipeDust, count = 28, size = new Vector2(0.32f, 0.62f),
                start = new Vector2(0.6f, -0.25f), spread = new Vector2(2.0f, 1.35f), travel = new Vector2(-2.3f, 0.05f),
                tint = new Color(0.9f, 0.58f, 0.36f), alpha = 0.88f, order = 36 });
            L.Add(new PanWipe.Layer { sprites = new[] { _windStreak }, count = 60, size = new Vector2(0.005f, 0.01f),
                start = new Vector2(0.8f, 0f), spread = new Vector2(2.4f, 1f), travel = new Vector2(-3.4f, -0.05f),
                tint = new Color(1f, 0.85f, 0.65f), alpha = 0.7f, order = 37 });
            L.Add(new PanWipe.Layer { sprites = new[] { _softDot }, count = 120, size = new Vector2(0.004f, 0.012f),
                start = new Vector2(0.7f, 0f), spread = new Vector2(2.4f, 1.3f), travel = new Vector2(-3.0f, 0.05f),
                wobble = 0.01f, tint = new Color(0.95f, 0.7f, 0.45f), alpha = 0.9f, order = 37 });
            if (_wipeTumbleweed != null)
                L.Add(new PanWipe.Layer { sprites = new[] { _wipeTumbleweed }, count = 4, size = new Vector2(0.09f, 0.16f),
                    start = new Vector2(1.1f, -0.2f), spread = new Vector2(0.9f, 0.35f), travel = new Vector2(-2.2f, 0.02f),
                    spin = 420f, wobble = 0.03f, tint = Color.white, alpha = 1f, order = 37 });
            return L;
        }
        if (world == 8)
        {
            // Confetti cannon: a warm veil, a storm of confetti in fair colours and popcorn flung across, a pumpkin
            // rolling past - right to left.
            _softDot ??= MakeSoftDot();
            _confetti ??= MakeConfetti();
            L.Add(new PanWipe.Layer { veil = true, tint = new Color(0.96f, 0.82f, 0.62f), alpha = 0.5f, order = 35 });
            if (_wipeDust != null && _wipeDust.Length > 0)
                L.Add(new PanWipe.Layer { sprites = _wipeDust, count = 18, size = new Vector2(0.3f, 0.55f),
                    start = new Vector2(0.6f, -0.25f), spread = new Vector2(2.0f, 1.3f), travel = new Vector2(-2.2f, 0.05f),
                    tint = new Color(1f, 0.95f, 0.88f), alpha = 0.8f, order = 36 });
            foreach (var col in new[] { new Color(0.95f, 0.2f, 0.25f), new Color(1f, 0.82f, 0.2f), new Color(0.3f, 0.6f, 1f),
                                        new Color(0.4f, 0.85f, 0.4f), new Color(1f, 1f, 1f) })
                L.Add(new PanWipe.Layer { sprites = new[] { _confetti }, count = 34, size = new Vector2(0.01f, 0.022f),
                    start = new Vector2(0.8f, 0f), spread = new Vector2(2.3f, 1.2f), travel = new Vector2(-2.8f, -0.2f),
                    spin = 720f, wobble = 0.03f, tint = col, alpha = 1f, order = 37 });
            if (_wipePopcorn != null)
                L.Add(new PanWipe.Layer { sprites = new[] { _wipePopcorn }, count = 30, size = new Vector2(0.025f, 0.05f),
                    start = new Vector2(0.9f, -0.05f), spread = new Vector2(2.0f, 1f), travel = new Vector2(-2.6f, 0.1f),
                    spin = 360f, wobble = 0.03f, tint = Color.white, alpha = 1f, order = 37 });
            if (_wipePumpkin != null)
                L.Add(new PanWipe.Layer { sprites = new[] { _wipePumpkin }, count = 1, size = new Vector2(0.14f, 0.16f),
                    start = new Vector2(1.2f, -0.25f), spread = new Vector2(0.6f, 0.1f), travel = new Vector2(-2.0f, 0f),
                    spin = 300f, tint = Color.white, alpha = 1f, order = 37 });
            return L;
        }
        if (world == 9)
        {
            // Steam blast: a pale teal veil, a wall of steam rolling across, corn kernels sprayed out of a hopper, a
            // produce crate tumbling past - right to left.
            _windStreak ??= MakeWindStreak();
            L.Add(new PanWipe.Layer { veil = true, tint = new Color(0.78f, 0.9f, 0.88f), alpha = 0.55f, order = 35 });
            if (_wipeDust != null && _wipeDust.Length > 0)
                L.Add(new PanWipe.Layer { sprites = _wipeDust, count = 28, size = new Vector2(0.32f, 0.62f),
                    start = new Vector2(0.6f, -0.25f), spread = new Vector2(2.0f, 1.35f), travel = new Vector2(-2.2f, 0.15f),
                    tint = new Color(0.94f, 0.98f, 0.98f), alpha = 0.88f, order = 36 });
            L.Add(new PanWipe.Layer { sprites = new[] { _windStreak }, count = 40, size = new Vector2(0.004f, 0.009f),
                start = new Vector2(0.8f, 0f), spread = new Vector2(2.4f, 1f), travel = new Vector2(-3.2f, 0f),
                tint = new Color(0.9f, 1f, 1f), alpha = 0.6f, order = 37 });
            if (_wipeKernel != null)
                L.Add(new PanWipe.Layer { sprites = new[] { _wipeKernel }, count = 70, size = new Vector2(0.015f, 0.03f),
                    start = new Vector2(0.85f, -0.05f), spread = new Vector2(2.1f, 1.1f), travel = new Vector2(-2.7f, 0.15f),
                    spin = 540f, wobble = 0.02f, tint = Color.white, alpha = 1f, order = 37 });
            if (_wipeCrate != null)
                L.Add(new PanWipe.Layer { sprites = new[] { _wipeCrate }, count = 1, size = new Vector2(0.13f, 0.15f),
                    start = new Vector2(1.2f, 0.12f), spread = new Vector2(0.6f, 0.2f), travel = new Vector2(-2.0f, 0.03f),
                    spin = 320f, tint = Color.white, alpha = 1f, order = 37 });
            return L;
        }
        if (world == 5 && _wipeBubble != null)
        {
            // Bubble surge: a teal veil, bubbles of every size boiling up from below and drifting left, a school of
            // fish darting across.
            L.Add(new PanWipe.Layer { veil = true, tint = new Color(0.35f, 0.72f, 0.82f), alpha = 0.5f, order = 35 });
            L.Add(new PanWipe.Layer { sprites = new[] { _wipeBubble }, count = 85, size = new Vector2(0.06f, 0.26f),
                start = new Vector2(-0.05f, -1.3f), spread = new Vector2(1.1f, 1.2f), travel = new Vector2(-0.35f, 2.4f),
                wobble = 0.03f, tint = Color.white, alpha = 0.95f, order = 36 });
            L.Add(new PanWipe.Layer { sprites = new[] { _wipeBubble }, count = 130, size = new Vector2(0.012f, 0.04f),
                start = new Vector2(-0.05f, -1.4f), spread = new Vector2(1.1f, 1.3f), travel = new Vector2(-0.3f, 2.8f),
                wobble = 0.02f, tint = Color.white, alpha = 0.9f, order = 37 });
            if (_wipeFish != null && _wipeFish.Length > 0)
                L.Add(new PanWipe.Layer { sprites = _wipeFish, count = 12, size = new Vector2(0.045f, 0.075f),
                    start = new Vector2(1.1f, 0.35f), spread = new Vector2(0.6f, 0.4f), travel = new Vector2(-1.9f, 0.05f),
                    faceTravel = true, facesRight = WipeFishFacesRight, tint = Color.white, alpha = 1f, order = 37 });
            return L;
        }
        return null;
    }

    static Sprite _confetti;
    // A small white rectangle (confetti), tinted per layer.
    static Sprite MakeConfetti()
    {
        var tex = new Texture2D(8, 4, TextureFormat.ARGB32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < 4; y++) for (int x = 0; x < 8; x++) tex.SetPixel(x, y, Color.white);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 8, 4), new Vector2(0.5f, 0.5f), 4);
    }

    // A soft round speck (snowflakes).
    static Sprite MakeSoftDot()
    {
        const int N = 32;
        var tex = new Texture2D(N, N, TextureFormat.ARGB32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float dd = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(N / 2f, N / 2f)) / (N / 2f);
                float a = Mathf.Clamp01(1f - dd);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a * a * (3f - 2f * a)));
            }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), N);
    }

    // A long thin white line, soft at both ends (64 x 2 px -> 32:1).
    static Sprite MakeWindStreak()
    {
        var tex = new Texture2D(64, 2, TextureFormat.ARGB32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int x = 0; x < 64; x++)
        {
            float a = Mathf.Sin(Mathf.PI * (x + 0.5f) / 64f);
            for (int y = 0; y < 2; y++) tex.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 64, 2), new Vector2(0.5f, 0.5f), 64f);
    }

    // Speed streaks: thin white lines across the screen, rushing the opposite way to the pan, strongest mid-pan.
    const int StreakCount = 14;
    static Sprite _streakSprite;
    readonly float[] _streakLane = new float[StreakCount], _streakPhase = new float[StreakCount];

    SpriteRenderer[] MakeStreaks()
    {
        if (_streakSprite == null)
        {
            var tex = new Texture2D(16, 1, TextureFormat.ARGB32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int i = 0; i < 16; i++) { float a = Mathf.Sin(Mathf.PI * (i + 0.5f) / 16f); tex.SetPixel(i, 0, new Color(1f, 1f, 1f, a * a)); }
            tex.Apply();
            _streakSprite = Sprite.Create(tex, new Rect(0, 0, 16, 1), new Vector2(0.5f, 0.5f), 16f);
        }
        var list = new SpriteRenderer[StreakCount];
        for (int i = 0; i < StreakCount; i++)
        {
            var sr = new GameObject("PanStreak").AddComponent<SpriteRenderer>();
            sr.sprite = _streakSprite;
            sr.sortingOrder = 36;   // over the play layer and the near clouds, under the weather and the HUD
            sr.color = new Color(1f, 1f, 1f, 0f);
            sr.transform.localScale = new Vector3(Random.Range(3f, 9f), Random.Range(0.04f, 0.1f), 1f);
            _streakLane[i]  = Random.Range(0.08f, 0.92f);   // height up the screen
            _streakPhase[i] = Random.value * 1.4f;
            list[i] = sr;
        }
        return list;
    }

    void UpdateStreaks(SpriteRenderer[] streaks, float k)
    {
        if (_camera == null) return;
        float camH = _camera.orthographicSize * 2f, camW = camH * _camera.aspect;
        Vector3 c = _camera.transform.position;
        float alpha = 0.55f * Mathf.Sin(Mathf.PI * k);
        for (int i = 0; i < streaks.Length; i++)
        {
            var sr = streaks[i];
            if (sr == null) continue;
            float x = c.x + camW * (0.7f - Mathf.Repeat(_streakPhase[i] + k * 2.2f, 1.4f));   // rushes right to left
            sr.transform.position = new Vector3(x, c.y - camH * 0.5f + _streakLane[i] * camH, 0f);
            sr.color = new Color(1f, 1f, 1f, alpha);
        }
    }

    void PrepareNextBird()
    {
        if (_readyBird != null) { Destroy(_readyBird.gameObject); _readyBird = null; }
        if (_levelLoader == null || !_levelLoader.HasBirdsRemaining) return;
        Vector3 loadedPos = _cannonRestPos + (Vector3)CannonLoadedBirdOffset;
        _readyBird = _levelLoader.CreateNextAnimal(_levelLoader.PeekNextBird(), loadedPos);
        if (_readyBird != null)
        {
            ApplyBirdScale(_readyBird);
            // Display only: Fire() destroys it and launches a fresh animal. Its collider used to stay on, so a room pan
            // (SetRoomOffset) dragged it through the cleared room's wreckage — each touch counted as a hit and it
            // switched to its impact pose (2026-11-12, user: "loading images change in the second room").
            foreach (var c in _readyBird.GetComponentsInChildren<Collider2D>()) c.enabled = false;
        }
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
        _activeAnimal.Launch(velocity, _gravityScaleNow);
        Fired?.Invoke();
        FarmFury.Core.HighlightMarkers.Shot(_activeAnimal, birdType, velocity,
            Mathf.InverseLerp(_minSpeedNow, _maxSpeedNow, velocity.magnitude));
        AudioManager.Play(AudioManager.Sound.Launch);
        AudioManager.Instance?.PlayFalling(birdType);   // every animal's own launch scream (silent if it has none)

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

    // ── Cannon skin (cosmetic) ────────────────────────────────────────────────────────────────────────────────────
    // Equipped skin (2026-11-08): same pose and canvas as Cannon.png, so it swaps in place. Since 2026-11-13 (user: "the
    // custom cannon only appears once the level is replayed - it should change automatically when selected (lets also
    // put a golden glow around it to show it is special)") it is applied the moment it is equipped
    // (SaveManager.EquippedChanged, e.g. from the Locker mid-level), and a skinned cannon gets CannonSkinGlow behind it.
    Sprite _cannonWorldSprite;   // this world's own cannon, chosen at level start
    CannonSkinGlow _skinGlow;

    void ApplyCannonSkin()
    {
        if (_cannonSR == null) return;
        var skin = FarmFury.Core.CosmeticEffects.CannonSkin();
        if (skin != null) _cannonSR.sprite = skin;
        else if (_cannonWorldSprite != null) _cannonSR.sprite = _cannonWorldSprite;
        if (skin != null) _skinGlow = CannonSkinGlow.Attach(_cannonSR);
        if (_skinGlow != null) _skinGlow.gameObject.SetActive(skin != null);
    }

    void OnEquippedChanged(string[] category)
    {
        if (category == FarmFury.Core.StoreProducts.Cannons) ApplyCannonSkin();
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
        // World 2 reskin — falls back to the World 1 sprite whenever the W2 art isn't wired or
        // this isn't a World 2 level, so an unwired _cannonSpriteW2 never renders a blank cannon.
        if (_cannonSR != null)
            _cannonSR.sprite = (data != null && data.world == 2 && _cannonSpriteW2 != null) ? _cannonSpriteW2
                             : (data != null && data.world == 3 && _cannonSpriteW3 != null) ? _cannonSpriteW3
                             : (data != null && data.world == 4 && _cannonSpriteW4 != null) ? _cannonSpriteW4
                             : (data != null && data.world == 5 && _cannonSpriteW5 != null) ? _cannonSpriteW5
                             : (data != null && data.world == 6 && _cannonSpriteW6 != null) ? _cannonSpriteW6
                             : (data != null && data.world == 7 && _cannonSpriteW7 != null) ? _cannonSpriteW7
                             : (data != null && data.world == 8 && _cannonSpriteW8 != null) ? _cannonSpriteW8
                             : (data != null && data.world == 9 && _cannonSpriteW9 != null) ? _cannonSpriteW9
                             : _cannonSprite;
        _cannonWorldSprite = _cannonSR != null ? _cannonSR.sprite : null;
        ApplyCannonSkin();
        _framed = ReferenceFraming.Active(data);
        _mountWorld = data != null ? data.world : 1;
        // Rooms: every attempt starts in the first room.
        if (_panRoutine != null) { StopCoroutine(_panRoutine); _panRoutine = null; }
        foreach (var st in FindObjectsByType<SpriteRenderer>()) if (st != null && st.name == "PanStreak") Destroy(st.gameObject);
        _roomOffsetX = 0f;
        if (EnvironmentDepthSystem.Instance != null) EnvironmentDepthSystem.Instance.RoomPanX = 0f;
        if (_camera != null)
            _camera.orthographicSize = _framed ? ReferenceFraming.OrthoSize : ComputeOrthoSizeForLevel(data);
        // Framed levels share one ground line with the scenery (ReferenceFraming.World1GroundY); every other
        // level keeps the original physics ground at the Launcher anchor's Y.
        _groundY = _framed ? ReferenceFraming.GroundY(data) : transform.position.y;
        SetGroundTop(_groundY);
        if (_framed) SetGroundSpan(-40f, 40f + LevelData.RoomStride * ((data != null ? data.RoomCount : 1) - 1) + 20f);
        ApplyMount(_framed, _groundY);   // also resets the cannon to its scene position (was done above)
        ApplyFlightTuning();
        RefreshRestPoint();
        if (_levelLoader != null && _levelLoader.CurrentRoom > 0) SnapToRoom(_levelLoader.CurrentRoom);   // QA start room
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

        _cannonRestPos  = _cannonGO.transform.position;
        _cannonScenePos = _cannonRestPos;
    }

    // ── Haybale mount (framed levels, 2026-11-08) ───────────────────────────────────────────────────────────
    // The reference puts the launcher up on a mound (framework A5), so framed levels sit the cannon on a stack of
    // hay bales: 3 on the ground, 2 on top. Visual only — shots start above it and nothing needs to rest on it.
    // Everything that aims or fires reads _cannonRestPos, so lifting it moves the whole launcher.
    [SerializeField] private Sprite _mountBaleSprite;   // Haybail.png, wired by SceneSetup.WireLauncher
    [SerializeField] private Sprite _mountBaleSpriteW2; // Frozen/FrozenBale.png — snow-capped bales (World 2, 2026-11-09)
    int _mountWorld = 1;
    [SerializeField] private Sprite _mountBaleSpriteW3; // Watermill/FlourSacks.png — stacked flour sacks (World 3, 2026-11-10)
    [SerializeField] private Sprite _mountBaleSpriteW4; // Sky/CloudBale.png — cloud bales tied with gold rope (World 4, 2026-11-11)
    [SerializeField] private Sprite _mountBaleSpriteW5; // Sunken/SpongeBale.png — sponge bales (World 5, 2026-11-12)
    [SerializeField] private Sprite _mountBaleSpriteW6; // Mothership/BatteryPod.png — battery pods (World 6, 2026-11-12)
    // Paid worlds (2026-11-12): dusty hay bales (World 7), prize cushions (World 8), produce crates (World 9).
    [SerializeField] private Sprite _mountBaleSpriteW7;
    [SerializeField] private Sprite _mountBaleSpriteW8;
    [SerializeField] private Sprite _mountBaleSpriteW9;
    Sprite MountSprite => _mountWorld == 9 && _mountBaleSpriteW9 != null ? _mountBaleSpriteW9
                        : _mountWorld == 8 && _mountBaleSpriteW8 != null ? _mountBaleSpriteW8
                        : _mountWorld == 7 && _mountBaleSpriteW7 != null ? _mountBaleSpriteW7
                        : _mountWorld == 6 && _mountBaleSpriteW6 != null ? _mountBaleSpriteW6
                        : _mountWorld == 5 && _mountBaleSpriteW5 != null ? _mountBaleSpriteW5
                        : _mountWorld == 4 && _mountBaleSpriteW4 != null ? _mountBaleSpriteW4
                        : _mountWorld == 2 && _mountBaleSpriteW2 != null ? _mountBaleSpriteW2
                        : _mountWorld == 3 && _mountBaleSpriteW3 != null ? _mountBaleSpriteW3
                        : _mountBaleSprite;
    const float MountBaleSize   = 1.15f;   // world units per bale (sprite is square)
    const float MountBaleBottom = 19f / 500f;   // transparent rows under the bale in Haybail.png
    const float MountBaleTop    = 63f / 500f;   // transparent rows above it
    const float MountRowStep    = 0.80f;   // second row sits this far above the first (bales nest)
    const float MountSpacing    = 0.95f;   // bale centres within a row (they overlap a little)
    const float MountSink       = 0.06f;   // the cannon's wheels settle into the top bales
    // Rows of bales (bottom row = rows + 1 bales). World 1 (2026-11-12, user: "place the cannon on a higher pyramid of
    // haybales to lift for the new enhanced structures"): 5-4-3-2, two rows (1.6 u) higher than the other worlds' 3-2.
    // Worlds 2 and 3 got the same pyramid the same day ("apply the same now to W2 ... then apply W3").
    int MountRows => _mountWorld != 4 && _mountWorld >= 1 && _mountWorld <= 9 ? 4 : 2;   // the paid worlds too (2026-11-12)   // Worlds 1-3, 5, 6   // World 5 too (2026-11-12)
    int _mountBuiltRows;
    const float CannonWheelOffsetX = -0.15f;   // Cannon.png's wheels sit left of the sprite centre
    const int   MountSortingOrder  = 3;    // behind the cannon (4)

    Vector3    _cannonScenePos;   // the hand-placed FarmCannon position in Game.unity (ground level)
    GameObject _mount;

    // Height the mount lifts the cannon by, with the cannon's wheels (visual bottom) on top of the stack.
    float MountLift(float groundY)
    {
        if (_cannonSR == null || _cannonSR.sprite == null) return 0f;
        float topOfStack = groundY + MountBaleSize * (1f - MountBaleBottom) + (MountRows - 1) * MountRowStep - MountBaleSize * MountBaleTop;
        // Visual bottom of the wheels at the scene position (Cannon.png has 21 of 512 rows transparent below them).
        float cannonBottom = _cannonSR.bounds.min.y + _cannonSR.bounds.size.y * (21f / 512f);
        return topOfStack - MountSink - cannonBottom;
    }

    void ApplyMount(bool on, float groundY)
    {
        _floating = false;
        if (_cannonGO != null) _cannonGO.transform.position = _cannonScenePos;
        _cannonRestPos = _cannonScenePos;
        if (!on || MountSprite == null)
        {
            if (_mount != null) _mount.SetActive(false);
            if (_silo != null) _silo.SetActive(false);
            return;
        }
        Vector3 worldShift = MountShift;
        _cannonRestPos = _cannonScenePos + new Vector3(0f, MountLift(groundY), 0f) + worldShift;
        if (_cannonGO != null) _cannonGO.transform.position = _cannonRestPos;

        if (_mount != null && _mountBuiltRows != MountRows) { Destroy(_mount); _mount = null; }
        if (_mount == null)
        {
            _mount = new GameObject("CannonMount");
            _mountBuiltRows = MountRows;
            float scale = MountBaleSize / MountSprite.bounds.size.x;
            int i = 0;
            for (int row = 0; row < MountRows; row++)
                for (int k = 0; k < MountRows + 1 - row; k++)
                {
                    var bale = new GameObject($"Bale{i++}");
                    bale.transform.SetParent(_mount.transform, false);
                    float x = (k - (MountRows - row) * 0.5f) * MountSpacing;
                    float y = MountBaleSize * (0.5f - MountBaleBottom) + row * MountRowStep;
                    bale.transform.localPosition = new Vector3(x, y, 0f);
                    bale.transform.localScale = new Vector3(scale, scale, 1f);
                    var sr = bale.AddComponent<SpriteRenderer>();
                    sr.sprite = MountSprite;
                    sr.sortingOrder = MountSortingOrder;
                    sr.flipX = (i & 1) == 0;   // vary the straw pattern
                }
        }
        _mount.SetActive(true);
        foreach (var sr in _mount.GetComponentsInChildren<SpriteRenderer>(true)) sr.sprite = MountSprite;   // per world
        _mount.transform.position = new Vector3(_cannonScenePos.x + CannonWheelOffsetX, groundY, 0f) + worldShift;
        _floating = _mountWorld == 4;
        _floatBase = _cannonRestPos;
        _mountBase = _mount.transform.position;
        PlaceSilo(groundY, worldShift);
    }

    // ── A landmark behind the cannon, bottom left (2026-11-12) ────────────────────────────────────────────────────────
    // World 1: the broken silo (user: "insert the broken Silo to the bottom left of the screen, behind the cannon";
    // Sprites/Environment/Cornfield/BrokenSilo.png, tools/import_silo.py). World 2: a cluster of ice crystals (World2Props/
    // IceCrystals.png, "in the same position as the silo"). World 3: the mossy waterwheel (World3Props/WaterWheel.png,
    // "to fill up the space"). Stands on the ground left of the bale pyramid, which hides its lower right; travels with
    // the cannon from room to room (SetRoomOffset).
    [SerializeField] private Sprite _siloSprite;
    [SerializeField] private Sprite _iceCrystalsBackSprite;
    [SerializeField] private Sprite _waterWheelBackSprite;
    [SerializeField] private Sprite _statueBackSprite;
    [SerializeField] private Sprite _beaconBackSprite;
    // Paid worlds (2026-11-12): the water tower (World 7, Stampede WaterTower), the ferris wheel (World 8, Stampede
    // FerrisWheel), the grain silo (World 9, Stampede GrainSilo).
    [SerializeField] private Sprite _waterTowerBackSprite;
    [SerializeField] private Sprite _ferrisWheelBackSprite;
    [SerializeField] private Sprite _grainSiloBackSprite;
    [SerializeField] private Sprite _tabletBackSprite;   // World 4: the rune tablet in the clouds (Sky/SkyTablet.png)   // World 6: the warning beacon (Mothership/WarningBeacon.png)   // World 5: the sunken statue (Sunken/SunkenStatue.png)
    struct BackProp { public float height, offsetX, sink; }
    static readonly BackProp Silo       = new BackProp { height = 6.6f, offsetX = -2.9f, sink = 0.15f };   // ~0.41 sh
    static readonly BackProp IceCluster = new BackProp { height = 4.6f, offsetX = -3.2f, sink = 0.2f };
    static readonly BackProp WaterWheel = new BackProp { height = 6.0f, offsetX = -3.4f, sink = 0.2f };
    static readonly BackProp Statue     = new BackProp { height = 5.6f, offsetX = -3.1f, sink = 0.25f };
    static readonly BackProp Beacon     = new BackProp { height = 6.4f, offsetX = -2.9f, sink = 0.15f };
    static readonly BackProp WaterTower  = new BackProp { height = 6.4f, offsetX = -3.0f, sink = 0.15f };
    static readonly BackProp FerrisWheel = new BackProp { height = 6.8f, offsetX = -3.2f, sink = 0.2f };
    static readonly BackProp GrainSilo   = new BackProp { height = 6.8f, offsetX = -2.9f, sink = 0.15f };
    static readonly BackProp Tablet     = new BackProp { height = 5.4f, offsetX = -3.0f, sink = -0.3f };   // floats a little
    const int   SiloSortingOrder = 1;       // behind the bales (3) and the cannon (4); decorative band
    GameObject _silo;

    void PlaceSilo(float groundY, Vector3 worldShift)
    {
        Sprite art = _mountWorld switch { 1 => _siloSprite, 2 => _iceCrystalsBackSprite, 3 => _waterWheelBackSprite,
                                          4 => _tabletBackSprite,
                                          5 => _statueBackSprite, 6 => _beaconBackSprite,
                                          7 => _waterTowerBackSprite, 8 => _ferrisWheelBackSprite, 9 => _grainSiloBackSprite,
                                          _ => null };
        BackProp bp = _mountWorld switch { 2 => IceCluster, 3 => WaterWheel, 4 => Tablet, 5 => Statue, 6 => Beacon,
                                           7 => WaterTower, 8 => FerrisWheel, 9 => GrainSilo, _ => Silo };
        if (art == null) { if (_silo != null) _silo.SetActive(false); return; }
        if (_silo == null)
        {
            _silo = new GameObject("BrokenSilo");
            var sr = _silo.AddComponent<SpriteRenderer>();
            sr.sortingOrder = SiloSortingOrder;
        }
        var srr = _silo.GetComponent<SpriteRenderer>();
        srr.sprite = art;
        float s = bp.height / Mathf.Max(0.0001f, art.bounds.size.y);
        _silo.transform.localScale = new Vector3(s, s, 1f);
        float bottom = groundY - bp.sink;
        _silo.transform.position = new Vector3(_cannonScenePos.x + worldShift.x + bp.offsetX + _roomOffsetX,
                                               bottom - art.bounds.min.y * s, 0f);
        _silo.SetActive(true);
    }

    // ── World 4: the cannon floats (2026-11-11, user: "make it a little more difficult with the cannon floating on the
    // clouds it is embedded on") ─────────────────────────────────────────────────────────────────────────────────────
    // The cloud-bale mount and the cannon on it bob slowly up and down, and so does everything derived from
    // _cannonRestPos: the barrel tip the shot leaves from, the aim pivot, the loaded animal and the trajectory preview.
    // The player has to time the release.
    // Vertical only: the recoil animates the cannon's x, a sideways sway would fight it.
    const float FloatAmp = 0.28f, FloatPeriod = 3.4f;

    // World 4 (2026-11-12, user: "slightly elevate and raise the cannon (up and to the left)... the character struggles
    // to fly over the structure"): the cloud-bale mount and cannon sit up and left of the other worlds' spot, floating
    // clear of the cloud floor. The camera stays framed on the scene position (RestCameraPosition), so the shift shows
    // on screen. Keep in step with LAUNCH_SHIFT in tools/design_world1.py (the designer's reach check).
    static readonly Vector3 SkyMountShift = new Vector3(-1.2f, 1.6f, 0f);
    // World 1 (2026-11-12, "apply the same logic as we did for W4"): the same 1.2 u to the left; its 1.6 u of lift comes
    // from the taller bale pyramid (MountRows) - it stands on the ground.
    static readonly Vector3 FarmMountShift = new Vector3(-1.2f, 0f, 0f);
    Vector3 MountShift => _mountWorld == 4 ? SkyMountShift
                        : _mountWorld >= 1 && _mountWorld <= 9 ? FarmMountShift : Vector3.zero;
    bool _floating;
    Vector3 _floatBase, _mountBase;

    void LateUpdate()
    {
        if (!_floating || _mount == null || !_mount.activeSelf) return;
        float t = Time.time * (2f * Mathf.PI / FloatPeriod);
        var off = new Vector3(0f, Mathf.Sin(t) * FloatAmp, 0f);
        _cannonRestPos = _floatBase + off;
        if (_cannonGO != null)
        {
            Vector3 p = _cannonGO.transform.position;   // x stays the recoil's
            _cannonGO.transform.position = new Vector3(p.x, _cannonRestPos.y, p.z);
        }
        _mount.transform.position = _mountBase + off;
        RefreshRestPoint();
        if (_readyBird != null && !_readyBird.IsLaunched)
            _readyBird.transform.position = _cannonRestPos + (Vector3)CannonLoadedBirdOffset;
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

        float   grav    = Physics2D.gravity.y * _gravityScaleNow; // must match what Fire() passes to AnimalBase.Launch
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

            int subs = Mathf.Max(1, Mathf.RoundToInt(_trajectorySubsteps / Mathf.Max(0.1f, AnimalBase.Pace)));
            for (int s = 0; s < subs; s++)
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

        Vector3 rest = RestCameraPosition();

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

    void SnapCameraToRest() => _camera.transform.position = RestCameraPosition();

    // Where the camera parks between shots. Framed levels (ReferenceFraming) place it from the cannon and the
    // ground line; the rest use the fixed offset from the Launcher anchor.
    bool  _framed;
    float _groundY;

    // Flight and aim values for the current level. Framed levels take them from ReferenceFraming (screen-height
    // targets measured from the reference, converted at the framed zoom); others keep the original tuning.
    float _gravityScaleNow  = AnimalBase.DefaultGravityScale;
    float _minSpeedNow      = 3.8f;
    float _maxSpeedNow      = 9.3f;
    float _maxDragNow       = 2.4f;
    float _aimDeadzoneNow   = MinAimRadius;
    float _grabRadiusNow    = BirdClickRadius;

    void ApplyFlightTuning()
    {
        if (_framed)
        {
            _gravityScaleNow = ReferenceFraming.ProjectileGravityScale;
            _minSpeedNow     = ReferenceFraming.ToWorld(ReferenceFraming.MinLaunchSpeedSh);
            _maxSpeedNow     = ReferenceFraming.ToWorld(ReferenceFraming.MaxLaunchSpeedSh);
            _maxDragNow      = ReferenceFraming.ToWorld(ReferenceFraming.MaxDragSh);
            _aimDeadzoneNow  = ReferenceFraming.ToWorld(ReferenceFraming.AimDeadzoneSh);
            _grabRadiusNow   = ReferenceFraming.ToWorld(ReferenceFraming.GrabRadiusSh);
        }
        else
        {
            _gravityScaleNow = AnimalBase.DefaultGravityScale;
            _minSpeedNow     = _minLaunchSpeed;
            _maxSpeedNow     = _maxLaunchSpeed;
            _maxDragNow      = _maxDragDistance;
            _aimDeadzoneNow  = MinAimRadius;
            _grabRadiusNow   = BirdClickRadius;
        }
        AnimalBase.Pace = _framed ? ReferenceFraming.FlightPace : 1f;
        float dot = _framed ? ReferenceFraming.ToWorld(ReferenceFraming.TrajectoryDotSh) : _trajectoryDotRadius * 2f;
        if (_trajDotRenderers != null)
            foreach (var sr in _trajDotRenderers)
                if (sr != null) sr.transform.localScale = new Vector3(dot, dot, 1f);
    }

    Vector3 RestCameraPosition()
    {
        float z = _camera.transform.position.z;
        if (_framed)
        {
            // The scene position, not the rest position: World 4's shifted mount must not drag the framing with it.
            float cannonX = (_cannonGO != null ? _cannonScenePos.x : transform.position.x) + _roomOffsetX;   // + the room
            float aspect  = _camera.aspect > 0f ? _camera.aspect : 16f / 9f;
            Vector2 c = ReferenceFraming.CameraCentre(cannonX, _groundY, aspect);
            return new Vector3(c.x, c.y, z);
        }
        return new Vector3(transform.position.x + _cameraRestOffset.x, transform.position.y + _cameraRestOffset.y, z);
    }

    // Moves the physics ground so its top edge sits at y (keeps its width/thickness).
    static void SetGroundTop(float y)
    {
        var ground = GameObject.Find("Ground");
        if (ground == null || !ground.TryGetComponent<BoxCollider2D>(out var col)) return;
        float delta = y - col.bounds.max.y;
        if (Mathf.Abs(delta) < 0.001f) return;
        ground.transform.position += new Vector3(0f, delta, 0f);
        Physics2D.SyncTransforms();
    }

    // Widens the physics ground so it spans [left, right] (multi-room levels run well past the original 60 u strip).
    static void SetGroundSpan(float left, float right)
    {
        var ground = GameObject.Find("Ground");
        if (ground == null || !ground.TryGetComponent<BoxCollider2D>(out var col)) return;
        if (col.bounds.min.x <= left && col.bounds.max.x >= right) return;
        float unit = col.size.x * Mathf.Abs(ground.transform.lossyScale.x) / Mathf.Max(0.0001f, Mathf.Abs(ground.transform.localScale.x));
        var sc = ground.transform.localScale;
        ground.transform.localScale = new Vector3((right - left) / Mathf.Max(0.0001f, unit), sc.y, sc.z);
        var p = ground.transform.position;
        ground.transform.position = new Vector3((left + right) * 0.5f - col.offset.x * ground.transform.lossyScale.x, p.y, p.z);
        Physics2D.SyncTransforms();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    // Refresh _launchPoint from the cannon's rest position (called once on start + level reset).
    void RefreshRestPoint()
    {
        _launchPoint = _cannonRestPos + (Vector3)CannonBarrelOffset;
    }

    // Pivot for the drag-angle/power math must track the REAL, currently-visible FarmCannon (the
    // loaded bird's actual on-screen position), not this script's own transform (the abstract
    // "Launcher" aim-math anchor — see the Coordinate System docs in CLAUDE.md). Bug found
    // 2026-07-14: this used to return transform.position + pivotHeight, which was a fine
    // approximation of the loaded bird's position back when FarmCannon sat only ~0.7 units from
    // the Launcher anchor (pre-2026-07-13), but the cannon reposition ("cannon too close" fix)
    // pushed FarmCannon ~5.2 units further left with nothing here re-anchoring the pivot to match
    // — exactly the same class of bug already caught and fixed the same day for
    // ComputeOrthoSizeForLevel's camera framing, just missed here. With the pivot left ~5 units
    // away from where the player actually drags (near the visually loaded bird), a drag near the
    // cannon read as an already-near-max pull DISTANCE from the very first frame (power pinned
    // near max) and swept only a tiny ANGLE when measured from that distant point (direction
    // barely changed) — i.e. "almost no directional trajectory, nor strength" (user report).
    Vector3 PivotPos() =>
        _cannonRestPos + (Vector3)CannonLoadedBirdOffset;

    Vector2 LaunchVelocity()
    {
        // Power fraction: how far the pointer was pulled from the pivot (0 = rest, 1 = at/past
        // _maxDragDistance) — independent of angle. Replaces the old scheme where a single
        // "how far into the clamped angle range" fraction drove both speed AND angle together,
        // which is why direction never used to matter (see class comment).
        float powerFrac = Mathf.Clamp01(_pullDistance / _maxDragNow);
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
        float speed    = Mathf.Lerp(_minSpeedNow, _maxSpeedNow, powerFrac);
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
