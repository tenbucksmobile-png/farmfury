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
    // Btn_plaque.png (2026-07-16, user-supplied) — replaces the flat-colour box that used to sit
    // behind the 7 settings tab "headings" (AUDIO/GAMEPLAY/STATS/SCORES/STORY/ACCOUNT/ABOUT —
    // "headings" is this project's own established term for these, see the BuildSettingsPopup
    // class comment referencing the old pre-tab flat-heading-list layout). Only the tab buttons
    // use this — every other MakeToggleButton call (Close, Tutorial Replay, Story/Inert rows,
    // etc.) keeps the plain _squareSpr backdrop it always had, since those are action buttons,
    // not headings.
    [SerializeField] private Sprite _plaqueSprite;

    // Btn_quit.png (2026-07-16, user request) — Settings popup's close icon, replacing the old
    // "SETTINGS" text header + text "X" close button entirely.
    [SerializeField] private Sprite _quitCloseButtonSprite;

    // Btn_off.png/Btn_on.png (2026-07-16, user-supplied star icons) — the Music/SFX enabled
    // toggles' icon-mode visual (see MakeStateToggle's offSprite/onSprite params). Only the
    // genuine binary on/off switches use these, not every MakeStateToggle call.
    [SerializeField] private Sprite _toggleOffSprite;
    [SerializeField] private Sprite _toggleOnSprite;

    private GameObject _panel;
    private Sprite     _squareSpr;
    private GameObject _settingsPopup;

    // Audio tab refresh state (2026-07-13) — set once when the tab is built, re-applied every
    // time the popup opens (OnSettingsClicked) so it reflects any change made elsewhere (e.g. the
    // HUD's own combined top-right Mute button) rather than going stale between opens.
    private System.Action<bool>  _setMusicToggleVisual;
    private System.Action<bool>  _setSfxToggleVisual;
    private Slider                _musicVolumeSlider;
    private Slider                _sfxVolumeSlider;
    private TextMeshProUGUI       _musicVolumeLabel;
    private TextMeshProUGUI       _sfxVolumeLabel;

    // Stats tab (2026-07-13) — each row is a value label + a getter, refreshed fresh every time
    // the popup opens (RefreshStatsTab, called from OnSettingsClicked) rather than computed once
    // at build time, since ScoreManager/PlayerStatsTracker data changes between menu visits.
    private readonly System.Collections.Generic.List<(TextMeshProUGUI label, System.Func<string> getText)> _statRows = new();

    // Story tab reader (2026-07-13) — one reusable popup for all 4 story categories, rather than
    // 4 separate panels, since they all share the same "title + paginated body text" shape.
    private GameObject        _storyReaderPopup;
    private TextMeshProUGUI   _storyReaderHeader;   // "CHARACTER PROFILES · 3 / 8"
    private TextMeshProUGUI   _storyReaderTitle;    // entry's own title, e.g. "Cluck the Chicken — Cluster Bomb"
    private TextMeshProUGUI   _storyReaderBody;
    private Button            _storyReaderPrevBtn;
    private Button            _storyReaderNextBtn;
    private string            _storyReaderCategoryName;
    private StoryContent.Entry[] _storyReaderEntries;
    private int                  _storyReaderPageIndex;

    private static readonly Color ToggleOnColor  = new(0.20f, 0.65f, 0.30f);
    private static readonly Color ToggleOffColor = new(0.45f, 0.45f, 0.45f);

    // ── Settings popup tabs (2026-07-13 redesign — see BuildSettingsPopup) ──────
    // Gameplay tab (Language/Left-Right Handed/Tutorial Replay) removed entirely 2026-07-16 per
    // explicit user request ("remove 'Gameplay'") — that content has no other home right now, so
    // it's gone, not hidden; re-add a tab for it (or fold it into another tab) if it's wanted back.
    enum SettingsTab { Audio, Stats, Scores, Story, Account, About }
    private readonly System.Collections.Generic.Dictionary<SettingsTab, GameObject> _tabPanels = new();
    private readonly System.Collections.Generic.Dictionary<SettingsTab, Image>      _tabButtonImages = new();
    private SettingsTab _activeTab = SettingsTab.Audio;

    private static readonly Color TabOnColor  = new(0.95f, 0.55f, 0.05f);
    private static readonly Color TabOffColor = new(0.45f, 0.45f, 0.45f);

    // 2026-07-16, user report (screenshot) — settings popup body text was plain white on
    // Scoreboard.png's light cream/parchment interior, badly low-contrast. Used for every label
    // that sits directly on that backdrop (row titles, headers, version text, etc.) — NOT for
    // text that sits on top of a button's own colored/plaque art (MakeToggleButton's label,
    // still white there — decent contrast on orange/grey/plaque backgrounds).
    private static readonly Color SettingsTextColor    = new(0.22f, 0.14f, 0.07f);
    private static readonly Color SettingsSubTextColor = new(0.40f, 0.28f, 0.16f, 0.9f);

    // ── Settings "page" navigation (2026-07-16 redesign) — tapping a tab heading now navigates
    // away from the tab list to a distinct content page (with a Back button), instead of showing
    // the selected tab's content underneath the tab grid in the same view. See SelectTab/ShowTabList.
    private GameObject _settingsTabGridRoot;
    private GameObject _settingsContentRoot;
    private GameObject _settingsBackButton;

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
    void OnSettingsClicked() => OpenSettings();

    // Public entry point (2026-07-19) — the Settings popup used to be reachable only via this
    // class's own gear icon, since it lived as a child of MainMenuController's own landing-page
    // Canvas (_panel) and was therefore only ever active while _panel itself was. Any OTHER
    // screen (e.g. World2LandingController) needed the identical popup without also forcing
    // MainMenuController's own background/PLAY/SETTINGS buttons to show underneath it — see
    // BuildSettingsPopup below, which now gives the popup its own independent Canvas instead of
    // parenting it under _panel. Behaviour from THIS class's own gear icon is unchanged.
    public void OpenSettings()
    {
        _setMusicToggleVisual(AudioManager.MusicEnabled);
        _setSfxToggleVisual(AudioManager.SfxEnabled);
        _musicVolumeSlider.SetValueWithoutNotify(AudioManager.MusicVolume);
        _musicVolumeLabel.text = Mathf.RoundToInt(AudioManager.MusicVolume * 100f) + "%";
        _sfxVolumeSlider.SetValueWithoutNotify(AudioManager.SfxVolume);
        _sfxVolumeLabel.text = Mathf.RoundToInt(AudioManager.SfxVolume * 100f) + "%";
        RefreshStatsTab();
        ShowTabList(); // always open on the tab list, not straight into a tab's content page
        _settingsPopup.SetActive(true);
    }

    void RefreshStatsTab()
    {
        foreach (var (label, getText) in _statRows)
            label.text = getText();
    }

    // Description label (left-aligned) + value label (right-aligned, gold, filled by
    // RefreshStatsTab on every popup open) — one row of the Stats tab's two columns.
    void AddStatRow(Transform parent, string description, Vector2 pos, System.Func<string> valueGetter)
    {
        MakeToggleLabel(parent, "StatDesc_" + description, description,
            new Vector2(pos.x - 130f, pos.y), new Vector2(280f, 46f), 26f, SettingsTextColor, useGameFont: false)
            .alignment = TMPro.TextAlignmentOptions.Left;
        var valueLabel = MakeToggleLabel(parent, "StatVal_" + description, "",
            new Vector2(pos.x + 130f, pos.y), new Vector2(220f, 46f), 26f, new Color(0.55f, 0.38f, 0.05f), useGameFont: false);
        valueLabel.alignment = TMPro.TextAlignmentOptions.Right;
        _statRows.Add((valueLabel, valueGetter));
    }

    // Navigates FROM the tab list TO that tab's content page (see the class comment on
    // _settingsTabGridRoot) — hides the tab grid, shows the content area.
    void SelectTab(SettingsTab tab)
    {
        _activeTab = tab;
        foreach (var kv in _tabPanels)
            kv.Value.SetActive(kv.Key == tab);
        foreach (var kv in _tabButtonImages)
            kv.Value.color = kv.Key == tab ? TabOnColor : TabOffColor;

        _settingsTabGridRoot.SetActive(false);
        _settingsContentRoot.SetActive(true);
    }

    // Navigates back FROM a tab's content page TO the tab list — the settings popup's default/
    // landing view (see OnSettingsClicked and the end of BuildSettingsPopup).
    void ShowTabList()
    {
        _settingsTabGridRoot.SetActive(true);
        _settingsContentRoot.SetActive(false);
    }

    // Single shared Quit/Back icon's click handler (2026-07-16) — contextual: back out to the tab
    // list if a tab's content page is currently showing, otherwise fully close Settings. Replaces
    // the old separate Close + Back icons (see the QuitBackBtn construction comment).
    void OnQuitOrBackClicked()
    {
        if (_settingsContentRoot != null && _settingsContentRoot.activeSelf)
            ShowTabList();
        else
            OnSettingsCloseClicked();
    }

    void OnSettingsCloseClicked() => _settingsPopup.SetActive(false);

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

        BuildSettingsPopup();

        _panel.SetActive(false);
    }

    // Settings popup opened by the gear icon — redesigned 2026-07-13 into a tabbed layout (was
    // a flat list of 5 headings down the Scoreboard.png backdrop: 'Player', 'High Scores',
    // 'Worlds', 'Saved Game', 'Music Toggle', added 2026-07-10). User supplied a 7-tab mockup —
    // AUDIO/GAMEPLAY/STATS/SCORES/STORY/ACCOUNT/ABOUT in a 2-column grid, title bar with a
    // top-right [X] close, divider, then a content area below that swaps per selected tab.
    // Building tab-by-tab per explicit user direction ("will build each one separately") — this
    // pass is the shell + tab-switching only. Audio is the one tab with real content, migrated
    // in from the old flat layout's Music Toggle button (see OnMusicToggleClicked) so that
    // existing functionality isn't orphaned by the redesign. The other 6 are empty containers in
    // _tabPanels, ready for content in later passes — just build inside the matching container,
    // no structural changes needed here. Same self-contained dismiss-catcher-behind-a-box pattern
    // used elsewhere (MatchUpScreen/LevelPreviewCard).
    // Own top-level Canvas (2026-07-19), separate from _panel/cvGO — a settings popup parented
    // under _panel would only ever be active while _panel was, which made it unreachable from
    // any screen other than this one (see OpenSettings' comment above). sortingOrder 410 sits
    // above MainMenuController's own landing Canvas (400) and WorldMapController's map Canvas
    // (300), so it always renders on top regardless of which screen opened it.
    void BuildSettingsPopup()
    {
        var settingsCvGO = new GameObject("SettingsCanvas");
        settingsCvGO.transform.SetParent(transform, false);
        var settingsCv = settingsCvGO.AddComponent<Canvas>();
        settingsCv.renderMode   = RenderMode.ScreenSpaceOverlay;
        settingsCv.sortingOrder = 410;
        var settingsCs = settingsCvGO.AddComponent<CanvasScaler>();
        settingsCs.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        settingsCs.referenceResolution = new Vector2(1920f, 1080f);
        settingsCs.screenMatchMode     = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        settingsCs.matchWidthOrHeight  = 0.5f;
        settingsCvGO.AddComponent<GraphicRaycaster>();

        var popGO = new GameObject("SettingsPopup");
        popGO.transform.SetParent(settingsCvGO.transform, false);
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

        // Scoreboard.png re-supplied 2026-07-16, larger and now 2:1 (2048x1024, was 4:3 1600x1200)
        // — 900 tall x 1800 wide keeps the new ratio undistorted while still fitting the 1920x1080
        // reference canvas with margin (60px horizontal, 90px vertical). The box is noticeably
        // SHORTER than before (1000 -> 900, was 4:3 so taller relative to its width) — every
        // vertical position/size below this point is scaled by VScale to match, so nothing that
        // used to fit inside the old box spills past the new, shorter backdrop art. Horizontal
        // positions are untouched — the box only got wider, never narrower, so nothing can clip
        // sideways from leaving those alone.
        const float VScale = 0.9f; // 900/1000 — new box height ÷ old box height
        var box = new GameObject("Box");
        box.transform.SetParent(popGO.transform, false);
        var boxRT = box.AddComponent<RectTransform>();
        boxRT.anchorMin        = new Vector2(0.5f, 0.5f);
        boxRT.anchorMax        = new Vector2(0.5f, 0.5f);
        boxRT.pivot            = new Vector2(0.5f, 0.5f);
        boxRT.sizeDelta        = new Vector2(1800f, 900f);
        var boxImg = box.AddComponent<Image>();
        boxImg.sprite = _scoreboardSprite != null ? _scoreboardSprite : _squareSpr;
        boxImg.color  = _scoreboardSprite != null ? Color.white : new Color(0.97f, 0.94f, 0.88f);
        var boxBtn = box.AddComponent<Button>(); // swallows clicks so the box itself never dismisses
        boxBtn.transition = Selectable.Transition.None;

        // ── Quit/Back icon — 2026-07-16, user request: was two separate Btn_quit.png icons (a
        // top-right Close + a top-left Back), which read as a redundant "secondary" duplicate
        // since both showed the same art at once whenever a tab's content page was open. Now a
        // single icon, always visible, whose tap target is contextual — OnQuitOrBackClicked()
        // below checks whether a tab's content is currently showing and either backs out to the
        // tab list or fully closes Settings. Enlarged to 150x150 (this project's standard icon
        // button size — matches the landing page's PLAY/SETTINGS icons and HUDController's
        // top-right row) and moved down from y=400 to y=300 (pre-VScale) to sit clear of the
        // frame's top corner medallion art instead of overlapping it. Nudged further IN toward
        // the board's centre (x=600->500, y=300->260 pre-VScale) 2026-07-16, user report
        // (screenshot): it was still sitting on the wooden border/corner medallion rather than
        // the cream board itself. Single shared button, so this position applies to the tab list
        // and every tab's content page automatically — no per-page duplication.
        var quitCloseGO = new GameObject("QuitBackBtn");
        quitCloseGO.transform.SetParent(box.transform, false);
        var quitCloseRT = quitCloseGO.AddComponent<RectTransform>();
        quitCloseRT.anchorMin        = new Vector2(0.5f, 0.5f);
        quitCloseRT.anchorMax        = new Vector2(0.5f, 0.5f);
        quitCloseRT.pivot            = new Vector2(0.5f, 0.5f);
        quitCloseRT.anchoredPosition = new Vector2(500f, 260f * VScale);
        quitCloseRT.sizeDelta        = new Vector2(150f, 150f);
        var quitCloseImg = quitCloseGO.AddComponent<Image>();
        quitCloseImg.sprite = _quitCloseButtonSprite != null ? _quitCloseButtonSprite : _squareSpr;
        quitCloseImg.color  = _quitCloseButtonSprite != null ? Color.white : new Color(0.7f, 0.15f, 0.1f);
        quitCloseImg.preserveAspect = true;
        var quitCloseBtn = quitCloseGO.AddComponent<Button>();
        quitCloseBtn.targetGraphic = quitCloseImg;
        quitCloseBtn.onClick.AddListener(OnQuitOrBackClicked);
        _settingsBackButton = quitCloseGO; // kept for compatibility, no longer shown/hidden per-page

        // ── Divider — permanent header/body separator, shown on both the tab list and every
        // tab's content page ─────────────────────────────────────────────────────
        var divider = new GameObject("Divider");
        divider.transform.SetParent(box.transform, false);
        var divRT = divider.AddComponent<RectTransform>();
        divRT.anchorMin        = new Vector2(0.5f, 0.5f);
        divRT.anchorMax        = new Vector2(0.5f, 0.5f);
        divRT.pivot            = new Vector2(0.5f, 0.5f);
        divRT.anchoredPosition = new Vector2(0f, 340f * VScale);
        divRT.sizeDelta        = new Vector2(1150f, 4f);
        var divImg = divider.AddComponent<Image>();
        divImg.sprite = _squareSpr;
        divImg.color  = new Color(1f, 1f, 1f, 0.4f);

        // ── Tab list: single column, one plaque per row (2026-07-16 redesign — was a 2-column
        // grid; GAMEPLAY removed per explicit user request; realigned to a single column and
        // widened so each plaque reads as a real button rather than a narrow tab strip). Grouped
        // under TabGridRoot so the whole list can be hidden in one call once a tab is selected
        // (see SelectTab/ShowTabList — "new page" navigation instead of an inline panel swap).
        var tabGridRootGO = new GameObject("TabGridRoot");
        tabGridRootGO.transform.SetParent(box.transform, false);
        var tabGridRT = tabGridRootGO.AddComponent<RectTransform>();
        tabGridRT.anchorMin        = new Vector2(0.5f, 0.5f);
        tabGridRT.anchorMax        = new Vector2(0.5f, 0.5f);
        tabGridRT.pivot            = new Vector2(0.5f, 0.5f);
        tabGridRT.anchoredPosition = Vector2.zero;
        tabGridRT.sizeDelta        = Vector2.zero;
        _settingsTabGridRoot = tabGridRootGO;

        (SettingsTab tab, string label)[] tabDefs =
        {
            (SettingsTab.Audio,   "AUDIO"),
            (SettingsTab.Stats,   "STATS"),
            (SettingsTab.Scores,  "SCORES"),
            (SettingsTab.Story,   "STORY"),
            (SettingsTab.Account, "ACCOUNT"),
            (SettingsTab.About,   "ABOUT"),
        };
        // 2026-07-16, user report (screenshot): tabWidth was 1100 — nearly the full board width,
        // reading as stretched bars rather than buttons wrapping their own text. Also the plaque
        // art (Btn_plaque.png, native ~1.9:1) was rendering heavily flattened at the old 1100x64
        // size (~17:1) — width narrowed and height raised together so the plaque reads closer to
        // its own proportions instead of a thin stretched strip, while still comfortably fitting
        // "ACCOUNT"/"GAMEPLAY"-length labels. Centered (x=0) per explicit user confirmation
        // ("you can middle align") rather than left/right-aligned now that it no longer spans the
        // board's full width.
        const float tabRowY0    = 290f * VScale;
        const float tabRowSpace = 120f * VScale;
        const float tabWidth    = 460f;
        const float tabHeight   = 92f;
        for (int i = 0; i < tabDefs.Length; i++)
        {
            var (tab, label) = tabDefs[i];
            float y = tabRowY0 - i * tabRowSpace;
            var tabBtn = MakeToggleButton(tabGridRootGO.transform, tab + "Tab", label, new Vector2(0f, y), tabWidth, _plaqueSprite, tabHeight);
            _tabButtonImages[tab] = tabBtn.targetGraphic as Image;
            tabBtn.onClick.AddListener(() => SelectTab(tab));
        }

        // ── Content area — one container per tab, only the active one enabled. Sized to use the
        // room the tab list vacates once a tab is selected (see SelectTab), not squeezed
        // underneath it — enlarged from the old inline-panel-swap layout's 1150x306. ──
        var contentRoot = new GameObject("Content");
        contentRoot.transform.SetParent(box.transform, false);
        _settingsContentRoot = contentRoot;
        // anchoredPosition raised -80->20 (2026-07-16, user report/screenshot: "text... still a
        // little low" — the lowest rows, e.g. Stats' "Time Played", were sitting close enough to
        // the bottom edge to read as touching the wood frame). Since every tab's content lives
        // inside this one shared container, this single change lifts every tab's text uniformly
        // rather than needing each tab's row positions re-tuned individually.
        var contentRT = contentRoot.AddComponent<RectTransform>();
        contentRT.anchorMin        = new Vector2(0.5f, 0.5f);
        contentRT.anchorMax        = new Vector2(0.5f, 0.5f);
        contentRT.pivot            = new Vector2(0.5f, 0.5f);
        contentRT.anchoredPosition = new Vector2(0f, 20f * VScale);
        contentRT.sizeDelta        = new Vector2(1150f, 640f * VScale);

        foreach (var (tab, _) in tabDefs)
        {
            var panel = new GameObject(tab + "Panel");
            panel.transform.SetParent(contentRoot.transform, false);
            var panelRT = panel.AddComponent<RectTransform>();
            panelRT.anchorMin = Vector2.zero;
            panelRT.anchorMax = Vector2.one;
            panelRT.offsetMin = panelRT.offsetMax = Vector2.zero;
            _tabPanels[tab] = panel;
        }

        // ── Audio tab content (2026-07-13) — Music/SFX are now independent toggles (were a
        // single combined "Music Toggle" in the old flat layout) plus a 0-100 volume slider for
        // each. Row layout: label on the left, control on the right, per the user's mockup.
        // Voice/Dialogue was in the original mockup but dropped per explicit user decision — no
        // voice-line audio content exists anywhere in this project to back it.
        // Row Y's spread out 2026-07-16 (user report, screenshot) to use the room the enlarged
        // content area (see contentRT above) actually has now, instead of staying clustered near
        // the top at the old cramped spacing. Label font 30->38, toggle icon 64->88 (see
        // MakeStateToggle's iconSize param), volume slider/percent enlarged to match (see
        // MakeVolumeSlider) — all "enlarge text and toggle buttons, nicely spaced" per that report.
        var audioTab = _tabPanels[SettingsTab.Audio].transform;
        const float audioLabelX = -330f;
        const float audioCtrlX  = 340f;

        // "MUSIC" page header (2026-07-16, user report/screenshot) — large uppercase title at the
        // top of the content page, 2x the row label font size (38*2=76). Rows shifted up to sit
        // close beneath it instead of being vertically centered with a big empty gap above them.
        MakeToggleLabel(audioTab, "AudioHeader", "MUSIC",
            new Vector2(0f, 260f * VScale), new Vector2(1000f, 100f), 76f, SettingsTextColor, useGameFont: false)
            .fontStyle = TMPro.FontStyles.Bold;

        MakeToggleLabel(audioTab, "MusicLabel", "Music",
            new Vector2(audioLabelX, 130f * VScale), new Vector2(480f, 70f), 38f, SettingsTextColor, useGameFont: false)
            .alignment = TMPro.TextAlignmentOptions.Left;
        var (musicToggleBtn, setMusicVisual) = MakeStateToggle(audioTab, "MusicToggle",
            new Vector2(audioCtrlX, 130f * VScale), AudioManager.MusicEnabled, AudioManager.SetMusicEnabled,
            offSprite: _toggleOffSprite, onSprite: _toggleOnSprite, iconSize: 88f);
        _setMusicToggleVisual = setMusicVisual;

        MakeToggleLabel(audioTab, "SfxLabel", "Sound Effects",
            new Vector2(audioLabelX, 20f * VScale), new Vector2(480f, 70f), 38f, SettingsTextColor, useGameFont: false)
            .alignment = TMPro.TextAlignmentOptions.Left;
        var (sfxToggleBtn, setSfxVisual) = MakeStateToggle(audioTab, "SfxToggle",
            new Vector2(audioCtrlX, 20f * VScale), AudioManager.SfxEnabled, AudioManager.SetSfxEnabled,
            offSprite: _toggleOffSprite, onSprite: _toggleOnSprite, iconSize: 88f);
        _setSfxToggleVisual = setSfxVisual;

        (_musicVolumeSlider, _musicVolumeLabel) = MakeVolumeSlider(audioTab, "MusicVolume",
            "Music Volume", new Vector2(0f, -90f * VScale), AudioManager.MusicVolume, AudioManager.SetMusicVolume);

        (_sfxVolumeSlider, _sfxVolumeLabel) = MakeVolumeSlider(audioTab, "SfxVolume",
            "SFX Volume", new Vector2(0f, -200f * VScale), AudioManager.SfxVolume, AudioManager.SetSfxVolume);

        // ── Stats tab content (2026-07-13) ───────────────────────────────────────
        // Two columns: Statistics (7 rows, real per-level data aggregated from ScoreManager plus
        // new PlayerStatsTracker counters) and Personal Records (2 rows). Denominators use
        // GameManager.Instance.TotalLevels (currently 18, auto-discovered — not hardcoded) rather
        // than the full 6-world design target (108 levels/324 stars), per explicit user decision
        // — showing progress against content that doesn't exist yet would be permanently
        // unreachable. "Longest Combo Chain" and "Most Stars in One Attempt" were both dropped
        // per user decision — no combo mechanic exists, and the latter was redundant with
        // Perfect Levels below it.
        var statsTab = _tabPanels[SettingsTab.Stats].transform;

        // "STATS" page header — same large-uppercase treatment as Audio's "MUSIC" (2026-07-16).
        MakeToggleLabel(statsTab, "PageHeader", "STATS",
            new Vector2(0f, 260f * VScale), new Vector2(1000f, 100f), 76f, SettingsTextColor, useGameFont: false)
            .fontStyle = TMPro.FontStyles.Bold;

        MakeToggleLabel(statsTab, "StatsHeader", "STATISTICS",
            new Vector2(-280f, 170f * VScale), new Vector2(500f, 50f), 30f, SettingsTextColor, useGameFont: false).fontStyle = TMPro.FontStyles.Bold;

        // 7 rows spaced 65 apart (was 40) — "space out evenly across the board" per the
        // screenshot report, using the room the enlarged content area actually has.
        AddStatRow(statsTab, "Total Stars Earned", new Vector2(-280f, 105f * VScale), () =>
        {
            int total = GameManager.Instance != null ? GameManager.Instance.TotalLevels : 0;
            int stars = 0;
            for (int i = 0; i < total; i++) stars += ScoreManager.GetBestStars(i);
            return $"{stars} / {total * 3}";
        });
        AddStatRow(statsTab, "Levels Completed", new Vector2(-280f, 40f * VScale), () =>
        {
            int total = GameManager.Instance != null ? GameManager.Instance.TotalLevels : 0;
            int done = 0;
            for (int i = 0; i < total; i++) if (ScoreManager.GetBestStars(i) >= 1) done++;
            return $"{done} / {total}";
        });
        AddStatRow(statsTab, "Total Score", new Vector2(-280f, -25f * VScale), () =>
        {
            int total = GameManager.Instance != null ? GameManager.Instance.TotalLevels : 0;
            int score = 0;
            for (int i = 0; i < total; i++) score += ScoreManager.GetBestScore(i);
            return score.ToString("N0");
        });
        AddStatRow(statsTab, "Cannonballs Fired", new Vector2(-280f, -90f * VScale), () =>
            PlayerStatsTracker.TotalCannonballsFired.ToString("N0"));
        AddStatRow(statsTab, "Robots Destroyed", new Vector2(-280f, -155f * VScale), () =>
            PlayerStatsTracker.TotalRobotsDestroyed.ToString("N0"));
        AddStatRow(statsTab, "Favourite Animal", new Vector2(-280f, -220f * VScale), () =>
        {
            var animal = PlayerStatsTracker.GetFavouriteAnimal(out int uses);
            return uses > 0 ? $"{animal} ({uses})" : "—";
        });
        AddStatRow(statsTab, "Time Played", new Vector2(-280f, -285f * VScale), () =>
        {
            int totalMinutes = Mathf.FloorToInt(PlayerStatsTracker.TimePlayedSeconds / 60f);
            return $"{totalMinutes / 60}h {totalMinutes % 60}m";
        });

        MakeToggleLabel(statsTab, "RecordsHeader", "PERSONAL RECORDS",
            new Vector2(280f, 170f * VScale), new Vector2(500f, 50f), 30f, SettingsTextColor, useGameFont: false).fontStyle = TMPro.FontStyles.Bold;

        AddStatRow(statsTab, "Highest Score", new Vector2(280f, 105f * VScale), () =>
        {
            int total = GameManager.Instance != null ? GameManager.Instance.TotalLevels : 0;
            int bestLevel = -1, bestScore = 0;
            for (int i = 0; i < total; i++)
            {
                int s = ScoreManager.GetBestScore(i);
                if (s > bestScore) { bestScore = s; bestLevel = i; }
            }
            return bestLevel >= 0 ? $"L{bestLevel + 1} — {bestScore:N0}" : "—";
        });
        AddStatRow(statsTab, "Perfect Levels", new Vector2(280f, 20f * VScale), () =>
        {
            int total = GameManager.Instance != null ? GameManager.Instance.TotalLevels : 0;
            int perfect = 0;
            for (int i = 0; i < total; i++) if (ScoreManager.GetBestStars(i) == 3) perfect++;
            return $"{perfect} / {total} 3-starred";
        });

        // ── Story tab content (2026-07-13) ───────────────────────────────────────
        // 4 rows, each opening the shared reader popup (BuildStoryReaderPopup below) at page 0
        // for that category. "Robot Enemy Files" covers the 4 real robot TYPES, not "18 pages,
        // one per robot" as originally specced — see StoryContent.cs's class comment for why.
        // Row Y's spread out 2026-07-16 (same "review the spacing" report as Audio/About) to use
        // the enlarged content area instead of staying clustered near the top. "STORY" page
        // header added (same large-uppercase treatment as Audio's "MUSIC") — rows shifted down to
        // clear it.
        var storyTab = _tabPanels[SettingsTab.Story].transform;
        MakeToggleLabel(storyTab, "PageHeader", "STORY",
            new Vector2(0f, 260f * VScale), new Vector2(1000f, 100f), 76f, SettingsTextColor, useGameFont: false)
            .fontStyle = TMPro.FontStyles.Bold;

        AddStoryRow(storyTab, "The Farm Fury Story", "Read the origin story",
            new Vector2(0f, 160f * VScale), "The Farm Fury Story", StoryContent.Story);
        AddStoryRow(storyTab, "Character Profiles", "8 pages, one per animal",
            new Vector2(0f, 40f * VScale), "Character Profiles", StoryContent.Characters);
        AddStoryRow(storyTab, "Robot Enemy Files", "4 pages, one per robot type",
            new Vector2(0f, -80f * VScale), "Robot Enemy Files", StoryContent.Robots);
        AddStoryRow(storyTab, "World Journal", "6 pages, one per world",
            new Vector2(0f, -200f * VScale), "World Journal", StoryContent.Worlds);

        BuildStoryReaderPopup(settingsCvGO.transform);

        // ── About tab content (2026-07-13) ───────────────────────────────────────
        // Privacy Policy / Terms of Service / Support-Contact / Rate The App / Restore Purchases
        // are all inert placeholders per explicit user decision — none have real backing (no
        // hosted legal pages, no support email, no live store listing, no IAP system at all).
        // Restore Purchases in particular is flagged clearly: per Apple's own App Store review
        // guidelines this is a hard submission requirement, not just a nice-to-have — it MUST be
        // wired to a real IAP system (Unity IAP/RevenueCat, per the GDD's Phase 6 monetisation
        // plan) before this app can ship to iOS. Credits opens the shared story reader with real
        // verifiable facts (Unity, Kling AI) plus placeholder Team/Special Thanks sections. App
        // Version is the one fully real row — reads Application.version live, no placeholder.
        // Row Y's spread out 2026-07-16 (same "review the spacing" report as Audio/Story) — 7 rows
        // evenly spaced across the enlarged content area instead of the old ~45 unit gaps, shifted
        // down to clear the new "ABOUT" page header (same large-uppercase treatment as elsewhere).
        var aboutTab = _tabPanels[SettingsTab.About].transform;
        MakeToggleLabel(aboutTab, "PageHeader", "ABOUT",
            new Vector2(0f, 260f * VScale), new Vector2(1000f, 100f), 76f, SettingsTextColor, useGameFont: false)
            .fontStyle = TMPro.FontStyles.Bold;

        // Spacing tightened 80->65 (matching Stats) 2026-07-16, same "still a little low" report —
        // 7 rows now end higher, with more margin above the bottom frame.
        AddInertRow(aboutTab, "Privacy Policy", "No hosted policy yet", new Vector2(0f, 170f * VScale));
        AddInertRow(aboutTab, "Terms of Service", "No hosted terms yet", new Vector2(0f, 105f * VScale));
        AddStoryRow(aboutTab, "Credits", "Team, tools, and attributions",
            new Vector2(0f, 40f * VScale), "Credits", StoryContent.Credits);
        AddInertRow(aboutTab, "Support / Contact", "No support email set yet", new Vector2(0f, -25f * VScale));

        MakeToggleLabel(aboutTab, "VersionLabel", "App Version",
            new Vector2(-260f, -90f * VScale), new Vector2(400f, 46f), 28f, SettingsTextColor, useGameFont: false)
            .alignment = TMPro.TextAlignmentOptions.Left;
        MakeToggleLabel(aboutTab, "VersionValue", Application.version,
            new Vector2(260f, -90f * VScale), new Vector2(300f, 46f), 28f, new Color(0.55f, 0.38f, 0.05f), useGameFont: false)
            .alignment = TMPro.TextAlignmentOptions.Right;

        AddInertRow(aboutTab, "Rate The App", "No store listing yet (app not published)", new Vector2(0f, -155f * VScale));
        AddInertRow(aboutTab, "Restore Purchases", "NOT FUNCTIONAL — no IAP system exists yet (App Store submission blocker)", new Vector2(0f, -220f * VScale));

        // Remaining 2 tabs — empty placeholder text, built out in a future pass. Each given its
        // own page header for consistency with every other tab. Scores never actually had its
        // "Coming Soon" label built (found while touching every tab this pass, not previously
        // reported) — its panel was completely blank, not just under-styled; fixed alongside this.
        MakeToggleLabel(_tabPanels[SettingsTab.Account].transform, "PageHeader", "ACCOUNT",
            new Vector2(0f, 260f * VScale), new Vector2(1000f, 100f), 76f, SettingsTextColor, useGameFont: false)
            .fontStyle = TMPro.FontStyles.Bold;
        MakeToggleLabel(_tabPanels[SettingsTab.Account].transform, "ComingSoon", "Coming Soon",
            Vector2.zero, new Vector2(900f, 80f), 32f, SettingsTextColor, useGameFont: false);

        MakeToggleLabel(_tabPanels[SettingsTab.Scores].transform, "PageHeader", "SCORES",
            new Vector2(0f, 260f * VScale), new Vector2(1000f, 100f), 76f, SettingsTextColor, useGameFont: false)
            .fontStyle = TMPro.FontStyles.Bold;
        MakeToggleLabel(_tabPanels[SettingsTab.Scores].transform, "ComingSoon", "Coming Soon",
            Vector2.zero, new Vector2(900f, 80f), 32f, SettingsTextColor, useGameFont: false);

        ShowTabList();
        _settingsPopup.SetActive(false);
    }

    // Row for a not-yet-backed feature — title + explanatory subtitle on the left, a dimmed
    // non-interactable button on the right so it visually reads as "present but inert" rather
    // than a broken/missing control.
    void AddInertRow(Transform parent, string title, string subtitle, Vector2 pos)
    {
        MakeToggleLabel(parent, title.Replace(" ", "").Replace("/", "") + "Title", title,
            new Vector2(-260f, pos.y + 10f), new Vector2(560f, 42f), 28f, SettingsTextColor, useGameFont: false)
            .alignment = TMPro.TextAlignmentOptions.Left;
        var sub = MakeToggleLabel(parent, title.Replace(" ", "").Replace("/", "") + "Sub", subtitle,
            new Vector2(-260f, pos.y - 18f), new Vector2(560f, 32f), 17f, SettingsSubTextColor, useGameFont: false);
        sub.alignment = TMPro.TextAlignmentOptions.Left;
        sub.fontStyle = TMPro.FontStyles.Italic;

        var btn = MakeToggleButton(parent, title.Replace(" ", "").Replace("/", "") + "Btn",
            "N/A", new Vector2(420f, pos.y), 180f);
        btn.interactable = false;
        var img = btn.targetGraphic as Image;
        if (img != null) img.color = new Color(0.3f, 0.3f, 0.3f, 0.6f);
    }

    // One row: title + subtitle stacked on the left, a "Read"/"View" button on the right that
    // opens the shared story reader popup for that category.
    void AddStoryRow(Transform parent, string title, string subtitle, Vector2 pos, string categoryName, StoryContent.Entry[] entries)
    {
        MakeToggleLabel(parent, title.Replace(" ", "") + "Title", title,
            new Vector2(-260f, pos.y + 16f), new Vector2(540f, 46f), 32f, SettingsTextColor, useGameFont: false)
            .alignment = TMPro.TextAlignmentOptions.Left;
        var sub = MakeToggleLabel(parent, title.Replace(" ", "") + "Sub", subtitle,
            new Vector2(-260f, pos.y - 20f), new Vector2(540f, 34f), 20f, SettingsSubTextColor, useGameFont: false);
        sub.alignment = TMPro.TextAlignmentOptions.Left;
        sub.fontStyle = TMPro.FontStyles.Italic;

        var btn = MakeToggleButton(parent, title.Replace(" ", "") + "Btn",
            entries.Length > 1 ? "VIEW" : "READ", new Vector2(420f, pos.y), 220f);
        btn.onClick.AddListener(() => OpenStoryReader(categoryName, entries));
    }

    // Shared full-panel reader for all 4 Story categories — built once, sits on top of
    // SettingsPopup (same root canvas, later in sibling order). "Back" returns to Settings, which
    // is still active underneath.
    void BuildStoryReaderPopup(Transform root)
    {
        var popGO = new GameObject("StoryReaderPopup");
        popGO.transform.SetParent(root, false);
        var popRT = popGO.AddComponent<RectTransform>();
        popRT.anchorMin = Vector2.zero;
        popRT.anchorMax = Vector2.one;
        popRT.offsetMin = popRT.offsetMax = Vector2.zero;
        _storyReaderPopup = popGO;

        var dismissImg = popGO.AddComponent<Image>();
        dismissImg.sprite = _squareSpr;
        dismissImg.color  = new Color(0f, 0f, 0f, 0.7f);
        var dismissBtn = popGO.AddComponent<Button>();
        dismissBtn.targetGraphic = dismissImg;
        dismissBtn.onClick.AddListener(() => _storyReaderPopup.SetActive(false));

        // Box matched to the exact same size as the Settings popup's own box (1800x900) —
        // 2026-07-16, user report (screenshot): this used to be a different size (1600x900), and
        // since Settings stays active underneath while the reader is open (see the class comment
        // above — "Back" just hides this popup, doesn't need to re-show Settings), the mismatched
        // sizes left the wider Settings box's wooden frame visibly peeking out from behind the
        // narrower reader box — a "secondary board" ghosting behind the real one. Same size +
        // same centered position means they now sit perfectly coincident — only one frame ever
        // shows through, matching the fix requested.
        var box = new GameObject("Box");
        box.transform.SetParent(popGO.transform, false);
        var boxRT = box.AddComponent<RectTransform>();
        boxRT.anchorMin        = new Vector2(0.5f, 0.5f);
        boxRT.anchorMax        = new Vector2(0.5f, 0.5f);
        boxRT.pivot            = new Vector2(0.5f, 0.5f);
        boxRT.sizeDelta        = new Vector2(1800f, 900f);
        var boxImg = box.AddComponent<Image>();
        boxImg.sprite = _scoreboardSprite != null ? _scoreboardSprite : _squareSpr;
        boxImg.color  = _scoreboardSprite != null ? Color.white : new Color(0.97f, 0.94f, 0.88f);
        var boxBtn = box.AddComponent<Button>(); // swallows clicks so the box itself never dismisses
        boxBtn.transition = Selectable.Transition.None;

        // Safe-interior container — 2026-07-16, same report: header/title/body text was
        // overlapping/bleeding onto the wooden frame border art (the frame's real border is
        // thicker, relative to the box, than the old hardcoded 1400-wide/edge-to-edge text
        // positions assumed). RectMask2D hard-clips anything that still doesn't fit rather than
        // relying purely on getting the inset numbers exactly right — text is guaranteed to never
        // visually escape onto the frame even if the border's actual thickness isn't measured
        // perfectly here. Sized/positioned well inside the frame's inner cream area, leaving the
        // Prev/Next/Back buttons below (already positioned to clear the bottom bar) outside it.
        var contentContainer = new GameObject("SafeContent");
        contentContainer.transform.SetParent(box.transform, false);
        var containerRT = contentContainer.AddComponent<RectTransform>();
        containerRT.anchorMin        = new Vector2(0.5f, 0.5f);
        containerRT.anchorMax        = new Vector2(0.5f, 0.5f);
        containerRT.pivot            = new Vector2(0.5f, 0.5f);
        containerRT.anchoredPosition = new Vector2(0f, 40f);
        containerRT.sizeDelta        = new Vector2(1180f, 620f);
        contentContainer.AddComponent<RectMask2D>();

        _storyReaderHeader = MakeToggleLabel(contentContainer.transform, "Header", "",
            new Vector2(0f, 270f), new Vector2(1100f, 50f), 22f, SettingsSubTextColor, useGameFont: false);
        _storyReaderTitle = MakeToggleLabel(contentContainer.transform, "Title", "",
            new Vector2(0f, 220f), new Vector2(1100f, 60f), 32f, SettingsTextColor, useGameFont: false);
        _storyReaderTitle.fontStyle = TMPro.FontStyles.Bold;

        // Body text — NOT built via MakeToggleLabel (that helper hardcodes centered, no-wrap,
        // gold-gradient styling meant for short UI labels, wrong for a multi-paragraph body).
        // Auto-sizing so any entry length (the Origin Story is ~280 words, character/robot/world
        // entries are ~80-100) always fits the box without needing a real scroll view.
        var bodyGO = new GameObject("Body");
        bodyGO.transform.SetParent(contentContainer.transform, false);
        var bodyRT = bodyGO.AddComponent<RectTransform>();
        bodyRT.anchorMin        = new Vector2(0.5f, 0.5f);
        bodyRT.anchorMax        = new Vector2(0.5f, 0.5f);
        bodyRT.pivot            = new Vector2(0.5f, 1f);
        bodyRT.anchoredPosition = new Vector2(0f, 170f);
        bodyRT.sizeDelta        = new Vector2(1100f, 420f);
        _storyReaderBody = bodyGO.AddComponent<TextMeshProUGUI>();
        _storyReaderBody.alignment          = TMPro.TextAlignmentOptions.TopLeft;
        _storyReaderBody.enableWordWrapping = true;
        _storyReaderBody.enableAutoSizing   = true;
        _storyReaderBody.fontSizeMin        = 16f;
        _storyReaderBody.fontSizeMax        = 28f;
        _storyReaderBody.color              = new Color(0.15f, 0.10f, 0.05f); // dark ink on the parchment backdrop
        _storyReaderBody.raycastTarget      = false;

        _storyReaderPrevBtn = MakeToggleButton(box.transform, "PrevBtn", "< PREV", new Vector2(-450f, -330f), 220f);
        _storyReaderPrevBtn.onClick.AddListener(() => { _storyReaderPageIndex--; RefreshStoryReaderPage(); });

        _storyReaderNextBtn = MakeToggleButton(box.transform, "NextBtn", "NEXT >", new Vector2(450f, -330f), 220f);
        _storyReaderNextBtn.onClick.AddListener(() => { _storyReaderPageIndex++; RefreshStoryReaderPage(); });

        var backBtn = MakeToggleButton(box.transform, "BackBtn", "BACK", new Vector2(0f, -330f), 220f);
        backBtn.onClick.AddListener(() => _storyReaderPopup.SetActive(false));

        _storyReaderPopup.SetActive(false);
    }

    void OpenStoryReader(string categoryName, StoryContent.Entry[] entries)
    {
        _storyReaderCategoryName = categoryName;
        _storyReaderEntries      = entries;
        _storyReaderPageIndex    = 0;
        RefreshStoryReaderPage();
        _storyReaderPopup.SetActive(true);
    }

    void RefreshStoryReaderPage()
    {
        int count = _storyReaderEntries.Length;
        _storyReaderPageIndex = Mathf.Clamp(_storyReaderPageIndex, 0, count - 1);
        var entry = _storyReaderEntries[_storyReaderPageIndex];

        _storyReaderHeader.text = count > 1
            ? $"{_storyReaderCategoryName.ToUpper()} · {_storyReaderPageIndex + 1} / {count}"
            : _storyReaderCategoryName.ToUpper();
        _storyReaderTitle.text = entry.Title;
        _storyReaderBody.text  = entry.Body;

        bool multiPage = count > 1;
        _storyReaderPrevBtn.gameObject.SetActive(multiPage);
        _storyReaderNextBtn.gameObject.SetActive(multiPage);
        _storyReaderPrevBtn.interactable = _storyReaderPageIndex > 0;
        _storyReaderNextBtn.interactable = _storyReaderPageIndex < count - 1;
    }

    // Small ON/OFF pill toggle, distinct from MakeToggleButton (whose own label text IS the
    // description) — used for the Settings > Audio rows, where the description sits in a
    // separate label to the left and this control just shows the current state. Returns both the
    // Button and a setVisual delegate so the caller can re-sync colour+text on popup open without
    // needing its own field bookkeeping per toggle.
    //
    // offSprite/onSprite (2026-07-16, user-supplied Btn_off.png/Btn_on.png) — when both are set,
    // this renders as a plain icon that swaps sprite on toggle (no colour tint, no text label)
    // instead of the original text pill. Used for Music/SFX enabled and the Left/Right Handed
    // toggle (all 3 MakeStateToggle calls in this file, 2026-07-16) — the onLabel/offLabel
    // params are ignored in icon mode, so Left/Right Handed no longer shows which state means
    // which hand on the control itself; the row's own "Left/Right Handed" label + note below it
    // are the only remaining indication. Volume sliders (MakeVolumeSlider) were explicitly left
    // untouched — not the same control, not passed icons.
    (Button, System.Action<bool>) MakeStateToggle(Transform parent, string name, Vector2 pos, bool initialOn, System.Action<bool> onToggle, string onLabel = "ON", string offLabel = "OFF", float width = 160f, Sprite offSprite = null, Sprite onSprite = null, float iconSize = 64f)
    {
        bool useIcons = offSprite != null && onSprite != null;

        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0.5f, 0.5f);
        rt.anchorMax        = new Vector2(0.5f, 0.5f);
        rt.pivot            = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta        = useIcons ? new Vector2(iconSize, iconSize) : new Vector2(width, 56f);
        var img = go.AddComponent<Image>();
        TMPro.TextMeshProUGUI label = null;
        if (useIcons)
        {
            img.sprite = initialOn ? onSprite : offSprite;
            img.color  = Color.white;
        }
        else
        {
            img.sprite = _squareSpr;
            label = MakeToggleLabel(go.transform, "Label", onLabel, Vector2.zero, new Vector2(width, 56f), 26f, Color.white);
        }
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;

        void SetVisual(bool on)
        {
            if (useIcons)
            {
                img.sprite = on ? onSprite : offSprite;
            }
            else
            {
                img.color  = on ? ToggleOnColor : ToggleOffColor;
                label.text = on ? onLabel : offLabel;
            }
        }
        SetVisual(initialOn);

        bool state = initialOn;
        btn.onClick.AddListener(() =>
        {
            state = !state;
            SetVisual(state);
            onToggle(state);
        });
        return (btn, (System.Action<bool>)SetVisual);
    }

    // 0-100 volume slider with a left-aligned description label and a right-side "NN%" readout.
    // Returns the Slider (so callers can SetValueWithoutNotify on popup open) and the percent
    // label (so that readout can be refreshed alongside it without re-triggering onChanged).
    (Slider, TextMeshProUGUI) MakeVolumeSlider(Transform parent, string name, string label, Vector2 pos, float initialValue01, System.Action<float> onChanged)
    {
        MakeToggleLabel(parent, name + "Label", label,
            new Vector2(pos.x - 330f, pos.y), new Vector2(480f, 70f), 34f, SettingsTextColor, useGameFont: false)
            .alignment = TMPro.TextAlignmentOptions.Left;

        var go = new GameObject(name + "Slider");
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0.5f, 0.5f);
        rt.anchorMax        = new Vector2(0.5f, 0.5f);
        rt.pivot            = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(pos.x + 250f, pos.y);
        rt.sizeDelta        = new Vector2(340f, 48f);

        var bgGO = new GameObject("Background");
        bgGO.transform.SetParent(go.transform, false);
        var bgRT = bgGO.AddComponent<RectTransform>();
        bgRT.anchorMin = new Vector2(0f, 0.35f);
        bgRT.anchorMax = new Vector2(1f, 0.65f);
        bgRT.offsetMin = bgRT.offsetMax = Vector2.zero;
        var bgImg = bgGO.AddComponent<Image>();
        bgImg.sprite = _squareSpr;
        bgImg.color  = ToggleOffColor;

        var fillAreaGO = new GameObject("Fill Area");
        fillAreaGO.transform.SetParent(go.transform, false);
        var fillAreaRT = fillAreaGO.AddComponent<RectTransform>();
        fillAreaRT.anchorMin = new Vector2(0f, 0.35f);
        fillAreaRT.anchorMax = new Vector2(1f, 0.65f);
        fillAreaRT.offsetMin = new Vector2(4f, 0f);
        fillAreaRT.offsetMax = new Vector2(-4f, 0f);

        var fillGO = new GameObject("Fill");
        fillGO.transform.SetParent(fillAreaGO.transform, false);
        var fillRT = fillGO.AddComponent<RectTransform>();
        fillRT.anchorMin = Vector2.zero;
        fillRT.anchorMax = new Vector2(0f, 1f); // width driven by Slider itself via fillRect
        fillRT.offsetMin = fillRT.offsetMax = Vector2.zero;
        var fillImg = fillGO.AddComponent<Image>();
        fillImg.sprite = _squareSpr;
        fillImg.color  = TabOnColor;

        var handleAreaGO = new GameObject("Handle Slide Area");
        handleAreaGO.transform.SetParent(go.transform, false);
        var handleAreaRT = handleAreaGO.AddComponent<RectTransform>();
        handleAreaRT.anchorMin = Vector2.zero;
        handleAreaRT.anchorMax = Vector2.one;
        handleAreaRT.offsetMin = handleAreaRT.offsetMax = Vector2.zero;

        var handleGO = new GameObject("Handle");
        handleGO.transform.SetParent(handleAreaGO.transform, false);
        var handleRT = handleGO.AddComponent<RectTransform>();
        handleRT.sizeDelta = new Vector2(30f, 48f);
        var handleImg = handleGO.AddComponent<Image>();
        handleImg.sprite = _squareSpr;
        handleImg.color  = Color.white;

        var slider = go.AddComponent<Slider>();
        slider.fillRect      = fillRT;
        slider.handleRect    = handleRT;
        slider.targetGraphic = handleImg;
        slider.direction     = Slider.Direction.LeftToRight;
        slider.minValue      = 0f;
        slider.maxValue      = 1f;

        var percentLabel = MakeToggleLabel(parent, name + "Percent", "",
            new Vector2(pos.x + 460f, pos.y), new Vector2(110f, 70f), 30f, SettingsTextColor, useGameFont: false);

        slider.onValueChanged.AddListener(v =>
        {
            percentLabel.text = Mathf.RoundToInt(v * 100f) + "%";
            onChanged(v);
        });
        slider.value = initialValue01; // fires the listener once, setting the initial readout text

        return (slider, percentLabel);
    }

    // Tap-to-cycle selector — used for Language (2026-07-13) in place of a real TMP_Dropdown.
    // A true dropdown needs a manually-built template/viewport/item hierarchy (this project has
    // no prefabs and builds all UI procedurally, same as every other control in this file) —
    // picked the simpler, more robust option per explicit user decision rather than risk a broken
    // dropdown list with no Play mode access to verify it. Tapping advances to the next option in
    // `options`, wrapping around at the end. Returns a setVisual delegate so the caller can
    // re-sync the displayed text on popup open.
    (Button, System.Action<string>) MakeCycleButton(Transform parent, string name, Vector2 pos, string[] options, string initialValue, System.Action<string> onChanged, float width = 320f)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0.5f, 0.5f);
        rt.anchorMax        = new Vector2(0.5f, 0.5f);
        rt.pivot            = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta        = new Vector2(width, 56f);
        var img = go.AddComponent<Image>();
        img.sprite = _squareSpr;
        img.color  = ToggleOffColor;
        var label = MakeToggleLabel(go.transform, "Label", initialValue, Vector2.zero, new Vector2(width, 56f), 26f, Color.white);
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;

        void SetVisual(string value) => label.text = value;

        int index = System.Array.IndexOf(options, initialValue);
        if (index < 0) index = 0;
        btn.onClick.AddListener(() =>
        {
            index = (index + 1) % options.Length;
            string value = options[index];
            SetVisual(value);
            onChanged(value);
        });
        return (btn, (System.Action<string>)SetVisual);
    }

    // bgSprite defaults to null, which keeps every existing call site's plain _squareSpr backdrop
    // unchanged — only the 7 settings tab buttons pass _plaqueSprite (see BuildSettingsPopup).
    Button MakeToggleButton(Transform parent, string name, string label, Vector2 pos, float width = 260f, Sprite bgSprite = null, float height = 64f)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0.5f, 0.5f);
        rt.anchorMax        = new Vector2(0.5f, 0.5f);
        rt.pivot            = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta        = new Vector2(width, height);
        var img = go.AddComponent<Image>();
        img.sprite = bgSprite != null ? bgSprite : _squareSpr;
        img.color  = ToggleOffColor;
        MakeToggleLabel(go.transform, "Label", label, Vector2.zero, new Vector2(width, height), 26f, Color.white);
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        return btn;
    }

    // useGameFont (2026-07-16 fix): StyleAsGameFont() unconditionally forced tmp.color back to
    // white plus a gold vertex gradient AFTER the `color` param was applied — every "darken the
    // text" call this session (SettingsTextColor/SettingsSubTextColor throughout the settings
    // popup) was silently discarded, which is why the Stats header still rendered gold in the
    // screenshot despite being passed a dark colour. Defaults true so every existing caller that
    // actually wants the gold bubble-lettering look (tab plaque labels, MakeToggleButton's own
    // label, etc.) is unaffected; every body-text call site that passes a dark colour now also
    // passes useGameFont: false so that colour actually renders.
    static TMPro.TextMeshProUGUI MakeToggleLabel(Transform parent, string name, string text, Vector2 pos, Vector2 size, float fontSize, Color color, bool useGameFont = true)
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
        if (useGameFont) StyleAsGameFont(tmp);
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
