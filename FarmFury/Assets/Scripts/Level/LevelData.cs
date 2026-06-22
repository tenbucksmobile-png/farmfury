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
        public Vector2 position;
        public Vector2 size;
    }

    [Serializable]
    public struct RobotSpawnData
    {
        public Vector2 position;
    }
}

public enum AnimalType { Cluck, Bessie }
public enum BlockType  { Wood,  Stone  }