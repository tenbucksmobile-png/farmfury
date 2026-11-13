using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Beginner tips on the first four cornfield levels (2026-11-12, user: "illustration that assists the user in gameplay -
/// specific to cornfield; things like advise that pops up ... 'pull back on cannon to aim and fire' 'press here to change
/// characters' ... same as angry birds? only show this perhaps with the first 4 levels of cornfield - then remove").
/// A speech bubble at the top of the screen and a cartoon pointing hand (UI/Franchise/Hint/HintHand.png, Kling):
///   W1_L01  "Pull back on the cannon to aim - let go to fire!" with the hand dragging back from the loaded animal,
///           until the first shot; then "Topple the structure onto the robots!" for a few seconds.
///   W1_L02  "Press here to change characters" with the hand pointing at the HUD's switch button.
///   W1_L03  "Clear every robot here - then the cannon moves on to the next yard!"
///   W1_L04  (abilities unlock) on the first shot: "Tap anywhere while Cluck is flying - she drops a burst of eggs!" with
///           a tapping hand, until the ability is used or the shot ends.
/// Every other level (2026-11-13): situational swap advice - see SwapAdvice. Added to the HUD's GameObject by <see cref="HUDController"/>; draws on its own
/// overlay canvas above the HUD (100) and below the menus (400).
/// </summary>
public class TutorialHints : MonoBehaviour
{
    HUDController _hud;
    Sprite _hand;
    Canvas _canvas;
    RectTransform _bubble;
    TextMeshProUGUI _text;
    Image _uiHand;
    SpriteRenderer _worldHand;
    Coroutine _routine;
    bool _fired, _ability;

    public void Init(HUDController hud, Sprite hand)
    {
        _hud = hud;
        _hand = hand;
        Build();
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnLevelStarted += OnLevelStarted;
            GameManager.Instance.OnStateChanged += OnStateChanged;
        }
        CatapultLauncher.Fired += OnFired;
        AnimalBase.AbilityUsed += OnAbility;
    }

    void OnDestroy()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnLevelStarted -= OnLevelStarted;
            GameManager.Instance.OnStateChanged -= OnStateChanged;
        }
        CatapultLauncher.Fired -= OnFired;
        AnimalBase.AbilityUsed -= OnAbility;
    }

    void OnFired() => _fired = true;
    void OnAbility() => _ability = true;

    void OnStateChanged(GameState s)
    {
        if (s != GameState.Playing && s != GameState.Paused) HideAll();
    }

    void OnLevelStarted(LevelData level)
    {
        if (_routine != null) StopCoroutine(_routine);
        HideAll();
        _fired = _ability = false;
        string id = level != null ? level.levelId : "";
        IEnumerator r = id switch
        {
            "W1_L01" => AimLesson(),
            "W1_L02" => SwapLesson(),
            "W1_L03" => RoomsLesson(),
            "W1_L04" => AbilityLesson(),
            _ => SwapAdvice(),   // every other level (2026-11-13)
        };
        if (r != null) _routine = StartCoroutine(r);
    }

    // ── swap advice, every level after the lessons (2026-11-13) ─────────────────────────────────────────────────
    // User: "remember the hint in situations where swapping characters for certain abilities should appear (all
    // levels)". Between shots, the stage is read and, when it calls for an ability that the animal in the cannon doesn't
    // have but one waiting in the lineup does, the bubble names that animal and the hand points at the switch button.
    // At most once per stage, gone on the next shot or after 6 s; nothing when the cannon already holds a good fit.
    // Situations, first match wins:
    //   robots sheltering under a roof    -> droppers: Cluck (egg burst), Gerald (feather bombs)
    //   mostly stone                      -> heavy hitters: Bessie (Ground Slam), Horace (horseshoes)
    //   robots spread wide across stage   -> area: Ducky (swirls), Billy (mud), Woolly (wool balls)
    //   a tall tower                      -> Percy (Bounce Roll), Horace
    static readonly (AnimalType[] who, string why)[] Situations =
    {
        (new[] { AnimalType.Cluck, AnimalType.Gerald }, "roof"),
        (new[] { AnimalType.Bessie, AnimalType.Horace }, "stone"),
        (new[] { AnimalType.Ducky, AnimalType.Billy, AnimalType.Woolly }, "wide"),
        (new[] { AnimalType.Percy, AnimalType.Horace }, "tall"),
    };

    static string Reason(AnimalType a) => a switch
    {
        AnimalType.Cluck => "Cluck's egg burst drops onto robots hiding under roofs",
        AnimalType.Gerald => "Gerald's feather bombs drop onto robots hiding under roofs",
        AnimalType.Bessie => "Bessie's Ground Slam shakes heavy stone loose",
        AnimalType.Horace => "Horace's horseshoes ricochet through the structure",
        AnimalType.Ducky => "Ducky's water swirls splash a wide area",
        AnimalType.Billy => "Billy's mud ball splats over a wide area",
        AnimalType.Woolly => "Woolly's wool balls bounce across the whole stage",
        AnimalType.Percy => "Percy's Bounce Roll bowls through tall towers",
        _ => "",
    };

    IEnumerator SwapAdvice()
    {
        yield return WaitForReveal();
        var loader = FindAnyObjectByType<LevelLoader>();
        int shownRoom = -1, lastKey = -1;
        while (loader != null)
        {
            var flow = FarmFury.UI.GameFlow.Instance;
            int room = loader.CurrentRoom;
            int key = room * 1000 + loader.BirdQueueSnapshot.Length;   // one look per loaded animal
            if (room != shownRoom && key != lastKey && flow != null && flow.CanSwapCharacters && !loader.InRoomTransition)
            {
                yield return new WaitForSeconds(0.6f);   // let the stage settle after the reveal / last shot
                if (flow.CanSwapCharacters && loader.CurrentRoom == room)
                {
                    lastKey = key;
                    var pick = Advise(loader);
                    if (pick.HasValue)
                    {
                        shownRoom = room;
                        Say($"Try {pick.Value} here - {Reason(pick.Value)}. Press here to switch!");
                        _fired = false;
                        yield return PointAtSwap(6f, () => !flow.CanSwapCharacters);
                        HideAll();
                    }
                }
            }
            yield return null;
        }
    }

    // The animal worth switching to for the current stage, if one is waiting in the lineup and the cannon's isn't a fit.
    static AnimalType? Advise(LevelLoader loader)
    {
        var queue = loader.BirdQueueSnapshot;
        if (queue.Length < 2) return null;
        AnimalType loaded = queue[0];

        var robots = new List<Bounds>();
        foreach (var r in loader.SpawnedRobots)
            if (r != null && !r.IsDestroyed && r.TryGetComponent<Collider2D>(out var c)) robots.Add(c.bounds);
        if (robots.Count == 0) return null;
        var blocks = new List<(Bounds b, bool stone)>();
        foreach (var b in loader.SpawnedBlocks)
            if (b != null && b.TryGetComponent<Collider2D>(out var c)) blocks.Add((c.bounds, b is StoneBlock));

        // Which situations apply.
        int sheltered = 0;
        foreach (var r in robots)
            foreach (var (b, _) in blocks)
                if (b.min.y >= r.max.y - 0.05f && b.min.y - r.max.y < 1.6f && b.min.x < r.max.x && b.max.x > r.min.x) { sheltered++; break; }
        int stone = 0;
        float top = float.MinValue;
        foreach (var (b, s) in blocks) { if (s) stone++; top = Mathf.Max(top, b.max.y); }
        float minX = float.MaxValue, maxX = float.MinValue;
        foreach (var r in robots) { minX = Mathf.Min(minX, r.center.x); maxX = Mathf.Max(maxX, r.center.x); }
        float ground = ReferenceFraming.World1GroundY;
        bool[] applies =
        {
            sheltered * 2 >= robots.Count,                        // half or more of the robots under cover
            blocks.Count > 0 && stone * 100 >= blocks.Count * 35, // a third or more of the pieces are stone
            maxX - minX > 6.5f,                                    // robots spread across the stage
            top - ground > 5.5f,                                   // a tall tower
        };

        for (int i = 0; i < Situations.Length; i++)
        {
            if (!applies[i]) continue;
            var who = Situations[i].who;
            if (System.Array.IndexOf(who, loaded) >= 0) return null;   // the cannon already holds a good fit
            foreach (var a in who)
                for (int q = 1; q < queue.Length; q++)
                    if (queue[q] == a) return a;
        }
        return null;
    }

    // The hand below-left of the HUD's switch button, bobbing toward it, until a shot, `stop`, or `seconds` pass.
    IEnumerator PointAtSwap(float seconds, System.Func<bool> stop)
    {
        RectTransform target = _hud != null ? _hud.SwapButtonRect : null;
        float t = 0f;
        while (!_fired && t < seconds && (stop == null || !stop()))
        {
            if (target != null && _uiHand != null)
            {
                _uiHand.enabled = true;
                Vector3 btn = target.position;
                float bob = Mathf.Sin(t * 6f) * 14f;
                _uiHand.rectTransform.position = btn + new Vector3(-90f - bob, -110f - bob, 0f) * CanvasScale();
                _uiHand.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -40f);
                _uiHand.rectTransform.localScale = Vector3.one;
            }
            t += Time.deltaTime;
            yield return null;
        }
    }

    // ── the lessons ───────────────────────────────────────────────────────────────────────────────────────────────
    IEnumerator WaitForReveal()
    {
        yield return null;
        while (LevelReveal.IsRevealing) yield return null;
        yield return new WaitForSeconds(0.3f);
    }

    IEnumerator AimLesson()
    {
        yield return WaitForReveal();
        Say("Pull back on the cannon to aim - let go to fire!");
        var launcher = FindAnyObjectByType<CatapultLauncher>();
        float t = 0f;
        while (!_fired)
        {
            // The hand grabs the loaded animal and drags back and down (the aiming pull), then lets go and repeats.
            if (launcher != null && _worldHand != null)
            {
                float k = Mathf.Repeat(t, 1.8f) / 1.8f;
                float pull = k < 0.15f ? 0f : k < 0.7f ? Mathf.SmoothStep(0f, 1f, (k - 0.15f) / 0.55f) : 1f;
                Vector3 from = launcher.LoadedPosition;
                _worldHand.enabled = k < 0.85f;
                // The finger points up: hang the hand half its height below the animal so the fingertip touches it.
                float half = Camera.main != null ? Camera.main.orthographicSize * 0.12f : 0.95f;
                _worldHand.transform.position = from + new Vector3(-1.6f, -1.1f, 0f) * pull + new Vector3(0.1f, -half, 0f);
                float s = 1.1f * (k < 0.15f ? 1.1f - 0.1f * (k / 0.15f) : 1f);
                _worldHand.transform.localScale = new Vector3(s, s, 1f) * HandScale();
            }
            t += Time.deltaTime;
            yield return null;
        }
        HideAll();
        yield return new WaitForSeconds(2.2f);
        Say("Topple the structure onto the robots!");
        yield return new WaitForSeconds(3.5f);
        HideAll();
    }

    IEnumerator SwapLesson()
    {
        yield return WaitForReveal();
        Say("Press here to change characters");
        yield return PointAtSwap(7f, null);
        HideAll();
    }

    IEnumerator RoomsLesson()
    {
        yield return WaitForReveal();
        Say("Clear every robot here - then the cannon moves on to the next yard!");
        float t = 0f;
        while (!_fired && t < 6f) { t += Time.deltaTime; yield return null; }
        HideAll();
    }

    IEnumerator AbilityLesson()
    {
        yield return WaitForReveal();
        Say("Abilities unlocked! Watch for your chance...");
        while (!_fired) yield return null;
        yield return new WaitForSeconds(0.25f);
        Say("Tap anywhere while Cluck is flying - she drops a burst of eggs!");
        var launcher = FindAnyObjectByType<CatapultLauncher>();
        float t = 0f;
        while (!_ability && t < 3.5f && (launcher == null || launcher.ShotInProgress))
        {
            if (_uiHand != null)
            {
                _uiHand.enabled = true;
                float tap = Mathf.Abs(Mathf.Sin(t * 5f));
                _uiHand.rectTransform.anchoredPosition = new Vector2(120f, -60f - 30f * tap);
                _uiHand.rectTransform.localRotation = Quaternion.identity;
                _uiHand.rectTransform.localScale = Vector3.one * (1f - 0.12f * tap);
            }
            t += Time.deltaTime;
            yield return null;
        }
        HideAll();
        if (_ability)
        {
            Say("Great! Every animal has its own ability.");
            yield return new WaitForSeconds(3f);
            HideAll();
        }
    }

    // ── UI ────────────────────────────────────────────────────────────────────────────────────────────────────────
    void Build()
    {
        var root = new GameObject("TutorialHints");
        root.transform.SetParent(transform, false);
        _canvas = root.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 150;
        var cs = root.AddComponent<CanvasScaler>();
        cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        cs.referenceResolution = new Vector2(1920f, 1080f);
        cs.matchWidthOrHeight = 0.5f;

        // Speech bubble: a dark rounded panel under the top bar, centred.
        var bubbleGo = new GameObject("Bubble", typeof(RectTransform));
        bubbleGo.transform.SetParent(root.transform, false);
        _bubble = (RectTransform)bubbleGo.transform;
        _bubble.anchorMin = _bubble.anchorMax = new Vector2(0.5f, 1f);
        _bubble.pivot = new Vector2(0.5f, 1f);
        _bubble.anchoredPosition = new Vector2(0f, -150f);
        _bubble.sizeDelta = new Vector2(1180f, 120f);
        var bg = bubbleGo.AddComponent<Image>();
        bg.sprite = MakeRoundedSprite();
        bg.type = Image.Type.Sliced;
        bg.color = new Color(0.08f, 0.1f, 0.16f, 0.82f);
        bg.raycastTarget = false;
        var textGo = new GameObject("Text", typeof(RectTransform));
        textGo.transform.SetParent(bubbleGo.transform, false);
        var trt = (RectTransform)textGo.transform;
        trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
        trt.offsetMin = new Vector2(30f, 10f); trt.offsetMax = new Vector2(-30f, -10f);
        _text = textGo.AddComponent<TextMeshProUGUI>();
        _text.alignment = TextAlignmentOptions.Center;
        _text.fontSize = 46f;
        _text.fontStyle = FontStyles.Bold;
        _text.color = new Color(1f, 0.95f, 0.75f);
        _text.enableAutoSizing = true; _text.fontSizeMin = 28f; _text.fontSizeMax = 46f;
        _text.raycastTarget = false;

        // The hand on the canvas (pointing at UI, tapping), and in the world (dragging the cannon's animal).
        var handGo = new GameObject("Hand", typeof(RectTransform));
        handGo.transform.SetParent(root.transform, false);
        _uiHand = handGo.AddComponent<Image>();
        _uiHand.sprite = _hand;
        _uiHand.preserveAspect = true;
        _uiHand.raycastTarget = false;
        _uiHand.rectTransform.sizeDelta = new Vector2(150f, 150f);
        if (_hand == null) _uiHand.color = new Color(1f, 1f, 1f, 0f);
        var worldGo = new GameObject("HintHandWorld");
        worldGo.transform.SetParent(transform, false);
        _worldHand = worldGo.AddComponent<SpriteRenderer>();
        _worldHand.sprite = _hand;
        _worldHand.sortingOrder = 50;   // over the play layer and the wipes, under the HUD canvas
        HideAll();
    }

    void Say(string line)
    {
        _text.text = line;
        _bubble.gameObject.SetActive(true);
        StartCoroutine(PopIn(_bubble));
    }

    static IEnumerator PopIn(RectTransform rt)
    {
        for (float e = 0f; e < 0.25f; e += Time.unscaledDeltaTime)
        {
            float k = e / 0.25f;
            rt.localScale = Vector3.one * (k < 0.7f ? Mathf.Lerp(0.6f, 1.08f, k / 0.7f) : Mathf.Lerp(1.08f, 1f, (k - 0.7f) / 0.3f));
            yield return null;
        }
        rt.localScale = Vector3.one;
    }

    void HideAll()
    {
        if (_bubble != null) _bubble.gameObject.SetActive(false);
        if (_uiHand != null) _uiHand.enabled = false;
        if (_worldHand != null) _worldHand.enabled = false;
    }

    // World units for the world-space hand: about a sixth of the screen height.
    float HandScale()
    {
        var cam = Camera.main;
        if (_hand == null || cam == null) return 1f;
        return cam.orthographicSize * 2f * 0.12f / Mathf.Max(0.0001f, _hand.bounds.size.y);
    }

    float CanvasScale() => _canvas != null ? _canvas.scaleFactor : 1f;

    static Sprite _rounded;
    static Sprite MakeRoundedSprite()
    {
        if (_rounded != null) return _rounded;
        const int N = 64, R = 24;
        var tex = new Texture2D(N, N, TextureFormat.ARGB32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float dx = Mathf.Max(0f, Mathf.Max(R - x - 0.5f, x + 0.5f - (N - R))),
                      dy = Mathf.Max(0f, Mathf.Max(R - y - 0.5f, y + 0.5f - (N - R)));
                float a = Mathf.Clamp01(R - Mathf.Sqrt(dx * dx + dy * dy) + 0.5f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        tex.Apply();
        _rounded = Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect,
                                 new Vector4(R, R, R, R));
        return _rounded;
    }
}
