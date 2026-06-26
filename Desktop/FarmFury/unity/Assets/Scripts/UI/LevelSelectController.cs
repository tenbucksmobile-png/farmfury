using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Phase 4 — Level select: world-art card thumbnails, star rating, lock overlay.
// Self-contained: builds its own Canvas (sortingOrder 300) in Awake().
// Activates on GameState.Idle; hides on any other state.
// _worldCardSprites[0..5] wired by SceneSetup from Assets/Sprites/UI/LevelCards/.
public class LevelSelectController : MonoBehaviour
{
    public static LevelSelectController Instance { get; private set; }

    // World card backgrounds indexed by world number (0 = Meadow Ruins … 5 = Mothership).
    // Wired by SceneSetup; falls back to a tinted solid when null.
    [SerializeField] private Sprite[] _worldCardSprites = new Sprite[6];

    private GameObject    _panel;
    private RectTransform _contentRT;
    private Sprite        _squareSpr;

    // World colour tints used when no art sprite is wired yet
    private static readonly Color[] WorldTints =
    {
        new Color(0.16f, 0.28f, 0.13f),   // W1 meadow green
        new Color(0.13f, 0.22f, 0.35f),   // W2 ice blue
        new Color(0.30f, 0.20f, 0.10f),   // W3 watermill amber
        new Color(0.10f, 0.18f, 0.32f),   // W4 sky blue
        new Color(0.08f, 0.20f, 0.30f),   // W5 ocean teal
        new Color(0.15f, 0.08f, 0.28f),   // W6 mothership purple
    };

    private static readonly string[] WorldNames =
    {
        "MEADOW RUINS", "FROZEN TUNDRA", "WATERMILL VILLAGE",
        "SKY ISLANDS",  "SUNKEN CITY",   "ROBOT MOTHERSHIP",
    };

    // Levels per world — used to map level index → world index
    private static readonly int[] LevelsPerWorld = { 18, 22, 22, 24, 22, 16 };

    private const float CardW   = 280f;
    private const float CardH   = 210f;
    private const int   Columns = 3;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
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

    // ── State ─────────────────────────────────────────────────────────────────

    void OnStateChanged(GameState state)
    {
        if (state == GameState.Idle) ShowPanel();
        else                         HidePanel();
    }

    public void Show() => ShowPanel();

    void ShowPanel() { RefreshGrid(); _panel.SetActive(true); }
    void HidePanel() => _panel.SetActive(false);

    // ── Grid ──────────────────────────────────────────────────────────────────

    void RefreshGrid()
    {
        foreach (Transform child in _contentRT)
            Destroy(child.gameObject);

        int total = GameManager.Instance?.TotalLevels ?? 0;
        for (int i = 0; i < total; i++)
        {
            bool unlocked = i == 0 || ScoreManager.GetBestStars(i - 1) > 0;
            BuildCard(_contentRT, i, unlocked, ScoreManager.GetBestStars(i));
        }
    }

    // Returns which world this level belongs to (0-based).
    static int WorldIndex(int levelIndex)
    {
        int remaining = levelIndex;
        for (int w = 0; w < LevelsPerWorld.Length; w++)
        {
            if (remaining < LevelsPerWorld[w]) return w;
            remaining -= LevelsPerWorld[w];
        }
        return LevelsPerWorld.Length - 1;
    }

