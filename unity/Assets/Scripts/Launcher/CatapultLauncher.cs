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
    // All arm geometry is private const — derived from sprite PPU=384, must never be
    // overridden by stale Inspector/scene-file values.
    // Pixel-measured (Python/Pillow on 2048×2048 canvas, PPU=768 → 2.667u), then scaled ×0.75
    // so the trebuchet reads correctly in the 3.5-orthoSize viewport.
    // Body: content-bottom at localPos.y=-0.020 (wheels at Y≈0); body top at Y≈1.97
    // Arm:  pivot-bolt at ~56% from canvas-bottom; pivotHeight=1.76 seats arm inside the body frame
    private const float _pivotHeight    = 1.76f;
    private const float _armLongLength  = 0.86f;
    private const float _armShortLength = 0.71f;
    private const float _armRestAngle   = 190f;   // z=0 in DrawArmAt → arm sprite appears horizontal
    private const float MaxLoadAngle    = 50f;    // degrees arm can be pulled past rest angle

    [Header("Camera")]
    [SerializeField] private float   _returnDelay          = 2.5f;   // seconds after landing before pan-back starts
    [SerializeField] private float   _cameraFollowSpeed    = 6f;     // exponential follow rate (units/s)
    [SerializeField] private float   _cameraReturnDuration = 1.2f;   // seconds for the pan-back animation
    [SerializeField] private Vector2 _cameraRestOffset     = new Vector2(5.5f, 2.5f);

    [Header("Trebuchet Art")]
    [SerializeField] private Sprite _trebuchetBodySprite;
    [SerializeField] private Sprite _trebuchetArmSprite;

    [Header("Trajectory")]
    [SerializeField] private int   _trajectoryDots      = 20;
    [SerializeField] private int   _trajectorySubsteps  = 3;
    [SerializeField] private float _trajectoryDotRadius = 0.08f;

    // Not serialized — value must come from code so Unity can't freeze a stale Inspector value
    private const float BirdClickRadius = 1.2f;

    // Bucket visual center relative to the arm pivot in the arm's rest orientation (values ×0.75 from original).
    // arm-tip at rest = pivot + (cos190°×0.86, sin190°×0.86) = pivot + (-0.847, -0.150).
    // bucket = arm-tip + (0.293, 0.030) = pivot + (-0.554, -0.120).
    private static readonly Vector2 BucketFromPivot = new Vector2(-0.55f, -0.12f);

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

    // Trebuchet sprite GOs (built at runtime if sprites are wired)
    private GameObject _armSpriteGO;

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

        if (_trebuchetBodySprite != null || _trebuchetArmSprite != null) BuildTrebuchetBody();
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

    // Creates a ground plane at runtime if none exists or if the existing one is invalid.
    static void EnsureGroundExists()
    {
        var existing = GameObject.Find("Ground");
        if (existing != null)
        {
            var check = existing.GetComponent<BoxCollider2D>();
            // Valid ground: surface near Y=-2.5 and wide enough to catch robots
            if (check != null && Mathf.Abs(check.bounds.max.y + 2.5f) < 0.5f && check.bounds.size.x > 5f)
                return;
            Object.Destroy(existing); // buggy/old ground — recreate below
        }

        var go = new GameObject("Ground");
        go.tag   = "Ground";
        go.layer = 6;
        go.transform.position   = new Vector3(0f, -2.75f, 0f);
        go.transform.localScale = new Vector3(60f,  0.5f,  1f);

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
        var mouse = Mouse.current;
        if (mouse == null) return;

        bool canFire = _activeAnimal == null
                    && _readyBird   != null
                    && _levelLoader != null
                    && _levelLoader.HasBirdsRemaining
                    && GameManager.Instance?.State == GameState.Playing;

        // ── Press: must click ON the bird (which sits in the bucket) ───────
        if (mouse.leftButton.wasPressedThisFrame && canFire && !_isDragging)
        {
            Vector3 world = ScreenToWorld(mouse.position.ReadValue());
            if (Vector3.Distance(world, _readyBird.transform.position) < BirdClickRadius)
            {
                _isDragging = true;
                _dragAngle  = _armRestAngle;
            }
        }

        // ── Hold: arm angle follows mouse from pivot; bird stays in bucket ──
        if (_isDragging && mouse.leftButton.isPressed)
        {
            Vector3 world = ScreenToWorld(mouse.position.ReadValue());
            Vector3 pivot = PivotPos();

            float angle = Mathf.Atan2(world.y - pivot.y, world.x - pivot.x) * Mathf.Rad2Deg;
            // Normalise to [0, 360] so the clamp against _armRestAngle (190°) works correctly.
            if (angle < 0f) angle += 360f;

            // Only allow the bucket side to pull DOWN and back (angle from rest up to MaxLoadAngle).
            _dragAngle = Mathf.Clamp(angle, _armRestAngle, _armRestAngle + MaxLoadAngle);
            DrawArmAt(_dragAngle);

            // Arm rotation is the visual feedback; no rubber band needed
            _rubberBandLine.positionCount = 0;
        }

        // ── Release: fire ────────────────────────────────────────────────────
        if (_isDragging && mouse.leftButton.wasReleasedThisFrame)
        {
            _isDragging = false;
            HideTrajDots();
            _rubberBandLine.positionCount = 0;
            DrawArmAt(_armRestAngle);
            Fire();
        }
    }

    // ── Fire ──────────────────────────────────────────────────────────────────

    void PrepareNextBird()
    {
        if (_readyBird != null) { Destroy(_readyBird.gameObject); _readyBird = null; }
        if (_levelLoader == null || !_levelLoader.HasBirdsRemaining) return;
        _readyBird = _levelLoader.CreateNextAnimal(_levelLoader.PeekNextBird(), _launchPoint);
        _readyBird?.SetLoadedPose();
    }

    void Fire()
    {
        Vector2 velocity = LaunchVelocity();
        if (velocity.magnitude < 0.1f) return;
        if (!_levelLoader.TryConsumeBird(out AnimalType birdType)) return;

        if (_readyBird != null) { Destroy(_readyBird.gameObject); _readyBird = null; }

        ScoreManager.Instance?.OnBirdFired();

        _activeAnimal = _levelLoader.CreateNextAnimal(birdType, BucketWorldPos(_dragAngle));
        _activeAnimal.OnAnimalDestroyed += OnAnimalLanded;
        _activeAnimal.Launch(velocity);
        AudioManager.Play(AudioManager.Sound.Launch);

        StartCoroutine(ArmSnap());

        if (_returnRoutine != null) StopCoroutine(_returnRoutine);
        _cameraFollowing = true;
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

        // Rotate arm sprite to match physics angle.
        // The arm sprite has the bucket end (left side) at ~190° in its unrotated local frame.
        // Setting z = angleDeg - 190° maps that local direction to the desired world direction.
        if (_armSpriteGO != null)
            _armSpriteGO.transform.localEulerAngles = new Vector3(0f, 0f, angleDeg - 190f);
    }

    IEnumerator ArmSnap()
    {
        // Snap forward from wherever the arm was pulled, then return to rest
        float start   = _dragAngle;
        float forward = _armRestAngle - 74.4f;  // barrel swings upward-left after firing
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / 0.25f;
            float a = Mathf.Lerp(start, forward, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t)));
            DrawArmAt(a);
            yield return null;
        }
        t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / 0.25f;
            float a = Mathf.Lerp(forward, _armRestAngle, Mathf.Clamp01(t * t));
            DrawArmAt(a);
            yield return null;
        }
        DrawArmAt(_armRestAngle);
    }

    // ── Trajectory preview (dotted arc) ──────────────────────────────────────

    void DrawTrajectory()
    {
        Vector2 vel = LaunchVelocity();
        if (vel.magnitude < 0.1f) { HideTrajDots(); return; }

        float   grav    = Physics2D.gravity.y;
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
        // Speed 7–13 m/s; full power (~45°, 13 m/s) gives range ~9.8 u from bucket,
        // reaching structure zone (x≈2.5–3.5) for World 1 from launcher at x=-5.5.
        float speed    = Mathf.Lerp(7f, 13f, loadFrac);
        float angleDeg = Mathf.Lerp(20f, 50f, loadFrac);
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
        if (_trebuchetArmSprite != null)
        {
            _armSpriteGO = new GameObject("TrebuchetArm");
            _armSpriteGO.transform.SetParent(transform);
            // Arm pivot aligned with the physics pivot (PivotPos offset). Scaled ×0.75 to match body.
            _armSpriteGO.transform.localPosition = new Vector3(0f, _pivotHeight, 0f);
            _armSpriteGO.transform.localScale    = new Vector3(0.75f, 0.75f, 1f);
            var armSR          = _armSpriteGO.AddComponent<SpriteRenderer>();
            armSR.sprite       = _trebuchetArmSprite;
            armSR.sortingOrder = 4;  // renders over frame, under rubber-band line

            // Hide the procedural arm line — sprite art replaces it
            _armLine.enabled = false;
        }
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
