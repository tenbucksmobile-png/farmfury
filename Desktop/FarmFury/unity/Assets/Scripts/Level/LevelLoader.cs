using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LevelLoader : MonoBehaviour
{
    [Header("Block Prefabs")]
    [SerializeField] private BlockBase  _woodPrefab;
    [SerializeField] private BlockBase  _stonePrefab;

    [Header("Animal Prefabs")]
    [SerializeField] private CluckAnimal  _cluckPrefab;
    [SerializeField] private BessieAnimal _bessiePrefab;
    [SerializeField] private PercyAnimal  _percyPrefab;
    [SerializeField] private WoollyAnimal _woollyPrefab;
    [SerializeField] private DuckyAnimal  _duckyPrefab;
    [SerializeField] private HoraceAnimal _horacePrefab;
    [SerializeField] private GeraldAnimal _geraldPrefab;
    [SerializeField] private BillyAnimal  _billyPrefab;

    [Header("Enemy Prefabs")]
    [SerializeField] private RobotEnemy _robotPrefab;

    [Header("Parents")]
    [SerializeField] private Transform _blockParent;
    [SerializeField] private Transform _robotParent;

    private readonly List<BlockBase>   _spawnedBlocks = new();
    private readonly List<RobotEnemy>  _spawnedRobots = new();
    private readonly Queue<AnimalType> _birdQueue     = new();

    public int        RemainingRobots    => _spawnedRobots.Count;
    public bool       HasBirdsRemaining  => _birdQueue.Count > 0;
    public int        BirdsRemaining     => _birdQueue.Count;
    public AnimalType PeekNextBird()     => _birdQueue.Count > 0 ? _birdQueue.Peek() : AnimalType.Cluck;

    // Snapshot of the queue at this moment (copy — safe to iterate while the queue changes)
    public AnimalType[] BirdQueueSnapshot => _birdQueue.ToArray();

    public bool TryConsumeBird(out AnimalType type)
    {
        if (_birdQueue.Count == 0) { type = default; return false; }
        type = _birdQueue.Dequeue();
        OnBirdConsumed?.Invoke();
        return true;
    }

    public event Action OnBirdConsumed;
    public event Action OnAllRobotsDestroyed;
    public event Action OnBirdsExhausted;

    void OnEnable()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.OnLevelStarted += HandleLevelStarted;
    }

    void OnDisable()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.OnLevelStarted -= HandleLevelStarted;
    }

    public void LoadLevel(LevelData data)
    {
        ClearLevel();

        _birdQueue.Clear();
        foreach (var b in data.birds) _birdQueue.Enqueue(b);

        foreach (var b in data.blocks) SpawnBlock(b);
        foreach (var r in data.robots) SpawnRobot(r);

        ScoreManager.Instance.InitLevel(
            GameManager.Instance.CurrentLevelIndex,
            data.birds.Length,
            _spawnedRobots.Count);
    }

    public AnimalBase CreateNextAnimal(AnimalType type, Vector3 spawnPosition)
    {
        AnimalBase prefab = type switch
        {
            AnimalType.Bessie => _bessiePrefab,
            AnimalType.Percy  => _percyPrefab,
            AnimalType.Woolly => _woollyPrefab,
            AnimalType.Ducky  => _duckyPrefab,
            AnimalType.Horace => _horacePrefab,
            AnimalType.Gerald => _geraldPrefab,
            AnimalType.Billy  => _billyPrefab,
            _                 => _cluckPrefab,
        };
        return Instantiate(prefab, spawnPosition, Quaternion.identity);
    }

    public void NotifyRobotDestroyed(RobotEnemy robot)
    {
        _spawnedRobots.Remove(robot);
        ScoreManager.Instance.AddRobotScore();

        if (_spawnedRobots.Count == 0)
        {
            OnAllRobotsDestroyed?.Invoke();
            StartCoroutine(DelayedLevelComplete());
        }
    }

    public void NotifyBirdsExhausted()
    {
        if (_spawnedRobots.Count > 0)
        {
            OnBirdsExhausted?.Invoke();
            StartCoroutine(DelayedLevelFailed());
        }
    }

    void HandleLevelStarted(LevelData data) => LoadLevel(data);

    void SpawnBlock(LevelData.BlockSpawnData data)
    {
        var prefab = data.type == BlockType.Wood ? _woodPrefab : _stonePrefab;
        var block  = Instantiate(prefab,
            new Vector3(data.position.x, data.position.y, 0f),
            Quaternion.identity,
            _blockParent);
        block.Initialise(data.size.x, data.size.y);
        _spawnedBlocks.Add(block);
    }

    void SpawnRobot(LevelData.RobotSpawnData data)
    {
        var robot = Instantiate(_robotPrefab,
            new Vector3(data.position.x, data.position.y, 0f),
            Quaternion.identity,
            _robotParent);
        robot.Initialise(this);
        _spawnedRobots.Add(robot);
    }

    void ClearLevel()
    {
        foreach (var b in _spawnedBlocks) if (b) Destroy(b.gameObject);
        foreach (var r in _spawnedRobots) if (r) Destroy(r.gameObject);
        _spawnedBlocks.Clear();
        _spawnedRobots.Clear();
    }

    IEnumerator DelayedLevelComplete()
    {
        yield return new WaitForSeconds(2f);
        ScoreManager.Instance.FinaliseLevel();
        GameManager.Instance.CompleteLevel();
    }

    IEnumerator DelayedLevelFailed()
    {
        yield return new WaitForSeconds(1.5f);
        GameManager.Instance.FailLevel();
    }
}
