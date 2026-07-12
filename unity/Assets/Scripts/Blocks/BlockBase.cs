using System;
using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(BoxCollider2D))]
public abstract class BlockBase : MonoBehaviour
{
    [Header("Health")]
    [SerializeField] protected float baseMaxHealth           = 20f;
    [SerializeField] protected float impulseDamageMultiplier = 1.0f;
    [SerializeField] protected float minDamageImpulse        = 1.5f;

    [Header("Physics")]
    [SerializeField] protected float baseMass   = 5f;
    [SerializeField] protected float bounciness = 0.2f;

    [Header("Art Sprites")]
    [SerializeField] protected Sprite _sprNormal;      // roughly square blocks
    [SerializeField] protected Sprite _sprHorizontal;  // wide flat blocks (w/h > 1.5)
    [SerializeField] protected Sprite _sprVertical;    // tall thin blocks  (h/w > 1.4)

    // Named-shape art slots — added 2026-07-12 alongside WoodArtVariant's expansion (see that
    // enum's comment) so each distinct prop shape placed in the Scene view gets its own real
    // sprite instead of being squeezed into the 3 generic slots above. Wood-relevant fields are
    // wired on WoodBlock.prefab, Stone-relevant ones on StoneBlock.prefab (SceneSetup.
    // WireBlockSprites) — an unwired field on a class that doesn't use it just stays null and
    // falls back to _sprNormal via Initialise()'s switch below, so this is safe to share on the
    // base class rather than duplicating per subclass.
    [SerializeField] protected Sprite _sprShort;         // Plank_Short.png
    [SerializeField] protected Sprite _sprVerticalShort;  // Plank_VeriticalShort.png
    [SerializeField] protected Sprite _sprHorizontal2D;   // Plank_2DHorizontal.png
    [SerializeField] protected Sprite _sprShork2D;        // Plank_2DShork.png
    [SerializeField] protected Sprite _sprShork;          // Plank_Shork.png
    [SerializeField] protected Sprite _sprCart;           // WoodenCart.png
    [SerializeField] protected Sprite _sprBarrelProp;     // WoodenBarrel.png (non-explosive prop, not ExplodingBarrelBlock)
    [SerializeField] protected Sprite _sprSquare;         // Stone_Square.png
    [SerializeField] protected Sprite _sprBlock;          // Stone_Block.png
    [SerializeField] protected Sprite _sprRuinedWall;     // RuinedStoneWall.png
    [SerializeField] protected Sprite _sprTower;          // StoneTower.png
    [SerializeField] protected Sprite _sprSkew;           // Plank_Skew.png (Wood) / Stone_Skew.png (Stone)
    [SerializeField] protected Sprite _sprDiagonal;       // Plank_Diagonal.png (Wood) / Stone_Diagonal.png (Stone)
    // Distinct from _sprNormal — added 2026-07-12 (L17 dump uses '2D_Block_Wood_Flat', a real
    // sprite separate from Plank_Horizontal.png). WoodArtVariant.Flat previously aliased straight
    // to _sprNormal (same field the Auto/default fallback uses), so a block explicitly placed
    // with the flat art rendered as Plank_Horizontal.png instead — same class of art-mismatch bug
    // as the other named-shape fixes this session.
    [SerializeField] protected Sprite _sprFlat;           // 2D_Block_Wood_Flat.png

    // Optional hit-reaction art (e.g. Haybail_Damaged.png) — null on block types that don't
    // have one wired, in which case TakeDamage() only does the existing colour-tint/crack
    // feedback below, unchanged.
    [SerializeField] protected Sprite _sprDamaged;

    // Optional death-burst art (e.g. Haybail_Damaged.png reused as a starburst explosion) —
    // when wired, DestroyBlock() shows this instead of the generic flying-square fragments.
    // Added 2026-07-26 for HaybaleBlock: at hp=10 it always dies in one hit anyway (a typical
    // Cluck impact deals ~15-20), so the _sprDamaged flash above never actually gets seen before
    // the GameObject is destroyed in the same frame — this is the death reaction players
    // actually see.
    [SerializeField] protected Sprite _sprExplode;

