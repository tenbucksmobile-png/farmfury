# FarmFury — Art Requirements (Kling AI)

Created 2026-11-02 alongside the franchise-flow port (FarmFury's menus, shop and monetisation now follow
FarmFury_Stampede/Arcade). This is the checklist of art still needed to **finish all nine worlds**, split into
what the port can borrow from Stampede for now and what has to be made for FarmFury.

**How the stand-ins work:** every menu art slot falls back to Stampede's shared art (same world names, same
button/sign/shop set). Anything listed below as **NEEDED** is either missing or carries Stampede branding.
Drop a finished file at the listed path, re-run **FarmFury → Wire Scene References**, and it replaces the
stand-in. No code change needed.

Legend: ✅ have it · ♻️ borrow from Stampede (fine to ship) · 🟡 stand-in only (Stampede-branded, replace) · ❌ NEEDED (make with Kling)

> **Status update 2026-11-02 (later):** much of the list below is superseded — read this box first.
> - **Worlds 3-9 gameplay art is in** (user decision: same World 1 blocks in every world, Stampede's backdrops,
>   robots and props per world): `tools/import_stampede_worlds.py`. Section 6's block/parallax/robot rows no
>   longer apply; World 2 keeps its 5-shape ice reskin. Launchers stay the Farm Cannon (World 2: Ice Cannon).
> - **Animal poses are in for all eight** (`tools/import_character_art.py`, from
>   `FarmFury_Technical/FarmFury_Artwork/Rough_Characters`): Ducky/Billy/Gerald full sets, Percy Idle+Impact,
>   Bessie/Woolly Idle. Horace: Idle/Loaded from his front render, InFlight = Stampede running frame, Trigger
>   (rear kick) and Impact generated with Kling (kling-image-o1, 1k, 1 credit each).
> - **FF_Logo** = `FarmFury_Artwork/Rough_Icons/FarmFury_Logo.png` -> `Sprites/UI/Franchise/Menu/FF_Logo.png`
>   (Leaderboard corner) and stamped onto all 28 Results/Pause/New Character canvases where Stampede's logo was.
> - **Videos** (section 8): the win/lose celebration videos were removed from the game — not needed.
> - In-game HUD queue uses Stampede's ThumbsUp portraits (all 8).
> - Still open: app icon, store screenshots, per-world music, World 2 / Mothership own bosses (W2 uses the
>   Commander, W6 the Factory Machine), robot damaged frames (white-flash fallback in use).

---

## Generation conventions (match the existing art)

- **Characters / props / launchers / robots:** 1024×1024, white background, subject fills ~75–80%
  (`tools/remove_backgrounds.py` strips the white). Side view, same chunky cartoon style as Cluck/Bessie.
