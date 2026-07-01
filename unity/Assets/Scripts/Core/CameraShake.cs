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

        // Fixed 2026-07-08: this used to do `cam.transform.position += <random>` every frame
        // with no corresponding subtraction — each shake permanently random-walked the camera
        // and never returned it. The comment above assumed CatapultLauncher's follow-lerp would
        // "absorb" the kick, but Fire() sets _cameraFollowing=false for the whole flight ("camera
        // stays fixed"), so nothing was ever correcting the drift — every block destroyed
        // (BlockBase.DestroyBlock() calls Shake() unconditionally) added another uncorrected
        // random offset. Reported as "the entire screen shifts upwards as the haybails are
        // destroyed". Now tracks the shake's own contribution as a delta and always nets to
        // exactly zero by the end, so it can't accumulate — and stays safe even if something
        // else (e.g. SmoothFollowAnimal) is also moving the camera at the same time, since it
        // only ever adds/removes its own offset rather than overwriting the whole position.
        Vector3 lastOffset = Vector3.zero;
        float   elapsed    = 0f;
        while (elapsed < duration)
        {
            float fade = 1f - elapsed / duration;
            Vector3 offset = new Vector3(
                Random.Range(-intensity, intensity) * fade,
                Random.Range(-intensity, intensity) * fade,
                0f);
            cam.transform.position += offset - lastOffset;
            lastOffset = offset;
            elapsed += Time.deltaTime;
            yield return null;
        }
        cam.transform.position -= lastOffset; // fully restore — net contribution is exactly zero
    }
}
