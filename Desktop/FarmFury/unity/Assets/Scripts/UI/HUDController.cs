using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

// Phase 2.1 — HUD: score (top-centre), bird queue (bottom-left, animated), pause button (top-right).
// Creates its own Canvas + all child elements procedurally in Awake(); nothing needs to be pre-wired.
// Place on any scene GO (SceneSetup creates a "HUD" GO for this).
public class HUDController : MonoBehaviour
{
    public static HUDController Instance { get; private set; }

    // UI leaf references set during BuildCanvas()
    private TextMeshProUGUI _scoreText;
    private TextMeshProUGUI _pauseGlyph;
    private RectTransform   _birdQueueRoot;

    // Parallel lists: icon RectTransforms + their base X positions
    private readonly List<RectTransform> _birdIcons   = new();
    private readonly List<float>         _birdIconsX  = new();

    private LevelLoader _levelLoader;
    private bool        _isPaused;

    // Generated once per HUDController lifetime
    private Sprite _circleSpr;
    private Sprite _squareSpr;

    private const float IconSize   = 52f;
    private const float IconStride = 60f;  // icon size + gap
    private const float BobAmp    = 3.5f;  // pixels
    private const float BobFreq   = 1.8f;  // Hz

    // ── Level Complete panel (Phase 2.2) ──────────────────────────────────────

    private GameObject                _lcPanel;
    private TextMeshProUGUI           _lcScoreText;
    private TextMeshProUGUI           _lcBestText;
    private readonly RectTransform[]  _lcStarRTs  = new RectTransform[3];
    private readonly TextMeshProUGUI[] _lcStarTMPs = new TextMeshProUGUI[3];
    private Coroutine                  _lcAnim;

    private static readonly Color StarFilled = new Color(1.00f, 0.82f, 0.00f);
    private static readonly Color StarEmpty  = new Color(0.38f, 0.38f, 0.42f);

    // ── Level Failed panel (Phase 2.3) ────────────────────────────────────────

    private GameObject       _lfPanel;
    private TextMeshProUGUI  _lfScoreText;

    // ── Pause menu (Phase 2.4) ────────────────────────────────────────────────

    private GameObject _pausePanel;
    private Image      _musicToggleImg;
    private Image      _sfxToggleImg;