    // SpawnExplosion() multiplies the block's own footprint by this to get the burst's size —
    // was 2.2 (matching the original Haybail_Damaged.png tuning, ~92%x83% content fill), lowered
    // to 1.4 for WoodBlock specifically 2026-07-10 (WoodDebris.png is full-bleed, read as
    // oversized at the same multiplier as haybale). Dropped to a flat 0.5 for every block type
    // same day, briefly corrected to 1.5 ("half again bigger"), then reverted back to 0.5 by
    // explicit final user request same day ("change the explosions back to half size smaller") —
    // 0.5 is the settled value: every burst renders at half the size of whatever died.
    [SerializeField] protected float _explodeSizeMultiplier = 0.5f;

    // When true, this block never transitions to a Dynamic Rigidbody2D on TakeDamage() (see
    // that method below). Added 2026-07-26 per user report: hitting one haybale in a cluster
    // used to wake ALL of them via a level-wide WakeAllStaticBlocks() sweep, so undamaged
    // neighbours visibly shifted/tumbled even though nothing hit them. That sweep was removed
    // entirely 2026-07-10 (same class of bug recurred for Wood — "the wood structure should not
    // fall off screen when the haybale is struck, it should all be independent in its
    // destruction") — TakeDamage() now only ever wakes the specific block that was actually hit,
    // never any other block in the level, so _stayKinematic is really only needed for blocks
    // that must never move even when directly hit themselves (HaybaleBlock still sets this true).
    [SerializeField] protected bool _stayKinematic;

    // Per-prefab audio overrides — both null/false by default, so WoodBlock/StoneBlock keep the
    // existing generic WoodHit/StoneHit + BlockDestroy behaviour unchanged. Added 2026-07-07 for
    // HaybaleBlock: at hp=10 every haybail hit is a guaranteed same-frame one-shot kill (see
    // CluckAnimal.OnCollisionEnter2D's pass-through branch), so the generic hit sound plus the
    // generic destroy sound plus the chicken's own pass-through punch sound all firing on top of
    // each other for one "pop" read as cluttered — user-requested a single dedicated explosion
    // sound instead. _silentHit skips PlayHitSound() entirely; _destroyClipOverride, when set,
    // plays instead of the generic BlockDestroy sound in DestroyBlock().
    [SerializeField] protected bool       _silentHit;
    [SerializeField] protected AudioClip  _destroyClipOverride;

    [Header("Fragments")]
    [SerializeField] private GameObject[] _fragmentPrefabs;
    [SerializeField] private int          _fragmentCount = 5;
    [SerializeField] private float        _fragmentSpeed = 4f;

    public float MaxHealth   { get; private set; }
    public float Health      { get; private set; }
    public bool  IsDestroyed { get; private set; }

    // Fixed scenery/structure that never takes damage — set by LevelLoader.SpawnBlock from
    // LevelData.BlockSpawnData.indestructible (2026-07-12, L10's StoneTower: "a structure that
    // is in place - cannot be destroyed"). TakeDamage() no-ops entirely when true; the block
    // stays Static forever, same as HaybaleBlock's _stayKinematic but for damage rather than physics.
    public bool Indestructible;

    public event Action<BlockBase> OnBlockDestroyed;

    protected Rigidbody2D    _rb;
    protected BoxCollider2D  _col;
    protected SpriteRenderer _sr;
    protected Color          _baseColor = new Color(0.65f, 0.38f, 0.12f); // overwritten in Initialise

    private SpriteRenderer _crackSR1;   // light cracks: visible below 67% health
    private SpriteRenderer _crackSR2;   // heavy cracks: visible below 33% health

    private Sprite    _normalSprite;       // the aspect-selected sprite chosen in Initialise(),
                                            // restored after a _sprDamaged flash
    private Coroutine _damageFlashRoutine;

    private static Sprite _crackSprite1;
    private static Sprite _crackSprite2;

    private const float StdArea = 1.2f * 0.4f;

