using UnityEngine;

// Persistent, non-per-level parallax backdrop stack — three full-bleed opaque paintings
// (ParallaxFarHills/Midground/Foreground.png, each a complete scene including its own sky)
// stacked like theatre flats via the FarmFury/ParallaxBandClip shader: each layer only reveals
// its own bottom [0, ClipAbove] UV band, so a nearer layer's transparent upper region lets the
// layer behind it show through. Lives once in Game.unity — built/wired at Editor time by
// SceneSetup.EnsureEnvironmentDepthSystem() (visible in Edit mode, not just Play), same
// "one persistent GO, not per-level" pattern as AudioManager/HUDController.
//
// Re-fits itself to the camera's ACTUAL current orthoSize/aspect/position every OnLevelStarted
// (RescaleToCamera(), below) — deliberately NOT frozen/hand-placed, unlike this project's earlier
// single-painting attempts at this same backdrop (see CLAUDE.md's 2026-07-18 history), because
// CatapultLauncher.ComputeOrthoSizeForLevel() clamps orthoSize to a 4.5-8.0 range that varies per
// level, and a static transform sized once against a single assumed zoom can never cover every
// level. No hardcoded size or position anywhere on any layer.
[DefaultExecutionOrder(-80)]
public class EnvironmentDepthSystem : MonoBehaviour
{
    public static EnvironmentDepthSystem Instance { get; private set; }

    [SerializeField] Sprite _sprFarHills;
    [SerializeField] Sprite _sprMidground;
    [SerializeField] Sprite _sprForeground;

    // World 2 (Frozen Tundra) backdrop — added 2026-07-19. Only FarHills/Midground: World 2 has
    // no opaque Foreground painting by design (per spec — the ground-seam ice/snow dressing is
    // instead a SceneryBuilder prop scatter, matching the World-1 fence/rock/flower pattern, not
    // a third full-bleed painting), so Layer_Foreground is simply deactivated whenever the
    // current level's World == 2 rather than ever being assigned World-2 art.
    [SerializeField] Sprite _sprFarHillsW2;
    [SerializeField] Sprite _sprMidgroundW2;

    // Tunable per-layer reveal bands — the fraction (0-1) of EACH painting's own local UV height
    // that stays visible before the shader clips the rest to transparent. Exposed as live
    // Inspector sliders (not baked into the shared material) specifically so the split point can
    // be re-tuned without re-exporting art. FarHills defaults to 1.0 (fully uncropped) since
    // nothing else renders behind it except Background's defensive plain-sky fallback — its own
    // painted sky fills whatever's left at the top. SceneSetup only stamps these three defaults
    // on first creation of this component — never overwritten by a later Wire Scene References
    // pass once hand-tuned.
    //
    // _midgroundClipAbove: 0.55 -> 0.23, 2026-07-18. The original 0.55 revealed part of
    // ParallaxMidground.png's OWN independently-painted sky, which visibly seamed against
    // Layer_FarHills' differently-painted sky behind it (user report, screenshot). Measured
    // directly against the source art (pixel sampling, not guessed): because the clip is a single
    // UNIFORM horizontal line, the value must clear the LOWEST point of Midground's hill-ridge
    // silhouette across its full width, not the highest peak — the ridge's deepest dip is at
    // x≈1440/2720 (row 1168/1536 ≈ v 0.24, confirmed via the bright yellow rim-light color
    // signature that marks the sky/hill boundary in this art style), so 0.23 (row ≈1180) clears it
    // with a small margin. This is LOWER than the windmill's own blade-tip height (row 1042 ≈ v
    // 0.32) — a single uniform clip line cannot keep the windmill fully visible AND guarantee zero
    // sky leakage everywhere, since the windmill pokes up higher than the ambient terrain dips
    // elsewhere in the same flattened painting. Confirmed with the user this is an accepted
    // tradeoff (the windmill's blades/roof are now clipped) pending a separate follow-up: pulling
    // the windmill out as its own standalone sprite, unaffected by this clip, once that art exists.
    [SerializeField, Range(0f, 1f)] float _farHillsClipAbove   = 1.0f;
    [SerializeField, Range(0f, 1f)] float _midgroundClipAbove  = 0.23f;
    [SerializeField, Range(0f, 1f)] float _foregroundClipAbove = 0.30f;

