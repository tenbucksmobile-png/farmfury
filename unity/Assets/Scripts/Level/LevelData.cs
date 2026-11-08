using System;
using UnityEngine;

[CreateAssetMenu(fileName = "LevelData", menuName = "FarmFury/Level Data", order = 0)]
public class LevelData : ScriptableObject
{
    [Header("Identity")]
    public string levelId;
    public string levelName;
    public int parBirds = 3;
    // Which of the 6 planned worlds this level belongs to (1 = Meadow Ruins/World 1, 2 = Frozen
    // Tundra/World 2, etc. — see CLAUDE.md's World table). Added 2026-07-19 alongside World 2's
    // backdrop groundwork (EnvironmentDepthSystem's World-2 branch). Defaults to 1 so every
    // existing L01-L18 asset (created before this field existed, and any generator Make() call
    // that doesn't pass world explicitly) is implicitly World 1 with no data migration needed.
    public int world = 1;

    // The world's boss level (L18 Commander for World 1). Clearing it unlocks the next story world and that
    // world's animals (SaveManager); Level Select draws it as the shield slot, opened once every regular level
    // in the world is complete. Added 2026-11-02 with the franchise-flow port.
    public bool isBossLevel;

    // Frame this level by the Angry Birds 2 reference (ReferenceFraming.cs, docs/REFERENCE_FRAMEWORK.md): fixed
    // zoom from the animal's on-screen size, launcher at 0.26 of the screen width, one shared ground line with the
    // scenery and the painted meadow. Off = the older content-fitting auto-zoom. Added 2026-11-08, L01 first.
    public bool referenceFraming;

    // Trim block colliders to their visible art without the reference framing: set by WorldLevelGenerator on levels
    // built from a framed World 1 template (their layouts are by visible edges). Framed levels always trim.
    public bool trimColliders;

    /// <summary>The franchise WorldType for the 1-based <see cref="world"/> int.</summary>
    public global::WorldType WorldType => (global::WorldType)Mathf.Clamp(world - 1, 0, (int)global::WorldType.CropFactory);

    [Header("Bird Queue")]
    public AnimalType[] birds;

    [Header("Layout")]
    public BlockSpawnData[] blocks;
    public RobotSpawnData[] robots;
    // Raised earth islands that structures stand on (reference framework rule A6, 2026-11-08). Solid terrain:
    // layer Ground, tag "Ground", never breaks. Spawned before blocks/robots by LevelLoader.SpawnPlatform.
    public PlatformSpawnData[] platforms;

    [Serializable]
    public struct PlatformSpawnData
    {
        public float x;          // centre X
        public float surfaceY;   // world Y of the walkable grass surface (blocks rest on this line)
        public float width;      // visual width in world units
        public int   variant;    // LevelLoader mount kind: 0/1 islands, 2 hill, 3 mesa, 4 scaffold, 5 wagon, 6 ramp
        // Wagon only: parked (static) until something hits it, then rolls (RollingWagon) — e.g. off a ramp's plateau
        // and down into a structure below. surfaceY is then the wagon bed's top.
        public bool  rolling;
    }

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
        // Forces this specific instance to stay physically fixed regardless of what happens to
        // its support (BlockBase.NeverFalls) — false (default) means "use the prefab's own
        // default," which is what every block type except Haybale needs. Added 2026-07-16
        // alongside flipping HaybaleBlock.prefab's own default from stayKinematic=true back to
        // false: that prefab-wide flag (added 2026-07-26 for L01's ground pile — "hitting one
        // haybale woke all four") had been silently applying to EVERY Haybale placement in every
        // level since, including ones stacked on top of tall structures (e.g. L12/L14) — user
        // report 2026-07-16: "structure sprites remain mid-air" — those haybales' own support
        // could be destroyed out from under them and they'd just stay Static, floating, forever.
        // Only L01's specific decorative ground pile actually needs this set true now.
        public bool forceStayKinematic;
        // Mirror the block's art horizontally. Set by WorldLevelGenerator when it mirrors a World 1
        // tower for another world, so skewed/diagonal planks still lean the way the structure does.
        public bool flipX;
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