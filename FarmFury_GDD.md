# FARM FURY — Game Design Document

**v3.0** (2 November 2026) · "Physics Destruction · Nine Worlds · One Farm to Save" · A Farm Fury Universe Game
Physics Puzzle · Casual · Free-to-Play · iOS & Android · Unity 6.5

This Markdown file replaces `FarmFury_GDD_v2.docx` (June 2026, concept phase) as the living design document, in the
same format as the franchise's other titles (`FarmFury_Arcade_GDD.md`, `FarmFury_Stampede_GDD.md`). v2's vision,
characters and level-design rules carry over; everything below is updated to what the game is today. Engineering
detail (file names, tuning values, history) lives in `CLAUDE.md` and `docs/HISTORY.md`; art status in
`docs/ART_REQUIREMENTS.md`. **Built** = in the Unity project; **Planned** = designed, not built. Nothing has been
verified in Play mode yet — see CLAUDE.md "Known open issues".

---

## 0. Where This Sits in the Franchise

Farm Fury is the original physics-destruction title of the Farm Fury Universe (cast, villain and brand shared with
**Farm Fury: Arcade** and **Farm Fury: Stampede**). Since November 2026 everything outside gameplay — menus, World /
Level / Character Select, results, pause, settings, shop, monetisation, saves — follows Stampede/Arcade, so all titles
feel like one family. Gameplay stays Farm Fury's own: launch animals at robot fortresses.

## 1. Game Vision

The Harvest Robots have seized the farm — automated every field, caged every animal, turned the barn into a robot
factory. The animals launch themselves from a cannon across nine worlds to take it back.

**Core fantasy:** aim your animals, wreck everything in your path, take back the farm. Pure cartoon destruction joy.

**Elevator pitch:** a physics destruction puzzle game where eight outrageous farm animals — each with an ability
rooted in their real nature — smash Harvest Robot fortresses across nine worlds. Every animal surprises you; every
level rewards the player who spots the clever shot.

**What makes it original**
- Farm animals, not fictional birds — instantly relatable.
- Every ability comes from real animal nature — cows stomp, chickens lay eggs, pigs roll, ducks skip.
- Harvest Robots as the enemy — cold machinery against warm organic animals; each world has its own robots and boss.
- A story with stakes — saving a home, not just scoring points.
- Built solo with an AI pipeline (Claude Code, Kling AI, Suno).

## 2. Core Loop (Built)

1. Pick a level (World Select → Level Select).
2. **Character Select:** choose a lineup — one slot per shot the level allows (3; 4 on a boss).
3. **Aim and fire the Farm Cannon:** drag back from the cannon — drag *direction* sets the launch angle (20°–80°),
   drag *distance* sets the power. A dotted arc previews the shot (fades on later levels).
4. **Tap mid-flight** to trigger the animal's ability (from World 1 level 4 on).
5. Destroy every robot. Blocks fall under real physics; explosive props (hay bales, dynamite barrels) chain.
6. Run out of animals with robots left → **revive** offer (+1 animal) or Level Failed.
7. **Results:** stars, coin payout, Double Coins (rewarded ad), then on to the next level.

## 3. Characters (Built — all eight scripted with art)

Each animal has one ability, triggered by a tap in flight. Animals unlock with the world that introduces them.

| Animal | Ability | Real-world logic | Strong against | Introduced |
|---|---|---|---|---|
| **Cluck** (chicken) | **Cluster Bomb** — lays a spread of eggs that scatter | Chickens lay eggs | Wood, spread-out targets; punches through hay | World 1 |
| **Bessie** (cow) | **Ground Slam** — radial shockwave on landing | Cows are huge and heavy | Stone, tall unstable towers | World 1 |
| **Percy** (pig) | **Bounce Roll** — curls up, bounces 3×, gaining speed | Pigs are round | Low, spread-out defences | World 2 |
| **Woolly** (sheep) | **Triple Clone** — splits into three at 15° spread | Shearing — three for one | Wide targets, ice | World 2 |
| **Ducky** (duck) | **Skip Shot** — skips flat surfaces 3× | Ducks on water | Low flat structures | World 3 |
| **Horace** (horse) | **Rear Kick** — kicks debris backward on impact | Horses kick | Targets behind the main structure | World 4 |
| **Gerald** (turkey) | **Puff Up** — inflates 3×, wrecking ball | Turkeys puff up | Stone, brute force | World 5 |
| **Billy** (goat) | **Headbutt Through** — penetrates the first obstacle | Goats headbutt everything | Layered / shelled structures | World 6 |

