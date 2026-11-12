using System.Collections;
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
/// Every other level shows nothing. Added to the HUD's GameObject by <see cref="HUDController"/>; draws on its own
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
            _ => null,
        };
        if (r != null) _routine = StartCoroutine(r);
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
        RectTransform target = _hud != null ? _hud.SwapButtonRect : null;
        float t = 0f;
        while (!_fired && t < 7f)
        {
            if (target != null && _uiHand != null)
            {
                _uiHand.enabled = true;
                Vector3 btn = target.position;
                float bob = Mathf.Sin(t * 6f) * 14f;
                // Below-left of the button, finger pointing up-right at it.
                _uiHand.rectTransform.position = btn + new Vector3(-90f - bob, -110f - bob, 0f) * CanvasScale();
                _uiHand.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -40f);
            }
            t += Time.deltaTime;
            yield return null;
        }
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
