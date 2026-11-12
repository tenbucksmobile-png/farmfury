namespace FarmFury.Core
{
    /// <summary>
    /// When each animal joins the team. Two per world, the whole roster out by Sky Islands, each released after a set
    /// number of levels cleared IN ITS WORLD (2026-11-12, user: "released after certain amount of levels on each world -
    /// not together, but two per world; so it goes to 'unlock character' as per stampede and arcade - on the carousel
    /// they are greyed out and cannot be selected until they are released"). Stampede / Arcade use a "clear N levels"
    /// ladder; FarmFury's is per world so each world's pair arrives while you play that world.
    ///
    /// Nothing is stored: SaveManager.IsAnimalUnlocked derives it from level completion (the world is open AND at
    /// least UnlockAfter of its levels are completed), so old saves pick it up on their own. The clear that crosses a
    /// threshold shows the New Character page (SaveManager.TakeNewlyUnlockedAnimals -> GameManager.CompleteLevel).
    ///
    ///   World 1  Cluck  from the start         Bessie after 1 level  (in time for L02's "change characters" tip)
    ///   World 2  Percy  after 1 level          Woolly after 8 levels
    ///   World 3  Ducky  after 1 level          Horace after 8 levels
    ///   World 4  Gerald after 1 level          Billy  after 8 levels
    /// Worlds 5-9 introduce nobody. Retune here (UnlockAfter) - the carousel's "clear N levels" label, the Character
    /// Story text and WorldData.introducedAnimals all read this table.
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
            AnimalType.Horace => WorldType.WatermillVillage,
            AnimalType.Gerald => WorldType.SkyIslands,
            AnimalType.Billy => WorldType.SkyIslands,
            _ => WorldType.MeadowRuins
        };

        /// <summary>Levels of IntroWorld that must be completed before the animal joins (0 = with the world).</summary>
        public static int UnlockAfter(AnimalType animal) => animal switch
        {
            AnimalType.Cluck => 0,
            AnimalType.Bessie => 1,
            AnimalType.Percy => 1,
            AnimalType.Woolly => 8,
            AnimalType.Ducky => 1,
            AnimalType.Horace => 8,
            AnimalType.Gerald => 1,
            AnimalType.Billy => 8,
            _ => 0
        };

        public static readonly AnimalType[] All =
        {
            AnimalType.Cluck, AnimalType.Bessie, AnimalType.Percy, AnimalType.Woolly,
            AnimalType.Ducky, AnimalType.Horace, AnimalType.Gerald, AnimalType.Billy
        };
    }
}
