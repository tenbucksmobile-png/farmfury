namespace FarmFury.Core
{
    /// <summary>
    /// The website demo (branch web-demo, never merged into the mobile game): Meadow Ruins only - its 17 levels and the
    /// Commander (L18) - free at farmfurygames.com/play/cannonfire/. The other eight worlds show on World Select and open
    /// the Full Game panel (UI/Flow/FullGameScreen) instead; no ads, purchases, shop, menu hub, Locker, leaderboards,
    /// cross-promo or analytics. Animals follow the real game's Meadow Ruins rules (Cluck, then Bessie after one level);
    /// the other six say FULL GAME on Character Select. See WEB_DEMO_PLAN.md at the repo root.
    /// </summary>
    public static class WebDemo
    {
        /// <summary>The one world the demo plays.</summary>
        public const WorldType World = WorldType.MeadowRuins;

        public static bool IsPlayable(WorldType world) => world == World;

        /// <summary>The full game's store pages (live on the App Store since 2026-11-18; Google Play still to come).</summary>
        public const string AppStoreUrl = "https://apps.apple.com/app/id6834522614";
    }
}
