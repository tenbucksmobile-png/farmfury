using UnityEngine;

// Backdrop painting filling the horizontal gap between the cannon and the structures, which
// otherwise reads as blank sky during bird flight. Lives once in Game.unity — built/wired at
// Editor time by SceneSetup.EnsureEnvironmentDepthSystem() (visible in Edit mode, not just Play),
// same "one persistent GO, not per-level" pattern as AudioManager/HUDController — rather than
// once per level; LevelData/LevelLoader are untouched.
//
// 2026-07-17: originally rescaled itself to cover-fit the camera's current per-level orthoSize
// (CatapultLauncher.OnLevelStarted() called UpdateParallaxScale() on every level load). REMOVED
// the same day per user decision: the user hand-positions/scales Layer_Midground directly in the
// Editor to match a specific fixed composition (barn/tree/cannon/backdrop all placed together,
// e.g. see the reference mockup discussed in chat) — the runtime auto-rescale was silently
// overwriting that hand-placed transform back to its own cover-scale formula the instant Play
// started (Awake()) and on every subsequent level load, which is why the game never actually
// rendered the saved composition. This class now does nothing at runtime except the usual
// self-bootstrap sprite/material wiring fallback (for a scene that hasn't had Wire Scene
// References run yet) — whatever transform is saved on Layer_Midground in Game.unity is what
// renders, permanently, the same for every level.
[DefaultExecutionOrder(-80)]
public class EnvironmentDepthSystem : MonoBehaviour
{
    public static EnvironmentDepthSystem Instance { get; private set; }

    [SerializeField] Sprite _sprMidground;
    [SerializeField, Range(0f, 1f)] float _midgroundClipAbove = 1.0f;

    const int MidgroundSortingOrder = -35;

    static readonly int ClipAboveID = Shader.PropertyToID("_ClipAbove");

    Material _bandClipMaterial;

    void Awake()
    {
        Instance = this;

        // Reuses the persistent GameObject SceneSetup.EnsureEnvironmentDepthSystem() already
        // built into Game.unity (visible in Edit mode too, see that method's comment) instead of
        // creating a fresh duplicate — MakeLayer() only creates a new one, and only applies the
        // clip property block, as a self-bootstrap fallback for a scene that hasn't had Wire Scene
        // References run yet. Never touches transform.position/localScale — that's entirely
        // whatever was hand-placed and saved in the scene.
        MakeLayer("Layer_Midground", _sprMidground, MidgroundSortingOrder, _midgroundClipAbove);
    }

    void MakeLayer(string name, Sprite sprite, int sortingOrder, float clipAbove)
    {
        var existing = transform.Find(name);
        var go = existing != null ? existing.gameObject : new GameObject(name);
        if (existing == null) go.transform.SetParent(transform, false);

        var sr = go.GetComponent<SpriteRenderer>();
        if (sr == null) sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = sortingOrder;

        if (sr.sharedMaterial == null || sr.sharedMaterial.shader == null || sr.sharedMaterial.shader.name != "FarmFury/ParallaxBandClip")
        {
            // Fallback only — normally SceneSetup already assigned the shared
            // Assets/Materials/ParallaxBandClip.mat asset to this renderer.
            if (_bandClipMaterial == null)
            {
                var shader = Shader.Find("FarmFury/ParallaxBandClip");
                _bandClipMaterial = shader != null ? new Material(shader) : null;
            }
            if (_bandClipMaterial != null) sr.sharedMaterial = _bandClipMaterial;
        }

        var mpb = new MaterialPropertyBlock();
        sr.GetPropertyBlock(mpb);
        mpb.SetFloat(ClipAboveID, clipAbove);
        sr.SetPropertyBlock(mpb);
    }
}
