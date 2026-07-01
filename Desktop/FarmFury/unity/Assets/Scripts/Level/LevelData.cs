using System;
using UnityEngine;

[CreateAssetMenu(fileName = "LevelData", menuName = "FarmFury/Level Data", order = 0)]
public class LevelData : ScriptableObject
{
    [Header("Identity")]
    public string levelId;
    public string levelName;
    public int parBirds = 3;

    [Header("Bird Queue")]
    public AnimalType[] birds;

    [Header("Layout")]
    public BlockSpawnData[] blocks;
    public RobotSpawnData[] robots;

    [Serializable]
    public struct BlockSpawnData
    {
        public BlockType type;
        public Vector2   position;
        public Vector2   size;
        public bool      passThrough;    // Cluck passes through at 70% velocity on impact
        public float     healthOverride; // 0 = use BlockBase default; >0 overrides maxHealth
        public float     massOverride;   // 0 = use BlockBase default; >0 overrides mass
    }

    [Serializable]
    public struct RobotSpawnData
    {
        public RobotType robotType;
        public Vector2   position;
        public Vector2   scale;     // (0,0) = use prefab default; non-zero overrides localScale
    }
}

public enum AnimalType { Cluck, Bessie, Percy, Woolly, Ducky, Horace, Gerald, Billy }
public enum BlockType  { Wood,  Stone,  Haybale }
public enum RobotType  { Basic, Harvester }