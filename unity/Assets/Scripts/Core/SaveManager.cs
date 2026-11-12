using System.Collections.Generic;
using System.Linq;
using FarmFury.Utilities;
using UnityEngine;

namespace FarmFury.Core
{
    /// <summary>
    /// Persists player progress and the franchise economy via PlayerPrefs behind a stable API — ported from
    /// FarmFury_Stampede's SaveManager (itself from Arcade) on 2026-11-02 so all Farm Fury titles share one save
    /// model: per-level stars/completion keyed by levelId, world unlocks via boss clears or purchase, a
    /// checksum-protected coin balance / Remove Ads / world + cosmetic purchases, and per-level leaderboard records.
    ///
    /// FarmFury differences from Stampede:
    ///  - Keys use the "FF_" prefix.
    ///  - Animal unlocks are derived, not stored: an animal is available once the world that introduces it is
    ///    unlocked (AnimalRoster.IntroWorld) — no rare pellets / level-count ladder in a launcher game.
    ///  - Leaderboard records are score / fastest time / animals left (no crops).
    ///  - A one-time migration copies the pre-port per-index keys (ff_stars_N / ff_score_N / ff_perfect_N, N =
    ///    GameManager's level index) onto levelIds, so existing World 1 progress survives the port.
    /// Lives on the GameManager GameObject (DontDestroyOnLoad).
    /// </summary>
    public class SaveManager : MonoSingleton<SaveManager>
    {
        private const string LevelStarsKeyPrefix = "FF_Stars_";
        private const string LevelCompletedKeyPrefix = "FF_Completed_";
        private const string LevelPerfectKeyPrefix = "FF_Perfect_";
        private const string CompletedCountKey = "FF_CompletedCount";
        private const string BossClearedKeyPrefix = "FF_BossCleared_";
        private const string LastWorldKey = "FF_LastWorld";
        private const string LastLineupKey = "FF_LastLineup";
        private const string CoinBalanceKey = "FF_Coins";
        private const string AdsRemovedKey = "FF_AdsRemoved";
        private const string LevelsSinceInterstitialKey = "FF_LevelsSinceInterstitial";
        private const string CosmeticOwnedKeyPrefix = "FF_CosmeticOwned_";
        private const string EquippedCannonKey = "FF_EquippedCannon";
        private const string EquippedImpactKey = "FF_EquippedImpact";
        private const string EquippedTrailKey = "FF_EquippedTrail";
        private const string WorldPurchasedKeyPrefix = "FF_WorldPurchased_";
        private const string BestScoreKeyPrefix = "FF_BestScore_";
        private const string BestScoreAnimalKeyPrefix = "FF_BestScoreAnimal_";
        private const string FastestTimeKeyPrefix = "FF_FastestTime_";
        private const string BestAnimalsLeftKeyPrefix = "FF_BestAnimalsLeft_";
        private const string WorldCoinsEarnedKeyPrefix = "FF_WorldCoins_";
        private const string MigratedKey = "FF_MigratedFromIndexKeys";

        /// <summary>Coins (franchise economy): earned per level, spent on revives, cooldown skips and cosmetics.</summary>
        public int CoinBalance { get; private set; }

        /// <summary>Raised whenever the coin balance changes (HUD chip, shop screens).</summary>
        public event System.Action<int> CoinsChanged;

        protected override void Awake()
        {
            base.Awake();
            CoinBalance = GetProtectedInt(CoinBalanceKey, 0);
        }

        // After every Awake: GameManager's level registry (Editor auto-load included) is ready by then.
        private void Start() => MigrateIndexKeysOnce();

        public void SaveProgress() => PlayerPrefs.Save();

        // ------------------------------------------------------------ migration (pre-2026-11-02 saves)

