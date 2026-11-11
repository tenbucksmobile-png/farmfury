"""
Farm Fury - World 4 (Sky Islands) level designer (2026-11-11).

World 4 built on the World 1-3 template (user: "build the next world utilising the exact process you have used for every
world build - applying the same frameworks"): framed levels with live physics, robot-built wonky structures (see
design_world1.tower and the set pieces), standing on floating isles and rocks at different heights above a sea of
clouds, the sky mounts (cloud-wrapped stone pillar, sky dock, slide ramp), the sky contraptions (thunderclouds that hurl
lightning, sky cranes dangling a thunder wheel, thunder wheels rolling down the slide), the sky robots and the Storm
Baron, dropped off by the mothership. Uses the shared piece/mount/check code in design_world1.py (world=4).

Usage (repo root):  python tools/design_world4.py [--render OUTDIR]
Writes unity/Assets/Editor/LevelDataGenerator.World4.cs; then FarmFury -> Generate All Level Data.
"""
import math
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import design_world1 as d   # noqa: E402  (its World 1 levels build on import; harmless)

G = d.G
H, D, P, W, C, B = "Horace", "Ducky", "Percy", "Woolly", "Cluck", "Bessie"
LEVELS = []
tower, seesaw, aframe, lean = d.tower, d.seesaw, d.aframe, d.lean
upside_pyramid, wobble_stack, stilt_house = d.upside_pyramid, d.wobble_stack, d.stilt_house
cantilever, balanced_rock, barrel_deck = d.cantilever, d.balanced_rock, d.barrel_deck


def level(k, name, par, birds, comment, weather="ClearSkies", boss=False):
    key = name.replace("'", "").replace(" ", "").replace("-", "")
    L = d.Level(f"W04_L{k:02d}_{key}", f"W4_L{k:02d}", name, par, birds, comment, boss=boss, world=4)
    L.weather = weather
    LEVELS.append(L)
    return L


CLOUDS = [(v, d.MOUNTS_W[4][n]) for v, n in ((0, "CloudLow"), (1, "CloudMid"), (2, "CloudTall"))]
MAX_STRETCH, MIN_STRETCH = 1.3, 0.7


def cloud(L, x, surface, top_width):
    """A cloud mountain rising from the cloud sea (the ground) to a flat plateau at `surface` (2026-11-11, replaced the
    floating isles and rocks). Sized later by fit_clouds() to what actually stands on it; `top_width` bounds where
    that is looked for. Returns the surface."""
    L.islands.append(dict(x=x, s=surface, w=top_width, v=0, solids=[], h=1.0, uv=1.0, stretch=True, cloud=True,
                          span=(x - top_width / 2, x + top_width / 2)))
    return surface


def _shape(need, top_width):
    """Best of the low / medium / tall shapes for a plateau top_width wide at height need: (variant, m, w, h)."""
    best = None
    for v, m in CLOUDS:
        f0, f1 = m["flat"]
        w = top_width / (f1 - f0 - 0.04)            # base width giving that plateau (a little margin each side)
        h = need / m["uv"]
        st = h / (w / m["aspect"])                  # vertical stretch needed at that width
        if st > MAX_STRETCH: w, st = h * m["aspect"] / MAX_STRETCH, MAX_STRETCH   # too tall: widen instead
        score = abs(math.log(max(st, MIN_STRETCH))) + (1.0 if st < MIN_STRETCH else 0.0)
        if best is None or score < best[0]: best = (score, v, m, w, h)
    return best[1:]


MAX_SURFACE = G + 4.0   # ...but never lifted above this (tall towers on top would reach the HUD)
LIFT_CLOUDS = False      # the sky cities place their peaks' heights deliberately (walkways line up across them)
MIN_PROPORTION = 0.85   # a cloud flatter than this (vs its art) is raised, with everything on it, to look like a mountain


def _owner(clouds, bottom, cx):
    """The cloud whose plateau something with this bottom / centre x stands on or above: the highest one below it."""
    c = [i for i in clouds if bottom >= i["s"] - 0.05 and i["span"][0] - 0.6 <= cx <= i["span"][1] + 0.6]
    return max(c, key=lambda i: i["s"]) if c else None


def _lift_block(b, dy):
    b["y"] += dy
    b["poly"] = [(x, y + dy) for x, y in b["poly"]]
    b["vc"] = (b["vc"][0], b["vc"][1] + dy)
    v = b["vis"]; b["vis"] = (v[0], v[1], v[2] + dy, v[3] + dy)


