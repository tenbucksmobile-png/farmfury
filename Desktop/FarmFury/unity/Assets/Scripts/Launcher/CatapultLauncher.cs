using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

// Phase 1.1 — Trebuchet mechanic: click the bird in the bucket, drag to rotate the arm,
// release to launch. The bird stays locked in the visual bucket throughout the drag.
// Place on a GameObject at world position (11.2, 0, 0).
public class CatapultLauncher : MonoBehaviour
{
    [Header("Scene References")]
    [SerializeField] private LevelLoader _levelLoader;
    [SerializeField] private Camera      _camera;

    [Header("Launch Physics")]
    [SerializeField] private float _maxDragDistance = 2.4f;   // 120 px / 50
    [SerializeField] private float _maxLaunchSpeed  = 16.8f;  // 14 px/frame × 60 fps / 50

    [Header("Arm Geometry")]
    // Arm geometry — calibrated to Inspector-verified scene layout:
    //   Trabuchet_Arm  GO at (-2.327, -4.686)  → that IS the pivot
    //   Cluck_Loaded   GO at (-2.777, -5.038)  → arm tip at rest
    //   Trabuchet_Swing GO at (-2.777, -5.017) → bucket visual at rest
    // pivotHeight: launcher at y=-6.60; pivot = -6.60 + 1.914 = -4.686 ✓
    // armLongLength: |pivot→tip| = sqrt(0.450²+0.352²) = 0.571 u
    // armRestAngle: atan2(-0.352,-0.450)+360 ≈ 218° (arm loaded/pulled position at rest)
    private const float _pivotHeight    = 1.914f;
    private const float _armLongLength  = 0.571f;
    private const float _armShortLength = 0.471f;
    private const float _armRestAngle   = 218f;
    private const float MaxLoadAngle    = 50f;

    [Header("Camera")]
    [SerializeField] private float   _returnDelay          = 2.5f;   // seconds after landing before pan-back starts
    [SerializeField] private float   _cameraFollowSpeed    = 6f;     // exponential follow rate (units/s)
    [SerializeField] private float   _cameraReturnDuration = 1.2f;   // seconds for the pan-back animation
    [SerializeField] private Vector2 _cameraRestOffset     = new Vector2(5.5f, 2.5f);

    [Header("Trebuchet Art")]
    [SerializeField] private Sprite _trebuchetBodySprite;
    [SerializeField] private Sprite _trebuchetArmSprite;
    [SerializeField] private Sprite _trebuchetCounterweightSprite;

    [Header("Trajectory")]
    [SerializeField] private int   _trajectoryDots      = 20;
    [SerializeField] private int   _trajectorySubsteps  = 3;
    [SerializeField] private float _trajectoryDotRadius = 0.08f;

    // Not serialized — value must come from code so Unity can't freeze a stale Inspector value
    private const float BirdClickRadius = 1.2f;

    // Bucket at the arm tip: pivot + (cos218°×0.571, sin218°×0.571) = pivot + (-0.450, -0.352).
    private static readonly Vector2 BucketFromPivot = new Vector2(-0.450f, -0.352f);

    // Runtime state
    private float      _armAngle;
    private float      _dragAngle;      // arm angle while dragging (for snap start)
    private bool       _isDragging;
    private Vector3    _pocketPos;      // unused — kept to avoid serialisation churn
    private Vector3    _launchPoint;    // arm tip at rest angle — the physical fire origin
    private AnimalBase _activeAnimal;
    private AnimalBase _readyBird;
    private bool       _cameraFollowing;
    private bool       _returnPending;    // true once ReturnCamera() has been started for this shot
    private Coroutine  _returnRoutine;

    // Renderers
    private LineRenderer     _armLine;
    private LineRenderer     _rubberBandLine;
    private SpriteRenderer[] _trajDotRenderers;
    private static Sprite    _dotSprite;

