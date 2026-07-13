using System.Collections.Generic;
using UnityEngine;

// Lifetime player statistics, persisted via PlayerPrefs — powers Settings > Stats (2026-07-13).
// Everything ScoreManager already tracks per-level (best score, star count) is aggregated at
// display time in MainMenuController instead of duplicated here — this class only owns counters
// that don't exist anywhere else in the codebase: cannonballs fired, robots destroyed, per-animal
// usage (for "Favourite Animal"), and time played.
//
// Self-bootstraps via CatapultLauncher.Awake() (same null-safety fallback pattern as
// AudioManager/LevelCompleteManager/LevelFailedManager) so recording calls are never silently
// dropped just because a scene hasn't been re-wired since this was added.
[DefaultExecutionOrder(-80)]
public class PlayerStatsTracker : MonoBehaviour
{
    public static PlayerStatsTracker Instance { get; private set; }

    public static int   TotalCannonballsFired { get; private set; }
    public static int   TotalRobotsDestroyed  { get; private set; }
    public static float TimePlayedSeconds     { get; private set; }

    private static readonly Dictionary<AnimalType, int> _animalUsage = new();

    private const float SaveInterval = 5f; // flush to PlayerPrefs periodically, not every frame
    private float _saveTimer;
    private bool  _isPlaying;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        TotalCannonballsFired = PlayerPrefs.GetInt("ff_stat_cannonballs_fired", 0);
        TotalRobotsDestroyed  = PlayerPrefs.GetInt("ff_stat_robots_destroyed", 0);
        TimePlayedSeconds     = PlayerPrefs.GetFloat("ff_stat_time_played", 0f);
        foreach (AnimalType a in System.Enum.GetValues(typeof(AnimalType)))
            _animalUsage[a] = PlayerPrefs.GetInt("ff_stat_animal_used_" + a, 0);
    }

    void Start()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnStateChanged += OnStateChanged;
            _isPlaying = GameManager.Instance.State == GameState.Playing;
        }
    }

    void OnDestroy()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.OnStateChanged -= OnStateChanged;
        Save();
    }

    void OnStateChanged(GameState state) => _isPlaying = state == GameState.Playing;

    // Time Played only accumulates while a level is actually in progress (GameState.Playing) —
    // not menu/world-map browsing time, matching how the stat reads to a player ("how long have
    // I been playing levels").
    void Update()
    {
        if (!_isPlaying) return;
        TimePlayedSeconds += Time.unscaledDeltaTime;
        _saveTimer        += Time.unscaledDeltaTime;
        if (_saveTimer >= SaveInterval) { _saveTimer = 0f; Save(); }
    }

    void OnApplicationPause(bool paused) { if (paused) Save(); }
    void OnApplicationQuit() => Save();

    void Save()
    {
        PlayerPrefs.SetInt("ff_stat_cannonballs_fired", TotalCannonballsFired);
        PlayerPrefs.SetInt("ff_stat_robots_destroyed", TotalRobotsDestroyed);
        PlayerPrefs.SetFloat("ff_stat_time_played", TimePlayedSeconds);
        PlayerPrefs.Save();
    }

    // Called from CatapultLauncher.Fire() the instant a bird is actually consumed/launched.
    public static void RecordCannonballFired(AnimalType animal)
    {
        if (Instance == null) return;
        TotalCannonballsFired++;
        _animalUsage.TryGetValue(animal, out int c);
        _animalUsage[animal] = c + 1;
        PlayerPrefs.SetInt("ff_stat_cannonballs_fired", TotalCannonballsFired);
        PlayerPrefs.SetInt("ff_stat_animal_used_" + animal, _animalUsage[animal]);
        PlayerPrefs.Save();
    }

    // Called from RobotEnemy.Die().
    public static void RecordRobotDestroyed()
    {
        if (Instance == null) return;
        TotalRobotsDestroyed++;
        PlayerPrefs.SetInt("ff_stat_robots_destroyed", TotalRobotsDestroyed);
        PlayerPrefs.Save();
    }

    public static AnimalType GetFavouriteAnimal(out int uses)
    {
        AnimalType best = AnimalType.Cluck;
        uses = 0;
        foreach (var kv in _animalUsage)
            if (kv.Value > uses) { best = kv.Key; uses = kv.Value; }
        return best;
    }
}
