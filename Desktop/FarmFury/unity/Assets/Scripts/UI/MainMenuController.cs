using UnityEngine;
using UnityEngine.UI;
using TMPro;

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
        // Sunrise Meadows world map replaces the grid-based LevelSelectController as PLAY's
        // destination (2026-07-15) — LevelSelectController is left in place, unwired, for
        // World 2+ once that content exists and doesn't have its own map screen yet.
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

        // Subtle dark band at the bottom so the PLAY button pops against the image
        var vigGO = new GameObject("BottomVignette");
        vigGO.transform.SetParent(root, false);
        var vigRT = vigGO.AddComponent<RectTransform>();
        vigRT.anchorMin = Vector2.zero;
        vigRT.anchorMax = new Vector2(1f, 0f);
        vigRT.offsetMin = Vector2.zero;
        vigRT.offsetMax = new Vector2(0f, 200f);
        var vigImg = vigGO.AddComponent<Image>();
        vigImg.sprite = _squareSpr;
        vigImg.color  = new Color(0f, 0f, 0f, 0.45f);

        // ── PLAY button ───────────────────────────────────────────────────────
        // Square icon button (Play.png — orange rounded square, white play glyph, glow
        // baked into the art) replaces the earlier procedural orange-rect + "▶ PLAY" text.
        var playGO  = new GameObject("PlayBtn");
        playGO.transform.SetParent(root, false);
        var playRT  = playGO.AddComponent<RectTransform>();
        playRT.anchorMin        = new Vector2(0.5f, 0.5f);
        playRT.anchorMax        = new Vector2(0.5f, 0.5f);
        playRT.pivot            = new Vector2(0.5f, 0.5f);
        playRT.anchoredPosition = new Vector2(0f, -360f);  // ~17% from bottom
        playRT.sizeDelta        = new Vector2(220f, 220f); // square, matches Play.png's aspect
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

        // ── Version label — bottom-right, subtle ─────────────────────────────
        var verGO = new GameObject("Version");
        verGO.transform.SetParent(root, false);
        var verRT = verGO.AddComponent<RectTransform>();
        verRT.anchorMin        = new Vector2(1f, 0f);
        verRT.anchorMax        = new Vector2(1f, 0f);
        verRT.pivot            = new Vector2(1f, 0f);
        verRT.anchoredPosition = new Vector2(-22f, 22f);
        verRT.sizeDelta        = new Vector2(340f, 38f);
        var verTMP = verGO.AddComponent<TextMeshProUGUI>();
        verTMP.text               = "World 1 — Meadow Ruins";
        verTMP.fontSize           = 22f;
        verTMP.color              = new Color(1f, 1f, 1f, 0.55f);
        verTMP.alignment          = TextAlignmentOptions.Right;
        verTMP.enableWordWrapping = false;

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
