using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Full-screen "animal VS robot" transition screen shown between the Sunrise Meadows world map
// and gameplay — replaces the old centred LevelPreviewCard popup (2026-07-16 mockup pass,
// MatchUp_New.png). Built once by WorldMapController and toggled via Show(levelIndex)/Hide(),
// same self-contained runtime-built pattern as everything else in this project's UI layer.
//
// MATCHUP NOTE — both cards are read fresh from GameManager.GetLevelData(levelIndex) on every
// Show(), not fixed: the animal card is birds[0] and the robot card is robots[0].robotType for
// THAT level, so a different level shows a different matchup, per the user's explicit
// clarification that this is not a static "chicken vs robot" splash. Player-chosen-animal
// (mentioned by the user as a planned future feature, once animals are unlockable) is
// intentionally NOT built here — Show() always uses the level's birds[0], same convention the
// old LevelPreviewCard used, so swapping in a player-selected animal later only needs to change
// the `animal` lookup inside Show() (e.g. to a saved player preference), not this screen's layout.
//
// ENTRY-TRIGGER NOTE — the mockup has no visible PLAY/continue button (verified: the source
// image is a full 1280x720 frame, nothing is cropped). Implemented as tap-anywhere-to-continue
// with a pulsing "TAP TO CONTINUE" hint, which is a UX default I picked, not something the
// mockup specifies — flagging rather than silently assuming. A dedicated ✕ close button
// (top-left) always backs out to the map without starting the level, for players who opened
// this by tapping an old completed pin and don't want to replay it immediately.
public class MatchUpScreen : MonoBehaviour
{
    // Animal card art — 8-slot array indexed by AnimalType. Robot card art — indexed by
    // RobotType (Basic, Harvester); no dedicated "Basic" card art exists yet (only
    // Harvestor_Robot.png) — Show() falls back to a text label when null.
    //
    // NOT [SerializeField] on this component — MatchUpScreen is a child GameObject created at
    // runtime inside WorldMapController.BuildUI(), which only executes when Awake() actually
    // fires (Play mode, or immediately after a fresh AddComponent). SceneSetup's batch
    // "Wire Scene References" pass opens the scene without entering Play mode, so it never
    // reaches a live instance of this component to wire a SerializeField on — confirmed
    // empirically 2026-07-16 (the old LevelPreviewCard had the exact same silent gap). Instead,
    // WorldMapController holds these references (which DO persist, since that component lives
    // directly in the saved scene) and threads them through Init() below.
    private Sprite[] _animalCardSprites;
    private Sprite[] _robotCardSprites;
    private Sprite   _backgroundSprite; // MatchUp_Background.png
    private Sprite   _vsSprite;          // VS.png

    private GameObject      _panel;
    private Image           _bgImg;
    private Image           _animalImg;
    private Image           _robotImg;
    private TextMeshProUGUI _robotFallbackLabel;
    private TextMeshProUGUI _levelNumText;
    private TextMeshProUGUI _starsText;
    private TextMeshProUGUI _continueHint;
    private Button          _fullScreenBtn;
    private int             _levelIndex;
    private bool            _hasData;
    private Coroutine       _pulseRoutine;

    public void Init(Sprite squareSpr, Sprite backgroundSprite, Sprite vsSprite,
        Sprite[] animalCardSprites, Sprite[] robotCardSprites)
    {
        _backgroundSprite  = backgroundSprite;
        _vsSprite          = vsSprite;
        _animalCardSprites = animalCardSprites;
        _robotCardSprites  = robotCardSprites;
        BuildUI(squareSpr);
    }

    public void Show(int levelIndex)
    {
        _levelIndex = levelIndex;
        var data  = GameManager.Instance?.GetLevelData(levelIndex);
        int stars = ScoreManager.GetBestStars(levelIndex);

        _levelNumText.text = $"LEVEL {levelIndex + 1}";
        _starsText.text    = StarText(stars);
        _hasData           = data != null;

        if (data != null)
        {
            AnimalType animal = (data.birds != null && data.birds.Length > 0) ? data.birds[0] : AnimalType.Cluck;
            RobotType  robot  = (data.robots != null && data.robots.Length > 0) ? data.robots[0].robotType : RobotType.Basic;

            Sprite animalSpr = _animalCardSprites != null && (int)animal < _animalCardSprites.Length
                ? _animalCardSprites[(int)animal] : null;
            _animalImg.sprite  = animalSpr;
            _animalImg.enabled = animalSpr != null;

            Sprite robotSpr = _robotCardSprites != null && (int)robot < _robotCardSprites.Length
                ? _robotCardSprites[(int)robot] : null;
            _robotImg.sprite            = robotSpr;
            _robotImg.enabled           = robotSpr != null;
            _robotFallbackLabel.enabled = robotSpr == null;
            _robotFallbackLabel.text    = robot == RobotType.Harvester ? "HARVESTER\nROBOT" : "ROBOT";

            _continueHint.text = "TAP TO CONTINUE";
        }
        else
        {
            // Level data doesn't exist yet (only L01-L06 are authored right now, out of 18
            // marker slots — see CLAUDE.md Gap Analysis) — show a placeholder instead of
            // crashing or launching a level with no content.
            _animalImg.enabled          = false;
            _robotImg.enabled           = false;
            _robotFallbackLabel.enabled = true;
            _robotFallbackLabel.text    = "COMING\nSOON";
            _continueHint.text          = "TAP TO GO BACK";
        }

        _panel.SetActive(true);
        if (_pulseRoutine != null) StopCoroutine(_pulseRoutine);
        _pulseRoutine = StartCoroutine(PulseHint());
    }

