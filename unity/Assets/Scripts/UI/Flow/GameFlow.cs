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
        [SerializeField] private LeaderboardArt _leaderboardArt = new();

        public static GameFlow Instance { get; private set; }

        public LandingScreen Landing { get; private set; }
        public WorldSelectScreen Worlds { get; private set; }
        public LevelSelectScreen Levels { get; private set; }
        public CharacterSelectScreen CharacterSelect { get; private set; }
        public PauseScreen Pause { get; private set; }
        public ResultsScreen Results { get; private set; }
        public NewCharacterScreen NewCharacter { get; private set; }
        public WorldClearedScreen WorldCleared { get; private set; }
        public ParentalGate Gate { get; private set; }
        public MenuHubScreen MenuHub { get; private set; }
        public SettingsScreen Settings { get; private set; }
        public RevivePromptScreen Revive { get; private set; }
        public ShopScreen ShopHub { get; private set; }
        public LockerScreen Locker { get; private set; }
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
            _leaderboardArt ??= new LeaderboardArt();

            var canvas = UIKit.CreateCanvas("MenuCanvas", 400, transform);
            UIKit.EnsureEventSystem(transform);
            CanvasTransform = canvas.transform;

            Landing = new LandingScreen(canvas.transform, EnterWorldSelect, ExitGame, OpenMenuHub, _menuArt);
            Worlds = new WorldSelectScreen(canvas.transform, EnterLevelSelect, OpenWorldShop, EnterLanding, _menuArt);
            Levels = new LevelSelectScreen(canvas.transform, PickLevel, EnterWorldSelect, _menuArt);
            CharacterSelect = new CharacterSelectScreen(canvas.transform, LaunchLevel, CloseCharacterSelect, _menuArt);
            Results = new ResultsScreen(canvas.transform, PlayNextLevel, RestartLevel, QuitToLevelSelect, EnterLanding, OpenMenuHub,
                OpenPromo, OpenShopFromResults, _menuArt, _shopArt);
            Pause = new PauseScreen(canvas.transform, ResumeFromPause, OpenSettings, RestartLevel, QuitToLevelSelect, EnterLanding, _menuArt);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            gameObject.AddComponent<DebugPanel>();   // F1
