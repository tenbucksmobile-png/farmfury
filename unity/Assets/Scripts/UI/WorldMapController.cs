using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// Sunrise Meadows (World 1) world-map level select. Self-contained: builds its own Canvas
// (ScreenSpaceOverlay, sortingOrder 300) in Awake(). Shows on GameState.Idle; hides on any
// other state.
//
// The old grid-based LevelSelectController used this exact same sortingOrder and the same
// GameState.Idle show/hide lifecycle, and was never actually disabled once this screen
// superseded it for World 1 (2026-07-15) — the two silently raced to show themselves on every
// Idle transition until LevelSelectController was deleted entirely (2026-07-26, user-reported:
// the old "SELECT LEVEL" grid kept appearing instead of/behind this screen). If World 2+ needs
// its own map, build it following this component's pattern rather than resurrecting that one.
//
// ARCHITECTURE NOTE — deviates from the literal spec on purpose, flagged here rather than
// silently: the spec described literal world-space SpriteRenderers/Transform positions
// (background Z=10, marker "Button"s at given X/Y) but Unity's Button/GraphicRaycaster only
// work through uGUI — a SpriteRenderer has no click support of its own. Implemented instead as
// a ScreenSpaceOverlay Canvas with UI Image+Button markers, which (a) makes "OnClick" a real,
// standard Button rather than hand-rolled hit-testing, and (b) keeps this screen consistent
// with every other menu in the project (MainMenu/HUD are all Canvas-based) and avoids fighting
// CatapultLauncher for ownership of the world-space camera.
//
public class WorldMapController : MonoBehaviour
{
    public static WorldMapController Instance { get; private set; }

    public const int LevelCount = 18; // Sunrise Meadows / World 1

    // LAYOUT NOTE (2026-07-24, re-traced to include the full visible S-curve) — every previous
    // trace (Round 12, Round 13/2026-07-20) restricted itself to the LOWER dip-and-rise portion
    // of the path (y ≈ 660-833 in source-image pixels) and never went up near the windmill/ruins
    // bend, because every purely graph-distance-based method (geodesic BFS, skeleton shortest-
    // path) independently found that region to be a dead-end spur off the main route rather than
    // a through-path — confirmed multiple times by re-deriving from scratch and getting the same
    // answer. That answer was topologically defensible but wrong for this purpose: rendering it
    // (see docs/HISTORY.md) produced markers bunched in a visually flat row hugging the bottom of
    // the map, ignoring the prominent bend that's clearly part of the path in the art — reported
    // directly against a screenshot. Re-traced by hand this time (pixel-sampling directly against
    // the source image, verifying each candidate point's actual RGB against the same tan-path
    // color test used by the automated attempts, since eyeballing coordinates against the art
    // alone had already caused mis-reads earlier in this same investigation) to explicitly
    // include the bend: pond -> rises up past the windmill/ruins bend -> back down through the
    // lower dip -> tail to the fortress. Resampled to 18 evenly arc-length-spaced points
    // (~100-107 units apart, vs. markers' own ~80-unit width at the enlarged size below — some
    // touching is expected and fine, same reasoning as the MatchUp screen's card/VS spacing).
    // Confirmed by rendering all 18 points plus the connecting line back onto the source image.
    // Pin ordering (level 1 = pond/barn end, level 18 = fortress end) follows the path's own
    // visible start/end, unchanged from every previous round.
    private static readonly Vector2[] PathPositions =
    {
        new(-368.0f, -240.0f), new(-287.1f, -170.7f), new(-243.9f, -74.5f), new(-209.3f, 23.9f),
        new(-109.5f, 37.4f), new(-20.6f, -17.0f), new(21.6f, -110.4f), new(-44.1f, -187.0f),
        new(-144.1f, -223.5f), new(-234.0f, -254.0f), new(-133.7f, -285.6f), new(-27.5f, -290.1f),
        new(77.1f, -271.5f), new(180.9f, -246.8f), new(284.3f, -220.2f), new(385.4f, -186.3f),
        new(484.9f, -147.6f), new(588.0f, -120.0f),
    };

