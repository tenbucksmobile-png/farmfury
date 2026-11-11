using UnityEngine;

namespace FarmFury.UI
{
    /// <summary>
    /// Fades a screen in each time it is shown (2026-11-11, user: "slow down the flow into every page... the game plays
    /// very fast"). Unscaled time, so it also runs while a level is frozen behind Pause.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class ScreenFadeIn : MonoBehaviour
    {
        public const float Duration = 0.35f;
        CanvasGroup _group;
        float _t;

        void Awake() => _group = GetComponent<CanvasGroup>();

        void OnEnable()
        {
            if (_group == null) _group = GetComponent<CanvasGroup>();
            _t = 0f;
            _group.alpha = 0f;
        }

        void Update()
        {
            if (_t >= Duration) return;
            _t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(_t / Duration);
            _group.alpha = 1f - (1f - p) * (1f - p);
        }

        /// <summary>Adds the fade to a screen root (no-op when null or already there).</summary>
        public static void AddTo(GameObject root)
        {
            if (root == null || root.GetComponent<ScreenFadeIn>() != null) return;
            if (root.GetComponent<CanvasGroup>() == null) root.AddComponent<CanvasGroup>();
            root.AddComponent<ScreenFadeIn>();
        }
    }
}
