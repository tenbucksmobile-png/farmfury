using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(BoxCollider2D))]
public class RobotEnemy : MonoBehaviour
{
    [SerializeField] private float _maxHealth               = 35f;
    [SerializeField] private float _impulseDamageMultiplier = 2.5f;
    [SerializeField] private float _minDamageImpulse        = 2f;

    public float Health      { get; private set; }
    public bool  IsDestroyed { get; private set; }

    private Rigidbody2D   _rb;
    private SpriteRenderer _sr;
    private LevelLoader   _loader;

    void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _sr = GetComponent<SpriteRenderer>();
        if (_sr == null) _sr = gameObject.AddComponent<SpriteRenderer>();
        if (_sr.sprite == null) _sr.sprite = MakeSquareSprite();
        _sr.color        = new Color(0.25f, 0.25f, 0.28f); // dark robot grey
        _sr.sortingOrder = 3;
        transform.localScale = new Vector3(0.6f, 0.8f, 1f); // ~30×40px at 50px/unit
    }

    public void Initialise(LevelLoader loader)
    {
        _loader = loader;
        Health  = _maxHealth;
    }

    public void TakeDamage(float amount)
    {
        if (IsDestroyed) return;
        Health = Mathf.Max(0f, Health - amount);
        if (Health <= 0f) Die();
    }

    void OnCollisionEnter2D(Collision2D col)
    {
        if (IsDestroyed) return;
        if (col.rigidbody == null) return;

        float effMass = col.rigidbody.bodyType == RigidbodyType2D.Static
            ? _rb.mass * 0.6f
            : (_rb.mass * col.rigidbody.mass) / (_rb.mass + col.rigidbody.mass);

        float impulse = col.relativeVelocity.magnitude * effMass;
        if (impulse >= _minDamageImpulse)
            TakeDamage(impulse * _impulseDamageMultiplier);
    }

    void Die()
    {
        IsDestroyed = true;
        _loader?.NotifyRobotDestroyed(this);
        Destroy(gameObject);
    }

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