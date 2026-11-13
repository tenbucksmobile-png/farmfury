# Changelog – com.farmfury.core

## 0.2.0 – 2026-11-13
- **Breaking:** `UIKit.RoundButtonSize` is a settable static property (default `UIKit.DefaultRoundButtonSize` = 160),
  no longer a const. Games must not use it in their own `const` declarations (use a read-only property). A game
  with a different size sets it once before any UI is built (Cannonfire: 112).
- `UIKit.EnsureEventSystem` uses `InputSystemUIInputModule` when the project runs the Input System only, and the
  legacy `StandaloneInputModule` only when the legacy input manager is enabled (Stampede: Both - unchanged).
- New dependency: com.unity.inputsystem 1.19.0 (assembly reference `Unity.InputSystem`).

## 0.1.0 – 2026-11-13
- Seeded from Farm Fury Stampede (script GUIDs kept = Stampede's): MonoSingleton; UI kit and effects (UIKit,
  SafeAreaFitter, FitContent, HoldButton, HoldThenFade, HangingSignDrop, SpinAroundY, ZoomedCover,
  CardCarouselController, RobotRing, ConfettiBurst, CosmeticTrailGhost); AnalyticsManager; AdManager.
- AdManager: Remove Ads / interstitial pacing now come from `AdManager.Policy` (`IAdPolicy`, implemented by each
  game's SaveManager) instead of Stampede's SaveManager. Serialized fields unchanged.
- New: `GameIdentity` ScriptableObject (game id, save-key prefix, store-id prefix, store ids).
- Namespaces: `FarmFury.Shared.UI`, `.Monetisation`, `.Utilities`, `.Config`.
