# Farm Fury Cannonfire - Website demo (Meadow Ruins)

Branch `web-demo`, worktree folder `Desktop\FarmFury_Technical\FarmFury_Cannonfire_Web` (created 2026-11-18 from
`align-franchise-ui` at `7456c87`). The mobile game stays in `Desktop\FarmFury`; nothing here is merged back. Bring a
mobile fix over with `git cherry-pick <sha>`. Modelled on Stampede's (`FarmFury_Stampede_Web`) and Arcade's web demos.

Goal: a free demo at **www.farmfurygames.com/play/cannonfire/** - Meadow Ruins' 18 levels (L01-L18, the Commander).

## What this branch changes

- **No store / ads / analytics:** the packages stay installed (the vendored `com.farmfury.core` references LevelPlay and
  UGS Analytics in its asmdef - never edit it), but `GameManager.Awake` destroys IAPManager / AdManager /
  AnalyticsManager before their Start (where they connect) and WebDemoBuilder removes them from the scene. With no
  AdManager, Double Coins never shows and the revive prompt's Watch Ad is dimmed; the 5-coin revive works.
- **Meadow Ruins only:** `Core/WebDemo.cs`; `SaveManager.IsWorldUnlocked` = Meadow Ruins only, `IsWorldPurchased` =
  false. World Select shows all nine badges bright (`UI/Flow/WorldSelectScreen.cs` reports the others as
  unlocked + paid so they sort after Meadow Ruins); tapping one opens `UI/Flow/FullGameScreen.cs` ("<World> is in
  the full game", GET IT ON THE APP STORE behind the parental gate, Google Play "coming soon"). Beating the Commander
  (L18): Level Complete's play button opens the same panel ("You cleared Meadow Ruins!"), then World Select - no World
  Unlocked page, no New Worlds promo.
- **Animals:** the real game's Meadow Ruins rules (Cluck; Bessie after 1 level). The other six read "FULL GAME / Joins in
  <World>" on Character Select (user's choice 2026-11-18).
- **Hidden:** the landing Exit button, the HUD Locker (the character switch takes its place), Leaderboards in Settings,
  the Results shop promo cards and Level Failed's Shop sign. The settings cog opens Settings directly (no Shop /
  merch / cross-promo hub). Coins stay (revive costs coins).
- **Trim (`Editor/WebDemoBuilder.cs`, menu FarmFury > Web Demo):** "Trim To Meadow Ruins" keeps only Meadow Ruins'
  levels in GameManager, removes the store/ads/analytics components, and clears every scene / prefab reference to an
  asset whose path is another world's (`OtherWorldArt` regex) or a cosmetic, plus the other worlds' WorldData menu art
  (badges kept). "Apply Web Size Settings" = Stampede's WebGL overrides (actors 256, sprites 512, backdrops 2048;
  crunched DXT5 or ASTC; AAC audio), splash off, stripping High, IL2CPP optimise for size. "Build Website" does both
  and builds `unity/Builds/Website` with the `CannonfireEmbed` template (full-window page for an iframe, loading bar,
  "turn your phone sideways", postMessage `{source:"farmfury-cannonfire", ...}`), gzip + decompression fallback, no
  hashed names (they loop 6000.5.6f1's WebGL build) - index.html stamps `?v=<UTC build time>` on its Build/ links.

## Working rules

- Never run Wire Scene References / Generate All Level Data here without re-running Trim (Build Website trims anyway).
- Batch build: `Unity.exe -batchmode -quit -buildTarget WebGL -projectPath <worktree>\unity -executeMethod WebDemoBuilder.BuildWebsite`.

## Upload (cPanel, same host as Arcade / Stampede)

Zip the *contents* of `unity/Builds/Website` (index.html + Build/). Create `public_html/play/cannonfire/` (lowercase -
the server is case-sensitive), upload the zip into that folder, Extract there, delete the zip. **Never extract into
`public_html/play/`** (Arcade's forward page) or another game's folder. Check
`https://www.farmfurygames.com/play/cannonfire/` and one file under `Build/` return 200; hard-refresh (the host caches a day).

## Progress

- 2026-11-18: branch, Meadow-only flow, Full Game panel, trim/build tools, embed template written.