    protected virtual void Awake()
    {
        _rb  = GetComponent<Rigidbody2D>();
        _col = GetComponent<BoxCollider2D>();
        _sr  = GetComponent<SpriteRenderer>();
        if (_sr == null) _sr = gameObject.AddComponent<SpriteRenderer>();
        if (_sr.sprite == null) _sr.sprite = MakeSquareSprite();
        _sr.sortingOrder = 2;

        // Crack overlay sprites are generated once and shared across all block instances
        if (_crackSprite1 == null) _crackSprite1 = MakeCrackSprite(32, 3, seed: 42);
        if (_crackSprite2 == null) _crackSprite2 = MakeCrackSprite(32, 7, seed: 17);
        _crackSR1 = MakeCrackOverlay("CrackL", _crackSprite1, sortingOrder: 3);
        _crackSR2 = MakeCrackOverlay("CrackH", _crackSprite2, sortingOrder: 4);
    }

    public void Initialise(float width, float height, WoodArtVariant artVariant = WoodArtVariant.Auto)
    {
        _baseColor = _sr.color; // subclass Awake has already set the material colour

        // Select art sprite: an explicit artVariant (set by LevelLayoutDumper directly from the
        // design-time sprite's own filename) always wins over the aspect-ratio guess below —
        // aspect alone can't tell a near-square VERTICAL plank image (e.g. Plank_2DShork.png,
        // visually tall/narrow but its measured w/h footprint is close to 1:1) from a genuinely
        // flat one, so guessing from aspect silently showed the wrong art orientation for that
        // case (found 2026-07-10). Auto (the default, and what every hand-authored B() call
        // still uses) falls back to the original aspect-based heuristic unchanged.
        float aspect = width / height;
        Sprite chosen = artVariant switch
        {
            WoodArtVariant.Vertical      => _sprVertical      ?? _sprNormal,
            WoodArtVariant.Horizontal    => _sprHorizontal    ?? _sprNormal,
            WoodArtVariant.Flat          => _sprFlat ?? _sprNormal,
            WoodArtVariant.Short         => _sprShort         ?? _sprNormal,
            WoodArtVariant.VerticalShort => _sprVerticalShort ?? _sprVertical ?? _sprNormal,
            WoodArtVariant.Horizontal2D  => _sprHorizontal2D  ?? _sprHorizontal ?? _sprNormal,
            WoodArtVariant.Shork2D       => _sprShork2D       ?? _sprVertical ?? _sprNormal,
            WoodArtVariant.Shork         => _sprShork         ?? _sprNormal,
            WoodArtVariant.Cart          => _sprCart          ?? _sprNormal,
            WoodArtVariant.Barrel        => _sprBarrelProp    ?? _sprNormal,
            WoodArtVariant.Square        => _sprSquare        ?? _sprNormal,
            WoodArtVariant.Block         => _sprBlock         ?? _sprNormal,
            WoodArtVariant.RuinedWall    => _sprRuinedWall    ?? _sprNormal,
            WoodArtVariant.Tower         => _sprTower         ?? _sprNormal,
            WoodArtVariant.Skew          => _sprSkew          ?? _sprNormal,
            WoodArtVariant.Diagonal      => _sprDiagonal      ?? _sprNormal,
            _ => aspect < 0.72f ? (_sprVertical   ?? _sprNormal)
               : aspect > 1.5f  ? (_sprHorizontal ?? _sprNormal)
               : _sprNormal,
        };
        if (chosen != null)
        {
            _sr.sprite = chosen;
            _sr.color  = Color.white;
            _baseColor = Color.white;
        }
        _normalSprite = _sr.sprite; // restored after a _sprDamaged flash (see TakeDamage())

        // Scale so the rendered sprite matches the requested (width, height) world-space
        // footprint exactly, regardless of the chosen sprite's own native pixel aspect.
        // WireBlockPrefab's "PPU = texture width" convention only guarantees a native 1×1
        // size on the X axis — non-square art (e.g. Plank_Horizontal at 250×123px) has a
        // native Y size of height_px/width_px, not 1, so setting localScale directly to
        // (width, height) silently squashed/stretched the Y axis for any non-square sprite.
        // Dividing by the chosen sprite's actual native bounds corrects for that on both axes.
        Vector2 native = _sr.sprite != null ? (Vector2)_sr.sprite.bounds.size : Vector2.one;
        transform.localScale = new Vector3(
            native.x > 0.0001f ? width  / native.x : width,
            native.y > 0.0001f ? height / native.y : height,
            1f);

        // Re-fit the collider's LOCAL size to the sprite's native bounds so, after the SAME
        // transform.localScale above is applied, its WORLD-space size still lands exactly on
        // the requested (width, height) footprint — a real bug found 2026-07-10 (user-reported
        // wood blocks feeling "not active when hit" / misaligned): BoxCollider2D.size defaults
        // to (1,1) local, which only produced a correct (width,height) world collider back when
        // localScale WAS literally (width,height). Once localScale changed to account for native
        // sprite aspect (see above), the collider silently inherited that same correction and
        // drifted away from the intended footprint too — e.g. a 1.3x0.64 wood block using
        // Plank_Horizontal.png (native 1.0x0.492) ended up with a ~1.3x1.3 collider, nearly
        // double the visible sprite's actual height. Setting local size = native cancels the
        // localScale correction out algebraically (native * (size/native) = size), landing the
        // world-space collider exactly on (width, height) regardless of the chosen sprite's own
        // native aspect.
        if (_col != null) _col.size = native;

        float area  = width * height;
        float ratio = area / StdArea;

        MaxHealth = baseMaxHealth * ratio;
        Health    = MaxHealth;
        _rb.mass  = baseMass * ratio;

        var mat = new PhysicsMaterial2D("BlockMat") { bounciness = bounciness, friction = 0.8f };
        _col.sharedMaterial = mat;
        _rb.bodyType = RigidbodyType2D.Static;
    }

