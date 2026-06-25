using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Phase 2.5 — Level select: scrollable 3-column grid, star counts, locked/unlocked state.
// Self-contained: builds its own Canvas (sortingOrder 300) in Awake().
// Activates on GameState.Idle; hides on any other state.
// In Game.unity, CatapultLauncher.Start() immediately calls ForceStartLevel(0),
// so the panel is NOT shown on startup — it only appears when the player clicks MENU.
public class LevelSelectController : MonoBehaviour
{
    public static LevelSelectController Instance { get; private set; }

    private GameObject    _panel;
    private RectTransform _contentRT;
    private Sprite        _squareSpr;

    private const float CardW   = 260f;
    private const float CardH   = 200f;
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
        // Auto-show only when in the menu scene (no CatapultLauncher present).
        // In Game.unity, ForceStartLevel(0) fires from CatapultLauncher.Start()
        // and transitions to Playing — no need to show the panel here.
        if (GameManager.Instance.State == GameState.Idle &&
            FindAnyObjectByType<CatapultLauncher>() == null)
        {
            ShowPanel();
        }
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

    // Called by MainMenuController when the player clicks PLAY.
    public void Show() => ShowPanel();

    void ShowPanel()
    {
        RefreshGrid();
        _panel.SetActive(true);
    }

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

    void BuildCard(Transform parent, int index, bool unlocked, int stars)
    {
        var cardGO  = new GameObject($"Level_{index + 1}");
        cardGO.transform.SetParent(parent, false);
        cardGO.AddComponent<RectTransform>(); // GridLayoutGroup controls size

        var cardImg  = cardGO.AddComponent<Image>();
        cardImg.sprite = _squareSpr;
        cardImg.color  = unlocked
            ? new Color(0.13f, 0.15f, 0.23f)
            : new Color(0.25f, 0.25f, 0.28f);

        if (unlocked)
        {
            int idx  = index;
            var btn  = cardGO.AddComponent<Button>();
            btn.targetGraphic = cardImg;
            var cols = btn.colors;
            cols.normalColor      = Color.white;
            cols.highlightedColor = new Color(0.80f, 0.80f, 0.80f);
            cols.pressedColor     = new Color(0.58f, 0.58f, 0.58f);
            btn.colors = cols;
            btn.onClick.AddListener(() => OnLevelSelected(idx));
        }

        // Level number (larger when unlocked)
        AddText(cardGO.transform, "Num",
            text:     (index + 1).ToString(),
            pos:      new Vector2(0f, 32f),
            size:     new Vector2(220f, 88f),
            fontSize: unlocked ? 68f : 46f,
            color:    unlocked ? Color.white : new Color(0.52f, 0.52f, 0.56f));

        // Stars (unlocked) or LOCKED label
        if (unlocked)
            AddText(cardGO.transform, "Stars",
                text:     StarRichText(stars),
                pos:      new Vector2(0f, -54f),
                size:     new Vector2(220f, 36f),
                fontSize: 24f,
                color:    Color.white);
        else
            AddText(cardGO.transform, "LockLabel",
                text:     "LOCKED",
                pos:      new Vector2(0f, -58f),
                size:     new Vector2(220f, 32f),
                fontSize: 18f,
                color:    new Color(0.50f, 0.50f, 0.54f));
    }

    static void AddText(Transform parent, string name,
        string text, Vector2 pos, Vector2 size, float fontSize, Color color)
    {
        var go       = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt       = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot     = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        var tmp      = go.AddComponent<TextMeshProUGUI>();
        tmp.text      = text;
        tmp.fontSize  = fontSize;
        tmp.color     = color;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.enableWordWrapping = false;
        tmp.richText  = true;
    }

