using System.Collections;
using UnityEngine;
using UnityEngine.Video;

// Owns the Level Failed "defeat" beat between LevelLoader calling GameManager.FailLevel() and
// HUDController showing its Try Again/Menu panel. Old flow: state flips to LevelFailed ->
// HUDController shows the panel immediately. New flow: state flips to LevelFailed -> brief
// slow-motion (the "oh no" moment registering) -> hard freeze -> the current level's robot taunt
// video plays over the frozen level via VideoChromaKey -> fades out -> only then does
// HUDController.ShowLevelFailedPanel() get called. HUDController no longer reacts to
// GameState.LevelFailed by showing its panel itself (see its OnStateChanged) — this class is now
// the only thing that triggers it, so the taunt always plays first. Mirrors LevelCompleteManager
// exactly, but keys its clip off the level index (which robot the player is up against) rather
// than which animal was last fired.
[DefaultExecutionOrder(-40)]
public class LevelFailedManager : MonoBehaviour
{
    [Header("Scene References")]
    [SerializeField] private HUDController _hud;
    [SerializeField] private VideoChromaKey _videoChromaKey;

    [Header("Timing")]
    [SerializeField] private float _slowMotionScale    = 0.3f;
    [SerializeField] private float _slowMotionDuration  = 0.5f;
    // Taunt clips run ~4.04-4.05s (measured). This used to be 4f, which cut the hold off
    // fractionally before the clip's own last frame finished, then immediately started fading —
    // user-reported "ends very abruptly." Bumped to the clip length plus a full 1s of held-still
    // breathing room before the fade/panel transition begins.
    [SerializeField] private float _tauntDuration       = 5f;
    [SerializeField] private float _fadeOutDuration     = 0.3f;

    // Indexed by level number (0-based, matching GameManager.CurrentLevelIndex) — one taunt clip
    // per level's robot (e.g. Robot_Celebrations.mp4). Resize in the Inspector as levels are
    // added; any level index past the array's end, or an empty slot, falls back to index 0's
    // clip so every level still gets a taunt before every clip exists.
    [Header("Robot Taunt Videos (indexed by level number)")]
    [SerializeField] private VideoClip[] _robotTauntClips;

    // Accompanying one-shot taunt sound per level's robot (e.g. Robot_CelebrateSound.mp3), played
    // in lockstep with the video above (see VideoChromaKey.Play's audioClip parameter) — same
    // level-number indexing and index-0 fallback as _robotTauntClips.
    [Header("Robot Taunt Audio (indexed by level number)")]
    [SerializeField] private AudioClip[] _robotTauntAudioClips;

    private Coroutine _sequence;

    private void Awake()
    {
        if (_hud == null) _hud = FindAnyObjectByType<HUDController>();
        // Shared with LevelCompleteManager — see VideoChromaKey.FindOrCreate(); Level Complete
        // and Level Failed celebrations never play at the same time, so both managers reuse the
        // exact same overlay instance instead of creating a second one.
        if (_videoChromaKey == null) _videoChromaKey = VideoChromaKey.FindOrCreate();
    }

    private void OnEnable()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.OnStateChanged += HandleStateChanged;
    }

    private void OnDisable()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.OnStateChanged -= HandleStateChanged;

        if (_sequence != null) { StopCoroutine(_sequence); _sequence = null; }
        Time.timeScale = 1f;
    }

    private void HandleStateChanged(GameState state)
    {
        if (state != GameState.LevelFailed) return;
        if (_sequence != null) StopCoroutine(_sequence);
        _sequence = StartCoroutine(PlayTauntSequence());
    }

    private IEnumerator PlayTauntSequence()
    {
        Time.timeScale = _slowMotionScale;
        yield return new WaitForSecondsRealtime(_slowMotionDuration);
        Time.timeScale = 0f;

        int levelIndex = GameManager.Instance != null ? GameManager.Instance.CurrentLevelIndex : 0;
        VideoClip clip = GetTauntClip(levelIndex);
        if (_videoChromaKey != null && clip != null)
        {
            _videoChromaKey.Play(clip, GetTauntAudioClip(levelIndex));
            yield return new WaitForSecondsRealtime(_tauntDuration);
            yield return _videoChromaKey.FadeOut(_fadeOutDuration);
        }

        Time.timeScale = 1f;
        _sequence = null;
        _hud?.ShowLevelFailedPanel();
    }

    private VideoClip GetTauntClip(int levelIndex)
    {
        if (_robotTauntClips == null || _robotTauntClips.Length == 0) return null;

        VideoClip clip = (levelIndex >= 0 && levelIndex < _robotTauntClips.Length)
            ? _robotTauntClips[levelIndex]
            : null;

        return clip != null ? clip : _robotTauntClips[0];
    }

    private AudioClip GetTauntAudioClip(int levelIndex)
    {
        if (_robotTauntAudioClips == null || _robotTauntAudioClips.Length == 0) return null;

        AudioClip clip = (levelIndex >= 0 && levelIndex < _robotTauntAudioClips.Length)
            ? _robotTauntAudioClips[levelIndex]
            : null;

        return clip != null ? clip : _robotTauntAudioClips[0];
    }
}
