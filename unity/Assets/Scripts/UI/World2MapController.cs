using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// Frozen Tundra (World 2) level-select map. Built on the exact same pattern as
// WorldMapController (World 1's Sunrise Meadows) — see that class's own header comment for the
// full architecture rationale (ScreenSpaceOverlay Canvas + uGUI Button markers, not world-space
// SpriteRenderers). Reachable today ONLY via World2LandingController's PLAY button, itself only
// reachable via the L18 world-transition video (see HUDController.GoToWorldLandingAfterTransition)
// — this class deliberately does NOT subscribe to GameManager.OnStateChanged the way
// WorldMapController does, so it never races WorldMapController to show itself on a normal
// GameState.Idle transition (e.g. finishing any World 1 level). KNOWN GAP, flagged rather than
// solved here: once real World 2 levels exist and a player finishes one mid-World-2, the normal
// Home/LoadMenu() path would currently show WorldMapController (World 1's map), not this screen —
// there is no "which world is the player currently in" state anywhere yet. A real fix needs a
// persisted "current/highest-unlocked world" concept touching GameManager, which is a bigger
// decision than this task covered.
//
// PATH TRACE (2026-07-19, RE-TRACED same day after the user re-supplied FrozenTundra.png with a
// "Frozen Tundra" title banner and the path extended further right to the fortress entrance) —
// the first pass assumed the path was Y-monotonic top-to-bottom and used a per-row X-centroid,
// which broke on this updated art: the path now bends back on itself (an S-curve down, then a
// diagonal arm back UP-and-right to the fortress), so a single X-per-row centroid averaged two
// unrelated segments together wherever their Y ranges overlapped, producing garbage zigzag
// points — caught by re-rendering the result and visually checking it against the source art
// before trusting it, not shipped blind. A skeleton-and-walk approach was tried next but the
// hand-painted path's variable width produces a heavily branchy skeleton (15+ spurious endpoints
// even after pruning short spurs) that kept trapping a greedy walk in small local loops.
// Landed on: hand-picked ~18 waypoints by eye against a gridlined render, snapped each to its
// nearest actual path-mask pixel (same tan-color pixel classification as the first pass; snap
// distances all under 44px against a path rendering ~130-150px wide, i.e. comfortably inside the
// path, not a guess), then re-rendered the resulting polyline over the source art and visually
// confirmed it tracks the painted centerline end-to-end (start near the ice-pool/beacon, through
// the full S-bend, along the diagonal arm, ending at the fortress entrance archway) before
// resampling 22 evenly arc-length-spaced points from it and converting from FrozenTundra.png's
// native 2720x1536 pixel space into this canvas's 1920x1080 reference space (scaleX=1920/2720,
// scaleY=1080/1536). Point 1 = the end nearest the ice-pool/beacon, matching World 1's "level 1 =
// start of the visible path" convention; point 22 lands at the fortress entrance.
//
// MARKER SIZE — this re-traced path has a much more even spacing than the first pass (~65 canvas
// units between points, min 50/max 67 — the first pass's path was shorter and choppier, ~38.5
// average with a 21-unit minimum). Still short of World 1's ~100-107 spacing, so still smaller
// than LevelMarker's default 80x120, but less aggressively shrunk than the first pass's 40x60 —
// 50x75 (same 2:3 aspect) via LevelMarker's sizeOverride param keeps roughly World 1's own
// marker-width-to-spacing ratio (~0.77 here vs. World 1's ~0.78).
public class World2MapController : MonoBehaviour
{
    public static World2MapController Instance { get; private set; }

    public const int LevelCount = 22; // Frozen Tundra / World 2, per CLAUDE.md's World table

    // Global GameManager level index of this map's first marker = however many levels exist
    // before it in the flat auto-discovered LevelData list (today: World 1's 18). No World 2
    // LevelData assets exist yet, so GameManager.GetLevelData(18..39) all return null today —
    // MatchUpScreen already handles that generically (shows "COMING SOON", per its own existing
    // no-LevelData path), so every marker here is reachable/tappable but shows "coming soon"
    // until real World 2 levels are built.
    public const int GlobalIndexOffset = WorldMapController.LevelCount;