    [Header("Trebuchet Scene GOs")]
    [SerializeField] private GameObject _armSpriteGO;    // wire to Trabuchet_Arm scene GO
    [SerializeField] private GameObject _swingSpriteGO;  // wire to Trabuchet_Swing scene GO; shown at apex
    [SerializeField] private GameObject _counterweightGO;

    // Counterweight pendulum simulation
    private float _currentArmAngle;   // angle currently drawn (tracked for velocity)
    private float _prevArmAngle;
    private float _cwAngle;           // pendulum angle from vertical (0 = hanging straight down)
    private float _cwVelocity;        // degrees / second

    private const float CwRopeLen = 0.38f;  // short-arm-end to counterweight center (world units at ×0.75 scale)
    private const float CwGravity = 22f;    // pendulum restoring strength
    private const float CwDamping = 5f;     // angular damping

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Awake()
    {
        if (_camera == null) _camera = Camera.main;
        if (_levelLoader == null) _levelLoader = FindAnyObjectByType<LevelLoader>();
        if (GetComponent<CameraShake>()   == null) gameObject.AddComponent<CameraShake>();
        if (GetComponent<AudioManager>()  == null) gameObject.AddComponent<AudioManager>();

        // Ensure 2D orthographic view regardless of scene camera settings
        if (_camera != null)
        {
            _camera.orthographic     = true;
            _camera.orthographicSize = 4.5f;
        }

        _armAngle = _armRestAngle;

        _armLine          = MakeLine("ArmRenderer",        0.08f, new Color(0.42f, 0.23f, 0.06f));
        _rubberBandLine   = MakeLine("RubberBandRenderer", 0.05f, new Color(0.9f, 0.7f, 0.1f, 0.9f));
        _trajDotRenderers = CreateDotPool(_trajectoryDots);

        if (_trebuchetBodySprite != null || _trebuchetArmSprite != null || _armSpriteGO != null)
            BuildTrebuchetBody();

        // Swing sprite is always visible — DrawArmAt keeps it pinned to the arm tip.
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
        DrawArmAt(_armRestAngle);
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

        // Bird is always locked to the visual bucket — it rotates with the arm during drag
        if (_readyBird != null)
            _readyBird.transform.position = BucketWorldPos(_isDragging ? _dragAngle : _armRestAngle);

        UpdateCounterweight();

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

        // ── Press: must click/tap ON the bird (which sits in the bucket) ────
        if (ptr.press.wasPressedThisFrame && canFire && !_isDragging)
        {
            Vector3 world = ScreenToWorld(ptr.position.ReadValue());
            if (Vector3.Distance(world, _readyBird.transform.position) < BirdClickRadius)
            {
                _isDragging = true;
                _dragAngle  = _armRestAngle;
            }
        }

        // ── Hold: arm angle follows pointer from pivot; bird stays in bucket ─
        if (_isDragging && ptr.press.isPressed)
        {
            Vector3 world = ScreenToWorld(ptr.position.ReadValue());
            Vector3 pivot = PivotPos();

            float angle = Mathf.Atan2(world.y - pivot.y, world.x - pivot.x) * Mathf.Rad2Deg;
            if (angle < 0f) angle += 360f;

            _dragAngle = Mathf.Clamp(angle, _armRestAngle, _armRestAngle + MaxLoadAngle);
            DrawArmAt(_dragAngle);
            _rubberBandLine.positionCount = 0;
        }

        // ── Release: let ArmSnap animate the arm; do NOT pre-reset visually ─
        if (_isDragging && ptr.press.wasReleasedThisFrame)
        {
            _isDragging = false;
            HideTrajDots();
            _rubberBandLine.positionCount = 0;
            Fire(); // ArmSnap inside Fire() animates arm from drag → apex → rest
        }
    }

    // ── Fire ──────────────────────────────────────────────────────────────────