def fit_clouds(L):
    """Size every cloud to the pieces and robots standing on its plateau (+0.35 u each side), centred under them; a
    cloud that would come out flatter than MIN_PROPORTION of its art is raised (and what stands on it lifted) so it
    keeps a mountain's proportions."""
    clouds = [i for i in L.islands if i.get("cloud")]
    own_b = [(_owner(clouds, min(y for _, y in b["poly"]), sum(x for x, _ in b["poly"]) / len(b["poly"])), b) for b in L.blocks]
    own_r = [(_owner(clouds, r["surface"], r["x"]), r) for r in L.robots]
    for isl in clouds:
        xs = []
        for o, b in own_b:
            if o is isl and min(y for _, y in b["poly"]) < isl["s"] + 0.05: xs += [x for x, _ in b["poly"]]
        for o, r in own_r:
            if o is isl: xs += [r["x"] - 0.3 * r["vis"], r["x"] + 0.3 * r["vis"]]
        a, b_ = (min(xs) - 0.35, max(xs) + 0.35) if xs else isl["span"]
        v, m, w, h = _shape(isl["s"] - G, b_ - a)
        nat = w / m["aspect"]
        dy = 0.0
        if LIFT_CLOUDS and h < MIN_PROPORTION * nat:
            h = max(h, min(MIN_PROPORTION * nat, (MAX_SURFACE - G) / m["uv"]))
            dy = G + m["uv"] * h - isl["s"]
            for o, bl in own_b:
                if o is isl: _lift_block(bl, dy)
            for o, r in own_r:
                if o is isl: r["y"] += dy; r["surface"] += dy
        f0, f1 = m["flat"]
        isl.update(v=v, w=w, h=h, uv=m["uv"], solids=m["boxes"], s=isl["s"] + dy,
                   x=(a + b_) / 2 - ((f0 + f1) / 2 - 0.5) * w)


# The floating isle / rock helpers now raise cloud mountains (the level designs keep their call sites).
def isle(L, x, surface, width):
    """A wide cloud mountain whose plateau carries a structure `width` wide (was a floating grass isle)."""
    return cloud(L, x, surface, width * 0.9)


def rock(L, x, surface, width):
    """A narrow cloud mountain with a plateau ~`width` wide (was a small floating rock)."""
    return cloud(L, x, surface, width)


def cloud_checks(L):
    """Pieces and robots must not start inside a cloud mountain (they overlap their neighbours as a range)."""
    issues = []
    for isl in (i for i in L.islands if i.get("cloud")):
        base = isl["s"] - isl["uv"] * isl["h"]
        for (x0, x1, y0, y1) in isl["solids"]:
            sl, sr = isl["x"] - isl["w"] / 2 + x0 * isl["w"], isl["x"] - isl["w"] / 2 + x1 * isl["w"]
            box = [(sl, base + y0 * isl["h"]), (sr, base + y0 * isl["h"]), (sr, base + y1 * isl["h"]), (sl, base + y1 * isl["h"])]
            for j, b in enumerate(L.blocks):
                if d._sat_overlap(d._shrink(b["poly"], 0.02), box) > 0.02:
                    issues.append(f"block {j} ({b['kind']}) inside the cloud at x={isl['x']:.2f}")
            for k, r in enumerate(L.robots):
                w = 0.4 * r["vis"]; bot = r["surface"] + 0.05
                rb = [(r["x"] - w / 2, bot), (r["x"] + w / 2, bot), (r["x"] + w / 2, bot + 0.8 * r["vis"]), (r["x"] - w / 2, bot + 0.8 * r["vis"])]
                if d._sat_overlap(rb, box) > 0.02:
                    issues.append(f"robot {k} inside the cloud at x={isl['x']:.2f}")
            for g in L.gadgets:
                gx, gy = g["x"], g["y"] + 0.1
                if sl < gx < sr and box[0][1] < gy < box[2][1]:
                    issues.append(f"{g['type']} at x={gx:.2f} inside the cloud at x={isl['x']:.2f}")
    return sorted(set(issues))


def runaway_wheel(L, ramp_x, ramp_w=5.4, wheel=1.0):
    """A slide ramp with a thunder wheel parked at the plateau's edge: knock it and it rolls down into whatever is
    below. Returns the slope's end x."""
    s, l, r = L.mount("Ramp", ramp_x, ramp_w)
    L.boulder(r - wheel * 0.55, s, wheel)
    return ramp_x - ramp_w / 2 + 0.98 * ramp_w


# ═══════════════════════════════════════════ sky-city pieces (2026-11-11) ═══════════════════════════════════
# User: "make your structures wilder, more absurd, not generic structures - they mustnt make sense from a physics
# perspective - they must also be almost like cities of elevation". Live blocks start asleep and only have to TOUCH
# what is under them, never balance, so these are built top-heavy, lopsided and cantilevered on purpose; the first
# well-placed hit brings them down. Districts on cloud peaks at different heights, joined by walkways on pylons.
INSET = 0.15             # half a sky post: a post's inset from a roof edge (P is Percy)
WT = d.WOOD_T            # plank thickness


def AT(h):
    """World y h units above the cloud floor (named AT: H is Horace)."""
    return G + h


def pylon(L, x, b, top):
    """A post from b up to exactly `top` (so walkways line up across districts). Returns top."""
    return L.post(x, b, top - b)


def walkway(L, xa, xb, y, robots=()):
    """A plank bridge whose underside is at y (pylons / roofs / decks at exactly y hold it). robots: (x, kind)."""
    t = L.plank(xa, xb, y)
    for x, k in robots: L.robot(x, t, k)
    return t


def lollipop(L, x, b, stem=1.6, cap=2.6, hut=True, ends=("pawn", None), top="pawn"):
    """One thin stem, a cap far wider than it, a hut perched in the middle and robots out on the cap's ends."""
    t = L.post(x, b, stem)
    t = L.plank(x - cap / 2, x + cap / 2, t)
    for side, k in zip((-1, 1), ends):
        if k: L.robot(x + side * (cap / 2 - 0.4), t, k)
    if hut:
        t = L.hut(x - 0.5, x + 0.5, t, 0.9)
    if top: L.robot(x, t, top)
    return t