    static readonly Vector2 MarkerSizeOverride = new(50f, 75f); // see class comment above

    private static readonly Vector2[] PathPositions =
    {
        new(-216.7f, 69.6f), new(-150.2f, 64.5f), new(-87.3f, 47.0f), new(-28.5f, 15.5f),
        new(24.7f, -24.5f), new(70.0f, -72.4f), new(53.3f, -119.8f), new(-2.7f, -155.6f),
        new(-61.2f, -187.5f), new(-121.0f, -217.1f), new(-164.6f, -266.7f), new(-193.9f, -307.5f),
        new(-131.5f, -314.7f), new(-65.4f, -306.0f), new(-0.4f, -292.5f), new(59.7f, -263.7f),
        new(118.4f, -232.1f), new(176.0f, -198.6f), new(237.3f, -172.3f), new(298.0f, -144.8f),
        new(357.7f, -115.0f), new(422.1f, -97.7f),
    };

    [Header("Art (wired via FarmFury -> Wire Scene References)")]
    [SerializeField] private Sprite _backgroundSprite;      // Assets/Sprites/UI/LevelCards/World2/FrozenTundra.png
    [SerializeField] private Sprite _lockedSprite;          // Assets/Sprites/UI/LevelCards/World2/LevelMarker_Locked.png
    [SerializeField] private Sprite _unlockedSprite;        // Assets/Sprites/UI/LevelCards/World2/LevelMarker_tick.png
    [SerializeField] private Sprite _playerPositionSprite;  // Assets/Sprites/UI/LevelCards/World2/PlayerPosition.png
    [SerializeField] private Sprite _playButtonSprite;      // Btn_play.png — shared Icon/ asset, same as every other screen
    [SerializeField] private Sprite _homeButtonSprite;      // Btn_home.png — shared Icon/ asset, same as every other screen

    // Same MatchUpScreen art shape as WorldMapController — see that class's own comment on why
    // these live directly on this persisted component rather than as [SerializeField]s on the
    // nested MatchUpScreen instance. No dedicated World 2 match-up art exists yet (Frost-robot/
    // ice-cannon variants etc.) — left null/unwired for now; MatchUpScreen already falls back to
    // its generic placeholder square when a sprite is unwired, and to its "COMING SOON" branch
    // entirely when a marker's global index has no LevelData (true for all 22 today).
    [SerializeField] private Sprite   _matchUpBackgroundSprite;
    [SerializeField] private Sprite   _vsSprite;
    [SerializeField] private Sprite[] _levelHeaderSprites = new Sprite[LevelCount];
    [SerializeField] private Sprite   _countdown3Sprite;
    [SerializeField] private Sprite   _countdown2Sprite;
    [SerializeField] private Sprite   _countdown1Sprite;
    [SerializeField] private Sprite   _countdownReadySprite;
    [SerializeField] private AudioClip _countdownClip;
    [SerializeField] private Sprite   _cluckFlySprite;
    [SerializeField] private AudioClip _cluckFallingClip;
    [SerializeField] private Sprite   _bessieFlySprite;
    [SerializeField] private AudioClip _bessieFallingClip;
    [SerializeField] private Sprite[] _animalCardSprites = new Sprite[8];
    [SerializeField] private Sprite[] _robotCardSprites  = new Sprite[4];
    [SerializeField] private Sprite   _eggSprite;
    [SerializeField] private Sprite   _skipButtonSprite;

    private GameObject       _panel;
    private Sprite           _squareSpr;
    private readonly LevelMarker[] _markers = new LevelMarker[LevelCount];
    private RectTransform    _playerIndicatorRT;
    private Coroutine        _bobRoutine;
    private int              _lastHighestUnlocked = -1;
    private int              _highestUnlocked;
    private MatchUpScreen    _matchUpScreen;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance   = this;
        _squareSpr = MakeSquareSprite();
        BuildUI();
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