    void BuildCard(Transform parent, int index, bool unlocked, int stars)
    {
        int worldIdx = WorldIndex(index);

        // ── Root ─────────────────────────────────────────────────────────────
        var card   = new GameObject($"Level_{index + 1}");
        card.transform.SetParent(parent, false);
        var cardRT = card.AddComponent<RectTransform>(); // size set by GridLayoutGroup

        // ── Art background ───────────────────────────────────────────────────
        var artImg   = card.AddComponent<Image>();
        Sprite worldSpr = worldIdx < _worldCardSprites.Length ? _worldCardSprites[worldIdx] : null;
        artImg.sprite   = worldSpr != null ? worldSpr : _squareSpr;
        artImg.color    = worldSpr != null
            ? (unlocked ? Color.white : new Color(0.5f, 0.5f, 0.5f, 1f))
            : (unlocked ? WorldTints[worldIdx] : new Color(0.22f, 0.22f, 0.25f));

        // ── Bottom gradient — dark strip for text legibility ─────────────────
        AddOverlay(card.transform, "Gradient",
            anchorMin: new Vector2(0f, 0f), anchorMax: new Vector2(1f, 0.55f),
            color: new Color(0f, 0f, 0f, 0.72f));

        // ── Top-left world label ─────────────────────────────────────────────
        AddLabel(card.transform, "WorldLabel",
            text:     $"W{worldIdx + 1}",
            anchorMin: new Vector2(0f, 1f), anchorMax: new Vector2(0f, 1f),
            pivot:     new Vector2(0f, 1f),
            pos:       new Vector2(8f, -6f),
            size:      new Vector2(60f, 24f),
            fontSize:  13f,
            color:     new Color(1f, 1f, 1f, 0.70f));

        // ── Level number ─────────────────────────────────────────────────────
        AddLabel(card.transform, "LevelNum",
            text:     (index + 1).ToString(),
            anchorMin: new Vector2(0.5f, 0.5f), anchorMax: new Vector2(0.5f, 0.5f),
            pivot:     new Vector2(0.5f, 0.5f),
            pos:       new Vector2(0f, 20f),
            size:      new Vector2(240f, 90f),
            fontSize:  unlocked ? 72f : 52f,
            color:     unlocked ? Color.white : new Color(0.60f, 0.60f, 0.65f));

        // ── Stars or LOCKED ──────────────────────────────────────────────────
        if (unlocked)
        {
            AddLabel(card.transform, "Stars",
                text:     StarText(stars),
                anchorMin: new Vector2(0.5f, 0f), anchorMax: new Vector2(0.5f, 0f),
                pivot:     new Vector2(0.5f, 0f),
                pos:       new Vector2(0f, 12f),
                size:      new Vector2(200f, 34f),
                fontSize:  26f,
                color:     Color.white);
        }
        else
        {
            // Dark veil over entire card for locked state
            AddOverlay(card.transform, "LockVeil",
                anchorMin: Vector2.zero, anchorMax: Vector2.one,
                color: new Color(0f, 0f, 0f, 0.48f));

            AddLabel(card.transform, "LockLabel",
                text:     "🔒 LOCKED",
                anchorMin: new Vector2(0.5f, 0f), anchorMax: new Vector2(0.5f, 0f),
                pivot:     new Vector2(0.5f, 0f),
                pos:       new Vector2(0f, 10f),
                size:      new Vector2(200f, 30f),
                fontSize:  16f,
                color:     new Color(0.72f, 0.72f, 0.76f));
        }

        // ── Button (unlocked only) ───────────────────────────────────────────
        if (unlocked)
        {
            int idx  = index;
            var btn  = card.AddComponent<Button>();
            btn.targetGraphic = artImg;
            var cols = btn.colors;
            cols.normalColor      = Color.white;
            cols.highlightedColor = new Color(0.88f, 0.92f, 1.00f);
            cols.pressedColor     = new Color(0.60f, 0.64f, 0.72f);
            btn.colors = cols;
            btn.onClick.AddListener(() => OnLevelSelected(idx));
        }
    }

    static void AddOverlay(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Color color)
    {
        var go   = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt   = go.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        var img  = go.AddComponent<Image>();
        img.sprite = null;
        img.color  = color;
        img.raycastTarget = false;
    }