def upturned(L, x, b, floors=4, w0=0.9, grow=0.45, h=0.95, robots=None, top="pawn", stone_roof=False):
    """A tower that gets WIDER every storey: each roof overhangs both sides, the next storey stands on the overhangs.
    Seeded lopsided. Returns the top; L.last_span = the top roof's (left, right)."""
    rng = d._rng(L, "upturned", x, b)
    c, w = x, w0
    ra = rb = x
    for i in range(floors):
        t = L.post(c - w / 2 + INSET, b, h); L.post(c + w / 2 - INSET, b, h)
        rk = robots[i] if robots and i < len(robots) else None
        if rk and w >= 1.2: L.robot(c, b, rk)
        ol = grow / 2 + rng.uniform(0, 0.35); orr = grow / 2 + rng.uniform(0, 0.35)
        ra, rb = c - w / 2 - ol, c + w / 2 + orr
        b = L.plank(ra, rb, t, stone=stone_roof and i == floors - 1)
        w += grow
        lo, hi = ra + w / 2 - 0.02, rb - w / 2 + 0.02
        c = rng.uniform(lo, hi) if hi > lo else (ra + rb) / 2
    if top: L.robot((ra + rb) / 2, b, top)
    L.last_span = (ra, rb)
    return b


def spire(L, x, b, n=4, h=0.85, cap=1.0, zig=0.3, top="pawn", mid=None):
    """A zig-zag spire: every storey a single post with a short plank stuck on it off-centre, the next post on the
    plank's far end. mid: a robot on the middle cap. Returns the top; L.last_top_x = its x."""
    c = x
    for i in range(n):
        t = L.post(c, b, h)
        off = zig if i % 2 else -zig
        b = L.plank(c + off - cap / 2, c + off + cap / 2, t)
        c += off
        if mid and i == n // 2 - 1: L.robot(c, b, mid)
    if top: L.robot(c, b, top)
    L.last_top_x = c
    return b


def balcony(L, post_x, b, h, reach, side=1, robot="pawn", top=None):
    """One post with a long plank stuck out to one side and a whole hut out on the far end, over thin air."""
    t = L.post(post_x, b, h)
    dk = L.plank(post_x - 0.45, post_x + reach, t) if side > 0 else L.plank(post_x - reach, post_x + 0.45, t)
    far = post_x + side * (reach - 0.6)
    if robot: L.robot(far, dk, robot)
    r = L.hut(far - 0.55, far + 0.55, dk, 0.95)
    if top: L.robot(far, r, top)
    return r


def teeter_town(L, x, b, post_h=1.2, beam=3.6, left="pawn", right="pawn", tops=(None, None)):
    """A seesaw with a whole hut on each end."""
    t = L.post(x, b, post_h)
    t = L.plank(x - beam / 2, x + beam / 2, t)
    for side, k, tk in ((-1, left, tops[0]), (1, right, tops[1])):
        hx = x + side * (beam / 2 - 0.6)
        if k: L.robot(hx, t, k)
        r = L.hut(hx - 0.55, hx + 0.55, t, 0.9)
        if tk: L.robot(hx, r, tk)
    return t


def slab_on_point(L, x, b, half=0.9, h=1.4, slab=2.6, ends=("pawn", "pawn")):
    """Two planks leaning into a point and a long stone slab balanced on the very tip, robots on both ends."""
    n0 = len(L.blocks)
    L.beam(x - half, b, x - 0.03, b + h); L.beam(x + 0.03, b + h, x + half, b)
    tip = max(y for bl in L.blocks[n0:] for _, y in bl["poly"])
    t = L.plank(x - slab / 2, x + slab / 2, tip + 0.004, stone=True)
    for side, k in zip((-1, 1), ends):
        if k: L.robot(x + side * (slab / 2 - 0.4), t, k)
    return t


def chimney(L, x, b, n=5, s=0.55, cap=1.6, top="pawn"):
    """A tall single column of crates, each a little off, with a wide plank lid and a robot up top."""
    t = b
    for k in range(n):
        t = L.box(x + (0.04 if k % 2 else -0.04), t, s, stone=False)
    t = L.plank(x - cap / 2, x + cap / 2, t)
    if top: L.robot(x, t, top)
    return t


def stone_crown(L, x, b, h=1.6, s=0.9, top="pawn"):
    """A heavy stone block crowned on one skinny post, a robot on the stone."""
    return balanced_rock(L, x, b, h=h, s=s, top=top)



# ═══════════════════════════════════════════ the sky-city generator (2026-11-11) ══════════════════════════════
# User asked for "big & dense" cities: several districts on peaks of different heights, pieces stacked on pieces,
# walkways between districts, robots spread through it, filling the right two-thirds of the screen. Each level is a
# spec (skyline profile, number of peaks, robot range, fixed contraptions); the generator tries seeds until the
# designer's checks pass with 0 issues, so every level is valid by construction and stable from run to run.
import random as _random

BASE_PIECES = ["lollipop", "lollipop", "upturned", "upturned", "spire", "balcony", "balcony", "teeter", "slab", "chimney",
               "crown", "pyramid", "stilts"]   # no plain tower: too sensible
STACK_PIECES = ["spire", "lollipop", "crown", "chimney", "upturned"]


def _roof(L, n0):
    """(left, right, top y) of the highest level plank / slab added since block n0 (else the highest piece)."""
    new = L.blocks[n0:]
    if not new: return None
    flat = [b for b in new if abs(b["rot"]) < 0.5 and b["kind"] in ("plank", "slab")]
    b = max(flat or new, key=lambda q: q["vis"][3])
    return b["vis"][0], b["vis"][1], b["vis"][3]