#endif
        }

        /// <summary>
        /// Overlays (Stampede's OverlayScreen convention): open on top of whatever screen is showing, Back returns to
        /// it, any game-state change closes them all. Built in Start, after DataManager is up.
        /// </summary>
        private void BuildOverlays()
        {
            var c = CanvasTransform;
            // Every Settings / Shop page shares the Leaderboard's backdrop.
            if (_shopArt.pageBackground == null) { _shopArt.pageBackground = _leaderboardArt.background; }
            Gate = Add(new ParentalGate(c, _menuArt, _shopArt));
            NewCharacter = Add(new NewCharacterScreen(c, _menuArt, _shopArt));
            WorldCleared = Add(new WorldClearedScreen(c, _menuArt, _shopArt));
            var legal = Add(new LegalScreen(c, _menuArt, _shopArt, Gate));
            var story = Add(new CharacterStoryScreen(c, _menuArt, _shopArt));
            var worldDetail = Add(new WorldDetailScreen(c, _menuArt, _shopArt, _leaderboardArt));
            var leaderboards = Add(new LeaderboardsScreen(c, _menuArt, _shopArt, _leaderboardArt, worldDetail.Show, OpenWorldShop));
            Settings = Add(new SettingsScreen(c, _menuArt, _shopArt, leaderboards.Show, story.Show, legal.Show));

            // Shop family (franchise Phase 5, Arcade's catalogue and layout).
            var useCoins = Add(new UseCoinsPrompt(c, _menuArt, _shopArt));
            Revive = Add(new RevivePromptScreen(c, _menuArt, _shopArt));
            _worldShop = Add(new ItemPurchaseScreen(c, "WorldPurchase", _menuArt, _shopArt, Gate, useCoins, null, _menuArt.worldUnlockedSign,
                "NEW WORLDS", WorldItems(), new Vector2(380f, 380f), 40f, _shopArt.worldPrice));
            _worldShop.Closed += () => { if (Worlds.Root.activeSelf) { Worlds.Refresh(keepPosition: true); } };   // a bought world opens at once
            _worldPromo = Add(new ItemPurchaseScreen(c, "WorldPromo", _menuArt, _shopArt, Gate, useCoins, _shopArt.promoNewWorlds, null,
                "NEW WORLDS", WorldItems(), new Vector2(380f, 380f), 40f, _shopArt.worldPrice));
            _worldPromo.Closed += () =>
            {
                var then = _afterWorldPromo;
                _afterWorldPromo = null;
                then?.Invoke();
            };
            var itemCell = new Vector2(OverlayScreen.ItemWidth, OverlayScreen.ItemHeight);
            int trailsFirst = StoreProducts.Cannons.Length, impactsFirst = trailsFirst + StoreProducts.Trails.Length;
            _cannonsPage = Add(new ItemPurchaseScreen(c, "CosmeticsCannons", _menuArt, _shopArt, Gate, useCoins, _shopArt.cannonsBanner, null, "CANNONS",
                Items(StoreProducts.Cannons, _shopArt.cannonItems, 0), itemCell, 77f));
            _trailsPage = Add(new ItemPurchaseScreen(c, "CosmeticsTrails", _menuArt, _shopArt, Gate, useCoins, _shopArt.trailsBanner, null, "TRAILS",
                Items(StoreProducts.Trails, _shopArt.trailItems, trailsFirst), itemCell, 50f));
            _impactsPage = Add(new ItemPurchaseScreen(c, "CosmeticsImpacts", _menuArt, _shopArt, Gate, useCoins, _shopArt.impactsBanner, null, "IMPACTS",
                Items(StoreProducts.Impacts, _shopArt.impactItems, impactsFirst), itemCell, 77f));
            var chooser = Add(new CosmeticsChooserScreen(c, _menuArt, _shopArt, _cannonsPage.Show, _trailsPage.Show, _impactsPage.Show));
            Locker = Add(new LockerScreen(c, _menuArt, _shopArt, _cannonsPage.Show, _trailsPage.Show, _impactsPage.Show));
            var coins = Add(new CoinPurchaseScreen(c, _menuArt, _shopArt, Gate));
            ShopHub = Add(new ShopScreen(c, _menuArt, _shopArt, Gate, coins.Show, _worldShop.Show, chooser.Show));

            MenuHub = Add(new MenuHubScreen(c, _menuArt, _shopArt, Gate, Settings.Show, OpenShop));
        }

        private ItemPurchaseScreen _worldShop, _worldPromo, _cannonsPage, _trailsPage, _impactsPage;
        private System.Action _afterWorldPromo;

        // A price plaque per product; the label (shown only without art) is the cosmetic's name.
        private static ItemPurchaseScreen.Item[] Items(string[] productIds, Sprite[] sprites, int firstNameIndex) =>
            productIds.Select((id, i) => new ItemPurchaseScreen.Item(id, ShopArt.At(sprites, i),
                StoreProducts.Cosmetics[firstNameIndex + i].name)).ToArray();

        /// <summary>The Worlds page: the paid worlds (WorldData.purchaseRequired), each shown as its badge.</summary>
        private static ItemPurchaseScreen.Item[] WorldItems() =>
            IAPManager.PurchasableWorlds()
                .Select(w => DataManager.Instance.GetWorldData(w))
                .Where(w => w != null)
                .Select(w => new ItemPurchaseScreen.Item(StoreProducts.World(w.worldType), w.selectBadge, w.displayName))
                .ToArray();

        /// <summary>Level Complete's shop card: that item's page over the results; Back returns to them.</summary>
        private void OpenPromo(ShopPromo promo)
        {
            AnalyticsManager.Instance?.LogPromoTapped(ShopPromos.Id(promo));
            switch (promo)
            {
                case ShopPromo.Cannons: _cannonsPage?.Show(); break;
                case ShopPromo.Trails: _trailsPage?.Show(); break;
                case ShopPromo.Impacts: _impactsPage?.Show(); break;
                case ShopPromo.RemoveAds: ShopHub?.Show(); break;
            }
        }

        /// <summary>Level Failed's Shop sign.</summary>
        private void OpenShopFromResults()
        {
            AnalyticsManager.Instance?.LogPromoTapped("level_failed_shop");
            ShopHub?.Show();
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
            _gm.ReviveOffered += Revive.Show;
            EnterLanding();
            ApplyState(_gm.State);   // the boot state is already MainMenu, so EnterLanding fires no change event
        }

        private void OnDestroy()
        {
            if (Instance == this) { Instance = null; }
            if (_gm != null)
            {
                _gm.OnStateChanged -= ApplyState;
                if (Revive != null) { _gm.ReviveOffered -= Revive.Show; }
            }
        }

        private void Update()
        {
            if (_gm == null) { return; }
            if (Revive != null && Revive.IsOpen) { Revive.Tick(); }
            if (Results.Root.activeSelf) { Results.Tick(); }
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

            if (CharacterSelect.IsSwapping) { CharacterSelect.CancelSwap(); return; }

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
        /// When the boss clear opened the next world, Stampede's World Unlocked page (the new world's badge revealed
        /// from a silhouette) comes first; it replaced the L18 -> Frozen Tundra transition video (2026-11-02).
        public void PlayNextLevel()
        {
            Results.Hide();
            var level = _gm.CurrentLevel;
            if (level != null && level.isBossLevel && WorldCleared != null
                && WorldCleared.ShowFor(level.WorldType, _gm.RunState.worldUnlocked, ContinueAfterLevel))
            {
                return;
            }
            ContinueAfterLevel();
        }

        // After a boss: the first time it falls, the paid worlds not yet bought (New Worlds promo), then World Select.
        private void ContinueAfterLevel()
        {
            var level = _gm.CurrentLevel;
            if (level == null) { EnterWorldSelect(); return; }
            if (!level.isBossLevel) { QuitToLevelSelect(); return; }

            bool anyUnbought = IAPManager.PurchasableWorlds().Any(w => !SaveManager.Instance.IsWorldPurchased(w));
            if (_gm.RunState.firstClear && anyUnbought && _worldPromo != null)
            {
                AnalyticsManager.Instance?.LogPromoShown("new_worlds");
                _afterWorldPromo = EnterWorldSelect;
                _worldPromo.Show();
                return;
            }
            EnterWorldSelect();
        }

        // ------------------------------------------------------------ in-level screens

        public void OpenPause() => _gm.PauseGame();

        /// <summary>The HUD's Locker button: equip owned hats / trails mid-level (the level freezes while open).</summary>
        public void OpenLocker()
        {
            if (_gm.State == GameState.Playing && !_gm.ReviveDecisionPending) { Locker?.Open(); }
        }

        public void ResumeFromPause() => _gm.ResumeGame();

        // ------------------------------------------------------------ in-level character switch (2026-11-09)

        private CatapultLauncher _launcher;

        private bool FindGameplay()
        {
            if (_launcher == null) { _launcher = FindAnyObjectByType<CatapultLauncher>(); }
            if (_loader == null) { _loader = FindAnyObjectByType<LevelLoader>(); }
            return _launcher != null && _loader != null;
        }

        /// <summary>The HUD's switch button is usable: between shots, animals left, no other screen open.</summary>
        public bool CanSwapCharacters =>
            _gm != null && _gm.State == GameState.Playing && !_gm.ReviveDecisionPending && Time.timeScale > 0f &&
            !CharacterSelect.IsOpen && FindGameplay() && _launcher.CanSwapAnimals;

        /// <summary>
        /// The HUD's switch button (user request: "allow a user to switch characters whilst in gameplay... they cannot
        /// get more characters than what they have left"): freezes the level and opens Character Select with one slot
        /// per animal still to fire. Play swaps them in and resumes the same attempt; Back resumes unchanged.
        /// </summary>
        public void OpenCharacterSwap()
        {
            if (!CanSwapCharacters || _gm.CurrentLevel == null) { return; }
            Time.timeScale = 0f;
            CharacterSelect.OpenSwap(_gm.CurrentLevel, _loader.BirdQueueSnapshot, ApplySwap, ResumeAfterSwap);
        }

        private void ApplySwap(AnimalType[] picks)
        {
            if (FindGameplay())
            {
                _loader.ReplaceRemainingBirds(picks);
                _launcher.ReloadAfterSwap();
            }
            ResumeAfterSwap();
        }

        private void ResumeAfterSwap()
        {
            if (_gm.State == GameState.Playing) { Time.timeScale = 1f; }
        }

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

        /// <summary>Pause's cog: straight to Settings, over the pause screen (bypassing the hub).</summary>
        public void OpenSettings() => Settings.Show();

        /// <summary>Settings cog (landing, Level Complete): the menu hub over that screen; Back returns to it.</summary>
        public void OpenMenuHub() => MenuHub.Show();

        /// <summary>The menu hub's Shop sign.</summary>
        public void OpenShop() => ShopHub.Show();

        /// <summary>A paid world's badge (World Select / Leaderboards): the world shop ($3.99 each).</summary>
        public void OpenWorldShop() => _worldShop?.Show();

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
            // The results wait for LevelComplete/FailedManager's slow-mo beat to finish (they call ShowLevelComplete /
            // ShowLevelFailed); any other state hides them.
            if (state != GameState.LevelComplete && state != GameState.LevelFailed) { Results.Hide(); }

            // Arcade's banner placements: Pause and Level Failed only, and never once Remove Ads is owned.
            var ads = AdManager.Instance;
            if (ads != null)
            {
                bool banner = (state == GameState.Paused || state == GameState.LevelFailed)
                    && SaveManager.Instance != null && !SaveManager.Instance.AdsRemoved;
                if (banner) { ads.ShowBanner(); } else { ads.HideBanner(); }
            }

            if (state == GameState.WorldSelect) { Worlds.Refresh(); }
            if (state == GameState.LevelSelect) { Levels.Show(CurrentWorld); }
        }
    }
}
