// FarmFury — Editor utility. Run via menu: FarmFury ▶ Generate All Level Data
// Coordinate system (current, post-rebuild): ground surface Y = -6.60, launcher X = -2.327.
// Block/robot positions are raw world-space — LevelLoader applies no offset at spawn time.
//
// L01, L02, and L03 exist below as of 2026-07-10 — L04-L06 remain removed (user request 2026-07-09):
// they were auto-generated placeholder layouts (originally on the OLD pre-rebuild coordinate system,
// ground Y=-2.5/launcher X=-5.5, then just rigid-translated onto the current system by a fixed
// delta dx=+3.173/dy=-4.10 — never actually hand-built/verified level designs). Going forward,
// every level is built individually via LevelLayoutDumper (drag real content into the Scene
// view, dump to code — see that file's header comment), the same way L02 "Harvest Yard" and L03
// "The Tower" were built, replacing the old auto-generated stand-ins. See DeleteStaleAsset below
// for how old generated assets get cleaned up when a level's Make(...) call is removed.

using UnityEngine;
using UnityEditor;

public static class LevelDataGenerator
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
        Make(folder, "L01_FirstContact",
            id: "W1_L01", name: "First Contact", par: 2,
            birds: new[] { AnimalType.Cluck, AnimalType.Cluck, AnimalType.Cluck },
            blocks: new[]
            {
                B(BlockType.Haybale, 3.6462f, -5.180f, 1.0f, 0.9f, passThrough: true, hp: 10f, mass: 3f),
                B(BlockType.Haybale, 4.3098f, -5.235f, 1.0f, 0.9f, passThrough: true, hp: 10f, mass: 3f),
                B(BlockType.Haybale, 3.85f,   -5.281f, 1.0f, 0.9f, passThrough: true, hp: 10f, mass: 3f),
                // Top ("cap") bale — lowered 0.35u and recentred over the base row's mean X
                // (2026-07-06, user-reported "still too high and off the mark"). Was sitting a
                // full bale-height above a single base bale (a valid but precarious "balanced on
                // one point" stack); now nests down into the row for a squatter, more obviously-
                // connected pile. Re-check against a fresh screenshot after this change.
                B(BlockType.Haybale, 3.935f,  -4.85f, 1.0f, 0.9f, passThrough: true, hp: 10f, mass: 3f),
            },
            robots: new[]
            {
                R(5.7f, -5.36f, 5.7065f, 7.009f, RobotType.Harvester), // sheltering behind the hay
            });

        // ── W1_L02  Harvest Yard ──────────────────────────────────────────────
        // Rebuilt 2026-07-09 by the user — designed by dragging raw sprites (Haybail,
        // Plank_Horizontal, HarvesterRobot, Robot_SemiHarvest) into the Scene view under a
        // LevelScratch container and running FarmFury -> Debug -> Dump Level Layout To Log (see
        // LevelLayoutDumper.cs), which converted the placed transforms directly into the B()/R()
        // calls below — replaces the old wooden-palisade "Stone Wall" design entirely. Introduces
        // RobotType.SemiHarvester (Robot_SemiHarvest.png — see LevelData.cs/SceneSetup.cs's
        // EnsureSemiHarvesterRobotPrefab()).
        Make(folder, "L02_StoneWall",
            id: "W1_L02", name: "Harvest Yard", par: 2,
            birds: new[] { AnimalType.Cluck, AnimalType.Cluck, AnimalType.Cluck },
            blocks: new[]
            {
                B(BlockType.Haybale, 4.224f, -5.563f, 1f, 1f, passThrough: true, hp: 10f, mass: 3f),
                B(BlockType.Haybale, 4.964f, -5.583f, 1f, 1f, passThrough: true, hp: 10f, mass: 3f),
                B(BlockType.Haybale, 5.744f, -5.623f, 1f, 1f, passThrough: true, hp: 10f, mass: 3f),
                // Reverted to plain 1x1 squares 2026-07-10 (user-reported the resized/retagged
                // 1.3x0.64 version had "moved off original positioning" and still wasn't
                // rendering as desired) — back to the original coordinates. Visibility is now
                // fixed at the source instead: WoodBlock._sprNormal itself was re-pointed from
                // the poorly-filling 2D_Block_Wood_Flat.png to Plank_Horizontal.png (see
                // SceneSetup.WireBlockSprites), and a real collider-vs-visual mismatch bug
                // (BlockBase.cs, same day) is fixed too — every wood block's hitbox now
                // correctly matches its requested footprint regardless of the chosen sprite's
                // native aspect, which likely explains the "not active when hit" symptom.
                B(BlockType.Wood,    4.444f, -5.233f, 1f, 1f),
                B(BlockType.Wood,    5.344f, -5.243f, 1f, 1f),
            },
            robots: new[]
            {
                R(5.064f, -4.593f, 4.269f, 5.649f, RobotType.Harvester),
                // Scale corrected 2026-07-09 (was 4.96,4.251, user-reported "renders noticeably
                // smaller than the Harvester") — Robot_SemiHarvest.png's auto-trimmed sprite
                // rect is only 215x273px out of its 500x500 canvas (lots of padding around the
                // character), vs HarvesterRobot.png's 495x305px out of 612x408 (fills most of
                // its canvas). At the same PPU (1746) and the dumped scale, SemiHarvester was
                // rendering at roughly half the Harvester's world height. This scale is
                // computed to match Harvester's ~0.987u world height exactly; not yet visually
                // re-verified in the Editor (no Play-mode access here) — worth a live check.
                R(6.724f, -5.583f, 7.37f, 6.31f, RobotType.SemiHarvester),
            });

        // ── W1_L03  ────────────────────────────────────────────────────────────
        // Built 2026-07-10 via LevelLayoutDumper (same workflow as L02 "Harvest Yard" — drag
        // real prefabs/sprites into the Scene view, run FarmFury -> Debug -> Dump Level Layout
        // To Log, paste the result here). Still Cluck-only — Bessie and Cluck's own ability
        // don't unlock until L05 (user decision 2026-07-10), so birds[] stays 3x Cluck like
        // L01/L02. Robots are 1x Harvester + 2x SemiHarvester, so the match-up screen's existing
        // "second distinct RobotType" logic (MatchUpScreen.cs ~line 189) automatically shows
        // both the Harvester and SemiHarvester cards, same as L02 — no MatchUpScreen changes
        // needed for this level.
        //
        // Wood block sizes corrected 2026-07-10 (user-reported "planks render smaller than my
        // scene layout", also present in L02): the original dump recorded each raw design-time
        // sprite's untouched transform.localScale (1,1) rather than its actual rendered
        // world-space footprint, so non-square art (e.g. Plank_2DShork at 250x266px, i.e.
        // visually taller than wide) got silently flattened to a 1x1 "size" value. Fixed at the
        // root in LevelLayoutDumper.cs (now records SpriteRenderer.bounds.size) and
        // BlockBase.Initialise() (now scales relative to the chosen sprite's real native bounds
        // instead of assuming every wired sprite is exactly 1x1 at scale 1 — previously only
        // true along the X axis per WireBlockPrefab's "PPU = texture width" convention). The 4
        // blocks below sourced from 'Plank_2DShork' (see the original dump's inline comments)
        // are updated to that sprite's true footprint (250x266px / 250 PPU = 1.0 x 1.064); the
        // 2 sourced from '2D_Block_Wood_Flat' (500x500px, already square) needed no change.
        //
        // Wood ART (not just size) corrected 2026-07-10 (user-reported, second pass — "the wood
        // is not correct"): Plank_2DShork.png is visually a VERTICAL plank bundle, but its
        // measured w/h footprint (1.0 x 1.064) is nearly square, so BlockBase's old aspect-ratio
        // guess put it in the "flat/normal" bucket — every wood block in this level rendered
        // with the same flat 2D_Block_Wood_Flat.png art regardless of which sprite was actually
        // placed. Fixed at the root via a new explicit WoodArtVariant field on BlockSpawnData
        // (see LevelData.cs/BlockBase.Initialise) that LevelLayoutDumper now sets directly from
        // the design-time sprite's filename instead of leaving it to aspect-guessing — the 4
        // Plank_2DShork blocks below are tagged Vertical accordingly.
        //
        // The 'WoodenBarrel'-sourced block is now BlockType.Barrel (ExplodingBarrelBlock,
        // WoodenBarrel.png art, area-damage-on-death) instead of a plain Wood plank — added
        // 2026-07-10 per user request to "gradually introduce the exploding barrel" starting here.
        //
        // Layout compacted 2026-07-10 (user-reported: "move all sprites closer together, the
        // barrel is rendering off the safe area"): the original dump spanned X 4.16-8.54 (4.38
        // units), well past L01/L02's established safe X range (L02's widest content, the second
        // SemiHarvester, only reaches X=6.72) — the barrel at X=8.54 was rendering past the
        // camera's visible/safe-area edge. Every block/robot X below is uniformly compressed
        // toward the leftmost haybale (pivot X=4.16, factor 0.648) so the whole structure now
        // spans X 4.16-7.0, matching L02's known-good footprint; Y values are untouched. A pure
        // linear compression only shrinks gaps between objects, never widens them, so whatever
        // physical relationships existed in the original layout (robots resting near/on the wood
        // stack) are preserved or made tighter, not broken. Not yet visually re-verified in the
        // Editor (no Play-mode access here) — worth a live check, this was computed from the
        // recorded coordinates, not eyeballed against the actual rendered scene.
        //
        // Barrel nudged further right 2026-07-10 (user-reported, same-day follow-up: "move the
        // barrel slightly away from the Semi_harvester, they are now on top of each other" —
        // the compression above brought them too close together). 6.998 -> 7.4 opens a bigger
        // gap from the second SemiHarvester (X=6.507) without re-introducing the original
        // off-safe-area problem this same compaction pass fixed.
        Make(folder, "L03_TheTower",
            id: "W1_L03", name: "The Tower", par: 2,
            birds: new[] { AnimalType.Cluck, AnimalType.Cluck, AnimalType.Cluck },
            blocks: new[]
            {
                B(BlockType.Haybale, 4.16f, -5.39f, 1f, 1f, passThrough: true, hp: 10f, mass: 3f),
                B(BlockType.Haybale, 4.549f, -5.4f,  1f, 1f, passThrough: true, hp: 10f, mass: 3f),
                B(BlockType.Haybale, 4.944f, -5.44f, 1f, 1f, passThrough: true, hp: 10f, mass: 3f),
                B(BlockType.Wood,    5.326f, -5.29f, 1f, 1.064f, artVariant: WoodArtVariant.Vertical),
                B(BlockType.Wood,    5.145f, -5.13f, 1f, 1.064f, artVariant: WoodArtVariant.Vertical),
                B(BlockType.Wood,    5.488f, -5.39f, 1f, 1.064f, artVariant: WoodArtVariant.Vertical),
                B(BlockType.Wood,    5.650f, -5.54f, 1f, 1.064f, artVariant: WoodArtVariant.Vertical),
                // Reverted to plain 1x1 squares at their original Y 2026-07-10 (user-reported
                // the resized/retagged 1.3x0.64 version had "moved off original positioning" and
                // still wasn't rendering as desired) — see L02's Make() call above for the fix
                // that replaces it: WoodBlock._sprNormal re-pointed to Plank_Horizontal.png plus
                // a real collider-vs-visual mismatch bug fixed in BlockBase.cs.
                B(BlockType.Wood,    4.205f, -4.94f, 1f, 1f),
                B(BlockType.Wood,    4.737f, -4.94f, 1f, 1f),
                B(BlockType.Barrel,  7.4f,   -5.42f, 1f, 1f), // exploding barrel — see note above
            },
            robots: new[]
            {
                R(4.503f, -4.08f,  5.367f, 7.738f, RobotType.Harvester),
                R(6.022f, -5.334f, 5.887f, 5.53f,  RobotType.SemiHarvester),
                R(6.507f, -5.34f,  5.836f, 5.683f, RobotType.SemiHarvester),
            });

        // ── W1_L04  ────────────────────────────────────────────────────────────
        // Built 2026-07-10 via LevelLayoutDumper (same workflow as L02/L03). Introduces Cluck's
        // Cluster Bomb ability (egg shower) as a GDD milestone — the ability itself has always
        // been usable on any Cluck (tap mid-flight, see AnimalBase.Update()/CluckAnimal.
        // TriggerAbility()), so no code gate exists; L04 is simply the first level with enough
        // robots (4 — 2 Harvester + 2 SemiHarvester) that using it well actually matters. Same
        // day, the ability itself was reworked per user request: eggs now use real Egg.png art
        // at a visible scale (previously unwired/invisible) and fire in a fixed forward-and-down
        // cone ("like being fired from a cannon") instead of a spread centred on Cluck's exact
        // in-flight velocity angle — see CluckAnimal.cs/SceneSetup.EnsureEggPrefab() same-day
        // comments. birds[] stays 3x Cluck (Bessie isn't introduced until a later level).
        //
        // Layout compacted 2026-07-10 (same precaution as L03 — the raw dump spanned X 3.8-8.27,
        // 4.47 units, wider than any previously-verified-safe level): uniformly compressed toward
        // the leftmost wood plank (pivot X=3.8, factor 0.805), landing at X 3.8-7.4 to match L03's
        // own established safe right edge. Not yet visually verified in the Editor (no Play-mode
        // access here) — worth a live check, particularly since this is a denser 4-robot layout
        // than any level built so far.
        Make(folder, "L04_EggPractice",
            id: "W1_L04", name: "Egg Practice", par: 2,
            birds: new[] { AnimalType.Cluck, AnimalType.Cluck, AnimalType.Cluck },
            blocks: new[]
            {
                B(BlockType.Haybale, 6.77f, -5.41f, 0.977f, 0.977f, passThrough: true, hp: 10f, mass: 3f),
                B(BlockType.Haybale, 7.08f, -5.51f, 0.977f, 0.977f, passThrough: true, hp: 10f, mass: 3f),
                B(BlockType.Haybale, 7.40f, -5.58f, 0.977f, 0.977f, passThrough: true, hp: 10f, mass: 3f),
                B(BlockType.Haybale, 7.15f, -4.91f, 0.977f, 0.977f, passThrough: true, hp: 10f, mass: 3f),
                B(BlockType.Wood,    3.80f, -5.40f, 1f, 1.064f, artVariant: WoodArtVariant.Vertical),
                B(BlockType.Wood,    3.98f, -5.53f, 1f, 1.064f, artVariant: WoodArtVariant.Vertical),
                B(BlockType.Wood,    4.16f, -5.70f, 1f, 1.064f, artVariant: WoodArtVariant.Vertical),
                B(BlockType.Barrel,  5.88f, -5.49f, 1.017f, 1.083f),
                B(BlockType.Wood,    4.35f, -4.82f, 1f, 1f, artVariant: WoodArtVariant.Horizontal),
                B(BlockType.Wood,    4.44f, -4.97f, 1f, 1f, artVariant: WoodArtVariant.Horizontal),
                B(BlockType.Wood,    5.05f, -4.77f, 1f, 1f, artVariant: WoodArtVariant.Horizontal),
                B(BlockType.Wood,    5.76f, -4.78f, 1f, 1f, artVariant: WoodArtVariant.Horizontal),
                B(BlockType.Wood,    5.77f, -4.98f, 1f, 1f, artVariant: WoodArtVariant.Horizontal),
                B(BlockType.Wood,    5.10f, -4.97f, 1f, 1f, artVariant: WoodArtVariant.Horizontal),
            },
            robots: new[]
            {
                R(7.12f, -4.32f, 4.588f, 4.588f, RobotType.SemiHarvester),
                R(6.44f, -5.46f, 4.821f, 4.867f, RobotType.SemiHarvester),
                R(4.97f, -5.46f, 4.845f, 6.025f, RobotType.Harvester),
                R(4.97f, -4.14f, 4.845f, 6.025f, RobotType.Harvester),
            });

        // L05-L06 REMOVED 2026-07-09 (user request) — these were never actually built by the
        // user; they were auto-generated placeholder layouts (originally on the old pre-rebuild
        // coordinate system, then just rigid-translated onto the current one by delta, never
        // redesigned from scratch). User is now hand-building every level individually via
        // LevelLayoutDumper (drag real content into the Scene view, dump to code) the same way
        // L02/L03/L04 were built — auto-generated stand-ins for unbuilt levels are exactly the
        // "random sprites" that read as bugs when a real level should be there instead.
        // DeleteStaleAsset calls below remove any leftover .asset files from earlier runs of
        // this generator so GameManager (which loads EVERY LevelData asset found in this folder,
        // regardless of what this method currently generates) can't pick them back up. Add a new
        // Make(folder, "L05_...", ...) call here once L05 is built.
        DeleteStaleAsset(folder, "L05_TheFortress");
        DeleteStaleAsset(folder, "L06_BessiesDebut");

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        if (!silent)
            EditorUtility.DisplayDialog("FarmFury",
                "Generated 4 LevelData assets in\nAssets/ScriptableObjects/Levels\n(L05-L06 removed — build these individually)", "OK");
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
        LevelData.RobotSpawnData[] robots)
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
                                      WoodArtVariant artVariant = WoodArtVariant.Auto) =>
        new() { type = type, position = new Vector2(x, y), size = new Vector2(w, h),
                passThrough = passThrough, healthOverride = hp, massOverride = mass,
                artVariant = artVariant };

    static LevelData.RobotSpawnData R(float x, float y, float scaleX = 0f, float scaleY = 0f,
                                      RobotType robotType = RobotType.Basic) =>
        new() { position = new Vector2(x, y), scale = new Vector2(scaleX, scaleY), robotType = robotType };
}
