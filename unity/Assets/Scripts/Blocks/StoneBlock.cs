using UnityEngine;

public class StoneBlock : BlockBase
{
    protected override void Awake()
    {
        baseMaxHealth = 220f;
        baseMass      = 8f;
        bounciness    = 0.1f;
        base.Awake();
        if (_sr) _sr.color = new Color(0.55f, 0.55f, 0.58f); // stone grey
    }

    protected override void PlayHitSound() =>
        AudioManager.Play(AudioManager.Sound.StoneHit, cooldown: 0.08f);
}