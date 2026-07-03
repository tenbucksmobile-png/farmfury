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
    [SerializeField] private Sprite _settingsButtonSprite;

    private GameObject _panel;
    private Sprite     _squareSpr;
    private GameObject _settingsPopup;
    private Image      _musicToggleImg;
    private Image      _sfxToggleImg;

    private static readonly Color ToggleOnColor  = new(0.20f, 0.65f, 0.30f);
    private static readonly Color ToggleOffColor = new(0.45f, 0.45f, 0.45f);

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

    // Minimal Music/SFX toggle popup — reuses the same AudioManager.MusicEnabled/SfxEnabled +
    // PlayerPrefs-backed state HUDController's pause menu already toggles, so the two screens
    // can't disagree about whether audio is on. Not a full settings screen (no other settings
    // exist yet in this project) — scoped to what the mockup's gear icon plausibly opens.
    void OnSettingsClicked()
    {
        _musicToggleImg.color = AudioManager.MusicEnabled ? ToggleOnColor : ToggleOffColor;
        _sfxToggleImg.color   = AudioManager.SfxEnabled   ? ToggleOnColor : ToggleOffColor;
        _settingsPopup.SetActive(true);
    }

    void OnSettingsCloseClicked() => _settingsPopup.SetActive(false);

    void OnMusicToggleClicked()
    {
        bool on = !AudioManager.MusicEnabled;
        AudioManager.SetMusicEnabled(on);
        _musicToggleImg.color = on ? ToggleOnColor : ToggleOffColor;
    }

    void OnSfxToggleClicked()
    {
        bool on = !AudioManager.SfxEnabled;
        AudioManager.SetSfxEnabled(on);
        _sfxToggleImg.color = on ? ToggleOnColor : ToggleOffColor;
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

        // ── SETTINGS button — bottom-right corner, mirrors PLAY's corner-anchoring ──
        var settingsGO = new GameObject("SettingsBtn");
        settingsGO.transform.SetParent(root, false);
        var settingsRT = settingsGO.AddComponent<RectTransform>();
        settingsRT.anchorMin        = new Vector2(1f, 0f);
        settingsRT.anchorMax        = new Vector2(1f, 0f);
        settingsRT.pivot            = new Vector2(0.5f, 0.5f);
        settingsRT.anchoredPosition = new Vector2(-220f, 170f);
        settingsRT.sizeDelta        = new Vector2(150f, 150f);
        var settingsImg = settingsGO.AddComponent<Image>();
        settingsImg.sprite = _settingsButtonSprite != null ? _settingsButtonSprite : _squareSpr;
        settingsImg.color  = _settingsButtonSprite != null ? Color.white : new Color(1.00f, 0.55f, 0.05f);
        settingsImg.preserveAspect = true;

        var settingsBtn = settingsGO.AddComponent<Button>();
        settingsBtn.targetGraphic = settingsImg;
        var sc = settingsBtn.colors;
        sc.normalColor      = Color.white;
        sc.highlightedColor = new Color(0.88f, 0.88f, 0.88f);
        sc.pressedColor     = new Color(0.68f, 0.68f, 0.68f);
        settingsBtn.colors  = sc;
        settingsBtn.onClick.AddListener(OnSettingsClicked);

        BuildSettingsPopup(root);

        _panel.SetActive(false);
    }

    // Small centred Music/SFX toggle popup opened by the gear icon. Same self-contained
    // dismiss-catcher-behind-a-box pattern used by MatchUpScreen/LevelPreviewCard.
    void BuildSettingsPopup(Transform root)
    {
        var popGO = new GameObject("SettingsPopup");
        popGO.transform.SetParent(root, false);
        var popRT = popGO.AddComponent<RectTransform>();
        popRT.anchorMin = Vector2.zero;
        popRT.anchorMax = Vector2.one;
        popRT.offsetMin = popRT.offsetMax = Vector2.zero;
        _settingsPopup = popGO;

        var dismissImg = popGO.AddComponent<Image>();
        dismissImg.sprite = _squareSpr;
        dismissImg.color  = new Color(0f, 0f, 0f, 0.55f);
        var dismissBtn = popGO.AddComponent<Button>();
        dismissBtn.targetGraphic = dismissImg;
        dismissBtn.onClick.AddListener(OnSettingsCloseClicked);

        var box = new GameObject("Box");
        box.transform.SetParent(popGO.transform, false);
        var boxRT = box.AddComponent<RectTransform>();
        boxRT.anchorMin        = new Vector2(0.5f, 0.5f);
        boxRT.anchorMax        = new Vector2(0.5f, 0.5f);
        boxRT.pivot            = new Vector2(0.5f, 0.5f);
        boxRT.sizeDelta        = new Vector2(420f, 260f);
        var boxImg = box.AddComponent<Image>();
        boxImg.sprite = _squareSpr;
        boxImg.color  = new Color(0.97f, 0.94f, 0.88f);
        var boxBtn = box.AddComponent<Button>(); // swallows clicks so the box itself never dismisses
        boxBtn.transition = Selectable.Transition.None;

        var titleTMP = MakeToggleLabel(box.transform, "Title", "SETTINGS",
            new Vector2(0f, 85f), new Vector2(360f, 50f), 30f, new Color(0.12f, 0.10f, 0.06f));
        titleTMP.fontStyle = TMPro.FontStyles.Bold;

        var musicBtn = MakeToggleButton(box.transform, "MusicToggle", "MUSIC", new Vector2(-95f, -10f));
        _musicToggleImg = musicBtn.targetGraphic as Image;
        musicBtn.onClick.AddListener(OnMusicToggleClicked);

        var sfxBtn = MakeToggleButton(box.transform, "SfxToggle", "SFX", new Vector2(95f, -10f));
        _sfxToggleImg = sfxBtn.targetGraphic as Image;
        sfxBtn.onClick.AddListener(OnSfxToggleClicked);

        var closeBtn = MakeToggleButton(box.transform, "CloseBtn", "CLOSE", new Vector2(0f, -90f));
        closeBtn.onClick.AddListener(OnSettingsCloseClicked);

        _settingsPopup.SetActive(false);
    }

    Button MakeToggleButton(Transform parent, string name, string label, Vector2 pos)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0.5f, 0.5f);
        rt.anchorMax        = new Vector2(0.5f, 0.5f);
        rt.pivot            = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta        = new Vector2(150f, 52f);
        var img = go.AddComponent<Image>();
        img.sprite = _squareSpr;
        img.color  = ToggleOffColor;
        MakeToggleLabel(go.transform, "Label", label, Vector2.zero, new Vector2(150f, 52f), 22f, Color.white);
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        return btn;
    }

    static TMPro.TextMeshProUGUI MakeToggleLabel(Transform parent, string name, string text, Vector2 pos, Vector2 size, float fontSize, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0.5f, 0.5f);
        rt.anchorMax        = new Vector2(0.5f, 0.5f);
        rt.pivot            = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta        = size;
        var tmp = go.AddComponent<TMPro.TextMeshProUGUI>();
        tmp.text               = text;
        tmp.fontSize           = fontSize;
        tmp.color              = color;
        tmp.alignment          = TMPro.TextAlignmentOptions.Center;
        tmp.enableWordWrapping = false;
        tmp.raycastTarget      = false;
        return tmp;
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
