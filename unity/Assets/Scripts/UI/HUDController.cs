using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using FarmFury.Shared.UI;

// In-level HUD: the animal-card queue (top-left, animated) and the round pause button (top-right).
// Creates its own Canvas (sortingOrder 100) procedurally in Awake(); nothing needs pre-wiring beyond the
// card/pause sprites SceneSetup assigns.
// Franchise flow (2026-11-02): the Level Complete/Failed panels, the PAUSED toggle and the top-right Quit/Mute
// buttons moved out — results and pause are GameFlow's ResultsScreen/PauseScreen (Stampede's full-screen art
// screens), mute lives in Settings (Pause's cog), quit is Pause's X/home. Pause stays top-right, not at the
// bottom like Stampede's HUD: the cannon and the aiming drag own the bottom-left of the screen.
// Place on any scene GO (SceneSetup creates a "HUD" GO for this).
public class HUDController : MonoBehaviour
{
    public static HUDController Instance { get; private set; }

    // UI leaf references set during BuildCanvas()
    private RectTransform   _birdQueueRoot;

    // Parallel lists: card RectTransforms + their base X positions
    private readonly List<RectTransform> _birdIcons   = new();
    private readonly List<float>         _birdIconsX  = new();

    private LevelLoader _levelLoader;

    // Generated once per HUDController lifetime
    private Sprite _circleSpr;
    private Sprite _squareSpr;

    // Card sprites: indexed by (int)AnimalType, wired via SceneSetup
    [SerializeField] private Sprite[] _cardSprites = new Sprite[8];

    // Card layout constants — Angry Birds style: big active card, overlapping queue cards
    // Halved 2026-11-08 (user request: "make the top left character lives smaller") — at the reference framing
    // the old 200x260 portrait covered a big slice of the sky the shots fly through.
    private const float CardActiveW  = 104f;
    private const float CardActiveH  = 135f;
    private const float CardQueueW   = 78f;
    private const float CardQueueH   = 101f;
    private const float CardGap      = -26f;  // negative = cards overlap each other

    private const float BobAmp  = 3.5f;  // pixels
    private const float BobFreq = 1.8f;  // Hz