        private void MigrateIndexKeysOnce()
        {
            if (PlayerPrefs.GetInt(MigratedKey, 0) == 1) { return; }
            var gm = GameManager.Instance;
            if (gm == null || gm.TotalLevels == 0) { return; }   // try again next launch

            int migrated = 0;
            for (int i = 0; i < gm.TotalLevels; i++)
            {
                var level = gm.GetLevelData(i);
                if (level == null || string.IsNullOrEmpty(level.levelId)) { continue; }

                int stars = PlayerPrefs.GetInt($"ff_stars_{i}", 0);
                int score = PlayerPrefs.GetInt($"ff_score_{i}", 0);
                if (stars > 0)
                {
                    SetLevelStars(level.levelId, stars);
                    RecordLevelCompleted(level);
                    migrated++;
                }
                if (score > GetBestScore(level.levelId))
                {
                    PlayerPrefs.SetInt(BestScoreKeyPrefix + level.levelId, score);
                }
                if (PlayerPrefs.GetInt($"ff_perfect_{i}", 0) == 1)
                {
                    PlayerPrefs.SetInt(LevelPerfectKeyPrefix + level.levelId, 1);
                }
            }

            PlayerPrefs.SetInt(MigratedKey, 1);
            PlayerPrefs.Save();
            Debug.Log($"[SaveManager] Migrated {migrated} completed level(s) from the pre-port ff_stars_N keys.");
        }

        // ------------------------------------------------------------ coins & monetisation (franchise values)

        public void AddCoins(int amount)
        {
            CoinBalance += amount;
            PersistCoinBalance();
        }

        /// <summary>Spends coins if the balance covers it; false (nothing spent) otherwise.</summary>
        public bool SpendCoins(int amount)
        {
            if (CoinBalance < amount) { return false; }
            CoinBalance -= amount;
            PersistCoinBalance();
            return true;
        }

        private void PersistCoinBalance()
        {
            SetProtectedInt(CoinBalanceKey, CoinBalance);
            PlayerPrefs.Save();
            CoinsChanged?.Invoke(CoinBalance);
        }

        /// <summary>Remove Ads owned: no interstitials or banners (rewarded ads stay, they're opt-in).</summary>
        public bool AdsRemoved
        {
            get => GetProtectedBool(AdsRemovedKey, false);
            set { SetProtectedBool(AdsRemovedKey, value); PlayerPrefs.Save(); }
        }

        public int LevelsSinceLastInterstitial => PlayerPrefs.GetInt(LevelsSinceInterstitialKey, 0);

        public void SetLevelsSinceLastInterstitial(int count) => PlayerPrefs.SetInt(LevelsSinceInterstitialKey, count);

        // Shop cards on Level Complete (franchise ShopPromos pacing): plain prefs.
        public int LevelsSincePromo
        {
            get => PlayerPrefs.GetInt("FF_LevelsSincePromo", 0);
            set => PlayerPrefs.SetInt("FF_LevelsSincePromo", value);
        }

        public int InterstitialsSinceAdsCard
        {
            get => PlayerPrefs.GetInt("FF_InterstitialsSinceAdsCard", 0);
            set => PlayerPrefs.SetInt("FF_InterstitialsSinceAdsCard", value);
        }

        public int PromoTurn
        {
            get => PlayerPrefs.GetInt("FF_PromoTurn", 0);
            set => PlayerPrefs.SetInt("FF_PromoTurn", value);
        }

        public bool IsCosmeticOwned(string cosmeticId) =>
            !string.IsNullOrEmpty(cosmeticId) && GetProtectedBool(CosmeticOwnedKeyPrefix + cosmeticId, false);

        public void SetCosmeticOwned(string cosmeticId)
        {
            if (string.IsNullOrEmpty(cosmeticId)) { return; }
            SetProtectedBool(CosmeticOwnedKeyPrefix + cosmeticId, true);
            PlayerPrefs.Save();
        }

        /// <summary>The cannon skin (empty = the standard cannon).</summary>
        public string GetEquippedCannon() => PlayerPrefs.GetString(EquippedCannonKey, string.Empty);

        public void SetEquippedCannon(string cosmeticId) => PlayerPrefs.SetString(EquippedCannonKey, cosmeticId ?? string.Empty);

        /// <summary>The robot-defeat impact effect (empty = none, just the standard explosion).</summary>
        public string GetEquippedImpact() => PlayerPrefs.GetString(EquippedImpactKey, string.Empty);

        public void SetEquippedImpact(string cosmeticId) => PlayerPrefs.SetString(EquippedImpactKey, cosmeticId ?? string.Empty);

