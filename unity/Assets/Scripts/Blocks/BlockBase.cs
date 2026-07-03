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

    // Optional hit-reaction art (e.g. Haybail_Damaged.png) — null on block types that don't
    // have one wired, in which case TakeDamage() only does the existing colour-tint/crack
    // feedback below, unchanged.
    [SerializeField] protected Sprite _sprDamaged;

    [Header("Fragments")]
    [SerializeField] private GameObject[] _fragmentPrefabs;
    [SerializeField] private int          _fragmentCount = 5;
    [SerializeField] private float        _fragmentSpeed = 4f;

    public float MaxHealth   { get; private set; }
    public float Health      { get; private set; }
    public bool  IsDestroyed { get; private set; }

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

    public void Initialise(float width, float height)
    {
        transform.localScale = new Vector3(width, height, 1f);
        _baseColor = _sr.color; // subclass Awake has already set the material colour

        // Select art sprite based on aspect ratio; falls back to procedural colour if none wired
        float aspect = width / height;
        Sprite chosen = aspect < 0.72f ? (_sprVertical   ?? _sprNormal)
                      : aspect > 1.5f  ? (_sprHorizontal ?? _sprNormal)
                      : _sprNormal;
        if (chosen != null)
        {
            _sr.sprite = chosen;
            _sr.color  = Color.white;
            _baseColor = Color.white;
        }
        _normalSprite = _sr.sprite; // restored after a _sprDamaged flash (see TakeDamage())

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

    public void TakeDamage(float amount)
    {
        if (IsDestroyed) return;
        if (_rb.bodyType == RigidbodyType2D.Static)
            WakeAllStaticBlocks();

        Health = Mathf.Max(0f, Health - amount);
        PlayHitSound();
        OnHealthChanged();
        PlayDamageFlash();
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
        CameraShake.Shake(0.22f, 0.20f);
        SpawnFragments();
        SpawnImpactFlash();
        AudioManager.Play(AudioManager.Sound.BlockDestroy, 0.05f);
        OnBlockDestroyed?.Invoke(this);
        ScoreManager.Instance?.AddBlockScore(this);
        Destroy(gameObject);
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

    static void WakeAllStaticBlocks()
    {
        foreach (var block in FindObjectsByType<BlockBase>(FindObjectsInactive.Exclude))
        {
            if (!block.IsDestroyed && block._rb.bodyType == RigidbodyType2D.Static)
                block._rb.bodyType = RigidbodyType2D.Dynamic;
        }
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
