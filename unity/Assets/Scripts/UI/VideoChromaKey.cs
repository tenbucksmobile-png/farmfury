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

    // 0.3 (original) -> 0.25 fixed most of Cluck's body (belly/wing/shadows), confirmed live —
    // remaining ghosting is isolated to thin features (legs, comb, wingtips), most likely because
    // yuv420p chroma subsampling (half-resolution colour info, baked in at Kling's own export —
    // no re-encode can recover it) disproportionately dilutes anything narrower than a couple of
    // luma pixels toward the surrounding background colour. Nudged down one more small step to
    // 0.20 for the same reason as the 0.3->0.25 change — do NOT jump further based on simulated
    // numbers alone: the RenderTextureReadWrite.Linear fix (see HandlePrepared) means Unity now
    // does real color-space-correct comparison that a flat sRGB Python/ffmpeg simulation doesn't
    // replicate, so further tuning needs to be verified live in-game, not computed offline.
    [Range(0f, 1f)]
    public float tolerance = 0.20f;

    private static readonly int KeyColorId = Shader.PropertyToID("_KeyColor");
    private static readonly int ToleranceId = Shader.PropertyToID("_Tolerance");

    private Material _material;
    private RenderTexture _renderTexture;
    private AudioSource _audioSrc;
    private bool _gotFirstFrame;
    private bool _plainRender;

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
        _videoPlayer.errorReceived += HandleError;
        // Reusing one shared VideoPlayer for both the Level Complete (Cluck) and Level Failed
        // (robot) clips means the RenderTexture from whichever clip played last is still sitting
        // there, with its last decoded frame in it, until the new clip's own first frame arrives.
        // frameReady tells us exactly when that first real frame lands so the RawImage can stay
        // hidden until then — without this, Play() used to activate the RawImage immediately,
        // which showed the *previous* clip's leftover frame (e.g. the robot) for however long the
        // new clip took to prepare, and forever if it silently never got that far.
        _videoPlayer.sendFrameReadyEvents = true;
        _videoPlayer.frameReady += HandleFrameReady;

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
    //
    // plainRender skips the chroma-key shader entirely and renders the clip's own pixels as-is —
    // for clips that already have their backdrop composited in at generation time (see the
    // "future clips skip green screen" decision in Known Issues / project memory) rather than
    // shot on a green screen. The separate _backgroundSprite sky layer is also suppressed in this
    // mode since it would just sit uselessly behind an already-opaque frame.
    public void Play(VideoClip clip, AudioClip audioClip = null, bool plainRender = false)
    {
        if (clip == null) return;

        // RawImage stays hidden until HandleFrameReady confirms the new clip's own first frame
        // has actually been decoded into the RenderTexture — see the comment in Awake() on why.
        // The sky backdrop is static art (not video output), so it's safe to show right away.
        _rawImage.gameObject.SetActive(false);
        _gotFirstFrame = false;
        _plainRender = plainRender;
        _rawImage.material = plainRender ? null : _material;

        if (_background != null)
            _background.gameObject.SetActive(!plainRender && _backgroundSprite != null);

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

    // Blocks (real time, safe under Time.timeScale == 0) until the clip passed to the most recent
    // Play() has actually produced its first frame, or timeoutSeconds elapses — whichever comes
    // first. LevelCompleteManager/LevelFailedManager call this right after Play(), before starting
    // their fixed on-screen hold timer, so a slow-to-decode clip doesn't have its entire visible
    // window eaten by decode latency: Play()'s Prepare()/first-frame delivery is asynchronous and
    // its timing isn't guaranteed, especially for a larger clip on a memory-constrained machine —
    // previously the hold timer started counting the instant Play() was called, so if the first
    // frame took longer to arrive than the hold duration, the RawImage could still be hidden (see
    // HandleFrameReady) when FadeOut() ran, and the clip would never have been visible at all.
    public IEnumerator WaitUntilFirstFrame(float timeoutSeconds)
    {
        float elapsed = 0f;
        while (!_gotFirstFrame && elapsed < timeoutSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
        if (!_gotFirstFrame)
            Debug.LogWarning($"[FarmFury] VideoChromaKey: timed out after {timeoutSeconds}s waiting for '{_videoPlayer.clip?.name}' to produce a first frame.");
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
        _gotFirstFrame = false;
        _plainRender = false;
        _rawImage.material = _material;
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
        Debug.Log($"[FarmFury] VideoChromaKey: Prepared '{source.clip?.name}' {width}x{height}, frameCount={source.frameCount}, canSetTime={source.canSetTime}.");
        if (_renderTexture == null || _renderTexture.width != width || _renderTexture.height != height)
        {
            if (_renderTexture != null) _renderTexture.Release();
            // RenderTextureReadWrite.Linear here means "don't apply sRGB<->Linear conversion on
            // read/write" (Unity's naming is backwards from what it sounds like) — required
            // because this project uses Linear color space (ProjectSettings m_ActiveColorSpace=1).
            // Without it, the default sRGB read/write mode auto-linearizes the raw decoded video
            // bytes when the shader samples them, while ChromaKeyVideo.shader's hardcoded
            // _KeyColor stays in sRGB space — every pixel's distance-to-key gets computed across
            // two different color spaces. This shift is small enough that Robot's low-saturation
            // palette is unaffected (forced opaque by the shader's saturation gate regardless),
            // but Cluck's saturated palette sits close enough to the opaque threshold that the
            // shift pushes it into the shader's partial-transparency band — the "ghost" look.
            _renderTexture = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            source.targetTexture = _renderTexture;
            _rawImage.texture = _renderTexture;
        }
        source.Play();
        Debug.Log($"[FarmFury] VideoChromaKey: Play() called on '{source.clip?.name}', isPlaying={source.isPlaying}, isPrepared={source.isPrepared}.");
    }

    // First real frame of the current clip has been decoded into the RenderTexture — safe to
    // reveal the RawImage now. Fires once per Play() in practice (isLooping=false, and we don't
    // care about later frames), but guarded with _gotFirstFrame in case it fires more than once
    // before Stop()/the next Play() resets the flag.
    private void HandleFrameReady(VideoPlayer source, long frameIdx)
    {
        if (_gotFirstFrame) return;
        _gotFirstFrame = true;
        Debug.Log($"[FarmFury] VideoChromaKey: First frame ready for '{source.clip?.name}' at frameIdx={frameIdx}. Revealing RawImage.");
        Debug.Log($"[FarmFury] VideoChromaKey: _rawImage.texture == _renderTexture: {_rawImage.texture == _renderTexture}, RT size {_renderTexture.width}x{_renderTexture.height}.");
        SampleRenderTexturePixels();

        _rawImage.gameObject.SetActive(true);
        var c = _rawImage.color;
        c.a = 1f;
        _rawImage.color = c;
    }

    // Diagnostic only: reads a few raw pixels straight out of the RenderTexture the VideoPlayer
    // is supposedly writing into, right when Unity's own API says the first frame is ready. This
    // is the only way to tell "Unity reports the video is playing" apart from "actual decoded
    // pixel data is landing in the texture" — they turned out not to be the same thing here.
    private void SampleRenderTexturePixels()
    {
        var prevActive = RenderTexture.active;
        RenderTexture.active = _renderTexture;
        var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        int cx = _renderTexture.width / 2;
        int cy = _renderTexture.height / 2;
        tex.ReadPixels(new Rect(cx, cy, 1, 1), 0, 0);
        tex.Apply();
        Color center = tex.GetPixel(0, 0);
        tex.ReadPixels(new Rect(4, 4, 1, 1), 0, 0);
        tex.Apply();
        Color corner = tex.GetPixel(0, 0);
        RenderTexture.active = prevActive;
        Destroy(tex);
        Debug.Log($"[FarmFury] VideoChromaKey: RT sample center({cx},{cy})={center}, corner(4,4)={corner}.");
    }

    // VideoPlayer swallows most decode failures instead of throwing — this is the only signal
    // Unity gives for "this clip didn't actually play." Surfacing it directly (rather than only
    // ever seeing "the RawImage silently never appeared") is what's needed to root-cause why one
    // clip (e.g. Cluck_Celebration.mp4) fails to produce any frames while another (e.g.
    // Robot_Celebration.mp4) plays fine through the exact same pipeline.
    private void HandleError(VideoPlayer source, string message)
    {
        Debug.LogError($"[FarmFury] VideoChromaKey: VideoPlayer error on '{source.clip?.name}': {message}");
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
        _videoPlayer.errorReceived -= HandleError;
        _videoPlayer.frameReady -= HandleFrameReady;

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