    // 3 gold or grey stars via TMP rich text.
    static string StarRichText(int earned)
    {
        const string gold = "#FFD200";
        const string grey = "#4A4A58";
        string S(int i) => i < earned
            ? $"<color={gold}>●</color>"   // ● filled
            : $"<color={grey}>○</color>";  // ○ empty
        return S(0) + "  " + S(1) + "  " + S(2);
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
        // Canvas — sortingOrder 300 sits above HUD (100) and all result panels
        var cvGO              = new GameObject("LevelSelectCanvas");
        cvGO.transform.SetParent(transform, false);
        var cv                = cvGO.AddComponent<Canvas>();
        cv.renderMode         = RenderMode.ScreenSpaceOverlay;
        cv.sortingOrder       = 300;
        var cs                = cvGO.AddComponent<CanvasScaler>();
        cs.uiScaleMode        = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        cs.referenceResolution = new Vector2(1920f, 1080f);
        cs.screenMatchMode    = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        cs.matchWidthOrHeight  = 0.5f;
        cvGO.AddComponent<GraphicRaycaster>();
        _panel = cvGO;

        Transform root = cvGO.transform;

        // Background
        var bg      = FullScreenGO(root, "BG");
        var bgImg   = bg.AddComponent<Image>();
        bgImg.sprite = _squareSpr;
        bgImg.color  = new Color(0.07f, 0.09f, 0.06f);

        // Title
        var titleGO  = new GameObject("Title");
        titleGO.transform.SetParent(root, false);
        var titleRT  = titleGO.AddComponent<RectTransform>();
        titleRT.anchorMin        = new Vector2(0.5f, 1f);
        titleRT.anchorMax        = new Vector2(0.5f, 1f);
        titleRT.pivot            = new Vector2(0.5f, 1f);
        titleRT.anchoredPosition = new Vector2(0f, -30f);
        titleRT.sizeDelta        = new Vector2(700f, 76f);
        var titleTMP             = titleGO.AddComponent<TextMeshProUGUI>();
        titleTMP.text      = "SELECT LEVEL";
        titleTMP.fontSize  = 54f;
        titleTMP.color     = new Color(0.96f, 0.90f, 0.70f);
        titleTMP.alignment = TextAlignmentOptions.Center;
        titleTMP.enableWordWrapping = false;

        // Scroll root
        var scrollGO = new GameObject("ScrollArea");
        scrollGO.transform.SetParent(root, false);
        var scrollRT = scrollGO.AddComponent<RectTransform>();
        scrollRT.anchorMin        = new Vector2(0.5f, 0.5f);
        scrollRT.anchorMax        = new Vector2(0.5f, 0.5f);
        scrollRT.pivot            = new Vector2(0.5f, 0.5f);
        scrollRT.anchoredPosition = new Vector2(0f, -50f); // shift down to give title room
        scrollRT.sizeDelta        = new Vector2(960f, 620f);
        // Transparent image so raycasts hit this layer (blocks clicks to game behind)
        var scrollBg   = scrollGO.AddComponent<Image>();
        scrollBg.sprite = _squareSpr;
        scrollBg.color  = new Color(0f, 0f, 0f, 0f);

        // Viewport — clips content
        var vpGO = new GameObject("Viewport");
        vpGO.transform.SetParent(scrollGO.transform, false);
        var vpRT = vpGO.AddComponent<RectTransform>();
        vpRT.anchorMin = Vector2.zero;
        vpRT.anchorMax = Vector2.one;
        vpRT.offsetMin = Vector2.zero;
        vpRT.offsetMax = Vector2.zero;
        var vpImg = vpGO.AddComponent<Image>();
        vpImg.sprite = _squareSpr;
        vpImg.color  = Color.white;           // Mask needs opaque alpha to clip content
        var vpMask = vpGO.AddComponent<Mask>();
        vpMask.showMaskGraphic = false;        // hide the white rect visually

        // Content — GridLayoutGroup arranges cards; ContentSizeFitter grows height
        var contentGO = new GameObject("Content");
        contentGO.transform.SetParent(vpGO.transform, false);
        _contentRT    = contentGO.AddComponent<RectTransform>();
        _contentRT.anchorMin = new Vector2(0f, 1f);
        _contentRT.anchorMax = new Vector2(1f, 1f);
        _contentRT.pivot     = new Vector2(0.5f, 1f);
        _contentRT.sizeDelta = Vector2.zero; // width from anchors; height from ContentSizeFitter

        var grid             = contentGO.AddComponent<GridLayoutGroup>();
        grid.cellSize        = new Vector2(CardW, CardH);
        grid.spacing         = new Vector2(28f, 28f);
        grid.padding         = new RectOffset(28, 28, 28, 28);
        grid.constraint      = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = Columns;
        grid.startCorner     = GridLayoutGroup.Corner.UpperLeft;
        grid.startAxis       = GridLayoutGroup.Axis.Horizontal;
        grid.childAlignment  = TextAnchor.UpperCenter;

        var csf          = contentGO.AddComponent<ContentSizeFitter>();
        csf.verticalFit  = ContentSizeFitter.FitMode.PreferredSize;

        var scroll              = scrollGO.AddComponent<ScrollRect>();
        scroll.viewport         = vpRT;
        scroll.content          = _contentRT;
        scroll.horizontal       = false;
        scroll.vertical         = true;
        scroll.scrollSensitivity = 35f;
        scroll.movementType     = ScrollRect.MovementType.Clamped;

        // BACK button — bottom-centre, returns to main menu
        var backGO  = new GameObject("BackBtn");
        backGO.transform.SetParent(root, false);
        var backRT  = backGO.AddComponent<RectTransform>();
        backRT.anchorMin        = new Vector2(0.5f, 0f);
        backRT.anchorMax        = new Vector2(0.5f, 0f);
        backRT.pivot            = new Vector2(0.5f, 0f);
        backRT.anchoredPosition = new Vector2(0f, 30f);
        backRT.sizeDelta        = new Vector2(200f, 52f);
        var backImg = backGO.AddComponent<Image>();
        backImg.sprite = _squareSpr;
        backImg.color  = new Color(0.12f, 0.14f, 0.22f);
        // Label
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
        // Button component
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

    // Full-screen RectTransform child (anchors 0→1 on both axes, no offset).
    static GameObject FullScreenGO(Transform parent, string name)
    {
        var go       = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt       = go.AddComponent<RectTransform>();
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
