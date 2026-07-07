using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

// Drives a VideoPlayer through the FarmFury/ChromaKeyVideo shader onto a full-screen RawImage,
// so a green-screen character clip (e.g. an animal's Level Complete celebration) can play
// directly over the game scene with the #00B140 background keyed out. VideoPlayer is switched to
// RenderTexture mode here rather than relying on it being pre-configured in the inspector, since
// the target RenderTexture must match the clip's actual pixel dimensions, known only after
// Prepare(). Playback is driven explicitly via Play(clip)/FadeOut() (by LevelCompleteManager)
// rather than auto-starting in Awake() — this component has no opinion on which clip or when;
// it just renders whatever clip it's told to, then fades. Note VideoPlayer runs on its own
// wall-clock timeline regardless of Time.timeScale, so it plays correctly even while the game is
// frozen (Time.timeScale = 0) for the Level Complete freeze-frame.
[RequireComponent(typeof(VideoPlayer))]
public class VideoChromaKey : MonoBehaviour
{
    [SerializeField] private VideoPlayer _videoPlayer;
    [SerializeField] private RawImage _rawImage;
    [SerializeField] private Image _background;
    [SerializeField] private Sprite _backgroundSprite; // e.g. SkyPainting.png — sits behind the keyed video
    [SerializeField] private Shader _chromaKeyShader;
    [SerializeField] private Color _keyColor = new Color(0f, 0.6941177f, 0.2509804f, 1f); // #00B140

    [Range(0f, 1f)]
    public float tolerance = 0.3f;

    private static readonly int KeyColorId = Shader.PropertyToID("_KeyColor");
    private static readonly int ToleranceId = Shader.PropertyToID("_Tolerance");

    private Material _material;
    private RenderTexture _renderTexture;
    private AudioSource _audioSrc;

    // Finds the scene's existing overlay (there should only ever be one — Level Complete and
    // Level Failed celebrations never play simultaneously, so both LevelCompleteManager and
    // LevelFailedManager share this single instance) or builds the Canvas + RawImage + VideoPlayer
    // hierarchy from scratch, the same "nothing needs to be pre-wired" pattern HUDController uses
    // for its own Canvas. sortingOrder=150 sits above HUDController's Canvas (100, see
    // HUDController.BuildCanvas()) so the celebration draws over the HUD's dimmed backdrop, but
    // below WorldMap (300)/MainMenu (400), neither of which is ever visible mid-level anyway.
    public static VideoChromaKey FindOrCreate()
    {
        var existing = FindAnyObjectByType<VideoChromaKey>(FindObjectsInactive.Include);
        if (existing != null) return existing;

        var go = new GameObject("CelebrationVideo");
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 150;

        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        // Added BEFORE "RawImage" below so it renders first/underneath — Canvas draws children
        // in sibling order. Without this, the video's transparent (chroma-keyed) areas showed
        // straight through to whatever the frozen gameplay camera happened to be rendering
        // behind it (busy level art, robots, etc.), which read as messy/hard to see against —
        // a clean sky sits behind the character instead, regardless of which level is paused.
        var bgGO = new GameObject("Background", typeof(RectTransform));
        bgGO.transform.SetParent(go.transform, false);
        bgGO.AddComponent<Image>();

        var imageGO = new GameObject("RawImage", typeof(RectTransform));
        imageGO.transform.SetParent(go.transform, false);
        imageGO.AddComponent<RawImage>();

        // AddComponent<VideoChromaKey>() also satisfies its own [RequireComponent(typeof(VideoPlayer))].
        return go.AddComponent<VideoChromaKey>();
    }

