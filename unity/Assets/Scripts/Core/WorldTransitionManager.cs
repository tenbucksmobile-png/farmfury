using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Video;

// Plays a one-off transition video after the player clears the last level of a world, before
// landing back wherever the caller sends them next (see the L18 -> "Frozen Tundra" request).
// World 2 doesn't exist yet in this project at all (0% built, see CLAUDE.md roadmap) — this class
// only owns the video beat itself; there's nowhere else to land yet, so HUDController sends the
// player straight to the main menu once TryPlayTransition's callback fires, the same destination
// Home/Quit already use.
[DefaultExecutionOrder(-40)]
public class WorldTransitionManager : MonoBehaviour
{
    public static WorldTransitionManager Instance { get; private set; }

    [SerializeField] private VideoChromaKey _videoChromaKey;

    // Sparse map: _triggerLevelIndices[i] (0-based level index whose completion should show a
    // transition video) -> _transitionClips[i]. Today this is exactly one entry (L18 -> index 17
    // -> TransitionVideo_Draft.mp4, wired by SceneSetup.EnsureWorldTransitionManager()) but the
    // shape supports adding one per future world-ending level without new code.
    [Header("Transition Videos (sparse: level index -> clip)")]
    [SerializeField] private int[] _triggerLevelIndices = new int[0];
    [SerializeField] private VideoClip[] _transitionClips = new VideoClip[0];

    [SerializeField] private float _fadeOutDuration = 0.3f;

    private Coroutine _sequence;

    private void Awake()
    {
        Instance = this;
        if (_videoChromaKey == null) _videoChromaKey = VideoChromaKey.FindOrCreate();
    }

    // Starts the transition video and returns true if levelIndex has one wired; returns false
    // (starting nothing) if there's no clip for this level, so the caller can fall back to its
    // normal immediate behaviour instead of waiting on a callback that would never fire.
    public bool TryPlayTransition(int levelIndex, Action onComplete)
    {
        VideoClip clip = GetClipFor(levelIndex);
        if (clip == null) return false;

        if (_sequence != null) StopCoroutine(_sequence);
        _sequence = StartCoroutine(PlaySequence(clip, onComplete));
        return true;
    }

    private VideoClip GetClipFor(int levelIndex)
    {
        if (_triggerLevelIndices == null || _transitionClips == null) return null;
        int count = Mathf.Min(_triggerLevelIndices.Length, _transitionClips.Length);
        for (int i = 0; i < count; i++)
            if (_triggerLevelIndices[i] == levelIndex) return _transitionClips[i];
        return null;
    }

    private IEnumerator PlaySequence(VideoClip clip, Action onComplete)
    {
        // plainRender=true: this is a full backdrop-baked transition clip, not a green-screen
        // character clip — no chroma-key shader/sky-backdrop layer needed (see VideoChromaKey.Play).
        _videoChromaKey.Play(clip, null, true);
        yield return _videoChromaKey.WaitUntilFirstFrame(2f);

        // Hold for the clip's own real length rather than a hardcoded duration — this clip's
        // pacing isn't shared with the short per-animal celebration clips LevelCompleteManager
        // uses, so it needs its own timing, not a borrowed constant.
        double holdSeconds = clip.length > _fadeOutDuration ? clip.length - _fadeOutDuration : 0.0;
        yield return new WaitForSecondsRealtime((float)holdSeconds);
        yield return _videoChromaKey.FadeOut(_fadeOutDuration);

        _sequence = null;
        onComplete?.Invoke();
    }
}