- **Full-screen canvases:** 1280×720 content (setup widens results/pause art to ~2.2:1 by mirroring the
  edges, like Stampede's `*_Canvas_Wide.png`). Keep logos/lettering inside the central 16:9.
- **Parallax layers:** 2720×1536-ish, horizontally tileable, each layer's terrain in its bottom band
  (FarmFury's `ParallaxBandClip` reveals only the bottom part of each layer — see CLAUDE.md).
- **Kling tips that worked on Stampede** (`FarmFury_Stampede/CropFactory_KlingPrompts.md`): use
  image-to-image from an existing sprite for style lock; put view words first ("flat side view, 2D game
  sprite"); one object per image; negative prompt "perspective, 3/4 view, shadow, text, watermark".
- Lettering (titles like "Level Complete!") was hand-made in Photopea in Stampede, not by Kling — keep
  doing that; Kling text is unreliable.

---

## 1. Brand / shell (needed before any store release)

| Item | Path | Status | Notes |
|---|---|---|---|
| FarmFury logo, transparent | `Sprites/UI/Franchise/FF_Logo.png` (~1000×600) | ❌ | Cut from `LandingPage.png`'s "FARM FURY" lettering, or regenerate. Used on every canvas below + Leaderboard corner. **Once this exists, item 2's canvases can be made by script** (paint out Stampede's corner logo, paste this) — no Kling needed. |
| Landing poster | `Sprites/UI/LandingPage.png` | ✅ | Existing FarmFury poster (2720×1418). Optional: a ~2.2:1 widened version so wide phones don't crop the animals. |
| App icon 1024×1024 | `Sprites/AppIcon.png` | ❌ | Franchise style = character + Farm Fury logo (see Stampede/Arcade icons). Home-screen name will be "Farm Fury". |
| Store screenshots / feature graphic | (store consoles) | ❌ | After Play-mode verification. |

## 2. Shared menu canvases (one set, all worlds fall back to these)

| Item | Stampede stand-in | Status |
|---|---|---|
| Level Complete canvas (title + 3 gold stars painted in) | `LevelComplete_Canvas.png` | 🟡 Stampede logo top-left |
| Level Failed canvas | `LevelFailed_Canvas.png` | 🟡 Stampede logo |
| Pause canvas | `Pause_Canvas.png` | 🟡 Stampede logo |
| New Character page | `NewCharacter_Plain.png` | 🟡 Stampede logo |
| Settings / Shop page backdrop | `Settings_Canvas.png` | ♻️ unbranded (Cluck through a sign) |
| Character select backdrop ("Character" lettering) | `CharacterCanvas.png` | ♻️ |
| World Select backdrop | `Canvas.png` | ♻️ |
| Greyed star overlay | `LevelComplete_StarEmpty.png` | ♻️ |
| Score icon (Stampede uses a corn kernel) | `CornKernel.png` | ❌ FarmFury score icon — a robot-bolt/cog or a destroyed-robot badge, 256×256 |

## 3. Shared menu pieces (all ♻️ — no work needed)

Round buttons (play/home/back/quit/settings/pause/skip), level tiles (padlock / question / 1-2-3 star
boards / boss shield), World Unlocked & New Character signs, Settings/Shop/Legal/Leaderboard signs,
Settings & Shop icons, coin-pack art (100/500/5000/15000), price plaques, Yes/No, Watch Ad, Double Coins,
Use Coins, Locker banner/icon, Promo boards (Cluck — fits FarmFury), parental gate, purchase-complete,
owned/equipped badges, hats & trails shop art, world name lettering for the Leaderboard.

## 4. Character select cards (♻️ + gaps)

Stampede's framed cards (`Cluck_Chicken.png` … `Wooly_Sheep.png`, name baked in) cover all 8 animals and
are used for FarmFury's pre-level select. ✅/♻️ for all eight.

## 5. Animals — gameplay poses (FarmFury-specific: cannon game, not platformer)

Each animal needs, in `Sprites/Characters/<Short>/` (folder name must be the short name, e.g. `Ducky` —
see CLAUDE.md "Auto-Compile Pipeline"): **Idle, Loaded** (sitting in the cannon), **InFlight**,
**Impact** (squashed reaction), **Trigger** (ability pose), plus any ability projectile.

| Animal | World | Have | NEEDED |
|---|---|---|---|
| Cluck | 1 | all poses, Egg | — |
| Bessie | 1 | all except Idle | Idle |
| Percy | 2 | Loaded, InFlight, Trigger | Idle, Impact |
| Woolly | 2 | Loaded, InFlight, Impact, Ability | Idle |
| Ducky | 3 | nothing | Idle, Loaded, InFlight, Impact, Trigger (skip-shot pose), water-skip splash FX |
| Horace | 4 | nothing | Idle, Loaded, InFlight, Impact, Trigger (rear kick), debris FX |
| Gerald | 5 | nothing | Idle, Loaded, InFlight, Impact, Trigger (puffed-up 3× ball) |
| Billy | 6 | nothing | Idle, Loaded, InFlight, Impact, Trigger (headbutt) |

Tip: image-to-image from Stampede's side-view frames for each animal (`FarmFury_Stampede/.../Characters/`)
keeps them on-model.

## 6. Per-world gameplay art

FarmFury levels need, per world: a **launcher**, a **3-layer parallax**, **destructible blocks** in the
same shapes as World 1, **3 robot tiers + a boss**, and **scenery props**. Stampede already has robots,
props and far/mid parallax for every world (same world themes) — borrow those; make the rest.

Block shapes World 1 uses (each world needs its own material version): Normal, Vertical, Horizontal,
Short, Skew, Diagonal, Square, VerticalShort, Flat, Cart, Barrel, RuinedWall, Tower — plus an explosive
prop (World 1: haybale + dynamite barrel) and a damaged/debris burst.

| World | Launcher | Parallax (far/mid/fore) | Blocks | Robots (Basic/Harvester/Semi + boss) | Props |
|---|---|---|---|---|---|
| 1 Meadow Ruins | ✅ Cannon | ✅✅✅ | ✅ all | ✅ + Commander | ✅ |
| 2 Frozen Tundra | ✅ IceCannon | ✅✅❌ foreground | 🟡 5 of 13 (ice/snow: Normal, Vertical, Horizontal, Diagonal, Short) — ❌ the other 8 + an ice explosive | ✅ 3 tiers ♻️; ❌ boss (♻️ Stampede GlacierHarvestor is used as a tier here — need a distinct Tundra boss) | ✅ |
| 3 Watermill Village | ❌ Water Wheel (♻️ Stampede `WaterWheel.png` may work as a prop-launcher) | ♻️ far/mid/near | ❌ all (timber + thatch, "fire spread on wood") | ♻️ WaterRobot; ♻️ boss Milestone Mauler; ❌ 2 more tiers | ♻️ |
| 4 Sky Islands | ❌ Airdrop Biplane (♻️ `Plane.png` base) | ♻️ far; ❌ mid/fore | ❌ all (cloud stone / sky timber) | ♻️ boss Storm Baron; ❌ 3 tiers | ♻️ |
| 5 Sunken City | ❌ Torpedo Tube | ♻️ far; ❌ mid/fore | ❌ all (coral / sunken stone) | ♻️ Crab, Shrimp, JellyFish; ♻️ boss Deep Dredger | ♻️ |
| 6 Robot Mothership | ❌ Gravity Sling (♻️ `GravitySling.png` prop) | ♻️ far; ❌ mid/fore | ❌ all (steel plate / crates) | ♻️ Patrol, Harvester, Drone; ❌ Robot Overlord boss (missing in Stampede too) | ♻️ |
| 7 Dustbowl Canyon (paid) | ❌ (design TBD) | ♻️ far; ❌ mid/fore | ❌ all | ♻️ Scout, Harvester, Drone; ♻️ boss Drill Rig | ♻️ |
| 8 Harvest Fairground (paid) | ❌ (design TBD) | ♻️ far; ❌ mid/fore | ❌ all | ♻️ Scout, Harvester, Drone; ♻️ boss Ringmaster | ♻️ |
| 9 Crop Factory (paid) | ❌ (design TBD) | ♻️ far/mid; ❌ fore | ❌ all | ♻️ Scout, Harvester, Drone; ♻️ boss Harvester Prime | ♻️ |

Robots for FarmFury need a **damaged** frame too (World 1 has `*_Damage.png`) — Stampede only has
left/right/defeat. Either make damaged frames, or accept the white-flash fallback `RobotEnemy` already has.

## 7. Per-world menu art

| World | World Select badge | Level Select backdrop | Results / New Character / Character canvases |
|---|---|---|---|
| All 9 | ♻️ `Badge_<World>.png` | ♻️ `<World>.png` (name baked in) | 🟡 Stampede's `XX_*_Canvas.png` per world carry the Stampede logo → re-brand with FF_Logo by script once item 1 exists |

## 8. Video & audio

| Item | Have | NEEDED |
|---|---|---|
| Level Complete celebration video (per animal, green screen #00B140 or baked bg) | Cluck | Bessie, Percy, Woolly, Ducky, Horace, Gerald, Billy (+ laugh audio each) |
| Level Failed robot taunt video (per world) | Meadow robot | 8 worlds (+ taunt audio) |
| World-transition video (boss clear → next world) | W1→W2 draft (`TransitionVideo_Draft.mp4`) | final W1→W2, W2→W3 … W5→W6 |
| Music per world (Suno) | World 1 gameplay + menu | Worlds 2–9 gameplay loops |
| Falling/scream loop per animal | Cluck, Bessie | the other 6 |

## 9. Cosmetics (Phase 5, later)

Franchise hats/trails art is ♻️ from Stampede. FarmFury needs per-animal **hat anchor points** on the
Loaded and InFlight poses (measured, not generated) — no Kling work unless new hats are designed.

---

### Suggested order

1. FF_Logo (unblocks re-branding every canvas by script) + score icon + app icon.
2. Animal poses for Percy/Woolly gaps, then Ducky (World 3's debut) — every later world needs its animal.
3. World 2 completion: 8 missing ice block shapes, ice explosive, foreground parallax, Tundra boss.
4. World 3 launcher + blocks + 2 robot tiers, then worlds 4–6 in order, then the three paid worlds.
5. Videos and music per world as each world's levels are built.
