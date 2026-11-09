using UnityEngine;

/// <summary>
/// A boulder of glacier ice parked on a ledge (World 2 contraption, 2026-11-09 — the Frozen Tundra's rolling wagon).
/// Static until something hits it: an animal or ability projectile, or debris with enough momentum. Then it becomes a
/// heavy rolling body, nudged toward the structures (+x), and smashes through whatever it rolls into via the ordinary
/// collision damage. Art: World2Props/IceBoulder.png (a rock frozen inside a ball of ice).
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class RollingBoulder : MonoBehaviour
{
    const float Mass = 18f;
    const float ReleaseImpulse = 3f;
    const float Nudge = 7f;
    bool _released;
    Rigidbody2D _rb;

    public static RollingBoulder Spawn(Transform parent, Sprite sprite, Vector2 basePos, float diameter)
    {
        var go = new GameObject("IceBoulder");
        go.transform.SetParent(parent, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = 2;
        float native = sprite != null ? Mathf.Max(sprite.bounds.size.x, sprite.bounds.size.y) : 1f;
        float s = diameter / native;
        go.transform.localScale = new Vector3(s, s, 1f);
        go.transform.position = new Vector3(basePos.x, basePos.y + diameter * 0.5f, 0f);
        var rb = go.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Static;
        var col = go.AddComponent<CircleCollider2D>();
        col.radius = native * 0.47f;
        col.sharedMaterial = new PhysicsMaterial2D("IceBoulder") { friction = 0.35f, bounciness = 0.1f };
        return go.AddComponent<RollingBoulder>();
    }

    void Awake() => _rb = GetComponent<Rigidbody2D>();

    void OnCollisionEnter2D(Collision2D c)
    {
        if (_released || c.rigidbody == null) return;
        bool shot = c.rigidbody.GetComponent<AnimalBase>() != null || c.rigidbody.GetComponent<AbilityProjectile>() != null;
        if (shot || c.relativeVelocity.magnitude * c.rigidbody.mass >= ReleaseImpulse) Release();
    }

    public void Release()
    {
        if (_released) return;
        _released = true;
        _rb.bodyType = RigidbodyType2D.Dynamic;
        _rb.mass = Mass;
        _rb.gravityScale = BlockBase.LiveGravityScale;
        _rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        _rb.WakeUp();
        _rb.AddForce(Vector2.right * Nudge, ForceMode2D.Impulse);
        _rb.angularVelocity = -120f;
    }
}
