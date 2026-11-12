using UnityEngine;

// Percy (pig) — Bounce Roll: on tap he curls into a ball and dives forward-and-down into the structure, bounces off the
// first thing he hits once, and fades away.
// Was: bounciness 1.1, no friction and 1.35x speed after each of 3 bounces — he pinged faster and faster all over the
// screen (2026-11-12, user: "percy should not bounce all over the screen (this may freak out the kids) - he should
// bounce off once and disappear").
public class PercyAnimal : AnimalBase
{
    const float RollBounciness = 0.5f;   // one soft rebound, no boost
    const float FadeAfterBounce = 0.6f;  // seconds after the bounce before he fades out

    private bool _rolling;
    private PhysicsMaterial2D _rollMat;

    protected override bool KeepAbilityPose => _rolling;   // stays curled up for the whole roll

    protected override void Awake()
    {
        mass       = 8f;
        bounciness = 0.4f;
        linearDrag = 0.008f;
        base.Awake();
        if (!HasRealSprites) _sr.color = new Color(1f, 0.6f, 0.65f); // salmon pink
        _sr.sortingOrder = 6; // fixed 2026-07-08: was 5, needed a slot free for CannonSmoke at 5
        _col.radius      = 0.36f;

        _rollMat = new PhysicsMaterial2D("PercyRoll") { bounciness = RollBounciness, friction = 0.3f };
    }

    protected override void TriggerAbility()
    {
        _rolling = true;
        // Dives forward-and-down into the structure, faster than the flight (2026-11-11).
        _rb.linearVelocity = AbilityAim(30f) * AbilitySpeed(8f);
        _col.sharedMaterial = _rollMat;
        if (!HasRealSprites) _sr.color = new Color(1f, 0.85f, 0.2f); // golden "rolled up" tint
    }

    protected override void OnCollisionEnter2D(Collision2D col)
    {
        base.OnCollisionEnter2D(col);   // the hit (impact sound cue, contact timer) - the rebound itself is physics
        if (!_rolling) return;
        ShowAbilityPose();              // still curled up as he bounces off, not the impact pose
        EndSoon(FadeAfterBounce);
    }
}
