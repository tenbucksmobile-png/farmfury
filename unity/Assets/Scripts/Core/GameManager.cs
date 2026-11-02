using System;
using UnityEngine;

[DefaultExecutionOrder(-100)]
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Level Registry")]
    [SerializeField] private LevelData[] _levels;

    public GameState State { get; private set; } = GameState.MainMenu;
    public int CurrentLevelIndex { get; private set; }
    public int TotalLevels => _levels != null ? _levels.Length : 0;

    /// <summary>The level being played (or last played), or null.</summary>
    public LevelData CurrentLevel => GetLevelData(CurrentLevelIndex);

    // The animals the player picked in Character Select for this attempt (franchise flow, 2026-11-02 — replaces
    // the old one-shot BirdQueueOverride). LevelData.birds stays the level's authored default; LevelLoader uses
    // this instead when it's set. Kept across RestartLevel so a retry uses the same lineup; cleared when a level
    // is started directly by index (ForceStartLevel from Editor tools).
    public AnimalType[] CurrentLineup { get; private set; }

    /// <summary>This attempt's outcome and per-attempt limits (reset on every attempt, filled on completion).</summary>
    public FarmFury.Core.LevelRunState RunState { get; } = new();

    public event Action<GameState> OnStateChanged;
    public event Action<LevelData> OnLevelStarted;

    // ── Franchise economy (Arcade's numbers, Stampede's replay rule) ───────────────────────────────────────────
    // 10 coins + 5 per star on a level's first clear; a replay pays 3 + 5 for each star it adds to the level's best,
    // so replaying can't farm coins without end. A revive (+1 animal when the queue runs out) costs 5 coins once per
    // attempt, or a rewarded ad any number of times, or is free once per attempt for Remove Ads owners.
    public const int BaseCoinsPerLevel = 10;
    public const int CoinsPerStar = 5;
    public const int ReplayCoins = 3;
    public const int ReviveCoinsCost = 5;
    public const int ReviveAnimals = 1;

    public static int LevelPayout(bool firstClear, int previousBestStars, int stars) =>
        firstClear ? BaseCoinsPerLevel + stars * CoinsPerStar
                   : ReplayCoins + Mathf.Max(0, stars - previousBestStars) * CoinsPerStar;

    /// <summary>Raised when the queue ran out with robots left; GameFlow shows the revive prompt.</summary>
    public event Action ReviveOffered;
    public bool ReviveDecisionPending { get; private set; }
    float _levelStartTime;
    float Elapsed => Time.time - _levelStartTime;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Franchise save/data layer (2026-11-02). Wire Scene References adds both to this GameObject; this is
        // the same null-safety fallback CatapultLauncher uses for AudioManager, for a scene not yet re-wired.
        if (GetComponent<FarmFury.Core.DataManager>() == null) gameObject.AddComponent<FarmFury.Core.DataManager>();
        if (GetComponent<FarmFury.Core.SaveManager>() == null) gameObject.AddComponent<FarmFury.Core.SaveManager>();
        // Franchise monetisation (Phase 5): store, ads, analytics. AdManager stays inert until FarmFury's own
        // LevelPlay keys are entered on it (Inspector); IAP uses the fake store in the Editor.
        if (GetComponent<FarmFury.Core.IAPManager>() == null) gameObject.AddComponent<FarmFury.Core.IAPManager>();
        if (GetComponent<FarmFury.Core.AdManager>() == null) gameObject.AddComponent<FarmFury.Core.AdManager>();
        if (GetComponent<FarmFury.Core.AnalyticsManager>() == null) gameObject.AddComponent<FarmFury.Core.AnalyticsManager>();

        // Eggs spawn AT the firing Cluck's own position (see CluckAnimal.SpawnEggs()), so with
        // every layer pair colliding by default (Physics2D's project-wide matrix), each egg
        // immediately overlapped and collided with the very bird that just fired it — Cluck's
        // own OnCollisionEnter2D treated that as a real impact and destroyed it a moment later
        // (2026-07-10, user-reported: "when the player taps on screen, cluck disappears like
        // he's hit something"). Eggs are a weapon, not a hazard to the birds themselves — ignore
        // Animal<->Egg collisions globally for the whole session.
        Physics2D.IgnoreLayerCollision(LayerMask.NameToLayer("Animal"), LayerMask.NameToLayer("Egg"), true);

        // All 5 eggs from one Cluster Bomb burst spawn at the EXACT SAME position simultaneously
        // (CluckAnimal.SpawnEggs()) and share the same Egg layer — the very next physics step
        // would otherwise resolve them as 5 mutually-overlapping circle colliders, and
        // EggProjectile.OnCollisionEnter2D destroys itself unconditionally on ANY collision,
        // including egg-vs-egg. That could wipe out the whole burst before it ever visibly
        // separates (2026-07-10, user still reporting "no eggs at all" after the Animal<->Egg
        // fix above and the sorting-order fix — this is the remaining candidate). Eggs shouldn't
        // interact with each other at all, so ignore Egg<->Egg collisions too.
        Physics2D.IgnoreLayerCollision(LayerMask.NameToLayer("Egg"), LayerMask.NameToLayer("Egg"), true);
