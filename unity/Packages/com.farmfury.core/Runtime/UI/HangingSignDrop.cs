using UnityEngine;

namespace FarmFury.Shared.UI
{
    /// <summary>
    /// Drops a hanging sign in from above the screen: it falls to its resting place, overshoots a little and settles,
    /// then sways gently on its ropes (a damped swing about its top edge - give the RectTransform a top pivot).
    /// Unscaled time, so it runs while the game is frozen.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class HangingSignDrop : MonoBehaviour
    {
        public float dropSeconds = 0.6f;
        public float swayDegrees = 4f;
        public float swaySeconds = 2.2f;
        public float swayPeriod = 0.9f;

        private RectTransform _rect;
        private Vector2 _rest;
        private float _startY;
        private float _startedAt = -1f;

        /// <summary>Starts the drop towards restPosition (anchored), from just above the top of the parent.</summary>
        public void Play(Vector2 restPosition)
        {
            _rect = (RectTransform)transform;
            _rest = restPosition;
            var parent = (RectTransform)_rect.parent;
            _startY = restPosition.y + parent.rect.height * 0.5f + _rect.rect.height + 50f;
            _startedAt = Time.unscaledTime;
            Apply(0f);
        }

        private void Update()
        {
            if (_startedAt >= 0f)
            {
                Apply(Time.unscaledTime - _startedAt);
            }
        }

        private void Apply(float t)
        {
            float drop = Mathf.Clamp01(t / dropSeconds);
            // Ease-out-back: lands, overshoots slightly past the rest point, settles.
            const float c1 = 1.4f, c3 = c1 + 1f;
            float eased = 1f + c3 * Mathf.Pow(drop - 1f, 3f) + c1 * Mathf.Pow(drop - 1f, 2f);
            _rect.anchoredPosition = new Vector2(_rest.x, Mathf.LerpUnclamped(_startY, _rest.y, eased));

            float swayTime = t - dropSeconds;
            float angle = 0f;
            if (swayTime > 0f && swayTime < swaySeconds)
            {
                float damping = 1f - swayTime / swaySeconds;
                angle = swayDegrees * damping * damping * Mathf.Sin(swayTime * Mathf.PI * 2f / swayPeriod);
            }
            _rect.localRotation = Quaternion.Euler(0f, 0f, angle);
            if (swayTime >= swaySeconds)
            {
                _startedAt = -1f;
            }
        }
    }
}
