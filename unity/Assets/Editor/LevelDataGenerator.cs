// FarmFury — Editor utility. Run via menu: FarmFury ▶ Generate All Level Data
// Coordinate system (current, post-rebuild): ground surface Y = -6.60, launcher X = -2.327.
// Block/robot positions are raw world-space — LevelLoader applies no offset at spawn time.
//
// L01-L09 all exist below as of 2026-07-11 (L06 built out of order, after L07 — nothing
// requires strict numeric sequencing since GameManager auto-discovers every LevelData asset
// regardless of gaps). The original auto-generated L06 (on the OLD pre-rebuild coordinate system,
// ground Y=-2.5/launcher X=-5.5, rigid-translated by a fixed delta dx=+3.173/dy=-4.10, never
// hand-built/verified) was removed 2026-07-09 and replaced by a real hand-built layout 2026-07-10.
// Every level from L02 onward is built individually via LevelLayoutDumper (drag real content into
// the Scene view, dump to code — see that file's header comment). See DeleteStaleAsset below for
// how old generated assets get cleaned up when a level's Make(...) call is removed or renamed.
// Bessie debuts at L07 (revised 2026-07-10 — an earlier same-day plan had her debuting at L05
// instead) — L06 stays Cluck-only even though it was built after L07, since L06 sits before L07
// in level order/progression.

using UnityEngine;
using UnityEditor;

public static partial class LevelDataGenerator
{
    [MenuItem("FarmFury/Generate All Level Data")]
    public static void GenerateAll() => GenerateAll(silent: false);

