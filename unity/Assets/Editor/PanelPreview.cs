// Temporary QA automation — takes Play-mode screenshots of the Level Failed / Level Complete /
// Pause panels without needing OS-level input simulation (real gameplay would require dragging
// the cannon and deliberately missing/winning a level). Drives GameManager's public state-
// transition API directly, since HUDController shows each panel purely off GameManager's
// OnStateChanged event — no scripted gameplay needed to exercise the UI.
// Invoke via: Unity.exe -projectPath <proj> -executeMethod PanelPreview.Start (NOT -batchmode/
// -nographics — Play mode needs a real Game view to render/capture). Self-quits when done.
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class PanelPreview
{
    const string ShotDir = "C:/Users/Personel/AppData/Local/Temp/claude/C--Users-Personel-Desktop-FarmFury/6f091a27-a69a-421a-9844-f3c59c326ed5/scratchpad";

    enum Step { WaitPlaying, WaitGameManager, ShotFailed, ShotComplete, ShotPause, ExitPlay, Done }
    static Step _step;
    static int  _frameWait;

    [MenuItem("FarmFury/Debug/Run Panel Preview")]
    public static void Start()
    {
        var gameViewType = System.Type.GetType("UnityEditor.GameView,UnityEditor");
        var gameView = EditorWindow.GetWindow(gameViewType);
        gameView.Show();
        gameView.Focus();

        EditorSceneManager.OpenScene("Assets/Scenes/Game.unity", OpenSceneMode.Single);
        _step = Step.WaitPlaying;
        EditorApplication.EnterPlaymode();
        EditorApplication.update += Tick;
    }

    static void Tick()
    {
        switch (_step)
        {
            case Step.WaitPlaying:
                if (EditorApplication.isPlaying && !EditorApplication.isPaused)
                    _step = Step.WaitGameManager;
                break;

            case Step.WaitGameManager:
                if (GameManager.Instance != null && HUDController.Instance != null)
                {
                    GameManager.Instance.ForceStartLevel(0);
                    GameManager.Instance.FailLevel();
                    _frameWait = 10;
                    _step = Step.ShotFailed;
                }
                break;

            case Step.ShotFailed:
                if (--_frameWait <= 0)
                {
                    ScreenCapture.CaptureScreenshot($"{ShotDir}/lf_panel.png");
                    GameManager.Instance.ForceStartLevel(0);
                    GameManager.Instance.CompleteLevel();
                    _frameWait = 25; // Level Complete's star pops are staggered up to ~1.65s
                    _step = Step.ShotComplete;
                }
                break;

            case Step.ShotComplete:
                if (--_frameWait <= 0)
                {
                    ScreenCapture.CaptureScreenshot($"{ShotDir}/lc_panel.png");
                    GameManager.Instance.ForceStartLevel(0);
                    // OnPauseClicked() (and the pause button that called it) were removed
                    // 2026-07-26 — ShowPausePanel() is the direct replacement target, still
                    // private but reachable via SendMessage the same way OnPauseClicked was.
                    HUDController.Instance.SendMessage("ShowPausePanel");
                    _frameWait = 10;
                    _step = Step.ShotPause;
                }
                break;

            case Step.ShotPause:
                if (--_frameWait <= 0)
                {
                    ScreenCapture.CaptureScreenshot($"{ShotDir}/pause_panel.png");
                    _frameWait = 10;
                    _step = Step.ExitPlay;
                }
                break;

            case Step.ExitPlay:
                if (--_frameWait <= 0)
                {
                    EditorApplication.isPlaying = false;
                    _step = Step.Done;
                }
                break;

            case Step.Done:
                EditorApplication.update -= Tick;
                EditorApplication.Exit(0);
                break;
        }
    }
}
