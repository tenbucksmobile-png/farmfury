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

    // Worlds 3-9 (2026-11-02): each is one full-bleed Stampede painting (<XX>_Parallax_Far.png, copied to
    // Skies/World<N>_Backdrop.png by tools/import_stampede_worlds.py), indexed by LevelData.world. Shown
    // uncropped on Layer_FarHills with the Midground and Foreground layers hidden — the painting carries
    // its own ground. An unwired slot falls back to World 1's stack.
    [SerializeField] Sprite[] _worldBackdrops = new Sprite[10];

    // World 1 cornfield stack (2026-11-08, framed World 1 levels only — see CornfieldActive). Art in
    // Sprites/Environment/Cornfield/, cut out by tools/import_cornfield_art.py, wired by SceneSetup:
    //   Far  = CornfieldFar.png (Rough_Canvas/DistantBarn.png: sky, hazy hills, barn, windmill, silos), cover-fit
    //   Mid  = CornfieldMid.png  hazy mid-distance corn band, tiled, parallax
    //   Wall = CornWall.png      corn stalks at play depth, tiled, world-fixed
    //   Soil = CornSoil.png      grass lip + tilled soil strip; its grass line sits on the physics ground
    //   Silhouettes = CornSilhouetteL/R.png, corn clumps baked near-black with a soft inner edge, framing the corners
    [SerializeField] Sprite _sprCornFar;
    [SerializeField] Sprite _sprCornMid;
    [SerializeField] Sprite _sprCornWall;
    [SerializeField] Sprite _sprCornSoil;
    [SerializeField] Sprite _sprCornSilhouetteL;
    [SerializeField] Sprite _sprCornSilhouetteR;
    [SerializeField] Sprite _sprFlybyPlane;   // SkyFlyby: biplane (MeadowRuins/Plane.png)
    [SerializeField] Sprite _sprFlybyDrone;   // SkyFlyby: robot scout drone (Stampede Robots/Drone.png)
    // Depth + weather (2026-11-09, tools/import_cornfield_art.py --ambience, Kling art in Cornfield/Ambience/):
    //   Ridges = CornRidges.png  distant sandstone bluffs with a ruined tower, haze baked in, tiled, far parallax
    //   Hills  = CornHills.png   wooded hills with a water tower and farmhouse, lightly hazed, tiled, mid parallax
    //   Clouds = Cloud_0..2.png  drifting sky clouds (CornfieldAmbience)
    //   WindSway material = FarmFury/WindSway shader: the corn strips and silhouettes bend in the wind
    [SerializeField] Sprite _sprCornRidges;
    [SerializeField] Sprite _sprCornHills;
    [SerializeField] Sprite[] _sprClouds;
    [SerializeField] Material _windSwayMaterial;

    // World 2 Frozen Tundra framed stack (2026-11-09): the same layers as the cornfield, frozen. Art in
    // Sprites/Environment/Frozen/ (tools/import_frozen_art.py; Kling, magenta-keyed); the far layer is World 2's
    // existing aurora painting (ParallaxFarHills_W2.png, pure night sky — anchored at its bottom edge).
    //   Ridges = FrozenRidges.png  glaciers + a frozen watchtower ruin    Hills = FrozenHills.png  frozen farm hills
    //   Mid    = FrozenMid.png     frozen field band                     Wall  = FrozenWall.png   frozen corn
    //   Soil   = FrozenSoil.png    snow lip over frozen earth            Silhouettes = FrozenSilhouetteL/R.png (navy)
    [SerializeField] Sprite _sprFrozenRidges;
    [SerializeField] Sprite _sprFrozenHills;
    [SerializeField] Sprite _sprFrozenMid;
    [SerializeField] Sprite _sprFrozenWall;
    [SerializeField] Sprite _sprFrozenSoil;
    [SerializeField] Sprite _sprFrozenSilhouetteL;
    [SerializeField] Sprite _sprFrozenSilhouetteR;

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
    // highlight at 1190-1193, solid terrain navy from row ~1196 on).
    //
    // RAISED 0.21 -> 0.60 (2026-07-26, user request: "too much night sky in the parallax", pushed
    // ahead knowingly despite the seam this reintroduces — user chose "push it anyway, accept the
    // seam" over waiting for revised art). 0.60 clears the tallest mountain peaks on the left side
    // of the painting (measured onset of the snow-capped silhouette around row 600-650, v≈0.58-0.61
    // from the top) so the full ice bridge/mountain/tree scene reads before the seam. The seam
    // itself sits at the OLD v=0.227 boundary — a visible horizontal color-break where Midground's
    // own sky (revealed from v=0.227 up to v=0.60) meets FarHills' sky behind it, confirmed via a
    // direct pixel composite of the two source paintings at this value. This is an ART limitation,
    // not a further tuning problem: fix it properly by re-painting ParallaxMidGrounds_W2.png with
    // taller terrain and/or no separate baked sky above the peaks, then this can safely return
    // toward 0.21-0.23 territory without the seam. FarHillsW2 defaults to 1.0 (fully uncropped,
    // same as W1) since it's the one layer meant to supply all sky content.
    [SerializeField, Range(0f, 1f)] float _farHillsClipAboveW2  = 1.0f;
    [SerializeField, Range(0f, 1f)] float _midgroundClipAboveW2 = 0.60f;

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
        // Framed World 1 levels pin the painted meadow to the shared ground line (ReferenceFraming) instead of
        // centring the paintings on the camera, so the scenery, blocks and painted grass all meet at one line.
        _groundAnchored = ReferenceFraming.Active(data) && (_currentWorld == 1 || (_currentWorld == 2 && FrozenReady));
        _groundAnchorY  = _groundAnchored ? ReferenceFraming.GroundY(data) : 0f;
        _parallaxOriginPending = true;   // the launcher parks the camera after this handler; capture it next frame
        _weather = data != null ? data.weather : LevelWeather.Golden;
        ApplyWorldLayers();
        RescaleToCamera();
        if (_flyby != null) _flyby.ResetSchedule();
        if (_ambience != null && CornfieldActive) _ambience.Apply(_weather, _groundAnchorY);
        ApplyWeatherTints();
    }

    LevelWeather _weather;
    CornfieldAmbience.Profile Weather => _ambience != null ? _ambience.Current : CornfieldAmbience.For(_weather);

    // Colour grade of the painted layers for the level's weather (CornfieldAmbience profiles). Cornfield only: the
    // older stacks keep their plain colours.
    void ApplyWeatherTints()
    {
        bool corn = CornfieldActive;
        var p = CornfieldAmbience.For(_weather);
        Color sky = corn ? p.sky : Color.white, land = corn ? p.land : Color.white;
        var far = _farHills != null ? _farHills.GetComponent<SpriteRenderer>() : null;
        if (far != null) far.color = sky;
        if (!corn) return;
        TintStrip(_cornRidges, sky * new Color(1f, 1f, 1f, 1f));
        TintStrip(_cornHills, land);
        TintStrip(_cornMid, land);
        TintStrip(_cornWall, ThemeWallTint * land);
        TintStrip(_cornSoil, Color.Lerp(Color.white, land, 0.7f));
        if (_soilUnderfill != null) _soilUnderfill.color = SoilUnderfill * Color.Lerp(Color.white, land, 0.7f);
    }

    static void TintStrip(Strip s, Color c)
    {
        if (s == null) return;
        c.a = 1f;
        s.tint = c;
        foreach (var t in s.tiles) if (t != null) t.color = c;
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
        _groundAnchored = false;
        ApplyWorldLayers();
        RescaleToCamera();
    }

    Sprite WorldBackdrop =>
        _currentWorld >= 3 && _worldBackdrops != null && _currentWorld < _worldBackdrops.Length
            ? _worldBackdrops[_currentWorld] : null;

    void ApplyWorldLayers()
    {
        bool corn = CornfieldActive;
        SetCornStripsActive(corn);
        if (corn)
        {
            SetLayerSprite(_farHills, ThemeFar);
            ApplyClip(_farHills, 1f);
            if (_midground  != null) _midground.gameObject.SetActive(false);
            if (_foreground != null) _foreground.gameObject.SetActive(false);
            return;
        }

        Sprite backdrop = WorldBackdrop;
        if (backdrop != null)
        {
            SetLayerSprite(_farHills, backdrop);
            ApplyClip(_farHills, 1f);
            if (_midground  != null) _midground.gameObject.SetActive(false);
            if (_foreground != null) _foreground.gameObject.SetActive(false);
            return;
        }
        if (_midground != null) _midground.gameObject.SetActive(true);

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
    Vector2 _lastCamPos;
    bool  _groundAnchored;
    float _groundAnchorY;

    void LateUpdate()
    {
        if (_cam == null) return;
        if (_parallaxOriginPending)
        {
            _parallaxOriginPending = false;
            _parallaxOrigin = _cam.transform.position;
            RescaleToCamera();
            return;
        }
        // Also refit when the camera moves: framed levels set its position after this class's level-start handler.
        if (!Mathf.Approximately(_cam.orthographicSize, _lastOrthoSize) ||
            ((Vector2)_cam.transform.position - _lastCamPos).sqrMagnitude > 1e-6f)
            RescaleToCamera();
    }

    public void RescaleToCamera()
    {
        if (_cam == null) return;
        _lastOrthoSize = _cam.orthographicSize;
        _lastCamPos    = _cam.transform.position;
        CoverFit(_farHills);
        CoverFit(_midground);
        CoverFit(_foreground);
        if (CornfieldActive) UpdateCornfield();
        UpdateDepthFx();
    }

    // ── World 1 cornfield (framed levels, 2026-11-08) ───────────────────────────────────────────────────────
    // Sizes are world units at the framed zoom (ortho 8 → 16 u = 1 screen height). Parallax factor = how much a
    // layer follows the camera: 1 = locked to the screen (infinitely far), 0 = fixed in the world (play depth),
    // negative = slides past faster than the world (nearer than the play layer).
    const float CornFarAnchorUV     = 0.052f;  // barn/silo bases in DistantBarn.png, from the bottom (measured)
    const float CornMidHeight       = 4.2f;    // ≈0.26 sh: the far barn peeks over its tassels
    const float CornMidSink         = 0.3f;    // bottom tucked under the ground line
    const float CornMidParallax     = 0.55f;
    const float CornWallHeight      = 2.6f;    // ≈0.16 sh: about 3.5 animals tall, below most towers' tops
    const float CornWallSink        = 0.15f;   // stalk bases tuck under the soil strip's grass lip
    const float CornSoilGroundUV    = 0.837f;  // grass line in CornSoil.png, from the bottom (measured)
    const float CornSoilMinBelow    = 2.6f;    // soil drawn at least this far below the ground line
    static readonly Color SoilUnderfill = new Color(0.374f, 0.19f, 0.085f);   // bottom rows of CornSoil.png
    // The play-depth corn sits behind the structures: a little darker and less saturated than the islands and
    // blocks so it doesn't compete with them (framework B7: nothing behind the structures that competes).
    static readonly Color CornWallTint = new Color(0.80f, 0.84f, 0.74f, 1f);
    const float SilhouetteParallax  = -0.3f;
    // Landform layers (2026-11-09): bottoms tucked behind the mid corn and the mist band (their art fades out there).
    const float RidgesHeight = 4.4f, RidgesBase = 3.0f, RidgesParallax = 0.85f;   // tops ≈0.58 sh
    const float HillsHeight  = 3.3f, HillsBase  = 2.7f, HillsParallax  = 0.70f;   // tops ≈0.50 sh
    const int RidgesOrder = -37;   // behind the haze veil (-35): hazed like the far painting
    const int HillsOrder  = -33;   // in front of the veil, behind the mist (-32) and mid corn (-30)

    const int CornMidOrder  = -30;   // FarHills -40 < haze veil -35 < mist -32 < mid corn -30
    const int CornMistOrder = -32;
    const int CornWallOrder = -20;
    const int CornSoilOrder = -18;   // in front of the stalk bases
    const int SilhouetteOrder = 40;  // in front of everything on the play layer (animals are 6)

    sealed class Strip
    {
        public Transform root;
        public Sprite sprite;
        public float factor;
        public int order;
        public Color tint = Color.white;
        public Material material;
        public readonly System.Collections.Generic.List<SpriteRenderer> tiles = new();
    }

    Strip _cornMid, _cornWall, _cornSoil, _cornRidges, _cornHills;
    SpriteRenderer _soilUnderfill, _silLeft, _silRight;
    SkyFlyby _flyby;
    CornfieldAmbience _ambience;
    Transform _cornRoot;
    bool _parallaxOriginPending;
    Vector3 _parallaxOrigin;

    // The framed stack's art for the current world: World 1 cornfield, World 2 frozen (2026-11-09).
    bool Frozen => _currentWorld == 2;
    bool FrozenReady => _sprFarHillsW2 != null && _sprFrozenMid != null && _sprFrozenWall != null && _sprFrozenSoil != null;
    Sprite ThemeFar    => Frozen ? _sprFarHillsW2        : _sprCornFar;
    Sprite ThemeMid    => Frozen ? _sprFrozenMid         : _sprCornMid;
    Sprite ThemeWall   => Frozen ? _sprFrozenWall        : _sprCornWall;
    Sprite ThemeSoil   => Frozen ? _sprFrozenSoil        : _sprCornSoil;
    Sprite ThemeRidges => Frozen ? _sprFrozenRidges      : _sprCornRidges;
    Sprite ThemeHills  => Frozen ? _sprFrozenHills       : _sprCornHills;
    Sprite ThemeSilL   => Frozen ? _sprFrozenSilhouetteL : _sprCornSilhouetteL;
    Sprite ThemeSilR   => Frozen ? _sprFrozenSilhouetteR : _sprCornSilhouetteR;
    // The aurora painting is all sky: its bottom edge sits behind the mid band (DistantBarn's barns sit at 0.052).
    float ThemeFarAnchorUV  => Frozen ? 0f : CornFarAnchorUV;
    float ThemeSoilGroundUV => Frozen ? FrozenSoilGroundUV : CornSoilGroundUV;
    Color ThemeWallTint     => Frozen ? FrozenWallTint : CornWallTint;
    const float FrozenSoilGroundUV = 0.851f;   // snow lip in FrozenSoil.png, from the bottom (measured)
    static readonly Color FrozenWallTint = new Color(0.82f, 0.88f, 0.98f, 1f);
    // Frozen stalks are stiff: they barely sway.
    float SwayMid  => Frozen ? 0.025f : 0.07f;
    float SwayWall => Frozen ? 0.045f : 0.13f;
    float SwaySil  => Frozen ? 0.08f  : 0.22f;
    int _builtWorld = -1;

    bool CornfieldActive =>
        _groundAnchored && (_currentWorld == 1 || _currentWorld == 2) &&
        ThemeFar != null && ThemeMid != null && ThemeWall != null && ThemeSoil != null;

    void SetCornStripsActive(bool on)
    {
        // A different world's framed stack: tear the old one down and build this world's.
        if (on && _cornRoot != null && _builtWorld != _currentWorld)
        {
            Destroy(_cornRoot.gameObject);
            _cornRoot = null;
            _cornMid = _cornWall = _cornSoil = _cornRidges = _cornHills = null;
            _soilUnderfill = _silLeft = _silRight = null;
            _ambience = null;
            _flyby = null;
        }
        if (on && _cornRoot == null)
        {
            _builtWorld = _currentWorld;
            _cornRoot = new GameObject("Cornfield").transform;
            _cornRoot.SetParent(transform, false);
            _cornRidges = MakeStrip("Corn_Ridges", ThemeRidges, RidgesParallax, RidgesOrder);
            _cornHills  = MakeStrip("Corn_Hills",  ThemeHills,  HillsParallax,  HillsOrder);
            _cornMid  = MakeStrip("Corn_Mid",  ThemeMid,  CornMidParallax, CornMidOrder);
            _cornMid.material = SwayMaterial(SwayMid);
            _cornWall = MakeStrip("Corn_Wall", ThemeWall, 0f,              CornWallOrder);
            _cornWall.material = SwayMaterial(SwayWall);
            _cornWall.tint = ThemeWallTint;
            _cornSoil = MakeStrip("Corn_Soil", ThemeSoil, 0f,              CornSoilOrder);
            _soilUnderfill = MakeChildRenderer(_cornRoot, "Corn_SoilUnderfill", MakeSolidSprite(), CornSoilOrder);
            _soilUnderfill.color = SoilUnderfill;
            _silLeft  = MakeChildRenderer(_cornRoot, "Corn_SilhouetteLeft",  ThemeSilL, SilhouetteOrder);
            _silRight = MakeChildRenderer(_cornRoot, "Corn_SilhouetteRight", ThemeSilR, SilhouetteOrder);
            var silMat = SwayMaterial(SwaySil);
            if (silMat != null) { _silLeft.sharedMaterial = silMat; _silRight.sharedMaterial = silMat; }
            var amb = new GameObject("Ambience");
            amb.transform.SetParent(_cornRoot, false);
            _ambience = amb.AddComponent<CornfieldAmbience>();
            _ambience.Init(_cam, _sprClouds);
            var fly = new GameObject("SkyFlyby");
            fly.transform.SetParent(_cornRoot, false);
            _flyby = fly.AddComponent<SkyFlyby>();
            _flyby.Init(_cam, _sprFlybyPlane, _sprFlybyDrone);
        }
        if (_cornRoot != null) _cornRoot.gameObject.SetActive(on);
    }

    // A copy of the wired WindSway material per layer (each bends by its own amount). Null without the material:
    // the strip then draws with the default sprite material, unswayed.
    Material SwayMaterial(float amount)
    {
        if (_windSwayMaterial == null) return null;
        var m = new Material(_windSwayMaterial);
        m.SetFloat("_SwayAmount", amount);
        return m;
    }

    Strip MakeStrip(string name, Sprite sprite, float factor, int order)
    {
        var root = new GameObject(name).transform;
        root.SetParent(_cornRoot, false);
        return new Strip { root = root, sprite = sprite, factor = factor, order = order };
    }

    static SpriteRenderer MakeChildRenderer(Transform parent, string name, Sprite sprite, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = order;
        return sr;
    }

    void UpdateCornfield()
    {
        float camH = _cam.orthographicSize * 2f, camW = camH * _cam.aspect;
        Vector3 c = _cam.transform.position;
        float g = _groundAnchorY;
        float camBottom = c.y - camH * 0.5f;

        // Landforms behind the corn: far bluffs, then nearer wooded hills; their faded bottoms sink into the mist.
        LayoutStrip(_cornRidges, RidgesHeight, g + RidgesBase, c, camW);
        LayoutStrip(_cornHills,  HillsHeight,  g + HillsBase,  c, camW);
        // Mid corn: bottom just under the ground line, drifting with the camera.
        LayoutStrip(_cornMid, CornMidHeight, g - CornMidSink, c, camW);
        // Corn wall at play depth.
        LayoutStrip(_cornWall, CornWallHeight, g - CornWallSink, c, camW);
        // Soil: fixed size from the parked camera (no rescaling while the camera follows a shot), grass line on g.
        float restBottom = _parallaxOrigin.y - camH * 0.5f;
        float soilH = Mathf.Max(CornSoilMinBelow, g - restBottom + 0.6f) / ThemeSoilGroundUV;
        float soilBottom = g - soilH * ThemeSoilGroundUV;
        LayoutStrip(_cornSoil, soilH, soilBottom, c, camW);
        // Flat soil colour below the strip when the camera dips under the ground following a shot.
        float fillTop = soilBottom + 0.05f;
        float fillBottom = Mathf.Min(camBottom, soilBottom) - 1f;
        _soilUnderfill.enabled = fillTop > camBottom;
        _soilUnderfill.transform.position = new Vector3(c.x, (fillTop + fillBottom) * 0.5f, 0f);
        _soilUnderfill.transform.localScale = new Vector3(camW * 1.1f, fillTop - fillBottom, 1f);

        // Foreground silhouettes: framed to the screen corners, drifting a little against the pan (a near-layer cue)
        // rather than full 1.3x parallax — the camera follows shots far enough that a true near layer would sweep
        // the right-hand clump across the structures.
        float slide = (c.x - _parallaxOrigin.x) * SilhouetteParallax;   // screen-space drift, opposite the pan
        float left = c.x - camW * 0.5f, right = c.x + camW * 0.5f;
        PlaceClump(_silLeft,  0.50f * camH, left  + 0.09f * camW, rightEdge: true,  camBottom - 0.02f * camH, slide);
        PlaceClump(_silRight, 0.22f * camH, right - 0.06f * camW, rightEdge: false, camBottom - 0.02f * camH, slide);

        // Mist where the far hills meet the corn tops.
        _cornMistY = g - CornMidSink + CornMidHeight * 0.85f;
    }

    static void PlaceClump(SpriteRenderer sr, float height, float edgeX, bool rightEdge, float bottom, float slide)
    {
        if (sr == null || sr.sprite == null) return;
        Vector2 native = sr.sprite.bounds.size;
        float s = height / native.y, w = native.x * s;
        float x = rightEdge ? edgeX - w * 0.5f : edgeX + w * 0.5f;
        sr.transform.position = new Vector3(x + slide, bottom + height * 0.5f - sr.sprite.bounds.center.y * s, 0f);
        sr.transform.localScale = new Vector3(s, s, 1f);
    }

    // Tiles a strip across the screen, mirroring every other tile so neighbouring edges always match.
    void LayoutStrip(Strip s, float height, float bottomY, Vector3 cam, float camW)
    {
        if (s == null || s.sprite == null) return;
        Bounds b = s.sprite.bounds;
        float scale = height / b.size.y, tileW = b.size.x * scale;
        float originX = _parallaxOrigin.x + (cam.x - _parallaxOrigin.x) * s.factor;
        int k0 = Mathf.FloorToInt((cam.x - camW * 0.5f - originX) / tileW);
        int count = Mathf.CeilToInt(camW / tileW) + 2;
        while (s.tiles.Count < count)
        {
            var sr = MakeChildRenderer(s.root, $"Tile{s.tiles.Count}", s.sprite, s.order);
            sr.color = s.tint;
            if (s.material != null) sr.sharedMaterial = s.material;
            s.tiles.Add(sr);
        }
        float y = bottomY + height * 0.5f - b.center.y * scale;
        for (int i = 0; i < s.tiles.Count; i++)
        {
            var sr = s.tiles[i];
            bool used = i < count;
            sr.enabled = used;
            if (!used) continue;
            int k = k0 + i;
            sr.transform.position = new Vector3(originX + (k + 0.5f) * tileW, y, 0f);
            sr.transform.localScale = new Vector3(scale, scale, 1f);
            sr.flipX = (k & 1) != 0;
        }
    }

    float _cornMistY;

    // ── Atmospheric depth (framed World 1 levels, 2026-11-08) ───────────────────────────────────────────────
    // The reference keeps distant scenery lighter, softer and tinted toward the sky (atmospheric perspective),
    // with mist where far land meets near land. With the current paintings that is done here, in code: a
    // translucent haze veil drawn between the far layer and the nearer ones (so only what is far gets hazed), and
    // a soft mist band along the seam where the far hills meet the foreground meadow.
    static readonly Color HazeColour = new Color(1f, 0.87f, 0.74f);   // golden-hour haze
    const float HazeVeilAlpha  = 0.26f;
    const float MistAlpha      = 0.55f;
    const float MistHeightSh   = 0.11f;   // mist band height, fraction of the screen height
    const int   HazeVeilOrder  = -35;     // FarHills -40 < veil < Midground -30 < Foreground -20
    const int   MistOrder      = -19;     // in front of the foreground meadow, straddling its hard top edge

    SpriteRenderer _hazeVeil, _mistBand;

    void UpdateDepthFx()
    {
        bool corn = CornfieldActive;
        bool on = corn || (_groundAnchored && _foreground != null && _foreground.gameObject.activeSelf);
        if (!on)
        {
            if (_hazeVeil != null) _hazeVeil.enabled = false;
            if (_mistBand != null) _mistBand.enabled = false;
            return;
        }
        _hazeVeil ??= MakeFxRenderer("DepthHazeVeil", MakeSolidSprite(), HazeVeilOrder);
        _mistBand ??= MakeFxRenderer("DepthMistBand", MakeMistSprite(), MistOrder);

        float camH = _cam.orthographicSize * 2f, camW = camH * _cam.aspect;
        Vector3 c = _cam.transform.position;

        _hazeVeil.enabled = true;
        var wp = Weather;
        Color hazeCol = corn ? wp.haze : HazeColour;
        _hazeVeil.color = new Color(hazeCol.r, hazeCol.g, hazeCol.b, corn ? wp.hazeAlpha : HazeVeilAlpha);
        _hazeVeil.transform.position = new Vector3(c.x, c.y, 0f);
        _hazeVeil.transform.localScale = new Vector3(camW * 1.05f, camH * 1.05f, 1f);

        // Seam = top of the foreground meadow's visible band (cornfield: where the far hills meet the mid corn,
        // drawn behind the corn so only the hills haze over).
        float bandTop;
        if (corn) bandTop = _cornMistY;
        else
        {
            var fsr = _foreground.GetComponent<SpriteRenderer>();
            bandTop = fsr.bounds.min.y + fsr.bounds.size.y * _foregroundClipAbove;
        }
        _mistBand.sortingOrder = corn ? CornMistOrder : MistOrder;
        float h = MistHeightSh * camH;
        _mistBand.enabled = true;
        _mistBand.color = new Color(hazeCol.r, hazeCol.g, hazeCol.b, corn ? wp.mistAlpha : MistAlpha);
        _mistBand.transform.position = new Vector3(c.x, bandTop + h * 0.08f, 0f);   // densest just under the seam
        _mistBand.transform.localScale = new Vector3(camW * 1.05f, h, 1f);
    }

    SpriteRenderer MakeFxRenderer(string name, Sprite sprite, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = order;
        return sr;
    }

    // 1x1 unit white sprite (scaled to the camera).
    static Sprite MakeSolidSprite()
    {
        var tex = new Texture2D(4, 4, TextureFormat.ARGB32, false);
        var px = new Color[16];
        for (int i = 0; i < 16; i++) px[i] = Color.white;
        tex.SetPixels(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4);
    }

    // 1-unit-tall vertical soft bump: transparent at both edges, densest just below the middle.
    static Sprite MakeMistSprite()
    {
        const int H = 64;
        var tex = new Texture2D(4, H, TextureFormat.ARGB32, false) { wrapMode = TextureWrapMode.Clamp };
        var px = new Color[4 * H];
        for (int y = 0; y < H; y++)
        {
            float v = (y + 0.5f) / H;
            float a = Mathf.Exp(-Mathf.Pow((v - 0.42f) / 0.22f, 2f));
            for (int x = 0; x < 4; x++) px[y * 4 + x] = new Color(1f, 1f, 1f, a);
        }
        tex.SetPixels(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 4, H), new Vector2(0.5f, 0.5f), H);
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
        float centreY = camPos.y;
        if (layer == _farHills && CornfieldActive)
        {
            // Barn/silo bases just behind the mid corn's tassels; the corn and soil cover everything below, so
            // only the width and the top of the screen need covering.
            float anchorY = _groundAnchorY - CornMidSink + CornMidHeight * 0.8f;
            float camTop = camPos.y + camH * 0.5f;
            scale = Mathf.Max(camW / native.x, (camTop - anchorY) / ((1f - ThemeFarAnchorUV) * native.y));
            centreY = anchorY + (0.5f - ThemeFarAnchorUV) * scale * native.y;
        }
        else if (_groundAnchored)
        {
            // Smallest scale that still covers the screen with the painted ground line held at _groundAnchorY.
            float g = ReferenceFraming.World1PaintedGroundUV;
            float camBottom = camPos.y - camH * 0.5f, camTop = camPos.y + camH * 0.5f;
            scale = Mathf.Max(scale,
                (_groundAnchorY - camBottom) / (g * native.y),
                (camTop - _groundAnchorY) / ((1f - g) * native.y));
            centreY = _groundAnchorY + (0.5f - g) * scale * native.y;
        }
        layer.position   = new Vector3(camPos.x, centreY, layer.position.z);
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
        if (WorldBackdrop != null) return;
        if (_farHills  != null) ApplyClip(_farHills,  _currentWorld == 2 ? _farHillsClipAboveW2  : _farHillsClipAbove);
        if (_midground != null) ApplyClip(_midground, _currentWorld == 2 ? _midgroundClipAboveW2 : _midgroundClipAbove);
        if (_foreground != null) ApplyClip(_foreground, _foregroundClipAbove);
    }
#endif
}
