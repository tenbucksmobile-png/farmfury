using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

// Phase 2.1 — HUD: bird queue (top-left cards, animated), top-right button row (top-right
// corner). Creates its own Canvas + all child elements procedurally in Awake(); nothing needs
// to be pre-wired.
// The top-centre score readout ("0" box) was removed 2026-07-26 (user request — read as an
// unstyled placeholder over the sky art) and never came back; score is still shown on the
// Level Complete/Failed panels. The top-right pause button was removed in that same pass, then
// reinstated later the same day as part of a proper 3-button row (Quit/Mute/Pause) using real
// icon art instead of the old plain grey circle — see BuildTopRightButtons().
// Place on any scene GO (SceneSetup creates a "HUD" GO for this).
public class HUDController : MonoBehaviour
{
    public static HUDController Instance { get; private set; }

    // UI leaf references set during BuildCanvas()
    private RectTransform   _birdQueueRoot;

    // Parallel lists: card RectTransforms + their base X positions
    private readonly List<RectTransform> _birdIcons   = new();
    private readonly List<float>         _birdIconsX  = new();

    private LevelLoader _levelLoader;
    private bool        _isPaused;

    // Generated once per HUDController lifetime
    private Sprite _circleSpr;
    private Sprite _squareSpr;

    // Card sprites: indexed by (int)AnimalType, wired via SceneSetup
    [SerializeField] private Sprite[] _cardSprites = new Sprite[8];

    // Card layout constants — Angry Birds style: big active card, overlapping queue cards
    private const float CardActiveW  = 200f;
    private const float CardActiveH  = 260f;
    private const float CardQueueW   = 155f;
    private const float CardQueueH   = 202f;
    private const float CardGap      = -55f;  // negative = cards overlap each other

    private const float BobAmp  = 3.5f;  // pixels
    private const float BobFreq = 1.8f;  // Hz

    // ── Level Complete panel (Phase 2.2) ──────────────────────────────────────

    private GameObject                _lcPanel;
    private TextMeshProUGUI           _lcScoreText;
    // 3 slots: the real star rating (existing GetBestStars() 0-3 scale). The old 4th slot that
    // always popped last as a "you leveled up" reveal (and its accompanying LEVEL UP! text) was
    // removed 2026-07-26 — that beat is now the flashing _levelUpStarSprite button below, which
    // both reads as the level-up moment AND is the panel's primary tap target.
    private readonly RectTransform[]  _lcStarRTs  = new RectTransform[3];
    private readonly Image[]          _lcStarImgs = new Image[3];
    private Coroutine                  _lcAnim;
    private RectTransform               _lcLevelUpStarRT;
    private Coroutine                  _lcLevelUpPulse;

    private static readonly Color StarFilled = new Color(1.00f, 0.82f, 0.00f);
    private static readonly Color StarEmpty  = new Color(0.38f, 0.38f, 0.42f);

    [SerializeField] private Sprite _lcTitleSprite; // LevelComplete.png
    [SerializeField] private Sprite _starSprite;    // ScoreStars.png — tinted gold/grey per slot
    // Replaces the old Btn_play slot on the Level Complete panel only (2026-07-26) — a big
    // gold "LEVEL UP" star (art has the label baked in) that pulses continuously to draw the
    // tap, instead of a plain static play icon. Tapping it does exactly what Btn_play used to:
    // GameManager.LoadMenu() back to the Sunrise Meadows world map flow.
    [SerializeField] private Sprite _levelUpStarSprite; // Levelup.png

    // ── Level Failed panel (Phase 2.3) ────────────────────────────────────────

    private GameObject       _lfPanel;
    private TextMeshProUGUI  _lfScoreText;

    [SerializeField] private Sprite _lfTitleSprite; // LevelFailed.png

    // Shared art wired via SceneSetup (Assets/Sprites/UI/MatchUp/ + Assets/Sprites/UI/Icon/) —
    // both the LevelComplete and LevelFailed panels use the same backdrop/button assets.
    // Both BuildLevelCompletePanel()/BuildLevelFailedPanel() fall back to a plain procedural
    // box/text if any of these are unwired, so neither panel ever renders blank.
    [SerializeField] private Sprite _scoreboardSprite; // Scoreboard.png (backdrop)
    [SerializeField] private Sprite _playButtonSprite; // Btn_play.png
    [SerializeField] private Sprite _homeButtonSprite;  // Btn_home.png