        /// <summary>The equipped id for a category (one of StoreProducts.Cannons / Trails / Impacts).</summary>
        public string GetEquipped(string[] category) =>
            category == StoreProducts.Cannons ? GetEquippedCannon()
            : category == StoreProducts.Impacts ? GetEquippedImpact() : GetEquippedTrail();

        public void SetEquipped(string[] category, string cosmeticId)
        {
            if (category == StoreProducts.Cannons) SetEquippedCannon(cosmeticId);
            else if (category == StoreProducts.Impacts) SetEquippedImpact(cosmeticId);
            else SetEquippedTrail(cosmeticId);
        }

        /// <summary>The trail is shared by every animal.</summary>
        public string GetEquippedTrail() => PlayerPrefs.GetString(EquippedTrailKey, string.Empty);

        public void SetEquippedTrail(string cosmeticId) => PlayerPrefs.SetString(EquippedTrailKey, cosmeticId ?? string.Empty);

        public bool IsWorldPurchased(WorldType world) => GetProtectedBool(WorldPurchasedKeyPrefix + world, false);

        public void SetWorldPurchased(WorldType world)
        {
            SetProtectedBool(WorldPurchasedKeyPrefix + world, true);
            PlayerPrefs.Save();
        }

        // Tamper check for purchased/earned values (coins, Remove Ads, worlds, cosmetics): each int is stored with a
        // checksum, so hand-editing the prefs file resets the value instead of granting coins or items. A value saved
        // before its checksum existed is adopted rather than wiped. Stable FNV-1a hash with an app salt (Stampede's
        // 2026-10-25 audit fix: the original device-salted hash reset bought coins after a backup restore).
        private const string ChecksumSalt = "FarmFury|save|v2";

        private static int ComputeChecksum(string key, int value) => StableHash($"{ChecksumSalt}|{key}|{value}");

        private static int StableHash(string text)
        {
            unchecked
            {
                uint hash = 2166136261;
                foreach (char c in text)
                {
                    hash ^= c;
                    hash *= 16777619;
                }
                return (int)hash;
            }
        }

        private static void SetProtectedInt(string key, int value)
        {
            PlayerPrefs.SetInt(key, value);
            PlayerPrefs.SetInt(key + "_chk", ComputeChecksum(key, value));
        }

        private static int GetProtectedInt(string key, int defaultValue)
        {
            int value = PlayerPrefs.GetInt(key, defaultValue);
            string checksumKey = key + "_chk";
            if (!PlayerPrefs.HasKey(checksumKey))
            {
                SetProtectedInt(key, value);
                return value;
            }
            if (PlayerPrefs.GetInt(checksumKey) == ComputeChecksum(key, value))
            {
                return value;
            }

            Debug.LogWarning($"[SaveManager] Integrity check failed for '{key}' - resetting to default.");
            SetProtectedInt(key, defaultValue);
            return defaultValue;
        }

        private static bool GetProtectedBool(string key, bool defaultValue) => GetProtectedInt(key, defaultValue ? 1 : 0) == 1;

        private static void SetProtectedBool(string key, bool value) => SetProtectedInt(key, value ? 1 : 0);

        // ------------------------------------------------------------ animals

        /// <summary>
        /// An animal is available once the world that introduces it is unlocked AND enough of that world's levels are
        /// completed (AnimalRoster.UnlockAfter, 2026-11-12: two per world, each released after a number of levels).
        /// Derived, nothing stored. Debug "Unlock All Levels" opens everyone.
        /// </summary>
        public bool IsAnimalUnlocked(AnimalType animal)
        {
            if (DebugAllLevelsOpen) { return true; }
            var world = AnimalRoster.IntroWorld(animal);
            if (!IsWorldUnlocked(world)) { return false; }
            int need = AnimalRoster.UnlockAfter(animal);
            return need <= 0 || LevelsCompletedIn(world) >= need;
        }

        /// <summary>How many of a world's levels have been completed at least once.</summary>
        public int LevelsCompletedIn(WorldType world) =>
            DataManager.Instance == null ? 0 : DataManager.Instance.GetWorldLevels(world).Count(l => IsLevelCompleted(l.levelId));

        /// <summary>The animals the last RecordLevelCompleted call released (cleared when read).</summary>
        private readonly List<AnimalType> _newlyUnlockedAnimals = new();