    // World 2's Midground (ParallaxMidGrounds_W2.png) has its own baked-in aurora sky filling
    // roughly the top 60% of the frame — same lesson as World 1's windmill fix: two independently
    // painted skies both being partially revealed shows a visible seam, so only FarHills may
    // contribute sky content. Measured directly against the source art (pixel sampling — per-
    // column scan classifying genuine open-sky purple/violet pixels vs. the terrain's navy-shadow/
    // cyan-ice palette, which share similar hue and would otherwise false-positive; cross-checked
    // against direct visual crops with row-number gridlines, same method as the W1 windmill
    // diagnostic). The single deepest sky gap — where sky reaches furthest down between two
    // flanking mountain peaks on the right side of the painting — bottoms out at row 1188 of 1536
    // (v≈0.227), confirmed pixel-precise (sky purple through row 1188, a 2-3px white rim-light
    // highlight at 1190-1193, solid terrain navy from row ~1196 on). Ceiling set to v=0.21 (row
    // ≈1213, ~25 rows/1.6% margin below the measured dip), matching W1 Midground's own margin
    // convention. FarHillsW2 defaults to 1.0 (fully uncropped, same as W1) since it's the one
    // layer meant to supply all sky content.
    [SerializeField, Range(0f, 1f)] float _farHillsClipAboveW2  = 1.0f;
    [SerializeField, Range(0f, 1f)] float _midgroundClipAboveW2 = 0.21f;

    // Which world's layers are currently active — set from LevelData.world on every
    // OnLevelStarted (see HandleLevelStarted/ApplyWorldLayers below). Defaults to World 1 so the
    // main menu / World 1 map (which loads before any level's OnLevelStarted has fired) renders
    // exactly as it always has.
    int _currentWorld = 1;

    const int FarHillsSortingOrder   = -40;
    const int MidgroundSortingOrder  = -30;
    const int ForegroundSortingOrder = -20;

    static readonly int ClipAboveID = Shader.PropertyToID("_ClipAbove");

    Camera   _cam;
    Material _bandClipMaterial;

    Transform _farHills, _midground, _foreground;

    void Awake()
    {
        Instance = this;
        _cam = Camera.main;
        if (_cam == null) _cam = FindAnyObjectByType<Camera>();

        _farHills   = MakeLayer("Layer_FarHills",   _sprFarHills,   FarHillsSortingOrder);
        _midground  = MakeLayer("Layer_Midground",  _sprMidground,  MidgroundSortingOrder);
        _foreground = MakeLayer("Layer_Foreground", _sprForeground, ForegroundSortingOrder);

        ApplyWorldLayers();

        // Initial rest framing (main menu / world map / before any level has loaded) — the camera
        // already carries its saved rest orthoSize/position at this point (see SceneSetup.
        // PositionCamera()), so this sizes correctly even before OnLevelStarted first fires.
        RescaleToCamera();
    }

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

    void HandleLevelStarted(LevelData data)
    {
        _currentWorld = data != null ? data.world : 1;
        ApplyWorldLayers();
        RescaleToCamera();
    }

    // Swaps FarHills/Midground to the current world's art + clip band, and shows/hides the
    // Foreground layer entirely (World 1 only — World 2 has no opaque Foreground painting by
    // design, see the _sprFarHillsW2/_sprMidgroundW2 field comment). Falls back to the World-1
    // sprite/clip whenever a World-2 sprite isn't wired (e.g. before SceneSetup has assigned it),
    // so an unwired World 2 never renders as a blank/missing layer.
    // Editor-only preview hook (2026-07-19) — DebugPreviewWorldBackdrop.cs calls this directly
    // in Edit mode (no Play mode needed) so World 2 scenery/props can be hand-placed against the
    // real backdrop the same way World 1's barn/cannon were, before any real World 2 LevelData
    // exists to trigger HandleLevelStarted normally. Purely a live Scene-view preview — the next
    // "Wire Scene References" pass always re-forces World 1's sprites back via its own
    // CoverFitLayer editor-time preview (EnsureEnvironmentDepthSystem), so this never needs
    // resetting by hand, and never gets saved as if it were real level data.
    public void PreviewWorld(int world)
    {
        _currentWorld = world;
        ApplyWorldLayers();
        RescaleToCamera();
    }

    void ApplyWorldLayers()
    {
        bool isWorld2 = _currentWorld == 2;

        Sprite farHillsSprite  = (isWorld2 && _sprFarHillsW2  != null) ? _sprFarHillsW2  : _sprFarHills;
        Sprite midgroundSprite = (isWorld2 && _sprMidgroundW2 != null) ? _sprMidgroundW2 : _sprMidground;
        float  farHillsClip    = isWorld2 ? _farHillsClipAboveW2  : _farHillsClipAbove;
        float  midgroundClip   = isWorld2 ? _midgroundClipAboveW2 : _midgroundClipAbove;

        SetLayerSprite(_farHills,  farHillsSprite);
        SetLayerSprite(_midground, midgroundSprite);
        ApplyClip(_farHills,  farHillsClip);
        ApplyClip(_midground, midgroundClip);

        if (_foreground != null) _foreground.gameObject.SetActive(!isWorld2);
    }

