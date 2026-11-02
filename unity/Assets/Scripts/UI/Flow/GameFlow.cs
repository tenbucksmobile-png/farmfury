using FarmFury.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace FarmFury.UI
{
    /// <summary>
    /// Owns the menu screens and the navigation between them — FarmFury's port of FarmFury_Stampede's GameFlow
    /// (franchise flow, 2026-11-02):
    ///   Landing -> World Select -> Level Select -> Character Select -> Playing (HUD) -> results -> Level Select.
    /// Screens are built in code (UIKit) under one ScreenSpaceOverlay canvas (sortingOrder 400, above the HUD's 100
    /// and the celebration video's 150); GameManager's state decides which is visible. Going to a menu unloads the
    /// level (LevelLoader.UnloadLevel) so nothing simulates behind an opaque screen.
    /// Replaces MainMenuController, WorldMapController, World2MapController, World2LandingController,
    /// MatchUpScreen and World2CharacterSelectScreen (all removed). Art comes from <see cref="MenuArt"/>, wired by
    /// Wire Scene References (SceneSetup.EnsureGameFlow).
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class GameFlow : MonoBehaviour
    {
        [SerializeField] private MenuArt _menuArt = new();

        public static GameFlow Instance { get; private set; }

        public LandingScreen Landing { get; private set; }
        public WorldSelectScreen Worlds { get; private set; }
        public LevelSelectScreen Levels { get; private set; }
        public CharacterSelectScreen CharacterSelect { get; private set; }
        public MenuArt Art => _menuArt;
        public WorldType CurrentWorld { get; private set; } = WorldType.MeadowRuins;
        public Transform CanvasTransform { get; private set; }

        private GameManager _gm;
        private LevelLoader _loader;

        private void Awake()
        {
            Instance = this;
            _menuArt ??= new MenuArt();

            var canvas = UIKit.CreateCanvas("MenuCanvas", 400, transform);
            UIKit.EnsureEventSystem(transform);
            CanvasTransform = canvas.transform;

            Landing = new LandingScreen(canvas.transform, EnterWorldSelect, ExitGame, OpenMenuHub, _menuArt);
            Worlds = new WorldSelectScreen(canvas.transform, EnterLevelSelect, OpenWorldShop, EnterLanding, _menuArt);
            Levels = new LevelSelectScreen(canvas.transform, PickLevel, EnterWorldSelect, _menuArt);
            CharacterSelect = new CharacterSelectScreen(canvas.transform, LaunchLevel, CloseCharacterSelect, _menuArt);
        }

        private void Start()
        {
            _gm = GameManager.Instance;
            _loader = FindAnyObjectByType<LevelLoader>();
            if (_gm == null)
            {
                Debug.LogError("[GameFlow] No GameManager in the scene.");
                return;
            }

            if (SaveManager.Instance != null)
            {
                CurrentWorld = (WorldType)Mathf.Clamp(SaveManager.Instance.LastPlayedWorld, 0, (int)WorldType.CropFactory);
            }
            _gm.OnStateChanged += ApplyState;
            EnterLanding();
            ApplyState(_gm.State);   // the boot state is already MainMenu, so EnterLanding fires no change event
        }

        private void OnDestroy()
        {
            if (Instance == this) { Instance = null; }
            if (_gm != null) { _gm.OnStateChanged -= ApplyState; }
        }

        private void Update()
        {
            if (_gm == null) { return; }
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                HandleBack();
            }
            else if (Worlds.Root.activeSelf)
            {
                Worlds.HandleKeys(keyboard);
            }
        }

        /// <summary>Esc / Android back: one step back through the flow.</summary>
        public void HandleBack()
        {
            switch (_gm.State)
            {
                case GameState.CharacterSelect: CloseCharacterSelect(); break;
                case GameState.LevelSelect: EnterWorldSelect(); break;
                case GameState.WorldSelect: EnterLanding(); break;
                case GameState.Playing: _gm.PauseGame(); break;
                case GameState.Paused: _gm.ResumeGame(); break;
                case GameState.LevelComplete:
                case GameState.LevelFailed: QuitToLevelSelect(); break;
            }
        }

        // ------------------------------------------------------------ navigation

        public void EnterLanding()
        {
            LeaveGameplay();
            _gm.SetState(GameState.MainMenu);
        }

        public void EnterWorldSelect()
        {
            LeaveGameplay();
            _gm.SetState(GameState.WorldSelect);
        }

        public void EnterLevelSelect(WorldType world)
        {
            CurrentWorld = world;
            LeaveGameplay();
            _gm.SetState(GameState.LevelSelect);
        }

        /// <summary>A Level Select tile: locked levels do nothing, unlocked ones open Character Select.</summary>
        public void PickLevel(LevelData level)
        {
            if (level == null || SaveManager.Instance == null || !SaveManager.Instance.IsLevelUnlocked(level)) { return; }
            CharacterSelect.Open(level);
            _gm.SetState(GameState.CharacterSelect);
        }

        private void CloseCharacterSelect()
        {
            CharacterSelect.Hide();
            _gm.SetState(GameState.LevelSelect);
        }

        private void LaunchLevel(LevelData level, AnimalType[] lineup)
        {
            CurrentWorld = level.WorldType;
            _gm.StartLevel(level, lineup);
        }

        public void RestartLevel()
        {
            if (_gm.CurrentLevel == null) { return; }
            _gm.RestartLevel();
        }

        public void QuitToLevelSelect()
        {
            EnterLevelSelect(_gm.CurrentLevel != null ? _gm.CurrentLevel.WorldType : CurrentWorld);
        }

        /// <summary>
        /// Level Complete's play button: back to Level Select, where the next level now shows as the question mark;
        /// after a boss, World Select (which opens centred on the furthest unlocked world).
        /// </summary>
        public void PlayNextLevel()
        {
            var level = _gm.CurrentLevel;
            if (level == null || level.isBossLevel) { EnterWorldSelect(); return; }
            QuitToLevelSelect();
        }

        /// <summary>Settings cog: the Settings / Shop menu hub (franchise Phase 4).</summary>
        public void OpenMenuHub()
        {
            Debug.Log("[GameFlow] Settings / Shop hub not built yet (franchise-flow Phase 4).");
        }

        /// <summary>A paid world's badge: the world shop (franchise Phase 5).</summary>
        public void OpenWorldShop()
        {
            Debug.Log("[GameFlow] World shop not built yet (franchise-flow Phase 5).");
        }

        public void ExitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        /// <summary>Re-reads the save into whichever menu is showing (after a debug change).</summary>
        public void RefreshCurrentScreen()
        {
            if (_gm.State == GameState.WorldSelect) { Worlds.Refresh(keepPosition: true); }
            if (_gm.State == GameState.LevelSelect) { Levels.Show(CurrentWorld); }
        }

        private void LeaveGameplay()
        {
            if (_loader != null) { _loader.UnloadLevel(); }
        }

        // ------------------------------------------------------------ screens follow game state

        private void ApplyState(GameState state)
        {
            // Drop UI focus on every screen change so a stale selected button can't be "submitted" by a key.
            if (EventSystem.current != null) { EventSystem.current.SetSelectedGameObject(null); }

            Landing.Root.SetActive(state == GameState.MainMenu);
            Worlds.Root.SetActive(state == GameState.WorldSelect);
            Levels.Root.SetActive(state == GameState.LevelSelect);
            if (state != GameState.CharacterSelect) { CharacterSelect.Hide(); }

            if (state == GameState.WorldSelect) { Worlds.Refresh(); }
            if (state == GameState.LevelSelect) { Levels.Show(CurrentWorld); }
        }
    }
}