        /// <summary>The animals the latest level clear released - what the New Character page shows. Empties the list.</summary>
        public List<AnimalType> TakeNewlyUnlockedAnimals()
        {
            var list = new List<AnimalType>(_newlyUnlockedAnimals);
            _newlyUnlockedAnimals.Clear();
            return list;
        }

        public List<AnimalType> UnlockedAnimals() => AnimalRoster.All.Where(IsAnimalUnlocked).ToList();

        /// <summary>The animals a world introduces — what the New Character page shows when it unlocks.</summary>
        public static List<AnimalType> AnimalsIntroducedBy(WorldType world) =>
            AnimalRoster.All.Where(a => AnimalRoster.IntroWorld(a) == world).ToList();

        /// <summary>The lineup the player last launched with (pre-fills the character select). Empty on a fresh save.</summary>
        public List<AnimalType> LastLineup
        {
            get
            {
                var raw = PlayerPrefs.GetString(LastLineupKey, string.Empty);
                var list = new List<AnimalType>();
                foreach (var part in raw.Split(','))
                {
                    if (int.TryParse(part, out int v) && System.Enum.IsDefined(typeof(AnimalType), v)
                        && IsAnimalUnlocked((AnimalType)v))
                    {
                        list.Add((AnimalType)v);
                    }
                }
                return list;
            }
            set
            {
                PlayerPrefs.SetString(LastLineupKey, value == null ? string.Empty : string.Join(",", value.Select(a => (int)a)));
                PlayerPrefs.Save();
            }
        }

        // ------------------------------------------------------------ level progress

        public int GetLevelStars(string levelId) => PlayerPrefs.GetInt(LevelStarsKeyPrefix + levelId, 0);

        /// <summary>Records stars for the level, keeping the best result across attempts.</summary>
        public void SetLevelStars(string levelId, int stars) =>
            PlayerPrefs.SetInt(LevelStarsKeyPrefix + levelId, Mathf.Max(GetLevelStars(levelId), stars));

        public bool IsLevelPerfect(string levelId) => PlayerPrefs.GetInt(LevelPerfectKeyPrefix + levelId, 0) == 1;

        public void MarkLevelPerfect(string levelId) => PlayerPrefs.SetInt(LevelPerfectKeyPrefix + levelId, 1);

        public bool IsLevelCompleted(string levelId) => PlayerPrefs.GetInt(LevelCompletedKeyPrefix + levelId, 0) == 1;

        /// <summary>Distinct levels completed at least once.</summary>
        public int CompletedLevelCount => PlayerPrefs.GetInt(CompletedCountKey, 0);

        /// <summary>
        /// Records a completion (only the first counts). Completing a boss level clears its world; returns the
        /// world that this newly unlocked (the next story world), or null.
        /// </summary>
        public WorldType? RecordLevelCompleted(LevelData level)
        {
            if (level == null) { return null; }
            var lockedBefore = AnimalRoster.All.Where(a => !IsAnimalUnlocked(a)).ToList();
            _newlyUnlockedAnimals.Clear();
            if (!IsLevelCompleted(level.levelId))
            {
                PlayerPrefs.SetInt(LevelCompletedKeyPrefix + level.levelId, 1);
                PlayerPrefs.SetInt(CompletedCountKey, CompletedLevelCount + 1);
            }

            WorldType? opened = null;
            if (level.isBossLevel && !IsWorldBossCleared(level.WorldType))
            {
                var next = level.WorldType + 1;
                bool nextIsStory = next <= WorldType.RobotMothership;
                bool wasLocked = nextIsStory && !IsWorldUnlocked(next);
                SetWorldBossCleared(level.WorldType, true);
                if (wasLocked && IsWorldUnlocked(next)) { opened = next; }
            }
            _newlyUnlockedAnimals.AddRange(lockedBefore.Where(IsAnimalUnlocked));
            return opened;
        }

        public int LastPlayedWorld
        {
            get => PlayerPrefs.GetInt(LastWorldKey, 0);
            set { PlayerPrefs.SetInt(LastWorldKey, value); PlayerPrefs.Save(); }
        }

        // ------------------------------------------------------------ worlds & level locks

