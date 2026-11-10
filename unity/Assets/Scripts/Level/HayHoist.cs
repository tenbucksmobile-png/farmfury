using UnityEngine;

/// <summary>
/// A hay hoist (World 1 interactive contraption, 2026-11-09): a timber crane — post with an arm reaching right —
/// with a heavy water bucket hanging from its pulley on a rope. Shoot the rope or the bucket and the rope snaps:
/// the bucket drops onto whatever is below and smashes it through the ordinary collision damage (it is heavy, like
/// the rolling wagon). Debris knocking the bucket just makes it swing. The crane itself is scenery (no collider), so
/// it never blocks a shot.
/// </summary>
public class HayHoist : MonoBehaviour
{
    const float BucketMass = 14f;
    const float BucketWidth = 0.75f;
    const float RopeHitWidth = 0.42f;   // forgiving: the rope is thin on screen
    // Measured on HoistCrane.png by tools/import_cornfield_art.py --ambience (fractions of the trimmed sprite).
    // The rope hangs from the underside of the arm's tip (the art's pulley wheel dangles on a strap further down; the
    // rope is drawn through it). Measured: arm underside at 0.70 of the height, tip column 0.93 of the width.
    public static readonly Vector2 RopeAnchorUV = new Vector2(0.93f, 0.70f);
    public const float PostUV = 0.244f;   // post centre across the width
    public const float BucketHandleUV = 0.97f;   // rope knot on the handle in HoistBucket.png, from the bottom

    Rigidbody2D _bucket;
    DistanceJoint2D _joint;
    LineRenderer _rope;
    Transform _ropeHit;
    Vector2 _pulley;
    Vector2 _handleLocal;
    bool _cut;

