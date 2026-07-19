using UnityEngine;
using UnityEngine.UI;

// Frozen Tundra (World 2) interstitial landing screen — shown once, right after L18's world-
// transition video finishes (see HUDController.GoToWorldLandingAfterTransition). Deliberately
// NOT reactive to GameState.Idle the way MainMenuController/WorldMapController are: this screen
// only ever appears via that one explicit transition, never on a normal app boot or a plain
// LoadMenu() — a normal boot/Home always lands on MainMenuController (World 1's title screen),
// same destination as before this class existed. There is currently no way back into this screen
// or World2MapController once the player leaves it (e.g. via Home on the World 2 map, which goes
// to MainMenuController like every other Home button) — a known gap, not yet solved, since a
// real "world select"/highest-unlocked-world mechanism doesn't exist yet. Flagging rather than
// inventing one, since that's a bigger design decision than this task covered.
//
// Same shape as MainMenuController's own BuildUI() PLAY/SETTINGS section — same icon art, same
// corner positions/sizes — per the "ensure all icons and navigation are the same" instruction.
// SETTINGS reuses MainMenuController's own popup via its new public OpenSettings() (see that
// class — the popup was refactored onto its own independent Canvas specifically so it could be
// opened from here without also forcing MainMenuController's own background/buttons to show).
public class World2LandingController : MonoBehaviour
{
    public static World2LandingController Instance { get; private set; }

    [SerializeField] private Sprite _landingSprite;       // FarmFury_W2.png
    [SerializeField] private Sprite _playButtonSprite;    // Btn_play.png — same asset as MainMenuController
    [SerializeField] private Sprite _settingsButtonSprite; // Btn_settings.png — same asset as MainMenuController

    private GameObject _panel;
    private Sprite      _squareSpr;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance   = this;
        _squareSpr = MakeSquareSprite();
        BuildUI();
    }

    public void Show() => _panel.SetActive(true);
    public void Hide() => _panel.SetActive(false);

    void OnPlayClicked()
    {
        Hide();
        World2MapController.Instance?.Show();
    }

    void OnSettingsClicked() => MainMenuController.Instance?.OpenSettings();

    void BuildUI()
    {
        var cvGO = new GameObject("World2LandingCanvas");
        cvGO.transform.SetParent(transform, false);
        var cv = cvGO.AddComponent<Canvas>();
        cv.renderMode   = RenderMode.ScreenSpaceOverlay;
        cv.sortingOrder = 305; // between WorldMapController (300) and MainMenuController (400) —
                                // screens are mutually exclusive via explicit Show()/Hide() calls,
                                // not simultaneous, so this ordering is a formality, not load-bearing.
        var cs = cvGO.AddComponent<CanvasScaler>();
        cs.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        cs.referenceResolution = new Vector2(1920f, 1080f);
        cs.screenMatchMode     = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        cs.matchWidthOrHeight  = 0.5f;
        cvGO.AddComponent<GraphicRaycaster>();
        _panel = cvGO;
        var root = cvGO.transform;

        // ── Background — FarmFury_W2.png ──────────────────────────────────────
        var bgGO = new GameObject("Background");
        bgGO.transform.SetParent(root, false);
        var bgRT = bgGO.AddComponent<RectTransform>();
        bgRT.anchorMin = Vector2.zero;
        bgRT.anchorMax = Vector2.one;
        bgRT.offsetMin = bgRT.offsetMax = Vector2.zero;
        var bgImg = bgGO.AddComponent<Image>();
        bgImg.sprite         = _landingSprite != null ? _landingSprite : _squareSpr;
        bgImg.color          = _landingSprite != null ? Color.white : new Color(0.55f, 0.75f, 0.92f);
        bgImg.preserveAspect = false;

        // ── PLAY button — same corner anchor/size/inset as MainMenuController's ──
        var playGO = new GameObject("PlayBtn");
        playGO.transform.SetParent(root, false);
        var playRT = playGO.AddComponent<RectTransform>();
        playRT.anchorMin        = new Vector2(0f, 0f);
        playRT.anchorMax        = new Vector2(0f, 0f);
        playRT.pivot            = new Vector2(0.5f, 0.5f);
        playRT.anchoredPosition = new Vector2(220f, 170f);
        playRT.sizeDelta        = new Vector2(150f, 150f);
        var playImg = playGO.AddComponent<Image>();
        playImg.sprite         = _playButtonSprite != null ? _playButtonSprite : _squareSpr;
        playImg.color          = _playButtonSprite != null ? Color.white : new Color(1.00f, 0.55f, 0.05f);
        playImg.preserveAspect = true;
        var playBtn = playGO.AddComponent<Button>();
        playBtn.targetGraphic = playImg;
        var pc = playBtn.colors;
        pc.normalColor      = Color.white;
        pc.highlightedColor = new Color(0.88f, 0.88f, 0.88f);
        pc.pressedColor     = new Color(0.68f, 0.68f, 0.68f);
        playBtn.colors = pc;
        playBtn.onClick.AddListener(OnPlayClicked);

        // ── SETTINGS button — same corner anchor/size/inset as MainMenuController's ──
        var settingsGO = new GameObject("SettingsBtn");
        settingsGO.transform.SetParent(root, false);
        var settingsRT = settingsGO.AddComponent<RectTransform>();
        settingsRT.anchorMin        = new Vector2(1f, 0f);
        settingsRT.anchorMax        = new Vector2(1f, 0f);
        settingsRT.pivot            = new Vector2(0.5f, 0.5f);
        settingsRT.anchoredPosition = new Vector2(-220f, 170f);
        settingsRT.sizeDelta        = new Vector2(150f, 150f);
        var settingsImg = settingsGO.AddComponent<Image>();
        settingsImg.sprite         = _settingsButtonSprite != null ? _settingsButtonSprite : _squareSpr;
        settingsImg.color          = _settingsButtonSprite != null ? Color.white : new Color(1.00f, 0.55f, 0.05f);
        settingsImg.preserveAspect = true;
        var settingsBtn = settingsGO.AddComponent<Button>();
        settingsBtn.targetGraphic = settingsImg;
        var sc = settingsBtn.colors;
        sc.normalColor      = Color.white;
        sc.highlightedColor = new Color(0.88f, 0.88f, 0.88f);
        sc.pressedColor     = new Color(0.68f, 0.68f, 0.68f);
        settingsBtn.colors  = sc;
        settingsBtn.onClick.AddListener(OnSettingsClicked);

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
