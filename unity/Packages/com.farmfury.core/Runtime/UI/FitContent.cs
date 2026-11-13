using UnityEngine;

namespace FarmFury.Shared.UI
{
    /// <summary>
    /// Shows a picture whose sprite is its real content (<see cref="contentSize"/>) with extra margin painted around
    /// it (<see cref="spriteSize"/>): the content is scaled to fit entirely inside the parent (never cropped, never
    /// stretched), centred, and the margin fills whatever room is left over - on a phone wider than the content,
    /// the extended edges show instead of empty bars. Re-fits every frame (a few multiplies).
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class FitContent : MonoBehaviour
    {
        public Vector2 contentSize = new(1280f, 720f);
        public Vector2 spriteSize = new(1280f, 720f);

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
            float scale = Mathf.Min(size.x / contentSize.x, size.y / contentSize.y);

            var rt = (RectTransform)transform;
            var centre = new Vector2(0.5f, 0.5f);
            rt.anchorMin = centre;
            rt.anchorMax = centre;
            rt.pivot = centre;
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = spriteSize * scale;
        }
    }
}
