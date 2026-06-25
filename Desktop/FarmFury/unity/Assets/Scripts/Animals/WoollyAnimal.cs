using UnityEngine;

// Woolly (sheep) — Triple Clone: on tap, spawns 2 clones at ±15° from current
// trajectory. Clones are silent (don't fire OnAnimalDestroyed).
public class WoollyAnimal : AnimalBase
{
    [Header("Triple Clone")]
    [SerializeField] private float _spreadDeg    = 15f;
    [SerializeField] private float _cloneSpeedMul = 1f;

    private bool _isClone;

    // Called immediately after Instantiate to mark a spawned copy as a clone.
    public void SetAsClone()
    {
        _isClone     = true;
        _abilityUsed = true; // no double-split
    }

    protected override void Awake()
    {
        mass       = 6f;
        bounciness = 0.3f;
        linearDrag = 0.01f;
        base.Awake();
        _sr.color        = Color.white;
        _sr.sortingOrder = 4;
        _col.radius      = 0.36f;
    }

    protected override void TriggerAbility()
    {
        Vector2 vel   = _rb.linearVelocity;
        float   angle = Mathf.Atan2(vel.y, vel.x) * Mathf.Rad2Deg;
        float   speed = vel.magnitude * _cloneSpeedMul;

        for (int i = -1; i <= 1; i += 2)
        {
            float   rad      = (angle + i * _spreadDeg) * Mathf.Deg2Rad;
            Vector2 cloneVel = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * speed;

            var clone = Instantiate(this, transform.position, Quaternion.identity);
            clone.SetAsClone();
            clone.Launch(cloneVel);
        }
    }

    // Clones self-destruct silently so they don't trigger CatapultLauncher's
    // "load next bird" logic.
    protected override void DestroyAnimal()
    {
        if (IsDestroyed) return;
        IsDestroyed = true;
        if (!_isClone) OnAnimalDestroyed?.Invoke(this);
        Destroy(gameObject);
    }
}