    // Applies per-instance LevelData overrides after Initialise(). 0 = keep the
    // area-scaled default computed in Initialise().
    public void ApplyOverrides(float healthOverride, float massOverride)
    {
        if (healthOverride > 0f)
        {
            MaxHealth = healthOverride;
            Health    = healthOverride;
        }
        if (massOverride > 0f)
            _rb.mass = massOverride;
    }

    // Scripted override that bypasses BOTH the IsDestroyed guard AND the Indestructible flag —
    // added 2026-07-12 for boss fights where a guarded structure (e.g. L18's StoneTower) should
    // crumble the moment its defending robot falls, even though it's otherwise permanently
    // immune to normal damage/collapse-cascade (see RobotEnemy's _destroyOnDeath and
    // CommanderRobot usage). DestroyBlock() itself only checks IsDestroyed, not Indestructible,
    // so this is just a public entry point into the same real destruction path — not a separate
    // code path that could desync from normal block death (VFX/SFX/scoring all still fire).
    public void ForceDestroy() => DestroyBlock();

    public void TakeDamage(float amount)
    {
        if (IsDestroyed || Indestructible) return;
        // Only THIS block wakes on its own hit — see the removed WakeAllStaticBlocks() note
        // below for why a level-wide wake was wrong. _stayKinematic blocks (e.g. HaybaleBlock)
        // never wake at all, same as before.
        if (!_stayKinematic && _rb.bodyType == RigidbodyType2D.Static)
            _rb.bodyType = RigidbodyType2D.Dynamic;

        Health = Mathf.Max(0f, Health - amount);
        if (!_silentHit) PlayHitSound();
        OnHealthChanged();
        PlayDamageFlash();

        // A robot resting on top should fall the instant its support is disturbed by ANY real
        // hit — not only once the block is fully destroyed (2026-07-10, user request: "make sure
        // that robots fall if structure under them are disturbed, even if one haybale or wood is
        // fractured"). Runs here unconditionally (every hit, lethal or not) rather than inside
        // DestroyBlock() as before — moved, not duplicated, so a lethal hit still only checks
        // once (DestroyBlock() below no longer has its own call). _col/transform are still fully
        // valid at this point regardless of whether this hit is about to destroy the block.
        CheckForRobotsOnTop();
        CheckForBlocksOnTop();

        if (Health <= 0f) DestroyBlock();
    }

    // Briefly swaps to _sprDamaged (e.g. Haybail_Damaged.png) on hit, then restores the normal
    // sprite — mirrors RobotEnemy.FlashDamage()'s dedicated-art hit reaction. No-op for block
    // types that don't have one wired (_sprDamaged stays null), leaving the existing colour-tint/
    // crack-overlay feedback in OnHealthChanged() as the only damage feedback for those.
    void PlayDamageFlash()
    {
        if (_sprDamaged == null || _sr == null) return;
        if (_damageFlashRoutine != null) StopCoroutine(_damageFlashRoutine);
        _damageFlashRoutine = StartCoroutine(DamageFlashRoutine());
    }

