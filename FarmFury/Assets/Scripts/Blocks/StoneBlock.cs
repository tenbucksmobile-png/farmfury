public class StoneBlock : BlockBase
{
    protected override void Awake()
    {
        baseMaxHealth = 50f;
        baseMass      = 8f;
        bounciness    = 0.1f;
        base.Awake();
    }
}