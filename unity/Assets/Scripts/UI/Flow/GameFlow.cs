using System.Collections.Generic;
using System.Linq;
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
        [SerializeField] private ShopArt _shopArt = new();

        public static GameFlow Instance { get; private set; }

        public LandingScreen Landing { get; private set; }
        public WorldSelectScreen Worlds { get; private set; }
        public LevelSelectScreen Levels { get; private set; }
        public CharacterSelectScreen CharacterSelect { get; private set; }
        public PauseScreen Pause { get; private set; }
        public ResultsScreen Results { get; private set; }
        public NewCharacterScreen NewCharacter { get; private set; }
        public MenuArt Art => _menuArt;
        public ShopArt Shop => _shopArt;
        public WorldType CurrentWorld { get; private set; } = WorldType.MeadowRuins;
        public Transform CanvasTransform { get; private set; }

        private GameManager _gm;
        private LevelLoader _loader;
        private readonly List<OverlayScreen> _overlays = new();

        private void Awake()
        {
            Instance = this;
            _menuArt ??= new MenuArt();
            _shopArt ??= new ShopArt();

            var canvas = UIKit.CreateCanvas("MenuCanvas", 400, transform);
            UIKit.EnsureEventSystem(transform);
            CanvasTransform = canvas.transform;

            Landing = new LandingScreen(canvas.transform, EnterWorldSelect, ExitGame, OpenMenuHub, _menuArt);
            Worlds = new WorldSelectScreen(canvas.transform, EnterLevelSelect, OpenWorldShop, EnterLanding, _menuArt);
            Levels = new LevelSelectScreen(canvas.transform, PickLevel, EnterWorldSelect, _menuArt);
            CharacterSelect = new CharacterSelectScreen(canvas.transform, LaunchLevel, CloseCharacterSelect, _menuArt);
            Results = new ResultsScreen(canvas.transform, PlayNextLevel, RestartLevel, QuitToLevelSelect, EnterLanding, OpenMenuHub, _menuArt);
            Pause = new PauseScreen(canvas.transform, ResumeFromPause, OpenSettings, RestartLevel, QuitToLevelSelect, EnterLanding, _menuArt);
        }

        /// <summary>
        /// Overlays (Stampede's OverlayScreen convention): open on top of whatever screen is showing, Back returns to
        /// it, any game-state change closes them all. Built in Start, after DataManager is up.
        /// </summary>
        private void BuildOverlays()
        {
            var c = CanvasTransform;
            NewCharacter = Add(new NewCharacterScreen(c, _menuArt, _shopArt));
        }

        private T Add<T>(T overlay) where T : OverlayScreen
        {
            _overlays.Add(overlay);
            return overlay;
        }

        private OverlayScreen TopOverlay() =>
            _overlays.Where(o => o.IsOpen).OrderByDescending(o => o.Root.transform.GetSiblingIndex()).FirstOrDefault();

        private void CloseOverlays()
        {
            foreach (var overlay in _overlays) { overlay.Hide(); }
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
            BuildOverlays();
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
            var overlay = TopOverlay();
            if (overlay != null)
            {
                overlay.Hide();
                return;
            }

            switch (_gm.State)
            {
                case GameState.CharacterSelect: CloseCharacterSelect(); break;
                case GameState.LevelSelect: EnterWorldSelect(); break;
                case GameState.WorldSelect: EnterLanding(); break;
                case GameState.Playing: OpenPause(); break;
                case GameState.Paused: ResumeFromPause(); break;
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
        /// A world-transition video (L18 -> Frozen Tundra) plays first when one is wired for the level.
        public void PlayNextLevel()
        {
            Results.Hide();
            if (WorldTransitionManager.Instance != null &&
                WorldTransitionManager.Instance.TryPlayTransition(_gm.CurrentLevelIndex, ContinueAfterLevel))
            {
                return;
            }
            ContinueAfterLevel();
        }

        private void ContinueAfterLevel()
        {
            var level = _gm.CurrentLevel;
            if (level == null || level.isBossLevel) { EnterWorldSelect(); return; }
            QuitToLevelSelect();
        }

        // ------------------------------------------------------------ in-level screens

        public void OpenPause() => _gm.PauseGame();

        public void ResumeFromPause() => _gm.ResumeGame();

        /// <summary>LevelCompleteManager, once the celebration video ends: the results, then any New Character page.</summary>
        public void ShowLevelComplete()
        {
            if (_gm.State != GameState.LevelComplete) { return; }
            Results.ShowComplete(_gm.RunState, CurrentLevelWorld());
            if (_gm.RunState.newlyUnlockedAnimals.Count > 0)
            {
                NewCharacter.ShowUnlocks(_gm.RunState.newlyUnlockedAnimals);
            }
        }

        /// <summary>LevelFailedManager, once the taunt video ends.</summary>
        public void ShowLevelFailed()
        {
            if (_gm.State != GameState.LevelFailed) { return; }
            Results.ShowFailed(CurrentLevelWorld());
        }

        private WorldData CurrentLevelWorld()
        {
            var level = _gm.CurrentLevel;
            return level != null && DataManager.Instance != null ? DataManager.Instance.GetWorldData(level.WorldType) : null;
        }

        /// <summary>Pause's cog: Settings over the pause screen (franchise Phase 4).</summary>
        public void OpenSettings()
        {
            Debug.Log("[GameFlow] Settings not built yet (franchise-flow Phase 4).");
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
            CloseOverlays();

            Landing.Root.SetActive(state == GameState.MainMenu);
            Worlds.Root.SetActive(state == GameState.WorldSelect);
            Levels.Root.SetActive(state == GameState.LevelSelect);
            if (state != GameState.CharacterSelect) { CharacterSelect.Hide(); }
            if (state == GameState.Paused) { Pause.Show(); } else { Pause.Hide(); }
            // The results wait for LevelComplete/FailedManager's video to finish (they call ShowLevelComplete /
            // ShowLevelFailed); any other state hides them.
            if (state != GameState.LevelComplete && state != GameState.LevelFailed) { Results.Hide(); }

            if (state == GameState.WorldSelect) { Worlds.Refresh(); }
            if (state == GameState.LevelSelect) { Levels.Show(CurrentWorld); }
        }
    }
}
