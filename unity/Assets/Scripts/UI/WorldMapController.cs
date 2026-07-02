using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// Sunrise Meadows (World 1) world-map level select. Self-contained: builds its own Canvas
// (ScreenSpaceOverlay, sortingOrder 300 — same tier LevelSelectController used) in Awake().
// Shows on GameState.Idle; hides on any other state — same lifecycle as LevelSelectController
// and MainMenuController.
//
// ARCHITECTURE NOTE — deviates from the literal spec on purpose, flagged here rather than
// silently: the spec described literal world-space SpriteRenderers/Transform positions
// (background Z=10, marker "Button"s at given X/Y) but Unity's Button/GraphicRaycaster only
// work through uGUI — a SpriteRenderer has no click support of its own. Implemented instead as
// a ScreenSpaceOverlay Canvas with UI Image+Button markers, which (a) makes "OnClick" a real,
// standard Button rather than hand-rolled hit-testing, and (b) keeps this screen consistent
// with every other menu in the project (MainMenu/LevelSelect/HUD are all Canvas-based) and
// avoids fighting CatapultLauncher for ownership of the world-space camera.
//
public class WorldMapController : MonoBehaviour
{
    public static WorldMapController Instance { get; private set; }

    public const int LevelCount = 18; // Sunrise Meadows / World 1

    // LAYOUT NOTE (2026-07-16 mockup pass) — PathPositions below are measured directly from the
    // user-supplied SunriseMeadows_New.png concept mockup (1280x720), converted to this canvas's
    // 1920x1080 reference resolution via a uniform 1.5x scale (both share a 16:9 aspect, and the
    // background Image stretches non-aspect-preserving to fill the canvas, so this mapping is
    // exact regardless of the sprite's imported PPU). Pin *positions* were measured with an
    // automated color-blob detector cross-checked against pixel-grid crops; pin *ordering* (which
    // pin = which level number) is my best-guess left-to-right path traversal — NOT verified
    // against a hand-traced path, since the mockup's winding trail is hard to disambiguate from a
    // flat image alone. Flagging per the project's established "don't silently guess" convention:
    // verify in-Editor and tell me if any level numbers should swap.
    private static readonly Vector2[] PathPositions =
    {
        new(-366.3f, -109.2f), new(-343.4f, -176.3f), new(-343.2f,   32.3f), new(-221.3f,   66.3f),
        new(-218.6f, -221.4f), new(-166.1f, -126.5f), new(-125.0f,  149.1f), new(-113.7f,   35.4f),
        new( -75.3f, -248.7f), new( -19.8f,  -77.7f), new(  19.2f,   43.1f), new(  31.4f, -195.9f),
        new(  80.9f,  -54.6f), new( 138.0f, -222.9f), new( 177.2f,   10.1f), new( 197.0f, -273.5f),
        new( 294.9f, -117.0f), new( 448.8f, -134.6f),
    };

    [Header("Art (wired via FarmFury -> Wire Scene References)")]
    [SerializeField] private Sprite _backgroundSprite;      // SunriseMeadows.png
    [SerializeField] private Sprite _lockedSprite;          // LevelMarker_Locked.png
    [SerializeField] private Sprite _unlockedSprite;        // LevelMarker_Unlocked.png
    [SerializeField] private Sprite _star1Sprite;           // LevelMarker_1star.png
    [SerializeField] private Sprite _star2Sprite;           // no dedicated art yet — falls back to 3-star
    [SerializeField] private Sprite _star3Sprite;           // LevelMarker_3stars.png
    [SerializeField] private Sprite _playerPositionSprite;  // PlayerPosition.png
    [SerializeField] private Sprite _nextLevelButtonSprite; // Btn_nextlevel.png
    [SerializeField] private Sprite _homeButtonSprite;      // Btn_home.png

    // MatchUpScreen art — kept as fields on THIS component rather than on the nested
    // MatchUpScreen instance itself. MatchUpScreen is a child GameObject created inside
    // BuildUI() (i.e. only exists once Awake() has actually run), and Awake() only fires at
    // Play-mode runtime or immediately after a fresh AddComponent — never just from a scene
    // being opened in the Editor. SceneSetup's batch "Wire Scene References" pass does the
    // latter, so any [SerializeField] living on that nested object would never get an art
    // reference persisted (confirmed empirically 2026-07-16 — same silent gap the old
    // LevelPreviewCard had). Fields directly on WorldMapController persist correctly (proven by
    // every field above already working), so BuildUI() threads them into MatchUpScreen.Init()
    // as parameters instead of relying on external wiring of a nested SerializeField.
    [SerializeField] private Sprite   _matchUpBackgroundSprite; // MatchUp_Background.png
    [SerializeField] private Sprite   _vsSprite;                // VS.png
    [SerializeField] private Sprite[] _animalCardSprites = new Sprite[8]; // Sprites/UI/Cards/, AnimalType-indexed
    [SerializeField] private Sprite[] _robotCardSprites  = new Sprite[2]; // Sprites/UI/Cards/, RobotType-indexed

