using System.Collections;
using UnityEngine;

namespace FarmFury.Core
{
    /// <summary>One stamp of a trail cosmetic (Arcade's ghost trail): holds briefly, then shrinks and fades out.</summary>
    public class CosmeticTrailGhost : MonoBehaviour
    {
        private const float HoldFraction = 0.45f;
        private const float DarkenMultiplier = 0.7f;

        public void Configure(Sprite sprite, int sortingLayerID, int sortingOrder, float scale, float lifetimeSeconds)
        {
            var sr = gameObject.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingLayerID = sortingLayerID;
            sr.sortingOrder = sortingOrder;
            sr.color = new Color(DarkenMultiplier, DarkenMultiplier, DarkenMultiplier, 1f);
            transform.localScale = Vector3.one * scale;
            StartCoroutine(FadeAndDestroy(sr, lifetimeSeconds));
        }

        private IEnumerator FadeAndDestroy(SpriteRenderer sr, float lifetimeSeconds)
        {
            Vector3 startScale = transform.localScale;
            float t = 0f;
            while (t < lifetimeSeconds)
            {
                t += Time.deltaTime;
                float progress = Mathf.Clamp01(t / lifetimeSeconds);
                float fade = Mathf.Clamp01((progress - HoldFraction) / (1f - HoldFraction));
                Color c = sr.color;
                c.a = 1f - fade;
                sr.color = c;
                transform.localScale = Vector3.Lerp(startScale, startScale * 0.75f, progress);
                yield return null;
            }
            Destroy(gameObject);
        }
    }
}