def _piece(L, kind, x, b, rng, side, stacked=False):
    r = lambda lo, hi: rng.uniform(lo, hi)
    if kind == "lollipop":
        lollipop(L, x, b, stem=r(0.8, 1.2) if stacked else r(1.0, 1.8), cap=r(1.8, 2.4) if stacked else r(2.4, 3.2),
                 hut=not stacked and rng.random() < 0.6, ends=(rng.choice([None, "pawn"]), rng.choice([None, "pawn"])), top=None)
    elif kind == "upturned":
        upturned(L, x, b, floors=2 if stacked else rng.randint(2, 4), w0=0.9 if stacked else 1.0, grow=0.55,
                 robots=[None, "pawn", None, "semi"], top=None)
    elif kind == "spire":
        spire(L, x, b, n=rng.randint(1, 2) if stacked else rng.randint(2, 4), h=r(0.7, 0.9), cap=1.0, zig=r(0.18, 0.3),
              top=None, mid=None if stacked else rng.choice([None, "pawn"]))
    elif kind == "balcony":
        balcony(L, x - side * 0.2, b, r(0.8, 1.6), r(2.2, 2.8), side=side, robot="pawn", top=None)
    elif kind == "teeter":
        teeter_town(L, x, b, post_h=r(0.8, 1.2), beam=r(3.0, 3.8), left=rng.choice(["pawn", None]), right=rng.choice(["pawn", "semi"]))
    elif kind == "slab":
        slab_on_point(L, x, b, half=0.75, h=r(1.0, 1.4), slab=r(2.4, 3.0), ends=(rng.choice(["pawn", None]), rng.choice(["pawn", None])))
    elif kind == "chimney":
        chimney(L, x, b, n=rng.randint(2, 3) if stacked else rng.randint(3, 5), top=None)
    elif kind == "crown":
        stone_crown(L, x, b, h=r(0.7, 1.0) if stacked else r(1.0, 1.8), s=0.75, top=None)
    elif kind == "pyramid":
        upside_pyramid(L, x, b, rows=(1, 2, 3), s=0.5, robots=[rng.choice(["pawn", None]), None, rng.choice(["pawn", None])])
    elif kind == "tower":
        tower(L, x, b, rng.randint(2, 3), width=1.6, h=1.0, taper=0.15, robots=["pawn", None, "semi"])
    elif kind == "stilts":
        stilt_house(L, x, b, legs_h=r(1.2, 2.0), w=1.3, robot="pawn", top=None)


def _heights(profile, n, rng):
    lo, hi = 1.6, 4.6
    if profile == "rise": hs = [lo + (hi - lo) * i / max(1, n - 1) for i in range(n)]
    elif profile == "fall": hs = [hi - (hi - lo) * i / max(1, n - 1) for i in range(n)]
    elif profile == "valley": hs = [hi - (hi - lo) * (1 - abs(2 * i / max(1, n - 1) - 1)) for i in range(n)]
    elif profile == "mount": hs = [lo + (hi - lo) * (1 - abs(2 * i / max(1, n - 1) - 1)) for i in range(n)]
    else: hs = [rng.uniform(lo, hi) for _ in range(n)]
    return [h_ + rng.uniform(-0.3, 0.3) for h_ in hs]


def city(L, rng, x0=5.6, x1=15.4, peaks=4, profile="random", stack=0.5, walk=0.7, mounts=("cloud",), ground=0.0):
    """Lay out the districts: peaks across [x0, x1] at the profile's heights, an absurd piece on each, sometimes a
    second piece stacked on top, walkways between neighbours, a robot on every final roof."""
    pitch = (x1 - x0) / peaks
    hs = _heights(profile, peaks, rng)
    roofs = []
    for i in range(peaks):
        x = x0 + pitch * (i + 0.5) + rng.uniform(-0.2, 0.2)
        kind = rng.choice(mounts)
        if rng.random() < ground: kind = "ground"
        if kind == "cloud": b = cloud(L, x, AT(hs[i]), rng.uniform(1.4, 2.0))
        elif kind == "mesa":
            b, l_, r_ = L.mount("Mesa", x, 1.9); x = (l_ + r_) / 2
        elif kind == "dock":
            b, l_, r_ = L.mount("Scaffold", x, 2.3); x = (l_ + r_) / 2
        else: b = G
        side = 1 if i < peaks / 2 else -1
        n0 = len(L.blocks)
        _piece(L, rng.choice(BASE_PIECES), x, b, rng, side)
        roof = _roof(L, n0)
        if roof and roof[1] - roof[0] >= 0.9 and roof[2] < AT(7.5) and rng.random() < stack:
            n1 = len(L.blocks)
            _piece(L, rng.choice(STACK_PIECES), (roof[0] + roof[1]) / 2, roof[2], rng, side, stacked=True)
            roof = _roof(L, n1) or roof
        roofs.append(roof)
    for i in range(peaks - 1):                       # walkways between neighbouring roofs
        a, c = roofs[i], roofs[i + 1]
        if not a or not c or rng.random() > walk: continue
        gap = c[0] - a[1]
        if not 0.5 < gap < 3.4: continue
        y = max(a[2], c[2])
        if abs(a[2] - c[2]) < 0.35: y += 0.7         # too close to step: both get pylons
        ends = []
        for rf, inner in ((a, a[1] - 0.25), (c, c[0] + 0.25)):
            if y - rf[2] > 0.3: pylon(L, inner, rf[2], y); ends.append(inner)
            else: ends.append(rf[1] - 0.45 if rf is a else rf[0] + 0.45)
        xa, xb = min(ends) - 0.2, max(ends) + 0.2
        walkway(L, xa, xb, y, robots=[((xa + xb) / 2, "pawn")] if xb - xa > 1.6 and rng.random() < 0.7 else [])
    for rf in roofs:                                  # a robot on every final roof wide enough to stand on
        if rf and rf[1] - rf[0] >= 0.6: L.robot((rf[0] + rf[1]) / 2, rf[2], rng.choice(["pawn", "pawn", "semi"]))


