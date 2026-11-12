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

    // World 2 (Frozen Tundra) ice/snow reskins — added 2026-07-26 (user: "how do i build level 19
    // with new sprites"). Only the 5 shapes that actually have ice art today (see
    // Assets/Sprites/Environment/World2Props/): Normal/Square (Ice_block.png), Vertical
    // (Ice_vertical.png), Horizontal (Snow_block.png), Diagonal (Ice_diagonal.png), Short
    // (Snow_short.png). Every OTHER WoodArtVariant (Skew/Cart/Barrel/RuinedWall/Tower/etc.) has no
    // ice counterpart yet and simply falls back to whichever of these 5 resolves for "normal" —
    // see Initialise()'s world-aware `normal`/`vertical`/etc. locals, which every other variant's
    // own `?? ` fallback chain already resolves through. Wired on both WoodBlock.prefab and
    // StoneBlock.prefab (SceneSetup.WireBlockSprites) — "ice" is a visual reskin only, not a new
    // BlockType; physics/health/damage behaviour is unchanged (still plain Wood/Stone).
    [SerializeField] protected Sprite _sprNormalW2;
    [SerializeField] protected Sprite _sprVerticalW2;
    [SerializeField] protected Sprite _sprHorizontalW2;
    [SerializeField] protected Sprite _sprDiagonalW2;
    [SerializeField] protected Sprite _sprShortW2;
    // World 2 death burst (2026-11-09): ice shatters into splinters (World2Props/IceSplinters.png) instead of rubble.
    [SerializeField] protected Sprite _sprExplodeW2;
    // World 3 (Watermill Village, 2026-11-10): mill timber (iron-banded oak post and plank) and river-stone masonry
    // (cobble block, pillar, lintel slab) instead of stone; flour sacks for hay (tools/import_watermill_art.py).
    // Same shape slots as World 2's; a stone box is the cobble block, a stone slab the lintel.
    [SerializeField] protected Sprite _sprNormalW3;
    [SerializeField] protected Sprite _sprVerticalW3;
    [SerializeField] protected Sprite _sprHorizontalW3;
    [SerializeField] protected Sprite _sprShortW3;
    [SerializeField] protected Sprite _sprExplodeW3;
    // World 4 (Sky Islands, 2026-11-11): sky-blue airship timber (brass-banded post and plank), white marble (block,
    // column, lintel slab) instead of stone, cloud bales for hay (tools/import_sky_art.py). Same slots as World 3's.
    [SerializeField] protected Sprite _sprNormalW4;
    [SerializeField] protected Sprite _sprVerticalW4;
    [SerializeField] protected Sprite _sprHorizontalW4;
    [SerializeField] protected Sprite _sprShortW4;
    // World 5 (Sunken City, 2026-11-12): barnacled shipwreck timber (post and plank), teal sunken-city stone (block,
    // column, lintel slab), sponge bales for hay (tools/import_sunken_art.py). Same slots as World 4's.
    [SerializeField] protected Sprite _sprNormalW5;
    [SerializeField] protected Sprite _sprVerticalW5;
    [SerializeField] protected Sprite _sprHorizontalW5;
    [SerializeField] protected Sprite _sprShortW5;
    // World 6 (Robot Mothership, 2026-11-12): steel girder post and I-beam, riveted crate / energy column / steel slab,
    // battery pods for hay (tools/import_mothership_art.py). Same slots as World 5's.
    [SerializeField] protected Sprite _sprNormalW6;
    [SerializeField] protected Sprite _sprVerticalW6;
    [SerializeField] protected Sprite _sprHorizontalW6;
    [SerializeField] protected Sprite _sprShortW6;
    // Paid worlds (2026-11-12, tools/import_paid_worlds_art.py), same slots again: World 7 ranch timber / red sandstone /
    // dusty hay, World 8 candy-striped poles / striped crates / prize cushions, World 9 steel beams / steel crates /
    // produce crates.
    [SerializeField] protected Sprite _sprNormalW7;
    [SerializeField] protected Sprite _sprVerticalW7;
    [SerializeField] protected Sprite _sprHorizontalW7;
    [SerializeField] protected Sprite _sprShortW7;
    [SerializeField] protected Sprite _sprNormalW8;
    [SerializeField] protected Sprite _sprVerticalW8;
    [SerializeField] protected Sprite _sprHorizontalW8;
    [SerializeField] protected Sprite _sprShortW8;
    [SerializeField] protected Sprite _sprNormalW9;
    [SerializeField] protected Sprite _sprVerticalW9;
    [SerializeField] protected Sprite _sprHorizontalW9;
    [SerializeField] protected Sprite _sprShortW9;

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
    // 2026-11-09: now the k of EffectSizing.BurstDiameter — the burst spans k × the geometric mean of the block's
    // VISIBLE sides (see EffectSizing). Set in each block type's Awake (Wood 1.5, Stone 1.6, Haybale 2.0, Barrel 2.2).
    [SerializeField] protected float _explodeSizeMultiplier = 1.5f;

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

    // Per-instance override, set by LevelLoader.SpawnBlock from BlockSpawnData.forceStayKinematic
    // — lets a specific placement (e.g. L01's decorative ground haybale pile) opt into staying
    // fixed even on a prefab whose own default is now false (see the field comment on
    // forceStayKinematic in LevelData.cs for why this exists).
    public void SetStayKinematic(bool value) => _stayKinematic = value;

    // ── Live physics (framed levels, 2026-11-08: "when they hit they slowly topple over and break down on the
    // way down, like Angry Birds") ──────────────────────────────────────────────────────────────────────────────
    // The block becomes a real Dynamic body that starts ASLEEP: a sleeping stack doesn't move at all until something
    // touches it, then the touched part wakes, leans, topples and breaks on impact like an Angry Birds structure.
    // Lower gravity makes topples slower and more readable. Settling contacts in the first moments are ignored.
    // 0.6 -> 0.42 (2026-11-09, user: "topples slower, so it increases the drastic fall"): every collapse plays out
    // about 20% slower, so a tower visibly leans, tips and comes down storey by storey. Robots, the hoist bucket and
    // rolling wagons share it, so everything in a collapse falls at the same pace.
    // 0.42 -> 0.5 (2026-11-10, user: "a soft strike, with a slow topple that speeds up as gravity takes over"): the lean
    // still starts slowly (a tower near upright has little torque) but the fall now visibly accelerates.
    public const float LiveGravityScale = 0.5f;
    // Struck blocks get an extra shove at the contact point (fraction of the striker's momentum) so the hit part
    // tips easily even under the lower gravity — the "topples easier when struck" half of the same request.
    // 2026-11-10 ("remove all second strike requirements"): one soft strike must start the topple, so the shove is
    // stronger and also leans the stack resting on and beside the struck block (StackShove), not only the block itself.
    // 2026-11-11 (user: "the impact ... is too high and shoots the structure apart - striking the structure is just
    // enough to topple should it hit correctly - if it doesnt nothing should happen - for example hitting a stone"):
    // the 0.6 x momentum shove (max 36) and the StackShove that pushed every block above the struck one are gone. What
    // is left is a small tipping nudge for a LIGHT piece (wood / hay / barrel, not stone) struck ABOVE its middle — the
    // "hit it right" case; a low hit or a hit on stone gets only the real collision. Animals now carry
    // AnimalBase.ImpactMassScale of their mass into hits and stone is twice as dense (StoneBlock), so the collision
    // itself no longer throws pieces across the level.
    const float TipNudgeFraction = 0.08f, TipNudgeMax = 3f;
    // No bounce: bouncy contacts made an awake stack jiggle and creep until it toppled (PhysicsProbe, 2026-11-08).
    static PhysicsMaterial2D _liveMaterial;
    // Friction 0.9 -> 0.6 and angular damping 0.6 -> 0.15 (2026-11-09, user: structures must "topple easier"): a hit
    // tower now leans and keeps going instead of the high friction and damping stopping it part-way. Untouched stacks
    // are unaffected — they are asleep (see LevelLoader.GoLiveAfterReveal) and sleeping bodies don't slide.
    static PhysicsMaterial2D LiveMaterial => _liveMaterial ??= new PhysicsMaterial2D("LiveBlock") { friction = 0.55f, bounciness = 0f };
    const float LiveGraceSeconds = 0.6f;
    float _graceUntil;
    public bool IsLive { get; private set; }

    public void GoLive()
    {
        if (NeverFalls || _rb == null) return;
        IsLive = true;
        _rb.bodyType = RigidbodyType2D.Dynamic;
        _rb.gravityScale = LiveGravityScale;
        _rb.linearVelocity = Vector2.zero;
        _rb.angularVelocity = 0f;
        _rb.linearDamping = 0.3f;     // 0.05 -> 0.3 (2026-11-11): loose pieces drop rather than fly across the level
        _rb.angularDamping = 0.04f;   // 0.15 -> 0.04 (2026-11-10): the lean builds up instead of being braked
        if (_col != null) _col.sharedMaterial = LiveMaterial;
        _rb.Sleep();
        _graceUntil = Time.time + LiveGraceSeconds;
        RememberRestPose();
    }

    /// <summary>Zeroes any settling motion and puts the (live) body back to sleep.</summary>
    public void Freeze()
    {
        if (!IsLive || IsDestroyed || _rb == null || _rb.bodyType != RigidbodyType2D.Dynamic) return;
        _rb.linearVelocity = Vector2.zero;
        _rb.angularVelocity = 0f;
        _rb.Sleep();
    }

    // An animal or a thrown ability projectile striking a light live block high up tips it a little further (see
    // TipNudgeFraction): a gentle torque at the contact point, never a throw. Stone and low hits: nothing extra.
    void ShoveOnHit(Collision2D col)
    {
        if (!IsLive || IsDestroyed || _rb == null || _rb.bodyType != RigidbodyType2D.Dynamic || _col == null) return;
        if (this is StoneBlock) return;
        var other = col.rigidbody;
        if (other == null || (other.GetComponent<AnimalBase>() == null && other.GetComponent<AbilityProjectile>() == null
                              && other.GetComponent<EggProjectile>() == null)) return;
        Vector2 at = col.contactCount > 0 ? col.GetContact(0).point : (Vector2)transform.position;
        if (at.y <= _col.bounds.center.y) return;            // struck low: no tipping help
        float push = Mathf.Min(TipNudgeMax, TipNudgeFraction * other.mass * col.relativeVelocity.magnitude);
        if (push < 0.2f) return;
        float side = Mathf.Sign(transform.position.x - other.position.x);
        if (side == 0f) side = 1f;
        _rb.WakeUp();
        _rb.AddForceAtPosition(new Vector2(side, 0f) * push, at, ForceMode2D.Impulse);
    }

    // DestroyBlock wakes the neighbours, but the block's collider lingers for DestroyDelay (0.12 s) — long enough for
    // something resting on it to be put back to sleep on top of a body that is about to vanish. Wake them again as it
    // actually goes (2026-11-10, floating-robot report).
    void OnDestroy()
    {
        if (IsLive && IsDestroyed) WakeNeighbours();
    }

    // Wakes everything resting on or against this block (a destroyed body doesn't reliably wake its neighbours).
    void WakeNeighbours()
    {
        if (_col == null) return;
        Bounds b = _col.bounds;
        foreach (var hit in Physics2D.OverlapBoxAll(b.center, (Vector2)b.size + new Vector2(0.3f, 0.6f), 0f))
            if (hit != null && hit.attachedRigidbody != null && hit.attachedRigidbody.bodyType == RigidbodyType2D.Dynamic)
                hit.attachedRigidbody.WakeUp();
    }

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

    // True for blocks that must stay physically fixed regardless of support — HaybaleBlock's
    // _stayKinematic (piles that stay visually fixed even with their base gone) or a scripted
    // Indestructible structure (StoneTower). SettleIfUnsupported() below must never touch either.
    public bool NeverFalls => _stayKinematic || Indestructible;

    // Called once by LevelLoader right after a level's blocks finish spawning (see
    // LevelLoader.SettleUnsupportedBlocks) — wakes this block immediately (Static -> Dynamic) if
    // nothing is actually touching its underside, instead of leaving it permanently Static and
    // visibly floating for the rest of the level. Added 2026-07-13, user report + screenshot on
    // L10: "structure still remains randomly in the air." Root cause: some hand-placed blocks
    // (captured via LevelLayoutDumper from the Scene view) end up with a small unintentional gap
    // between them and whatever was meant to support them — Static rigidbodies never respond to
    // gravity regardless of whether real support exists, so a level-authoring gap like that leaves
    // the block hanging for the entire level since nothing else ever disturbs it. This check only
    // ever wakes a block that has ZERO support directly beneath it (ground or another block) — a
    // properly-supported block (the overwhelming majority in every level) is untouched.
    //
    // FOLLOW-UP FIX (2026-07-13, same user report persisting after the first pass): this method
    // originally only flipped THIS block to Dynamic and stopped — verified against L10's actual
    // recorded coordinates (not a guess): Stone_Square block #2 at (3.25, -5.44) sits 0.66 units
    // above the ground (top=-6.60) with nothing else beneath it, and it in turn supports
    // Stone_Square #3, which supports two Plank_Short pieces, which support a Barrel_Dynamite —
    // a 5-block column. Waking only the bottom Stone left the other 4 blocks permanently Static
    // (nothing ever told them their support just fell away), so they stayed exactly where they
    // were — floating — even after the bottom block dropped out from under them. Now calls the
    // same CheckForRobotsOnTop()/CheckForBlocksOnTop() cascade TakeDamage() already uses on a real
    // hit, so the entire above-stack wakes and falls together, same as when a player destroys the
    // base of a tower mid-game.
    public void SettleIfUnsupported()
    {
        if (IsDestroyed || NeverFalls || _rb.bodyType != RigidbodyType2D.Static) return;
        const int groundAndBlockMask = (1 << 6) | (1 << 8); // Ground=6, Block=8 — see CLAUDE.md's layer table
        const float checkHeight = 0.08f;
        Bounds b = _col.bounds;
        Vector2 checkCenter = new Vector2(b.center.x, b.min.y - checkHeight * 0.5f);
        Vector2 checkSize   = new Vector2(b.size.x * 0.9f, checkHeight);
        if (Physics2D.OverlapBox(checkCenter, checkSize, 0f, groundAndBlockMask) == null)
        {
            _rb.bodyType = RigidbodyType2D.Dynamic;
            // Cascade to whatever was resting on THIS block — without this, everything above an
            // unsupported block stays frozen Static even once the block it depended on starts
            // falling. Same cascade TakeDamage() already runs on a real hit.
            CheckForRobotsOnTop();
            CheckForBlocksOnTop();
        }
    }

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
        // Self-healing, same pattern WoodBlock/StoneBlock already use for baseMaxHealth/
        // baseMass/bounciness — 2026-08-09, after an audit found Robot.prefab and both
        // WoodBlock.prefab/StoneBlock.prefab still serialized at 2.5/2 (the pre-2026-06-27
        // values) six weeks after the C# default changed to 1.0f/1.5f, because nothing ever
        // re-applies a [SerializeField] default once it's baked into an existing prefab. A
        // SceneSetup.cs Ensure...() fix would only self-correct on the next "Wire Scene
        // References" run — this is exactly how the original mismatch survived six weeks
        // unnoticed. Asserting here means every block always uses the current intended value
        // regardless of whatever happens to be serialized on its prefab.
        impulseDamageMultiplier = 1.0f;
        minDamageImpulse        = 1.5f;

        _rb  = GetComponent<Rigidbody2D>();
        // Continuous detection (2026-07-16) — with more blocks now genuinely falling under
        // gravity (see the _stayKinematic flip above and the collapse cascade this class already
        // runs), a dense stack toppling all at once can build real velocity before it reaches the
        // Ground collider, which is only 0.5 units thick (see CLAUDE.md Coordinate System). The
        // default Discrete mode only checks for overlap at each fixed-update's START/END position,
        // so a fast-enough-falling block can tunnel straight through a collider that thin in one
        // step and land through the floor instead of on it. AnimalBase/EggProjectile already use
        // Continuous for the same reason (fast-moving small colliders vs. thin geometry).
        _rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
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

    public void Initialise(float width, float height, WoodArtVariant artVariant = WoodArtVariant.Auto, int world = 1,
                           bool trimCollider = false)
    {
        _baseColor = _sr.color; // subclass Awake has already set the material colour

        // World 2 reskin — resolve the 5 shapes that have ice art (see the _sprNormalW2 etc.
        // field comment), falling back to the World 1 sprite whenever the ice counterpart isn't
        // wired. Every OTHER variant below still references these locals (not the raw W1 fields
        // directly) via its own `?? ` chain, so e.g. a Skew block on a World 2 level correctly
        // falls back to the ice "normal" tile rather than always landing on wood/stone art.
        bool   isW2       = world == 2;
        bool   isW3       = world == 3;
        bool   isW4       = world == 4;
        bool   isW5       = world == 5;
        bool   isW6       = world == 6;
        // Worlds 4-6 share one set of slots: the sky, sunken-city or mothership art (2026-11-12).
        Sprite hiNormal   = world switch { 9 => _sprNormalW9, 8 => _sprNormalW8, 7 => _sprNormalW7,
                                       _ => isW6 ? _sprNormalW6     : isW5 ? _sprNormalW5     : isW4 ? _sprNormalW4     : null };
        Sprite hiVertical = world switch { 9 => _sprVerticalW9, 8 => _sprVerticalW8, 7 => _sprVerticalW7,
                                       _ => isW6 ? _sprVerticalW6   : isW5 ? _sprVerticalW5   : isW4 ? _sprVerticalW4   : null };
        Sprite hiHoriz    = world switch { 9 => _sprHorizontalW9, 8 => _sprHorizontalW8, 7 => _sprHorizontalW7,
                                       _ => isW6 ? _sprHorizontalW6 : isW5 ? _sprHorizontalW5 : isW4 ? _sprHorizontalW4 : null };
        Sprite hiShort    = world switch { 9 => _sprShortW9, 8 => _sprShortW8, 7 => _sprShortW7,
                                       _ => isW6 ? _sprShortW6      : isW5 ? _sprShortW5      : isW4 ? _sprShortW4      : null };
        Sprite normal     = hiNormal   != null ? hiNormal   : (isW2 && _sprNormalW2     != null) ? _sprNormalW2     : (isW3 && _sprNormalW3     != null) ? _sprNormalW3     : _sprNormal;
        Sprite vertical   = hiVertical != null ? hiVertical : (isW2 && _sprVerticalW2   != null) ? _sprVerticalW2   : (isW3 && _sprVerticalW3   != null) ? _sprVerticalW3   : _sprVertical;
        Sprite horizontal = hiHoriz    != null ? hiHoriz    : (isW2 && _sprHorizontalW2 != null) ? _sprHorizontalW2 : (isW3 && _sprHorizontalW3 != null) ? _sprHorizontalW3 : _sprHorizontal;
        Sprite diagonal   = (isW2 && _sprDiagonalW2   != null) ? _sprDiagonalW2   : _sprDiagonal;
        Sprite shortSpr   = hiShort    != null ? hiShort    : (isW2 && _sprShortW2      != null) ? _sprShortW2      : (isW3 && _sprShortW3      != null) ? _sprShortW3      : _sprShort;
        bool   themed     = isW2 || (isW3 && _sprNormalW3 != null) || hiNormal != null;

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
            WoodArtVariant.Vertical      => vertical          ?? normal,
            WoodArtVariant.Horizontal    => horizontal        ?? normal,
            WoodArtVariant.Flat          => _sprFlat ?? normal,
            WoodArtVariant.Short         => shortSpr          ?? normal,
            WoodArtVariant.VerticalShort => _sprVerticalShort ?? vertical ?? normal,
            WoodArtVariant.Horizontal2D  => _sprHorizontal2D  ?? horizontal ?? normal,
            WoodArtVariant.Shork2D       => _sprShork2D       ?? vertical ?? normal,
            WoodArtVariant.Shork         => _sprShork         ?? normal,
            WoodArtVariant.Cart          => _sprCart          ?? normal,
            WoodArtVariant.Barrel        => _sprBarrelProp    ?? normal,
            // Frozen Tundra framed towers (2026-11-09): a stone box is an ice block, a stone slab a long ice slab.
            WoodArtVariant.Square        => themed ? normal : _sprSquare ?? normal,
            WoodArtVariant.Block         => themed ? horizontal ?? normal : _sprBlock ?? normal,
            WoodArtVariant.RuinedWall    => _sprRuinedWall    ?? normal,
            WoodArtVariant.Tower         => _sprTower         ?? normal,
            WoodArtVariant.Skew          => _sprSkew          ?? normal,
            WoodArtVariant.Diagonal      => diagonal          ?? normal,
            _ => aspect < 0.72f ? (vertical   ?? normal)
               : aspect > 1.5f  ? (horizontal ?? normal)
               : normal,
        };
        if (isW2 && _sprExplodeW2 != null) _sprExplode = _sprExplodeW2;
        if (isW3 && _sprExplodeW3 != null) _sprExplode = _sprExplodeW3;
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
        Footprint = new Vector2(width, height);
        // Trim the collider to the art (2026-11-08, user-approved): the block sprites carry transparent padding
        // (Plank_Horizontal's wood is only the middle 48% of its height, the vertical post 27% of its width), so a
        // full-rect collider let woken blocks rest on and collide with empty space. The sprite's tight mesh hugs the
        // visible pixels; its vertex bounds are in the same local units as `native`. Full-rect sprites (or none)
        // keep the full footprint. Health and mass still come from the footprint, so balance is unchanged.
        // Only on levels laid out by visible edges (ReferenceFraming): the older hand-dumped layouts rely on the
        // padded colliders to touch — trimming them leaves blocks over gaps in 109 levels
        // (tools/check_block_support.py), so they keep full-rect colliders until they're re-laid out.
        _trimCollider = trimCollider;
        if (_trimCollider) TrimColliderToArt();

        // Trimmed blocks size their health and mass from the VISIBLE art, not the padded rect (2026-11-08, Angry
        // Birds pass: a post's rect is ~4x its visible wood, which made it ~4x too tough and too heavy to topple).
        Vector2 visible = _trimCollider && _col != null
            ? Vector2.Scale(_col.size, new Vector2(Mathf.Abs(transform.localScale.x), Mathf.Abs(transform.localScale.y)))
            : new Vector2(width, height);
        float area  = Mathf.Max(0.05f, visible.x * visible.y);
        float ratio = area / StdArea;

        MaxHealth = baseMaxHealth * ratio;
        Health    = MaxHealth;
        _rb.mass  = baseMass * ratio;

        var mat = new PhysicsMaterial2D("BlockMat") { bounciness = bounciness, friction = 0.8f };
        _col.sharedMaterial = mat;
        _rb.bodyType = RigidbodyType2D.Static;
        RememberRestPose();
    }

    /// <summary>The authored (width, height) from LevelData — the art's full rect, padding included.</summary>
    public Vector2 Footprint { get; private set; } = Vector2.one;

    /// <summary>Mirror the art (WorldLevelGenerator's mirrored towers) and keep the trimmed collider on it.</summary>
    public void SetFlipX(bool flip)
    {
        if (_sr != null) _sr.flipX = flip;
        TrimColliderToArt();
    }

    bool _trimCollider;
    const float TrimInset = 0.04f;   // world units taken off each trimmed collider dimension

    void TrimColliderToArt()
    {
        if (!_trimCollider) return;
        if (_col == null || _sr == null || _sr.sprite == null) return;
        Sprite sp = _sr.sprite;
        Vector2[] v = sp.vertices;
        if (v == null || v.Length < 3) return;
        Vector2 min = v[0], max = v[0];
        for (int i = 1; i < v.Length; i++) { min = Vector2.Min(min, v[i]); max = Vector2.Max(max, v[i]); }
        Vector2 size = max - min;
        Vector2 full = sp.bounds.size;
        if (size.x >= full.x * 0.98f && size.y >= full.y * 0.98f)
        {
            // Full-rect mesh or no padding (World 2's tightly cropped frozen pieces, 2026-11-09): nothing to trim, but
            // still pull the edges in by TrimInset like the trimmed path — without it stacked neighbours overlapped by
            // ~0.02 u and pushed each other apart the moment the stack woke (PhysicsProbe: World 2 towers collapsing
            // during the reveal).
            Vector3 fls = transform.localScale;
            _col.size = new Vector2(Mathf.Max(full.x * 0.5f, full.x - TrimInset / Mathf.Max(0.0001f, Mathf.Abs(fls.x))),
                                    Mathf.Max(full.y * 0.5f, full.y - TrimInset / Mathf.Max(0.0001f, Mathf.Abs(fls.y))));
            _col.offset = Vector2.zero;
            return;
        }
        Vector2 centre = (min + max) * 0.5f;
        if (_sr.flipX) centre.x = -centre.x;
        // The tight mesh sits ~0.02 u outside the visible pixels on each side, so stacked blocks overlapped by ~0.04
        // and pushed apart when woken (PhysicsProbe). Shrink by that much (world units -> local).
        Vector3 ls = transform.localScale;
        size -= new Vector2(TrimInset / Mathf.Max(0.0001f, Mathf.Abs(ls.x)), TrimInset / Mathf.Max(0.0001f, Mathf.Abs(ls.y)));
        _col.size   = new Vector2(Mathf.Max(size.x, full.x * 0.05f), Mathf.Max(size.y, full.y * 0.05f));
        _col.offset = centre;
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

    /// <summary>Editor physics probe (Editor/PhysicsProbe.cs): log every hit with its caller.</summary>
    public static bool ProbeLog;

    public void TakeDamage(float amount)
    {
        if (IsDestroyed || Indestructible) return;
        // Abilities, eggs, Bessie's slam and the barrel blast all call this: a plain block topples, it doesn't break.
        if (!BreaksOnHit) { Disturb(); return; }
        if (ProbeLog)
            Debug.Log($"[Probe] DAMAGE {name}@{transform.position} amount={amount:F2} health={Health:F2} t={Time.time:F2} " +
                      $"awake={(_rb != null && _rb.IsAwake())} via {new System.Diagnostics.StackTrace(1, false).ToString().Replace(System.Environment.NewLine, " | ")}");
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

    // ── Topple, don't break (2026-11-11, user: "structures should not explode - they should topple over when struck,
    // slow and then gravity takes over - knocking everything down... the only explosion is that of the dynamite barrel
    // - on first hit") ─────────────────────────────────────────────────────────────────────────────────────────────
    // A plain block (wood, stone, hay) is never destroyed by a hit any more: a strike wakes it and shoves it over
    // (ShoveOnHit / StackShove), and the collapse does the rest. Only a block that BreaksOnHit (the dynamite barrel)
    // goes off, on its first real hit. ForceDestroy (a boss crumbling its keep) is the one scripted exception.
    protected virtual bool BreaksOnHit => false;
    const float BarrelTriggerImpulse = 2f;   // falling debris this hard sets the dynamite off too

    protected virtual void OnCollisionEnter2D(Collision2D col)
    {
        if (IsDestroyed) return;
        if (IsLive && Time.time < _graceUntil) return;   // settling, not a hit
        ShoveOnHit(col);
        if (_rb.bodyType == RigidbodyType2D.Static &&
            col.rigidbody != null &&
            col.rigidbody.bodyType == RigidbodyType2D.Static)
            return;

        var other = col.rigidbody;
        bool struck = other != null && (other.GetComponent<AnimalBase>() != null || other.GetComponent<AbilityProjectile>() != null
                                        || other.GetComponent<EggProjectile>() != null);
        float impulse = col.relativeVelocity.magnitude * CalculateEffectiveMass(other);
        if (!struck && impulse < minDamageImpulse) return;
        if (ProbeLog) Debug.Log($"[Probe] HIT {name}@{transform.position} by {col.gameObject.name}@{col.transform.position} " +
                                $"relVel={col.relativeVelocity.magnitude:F2} otherType={(other != null ? other.bodyType.ToString() : "none")}");
        if (BreaksOnHit && !NeverFalls && !Indestructible && (struck || impulse >= BarrelTriggerImpulse))
        {
            TakeDamage(MaxHealth);
            return;
        }
        Disturb();
    }

    /// <summary>
    /// A hit that doesn't break the block: it wakes (a static block on an unframed level turns Dynamic) and whatever
    /// rests on it is told its support moved, so the collapse carries on under gravity. Hit sound, rate-limited.
    /// </summary>
    public void Disturb()
    {
        if (IsDestroyed || Indestructible) return;
        if (!_stayKinematic && _rb.bodyType == RigidbodyType2D.Static)
        {
            RememberRestPose();
            _rb.bodyType = RigidbodyType2D.Dynamic;
            CheckForRobotsOnTop();
            CheckForBlocksOnTop();
        }
        else if (_rb.bodyType == RigidbodyType2D.Dynamic) _rb.WakeUp();
        if (!_silentHit) PlayHitSound();
    }

    // ── Toppled score + floating watchdog ─────────────────────────────────────────────────────────────────────────
    // Blocks no longer break, so a block scores once it has visibly toppled (turned > 40 deg or dropped > 0.6 u).
    // And a live block left asleep with nothing under it (its support slid away without touching it again) is woken so
    // it falls (user: "structures that topple should not remain in the air randomly ... everything falls down").
    const float ToppleAngle = 40f, ToppleDrop = 0.6f, SupportProbeInterval = 0.35f, SupportProbeDepth = 0.25f;
    bool _toppleScored;
    Vector2 _restPos;
    float _restAngle, _nextSupportProbe;

    void FixedUpdate()
    {
        if (IsDestroyed || _rb == null || _rb.bodyType != RigidbodyType2D.Dynamic) return;
        if (!_toppleScored && _restPosSet)
        {
            float turned = Mathf.Abs(Mathf.DeltaAngle(_rb.rotation, _restAngle));
            if (turned > ToppleAngle || _restPos.y - _rb.position.y > ToppleDrop)
            {
                _toppleScored = true;
                ScoreManager.Instance?.AddBlockScore(this);
                FarmFury.Core.HighlightMarkers.BlockDestroyed(GetType().Name);
            }
        }
        if (!IsLive || _rb.IsAwake() || Time.time < _nextSupportProbe || Time.time < _graceUntil) return;
        _nextSupportProbe = Time.time + SupportProbeInterval + UnityEngine.Random.value * 0.1f;
        if (HasSupportBelow()) return;
        if (ProbeLog) Debug.Log($"[Probe] WATCHDOG woke {name}@{transform.position} t={Time.time:F2}");
        _rb.WakeUp();
    }

    bool _restPosSet;
    void RememberRestPose()
    {
        if (_rb == null) return;
        _restPos = _rb.position;
        _restAngle = _rb.rotation;
        _restPosSet = true;
    }

    bool HasSupportBelow()
    {
        if (_col == null || !_col.enabled) return true;
        // Probe just under the collider's lowest edge (works for tilted pieces too: the four corners' lowest point).
        Vector2 half = _col.size * 0.5f;
        float lowest = float.MaxValue, lx = 0f;
        for (int i = 0; i < 4; i++)
        {
            var corner = (Vector2)transform.TransformPoint(_col.offset + new Vector2(i < 2 ? -half.x : half.x, (i & 1) == 0 ? -half.y : half.y));
            if (corner.y < lowest) { lowest = corner.y; lx = corner.x; }
        }
        Bounds b = _col.bounds;
        var centre = new Vector2(b.center.x, lowest - SupportProbeDepth * 0.5f);
        var size = new Vector2(b.size.x * 0.9f, SupportProbeDepth + 0.04f);
        foreach (var hit in Physics2D.OverlapBoxAll(centre, size, 0f))
            if (hit != null && !hit.isTrigger && hit.attachedRigidbody != _rb && hit != _col) return true;
        return false;
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

    // Brief hang-time between the death VFX/SFX firing and the GameObject actually disappearing —
    // 2026-07-13, user request: "throughout gameplay, once the explosion event happens slightly
    // delay the disappearance for effect - not too long so it looks unnatural, but slightly
    // longer for effect." Previously Destroy(gameObject) ran the same frame the explosion/
    // fragments spawned, so the block's own sprite vanished at the exact instant its death burst
    // appeared rather than lingering through it even briefly. Lives here (not per-level data), so
    // it applies to every block type/level automatically — WoodBlock/ExplodingBarrelBlock both
    // route through this same base call (see their own DestroyBlock() overrides).
    private const float DestroyDelay = 0.12f;

    protected virtual void DestroyBlock()
    {
        if (IsLive) WakeNeighbours();
        if (IsDestroyed) return;
        IsDestroyed = true;
        FarmFury.Core.HighlightMarkers.BlockDestroyed(GetType().Name);
        // 2026-08-09 (user report: wood/stone left hanging mid-air, "unaffected by gravity" on
        // some levels). Re-run here unconditionally rather than relying on TakeDamage() to have
        // already done it: TakeDamage() calls this cascade on every hit (lethal or not) BEFORE
        // deciding whether to call DestroyBlock(), which covers the normal "player hits a block"
        // path — but ForceDestroy() (used by RobotEnemy.Die()'s _destroyOnDeath, e.g. L18's
        // Commander crumbling its own StoneTower) calls DestroyBlock() directly and skips
        // TakeDamage() entirely, so nothing ever told whatever was resting on that block that its
        // support just vanished. Both CheckForRobotsOnTop/CheckForBlocksOnTop only ever wake a
        // block/robot that's still Static, so this second call is a harmless no-op for the
        // already-cascaded TakeDamage() path and the only cascade for the ForceDestroy() path —
        // every real destruction now unconditionally wakes whatever depended on it, regardless of
        // which path triggered the destruction.
        CheckForRobotsOnTop();
        CheckForBlocksOnTop();
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
        // Destroy(obj, delay) only marks the object for deferred destruction after `delay`
        // seconds — transform/_col stay fully valid in the meantime, so WoodBlock.DestroyBlock()'s
        // subsequent DamageNearby() call (which runs a Physics2D query off this same transform) is
        // unaffected by the added delay.
        Destroy(gameObject, DestroyDelay);
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
        // Full width, not the old 0.9x-shrunk band (2026-07-19, user report: "structure still
        // floating in the air when struck"). The box's bottom edge already sits exactly at
        // b.max.y, so a same-level sideways neighbour (whose own top is also at b.max.y) never
        // pokes into this box regardless of X width — the 0.9x shrink was never actually needed to
        // exclude those, and only ever excluded genuine overlaps sitting near this block's own
        // edge (an overhanging plank, a Skew/Diagonal-art block whose collider is a plain
        // axis-aligned box under its angled sprite, anything placed slightly off-centre from a dump)
        // from ever being detected as resting on top, leaving them permanently Static/floating once
        // this block was destroyed.
        Vector2 checkSize   = new Vector2(b.size.x, checkHeight);
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
    //
    // Briefly widened to a box padded on every side (2026-07-13, "certain levels the wood hangs in
    // the air") and then REVERTED back to this directly-above-only column the same day — the
    // all-directions version was a real regression (user report: "level 11... takes only one
    // chicken and everything explodes... damage too easy"). Root cause verified directly from the
    // code, not guessed: TakeDamage() calls this on EVERY hit (lethal or not), and the padded
    // check woke every block within 0.35 units in ANY direction — including sideways and
    // downward — not just things resting on top. In a densely packed level (blocks placed nearly
    // edge-to-edge, e.g. L11), one hit could wake a block, which woke its immediate neighbours in
    // every direction, which each recursively woke THEIR neighbours the same way — a single
    // impact could cascade through nearly the entire structure in one frame, turning it all
    // Dynamic and colliding/toppling at once. The original floating-wood bug this padding was
    // meant to fix is now handled correctly and specifically by
    // SettleIfUnsupported()'s own cascade (added the same session) at level LOAD time, which
    // reuses this exact above-only method — so the ongoing in-game damage cascade no longer needs
    // to reach sideways/downward at all. Block colliders are already correctly re-fitted to their
    // visuals in Initialise(), so this narrow column reliably catches a directly-stacked neighbour
    // without reaching into an unrelated structure elsewhere in the level.
    // How long a tier of a collapsing stack waits before its own cascade continues upward —
    // 2026-08-09, "mechanism 2 option D" (user report: a whole tower could be released and
    // destroy everything in it in a single instant, far beyond whatever the player actually
    // touched). The block directly above a hit still wakes (Static -> Dynamic) immediately, same
    // as always — only the CONTINUATION of the cascade into whatever rests on THAT block is
    // deferred by this delay, so a tall structure comes down tier by tier over real time instead
    // of the entire stack letting go in the same physics frame. Does not reintroduce the old
    // "floating block" bug this cascade was built to fix (see the method comment below) — every
    // tier still eventually wakes and falls, it's just staggered, never skipped.
    const float CascadeStaggerDelay = 0.1f;

    void CheckForBlocksOnTop()
    {
        const int blockLayerMask = 1 << 8;
        const float checkHeight = 1.5f;
        Bounds b = _col.bounds;
        Vector2 checkCenter = new Vector2(b.center.x, b.max.y + checkHeight * 0.5f);
        // Full width, not the old 0.9x-shrunk band — see the matching comment in
        // CheckForRobotsOnTop() above (same fix, same reasoning, applied 2026-07-19).
        Vector2 checkSize   = new Vector2(b.size.x, checkHeight);
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
                // it only ever climbs through however many blocks are actually touching this one,
                // and stops the moment it finds no more Static blocks in range.
                //
                // The CONTINUATION beyond this tier is staggered (see CascadeStaggerDelay above)
                // rather than called inline here — this block itself still wakes and starts
                // falling this same frame (no change), only whatever rests on IT gets checked a
                // beat later, so a tall stack releases tier by tier instead of all at once.
                block.StartCoroutine(block.DelayedOnTopCascade());
            }
        }
    }

    IEnumerator DelayedOnTopCascade()
    {
        yield return new WaitForSeconds(CascadeStaggerDelay);
        if (this == null || IsDestroyed) yield break;
        CheckForRobotsOnTop();
        CheckForBlocksOnTop();
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
        Vector2 vis = EffectSizing.VisibleSize(_sr, out Vector3 centre);
        go.transform.position   = new Vector3(centre.x, centre.y, 0f);
        float sz = EffectSizing.ScaleFor(_sprExplode, EffectSizing.BurstDiameter(vis, _explodeSizeMultiplier));
        go.transform.localScale = new Vector3(sz, sz, 1f);
        go.transform.rotation   = Quaternion.Euler(0f, 0f, UnityEngine.Random.Range(-20f, 20f));
        go.AddComponent<FragmentFader>();
    }

    void SpawnImpactFlash()
    {
        var go = new GameObject("BlockFlash");
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite       = MakeSquareSprite();
        sr.color        = new Color(1f, 0.88f, 0.55f, 0.55f);
        sr.sortingOrder = 15;
        // A flash over the block's own visible shape (was a square 1.6x the collider's WIDTH — a 3 u square on a
        // long plank).
        Vector2 vis = EffectSizing.VisibleSize(_sr, out Vector3 centre);
        go.transform.position   = new Vector3(centre.x, centre.y, 0f);
        go.transform.rotation   = transform.rotation;
        go.transform.localScale = new Vector3(vis.x * 1.15f + 0.08f, vis.y * 1.15f + 0.08f, 1f);
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
