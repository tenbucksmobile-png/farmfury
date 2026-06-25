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

    public void RestartLevel() => StartLevel(CurrentLevelIndex);

    // Starts a level without reloading the scene — for direct play in Game.unity
    public void ForceStartLevel(int index)
    {
        if (_levels == null || _levels.Length == 0)
        {
            Debug.LogError("[GameManager] No levels wired. Run FarmFury → Wire Scene References.");
            return;
        }
        CurrentLevelIndex = Mathf.Clamp(index, 0, _levels.Length - 1);
        TransitionTo(GameState.Playing);
        OnLevelStarted?.Invoke(_levels[CurrentLevelIndex]);
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
        // When it isn't (e.g. Phase 2.5 dev), LevelSelectController handles Idle in-scene.
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