using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(BoxCollider2D))]
public class RobotEnemy : MonoBehaviour
{
    [SerializeField] private float _maxHealth               = 35f;
    [SerializeField] private float _impulseDamageMultiplier = 1.0f;
    [SerializeField] private float _minDamageImpulse        = 1.5f;

    // Damage-factor model v2 (2026-07-10, fifth balance pass — user report: "from level 3 the
    // gameplay and destruction has become only one hit... we have made this too easy"). Root
    // cause of the "one hit" complaint: the previous model (see git history) let ANY wood plank
    // breaking fire a flat, no-falloff ExplosionDamageFraction hit at every robot within a wide
    // (1.6-2.0u) radius — in a packed L03+ tower, one swing could chain-break 4-5 neighbouring
    // planks, each one landing a "free" 20% hit with zero player skill involved. Redesigned
    // around 4 distinct, deliberately harder damage factors, each a fixed fraction of max HP
    // (still no impulse/speed dependency — a "hit" always costs the same regardless of how hard
    // the physics engine happened to resolve it):
    //   - Direct hit   (TakeDirectHitDamage) — a launched animal physically colliding with the
    //     robot. 2 hits to kill.
    //   - Explosion    (TakeExplosionDamage) — ONLY from a genuine explosive prop dying next to
    //     the robot (Haybale or ExplodingBarrelBlock — see WoodBlock._explodesOnRobots). Plain
    //     Wood breaking apart no longer triggers this at all (see WoodBlock.DamageNearby) — wood
    //     is now purely structural, see the Fall category below. 2 hits to kill.
    //   - Egg          (TakeEggDamage) — Cluck's Cluster Bomb ability, one call per egg that
    //     actually connects. Deliberately the weakest of the four (a supplementary tool, not a
    //     solo-kill weapon) — ~9 eggs (nearly two full 5-egg bursts) to kill unassisted.
    //   - Fall         (TakeFallDamage) — a robot's supporting block is destroyed and it drops
    //     (MakeDynamicFromSupportLoss). This is the ONLY damage wood's own destruction can still
    //     cause to a robot, and only when the robot was actually resting on the block that broke,
    //     not just "nearby" — matches the requested rule "wood breaking... should not affect any
    //     damage to the robot, this is only a structural feature... in its falling presents
    //     fractional damage." Weaker than a real explosion. ~7 falls to kill unassisted (in
    //     practice the subsequent landing impact via OnCollisionEnter2D usually adds more on top).
    // Sixth balance pass (2026-07-10, same day — user report: "I am not able to destroy all
    // robots within the 3 animals... retune the factors so 3 birds works"): raised Direct-hit and
    // Explosion 0.34/0.28 -> 0.55 each (both now exactly 2 hits to kill, matching this project's
    // long-standing "always exactly 2 direct hits" design language from the fourth balance pass,
    // just now shared by explosions too) so a level's 3 birds have real room to clear a dense
    // multi-robot layout (e.g. L05's 5 robots) via a mix of direct hits, an explosive prop or two,
    // and incidental fall/contact damage, without needing to widen blast radius back out (which
    // would reintroduce the "one hit chain-kills everything" bug this pass fixed). Egg/Fall left
    // untouched — still the deliberately weaker supplementary categories.
    // Robot-vs-robot contact damage (_robotContactDamage below) remains its own, separate,
    // impulse-plus-floor category, untouched by this pass.
    private const float DirectHitDamageFraction    = 0.55f;
    private const float ExplosionDamageFraction    = 0.55f;
    // Raised 0.12 -> 0.16 (2026-07-12, user request: "give the eggs slightly stronger damage") —
    // still deliberately the weakest per-hit category (a Cluster Bomb supplement, not a solo-kill
    // weapon), just a bit more impactful now (~6-7 connecting eggs to kill instead of ~9).
    private const float EggDamageFraction          = 0.16f;
    private const float FallDamageFraction         = 0.15f;
    [SerializeField] private float _robotContactDamage      = 18f;

    // Every Take*Damage() wrapper below computes its hit as `_maxHealth * fraction` — a
    // deliberate design choice so damage is impulse/speed-independent, but it has a side effect
    // that bit L18's Commander boss: since the SAME fraction applies regardless of how high
    // _maxHealth is set, raising a robot's _maxHealth alone changes NOTHING about how many hits
    // it takes to kill (a direct hit is always ~2 hits to kill any robot, whether _maxHealth is
    // 26 or 900 — the ratio cancels out). Found 2026-07-14, user report: "the commander must be
    // stronger than it is" — Commander's _maxHealth=90 (vs 26-35 for regular grunts) looked
    // tougher on paper but died in the exact same ~2 direct hits as any basic robot. This
    // multiplier (default 1f — no behaviour change for every existing robot type) scales
    // TakeDirectHitDamage/TakeEggDamage — the two categories a player directly causes by landing
    // a real hit — independent of _maxHealth, so a real "boss" can be given genuine extra
    // toughness. Wired to 0.61 on CommanderRobot.prefab (SceneSetup.EnsureCommanderRobotPrefab) —
    // 3 solid direct hits to kill instead of 2, sized to exactly this level's 3-bird budget.
    [SerializeField] private float _damageResistance = 1f;