    IEnumerator DamageFlashRoutine()
    {
        _sr.sprite = _sprDamaged;
        yield return new WaitForSeconds(0.15f);
        if (!IsDestroyed && _sr != null) _sr.sprite = _normalSprite;
        _damageFlashRoutine = null;
    }

    protected virtual void OnCollisionEnter2D(Collision2D col)
    {
        if (IsDestroyed) return;
        if (_rb.bodyType == RigidbodyType2D.Static &&
            col.rigidbody != null &&
            col.rigidbody.bodyType == RigidbodyType2D.Static)
            return;

        float effMass = CalculateEffectiveMass(col.rigidbody);
        float impulse = col.relativeVelocity.magnitude * effMass;
        if (impulse < minDamageImpulse) return;
        TakeDamage(impulse * impulseDamageMultiplier);
    }

    protected virtual void PlayHitSound()
    {
        var s = this is StoneBlock ? AudioManager.Sound.StoneHit : AudioManager.Sound.WoodHit;
        AudioManager.Play(s, 0.08f);
    }

    protected virtual void OnHealthChanged()
    {
        if (_sr == null) return;
        float t = Health / MaxHealth;

        // Healthy (material colour) → orange at 50% → red-orange at 25% → red at 0%
        var orange    = new Color(1f, 0.55f, 0f);
        var redOrange = new Color(0.9f, 0.15f, 0f);
        _sr.color = t > 0.50f
            ? Color.Lerp(orange,    _baseColor, (t - 0.50f) / 0.50f)
            : t > 0.25f
            ? Color.Lerp(redOrange, orange,     (t - 0.25f) / 0.25f)
            : Color.Lerp(Color.red, redOrange,  t           / 0.25f);

        // Crack stage 1: <50% health shows light cracks (damaged sprite swap)
        // Crack stage 2: <25% health shows heavy cracks on top
        if (_crackSR1 != null)
            _crackSR1.color = t < 0.50f ? new Color(0f, 0f, 0f, 0.7f) : Color.clear;
        if (_crackSR2 != null)
            _crackSR2.color = t < 0.25f ? new Color(0f, 0f, 0f, 0.9f) : Color.clear;
    }

    protected virtual void DestroyBlock()
    {
        if (IsDestroyed) return;
        IsDestroyed = true;
        // CheckForRobotsOnTop() no longer lives here — TakeDamage() now calls it once on every
        // hit (lethal or not), see that method's comment. Calling it again here would double-fire
        // for the specific hit that both damages AND destroys the block (the common case for any
        // one-hit-kill block like Haybale/Wood/Barrel).
        CameraShake.Shake(0.22f, 0.20f);
        if (_sprExplode != null) SpawnExplosion();
        else                     SpawnFragments();
        SpawnImpactFlash();
        if (_destroyClipOverride != null)
            AudioManager.PlayClip(_destroyClipOverride);
        else
            AudioManager.Play(AudioManager.Sound.BlockDestroy, 0.05f);
        OnBlockDestroyed?.Invoke(this);
        ScoreManager.Instance?.AddBlockScore(this);
        Destroy(gameObject);
    }

