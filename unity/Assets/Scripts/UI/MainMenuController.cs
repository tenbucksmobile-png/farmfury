using UnityEngine;
using UnityEngine.UI;

// Phase 2.6 (updated) — Main menu backed by the LandingPage.png splash art.
// Builds its own Canvas (sortingOrder 400). Shows on startup (State == Idle).
// CatapultLauncher defers ForceStartLevel(0) one frame and skips if IsVisible.
public class MainMenuController : MonoBehaviour
{
    public static MainMenuController Instance { get; private set; }
    public bool IsVisible => _panel != null && _panel.activeSelf;

    [SerializeField] private Sprite _landingSprite;
    [SerializeField] private Sprite _playButtonSprite;

    private GameObject _panel;
    private Sprite     _squareSpr;

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
        if (GameManager.Instance == null || GameManager.Instance.State == GameState.Idle)
            _panel.SetActive(true);
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public void Show() => _panel.SetActive(true);

    // ── Interaction ───────────────────────────────────────────────────────────

    void OnPlayClicked()
    {
        _panel.SetActive(false);
        // Sunrise Meadows world map is PLAY's destination (since 2026-07-15). The old grid-based
        // LevelSelectController was removed entirely 2026-07-26 — it was still silently reacting
        // to GameState.Idle in the background, racing WorldMapController to show itself on every
        // Idle transition (same event, same Canvas sortingOrder). World 2+ should get its own
        // map screen built on the WorldMapController pattern when that content exists, not a
        // resurrected LevelSelectController.
        WorldMapController.Instance?.Show();
    }

    // ── UI construction ───────────────────────────────────────────────────────

    void BuildUI()
    {
        // Canvas — sortingOrder 400 sits above all HUD panels
        var cvGO = new GameObject("MainMenuCanvas");
        cvGO.transform.SetParent(transform, false);
        var cv = cvGO.AddComponent<Canvas>();
        cv.renderMode   = RenderMode.ScreenSpaceOverlay;
        cv.sortingOrder = 400;
        var cs = cvGO.AddComponent<CanvasScaler>();
        cs.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        cs.referenceResolution = new Vector2(1920f, 1080f);
        cs.screenMatchMode     = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        cs.matchWidthOrHeight  = 0.5f;
        cvGO.AddComponent<GraphicRaycaster>();
        _panel = cvGO;

        var root = cvGO.transform;

        // ── Background — landing page art ─────────────────────────────────────
        var bgGO = new GameObject("Background");
        bgGO.transform.SetParent(root, false);
        var bgRT = bgGO.AddComponent<RectTransform>();
        bgRT.anchorMin = Vector2.zero;
        bgRT.anchorMax = Vector2.one;
        bgRT.offsetMin = bgRT.offsetMax = Vector2.zero;
        var bgImg = bgGO.AddComponent<Image>();
        bgImg.sprite         = _landingSprite != null ? _landingSprite : _squareSpr;
        bgImg.color          = _landingSprite != null ? Color.white : new Color(0.36f, 0.62f, 0.88f);
        bgImg.preserveAspect = false;  // stretch to fill canvas

        // ── PLAY button — bottom-left corner, per LandingPage_New.png mockup (2026-07-16) ──
        // CORNER-anchored (not centre-anchored with a fixed offset) so it stays a fixed inset
        // from the actual corner regardless of device aspect ratio. The first attempt's 160u
        // inset on both axes was still measured (2026-07-17, from a user phone-frame screenshot,
        // pixel-measured against the button's own 150u sizeDelta to calibrate px→unit scale)
        // to leave the button ~32u left of the yellow safe-area guide — i.e. straddling/outside
        // it — while the vertical inset was already adequate. Widened the X inset to clear the
        // guide with margin; Y bumped slightly too as a safety margin against mockup measurement
        // noise, not because it measured as a real problem.
        var playGO  = new GameObject("PlayBtn");
        playGO.transform.SetParent(root, false);
        var playRT  = playGO.AddComponent<RectTransform>();
        playRT.anchorMin        = new Vector2(0f, 0f);
        playRT.anchorMax        = new Vector2(0f, 0f);
        playRT.pivot            = new Vector2(0.5f, 0.5f);
        playRT.anchoredPosition = new Vector2(220f, 170f);
        playRT.sizeDelta        = new Vector2(150f, 150f);
        var playImg = playGO.AddComponent<Image>();
        playImg.sprite = _playButtonSprite != null ? _playButtonSprite : _squareSpr;
        playImg.color  = _playButtonSprite != null ? Color.white : new Color(1.00f, 0.55f, 0.05f);
        playImg.preserveAspect = true;

        var playBtn = playGO.AddComponent<Button>();
        playBtn.targetGraphic = playImg;
        var pc = playBtn.colors;
        pc.normalColor      = Color.white;
        pc.highlightedColor = new Color(0.88f, 0.88f, 0.88f);
        pc.pressedColor     = new Color(0.68f, 0.68f, 0.68f);
        playBtn.colors = pc;
        playBtn.onClick.AddListener(OnPlayClicked);

        _panel.SetActive(false);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

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
