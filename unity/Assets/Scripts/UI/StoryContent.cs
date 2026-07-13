// Lore content for Settings > Story (2026-07-13 first draft). Split into its own data file,
// separate from MainMenuController's UI-building code, so the text can be edited/expanded
// without touching layout code. This is a first draft written to establish tone and structure —
// review and rewrite freely, nothing here is meant to be final.
//
// "Robot Enemy Files" covers the 4 actual robot TYPES in the game (Basic/Harvester/
// SemiHarvester/Commander), not "18 pages, one per robot" as originally specced — there are only
// 4 distinct robot types, reused across the 18 levels; there's no such thing as 18 unique named
// robots to profile. World Journal covers all 6 planned worlds even though only World 1 (Meadow
// Ruins) exists in-game today — the other 5 are 0% built (Phase 5) — using the names/mechanics
// already established in CLAUDE.md's world table.
public static class StoryContent
{
    public struct Entry
    {
        public string Title;
        public string Body;
        public Entry(string title, string body) { Title = title; Body = body; }
    }

    public static readonly Entry[] Story =
    {
        new Entry("The Farm Fury Story",
@"Sunrise Meadows was quiet for a thousand harvests. Then the sky cracked open with a hum of gears, and the Robots came down on burning trails of exhaust, claiming to be ""here to optimize the land."" What they meant was: strip it, cage the barns, and fold the fields into cold metal grids. The animals watched their home get bolted shut piece by piece, and for a while, they did what animals do: they waited, and they hoped the humans would come back.

The humans never came back. So the animals stopped waiting.

It started small — Cluck, of all creatures, refused to be herded into a Robot transport crate, and flung himself, feathers first, straight through a control panel. It didn't do much. But it did something. Word got around the coop, then the pasture, then the whole meadow: the Robots weren't invincible. They rusted. They toppled. They exploded in a satisfying shower of bolts if you hit them exactly right.

So the farm built a cannon out of a broken silo strut and a length of fence wire, and the animals lined up to be fired out of it — not because any of them were soldiers, but because every last one of them had something to fight for: a barn, a pond, a field of wildflowers, a home.

This is Farm Fury: not a war between armies, but a farmyard's answer to being told it didn't matter anymore. One launch at a time, the animals are taking Sunrise Meadows back — and whatever's waiting in the Frozen Tundra, the Watermill, the Sky Islands, the Sunken City, and the Robot Mothership itself, they're bringing the fight there too."),
    };

    public static readonly Entry[] Characters =
    {
        new Entry("Cluck the Chicken — Cluster Bomb",
@"The first bird to ever refuse a Robot crate, and the reason the whole rebellion started. Cluck is loud, easily distracted by literally anything shiny, and inexplicably fearless once airborne — his flock always figured he was all bluster until the day he proved otherwise. Fires an egg-cluster mid-flight because, as Cluck puts it, ""one hit is a suggestion, five is an argument."""),
        new Entry("Bessie the Cow — Ground Slam",
@"Sunrise Meadows' gentlest resident and, when she finally lands, its heaviest argument. Bessie spent years just chewing cud and watching the sunset — until the Robots paved over her favorite grazing hill. Now when she hits the ground, she means it, and the whole structure around her feels it too."),
        new Entry("Percy the Pig — Bounce Roll",
@"Percy never met an obstacle he didn't want to roll straight through. Mischievous, a little vain about his snout, and allergic to slowing down, Percy discovered mid-battle that curling into a ball and picking up speed on the bounce works just as well on Robots as it used to work on hay bales back home."),
        new Entry("Woolly the Sheep — Triple Clone",
@"Nobody's ever sure which Woolly is the real one, including — allegedly — Woolly. Quiet, a bit of a copycat by nature, Woolly turned that habit into a weapon: split into three mid-air, and suddenly one sheep is a small flock, hitting from three angles at once."),
        new Entry("Ducky the Duck — Skip Shot",
@"Ducky grew up skipping stones across the farm pond for fun, and it turns out that same flat, low, skimming flight works even better on a Robot patrol line. Cheerful, a little smug about her aim, and always first to volunteer for a tricky shot."),
        new Entry("Horace the Horse — Rear Kick",
@"Big, proud, and a little old-fashioned, Horace was the farm's workhorse long before the Robots showed up — plowing fields, hauling carts, never one to complain. He brought that same stubborn work ethic to the fight: on impact, he kicks back, sending debris flying into whatever's still standing behind him."),
        new Entry("Gerald the Turkey — Puff Up",
@"Gerald has always had main character energy and a temper to match. Easily offended, endlessly dramatic, and secretly a little insecure about being ""just a turkey,"" Gerald found his calling mid-battle: puff up bigger and bigger until he's less bird, more wrecking ball."),
        new Entry("Billy the Goat — Headbutt Through",
@"Billy headbutts things. Fence posts, barn doors, the occasional unsuspecting Robot patrol unit — it's less a combat technique and more just who Billy is as a creature. Blunt, stubborn, and utterly fearless, Billy doesn't go around obstacles. He goes through them."),
    };

    public static readonly Entry[] Robots =
    {
        new Entry("Robot Pawn — Threat Level: Low",
@"Standard-issue patrol unit, mass-produced and none too proud of it. Strengths: cheap, expendable, deployed in numbers to wear down a defense through sheer volume. Weaknesses: no ranged capability, no special armor, and a chassis that dents on the first solid hit. How to defeat: a single well-aimed direct hit is usually enough — don't waste your strongest animal on one of these."),
        new Entry("Harvester — Threat Level: Moderate",
@"Repurposed farm-clearing unit, originally built to strip fields bare — now armored and turned against the animals whose home it was designed to consume. Strengths: sturdier frame than a Pawn, often anchors a structure's weak point. Weaknesses: still relatively low HP once you get past the intimidating chassis. How to defeat: two solid hits, or one good explosion from a nearby haybale or barrel, will finish the job."),
        new Entry("Semi-Harvester — Threat Level: Moderate",
@"A lighter, faster cousin of the Harvester, deployed in clusters to overwhelm a position rather than tank hits alone. Strengths: shows up in numbers, often stacked along elevated platforms to be harder to reach. Weaknesses: individually fragile — the danger is in how many of them you'll face at once, not how tough any single unit is. How to defeat: look for chain-reaction opportunities — one exploding barrel near a cluster of Semi-Harvesters can end a fight in one shot."),
        new Entry("Commander — Threat Level: Severe (Boss)",
@"The largest Robot unit ever fielded against Sunrise Meadows, and the first true commanding officer the animals have faced. Strengths: far more durable than any standard unit, guards a fortified stone tower, and visibly escalates through Alert and Critical states as the fight wears on. Weaknesses: everything he protects goes down with him — destroy the Commander and his entire guarded structure collapses in one motion. How to defeat: this is not a one-shot fight. Watch for his pose changes — Alert means he's hurt, Critical means he's close. Finish him there."),
    };

    // Credits (2026-07-13, Settings > About) — only verifiably-true project facts per explicit
    // user decision; Team/Special Thanks are left as empty placeholder sections rather than
    // invented names. Fill those in once real names are provided.
    public static readonly Entry[] Credits =
    {
        new Entry("Credits",
@"ENGINE
Built with Unity 6.5 (URP 2D).

ART
Character, robot, and environment art generated with Kling AI.

TEAM
[Add team credits here]

SPECIAL THANKS
[Add special thanks here]"),
    };

    public static readonly Entry[] Worlds =
    {
        new Entry("World 1 — Meadow Ruins",
@"The animals' own home, and the first ground the Robots ever touched. Barns, hay bales, and wooden fences now stand rebuilt into makeshift fortresses, launched from a jury-rigged Farm Cannon — drag to aim, pull to power. A heavier animal flies differently than a light one here; the Cannon doesn't care who you are, only how much you weigh."),
        new Entry("World 2 — Frozen Tundra",
@"North of the meadow, where the Robots set up a supply line across ice fields that never used to exist. The Ice Cannon ricochets shots off frozen surfaces, and freeze zones slow anything that flies through them — a world that fights back against the animals almost as hard as the Robots do."),
        new Entry("World 3 — Watermill Village",
@"A once-peaceful river settlement, its old Water Wheel repurposed by the animals into a timing-based launcher — tap to fire, and get the rhythm right. Wood structures here catch fire and spread the damage, turning a single good hit into a slow-burning collapse."),
        new Entry("World 4 — Sky Islands",
@"Robots don't fly, but they built floating platforms anyway, stringing supply routes between islands drifting far above the ground. The animals fight back from an Airdrop Biplane, timing their drop as they pass overhead, riding updraft columns to steer mid-fall."),
        new Entry("World 5 — Sunken City",
@"A drowned ruin the Robots are dredging up for reasons no one's fully figured out. Fired from a Torpedo Tube, animals travel angle-first through current lanes and lever-switch gates, popping bubbles as they go — the strangest battlefield the farm has ever seen."),
        new Entry("World 6 — Robot Mothership",
@"The source. Every Robot that ever marched into Sunrise Meadows came from here, and this is where the animals finally take the fight to them — launched from a Gravity Sling through zero-gravity wells that bend a shot's whole trajectory. If the animals win here, the invasion ends."),
    };
}
