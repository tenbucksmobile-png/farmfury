using System.Collections.Generic;
using System.Linq;
using FarmFury.Shared.Utilities;
using UnityEngine;

namespace FarmFury.Core
{
    /// <summary>
    /// Read-only lookup over the game's data assets (franchise convention, ported from Stampede's DataManager):
    /// worlds by WorldType, levels by levelId and per world. Levels come from GameManager's existing registry
    /// (still the one place levels are wired/auto-loaded); worlds are wired here by Wire Scene References, with an
    /// Editor-only auto-load from Assets/ScriptableObjects/Worlds as a fallback (same pattern as GameManager's).
    /// Lives on the GameManager GameObject (DontDestroyOnLoad).
    /// </summary>
    public class DataManager : MonoSingleton<DataManager>
    {
        [SerializeField] private List<WorldData> _worlds = new();

        private Dictionary<WorldType, WorldData> _worldMap;
        private Dictionary<string, LevelData> _levelMap;

        protected override void Awake()
        {
            base.Awake();
#if UNITY_EDITOR
            if (_worlds.Count == 0 || _worlds.Any(w => w == null))
            {
                _worlds = UnityEditor.AssetDatabase.FindAssets("t:WorldData", new[] { "Assets/ScriptableObjects/Worlds" })
                    .Select(g => UnityEditor.AssetDatabase.LoadAssetAtPath<WorldData>(UnityEditor.AssetDatabase.GUIDToAssetPath(g)))
                    .Where(w => w != null)
                    .ToList();
                if (_worlds.Count == 0)
                {
                    Debug.LogWarning("[DataManager] No WorldData found — run FarmFury → Generate World Data.");
                }
            }
#endif
            _worldMap = new Dictionary<WorldType, WorldData>();
            foreach (var w in _worlds)
            {
                if (w != null && !_worldMap.TryAdd(w.worldType, w))
                {
                    Debug.LogError($"[DataManager] Duplicate world '{w.worldType}' ({w.name}); keeping {_worldMap[w.worldType].name}.");
                }
            }
        }

        // Built lazily: GameManager's own Editor auto-load runs in its Awake, which may be after ours.
        private Dictionary<string, LevelData> Levels
        {
            get
            {
                if (_levelMap != null) { return _levelMap; }
                _levelMap = new Dictionary<string, LevelData>();
                var gm = GameManager.Instance;
                if (gm == null) { return _levelMap; }
                for (int i = 0; i < gm.TotalLevels; i++)
                {
                    var level = gm.GetLevelData(i);
                    if (level == null || string.IsNullOrEmpty(level.levelId)) { continue; }
                    if (!_levelMap.TryAdd(level.levelId, level))
                    {
                        Debug.LogError($"[DataManager] Duplicate levelId '{level.levelId}' ({level.name}).");
                    }
                }
                return _levelMap;
            }
        }

        public WorldData GetWorldData(WorldType type) => _worldMap.TryGetValue(type, out var data) ? data : null;

        /// <summary>Every world, in WorldType order (story worlds first, then the paid ones).</summary>
        public List<WorldData> GetAllWorlds() => _worldMap.Values.OrderBy(w => (int)w.worldType).ToList();

        public LevelData GetLevelData(string levelId) =>
            !string.IsNullOrEmpty(levelId) && Levels.TryGetValue(levelId, out var data) ? data : null;

        /// <summary>The world's levels in play order (levelId order, boss last).</summary>
        public List<LevelData> GetWorldLevels(WorldType world) =>
            Levels.Values
                .Where(l => l.WorldType == world)
                .OrderBy(l => l.isBossLevel ? 1 : 0)
                .ThenBy(l => l.levelId, System.StringComparer.Ordinal)
                .ToList();

        public List<LevelData> GetAllLevels() =>
            Levels.Values.OrderBy(l => l.levelId, System.StringComparer.Ordinal).ToList();

        /// <summary>GameManager's index for a level (what ForceStartLevel takes), or -1.</summary>
        public int GetGlobalIndex(LevelData level)
        {
            var gm = GameManager.Instance;
            if (gm == null || level == null) { return -1; }
            for (int i = 0; i < gm.TotalLevels; i++)
            {
                if (gm.GetLevelData(i) == level) { return i; }
            }
            return -1;
        }
    }
}
