using System.Collections.Generic;
using UnityEngine;

// Rebuilds World 1 decorative scenery every time a level starts.
// All props are purely visual (no colliders).
//
// Two placement modes:
//   RNG mode (default)        — deterministic-RNG layout, same replay every time.
//   Exact mode (_useExactPlacement && levelIdx == 0) — designer positions from Canva.
//
// RNG props are bottom-anchored at GroundSurface (Y = -2.5).
// Exact props use the centre Y from the Canva-to-Unity conversion directly.
public class SceneryBuilder : MonoBehaviour
{
    [Header("Ground Clutter")]
    [SerializeField] private Sprite _sprGrassTuft;
    [SerializeField] private Sprite _sprWildFlowers;
    [SerializeField] private Sprite _sprRock;

    [Header("Farm Props")]
    [SerializeField] private Sprite _sprWoodenFence;
    [SerializeField] private Sprite _sprHaybail;
    [SerializeField] private Sprite _sprWoodenBarrel;
    [SerializeField] private Sprite _sprWoodenCart;
    [SerializeField] private Sprite _sprFarmSilo;
    [SerializeField] private Sprite _sprWindmill;       // LEVEL1_EXACT

    [Header("Trees")]
    [SerializeField] private Sprite _sprOakTree;
    [SerializeField] private Sprite _sprGnarledTree;

    [Header("Ruins & Barns")]
    [SerializeField] private Sprite _sprRuinedStoneWall;
    [SerializeField] private Sprite _sprStoneTower;
    [SerializeField] private Sprite _sprStoneWallTall;
    [SerializeField] private Sprite _sprOldBarn;
    [SerializeField] private Sprite _sprDamagedBarn;

    // LEVEL1_EXACT: tick in Inspector to use designer-authored positions for Level 1
    [Header("Exact Placement (Level 1 only)")]
    [SerializeField] private bool _useExactPlacement;

    // World 2 (Frozen Tundra) ground-seam dressing — added 2026-07-19. Scattered along the
    // ground seam where EnvironmentDepthSystem's Midground layer meets gameplay, using the same
    // RNG-scatter shape as World 1's (currently-disabled, see the early `return` in
    // BuildForLevel) fence/rock/flower placement — System.Random seeded by level index,
    // randomized scale AND rotation (rotation is new; World 1's Place() never needed it), light
    // density so gameplay stays readable. No per-level tuning yet since no real World 2 LevelData
    // exists — the X range below is a placeholder matching World 1's typical level-content span;
    // revisit once actual World 2 levels are built and their real ground-seam extents are known.
    [Header("World 2 - Ice Ground Dressing")]
    [SerializeField] private Sprite _sprIceBlock;
    [SerializeField] private Sprite _sprPackedSnow;
    [SerializeField] private Sprite _sprIcicleSpike;

    private readonly List<GameObject> _props = new();

    private const float GroundSurface = -6.60f;

