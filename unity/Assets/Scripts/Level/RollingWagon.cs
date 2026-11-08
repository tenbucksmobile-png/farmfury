using UnityEngine;

/// <summary>
/// A hay wagon parked on a mount (LevelData.PlatformSpawnData.rolling, 2026-11-08): it stays Static until an animal,
/// an ability projectile or falling debris hits it, then becomes a heavy Dynamic body and rolls — typically off a
/// ramp's plateau, down the slope and into the bigger structure below, carrying whatever was built on its bed.
/// Blocks and robots take its impact through their own collision damage (it is heavy, so it smashes).
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class RollingWagon : MonoBehaviour
{
    const float Mass = 20f;
    const float ReleaseImpulse = 3f;     // momentum of a hit that sets it rolling
    const float NudgeImpulse = 6f;       // extra push toward the slope (+x) so a glancing hit still tips it over the edge

    Rigidbody2D _rb;
    bool _released;

    void Awake() => _rb = GetComponent<Rigidbody2D>();

    void OnCollisionEnter2D(Collision2D c)
    {
        if (_released || c.rigidbody == null) return;
        bool animal = c.rigidbody.GetComponent<AnimalBase>() != null || c.rigidbody.GetComponent<AbilityProjectile>() != null;
        if (animal || c.relativeVelocity.magnitude * c.rigidbody.mass >= ReleaseImpulse) Release();
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
        _rb.AddForce(Vector2.right * NudgeImpulse, ForceMode2D.Impulse);
    }
}