    static void SetLayerSprite(Transform layer, Sprite sprite)
    {
        if (layer == null) return;
        var sr = layer.GetComponent<SpriteRenderer>();
        if (sr != null) sr.sprite = sprite;
    }

    // Re-fits every layer to the camera's ACTUAL current orthoSize/aspect/position. Called at
    // Awake(), on every OnLevelStarted, AND on every LateUpdate() where orthoSize has actually
    // changed since the last check (see _lastOrthoSize below) — the LateUpdate() catch-all is not
    // redundant: this class has [DefaultExecutionOrder(-80)], CatapultLauncher has none (defaults
    // to 0), so this class's OnEnable() runs first and subscribes to GameManager.OnLevelStarted
    // BEFORE CatapultLauncher does — meaning THIS class's OnLevelStarted handler fires and reads
    // _cam.orthographicSize BEFORE CatapultLauncher.OnLevelStarted() has actually set it via
    // ComputeOrthoSizeForLevel(). Confirmed via direct code inspection 2026-07-18 (user report +
    // screenshots: Scene view showed full coverage, Play mode showed black gaps at the top/right
    // edges) — every level was cover-fit against the STALE previous-level (or rest) orthoSize, not
    // its own actual computed zoom. Same root cause, same fix BackgroundController already uses
    // for this exact bug shape (see that class's own _lastOrthoSize comment) — a cheap per-frame
    // float compare, not a full recompute every frame. At the widest per-level zoom (orthoSize
    // 8.0, e.g. L18) this scales the source paintings up somewhat past their native resolution —
    // an accepted tradeoff for a single static painting (not a seamless tile), not a bug, unless
    // it visibly softens.
    float _lastOrthoSize = -1f;

    void LateUpdate()
    {
        if (_cam == null) return;
        if (!Mathf.Approximately(_cam.orthographicSize, _lastOrthoSize))
            RescaleToCamera();
    }

    public void RescaleToCamera()
    {
        if (_cam == null) return;
        _lastOrthoSize = _cam.orthographicSize;
        CoverFit(_farHills);
        CoverFit(_midground);
        CoverFit(_foreground);
    }

    void CoverFit(Transform layer)
    {
        if (layer == null) return;
        var sr = layer.GetComponent<SpriteRenderer>();
        if (sr == null || sr.sprite == null) return;

        float camH = _cam.orthographicSize * 2f;
        float camW = camH * _cam.aspect;
        Vector2 native = sr.sprite.bounds.size; // world-space size at localScale = 1
        if (native.x <= 0f || native.y <= 0f) return;

        float scale = Mathf.Max(camW / native.x, camH / native.y);
        Vector3 camPos = _cam.transform.position;
        layer.position   = new Vector3(camPos.x, camPos.y, layer.position.z);
        layer.localScale = new Vector3(scale, scale, 1f);
    }

    Transform MakeLayer(string name, Sprite sprite, int sortingOrder)
    {
        var existing = transform.Find(name);
        var go = existing != null ? existing.gameObject : new GameObject(name);
        if (existing == null) go.transform.SetParent(transform, false);

        var sr = go.GetComponent<SpriteRenderer>();
        if (sr == null) sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = sortingOrder;

        if (sr.sharedMaterial == null || sr.sharedMaterial.shader == null || sr.sharedMaterial.shader.name != "FarmFury/ParallaxBandClip")
        {
            // Fallback only — normally SceneSetup already assigned the shared
            // Assets/Materials/ParallaxBandClip.mat asset to this renderer.
            if (_bandClipMaterial == null)
            {
                var shader = Shader.Find("FarmFury/ParallaxBandClip");
                _bandClipMaterial = shader != null ? new Material(shader) : null;
            }
            if (_bandClipMaterial != null) sr.sharedMaterial = _bandClipMaterial;
        }

        return go.transform;
    }

    static void ApplyClip(Transform layer, float clipAbove)
    {
        if (layer == null) return;
        var sr = layer.GetComponent<SpriteRenderer>();
        if (sr == null) return;
        var mpb = new MaterialPropertyBlock();
        sr.GetPropertyBlock(mpb);
        mpb.SetFloat(ClipAboveID, clipAbove);
        sr.SetPropertyBlock(mpb);
    }

#if UNITY_EDITOR
    // Re-applies the clip sliders live while tweaking them in the Inspector in Play mode, so the
    // split point can be tuned without re-exporting art or restarting — see the field comment on
    // why these three are exposed as sliders in the first place.
    void OnValidate()
    {
        if (_farHills  != null) ApplyClip(_farHills,  _currentWorld == 2 ? _farHillsClipAboveW2  : _farHillsClipAbove);
        if (_midground != null) ApplyClip(_midground, _currentWorld == 2 ? _midgroundClipAboveW2 : _midgroundClipAbove);
        if (_foreground != null) ApplyClip(_foreground, _foregroundClipAbove);
    }
#endif
}