def settle_robots(L, rng, lo, hi):
    """Robots go in last and nothing rests on them: drop any that a later piece was built through, then trim at
    random to the level's range. Returns False if fewer than lo are left."""
    # The in-game robot collider is 0.6 x 0.9 u whatever the art (RobotEnemy / LevelLoader.SpawnRobot) - wider and
    # taller than the designer's art-based box. A robot that overlaps wood when its collider switches on after the
    # reveal pop shoves the district apart (PhysicsProbe 2026-11-11: 10 of 24 cities fell by themselves).
    cols = [d._shrink(b["poly"], 0.02) for b in L.blocks]
    keep, boxes = [], []
    for r in L.robots:
        bot = r["surface"]
        box = [(r["x"] - 0.31, bot + 0.02), (r["x"] + 0.31, bot + 0.02), (r["x"] + 0.31, bot + 0.93), (r["x"] - 0.31, bot + 0.93)]
        clear = not any(d._sat_overlap(box, c) > 0.0 for c in cols) and not any(d._sat_overlap(box, o) > 0.0 for o in boxes)
        if r["kind"] == "boss" or clear: keep.append(r); boxes.append(box)
    boss = [r for r in keep if r["kind"] == "boss"]
    rest = [r for r in keep if r["kind"] != "boss"]
    while len(rest) > hi: rest.pop(rng.randrange(len(rest)))
    L.robots = rest + boss
    return len(rest) >= lo


def city_level(k, name, par, birds, comment, weather="ClearSkies", boss=False, robots=(5, 8), fixed=None, skip=0, **kw):
    """Try seeds until the city passes every designer check; append the winner to LEVELS. skip: pass over that many
    valid cities first (one that passed the designer but fell by itself in Unity's PhysicsProbe)."""
    key = name.replace("'", "").replace(" ", "").replace("-", "")
    if kw.get("peaks", 4) >= 5: kw.setdefault("x0", 6.0); kw.setdefault("x1", 15.8)
    for seed in range(800):
        L = d.Level(f"W04_L{k:02d}_{key}", f"W4_L{k:02d}", name, par, birds, comment, boss=boss, world=4)
        L.weather = weather
        rng = _random.Random(k * 1000 + seed)
        if fixed: fixed(L, rng)
        city(L, rng, **kw)
        if not settle_robots(L, rng, *robots): continue
        fit_clouds(L)
        if d.check(L) or cloud_checks(L): continue
        if any(b["vis"][3] > AT(11.0) for b in L.blocks): continue
        if skip > 0: skip -= 1; continue
        L.seed = seed
        LEVELS.append(L)
        return L
    raise RuntimeError(f"W4_L{k:02d} {name}: no valid city in 800 seeds")


# ── fixed contraptions per level ──────────────────────────────────────────────────────────────────────────────
def thunder(*xs):
    def f(L, rng):
        for x in xs: L.sunflower(x, G, 1.8)
    return f


def slide(ramp_x=6.0, ramp_w=4.8):
    def f(L, rng):
        L.city_x0 = runaway_wheel(L, ramp_x, ramp_w=ramp_w) + 0.3
    return f


# ═══════════════════════════════════════════ the levels ═══════════════════════════════════════════════════════
city_level(1, "Cloud Landing", 2, [H, H, C], """
The robots' first sky town: three cloud peaks climbing to the right, a crooked district on each.""",
           robots=(3, 4), peaks=3, profile="rise", stack=0.3, walk=0.5, x0=6.4, x1=13.6, fixed=thunder(4.6))
city_level(2, "Stepping Clouds", 2, [H, H, C], """
Four peaks stepping up and down, walkways strung between the roofs.""", weather="Windswept",
           robots=(4, 6), peaks=4, profile="random", stack=0.4, walk=0.9)
city_level(3, "Marble Folly", 2, [H, P, C], """
A skyline of stone pillars and clouds, the tallest districts in the middle.""", weather="SunsetClouds",
           robots=(4, 6), peaks=4, profile="mount", mounts=("cloud", "mesa"), stack=0.5)
city_level(4, "Thunder Slide", 2, [H, P, C], """
A thunder wheel at the top of the slide; below, a town on three peaks waits for it.""",
           robots=(4, 6), peaks=3, profile="rise", x0=10.6, x1=16.0, fixed=slide(6.0))
city_level(5, "Upside Down Temple", 2, [H, W, C], """
Districts stacked two pieces high on every peak — towers on towers.""",
           skip=1,  # first city fell by itself in PhysicsProbe (2026-11-11)
           robots=(5, 7), peaks=4, profile="valley", stack=0.9)
