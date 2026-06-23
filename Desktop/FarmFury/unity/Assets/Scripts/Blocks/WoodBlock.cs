using UnityEngine;

public class WoodBlock : BlockBase
{
    protected override void Awake()
    {
        baseMaxHealth = 20f;
        baseMass      = 5f;
        bounciness    = 0.2f;
        base.Awake();
        if (_sr) _sr.color = new Color(0.65f, 0.38f, 0.12f); // wood brown
    }
}