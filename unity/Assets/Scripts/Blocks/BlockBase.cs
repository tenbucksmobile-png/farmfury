using System;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(BoxCollider2D))]
public abstract class BlockBase : MonoBehaviour
{
    [Header("Health")]
    [SerializeField] protected float baseMaxHealth           = 20f;
    [SerializeField] protected float impulseDamageMultiplier = 2.5f;
    [SerializeField] protected float minDamageImpulse        = 2f;

    [Header("Physics")]
    [SerializeField] protected float baseMass   = 5f;
    [SerializeField] protected float bounciness = 0.2f;

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

    private SpriteRenderer _crackSR1;   // light cracks: visible below 67% health
    private SpriteRenderer _crackSR2;   // heavy cracks: visible below 33% health

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

        float area  = width * height;
        float ratio = area / StdArea;

        MaxHealth = baseMaxHealth * ratio;
        Health    = MaxHealth;
        _rb.mass  = baseMass * ratio;

        var mat = new PhysicsMaterial2D("BlockMat") { bounciness = bounciness, friction = 0.8f };
        _col.sharedMaterial = mat;
        _rb.bodyType = RigidbodyType2D.Static;
    }

    public void TakeDamage(float amount)
    {
        if (IsDestroyed) return;
        if (_rb.bodyType == RigidbodyType2D.Static)
            WakeAllStaticBlocks();

        Health = Mathf.Max(0f, Health - amount);
        PlayHitSound();
        OnHealthChanged();
        if (Health <= 0f) DestroyBlock();
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

    protected virtual void PlayHitSound() { }

    protected virtual void OnHealthChanged()
    {
        if (_sr == null) return;
        float t = Health / MaxHealth;

        // Colour shift: healthy (material colour) → orange → red
        _sr.color = t > 0.5f
            ? Color.Lerp(new Color(1f, 0.6f, 0f), Color.white, (t - 0.5f) * 2f)
            : Color.Lerp(Color.red, new Color(1f, 0.6f, 0f), t * 2f);

        // Crack stage 1: <67% health shows light cracks
        // Crack stage 2: <33% health shows heavy cracks on top
        if (_crackSR1 != null)
            _crackSR1.color = t < 0.67f ? new Color(0f, 0f, 0f, 0.7f) : Color.clear;
        if (_crackSR2 != null)
            _crackSR2.color = t < 0.33f ? new Color(0f, 0f, 0f, 0.9f) : Color.clear;
    }

    protected virtual void DestroyBlock()
    {
        if (IsDestroyed) return;
        IsDestroyed = true;
        CameraShake.Shake(0.12f, 0.15f);
        SpawnFragments();
        OnBlockDestroyed?.Invoke(this);
        ScoreManager.Instance?.AddBlockScore(this);
        Destroy(gameObject);
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

        // Procedural: tiny squares tinted from the block's current colour, no collider
        Color col = _sr != null
            ? new Color(_sr.color.r, _sr.color.g, _sr.color.b)
            : Color.grey;
        int count = UnityEngine.Random.Range(4, 7);
        for (int i = 0; i < count; i++)
        {
            var go = new GameObject("Frag");
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = MakeSquareSprite();
            float dim = UnityEngine.Random.Range(0.55f, 0.95f);
            sr.color = new Color(col.r * dim, col.g * dim, col.b * dim);
            sr.sortingOrder = 10;

            go.transform.position = transform.position +
                                    (Vector3)(UnityEngine.Random.insideUnitCircle * 0.15f);
            float s = UnityEngine.Random.Range(0.06f, 0.22f);
            go.transform.localScale = new Vector3(s, s, 1f);

            var rb = go.AddComponent<Rigidbody2D>();
            rb.gravityScale    = 2f;
            rb.linearVelocity  = UnityEngine.Random.insideUnitCircle.normalized *
                                 UnityEngine.Random.Range(3f, 7f);
            rb.angularVelocity = UnityEngine.Random.Range(-400f, 400f);

            Destroy(go, 1.2f);
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
        var rng   = new Random(seed);
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