    void PrepareNextBird()
    {
        if (_readyBird != null) { Destroy(_readyBird.gameObject); _readyBird = null; }
        if (_levelLoader == null || !_levelLoader.HasBirdsRemaining) return;
        _readyBird = _levelLoader.CreateNextAnimal(_levelLoader.PeekNextBird(), _launchPoint);
        if (_readyBird != null) _readyBird.transform.localScale = new Vector3(2.2676f, 2.5454f, 1f);
        _readyBird?.SetLoadedPose();
    }

    void Fire()
    {
        Vector2 velocity = LaunchVelocity();
        if (velocity.magnitude < 0.1f)
        {
            DrawArmAt(_armRestAngle); // not enough pull — snap arm back quietly
            return;
        }
        if (!_levelLoader.TryConsumeBird(out AnimalType birdType))
        {
            DrawArmAt(_armRestAngle);
            return;
        }

        if (_readyBird != null) { Destroy(_readyBird.gameObject); _readyBird = null; }

        ScoreManager.Instance?.OnBirdFired();

        _activeAnimal = _levelLoader.CreateNextAnimal(birdType, BucketWorldPos(_dragAngle));
        _activeAnimal.transform.localScale = new Vector3(2.2676f, 2.5454f, 1f);
        _activeAnimal.OnAnimalDestroyed += OnAnimalLanded;
        _activeAnimal.Launch(velocity);
        AudioManager.Play(AudioManager.Sound.Launch);

        StartCoroutine(ArmSnap());

        // Camera stays fixed — the bird flies across the static scene.
        if (_returnRoutine != null) StopCoroutine(_returnRoutine);
        _cameraFollowing = false;
        _returnPending   = false;
    }

    void OnAnimalLanded(AnimalBase _)
    {
        _activeAnimal = null;
        DrawArmAt(_armRestAngle);

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

    void OnLevelStarted(LevelData _)
    {
        if (_readyBird != null) { Destroy(_readyBird.gameObject); _readyBird = null; }
        _activeAnimal                 = null;
        _isDragging      = false;
        _cameraFollowing = false;
        _returnPending   = false;
        _armAngle        = _armRestAngle;
        HideTrajDots();
        _rubberBandLine.positionCount = 0;
        if (_returnRoutine != null) StopCoroutine(_returnRoutine);
        RefreshRestPoint();
        DrawArmAt(_armRestAngle);
        PrepareNextBird();
    }

    // ── Arm visual ────────────────────────────────────────────────────────────

    // Draw arm using an explicit world angle.
    void DrawArmAt(float angleDeg)
    {
        _currentArmAngle = angleDeg;
        Vector3 pivot = PivotPos();
        float   rad   = angleDeg * Mathf.Deg2Rad;

        Vector3 tip = pivot + new Vector3(
            Mathf.Cos(rad) * _armLongLength,
            Mathf.Sin(rad) * _armLongLength, 0f);

        Vector3 cw = pivot + new Vector3(
            Mathf.Cos(rad + Mathf.PI) * _armShortLength,
            Mathf.Sin(rad + Mathf.PI) * _armShortLength, 0f);

        if (_armLine.enabled)
        {
            _armLine.positionCount = 3;
            _armLine.SetPosition(0, cw);
            _armLine.SetPosition(1, pivot);
            _armLine.SetPosition(2, tip);
        }

        // Pin arm GO to the physical pivot every frame so it never drifts from the body,
        // then rotate around that fixed point. eulerAngles (world) is correct here because
        // Trabuchet_Arm is a top-level scene GO (not a child of Launcher).
        if (_armSpriteGO != null)
        {
            _armSpriteGO.transform.position    = pivot;
            _armSpriteGO.transform.eulerAngles = new Vector3(0f, 0f, angleDeg - 218f);
        }

        // Swing sprite (bucket/sling visual) stays pinned to the arm tip at all times.
        if (_swingSpriteGO != null)
        {
            _swingSpriteGO.SetActive(true);
            _swingSpriteGO.transform.position = new Vector3(tip.x, tip.y,
                _swingSpriteGO.transform.position.z);
        }
    }

    IEnumerator ArmSnap()
    {
        float start   = _dragAngle;
        float forward = _armRestAngle - 74.4f;  // apex ~115.6° (upper-left)
        float t = 0f;

        // Phase 1 (0.35s): arm sweeps from pulled-back position to apex.
        // DrawArmAt() keeps both arm and swing sprites tracked — no manual SetActive needed.
        while (t < 1f)
        {
            t += Time.deltaTime / 0.35f;
            DrawArmAt(Mathf.Lerp(start, forward, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t))));
            yield return null;
        }

