/// <summary>
/// FarmFury's worlds, matching the franchise (Stampede/Arcade) order: the six free story worlds, then the three
/// paid post-finale worlds (WorldData.purchaseRequired, $3.99 each, unlocked by purchase alone). Serialized as ints,
/// so only ever append. LevelData.world is the older 1-based int (1 = MeadowRuins); LevelData.WorldType maps it.
/// </summary>
public enum WorldType
{
    MeadowRuins,
    FrozenTundra,
    WatermillVillage,
    SkyIslands,
    SunkenCity,
    RobotMothership,
    DustbowlCanyon,
    HarvestFairground,
    CropFactory
}