city_level(6, "Sky Crane", 2, [H, W, C], """
A sky crane dangling a thunder wheel over the low end of a town that climbs away from the cannon.""",
           weather="Windswept", robots=(4, 6), peaks=3, profile="rise", x0=6.2, x1=12.4,
           fixed=lambda L, rng: (L.hoist(15.2, G, 4.6, 0.4), L.robot(13.0, G, "semi"), L.sunflower(4.8, G, 1.7)))
city_level(7, "Pillar Perches", 2, [H, P, W], """
Marble pillars and sky docks carry the town, walkways from roof to roof.""", weather="SunsetClouds",
           robots=(5, 7), peaks=4, profile="random", mounts=("mesa", "dock", "cloud"), walk=1.0)
city_level(8, "Storm Front", 3, [H, W, C], """
Rain and lightning over a town of five districts.""", weather="StormFront",
           robots=(6, 8), peaks=5, profile="random", stack=0.5, fixed=thunder(4.8))
city_level(9, "Dynamite Deck", 2, [H, P, B], """
A valley town with the dynamite stored in the middle.""",
           robots=(5, 7), peaks=4, profile="valley",
           fixed=lambda L, rng: (L.barrel(10.6, G), L.barrel(11.2, G)))
city_level(10, "Balanced Rocks", 2, [H, B, C], """
Bessie's sky debut: stone crowns and towers on four peaks, a mountain-shaped skyline.""", weather="Windswept",
           skip=1,  # first city fell by itself in PhysicsProbe (2026-11-11)
           robots=(5, 7), peaks=4, profile="mount", stack=0.6)
city_level(11, "Lookout Stilts", 3, [H, P, B], """
A town falling away from a high lookout, walkways all the way down.""", weather="SunsetClouds",
           robots=(5, 8), peaks=4, profile="fall", walk=1.0, fixed=thunder(4.8))
city_level(12, "Harvest Moon", 3, [H, W, B], """
Under the stars: the thunder wheel at the top of the slide above a three-peak town.""", weather="StarlitSky",
           robots=(5, 7), peaks=3, profile="random", x0=10.2, x1=16.0, stack=0.7, fixed=slide(5.8))
city_level(13, "Windmill Isle", 3, [H, P, W], """
Five districts crammed together in a gusty valley.""", weather="Windswept",
           robots=(6, 8), peaks=5, profile="valley", stack=0.5)
city_level(14, "Cloud Bale Barn", 2, [H, B, C], """
A town partly on the clouds and partly on the ground, stacked high.""",
           robots=(5, 7), peaks=4, profile="random", ground=0.35, stack=0.7, fixed=thunder(5.0))
city_level(15, "Twin Docks", 3, [H, W, B], """
Sky docks and clouds, the town bridged end to end.""", weather="SunsetClouds",
           skip=1,  # first city fell by itself in PhysicsProbe (2026-11-11)
           robots=(6, 8), peaks=4, profile="random", mounts=("dock", "cloud"), walk=1.0)
city_level(16, "Lightning Rods", 2, [H, P, B], """
In the storm, a rising town with thunderclouds at both ends.""", weather="StormFront",
           robots=(5, 7), peaks=4, profile="rise", x0=6.6, x1=14.4, fixed=thunder(5.0, 15.6))
city_level(17, "Floating Market", 3, [H, W, C], """
A market town on five small peaks, stalls on every roof.""",
           robots=(6, 8), peaks=5, profile="random", stack=0.4, walk=0.8)
city_level(18, "Sky Temple", 3, [H, B, B], """
A temple town on a mountain of peaks, its middle districts stacked to the sky.""", weather="SunsetClouds",
           robots=(6, 8), peaks=4, profile="mount", stack=0.9, mounts=("cloud", "mesa"))
city_level(19, "Starlit Spires", 3, [H, P, W], """
Under the stars: marble pillars and clouds, every district crowned with something taller.""", weather="StarlitSky",
           robots=(6, 8), peaks=4, profile="rise", mounts=("mesa", "cloud"), stack=1.0)
city_level(20, "Wheel and Crane", 3, [H, W, B], """
The thunder wheel at the top of the slide and a town climbing away from it.""",
           robots=(5, 7), peaks=3, profile="rise", x0=10.2, x1=16.0, stack=0.6, fixed=slide(5.8))
city_level(21, "Stormbreak Citadel", 3, [H, B, B], """
The Storm Baron's outer citadel: five districts on peaks and pillars, dynamite at the gate.""", weather="StormFront",
           robots=(6, 9), peaks=5, profile="mount", mounts=("cloud", "mesa"), stack=0.6,
           fixed=lambda L, rng: (L.barrel(5.6, G), L.sunflower(4.6, G, 1.8)))
city_level(22, "Highwind Fortress", 3, [H, W, B], """
High winds over a fortress town, the highest districts at the back.""", weather="Windswept",
           robots=(6, 9), peaks=5, profile="rise", stack=0.7, walk=0.9)
city_level(23, "Gathering Storm", 3, [H, B, B], """
The last approach: a valley town under the storm, thunderclouds below.""", weather="StormFront",
           robots=(6, 9), peaks=5, profile="valley", stack=0.7, fixed=thunder(4.8))