    void Start()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.OnLevelStarted += OnLevelStarted;
    }

    void OnDestroy()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.OnLevelStarted -= OnLevelStarted;
    }

    void OnLevelStarted(LevelData data) =>
        BuildForLevel(GameManager.Instance?.CurrentLevelIndex ?? 0, data != null ? data.world : 1);

    // world param added 2026-07-19 (default 1 preserves every existing call site/behaviour).
    // World 2 branches to BuildIceGroundProps() below instead of falling into World 1's
    // exact-placement/disabled-RNG logic — World 1's own path (including the early `return`
    // that disables its RNG scatter) is completely untouched by this branch.
    public void BuildForLevel(int levelIdx, int world = 1)
    {
        ClearProps();

        if (world == 2)
        {
            BuildIceGroundProps(levelIdx);
            return;
        }

        // Level 1 scenery is hand-authored in the scene as Scenery_L1 GameObjects.
        // When exact placement is on, skip all code-spawning for level 0 so those
        // permanent scene objects are not overwritten.
        if (_useExactPlacement && levelIdx == 0) return;

        // RNG auto-scatter DISABLED for every level (2026-07-09, user request) — this was a
        // rough placeholder that scattered leftover World1Props art with no design intent, and
        // kept surfacing as "old sprites"/"random sprites still visible" bug reports on L02
        // once the user started hand-building each level's actual gameplay content (blocks/
        // robots) via LevelLayoutDumper without also hand-building scenery. Every level now
        // gets a clean sky/ground backdrop with nothing auto-placed, matching L01's fully
        // hand-authored approach — re-enable (delete this early return) once there's an actual
        // plan for designed, per-level RNG scenery rather than the code below's rough ranges.
        return;

        var rng = new System.Random(levelIdx * 137 + 42);

        // ── Far background ────────────────────────────────────────────────
        // FarmSilo excluded — not present in World 1 level designs
        Place(_sprOldBarn,         R(rng, 5.5f,  6.5f),  R(rng, 2.2f,  2.6f), -15);
        Place(_sprOakTree,         R(rng, -0.5f, 1.0f),  R(rng, 2.0f,  2.6f), -14);
        Place(_sprGnarledTree,     R(rng, 11.5f, 14.5f), R(rng, 0.85f, 1.1f), -14);
        if (rng.NextDouble() > 0.45)
            Place(_sprOakTree,     R(rng, 15.0f, 18.0f), R(rng, 0.65f, 0.85f), -13);
        if (_sprStoneTower != null && rng.NextDouble() > 0.25)
            Place(_sprStoneTower,  R(rng, 11.0f, 14.0f), R(rng, 1.0f,  1.4f), -13);
        if (_sprStoneWallTall != null && rng.NextDouble() > 0.35)
            Place(_sprStoneWallTall, R(rng, 4.8f, 6.5f), R(rng, 0.72f, 0.95f), -12);
        if (_sprDamagedBarn != null && rng.NextDouble() > 0.55)
            Place(_sprDamagedBarn, R(rng, 13.0f, 16.0f), R(rng, 0.72f, 0.95f), -15);

        // ── Fence line ────────────────────────────────────────────────────
        float[] fenceL = { -4.5f, -3.0f, -1.5f,  0.0f };
        float[] fenceR = {  4.8f,  6.3f };
        foreach (var fx in fenceL)
            Place(_sprWoodenFence, fx + R(rng, -0.15f, 0.15f), R(rng, 0.50f, 0.62f), 1);
        foreach (var fx in fenceR)
            Place(_sprWoodenFence, fx + R(rng, -0.15f, 0.15f), R(rng, 0.50f, 0.62f), 1);

        // ── Farm props ────────────────────────────────────────────────────
        Place(_sprHaybail,      R(rng, 7.5f,  8.5f),  R(rng, 0.44f, 0.54f), 1);
        Place(_sprWoodenBarrel, R(rng, 9.0f,  10.5f), R(rng, 0.32f, 0.40f), 1);
        Place(_sprWoodenCart,   R(rng, 11.0f, 13.0f), R(rng, 0.44f, 0.56f), 1);
        if (_sprRuinedStoneWall != null)
            Place(_sprRuinedStoneWall, R(rng, 1.3f, 2.0f), R(rng, 0.50f, 0.65f), 0);

        // ── Ground clutter ────────────────────────────────────────────────
        PlaceMany(_sprGrassTuft,   rng, -5.0f, 0.5f,  9, 0.26f, 0.44f, 1);
        PlaceMany(_sprGrassTuft,   rng,  5.0f, 9.0f,  6, 0.24f, 0.40f, 1);
        PlaceMany(_sprWildFlowers, rng, -4.5f, 0.5f,  5, 0.24f, 0.36f, 1);
        PlaceMany(_sprWildFlowers, rng,  5.0f, 8.5f,  3, 0.22f, 0.32f, 1);
        PlaceMany(_sprRock,        rng, -5.5f, -3.0f, 4, 0.22f, 0.32f, 1);
    }

    // ── LEVEL1_EXACT ───────────────────────────────────────────────────────────
    // All positions converted from Canva 1275×720px canvas layout.
    // Formula: X  = (CanvaX + W/2 - 637.5) / 100
    //           Y  = (360 - CanvaY - H/2)   / 100
    //          Sx  = W / 100
    //          Sy  = H / 100
    // Z depth drives rendering order and parallax layering.
    void BuildLevel1Exact() // LEVEL1_EXACT
    {
        // ── Far background (large art, deep Z, slow parallax) ────────────────

        // OldBarn_Right: CanvaX=-23  Y=352  W=477  H=435
        var barnGo = PlaceExact(_sprOldBarn,        -1.225f, -0.150f, -5f, 4.77f, 4.35f, -15); // LEVEL1_EXACT
        AddParallax(barnGo, 0.3f);                                                               // LEVEL1_EXACT

        // Oak_Tree: CanvaX=133  Y=285  W=434  H=434
        var oakGo = PlaceExact(_sprOakTree,         -4.205f, -0.595f, -4f, 4.34f, 4.34f, -14); // LEVEL1_EXACT
        AddParallax(oakGo, 0.4f);                                                                // LEVEL1_EXACT

        // Gnarled_Tree: CanvaX=1065  Y=491  W=236  H=236
        var gnarledGo = PlaceExact(_sprGnarledTree,  4.275f, -1.435f, -3f, 2.36f, 2.36f, -13); // LEVEL1_EXACT
        AddParallax(gnarledGo, 0.6f);                                                            // LEVEL1_EXACT

        // Windmill: CanvaX=680  Y=585  W=107  H=107
        var millGo = PlaceExact(_sprWindmill,         0.935f, -2.600f, -2f, 1.07f, 1.07f, -12); // LEVEL1_EXACT
        AddParallax(millGo, 0.5f);                                                                // LEVEL1_EXACT

        // ── Fence line (perspective scale: small left → large right) ─────────

        // Wooden_Fence(1): CanvaX=544  Y=643  W=52  H=52
        var f1 = PlaceExact(_sprWoodenFence, -0.635f, -3.045f, -1f, 0.52f, 0.52f, 1); // LEVEL1_EXACT
        AddParallax(f1, 0.7f);                                                          // LEVEL1_EXACT

        // Wooden_Fence(2): CanvaX=596  Y=635  W=75  H=75
        var f2 = PlaceExact(_sprWoodenFence, -0.250f, -2.965f, -1f, 0.75f, 0.75f, 1); // LEVEL1_EXACT
        AddParallax(f2, 0.7f);                                                          // LEVEL1_EXACT

        // Wooden_Fence(3): CanvaX=672  Y=629  W=90  H=90
        var f3 = PlaceExact(_sprWoodenFence,  0.425f, -2.900f, -1f, 0.90f, 0.90f, 1); // LEVEL1_EXACT
        AddParallax(f3, 0.7f);                                                          // LEVEL1_EXACT

        // Wooden_Fence(4): CanvaX=765  Y=621  W=105  H=105
        var f4 = PlaceExact(_sprWoodenFence,  1.300f, -2.835f, -1f, 1.05f, 1.05f, 1); // LEVEL1_EXACT
        AddParallax(f4, 0.7f);                                                          // LEVEL1_EXACT

        // ── Ground clutter (Z=0, no parallax) ────────────────────────────────

        // Rock: CanvaX=378  Y=629  W=66  H=66
        PlaceExact(_sprRock,        -0.835f, -2.960f, 0f, 0.66f, 0.66f, 1); // LEVEL1_EXACT

        // WildFlowers: CanvaX=519  Y=660  W=33  H=33
        PlaceExact(_sprWildFlowers, -0.955f, -3.165f, 0f, 0.33f, 0.33f, 1); // LEVEL1_EXACT

        // Grass_Tuft(1): CanvaX=17   Y=619  W=57  H=57
        PlaceExact(_sprGrassTuft,   -6.090f, -3.015f, 0f, 0.57f, 0.57f, 1); // LEVEL1_EXACT

        // Grass_Tuft(2): CanvaX=167  Y=654  W=57  H=57
        PlaceExact(_sprGrassTuft,   -4.525f, -3.290f, 0f, 0.57f, 0.57f, 1); // LEVEL1_EXACT

        // Grass_Tuft(3): CanvaX=656  Y=674  W=30  H=30
        PlaceExact(_sprGrassTuft,   -0.225f, -3.490f, 0f, 0.30f, 0.30f, 1); // LEVEL1_EXACT
    }

    // Places one prop at exact world-space centre (X, Y, Z).
    // sx/sy are DESIRED WORLD SIZES in Unity units (from Canva: W/100, H/100).
    // localScale is back-calculated from the sprite's native size so the formula works
    // regardless of PPU or source texture dimensions.
    // Returns the GO so the caller can attach components (e.g. ParallaxScroller).
    GameObject PlaceExact(Sprite sprite,                                        // LEVEL1_EXACT
                          float x, float y, float z,
                          float sx, float sy, int sortOrder)
    {
        if (sprite == null) return null;

        // native size in Unity units (depends on PPU and trimmed pixel rect)
        float nativeW = sprite.rect.width  / sprite.pixelsPerUnit;
        float nativeH = sprite.rect.height / sprite.pixelsPerUnit;
        float scaleX  = nativeW > 0.001f ? sx / nativeW : sx;
        float scaleY  = nativeH > 0.001f ? sy / nativeH : sy;

        var go = new GameObject("Prop_" + sprite.name);
        go.transform.SetParent(transform);
        go.transform.position   = new Vector3(x, y, z);
        go.transform.localScale = new Vector3(scaleX, scaleY, 1f);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite       = sprite;
        sr.sortingOrder = sortOrder;

        _props.Add(go);
        return go;
    }

    // Attaches a ParallaxScroller with the given camera-follow fraction.
    // speed=0 → stays at world position; speed=1 → moves exactly with camera.
    static void AddParallax(GameObject go, float speed) // LEVEL1_EXACT
    {
        if (go == null) return;
        go.AddComponent<ParallaxScroller>().speed = speed;
    }

    // ── World 2: Ice ground-seam dressing ────────────────────────────────────
    // Same seed formula as World 1's (disabled) RNG scatter above, so layouts stay deterministic
    // per level index. Light density (8 props spread across the placeholder X range) deliberately
    // sparser than World 1's old scatter counts, per the "light density so gameplay stays
    // readable" spec — this is ground-seam dressing, not a wall of props between the cannon and
    // the structures.
    void BuildIceGroundProps(int levelIdx)
    {
        var rng = new System.Random(levelIdx * 137 + 42);
        Sprite[] props = { _sprIceBlock, _sprPackedSnow, _sprIcicleSpike };

        const int   count = 8;
        const float xMin  = -1.0f;
        const float xMax  = 9.0f;
        for (int i = 0; i < count; i++)
        {
            Sprite s = props[rng.Next(props.Length)];
            float x        = R(rng, xMin, xMax);
            float scale    = R(rng, 0.22f, 0.42f);
            float rotation = R(rng, -8f, 8f); // slight tilt only — a prop rotated far off-axis
                                               // would read as floating/wrong rather than "scattered ice debris"
            PlaceRotated(s, x, scale, rotation, 1);
        }
    }

    // Same ground-anchor math as Place() below, plus a Z rotation — World 1's Place() never
    // needed rotation, so this is a separate helper rather than a param added to every existing
    // W1 call site.
    void PlaceRotated(Sprite sprite, float worldX, float scale, float rotationDeg, int sortOrder)
    {
        if (sprite == null) return;

        var go = new GameObject("Prop_" + sprite.name);
        go.transform.SetParent(transform);
        go.transform.localScale = new Vector3(scale, scale, 1f);
        go.transform.rotation  = Quaternion.Euler(0f, 0f, rotationDeg);

        float pivotH = sprite.pivot.y / sprite.pixelsPerUnit;
        go.transform.position = new Vector3(worldX, GroundSurface + pivotH * scale, 0f);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite       = sprite;
        sr.sortingOrder = sortOrder;

        _props.Add(go);
    }

    // ── RNG helpers ───────────────────────────────────────────────────────────

    void PlaceMany(Sprite s, System.Random rng, float xMin, float xMax,
                   int count, float scMin, float scMax, int order)
    {
        for (int i = 0; i < count; i++)
            Place(s, R(rng, xMin, xMax), R(rng, scMin, scMax), order);
    }

    // Places one prop bottom-anchored at GroundSurface using a single uniform scale.
    void Place(Sprite sprite, float worldX, float scale, int sortOrder)
    {
        if (sprite == null) return;

        var go = new GameObject("Prop_" + sprite.name);
        go.transform.SetParent(transform);
        go.transform.localScale = new Vector3(scale, scale, 1f);

        float pivotH = sprite.pivot.y / sprite.pixelsPerUnit;
        go.transform.position = new Vector3(worldX, GroundSurface + pivotH * scale, 0f);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite       = sprite;
        sr.sortingOrder = sortOrder;

        _props.Add(go);
    }

    void ClearProps()
    {
        foreach (var g in _props) if (g != null) Destroy(g);
        _props.Clear();
    }

    static float R(System.Random rng, float min, float max) =>
        min + (float)(rng.NextDouble() * (max - min));
}
