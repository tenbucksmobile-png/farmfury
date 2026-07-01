using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-50)]
public class LevelLoader : MonoBehaviour
{
    [Header("Block Prefabs")]
    [SerializeField] private BlockBase  _woodPrefab;
    [SerializeField] private BlockBase  _stonePrefab;
    [SerializeField] private BlockBase  _haybalePrefab; // WoodBlock mechanics, Haybail.png art

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
    [SerializeField] private RobotEnemy _harvesterPrefab;

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

    void Awake()
    {
#if UNITY_EDITOR
        AutoLoadPrefabs();
#endif
    }

#if UNITY_EDITOR
    void AutoLoadPrefabs()
    {
        if (_woodPrefab       == null) _woodPrefab       = LoadPrefabComponent<WoodBlock>("WoodBlock");
        if (_stonePrefab      == null) _stonePrefab      = LoadPrefabComponent<StoneBlock>("StoneBlock");
        if (_haybalePrefab    == null) _haybalePrefab    = LoadPrefabComponent<WoodBlock>("HaybaleBlock");
        if (_robotPrefab      == null) _robotPrefab      = LoadPrefabComponent<RobotEnemy>("Robot");
        if (_harvesterPrefab  == null) _harvesterPrefab  = LoadPrefabComponent<RobotEnemy>("HarvesterRobot");
        if (_cluckPrefab  == null) _cluckPrefab  = LoadPrefabComponent<CluckAnimal>("CluckAnimal");
        if (_bessiePrefab == null) _bessiePrefab = LoadPrefabComponent<BessieAnimal>("BessieAnimal");
        if (_percyPrefab  == null) _percyPrefab  = LoadPrefabComponent<PercyAnimal>("PercyAnimal");
        if (_woollyPrefab == null) _woollyPrefab = LoadPrefabComponent<WoollyAnimal>("WoollyAnimal");
        if (_duckyPrefab  == null) _duckyPrefab  = LoadPrefabComponent<DuckyAnimal>("DuckyAnimal");
        if (_horacePrefab == null) _horacePrefab = LoadPrefabComponent<HoraceAnimal>("HoraceAnimal");
        if (_geraldPrefab == null) _geraldPrefab = LoadPrefabComponent<GeraldAnimal>("GeraldAnimal");
        if (_billyPrefab  == null) _billyPrefab  = LoadPrefabComponent<BillyAnimal>("BillyAnimal");
        if (_blockParent  == null) _blockParent  = (GameObject.Find("BlockParent") ?? new GameObject("BlockParent")).transform;
        if (_robotParent  == null) _robotParent  = (GameObject.Find("RobotParent") ?? new GameObject("RobotParent")).transform;
    }

    static T LoadPrefabComponent<T>(string name) where T : Component
    {
        var guids = UnityEditor.AssetDatabase.FindAssets($"{name} t:Prefab", new[] { "Assets/Prefabs" });
        foreach (var g in guids)
        {
            var go   = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                           UnityEditor.AssetDatabase.GUIDToAssetPath(g));
            var comp = go != null ? go.GetComponent<T>() : null;
            if (comp != null) return comp;
        }
        return null;
    }
#endif

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

    public Sprite GetAnimalIdleSprite(AnimalType type)
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
        return prefab != null ? prefab.IdleSprite : null;
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
        var prefab = data.type switch
        {
            BlockType.Stone   => _stonePrefab,
            BlockType.Haybale => _haybalePrefab != null ? _haybalePrefab : _woodPrefab,
            _                 => _woodPrefab,
        };
        if (prefab == null) { Debug.LogWarning("[LevelLoader] Block prefab null — run Wire Scene References."); return; }
        var block  = Instantiate(prefab,
            new Vector3(data.position.x, data.position.y, 0f),
            Quaternion.identity,
            _blockParent);
        block.Initialise(data.size.x, data.size.y);
        block.ApplyOverrides(data.healthOverride, data.massOverride);
        if (data.passThrough && block is WoodBlock wood) wood._passThrough = true;
        _spawnedBlocks.Add(block);
    }

    void SpawnRobot(LevelData.RobotSpawnData data)
    {
        var prefab = data.robotType == RobotType.Harvester && _harvesterPrefab != null
            ? _harvesterPrefab
            : _robotPrefab;
        if (prefab == null) { Debug.LogWarning("[LevelLoader] Robot prefab null — run Wire Scene References."); return; }
        var robot = Instantiate(prefab,
            new Vector3(data.position.x, data.position.y, 0f),
            Quaternion.identity,
            _robotParent);
        if (data.scale != Vector2.zero)
        {
            robot.transform.localScale = new Vector3(data.scale.x, data.scale.y, 1f);

            // RobotEnemy.Awake() sets BoxCollider2D.size=(1,1) assuming the default
            // (0.6, 0.9) scale, which gives a sane 0.6×0.9 world hitbox. A large custom
            // scale (used to make L01's HarvesterRobot visually imposing) would otherwise
            // inflate the SAME collider to e.g. 4.36×4.69 world units — deep enough to
            // overlap the ground collider at spawn and get launched into the air by
            // physics separation. Re-derive collider size so the world-space hitbox stays
            // pinned to the default 0.6×0.9 regardless of visual scale.
            if (robot.TryGetComponent<BoxCollider2D>(out var col))
                col.size = new Vector2(0.6f / data.scale.x, 0.9f / data.scale.y);
        }
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
