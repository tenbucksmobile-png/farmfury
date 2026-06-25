using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Phase 2.6 — Main menu: "FARM FURY" logo, animated farm background, PLAY button.
// Self-contained: builds its own Canvas (sortingOrder 400) in Awake().
// Shows on startup (State == Idle in Start()); hides when PLAY is clicked.
// CatapultLauncher defers ForceStartLevel(0) by one frame and skips it if this panel is visible.
public class MainMenuController : MonoBehaviour
{
    public static MainMenuController Instance { get; private set; }
    public bool IsVisible => _panel != null && _panel.activeSelf;

    private GameObject    _panel;

    // Animated background elements
    private RawImage      _farHillImg, _nearHillImg;
    private float         _farScroll, _nearScroll;
    private RectTransform _sunRT;
    private Vector2       _sunBasePos;
    private RectTransform[] _cloudRTs;
    private float[]         _cloudSpeeds;
    private float           _t;

    // Shared sprites
    private Sprite _squareSpr, _circleSpr, _cloudSpr;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance    = this;
        _squareSpr  = MakeSquareSprite();
        _circleSpr  = MakeCircleSprite(64);
        _cloudSpr   = MakeCloudSprite(128, 48);
        BuildUI();  // panel starts INACTIVE; Start() shows it when State == Idle
    }

    void Start()
    {
        // Show main menu on a fresh game session. If State == Idle (or GameManager missing),
        // the player hasn't started yet. If State == Playing (level restart), stay hidden.
        if (GameManager.Instance == null || GameManager.Instance.State == GameState.Idle)
            _panel.SetActive(true);
    }

    void Update()
    {
        if (!IsVisible) return;
        _t += Time.deltaTime;
        AnimateBackground();
    }

    // ── Public API ────────────────────────────────────────────────────────────

    // Called from LevelSelectController's BACK button.
    public void Show() => _panel.SetActive(true);

    // ── Interaction ───────────────────────────────────────────────────────────

    void OnPlayClicked()
    {
        _panel.SetActive(false);
        LevelSelectController.Instance?.Show();
    }

    // ── Animation ─────────────────────────────────────────────────────────────

    void AnimateBackground()
    {
        // Scroll hill textures via RawImage.uvRect (Repeat wrap mode tiles seamlessly)
        _farScroll  = (_farScroll  + Time.deltaTime * 0.018f) % 1f;
        _nearScroll = (_nearScroll + Time.deltaTime * 0.038f) % 1f;
        if (_farHillImg  != null) _farHillImg.uvRect  = new Rect(_farScroll,  0f, 1f, 1f);
        if (_nearHillImg != null) _nearHillImg.uvRect = new Rect(_nearScroll, 0f, 1f, 1f);

        // Bob sun vertically on a slow sine
        if (_sunRT != null)
            _sunRT.anchoredPosition = new Vector2(_sunBasePos.x, _sunBasePos.y + Mathf.Sin(_t * 0.45f) * 12f);

        // Drift clouds left; wrap when they leave the left edge
        if (_cloudRTs == null) return;
        for (int i = 0; i < _cloudRTs.Length; i++)
        {
            if (_cloudRTs[i] == null) continue;
            var pos = _cloudRTs[i].anchoredPosition;
            pos.x -= _cloudSpeeds[i] * Time.deltaTime;
            if (pos.x < -1060f) pos.x = 1060f;
            _cloudRTs[i].anchoredPosition = pos;
        }
    }

    // ── UI construction ───────────────────────────────────────────────────────

    void BuildUI()
    {
        // Canvas — sortingOrder 400 sits above LevelSelect (300), HUD panels, result screens
        var cvGO  = new GameObject("MainMenuCanvas");
        cvGO.transform.SetParent(transform, false);
        var cv    = cvGO.AddComponent<Canvas>();
        cv.renderMode   = RenderMode.ScreenSpaceOverlay;
        cv.sortingOrder = 400;
        var cs    = cvGO.AddComponent<CanvasScaler>();
        cs.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        cs.referenceResolution = new Vector2(1920f, 1080f);
        cs.screenMatchMode     = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        cs.matchWidthOrHeight  = 0.5f;
        cvGO.AddComponent<GraphicRaycaster>();
        _panel = cvGO;

        var root = cvGO.transform;

        // Sky — two overlapping rects give a subtle gradient feel without shaders
        AddFullScreen(root, "SkyTop",    _squareSpr, new Color(0.36f, 0.62f, 0.88f));
        var skyBot = new GameObject("SkyBot");
        skyBot.transform.SetParent(root, false);
        var sbRT   = skyBot.AddComponent<RectTransform>();
        sbRT.anchorMin = Vector2.zero;
        sbRT.anchorMax = new Vector2(1f, 0.45f);
        sbRT.offsetMin = sbRT.offsetMax = Vector2.zero;
        AddImg(skyBot, _squareSpr, new Color(0.26f, 0.50f, 0.78f));

        // Far hills — dark green, slow scroll
        var fhTex        = MakeHillTexture(256, 128, 2.5f, 20f, new Color(0.17f, 0.39f, 0.11f));
        var fhGO         = new GameObject("FarHills");
        fhGO.transform.SetParent(root, false);
        var fhRT         = fhGO.AddComponent<RectTransform>();
        fhRT.anchorMin   = Vector2.zero;
        fhRT.anchorMax   = new Vector2(1f, 0.50f);  // covers bottom 50%
        fhRT.offsetMin   = fhRT.offsetMax = Vector2.zero;
        _farHillImg      = fhGO.AddComponent<RawImage>();
        _farHillImg.texture = fhTex;
        _farHillImg.uvRect  = new Rect(0f, 0f, 1f, 1f);

        // Near hills — medium green, faster scroll, slightly shorter coverage
        var nhTex        = MakeHillTexture(256, 128, 1.8f, 22f, new Color(0.22f, 0.54f, 0.15f));
        var nhGO         = new GameObject("NearHills");
        nhGO.transform.SetParent(root, false);
        var nhRT         = nhGO.AddComponent<RectTransform>();
        nhRT.anchorMin   = Vector2.zero;
        nhRT.anchorMax   = new Vector2(1f, 0.38f);  // covers bottom 38%
        nhRT.offsetMin   = nhRT.offsetMax = Vector2.zero;
        _nearHillImg     = nhGO.AddComponent<RawImage>();
        _nearHillImg.texture = nhTex;
        _nearHillImg.uvRect  = new Rect(0f, 0f, 1f, 1f);

        // Grass strip — bright green band along the very bottom
        var grGO = new GameObject("Grass");
        grGO.transform.SetParent(root, false);
        var grRT = grGO.AddComponent<RectTransform>();
        grRT.anchorMin = Vector2.zero;
        grRT.anchorMax = new Vector2(1f, 0f);
        grRT.offsetMin = Vector2.zero;
        grRT.offsetMax = new Vector2(0f, 72f);
        AddImg(grGO, _squareSpr, new Color(0.28f, 0.65f, 0.16f));

        // Sun — glowing circle, top-right quadrant, bobs slowly
        var sunGO   = new GameObject("Sun");
        sunGO.transform.SetParent(root, false);
        _sunRT      = sunGO.AddComponent<RectTransform>();
        _sunBasePos = new Vector2(570f, 210f);
        _sunRT.anchorMin        = new Vector2(0.5f, 0.5f);
        _sunRT.anchorMax        = new Vector2(0.5f, 0.5f);
        _sunRT.pivot            = new Vector2(0.5f, 0.5f);
        _sunRT.anchoredPosition = _sunBasePos;
        _sunRT.sizeDelta        = new Vector2(130f, 130f);
        AddImg(sunGO, _circleSpr, new Color(1.00f, 0.88f, 0.12f));

        // Clouds — three soft ellipses that drift left and wrap
        float[] cloudY     = {  240f,  155f,  310f };
        float[] cloudX     = { -310f,  200f, -640f };
        float[] cloudW     = {  320f,  240f,  280f };
        float[] cloudH     = {   88f,   66f,   74f };
        _cloudSpeeds       = new float[] { 42f, 27f, 34f };
        _cloudRTs          = new RectTransform[3];
        for (int i = 0; i < 3; i++)
        {
            var cGO    = new GameObject($"Cloud{i}");
            cGO.transform.SetParent(root, false);
            var cRT    = cGO.AddComponent<RectTransform>();
            cRT.anchorMin        = new Vector2(0.5f, 0.5f);
            cRT.anchorMax        = new Vector2(0.5f, 0.5f);
            cRT.pivot            = new Vector2(0.5f, 0.5f);
            cRT.anchoredPosition = new Vector2(cloudX[i], cloudY[i]);
            cRT.sizeDelta        = new Vector2(cloudW[i], cloudH[i]);
            AddImg(cGO, _cloudSpr, new Color(0.94f, 0.96f, 0.98f, 0.88f));
            _cloudRTs[i] = cRT;
        }

        // Logo shadow — dark offset copy renders below the main logo
        AddLogoText(root, "LogoShadow", "FARM FURY",
            pos:      new Vector2(5f, 55f),
            size:     new Vector2(1100f, 200f),
            fontSize: 128f,
            color:    new Color(0.08f, 0.12f, 0.04f, 0.65f));

        // Logo "FARM FURY" — large, bold, golden yellow
        AddLogoText(root, "Logo", "FARM FURY",
            pos:      new Vector2(0f, 60f),
            size:     new Vector2(1100f, 200f),
            fontSize: 128f,
            color:    new Color(1.00f, 0.88f, 0.10f));

        // Subtitle
        AddLogoText(root, "Subtitle", "Farm Animals vs The Robot Invasion",
            pos:      new Vector2(0f, -42f),
            size:     new Vector2(1100f, 60f),
            fontSize: 30f,
            color:    new Color(0.94f, 0.90f, 0.72f));

        // PLAY button — large, prominent green button
        var playGO  = new GameObject("PlayBtn");
        playGO.transform.SetParent(root, false);
        var playRT  = playGO.AddComponent<RectTransform>();
        playRT.anchorMin        = new Vector2(0.5f, 0.5f);
        playRT.anchorMax        = new Vector2(0.5f, 0.5f);
        playRT.pivot            = new Vector2(0.5f, 0.5f);
        playRT.anchoredPosition = new Vector2(0f, -180f);
        playRT.sizeDelta        = new Vector2(320f, 90f);
        var playImg = playGO.AddComponent<Image>();
        playImg.sprite = _squareSpr;
        playImg.color  = new Color(0.16f, 0.52f, 0.20f);

        var lblGO   = new GameObject("Label");
        lblGO.transform.SetParent(playGO.transform, false);
        var lRT     = lblGO.AddComponent<RectTransform>();
        lRT.anchorMin = Vector2.zero;
        lRT.anchorMax = Vector2.one;
        lRT.offsetMin = lRT.offsetMax = Vector2.zero;
        var lTMP    = lblGO.AddComponent<TextMeshProUGUI>();
        lTMP.text      = "> PLAY";
        lTMP.fontSize  = 48f;
        lTMP.color     = Color.white;
        lTMP.alignment = TextAlignmentOptions.Center;
        lTMP.enableWordWrapping = false;
        lTMP.fontStyle = FontStyles.Bold;

        var playBtn = playGO.AddComponent<Button>();
        playBtn.targetGraphic = playImg;
        var pc      = playBtn.colors;
        pc.normalColor      = Color.white;
        pc.highlightedColor = new Color(0.80f, 0.80f, 0.80f);
        pc.pressedColor     = new Color(0.58f, 0.58f, 0.58f);
        playBtn.colors = pc;
        playBtn.onClick.AddListener(OnPlayClicked);

        // Version / world label — bottom-right, subtle
        var verGO   = new GameObject("Version");
        verGO.transform.SetParent(root, false);
        var verRT   = verGO.AddComponent<RectTransform>();
        verRT.anchorMin        = new Vector2(1f, 0f);
        verRT.anchorMax        = new Vector2(1f, 0f);
        verRT.pivot            = new Vector2(1f, 0f);
        verRT.anchoredPosition = new Vector2(-22f, 22f);
        verRT.sizeDelta        = new Vector2(340f, 38f);
        var verTMP  = verGO.AddComponent<TextMeshProUGUI>();
        verTMP.text      = "World 1 — Meadow Ruins";
        verTMP.fontSize  = 22f;
        verTMP.color     = new Color(0.80f, 0.80f, 0.80f, 0.55f);
        verTMP.alignment = TextAlignmentOptions.Right;
        verTMP.enableWordWrapping = false;

        _panel.SetActive(false);  // Start() enables when State == Idle
    }

    // ── Layout helpers ────────────────────────────────────────────────────────

    static void AddFullScreen(Transform parent, string name, Sprite spr, Color col)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        AddImg(go, spr, col);
    }

    static void AddImg(GameObject go, Sprite spr, Color col)
    {
        var img  = go.AddComponent<Image>();
        img.sprite = spr;
        img.color  = col;
    }

    static void AddLogoText(Transform parent, string name, string text,
        Vector2 pos, Vector2 size, float fontSize, Color color)
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
        tmp.text      = text;
        tmp.fontSize  = fontSize;
        tmp.color     = color;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.enableWordWrapping = false;
        tmp.fontStyle = FontStyles.Bold;
    }

    // ── Texture / sprite generators ───────────────────────────────────────────

    // Hill silhouette: sine wave at midpoint, solid col below, transparent above.
    // wrapMode=Repeat enables seamless horizontal scrolling via RawImage.uvRect.
    static Texture2D MakeHillTexture(int w, int h, float cycles, float amp, Color col)
    {
        var tex      = new Texture2D(w, h, TextureFormat.ARGB32, false);
        tex.wrapMode = TextureWrapMode.Repeat;
        var px       = new Color[w * h];
        float mid    = h * 0.50f;
        for (int x = 0; x < w; x++)
        {
            float top = mid + Mathf.Sin(x / (float)w * Mathf.PI * 2f * cycles) * amp;
            for (int y = 0; y < h; y++)
                px[y * w + x] = y < top ? col : Color.clear;
        }
        tex.SetPixels(px);
        tex.Apply();
        return tex;
    }

    // Soft-edged ellipse — used for clouds.
    static Sprite MakeCloudSprite(int w, int h)
    {
        var tex = new Texture2D(w, h, TextureFormat.ARGB32, false);
        var px  = new Color[w * h];
        float cx = w * 0.5f, cy = h * 0.5f;
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            float dx = (x - cx) / (w * 0.5f);
            float dy = (y - cy) / (h * 0.5f);
            float a  = Mathf.Clamp01((1f - Mathf.Sqrt(dx * dx + dy * dy)) / 0.22f);
            px[y * w + x] = new Color(1f, 1f, 1f, a);
        }
        tex.SetPixels(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), Vector2.one * 0.5f, 1f);
    }

    // Soft-edged circle — used for the sun.
    static Sprite MakeCircleSprite(int d)
    {
        var tex = new Texture2D(d, d, TextureFormat.ARGB32, false);
        var px  = new Color[d * d];
        float r = d * 0.5f;
        for (int y = 0; y < d; y++)
        for (int x = 0; x < d; x++)
        {
            float dx = x - r + 0.5f, dy = y - r + 0.5f;
            float a  = Mathf.Clamp01((r - Mathf.Sqrt(dx * dx + dy * dy)) / 1.5f);
            px[y * d + x] = new Color(1f, 1f, 1f, a);
        }
        tex.SetPixels(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, d, d), Vector2.one * 0.5f, d);
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