    // Checks for a robot resting directly on top of this block and, if found, makes it fall
    // (RobotEnemy.MakeDynamicFromSupportLoss switches it from Static to Dynamic so gravity takes
    // over) rather than leaving it floating in place once its support is gone — user-requested
    // 2026-07-09: "if the robot is on top of a structure and the structure is hit (disappears)
    // then naturally the robot should fall, which helps in destroying them." Checks a box above
    // this block's collider bounds against layer 9 (Robot — see CLAUDE.md's layer table); only
    // robots directly overlapping THIS block's footprint (X-wise) fall, so destroying one block
    // in a taller stack doesn't affect a robot resting on a different block.
    //
    // Box height widened 0.3 -> 2.5 (2026-07-10, user-reported: "L03 works in L02 but the robot
    // does not tumble in L03"), then 2.5 -> 8 (same day, user-reported again after re-testing:
    // "the robot on top of structure is still not falling"). Root cause: RobotEnemy's actual
    // physics collider is always re-derived to a small fixed 0.6x0.9 world-space hitbox
    // regardless of visual scale (see LevelLoader.SpawnRobot), but a big visually-scaled robot
    // needs its transform.position placed much higher to make its oversized sprite visually
    // "stand" on the pile — so the tiny collider can end up well above a modest detection
    // column, even though the robot reads as clearly resting on the structure by eye. Rather
    // than keep guessing a height that covers every future robot placement exactly, this is now
    // deliberately generous (8 units — comfortably taller than this level's entire play area)
    // so any robot anywhere above a destroyed block's own X footprint falls, no per-level tuning
    // needed. Still limited to 90% of this block's own width in X, so it can't reach a robot
    // sitting on an unrelated block elsewhere in the level.
    void CheckForRobotsOnTop()
    {
        const int robotLayerMask = 1 << 9;
        const float checkHeight = 8f;
        Bounds b = _col.bounds;
        Vector2 checkCenter = new Vector2(b.center.x, b.max.y + checkHeight * 0.5f);
        Vector2 checkSize   = new Vector2(b.size.x * 0.9f, checkHeight);
        var hits = Physics2D.OverlapBoxAll(checkCenter, checkSize, 0f, robotLayerMask);
        foreach (var hit in hits)
        {
            var robot = hit.GetComponentInParent<RobotEnemy>();
            if (robot != null) robot.MakeDynamicFromSupportLoss();
        }
    }

    // Block-level counterpart to CheckForRobotsOnTop() above — 2026-07-11 fundamental gameplay
    // rule, user request: "when structure is hit, the structure naturally collapses and whatever
    // it hits on the way down naturally also topples (like in real life)". Previously only a
    // resting ROBOT fell when its supporting block was destroyed; a block resting on another
    // block (e.g. the top plank of a stacked tower) had nothing waking it, so it was left
    // floating in place — Static rigidbodies ignore gravity entirely regardless of whether
    // there's still anything beneath them. Wakes any block directly above (Static -> Dynamic, the
    // same "give real physics control of it" transition TakeDamage() already does to whichever
    // block was actually hit) so it now genuinely falls/topples under gravity rather than
    // artificial damage math alone. Real domino propagation up a taller stack needs no extra code
    // beyond this: once a block is Dynamic it falls, its own eventual landing collision runs
    // through the normal OnCollisionEnter2D -> TakeDamage() path exactly like any other hit
    // (including this same check for whatever rests on IT), so a whole tower can cascade down
    // from one hit at its base without recursion here.
    //
    // Unlike CheckForRobotsOnTop's deliberately generous 8-unit column (needed only because a
    // robot's small physics collider sits far below its scaled-up visual sprite — see that
    // method's comment), block colliders are already correctly re-fitted to their visuals in
    // Initialise(), so a much shorter check reliably catches a directly-stacked neighbour without
    // reaching into an unrelated structure elsewhere in the level.
    void CheckForBlocksOnTop()
    {
        const int blockLayerMask = 1 << 8;
        const float checkHeight = 1.5f;
        Bounds b = _col.bounds;
        Vector2 checkCenter = new Vector2(b.center.x, b.max.y + checkHeight * 0.5f);
        Vector2 checkSize   = new Vector2(b.size.x * 0.9f, checkHeight);
        var hits = Physics2D.OverlapBoxAll(checkCenter, checkSize, 0f, blockLayerMask);
        foreach (var hit in hits)
        {
            var block = hit.GetComponentInParent<BlockBase>();
            // Indestructible blocks (e.g. StoneTower — "a structure that is in place, cannot be
            // destroyed") never fall via this cascade either, same exemption as _stayKinematic —
            // 2026-07-12, alongside the user's "stone also falls if wood under it gives way"
            // request: ordinary Stone SHOULD fall when its own support breaks (no exemption
            // needed there, it has no _stayKinematic/Indestructible flag), but a fixed structure
            // marked Indestructible must stay physically in place regardless of what breaks
            // beneath or beside it.
            if (block == null || block == this || block.IsDestroyed || block._stayKinematic || block.Indestructible) continue;
            if (block._rb.bodyType == RigidbodyType2D.Static)
            {
                block._rb.bodyType = RigidbodyType2D.Dynamic;
                // Cascade the wake-up immediately, same frame, instead of waiting for this
                // block's own eventual landing collision to generate enough impulse to trigger
                // TakeDamage() (which is what this cascade used to rely on to reach further up a
                // stack — see the class-comment above this method). A gentle settling fall often
                // lands under minDamageImpulse and never calls TakeDamage() at all, so whatever
                // was silently found here in every fully-cascading case, that reliance would
                // otherwise leave everything further up the tower permanently frozen as Static
                // rigidbodies, floating with no real support beneath them — 2026-07-12, user
                // report: "structure fall and break all the way to the ground, blocks can't just
                // fall over and hang in the middle of the air." Recursion terminates naturally —
                // it only ever climbs upward through however many blocks are actually stacked,
                // and stops the moment it finds no more Static blocks directly above.
                block.CheckForRobotsOnTop();
                block.CheckForBlocksOnTop();
            }
        }
    }