    // Separate multiplier for the two PASSIVE/environmental damage categories — TakeExplosionDamage
    // (a nearby explosive prop dying) and TakeFallDamage (this robot's own support collapsing).
    // Added 2026-07-14, same session as _damageResistance above, same user request extended:
    // "make the commander stronger - it should take all three sprites to destroy - even with
    // falling structure." L18's redesigned staircase is built from destructible Stone/Wood
    // pieces plus 2 dynamite barrels around the Commander — without a SEPARATE multiplier here,
    // Explosion/Fall damage would apply the SAME _damageResistance as a direct hit (both
    // ExplosionDamageFraction and DirectHitDamageFraction are 0.55, identical), so one barrel
    // catching him in its blast plus 2 direct hits could finish him in "2.5 birds," undermining
    // the "must take all three" requirement — the collapsing structure would let the player
    // shortcut the fight instead of needing 3 genuine thrown-animal hits. Defaults to 1f (no
    // behaviour change for every other robot type, which has no boss-tier toughness to protect);
    // wired to a much lower 0.1 on Commander specifically, so structural collapse/explosions
    // chip only token damage and can't meaningfully shortcut the 3-hit requirement above.
    [SerializeField] private float _structuralDamageResistance = 1f;

    public float Health      { get; private set; }
    public bool  IsDestroyed { get; private set; }

    // Wired by SceneSetup from Assets/Sprites/Enemies/Robot/Robot_Idle.png
    [SerializeField] private Sprite _robotSprite;

    // Right-facing counterpart of _robotSprite (2026-07-18, user-supplied Robot_Pawn_right.png /
    // Robot_Harvestor_right.png / Robot_SemiHarvestor_right.png — hand-mirrored art, not a
    // runtime SpriteRenderer.flipX, since the user drew genuinely separate art rather than
    // wanting an automatic mirror). _robotSprite is the left-facing/rest pose by convention (the
    // "_right" suffix on the new files implies the existing base art already reads as facing
    // left). Drives IdlePatrolRoutine() below — null on CommanderRobot (no right-facing art
    // exists for it), which keeps the older in-place IdleLookAroundRoutine unchanged.
    [SerializeField] private Sprite _sprFacingRight;

    // Hit-reaction art — wired only on HarvesterRobot.prefab from HarvesterRobot_Damaged.png
    // (no dedicated damaged art exists for the plain Robot.prefab yet, so this stays null there
    // and FlashDamage() falls back to its old plain white tint below). BRIEF (0.15s) flash on
    // every non-lethal hit, unrelated to the persistent critical-state sprite below.
    [SerializeField] private Sprite _robotDamagedSprite;

    // Persistent "about to explode" pose (2026-07-10, user request: "apply [Robot_SemiHarvest_
    // Damage.png] to the semi_harvest robot when it is one hit away from exploding") — wired
    // only on SemiHarvesterRobot.prefab. Unlike _robotDamagedSprite above (a brief per-hit
    // flash that always reverts), this REPLACES the robot's rest pose permanently once its
    // health drops to/below CriticalHealthFraction of max — a robot at ~40% HP or less is
    // realistically one typical hit away from dying given current damage values (a Cluck impact
    // ~15-20, area-damage from an exploding block up to 20, a robot-vs-robot "complete hit"
    // 25+impulse — see the damage-rebalance entries in CLAUDE.md), so this reads as a visible
    // "one more hit and I'm done" warning rather than a fixed HP number. Null on every other
    // robot type until similar dedicated art exists for them.
    [SerializeField] private Sprite _criticalSprite;
    private const float CriticalHealthFraction = 0.4f;
    private bool _isCritical;

    // Second sprite in CommanderRobot's continuous taunt loop (Commander.png -> Commander_Alert.png
    // -> Commander_Hit.png, wired to _criticalSprite above — see TauntLoopRoutine()/_isTauntLoop
    // further down). Null on every other robot type, which never enters taunt-loop mode. Used to
    // drive a discrete health-threshold pose swap instead (normal -> alert -> critical as HP
    // dropped) until 2026-07-18, when the user asked for a continuous taunting animation instead —
    // see TauntLoopRoutine()'s comment for the full replacement.
    [SerializeField] private Sprite _alertSprite;

