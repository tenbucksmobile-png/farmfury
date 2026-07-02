using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

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
// avoids fighting CatapultLauncher for ownership of the world-space camera. The given path
// X/Y coordinates are honoured directly via a 1 unit = 100px mapping (matching the marker
// art's own PPU=100 import), so PathPositions below IS the spec's numbers, just multiplied.
//
// STARS/UNLOCK NOTE — also reuses the existing ScoreManager.GetBestStars(index) convention
// (0-based ff_stars_N keys, plain star count) rather than introducing the spec's separate
// 1-based ff_stars_1..18 scheme with a -1 "unlocked but unplayed" sentinel — LevelSelectController
// already reads stars this exact way, and a second parallel key scheme would let the two
// level-select screens disagree about what's unlocked. The one behavioural difference: this
// screen can't distinguish "never played" from "played and failed" (both read as 0 stars);
// flagged in the scene setup checklist rather than silently glossed over.
public class WorldMapController : MonoBehaviour
{
    public static WorldMapController Instance { get; private set; }

    public const int LevelCount = 18; // Sunrise Meadows / World 1

    [Header("Art (wired via FarmFury -> Wire Scene References)")]
    [SerializeField] private Sprite _backgroundSprite;      // SunriseMeadows.png
    [SerializeField] private Sprite _lockedSprite;          // LevelMarker_Locked.png
    [SerializeField] private Sprite _unlockedSprite;        // LevelMarker_Unlocked.png
    [SerializeField] private Sprite _star1Sprite;           // LevelMarker_1star.png
    [SerializeField] private Sprite _star2Sprite;           // no dedicated art yet — falls back to 3-star
    [SerializeField] private Sprite _star3Sprite;           // LevelMarker_3stars.png
    [SerializeField] private Sprite _playerPositionSprite;  // PlayerPosition.png

    // Design-spec path coordinates verbatim (X right / Y up), 1 unit = 100px on this canvas.
    // "approximate — adjust after seeing in scene" per the spec: markers 13/14 (both X=4.0,
    // only 0.5u/50px apart in Y against an 84px-tall marker) WILL visually overlap and need a
    // manual nudge once seen in the Editor — not silently altered here.
    private static readonly Vector2[] PathPositions =
    {
        new(-5.5f, -2.8f), new(-4.2f, -2.5f), new(-3.0f, -2.1f), new(-2.0f, -2.4f),
        new(-1.0f, -2.8f), new( 0.0f, -2.5f), new( 1.0f, -2.0f), new( 1.8f, -1.5f),
        new( 2.5f, -1.0f), new( 3.0f, -0.5f), new( 3.5f,  0.0f), new( 3.8f,  0.5f),
        new( 4.0f,  1.0f), new( 4.0f,  1.5f), new( 3.5f,  2.0f), new( 3.0f,  2.5f),
        new( 2.5f,  3.0f), new( 2.0f,  3.5f),
    };
    private const float UnitToPixels = 100f;

