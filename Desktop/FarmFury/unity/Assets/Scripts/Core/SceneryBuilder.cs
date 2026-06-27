using System.Collections.Generic;
using UnityEngine;

// Rebuilds World 1 decorative scenery every time a level starts.
// All props are purely visual (no colliders). The same levelIndex always produces
// the same layout — deterministic seed, so the scene is consistent on replays.
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

    [Header("Trees")]
    [SerializeField] private Sprite _sprOakTree;
    [SerializeField] private Sprite _sprGnarledTree;

    private readonly List<GameObject> _props = new();

    // Subscribe in Start() (not OnEnable) so GameManager.Instance is guaranteed to exist.
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

    void OnLevelStarted(LevelData _) =>
        BuildForLevel(GameManager.Instance?.CurrentLevelIndex ?? 0);

    public void BuildForLevel(int levelIdx)
    {
        ClearProps();
        var rng = new System.Random(levelIdx * 137 + 42);

        // ── Far background — trees + silo ────────────────────────────────────
        Place(_sprFarmSilo,     5.5f,                         1.40f, -15);
        Place(_sprOakTree,      R(rng, 7.0f,  8.5f),          R(rng, 1.0f, 1.3f), -14);
        Place(_sprGnarledTree,  R(rng, 22.5f, 24.5f),         R(rng, 0.9f, 1.2f), -14);
        if (rng.NextDouble() > 0.35)
            Place(_sprOakTree,  R(rng, 21.0f, 23.0f),         R(rng, 0.85f, 1.1f), -14);

        // ── Fence line — left approach + right of structures ──────────────────
        float[] fL = { 7.2f, 8.7f, 10.2f };
        float[] fR = { 20.2f, 21.7f };
        foreach (var fx in fL) Place(_sprWoodenFence, fx + R(rng, -0.15f, 0.15f), R(rng, 0.50f, 0.60f), 1);
        foreach (var fx in fR) Place(_sprWoodenFence, fx + R(rng, -0.15f, 0.15f), R(rng, 0.50f, 0.60f), 1);

        // ── Farm props around the play zone ───────────────────────────────────
        Place(_sprHaybail,     12.6f + R(rng, -0.2f, 0.2f),  R(rng, 0.44f, 0.54f), 1);
        Place(_sprWoodenBarrel,13.2f + R(rng, -0.3f, 0.3f),  R(rng, 0.32f, 0.40f), 1);
        Place(_sprWoodenCart,  21.0f + R(rng, -0.3f, 0.3f),  R(rng, 0.44f, 0.56f), 1);

        // ── Ground clutter: grass tufts ───────────────────────────────────────
        // Bands: far-left, between launcher & structures, right of structures
        // sortingOrder=1 keeps props BEHIND blocks (order=2) and robots (order=3).
        // Avoid x=14–20 (structure zone) so props never visually cover gameplay elements.
        PlaceMany(_sprGrassTuft,  rng,  5.0f, 13.5f, 10, 0.26f, 0.42f, 1);
        PlaceMany(_sprGrassTuft,  rng, 20.5f, 26.0f,  5, 0.26f, 0.38f, 1);

        // Wild flowers — left of launcher only
        PlaceMany(_sprWildFlowers, rng, 5.5f, 12.5f, 5, 0.24f, 0.36f, 1);

        // Rocks
        PlaceMany(_sprRock, rng, 7.0f, 13.0f, 4, 0.22f, 0.32f, 1);
    }

    // Place several props randomly within [xMin, xMax], varying scale.
    void PlaceMany(Sprite s, System.Random rng, float xMin, float xMax,
                   int count, float scMin, float scMax, int order)
    {
        for (int i = 0; i < count; i++)
            Place(s, R(rng, xMin, xMax), R(rng, scMin, scMax), order);
    }

    // Place one decorative prop, bottom-anchored to the ground surface (Y=0).
    // sortOrder: negative = far back, 1 = on the grass, 2 = in front of grass clutter.
    void Place(Sprite sprite, float worldX, float scale, int sortOrder)
    {
        if (sprite == null) return;

        var go = new GameObject("Prop_" + sprite.name);
        go.transform.SetParent(transform);
        go.transform.localScale = new Vector3(scale, scale, 1f);

        // Centre-pivot sprite: pivot.y / PPU = distance from canvas-bottom to pivot in world units.
        // Lifting the GO by that amount places the canvas bottom (and thus the art bottom) at Y=0.
        float pivotH = sprite.pivot.y / sprite.pixelsPerUnit;
        go.transform.position = new Vector3(worldX, pivotH * scale, 0f);

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
