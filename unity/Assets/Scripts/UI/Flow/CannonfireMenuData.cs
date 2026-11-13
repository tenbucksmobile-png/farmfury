using System.Collections.Generic;
using FarmFury.Core;
using FarmFury.Shared.UI;

namespace FarmFury.UI
{
    /// <summary>
    /// Cannonfire's <see cref="IMenuData{TWorld,TLevel}"/> for com.farmfury.core's shared World Select, Level Select
    /// and World Unlocked screens: reads DataManager (WorldData / LevelData) and SaveManager. Stateless.
    /// </summary>
    public sealed class CannonfireMenuData : IMenuData<WorldType, LevelData>
    {
        public static readonly CannonfireMenuData Instance = new();

        public WorldView World(WorldType world)
        {
            var data = DataManager.Instance != null ? DataManager.Instance.GetWorldData(world) : null;
            var save = SaveManager.Instance;
            return new WorldView
            {
                HasData = data != null,
                DisplayName = data != null && !string.IsNullOrEmpty(data.displayName) ? data.displayName : world.ToString(),
                Badge = data != null ? data.selectBadge : null,
                LevelSelectBackground = data != null ? data.levelSelectBackground : null,
                NewCharacterBackground = data != null ? data.newCharacterBackground : null,
                PurchaseRequired = data != null && data.purchaseRequired,
                Unlocked = save != null && save.IsWorldUnlocked(world),
            };
        }

        public WorldView? WorldOrNull(WorldType? world) => world.HasValue ? World(world.Value) : null;

        public IReadOnlyList<LevelData> Levels(WorldType world) =>
            DataManager.Instance != null ? DataManager.Instance.GetWorldLevels(world) : new List<LevelData>();

        public LevelView Level(LevelData level)
        {
            var save = SaveManager.Instance;
            return new LevelView
            {
                Id = level.levelId,
                Name = level.levelName,
                IsBoss = level.isBossLevel,
                Unlocked = save != null && save.IsLevelUnlocked(level),
                Completed = save != null && save.IsLevelCompleted(level.levelId),
                Stars = save != null ? save.GetLevelStars(level.levelId) : 0,
                SecretUnfound = false,   // Cannonfire levels have no character-gated secrets
            };
        }

        /// <summary>The level being played's world's own New Character backdrop (null = the shared one).</summary>
        public static UnityEngine.Sprite CurrentWorldNewCharacterBackground()
        {
            var level = GameManager.Instance != null ? GameManager.Instance.CurrentLevel : null;
            return level != null ? Instance.World(level.WorldType).NewCharacterBackground : null;
        }
    }
}