def _baron(L, rng):
    s_ = cloud(L, 13.4, AT(2.0), 3.4)
    top = L.post(12.2, s_, 1.5, stone=True); L.post(14.6, s_, 1.5, stone=True)
    r_ = L.plank(11.9, 14.9, top, stone=True)
    L.robot(13.4, s_, "semi")
    t = tower(L, 13.4, r_, 3, width=2.2, h=1.1, taper=0.2, robots=["pawn", None, "semi"], wonky=False)   # he lands on it
    L.robot(13.4, t, "boss")


city_level(24, "The Storm Baron", 3, [H, B, B], """
Boss: the Storm Baron's keep at the back of his city (the mothership drops him on it), three districts in front.""",
           weather="StormFront", boss=True, robots=(6, 9), peaks=3, profile="rise", x0=5.8, x1=11.0, stack=0.6,
           fixed=_baron)


for _L in LEVELS: fit_clouds(_L)


# ═══════════════════════════════════════════ output ═══════════════════════════════════════════════════════
def write_cs():
    body = "\n\n".join(L.cs() for L in LEVELS)
    keep = ", ".join(f'"{L.file}"' for L in LEVELS)
    src = f"""// GENERATED by tools/design_world4.py — edit the designs there, not here.
// World 4 (Sky Islands) levels W4_L01-W4_L24 (2026-11-11): the World 1-3 template above the clouds — framed, live
// physics, robot-built wonky structures on floating isles, sky mounts and contraptions, the Storm Baron boss.

using UnityEditor;

public static partial class LevelDataGenerator
{{
    static void MakeWorld4Framed(string folder)
    {{
        // Drop any older World 4 asset that isn't one of these (the generated W04_* set had different names).
        var keep = new System.Collections.Generic.HashSet<string> {{ {keep} }};
        foreach (var guid in AssetDatabase.FindAssets("t:LevelData", new[] {{ folder }}))
        {{
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string file = System.IO.Path.GetFileNameWithoutExtension(path);
            if (file.StartsWith("W04_") && !keep.Contains(file)) AssetDatabase.DeleteAsset(path);
        }}

{body}
    }}
}}
"""
    path = os.path.join(d.UNITY, "Editor", "LevelDataGenerator.World4.cs")
    open(path, "w", encoding="utf-8").write(src)
    print("wrote", os.path.normpath(path))


