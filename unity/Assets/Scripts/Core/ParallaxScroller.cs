using UnityEngine;

// Moves this GameObject at a fraction of camera movement, creating parallax depth.
// Attach to background scenery props; set speed per layer:
//   speed = 0.0 → stays fixed in world space (maximum parallax)
//   speed = 0.3 → far background (barn, slow drift)
//   speed = 0.7 → near foreground (fences, fast drift)
//   speed = 1.0 → moves exactly with camera (no parallax, like BackgroundController)
//
// SceneryBuilder.AddParallax() attaches this component and sets .speed in one call.
public class ParallaxScroller : MonoBehaviour
{
    public float speed = 0.5f;

    private Transform _cam;
    private float     _startX;
    private float     _camStartX;

    void Start()
    {
        var c = Camera.main;
        if (c == null) c = FindAnyObjectByType<Camera>();
        if (c != null)
        {
            _cam       = c.transform;
            _camStartX = _cam.position.x;
        }
        _startX = transform.position.x;
    }

    void LateUpdate()
    {
        if (_cam == null) return;
        float camDelta = _cam.position.x - _camStartX;
        transform.position = new Vector3(
            _startX + camDelta * speed,
            transform.position.y,
            transform.position.z);
    }
}
