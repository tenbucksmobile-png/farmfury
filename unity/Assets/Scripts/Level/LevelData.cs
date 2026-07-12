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
        // Fixed scenery/structure that never takes damage or breaks — e.g. L10's StoneTower,
        // "a structure that is in place - cannot be destroyed" (2026-07-12). Distinct from a
        // normal tanky Stone block (which still dies eventually): TakeDamage() no-ops entirely.
        public bool          indestructible;
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
// Commander added 2026-07-12 for L18's boss level — Commander.png/Commander_Hit.png/
// Commander_Explode.png art dropped in by the user, see SceneSetup.EnsureCommanderRobotPrefab().
public enum RobotType     { Basic, Harvester, SemiHarvester, Commander }
// Expanded 2026-07-12 (user report: "the sprites that I made the scene with are not the correct
// ones... too many gaps") — LevelLayoutDumper's original 4 generic buckets (Auto/Flat/Horizontal/
// Vertical) collapsed ~10 distinct named prop shapes (Plank_Skew, Plank_Diagonal, Stone_Square,
// etc.) down to reusing just 2-3 images per block type, so a diagonal/skewed piece placed to
// visually bridge a gap in the Scene view rendered as a plain flat rectangle in-game instead,
// leaving a visible gap. Each new case maps to its own dedicated sprite per block type (see
// BlockBase's new _spr* fields and SceneSetup.WireBlockSprites) — shared cases like Skew/Diagonal
// resolve to a DIFFERENT sprite depending on which block class (Wood vs Stone) is asking, since
// each subclass has its own separate serialized field for the same enum value.
public enum WoodArtVariant
{
    Auto, Flat, Horizontal, Vertical,
    // Wood-specific named shapes
    Short, VerticalShort, Horizontal2D, Shork2D, Shork, Cart, Barrel,
    // Stone-specific named shapes
    Square, Block, RuinedWall, Tower,
    // Shared shapes (a different sprite per block class — see BlockBase.Initialise())
    Skew, Diagonal,
}