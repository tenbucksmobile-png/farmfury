using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// A single tappable pin on the Sunrise Meadows world map. Built entirely at runtime by
// WorldMapController (no prefab) as a UI Image + Button rather than a literal SpriteRenderer —
// Unity's Button/GraphicRaycaster only work through uGUI, so this keeps the marker genuinely
// clickable the same way every other button in this project already works.
public class LevelMarker : MonoBehaviour
{
    public int LevelIndex { get; private set; } // 0-based

    private RectTransform      _rt;
    private Image              _image;
    private TextMeshProUGUI    _numberLabel;
    private Action<int>        _onClicked;
    private Sprite              _fallbackSpr;
    private Coroutine          _shakeRoutine;

    private static readonly Vector2 MarkerSize = new(56f, 84f); // matches LevelMarker_*.png's 256x384 (2:3) aspect

    // fallbackSpr: a plain white square used when no real marker art is wired yet, so markers
    // are still visible/tappable (tinted per state) before FarmFury -> Wire Scene References runs.
    public void Init(int levelIndex, Vector2 anchoredPos, Sprite fallbackSpr, Action<int> onClicked)
    {
        LevelIndex   = levelIndex;
        _onClicked   = onClicked;
        _fallbackSpr = fallbackSpr;

        _rt = gameObject.AddComponent<RectTransform>();
        _rt.anchorMin        = new Vector2(0.5f, 0.5f);
        _rt.anchorMax        = new Vector2(0.5f, 0.5f);
        _rt.pivot            = new Vector2(0.5f, 0.5f);
        _rt.anchoredPosition = anchoredPos;
        _rt.sizeDelta        = MarkerSize;

        _image = gameObject.AddComponent<Image>();

        var btn = gameObject.AddComponent<Button>();
        btn.targetGraphic = _image;
        var colors = btn.colors;
        colors.highlightedColor = new Color(0.92f, 0.92f, 0.92f);
        colors.pressedColor     = new Color(0.75f, 0.75f, 0.75f);
        btn.colors = colors;
        btn.onClick.AddListener(() => _onClicked?.Invoke(LevelIndex));

        var lblGO = new GameObject("Number");
        lblGO.transform.SetParent(transform, false);
        var lblRT = lblGO.AddComponent<RectTransform>();
        lblRT.anchorMin        = new Vector2(0.5f, 0.5f);
        lblRT.anchorMax        = new Vector2(0.5f, 0.5f);
        lblRT.pivot            = new Vector2(0.5f, 0.5f);
        lblRT.anchoredPosition = new Vector2(0f, 12f); // upper portion of the badge art
        lblRT.sizeDelta        = new Vector2(50f, 40f);
        _numberLabel = lblGO.AddComponent<TextMeshProUGUI>();
        _numberLabel.text               = (levelIndex + 1).ToString();
        _numberLabel.fontSize           = 22f;
        _numberLabel.fontStyle          = FontStyles.Bold;
        _numberLabel.alignment          = TextAlignmentOptions.Center;
        _numberLabel.enableWordWrapping = false;
        _numberLabel.raycastTarget      = false;
    }

    // Sprite params may be null individually — falls back to the nearest available state art,
    // then to a tinted placeholder square if nothing is wired at all. There is currently no
    // dedicated 2-star marker asset (only Locked/Unlocked/1star/3stars exist — see CLAUDE.md
    // checklist), so a 2-star result borrows the 3-star art.
    public void Refresh(bool unlocked, int stars,
        Sprite lockedSpr, Sprite unlockedSpr, Sprite star1Spr, Sprite star2Spr, Sprite star3Spr)
    {
        Sprite chosen =
            !unlocked      ? lockedSpr :
            stars >= 3     ? (star3Spr != null ? star3Spr : unlockedSpr) :
            stars == 2     ? (star2Spr != null ? star2Spr : (star3Spr != null ? star3Spr : unlockedSpr)) :
            stars == 1     ? (star1Spr != null ? star1Spr : unlockedSpr) :
                             unlockedSpr;

        if (chosen != null)
        {
            _image.sprite = chosen;
            _image.color  = Color.white;
        }
        else
        {
            _image.sprite = _fallbackSpr;
            _image.color  = !unlocked ? new Color(0.35f, 0.35f, 0.40f)
                          : stars > 0 ? new Color(1.00f, 0.82f, 0.20f)
                                      : new Color(0.55f, 0.75f, 0.55f);
        }

        _numberLabel.color = unlocked ? Color.white : new Color(0.55f, 0.55f, 0.60f);
    }

    public Vector2 AnchoredPosition => _rt.anchoredPosition;

    // Locked-tap feedback: quick damped horizontal shake, then settle back exactly on the
    // marker's real anchored position (reads it fresh each call so it can't drift).
    public void PlayLockedShake()
    {
        if (_shakeRoutine != null) StopCoroutine(_shakeRoutine);
        _shakeRoutine = StartCoroutine(ShakeRoutine());
    }

    IEnumerator ShakeRoutine()
    {
        Vector2 basePos = _rt.anchoredPosition;
        const float duration  = 0.3f;
        const float amplitude = 8f;
        const float frequency = 28f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float damp = 1f - (elapsed / duration);
            float x    = Mathf.Sin(elapsed * frequency) * amplitude * damp;
            _rt.anchoredPosition = basePos + new Vector2(x, 0f);
            yield return null;
        }
        _rt.anchoredPosition = basePos;
        _shakeRoutine = null;
    }
}
