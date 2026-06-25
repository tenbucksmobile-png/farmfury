using System.Collections;
using UnityEngine;

public class CameraShake : MonoBehaviour
{
    public static CameraShake Instance { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
    }

    // Add a positional kick that decays over duration.
    // While CatapultLauncher is lerping the camera toward the bird,
    // the lerp naturally absorbs the kick — this creates the shake feel.
    public static void Shake(float intensity = 0.2f, float duration = 0.2f)
    {
        if (Instance != null)
            Instance.StartCoroutine(Instance.DoShake(intensity, duration));
    }

    IEnumerator DoShake(float intensity, float duration)
    {
        var cam = Camera.main;
        if (cam == null) yield break;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            float fade = 1f - elapsed / duration;
            cam.transform.position += new Vector3(
                Random.Range(-intensity, intensity) * fade,
                Random.Range(-intensity, intensity) * fade,
                0f);
            elapsed += Time.deltaTime;
            yield return null;
        }
    }
}