        public bool IsWorldBossCleared(WorldType world) => PlayerPrefs.GetInt(BossClearedKeyPrefix + world, 0) == 1;

        public void SetWorldBossCleared(WorldType world, bool cleared) =>
            PlayerPrefs.SetInt(BossClearedKeyPrefix + world, cleared ? 1 : 0);

        /// <summary>
        /// A story world unlocks once the previous world's boss level has been completed (Meadow Ruins is always
        /// open). A paid world (WorldData.purchaseRequired) unlocks when bought.
        /// </summary>
        public bool IsWorldUnlocked(WorldType world)
        {
            if (DebugAllLevelsOpen) { return true; }
            if (IsPaidWorld(world)) { return IsWorldPurchased(world); }
            return world == WorldType.MeadowRuins || IsWorldBossCleared(world - 1);
        }

        /// <summary>Paid post-finale world. Falls back to the enum order if WorldData isn't loaded.</summary>
        public static bool IsPaidWorld(WorldType world)
        {
            var data = DataManager.Instance != null ? DataManager.Instance.GetWorldData(world) : null;
            return data != null ? data.purchaseRequired : world >= WorldType.DustbowlCanyon;
        }

        /// <summary>
        /// First level of a world is open; each later level opens once the previous one is completed; the boss
        /// opens once every regular level is completed.
        /// </summary>
        public bool IsLevelUnlocked(LevelData level)
        {
            if (level == null || DataManager.Instance == null || !IsWorldUnlocked(level.WorldType)) { return false; }
            if (DebugAllLevelsOpen) { return true; }

            var levels = DataManager.Instance.GetWorldLevels(level.WorldType);
            int index = levels.IndexOf(level);
            if (index <= 0) { return index == 0; }
            if (level.isBossLevel)
            {
                return levels.Where(l => !l.isBossLevel).All(l => IsLevelCompleted(l.levelId));
            }
            return IsLevelCompleted(levels[index - 1].levelId);
        }

        public int GetWorldStars(WorldType world) =>
            DataManager.Instance == null ? 0 : DataManager.Instance.GetWorldLevels(world).Sum(l => GetLevelStars(l.levelId));

        // ------------------------------------------------------------ leaderboard records

        /// <summary>Keeps each record's best: highest score (and the animal that set it), fastest time, most animals left.</summary>
        public void RecordLevelResult(string levelId, int score, float seconds, int animalsLeft, AnimalType animal)
        {
            if (score > GetBestScore(levelId) || !PlayerPrefs.HasKey(BestScoreAnimalKeyPrefix + levelId))
            {
                PlayerPrefs.SetInt(BestScoreKeyPrefix + levelId, Mathf.Max(score, GetBestScore(levelId)));
                PlayerPrefs.SetInt(BestScoreAnimalKeyPrefix + levelId, (int)animal);
            }

            float fastest = GetFastestTime(levelId);
            if (seconds > 0f && (fastest <= 0f || seconds < fastest))
            {
                PlayerPrefs.SetFloat(FastestTimeKeyPrefix + levelId, seconds);
            }

            PlayerPrefs.SetInt(BestAnimalsLeftKeyPrefix + levelId, Mathf.Max(GetBestAnimalsLeft(levelId), animalsLeft));
        }

        public int GetBestScore(string levelId) => PlayerPrefs.GetInt(BestScoreKeyPrefix + levelId, 0);

        public AnimalType? GetBestScoreAnimal(string levelId)
        {
            if (!PlayerPrefs.HasKey(BestScoreAnimalKeyPrefix + levelId)) { return null; }
            var type = (AnimalType)PlayerPrefs.GetInt(BestScoreAnimalKeyPrefix + levelId);
            return System.Enum.IsDefined(typeof(AnimalType), type) ? type : null;
        }

        /// <summary>Fastest completion in seconds (paused time excluded); 0 = none yet.</summary>
        public float GetFastestTime(string levelId) => PlayerPrefs.GetFloat(FastestTimeKeyPrefix + levelId, 0f);

        public int GetBestAnimalsLeft(string levelId) => PlayerPrefs.GetInt(BestAnimalsLeftKeyPrefix + levelId, 0);

