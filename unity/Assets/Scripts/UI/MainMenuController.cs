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

    // Audio tab refresh state (2026-07-13) — set once when the tab is built, re-applied every
    // time the popup opens (OnSettingsClicked) so it reflects any change made elsewhere (e.g. the
    // HUD's own combined top-right Mute button) rather than going stale between opens.
    private System.Action<bool>  _setMusicToggleVisual;
    private System.Action<bool>  _setSfxToggleVisual;
    private Slider                _musicVolumeSlider;
    private Slider                _sfxVolumeSlider;
    private TextMeshProUGUI       _musicVolumeLabel;
    private TextMeshProUGUI       _sfxVolumeLabel;
    private System.Action<string> _setLanguageVisual;
    private System.Action<bool>   _setHandedVisual;

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
    // Only Audio has real content today (the pre-existing Music Toggle, migrated in from the old
    // flat heading-list layout). The other 6 are empty containers, built one at a time in future
    // passes — each just needs its content built inside the matching entry of _tabPanels.
    enum SettingsTab { Audio, Gameplay, Stats, Scores, Story, Account, About }
    private readonly System.Collections.Generic.Dictionary<SettingsTab, GameObject> _tabPanels = new();
    private readonly System.Collections.Generic.Dictionary<SettingsTab, Image>      _tabButtonImages = new();
    private SettingsTab _activeTab = SettingsTab.Audio;

    private static readonly Color TabOnColor  = new(0.95f, 0.55f, 0.05f);
    private static readonly Color TabOffColor = new(0.45f, 0.45f, 0.45f);

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
        _setMusicToggleVisual(AudioManager.MusicEnabled);
        _setSfxToggleVisual(AudioManager.SfxEnabled);
        _musicVolumeSlider.SetValueWithoutNotify(AudioManager.MusicVolume);
        _musicVolumeLabel.text = Mathf.RoundToInt(AudioManager.MusicVolume * 100f) + "%";
        _sfxVolumeSlider.SetValueWithoutNotify(AudioManager.SfxVolume);
        _sfxVolumeLabel.text = Mathf.RoundToInt(AudioManager.SfxVolume * 100f) + "%";
        _setLanguageVisual(GameplaySettings.Language);
        _setHandedVisual(GameplaySettings.LeftHanded);
        RefreshStatsTab();
        SelectTab(SettingsTab.Audio); // always open on the one tab with real content today
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
            new Vector2(pos.x - 130f, pos.y), new Vector2(260f, 40f), 22f, Color.white)
            .alignment = TMPro.TextAlignmentOptions.Left;
        var valueLabel = MakeToggleLabel(parent, "StatVal_" + description, "",
            new Vector2(pos.x + 130f, pos.y), new Vector2(220f, 40f), 22f, new Color(1f, 0.87f, 0.4f));
        valueLabel.alignment = TMPro.TextAlignmentOptions.Right;
        _statRows.Add((valueLabel, valueGetter));
    }

    // No dedicated tutorial overlay exists — L01 itself is the de facto tutorial (see
    // LevelDataGenerator.cs's "Tutorial level" comment on L01), so this just jumps straight into
    // it, closing both the settings popup and the main menu panel first (same as OnPlayClicked).
    void OnTutorialReplayClicked()
    {
        _settingsPopup.SetActive(false);
        _panel.SetActive(false);
        GameManager.Instance?.ForceStartLevel(0);
    }

    void SelectTab(SettingsTab tab)
    {
        _activeTab = tab;
        foreach (var kv in _tabPanels)
            kv.Value.SetActive(kv.Key == tab);
        foreach (var kv in _tabButtonImages)
            kv.Value.color = kv.Key == tab ? TabOnColor : TabOffColor;
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

        BuildSettingsPopup(root);

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

        // Scoreboard.png (4:3, 1600x1200) — 1000 tall x 1333 wide keeps that ratio undistorted
        // while comfortably fitting the 1920x1080 reference canvas with margin on every side.
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

        // ── Title bar: "SETTINGS" + top-right [X] close ─────────────────────────
        var titleTMP = MakeToggleLabel(box.transform, "Title", "SETTINGS",
            new Vector2(-40f, 400f), new Vector2(600f, 80f), 46f, Color.white);
        titleTMP.fontStyle   = TMPro.FontStyles.Bold;
        titleTMP.alignment   = TMPro.TextAlignmentOptions.Left;

        var closeBtn = MakeToggleButton(box.transform, "CloseBtn", "X", new Vector2(600f, 400f), 70f);
        closeBtn.onClick.AddListener(OnSettingsCloseClicked);

        // ── Tab grid: 2 columns, mockup order (AUDIO/GAMEPLAY, STATS/SCORES, STORY/ACCOUNT,
        // ABOUT alone on the last row, centered) ────────────────────────────────
        (SettingsTab tab, string label)[] tabDefs =
        {
            (SettingsTab.Audio,    "AUDIO"),
            (SettingsTab.Gameplay, "GAMEPLAY"),
            (SettingsTab.Stats,    "STATS"),
            (SettingsTab.Scores,   "SCORES"),
            (SettingsTab.Story,    "STORY"),
            (SettingsTab.Account,  "ACCOUNT"),
            (SettingsTab.About,    "ABOUT"),
        };
        const float colX        = 300f;
        const float tabRowY0    = 280f;
        const float tabRowSpace = 90f;
        for (int i = 0; i < tabDefs.Length; i++)
        {
            var (tab, label) = tabDefs[i];
            int row = i / 2;
            bool lastOdd = i == tabDefs.Length - 1 && tabDefs.Length % 2 == 1;
            float x = lastOdd ? 0f : (i % 2 == 0 ? -colX : colX);
            float y = tabRowY0 - row * tabRowSpace;
            var tabBtn = MakeToggleButton(box.transform, tab + "Tab", label, new Vector2(x, y), 540f);
            _tabButtonImages[tab] = tabBtn.targetGraphic as Image;
            tabBtn.onClick.AddListener(() => SelectTab(tab));
        }

        // ── Divider ───────────────────────────────────────────────────────────
        var divider = new GameObject("Divider");
        divider.transform.SetParent(box.transform, false);
        var divRT = divider.AddComponent<RectTransform>();
        divRT.anchorMin        = new Vector2(0.5f, 0.5f);
        divRT.anchorMax        = new Vector2(0.5f, 0.5f);
        divRT.pivot            = new Vector2(0.5f, 0.5f);
        divRT.anchoredPosition = new Vector2(0f, -70f);
        divRT.sizeDelta        = new Vector2(1150f, 4f);
        var divImg = divider.AddComponent<Image>();
        divImg.sprite = _squareSpr;
        divImg.color  = new Color(1f, 1f, 1f, 0.4f);

        // ── Content area — one container per tab, only the active one enabled ──
        var contentRoot = new GameObject("Content");
        contentRoot.transform.SetParent(box.transform, false);
        var contentRT = contentRoot.AddComponent<RectTransform>();
        contentRT.anchorMin        = new Vector2(0.5f, 0.5f);
        contentRT.anchorMax        = new Vector2(0.5f, 0.5f);
        contentRT.pivot            = new Vector2(0.5f, 0.5f);
        contentRT.anchoredPosition = new Vector2(0f, -300f);
        contentRT.sizeDelta        = new Vector2(1150f, 340f);

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
        var audioTab = _tabPanels[SettingsTab.Audio].transform;
        const float audioLabelX = -330f;
        const float audioCtrlX  = 340f;

        MakeToggleLabel(audioTab, "MusicLabel", "Music",
            new Vector2(audioLabelX, 130f), new Vector2(480f, 60f), 30f, Color.white)
            .alignment = TMPro.TextAlignmentOptions.Left;
        var (musicToggleBtn, setMusicVisual) = MakeStateToggle(audioTab, "MusicToggle",
            new Vector2(audioCtrlX, 130f), AudioManager.MusicEnabled, AudioManager.SetMusicEnabled);
        _setMusicToggleVisual = setMusicVisual;

        MakeToggleLabel(audioTab, "SfxLabel", "Sound Effects",
            new Vector2(audioLabelX, 60f), new Vector2(480f, 60f), 30f, Color.white)
            .alignment = TMPro.TextAlignmentOptions.Left;
        var (sfxToggleBtn, setSfxVisual) = MakeStateToggle(audioTab, "SfxToggle",
            new Vector2(audioCtrlX, 60f), AudioManager.SfxEnabled, AudioManager.SetSfxEnabled);
        _setSfxToggleVisual = setSfxVisual;

        (_musicVolumeSlider, _musicVolumeLabel) = MakeVolumeSlider(audioTab, "MusicVolume",
            "Music Volume", new Vector2(0f, -20f), AudioManager.MusicVolume, AudioManager.SetMusicVolume);

        (_sfxVolumeSlider, _sfxVolumeLabel) = MakeVolumeSlider(audioTab, "SfxVolume",
            "SFX Volume", new Vector2(0f, -100f), AudioManager.SfxVolume, AudioManager.SetSfxVolume);

        // ── Gameplay tab content (2026-07-13) ────────────────────────────────────
        // Language: real control, persisted, but no localization framework exists anywhere in
        // this project — only English has any actual translated text behind it (see
        // GameplaySettings.cs). Left/Right Handed: persists the preference only, per explicit
        // user decision — no camera/aim-math/physics mirroring is implemented yet; that's a
        // separate, bigger pass. Tutorial Replay: there's no dedicated tutorial overlay/hint
        // system in this project — L01 itself is the de facto tutorial (see LevelDataGenerator.cs
        // comment), so this just jumps straight into L01 gameplay via the same
        // GameManager.ForceStartLevel() path MatchUpScreen's own Skip button uses.
        var gameplayTab = _tabPanels[SettingsTab.Gameplay].transform;

        MakeToggleLabel(gameplayTab, "LanguageLabel", "Language",
            new Vector2(audioLabelX, 130f), new Vector2(480f, 60f), 30f, Color.white)
            .alignment = TMPro.TextAlignmentOptions.Left;
        var (_, setLanguageVisual) = MakeCycleButton(gameplayTab, "LanguageCycle",
            new Vector2(audioCtrlX, 130f), GameplaySettings.SupportedLanguages, GameplaySettings.Language,
            GameplaySettings.SetLanguage);
        _setLanguageVisual = setLanguageVisual;

        MakeToggleLabel(gameplayTab, "HandedLabel", "Left/Right Handed",
            new Vector2(audioLabelX, 40f), new Vector2(480f, 80f), 30f, Color.white)
            .alignment = TMPro.TextAlignmentOptions.Left;
        var (_, setHandedVisual) = MakeStateToggle(gameplayTab, "HandedToggle",
            new Vector2(audioCtrlX, 40f), GameplaySettings.LeftHanded, GameplaySettings.SetLeftHanded,
            onLabel: "Left-Handed", offLabel: "Right-Handed", width: 220f);
        _setHandedVisual = setHandedVisual;
        MakeToggleLabel(gameplayTab, "HandedNote", "(mirrors the layout — cannon on right instead of left)",
            new Vector2(0f, -10f), new Vector2(1100f, 40f), 20f, new Color(1f, 1f, 1f, 0.7f));

        var tutorialBtn = MakeToggleButton(gameplayTab, "TutorialReplayBtn",
            "Replay Tutorial (Level 1)", new Vector2(0f, -110f), 500f);
        tutorialBtn.onClick.AddListener(OnTutorialReplayClicked);

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

        MakeToggleLabel(statsTab, "StatsHeader", "STATISTICS",
            new Vector2(-280f, 170f), new Vector2(500f, 50f), 28f, Color.white).fontStyle = TMPro.FontStyles.Bold;

        AddStatRow(statsTab, "Total Stars Earned", new Vector2(-280f, 130f), () =>
        {
            int total = GameManager.Instance != null ? GameManager.Instance.TotalLevels : 0;
            int stars = 0;
            for (int i = 0; i < total; i++) stars += ScoreManager.GetBestStars(i);
            return $"{stars} / {total * 3}";
        });
        AddStatRow(statsTab, "Levels Completed", new Vector2(-280f, 90f), () =>
        {
            int total = GameManager.Instance != null ? GameManager.Instance.TotalLevels : 0;
            int done = 0;
            for (int i = 0; i < total; i++) if (ScoreManager.GetBestStars(i) >= 1) done++;
            return $"{done} / {total}";
        });
        AddStatRow(statsTab, "Total Score", new Vector2(-280f, 50f), () =>
        {
            int total = GameManager.Instance != null ? GameManager.Instance.TotalLevels : 0;
            int score = 0;
            for (int i = 0; i < total; i++) score += ScoreManager.GetBestScore(i);
            return score.ToString("N0");
        });
        AddStatRow(statsTab, "Cannonballs Fired", new Vector2(-280f, 10f), () =>
            PlayerStatsTracker.TotalCannonballsFired.ToString("N0"));
        AddStatRow(statsTab, "Robots Destroyed", new Vector2(-280f, -30f), () =>
            PlayerStatsTracker.TotalRobotsDestroyed.ToString("N0"));
        AddStatRow(statsTab, "Favourite Animal", new Vector2(-280f, -70f), () =>
        {
            var animal = PlayerStatsTracker.GetFavouriteAnimal(out int uses);
            return uses > 0 ? $"{animal} ({uses})" : "—";
        });
        AddStatRow(statsTab, "Time Played", new Vector2(-280f, -110f), () =>
        {
            int totalMinutes = Mathf.FloorToInt(PlayerStatsTracker.TimePlayedSeconds / 60f);
            return $"{totalMinutes / 60}h {totalMinutes % 60}m";
        });

        MakeToggleLabel(statsTab, "RecordsHeader", "PERSONAL RECORDS",
            new Vector2(280f, 170f), new Vector2(500f, 50f), 28f, Color.white).fontStyle = TMPro.FontStyles.Bold;

        AddStatRow(statsTab, "Highest Score", new Vector2(280f, 130f), () =>
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
        AddStatRow(statsTab, "Perfect Levels", new Vector2(280f, 90f), () =>
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
        var storyTab = _tabPanels[SettingsTab.Story].transform;
        AddStoryRow(storyTab, "The Farm Fury Story", "Read the origin story",
            new Vector2(0f, 120f), "The Farm Fury Story", StoryContent.Story);
        AddStoryRow(storyTab, "Character Profiles", "8 pages, one per animal",
            new Vector2(0f, 50f), "Character Profiles", StoryContent.Characters);
        AddStoryRow(storyTab, "Robot Enemy Files", "4 pages, one per robot type",
            new Vector2(0f, -20f), "Robot Enemy Files", StoryContent.Robots);
        AddStoryRow(storyTab, "World Journal", "6 pages, one per world",
            new Vector2(0f, -90f), "World Journal", StoryContent.Worlds);

        BuildStoryReaderPopup(root);

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
        var aboutTab = _tabPanels[SettingsTab.About].transform;

        AddInertRow(aboutTab, "Privacy Policy", "No hosted policy yet", new Vector2(0f, 140f));
        AddInertRow(aboutTab, "Terms of Service", "No hosted terms yet", new Vector2(0f, 95f));
        AddStoryRow(aboutTab, "Credits", "Team, tools, and attributions",
            new Vector2(0f, 50f), "Credits", StoryContent.Credits);
        AddInertRow(aboutTab, "Support / Contact", "No support email set yet", new Vector2(0f, 5f));

        MakeToggleLabel(aboutTab, "VersionLabel", "App Version",
            new Vector2(-260f, -40f), new Vector2(400f, 40f), 26f, Color.white)
            .alignment = TMPro.TextAlignmentOptions.Left;
        MakeToggleLabel(aboutTab, "VersionValue", Application.version,
            new Vector2(260f, -40f), new Vector2(300f, 40f), 26f, new Color(1f, 0.87f, 0.4f))
            .alignment = TMPro.TextAlignmentOptions.Right;

        AddInertRow(aboutTab, "Rate The App", "No store listing yet (app not published)", new Vector2(0f, -85f));
        AddInertRow(aboutTab, "Restore Purchases", "NOT FUNCTIONAL — no IAP system exists yet (App Store submission blocker)", new Vector2(0f, -130f));

        // Remaining 1 tab — empty placeholder text, built out in a future pass.
        MakeToggleLabel(_tabPanels[SettingsTab.Account].transform, "ComingSoon", "Coming Soon",
            Vector2.zero, new Vector2(900f, 80f), 32f, Color.white);

        SelectTab(SettingsTab.Audio);
        _settingsPopup.SetActive(false);
    }

    // Row for a not-yet-backed feature — title + explanatory subtitle on the left, a dimmed
    // non-interactable button on the right so it visually reads as "present but inert" rather
    // than a broken/missing control.
    void AddInertRow(Transform parent, string title, string subtitle, Vector2 pos)
    {
        MakeToggleLabel(parent, title.Replace(" ", "").Replace("/", "") + "Title", title,
            new Vector2(-260f, pos.y + 8f), new Vector2(560f, 36f), 24f, Color.white)
            .alignment = TMPro.TextAlignmentOptions.Left;
        var sub = MakeToggleLabel(parent, title.Replace(" ", "").Replace("/", "") + "Sub", subtitle,
            new Vector2(-260f, pos.y - 14f), new Vector2(560f, 28f), 15f, new Color(1f, 1f, 1f, 0.55f));
        sub.alignment = TMPro.TextAlignmentOptions.Left;
        sub.fontStyle = TMPro.FontStyles.Italic;

        var btn = MakeToggleButton(parent, title.Replace(" ", "").Replace("/", "") + "Btn",
            "N/A", new Vector2(400f, pos.y), 180f);
        btn.interactable = false;
        var img = btn.targetGraphic as Image;
        if (img != null) img.color = new Color(0.3f, 0.3f, 0.3f, 0.6f);
    }

    // One row: title + subtitle stacked on the left, a "Read"/"View" button on the right that
    // opens the shared story reader popup for that category.
    void AddStoryRow(Transform parent, string title, string subtitle, Vector2 pos, string categoryName, StoryContent.Entry[] entries)
    {
        MakeToggleLabel(parent, title.Replace(" ", "") + "Title", title,
            new Vector2(-260f, pos.y + 12f), new Vector2(540f, 40f), 28f, Color.white)
            .alignment = TMPro.TextAlignmentOptions.Left;
        var sub = MakeToggleLabel(parent, title.Replace(" ", "") + "Sub", subtitle,
            new Vector2(-260f, pos.y - 16f), new Vector2(540f, 32f), 18f, new Color(1f, 1f, 1f, 0.65f));
        sub.alignment = TMPro.TextAlignmentOptions.Left;
        sub.fontStyle = TMPro.FontStyles.Italic;

        var btn = MakeToggleButton(parent, title.Replace(" ", "") + "Btn",
            entries.Length > 1 ? "VIEW" : "READ", new Vector2(400f, pos.y), 220f);
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

        var box = new GameObject("Box");
        box.transform.SetParent(popGO.transform, false);
        var boxRT = box.AddComponent<RectTransform>();
        boxRT.anchorMin        = new Vector2(0.5f, 0.5f);
        boxRT.anchorMax        = new Vector2(0.5f, 0.5f);
        boxRT.pivot            = new Vector2(0.5f, 0.5f);
        boxRT.sizeDelta        = new Vector2(1600f, 900f);
        var boxImg = box.AddComponent<Image>();
        boxImg.sprite = _scoreboardSprite != null ? _scoreboardSprite : _squareSpr;
        boxImg.color  = _scoreboardSprite != null ? Color.white : new Color(0.97f, 0.94f, 0.88f);
        var boxBtn = box.AddComponent<Button>(); // swallows clicks so the box itself never dismisses
        boxBtn.transition = Selectable.Transition.None;

        _storyReaderHeader = MakeToggleLabel(box.transform, "Header", "",
            new Vector2(0f, 380f), new Vector2(1400f, 50f), 24f, new Color(1f, 1f, 1f, 0.75f));
        _storyReaderTitle = MakeToggleLabel(box.transform, "Title", "",
            new Vector2(0f, 320f), new Vector2(1400f, 60f), 34f, Color.white);
        _storyReaderTitle.fontStyle = TMPro.FontStyles.Bold;

        // Body text — NOT built via MakeToggleLabel (that helper hardcodes centered, no-wrap,
        // gold-gradient styling meant for short UI labels, wrong for a multi-paragraph body).
        // Auto-sizing so any entry length (the Origin Story is ~280 words, character/robot/world
        // entries are ~80-100) always fits the box without needing a real scroll view.
        var bodyGO = new GameObject("Body");
        bodyGO.transform.SetParent(box.transform, false);
        var bodyRT = bodyGO.AddComponent<RectTransform>();
        bodyRT.anchorMin        = new Vector2(0.5f, 0.5f);
        bodyRT.anchorMax        = new Vector2(0.5f, 0.5f);
        bodyRT.pivot            = new Vector2(0.5f, 1f);
        bodyRT.anchoredPosition = new Vector2(0f, 270f);
        bodyRT.sizeDelta        = new Vector2(1400f, 560f);
        _storyReaderBody = bodyGO.AddComponent<TextMeshProUGUI>();
        _storyReaderBody.alignment          = TMPro.TextAlignmentOptions.TopLeft;
        _storyReaderBody.enableWordWrapping = true;
        _storyReaderBody.enableAutoSizing   = true;
        _storyReaderBody.fontSizeMin        = 16f;
        _storyReaderBody.fontSizeMax        = 30f;
        _storyReaderBody.color              = new Color(0.15f, 0.10f, 0.05f); // dark ink on the parchment backdrop
        _storyReaderBody.raycastTarget      = false;

        _storyReaderPrevBtn = MakeToggleButton(box.transform, "PrevBtn", "< PREV", new Vector2(-500f, -380f), 220f);
        _storyReaderPrevBtn.onClick.AddListener(() => { _storyReaderPageIndex--; RefreshStoryReaderPage(); });

        _storyReaderNextBtn = MakeToggleButton(box.transform, "NextBtn", "NEXT >", new Vector2(500f, -380f), 220f);
        _storyReaderNextBtn.onClick.AddListener(() => { _storyReaderPageIndex++; RefreshStoryReaderPage(); });

        var backBtn = MakeToggleButton(box.transform, "BackBtn", "BACK", new Vector2(0f, -380f), 220f);
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
    (Button, System.Action<bool>) MakeStateToggle(Transform parent, string name, Vector2 pos, bool initialOn, System.Action<bool> onToggle, string onLabel = "ON", string offLabel = "OFF", float width = 160f)
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
        var label = MakeToggleLabel(go.transform, "Label", onLabel, Vector2.zero, new Vector2(width, 56f), 26f, Color.white);
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;

        void SetVisual(bool on)
        {
            img.color  = on ? ToggleOnColor : ToggleOffColor;
            label.text = on ? onLabel : offLabel;
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
            new Vector2(pos.x - 330f, pos.y), new Vector2(480f, 60f), 30f, Color.white)
            .alignment = TMPro.TextAlignmentOptions.Left;

        var go = new GameObject(name + "Slider");
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0.5f, 0.5f);
        rt.anchorMax        = new Vector2(0.5f, 0.5f);
        rt.pivot            = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(pos.x + 220f, pos.y);
        rt.sizeDelta        = new Vector2(280f, 40f);

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
        handleRT.sizeDelta = new Vector2(26f, 40f);
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
            new Vector2(pos.x + 400f, pos.y), new Vector2(100f, 60f), 26f, Color.white);

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
