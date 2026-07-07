using System.Collections;
using UnityEngine;
using UnityEngine.Video;

// Owns the Level Complete "reward" beat between LevelLoader calling GameManager.CompleteLevel()
// and HUDController showing its score/stars panel. Old flow: state flips to LevelComplete ->
// HUDController shows the panel immediately. New flow: state flips to LevelComplete -> brief
// slow-motion -> hard freeze -> the last-fired animal's celebration video plays over the frozen
// level via VideoChromaKey -> fades out -> only then does HUDController.ShowLevelCompletePanel()
// get called. HUDController no longer reacts to GameState.LevelComplete by showing its panel
// itself (see its OnStateChanged) — this class is now the only thing that triggers it, so the
// celebration always plays first.
[DefaultExecutionOrder(-40)]
public class LevelCompleteManager : MonoBehaviour
{
    [Header("Scene References")]
    [SerializeField] private HUDController _hud;
    [SerializeField] private VideoChromaKey _videoChromaKey;

    [Header("Timing")]
    [SerializeField] private float _slowMotionScale    = 0.2f;
    [SerializeField] private float _slowMotionDuration  = 0.5f;
    // Celebration clips run ~4.04-4.05s (measured). This used to be 4f, which cut the hold off
    // fractionally before the clip's own last frame finished, then immediately started fading —
    // user-reported "ends very abruptly." Bumped to the clip length plus a full 1s of held-still
    // breathing room before the fade/panel transition begins.
    [SerializeField] private float _celebrationDuration = 5f;
    [SerializeField] private float _fadeOutDuration     = 0.3f;

    // Indexed by AnimalType (Cluck, Bessie, Percy, Woolly, Ducky, Horace, Gerald, Billy — see
    // LevelData.cs). Any slot left empty in the Inspector falls back to the Cluck clip (index 0)
    // so every animal still gets a celebration before every clip exists.
    [Header("Celebration Videos (indexed by AnimalType)")]
    [SerializeField] private VideoClip[] _celebrationClips = new VideoClip[8];

    // Accompanying one-shot laugh/cheer per animal, played in lockstep with the video above (see
    // VideoChromaKey.Play's audioClip parameter) — same AnimalType indexing and Cluck fallback.
    [Header("Celebration Audio (indexed by AnimalType)")]
    [SerializeField] private AudioClip[] _celebrationAudioClips = new AudioClip[8];

    private Coroutine _sequence;

    private void Awake()
    {
        if (_hud == null) _hud = FindAnyObjectByType<HUDController>();
        // Shared with LevelFailedManager — see VideoChromaKey.FindOrCreate(); Level Complete and
        // Level Failed celebrations never play at the same time, so both managers reuse the
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
        if (state != GameState.LevelComplete) return;
        if (_sequence != null) StopCoroutine(_sequence);
        _sequence = StartCoroutine(PlayCelebrationSequence());
    }

    private IEnumerator PlayCelebrationSequence()
    {
        Time.timeScale = _slowMotionScale;
        yield return new WaitForSecondsRealtime(_slowMotionDuration);
        Time.timeScale = 0f;

        AnimalType lastAnimal = CatapultLauncher.LastAnimalUsed;
        VideoClip clip = GetCelebrationClip(lastAnimal);
        if (_videoChromaKey != null && clip != null)
        {
            _videoChromaKey.Play(clip, GetCelebrationAudioClip(lastAnimal));
            yield return new WaitForSecondsRealtime(_celebrationDuration);
            yield return _videoChromaKey.FadeOut(_fadeOutDuration);
        }

        Time.timeScale = 1f;
        _sequence = null;
        _hud?.ShowLevelCompletePanel();
    }

    private VideoClip GetCelebrationClip(AnimalType animal)
    {
        int idx = (int)animal;
        VideoClip clip = (_celebrationClips != null && idx >= 0 && idx < _celebrationClips.Length)
            ? _celebrationClips[idx]
            : null;

        if (clip == null && _celebrationClips != null && _celebrationClips.Length > (int)AnimalType.Cluck)
            clip = _celebrationClips[(int)AnimalType.Cluck];

        return clip;
    }

    private AudioClip GetCelebrationAudioClip(AnimalType animal)
    {
        int idx = (int)animal;
        AudioClip clip = (_celebrationAudioClips != null && idx >= 0 && idx < _celebrationAudioClips.Length)
            ? _celebrationAudioClips[idx]
            : null;

        if (clip == null && _celebrationAudioClips != null && _celebrationAudioClips.Length > (int)AnimalType.Cluck)
            clip = _celebrationAudioClips[(int)AnimalType.Cluck];

        return clip;
    }
}