    private void Awake()
    {
        if (_videoPlayer == null) _videoPlayer = GetComponent<VideoPlayer>();
        if (_rawImage == null) _rawImage = GetComponentInChildren<RawImage>(true);
        if (_background == null)
        {
            var bgTransform = transform.Find("Background");
            if (bgTransform != null) _background = bgTransform.GetComponent<Image>();
        }

        var shader = _chromaKeyShader != null ? _chromaKeyShader : Shader.Find("FarmFury/ChromaKeyVideo");
        _material = new Material(shader);
        _material.SetColor(KeyColorId, _keyColor);
        _material.SetFloat(ToleranceId, tolerance);

        FillScreen(_rawImage.rectTransform);
        _rawImage.material = _material;
        _rawImage.color = Color.white;
        _rawImage.gameObject.SetActive(false); // hidden until Play() is called

        // No sprite wired (e.g. a scene that hasn't run Wire Scene References since this was
        // added) -> stays fully transparent, same as before this existed, rather than covering
        // the frozen gameplay scene with a blank white/coloured rect.
        if (_background != null)
        {
            FillScreen(_background.rectTransform);
            _background.sprite = _backgroundSprite;
            _background.color = _backgroundSprite != null ? Color.white : Color.clear;
            _background.preserveAspect = false; // stretch to fill, matches MainMenuController's landing art
            _background.gameObject.SetActive(false); // shown/hidden in lockstep with the video below
        }

        _videoPlayer.renderMode = VideoRenderMode.RenderTexture;
        _videoPlayer.playOnAwake = false;
        _videoPlayer.isLooping = false;
        _videoPlayer.prepareCompleted += HandlePrepared;

        // Accompanying one-shot audio (e.g. Cluck_CelebratingLaugh.mp3 / Robot_CelebrateSound.mp3)
        // — separate from the VideoPlayer's own audio track since the source clips have none/
        // aren't guaranteed to; started and faded in lockstep with the video in Play()/FadeOut().
        _audioSrc             = gameObject.AddComponent<AudioSource>();
        _audioSrc.playOnAwake = false;
        _audioSrc.loop        = false;
    }

    // Starts playing clip over the frozen (or live) scene, with an optional accompanying one-shot
    // audioClip (e.g. the animal's celebratory laugh or the robot's taunt sound) started in the
    // same frame. Safe to call repeatedly with a different clip — HandlePrepared only reallocates
    // the RenderTexture if dimensions changed.
    public void Play(VideoClip clip, AudioClip audioClip = null)
    {
        if (clip == null) return;

        _rawImage.gameObject.SetActive(true);
        var c = _rawImage.color;
        c.a = 1f;
        _rawImage.color = c;

        if (_background != null) _background.gameObject.SetActive(_backgroundSprite != null);

        _videoPlayer.clip = clip;
        _videoPlayer.Prepare();

        if (audioClip != null)
        {
            _audioSrc.mute   = !AudioManager.SfxEnabled;
            _audioSrc.volume = 1f;
            _audioSrc.clip   = audioClip;
            _audioSrc.Play();
        }
    }

    // Fades the video (and any accompanying audio) out over duration (unscaled — safe to call
    // while Time.timeScale is 0 during the Level Complete/Failed freeze), then stops playback
    // and hides the RawImage.
    public IEnumerator FadeOut(float duration)
    {
        float startAlpha    = _rawImage.color.a;
        float startBgAlpha  = _background != null ? _background.color.a : 0f;
        float startVolume   = _audioSrc.volume;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            var c = _rawImage.color;
            c.a = Mathf.Lerp(startAlpha, 0f, t);
            _rawImage.color = c;
            if (_background != null)
            {
                var bc = _background.color;
                bc.a = Mathf.Lerp(startBgAlpha, 0f, t);
                _background.color = bc;
            }
            _audioSrc.volume = Mathf.Lerp(startVolume, 0f, t);
            yield return null;
        }
        Stop();
    }

    public void Stop()
    {
        _videoPlayer.Stop();
        _audioSrc.Stop();
        var c = _rawImage.color;
        c.a = 0f;
        _rawImage.color = c;
        _rawImage.gameObject.SetActive(false);

        if (_background != null)
        {
            _background.gameObject.SetActive(false);
            var bc = _background.color;
            bc.a = _backgroundSprite != null ? 1f : 0f; // reset for next Play()
            _background.color = bc;
        }
    }

    private void HandlePrepared(VideoPlayer source)
    {
        int width = (int)source.width;
        int height = (int)source.height;
        if (_renderTexture == null || _renderTexture.width != width || _renderTexture.height != height)
        {
            if (_renderTexture != null) _renderTexture.Release();
            _renderTexture = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32);
            source.targetTexture = _renderTexture;
            _rawImage.texture = _renderTexture;
        }
        source.Play();
    }

    private void Update()
    {
        _material.SetFloat(ToleranceId, tolerance);
    }

    private static void FillScreen(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.anchoredPosition = Vector2.zero;
    }

    private void OnDestroy()
    {
        _videoPlayer.prepareCompleted -= HandlePrepared;

        if (_renderTexture != null)
        {
            _renderTexture.Release();
            _renderTexture = null;
        }

        if (_material != null)
        {
            Destroy(_material);
        }
    }
}
