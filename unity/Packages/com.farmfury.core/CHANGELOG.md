# Changelog – com.farmfury.core

## 0.3.0 – 2026-11-13
- Overlay screens seeded from Stampede (GUIDs kept): `OverlayScreen`, `PauseScreen`, `ParentalGate`, `LegalScreen`,
  `ShopScreen`, `CoinPurchaseScreen`, `ItemPurchaseScreen`, `UseCoinsPrompt`, `LeaderboardPage` (`FarmFury.Shared.UI`).
- New art bases `MenuArtBase` / `ShopArtBase`: the fields the shared screens use. A game derives its own `MenuArt` /
  `ShopArt` from them and **deletes those fields from its own class** (inherited fields serialize by name, so scene
  values survive). The screens take the base types; a game subclass that needs game-only sprites casts
  (`private new MenuArt Art => (MenuArt)base.Art;`).
- `LeaderboardArt` moved in (Stampede's GUID); `WorldName` now takes an index: `WorldName((int)world)`.
- `ResultsScreen.ArtPad` is now `OverlayScreen.WideArtPad` (160).
- New `Store` facade (`FarmFury.Shared.Monetisation`) over `IStoreBackend`: the game registers an adapter over its
  IAPManager in `Store.Backend` before the first scene loads. In the shared screens `StoreProducts.RemoveAds` /
  `CoinPacks` became `Store.RemoveAdsId` / `Store.CoinPackIds`; `Store.CanAffordWithCoins` replaces the SaveManager check.
- **Breaking:** `UseCoinsPrompt` takes the coin cost (`coinCost`, used only by the text fallback).

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
