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
    [SerializeField] protected float baseMass    = 5f;
    [SerializeField] protected float bounciness  = 0.2f;

    [Header("Fragments")]
    [SerializeField] private GameObject[] _fragmentPrefabs;
    [SerializeField] private int          _fragmentCount = 5;
    [SerializeField] private float        _fragmentSpeed = 4f;

    public float MaxHealth   { get; private set; }
    public float Health      { get; private set; }
    public bool  IsDestroyed { get; private set; }

    public event System.Action<BlockBase> OnBlockDestroyed;

    protected Rigidbody2D   _rb;
    protected BoxCollider2D _col;
    protected SpriteRenderer _sr;

    private const float StdArea = 1.2f * 0.4f;

    protected virtual void Awake()
    {
        _rb  = GetComponent<Rigidbody2D>();
        _col = GetComponent<BoxCollider2D>();
        _sr  = GetComponent<SpriteRenderer>();
        if (_sr == null) _sr = gameObject.AddComponent<SpriteRenderer>();
        if (_sr.sprite == null) _sr.sprite = MakeSquareSprite();
        _sr.sortingOrder = 2;
    }

    public void Initialise(float width, float height)
    {
        transform.localScale = new Vector3(width, height, 1f);

        float area  = width * height;
        float ratio = area / StdArea;

        MaxHealth = baseMaxHealth * ratio;
        Health    = MaxHealth;
        _rb.mass  = baseMass * ratio;

        var mat = new PhysicsMaterial2D("BlockMat")
        {
            bounciness = this.bounciness,
            friction   = 0.8f,
        };
        _col.sharedMaterial = mat;
        _rb.bodyType = RigidbodyType2D.Static;
    }

    public void TakeDamage(float amount)
    {
        if (IsDestroyed) return;
        if (_rb.bodyType == RigidbodyType2D.Static)
            WakeAllStaticBlocks();

        Health = Mathf.Max(0f, Health - amount);
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

    protected virtual void OnHealthChanged()
    {
        if (_sr == null) return;
        float t = Health / MaxHealth;
        _sr.color = t > 0.5f
            ? Color.Lerp(new Color(1f, 0.6f, 0f), Color.white, (t - 0.5f) * 2f)
            : Color.Lerp(Color.red, new Color(1f, 0.6f, 0f), t * 2f);
    }

    protected virtual void DestroyBlock()
    {
        if (IsDestroyed) return;
        IsDestroyed = true;
        SpawnFragments();
        OnBlockDestroyed?.Invoke(this);
        ScoreManager.Instance?.AddBlockScore(this);
        Destroy(gameObject);
    }

    protected virtual void SpawnFragments()
    {
        if (_fragmentPrefabs == null || _fragmentPrefabs.Length == 0) return;
        for (int i = 0; i < _fragmentCount; i++)
        {
            var prefab = _fragmentPrefabs[Random.Range(0, _fragmentPrefabs.Length)];
            var frag   = Instantiate(prefab, transform.position, Random.rotation);
            if (frag.TryGetComponent<Rigidbody2D>(out var rb))
            {
                rb.linearVelocity  = Random.insideUnitCircle.normalized * _fragmentSpeed;
                rb.angularVelocity = Random.Range(-360f, 360f);
            }
            Destroy(frag, 2f);
        }
    }

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