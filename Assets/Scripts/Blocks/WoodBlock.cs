public class WoodBlock : BlockBase
{
    protected override void Awake()
    {
        baseMaxHealth = 20f;
        baseMass      = 5f;
        bounciness    = 0.2f;
        base.Awake();
    }
}