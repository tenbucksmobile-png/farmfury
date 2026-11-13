using UnityEngine;

namespace FarmFury.UI
{
    /// <summary>
    /// Gently breathes a UI element's scale (and optionally alpha) — the landing page's PRESS START prompt.
    /// Unscaled time, so it runs whatever Time.timeScale is.
    /// </summary>
    public sealed class PulseScale : MonoBehaviour
    {
        public float amount = 0.08f;     // +/- fraction of the base scale
        public float period = 1.4f;      // seconds per full breath
        public float minAlpha = 0.65f;   // alpha at the small end (1 = no fade); needs a CanvasGroup to apply

        Vector3 _base;
        CanvasGroup _group;

        void Awake()
        {
            _base = transform.localScale;
            _group = GetComponent<CanvasGroup>();
        }

        void OnEnable() => _base = _base == Vector3.zero ? transform.localScale : _base;

        void Update()
        {
            float s = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * Mathf.PI * 2f / Mathf.Max(0.1f, period));
            transform.localScale = _base * (1f + amount * (2f * s - 1f));
            if (_group != null) _group.alpha = Mathf.Lerp(minAlpha, 1f, s);
        }
    }
}
