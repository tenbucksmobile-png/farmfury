using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(BoxCollider2D))]
public class RobotEnemy : MonoBehaviour
{
    [SerializeField] private float _maxHealth               = 35f;
    [SerializeField] private float _impulseDamageMultiplier = 1.0f;
    [SerializeField] private float _minDamageImpulse        = 1.5f;

    // Precise damage-percentage model (2026-07-10, fourth balance pass — user report: "still too
    // easy, increase the damage for the robots; if directly struck by an animal, lose only 50%
    // damage — so technically it should be hit directly twice. If indirectly hit then lessen the
    // damage to 25% per explosion — so 4 things must explode around it for the robot to
    // explode... apply the math, let's see what difficulty it renders"). Replaces the previous
    // impulse-derived/flat-floor system for these two specific cases with a deterministic,
    // countable one: a direct animal collision (TakeDirectHitDamage) always costs exactly half
    // max HP regardless of impact speed/angle, so it's always exactly 2 direct hits to kill; an
    // explosion hit (TakeExplosionDamage — from a nearby block's WoodBlock.DamageNearby(), or a
    // robot's own support being destroyed under it) always costs exactly a quarter, so it's
    // always exactly 4 to kill via chain reaction alone (mixing the two obviously finishes
    // faster). Robot-vs-robot contact damage (_robotContactDamage below) is a separate,
    // unrequested category — left as its existing impulse-plus-floor formula.
    private const float DirectHitDamageFraction    = 0.5f;
    // 0.25 -> 0.20 (2026-07-10, immediate follow-up: "also reduce the damage from explosions to
    // 20% to robots") — 5 explosions now needed to kill a robot via chain reaction alone,
    // instead of 4. Direct-hit fraction above is unchanged.
    private const float ExplosionDamageFraction    = 0.20f;
    [SerializeField] private float _robotContactDamage      = 18f;

    public float Health      { get; private set; }
    public bool  IsDestroyed { get; private set; }

    // Wired by SceneSetup from Assets/Sprites/Enemies/Robot/Robot_Idle.png
    [SerializeField] private Sprite _robotSprite;

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

        StartCoroutine(IdleLookAroundRoutine());
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
        _rb.bodyType = RigidbodyType2D.Dynamic;

        // Structural collapse counts as one "explosion"-tier hit (see the damage-model comment
        // at the top of this class) — on top of whatever the eventual fall/landing impact adds
        // via OnCollisionEnter2D below, since a gentle drop onto the next block down could
        // otherwise produce near-zero impulse and read as "nothing happened" even though its
        // support was just blown out from under it.
        TakeExplosionDamage();
    }

    // Direct hit from a launched animal (Cluck etc.) — always exactly half max HP, regardless of
    // impact speed/angle. See the damage-model comment at the top of this class.
    public void TakeDirectHitDamage() => TakeDamage(_maxHealth * DirectHitDamageFraction);

    // Indirect hit from a nearby explosion (WoodBlock.DamageNearby()) or a structural collapse
    // (MakeDynamicFromSupportLoss()) — always exactly a quarter max HP. See the damage-model
    // comment at the top of this class.
    public void TakeExplosionDamage() => TakeDamage(_maxHealth * ExplosionDamageFraction);

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
        _loader?.NotifyRobotDestroyed(this);
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

        yield return new WaitForSeconds(0.06f);
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