    // Blocks to force-destroy the instant this robot dies (Die(), before the death VFX even
    // starts) — added 2026-07-12, user request: "when commander explodes the whole tower should
    // destruct." Wired at runtime by LevelLoader.SpawnRobot() for RobotType.Commander only (an
    // Indestructible block can't be linked at prefab-authoring time since both the boss and its
    // guarded structure are freshly spawned per level from LevelData, not static scene objects).
    // Empty on every other robot type. ForceDestroy() bypasses the normal Indestructible guard
    // (see BlockBase) — this is a deliberate scripted exception, not a change to how
    // Indestructible behaves under ordinary damage/collapse-cascade.
    private readonly System.Collections.Generic.List<BlockBase> _destroyOnDeath = new();
    public void AddDestroyOnDeath(BlockBase block) { if (block != null) _destroyOnDeath.Add(block); }

    // Death VFX/SFX — wired on all 3 robot prefabs (Robot/HarvesterRobot/SemiHarvesterRobot)
    // from Explosion.png / Explosion_Robot.mp3 (see SceneSetup.WireRobotDeathFx). Both null =
    // falls back to the existing procedural fragments (SpawnDeathParticles) and the
    // AudioManager.Sound.RobotDeath DSP jingle — user-requested 2026-07-09.
    [SerializeField] private Sprite     _deathExplosionSprite;
    [SerializeField] private AudioClip  _deathSoundOverride;

    // Hit SFX — wired on all 3 robot prefabs from Assets/Audio/Robot_Hit.mp3 (2026-07-10, user
    // request: "remove all current sound effects attached [to a robot hit] and wire in this
    // sound"). Replaces the procedural AudioManager.Sound.RobotHit DSP ping entirely on every
    // TakeDamage() call — not layered alongside it, same "one dedicated clip, not several
    // stacked sounds" pattern already used for HaybaleBlock's explosion sound and RobotEnemy's
    // own death sound above. Null falls back to the old procedural ping so a robot missing this
    // wiring (e.g. mid-development) still has SOME hit sound rather than silence.
    [SerializeField] private AudioClip  _hitSoundOverride;

    // Steel blue-grey fallback when no art sprite is wired
    private static readonly Color BaseColor = new Color(0.38f, 0.44f, 0.54f);

    private Rigidbody2D    _rb;
    private SpriteRenderer _sr;
    private LevelLoader    _loader;
    private Color          _restColor;
    private float          _invincibleUntil; // ignores collision damage for 0.8s after spawn

    // Idle "look around" animation (2026-07-10, user request: "make the robots look alive... on
    // the spot movement look left and right"; redesigned same day, second pass — user-reported
    // the first version, a continuous sine-wave rotation, just read as "slightly shake" rather
    // than looking deliberately alive). No new art needed — a coroutine-driven burst cycle
    // instead of a continuous Update(): hold still for a random pause, then a quick discrete
    // turn to one side, hold, turn to the other side, hold, return to centre, repeat. Reads as a
    // robot actively glancing around rather than idly vibrating. Rotates the same transform the
    // BoxCollider2D lives on rather than a separate visual-only child — at this small angle/
    // scale the collider wobble is gameplay-imperceptible, not worth the added hierarchy
    // complexity for a handful of on-screen robots.
    private const float IdleTurnDegrees   = 14f;
    private const float IdleTurnDuration  = 0.18f; // time to rotate INTO a turn or back to centre
    private const float IdleHoldDuration  = 0.35f; // pause at each turned extreme
    private const float IdlePauseMin      = 1.2f;  // rest at centre between look-around bursts
    private const float IdlePauseMax      = 3.0f;

    // Left/right patrol shuffle (2026-07-18, user request: "apply to every robot sprite in all
    // levels as animation movement. So it appears as if the robots are alive and moving from one
    // direction to another. left and right") — only runs when _sprFacingRight is wired (see
    // Awake()); otherwise falls back to the pre-existing IdleLookAroundRoutine above unchanged.
    // Deliberately a small shuffle, not a real patrol across the level: robots are placed to
    // guard specific structures in already-tuned level layouts, and moving the actual
    // Rigidbody2D/collider (kept Static, not switched to Kinematic — see WalkTo()'s comment) a
    // large distance risks walking off a support ledge or overlapping a neighbouring robot in a
    // dense level. PatrolRange is comfortably smaller than the smallest robot-to-robot spacing
    // seen in any hand-built level layout to date.
    private const float PatrolRange       = 0.3f;  // world units either side of spawn X
    private const float PatrolSpeed       = 0.2f;  // world units/sec
    private const float PatrolHoldDuration = 0.4f; // pause at each walked extreme
    private const float PatrolPauseMin    = 1.0f;  // rest before the next walk
    private const float PatrolPauseMax    = 2.5f;

