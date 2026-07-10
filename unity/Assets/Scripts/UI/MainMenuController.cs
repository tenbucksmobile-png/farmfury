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
    [SerializeField] private Sprite _settingsButtonSprite;
    [SerializeField] private Sprite _scoreboardSprite; // Scoreboard.png — settings popup backdrop

    private GameObject _panel;
    private Sprite     _squareSpr;
    private GameObject _settingsPopup;
    private Image      _musicToggleImg;

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
        // Sunrise Meadows world map is PLAY's destination (since 2026-07-15). The old grid-based
        // LevelSelectController was removed entirely 2026-07-26 — it was still silently reacting
        // to GameState.Idle in the background, racing WorldMapController to show itself on every
        // Idle transition (same event, same Canvas sortingOrder). World 2+ should get its own
        // map screen built on the WorldMapController pattern when that content exists, not a
        // resurrected LevelSelectController.
        WorldMapController.Instance?.Show();
    }

    // Re-added 2026-07-10 (was removed 2026-07-07, user request to simplify the landing page —
    // now restored, also user request), then rebuilt into a fuller settings screen the same day
    // ("on navigate place [Scoreboard.png] stretch across the screen; list the following
    // headings 'Player' 'High Scores' 'Worlds' 'Saved Game' 'Music Toggle' overlaying the
    // scoreboard backdrop"). Reuses the same AudioManager.MusicEnabled/SfxEnabled + PlayerPrefs-
    // backed state HUDController's top-right mute button already toggles, so the two screens
    // can't disagree about whether audio is on.
    void OnSettingsClicked()
    {
        _musicToggleImg.color = AudioManager.MusicEnabled ? ToggleOnColor : ToggleOffColor;
        _settingsPopup.SetActive(true);
    }

    void OnSettingsCloseClicked() => _settingsPopup.SetActive(false);

    // Single "Music Toggle" heading controls both music and SFX together (consolidated from the
    // old separate MUSIC/SFX buttons 2026-07-10 to match the one-heading list the user asked
    // for) — same combined on/off semantics as HUDController's top-right mute button.
    void OnMusicToggleClicked()
    {
        bool on = !AudioManager.MusicEnabled;
        AudioManager.SetMusicEnabled(on);
        AudioManager.SetSfxEnabled(on);
        _musicToggleImg.color = on ? ToggleOnColor : ToggleOffColor;
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

    // Settings popup opened by the gear icon — rebuilt 2026-07-10 into a fuller menu screen
    // (was a small plain-colour MUSIC/SFX toggle box) per user request: Scoreboard.png as the
    // backdrop, stretched large ("across the screen"), with 5 headings listed down it —
    // 'Player', 'High Scores', 'Worlds', 'Saved Game', 'Music Toggle'. Only Music Toggle is
    // wired to real behaviour (see OnMusicToggleClicked) — Player/High Scores/Worlds/Saved Game
    // have no backing systems anywhere else in this project yet (no player profile, no
    // leaderboard, no World 2+ map, no save-slot concept beyond the existing PlayerPrefs
    // autosave), so they render as plain heading labels for now rather than invented features;
    // wire real navigation/behaviour to each once those systems exist. Same self-contained
    // dismiss-catcher-behind-a-box pattern used elsewhere (MatchUpScreen/LevelPreviewCard).
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

        // Scoreboard.png re-supplied by the user 2026-07-10 at a new native aspect — was
        // ~653:382 (1.71:1), now 1600x1200 (4:3, 1.333:1) — noticeably taller per unit width, so
        // the old 1600x938 box (sized for the previous aspect) no longer matches and everything
        // positioned on it needed re-fitting. 1000 tall x 1333 wide keeps the new 4:3 ratio
        // undistorted while comfortably fitting the 1920x1080 reference canvas with margin on
        // every side (the board is taller-per-width now, so it can't stretch as wide as the old
        // art without overflowing top/bottom).
        var box = new GameObject("Box");
        box.transform.SetParent(popGO.transform, false);
        var boxRT = box.AddComponent<RectTransform>();
        boxRT.anchorMin        = new Vector2(0.5f, 0.5f);
        boxRT.anchorMax        = new Vector2(0.5f, 0.5f);
        boxRT.pivot            = new Vector2(0.5f, 0.5f);
        boxRT.sizeDelta        = new Vector2(1333f, 1000f);
        var boxImg = box.AddComponent<Image>();
        boxImg.sprite = _scoreboardSprite != null ? _scoreboardSprite : _squareSpr;
        boxImg.color  = _scoreboardSprite != null ? Color.white : new Color(0.97f, 0.94f, 0.88f);
        var boxBtn = box.AddComponent<Button>(); // swallows clicks so the box itself never dismisses
        boxBtn.transition = Selectable.Transition.None;

        var titleTMP = MakeToggleLabel(box.transform, "Title", "SETTINGS",
            new Vector2(0f, 380f), new Vector2(600f, 80f), 46f, Color.white);
        titleTMP.fontStyle = TMPro.FontStyles.Bold;

        // Heading list, evenly spaced down the parchment area (re-fitted to the new taller 4:3
        // board — was 4 rows from y=190 down; now 5 rows from y=220 down, with more vertical
        // room to work with). Player/High Scores/Worlds/Saved Game are static labels (no
        // feature to wire yet — see class comment above); Music Toggle is a real button.
        string[] staticHeadings = { "Player", "High Scores", "Worlds", "Saved Game" };
        float    y              = 220f;
        const float rowSpacing  = 120f;
        foreach (var heading in staticHeadings)
        {
            MakeToggleLabel(box.transform, heading.Replace(" ", "") + "Heading", heading,
                new Vector2(0f, y), new Vector2(700f, 70f), 36f, Color.white);
            y -= rowSpacing;
        }

        var musicBtn = MakeToggleButton(box.transform, "MusicToggle", "Music Toggle", new Vector2(0f, y));
        _musicToggleImg = musicBtn.targetGraphic as Image;
        musicBtn.onClick.AddListener(OnMusicToggleClicked);

        var closeBtn = MakeToggleButton(box.transform, "CloseBtn", "CLOSE", new Vector2(0f, -440f));
        closeBtn.onClick.AddListener(OnSettingsCloseClicked);

        _settingsPopup.SetActive(false);
    }

    Button MakeToggleButton(Transform parent, string name, string label, Vector2 pos, float width = 260f)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0.5f, 0.5f);
        rt.anchorMax        = new Vector2(0.5f, 0.5f);
        rt.pivot            = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta        = new Vector2(width, 64f);
        var img = go.AddComponent<Image>();
        img.sprite = _squareSpr;
        img.color  = ToggleOffColor;
        MakeToggleLabel(go.transform, "Label", label, Vector2.zero, new Vector2(width, 64f), 26f, Color.white);
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
        StyleAsGameFont(tmp);
        return tmp;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    // Bold + black outline + gold-to-orange gradient — the game's bubble-lettering look, same
    // recipe as HUDController.StyleAsGameNumber() (that one's private to HUDController, so
    // duplicated here rather than exposed cross-class for one shared call). Added 2026-07-10 —
    // user request to apply "the game font" to the settings popup's title/headings, which had
    // been built as plain flat-coloured TMP text.
    static void StyleAsGameFont(TMPro.TextMeshProUGUI tmp)
    {
        tmp.color               = Color.white; // must be white or it tints the gradient below
        tmp.fontStyle            = TMPro.FontStyles.Bold;
        tmp.enableVertexGradient = true;
        tmp.colorGradient        = new VertexGradient(
            new Color(1.00f, 0.87f, 0.40f), new Color(1.00f, 0.87f, 0.40f),
            new Color(0.95f, 0.55f, 0.05f), new Color(0.95f, 0.55f, 0.05f));
        var mat = tmp.fontMaterial; // instance, safe to edit without affecting other TMP text
        mat.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.22f);
        mat.SetColor(ShaderUtilities.ID_OutlineColor, Color.black);
        tmp.fontMaterial = mat;
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
