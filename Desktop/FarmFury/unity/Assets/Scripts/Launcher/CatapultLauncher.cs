using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

// Phase 1.1 — Slingshot mechanic: click the BIRD, drag backward, release to fire.
// The arm rotates to track the drag; a rubber-band line shows the pull.
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
    [SerializeField] private float _pivotHeight    = 1.6f;    // 80 px / 50
    [SerializeField] private float _armLongLength  = 1.76f;   // 88 px / 50
    [SerializeField] private float _armShortLength = 0.52f;   // 26 px / 50
    [SerializeField] private float _armRestAngle   = -140.4f;

    [Header("Camera")]
    [SerializeField] private float   _returnDelay      = 2f;
    [SerializeField] private Vector2 _cameraRestOffset = new Vector2(1.8f, 3f);

    [Header("Trajectory")]
    [SerializeField] private int _trajectoryDots     = 20;
    [SerializeField] private int _trajectorySubsteps = 3;

    // Not serialized — value must come from code so Unity can't freeze a stale Inspector value
    private const float BirdClickRadius = 1.2f;

    // Runtime state
    private float      _armAngle;
    private float      _dragAngle;      // arm angle while dragging (for snap start)
    private bool       _isDragging;
    private Vector3    _pocketPos;      // where the bird lives during drag
    private Vector3    _launchPoint;    // arm tip at rest angle — the physical fire origin
    private AnimalBase _activeAnimal;
    private AnimalBase _readyBird;
    private bool       _cameraFollowing;
    private Coroutine  _returnRoutine;

    // Renderers
    private LineRenderer _armLine;
    private LineRenderer _trajectoryLine;
    private LineRenderer _rubberBandLine;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Awake()
    {
        if (_camera == null) _camera = Camera.main;
        if (_levelLoader == null) _levelLoader = FindAnyObjectByType<LevelLoader>();

        // Ensure 2D orthographic view regardless of scene camera settings
        if (_camera != null)
        {
            _camera.orthographic     = true;
            _camera.orthographicSize = 5f;
        }

        _armAngle = _armRestAngle;

        _armLine        = MakeLine("ArmRenderer",        0.08f, new Color(0.42f, 0.23f, 0.06f));
        _trajectoryLine = MakeLine("TrajectoryRenderer", 0.05f, new Color(1f, 1f, 0f, 0.75f));
        _rubberBandLine = MakeLine("RubberBandRenderer", 0.05f, new Color(0.9f, 0.7f, 0.1f, 0.9f));
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
            // Valid ground: surface near Y=0 and wide enough to catch robots
            if (check != null && check.bounds.max.y >= -0.2f && check.bounds.size.x > 5f)
                return;
            Object.Destroy(existing); // buggy/old ground — recreate below
        }

        var go = new GameObject("Ground");
        go.tag   = "Ground";
        go.layer = 6;
        go.transform.position   = new Vector3(14f, -0.5f, 0f);
        go.transform.localScale = new Vector3(60f,  1f,   1f);

        // 1×1 local col × (60,1) scale = 60×1 world, top edge at Y=0
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

        // Bird sits at pocket while dragging, at arm tip otherwise
        if (_readyBird != null)
            _readyBird.transform.position = _isDragging ? _pocketPos : _launchPoint;

        if (_cameraFollowing && _activeAnimal != null && !_activeAnimal.IsDestroyed)
            SmoothFollowAnimal();
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

        // ── Press: must click ON the bird ───────────────────────────────────
        if (mouse.leftButton.wasPressedThisFrame && canFire && !_isDragging)
        {
            Vector3 world = ScreenToWorld(mouse.position.ReadValue());
            if (Vector3.Distance(world, _readyBird.transform.position) < BirdClickRadius)
            {
                _isDragging = true;
                _dragAngle  = _armRestAngle;
                _pocketPos  = _launchPoint;
            }
        }

        // ── Hold: bird follows mouse, clamped by max drag distance ───────────
        if (_isDragging && mouse.leftButton.isPressed)
        {
            Vector3 world = ScreenToWorld(mouse.position.ReadValue());
            Vector3 delta = world - _launchPoint;
            if (delta.magnitude > _maxDragDistance)
                delta = delta.normalized * _maxDragDistance;
            _pocketPos = _launchPoint + delta;

            // Arm tracks the pocket direction from pivot
            Vector3 pivot  = PivotPos();
            Vector2 dir    = (Vector2)(_pocketPos - pivot);
            _dragAngle     = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            DrawArmAt(_dragAngle);

            // Rubber band from rest tip to bird
            _rubberBandLine.positionCount = 2;
            _rubberBandLine.SetPosition(0, _launchPoint);
            _rubberBandLine.SetPosition(1, _pocketPos);

            DrawTrajectory();
        }

        // ── Release: fire ────────────────────────────────────────────────────
        if (_isDragging && mouse.leftButton.wasReleasedThisFrame)
        {
            _isDragging = false;
            _trajectoryLine.positionCount = 0;
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
    }

    void Fire()
    {
        Vector2 velocity = LaunchVelocity();
        if (velocity.magnitude < 0.1f) return;
        if (!_levelLoader.TryConsumeBird(out AnimalType birdType)) return;

        if (_readyBird != null) { Destroy(_readyBird.gameObject); _readyBird = null; }

        ScoreManager.Instance?.OnBirdFired();

        _activeAnimal = _levelLoader.CreateNextAnimal(birdType, _launchPoint);
        _activeAnimal.OnAnimalDestroyed += OnAnimalLanded;
        _activeAnimal.Launch(velocity);

        StartCoroutine(ArmSnap());

        if (_returnRoutine != null) StopCoroutine(_returnRoutine);
        _cameraFollowing = true;
        _returnRoutine   = StartCoroutine(ReturnCamera());
    }

    void OnAnimalLanded(AnimalBase _)
    {
        _activeAnimal    = null;
        _cameraFollowing = false;
        DrawArmAt(_armRestAngle);

        if (_levelLoader != null && !_levelLoader.HasBirdsRemaining)
            _levelLoader.NotifyBirdsExhausted();
        else
            PrepareNextBird();
    }

    void OnLevelStarted(LevelData _)
    {
        if (_readyBird != null) { Destroy(_readyBird.gameObject); _readyBird = null; }
        _activeAnimal                 = null;
        _isDragging                   = false;
        _cameraFollowing              = false;
        _armAngle                     = _armRestAngle;
        _trajectoryLine.positionCount = 0;
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

        _armLine.positionCount = 3;
        _armLine.SetPosition(0, cw);
        _armLine.SetPosition(1, pivot);
        _armLine.SetPosition(2, tip);
    }

    IEnumerator ArmSnap()
    {
        // Snap forward from wherever the arm was pulled, then return to rest
        float start   = _dragAngle;
        float forward = _armRestAngle + 74.4f;
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

    // ── Trajectory preview ────────────────────────────────────────────────────

    void DrawTrajectory()
    {
        Vector2 vel = LaunchVelocity();
        if (vel.magnitude < 0.1f) { _trajectoryLine.positionCount = 0; return; }

        float   grav = Physics2D.gravity.y;
        float   fa   = NextBirdFA();
        float   dt   = Time.fixedDeltaTime;
        var     pts  = new Vector3[_trajectoryDots];
        Vector2 pos  = _launchPoint;
        Vector2 v    = vel;

        for (int i = 0; i < _trajectoryDots; i++)
        {
            pts[i] = new Vector3(pos.x, pos.y, 0f);
            for (int s = 0; s < _trajectorySubsteps; s++)
            {
                v.y += grav * dt;
                v   *= Mathf.Max(0f, 1f - fa * dt);
                pos += v * dt;
            }
        }

        _trajectoryLine.positionCount = _trajectoryDots;
        _trajectoryLine.SetPositions(pts);
    }

    // ── Camera ────────────────────────────────────────────────────────────────

    void SmoothFollowAnimal()
    {
        Vector3 target = _activeAnimal.transform.position;
        target.z = _camera.transform.position.z;
        _camera.transform.position = Vector3.Lerp(_camera.transform.position, target, 0.08f);
    }

    IEnumerator ReturnCamera()
    {
        yield return new WaitForSeconds(_returnDelay);
        _cameraFollowing = false;

        Vector3 rest = new Vector3(
            transform.position.x + _cameraRestOffset.x,
            transform.position.y + _cameraRestOffset.y,
            _camera.transform.position.z);

        Vector3 from = _camera.transform.position;
        float   t    = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime * 0.8f;
            _camera.transform.position = Vector3.Lerp(from, rest, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t)));
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

    Vector2 LaunchVelocity()
    {
        // Velocity opposes the pull: drag LEFT-DOWN → launch RIGHT-UP
        Vector3 delta = _pocketPos - _launchPoint;
        float   dist  = Mathf.Min(delta.magnitude, _maxDragDistance);
        if (dist < 0.05f) return Vector2.zero;
        return -(Vector2)delta.normalized * (dist / _maxDragDistance * _maxLaunchSpeed);
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
}