    private GameObject       _panel;
    private Sprite           _squareSpr;
    private readonly LevelMarker[] _markers = new LevelMarker[LevelCount];
    private RectTransform    _playerIndicatorRT;
    private Coroutine        _bobRoutine;
    private LevelPreviewCard _previewCard;

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
        RefreshMarkers();
        _panel.SetActive(true);
        if (_bobRoutine == null) _bobRoutine = StartCoroutine(BobIndicator());
    }

    void HidePanel()
    {
        _previewCard?.Hide();
        if (_bobRoutine != null) { StopCoroutine(_bobRoutine); _bobRoutine = null; }
        _panel.SetActive(false);
    }

    // ── Markers ───────────────────────────────────────────────────────────────

    void RefreshMarkers()
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

        if (_playerIndicatorRT != null)
            _playerIndicatorRT.anchoredPosition = _markers[highestUnlocked].AnchoredPosition + new Vector2(0f, 70f);
    }

    // Level 1 always unlocked; level N unlocks once level N-1 has >=1 star.
    static bool IsUnlocked(int levelIndex) =>
        levelIndex == 0 || ScoreManager.GetBestStars(levelIndex - 1) > 0;

    void OnMarkerTapped(int levelIndex)
    {
        if (!IsUnlocked(levelIndex)) { _markers[levelIndex].PlayLockedShake(); return; }
        _previewCard.Show(levelIndex);
    }

    void OnBackClicked()
    {
        HidePanel();
        MainMenuController.Instance?.Show();
    }

    // ── Player position indicator (bobs while the panel is visible) ──────────

    IEnumerator BobIndicator()
    {
        const float amplitude = 15f; // spec: "+0.15" world units * 100px/unit
        const float half      = 0.6f;
        while (true)
        {
            yield return MoveIndicatorBy(amplitude, half);
            yield return MoveIndicatorBy(-amplitude, half);
        }
    }

    IEnumerator MoveIndicatorBy(float deltaY, float duration)
    {
        float startY  = _playerIndicatorRT.anchoredPosition.y;
        float targetY = startY + deltaY;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t   = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            var   pos = _playerIndicatorRT.anchoredPosition;
            _playerIndicatorRT.anchoredPosition = new Vector2(pos.x, Mathf.Lerp(startY, targetY, t));
            yield return null;
        }
        var final = _playerIndicatorRT.anchoredPosition;
        _playerIndicatorRT.anchoredPosition = new Vector2(final.x, targetY);
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
            marker.Init(i, PathPositions[i] * UnitToPixels, _squareSpr, OnMarkerTapped);
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

        // ── Title — redundant if the background art already shows it; harmless either way ──
        var titleGO = new GameObject("Title");
        titleGO.transform.SetParent(root, false);
        var titleRT = titleGO.AddComponent<RectTransform>();
        titleRT.anchorMin        = new Vector2(0.5f, 1f);
        titleRT.anchorMax        = new Vector2(0.5f, 1f);
        titleRT.pivot            = new Vector2(0.5f, 1f);
        titleRT.anchoredPosition = new Vector2(0f, -24f);
        titleRT.sizeDelta        = new Vector2(800f, 60f);
        var titleTMP = titleGO.AddComponent<TextMeshProUGUI>();
        titleTMP.text               = "SUNRISE MEADOWS";
        titleTMP.fontSize           = 44f;
        titleTMP.fontStyle          = FontStyles.Bold;
        titleTMP.color              = new Color(1f, 0.97f, 0.86f);
        titleTMP.alignment          = TextAlignmentOptions.Center;
        titleTMP.enableWordWrapping = false;

        // ── Back button — top-left, returns to main menu ─────────────────────
        var backGO = new GameObject("BackBtn");
        backGO.transform.SetParent(root, false);
        var backRT = backGO.AddComponent<RectTransform>();
        backRT.anchorMin        = new Vector2(0f, 1f);
        backRT.anchorMax        = new Vector2(0f, 1f);
        backRT.pivot            = new Vector2(0f, 1f);
        backRT.anchoredPosition = new Vector2(24f, -24f);
        backRT.sizeDelta        = new Vector2(140f, 52f);
        var backImg = backGO.AddComponent<Image>();
        backImg.sprite = _squareSpr;
        backImg.color  = new Color(0.12f, 0.14f, 0.22f, 0.85f);
        var backLblGO = new GameObject("Label");
        backLblGO.transform.SetParent(backGO.transform, false);
        var backLblRT = backLblGO.AddComponent<RectTransform>();
        backLblRT.anchorMin = Vector2.zero;
        backLblRT.anchorMax = Vector2.one;
        backLblRT.offsetMin = backLblRT.offsetMax = Vector2.zero;
        var backLblTMP = backLblGO.AddComponent<TextMeshProUGUI>();
        backLblTMP.text               = "← BACK";
        backLblTMP.fontSize           = 22f;
        backLblTMP.color              = Color.white;
        backLblTMP.alignment          = TextAlignmentOptions.Center;
        backLblTMP.enableWordWrapping = false;
        var backBtn = backGO.AddComponent<Button>();
        backBtn.targetGraphic = backImg;
        var bc = backBtn.colors;
        bc.normalColor      = Color.white;
        bc.highlightedColor = new Color(0.85f, 0.85f, 0.85f);
        bc.pressedColor     = new Color(0.60f, 0.60f, 0.60f);
        backBtn.colors      = bc;
        backBtn.onClick.AddListener(OnBackClicked);

        // ── Level preview card overlay — added last so it renders on top ────
        var cardGO = new GameObject("LevelPreviewCard");
        cardGO.transform.SetParent(root, false);
        _previewCard = cardGO.AddComponent<LevelPreviewCard>();
        _previewCard.Init(_squareSpr);

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
