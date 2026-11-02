using FarmFury.Core;
using UnityEngine;

/// <summary>
/// Draws an animal's equipped cosmetics (franchise Phase 5b, adapted from Stampede's CharacterCosmeticRenderer for
/// a launcher game). Added by LevelLoader.CreateNextAnimal to every animal it spawns (the one sitting in the cannon
/// and the one in flight).
///  - Hat (SaveManager.GetEquippedHat): a child sprite on top of the body, half the body sprite's width, its centre at
///    82% of the sprite's height, measured from the body SpriteRenderer's current sprite each frame (so it follows
///    pose swaps). An approximation — FarmFury has no per-pose head points yet; tune HatTop/HatWidth after seeing it.
///  - Trail (SaveManager.GetEquippedTrail, shared by all animals): fading ghost stamps of the trail art behind the
///    animal while it is flying (Dynamic body moving faster than MinTrailSpeed), Arcade/Stampede's ghost trail.
/// Art comes from ShopArt.hatArt / trailArt on GameFlow, in StoreProducts.Hats / Trails order.
/// </summary>
public class AnimalCosmeticRenderer : MonoBehaviour
{
    const float HatWidth = 0.5f;          // of the body sprite's width
    const float HatTop = 0.82f;           // hat centre, as a fraction of the body sprite's height from its bottom
    const float GhostInterval = 0.05f;
    const float GhostLifetime = 0.45f;
    const float GhostScale = 0.6f;        // of the animal's world-space width
    const float MinTrailSpeed = 1.5f;

    SpriteRenderer _body;
    Rigidbody2D _rb;
    SpriteRenderer _hat;
    Sprite _trail;
    float _nextGhost;

    public void Init(AnimalType animal)
    {
        _body = GetComponent<SpriteRenderer>();
        _rb = GetComponent<Rigidbody2D>();
        var save = SaveManager.Instance;
        var shop = FarmFury.UI.GameFlow.Instance != null ? FarmFury.UI.GameFlow.Instance.Shop : null;
        if (save == null || shop == null) { enabled = false; return; }

        int hat = System.Array.IndexOf(StoreProducts.Hats, save.GetEquippedHat(animal));
        var hatSprite = hat >= 0 && save.IsCosmeticOwned(StoreProducts.Hats[hat]) ? FarmFury.UI.ShopArt.At(shop.hatArt, hat) : null;
        if (hatSprite != null)
        {
            var go = new GameObject("Hat");
            go.transform.SetParent(transform, false);
            _hat = go.AddComponent<SpriteRenderer>();
            _hat.sprite = hatSprite;
        }

        int trail = System.Array.IndexOf(StoreProducts.Trails, save.GetEquippedTrail());
        _trail = trail >= 0 && save.IsCosmeticOwned(StoreProducts.Trails[trail]) ? FarmFury.UI.ShopArt.At(shop.trailArt, trail) : null;

        if (_hat == null && _trail == null) { enabled = false; }
    }

    void LateUpdate()
    {
        if (_body == null || _body.sprite == null) return;

        if (_hat != null)
        {
            _hat.enabled = _body.enabled;
            _hat.sortingLayerID = _body.sortingLayerID;
            _hat.sortingOrder = _body.sortingOrder + 1;
            var b = _body.sprite.bounds;   // local (pre-transform) units, so the child inherits scale/rotation
            float width = b.size.x * HatWidth;
            float hatWidth = Mathf.Max(0.0001f, _hat.sprite.bounds.size.x);
            _hat.transform.localScale = Vector3.one * (width / hatWidth);
            _hat.transform.localPosition = new Vector3(b.center.x, b.min.y + b.size.y * HatTop, 0f);
        }

        if (_trail != null && _rb != null && _rb.bodyType == RigidbodyType2D.Dynamic
            && _rb.linearVelocity.sqrMagnitude > MinTrailSpeed * MinTrailSpeed && Time.time >= _nextGhost)
        {
            _nextGhost = Time.time + GhostInterval;
            var go = new GameObject("TrailGhost");
            go.transform.position = _body.bounds.center;
            float scale = _body.bounds.size.x * GhostScale / Mathf.Max(0.0001f, _trail.bounds.size.x);
            go.AddComponent<FarmFury.Core.CosmeticTrailGhost>().Configure(_trail, _body.sortingLayerID, _body.sortingOrder - 1, scale, GhostLifetime);
        }
    }
}