    [Header("Art (wired via FarmFury -> Wire Scene References)")]
    [SerializeField] private Sprite _backgroundSprite;      // SunriseMeadows.png
    [SerializeField] private Sprite _lockedSprite;          // LevelMarker_Locked.png
    // Every unlocked marker (any star count) now renders the same LevelMarker_tick.png,
    // replacing the old per-star-tier art (Unlocked/1star/3stars) per user request 2026-07-27 —
    // star count is still tracked (ScoreManager.GetBestStars) for unlock-gating, just no longer
    // drives which marker sprite shows.
    [SerializeField] private Sprite _unlockedSprite;        // LevelMarker_tick.png
    [SerializeField] private Sprite _playerPositionSprite;  // PlayerPosition.png
    // Removed, then re-added same day (2026-07-19): briefly deleted because the OLD
    // SunriseMeadows.png baked NEXT LEVEL/Home art directly into the background, so a second
    // rendered sprite doubled up (see git history for that fix). The background has since been
    // regenerated clean (no pins/buttons baked in) specifically to fix the pin duplication bug —
    // that also removed the baked buttons, so these need to go back to being real rendered
    // sprites again or they'd just be invisible with nothing underneath.
    // Bottom-left button switched from the wide Btn_nextlevel.png pill to the same square
    // Btn_play.png icon HUDController/MainMenuController already use elsewhere (2026-07-26) —
    // sized to exactly match Home below rather than the old pill's own 4:1 aspect.
    [SerializeField] private Sprite _playButtonSprite;      // Btn_play.png
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
    // Dedicated backdrop for this screen — Assets/Sprites/UI/MatchUp/MatchUpBackground.png.
    // (Earlier revisions of this screen reused the gameplay sky backdrop to avoid a duplicate-
    // frame bug in a since-replaced mockup image — no longer relevant now that a purpose-built
    // backdrop with nothing else baked into it exists; see docs/HISTORY.md for that history.)
    [SerializeField] private Sprite   _matchUpBackgroundSprite; // MatchUpBackground.png
    [SerializeField] private Sprite   _vsSprite;                // VS.png
    [SerializeField] private Sprite   _levelHeaderSprite;       // LevelHeader1.png (level 1 only)
    [SerializeField] private Sprite   _countdown3Sprite;        // countdown3.png
    [SerializeField] private Sprite   _countdown2Sprite;        // countdown2.png
    [SerializeField] private Sprite   _countdown1Sprite;        // countdown1.png
    [SerializeField] private Sprite   _countdownReadySprite;    // Countdown_Ready.png
    [SerializeField] private Sprite[] _animalCardSprites = new Sprite[8]; // Sprites/UI/MatchUp/, AnimalType-indexed
    [SerializeField] private Sprite[] _robotCardSprites  = new Sprite[2]; // Sprites/UI/MatchUp/, RobotType-indexed

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

    // Called by other panels (e.g. HUDController's Level Complete/Failed "Home" button) that
    // need to land on the main menu specifically, not the world map. GameManager.LoadMenu()
    // transitions GameState to Idle, which this class's own OnStateChanged reacts to by calling
    // ShowPanel() — this method runs immediately after that in the same call stack, so the map
    // gets hidden again before a frame ever renders it (same "atomic same-frame" reasoning as
    // MatchUpScreen's fade-to-black fix — see docs/HISTORY.md Round 14).
    public void SkipToMainMenu()
    {
        HidePanel();
        MainMenuController.Instance?.Show();
    }

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
            _markers[i].Refresh(unlocked, _lockedSprite, _unlockedSprite);
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

    // Play button (bottom-left) — shortcut to the matchup screen for whichever level the
    // bobbing indicator is currently sitting on, without needing to tap the pin itself.
    void OnPlayClicked() => _matchUpScreen.Show(_highestUnlocked);

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
        Vector2 basePos = _markers[_highestUnlocked].AnchoredPosition + new Vector2(0f, 100f);

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

        // ── Map content — centred; anchoredPosition IS PathPositions[i] directly (no extra
        // scale factor — SunriseMeadows.png is exactly 1920x1080, matching this canvas's
        // reference resolution 1:1, so measured image pixels convert straight to canvas units) ─
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
        // Enlarged 2026-07-24 (56x56 -> 90x90) to match the bigger markers below, and the hover
        // offset in IndicatorRoutine bumped 70 -> 100 to sit proportionally above the taller
        // marker art (MarkerSize now 80x120, was 56x84 — see LevelMarker.cs).
        var pGO = new GameObject("PlayerPositionIndicator");
        pGO.transform.SetParent(mapRT, false);
        _playerIndicatorRT = pGO.AddComponent<RectTransform>();
        _playerIndicatorRT.anchorMin = new Vector2(0.5f, 0.5f);
        _playerIndicatorRT.anchorMax = new Vector2(0.5f, 0.5f);
        _playerIndicatorRT.pivot     = new Vector2(0.5f, 0.5f);
        _playerIndicatorRT.sizeDelta = new Vector2(90f, 90f);
        var pImg = pGO.AddComponent<Image>();
        pImg.sprite         = _playerPositionSprite != null ? _playerPositionSprite : _squareSpr;
        pImg.color          = _playerPositionSprite != null ? Color.white : new Color(1f, 0.85f, 0.2f);
        pImg.preserveAspect = true;
        pImg.raycastTarget  = false;

