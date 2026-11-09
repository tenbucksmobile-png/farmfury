using UnityEngine;

namespace FarmFury.Core
{
    /// <summary>
    /// In-game side of the cosmetics (2026-11-08): the equipped cannon skin and the robot-defeat impact effect. Art is
    /// ShopArt.cannonArt / impactArt on GameFlow (StoreProducts.Cannons / Impacts order); nothing shows unless the item
    /// is owned and equipped. Trails are drawn by AnimalCosmeticRenderer.
    /// </summary>
    public static class CosmeticEffects
    {
        const float ImpactSize = 1.3f;     // world units across at full size (a robot is ~0.8; was 2.4, 3x a robot — 2026-11-09)
        const int ImpactSortingOrder = 11; // over the block/robot death bursts (10)

        static FarmFury.UI.ShopArt Shop => FarmFury.UI.GameFlow.Instance != null ? FarmFury.UI.GameFlow.Instance.Shop : null;

        static Sprite Equipped(string[] category, Sprite[] art)
        {
            var save = SaveManager.Instance;
            if (save == null || art == null) return null;
            int i = System.Array.IndexOf(category, save.GetEquipped(category));
            return i >= 0 && save.IsCosmeticOwned(category[i]) ? FarmFury.UI.ShopArt.At(art, i) : null;
        }

        /// <summary>The equipped cannon skin, or null for the world's standard cannon.</summary>
        public static Sprite CannonSkin() => Shop != null ? Equipped(StoreProducts.Cannons, Shop.cannonArt) : null;

        /// <summary>Spawns the equipped impact burst at a defeated robot (no-op when none is equipped).</summary>
        public static void SpawnImpact(Vector3 position)
        {
            var sprite = Shop != null ? Equipped(StoreProducts.Impacts, Shop.impactArt) : null;
            if (sprite == null) return;
            var go = new GameObject("ImpactEffect");
            go.transform.position = new Vector3(position.x, position.y, 0f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = ImpactSortingOrder;
            float native = Mathf.Max(sprite.bounds.size.x, sprite.bounds.size.y);
            go.AddComponent<ImpactBurst>().Init(ImpactSize / Mathf.Max(0.0001f, native));
        }
    }

    /// <summary>Pops an impact sprite in (overshoot), holds briefly, then drifts up and fades out.</summary>
    public class ImpactBurst : MonoBehaviour
    {
        const float PopTime = 0.18f, HoldTime = 0.25f, FadeTime = 0.55f;
        float _scale, _t;
        SpriteRenderer _sr;

        public void Init(float scale)
        {
            _scale = scale;
            _sr = GetComponent<SpriteRenderer>();
            transform.localScale = Vector3.one * scale * 0.3f;
            transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(-15f, 15f));
        }

        void Update()
        {
            _t += Time.deltaTime;
            if (_t < PopTime)
            {
                float k = _t / PopTime;
                transform.localScale = Vector3.one * _scale * Mathf.Lerp(0.3f, 1.12f, 1f - (1f - k) * (1f - k));
            }
            else if (_t < PopTime + HoldTime)
            {
                transform.localScale = Vector3.one * _scale * Mathf.Lerp(1.12f, 1f, (_t - PopTime) / HoldTime);
            }
            else
            {
                float k = (_t - PopTime - HoldTime) / FadeTime;
                if (k >= 1f) { Destroy(gameObject); return; }
                var c = _sr.color; c.a = 1f - k; _sr.color = c;
                transform.position += Vector3.up * Time.deltaTime * 0.6f;
                transform.localScale = Vector3.one * _scale * (1f + 0.15f * k);
            }
        }
    }
}
