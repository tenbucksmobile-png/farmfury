using UnityEngine;

/// <summary>
/// The golden glow on an equipped cannon skin (2026-11-13, user: "put a golden glow around it to show it is special";
/// then "make the glow more prominent - the user should see they have a purchased cannon and not the norm"). Three
/// layers, all children of the FarmCannon so they follow the recoil and World 4's bob:
///   Rim    - the cannon's own sprite drawn as a soft gold silhouette (Resources/Shaders/SpriteSilhouette), 12% larger
///            and centred on the art, so a bright gold outline traces the whole cannon; pulses strongly.
///   Halo   - a wide soft gold radial glow behind it all (procedural), pulsing with the rim.
///   Sparks - eight twinkling star points circling the cannon.
/// Rim and halo draw at the mount's order (3) a hair nearer the camera, i.e. in front of the bales and behind the
/// cannon (4); the sparks draw over the cannon (5, like the smoke). Built and shown by <see cref="CatapultLauncher"/>
/// only while a skin is equipped.
/// </summary>
public class CannonSkinGlow : MonoBehaviour
{
    const int BehindOrder = 3, SparkOrder = 5;
    const float RimScale = 1.12f;       // the outline: the silhouette this much bigger than the cannon
    const float HaloPad = 1.75f;        // the halo vs the cannon art's visible size
    const float Period = 1.8f;          // seconds per pulse
    static readonly Color Gold = new(1f, 0.8f, 0.15f, 1f);
    static readonly Color HaloGold = new(1f, 0.72f, 0.1f, 1f);

    SpriteRenderer _cannon, _rim, _halo;
    readonly SpriteRenderer[] _sparks = new SpriteRenderer[8];
    Vector2 _size;   // the cannon art's visible size in world units
    float _t;

    public static CannonSkinGlow Attach(SpriteRenderer cannon)
    {
        var existing = cannon.transform.Find("SkinGlow");
        var glow = existing != null ? existing.GetComponent<CannonSkinGlow>() : null;
        if (glow == null)
        {
            var go = new GameObject("SkinGlow");
            go.transform.SetParent(cannon.transform, false);
            glow = go.AddComponent<CannonSkinGlow>();
            glow.Build();
        }
        glow._cannon = cannon;
        glow.Fit();
        return glow;
    }

    void Build()
    {
        _halo = Child("Halo", Radial(), BehindOrder, -0.04f);
        _rim = Child("Rim", null, BehindOrder, -0.05f);
        var shader = Shader.Find("FarmFury/SpriteSilhouette");
        if (shader != null) _rim.sharedMaterial = new Material(shader);
        else _rim.enabled = false;   // no silhouette shader: the halo and sparks still show
        for (int i = 0; i < _sparks.Length; i++) _sparks[i] = Child("Spark", Star(), SparkOrder, -0.06f);
    }

    SpriteRenderer Child(string name, Sprite sprite, int order, float z)
    {
        var sr = new GameObject(name).AddComponent<SpriteRenderer>();
        sr.transform.SetParent(transform, false);
        sr.transform.localPosition = new Vector3(0f, 0f, z);
        sr.sprite = sprite;
        sr.sortingOrder = order;
        return sr;
    }

    // Everything is placed in the cannon's local space around its art's centre; sizes are converted from world units.
    void Fit()
    {
        if (_cannon == null || _cannon.sprite == null) return;
        transform.localPosition = Vector3.zero;
        transform.localScale = Vector3.one;
        Bounds local = _cannon.sprite.bounds;
        Vector3 ls = _cannon.transform.lossyScale;
        float sx = Mathf.Max(0.001f, Mathf.Abs(ls.x)), sy = Mathf.Max(0.001f, Mathf.Abs(ls.y));
        _size = new Vector2(local.size.x * sx, local.size.y * sy);

        // Rim: the same sprite, scaled about the art's centre (so it grows evenly all round).
        _rim.sprite = _cannon.sprite;
        _rim.flipX = _cannon.flipX;
        _rim.transform.localScale = new Vector3(RimScale, RimScale, 1f);
        Vector3 c = local.center;
        _rim.transform.localPosition = new Vector3(c.x * (1f - RimScale), c.y * (1f - RimScale), -0.05f);

        // Halo: a 1 x 1 unit radial sprite stretched over the art, centred on it.
        _halo.transform.localPosition = new Vector3(c.x, c.y, -0.04f);
        _halo.transform.localScale = new Vector3(_size.x * HaloPad / sx, _size.y * HaloPad / sy, 1f);
        _t = 0f;
    }

    void Update()
    {
        if (_cannon == null) return;
        if (_rim.sprite != _cannon.sprite) Fit();   // a different skin was equipped
        _t += Time.deltaTime;
        float p = 0.5f + 0.5f * Mathf.Sin(_t * Mathf.PI * 2f / Period);
        _rim.color = new Color(Gold.r, Gold.g, Gold.b, Mathf.Lerp(0.7f, 1f, p));
        _halo.color = new Color(HaloGold.r, HaloGold.g, HaloGold.b, Mathf.Lerp(0.45f, 0.8f, p));

        Bounds local = _cannon.sprite.bounds;
        Vector3 ls = _cannon.transform.lossyScale;
        float sx = Mathf.Max(0.001f, Mathf.Abs(ls.x)), sy = Mathf.Max(0.001f, Mathf.Abs(ls.y));
        for (int i = 0; i < _sparks.Length; i++)
        {
            // Each star circles the cannon slowly and twinkles on its own beat.
            float a = _t * 0.45f + i * Mathf.PI * 2f / _sparks.Length;
            float tw = Mathf.Max(0f, Mathf.Sin(_t * 2.6f + i * 1.9f));
            Vector3 world = new(Mathf.Cos(a) * _size.x * 0.62f, Mathf.Sin(a) * _size.y * 0.58f, 0f);
            _sparks[i].transform.localPosition = new Vector3(local.center.x + world.x / sx, local.center.y + world.y / sy, -0.06f);
            float s = (0.16f + 0.22f * tw) * Mathf.Min(_size.x, _size.y);   // world units
            _sparks[i].transform.localScale = new Vector3(s / sx, s / sy, 1f);
            _sparks[i].transform.localRotation = Quaternion.Euler(0f, 0f, _t * 40f + i * 20f);
            _sparks[i].color = new Color(1f, 0.95f, 0.65f, tw);
        }
    }

    static Sprite _radial, _star;

    static Sprite Radial()
    {
        if (_radial != null) return _radial;
        const int N = 64;
        var tex = new Texture2D(N, N, TextureFormat.ARGB32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float dx = (x + 0.5f) / N * 2f - 1f, dy = (y + 0.5f) / N * 2f - 1f;
                float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
            }
        tex.Apply();
        _radial = Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), N);   // 1 x 1 unit
        return _radial;
    }

    // A four-pointed twinkle star, 1 x 1 unit.
    static Sprite Star()
    {
        if (_star != null) return _star;
        const int N = 64;
        var tex = new Texture2D(N, N, TextureFormat.ARGB32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float dx = Mathf.Abs((x + 0.5f) / N * 2f - 1f), dy = Mathf.Abs((y + 0.5f) / N * 2f - 1f);
                float arms = Mathf.Max(Mathf.Clamp01(1f - dx * 8f) * (1f - dy), Mathf.Clamp01(1f - dy * 8f) * (1f - dx));
                float core = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy) * 3f);
                float a = Mathf.Clamp01(arms + core);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        tex.Apply();
        _star = Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), N);
        return _star;
    }
}