Personalities (from v2): Cluck anxious then delighted; Bessie slow and dignified, devastating on impact; Percy
gleeful chaos; Woolly bewildered by its clones; Ducky smug; Horace noble until the kick; Gerald self-important;
Billy furious and unstoppable.

**Art:** each animal has cannon-game poses — Idle, Loaded, In-flight, Impact, Trigger (ability) — plus a framed
select card and a thumbs-up portrait used in the in-game queue. Horace's ability and impact poses are pending
(Kling AI). **Physics:** heavier animals (Bessie mass 28 vs Cluck 8) hit harder; slow, loopy arcs (gravity scale 0.18).

## 4. Enemies — the Harvest Robots (Built)

Three robot tiers plus a boss in every world; each world has its own look for each tier (Stampede's world robots).

| Tier | Health | Role |
|---|---|---|
| Basic | 35 | Toughest regular robot |
| Harvester | 28 | Standard guard |
| Semi-Harvester | 26 | Most common guard |
| Boss | 90 | One per world, resists direct hits (structural collapse hurts it most) |

**Damage model:** every source is a fixed fraction of a robot's health — direct animal hit (~2 hits to kill),
egg (~6–7), falling when its support is destroyed (~7). Exploding blocks never damage robots directly. Robots
patrol a little on their perch and flash when hit.

| World | Basic | Harvester | Semi-Harvester | Boss |
|---|---|---|---|---|
| 1 Meadow Ruins | Pawn | Harvester | Semi-Harvester | **The Commander** |
| 2 Frozen Tundra | Frost Robot | Ice Harvestor | Glacier Harvestor | The Commander (own boss planned) |
| 3 Watermill Village | Water Robot | Harvester | Scout | **Milestone Mauler** |
| 4 Sky Islands | Drift Robot | Harvester | Drone | **Storm Baron** |
| 5 Sunken City | Shrimp | Crab | Jellyfish | **Deep Dredger** |
| 6 Robot Mothership | Patrol Tank | Harvestor | Drone | **Factory Machine** (Robot Overlord planned) |
| 7 Dustbowl Canyon | Scout | Harvester | Drone | **Drill Rig** |
| 8 Harvest Fairground | Scout | Harvester | Drone | **Ringmaster** |
| 9 Crop Factory | Scout | Harvester | Drone | **Harvester Prime** |

## 5. Worlds (Built: all nine have levels)

Six free story worlds in order, then three paid worlds (bought separately, playable once bought).

| World | Levels | Look | Launcher | Animals introduced |
|---|---|---|---|---|
| 1 Meadow Ruins | 18 (hand-built) | Rolling hills, ruined walls, golden light | Farm Cannon | Cluck, Bessie |
| 2 Frozen Tundra | 22 | Aurora skies, ice fortresses, ice/snow blocks | Ice Cannon (look) | Percy, Woolly |
| 3 Watermill Village | 22 | Riverside village, mills, willows | Farm Cannon | Ducky |
| 4 Sky Islands | 24 | Floating islands, clouds, balloons | Farm Cannon | Horace |
| 5 Sunken City | 22 | Flooded ruins, coral, fish | Farm Cannon | Gerald |
| 6 Robot Mothership | 16 | Deep space, the Mothership | Farm Cannon | Billy |
| 7 Dustbowl Canyon *(paid)* | 12 | Desert canyon, ranch, cacti | Farm Cannon | — |
| 8 Harvest Fairground *(paid)* | 12 | Night fair, big top, ferris wheel | Farm Cannon | — |
| 9 Crop Factory *(paid)* | 12 | Industrial food factory | Farm Cannon | — |

160 levels in all. Each world has its own painted backdrop and its own scenery props (cottages, ferris wheels,
coral, silos…), and the same building blocks everywhere (World 2 shows them as ice and snow).

**Planned — the per-world launcher mechanics from v2** (not built; every world currently uses the Farm Cannon's
drag-to-aim): Ice Cannon ricochet + freeze zones (W2), Water Wheel timing + fire spread on wood (W3), Airdrop
Biplane timing + updraft steering (W4), Torpedo Tube bubble-pop + current lanes and levers (W5), Gravity Sling
gravity wells + zero-G ability variants (W6). Character weight affecting range (W1) is built through animal mass.

## 6. Level Design

**Level anatomy:** cannon and animal queue on the left; open space for the arc; robot fortress on the right
(x ≈ 3–8.4 world units); world backdrop and props behind.

**World 1** is hand-built — every tower placed in the Unity Scene view and exported by the Level Layout Dumper.
Cluck alone for levels 1–9, Bessie joins from level 10; level 18 is the Commander's fortress (boss).

**Worlds 2–9** are generated from World 1's towers (`WorldLevelGenerator`), so the physics is the already-tuned
physics:
- levels walk through World 1's towers 1–17 in order, so every world ramps up like World 1;
- every other level mirrors the tower left-right; towers shift slightly;
- later worlds turn a few planks to stone and promote some robots a tier (difficulty 1–5 across the story worlds;
  paid worlds mid-curve);
- the last level is the boss fortress with that world's boss;
- lineups lead with the world's new animal (paid worlds use Cluck and Bessie).

**Stars**

| Rating | Condition | Purpose |
|---|---|---|
| 1 star | All robots destroyed | Progress — unlocks the next level |
| 2 stars | All robots + 1 animal left | Mastery |
| 3 stars | All robots + 2 or more animals left | Prestige |
| Perfect *(planned)* | 3 stars + every block destroyed | Leaderboard goal |

**Level design rules (from v2, still the goal):** completable with one fewer animal than given; at least one clever
alternative solution; weak points telegraphed; no accidental one-shot clears; stable before the first shot; bosses
are showpieces. **Planned:** a level validator (physics-sim stability/solvability check) — none exists yet, and
the generated levels are untested in play.

## 7. Progression (Built)

- Levels unlock in order; the boss opens once every other level in its world is complete.
- Clearing a world's boss unlocks the next story world and its animals.
- After a boss: Level Complete → **World Unlocked** page (the new world's badge revealed from a silhouette over
  turning golden rays, confetti, "Next stop: …") → New Worlds promo (first clear) → World Select on the new world.
  A **New Character** page announces the animals the new world brings.
- Paid worlds unlock by purchase only.
- Coins: 10 + 5 per star on a first clear; 3 + 5 per new star on replays; Double Coins via a rewarded ad.
- Existing World 1 progress from before the franchise port migrates automatically.

## 8. User Interface (Built — the franchise flow)

```
Landing (poster; Play / Exit / Settings)
  -> World Select (badge carousel: 6 story worlds, then 3 paid)
  -> Level Select (world backdrop; padlock / question / 1-2-3 star boards / boss shield)
  -> Character Select (card carousel; tap the centred card and it drops into the next open lineup slot)
  -> Playing (HUD: thumbs-up animal queue top-left, coins, pause, Locker)
  -> Pause / revive prompt / Results (Level Complete or Failed canvas, payout, Double Coins)
Settings cog -> Menu hub: Settings (Sound, Leaderboards, Character Story, Policies) / Shop / Visit Our Store
```

Results, Pause and New Character canvases carry the Farm Fury logo; each world has its own versions. Esc / Android
back closes the top overlay. A brief slow-motion beat plays between the last robot falling and the results.

## 9. Monetisation (Built; needs store accounts)

Ethical-first, unchanged in spirit from v2: free through every story level, no pay-to-win, no energy system, no
loot boxes, every level completable without spending. The model now matches the franchise (Arcade/Stampede):

| Item | Price |
|---|---|
| Remove Ads (+100 coins) | $4.99 |
| Coin packs: 100 / 500 / 5,000 / 15,000 | $0.99 / $3.99 / $9.99 / $19.99 |
| Paid worlds (Dustbowl, Fairground, Crop Factory) | $3.99 each |
| Hats and trails (shared by every animal) | $1.99 or 2,500 coins |

- **Ads:** rewarded (revive, Double Coins); interstitial every 6 level loads; banner on Pause / Level Failed —
  none with Remove Ads. Inert until FarmFury's LevelPlay keys are entered.
- **Revive:** +1 animal for 5 coins (once) or a rewarded ad (unlimited).
- Purchases and external links go through a parental gate. Coins, purchases and cosmetics are checksum-protected.
- **Dropped from v2:** season pass, starter pack, power-ups. **Still open:** creating the `farmfury_` products in App
  Store Connect / Play Console, ad unit ids, bundle id (currently a Unity default).

## 10. Visual & Audio Design

- **Style:** chunky cartoon renders with bold outlines; warm organic animals against cold machinery.
- **Backdrops:** World 1 is a three-layer parallax stack; World 2 a two-layer aurora stack; Worlds 3–9 one full
  painting each (shared with Stampede).
- **Effects:** wood/stone debris bursts, explosions, egg splats, dust.
- **Audio:** World 1 gameplay and menu music; cannon shot; falling screams for Cluck and Bessie; robot hit/death SFX.
  **Planned:** music per world (Suno), falling loops for the other six animals.
- The win/lose celebration videos and the World 1 → 2 transition video were **removed** (November 2026).

## 11. Technical Specifications (Built)

- **Engine:** Unity 6.5 (6000.5.0f1), URP 2D, Physics2D (gravity −20), New Input System (pointer covers touch),
  uGUI built in code for menus, TextMeshPro.
- **Levels:** `LevelData` ScriptableObjects (blocks, robots, lineup, world, boss flag), generated by
  `LevelDataGenerator` (World 1 + L19) and `WorldLevelGenerator` (Worlds 2–9) — replaces v2's JSON descriptors.
- **Backend:** none yet. Saves are local (PlayerPrefs via SaveManager). Planned: Supabase (economy/backend) and
  Crashlytics, per the July 2026 plan. Analytics via Unity Gaming Services; ads via LevelPlay; purchases via Unity IAP.
- **Platforms:** iOS and Android (landscape, safe-area aware). Home-screen name "Farm Fury".
- **Automation:** `Run-Unity.ps1` (levels / setup / check / builds); a Phaser 3 prototype (`index.html`) remains as
  the frozen physics reference.

## 12. Production Pipeline

Solo developer + AI: **Claude Code** writes and maintains all code, level generation and art import scripts;
**Kling AI** generates character and prop art (image-to-image from existing art for style lock, cheapest model that
holds the character, only for genuinely missing pieces); **Suno** for music. Art lives in
`FarmFury_Technical/FarmFury_Artwork` and is imported by `tools/import_character_art.py` and
`tools/import_stampede_worlds.py`.

## 13. Roadmap

| Phase | Status |
|---|---|
| 1 Core feel (cannon, arc, destruction, audio, camera) | Done |
| 2 UI/UX shell | Done — replaced by the franchise flow (Nov 2026) |
| 3 Character roster (8 animals) | Done (Horace's ability/impact art pending) |
| 4 World 1 (18 levels + boss) | Done |
| 5 Worlds 2–9 | Levels, robots, bosses, backdrops, props built (Nov 2026); per-world launcher mechanics planned |
| 6 Polish & release | Monetisation built; next: Play-mode verification of everything, level validator, store setup, device testing, soft launch |

**Post-launch ideas (from v2, not scheduled):** daily challenge levels, weekly timed challenges, story events,
leaderboards on the "Perfect" goal, seasonal cosmetics.

## 14. Risks and Mitigation

| Risk | Impact | Mitigation |
|---|---|---|
| Generated levels too easy / hard / unsolvable | High | Built from tuned World 1 towers; play-test each world; add the level validator |
| Nothing verified in Play mode | High | Full play-through of every screen and world before store submission |
| Art style drift between AI sessions | Medium | Image-to-image from existing art; reuse franchise art |
| Per-world mechanics never land | Medium | Worlds are fully playable with the cannon; mechanics are additive |
| IP perception (Angry Birds) | High | Cannon not slingshot, farm animals not birds, original robots and story |
| Solo developer burnout | High | Small concrete milestones; AI pipeline carries execution load |

---

*Farm Fury — Game Design Document v3.0. Built by one developer, powered by AI. One farm to save.*
