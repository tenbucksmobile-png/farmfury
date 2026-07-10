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
        public BlockType     type;
        public Vector2       position;
        public Vector2       size;
        public bool          passThrough;    // Cluck passes through at 70% velocity on impact
        public float         healthOverride; // 0 = use BlockBase default; >0 overrides maxHealth
        public float         massOverride;   // 0 = use BlockBase default; >0 overrides mass
        // Which wood art sprite to show — Auto (default) picks by aspect ratio in
        // BlockBase.Initialise() like before, which silently misidentifies any art asset whose
        // own visual orientation doesn't match its computed w/h aspect (e.g. Plank_2DShork.png is
        // a clearly VERTICAL plank image but its footprint is nearly square, so aspect-based
        // guessing put it in the "normal/flat" bucket — found 2026-07-10 investigating an
        // L03 "wood renders wrong" report). LevelLayoutDumper now sets this directly from the
        // actual design-time sprite's filename instead of leaving it to aspect-guessing.
        public WoodArtVariant artVariant;
    }

    [Serializable]
    public struct RobotSpawnData
    {
        public RobotType robotType;
        public Vector2   position;
        public Vector2   scale;     // (0,0) = use prefab default; non-zero overrides localScale
    }
}

public enum AnimalType    { Cluck, Bessie, Percy, Woolly, Ducky, Horace, Gerald, Billy }
public enum BlockType     { Wood,  Stone,  Haybale, Barrel }
public enum RobotType     { Basic, Harvester, SemiHarvester }
public enum WoodArtVariant { Auto, Flat, Horizontal, Vertical }