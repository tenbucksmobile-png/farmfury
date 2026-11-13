using UnityEngine;

namespace FarmFury.Shared.UI
{
    /// <summary>
    /// Sizes a full-screen picture a little smaller than "cover": <see cref="zoom"/> times the size that would just
    /// cover the parent, but never smaller than the size that fits all of it on screen. Whatever it no longer covers
    /// shows the layer behind it. <see cref="pivotY"/> is both where it's anchored on the parent and its own pivot, so
    /// any overflow is split the same way as a pivoted cover backdrop (0.8 = mostly off the bottom).
    /// Re-fits every frame (a few multiplies), so it follows resolution changes and fields set after AddComponent.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class ZoomedCover : MonoBehaviour
    {
        public float aspect = 16f / 9f;
        [Range(0.5f, 1f)] public float zoom = 1f;
        [Range(0f, 1f)] public float pivotY = 0.5f;

        private void OnEnable() => Fit();

        private void Update() => Fit();

        private void Fit()
        {
            var parent = transform.parent as RectTransform;
            if (parent == null)
            {
                return;
            }

            var size = parent.rect.size;

            float coverWidth = Mathf.Max(size.x, size.y * aspect);
            float containWidth = Mathf.Min(size.x, size.y * aspect);
            float width = Mathf.Max(containWidth, coverWidth * zoom);

            var rt = (RectTransform)transform;
            var anchor = new Vector2(0.5f, pivotY);
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(width, width / aspect);
        }
    }
}