    public void Hide()
    {
        if (_pulseRoutine != null) { StopCoroutine(_pulseRoutine); _pulseRoutine = null; }
        _panel.SetActive(false);
    }

    void OnFullScreenClicked()
    {
        Hide();
        if (_hasData) GameManager.Instance?.ForceStartLevel(_levelIndex);
    }

    void OnCloseClicked() => Hide();

    System.Collections.IEnumerator PulseHint()
    {
        const float period = 1.1f;
        while (true)
        {
            float t = (Mathf.Sin(Time.time * (2f * Mathf.PI / period)) + 1f) * 0.5f;
            var c = _continueHint.color;
            c.a = Mathf.Lerp(0.35f, 1f, t);
            _continueHint.color = c;
            yield return null;
        }
    }

    static string StarText(int earned)
    {
        const string gold = "#FFD200";
        const string grey = "#8A8A90";
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < 3; i++)
        {
            if (i > 0) sb.Append("  ");
            sb.Append(i < earned ? $"<color={gold}>★</color>" : $"<color={grey}>★</color>");
        }
        return sb.ToString();
    }

    // ── UI construction ────────────────────────────────────────────────────────
    // Coordinates measured from MatchUp_New.png (1280x720), converted to this canvas's
    // 1920x1080 reference resolution via the same uniform 1.5x scale WorldMapController uses
    // (identical 16:9 aspect, non-aspect-preserving full-bleed background).

    void BuildUI(Sprite squareSpr)
    {
        var rootRT = gameObject.AddComponent<RectTransform>();
        rootRT.anchorMin = Vector2.zero;
        rootRT.anchorMax = Vector2.one;
        rootRT.offsetMin = rootRT.offsetMax = Vector2.zero;
        _panel = gameObject;

        // Background — sky/clouds/hills art, full-bleed
        var bgGO = new GameObject("Background");
        bgGO.transform.SetParent(transform, false);
        var bgRT = bgGO.AddComponent<RectTransform>();
        bgRT.anchorMin = Vector2.zero;
        bgRT.anchorMax = Vector2.one;
        bgRT.offsetMin = bgRT.offsetMax = Vector2.zero;
        _bgImg = bgGO.AddComponent<Image>();
        _bgImg.sprite         = _backgroundSprite != null ? _backgroundSprite : squareSpr;
        _bgImg.color          = _backgroundSprite != null ? Color.white : new Color(0.35f, 0.55f, 0.85f);
        _bgImg.preserveAspect = false;

        // Full-screen tap-to-continue catcher, sits above the background but below the cards
        // and close button (added later in the hierarchy so they receive taps first).
        var catcherGO = new GameObject("ContinueCatcher");
        catcherGO.transform.SetParent(transform, false);
        var catcherRT = catcherGO.AddComponent<RectTransform>();
        catcherRT.anchorMin = Vector2.zero;
        catcherRT.anchorMax = Vector2.one;
        catcherRT.offsetMin = catcherRT.offsetMax = Vector2.zero;
        var catcherImg = catcherGO.AddComponent<Image>();
        catcherImg.color = new Color(0f, 0f, 0f, 0f); // invisible but raycastable
        _fullScreenBtn = catcherGO.AddComponent<Button>();
        _fullScreenBtn.targetGraphic = catcherImg;
        var fc = _fullScreenBtn.colors;
        fc.normalColor = fc.highlightedColor = fc.pressedColor = Color.white;
        _fullScreenBtn.colors = fc;
        _fullScreenBtn.onClick.AddListener(OnFullScreenClicked);

        // Left card — player's animal
        var animGO = new GameObject("AnimalCard");
        animGO.transform.SetParent(transform, false);
        var animRT = animGO.AddComponent<RectTransform>();
        animRT.anchorMin        = new Vector2(0.5f, 0.5f);
        animRT.anchorMax        = new Vector2(0.5f, 0.5f);
        animRT.pivot            = new Vector2(0.5f, 0.5f);
        animRT.anchoredPosition = new Vector2(-537f, 19.5f);
        animRT.sizeDelta        = new Vector2(517.5f, 592.5f);
        _animalImg = animGO.AddComponent<Image>();
        _animalImg.preserveAspect = true;
        _animalImg.raycastTarget  = false;

        // Right card — the robot(s) this level's player will face
        var robotGO = new GameObject("RobotCard");
        robotGO.transform.SetParent(transform, false);
        var robotRT = robotGO.AddComponent<RectTransform>();
        robotRT.anchorMin        = new Vector2(0.5f, 0.5f);
        robotRT.anchorMax        = new Vector2(0.5f, 0.5f);
        robotRT.pivot            = new Vector2(0.5f, 0.5f);
        robotRT.anchoredPosition = new Vector2(480f, 19.5f);
        robotRT.sizeDelta        = new Vector2(517.5f, 592.5f);
        _robotImg = robotGO.AddComponent<Image>();
        _robotImg.preserveAspect = true;
        _robotImg.raycastTarget  = false;

        // Fallback label shown inside the robot card slot when no dedicated art exists yet
        // (RobotType.Basic) or when the level has no data ("COMING SOON").
        _robotFallbackLabel = MakeLabel(transform, "RobotFallbackLabel",
            new Vector2(480f, 19.5f), new Vector2(400f, 200f), 36f, new Color(0.25f, 0.20f, 0.12f));
        _robotFallbackLabel.fontStyle = FontStyles.Bold;

        // VS graphic, centred between the two cards
        var vsGO = new GameObject("VS");
        vsGO.transform.SetParent(transform, false);
        var vsRT = vsGO.AddComponent<RectTransform>();
        vsRT.anchorMin        = new Vector2(0.5f, 0.5f);
        vsRT.anchorMax        = new Vector2(0.5f, 0.5f);
        vsRT.pivot            = new Vector2(0.5f, 0.5f);
        vsRT.anchoredPosition = new Vector2(-22.5f, 75f);
        vsRT.sizeDelta        = new Vector2(160f, 160f);
        var vsImg = vsGO.AddComponent<Image>();
        vsImg.sprite         = _vsSprite;
        vsImg.enabled        = _vsSprite != null;
        vsImg.preserveAspect = true;
        vsImg.raycastTarget  = false;

        // Level number — top of screen
        _levelNumText = MakeLabel(transform, "LevelNum",
            new Vector2(0f, 460f), new Vector2(700f, 70f), 46f, new Color(1f, 0.97f, 0.86f));
        _levelNumText.fontStyle = FontStyles.Bold;

        // Stars (replay context) — just under the level number
        _starsText = MakeLabel(transform, "Stars",
            new Vector2(0f, 400f), new Vector2(400f, 50f), 30f, Color.white);

        // Pulsing continue hint — bottom of screen
        _continueHint = MakeLabel(transform, "ContinueHint",
            new Vector2(0f, -470f), new Vector2(600f, 50f), 26f, new Color(1f, 1f, 1f, 1f));
        _continueHint.fontStyle = FontStyles.Bold;

        // Close button — top-left, backs out without starting the level. Its own Button
        // swallows the click so it never falls through to the full-screen continue catcher.
        var closeGO = new GameObject("CloseBtn");
        closeGO.transform.SetParent(transform, false);
        var closeRT = closeGO.AddComponent<RectTransform>();
        closeRT.anchorMin        = new Vector2(0f, 1f);
        closeRT.anchorMax        = new Vector2(0f, 1f);
        closeRT.pivot            = new Vector2(0f, 1f);
        closeRT.anchoredPosition = new Vector2(24f, -24f);
        closeRT.sizeDelta        = new Vector2(64f, 64f);
        var closeImg = closeGO.AddComponent<Image>();
        closeImg.sprite = squareSpr;
        closeImg.color  = new Color(0.12f, 0.14f, 0.22f, 0.85f);
        var closeLblGO = new GameObject("Label");
        closeLblGO.transform.SetParent(closeGO.transform, false);
        var closeLblRT = closeLblGO.AddComponent<RectTransform>();
        closeLblRT.anchorMin = Vector2.zero;
        closeLblRT.anchorMax = Vector2.one;
        closeLblRT.offsetMin = closeLblRT.offsetMax = Vector2.zero;
        var closeLblTMP = closeLblGO.AddComponent<TextMeshProUGUI>();
        closeLblTMP.text               = "×";
        closeLblTMP.fontSize           = 40f;
        closeLblTMP.color              = Color.white;
        closeLblTMP.alignment          = TextAlignmentOptions.Center;
        closeLblTMP.enableWordWrapping = false;
        closeLblTMP.raycastTarget      = false;
        var closeBtn = closeGO.AddComponent<Button>();
        closeBtn.targetGraphic = closeImg;
        var cc = closeBtn.colors;
        cc.normalColor      = Color.white;
        cc.highlightedColor = new Color(0.85f, 0.85f, 0.85f);
        cc.pressedColor     = new Color(0.60f, 0.60f, 0.60f);
        closeBtn.colors     = cc;
        closeBtn.onClick.AddListener(OnCloseClicked);

        _panel.SetActive(false);
    }

    static TextMeshProUGUI MakeLabel(Transform parent, string name, Vector2 pos, Vector2 size, float fontSize, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0.5f, 0.5f);
        rt.anchorMax        = new Vector2(0.5f, 0.5f);
        rt.pivot            = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta        = size;
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text               = "";
        tmp.fontSize           = fontSize;
        tmp.color              = color;
        tmp.alignment          = TextAlignmentOptions.Center;
        tmp.enableWordWrapping = true;
        tmp.richText           = true;
        tmp.raycastTarget      = false;
        return tmp;
    }
}
