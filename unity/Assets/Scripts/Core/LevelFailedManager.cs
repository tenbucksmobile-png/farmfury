using System.Collections;
using UnityEngine;

// Owns the Level Failed beat between LevelLoader calling GameManager.FailLevel() and the
// franchise Results screen: brief slow-motion -> GameFlow.ShowLevelFailed(). The robot taunt
// video that used to play here was removed 2026-11-02 (user request).
[DefaultExecutionOrder(-40)]
public class LevelFailedManager : MonoBehaviour
{
    [Header("Timing")]
    [SerializeField] private float _slowMotionScale    = 0.3f;
    [SerializeField] private float _slowMotionDuration  = 0.5f;

    private Coroutine _sequence;

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
        _sequence = StartCoroutine(PlaySequence());
    }

    private IEnumerator PlaySequence()
    {
        Time.timeScale = _slowMotionScale;
        yield return new WaitForSecondsRealtime(_slowMotionDuration);
        Time.timeScale = 1f;
        _sequence = null;
        FarmFury.UI.GameFlow.Instance?.ShowLevelFailed();
    }
}
