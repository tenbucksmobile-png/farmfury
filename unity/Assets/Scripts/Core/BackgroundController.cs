using UnityEngine;

// Displays the sky painting behind all game objects and follows the camera
// so the backdrop appears infinite as the bird flies across the level.
public class BackgroundController : MonoBehaviour
{
    [SerializeField] private Sprite _skySprite;

    private Camera         _cam;
    private SpriteRenderer _sr;

    void Awake()
    {
        _cam = Camera.main;
        if (_cam == null) _cam = FindAnyObjectByType<Camera>();

        _sr = GetComponent<SpriteRenderer>();
        if (_sr == null) _sr = gameObject.AddComponent<SpriteRenderer>();
        _sr.sortingOrder = -100;
        _sr.sprite       = _skySprite;

        if (_cam != null)
        {
            var p = _cam.transform.position;
            transform.position = new Vector3(p.x, p.y, 0f);
        }
    }

    // Start() runs after all Awake() calls, so camera orthoSize is final
    // (CatapultLauncher.Awake sets orthoSize before this fires).
    void Start()
    {
        ScaleToFillCamera();
    }

    void LateUpdate()
    {
        if (_cam == null) return;
        var p = _cam.transform.position;
        transform.position = new Vector3(p.x, p.y, 0f);
    }

    void ScaleToFillCamera()
    {
        if (_cam == null || _sr == null || _sr.sprite == null) return;

        float camH = _cam.orthographicSize * 2f;
        float camW = camH * _cam.aspect;

        float sprH = _sr.sprite.bounds.size.y;
        float sprW = _sr.sprite.bounds.size.x;

        // 'Cover' scale — fills the screen with no gaps at any aspect ratio
        float scale = Mathf.Max(camW / sprW, camH / sprH);
        transform.localScale = new Vector3(scale, scale, 1f);
    }
}