        public int GetWorldCoinsEarned(WorldType world) => PlayerPrefs.GetInt(WorldCoinsEarnedKeyPrefix + world, 0);

        public void AddWorldCoinsEarned(WorldType world, int amount)
        {
            if (amount > 0)
            {
                PlayerPrefs.SetInt(WorldCoinsEarnedKeyPrefix + world, GetWorldCoinsEarned(world) + amount);
            }
        }

        // ------------------------------------------------------------ debug (testing only)

        public const string DebugAllLevelsOpenKey = "FF_DebugAllLevelsOpen";

        /// <summary>DEBUG (F1 / Editor menu): every world and level opens without marking anything completed. Always false in release.</summary>
        public bool DebugAllLevelsOpen
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            get => PlayerPrefs.GetInt(DebugAllLevelsOpenKey, 0) == 1;
            set { PlayerPrefs.SetInt(DebugAllLevelsOpenKey, value ? 1 : 0); PlayerPrefs.Save(); }
#else
            get => false;
            set { }
#endif
        }

        public void DebugAddCoins(int amount) => AddCoins(amount);

        public void DebugSetWorldPurchased(WorldType world, bool bought)
        {
            if (bought) { SetWorldPurchased(world); return; }
            PlayerPrefs.DeleteKey(WorldPurchasedKeyPrefix + world);
            PlayerPrefs.DeleteKey(WorldPurchasedKeyPrefix + world + "_chk");
            PlayerPrefs.Save();
        }

        /// <summary>DEBUG: marks every regular (non-boss) level of the world completed with one star.</summary>
        public void DebugCompleteWorldLevels(WorldType world)
        {
            if (DataManager.Instance == null) { return; }
            foreach (var level in DataManager.Instance.GetWorldLevels(world).Where(l => !l.isBossLevel))
            {
                SetLevelStars(level.levelId, 1);
                RecordLevelCompleted(level);
            }
            PlayerPrefs.Save();
        }

        /// <summary>DEBUG: wipes progress back to a fresh save. Purchases and coins are kept.</summary>
        public void DebugResetProgress()
        {
            var gm = GameManager.Instance;
            var levels = new List<LevelData>();
            for (int i = 0; gm != null && i < gm.TotalLevels; i++) { levels.Add(gm.GetLevelData(i)); }
            ClearProgressKeys(levels);
            Debug.Log("[SaveManager] DEBUG: progress reset to a fresh save.");
        }

        /// <summary>
        /// Deletes every progress key (stars, completions, records, boss clears, lineup) for these levels, plus the
        /// pre-port ff_*_N index keys so the one-time migration can't bring them back. Static so the Editor's
        /// FarmFury > Reset Progress menu can call it outside Play mode. Purchases and coins are kept.
        /// </summary>
        public static void ClearProgressKeys(IList<LevelData> levels)
        {
            for (int i = 0; i < levels.Count; i++)
            {
                PlayerPrefs.DeleteKey($"ff_stars_{i}");
                PlayerPrefs.DeleteKey($"ff_score_{i}");
                PlayerPrefs.DeleteKey($"ff_perfect_{i}");
                var level = levels[i];
                if (level == null || string.IsNullOrEmpty(level.levelId)) { continue; }
                foreach (var prefix in new[] { LevelCompletedKeyPrefix, LevelStarsKeyPrefix, LevelPerfectKeyPrefix,
                             BestScoreKeyPrefix, BestScoreAnimalKeyPrefix, FastestTimeKeyPrefix, BestAnimalsLeftKeyPrefix })
                {
                    PlayerPrefs.DeleteKey(prefix + level.levelId);
                }
            }
            foreach (WorldType world in System.Enum.GetValues(typeof(WorldType)))
            {
                PlayerPrefs.DeleteKey(BossClearedKeyPrefix + world);
                PlayerPrefs.DeleteKey(WorldCoinsEarnedKeyPrefix + world);
            }
            PlayerPrefs.DeleteKey(CompletedCountKey);
            PlayerPrefs.DeleteKey(LastLineupKey);
            PlayerPrefs.DeleteKey(LastWorldKey);
            PlayerPrefs.SetInt(MigratedKey, 1);
            PlayerPrefs.Save();
        }
    }
}