    // Continuous taunt-loop animation (2026-07-18, CommanderRobot only, user request: "animate
    // the robot between these three images as a loop - so it looks like the commander is taunting
    // the animals... once hit and exploding change to [Commander_Explode]"). Reuses the exact
    // sprites already wired for the old health-threshold pose system (_robotSprite/_alertSprite/
    // _criticalSprite = Commander/Commander_Alert/Commander_Hit — see EnsureCommanderRobotPrefab)
    // instead of that discrete "swap once and stay" progression: cycles through all three on a
    // fixed timer, continuously, regardless of health, replacing rather than layering on top of
    // both the old alert/critical threshold logic AND FlashDamage()'s per-hit sprite flash (both
    // skipped below when this is active — see TakeDamage()) so nothing fights the loop for
    // _sr.sprite. Detected automatically via `_alertSprite != null`, which today is unique to
    // Commander (no other robot type has alert art) — no new [SerializeField] needed. Die() swaps
    // straight to _deathExplosionSprite (Commander_Explode.png) before the squish/destroy
    // sequence, matching "once hit and exploding change to" the 4th image.
    private bool _isTauntLoop;
    private const float TauntFrameDuration = 0.6f;

    void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _sr = GetComponent<SpriteRenderer>();
        if (_sr == null) _sr = gameObject.AddComponent<SpriteRenderer>();

        bool hasArt      = _robotSprite != null;
        _sr.sprite       = hasArt ? _robotSprite : MakeSquareSprite();
        _restColor       = hasArt ? Color.white : BaseColor;
        _sr.color        = _restColor;
        _sr.sortingOrder = 3;

        transform.localScale = new Vector3(0.6f, 0.9f, 1f);

        // Prefab BoxCollider2D size is near-zero — set it here so robots land on blocks
        var col = GetComponent<BoxCollider2D>();
        if (col == null) col = gameObject.AddComponent<BoxCollider2D>();
        col.size = new Vector2(1f, 1f); // local; world = 0.6 × 0.9 u after scale

        // Prefab mass defaults to 1; override to match design spec (used only by the
        // effective-mass damage formula in OnCollisionEnter2D below — see bodyType note next).
        _rb.mass = 20f;

        // Prefab Rigidbody2D ships Dynamic with no constraints (Unity default) and nothing in
        // this class ever set it otherwise, so every robot free-fell under gravity from its
        // LevelData spawn Y to wherever its tiny 0.6x0.9 collider first found support — reported
        // 2026-07-18 as "begins in the right position then falls back down". Static matches
        // BlockBase.Initialise()'s existing convention for every block type (immovable
        // structure piece you destroy, not push around) and OnCollisionEnter2D's effMass
        // formula already special-cases a Static collision partner, so damage-on-hit is
        // unaffected — only the free-fall stops.
        _rb.bodyType = RigidbodyType2D.Static;

        if (!hasArt) AddEyes();

        _isTauntLoop = _alertSprite != null;