        // ── Title — dropped (2026-07-16 mockup pass): SunriseMeadows.png now bakes the
        // "SUNRISE MEADOWS" banner directly into the background art, so a second TMP title
        // here would double up / clash in font with the baked one. See old git history if a
        // code-driven title is ever needed again (e.g. for a differently-worded background).

        // ── SafeArea — NEXT LEVEL and Home live inside this, same Screen.safeArea-driven
        // pattern as HUDController.BuildSafeArea/ApplySafeArea and MatchUpScreen's SafeArea
        // (see that file's comment for the full reasoning). Fixes a real clipping bug reported
        // 2026-07-24 — both buttons rendered outside the device's actual safe area on a real
        // phone aspect ratio, even though their old corner offsets looked fine against the flat
        // 1920x1080 reference canvas math. Everything that needs to be reachable/tappable near a
        // screen edge goes in here now; the full-bleed background and the map content (centred,
        // away from any edge) don't need to.
        var safeGO = new GameObject("SafeArea");
        safeGO.transform.SetParent(root, false);
        var safeRT = safeGO.AddComponent<RectTransform>();
        ApplySafeArea(safeRT);
        Transform safe = safeRT;

        // ── Play button — bottom-left, rendered from Btn_play.png ──────────────────
        // Replaced the old wide Btn_nextlevel.png pill (2026-07-26) with the same square
        // Btn_play.png icon used elsewhere, sized to exactly match Home below (150x150) rather
        // than the pill's own 4:1 aspect.
        var nextGO = new GameObject("PlayBtn");
        nextGO.transform.SetParent(safe, false);
        var nextRT = nextGO.AddComponent<RectTransform>();
        nextRT.anchorMin        = new Vector2(0f, 0f);
        nextRT.anchorMax        = new Vector2(0f, 0f);
        nextRT.pivot            = new Vector2(0f, 0f);
        nextRT.anchoredPosition = new Vector2(40f, 40f);
        nextRT.sizeDelta        = new Vector2(150f, 150f);
        var nextImg = nextGO.AddComponent<Image>();
        nextImg.sprite         = _playButtonSprite != null ? _playButtonSprite : _squareSpr;
        nextImg.color          = _playButtonSprite != null ? Color.white : new Color(0.85f, 0.55f, 0.05f);
        nextImg.preserveAspect = true;
        var nextBtn = nextGO.AddComponent<Button>();
        nextBtn.targetGraphic = nextImg;
        var nc = nextBtn.colors;
        nc.normalColor      = Color.white;
        nc.highlightedColor = new Color(0.90f, 0.90f, 0.90f);
        nc.pressedColor     = new Color(0.70f, 0.70f, 0.70f);
        nextBtn.colors      = nc;
        nextBtn.onClick.AddListener(OnPlayClicked);

        // ── Home button — bottom-right, rendered from Btn_home.png ──────────────────
        // Enlarged 84x84 -> 150x150 to exactly match the landing page's PLAY/SETTINGS icons
        // (same 1:1-aspect Btn_*.png category — see MainMenuController.cs). Corner-anchored
        // within the SafeArea — see NEXT LEVEL button comment above for why.
        var homeGO = new GameObject("HomeBtn");
        homeGO.transform.SetParent(safe, false);
        var homeRT = homeGO.AddComponent<RectTransform>();
        homeRT.anchorMin        = new Vector2(1f, 0f);
        homeRT.anchorMax        = new Vector2(1f, 0f);
        homeRT.pivot            = new Vector2(1f, 0f);
        homeRT.anchoredPosition = new Vector2(-40f, 40f);
        homeRT.sizeDelta        = new Vector2(150f, 150f);
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
        _matchUpScreen.Init(_squareSpr, _matchUpBackgroundSprite, _vsSprite, _levelHeaderSprite,
            _countdown3Sprite, _countdown2Sprite, _countdown1Sprite, _countdownReadySprite,
            _animalCardSprites, _robotCardSprites);

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

    // Same technique as HUDController.ApplySafeArea / MatchUpScreen.ApplySafeArea — maps
    // Screen.safeArea (actual device pixels) to normalized anchors so children of this
    // RectTransform can never render into a notch, rounded corner, or home-indicator zone.
    static void ApplySafeArea(RectTransform rt)
    {
        if (Screen.width <= 0 || Screen.height <= 0)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
        }
        else
        {
            Rect safe = Screen.safeArea;
            Vector2 min = safe.position;
            Vector2 max = safe.position + safe.size;
            min.x /= Screen.width;  min.y /= Screen.height;
            max.x /= Screen.width;  max.y /= Screen.height;
            rt.anchorMin = min;
            rt.anchorMax = max;
        }
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