    public static void GenerateAll(bool silent)
    {
        const string folder = "Assets/ScriptableObjects/Levels";
        EnsureFolder("Assets/ScriptableObjects", "Levels");

        // ── W1_L01  First Contact ─────────────────────────────────────────────
        // Tutorial level. Player shoots Cluck through a loose pile of hay bales
        // to destroy the HarvesterRobot sheltering behind them.
        //
        // Coordinates use the CURRENT (post-rebuild) system: ground Y=-6.60, launcher
        // X=-2.327. This entry was previously out of sync with the live hand-edited
        // asset (old 2.5-ground-relative system) — see CLAUDE.md Audit Findings. Now
        // kept in sync: regenerating via "Generate All Level Data" reproduces this design.
        //
        // HarvesterRobot position/scale — user-provided ground truth, updated whenever the user
        // reports it's still wrong (2026-07-09: (5.6,-5.25); 2026-07-14: (5.7,-5.36), scale
        // (5.7065,7.009) unchanged both times). Do not re-derive this from camera/sprite-padding
        // math — three earlier rounds of Claude's own calculations all came back "still wrong".
        // BoxCollider2D is re-derived in LevelLoader.SpawnRobot() to stay pinned to the default
        // 0.6×0.9 world-space hitbox regardless of visual scale (fixed 2026-07-01 — previously
        // the collider inherited the full scale, deeply overlapping the ground at spawn and
        // getting launched into the air by physics separation) — this formula is scale-agnostic,
        // so position/scale changes here need no corresponding code change.
        //
        // Hay pile: base 3 positions originally matched the hand-placed decorative
        // "Haybail"/"Haybail (1-3)" scene GameObjects (those 4 decorative scene GOs were since
        // deleted, round 6 — superseded by these gameplay blocks). The internal stack gap (top
        // bale above the base row) was correctly recomputed 2026-07-14 from Haybail.png's real
        // pixel content (PIL-measured: 500x500 canvas at PPU=512, trimmed content 500x419px ->
        // 0.7365 units tall at scale 0.9, not the nominal 0.9) — that gap was NOT the bug being
        // fixed 2026-07-18. What was never checked until then: the base row's height *above the
        // true ground line* (Y=-6.60) — those Y values were copied straight from the user's old
        // decorative placement without ever validating them against the ground, and sat ~0.63
        // units above where a bale's bottom edge should actually touch (-6.60 + 0.7365/2 =
        // -6.232 for a bale centred there), i.e. the whole pile was floating with a visible gap
        // beneath it — reported as "the top haybail is still too high" from a screenshot showing
        // exactly that gap. Fixed by shifting all 4 Y values down by the same 0.627u so the
        // lowest base bale now rests at the ground line (preserving the base row's original
        // relative unevenness — an intentional hand-placed "pile" look, not a flat stack — and
        // the top bale's already-correct 0.7365u gap above the base row moves down with it).
        // hp=10 (fixed 2026-07-01, was 60 — that survived multiple hits instead of exploding in
        // one; a typical Cluck impact does ~15-20 impulse damage, so 10 reliably one-shots it).
        // passThrough=true lets Cluck punch through at 70% speed and continue to the robot.
        // Y values shifted up by +1.0 on 2026-07-26 (user-reported: pile sat below the camera's
        // visible safe line) — the previous baseline (ground-touching, per the 2026-07-18 fix
        // below) put the lowest bale's bottom edge at Y~-6.73, well past the camera's own visible
        // bottom (Y=-6.5 at rest — see CLAUDE.md Coordinate System) and therefore cropped/invisible
        // regardless of the physics ground being correct. Since GroundVisual_Placeholder's visible
        // grass top edge is Y=-5.3 (added the same day for the same underlying reason — the true
        // physics ground is basically off-screen), +1.0 lands the pile's lowest bottom edge at
        // Y~-5.73 — comfortably clear of the camera cutoff and roughly matching where the other
        // hand-placed props (barn/tree/fence) already sit visually, rather than the true (mostly
        // off-screen) physics ground line. All 4 bales stay _stayKinematic (see BlockBase.cs), so
        // once placed here none of them will ever move again, struck or not.
        // Top bale re-lowered 0.35u + recentred 2026-07-06 (see its own comment below) — the
        // "already-correct 0.7365u gap" note above refers to the *internal* single-bale-height
        // math, which was fine; the complaint this time was the cap bale balancing on a single
        // point above one base bale looking precarious/disconnected, not the gap size itself.
        // Layout replaced 2026-07-17 with a fresh hand-placed dump against the new
        // ParallaxMidground.png-only backdrop (FarmFury -> Debug -> Dump Level Layout To Log,
        // unity/Logs/level_layout_dump.txt) — user confirmed "the haybale and robot is the new
        // level 1". The raw dump also contained 2 Wood blocks sourced from 'WoodenFence' art at
        // (3.34,-5.368) and (1.55,-5.34) — deliberately EXCLUDED here, not just overlooked: those
        // coordinates sit almost exactly on top of the permanent decorative WoodenFence/
        // WoodenFence(1) scene objects (at 3.4887,-5.2833 and 2,-5.28), the same "decorative fence
        // prop picked up as gameplay blocks by mistake" bug already hit and fixed once before
        // (2026-07-12, L02) — and the user's own description of this dump ("the haybale and robot
        // is the new level 1") already scopes the real content to just those two. SemiHarvester
        // scale (3.564, 3.975) recorded here as the new standard size going forward — see chat
        // for the full callout, this replaces every earlier per-level custom SemiHarvester scale.
        // Layout replaced AGAIN 2026-07-18 with a fresh dump against the new 3-layer parallax
        // backdrop (see unity/Logs/level_layout_dump.txt) — user request: "remove level 1 and use
        // new design." 4 haybale (up from the previous 4-bale pile — same count, new positions/a
        // slightly taller stack) + 1 SemiHarvester, same composition shape as the previous layout,
        // no wood/fence blocks in this dump so no decorative-prop-collision exclusion was needed
        // this time. Not visually re-verified (no Play-mode access here).
        // 2026-11-08 reference-framework pass (docs/REFERENCE_FRAMEWORK.md): L01 is the first level framed by
        // ReferenceFraming (fixed zoom from the animal's size, launcher at 0.26 sw, ground at 0.12 sh). The pile
        // and robot moved onto World 1's shared ground line y=-5.6 (where the barn/fences/cannon stand): all four
        // bales +0.5685 so the lowest bale's collider bottom touches -5.6 (pile shape unchanged); the robot's
        // 0.90-tall body now stands on it (centre -5.15; it was 0.34 above the bales' line).
        // 2026-11-08 cornfield pass (user: "the robot on a ledge doesn't seem right"): rebuilt as a first
        // Angry-Birds-style lesson on the ground — a wooden shelter (two posts and a roof plank) with one robot
        // sheltering inside and one standing on the roof, plus a haybale stack beside it. Placed by the planks'
        // VISIBLE edges (the sprites carry transparent padding that their colliders include):
        //   2D_Block_Wood_Vertical: art spans x 0.32-0.59, y 0.05-0.92 (from the top) of the footprint
        //   Plank_Horizontal:       art spans x 0.08-0.98, y 0.33-0.81
        // Posts 1.4 u tall (visible), roof top 1.65 u above the ground; 12-13 u from the cannon (~0.45 sw).
        const float g = ReferenceFraming.World1GroundY;          // -5.6
        const float postH = 1.61f;                               // footprint -> 1.40 visible
        const float postY = g - 0.08f * postH + postH * 0.5f;    // visible bottom on the ground
        const float postTop = g + 0.87f * postH;                 // visible top
        const float roofH = 0.52f;                               // footprint -> 0.25 visible
        const float roofY = postTop - 0.19f * roofH + roofH * 0.5f;
        const float roofTop = postTop + 0.48f * roofH;
        Make(folder, "L01_FirstContact",
            id: "W1_L01", name: "First Contact", par: 2,
            birds: new[] { AnimalType.Cluck, AnimalType.Cluck, AnimalType.Cluck },
            blocks: new[]
            {
                B(BlockType.Wood, 8.2f,   postY, 1.0f,  postH, artVariant: WoodArtVariant.Vertical),
                B(BlockType.Wood, 9.8f,   postY, 1.0f,  postH, artVariant: WoodArtVariant.Vertical),
                B(BlockType.Wood, 8.927f, roofY, 2.44f, roofH, artVariant: WoodArtVariant.Horizontal),
                B(BlockType.Haybale, 10.70f, -5.1115f, 0.977f, 0.977f, passThrough: true, hp: 10f, mass: 3f),
                B(BlockType.Haybale, 11.70f, -5.1115f, 0.977f, 0.977f, passThrough: true, hp: 10f, mass: 3f),
                B(BlockType.Haybale, 11.20f, -4.31f,   0.977f, 0.977f, passThrough: true, hp: 10f, mass: 3f),
            },
            robots: new[]
            {
                R(9.0f, g + 0.45f,       6.001f, 6.153f, RobotType.SemiHarvester),   // inside the shelter
                R(9.0f, roofTop + 0.45f, 6.001f, 6.153f, RobotType.SemiHarvester),   // on the roof
            },
            referenceFraming: true);

        // ── W1_L02 - W1_L18 ──────────────────────────────────────────────────
        // Rebuilt 2026-11-08 for the reference framing (structures on the ground, raised islands and mounts, laid
        // out by visible edges). Designed in tools/design_world1.py, which writes LevelDataGenerator.World1.cs.
        MakeWorld1Rebuilt(folder);

        // ── World 2 (Frozen Tundra) W2_L01 - W2_L22 ─────────────────────────────────────────────────────
        // Rebuilt 2026-11-09 on the World 1 template (framed, live physics, frozen mounts/contraptions, the Frost
        // Tyrant boss). Designed in tools/design_world2.py, which writes LevelDataGenerator.World2.cs. Level 1 keeps
        // its L19_FrostboundWatch file and W2_L01 id (save progress is keyed by levelId); the rest keep W2_LNN ids.
        MakeWorld2Framed(folder);
        MakeWorld3Framed(folder);   // World 3 Watermill Village (tools/design_world3.py, 2026-11-10)

        // Worlds 3-9 (2026-11-02): built from the World 1 towers above — see WorldLevelGenerator (World 2 is designed).
        WorldLevelGenerator.GenerateAll();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        if (!silent)
            EditorUtility.DisplayDialog("FarmFury",
                "Generated every LevelData asset in\nAssets/ScriptableObjects/Levels\n(L01-L19 + Worlds 2-9)", "OK");
        Debug.Log("[FarmFury] Level data generation complete.");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    // Deletes a previously-generated LevelData asset that no longer has a Make(...) call —
    // see the L03-L06 removal comment above for why this exists.
    static void DeleteStaleAsset(string folder, string filename)
    {
        string path = $"{folder}/{filename}.asset";
        if (AssetDatabase.LoadAssetAtPath<LevelData>(path) != null)
        {
            AssetDatabase.DeleteAsset(path);
            Debug.Log($"[FarmFury] Deleted stale level asset {path}");
        }
    }

    static void Make(string folder, string filename,
        string id, string name, int par,
        AnimalType[] birds,
        LevelData.BlockSpawnData[] blocks,
        LevelData.RobotSpawnData[] robots,
        int world = 1,
        bool isBoss = false,
        bool referenceFraming = false,
        LevelData.PlatformSpawnData[] platforms = null,
        LevelWeather weather = LevelWeather.Golden,
        LevelData.GadgetSpawnData[] gadgets = null)
    {
        string path = $"{folder}/{filename}.asset";
        if (AssetDatabase.LoadAssetAtPath<LevelData>(path) != null)
            AssetDatabase.DeleteAsset(path);

        var asset         = ScriptableObject.CreateInstance<LevelData>();
        asset.levelId     = id;
        asset.levelName   = name;
        asset.parBirds    = par;
        asset.birds       = birds;
        asset.blocks      = blocks;
        asset.robots      = robots;
        asset.world       = world;
        asset.isBossLevel = isBoss;
        asset.referenceFraming = referenceFraming;
        asset.platforms   = platforms ?? new LevelData.PlatformSpawnData[0];
        asset.weather     = weather;
        asset.gadgets     = gadgets ?? new LevelData.GadgetSpawnData[0];

        AssetDatabase.CreateAsset(asset, path);
        Debug.Log($"[FarmFury] Created {path}");
    }

    static void EnsureFolder(string parent, string child)
    {
        string full = $"{parent}/{child}";
        if (!AssetDatabase.IsValidFolder(full))
            AssetDatabase.CreateFolder(parent, child);
    }

    // passThrough, hp, mass are optional overrides — 0 values mean "use BlockBase default".
    // artVariant only matters for BlockType.Wood — Auto (default) lets BlockBase guess the art
    // orientation from the w/h aspect ratio like before; LevelLayoutDumper sets it explicitly
    // when it knows the real design-time sprite (see InferWoodArtVariant in that file).
    static LevelData.BlockSpawnData B(BlockType type, float x, float y, float w, float h,
                                      bool passThrough = false, float hp = 0f, float mass = 0f,
                                      WoodArtVariant artVariant = WoodArtVariant.Auto,
                                      bool indestructible = false,
                                      bool forceStayKinematic = false) =>
        new() { type = type, position = new Vector2(x, y), size = new Vector2(w, h),
                passThrough = passThrough, healthOverride = hp, massOverride = mass,
                artVariant = artVariant, indestructible = indestructible,
                forceStayKinematic = forceStayKinematic };

    static LevelData.PlatformSpawnData P(float x, float surfaceY, float width, int variant = 0, bool rolling = false) =>
        new LevelData.PlatformSpawnData { x = x, surfaceY = surfaceY, width = width, variant = variant, rolling = rolling };

    static LevelData.GadgetSpawnData Gd(GadgetType type, float x, float y, float size, float ropeLength = 0f, bool faceLeft = false) =>
        new LevelData.GadgetSpawnData { type = type, position = new Vector2(x, y), size = size, ropeLength = ropeLength, faceLeft = faceLeft };

    static LevelData.RobotSpawnData R(float x, float y, float scaleX = 0f, float scaleY = 0f,
                                      RobotType robotType = RobotType.Basic) =>
        new() { position = new Vector2(x, y), scale = new Vector2(scaleX, scaleY), robotType = robotType };
}