    static void AddLabel(Transform parent, string name,
        string text, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
        Vector2 pos, Vector2 size, float fontSize, Color color)
    {
        var go   = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt   = go.AddComponent<RectTransform>();
        rt.anchorMin        = anchorMin;
        rt.anchorMax        = anchorMax;
        rt.pivot            = pivot;
        rt.anchoredPosition = pos;
        rt.sizeDelta        = size;
        var tmp  = go.AddComponent<TextMeshProUGUI>();
        tmp.text               = text;
        tmp.fontSize           = fontSize;
        tmp.color              = color;
        tmp.alignment          = TextAlignmentOptions.Center;
        tmp.enableWordWrapping = false;
        tmp.richText           = true;
        tmp.raycastTarget      = false;
    }

    // ★★★ in gold/grey via TMP rich text.
    static string StarText(int earned)
    {
        const string gold = "#FFD200";
        const string grey = "#3A3A48";
        System.Text.StringBuilder sb = new();
        for (int i = 0; i < 3; i++)
        {
            if (i > 0) sb.Append("  ");
            sb.Append(i < earned
                ? $"<color={gold}>★</color>"
                : $"<color={grey}>★</color>");
        }
        return sb.ToString();
    }

    void OnLevelSelected(int index)
    {
        HidePanel();
        GameManager.Instance?.ForceStartLevel(index);
    }

    void OnBackClicked()
    {
        HidePanel();
        MainMenuController.Instance?.Show();
    }

    // ── Canvas construction ────────────────────────────────────────────────────

