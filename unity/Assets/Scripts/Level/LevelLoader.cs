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
    [SerializeField] private BlockBase  _barrelPrefab;  // ExplodingBarrelBlock, WoodenBarrel.png art

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
    [SerializeField] private RobotEnemy _semiHarvesterPrefab;
    [SerializeField] private RobotEnemy _commanderPrefab; // L18 boss

    // World 2 (Frozen Tundra) reskins of the same 3 tiers — added 2026-07-19. Deliberately NOT
    // new RobotType enum values: the World 2 match-up cards were already wired
    // (SceneSetup.WireWorld2MatchUpCards) indexed by the EXISTING RobotType.Basic/Harvester/
    // SemiHarvester values (FrostRobot=Basic tier, IceHarvestor=Harvester tier,
    // GlacierHarvestor=SemiHarvester tier), so SpawnRobot() below picks between these and the W1
    // prefabs above based on LevelData.world, same division of responsibility as
    // EnvironmentDepthSystem.ApplyWorldLayers() swapping backdrop sprites by world. No Commander
    // reskin exists yet — World 2 L-final-boss levels fall back to CommanderRobot's W1 art until
    // one does.
    [Header("World 2 Enemy Prefabs (reskins, same RobotType tiers)")]
    [SerializeField] private RobotEnemy _robotPrefabW2;
    [SerializeField] private RobotEnemy _harvesterPrefabW2;
    [SerializeField] private RobotEnemy _semiHarvesterPrefabW2;

    [Header("Parents")]
    [SerializeField] private Transform _blockParent;
    [SerializeField] private Transform _robotParent;

    private readonly List<BlockBase>   _spawnedBlocks = new();
    private readonly List<RobotEnemy>  _spawnedRobots = new();
    private readonly Queue<AnimalType> _birdQueue     = new();

    // Tracks the pending "level failed" coroutine so a robot dying after it's already started can
    // cancel it — see NotifyRobotDestroyed()/NotifyBirdsExhausted() below.
    private Coroutine _failedRoutine;

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
        if (_barrelPrefab     == null) _barrelPrefab     = LoadPrefabComponent<ExplodingBarrelBlock>("ExplodingBarrelBlock");
        if (_robotPrefab      == null) _robotPrefab      = LoadPrefabComponent<RobotEnemy>("Robot");
        if (_harvesterPrefab  == null) _harvesterPrefab  = LoadPrefabComponent<RobotEnemy>("HarvesterRobot");
        if (_semiHarvesterPrefab == null) _semiHarvesterPrefab = LoadPrefabComponent<RobotEnemy>("SemiHarvesterRobot");
        if (_commanderPrefab  == null) _commanderPrefab  = LoadPrefabComponent<RobotEnemy>("CommanderRobot");
        if (_robotPrefabW2         == null) _robotPrefabW2         = LoadPrefabComponent<RobotEnemy>("FrostRobot");
        if (_harvesterPrefabW2     == null) _harvesterPrefabW2     = LoadPrefabComponent<RobotEnemy>("IceHarvestorRobot");
        if (_semiHarvesterPrefabW2 == null) _semiHarvesterPrefabW2 = LoadPrefabComponent<RobotEnemy>("GlacierHarvestorRobot");
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
        foreach (var r in data.robots) SpawnRobot(r, data.world);

        ScoreManager.Instance.InitLevel(
            GameManager.Instance.CurrentLevelIndex,
            data.birds.Length,
            _spawnedRobots.Count);

        StartCoroutine(SettleUnsupportedBlocksNextFrame());
    }

    // Wakes any spawned block that has zero real support beneath it (see
    // BlockBase.SettleIfUnsupported) right at level start, before the player can act — fixes
    // orphaned floating debris left by a level-authoring gap rather than leaving it hanging for
    // the whole level. Waits one frame first so every block's BoxCollider2D has actually
    // registered with Physics2D (colliders added this same frame aren't guaranteed queryable yet).
    IEnumerator SettleUnsupportedBlocksNextFrame()
    {
        yield return null;
        foreach (var block in _spawnedBlocks)
            if (block != null) block.SettleIfUnsupported();
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
            // Cancel any already-pending "level failed" sequence — 2026-07-13 fix for a real
            // race: the player's last bird can land and trigger NotifyBirdsExhausted() (below)
            // BEFORE a structural collapse that same bird set off finishes killing the last
            // robot(s) a few tenths of a second later (e.g. a toppling wood tower crushing a
            // robot underneath, or a delayed Fall-damage death). GameManager.CompleteLevel()/
            // FailLevel() both only transition from GameState.Playing, so whichever coroutine's
            // wait elapses first wins and the other silently no-ops — without cancelling the
            // fail path here, a level the player actually cleared could still force the fail
            // screen and make them replay it. Robot destruction always wins outright now.
            if (_failedRoutine != null) { StopCoroutine(_failedRoutine); _failedRoutine = null; }
            OnAllRobotsDestroyed?.Invoke();
            StartCoroutine(DelayedLevelComplete());
        }
    }

    public void NotifyBirdsExhausted()
    {
        if (_spawnedRobots.Count > 0)
        {
            OnBirdsExhausted?.Invoke();
            _failedRoutine = StartCoroutine(DelayedLevelFailed());
        }
    }

    void HandleLevelStarted(LevelData data) => LoadLevel(data);

    void SpawnBlock(LevelData.BlockSpawnData data)
    {
        var prefab = data.type switch
        {
            BlockType.Stone   => _stonePrefab,
            BlockType.Haybale => _haybalePrefab != null ? _haybalePrefab : _woodPrefab,
            BlockType.Barrel  => _barrelPrefab  != null ? _barrelPrefab  : _woodPrefab,
            _                 => _woodPrefab,
        };
        if (prefab == null) { Debug.LogWarning("[LevelLoader] Block prefab null — run Wire Scene References."); return; }
        var block  = Instantiate(prefab,
            new Vector3(data.position.x, data.position.y, 0f),
            Quaternion.identity,
            _blockParent);
        block.Initialise(data.size.x, data.size.y, data.artVariant);
        block.ApplyOverrides(data.healthOverride, data.massOverride);
        block.Indestructible = data.indestructible;
        if (data.forceStayKinematic) block.SetStayKinematic(true);
        if (data.passThrough && block is WoodBlock wood) wood._passThrough = true;
        _spawnedBlocks.Add(block);
    }

    void SpawnRobot(LevelData.RobotSpawnData data, int world = 1)
    {
        // World 2 reskins picked first when available and the level actually belongs to World
        // 2 — falls through to the normal World 1 switch below whenever a W2 variant isn't
        // wired yet (e.g. Commander, which has no reskin), so this can never spawn a null robot
        // just because one tier's reskin is still missing.
        RobotEnemy prefab = null;
        if (world == 2)
        {
            prefab = data.robotType switch
            {
                RobotType.Harvester     => _harvesterPrefabW2,
                RobotType.SemiHarvester => _semiHarvesterPrefabW2,
                RobotType.Basic         => _robotPrefabW2,
                _                       => null,
            };
        }
        prefab ??= data.robotType switch
        {
            RobotType.Harvester     when _harvesterPrefab     != null => _harvesterPrefab,
            RobotType.SemiHarvester when _semiHarvesterPrefab != null => _semiHarvesterPrefab,
            RobotType.Commander     when _commanderPrefab     != null => _commanderPrefab,
            _                                                          => _robotPrefab,
        };
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

        // Boss fights: a Commander's death brings down any Indestructible "guarded structure" in
        // the level (e.g. L18's StoneTower) — 2026-07-12, user request: "when commander explodes
        // the whole tower should destruct." Linked here at runtime rather than on the prefab
        // since both the boss and its guarded structure are freshly spawned per level from
        // LevelData, not static scene objects that could hold a fixed reference. Blocks are
        // always spawned before robots in LoadLevel(), so every Indestructible block for this
        // level is already in _spawnedBlocks by the time any robot spawns.
        if (data.robotType == RobotType.Commander)
            foreach (var block in _spawnedBlocks)
                if (block != null && block.Indestructible)
                    robot.AddDestroyOnDeath(block);
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
        _failedRoutine = null;
        // Defensive re-check alongside the cancel-on-clear in NotifyRobotDestroyed() above — if
        // the last robot somehow died in the same frame this coroutine's wait elapsed (before its
        // own cancellation could run), don't fail a level that's actually already cleared.
        if (_spawnedRobots.Count == 0) yield break;
        GameManager.Instance.FailLevel();
    }
}
