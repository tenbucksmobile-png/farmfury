using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// A single tappable pin on the Sunrise Meadows world map. Built entirely at runtime by
// WorldMapController (no prefab) as a UI Image + Button rather than a literal SpriteRenderer —
// Unity's Button/GraphicRaycaster only work through uGUI, so this keeps the marker genuinely
// clickable the same way every other button in this project already works.
public class LevelMarker : MonoBehaviour
{
    public int LevelIndex { get; private set; } // 0-based

    private RectTransform      _rt;
    private Image              _image;
    private Action<int>        _onClicked;
    private Sprite              _fallbackSpr;
    private Coroutine          _shakeRoutine;

    // Enlarged 2026-07-24 per user feedback ("increase the sizing of the widgets on the path")
    // — was 56x84, now 80x120 (same 2:3 aspect as the 256x384 source art, just ~1.43x bigger).
    private static readonly Vector2 MarkerSize = new(80f, 120f);

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
        // LevelMarker_Locked.png is 256x384 (2:3, matches MarkerSize exactly) but
        // LevelMarker_tick.png is a square 500x500 asset — the two source images have
        // permanently different native aspect ratios, so no per-sprite fit mode can make them
        // BOTH undistorted AND identically sized at the same time. Tried preserveAspect=true
        // 2026-07-10 (avoids distortion) but that made the two sprites render at visibly
        // different sizes — tick letterboxes down to 80x80 inside the 80x120 box while locked
        // fills it exactly — which the user then flagged as worse ("they still not the same
        // sizing, ensure they are the same size"). Reverted to the default stretch-to-fill
        // (preserveAspect left false/unset): both sprites now render at IDENTICAL 80x120
        // (MarkerSize) — the tick art is mildly stretched vertically, a smaller visual cost than
        // 18 markers along the path reading as inconsistent sizes.

        var btn = gameObject.AddComponent<Button>();
        btn.targetGraphic = _image;
        var colors = btn.colors;
        colors.highlightedColor = new Color(0.92f, 0.92f, 0.92f);
        colors.pressedColor     = new Color(0.75f, 0.75f, 0.75f);
        btn.colors = colors;
        btn.onClick.AddListener(() => _onClicked?.Invoke(LevelIndex));
    }

    // Sprite params may be null individually — falls back to a tinted placeholder square if
    // nothing is wired at all. Every unlocked level (any star count) shows the same tick art
    // (LevelMarker_tick.png, wired as unlockedSpr) — there is no per-star-tier marker art.
    public void Refresh(bool unlocked, Sprite lockedSpr, Sprite unlockedSpr)
    {
        Sprite chosen = unlocked ? unlockedSpr : lockedSpr;

        if (chosen != null)
        {
            _image.sprite = chosen;
            _image.color  = Color.white;
        }
        else
        {
            _image.sprite = _fallbackSpr;
            _image.color  = unlocked ? new Color(0.55f, 0.75f, 0.55f)
                                      : new Color(0.35f, 0.35f, 0.40f);
        }
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