    // Death burst for block types with dedicated explosion art (see _sprExplode) — replaces the
    // generic flying-square SpawnFragments() below. Reuses FragmentFader's existing fade-to-
    // transparent-over-0.6s-then-destroy behaviour rather than adding a second fade coroutine.
    void SpawnExplosion()
    {
        var go = new GameObject("BlockExplosion");
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite       = _sprExplode;
        sr.sortingOrder = 10;
        go.transform.position   = transform.position;
        float sz = Mathf.Max(_col.bounds.size.x, _col.bounds.size.y) * _explodeSizeMultiplier; // bigger than the block itself
        go.transform.localScale = new Vector3(sz, sz, 1f);
        go.AddComponent<FragmentFader>();
    }

    void SpawnImpactFlash()
    {
        var go = new GameObject("BlockFlash");
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite       = MakeSquareSprite();
        sr.color        = new Color(1f, 0.88f, 0.55f, 0.55f);
        sr.sortingOrder = 15;
        float sz = _col.bounds.size.x * 1.6f;
        go.transform.position   = transform.position;
        go.transform.localScale = new Vector3(sz, sz, 1f);
        Destroy(go, 0.12f);
    }

    protected virtual void SpawnFragments()
    {
        // Use art prefabs when wired; otherwise spawn procedural coloured squares
        if (_fragmentPrefabs != null && _fragmentPrefabs.Length > 0)
        {
            for (int i = 0; i < _fragmentCount; i++)
            {
                var prefab = _fragmentPrefabs[UnityEngine.Random.Range(0, _fragmentPrefabs.Length)];
                var frag   = Instantiate(prefab, transform.position, UnityEngine.Random.rotation);
                if (frag.TryGetComponent<Rigidbody2D>(out var rb))
                {
                    rb.linearVelocity  = UnityEngine.Random.insideUnitCircle.normalized * _fragmentSpeed;
                    rb.angularVelocity = UnityEngine.Random.Range(-360f, 360f);
                }
                Destroy(frag, 2f);
            }
            return;
        }

        // 4 procedural fragments that fly outward and fade over 0.6 seconds
        Color col = _sr != null
            ? new Color(_sr.color.r, _sr.color.g, _sr.color.b)
            : Color.grey;
        for (int i = 0; i < 4; i++)
        {
            var go = new GameObject("Frag");
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = MakeSquareSprite();
            float dim = UnityEngine.Random.Range(0.55f, 0.90f);
            sr.color = new Color(col.r * dim, col.g * dim, col.b * dim);
            sr.sortingOrder = 10;

            go.transform.position   = transform.position +
                                      (Vector3)(UnityEngine.Random.insideUnitCircle * 0.15f);
            float s = UnityEngine.Random.Range(0.06f, 0.22f);
            go.transform.localScale = new Vector3(s, s, 1f);

            float angle = i * 90f + UnityEngine.Random.Range(-30f, 30f);
            float speed = UnityEngine.Random.Range(4f, 10f);
            var rb = go.AddComponent<Rigidbody2D>();
            rb.gravityScale    = 2.5f;
            rb.linearVelocity  = new Vector2(
                Mathf.Cos(angle * Mathf.Deg2Rad) * speed,
                Mathf.Sin(angle * Mathf.Deg2Rad) * speed);
            rb.angularVelocity = UnityEngine.Random.Range(-400f, 400f);

            go.AddComponent<FragmentFader>(); // fades alpha 1→0 over 0.6s then self-destructs
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    SpriteRenderer MakeCrackOverlay(string name, Sprite sprite, int sortingOrder)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform);
        go.transform.localPosition = Vector3.zero;
        go.transform.localScale    = Vector3.one;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite       = sprite;
        sr.color        = Color.clear;
        sr.sortingOrder = sortingOrder;
        return sr;
    }