    // Local index 0 is always unlocked — the only way to reach this screen today is via
    // World2LandingController, itself only reachable after L18 is already beaten, so there's no
    // scenario where this map is visible but its first level shouldn't be playable yet.
    static bool IsUnlocked(int localIndex) =>
        localIndex == 0 || ScoreManager.GetBestStars(GlobalIndexOffset + localIndex - 1) > 0;

    void OnMarkerTapped(int localIndex)
    {
        if (!IsUnlocked(localIndex)) { _markers[localIndex].PlayLockedShake(); return; }
        _matchUpScreen.Show(GlobalIndexOffset + localIndex);
    }

    void OnPlayClicked() => _matchUpScreen.Show(GlobalIndexOffset + _highestUnlocked);

    void OnBackClicked()
    {
        HidePanel();
        MainMenuController.Instance?.Show();
    }

    IEnumerator IndicatorRoutine(bool slideFirst)
    {
        if (_playerIndicatorRT == null) yield break;
        Vector2 basePos = _markers[_highestUnlocked].AnchoredPosition + new Vector2(0f, 62f);

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

        const float amplitude = 10f; // scaled down from World 1's 15f to match this map's smaller markers
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

    void BuildUI()
    {
        var cvGO        = new GameObject("World2MapCanvas");
        cvGO.transform.SetParent(transform, false);
        var cv          = cvGO.AddComponent<Canvas>();
        cv.renderMode   = RenderMode.ScreenSpaceOverlay;
        cv.sortingOrder = 301; // alongside WorldMapController's 300 — mutually exclusive via explicit Show()/Hide()
        var cs                 = cvGO.AddComponent<CanvasScaler>();
        cs.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        cs.referenceResolution = new Vector2(1920f, 1080f);
        cs.screenMatchMode     = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        cs.matchWidthOrHeight  = 0.5f;
        cvGO.AddComponent<GraphicRaycaster>();
        _panel = cvGO;
        Transform root = cvGO.transform;

        // ── Background — FrozenTundra.png, stretched to fill exactly like SunriseMeadows.png ──
        var bgGO = new GameObject("Background");
        bgGO.transform.SetParent(root, false);
        var bgRT = bgGO.AddComponent<RectTransform>();
        bgRT.anchorMin = Vector2.zero;
        bgRT.anchorMax = Vector2.one;
        bgRT.offsetMin = bgRT.offsetMax = Vector2.zero;
        var bgImg = bgGO.AddComponent<Image>();
        bgImg.sprite         = _backgroundSprite != null ? _backgroundSprite : _squareSpr;
        bgImg.color          = _backgroundSprite != null ? Color.white : new Color(0.55f, 0.75f, 0.92f);
        bgImg.preserveAspect = false;

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
            marker.Init(i, PathPositions[i], _squareSpr, OnMarkerTapped, MarkerSizeOverride);
            _markers[i] = marker;
        }

        // ── Player position indicator — scaled down from World 1's 90x90 to match this map's
        // smaller markers (50x75 vs. 80x120) ──
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

        // ── SafeArea — Play/Home, same pattern as WorldMapController ──
        var safeGO = new GameObject("SafeArea");
        safeGO.transform.SetParent(root, false);
        var safeRT = safeGO.AddComponent<RectTransform>();
        ApplySafeArea(safeRT);
        Transform safe = safeRT;

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

        var matchGO = new GameObject("MatchUpScreen");
        matchGO.transform.SetParent(root, false);
        _matchUpScreen = matchGO.AddComponent<MatchUpScreen>();
        _matchUpScreen.Init(_squareSpr, _matchUpBackgroundSprite, _vsSprite, _levelHeaderSprites,
            _countdown3Sprite, _countdown2Sprite, _countdown1Sprite, _countdownReadySprite, _countdownClip,
            _cluckFlySprite, _cluckFallingClip, _bessieFlySprite, _bessieFallingClip,
            _animalCardSprites, _robotCardSprites, _eggSprite, _skipButtonSprite);

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