#if UNITY_EDITOR
        TryAutoLoadLevels();
#endif
    }

#if UNITY_EDITOR
    void TryAutoLoadLevels()
    {
        if (_levels != null && _levels.Length > 0 && !System.Array.Exists(_levels, l => l == null))
            return;
        var guids = UnityEditor.AssetDatabase.FindAssets("t:LevelData",
            new[] { "Assets/ScriptableObjects/Levels" });
        var list = new System.Collections.Generic.List<LevelData>();
        foreach (var g in guids)
        {
            var data = UnityEditor.AssetDatabase.LoadAssetAtPath<LevelData>(
                UnityEditor.AssetDatabase.GUIDToAssetPath(g));
            if (data != null) list.Add(data);
        }
        list.Sort((a, b) => string.Compare(a.name, b.name, System.StringComparison.Ordinal));
        _levels = list.ToArray();
        if (_levels.Length == 0)
            Debug.LogWarning("[GameManager] No LevelData found — run FarmFury → Generate All Level Data.");
        else
            Debug.Log($"[GameManager] Auto-loaded {_levels.Length} level(s) from ScriptableObjects/Levels.");

        if (_levels.Length == 0)
            _levels = new[] { BuildFallbackLevel() };
    }
#endif

    // Procedural fallback — runs in editor AND in builds when no assets are wired.
    static LevelData BuildFallbackLevel()
    {
        Debug.Log("[GameManager] Using built-in fallback level (no LevelData assets found).");
        var d = ScriptableObject.CreateInstance<LevelData>();
        d.levelName = "First Contact";
        d.parBirds  = 2;
        d.birds     = new[] { AnimalType.Cluck, AnimalType.Cluck, AnimalType.Cluck };
        d.blocks    = new[]
        {
            new LevelData.BlockSpawnData { type = BlockType.Wood,  position = new Vector2(16.0f, 0.2f), size = new Vector2(1.0f, 0.4f) },
            new LevelData.BlockSpawnData { type = BlockType.Wood,  position = new Vector2(16.0f, 0.6f), size = new Vector2(1.0f, 0.4f) },
            new LevelData.BlockSpawnData { type = BlockType.Wood,  position = new Vector2(16.0f, 1.0f), size = new Vector2(1.0f, 0.4f) },
            new LevelData.BlockSpawnData { type = BlockType.Stone, position = new Vector2(17.6f, 0.2f), size = new Vector2(0.8f, 0.4f) },
            new LevelData.BlockSpawnData { type = BlockType.Stone, position = new Vector2(17.6f, 0.6f), size = new Vector2(0.8f, 0.4f) },
        };
        d.robots    = new[]
        {
            new LevelData.RobotSpawnData { position = new Vector2(16.5f, 1.6f) },
            new LevelData.RobotSpawnData { position = new Vector2(17.6f, 1.2f) },
        };
        return d;
    }

    public LevelData GetLevelData(int index) =>
        (index >= 0 && index < TotalLevels) ? _levels[index] : null;

    /// <summary>
    /// Starts a level with the lineup picked in Character Select (franchise flow). Single-scene: no scene load,
    /// the level is swapped in place by LevelLoader via OnLevelStarted.
    /// </summary>
    public void StartLevel(LevelData level, AnimalType[] lineup)
    {
        int index = Array.IndexOf(_levels, level);
        if (index < 0)
        {
            Debug.LogError($"[GameManager] StartLevel: '{(level != null ? level.levelId : "null")}' is not in the level registry.");
            return;
        }
        CurrentLineup = lineup != null && lineup.Length > 0 ? lineup : null;
        BeginLevel(index);
    }

    // Retry/Try Again: same level, same lineup, no scene reload.
    public void RestartLevel() => BeginLevel(CurrentLevelIndex);

    /// <summary>Starts a level by registry index with its authored bird queue (Editor/debug tools).</summary>
    public void ForceStartLevel(int index)
    {
        CurrentLineup = null;
        BeginLevel(index);
    }

    void BeginLevel(int index)
    {
        if (_levels == null || _levels.Length == 0)
        {
            Debug.LogError("[GameManager] No levels wired. Run FarmFury → Wire Scene References.");
            return;
        }
        CurrentLevelIndex = Mathf.Clamp(index, 0, _levels.Length - 1);
        var data = _levels[CurrentLevelIndex];
        if (data == null)
        {
            Debug.LogError($"[GameManager] Level {CurrentLevelIndex} asset is null. Run FarmFury → Generate All Level Data, then Wire Scene References.");
            return;
        }
        Time.timeScale = 1f;
        RunState.Reset();
        ReviveDecisionPending = false;
        _levelStartTime = Time.time;
        if (FarmFury.Core.SaveManager.Instance != null)
            FarmFury.Core.SaveManager.Instance.LastPlayedWorld = (int)data.WorldType;
        TransitionTo(GameState.Playing);
        OnLevelStarted?.Invoke(data);
        FarmFury.Core.AnalyticsManager.Instance?.LogLevelStart(CurrentLevelIndex + 1, data.levelId);
        ShowInterstitialIfDue();
    }

    // Arcade's between-levels interstitial: every Nth level load (AdManager decides; never with Remove Ads). The game
    // is frozen (time and audio) until the ad closes or times out.
    void ShowInterstitialIfDue()
    {
        var ads = FarmFury.Core.AdManager.Instance;
        if (ads == null) return;
        Time.timeScale = 0f;
        AudioListener.pause = true;
        ads.NotifyLevelLoaded(() =>
        {
            AudioListener.pause = false;
            if (State == GameState.Playing && !ReviveDecisionPending) Time.timeScale = 1f;
        });
    }

    // LevelLoader calls ScoreManager.FinaliseLevel() (which saves the result) right before this, so the
    // finalised numbers are copied into RunState for the results screen / New Character page.
    public void CompleteLevel()
    {
        if (State != GameState.Playing) return;
        var score = ScoreManager.Instance;
        var level = CurrentLevel;
        if (score != null)
        {
            RunState.starsEarned = score.Stars;
            RunState.score = score.Score;
            RunState.isNewBest = score.IsNewBest;
            RunState.isPerfect = score.IsPerfect;
            RunState.firstClear = score.WasFirstClear;
            RunState.previousBestStars = score.PreviousBestStars;
            RunState.worldUnlocked = score.WorldUnlocked;
        }
        RunState.bossCleared = level != null && level.isBossLevel;
        RunState.newlyUnlockedAnimals.Clear();
        if (RunState.worldUnlocked.HasValue)
            RunState.newlyUnlockedAnimals.AddRange(FarmFury.Core.SaveManager.AnimalsIntroducedBy(RunState.worldUnlocked.Value));

        // Coins for the clear (franchise payout). firstClear/previousBestStars were read before the result was saved.
        var save = FarmFury.Core.SaveManager.Instance;
        RunState.coinsEarned = 0;
        RunState.doubleCoinsClaimed = false;
        if (save != null && level != null && RunState.starsEarned > 0)
        {
            RunState.coinsEarned = LevelPayout(RunState.firstClear, RunState.previousBestStars, RunState.starsEarned);
            save.AddCoins(RunState.coinsEarned);
            save.AddWorldCoinsEarned(level.WorldType, RunState.coinsEarned);
            save.SaveProgress();
        }
        FarmFury.Core.AnalyticsManager.Instance?.LogLevelComplete(CurrentLevelIndex + 1, RunState.starsEarned, RunState.score, Elapsed);
        TransitionTo(GameState.LevelComplete);
    }

    /// <summary>Level Complete's Double Coins (rewarded ad): pays this clear's coins a second time.</summary>
    public bool ClaimDoubleCoinsViaAd()
    {
        var save = FarmFury.Core.SaveManager.Instance;
        if (RunState.doubleCoinsClaimed || RunState.coinsEarned <= 0 || save == null) return false;
        save.AddCoins(RunState.coinsEarned);
        if (CurrentLevel != null) save.AddWorldCoinsEarned(CurrentLevel.WorldType, RunState.coinsEarned);
        save.SaveProgress();
        RunState.doubleCoinsClaimed = true;
        return true;
    }

    // ── Revive: +1 animal when the queue runs out (franchise continue-after-death) ──────────────────────────────

    /// <summary>LevelLoader, when the last animal has flown and robots remain: offer a revive, or fail.</summary>
    public void RequestRevive()
    {
        if (State != GameState.Playing) return;
        if (ReviveOffered == null) { FailLevel(); return; }
        ReviveDecisionPending = true;
        Time.timeScale = 0f;
        ReviveOffered.Invoke();
    }

    public void DeclineRevive()
    {
        if (!ReviveDecisionPending) return;
        ReviveDecisionPending = false;
        Time.timeScale = 1f;
        FailLevel();
    }

    public bool AcceptRevive()
    {
        var save = FarmFury.Core.SaveManager.Instance;
        if (!ReviveDecisionPending || RunState.coinReviveUsed || save == null || !save.SpendCoins(ReviveCoinsCost)) return false;
        RunState.coinReviveUsed = true;
        GrantRevive();
        return true;
    }

    public bool AcceptFreeRevive()
    {
        var save = FarmFury.Core.SaveManager.Instance;
        if (!ReviveDecisionPending || RunState.freeReviveUsed || save == null || !save.AdsRemoved) return false;
        RunState.freeReviveUsed = true;
        GrantRevive();
        return true;
    }

    public bool AcceptReviveViaAd()
    {
        if (!ReviveDecisionPending) return false;
        GrantRevive();
        return true;
    }

    // One more of the animal that fired last goes into the cannon.
    void GrantRevive()
    {
        ReviveDecisionPending = false;
        Time.timeScale = 1f;
        var loader = FindAnyObjectByType<LevelLoader>();
        for (int i = 0; i < ReviveAnimals; i++)
        {
            if (loader != null) loader.AddBird(CatapultLauncher.LastAnimalUsed);
            if (ScoreManager.Instance != null) ScoreManager.Instance.AddBird();
        }
        var launcher = FindAnyObjectByType<CatapultLauncher>();
        if (launcher != null) launcher.ReloadAfterRevive();
        Debug.Log($"[GameManager] Revived: +{ReviveAnimals} {CatapultLauncher.LastAnimalUsed}.");
    }

    public void FailLevel()
    {
        if (State != GameState.Playing) return;
        ReviveDecisionPending = false;
        FarmFury.Core.AnalyticsManager.Instance?.LogLevelFailed(CurrentLevelIndex + 1, Elapsed);
        TransitionTo(GameState.LevelFailed);
    }

    /// <summary>Pauses a level in progress (timeScale 0) without ending the attempt.</summary>
    public void PauseGame()
    {
        if (State != GameState.Playing || ReviveDecisionPending) return;
        Time.timeScale = 0f;
        TransitionTo(GameState.Paused);
    }

    public void ResumeGame()
    {
        if (State != GameState.Paused) return;
        Time.timeScale = 1f;
        TransitionTo(GameState.Playing);
    }

    /// <summary>Moves to a menu/selection state (GameFlow's navigation). Always un-freezes time.</summary>
    public void SetState(GameState state)
    {
        Time.timeScale = 1f;
        TransitionTo(state);
    }

    // Leaving the app (home, a call, the app switcher) flushes the save and pauses a level in progress — iOS and
    // Android may kill a backgrounded app without OnApplicationQuit (franchise lifecycle rule, Stampede audit).
    void OnApplicationPause(bool paused)
    {
        if (!paused) return;
        PlayerPrefs.Save();
        if (State == GameState.Playing && Time.timeScale > 0f) PauseGame();
    }

    void TransitionTo(GameState next)
    {
        if (State == next) return;
        State = next;
        OnStateChanged?.Invoke(next);
    }
}

/// <summary>App/game flow states (franchise set, matching Stampede/Arcade). GameFlow shows the screen for each.</summary>
public enum GameState
{
    MainMenu,
    WorldSelect,
    LevelSelect,
    CharacterSelect,
    Playing,
    Paused,
    LevelComplete,
    LevelFailed,
}
