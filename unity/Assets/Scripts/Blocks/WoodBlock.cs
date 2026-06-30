using UnityEngine;

public class WoodBlock : BlockBase
{
    // When true, a launched animal passes through this block at 70% of its incoming velocity.
    // Set by LevelLoader after spawn when BlockSpawnData.passThrough is true.
    [SerializeField] public bool _passThrough;

    protected override void Awake()
    {
        baseMaxHealth = 80f;
        baseMass      = 5f;
        bounciness    = 0.2f;
        base.Awake();
        if (_sr) _sr.color = new Color(0.65f, 0.38f, 0.12f); // wood brown
    }

    protected override void PlayHitSound() =>
        AudioManager.Play(AudioManager.Sound.WoodHit, cooldown: 0.08f);
}