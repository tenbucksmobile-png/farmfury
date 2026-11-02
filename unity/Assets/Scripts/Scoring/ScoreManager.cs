using System;
using FarmFury.Core;
using UnityEngine;

public class ScoreManager : MonoBehaviour
{
    public static ScoreManager Instance { get; private set; }

    private const int PtsRobot       = 1000;
    private const int PtsWoodBlock   = 100;
    private const int PtsStoneBlock  = 200;
    private const int PtsEggHit      = 50;
    private const int PtsBirdLeft    = 500;

    public int  Score           { get; private set; }
    public int  BirdsRemaining  { get; private set; }
    public int  RobotsDestroyed { get; private set; }
    public int  BlocksDestroyed { get; private set; }
    public int  Stars           { get; private set; }
    public bool IsNewBest       { get; private set; }

    // "Perfect" tier (GDD 4th star tier: 3-star clear + every block on the level destroyed too,
    // not just the robots) — added 2026-08-09 as a mastery target for players who were one-
    // shotting structures and getting a trivial 3-star with blocks still standing. Tracked/
    // persisted here; no dedicated art exists yet to surface it in the Level Complete panel (see
    // HUDController), so it's scoring-layer only for now.
    public bool IsPerfect       { get; private set; }

    public event Action<int> OnScoreChanged;
    public event Action<int> OnStarsAwarded;

    private int  _levelIndex;
    private int  _totalRobots;
    private int  _totalBlocks;
    private bool _levelEnded;
    private float _levelStartTime;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public void InitLevel(int levelIndex, int totalBirds, int totalRobots, int totalBlocks = 0)
    {
        _levelIndex     = levelIndex;
        _totalRobots    = totalRobots;
        _totalBlocks    = totalBlocks;
        _levelEnded     = false;
        _levelStartTime = Time.time;   // scaled time: pausing (timeScale 0) doesn't count toward Fastest Time
        IsNewBest       = false;
        Score           = 0;
        Stars           = 0;
        IsPerfect       = false;
        BirdsRemaining  = totalBirds;
        RobotsDestroyed = 0;
        BlocksDestroyed = 0;
        OnScoreChanged?.Invoke(Score);
    }

    public void OnBirdFired() =>
        BirdsRemaining = Mathf.Max(0, BirdsRemaining - 1);

    public void AddRobotScore()
    {
        RobotsDestroyed++;
        AwardPoints(PtsRobot);
    }

    public void AddBlockScore(BlockBase block)
    {
        BlocksDestroyed++;
        int pts = block is StoneBlock ? PtsStoneBlock : PtsWoodBlock;
        AwardPoints(pts);
    }

    public void AddEggHitScore(Vector3 worldPos)
    {
        if (_levelEnded) return;
        AwardPoints(PtsEggHit);
    }

    public void FinaliseLevel()
    {
        if (_levelEnded) return;
        _levelEnded = true;

        int bonus = BirdsRemaining * PtsBirdLeft;
        if (bonus > 0) AwardPoints(bonus);

        Stars     = CalculateStars();
        IsPerfect = Stars >= 3 && _totalBlocks > 0 && BlocksDestroyed >= _totalBlocks;
        SaveIfBest();
        OnStarsAwarded?.Invoke(Stars);
    }

    // Index-based lookups kept for the pre-port World 1/2 maps (removed in the franchise-flow Phase 2); since
    // 2026-11-02 they read SaveManager by levelId — the ff_score_N/ff_stars_N keys are migrated once and no
    // longer written.
    public static int GetBestScore(int levelIndex) =>
        SaveManager.Instance != null && LevelIdAt(levelIndex) is string id ? SaveManager.Instance.GetBestScore(id) : 0;

    public static int GetBestStars(int levelIndex) =>
        SaveManager.Instance != null && LevelIdAt(levelIndex) is string id ? SaveManager.Instance.GetLevelStars(id) : 0;

    public static bool GetIsPerfect(int levelIndex) =>
        SaveManager.Instance != null && LevelIdAt(levelIndex) is string id && SaveManager.Instance.IsLevelPerfect(id);

    static string LevelIdAt(int levelIndex)
    {
        var level = GameManager.Instance != null ? GameManager.Instance.GetLevelData(levelIndex) : null;
        return level != null && !string.IsNullOrEmpty(level.levelId) ? level.levelId : null;
    }

    void AwardPoints(int amount)
    {
        Score += amount;
        OnScoreChanged?.Invoke(Score);
    }

    int CalculateStars()
    {
        if (RobotsDestroyed < _totalRobots) return 0;
        if (BirdsRemaining >= 2)            return 3;
        if (BirdsRemaining >= 1)            return 2;
        return 1;
    }

    // Writes through SaveManager (franchise save model). Stars/score keep the best; a 1+ star clear records the
    // completion, which on a boss level clears the world and unlocks the next one — WorldUnlocked reports that
    // for the results screen.
    public WorldType? WorldUnlocked { get; private set; }
    /// <summary>This result was the level's first ever completion (read before it was recorded).</summary>
    public bool WasFirstClear { get; private set; }
    /// <summary>The level's best stars before this result.</summary>
    public int PreviousBestStars { get; private set; }

    void SaveIfBest()
    {
        WorldUnlocked = null;
        WasFirstClear = false;
        PreviousBestStars = 0;
        var save  = SaveManager.Instance;
        var level = GameManager.Instance != null ? GameManager.Instance.GetLevelData(_levelIndex) : null;
        if (save == null || level == null || string.IsNullOrEmpty(level.levelId))
        {
            Debug.LogWarning("[ScoreManager] No SaveManager or levelId — result not saved.");
            return;
        }

        string id = level.levelId;
        IsNewBest = Score > save.GetBestScore(id);
        PreviousBestStars = save.GetLevelStars(id);
        WasFirstClear = Stars > 0 && !save.IsLevelCompleted(id);
        if (Stars > 0)
        {
            save.SetLevelStars(id, Stars);
            if (IsPerfect) save.MarkLevelPerfect(id);
            save.RecordLevelResult(id, Score, Time.time - _levelStartTime, BirdsRemaining, CatapultLauncher.LastAnimalUsed);
            WorldUnlocked = save.RecordLevelCompleted(level);
        }
        save.SaveProgress();
    }

}
