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
        // Only assign if wired — preserves the sprite already on the SpriteRenderer
        // (e.g. Background_SkyV1 with sprite set directly in the scene)
        if (_skySprite != null)
            _sr.sprite = _skySprite;

        if (_cam != null)
        {
            var p = _cam.transform.position;
            transform.position = new Vector3(p.x, p.y, 0f);
        }
    }

    void Start()
    {
        ScaleToFillCamera();
    }

    // Re-scales whenever orthoSize actually changes, not just once at Start() — CatapultLauncher.
    // OnLevelStarted() recomputes orthoSize per level (see ComputeOrthoSizeForLevel) well after
    // this Start() already ran, so a level needing a bigger orthoSize than whatever was current at
    // boot used to leave the backdrop too small to cover the new, wider view (2026-07-12 bug:
    // "the backdrop falls short of the safe area" on later levels). Comparing against the last-seen
    // orthoSize keeps this a cheap float compare on every other frame's LateUpdate.
    float _lastOrthoSize = -1f;

    void LateUpdate()
    {
        if (_cam == null) return;
        var p = _cam.transform.position;
        transform.position = new Vector3(p.x, p.y, 0f);

        if (!Mathf.Approximately(_cam.orthographicSize, _lastOrthoSize))
        {
            _lastOrthoSize = _cam.orthographicSize;
            ScaleToFillCamera();
        }
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