    [SerializeField] private Sprite _pauseButtonSprite; // Btn_pause.png
    [SerializeField] private Sprite _coinSprite;        // Franchise/Shop/Coin_UI.png
    [SerializeField] private Sprite _lockerSprite;      // Franchise/Shop/Locker.png
    [SerializeField] private Sprite _swapSprite;        // Franchise/Menu/SwapCharacter.png (Arcade/Stampede swap icon)
    Button _swapButton;
    /// <summary>The in-level character switch button (TutorialHints points at it).</summary>
    public RectTransform SwapButtonRect => _swapButton != null ? (RectTransform)_swapButton.transform : null;
    // Beginner tips on the first four cornfield levels (TutorialHints, 2026-11-12): the pointing hand
    // (Sprites/UI/Franchise/Hint/HintHand.png, Kling), wired by SceneSetup.
    [SerializeField] private Sprite _hintHandSprite;
    Image  _swapImage;
    private RectTransform  _topRightRoot;
    private Text           _coinText;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        EnsureEventSystem();
        _circleSpr = MakeCircleSprite(64);
        _squareSpr = MakeSquareSprite();
        BuildCanvas();
    }

    void Start()
    {
        gameObject.AddComponent<TutorialHints>().Init(this, _hintHandSprite);
        _levelLoader = FindAnyObjectByType<LevelLoader>();
        if (FarmFury.Core.SaveManager.Instance != null)
        {
            FarmFury.Core.SaveManager.Instance.CoinsChanged += RefreshCoins;
            RefreshCoins(FarmFury.Core.SaveManager.Instance.CoinBalance);
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnLevelStarted += OnLevelStarted;
            GameManager.Instance.OnStateChanged  += OnStateChanged;

            // Catch up with the state we start in (MainMenu at boot fires no transition event, so without this
            // the top bar would sit visible behind the landing screen).
            OnStateChanged(GameManager.Instance.State);
            if (GameManager.Instance.State == GameState.Playing)
                RefreshBirdIcons();
        }

        if (_levelLoader != null)
            _levelLoader.OnBirdConsumed += RefreshBirdIcons;
    }

    void OnDestroy()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnLevelStarted -= OnLevelStarted;
            GameManager.Instance.OnStateChanged  -= OnStateChanged;
        }
        if (_levelLoader != null)
            _levelLoader.OnBirdConsumed -= RefreshBirdIcons;
        if (FarmFury.Core.SaveManager.Instance != null)
            FarmFury.Core.SaveManager.Instance.CoinsChanged -= RefreshCoins;
    }

    void RefreshCoins(int balance)
    {
        if (_coinText != null) _coinText.text = balance.ToString("N0");
    }

    void Update()
    {
        AnimateBirdIcons();
        // The switch only works between shots (nothing in flight, not aiming); dimmed otherwise.
        if (_swapButton != null)
        {
            var flow = FarmFury.UI.GameFlow.Instance;
            bool can = flow != null && flow.CanSwapCharacters;
            if (_swapButton.interactable != can) _swapButton.interactable = can;
        }
    }

    // ── Event handlers ────────────────────────────────────────────────────────

    void OnLevelStarted(LevelData _) => RefreshBirdIcons();

    // The queue and pause button show only while a level is being played; Pause, the results screens and every
    // menu are full-screen GameFlow screens drawn over this canvas.
    void OnStateChanged(GameState state) =>
        SetTopBarVisible(state == GameState.Playing || state == GameState.Paused);

    // The bird-queue cards and the top-right button row both live top-of-screen; hidden
    // whenever a full-screen end-of-level panel (LevelComplete/LevelFailed) is showing, same
    // reasoning as the old score readout used to get (self-contained panels, nothing to
    // pause/mute-from-here on a finished level). _birdQueueRoot was actually missing from this
    // toggle until 2026-07-26 (user-reported leftover animal cards still visible behind the
    // Level Complete panel) — _topRightRoot added in the same pass that reinstated the button
    // row, so it doesn't repeat that bug.
    void SetTopBarVisible(bool visible)
    {
        if (_birdQueueRoot != null) _birdQueueRoot.gameObject.SetActive(visible);
        if (_topRightRoot  != null) _topRightRoot.gameObject.SetActive(visible);
        if (!visible && _roomRoot != null) _roomRoot.gameObject.SetActive(false);
        if (visible && _roomRoot != null && _roomDots.Count > 0)
        {
            var loader = _levelLoader != null ? _levelLoader : FindAnyObjectByType<LevelLoader>();
            if (loader != null) SetRoomProgress(loader.CurrentRoom, loader.RoomCount);
        }
    }

    // ── Canvas construction ───────────────────────────────────────────────────

    void BuildCanvas()
    {
        var root          = new GameObject("Canvas");
        root.transform.SetParent(transform, false);

        var canvas         = root.AddComponent<Canvas>();
        canvas.renderMode  = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        var cs                    = root.AddComponent<CanvasScaler>();
        cs.uiScaleMode            = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        cs.referenceResolution    = new Vector2(1920f, 1080f);
        cs.screenMatchMode        = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        cs.matchWidthOrHeight     = 0.5f;

        root.AddComponent<GraphicRaycaster>();

        // Screen-edge elements (the bird queue) anchor inside this instead of the raw canvas
        // rect, so they respect notch/rounded-corner safe insets. Without it they're positioned
        // against the full device screen and can render into — or get visually clipped by —
        // the unsafe edge zone (e.g. top-left cards overlapping a notch/corner).
        var safeArea = BuildSafeArea(root.transform);

        BuildBirdQueueArea(safeArea);
        BuildPauseButton(safeArea);
        BuildRoomIndicator(safeArea);
    }

    // ── Rooms (multi-room levels, 2026-11-12) ─────────────────────────────────────────────────────────────────
    // A row of dots top-centre (filled = rooms reached), shown only on levels with more than one room, and a short
    // banner over the play area when a room is cleared ("ONE SHOT! ADDING FARMFURY").
    RectTransform _roomRoot;
    readonly List<Image> _roomDots = new();
    TextMeshProUGUI _banner;
    Coroutine _bannerRoutine;

    void BuildRoomIndicator(Transform canvas)
    {
        _roomRoot = MakeRect(canvas, "RoomDots",
            anchorMin: new Vector2(0.5f, 1f), anchorMax: new Vector2(0.5f, 1f), pivot: new Vector2(0.5f, 1f),
            pos: new Vector2(0f, -22f), size: new Vector2(200f, 40f));
        _roomRoot.gameObject.SetActive(false);
        _banner = MakeCentredText(canvas, "RoomBanner", new Vector2(0f, 220f), new Vector2(1400f, 140f), 84f, "", Color.white);
        StyleAsGameNumber(_banner);
        _banner.raycastTarget = false;
        _banner.gameObject.SetActive(false);
    }

    /// <summary>LevelLoader: which room of how many (dots hidden on a one-room level).</summary>
    public void SetRoomProgress(int current, int total)
    {
        if (_roomRoot == null) return;
        _roomRoot.gameObject.SetActive(total > 1);
        while (_roomDots.Count < total)
        {
            var img = new GameObject($"Dot{_roomDots.Count}", typeof(RectTransform)).AddComponent<Image>();
            img.transform.SetParent(_roomRoot, false);
            img.sprite = _circleSpr;
            img.raycastTarget = false;
            _roomDots.Add(img);
        }
        const float D = 26f, Gap = 18f;
        float x0 = -(total - 1) * (D + Gap) * 0.5f;
        for (int i = 0; i < _roomDots.Count; i++)
        {
            var img = _roomDots[i];
            img.gameObject.SetActive(i < total);
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(D, D);
            rt.anchoredPosition = new Vector2(x0 + i * (D + Gap), 0f);
            img.color = i <= current ? new Color(1f, 0.82f, 0.25f) : new Color(1f, 1f, 1f, 0.35f);
        }
    }

    /// <summary>A short message that pops, holds and fades (unscaled time).</summary>
    public void ShowBanner(string text)
    {
        if (_banner == null) return;
        if (_bannerRoutine != null) StopCoroutine(_bannerRoutine);
        _bannerRoutine = StartCoroutine(BannerRoutine(text));
    }

    IEnumerator BannerRoutine(string text)
    {
        _banner.text = text;
        _banner.gameObject.SetActive(true);
        var t = _banner.rectTransform;
        for (float e = 0f; e < 1.9f; e += Time.unscaledDeltaTime)
        {
            float pop = e < 0.25f ? Mathf.Lerp(0.4f, 1.12f, e / 0.25f) : e < 0.4f ? Mathf.Lerp(1.12f, 1f, (e - 0.25f) / 0.15f) : 1f;
            t.localScale = Vector3.one * pop;
            _banner.alpha = e < 1.5f ? 1f : 1f - (e - 1.5f) / 0.4f;
            yield return null;
        }
        _banner.gameObject.SetActive(false);
        _bannerRoutine = null;
    }

    RectTransform BuildSafeArea(Transform canvas)
    {
        var rt = MakeFullScreenRect(canvas, "SafeArea");
        ApplySafeArea(rt);
        return rt;
    }

    static void ApplySafeArea(RectTransform rt)
    {
        if (Screen.width <= 0 || Screen.height <= 0) return;
        Rect safe = Screen.safeArea;
        Vector2 min = safe.position;
        Vector2 max = safe.position + safe.size;
        min.x /= Screen.width;  min.y /= Screen.height;
        max.x /= Screen.width;  max.y /= Screen.height;
        rt.anchorMin = min;
        rt.anchorMax = max;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    // Bird queue: anchored 2% from left edge and 4% from bottom edge of actual screen —
    // works at any landscape resolution / DPI without pixel-offset guesswork.
    void BuildBirdQueueArea(Transform canvas)
    {
        var rt         = MakeRect(canvas, "BirdQueue",
                             anchorMin: new Vector2(0.02f, 1f),
                             anchorMax: new Vector2(0.02f, 1f),
                             pivot:     new Vector2(0f, 1f),    // top-left corner anchors to top-left of screen
                             pos:       new Vector2(0f, -12f),  // 12px inset from top edge
                             size:      new Vector2(400f, 150f));
        _birdQueueRoot = rt;
    }

    // Round pause button, top-right of the safe area (96, smaller than the menus' round buttons).
    void BuildPauseButton(Transform canvas)
    {
        // Pause / Locker / Switch sit over the play area: smaller than the menus' round buttons (2026-11-11, user:
        // "dont forget the pause button on the gameplay").
        const float btnSize = 96f;
        const float inset   = 16f;

        var rootGO = new GameObject("TopRightButtons");
        rootGO.transform.SetParent(canvas, false);
        _topRightRoot = rootGO.AddComponent<RectTransform>();
        _topRightRoot.anchorMin        = new Vector2(1f, 1f);
        _topRightRoot.anchorMax        = new Vector2(1f, 1f);
        _topRightRoot.pivot            = new Vector2(1f, 1f);
        _topRightRoot.anchoredPosition = new Vector2(-inset, -inset);
        _topRightRoot.sizeDelta        = new Vector2(btnSize, btnSize);

        var pauseImg = MakeImage(_topRightRoot, "PauseBtn",
                          _pauseButtonSprite != null ? _pauseButtonSprite : _squareSpr,
                          _pauseButtonSprite != null ? Color.white : new Color(0f, 0f, 0f, 0.62f),
                          anchorMin: new Vector2(1f, 0.5f), anchorMax: new Vector2(1f, 0.5f),
                          pivot:     new Vector2(1f, 0.5f),
                          pos:       Vector2.zero,
                          size:      new Vector2(btnSize, btnSize));
        var pauseBtn = pauseImg.gameObject.AddComponent<Button>();
        pauseBtn.targetGraphic = pauseImg;
        StyleTopButtonColors(pauseBtn);
        pauseBtn.onClick.AddListener(OnPauseClicked);

        // Locker (franchise cosmetics), right under the pause button.
        var lockerImg = MakeImage(_topRightRoot, "LockerBtn",
                          _lockerSprite != null ? _lockerSprite : _squareSpr,
                          _lockerSprite != null ? Color.white : new Color(0.62f, 0.38f, 0.17f),
                          anchorMin: new Vector2(1f, 0f), anchorMax: new Vector2(1f, 0f),
                          pivot:     new Vector2(1f, 1f),
                          pos:       new Vector2(0f, -16f),
                          size:      new Vector2(btnSize, btnSize));
        var lockerBtn = lockerImg.gameObject.AddComponent<Button>();
        lockerBtn.targetGraphic = lockerImg;
        StyleTopButtonColors(lockerBtn);
        lockerBtn.onClick.AddListener(() => FarmFury.UI.GameFlow.Instance?.OpenLocker());

        // Character switch (2026-11-09), under the Locker: back to Character Select for the animals still to fire.
        _swapImage = MakeImage(_topRightRoot, "SwapBtn",
                          _swapSprite != null ? _swapSprite : _squareSpr,
                          _swapSprite != null ? Color.white : new Color(0.2f, 0.25f, 0.38f, 0.95f),
                          anchorMin: new Vector2(1f, 0f), anchorMax: new Vector2(1f, 0f),
                          pivot:     new Vector2(1f, 1f),
                          pos:       new Vector2(0f, -16f - btnSize - 16f),
                          size:      new Vector2(btnSize, btnSize));
        _swapImage.preserveAspect = true;
        _swapButton = _swapImage.gameObject.AddComponent<Button>();
        _swapButton.targetGraphic = _swapImage;
        StyleTopButtonColors(_swapButton);
        _swapButton.onClick.AddListener(() => FarmFury.UI.GameFlow.Instance?.OpenCharacterSwap());

        // Coin balance (franchise HUD chip): spinning coin + count, just left of the pause button.
        const float coinSize = 72f;
        var coinImg = MakeImage(_topRightRoot, "CoinIcon",
                          _coinSprite != null ? _coinSprite : _circleSpr,
                          _coinSprite != null ? Color.white : new Color(0.95f, 0.72f, 0.2f),
                          anchorMin: new Vector2(0f, 0.5f), anchorMax: new Vector2(0f, 0.5f),
                          pivot:     new Vector2(0.5f, 0.5f),
                          pos:       new Vector2(-24f - coinSize * 0.5f, 0f),
                          size:      new Vector2(coinSize, coinSize));
        coinImg.raycastTarget = false;
        coinImg.gameObject.AddComponent<FarmFury.Shared.UI.SpinAroundY>();
        _coinText = FarmFury.Shared.UI.UIKit.Label(_topRightRoot, "CoinCount", "0", 52, TextAnchor.MiddleRight, new Color(1f, 0.84f, 0.25f));
        _coinText.fontStyle = FontStyle.Bold;
        _coinText.horizontalOverflow = HorizontalWrapMode.Overflow;
        var outline = _coinText.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.24f, 0.11f, 0.03f);
        outline.effectDistance = new Vector2(3f, -3f);
        FarmFury.Shared.UI.UIKit.Place(_coinText.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f),
            new Vector2(-24f - coinSize - 12f, 0f), new Vector2(300f, coinSize));
    }

    static void StyleTopButtonColors(Button b)
    {
        var c = b.colors;
        c.normalColor      = Color.white;
        c.highlightedColor = new Color(0.88f, 0.88f, 0.88f);
        c.pressedColor     = new Color(0.68f, 0.68f, 0.68f);
        b.colors = c;
    }

    // ── Bird queue (card widgets) ─────────────────────────────────────────────

    void RefreshBirdIcons()
    {
        foreach (var rt in _birdIcons) if (rt != null) Destroy(rt.gameObject);
        _birdIcons.Clear();
        _birdIconsX.Clear();

        if (_levelLoader == null) return;

        var queue = _levelLoader.BirdQueueSnapshot;
        float cursorX = 0f;

        for (int i = 0; i < queue.Length; i++)
        {
            bool  active = (i == 0);
            float w = active ? CardActiveW : CardQueueW;
            float h = active ? CardActiveH : CardQueueH;
            float x = cursorX + w * 0.5f;

            AnimalType atype = queue[i];

            // Slot container — anchored left-centre of queue root
            var slot   = new GameObject($"Card_{i}");
            slot.transform.SetParent(_birdQueueRoot, false);
            var rt     = slot.AddComponent<RectTransform>();
            rt.anchorMin        = new Vector2(0f, 0.5f);
            rt.anchorMax        = new Vector2(0f, 0.5f);
            rt.pivot            = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(x, 0f);
            rt.sizeDelta        = new Vector2(w, h);

            // Card face image
            var cardImg   = slot.AddComponent<Image>();
            Sprite cardSpr = GetCardSprite(atype);
            cardImg.sprite = cardSpr != null ? cardSpr : _squareSpr;
            cardImg.color  = cardSpr != null ? Color.white : BirdColor(atype);
            // Thumbs-up portraits (square cut-outs, 2026-11-02) replace the old framed cards — keep their shape
            // inside the card-sized slot rather than stretching them to it.
            cardImg.preserveAspect = cardSpr != null;

            // Damage badge: top-right corner, orange pill with lightning + number
            BuildDamageBadge(rt, atype, active);

            // Dim inactive cards so the active one pops
            if (!active) cardImg.color = new Color(0.72f, 0.72f, 0.72f, 1f);

            _birdIcons.Add(rt);
            _birdIconsX.Add(x);
            cursorX += w + CardGap;
        }

        // Active card (index 0) must render on top of overlapping queue cards.
        // In Canvas, last sibling renders in front — move it to the end.
        if (_birdIcons.Count > 0 && _birdIcons[0] != null)
            _birdIcons[0].SetAsLastSibling();
    }

    // Badge enlarged + restyled 2026-07-26 (user request: "enlarge the score numbers and match
    // to the game font") — text uses StyleAsGameNumber() (bold + black outline + gold-to-
    // orange gradient), the same treatment as the Level Complete/Failed score numbers, instead
    // of plain flat white. Sizes enlarged again 2026-07-06 (user-reported still too small to
    // read, especially on the smaller queued cards) — badge grown to fit the bigger text
    // without clipping. The leading "⚡" was dropped the same day — this project's TMP font
    // (LiberationSans SDF, the only font asset shipped) has no glyph for U+26A1 and rendered it
    // as a tofu box, which read as "a square before the numbers" (same class of bug as the
    // missing ★ glyph fixed in MatchUpScreen's stars display — see docs/HISTORY.md Round 14).
    void BuildDamageBadge(RectTransform parent, AnimalType type, bool large)
    {
        float bw = large ? 50f : 40f;
        float bh = large ? 24f : 20f;

        var badge = MakeImage(parent, "DamageBadge", _squareSpr,
                        new Color(0.96f, 0.56f, 0.07f, 0.93f),
                        anchorMin: new Vector2(1f, 1f), anchorMax: new Vector2(1f, 1f),
                        pivot: new Vector2(1f, 1f),
                        pos: new Vector2(-3f, -3f),
                        size: new Vector2(bw, bh));

        var dmgText = MakeStretchText(badge.transform, "DmgText",
            fontSize: large ? 19f : 15f,
            text: $"{GetDamage(type)}",
            color: Color.white,
            align: TextAlignmentOptions.Center);
        StyleAsGameNumber(dmgText);
    }

    Sprite GetCardSprite(AnimalType type)
    {
        int idx = (int)type;
        return (_cardSprites != null && idx < _cardSprites.Length) ? _cardSprites[idx] : null;
    }

    static int GetDamage(AnimalType type) => type switch
    {
        AnimalType.Cluck  => 15,
        AnimalType.Bessie => 50,
        AnimalType.Percy  => 20,
        AnimalType.Woolly => 12,
        AnimalType.Ducky  => 18,
        AnimalType.Horace => 35,
        AnimalType.Gerald => 55,
        AnimalType.Billy  => 25,
        _                 => 10,
    };

    // Staggered sine-wave bob; active card (first) gets larger amplitude.
    void AnimateBirdIcons()
    {
        int n = _birdIcons.Count;
        for (int i = 0; i < n; i++)
        {
            if (_birdIcons[i] == null) continue;
            float phase = i * (Mathf.PI * 2f / Mathf.Max(n, 1));
            float amp   = (i == 0) ? BobAmp * 1.5f : BobAmp;
            float yOff  = Mathf.Sin(Time.unscaledTime * BobFreq * Mathf.PI * 2f + phase) * amp;
            _birdIcons[i].anchoredPosition = new Vector2(_birdIconsX[i], yOff);
        }
    }

    // ── Pause logic ───────────────────────────────────────────────────────────
    // No PAUSED popup (removed 2026-07-06 per user request) — tapping Pause pauses/resumes
    // directly, and the button's own icon swaps to communicate the next tap's action.

    // Also called by Editor/PanelPreview.cs via SendMessage("OnPauseClicked").
    void OnPauseClicked() => FarmFury.UI.GameFlow.Instance?.OpenPause();

    static TextMeshProUGUI MakeCentredText(Transform parent, string name,
        Vector2 pos, Vector2 size, float fontSize, string text, Color color,
        TextAlignmentOptions align = TextAlignmentOptions.Center)
    {
        var rt = MakeRect(parent, name,
                     anchorMin: new Vector2(0.5f, 0.5f), anchorMax: new Vector2(0.5f, 0.5f),
                     pivot: new Vector2(0.5f, 0.5f), pos: pos, size: size);
        var tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
        tmp.text               = text;
        tmp.fontSize           = fontSize;
        tmp.color              = color;
        tmp.alignment          = align;
        tmp.enableWordWrapping = false;
        return tmp;
    }

    // Approximates the bold, black-outlined, gold-to-orange gradient cartoon lettering used in
    // the game's own art (LevelFailed.png/LevelComplete.png titles) using only the default TMP
    // font's built-in outline + vertex-gradient support. No custom font/sprite asset exists in
    // this project (TextMesh Pro ships only LiberationSans SDF) — this is the closest match
    // achievable without importing new font or sprite art.
    static void StyleAsGameNumber(TextMeshProUGUI tmp)
    {
        tmp.color               = Color.white; // must be white or it tints the gradient below
        tmp.fontStyle            = FontStyles.Bold;
        tmp.enableVertexGradient = true;
        tmp.colorGradient        = new VertexGradient(
            new Color(1.00f, 0.87f, 0.40f), new Color(1.00f, 0.87f, 0.40f),
            new Color(0.95f, 0.55f, 0.05f), new Color(0.95f, 0.55f, 0.05f));
        var mat = tmp.fontMaterial; // instance, safe to edit without affecting other TMP text
        mat.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.22f);
        mat.SetColor(ShaderUtilities.ID_OutlineColor, Color.black);
        tmp.fontMaterial = mat;
    }

    // Square icon button (e.g. Btn_play / Btn_home) — falls back to a plain orange
    // square with no icon if sprite is unwired, same fallback pattern MainMenuController uses.
    Button MakeIconButton(Transform parent, string name, Sprite sprite, Color fallbackColor,
        Vector2 pos, Vector2 size)
    {
        var img = MakeImage(parent, name, sprite != null ? sprite : _squareSpr,
                      sprite != null ? Color.white : fallbackColor,
                      new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                      new Vector2(0.5f, 0.5f), pos, size);
        img.preserveAspect = true;
        var btn           = img.gameObject.AddComponent<Button>();
        btn.targetGraphic = img;
        var cols              = btn.colors;
        cols.normalColor      = Color.white;
        cols.highlightedColor = new Color(0.88f, 0.88f, 0.88f);
        cols.pressedColor     = new Color(0.68f, 0.68f, 0.68f);
        btn.colors            = cols;
        return btn;
    }

    // RectTransform that stretches to fill its parent — used for full-screen overlays.
    static RectTransform MakeFullScreenRect(Transform parent, string name)
    {
        var go       = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt       = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        return rt;
    }

    static RectTransform MakeRect(Transform parent, string name,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        var go              = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt              = go.AddComponent<RectTransform>();
        rt.anchorMin        = anchorMin;
        rt.anchorMax        = anchorMax;
        rt.pivot            = pivot;
        rt.anchoredPosition = pos;
        rt.sizeDelta        = size;
        return rt;
    }

    static Image MakeImage(Transform parent, string name, Sprite sprite, Color color,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        var rt              = MakeRect(parent, name, anchorMin, anchorMax, pivot, pos, size);
        var img             = rt.gameObject.AddComponent<Image>();
        img.sprite          = sprite;
        img.color           = color;
        img.type            = Image.Type.Simple;
        return img;
    }

    // Text that stretches to fill its parent RectTransform.
    static TextMeshProUGUI MakeStretchText(Transform parent, string name,
        float fontSize, string text, Color color, TextAlignmentOptions align)
    {
        var go          = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt          = go.AddComponent<RectTransform>();
        rt.anchorMin    = Vector2.zero;
        rt.anchorMax    = Vector2.one;
        rt.offsetMin    = Vector2.zero;
        rt.offsetMax    = Vector2.zero;
        var tmp         = go.AddComponent<TextMeshProUGUI>();
        tmp.text        = text;
        tmp.fontSize    = fontSize;
        tmp.color       = color;
        tmp.alignment   = align;
        tmp.enableWordWrapping = false;
        return tmp;
    }

    // ── Sprite generation ─────────────────────────────────────────────────────

    // Anti-aliased circle, alpha = 1 inside, 0 outside with 1-pixel feather.
    static Sprite MakeCircleSprite(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.ARGB32, false);
        var px  = new Color[size * size];
        float r = size * 0.5f;
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float dx   = x - r + 0.5f;
            float dy   = y - r + 0.5f;
            float dist = Mathf.Sqrt(dx * dx + dy * dy);
            float a    = Mathf.Clamp01(r - dist);
            px[y * size + x] = new Color(1f, 1f, 1f, a);
        }
        tex.SetPixels(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), Vector2.one * 0.5f, size);
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

    // ── Animal colour map ─────────────────────────────────────────────────────

    static Color BirdColor(AnimalType type) => type switch
    {
        AnimalType.Cluck  => new Color(1.00f, 0.87f, 0.10f),
        AnimalType.Bessie => new Color(1.00f, 0.48f, 0.70f),
        _                 => new Color(0.78f, 0.78f, 0.83f),
    };

    // ── EventSystem ───────────────────────────────────────────────────────────

    static void EnsureEventSystem()
    {
        if (FindAnyObjectByType<EventSystem>() != null) return;
        var go = new GameObject("EventSystem");
        go.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
        go.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
        go.AddComponent<StandaloneInputModule>();
#endif
    }
}
