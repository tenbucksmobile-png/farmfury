using UnityEngine;

namespace FarmFury.UI
{
    /// <summary>Shows a UI element, holds it, fades it out and deactivates it (unscaled time, so it runs while paused). Arcade's Purchase Complete banner: 4s hold, 0.5s fade.</summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class HoldThenFade : MonoBehaviour
    {
        public float holdSeconds = 4f;
        public float fadeSeconds = 0.5f;
        private float _shownAt;

        public void Play()
        {
            gameObject.SetActive(true);
            GetComponent<CanvasGroup>().alpha = 1f;
            _shownAt = Time.unscaledTime;
        }

        private void Update()
        {
            float t = Time.unscaledTime - _shownAt - holdSeconds;
            if (t <= 0f)
            {
                return;
            }

            GetComponent<CanvasGroup>().alpha = 1f - Mathf.Clamp01(t / fadeSeconds);
            if (t >= fadeSeconds)
            {
                gameObject.SetActive(false);
            }
        }
    }
}
