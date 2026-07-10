using System;
using UnityEngine;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(-100)]
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Level Registry")]
    [SerializeField] private LevelData[] _levels;

    [Header("Scene Names")]
    [SerializeField] private string _menuSceneName = "MainMenu";
    [SerializeField] private string _gameSceneName = "Game";

    public GameState State { get; private set; } = GameState.Idle;
    public int CurrentLevelIndex { get; private set; }
    public int TotalLevels => _levels != null ? _levels.Length : 0;

    public event Action<GameState> OnStateChanged;
    public event Action<LevelData> OnLevelStarted;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

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

    public void StartLevel(int index)
    {
        CurrentLevelIndex = Mathf.Clamp(index, 0, TotalLevels - 1);
        TransitionTo(GameState.Playing);
        var data = _levels[CurrentLevelIndex];
        SceneManager.LoadScene(_gameSceneName);
        OnLevelStarted?.Invoke(data);
    }

    // Uses ForceStartLevel (no scene reload) rather than StartLevel — restarting the exact
    // same level doesn't need a full scene teardown/rebuild, and this reuses the same
    // in-place reset path (CatapultLauncher.OnLevelStarted, LevelLoader.ClearLevel +
    // respawn, ScoreManager.InitLevel) that already runs on every level load, rather than
    // the SceneManager.LoadScene path, which exists for switching to a *different* level.
    public void RestartLevel() => ForceStartLevel(CurrentLevelIndex);

    // Starts a level without reloading the scene — for direct play in Game.unity
    public void ForceStartLevel(int index)
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
        TransitionTo(GameState.Playing);
        OnLevelStarted?.Invoke(data);
    }

    public void CompleteLevel()
    {
        if (State != GameState.Playing) return;
        TransitionTo(GameState.LevelComplete);
    }

    public void FailLevel()
    {
        if (State != GameState.Playing) return;
        TransitionTo(GameState.LevelFailed);
    }

    public void LoadNextLevel()
    {
        int next = CurrentLevelIndex + 1;
        if (next < TotalLevels)
            StartLevel(next);
        else
            LoadMenu();
    }

    public void LoadMenu()
    {
        TransitionTo(GameState.Idle);
        // Only load the menu scene if it's registered in Build Settings.
        // When it isn't (the current setup), WorldMapController/MainMenuController handle
        // Idle in-scene instead (LevelSelectController used to as well — removed 2026-07-26,
        // it was racing WorldMapController to show itself on every Idle transition).
        if (SceneInBuild(_menuSceneName))
            SceneManager.LoadScene(_menuSceneName);
    }

    static bool SceneInBuild(string sceneName)
    {
        for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
        {
            string path = SceneUtility.GetScenePathByBuildIndex(i);
            string name = System.IO.Path.GetFileNameWithoutExtension(path);
            if (name == sceneName) return true;
        }
        return false;
    }

    void TransitionTo(GameState next)
    {
        if (State == next) return;
        State = next;
        OnStateChanged?.Invoke(next);
    }
}

public enum GameState
{
    Idle,
    Playing,
    LevelComplete,
    LevelFailed,
}