def render(outdir):
    """Offline mock-up of every level at the framed zoom (backdrop, mounts, contraptions, pieces, robots)."""
    from PIL import Image, ImageOps, ImageDraw
    import numpy as np
    S = os.path.join(d.UNITY, "Sprites")
    PX, camH, asp = 40, 16.0, 16 / 9
    camW = camH * asp
    cx, cy = -3.72 + (0.5 - 0.26) * camW, G + (0.5 - 0.12) * camH
    left, bottom = cx - camW / 2, cy - camH / 2
    cache = {}

    def load(p):
        if p not in cache: cache[p] = Image.open(os.path.join(S, p)).convert("RGBA")
        return cache[p]

    def put(canvas, img, x, y, w, h, flip=False, rot=0.0):
        im = img.resize((max(1, int(w * PX)), max(1, int(h * PX))))
        if flip: im = ImageOps.mirror(im)
        if abs(rot) > 0.01: im = im.rotate(rot, expand=True, resample=Image.BICUBIC)
        canvas.paste(im, (int(x * PX - im.size[0] / 2 - left * PX), int((bottom + camH - y) * PX - im.size[1] / 2)), im)

    def tint(img, c):
        a = np.asarray(img).astype(np.float32).copy(); a[..., :3] *= np.array(c[:3], np.float32); return Image.fromarray(a.clip(0, 255).astype(np.uint8))

    def strip(canvas, p, h, b, col=(1, 1, 1)):
        im = tint(load(p), col); tw = h * im.size[0] / im.size[1]; k = math.floor((left - cx) / tw)
        for i in range(int(camW / tw) + 3):
            put(canvas, ImageOps.mirror(im) if (k + i) & 1 else im, cx + (k + i + 0.5) * tw, b + h / 2, tw, h)

    def overlay(canvas, col, a):
        if a <= 0: return
        canvas.alpha_composite(Image.new("RGBA", canvas.size, tuple(int(255 * v) for v in col) + (int(255 * a),)))

    WX = {  # mirrors CornfieldAmbience's sky profiles
        "ClearSkies": dict(sky=(1, 1, 1), land=(1, 1, 1), haze=(.86, .93, 1), hazeA=.2, grade=((.9, .96, 1), 0), rain=0),
        "Windswept": dict(sky=(.92, .96, 1), land=(.95, .98, 1), haze=(.84, .9, .98), hazeA=.28, grade=((.7, .82, 1), .05), rain=0),
        "SunsetClouds": dict(sky=(1, .82, .78), land=(1, .88, .86), haze=(1, .74, .66), hazeA=.3, grade=((.95, .5, .45), .1), rain=0),
        "StormFront": dict(sky=(.52, .58, .7), land=(.66, .72, .8), haze=(.58, .64, .74), hazeA=.42, grade=((.12, .18, .32), .22), rain=.8),
        "StarlitSky": dict(sky=(.42, .48, .72), land=(.6, .66, .86), haze=(.4, .46, .7), hazeA=.32, grade=((.12, .12, .4), .22), rain=0),
    }
    E = "Environment/Sky/"
    bases = {}

    def backdrop(wx):
        w = WX[wx]
        base = Image.new("RGBA", (int(camW * PX), int(camH * PX)), (120, 170, 230, 255))
        far = tint(load(E + "SkyFar.png"), w["sky"])
        anchor = G + 0.6
        fh = max(camW * far.size[1] / far.size[0], bottom + camH - anchor); fw = fh * far.size[0] / far.size[1]
        put(base, far, cx, anchor + fh / 2, fw, fh)
        overlay(base, w["haze"], w["hazeA"])
        soil = load(E + "SkySoil.png"); sh = 3.2; tw = sh * soil.size[0] / soil.size[1]
        for i in range(-3, 4): put(base, soil, cx + i * tw, G - sh * 0.929 + sh / 2, tw, sh, flip=bool(i & 1))
        return base

    pieces = {k: v + ".png" for k, v in d.SPRITE_W4.items()}
    robots = {"pawn": "Enemies/Robot/W4/Basic_left.png", "semi": "Enemies/Robot/W4/SemiHarvester_left.png",
              "harv": "Enemies/Robot/W4/Harvester_left.png", "boss": "Enemies/Robot/StormBaron.png"}
    mounts = {0: "Mount_CloudLow", 1: "Mount_CloudMid", 2: "Mount_CloudTall", 3: "Mount_Pillar", 4: "Mount_Dock", 6: "Mount_Ramp"}
    os.makedirs(outdir, exist_ok=True)
    thumbs = []
    for L in LEVELS:
        if L.weather not in bases: bases[L.weather] = backdrop(L.weather)
        c = bases[L.weather].copy()
        hb = load(E + "CloudBale.png")
        for row in range(2):
            for k in range(3 - row): put(c, hb, -3.87 + (k - (2 - row) * 0.5) * 0.95, G + 1.15 * (0.5 - 0.038) + row * 0.8, 1.15, 1.15)
        can = load("Environment/Launchers/SkyCannon.png"); put(c, can, -3.72, G + 2.3, 2.1, 2.1 * can.size[1] / can.size[0])
        for isl in L.islands:
            im = load(E + mounts[isl["v"]] + ".png"); h = isl["h"]
            put(c, im, isl["x"], isl["s"] - isl["uv"] * h + h / 2, isl["w"], h)
        for g in L.gadgets:
            if g["type"] == "StormCloud":
                im = load(E + "StormCloud.png"); h = g["size"]; put(c, im, g["x"], g["y"] + h / 2, h * im.size[0] / im.size[1], h)
            elif g["type"] == "ThunderWheel":
                im = load(E + "ThunderWheel.png"); put(c, im, g["x"], g["y"] + g["size"] / 2, g["size"], g["size"])
            else:
                put(c, load(E + "SkyCrane.png"), g["cl"] + g["cw"] / 2, g["y"] + g["size"] / 2, g["cw"], g["size"], flip=g["left"])
                bl, br, bb, bt = g["bucket"]; ax, ay = g["anchor"]
                ImageDraw.Draw(c).line([((ax - left) * PX, (bottom + camH - ay) * PX), ((ax - left) * PX, (bottom + camH - bt) * PX)], fill=(140, 107, 64, 255), width=2)
                im = load(E + "ThunderWheel.png"); bh = br - bl
                put(c, im, (bl + br) / 2, bt - bh / 2, br - bl, bh)
        for b in L.blocks:
            put(c, load(pieces[b["kind"]]), b["x"], b["y"], b["w"], b["h"], rot=b.get("rot", 0.0))
        for r in L.robots:
            im = load(robots[r["kind"]]); w = r["s"] * im.size[0] / 1746; h = r["s"] * im.size[1] / 1746
            put(c, im, r["x"], r["y"], w, h)
        overlay(c, *WX[L.weather]["grade"])
        import random as _r
        rnd = _r.Random(5); dr = ImageDraw.Draw(c)
        for _ in range(int(220 * WX[L.weather]["rain"])):
            x, y = rnd.uniform(0, c.size[0]), rnd.uniform(0, c.size[1])
            dr.line([x, y, x + 4, y + 22], fill=(210, 220, 240, 120), width=1)
        c = c.convert("RGB"); c.save(os.path.join(outdir, L.file + ".png")); thumbs.append((L, c))
    tw_, th_ = 480, 270
    sheet = Image.new("RGB", (tw_ * 3, th_ * math.ceil(len(thumbs) / 3)), (30, 30, 30))
    dd = ImageDraw.Draw(sheet)
    for i, (L, c) in enumerate(thumbs):
        sheet.paste(c.resize((tw_, th_)), ((i % 3) * tw_, (i // 3) * th_))
        dd.text(((i % 3) * tw_ + 6, (i // 3) * th_ + 4), f"{L.id} {L.name} [{L.weather}]", fill=(255, 255, 0))
    sheet.save(os.path.join(outdir, "_sheet.png"))
    print("rendered", len(thumbs), "levels to", outdir)


if __name__ == "__main__":
    bad = 0
    for L in LEVELS:
        for msg in d.check(L) + cloud_checks(L):
            print(f"{L.id}: {msg}"); bad += 1
    print(f"{len(LEVELS)} levels, {sum(len(L.blocks) for L in LEVELS)} blocks, {sum(len(L.robots) for L in LEVELS)} robots, {bad} issues")
    write_cs()
    if "--render" in sys.argv:
        render(sys.argv[sys.argv.index("--render") + 1])
