# FARM FURY CANNONFIRE — Game Design Document

*Renamed from "Farm Fury" on 2026-11-10 to run with Farm Fury Arcade and Farm Fury Stampede. Under the phone icon: "Cannonfire".*

**v3.4** (13 November 2026) · "Physics Destruction · Nine Worlds · One Farm to Save" · A Farm Fury Universe Game
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
- Every ability comes from real animal nature — cows stomp, chickens lay eggs, pigs roll, ducks whip up the pond.
- Harvest Robots as the enemy — cold machinery against warm organic animals; each world has its own robots and boss.
- A story with stakes — saving a home, not just scoring points.
- Built solo with an AI pipeline (Claude Code, Kling AI, Suno).

## 2. Core Loop (Built)

1. Pick a level (World Select → Level Select).
2. **Character Select:** choose a lineup of three animals (never more — `LevelData.MaxLineup`; a revive still adds one).
3. **Aim and fire the Farm Cannon:** drag back from the cannon — drag *direction* sets the launch angle (20°–80°),
   drag *distance* sets the power. A dotted arc previews the shot (fades on later levels).
4. **Tap mid-flight** to trigger the animal's ability (from World 1 level 4 on).
5. Destroy every robot. Structures are built the way robots would build them — wonky, top-heavy, on single posts,
   leaning, upside down — and stand still until something hits them; **one soft strike** is enough — nothing breaks
   or explodes, the struck piece is shoved and the stack leans, slowly at first, then faster as gravity takes over
   (live physics, slightly reduced gravity), knocking everything below down with it; a robot caught under falling
   pieces explodes (fire burst, dust clouds). The **dynamite barrel** is the only thing that explodes: on its first
   hit, and its blast throws the structure over rather than destroying it. Parked wagons roll off ramps and ledges
   into the structures below. The animal itself doesn't burst on impact: it tumbles on through the wreck, then fades.
   A **switch** button (under the Locker) reopens Character Select between shots to swap the animals still to fire
   — never more than are left. From World 1 level 5 on, a **swap hint** suggests a better-suited animal from the
   lineup when a stage calls for it (robots under roofs → Cluck / Gerald; stone → Bessie / Horace; robots spread
   wide → Ducky / Billy / Woolly; a tall tower → Percy / Horace), at most once per stage.
   Most levels have **two stages**: clearing the first shows "STAGE CLEARED!" (or "ONE SHOT! ADDING A FARM FURY" —
   a bonus animal — when it took a single shot) and the camera pans to the second.
6. Run out of animals with robots left → **revive** offer ("to add a Farm Fury": one more animal) or Level Failed.
7. **Results:** stars, coin payout, Double Coins (rewarded ad), then on to the next level.

## 3. Characters (Built — all eight scripted with art)

Each animal has one ability, triggered by a tap in flight. Two animals join per world, the whole roster by Sky
Islands, each released after a number of that world's levels are cleared (like Stampede / Arcade's "clear N levels"
ladder): Cluck from the start, Bessie after 1 Meadow Ruins level; Percy after 1 and Woolly after 8 Frozen Tundra
levels; Ducky 1 / Horace 8 in Watermill Village; Gerald 1 / Billy 8 in Sky Islands. Until then their cards are
greyed out in Character Select ("LOCKED - clear N levels") and can't be picked; the clear that releases one shows
the New Character page.

| Animal | Ability | Real-world logic | Strong against | Introduced |
|---|---|---|---|---|
| **Cluck** (chicken) | **Cluster Bomb** — lays a spread of eggs that scatter | Chickens lay eggs | Wood, spread-out targets; punches through hay | World 1 |
| **Bessie** (cow) | **Ground Slam** — radial shockwave on landing | Cows are huge and heavy | Stone, tall unstable towers | World 1 |
| **Percy** (pig) | **Bounce Roll** — curls up, bounces 3×, gaining speed | Pigs are round | Low, spread-out defences | World 2 |
| **Woolly** (sheep) | **Wool Balls** — throws three bouncy wool balls that each hit up to three times | Shearing — fleece as ammo | Wide targets, ice | World 2 |
| **Ducky** (duck) | **Water Swirls** — three spinning swirls of pond water that grow as they fly and burst over the structure, knocking it over | Ducks on the pond | Wide, top-heavy towers | World 3 |
| **Horace** (horse) | **Horseshoe Throw** — hurls two heavy iron horseshoes that ricochet through the structure | Horses are shod | Dense timber towers | World 3 |
| **Gerald** (turkey) | **Feather Bombs** — drops three bombs that burst in a small radius | Ruffled feathers | Clustered robots, wood | World 4 |
| **Billy** (goat) | **Mud Throw** — one mud ball that splats over a wide radius | Goats love mud | Spread-out structures | World 4 |

