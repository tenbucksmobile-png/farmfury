using System;
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

    public event Action<int> OnScoreChanged;
    public event Action<int> OnStarsAwarded;

    private int  _levelIndex;
    private int  _totalRobots;
    private bool _levelEnded;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public void InitLevel(int levelIndex, int totalBirds, int totalRobots)
    {
        _levelIndex     = levelIndex;
        _totalRobots    = totalRobots;
        _levelEnded     = false;
        IsNewBest       = false;
        Score           = 0;
        Stars           = 0;
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

        Stars = CalculateStars();
        SaveIfBest();
        OnStarsAwarded?.Invoke(Stars);
    }

    public static int GetBestScore(int levelIndex) =>
        PlayerPrefs.GetInt(ScoreKey(levelIndex), 0);

    public static int GetBestStars(int levelIndex) =>
        PlayerPrefs.GetInt(StarsKey(levelIndex), 0);

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

    void SaveIfBest()
    {
        if (Score > GetBestScore(_levelIndex))
        {
            PlayerPrefs.SetInt(ScoreKey(_levelIndex), Score);
            IsNewBest = true;
        }
        if (Stars > GetBestStars(_levelIndex))
            PlayerPrefs.SetInt(StarsKey(_levelIndex), Stars);
        PlayerPrefs.Save();
    }

    static string ScoreKey(int idx) => $"ff_score_{idx}";
    static string StarsKey(int idx) => $"ff_stars_{idx}";
}