    /// <param name="basePos">foot of the post</param>
    /// <param name="postHeight">height of the whole crane (the arm reaches about 0.68x this sideways)</param>
    /// <param name="faceLeft">mirror the crane so its arm reaches left (post standing right of the target)</param>
    public static HayHoist Spawn(Transform parent, Sprite crane, Sprite bucket, Vector2 basePos, float postHeight, float ropeLength, bool faceLeft = false,
                                 float handleUV = BucketHandleUV, Vector2? anchorUV = null, float postUV = PostUV)
    {
        var go = new GameObject("HayHoist");
        go.transform.SetParent(parent, false);
        var h = go.AddComponent<HayHoist>();

        // Crane: scaled so the art's height is the post height; its bottom-left corner region is the post foot.
        float craneW = postHeight, craneH = postHeight;
        Vector2 craneMin = basePos;
        if (crane != null)
        {
            var sr = new GameObject("Crane").AddComponent<SpriteRenderer>();
            sr.transform.SetParent(go.transform, false);
            sr.sprite = crane;
            sr.sortingOrder = 1;   // scenery band, behind blocks (2)
            Bounds b = crane.bounds;
            float s = postHeight / b.size.y;
            craneW = b.size.x * s; craneH = postHeight;
            // basePos is the post's foot; the post sits at the left of the art (right when mirrored).
            sr.flipX = faceLeft;
            craneMin = new Vector2(basePos.x - craneW * (faceLeft ? 1f - postUV : postUV), basePos.y);
            sr.transform.localScale = new Vector3(s, s, 1f);
            sr.transform.position = new Vector3(craneMin.x + craneW * 0.5f, craneMin.y + craneH * 0.5f, 0f);
        }
        // Other worlds' cranes (World 3's mill hoist, 2026-11-10) pass their own measured anchor and post.
        Vector2 anchor = anchorUV ?? RopeAnchorUV;
        float ax = faceLeft ? 1f - anchor.x : anchor.x;
        h._pulley = craneMin + new Vector2(craneW * ax, craneH * anchor.y);

        // Bucket: dynamic, hanging still under the pulley.
        var bgo = new GameObject("HoistBucket");
        bgo.transform.SetParent(go.transform, false);
        var bsr = bgo.AddComponent<SpriteRenderer>();
        bsr.sprite = bucket;
        bsr.sortingOrder = 2;
        float bh = BucketWidth;
        float bs = 1f;
        if (bucket != null)
        {
            bs = BucketWidth / bucket.bounds.size.x;
            bh = bucket.bounds.size.y * bs;
        }
        bgo.transform.localScale = new Vector3(bs, bs, 1f);
        // Handle top = rope end.
        float handleAboveCentre = (handleUV - 0.5f) * bh;
        Vector2 handle = h._pulley + Vector2.down * Mathf.Max(0.3f, ropeLength);
        bgo.transform.position = new Vector3(handle.x, handle.y - handleAboveCentre, 0f);
        h._handleLocal = new Vector2(0f, handleAboveCentre / bs);

        var rb = bgo.AddComponent<Rigidbody2D>();
        rb.mass = BucketMass;
        rb.gravityScale = BlockBase.LiveGravityScale;
        rb.angularDamping = 0.6f;
        rb.linearDamping = 0.05f;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb.sleepMode = RigidbodySleepMode2D.StartAsleep;
        var box = bgo.AddComponent<BoxCollider2D>();
        box.size = new Vector2(BucketWidth * 0.8f / bs, bh * 0.62f / bs);   // the tub, not the handle
        box.offset = new Vector2(0f, -bh * 0.17f / bs);
        // Default layer (not Block): collides with everything, but the blocks' support/neighbour queries on the Block
        // layer never mistake the bucket for part of a structure.
        bgo.layer = 0;
        bgo.AddComponent<HayHoistBucket>().Owner = h;
        h._bucket = rb;

        var joint = bgo.AddComponent<DistanceJoint2D>();
        joint.autoConfigureDistance = false;
        joint.anchor = h._handleLocal;
        joint.connectedBody = null;   // the world
        joint.connectedAnchor = h._pulley;
        joint.distance = Mathf.Max(0.3f, ropeLength);
        joint.maxDistanceOnly = true;  // a rope: slack allowed, never stretches
        h._joint = joint;

        // Rope visual.
        var lr = go.AddComponent<LineRenderer>();
        lr.positionCount = 2;
        lr.useWorldSpace = true;
        lr.widthMultiplier = 0.05f;
        lr.sortingOrder = 2;
        lr.material = new Material(Shader.Find("Sprites/Default"));
        lr.startColor = lr.endColor = new Color(0.55f, 0.42f, 0.25f);
        h._rope = lr;

        // Rope hit zone: a kinematic trigger kept along the rope.
        var hit = new GameObject("RopeHit");
        hit.transform.SetParent(go.transform, false);
        var hrb = hit.AddComponent<Rigidbody2D>();
        hrb.bodyType = RigidbodyType2D.Kinematic;
        var hc = hit.AddComponent<BoxCollider2D>();
        hc.isTrigger = true;
        hit.AddComponent<HayHoistRope>().Owner = h;
        h._ropeHit = hit.transform;
        h.LateUpdate();
        return h;
    }

    void LateUpdate()
    {
        if (_bucket == null) return;
        Vector2 handle = _bucket.transform.TransformPoint(_handleLocal);
        _rope.enabled = !_cut;
        if (_cut) return;
        _rope.SetPosition(0, _pulley);
        _rope.SetPosition(1, handle);
        Vector2 d = handle - _pulley;
        _ropeHit.position = (_pulley + handle) * 0.5f;
        _ropeHit.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
        _ropeHit.GetComponent<BoxCollider2D>().size = new Vector2(d.magnitude, RopeHitWidth);
    }

    internal static bool IsShot(Rigidbody2D rb) =>
        rb != null && (rb.GetComponent<AnimalBase>() != null || rb.GetComponent<AbilityProjectile>() != null ||
                       rb.GetComponent<EggProjectile>() != null);

    public void Cut()
    {
        if (_cut) return;
        _cut = true;
        if (_joint != null) Destroy(_joint);
        if (_ropeHit != null) _ropeHit.gameObject.SetActive(false);
        _rope.enabled = false;
        _bucket.WakeUp();
        AudioManager.Play(AudioManager.Sound.WoodHit);
    }
}

sealed class HayHoistRope : MonoBehaviour
{
    public HayHoist Owner;
    void OnTriggerEnter2D(Collider2D other) { if (HayHoist.IsShot(other.attachedRigidbody)) Owner.Cut(); }
}

sealed class HayHoistBucket : MonoBehaviour
{
    public HayHoist Owner;
    void OnCollisionEnter2D(Collision2D c) { if (HayHoist.IsShot(c.rigidbody)) Owner.Cut(); }
}