        yield return new WaitForSeconds(0.12f);

        // Phase 2 (0.45s): arm returns to rest.
        t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / 0.45f;
            DrawArmAt(Mathf.Lerp(forward, _armRestAngle, Mathf.Clamp01(t * t)));
            yield return null;
        }

        DrawArmAt(_armRestAngle);
    }

    // ── Trajectory preview (dotted arc) ──────────────────────────────────────

    void DrawTrajectory()
    {
        Vector2 vel = LaunchVelocity();
        if (vel.magnitude < 0.1f) { HideTrajDots(); return; }

        float   grav    = Physics2D.gravity.y * 0.4f; // bird uses gravityScale=0.4
        float   fa      = NextBirdFA();
        float   dt      = Time.fixedDeltaTime;
        float   visible = TrajectoryVisibleFraction();
        Vector2 pos     = _launchPoint;
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

    // Refresh _launchPoint from the REST angle (called once on start + level reset).
    void RefreshRestPoint()
    {
        Vector3 pivot = PivotPos();
        float   rad   = _armRestAngle * Mathf.Deg2Rad;
        _launchPoint  = pivot + new Vector3(
            Mathf.Cos(rad) * _armLongLength,
            Mathf.Sin(rad) * _armLongLength, 0f);
    }

    Vector3 PivotPos() =>
        transform.position + new Vector3(0f, _pivotHeight, 0f);

    // Returns the world position of the visual bucket for any arm angle.
    // BucketFromPivot is the bucket offset from pivot in the arm's default (rest) orientation.
    // Rotating that offset by (armAngle − restAngle) gives the bucket position at any arm angle.
    Vector3 BucketWorldPos(float armAngle)
    {
        float rotRad = (armAngle - _armRestAngle) * Mathf.Deg2Rad;
        float cos    = Mathf.Cos(rotRad), sin = Mathf.Sin(rotRad);
        return PivotPos() + new Vector3(
            BucketFromPivot.x * cos - BucketFromPivot.y * sin,
            BucketFromPivot.x * sin + BucketFromPivot.y * cos, 0f);
    }

    Vector2 LaunchVelocity()
    {
        // Load fraction: how far the arm was pulled (0 = rest, 1 = MaxLoadAngle)
        float loadFrac = Mathf.Clamp01((_dragAngle - _armRestAngle) / MaxLoadAngle);
        if (loadFrac < 0.05f) return Vector2.zero;
        // Bucket at x≈-2.78; robot at x=5.16 → 7.94u range with gravity=-8 (gravityScale=0.4).
        // At 9 m/s, 22°: t≈0.95s, Δy≈-0.40u — lands on robot at y≈-5.46. Visible arc.
        float speed    = Mathf.Lerp(6f, 9f, loadFrac);
        float angleDeg = Mathf.Lerp(45f, 22f, loadFrac); // steep arc at low power, flat at full
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

    void BuildTrebuchetBody()
    {
        // ── Static frame (wheels + A-frame) ──────────────────────────────────
        if (_trebuchetBodySprite != null)
        {
            var bodyGO = new GameObject("TrebuchetBody");
            bodyGO.transform.SetParent(transform);
            // Bottom-centre pivot (0.5, 0.0). At ×0.75 scale: content-bottom 0.026×0.75=0.020u
            // above canvas-bottom → offset -0.020 so wheel content sits exactly on Y=0.
            bodyGO.transform.localPosition = new Vector3(0f, -0.020f, 0f);
            bodyGO.transform.localScale    = new Vector3(0.75f, 0.75f, 1f);
            var bodySR          = bodyGO.AddComponent<SpriteRenderer>();
            bodySR.sprite       = _trebuchetBodySprite;
            bodySR.sortingOrder = 3;
        }

        // ── Rotating arm (pivot = sprite fulcrum, set in TextureImporter) ────
        // If _armSpriteGO is already wired (scene GO), skip creating a new child GO.
        if (_armSpriteGO == null && _trebuchetArmSprite != null)
        {
            _armSpriteGO = new GameObject("TrebuchetArm");
            _armSpriteGO.transform.SetParent(transform);
            _armSpriteGO.transform.localPosition = new Vector3(0f, _pivotHeight, 0f);
            _armSpriteGO.transform.localScale    = new Vector3(0.75f, 0.75f, 1f);
            var armSR          = _armSpriteGO.AddComponent<SpriteRenderer>();
            armSR.sprite       = _trebuchetArmSprite;
            armSR.sortingOrder = 4;
        }
        // Hide procedural line whenever sprite art is present
        if (_armSpriteGO != null) _armLine.enabled = false;

        // ── Counterweight: hangs from short arm end, swings as arm fires ─────
        if (_trebuchetCounterweightSprite != null)
        {
            _counterweightGO = new GameObject("TrebuchetCounterweight");
            _counterweightGO.transform.SetParent(transform);
            _counterweightGO.transform.localScale = new Vector3(0.75f, 0.75f, 1f);
            var cwSR          = _counterweightGO.AddComponent<SpriteRenderer>();
            cwSR.sprite       = _trebuchetCounterweightSprite;
            cwSR.sortingOrder = 3;   // between frame (3) and arm (4)

            _cwAngle    = 0f;
            _cwVelocity = 0f;
            _currentArmAngle = _armRestAngle;
            _prevArmAngle    = _armRestAngle;
        }
    }

    // ── Counterweight pendulum ────────────────────────────────────────────────

    void UpdateCounterweight()
    {
        if (_counterweightGO == null) return;

        float dt     = Time.deltaTime;
        float armVel = (_currentArmAngle - _prevArmAngle) / Mathf.Max(dt, 0.001f);
        _prevArmAngle = _currentArmAngle;

        // Pendulum physics: arm angular velocity drives the counterweight opposite its sweep
        _cwVelocity += -armVel * 0.28f * dt;                                  // inertial drive
        _cwVelocity -= CwGravity * Mathf.Sin(_cwAngle * Mathf.Deg2Rad) * dt;  // gravity restoring
        _cwVelocity -= _cwVelocity * CwDamping * dt;                          // damping
        _cwAngle    += _cwVelocity * dt;
        _cwAngle     = Mathf.Clamp(_cwAngle, -110f, 110f);

        // Position: world-space short arm tip + pendulum offset (cwAngle=0 → hangs straight down)
        float cwRad = _cwAngle * Mathf.Deg2Rad;
        _counterweightGO.transform.position =
            ShortArmEnd() + new Vector3(Mathf.Sin(cwRad) * CwRopeLen,
                                        -Mathf.Cos(cwRad) * CwRopeLen, 0f);
    }

    Vector3 ShortArmEnd()
    {
        Vector3 pivot = PivotPos();
        float   rad   = _currentArmAngle * Mathf.Deg2Rad;
        return pivot + new Vector3(Mathf.Cos(rad + Mathf.PI) * _armShortLength,
                                   Mathf.Sin(rad + Mathf.PI) * _armShortLength, 0f);
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
