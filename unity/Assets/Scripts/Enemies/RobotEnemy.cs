using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(BoxCollider2D))]
public class RobotEnemy : MonoBehaviour
{
    [SerializeField] private float _maxHealth               = 35f;
    [SerializeField] private float _impulseDamageMultiplier = 1.0f;
    [SerializeField] private float _minDamageImpulse        = 1.5f;

    public float Health      { get; private set; }
    public bool  IsDestroyed { get; private set; }

    // Wired by SceneSetup from Assets/Sprites/Enemies/Robot/Robot_Idle.png
    [SerializeField] private Sprite _robotSprite;

    // Hit-reaction art — wired only on HarvesterRobot.prefab from HarvesterRobot_Damaged.png
    // (no dedicated damaged art exists for the plain Robot.prefab yet, so this stays null there
    // and FlashDamage() falls back to its old plain white tint below).
    [SerializeField] private Sprite _robotDamagedSprite;

    // Death VFX/SFX — wired on all 3 robot prefabs (Robot/HarvesterRobot/SemiHarvesterRobot)
    // from Explosion.png / Explosion_Robot.mp3 (see SceneSetup.WireRobotDeathFx). Both null =
    // falls back to the existing procedural fragments (SpawnDeathParticles) and the
    // AudioManager.Sound.RobotDeath DSP jingle — user-requested 2026-07-09.
    [SerializeField] private Sprite     _deathExplosionSprite;
    [SerializeField] private AudioClip  _deathSoundOverride;

    // Steel blue-grey fallback when no art sprite is wired
    private static readonly Color BaseColor = new Color(0.38f, 0.44f, 0.54f);

    private Rigidbody2D    _rb;
    private SpriteRenderer _sr;
    private LevelLoader    _loader;
    private Color          _restColor;
    private float          _invincibleUntil; // ignores collision damage for 0.8s after spawn

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
    }

    public void TakeDamage(float amount)
    {
        if (IsDestroyed) return;
        Health = Mathf.Max(0f, Health - amount);
        StartCoroutine(FlashDamage());
        AudioManager.Play(AudioManager.Sound.RobotHit, 0.10f);
        if (Health <= 0f) Die();
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
        if (impulse >= _minDamageImpulse)
            TakeDamage(impulse * _impulseDamageMultiplier);
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
        // Multiplier lowered 1.8 -> 1.1 (2026-07-09, user-reported "a little bit oversized").
        float sz = Mathf.Max(transform.localScale.x, transform.localScale.y) * 1.1f;
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
