using UnityEngine;

/// <summary>
/// One rule for sizing hit/death effects to what the player actually sees (2026-11-09, user: "the effect art, like
/// explosions (both wood and stone), are too big and visibly over sized").
///
/// Before this, bursts were sized from numbers that no longer matched the art: blocks from their padded LevelData
/// rect (a stone slab's rect is ~1.4x its stone, so its burst came out ~4.5 u across), robots from their transform
/// scale (5.5-7.5 since the robot art moved to 256 px at a high PPU, which made a 0.8 u robot's explosion 2.7 u), and
/// every burst ignored its own sprite's native size. Now every effect is a diameter in world units derived from the
/// VISIBLE art (the sprite's tight mesh, × the renderer's scale), then the effect sprite is scaled so its own art
/// spans exactly that diameter.
/// </summary>
public static class EffectSizing
{
    /// <summary>World-space size and centre of a renderer's visible pixels (tight sprite mesh, rotation ignored).</summary>
    public static Vector2 VisibleSize(SpriteRenderer sr, out Vector3 centre)
    {
        centre = sr != null ? sr.transform.position : Vector3.zero;
        if (sr == null || sr.sprite == null) return Vector2.one * 0.5f;
        Sprite sp = sr.sprite;
        Vector2[] v = sp.vertices;
        Vector2 min, max;
        if (v != null && v.Length >= 3)
        {
            min = max = v[0];
            for (int i = 1; i < v.Length; i++) { min = Vector2.Min(min, v[i]); max = Vector2.Max(max, v[i]); }
        }
        else { min = sp.bounds.min; max = sp.bounds.max; }
        Vector3 ls = sr.transform.lossyScale;
        Vector2 local = (min + max) * 0.5f;
        if (sr.flipX) local.x = -local.x;
        centre = sr.transform.TransformPoint(local);
        return new Vector2((max.x - min.x) * Mathf.Abs(ls.x), (max.y - min.y) * Mathf.Abs(ls.y));
    }

    /// <summary>
    /// A burst diameter for an object of this visible size: k × the geometric mean of its sides (so a long thin
    /// plank gets a plank-sized puff, not one as wide as its length), clamped to [min, max].
    /// </summary>
    public static float BurstDiameter(Vector2 visible, float k, float min = 0.45f, float max = 1.8f) =>
        Mathf.Clamp(k * Mathf.Sqrt(Mathf.Max(0.01f, visible.x * visible.y)), min, max);

    /// <summary>Uniform scale that makes <paramref name="sprite"/>'s art span <paramref name="diameter"/> world units.</summary>
    public static float ScaleFor(Sprite sprite, float diameter)
    {
        if (sprite == null) return diameter;
        float native = Mathf.Max(sprite.bounds.size.x, sprite.bounds.size.y);
        return diameter / Mathf.Max(0.0001f, native);
    }
}