    // ── Pause menu (Phase 2.4) ────────────────────────────────────────────────

    private GameObject _pausePanel;
    private Image      _musicToggleImg;
    private Image      _sfxToggleImg;

    [SerializeField] private Sprite _quitButtonSprite; // Btn_quite.png ("QUIT" baked into the art)

    private static readonly Color ToggleOnColor  = new Color(0.12f, 0.14f, 0.22f);
    private static readonly Color ToggleOffColor = new Color(0.40f, 0.40f, 0.44f);

    // ── Top-right button row (2026-07-26) ─────────────────────────────────────
    // Re-adds a pause trigger (removed along with the old score readout earlier the same day)
    // plus a combined music+SFX mute toggle and a direct quit — all three real icon buttons,
    // top-right corner, nicely spaced. Order left-to-right: Quit, Mute, Pause (Pause sits
    // right in the corner per "insert Btn_pause in the top right", the other two placed
    // progressively further left/"next to it").
    [SerializeField] private Sprite _pauseButtonSprite; // Btn_pause.png
    [SerializeField] private Sprite _musicOnSprite;     // Btn_music.png
    [SerializeField] private Sprite _musicOffSprite;    // NoSound.png
    private Image          _topMuteImg;
    private RectTransform  _topRightRoot;

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
        if (_levelLoader != null)
            _levelLoader.OnBirdConsumed -= RefreshBirdIcons;
    }

    void Update() => AnimateBirdIcons();

    // ── Event handlers ────────────────────────────────────────────────────────

    void OnLevelStarted(LevelData _) => RefreshBirdIcons();

    void OnStateChanged(GameState state)
    {
        if (_isPaused && (state == GameState.LevelComplete || state == GameState.LevelFailed))
            SetPaused(false);

        switch (state)
        {
            case GameState.LevelComplete:
                HideLevelFailedPanel();
                ShowLevelCompletePanel();
                SetTopBarVisible(false);
                break;
            case GameState.LevelFailed:
                HideLevelCompletePanel();
                ShowLevelFailedPanel();
                SetTopBarVisible(false);
                break;
            default:
                HideLevelCompletePanel();
                HideLevelFailedPanel();
                HidePausePanel();
                SetTopBarVisible(true);
                break;
        }
    }

    // The bird-queue cards and the top-right button row both live top-of-screen; hidden
    // whenever a full-screen end-of-level panel (LevelComplete/LevelFailed) is showing, same
    // reasoning as the old score readout used to get (self-contained panels, nothing to
    // pause/mute-from-here on a finished level). _birdQueueRoot was actually missing from this
    // toggle until 2026-07-26 (user-reported leftover animal cards still visible behind the
    // Level Complete panel) — _topRightRoot added in the same pass that reinstated the button
    // row, so it doesn't repeat that bug.
    void SetTopBarVisible(bool visible)
    {
        if (_birdQueueRoot != null) _birdQueueRoot.gameObject.SetActive(visible);
        if (_topRightRoot  != null) _topRightRoot.gameObject.SetActive(visible);
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

        // Screen-edge elements (the bird queue) anchor inside this instead of the raw canvas
        // rect, so they respect notch/rounded-corner safe insets. Without it they're positioned
        // against the full device screen and can render into — or get visually clipped by —
        // the unsafe edge zone (e.g. top-left cards overlapping a notch/corner).
        var safeArea = BuildSafeArea(root.transform);

        BuildBirdQueueArea(safeArea);
        BuildTopRightButtons(safeArea);           // Quit / Mute / Pause row (2026-07-26)
        BuildLevelCompletePanel(root.transform);  // full-screen panels stay on the raw canvas
        BuildLevelFailedPanel(root.transform);    // hidden until LevelFailed state fires
        BuildPausePanel(root.transform);
    }

    RectTransform BuildSafeArea(Transform canvas)
    {
        var rt = MakeFullScreenRect(canvas, "SafeArea");
        ApplySafeArea(rt);
        return rt;
    }

    static void ApplySafeArea(RectTransform rt)
    {
        if (Screen.width <= 0 || Screen.height <= 0) return;
        Rect safe = Screen.safeArea;
        Vector2 min = safe.position;
        Vector2 max = safe.position + safe.size;
        min.x /= Screen.width;  min.y /= Screen.height;
        max.x /= Screen.width;  max.y /= Screen.height;
        rt.anchorMin = min;
        rt.anchorMax = max;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    // Bird queue: anchored 2% from left edge and 4% from bottom edge of actual screen —
    // works at any landscape resolution / DPI without pixel-offset guesswork.
    void BuildBirdQueueArea(Transform canvas)
    {
        var rt         = MakeRect(canvas, "BirdQueue",
                             anchorMin: new Vector2(0.02f, 1f),
                             anchorMax: new Vector2(0.02f, 1f),
                             pivot:     new Vector2(0f, 1f),    // top-left corner anchors to top-left of screen
                             pos:       new Vector2(0f, -12f),  // 12px inset from top edge
                             size:      new Vector2(700f, 280f));
        _birdQueueRoot = rt;
    }

    // Quit / Mute / Pause row — top-right corner, nicely spaced (2026-07-26). Reinstates pause
    // access (removed earlier the same day along with the old score readout) alongside a new
    // combined music+SFX mute toggle and a direct quit, all using real icon art instead of the
    // old plain grey circle. Order left-to-right: Quit, Mute, Pause — Pause sits flush in the
    // screen corner, per "insert Btn_pause in the top right", with Mute and Quit placed
    // progressively further left/"next to it".
    void BuildTopRightButtons(Transform canvas)
    {
        const float btnSize = 64f;
        const float gap     = 14f;
        const float inset   = 16f;

        var rootGO = new GameObject("TopRightButtons");
        rootGO.transform.SetParent(canvas, false);
        _topRightRoot = rootGO.AddComponent<RectTransform>();
        _topRightRoot.anchorMin        = new Vector2(1f, 1f);
        _topRightRoot.anchorMax        = new Vector2(1f, 1f);
        _topRightRoot.pivot            = new Vector2(1f, 1f);
        _topRightRoot.anchoredPosition = new Vector2(-inset, -inset);
        _topRightRoot.sizeDelta        = new Vector2(btnSize * 3f + gap * 2f, btnSize);

        // Pause — rightmost, flush with the corner.
        var pauseImg = MakeImage(_topRightRoot, "PauseBtn",
                          _pauseButtonSprite != null ? _pauseButtonSprite : _squareSpr,
                          _pauseButtonSprite != null ? Color.white : new Color(0f, 0f, 0f, 0.62f),
                          anchorMin: new Vector2(1f, 0.5f), anchorMax: new Vector2(1f, 0.5f),
                          pivot:     new Vector2(1f, 0.5f),
                          pos:       new Vector2(0f, 0f),
                          size:      new Vector2(btnSize, btnSize));
        var pauseBtn = pauseImg.gameObject.AddComponent<Button>();
        pauseBtn.targetGraphic = pauseImg;
        StyleTopButtonColors(pauseBtn);
        pauseBtn.onClick.AddListener(OnPauseClicked);

        // Mute — middle. Starts showing whichever icon matches the persisted audio state.
        var muteImg = MakeImage(_topRightRoot, "MuteBtn",
                          _musicOnSprite != null ? _musicOnSprite : _squareSpr,
                          _musicOnSprite != null ? Color.white : new Color(0f, 0f, 0f, 0.62f),
                          anchorMin: new Vector2(1f, 0.5f), anchorMax: new Vector2(1f, 0.5f),
                          pivot:     new Vector2(1f, 0.5f),
                          pos:       new Vector2(-(btnSize + gap), 0f),
                          size:      new Vector2(btnSize, btnSize));
        _topMuteImg = muteImg;
        var muteBtn = muteImg.gameObject.AddComponent<Button>();
        muteBtn.targetGraphic = muteImg;
        StyleTopButtonColors(muteBtn);
        muteBtn.onClick.AddListener(OnTopMuteToggleClicked);

        // Quit — leftmost.
        var quitImg = MakeImage(_topRightRoot, "TopQuitBtn",
                          _quitButtonSprite != null ? _quitButtonSprite : _squareSpr,
                          _quitButtonSprite != null ? Color.white : new Color(0.75f, 0.20f, 0.12f),
                          anchorMin: new Vector2(1f, 0.5f), anchorMax: new Vector2(1f, 0.5f),
                          pivot:     new Vector2(1f, 0.5f),
                          pos:       new Vector2(-2f * (btnSize + gap), 0f),
                          size:      new Vector2(btnSize, btnSize));
        var quitBtn = quitImg.gameObject.AddComponent<Button>();
        quitBtn.targetGraphic = quitImg;
        StyleTopButtonColors(quitBtn);
        quitBtn.onClick.AddListener(OnQuitClicked);

        RefreshTopMuteIcon();
    }

    static void StyleTopButtonColors(Button b)
    {
        var c = b.colors;
        c.normalColor      = Color.white;
        c.highlightedColor = new Color(0.88f, 0.88f, 0.88f);
        c.pressedColor     = new Color(0.68f, 0.68f, 0.68f);
        b.colors = c;
    }

    // ── Bird queue (card widgets) ─────────────────────────────────────────────

    void RefreshBirdIcons()
    {
        foreach (var rt in _birdIcons) if (rt != null) Destroy(rt.gameObject);
        _birdIcons.Clear();
        _birdIconsX.Clear();

        if (_levelLoader == null) return;

        var queue = _levelLoader.BirdQueueSnapshot;
        float cursorX = 0f;

        for (int i = 0; i < queue.Length; i++)
        {
            bool  active = (i == 0);
            float w = active ? CardActiveW : CardQueueW;
            float h = active ? CardActiveH : CardQueueH;
            float x = cursorX + w * 0.5f;

            AnimalType atype = queue[i];

            // Slot container — anchored left-centre of queue root
            var slot   = new GameObject($"Card_{i}");
            slot.transform.SetParent(_birdQueueRoot, false);
            var rt     = slot.AddComponent<RectTransform>();
            rt.anchorMin        = new Vector2(0f, 0.5f);
            rt.anchorMax        = new Vector2(0f, 0.5f);
            rt.pivot            = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(x, 0f);
            rt.sizeDelta        = new Vector2(w, h);

            // Card face image
            var cardImg   = slot.AddComponent<Image>();
            Sprite cardSpr = GetCardSprite(atype);
            cardImg.sprite = cardSpr != null ? cardSpr : _squareSpr;
            cardImg.color  = cardSpr != null ? Color.white : BirdColor(atype);

            // Damage badge: top-right corner, orange pill with lightning + number
            BuildDamageBadge(rt, atype, active);

            // Dim inactive cards so the active one pops
            if (!active) cardImg.color = new Color(0.72f, 0.72f, 0.72f, 1f);

            _birdIcons.Add(rt);
            _birdIconsX.Add(x);
            cursorX += w + CardGap;
        }

        // Active card (index 0) must render on top of overlapping queue cards.
        // In Canvas, last sibling renders in front — move it to the end.
        if (_birdIcons.Count > 0 && _birdIcons[0] != null)
            _birdIcons[0].SetAsLastSibling();
    }

    // Badge enlarged + restyled 2026-07-26 (user request: "enlarge the score numbers and match
    // to the game font") — text now uses StyleAsGameNumber() (bold + black outline + gold-to-
    // orange gradient), the same treatment as the Level Complete/Failed score numbers, instead
    // of plain flat white. Badge grown to fit the bigger text without clipping.
    void BuildDamageBadge(RectTransform parent, AnimalType type, bool large)
    {
        float bw = large ? 72f : 56f;
        float bh = large ? 34f : 28f;

        var badge = MakeImage(parent, "DamageBadge", _squareSpr,
                        new Color(0.96f, 0.56f, 0.07f, 0.93f),
                        anchorMin: new Vector2(1f, 1f), anchorMax: new Vector2(1f, 1f),
                        pivot: new Vector2(1f, 1f),
                        pos: new Vector2(-3f, -3f),
                        size: new Vector2(bw, bh));

        var dmgText = MakeStretchText(badge.transform, "DmgText",
            fontSize: large ? 24f : 18f,
            text: $"⚡{GetDamage(type)}",
            color: Color.white,
            align: TextAlignmentOptions.Center);
        StyleAsGameNumber(dmgText);
    }

    Sprite GetCardSprite(AnimalType type)
    {
        int idx = (int)type;
        return (_cardSprites != null && idx < _cardSprites.Length) ? _cardSprites[idx] : null;
    }

    static int GetDamage(AnimalType type) => type switch
    {
        AnimalType.Cluck  => 15,
        AnimalType.Bessie => 50,
        AnimalType.Percy  => 20,
        AnimalType.Woolly => 12,
        AnimalType.Ducky  => 18,
        AnimalType.Horace => 35,
        AnimalType.Gerald => 55,
        AnimalType.Billy  => 25,
        _                 => 10,
    };

    // Staggered sine-wave bob; active card (first) gets larger amplitude.
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

        // Scoreboard art backdrop. Enlarged 620x363 -> 680x398 (2026-07-26, same 653:382 native
        // aspect as Scoreboard.png so preserveAspect doesn't letterbox it) — the old size left
        // the star row sitting too close to the board's own parchment edges; this gives the
        // (now 3, not 4 — see star loop below) stars more headroom without moving them at all,
        // since children anchored to box's centre don't rescale with the parent's sizeDelta.
        // Falls back to the old plain cream box if unwired so the panel never renders blank.
        Image box;
        if (_scoreboardSprite != null)
        {
            box = MakeImage(rootRT, "LCBox", _scoreboardSprite, Color.white,
                      new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                      new Vector2(0.5f, 0.5f), new Vector2(0f, -25f), new Vector2(680f, 398f));
            box.preserveAspect = true;
        }
        else
        {
            box = MakeImage(rootRT, "LCBox", _squareSpr, new Color(0.97f, 0.95f, 0.90f),
                      new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                      new Vector2(0.5f, 0.5f), new Vector2(0f, -25f), new Vector2(640f, 460f));
        }

        // Title — LevelComplete.png sign-topper, overlapping the board's top edge.
        // Falls back to gold TMP text if unwired.
        if (_lcTitleSprite != null)
        {
            var titleImg = MakeImage(box.transform, "LCTitle", _lcTitleSprite, Color.white,
                      new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                      new Vector2(0.5f, 0.5f), new Vector2(0f, 255f), new Vector2(340f, 183f));
            titleImg.preserveAspect = true;
        }
        else
        {
            MakeCentredText(box.transform, "LCTitle",
                pos: new Vector2(0f, 220f), size: new Vector2(540f, 58f),
                fontSize: 42f, text: "LEVEL COMPLETE!",
                color: new Color(0.92f, 0.60f, 0.04f));
        }

        // Three star slots — grey until animated gold, purely the real star rating (0-3, see
        // ScoreManager). The old 4th "always pops, means level up" slot was removed 2026-07-26 —
        // that beat now belongs entirely to the flashing level-up star button below.
        for (int i = 0; i < 3; i++)
        {
            float xOff = -130f + i * 130f;
            var starGO = new GameObject($"LCStar_{i}");
            starGO.transform.SetParent(box.transform, false);
            var rt = starGO.AddComponent<RectTransform>();
            rt.anchorMin        = new Vector2(0.5f, 0.5f);
            rt.anchorMax        = new Vector2(0.5f, 0.5f);
            rt.pivot            = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(xOff, 90f);
            rt.sizeDelta        = new Vector2(85f, 85f);
            var img = starGO.AddComponent<Image>();
            img.sprite         = _starSprite != null ? _starSprite : _circleSpr;
            img.color          = StarEmpty;
            img.preserveAspect = true;
            _lcStarRTs[i]  = rt;
            _lcStarImgs[i] = img;
        }

        // Score value (large)
        _lcScoreText = MakeCentredText(box.transform, "LCScore",
            pos: new Vector2(0f, 10f), size: new Vector2(460f, 64f),
            fontSize: 52f, text: "0",
            color: new Color(0.20f, 0.10f, 0.03f));
        StyleAsGameNumber(_lcScoreText);

        // Level-up star — replaces the old Btn_play slot (bottom-left, same position/size as
        // Home so the two mirror each other). Art has "LEVEL UP" baked in, so no separate text
        // label is needed. Pulses continuously (see LevelUpStarPulse) to draw the tap; tapping
        // it does exactly what Btn_play used to (OnLevelCompletePlayClicked -> world map flow).
        var levelUpBtn = MakeIconButton(box.transform, "LevelUpStarBtn", _levelUpStarSprite,
                          new Color(1.00f, 0.82f, 0.00f),
                          pos: new Vector2(-170f, -260f), size: new Vector2(130f, 130f));
        levelUpBtn.onClick.AddListener(OnLevelCompletePlayClicked);
        _lcLevelUpStarRT = levelUpBtn.GetComponent<RectTransform>();

        var homeBtn = MakeIconButton(box.transform, "HomeBtn", _homeButtonSprite,
                          new Color(1.00f, 0.55f, 0.05f),
                          pos: new Vector2(+170f, -260f), size: new Vector2(130f, 130f));
        homeBtn.onClick.AddListener(OnLevelCompleteHomeClicked);

        _lcPanel.SetActive(false);
    }

    void ShowLevelCompletePanel()
    {
        if (_lcPanel == null) return;

        int score = ScoreManager.Instance?.Score ?? 0;
        int stars = ScoreManager.Instance?.Stars ?? 0;

        _lcScoreText.text = score.ToString("N0");

        // Reset all stars to grey at normal scale before animating.
        for (int i = 0; i < 3; i++)
        {
            _lcStarRTs[i].localScale = Vector3.one;
            _lcStarImgs[i].color     = StarEmpty;
        }

        _lcPanel.SetActive(true);

        if (_lcAnim != null) StopCoroutine(_lcAnim);
        _lcAnim = StartCoroutine(AnimateStars(stars));

        if (_lcLevelUpPulse != null) StopCoroutine(_lcLevelUpPulse);
        if (_lcLevelUpStarRT != null) _lcLevelUpPulse = StartCoroutine(LevelUpStarPulse());
    }

    void HideLevelCompletePanel()
    {
        if (_lcAnim != null) { StopCoroutine(_lcAnim); _lcAnim = null; }
        if (_lcLevelUpPulse != null) { StopCoroutine(_lcLevelUpPulse); _lcLevelUpPulse = null; }
        if (_lcLevelUpStarRT != null) _lcLevelUpStarRT.localScale = Vector3.one;
        if (_lcPanel != null) _lcPanel.SetActive(false);
    }

    // Continuous "breathing" pulse (scale 1 <-> 1.12, sine wave) while the panel is up — draws
    // the eye to the level-up star since it's now the panel's primary/only forward action.
    IEnumerator LevelUpStarPulse()
    {
        const float period = 1.1f; // seconds per full pulse cycle
        while (true)
        {
            float t = (Time.unscaledTime % period) / period;
            float s = 1f + 0.12f * Mathf.Sin(t * Mathf.PI * 2f);
            _lcLevelUpStarRT.localScale = Vector3.one * s;
            yield return null;
        }
    }

    // Fired by the flashing level-up star (was Btn_play before 2026-07-26) — advances GameState
    // to Idle, which WorldMapController reacts to on its own by showing itself and sliding the
    // position indicator from the level just completed to the newly-unlocked next one (see
    // WorldMapController.RefreshMarkers/IndicatorRoutine — this already happens automatically
    // once the just-finished level's star result unlocks the next pin, no extra plumbing needed
    // here).
    void OnLevelCompletePlayClicked()
    {
        HideLevelCompletePanel();
        GameManager.Instance?.LoadMenu();
    }

    // Btn_home — same "skip the world map, land on the main menu" pattern as Level Failed's
    // Home button (see OnMenuClicked and WorldMapController.SkipToMainMenu).
    void OnLevelCompleteHomeClicked()
    {
        HideLevelCompletePanel();
        GameManager.Instance?.LoadMenu();
        WorldMapController.Instance?.SkipToMainMenu();
    }

    // ── Level Failed panel ────────────────────────────────────────────────────

    void BuildLevelFailedPanel(Transform canvas)
    {
        var rootRT  = MakeFullScreenRect(canvas, "LevelFailedPanel");
        _lfPanel    = rootRT.gameObject;
        var overlay = _lfPanel.AddComponent<Image>();
        overlay.sprite = _squareSpr;
        overlay.color  = new Color(0f, 0f, 0f, 0.55f);

        // Scoreboard art is the whole backdrop (wooden sign, blank parchment centre) —
        // falls back to the old plain cream box if unwired so the panel never renders blank.
        Image box;
        if (_scoreboardSprite != null)
        {
            box = MakeImage(rootRT, "LFBox", _scoreboardSprite, Color.white,
                      new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                      new Vector2(0.5f, 0.5f), new Vector2(0f, -25f), new Vector2(620f, 363f));
            box.preserveAspect = true;
        }
        else
        {
            box = MakeImage(rootRT, "LFBox", _squareSpr, new Color(0.96f, 0.93f, 0.90f),
                      new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                      new Vector2(0.5f, 0.5f), new Vector2(0f, -25f), new Vector2(480f, 300f));
        }

        // Title — LevelFailed.png sign-topper, overlapping the board's top edge.
        // Falls back to red TMP text if unwired.
        if (_lfTitleSprite != null)
        {
            var titleImg = MakeImage(box.transform, "LFTitle", _lfTitleSprite, Color.white,
                      new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                      new Vector2(0.5f, 0.5f), new Vector2(0f, 255f), new Vector2(340f, 183f));
            titleImg.preserveAspect = true;
        }
        else
        {
            MakeCentredText(box.transform, "LFTitle",
                pos: new Vector2(0f, 220f), size: new Vector2(440f, 54f),
                fontSize: 40f, text: "LEVEL FAILED!",
                color: new Color(0.82f, 0.14f, 0.10f));
        }

        // Score value only (no "SCORE" label), centred dead-middle of the backdrop
        _lfScoreText = MakeCentredText(box.transform, "LFScore",
            pos: new Vector2(0f, 0f), size: new Vector2(400f, 64f),
            fontSize: 52f, text: "0",
            color: new Color(0.20f, 0.10f, 0.03f));
        StyleAsGameNumber(_lfScoreText);

        // Buttons — Btn_play (try again) on the left, Btn_home (landing page) on the right.
        // Dropped clear below the board's bottom edge (board half-height 181.5 + button half-
        // height 65 = 246.5 is the minimum non-overlapping offset) so the icons sit on the dark
        // overlay instead of covering the sign art.
        var tryAgainBtn = MakeIconButton(box.transform, "TryAgainBtn", _playButtonSprite,
                              new Color(1.00f, 0.55f, 0.05f),
                              pos: new Vector2(-170f, -260f), size: new Vector2(130f, 130f));
        tryAgainBtn.onClick.AddListener(OnTryAgainClicked);

        var menuBtn = MakeIconButton(box.transform, "MenuBtn", _homeButtonSprite,
                          new Color(1.00f, 0.55f, 0.05f),
                          pos: new Vector2(+170f, -260f), size: new Vector2(130f, 130f));
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
        // LoadMenu() transitions GameState to Idle, which WorldMapController reacts to by
        // showing itself (it's the PLAY destination) — SkipToMainMenu() immediately hides it
        // again in the same call so the world map never actually flashes on screen, landing
        // cleanly on the main menu instead. Same pattern used by the Level Complete Home button.
        WorldMapController.Instance?.SkipToMainMenu();
    }

    // ── Pause menu ────────────────────────────────────────────────────────────

    void BuildPausePanel(Transform canvas)
    {
        var rootRT  = MakeFullScreenRect(canvas, "PausePanel");
        _pausePanel = rootRT.gameObject;
        var overlay = _pausePanel.AddComponent<Image>();
        overlay.sprite = _squareSpr;
        overlay.color  = new Color(0f, 0f, 0f, 0.60f);

        // Centre card — enlarged 340->480 to fit the new QUIT row below MENU; MENU and
        // everything above it keep the exact same relative position/spacing as before
        // (unchanged offsets), only the divider/toggle row shift down to make room.
        var box = MakeImage(rootRT, "PauseBox", _squareSpr, new Color(0.97f, 0.95f, 0.90f),
                      new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                      new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(380f, 480f));

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

        // QUIT — Btn_quite.png already has "QUIT" baked into the art (a labelled button, not
        // a bare icon), so it's sized/treated like the icon buttons elsewhere rather than
        // stretched to the 280-wide text-button rows above.
        var quitBtn = MakeIconButton(box.transform, "QuitBtn", _quitButtonSprite,
                          new Color(0.75f, 0.20f, 0.12f),
                          pos: new Vector2(0f, -145f), size: new Vector2(90f, 90f));
        quitBtn.onClick.AddListener(OnQuitClicked);

        // Divider
        MakeImage(box.transform, "PauseDivider", _squareSpr, new Color(0.72f, 0.70f, 0.68f),
              new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
              new Vector2(0.5f, 0.5f), new Vector2(0f, -185f), new Vector2(300f, 2f));

        // Toggle buttons
        var musicBtn = MakePanelButton(box.transform, "MusicToggle", "MUSIC",
                           new Vector2(-80f, -213f), new Vector2(130f, 44f));
        _musicToggleImg = musicBtn.targetGraphic as Image;
        musicBtn.onClick.AddListener(OnMusicToggleClicked);

        var sfxBtn = MakePanelButton(box.transform, "SfxToggle", "SFX",
                         new Vector2(+80f, -213f), new Vector2(130f, 44f));
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

    // Quits the standalone app; in the Editor, Application.Quit() is a no-op, so this stops
    // Play mode instead — the same idiom used throughout Unity projects for an in-game quit
    // button that also behaves sensibly while testing in the Editor.
    void OnQuitClicked()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
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

    // Top-right mute button (2026-07-26) — one icon toggling BOTH music and SFX together,
    // distinct from the pause panel's two separate Music/SFX toggles above. Swaps sprite
    // (Btn_music.png <-> NoSound.png) rather than tinting, per explicit request.
    void OnTopMuteToggleClicked()
    {
        // Currently-on means both are enabled; toggling flips both to the same new state.
        bool newState = !(AudioManager.MusicEnabled && AudioManager.SfxEnabled);
        AudioManager.SetMusicEnabled(newState);
        AudioManager.SetSfxEnabled(newState);
        RefreshTopMuteIcon();
    }

    // Re-derives the icon from actual AudioManager state (rather than a locally-tracked flag)
    // so it stays correct even if the pause panel's separate Music/SFX toggles are used instead
    // — "on" only when both are enabled, otherwise the muted icon.
    void RefreshTopMuteIcon()
    {
        if (_topMuteImg == null) return;
        bool on = AudioManager.MusicEnabled && AudioManager.SfxEnabled;
        Sprite spr = on ? _musicOnSprite : _musicOffSprite;
        if (spr != null) _topMuteImg.sprite = spr;
    }

    // Stagger: star 1 at 0.3s, star 2 at 0.75s, star 3 at 1.2s. All 3 slots are now the real
    // star rating (0-3) — the old forced 4th "always pops, means level up" slot was removed
    // 2026-07-26; that beat now belongs to the flashing level-up star button instead.
    IEnumerator AnimateStars(int earnedCount)
    {
        for (int i = 0; i < 3; i++)
        {
            yield return new WaitForSecondsRealtime(0.30f + i * 0.45f);
            if (i >= earnedCount) continue;
            StartCoroutine(PopStar(i));
        }
        _lcAnim = null;
    }

    // Bounce scale 1→1.42→1, simultaneous grey→gold colour transition.
    IEnumerator PopStar(int idx)
    {
        var rt  = _lcStarRTs[idx];
        var img = _lcStarImgs[idx];
        if (rt == null || img == null) yield break;

        float elapsed = 0f;
        const float dur = 0.38f;
        while (elapsed < dur)
        {
            float t      = Mathf.Clamp01(elapsed / dur);
            rt.localScale = Vector3.one * StarBounce(t);
            img.color     = Color.Lerp(StarEmpty, StarFilled,
                                Mathf.SmoothStep(0f, 1f, t * 1.4f));
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
        rt.localScale = Vector3.one;
        img.color     = StarFilled;
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

    // Approximates the bold, black-outlined, gold-to-orange gradient cartoon lettering used in
    // the game's own art (LevelFailed.png/LevelComplete.png titles) using only the default TMP
    // font's built-in outline + vertex-gradient support. No custom font/sprite asset exists in
    // this project (TextMesh Pro ships only LiberationSans SDF) — this is the closest match
    // achievable without importing new font or sprite art.
    static void StyleAsGameNumber(TextMeshProUGUI tmp)
    {
        tmp.color               = Color.white; // must be white or it tints the gradient below
        tmp.fontStyle            = FontStyles.Bold;
        tmp.enableVertexGradient = true;
        tmp.colorGradient        = new VertexGradient(
            new Color(1.00f, 0.87f, 0.40f), new Color(1.00f, 0.87f, 0.40f),
            new Color(0.95f, 0.55f, 0.05f), new Color(0.95f, 0.55f, 0.05f));
        var mat = tmp.fontMaterial; // instance, safe to edit without affecting other TMP text
        mat.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.22f);
        mat.SetColor(ShaderUtilities.ID_OutlineColor, Color.black);
        tmp.fontMaterial = mat;
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

    // Square icon button (e.g. Btn_play / Btn_home) — falls back to a plain orange
    // square with no icon if sprite is unwired, same fallback pattern MainMenuController uses.
    Button MakeIconButton(Transform parent, string name, Sprite sprite, Color fallbackColor,
        Vector2 pos, Vector2 size)
    {
        var img = MakeImage(parent, name, sprite != null ? sprite : _squareSpr,
                      sprite != null ? Color.white : fallbackColor,
                      new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                      new Vector2(0.5f, 0.5f), pos, size);
        img.preserveAspect = true;
        var btn           = img.gameObject.AddComponent<Button>();
        btn.targetGraphic = img;
        var cols              = btn.colors;
        cols.normalColor      = Color.white;
        cols.highlightedColor = new Color(0.88f, 0.88f, 0.88f);
        cols.pressedColor     = new Color(0.68f, 0.68f, 0.68f);
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