    private static readonly Color ToggleOnColor  = new Color(0.12f, 0.14f, 0.22f);
    private static readonly Color ToggleOffColor = new Color(0.40f, 0.40f, 0.44f);

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        EnsureEventSystem();
        _circleSpr = MakeCircleSprite(64);
        _squareSpr = MakeSquareSprite();
        BuildCanvas();
    }

    void Start()
    {
        _levelLoader = FindAnyObjectByType<LevelLoader>();

        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnLevelStarted += OnLevelStarted;
            GameManager.Instance.OnStateChanged  += OnStateChanged;

            // CatapultLauncher.Start() may have called ForceStartLevel(0) before us — catch up.
            if (GameManager.Instance.State == GameState.Playing)
                RefreshBirdIcons();
        }

        if (ScoreManager.Instance != null)
        {
            ScoreManager.Instance.OnScoreChanged += UpdateScore;
            UpdateScore(ScoreManager.Instance.Score);
        }

        if (_levelLoader != null)
            _levelLoader.OnBirdConsumed += RefreshBirdIcons;
    }

    void OnDestroy()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnLevelStarted -= OnLevelStarted;
            GameManager.Instance.OnStateChanged  -= OnStateChanged;
        }
        if (ScoreManager.Instance != null)
            ScoreManager.Instance.OnScoreChanged -= UpdateScore;
        if (_levelLoader != null)
            _levelLoader.OnBirdConsumed -= RefreshBirdIcons;
    }

    void Update() => AnimateBirdIcons();

    // ── Event handlers ────────────────────────────────────────────────────────

    void OnLevelStarted(LevelData _) => RefreshBirdIcons();

    void UpdateScore(int score)
    {
        if (_scoreText != null) _scoreText.text = score.ToString("N0");
    }

    void OnStateChanged(GameState state)
    {
        if (_isPaused && (state == GameState.LevelComplete || state == GameState.LevelFailed))
            SetPaused(false);

        switch (state)
        {
            case GameState.LevelComplete:
                HideLevelFailedPanel();
                ShowLevelCompletePanel();
                break;
            case GameState.LevelFailed:
                HideLevelCompletePanel();
                ShowLevelFailedPanel();
                break;
            default:
                HideLevelCompletePanel();
                HideLevelFailedPanel();
                HidePausePanel();
                break;
        }
    }

    // ── Canvas construction ───────────────────────────────────────────────────

    void BuildCanvas()
    {
        var root          = new GameObject("Canvas");
        root.transform.SetParent(transform, false);

        var canvas         = root.AddComponent<Canvas>();
        canvas.renderMode  = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        var cs                    = root.AddComponent<CanvasScaler>();
        cs.uiScaleMode            = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        cs.referenceResolution    = new Vector2(1920f, 1080f);
        cs.screenMatchMode        = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        cs.matchWidthOrHeight     = 0.5f;

        root.AddComponent<GraphicRaycaster>();

        BuildScoreDisplay(root.transform);
        BuildBirdQueueArea(root.transform);
        BuildPauseButton(root.transform);
        BuildLevelCompletePanel(root.transform);  // hidden until LevelComplete state fires
        BuildLevelFailedPanel(root.transform);    // hidden until LevelFailed state fires
        BuildPausePanel(root.transform);          // shown/hidden by pause button
    }

    // Score: dark backing strip, top-centre, with large number inside.
    void BuildScoreDisplay(Transform canvas)
    {
        var backing   = MakeImage(canvas, "ScoreBacking", _squareSpr, new Color(0f, 0f, 0f, 0.55f),
                            anchorMin: new Vector2(0.5f, 1f), anchorMax: new Vector2(0.5f, 1f),
                            pivot:     new Vector2(0.5f, 1f),
                            pos:       new Vector2(0f, 0f),
                            size:      new Vector2(260f, 62f));

        _scoreText = MakeStretchText(backing.transform, "ScoreValue",
                         fontSize: 50f, text: "0",
                         color: Color.white, align: TextAlignmentOptions.Center);
    }

    // Bird queue container: bottom-left, icons are created dynamically.
    void BuildBirdQueueArea(Transform canvas)
    {
        var rt         = MakeRect(canvas, "BirdQueue",
                             anchorMin: new Vector2(0f, 0f), anchorMax: new Vector2(0f, 0f),
                             pivot:     new Vector2(0f, 0f),
                             pos:       new Vector2(20f, 60f),
                             size:      new Vector2(500f, 70f));
        _birdQueueRoot = rt;
    }

    // Pause: circular dark button, top-right, toggles Time.timeScale.
    void BuildPauseButton(Transform canvas)
    {
        var btn = MakeImage(canvas, "PauseBtn", _circleSpr, new Color(0f, 0f, 0f, 0.62f),
                      anchorMin: new Vector2(1f, 1f), anchorMax: new Vector2(1f, 1f),
                      pivot:     new Vector2(1f, 1f),
                      pos:       new Vector2(-16f, -8f),
                      size:      new Vector2(58f, 58f));

        _pauseGlyph = MakeStretchText(btn.transform, "PauseGlyph",
                          fontSize: 24f, text: "II",
                          color: Color.white, align: TextAlignmentOptions.Center);

        var b   = btn.gameObject.AddComponent<Button>();
        var col = b.colors;
        col.normalColor      = Color.white;
        col.highlightedColor = new Color(0.78f, 0.78f, 0.78f);
        col.pressedColor     = new Color(0.55f, 0.55f, 0.55f);
        b.colors             = col;
        b.onClick.AddListener(OnPauseClicked);
    }

    // ── Bird queue ────────────────────────────────────────────────────────────

    void RefreshBirdIcons()
    {
        foreach (var rt in _birdIcons) if (rt != null) Destroy(rt.gameObject);
        _birdIcons.Clear();
        _birdIconsX.Clear();

        if (_levelLoader == null) return;

        var queue = _levelLoader.BirdQueueSnapshot;
        for (int i = 0; i < queue.Length; i++)
        {
            float x     = i * IconStride;
            bool  first = (i == 0);

            var iconGO      = new GameObject($"BirdIcon_{i}");
            iconGO.transform.SetParent(_birdQueueRoot, false);

            var img         = iconGO.AddComponent<Image>();
            var animalSpr   = _levelLoader?.GetAnimalIdleSprite(queue[i]);
            img.sprite      = animalSpr != null ? animalSpr : _circleSpr;
            img.color       = animalSpr != null ? Color.white : BirdColor(queue[i]);

            var rt          = img.rectTransform;
            rt.anchorMin    = new Vector2(0f, 0.5f);
            rt.anchorMax    = new Vector2(0f, 0.5f);
            rt.pivot        = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(x, 0f);
            rt.sizeDelta    = new Vector2(first ? IconSize * 1.2f : IconSize,
                                          first ? IconSize * 1.2f : IconSize);

            _birdIcons.Add(rt);
            _birdIconsX.Add(x);
        }
    }

    // Staggered sine-wave bob; first icon gets larger amplitude.
    void AnimateBirdIcons()
    {
        int n = _birdIcons.Count;
        for (int i = 0; i < n; i++)
        {
            if (_birdIcons[i] == null) continue;
            float phase = i * (Mathf.PI * 2f / Mathf.Max(n, 1));
            float amp   = (i == 0) ? BobAmp * 1.5f : BobAmp;
            float yOff  = Mathf.Sin(Time.unscaledTime * BobFreq * Mathf.PI * 2f + phase) * amp;
            _birdIcons[i].anchoredPosition = new Vector2(_birdIconsX[i], yOff);
        }
    }

    // ── Pause logic ───────────────────────────────────────────────────────────

    void OnPauseClicked()
    {
        if (GameManager.Instance == null) return;
        if (GameManager.Instance.State != GameState.Playing) return;
        if (_isPaused)
            SetPaused(false);
        else
        {
            SetPaused(true);
            ShowPausePanel();
        }
    }

    void SetPaused(bool pause)
    {
        _isPaused          = pause;
        Time.timeScale     = pause ? 0f : 1f;
        if (_pauseGlyph != null) _pauseGlyph.text = pause ? ">" : "II";
        if (!pause) HidePausePanel();
    }

    // ── Level Complete panel ──────────────────────────────────────────────────

    void BuildLevelCompletePanel(Transform canvas)
    {
        // Root doubles as the full-screen dark overlay
        var rootRT   = MakeFullScreenRect(canvas, "LevelCompletePanel");
        _lcPanel     = rootRT.gameObject;
        var overlay  = _lcPanel.AddComponent<Image>();
        overlay.sprite = _squareSpr;
        overlay.color  = new Color(0f, 0f, 0f, 0.50f);

        // Centre card — warm cream
        var box = MakeImage(rootRT, "LCBox", _squareSpr, new Color(0.97f, 0.95f, 0.90f),
                      new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                      new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(580f, 420f));

        // Title
        MakeCentredText(box.transform, "LCTitle",
            pos: new Vector2(0f, 155f), size: new Vector2(540f, 58f),
            fontSize: 42f, text: "LEVEL COMPLETE!",
            color: new Color(0.92f, 0.60f, 0.04f));

        // Three star slots — grey until animated gold
        for (int i = 0; i < 3; i++)
        {
            float xOff      = -160f + i * 160f;
            var starGO      = new GameObject($"LCStar_{i}");
            starGO.transform.SetParent(box.transform, false);
            var rt          = starGO.AddComponent<RectTransform>();
            rt.anchorMin    = new Vector2(0.5f, 0.5f);
            rt.anchorMax    = new Vector2(0.5f, 0.5f);
            rt.pivot        = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(xOff, 65f);
            rt.sizeDelta    = new Vector2(95f, 95f);
            var tmp         = starGO.AddComponent<TextMeshProUGUI>();
            tmp.text        = "●";
            tmp.fontSize    = 80f;
            tmp.color       = StarEmpty;
            tmp.alignment   = TextAlignmentOptions.Center;
            tmp.enableWordWrapping = false;
            _lcStarRTs[i]  = rt;
            _lcStarTMPs[i] = tmp;
        }

        // Score value (large)
        _lcScoreText = MakeCentredText(box.transform, "LCScore",
            pos: new Vector2(0f, -28f), size: new Vector2(460f, 56f),
            fontSize: 48f, text: "0",
            color: new Color(0.12f, 0.10f, 0.06f));

        // Best / new-best line
        _lcBestText = MakeCentredText(box.transform, "LCBest",
            pos: new Vector2(0f, -80f), size: new Vector2(400f, 36f),
            fontSize: 26f, text: "",
            color: new Color(0.50f, 0.50f, 0.54f));

        // Buttons
        var replayBtn = MakePanelButton(box.transform, "ReplayBtn", "REPLAY",
                            new Vector2(-148f, -160f), new Vector2(196f, 56f));
        replayBtn.onClick.AddListener(OnReplayClicked);

        var nextBtn = MakePanelButton(box.transform, "NextBtn", "NEXT >",
                          new Vector2(+148f, -160f), new Vector2(196f, 56f));
        nextBtn.onClick.AddListener(OnNextClicked);

        _lcPanel.SetActive(false);
    }

    void ShowLevelCompletePanel()
    {
        if (_lcPanel == null) return;

        int  score   = ScoreManager.Instance?.Score ?? 0;
        int  stars   = ScoreManager.Instance?.Stars ?? 0;
        bool newBest = ScoreManager.Instance?.IsNewBest ?? false;
        int  best    = ScoreManager.GetBestScore(GameManager.Instance?.CurrentLevelIndex ?? 0);

        _lcScoreText.text = score.ToString("N0");
        _lcBestText.text  = newBest ? "●  NEW BEST!" : $"BEST  {best:N0}";
        _lcBestText.color = newBest ? StarFilled : new Color(0.50f, 0.50f, 0.54f);

        // Reset all stars to grey at normal scale before animating
        for (int i = 0; i < 3; i++)
        {
            _lcStarRTs[i].localScale = Vector3.one;
            _lcStarTMPs[i].color     = StarEmpty;
        }

        _lcPanel.SetActive(true);

        if (_lcAnim != null) StopCoroutine(_lcAnim);
        _lcAnim = StartCoroutine(AnimateStars(stars));
    }

    void HideLevelCompletePanel()
    {
        if (_lcAnim != null) { StopCoroutine(_lcAnim); _lcAnim = null; }
        if (_lcPanel != null) _lcPanel.SetActive(false);
    }

    void OnReplayClicked()
    {
        HideLevelCompletePanel();
        GameManager.Instance?.RestartLevel();
    }

    void OnNextClicked()
    {
        HideLevelCompletePanel();
        GameManager.Instance?.LoadNextLevel();
    }

    // ── Level Failed panel ────────────────────────────────────────────────────

    void BuildLevelFailedPanel(Transform canvas)
    {
        var rootRT  = MakeFullScreenRect(canvas, "LevelFailedPanel");
        _lfPanel    = rootRT.gameObject;
        var overlay = _lfPanel.AddComponent<Image>();
        overlay.sprite = _squareSpr;
        overlay.color  = new Color(0f, 0f, 0f, 0.55f);

        // Card — slightly cooler/darker cream than Level Complete
        var box = MakeImage(rootRT, "LFBox", _squareSpr, new Color(0.96f, 0.93f, 0.90f),
                      new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                      new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(480f, 300f));

        // Title — deep red
        MakeCentredText(box.transform, "LFTitle",
            pos: new Vector2(0f, 100f), size: new Vector2(440f, 54f),
            fontSize: 40f, text: "LEVEL FAILED!",
            color: new Color(0.82f, 0.14f, 0.10f));

        // Score label
        MakeCentredText(box.transform, "LFScoreLabel",
            pos: new Vector2(0f, 28f), size: new Vector2(380f, 32f),
            fontSize: 24f, text: "SCORE",
            color: new Color(0.45f, 0.42f, 0.40f));

        // Score value
        _lfScoreText = MakeCentredText(box.transform, "LFScore",
            pos: new Vector2(0f, -16f), size: new Vector2(380f, 48f),
            fontSize: 42f, text: "0",
            color: new Color(0.12f, 0.10f, 0.06f));

        // Buttons
        var tryAgainBtn = MakePanelButton(box.transform, "TryAgainBtn", "TRY AGAIN",
                              new Vector2(-118f, -110f), new Vector2(196f, 56f));
        tryAgainBtn.onClick.AddListener(OnTryAgainClicked);

        var menuBtn = MakePanelButton(box.transform, "MenuBtn", "MENU",
                          new Vector2(+118f, -110f), new Vector2(130f, 56f));
        menuBtn.onClick.AddListener(OnMenuClicked);

        _lfPanel.SetActive(false);
    }

    void ShowLevelFailedPanel()
    {
        if (_lfPanel == null) return;
        _lfScoreText.text = (ScoreManager.Instance?.Score ?? 0).ToString("N0");
        _lfPanel.SetActive(true);
    }

    void HideLevelFailedPanel()
    {
        if (_lfPanel != null) _lfPanel.SetActive(false);
    }

    void OnTryAgainClicked()
    {
        HideLevelFailedPanel();
        GameManager.Instance?.RestartLevel();
    }

    void OnMenuClicked()
    {
        HideLevelFailedPanel();
        GameManager.Instance?.LoadMenu();
    }

    // ── Pause menu ────────────────────────────────────────────────────────────

    void BuildPausePanel(Transform canvas)
    {
        var rootRT  = MakeFullScreenRect(canvas, "PausePanel");
        _pausePanel = rootRT.gameObject;
        var overlay = _pausePanel.AddComponent<Image>();
        overlay.sprite = _squareSpr;
        overlay.color  = new Color(0f, 0f, 0f, 0.60f);

        // Centre card
        var box = MakeImage(rootRT, "PauseBox", _squareSpr, new Color(0.97f, 0.95f, 0.90f),
                      new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                      new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(380f, 340f));

        // Title
        MakeCentredText(box.transform, "PauseTitle",
            pos: new Vector2(0f, 130f), size: new Vector2(340f, 50f),
            fontSize: 38f, text: "PAUSED",
            color: new Color(0.12f, 0.10f, 0.06f));

        // Action buttons
        var resumeBtn = MakePanelButton(box.transform, "ResumeBtn", "RESUME",
                            new Vector2(0f, 50f), new Vector2(280f, 52f));
        resumeBtn.onClick.AddListener(OnPauseResumeClicked);

        var restartBtn = MakePanelButton(box.transform, "PauseRestartBtn", "RESTART",
                             new Vector2(0f, -15f), new Vector2(280f, 52f));
        restartBtn.onClick.AddListener(OnPauseRestartClicked);

        var menuBtn = MakePanelButton(box.transform, "PauseMenuBtn", "MENU",
                          new Vector2(0f, -80f), new Vector2(280f, 52f));
        menuBtn.onClick.AddListener(OnPauseMenuClicked);

        // Divider
        MakeImage(box.transform, "PauseDivider", _squareSpr, new Color(0.72f, 0.70f, 0.68f),
              new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
              new Vector2(0.5f, 0.5f), new Vector2(0f, -120f), new Vector2(300f, 2f));

        // Toggle buttons
        var musicBtn = MakePanelButton(box.transform, "MusicToggle", "MUSIC",
                           new Vector2(-80f, -148f), new Vector2(130f, 44f));
        _musicToggleImg = musicBtn.targetGraphic as Image;
        musicBtn.onClick.AddListener(OnMusicToggleClicked);

        var sfxBtn = MakePanelButton(box.transform, "SfxToggle", "SFX",
                         new Vector2(+80f, -148f), new Vector2(130f, 44f));
        _sfxToggleImg = sfxBtn.targetGraphic as Image;
        sfxBtn.onClick.AddListener(OnSfxToggleClicked);

        _pausePanel.SetActive(false);
    }

    void ShowPausePanel()
    {
        if (_pausePanel == null) return;
        // Sync toggle button colors to persisted AudioManager state
        if (_musicToggleImg != null)
            _musicToggleImg.color = AudioManager.MusicEnabled ? ToggleOnColor : ToggleOffColor;
        if (_sfxToggleImg != null)
            _sfxToggleImg.color   = AudioManager.SfxEnabled   ? ToggleOnColor : ToggleOffColor;
        _pausePanel.SetActive(true);
    }

    void HidePausePanel()
    {
        if (_pausePanel != null) _pausePanel.SetActive(false);
    }

    void OnPauseResumeClicked()  => SetPaused(false);

    void OnPauseRestartClicked()
    {
        SetPaused(false);
        GameManager.Instance?.RestartLevel();
    }

    void OnPauseMenuClicked()
    {
        SetPaused(false);
        GameManager.Instance?.LoadMenu();
    }

    void OnMusicToggleClicked()
    {
        bool on = !AudioManager.MusicEnabled;
        AudioManager.SetMusicEnabled(on);
        if (_musicToggleImg != null)
            _musicToggleImg.color = on ? ToggleOnColor : ToggleOffColor;
    }

    void OnSfxToggleClicked()
    {
        bool on = !AudioManager.SfxEnabled;
        AudioManager.SetSfxEnabled(on);
        if (_sfxToggleImg != null)
            _sfxToggleImg.color = on ? ToggleOnColor : ToggleOffColor;
    }

    // Stagger: star 1 at 0.3s, star 2 at 0.75s, star 3 at 1.2s after panel opens.
    IEnumerator AnimateStars(int earnedCount)
    {
        for (int i = 0; i < 3; i++)
        {
            yield return new WaitForSecondsRealtime(0.30f + i * 0.45f);
            if (i < earnedCount)
                StartCoroutine(PopStar(i));
        }
        _lcAnim = null;
    }

    // Bounce scale 1→1.42→1, simultaneous grey→gold colour transition.
    IEnumerator PopStar(int idx)
    {
        var rt  = _lcStarRTs[idx];
        var tmp = _lcStarTMPs[idx];
        if (rt == null || tmp == null) yield break;

        float elapsed = 0f;
        const float dur = 0.38f;
        while (elapsed < dur)
        {
            float t      = Mathf.Clamp01(elapsed / dur);
            rt.localScale = Vector3.one * StarBounce(t);
            tmp.color     = Color.Lerp(StarEmpty, StarFilled,
                                Mathf.SmoothStep(0f, 1f, t * 1.4f));
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
        rt.localScale = Vector3.one;
        tmp.color     = StarFilled;
    }

    // Ease-out overshoot: 1 at t=0, peak 1.42 at t=0.6, settles to 1 at t=1.
    static float StarBounce(float t)
    {
        const float peak = 1.42f, peakT = 0.60f;
        return t < peakT
            ? Mathf.SmoothStep(1f, peak, t / peakT)
            : Mathf.SmoothStep(peak, 1f, (t - peakT) / (1f - peakT));
    }

    // ── UI factory helpers ────────────────────────────────────────────────────

    // Text at a fixed centre-anchored position within its parent.
    static TextMeshProUGUI MakeCentredText(Transform parent, string name,
        Vector2 pos, Vector2 size, float fontSize, string text, Color color,
        TextAlignmentOptions align = TextAlignmentOptions.Center)
    {
        var rt = MakeRect(parent, name,
                     anchorMin: new Vector2(0.5f, 0.5f), anchorMax: new Vector2(0.5f, 0.5f),
                     pivot: new Vector2(0.5f, 0.5f), pos: pos, size: size);
        var tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
        tmp.text               = text;
        tmp.fontSize           = fontSize;
        tmp.color              = color;
        tmp.alignment          = align;
        tmp.enableWordWrapping = false;
        return tmp;
    }

    // Dark rectangular button with centred white label; returns the Button component.
    Button MakePanelButton(Transform parent, string name, string label, Vector2 pos, Vector2 size)
    {
        var img = MakeImage(parent, name, _squareSpr, new Color(0.12f, 0.14f, 0.22f),
                      new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                      new Vector2(0.5f, 0.5f), pos, size);
        MakeStretchText(img.transform, "Label", fontSize: 26f, text: label,
                        color: Color.white, align: TextAlignmentOptions.Center);
        var btn           = img.gameObject.AddComponent<Button>();
        btn.targetGraphic = img;
        var cols          = btn.colors;
        cols.normalColor      = Color.white;
        cols.highlightedColor = new Color(0.80f, 0.80f, 0.80f);
        cols.pressedColor     = new Color(0.52f, 0.52f, 0.52f);
        btn.colors            = cols;
        return btn;
    }

    // RectTransform that stretches to fill its parent — used for full-screen overlays.
    static RectTransform MakeFullScreenRect(Transform parent, string name)
    {
        var go       = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt       = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        return rt;
    }

    static RectTransform MakeRect(Transform parent, string name,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        var go              = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt              = go.AddComponent<RectTransform>();
        rt.anchorMin        = anchorMin;
        rt.anchorMax        = anchorMax;
        rt.pivot            = pivot;
        rt.anchoredPosition = pos;
        rt.sizeDelta        = size;
        return rt;
    }

    static Image MakeImage(Transform parent, string name, Sprite sprite, Color color,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        var rt              = MakeRect(parent, name, anchorMin, anchorMax, pivot, pos, size);
        var img             = rt.gameObject.AddComponent<Image>();
        img.sprite          = sprite;
        img.color           = color;
        img.type            = Image.Type.Simple;
        return img;
    }

    // Text that stretches to fill its parent RectTransform.
    static TextMeshProUGUI MakeStretchText(Transform parent, string name,
        float fontSize, string text, Color color, TextAlignmentOptions align)
    {
        var go          = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt          = go.AddComponent<RectTransform>();
        rt.anchorMin    = Vector2.zero;
        rt.anchorMax    = Vector2.one;
        rt.offsetMin    = Vector2.zero;
        rt.offsetMax    = Vector2.zero;
        var tmp         = go.AddComponent<TextMeshProUGUI>();
        tmp.text        = text;
        tmp.fontSize    = fontSize;
        tmp.color       = color;
        tmp.alignment   = align;
        tmp.enableWordWrapping = false;
        return tmp;
    }

    // ── Sprite generation ─────────────────────────────────────────────────────

    // Anti-aliased circle, alpha = 1 inside, 0 outside with 1-pixel feather.
    static Sprite MakeCircleSprite(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.ARGB32, false);
        var px  = new Color[size * size];
        float r = size * 0.5f;
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float dx   = x - r + 0.5f;
            float dy   = y - r + 0.5f;
            float dist = Mathf.Sqrt(dx * dx + dy * dy);
            float a    = Mathf.Clamp01(r - dist);
            px[y * size + x] = new Color(1f, 1f, 1f, a);
        }
        tex.SetPixels(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), Vector2.one * 0.5f, size);
    }

    static Sprite MakeSquareSprite()
    {
        var tex = new Texture2D(4, 4, TextureFormat.ARGB32, false);
        var px  = new Color[16];
        for (int i = 0; i < 16; i++) px[i] = Color.white;
        tex.SetPixels(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 4, 4), Vector2.one * 0.5f, 4f);
    }

    // ── Animal colour map ─────────────────────────────────────────────────────

    static Color BirdColor(AnimalType type) => type switch
    {
        AnimalType.Cluck  => new Color(1.00f, 0.87f, 0.10f),
        AnimalType.Bessie => new Color(1.00f, 0.48f, 0.70f),
        _                 => new Color(0.78f, 0.78f, 0.83f),
    };

    // ── EventSystem ───────────────────────────────────────────────────────────

    static void EnsureEventSystem()
    {
        if (FindAnyObjectByType<EventSystem>() != null) return;
        var go = new GameObject("EventSystem");
        go.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
        go.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
        go.AddComponent<StandaloneInputModule>();
#endif
    }
}
