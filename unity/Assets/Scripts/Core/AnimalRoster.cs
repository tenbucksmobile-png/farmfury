namespace FarmFury.Core
{
    /// <summary>
    /// Which world first introduces each animal (GDD animal table). An animal is available in the pre-level
    /// character select once that world is unlocked (SaveManager.IsAnimalUnlocked) — FarmFury's take on the
    /// franchise's "characters unlock as you progress" rule (user decision 2026-11-02: "use select as characters
    /// become available"). WorldData.introducedAnimals holds the same mapping for the menus; this is the
    /// code-side source so unlocks still work before any WorldData asset exists.
    /// </summary>
    public static class AnimalRoster
    {
        public static WorldType IntroWorld(AnimalType animal) => animal switch
        {
            AnimalType.Cluck => WorldType.MeadowRuins,
            AnimalType.Bessie => WorldType.MeadowRuins,
            AnimalType.Percy => WorldType.FrozenTundra,
            AnimalType.Woolly => WorldType.FrozenTundra,
            AnimalType.Ducky => WorldType.WatermillVillage,
            AnimalType.Horace => WorldType.SkyIslands,
            AnimalType.Gerald => WorldType.SunkenCity,
            AnimalType.Billy => WorldType.RobotMothership,
            _ => WorldType.MeadowRuins
        };

        public static readonly AnimalType[] All =
        {
            AnimalType.Cluck, AnimalType.Bessie, AnimalType.Percy, AnimalType.Woolly,
            AnimalType.Ducky, AnimalType.Horace, AnimalType.Gerald, AnimalType.Billy
        };
    }
}