        // Commander (taunt-loop mode) skips both the walking patrol and the in-place idle-turn —
        // TauntLoopRoutine() is its own "looks alive" animation. Everything else: robots with a
        // right-facing sprite wired get the left/right walking patrol; anything with neither gets
        // the older in-place turn-and-glance. See the field comments on _isTauntLoop/
        // _sprFacingRight for why each one is a real second/third sprite, not a runtime flip.
        if (_isTauntLoop)
            StartCoroutine(TauntLoopRoutine());
        else if (_sprFacingRight != null)
            StartCoroutine(IdlePatrolRoutine());
        else
            StartCoroutine(IdleLookAroundRoutine());
    }

    // Cycles _robotSprite -> _alertSprite -> _criticalSprite -> repeat on a fixed timer, for as
    // long as the robot is alive — continues through damage (unlike the patrol/look-around
    // routines above), only stopping when IsDestroyed is set in Die().
    IEnumerator TauntLoopRoutine()
    {
        Sprite[] frames = { _robotSprite, _alertSprite, _criticalSprite };
        int i = 0;
        while (!IsDestroyed)
        {
            _sr.sprite = frames[i % frames.Length];
            i++;
            yield return new WaitForSeconds(TauntFrameDuration);
        }
    }

    // Loops while the robot is Static (resting) AND undamaged: pause -> quick turn to one side
    // -> hold -> turn to the other side -> hold -> back to centre -> repeat. Also stops the
    // instant the robot takes ANY damage (2026-07-10, user report: "robot positioning is also
    // moving when struck, they should remain static" — a robot mid-idle-turn taking a hit at the
    // same time read as an unwanted extra wobble on top of the actual hit reaction) — resets
    // rotation to neutral so it doesn't freeze mid-turn, and never resumes for that robot's
    // remaining lifetime (a damaged robot reads as alert/braced, not casually looking around).
    // Also stops instantly if it goes Dynamic (support-loss fall or a hard hit — physics owns
    // rotation from then on) or is destroyed.
    IEnumerator IdleLookAroundRoutine()
    {
        bool IsIdle() => !IsDestroyed && _rb.bodyType == RigidbodyType2D.Static && Health >= _maxHealth;

        while (IsIdle())
        {
            yield return new WaitForSeconds(Random.Range(IdlePauseMin, IdlePauseMax));
            if (!IsIdle()) break;

            float side = Random.value < 0.5f ? -1f : 1f;
            yield return RotateTo(side * IdleTurnDegrees, IdleTurnDuration);
            yield return new WaitForSeconds(IdleHoldDuration);
            if (!IsIdle()) break;

            yield return RotateTo(-side * IdleTurnDegrees, IdleTurnDuration * 2f);
            yield return new WaitForSeconds(IdleHoldDuration);
            if (!IsIdle()) break;

            yield return RotateTo(0f, IdleTurnDuration);
        }

        if (!IsDestroyed && _rb.bodyType == RigidbodyType2D.Static)
            transform.localRotation = Quaternion.identity;
    }

    IEnumerator RotateTo(float targetAngle, float duration)
    {
        float startAngle = transform.localEulerAngles.z;
        if (startAngle > 180f) startAngle -= 360f; // normalise to -180..180 for a short-way lerp
        float t = 0f;
        while (t < duration)
        {
            if (IsDestroyed || _rb.bodyType != RigidbodyType2D.Static) yield break;
            t += Time.deltaTime;
            float angle = Mathf.LerpAngle(startAngle, targetAngle, Mathf.Clamp01(t / duration));
            transform.localRotation = Quaternion.Euler(0f, 0f, angle);
            yield return null;
        }
        transform.localRotation = Quaternion.Euler(0f, 0f, targetAngle);
    }

    // Shuffles the robot a small distance left/right of its spawn X, swapping between _robotSprite
    // (facing left) and _sprFacingRight (facing right) to match the current direction of travel —
    // same stop conditions as IdleLookAroundRoutine (any damage, going Dynamic, or destroyed).
    // Directly drives transform.localPosition on a Static-bodied Rigidbody2D — the same pattern
    // RotateTo() above already uses for rotation, kept Static (not switched to Kinematic) so this
    // doesn't change how other scripts' collision-damage formulas treat this robot as a collision
    // partner (several check `col.rigidbody.bodyType == RigidbodyType2D.Static` — see
    // BlockBase.OnCollisionEnter2D — which would misclassify a Kinematic robot).
    IEnumerator IdlePatrolRoutine()
    {
        bool IsIdle() => !IsDestroyed && _rb.bodyType == RigidbodyType2D.Static && Health >= _maxHealth;

        float centerX = transform.localPosition.x;
        float y       = transform.localPosition.y;
        bool movingRight = Random.value < 0.5f;

        while (IsIdle())
        {
            yield return new WaitForSeconds(Random.Range(PatrolPauseMin, PatrolPauseMax));
            if (!IsIdle()) break;

            _sr.sprite = movingRight ? _sprFacingRight : _robotSprite;
            float targetX = centerX + (movingRight ? PatrolRange : -PatrolRange);
            yield return WalkTo(targetX, y);
            if (!IsIdle()) break;

            yield return new WaitForSeconds(PatrolHoldDuration);
            movingRight = !movingRight;
        }
    }

    IEnumerator WalkTo(float targetX, float y)
    {
        float startX = transform.localPosition.x;
        float distance = Mathf.Abs(targetX - startX);
        float duration = distance / PatrolSpeed;
        float t = 0f;
        while (t < duration)
        {
            if (IsDestroyed || _rb.bodyType != RigidbodyType2D.Static) yield break;
            t += Time.deltaTime;
            float x = Mathf.Lerp(startX, targetX, Mathf.Clamp01(t / duration));
            transform.localPosition = new Vector3(x, y, transform.localPosition.z);
            yield return null;
        }
        transform.localPosition = new Vector3(targetX, y, transform.localPosition.z);
    }

    void AddEyes()
    {
        AddEye(-0.18f, 0.20f);
        AddEye( 0.18f, 0.20f);
    }

    void AddEye(float localX, float localY)
    {
        var go = new GameObject("Eye");
        go.transform.SetParent(transform);
        go.transform.localPosition = new Vector3(localX, localY, -0.01f);
        go.transform.localScale    = new Vector3(0.26f, 0.24f, 1f);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite       = MakeSquareSprite();
        sr.color        = new Color(1f, 0.12f, 0.08f); // bright red eyes
        sr.sortingOrder = 4;
    }

    public void Initialise(LevelLoader loader)
    {
        _loader          = loader;
        Health           = _maxHealth;
        _invincibleUntil = Time.time + 0.8f; // ignore spawn-settling impacts
    }

    // Called by BlockBase.DestroyBlock() when a block directly beneath this robot is destroyed
    // (see BlockBase.CheckForRobotsOnTop) — a structural collapse should bring the robot down
    // with it rather than leaving it floating in place on a Static rigidbody that no longer has
    // anything under it. Switches to Dynamic so gravity takes over; subsequent collisions (e.g.
    // landing on a lower block, or the ground) go through the normal OnCollisionEnter2D damage
    // path like any other impact, making a knocked-down robot easier to finish off — user-
    // requested 2026-07-09.
    public void MakeDynamicFromSupportLoss()
    {
        if (IsDestroyed) return;
        // Guard against re-stacking fall damage: BlockBase.TakeDamage() now calls
        // CheckForRobotsOnTop() on EVERY hit to a supporting block, not just its destroying one
        // (2026-07-10, user request: "robots fall if structure under them are disturbed, even if
        // one haybale or wood is fractured") — so a tanky block (e.g. StoneBlock) taking several
        // non-lethal hits while a robot rests on it would otherwise call this once per hit. Once
        // truly falling (Dynamic), further disturbances to whatever's below don't matter — the
        // robot already left its perch.
        if (_rb.bodyType == RigidbodyType2D.Dynamic) return;
        _rb.bodyType = RigidbodyType2D.Dynamic;

        // Structural collapse counts as one "fall"-tier hit (its own, weaker category — see the
        // damage-model comment at the top of this class) — on top of whatever the eventual fall/
        // landing impact adds via OnCollisionEnter2D below, since a gentle drop onto the next
        // block down could otherwise produce near-zero impulse and read as "nothing happened"
        // even though its support was just blown out from under it. Deliberately NOT
        // TakeExplosionDamage() — a structural collapse from wood is not a real explosion, per
        // the 2026-07-10 fifth balance pass.
        TakeFallDamage();
    }

    // Direct hit from a launched animal (Cluck etc.) — always exactly this fraction of max HP,
    // regardless of impact speed/angle. See the damage-model comment at the top of this class.
    public void TakeDirectHitDamage() => TakeDamage(_maxHealth * DirectHitDamageFraction * _damageResistance);

    // A genuine explosive prop (Haybale or ExplodingBarrelBlock — see
    // WoodBlock._explodesOnRobots) dying within blast range. See the damage-model comment at the
    // top of this class. strengthMultiplier lets a specific prop type hit harder than the shared
    // base fraction (2026-07-10, user request: "these barrels must explode like the haybales,
    // only with stronger strength") — Haybale/plain explosive Wood pass the default 1x
    // (WoodBlock._explosionStrengthMultiplier's own default), ExplodingBarrelBlock overrides
    // theirs higher.
    public void TakeExplosionDamage(float strengthMultiplier = 1f) =>
        TakeDamage(_maxHealth * ExplosionDamageFraction * strengthMultiplier * _structuralDamageResistance);

    // One connecting egg from Cluck's Cluster Bomb ability. Deliberately the weakest category —
    // see the damage-model comment at the top of this class.
    public void TakeEggDamage() => TakeDamage(_maxHealth * EggDamageFraction * _damageResistance);

    // A structural collapse dropping this robot (MakeDynamicFromSupportLoss above) — "wood
    // breaking is structural only, falling presents fractional damage", per the 2026-07-10 fifth
    // balance pass. Weaker than a real explosion. See the damage-model comment at the top of
    // this class.
    public void TakeFallDamage() => TakeDamage(_maxHealth * FallDamageFraction * _structuralDamageResistance);

    public void TakeDamage(float amount)
    {
        if (IsDestroyed) return;
        Health = Mathf.Max(0f, Health - amount);
        bool killed = Health <= 0f;

        // The hit sound and the death sound (DeathSequence(), below) were both firing on the
        // killing blow and stepping on each other — user-reported 2026-07-10 ("the two sounds
        // are interfering with each other"). Only play the hit sound when this damage DOESN'T
        // kill the robot; a lethal hit goes straight to the death sound instead.
        if (!killed)
        {
            // Skipped entirely in taunt-loop mode (CommanderRobot) — TauntLoopRoutine() already
            // owns _sr.sprite, continuously cycling through these same three sprites regardless of
            // health, so the old discrete "swap once and stay" progression and FlashDamage()'s
            // per-hit flash would just fight it for the same field. Hit sound still plays either way.
            if (!_isTauntLoop)
            {
                // Enter the persistent "critical" pose exactly once, before FlashDamage() runs —
                // that coroutine captures whatever _sr.sprite currently is as the pose to restore
                // to after its brief flash, so setting it here means the critical sprite naturally
                // "sticks" through every flash from here on, no changes needed to FlashDamage() itself.
                if (!_isCritical && _criticalSprite != null && Health <= _maxHealth * CriticalHealthFraction)
                {
                    _isCritical = true;
                    _sr.sprite  = _criticalSprite;
                    _restColor  = Color.white;
                }

                StartCoroutine(FlashDamage());
            }
            if (_hitSoundOverride != null) AudioManager.PlayClip(_hitSoundOverride);
            else                            AudioManager.Play(AudioManager.Sound.RobotHit, 0.10f);
        }
        if (killed) Die();
    }

    void OnCollisionEnter2D(Collision2D col)
    {
        if (IsDestroyed) return;
        if (col.rigidbody == null) return;
        if (Time.time < _invincibleUntil) return; // settling after spawn — no fall damage

        float effMass = col.rigidbody.bodyType == RigidbodyType2D.Static
            ? _rb.mass * 0.6f
            : (_rb.mass * col.rigidbody.mass) / (_rb.mass + col.rigidbody.mass);

        float impulse = col.relativeVelocity.magnitude * effMass;

        // Robot-vs-robot contact always counts as damage (fundamental gameplay rule, applies to
        // every level automatically since it lives here rather than in per-level data) — a
        // toppled robot knocking into a neighbour shouldn't depend on clearing the same impulse
        // threshold a block-collision needs to register. ADDS the real impulse damage on top of
        // the guaranteed floor (not just whichever is bigger — changed 2026-07-10, see the field
        // comment above) so a genuinely hard collision can stack past a robot's remaining HP and
        // finish it outright ("complete hits").
        if (col.rigidbody.GetComponent<RobotEnemy>() != null)
        {
            TakeDamage(impulse * _impulseDamageMultiplier + _robotContactDamage);
        }
        // A launched animal (Cluck etc.) landing a direct hit — flat half max HP regardless of
        // impact speed/angle, see the damage-model comment at the top of this class. Checked
        // before the generic impulse fallback below so a weak-looking graze still counts as a
        // full "direct hit" rather than needing to clear _minDamageImpulse first.
        else if (col.rigidbody.GetComponent<AnimalBase>() != null)
        {
            TakeDirectHitDamage();
        }
        else if (impulse >= _minDamageImpulse)
        {
            TakeDamage(impulse * _impulseDamageMultiplier);
        }
    }

    // ── Feedback ──────────────────────────────────────────────────────────────

    IEnumerator FlashDamage()
    {
        if (_sr == null) yield break;

        if (_robotDamagedSprite != null)
        {
            // Dedicated hit-reaction pose (still the whole robot, just with an impact burst/red
            // eye baked into the art) already reads as "just got hit" on its own — swap the
            // whole sprite instead of the old plain white tint, then swap back once the flash
            // window ends (unless the robot died from this hit, in which case Die()'s squish/
            // destroy sequence takes over and there's nothing to revert on a destroyed object).
            Sprite normalSprite = _sr.sprite;
            _sr.sprite = _robotDamagedSprite;
            yield return new WaitForSeconds(0.15f);
            if (!IsDestroyed && _sr != null) _sr.sprite = normalSprite;
        }
        else
        {
            _sr.color = Color.white;
            yield return new WaitForSeconds(0.07f);
            if (!IsDestroyed && _sr != null) _sr.color = _restColor;
        }
    }

    void Die()
    {
        IsDestroyed = true;
        // Taunt-loop robots (CommanderRobot) show their own dedicated explosion art on their own
        // body as they die, not just the separate SpawnDeathExplosion() burst overlay every robot
        // gets — user request: "once hit and exploding change to [Commander_Explode]". IsDestroyed
        // is already true here, so TauntLoopRoutine() has already stopped touching _sr.sprite by
        // the time DeathSequence()'s squish animation runs.
        if (_isTauntLoop && _deathExplosionSprite != null && _sr != null)
            _sr.sprite = _deathExplosionSprite;
        PlayerStatsTracker.RecordRobotDestroyed();
        _loader?.NotifyRobotDestroyed(this);
        foreach (var block in _destroyOnDeath)
            if (block != null) block.ForceDestroy();
        StartCoroutine(DeathSequence());
    }

    IEnumerator DeathSequence()
    {
        if (_deathSoundOverride != null) AudioManager.PlayClip(_deathSoundOverride);
        else                              AudioManager.Play(AudioManager.Sound.RobotDeath);
        CameraShake.Shake(0.35f, 0.30f);
        SpawnDeathParticles();
        SpawnDeathExplosion();

        // Squish: flatten robot into the ground, then disappear
        Vector3 startScale = transform.localScale;   // (0.7, 0.8, 1)
        float t = 0f;
        const float squishDur = 0.14f;
        while (t < squishDur)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / squishDur);
            transform.localScale = new Vector3(
                Mathf.Lerp(startScale.x, 1.3f,  p),
                Mathf.Lerp(startScale.y, 0.04f, p),
                1f);
            yield return null;
        }

        // Held a little longer post-squish (0.06s -> 0.16s) so the death explosion/particles
        // (both spawned at the top of this coroutine, same frame as the squish starting) have a
        // bit more time on screen before the robot itself is gone — 2026-07-13, user request:
        // "once the explosion event happens slightly delay the disappearance for effect... apply
        // across the levels." Lives on the shared RobotEnemy class, so every robot type/level
        // gets this automatically.
        yield return new WaitForSeconds(0.16f);
        Destroy(gameObject);
    }

    // Comic-style explosion burst on death (Explosion.png) — sized off the robot's own
    // transform.localScale (its actual rendered size, e.g. L01's oversized Harvester) rather
    // than the collider bounds, which stay pinned to a small fixed 0.6x0.9 hitbox regardless of
    // visual scale (see SpawnRobot's collider re-derivation) and would give a disproportionately
    // tiny explosion on a large robot. Reuses FragmentFader (defined in BlockBase.cs, same
    // assembly) for the fade-out, same pattern as BlockBase.SpawnExplosion().
    void SpawnDeathExplosion()
    {
        if (_deathExplosionSprite == null) return;
        var go = new GameObject("RobotExplosion");
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite       = _deathExplosionSprite;
        sr.sortingOrder = 10;
        go.transform.position   = transform.position;
        // Multiplier lowered 1.8 -> 1.1 -> 0.6 -> 0.5 -> briefly 1.5 -> back to 0.5 (2026-07-10,
        // several corrections same day, final explicit request: "change the explosions back to
        // half size smaller"). Matches BlockBase.SpawnExplosion()'s own _explodeSizeMultiplier.
        float sz = Mathf.Max(transform.localScale.x, transform.localScale.y) * 0.5f;
        go.transform.localScale = new Vector3(sz, sz, 1f);
        go.AddComponent<FragmentFader>();
    }

    void SpawnDeathParticles()
    {
        int count = Random.Range(12, 18);
        for (int i = 0; i < count; i++)
        {
            var go = new GameObject("RobotFrag");
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = MakeSquareSprite();
            // 40% bright sparks, rest dark metal shards
            sr.color = Random.value > 0.60f
                ? new Color(1f, 0.80f + Random.value * 0.20f, 0.05f)
                : new Color(0.30f, 0.32f, 0.40f);
            sr.sortingOrder = 10;

            go.transform.position  = transform.position + (Vector3)(Random.insideUnitCircle * 0.18f);
            float s = Random.Range(0.04f, 0.18f);
            go.transform.localScale = new Vector3(s, s, 1f);

            float angle = (i / (float)count) * 360f + Random.Range(-25f, 25f);
            float speed = Random.Range(6f, 14f);
            var rb = go.AddComponent<Rigidbody2D>();
            rb.gravityScale    = 2.2f;
            rb.linearVelocity  = new Vector2(
                Mathf.Cos(angle * Mathf.Deg2Rad) * speed,
                Mathf.Sin(angle * Mathf.Deg2Rad) * speed);
            rb.angularVelocity = Random.Range(-600f, 600f);

            Destroy(go, 1.8f);
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    static Sprite MakeSquareSprite()
    {
        var tex = new Texture2D(4, 4, TextureFormat.ARGB32, false);
        var px  = new Color[16];
        for (int i = 0; i < 16; i++) px[i] = Color.white;
        tex.SetPixels(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
    }
}