Personalities (from v2): Cluck anxious then delighted; Bessie slow and dignified, devastating on impact; Percy
gleeful chaos; Woolly bewildered by its clones; Ducky smug; Horace noble until the kick; Gerald self-important;
Billy furious and unstoppable.

**Art:** each animal has cannon-game poses — Idle, Loaded, In-flight, Impact, Trigger (ability) — plus a framed
select card and a thumbs-up portrait used in the in-game queue. Horace's ability and impact poses are pending
(Kling AI). **Physics:** heavier animals (Bessie mass 28 vs Cluck 8) hit harder. Every built world uses
Angry-Birds-measured framing: the camera is fixed per level and gravity and launch speed are set in screen terms.
Shots fly in slight slow motion (the anticipation of the shot) on a light, floaty arc — projectile gravity 0.38
screen heights/s² since 13 Nov 2026 (was 0.5, which felt heavy and short), so a full-power shot carries well past
the stage.

**Thrown abilities:** Gerald (Feather Bombs), Woolly (Wool Balls) and Billy (Mud Throw) throw projectiles that
burst in a radius or bounce and hit; robots take egg-strength damage from them.

## 4. Enemies — the Harvest Robots (Built)

Three robot tiers plus a boss in every world; each world has its own look for each tier (Stampede's world robots).

| Tier | Health | Role |
|---|---|---|
| Basic | 35 | Toughest regular robot |
| Harvester | 28 | Standard guard |
| Semi-Harvester | 26 | Most common guard |
| Boss | 90 | One per world, resists direct hits (structural collapse hurts it most) |

**Damage model (topple, don't break — 11 Nov 2026):** blocks never break; a hit shoves them over. A direct animal
hit, one egg or thrown-ability hit, or one crush from a falling piece kills a regular robot. Bosses still divide every
hit by their resistance (a boss fight). The dynamite blast blows up robots right beside it and throws everything else.
Thrown abilities always leave forward and down (into the structure, never over it), faster than the animal. Robots stand still on live-physics levels (patrolling would shove the structure) and flash when hit.

**Bosses (13 Nov 2026):** each boss holds a single pose until it is destroyed (no cycling through hurt / defeated
art). Knocked over but not destroyed, it gets back up after a moment instead of lying there; it resists rolling. Any
robot — boss or not — that is knocked off the screen or falls far below the ground counts as destroyed, so a level
can never be left unwinnable. **Fair placement:** the level designers drop any robot sheltered behind a mound that no
real shot can reach (a shot must be able to hit it, or hit a piece right beside it, at its height, on a fairly flat
arc).

**Settings → Character Story → Robots** shows one world at a time (< and >): the world's boss in the middle of a
slowly turning ring of its three robots, and a card with the tapped robot's name and story — each boss has its own
short write-up.

| World | Basic | Harvester | Semi-Harvester | Boss |
|---|---|---|---|---|
| 1 Meadow Ruins | Pawn | Harvester | Semi-Harvester | **The Commander** |
| 2 Frozen Tundra | Frost Robot | Ice Harvestor | Glacier Harvestor | **Frost Tyrant** |
| 3 Watermill Village | Water Robot | Harvester | Scout | **Mill Stone Mauler** |
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
| 3 Watermill Village | 22 | River valley, watermill village, bulrushes | Mill Cannon (look) | Ducky, Horace |
| 4 Sky Islands | 24 | Floating islands, clouds, balloons | Farm Cannon | Gerald, Billy |
| 5 Sunken City | 22 | Flooded ruins, coral, fish | Farm Cannon | — |
| 6 Robot Mothership | 16 | Deep space, the Mothership | Farm Cannon | — |
| 7 Dustbowl Canyon *(paid)* | 12 | Red-rock canyon, frontier towns on mesas, tumbleweeds | Prospector Cannon (look) | — |
| 8 Harvest Fairground *(paid)* | 12 | The robots' harvest fair on circus podiums, fireworks | Circus Cannon (look) | — |
| 9 Crop Factory *(paid)* | 12 | Production lines on machine housings, steam | Pneumatic Cannon (look) | — |

160 levels in all. Each world has its own painted backdrop and its own scenery props (cottages, ferris wheels,
coral, silos…), and the same building blocks everywhere (World 2 shows them as ice and snow).

**The paid worlds (built Nov 2026, same framed template as Worlds 1-6):** two rooms per level, robot-built cities
on each world's mounts with world-specific set pieces — World 7 mine headframes, water towers, two-storey saloons and
sandstone arches on canyon mesas, a saguaro that fires spines and a rolling tumbleweed; World 8 carousels, high
strikers with a dynamite bell, prize-cushion pyramids and tents on booths on circus podiums, a popcorn cart and a
rolling pumpkin; World 9 conveyors, hydraulic presses, stacked steel crates and crane arms on machine housings, a corn
hopper and a rolling cog. Bosses: the Drill Rig, the Ringmaster, the Harvester Prime, each dropped onto its keep by
the mothership. Room-change wipes: sandstorm, confetti cannon, steam blast.

**Planned — the per-world launcher mechanics from v2** (not built; every world currently uses the Farm Cannon's
drag-to-aim): Ice Cannon ricochet + freeze zones (W2), Water Wheel timing + fire spread on wood (W3), Airdrop
Biplane timing + updraft steering (W4), Torpedo Tube bubble-pop + current lanes and levers (W5), Gravity Sling
gravity wells + zero-G ability variants (W6). Character weight affecting range (W1) is built through animal mass.

## 6. Level Design

**Level anatomy:** cannon and animal queue on the left; open space for the arc; robot fortress on the right
(x ≈ 3–8.4 world units); world backdrop and props behind.

**World 1** (rebuilt Nov 2026, Angry Birds style) is designed in `tools/design_world1.py`, which checks every
piece is supported and every target reachable, and renders previews. The structures:
- multi-storey towers (mostly four to six tapering storeys, a robot every other floor) that topple easily and
  break on the way down;
- balancing beams (see-saws carrying a robot against a barrel);
- **mounts** at near, middle and far distances: floating islands, a tall rock mesa, a scaffold, a wagon bed and a
  ramp;
- **rolling wagons:** a wagon parked on a ramp, a mesa edge or a high island, loaded with robots or dynamite — hit
  it and it rolls or tips down into the bigger structure below (L05, L07, L09, L11, L12, L13, L17);
- **farm contraptions** (Nov 2026): a **sunflower** that sprays a fan of seeds into the structure beside it when
  anything hits it (L02, L06, L08, L16), and a **hay hoist** — a timber crane with a heavy water bucket on a rope;
  shoot the rope or bucket and it drops onto what's below (L04, L06);
- **weather** per level: golden afternoon, morning mist, overcast, rain with lightning, dusk with fireflies —
  purely visual mood (sky grade, haze, wind strength, particles);
- level 1 is a simple shelter; level 18 is the Commander's keep (boss), in a thunderstorm — the Commander is
  dropped off by the robot mothership, leaps onto the keep, and the ship flies away (Angry Birds-style boss intro).

All layouts sit between x 5.5 and 13.5 so nothing is too far apart. A headless physics probe confirms every World 1
structure stands untouched until hit.

**World 2 — Frozen Tundra** (rebuilt Nov 2026 on the World 1 template, `tools/design_world2.py`): 22 framed levels
under the aurora, in snowfall, blizzards, ice fog and polar dawn. Structures are frosted timber with ice where World 1
has stone; mounts are ice floes, ice pillars, frosted scaffolds and snow ramps; a **sled** replaces the wagon. Frozen
contraptions: **ice crystals** that fire a fan of ice shards, **frosted hoists** that drop a block of ice, and **ice
boulders** that roll off ledges and ramps. Percy and Woolly lead the lineups. The boss is the **Frost Tyrant** — an
ice-armoured war robot with a freeze-ray arm, dropped onto his keep by the robot mothership.

**World 3 — Watermill Village** (built Nov 2026 on the same template, `tools/design_world3.py`): 22 framed levels in
a river valley the robots have taken over — summer mornings, river mist, spring showers (with a rainbow), lantern-lit
dusk with fireflies, and a summer thunderstorm. Structures are iron-banded mill timber with **river-stone masonry**
where World 1 has stone, and **flour sacks** instead of hay. Mounts are grassy river islets, stone bridge piers and
fishing jetties on stilts; a **rowing boat** replaces the wagon and slides down a **boat slipway**. Village
contraptions: **water pumps** that gush a flat jet of water into the structure, the mill's **sack hoist** dangling a
**millstone**, and **millstones** that roll off ledges and slipways. Ducky leads most lineups. The cannon is the **Mill
Cannon** (oak staves in copper hoops, a dripping copper pipe) on a stack of flour sacks. The boss is the **Mill Stone
Mauler** — a steam-and-waterwheel brute, dropped onto his keep by the robot mothership. Level ideas: the weir's
stepping stones, a toll bridge with a troll-Harvester underneath, a flour store on sack stilts, a market on a long
islet, the miller's manor, the sluice-gate fortress.

**Worlds 4–9** are generated from World 1's towers (`WorldLevelGenerator`), so the physics is the already-tuned
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
the generated levels are untested in play. The physics probe (Editor tool) checks stability only, not
solvability.

## 7. Progression (Built)

- Levels unlock in order; the boss opens once every other level in its world is complete.
- Clearing a world's boss unlocks the next story world; its two animals are released as its levels are cleared.
- After a boss: Level Complete → **World Unlocked** page (the new world's badge revealed from a silhouette over
  turning golden rays, confetti, "Next stop: …") → New Worlds promo (first clear) → World Select on the new world.
  A **New Character** page announces the animals the new world brings.
- Paid worlds unlock by purchase only.
- Coins: 10 + 5 per star on a first clear; 3 + 5 per new star on replays; Double Coins via a rewarded ad.
- Existing World 1 progress from before the franchise port migrates automatically.

## 8. User Interface (Built — the franchise flow)

```
Title page (poster + pulsing PRESS START; tap anywhere) -> Landing (poster; Play / Exit / Settings)
  -> World Select (badge carousel: 6 story worlds, then 3 paid)
  -> Level Select (world backdrop; padlock / question / 1-2-3 star boards / boss shield)
  -> Character Select (card carousel; tap the centred card and it drops into the next open lineup slot)
  -> Playing (HUD: thumbs-up animal queue top-left, coins, pause, Locker)
  -> Pause / revive prompt / Results (Level Complete or Failed canvas, payout, Double Coins)
Settings cog -> Menu hub: Settings (Sound, Leaderboards, Character Story, Policies) / Shop / Visit Our Store
```

Results, Pause and New Character canvases carry the Farm Fury logo; each world has its own versions. Esc / Android
back closes the top overlay. A brief slow-motion beat plays between the last robot falling and the results.

The game opens on the title page; coming back from World Select or a Home button lands on the Play / Exit /
Settings page. Both posters are 20:9, filling a phone screen. The **menu hub** advertises the sister games: a card
for **Farm Fury Stampede** (left) and **Farm Fury Arcade** (right) — the app icon, PLAY FREE — opening the store page
behind the parental gate (no rewards for installing; store policy). The **Locker** (in-level) shows three rows —
cannon skins, trails, impact effects — each tile ticked when equipped; tap again to take it off.

## 9. Monetisation (Built; needs store accounts)

Ethical-first, unchanged in spirit from v2: free through every story level, no pay-to-win, no energy system, no
loot boxes, every level completable without spending. The model now matches the franchise (Arcade/Stampede):

| Item | Price |
|---|---|
| Remove Ads (+100 coins) | $4.99 |
| Coin packs: 100 / 500 / 5,000 / 15,000 | $0.99 / $3.99 / $9.99 / $19.99 |
| Paid worlds (Dustbowl, Fairground, Crop Factory) | $3.99 each |
| Cosmetics: cannon skins (Golden, Pumpkin, Corncob, Milk Churn, Tractor), flight trails, impact effects (Confetti, Popcorn, Fireworks, Feathers, Egg Splat) — shared by every animal | $1.99 or 2,500 coins |

- **Ads:** rewarded (revive — placement `continue_after_death`; Double Coins — `double_coins_level_complete`);
  interstitial every 6 level loads; banner on Pause / Level Failed — none with Remove Ads. LevelPlay mediation with
  AdMob and Unity Ads, set up like Stampede (13 Nov 2026: Cannonfire's own LevelPlay apps, ad units and AdMob apps;
  child-directed). Not yet tested on a device.
- **Revive:** +1 animal for 5 coins (once) or a rewarded ad (unlimited).
- **Cosmetics show off:** an equipped cannon skin appears the moment it is chosen and wears a golden glow (a gold
  outline, a halo and twinkling stars), so a bought cannon never looks like the standard one.
- Purchases and external links go through a parental gate. Coins, purchases and cosmetics are checksum-protected.
- **Cross-promotion:** the menu hub links to Farm Fury Stampede and Farm Fury Arcade (see section 8).
- **Dropped from v2:** season pass, starter pack, power-ups. **Still open:** creating the `farmfury_` products in App
  Store Connect / Play Console.

## 10. Visual & Audio Design

- **Style:** chunky cartoon renders with bold outlines; warm organic animals against cold machinery.
- **Backdrops:** World 1 (framed levels) is the cornfield stack: far painting, drifting clouds and sun glow,
  hazy sandstone bluffs with a ruined tower, wooded hills with water towers, mid corn, the corn wall swaying in the
  wind, ground fog, crows and a biplane crossing, and the weather layer (rain, lightning, pollen/fireflies/chaff);
  the older World 1 stack is three painted layers; World 2 a two-layer aurora stack; Worlds 3–9 one full
  painting each (shared with Stampede).
- **Effects:** wood/stone debris bursts, explosions, egg splats, dust — every burst sized from the visible art of
  what broke (about 1.5x a block's or robot's size), never bigger than the action.
- **Audio:** the menu theme and World 1's Sunrise Meadows track; Worlds 2-9 play the franchise world tracks from
  Farm Fury Stampede; each animal's franchise ability sound on the ability tap (Woolly has none yet); cannon shot;
  falling screams for Cluck and Bessie; robot hit/death SFX. **Planned:** falling loops for the other six animals.
- The win/lose celebration videos and the World 1 → 2 transition video were **removed** (November 2026).

## 11. Technical Specifications (Built)

- **Engine:** Unity 6.5 (6000.5.0f1), URP 2D, Physics2D (gravity −20), New Input System (pointer covers touch),
  uGUI built in code for menus, TextMeshPro.
- **Levels:** `LevelData` ScriptableObjects (blocks, robots, lineup, world, boss flag), generated by
  `LevelDataGenerator` (World 1 + L19) and `WorldLevelGenerator` (Worlds 2–9) — replaces v2's JSON descriptors.
- **Backend:** none yet. Saves are local (PlayerPrefs via SaveManager). Planned: Supabase (economy/backend) and
  Crashlytics, per the July 2026 plan. Analytics via Unity Gaming Services; ads via LevelPlay; purchases via Unity IAP.
- **Platforms:** iOS and Android (landscape, safe-area aware), bundle id `com.farmfury.cannonfire`. Home-screen
  name "Cannonfire".
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
| 4 World 1 (18 levels + boss) | Done — rebuilt Nov 2026: tall live-physics towers, mounts, rolling wagons |
| 5 Worlds 2–9 | Levels, robots, bosses, backdrops, props built (Nov 2026); per-world launcher mechanics planned |
| 6 Polish & release | Monetisation built; next: Play-mode verification of everything, level validator, store setup, device testing, soft launch |

**Post-launch ideas (from v2, not scheduled):** daily challenge levels, weekly timed challenges, story events,
leaderboards on the "Perfect" goal, seasonal cosmetics.

## 14. Risks and Mitigation

| Risk | Impact | Mitigation |
|---|---|---|
| Generated levels too easy / hard / unsolvable | High | Built from tuned World 1 towers; play-test each world; add the level validator |
| Tall towers fragile once woken (L03, L05, L11) | Medium | Probe stress mode flags them; widen posts if play feels twitchy |
| Nothing verified in Play mode | High | Full play-through of every screen and world before store submission |
| Art style drift between AI sessions | Medium | Image-to-image from existing art; reuse franchise art |
| Per-world mechanics never land | Medium | Worlds are fully playable with the cannon; mechanics are additive |
| IP perception (Angry Birds) | High | Cannon not slingshot, farm animals not birds, original robots and story |
| Solo developer burnout | High | Small concrete milestones; AI pipeline carries execution load |

---

*Farm Fury Cannonfire — Game Design Document v3.4. Built by one developer, powered by AI. One farm to save.*