    void BuildUI()
    {
        var cvGO               = new GameObject("LevelSelectCanvas");
        cvGO.transform.SetParent(transform, false);
        var cv                 = cvGO.AddComponent<Canvas>();
        cv.renderMode          = RenderMode.ScreenSpaceOverlay;
        cv.sortingOrder        = 300;
        var cs                 = cvGO.AddComponent<CanvasScaler>();
        cs.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        cs.referenceResolution = new Vector2(1920f, 1080f);
        cs.screenMatchMode     = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        cs.matchWidthOrHeight  = 0.5f;
        cvGO.AddComponent<GraphicRaycaster>();
        _panel = cvGO;

        Transform root = cvGO.transform;

        // Background — rich dark blue-grey
        var bg    = FullScreenGO(root, "BG");
        var bgImg = bg.AddComponent<Image>();
        bgImg.sprite = _squareSpr;
        bgImg.color  = new Color(0.05f, 0.06f, 0.10f);

        // Title
        var titleGO  = new GameObject("Title");
        titleGO.transform.SetParent(root, false);
        var titleRT  = titleGO.AddComponent<RectTransform>();
        titleRT.anchorMin        = new Vector2(0.5f, 1f);
        titleRT.anchorMax        = new Vector2(0.5f, 1f);
        titleRT.pivot            = new Vector2(0.5f, 1f);
        titleRT.anchoredPosition = new Vector2(0f, -28f);
        titleRT.sizeDelta        = new Vector2(800f, 72f);
        var titleTMP             = titleGO.AddComponent<TextMeshProUGUI>();
        titleTMP.text              = "SELECT LEVEL";
        titleTMP.fontSize          = 52f;
        titleTMP.color             = new Color(0.98f, 0.88f, 0.52f);
        titleTMP.alignment         = TextAlignmentOptions.Center;
        titleTMP.enableWordWrapping = false;

        // Scroll area
        var scrollGO = new GameObject("ScrollArea");
        scrollGO.transform.SetParent(root, false);
        var scrollRT = scrollGO.AddComponent<RectTransform>();
        scrollRT.anchorMin        = new Vector2(0.5f, 0.5f);
        scrollRT.anchorMax        = new Vector2(0.5f, 0.5f);
        scrollRT.pivot            = new Vector2(0.5f, 0.5f);
        scrollRT.anchoredPosition = new Vector2(0f, -46f);
        scrollRT.sizeDelta        = new Vector2(1020f, 640f);
        var scrollBg  = scrollGO.AddComponent<Image>();
        scrollBg.sprite = _squareSpr;
        scrollBg.color  = Color.clear;

        // Viewport
        var vpGO = new GameObject("Viewport");
        vpGO.transform.SetParent(scrollGO.transform, false);
        var vpRT = vpGO.AddComponent<RectTransform>();
        vpRT.anchorMin = Vector2.zero;
        vpRT.anchorMax = Vector2.one;
        vpRT.offsetMin = Vector2.zero;
        vpRT.offsetMax = Vector2.zero;
        var vpImg  = vpGO.AddComponent<Image>();
        vpImg.sprite = _squareSpr;
        vpImg.color  = Color.white;
        var vpMask = vpGO.AddComponent<Mask>();
        vpMask.showMaskGraphic = false;

        // Content grid
        var contentGO = new GameObject("Content");
        contentGO.transform.SetParent(vpGO.transform, false);
        _contentRT    = contentGO.AddComponent<RectTransform>();
        _contentRT.anchorMin = new Vector2(0f, 1f);
        _contentRT.anchorMax = new Vector2(1f, 1f);
        _contentRT.pivot     = new Vector2(0.5f, 1f);
        _contentRT.sizeDelta = Vector2.zero;

        var grid             = contentGO.AddComponent<GridLayoutGroup>();
        grid.cellSize        = new Vector2(CardW, CardH);
        grid.spacing         = new Vector2(24f, 24f);
        grid.padding         = new RectOffset(24, 24, 24, 24);
        grid.constraint      = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = Columns;
        grid.startCorner     = GridLayoutGroup.Corner.UpperLeft;
        grid.startAxis       = GridLayoutGroup.Axis.Horizontal;
        grid.childAlignment  = TextAnchor.UpperCenter;

        var csf         = contentGO.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var scroll               = scrollGO.AddComponent<ScrollRect>();
        scroll.viewport          = vpRT;
        scroll.content           = _contentRT;
        scroll.horizontal        = false;
        scroll.vertical          = true;
        scroll.scrollSensitivity = 35f;
        scroll.movementType      = ScrollRect.MovementType.Clamped;

        // BACK button
        var backGO  = new GameObject("BackBtn");
        backGO.transform.SetParent(root, false);
        var backRT  = backGO.AddComponent<RectTransform>();
        backRT.anchorMin        = new Vector2(0.5f, 0f);
        backRT.anchorMax        = new Vector2(0.5f, 0f);
        backRT.pivot            = new Vector2(0.5f, 0f);
        backRT.anchoredPosition = new Vector2(0f, 28f);
        backRT.sizeDelta        = new Vector2(200f, 52f);
        var backImg = backGO.AddComponent<Image>();
        backImg.sprite = _squareSpr;
        backImg.color  = new Color(0.12f, 0.14f, 0.22f);
        var lblGO  = new GameObject("Label");
        lblGO.transform.SetParent(backGO.transform, false);
        var lblRT  = lblGO.AddComponent<RectTransform>();
        lblRT.anchorMin = Vector2.zero;
        lblRT.anchorMax = Vector2.one;
        lblRT.offsetMin = Vector2.zero;
        lblRT.offsetMax = Vector2.zero;
        var lblTMP = lblGO.AddComponent<TextMeshProUGUI>();
        lblTMP.text      = "← BACK";
        lblTMP.fontSize  = 24f;
        lblTMP.color     = Color.white;
        lblTMP.alignment = TextAlignmentOptions.Center;
        lblTMP.enableWordWrapping = false;
        var backBtn = backGO.AddComponent<Button>();
        backBtn.targetGraphic = backImg;
        var bc      = backBtn.colors;
        bc.normalColor      = Color.white;
        bc.highlightedColor = new Color(0.80f, 0.80f, 0.80f);
        bc.pressedColor     = new Color(0.55f, 0.55f, 0.55f);
        backBtn.colors      = bc;
        backBtn.onClick.AddListener(OnBackClicked);

        _panel.SetActive(false);
    }

    static GameObject FullScreenGO(Transform parent, string name)
    {
        var go   = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt   = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        return go;
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