    private GameObject       _panel;
    private Sprite           _squareSpr;
    private readonly LevelMarker[] _markers = new LevelMarker[LevelCount];
    private RectTransform    _playerIndicatorRT;
    private Coroutine        _bobRoutine;
    private int              _lastHighestUnlocked = -1;
    private int              _highestUnlocked;
    private MatchUpScreen    _matchUpScreen;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance   = this;
        _squareSpr = MakeSquareSprite();
        BuildUI();
    }

    void Start()
    {
        if (GameManager.Instance == null) return;
        GameManager.Instance.OnStateChanged += OnStateChanged;
        if (GameManager.Instance.State == GameState.Idle &&
            FindAnyObjectByType<CatapultLauncher>() == null)
            ShowPanel();
    }

    void OnDestroy()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.OnStateChanged -= OnStateChanged;
    }

    void OnStateChanged(GameState state)
    {
        if (state == GameState.Idle) ShowPanel();
        else                         HidePanel();
    }

    public void Show() => ShowPanel();

    void ShowPanel()
    {
        bool needsSlide = RefreshMarkers();
        _panel.SetActive(true);
        if (_bobRoutine != null) StopCoroutine(_bobRoutine);
        _bobRoutine = StartCoroutine(IndicatorRoutine(needsSlide));
    }

    void HidePanel()
    {
        _matchUpScreen?.Hide();
        if (_bobRoutine != null) { StopCoroutine(_bobRoutine); _bobRoutine = null; }
        _panel.SetActive(false);
    }

    // ── Markers ───────────────────────────────────────────────────────────────

    // Refreshes lock/star art on every marker and reports whether the player's furthest-
    // unlocked level changed since the last refresh (i.e. they just won a level) — the caller
    // uses this to decide whether the position indicator should slide to its new pin before
    // bobbing, per the requested flow: "the marker moves to the next pin and hovers ... until
    // NEXT LEVEL". Does NOT move the indicator itself — IndicatorRoutine owns that, so a single
    // coroutine is ever responsible for _playerIndicatorRT's position at a time.
    bool RefreshMarkers()
    {
        int highestUnlocked = 0;
        for (int i = 0; i < LevelCount; i++)
        {
            bool unlocked = IsUnlocked(i);
            int  stars    = ScoreManager.GetBestStars(i);
            _markers[i].Refresh(unlocked, stars,
                _lockedSprite, _unlockedSprite, _star1Sprite, _star2Sprite, _star3Sprite);
            if (unlocked) highestUnlocked = i;
        }

        _highestUnlocked = highestUnlocked;
        bool firstShow = _lastHighestUnlocked < 0;
        bool changed   = highestUnlocked != _lastHighestUnlocked;
        _lastHighestUnlocked = highestUnlocked;
        return changed && !firstShow;
    }

    // Level 1 always unlocked; level N unlocks once level N-1 has >=1 star.
    static bool IsUnlocked(int levelIndex) =>
        levelIndex == 0 || ScoreManager.GetBestStars(levelIndex - 1) > 0;

    void OnMarkerTapped(int levelIndex)
    {
        if (!IsUnlocked(levelIndex)) { _markers[levelIndex].PlayLockedShake(); return; }
        _matchUpScreen.Show(levelIndex);
    }

    // NEXT LEVEL button (bottom-left, per mockup) — shortcut to the matchup screen for
    // whichever level the bobbing indicator is currently sitting on, without needing to tap
    // the pin itself.
    void OnNextLevelClicked() => _matchUpScreen.Show(_highestUnlocked);

    void OnBackClicked()
    {
        HidePanel();
        MainMenuController.Instance?.Show();
    }

    // ── Player position indicator (slides to the current pin, then bobs there) ──

    // Single coroutine owns _playerIndicatorRT for its whole lifetime so the slide-in and the
    // idle bob never fight over the same RectTransform in the same frame. If slideFirst is
    // true (the player just won a level and unlocked a new pin), it eases from wherever the
    // indicator currently sits to the new pin before starting to bob; otherwise it snaps
    // straight there (first-ever show, or reopening the map with no change).
    IEnumerator IndicatorRoutine(bool slideFirst)
    {
        if (_playerIndicatorRT == null) yield break;
        Vector2 basePos = _markers[_highestUnlocked].AnchoredPosition + new Vector2(0f, 70f);

        if (slideFirst)
        {
            Vector2 start = _playerIndicatorRT.anchoredPosition;
            const float slideDuration = 0.7f;
            float elapsed = 0f;
            while (elapsed < slideDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / slideDuration);
                _playerIndicatorRT.anchoredPosition = Vector2.Lerp(start, basePos, t);
                yield return null;
            }
        }
        _playerIndicatorRT.anchoredPosition = basePos;

        const float amplitude = 15f; // spec: "+0.15" world units * 100px/unit
        const float half      = 0.6f;
        while (true)
        {
            yield return MoveIndicatorBy(basePos, amplitude, half);
            yield return MoveIndicatorBy(basePos, -amplitude, half);
        }
    }

    IEnumerator MoveIndicatorBy(Vector2 basePos, float deltaY, float duration)
    {
        float startY  = _playerIndicatorRT.anchoredPosition.y;
        float targetY = basePos.y + deltaY;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t   = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            _playerIndicatorRT.anchoredPosition = new Vector2(basePos.x, Mathf.Lerp(startY, targetY, t));
            yield return null;
        }
        _playerIndicatorRT.anchoredPosition = new Vector2(basePos.x, targetY);
    }

    // ── UI construction ────────────────────────────────────────────────────────

    void BuildUI()
    {
        var cvGO        = new GameObject("WorldMapCanvas");
        cvGO.transform.SetParent(transform, false);
        var cv          = cvGO.AddComponent<Canvas>();
        cv.renderMode   = RenderMode.ScreenSpaceOverlay;
        cv.sortingOrder = 300;
        var cs                 = cvGO.AddComponent<CanvasScaler>();
        cs.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        cs.referenceResolution = new Vector2(1920f, 1080f);
        cs.screenMatchMode     = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        cs.matchWidthOrHeight  = 0.5f;
        cvGO.AddComponent<GraphicRaycaster>();
        _panel = cvGO;
        Transform root = cvGO.transform;

        // ── Background — SunriseMeadows.png, 1920x1080, fills exactly ────────
        var bgGO = new GameObject("Background");
        bgGO.transform.SetParent(root, false);
        var bgRT = bgGO.AddComponent<RectTransform>();
        bgRT.anchorMin = Vector2.zero;
        bgRT.anchorMax = Vector2.one;
        bgRT.offsetMin = bgRT.offsetMax = Vector2.zero;
        var bgImg = bgGO.AddComponent<Image>();
        bgImg.sprite         = _backgroundSprite != null ? _backgroundSprite : _squareSpr;
        bgImg.color          = _backgroundSprite != null ? Color.white : new Color(0.55f, 0.75f, 0.55f);
        bgImg.preserveAspect = false;

        // ── Map content — centred; anchoredPosition IS PathPositions[i]*100 directly ─
        var mapGO = new GameObject("MapContent");
        mapGO.transform.SetParent(root, false);
        var mapRT = mapGO.AddComponent<RectTransform>();
        mapRT.anchorMin        = new Vector2(0.5f, 0.5f);
        mapRT.anchorMax        = new Vector2(0.5f, 0.5f);
        mapRT.pivot            = new Vector2(0.5f, 0.5f);
        mapRT.anchoredPosition = Vector2.zero;
        mapRT.sizeDelta        = Vector2.zero;

        for (int i = 0; i < LevelCount; i++)
        {
            var go = new GameObject($"Marker_{i + 1}");
            go.transform.SetParent(mapRT, false);
            var marker = go.AddComponent<LevelMarker>();
            marker.Init(i, PathPositions[i], _squareSpr, OnMarkerTapped);
            _markers[i] = marker;
        }

        // ── Player position indicator ────────────────────────────────────────
        var pGO = new GameObject("PlayerPositionIndicator");
        pGO.transform.SetParent(mapRT, false);
        _playerIndicatorRT = pGO.AddComponent<RectTransform>();
        _playerIndicatorRT.anchorMin = new Vector2(0.5f, 0.5f);
        _playerIndicatorRT.anchorMax = new Vector2(0.5f, 0.5f);
        _playerIndicatorRT.pivot     = new Vector2(0.5f, 0.5f);
        _playerIndicatorRT.sizeDelta = new Vector2(56f, 56f);
        var pImg = pGO.AddComponent<Image>();
        pImg.sprite         = _playerPositionSprite != null ? _playerPositionSprite : _squareSpr;
        pImg.color          = _playerPositionSprite != null ? Color.white : new Color(1f, 0.85f, 0.2f);
        pImg.preserveAspect = true;
        pImg.raycastTarget  = false;

        // ── Title — dropped (2026-07-16 mockup pass): SunriseMeadows.png now bakes the
        // "SUNRISE MEADOWS" banner directly into the background art, so a second TMP title
        // here would double up / clash in font with the baked one. See old git history if a
        // code-driven title is ever needed again (e.g. for a differently-worded background).

        // ── NEXT LEVEL button — bottom-left, per mockup. Shortcut to the matchup screen for
        // whichever level the bobbing indicator currently sits on ─────────────
        // CORNER-anchored, not centre-anchored with a fixed offset — the Main Menu's PLAY/
        // Settings buttons used the latter first and ended up outside the safe area (clipped
        // by the phone bezel/notch) on a real preview, since a centre-relative offset computed
        // from one flat 16:9 mockup doesn't hold across different device aspect ratios. Corner
        // anchoring keeps a fixed inset from the actual corner regardless of aspect.
        var nextGO = new GameObject("NextLevelBtn");
        nextGO.transform.SetParent(root, false);
        var nextRT = nextGO.AddComponent<RectTransform>();
        nextRT.anchorMin        = new Vector2(0f, 0f);
        nextRT.anchorMax        = new Vector2(0f, 0f);
        nextRT.pivot            = new Vector2(0f, 0.5f);
        nextRT.anchoredPosition = new Vector2(70f, 150f);
        nextRT.sizeDelta        = new Vector2(300f, 64f);
        var nextImg = nextGO.AddComponent<Image>();
        nextImg.sprite         = _nextLevelButtonSprite != null ? _nextLevelButtonSprite : _squareSpr;
        nextImg.color          = _nextLevelButtonSprite != null ? Color.white : new Color(0.85f, 0.55f, 0.05f);
        nextImg.preserveAspect = true;
        var nextBtn = nextGO.AddComponent<Button>();
        nextBtn.targetGraphic = nextImg;
        var nc = nextBtn.colors;
        nc.normalColor      = Color.white;
        nc.highlightedColor = new Color(0.90f, 0.90f, 0.90f);
        nc.pressedColor     = new Color(0.70f, 0.70f, 0.70f);
        nextBtn.colors      = nc;
        nextBtn.onClick.AddListener(OnNextLevelClicked);

        // ── Home button — bottom-right, per mockup. Returns to main menu ─────
        // Corner-anchored — see NEXT LEVEL button comment above for why.
        var homeGO = new GameObject("HomeBtn");
        homeGO.transform.SetParent(root, false);
        var homeRT = homeGO.AddComponent<RectTransform>();
        homeRT.anchorMin        = new Vector2(1f, 0f);
        homeRT.anchorMax        = new Vector2(1f, 0f);
        homeRT.pivot            = new Vector2(0.5f, 0.5f);
        homeRT.anchoredPosition = new Vector2(-160f, 160f);
        homeRT.sizeDelta        = new Vector2(84f, 84f);
        var homeImg = homeGO.AddComponent<Image>();
        homeImg.sprite         = _homeButtonSprite != null ? _homeButtonSprite : _squareSpr;
        homeImg.color          = _homeButtonSprite != null ? Color.white : new Color(0.12f, 0.14f, 0.22f, 0.85f);
        homeImg.preserveAspect = true;
        var homeBtn = homeGO.AddComponent<Button>();
        homeBtn.targetGraphic = homeImg;
        var hc = homeBtn.colors;
        hc.normalColor      = Color.white;
        hc.highlightedColor = new Color(0.90f, 0.90f, 0.90f);
        hc.pressedColor     = new Color(0.70f, 0.70f, 0.70f);
        homeBtn.colors      = hc;
        homeBtn.onClick.AddListener(OnBackClicked);

        // ── Matchup screen overlay — added last so it renders on top ────────
        var matchGO = new GameObject("MatchUpScreen");
        matchGO.transform.SetParent(root, false);
        _matchUpScreen = matchGO.AddComponent<MatchUpScreen>();
        _matchUpScreen.Init(_squareSpr, _matchUpBackgroundSprite, _vsSprite, _animalCardSprites, _robotCardSprites);

        _panel.SetActive(false);
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
}