    // Procedural crack texture: numCracks main lines, each with one branch.
    // seed keeps the pattern deterministic across runs.
    static Sprite MakeCrackSprite(int size, int numCracks, int seed)
    {
        var rng   = new System.Random(seed);
        var tex   = new Texture2D(size, size, TextureFormat.ARGB32, false);
        var clear = new Color[size * size]; // Color() defaults to (0,0,0,0)
        tex.SetPixels(clear);

        var dark  = new Color(0f, 0f, 0f, 0.85f);
        var faint = new Color(0f, 0f, 0f, 0.50f);

        for (int c = 0; c < numCracks; c++)
        {
            // Start near the centre and reach toward an edge
            int x0 = rng.Next(size / 3, 2 * size / 3);
            int y0 = rng.Next(size / 3, 2 * size / 3);
            int x1 = Clamp(x0 + (int)(rng.NextDouble() * size * 0.7 - size * 0.35), 0, size - 1);
            int y1 = Clamp(y0 + (int)(rng.NextDouble() * size * 0.7 - size * 0.35), 0, size - 1);
            DrawLine(tex, x0, y0, x1, y1, dark);

            // Branch from the midpoint
            int mx = (x0 + x1) / 2, my = (y0 + y1) / 2;
            int bx = Clamp(mx + (int)(rng.NextDouble() * size * 0.4 - size * 0.2), 0, size - 1);
            int by = Clamp(my + (int)(rng.NextDouble() * size * 0.4 - size * 0.2), 0, size - 1);
            DrawLine(tex, mx, my, bx, by, faint);
        }

        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), Vector2.one * 0.5f, (float)size);
    }

    static void DrawLine(Texture2D tex, int x0, int y0, int x1, int y1, Color col)
    {
        int w = tex.width, h = tex.height;
        int dx = Mathf.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1;
        int dy = Mathf.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1;
        int err = dx - dy;
        for (;;)
        {
            if (x0 >= 0 && x0 < w && y0 >= 0 && y0 < h) tex.SetPixel(x0, y0, col);
            if (x0 == x1 && y0 == y1) break;
            int e2 = err * 2;
            if (e2 > -dy) { err -= dy; x0 += sx; }
            if (e2 <  dx) { err += dx; y0 += sy; }
        }
    }

    static int Clamp(int v, int min, int max) => v < min ? min : v > max ? max : v;

    protected static Sprite MakeSquareSprite()
    {
        var tex = new Texture2D(4, 4, TextureFormat.ARGB32, false);
        var px  = new Color[16];
        for (int i = 0; i < 16; i++) px[i] = Color.white;
        tex.SetPixels(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
    }

    float CalculateEffectiveMass(Rigidbody2D other)
    {
        if (other == null || other.bodyType == RigidbodyType2D.Static)
            return _rb.mass * 0.6f;
        return (_rb.mass * other.mass) / (_rb.mass + other.mass);
    }

}

// Runs on each fragment GO — fades SpriteRenderer alpha from 1 to 0 over 0.6s then destroys
sealed class FragmentFader : MonoBehaviour
{
    System.Collections.IEnumerator Start()
    {
        var sr = GetComponent<SpriteRenderer>();
        if (sr == null) { Destroy(gameObject); yield break; }
        const float duration = 0.6f;
        Color c = sr.color;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            sr.color = new Color(c.r, c.g, c.b, Mathf.Lerp(1f, 0f, elapsed / duration));
            yield return null;
        }
        Destroy(gameObject);
    }
}
