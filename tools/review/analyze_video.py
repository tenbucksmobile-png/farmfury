"""Measure a recorded session so it can be compared against the reference framework (docs/REFERENCE_FRAMEWORK.md).

Works on any recording (FarmFury or a reference game). All measurements are in SCREEN units so different games
compare directly: "sh" = fractions of the screen height, "sw" = of the screen width, times in seconds.

What it measures (vision only, no game data needed):
  camera   - pans (duration, distance in sw, peak speed) and the PARALLAX ratio of each screen band
             (sky / middle / ground) during those pans: 1.0 = moves with the playfield, 0 = fixed to the screen
  zoom     - frames where the image scales (camera zoom in/out), from the phase-correlation confidence drop
  flights  - projectile tracks fitted with a parabola: gravity (sh/s^2), launch speed (sh/s), angle, flight
             time, horizontal range (sw), on-screen size of the projectile (sh)
  activity - bursts of motion after an impact: duration, peak, the downward speed of falling debris (sh/s,
             optical flow), and the time to settle (motion back under 5% of the peak)
And for a FarmFury session with markers (parse_markers.py), each shot's in-game numbers alongside.

Outputs in <session>/analysis/:
  report.json, report.md             numbers
  sheet_overview.jpg                 one frame every few seconds
  sheet_seg_NN.jpg                   each activity burst / flight, 16 frames, timestamps
  still_NN.jpg                       full-resolution rest frames with a 5% grid, for reading sizes by eye

Usage: python analyze_video.py [sessions/<id> | path/to/video.mp4] [--max-seconds N] [--start S]
"""

import argparse
import json
import math
import sys
from pathlib import Path

import cv2
import numpy as np

from common import session_arg

ANALYSIS_W = 480                   # analysis resolution (width); frames are downscaled to this
BANDS = {"sky": (0.08, 0.35), "middle": (0.35, 0.65), "ground": (0.68, 0.95)}  # vertical spans of the frame
PAN_PX = 0.35                      # ground-band shift (analysis px/frame) above which the camera counts as panning
ZOOM_RESPONSE = 0.08               # phase-correlation response below this while things move = probably zooming
DIFF_THRESHOLD = 22
SETTLE_FRACTION = 0.05
MAX_MISS_FRAMES = 18               # a projectile may vanish this many frames (behind HUD, blur) and keep its track


def phase_shift(prev, cur, window):
    # Copies: with a window, cv2.phaseCorrelate writes into its inputs (these are views of the frames).
    (dx, dy), response = cv2.phaseCorrelate(prev.copy(), cur.copy(), window)
    return dx, dy, response


def band(img, name):
    h = img.shape[0]
    a, b = BANDS[name]
    return img[int(a * h):int(b * h)]


def read_frames(path, start, max_seconds):
    cap = cv2.VideoCapture(str(path))
    if not cap.isOpened():
        sys.exit(f"Cannot open {path}")
    fps = cap.get(cv2.CAP_PROP_FPS) or 30.0
    step = max(1, round(fps / 30.0))
    cap.set(cv2.CAP_PROP_POS_MSEC, start * 1000)
    idx = 0
    while True:
        ok, frame = cap.read()
        if not ok:
            break
        t = start + idx / fps
        if max_seconds and t - start > max_seconds:
            break
        if idx % step == 0:
            yield t, frame
        idx += 1
    cap.release()


def analyse(video, start=0.0, max_seconds=0.0):
    rows = []          # per analysed frame
    full_frames = {}   # t -> full-res frame, kept sparsely for stills/sheets
    prev = None
    blobs_per_frame = []
    flow_down = []
    windows = {}
    cam_x = 0.0
    keep_every = 0.2
    last_kept = -1e9
    for t, frame in read_frames(video, start, max_seconds):
        h0, w0 = frame.shape[:2]
        scale = ANALYSIS_W / w0
        small = cv2.resize(frame, (ANALYSIS_W, int(h0 * scale)), interpolation=cv2.INTER_AREA)
        gray = cv2.cvtColor(small, cv2.COLOR_BGR2GRAY).astype(np.float32)
        H, W = gray.shape
        if t - last_kept >= keep_every:
            full_frames[round(t, 3)] = frame
            last_kept = t
        if prev is None:
            prev = gray
            rows.append({"t": t, "cam_dx": 0.0, "cam_x": 0.0, "energy": 0.0, "response": 1.0})
            blobs_per_frame.append([])
            flow_down.append(0.0)
            continue

        shifts = {}
        for name in BANDS:
            pb, cb = band(prev, name), band(gray, name)
            key = (name, pb.shape)
            if key not in windows:
                windows[key] = cv2.createHanningWindow((pb.shape[1], pb.shape[0]), cv2.CV_32F)
            shifts[name] = phase_shift(pb, cb, windows[key])
        gdx, gdy, gresp = shifts["ground"]
        cam_dx = -gdx                       # image moving left = camera moving right
        cam_x += cam_dx

        # Motion with the camera move removed (ground plane), so debris/projectile motion stands out.
        # Each screen zone is aligned with its own band's shift, so parallax layers don't read as motion.
        prev_aligned = np.empty_like(prev)
        cuts = [0, int(0.35 * H), int(0.65 * H), H]
        for (y0, y1), name in zip(zip(cuts, cuts[1:]), ("sky", "middle", "ground")):
            sdx, sdy, sresp = shifts[name]
            if sresp < 0.05:
                sdx, sdy = gdx, gdy
            M = np.float32([[1, 0, sdx], [0, 1, sdy]])
            prev_aligned[y0:y1] = cv2.warpAffine(prev, M, (W, H), borderMode=cv2.BORDER_REPLICATE)[y0:y1]
        diff = cv2.absdiff(gray, prev_aligned)
        diff[: int(0.08 * H)] = 0                    # HUD strip
        # Noise floor adapts to the codec: well above the typical per-pixel change of a still frame.
        floor = max(DIFF_THRESHOLD, 6.0 * float(np.median(diff[int(0.08 * H):])))
        mask = (diff > floor).astype(np.uint8)
        mask = cv2.morphologyEx(mask, cv2.MORPH_OPEN, np.ones((3, 3), np.uint8))
        energy = float(mask.mean())

        n, labels, stats, cents = cv2.connectedComponentsWithStats(mask)
        blobs = []
        for i in range(1, n):
            area = stats[i, cv2.CC_STAT_AREA] / (W * H)
            if 0.0003 < area < 0.03:
                blobs.append({"x": cents[i][0] / H, "y": cents[i][1] / H,   # both in sh (fractions of height)
                              "h": stats[i, cv2.CC_STAT_HEIGHT] / H, "area": area})
        blobs_per_frame.append(blobs)

        down = 0.0
        if energy > 0.0015 and abs(gdx) < PAN_PX:
            flow = cv2.calcOpticalFlowFarneback(prev_aligned.astype(np.uint8), gray.astype(np.uint8), None,
                                                0.5, 3, 15, 3, 5, 1.2, 0)
            vy = flow[..., 1][mask > 0]
            if vy.size > 30:
                down = float(np.percentile(vy, 90)) / H     # sh per frame (positive = downward)
        flow_down.append(down)

        rows.append({"t": t, "cam_dx": cam_dx / W, "cam_x": cam_x / W, "energy": energy,
                     "response": gresp,
                     "par": {k: (-v[0] / W) for k, v in shifts.items()},
                     "resp": {k: v[2] for k, v in shifts.items()}})
        prev = gray
    if not rows:
        sys.exit("No frames read.")
    return rows, blobs_per_frame, flow_down, full_frames, (H, W)


def find_pans(rows, dt):
    pans, cur = [], None
    for i, r in enumerate(rows):
        moving = abs(r["cam_dx"]) * ANALYSIS_W > PAN_PX and r["response"] > ZOOM_RESPONSE
        if moving and cur is None:
            cur = [i, i]
        elif moving:
            cur[1] = i
        elif cur is not None:
            if rows[cur[1]]["t"] - rows[cur[0]]["t"] >= 0.25:
                pans.append(cur)
            cur = None
    out = []
    for a, b in pans:
        seg = rows[a:b + 1]
        dist = sum(r["cam_dx"] for r in seg)
        ratios = {}
        for name in ("sky", "middle"):
            vals = [r["par"][name] / r["cam_dx"] for r in seg
                    if abs(r["cam_dx"]) * ANALYSIS_W > 1.0 and r["resp"][name] > 0.05]
            ratios[name] = round(float(np.median(vals)), 3) if len(vals) >= 3 else None
        out.append({"t0": round(seg[0]["t"], 2), "t1": round(seg[-1]["t"], 2),
                    "duration_s": round(seg[-1]["t"] - seg[0]["t"], 2),
                    "distance_sw": round(abs(dist), 3), "direction": "right" if dist > 0 else "left",
                    "peak_speed_sw_s": round(max(abs(r["cam_dx"]) for r in seg) / dt, 3),
                    "parallax_sky": ratios["sky"], "parallax_middle": ratios["middle"]})
    return out


def find_zooms(rows):
    zooms, cur = [], None
    for r in rows[1:]:
        z = r["response"] < ZOOM_RESPONSE and r["energy"] > 0.08
        if z and cur is None:
            cur = [r["t"], r["t"]]
        elif z:
            cur[1] = r["t"]
        elif cur:
            if cur[1] - cur[0] >= 0.2:
                zooms.append({"t0": round(cur[0], 2), "t1": round(cur[1], 2)})
            cur = None
    return zooms


def track_flights(rows, blobs_per_frame, aspect):
    """Greedy nearest-neighbour tracking in world-ish coordinates (screen position + camera offset)."""
    tracks, active = [], []
    for i, blobs in enumerate(blobs_per_frame):
        t = rows[i]["t"]
        camx_sh = rows[i]["cam_x"] * aspect            # camera offset in sh units
        pts = [(b["x"] + camx_sh, b["y"], b["h"]) for b in blobs]
        used = set()
        for tr in active:
            (t1, x1, y1, _), = tr["pts"][-1:]
            vx, vy = tr.get("v", (0.0, 0.0))
            dt = t - t1
            px, py = x1 + vx * dt, y1 + vy * dt
            best, bd = None, 0.06 + 0.5 * dt
            for j, (x, y, hh) in enumerate(pts):
                d = math.hypot(x - px, y - py)
                if j not in used and d < bd:
                    best, bd = j, d
            if best is None:
                tr["miss"] += 1
                continue
            used.add(best)
            x, y, hh = pts[best]
            ref = tr["pts"][-3] if len(tr["pts"]) >= 3 else tr["pts"][-1]   # smoother velocity estimate
            rdt = t - ref[0]
            tr["v"] = ((x - ref[1]) / rdt, (y - ref[2]) / rdt) if rdt > 0 else tr.get("v", (0, 0))
            tr["pts"].append((t, x, y, hh))
            tr["miss"] = 0
        for j, (x, y, hh) in enumerate(pts):
            if j not in used:
                tr = {"pts": [(t, x, y, hh)], "miss": 0}
                active.append(tr)
                tracks.append(tr)
        active = [tr for tr in active if tr["miss"] <= MAX_MISS_FRAMES]

    flights = []
    for tr in tracks:
        p = tr["pts"]
        if len(p) < 8:
            continue
        ts = np.array([q[0] for q in p]); xs = np.array([q[1] for q in p]); ys = np.array([q[2] for q in p])
        ts = ts - ts[0]
        span_x = xs.max() - xs.min()
        if span_x < 0.35 or ts[-1] < 0.35:      # must travel at least ~0.2 sw
            continue
        # y grows downward on screen; fit y = a t^2 + b t + c
        a, b, c = np.polyfit(ts, ys, 2)
        fit = a * ts ** 2 + b * ts + c
        ss_res = float(((ys - fit) ** 2).sum()); ss_tot = float(((ys - ys.mean()) ** 2).sum()) or 1e-9
        r2 = 1 - ss_res / ss_tot
        vx = float(np.polyfit(ts, xs, 1)[0])
        lin_res = float(((xs - np.polyval(np.polyfit(ts, xs, 1), ts)) ** 2).mean())
        if r2 < 0.85 or a <= 0 or lin_res > 0.01:
            continue
        g = 2 * a
        vy0 = -b
        flights.append({"t0": round(float(p[0][0]), 2), "t1": round(float(p[-1][0]), 2),
                        "points": len(p), "fit_r2": round(r2, 3),
                        "gravity_sh_s2": round(float(g), 3),
                        "launch_speed_sh_s": round(math.hypot(vx, vy0), 3),
                        "angle_deg": round(math.degrees(math.atan2(vy0, abs(vx))), 1),
                        "vx_sh_s": round(abs(vx), 3),
                        "flight_s": round(float(ts[-1]), 2),
                        "range_sw": round(float(span_x) / aspect, 3),
                        "projectile_h_sh": round(float(np.median([q[3] for q in p])), 3)})
    flights.sort(key=lambda f: -f["points"])
    kept = []   # drop fragments that sit inside a longer track's time span
    for f in flights:
        if not any(k["t0"] - 0.1 <= f["t0"] and f["t1"] <= k["t1"] + 0.1 for k in kept):
            kept.append(f)
    return sorted(kept[:40], key=lambda f: f["t0"])


def find_activity(rows, flow_down, dt):
    e = np.array([r["energy"] for r in rows])
    if len(e) < 5:
        return []
    k = max(1, int(0.2 / dt))
    smooth = np.convolve(e, np.ones(k) / k, mode="same")
    thr = max(0.0015, float(np.percentile(smooth, 60)) * 1.5)
    segs, cur = [], None
    for i, v in enumerate(smooth):
        if v > thr and cur is None:
            cur = [i, i]
        elif v > thr:
            cur[1] = i
        elif cur is not None:
            if (cur[1] - cur[0]) * dt > 0.4:
                segs.append(cur)
            cur = None
    if cur and (cur[1] - cur[0]) * dt > 0.4:
        segs.append(cur)
    out = []
    for a, b in segs:
        seg = smooth[a:b + 1]
        pk = int(np.argmax(seg)) + a
        settle = pk
        while settle < len(smooth) - 1 and smooth[settle] > SETTLE_FRACTION * smooth[pk] + 0.001:
            settle += 1
        panning = float(np.mean([abs(rows[i]["cam_dx"]) * ANALYSIS_W > PAN_PX for i in range(a, b + 1)]))
        fd = [flow_down[i] for i in range(a, b + 1) if flow_down[i] > 0]
        out.append({"t0": round(rows[a]["t"], 2), "t1": round(rows[b]["t"], 2),
                    "duration_s": round((b - a) * dt, 2), "t_peak": round(rows[pk]["t"], 2),
                    "peak_energy": round(float(smooth[pk]), 4),
                    "settle_s": round((settle - pk) * dt, 2),
                    "peak_fall_speed_sh_s": round(max(fd) / dt, 3) if fd else None,
                    "fraction_panning": round(panning, 2)})
    return out


def label(img, text):
    cv2.rectangle(img, (0, 0), (min(img.shape[1], 12 + 11 * len(text)), 26), (0, 0, 0), -1)
    cv2.putText(img, text, (6, 19), cv2.FONT_HERSHEY_SIMPLEX, 0.55, (255, 255, 255), 1, cv2.LINE_AA)
    return img


def grid(img, step=0.05):
    h, w = img.shape[:2]
    out = img.copy()
    for k in range(1, int(1 / step)):
        major = k % 2 == 0
        col = (0, 255, 255) if major else (0, 160, 160)
        x, y = int(k * step * w), int(k * step * h)
        cv2.line(out, (x, 0), (x, h), col, 1)
        cv2.line(out, (0, y), (w, y), col, 1)
        if major:
            cv2.putText(out, f"{k * step:.1f}", (x + 2, 14), cv2.FONT_HERSHEY_SIMPLEX, 0.4, col, 1)
            cv2.putText(out, f"{k * step:.1f}", (2, y - 2), cv2.FONT_HERSHEY_SIMPLEX, 0.4, col, 1)
    return cv2.addWeighted(img, 0.45, out, 0.55, 0)


def nearest(full_frames, t):
    keys = list(full_frames)
    return full_frames[min(keys, key=lambda k: abs(k - t))]


def sheet(full_frames, times, path, cols=4, cell_w=480, with_grid=True):
    cells = []
    for t in times:
        f = nearest(full_frames, t)
        h0, w0 = f.shape[:2]
        c = cv2.resize(f, (cell_w, int(h0 * cell_w / w0)))
        if with_grid:
            c = grid(c, 0.1)
        cells.append(label(c, f"t={t:.2f}s"))
    if not cells:
        return
    ch, cw = cells[0].shape[:2]
    rows = math.ceil(len(cells) / cols)
    canvas = np.zeros((rows * ch, cols * cw, 3), np.uint8)
    for i, c in enumerate(cells):
        r, k = divmod(i, cols)
        canvas[r * ch:(r + 1) * ch, k * cw:(k + 1) * cw] = c[:ch, :cw]
    cv2.imwrite(str(path), canvas, [cv2.IMWRITE_JPEG_QUALITY, 82])


def summarise(values):
    v = [x for x in values if x is not None]
    if not v:
        return None
    return {"median": round(float(np.median(v)), 3), "min": round(float(min(v)), 3),
            "max": round(float(max(v)), 3), "n": len(v)}


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("target", nargs="?")
    ap.add_argument("--start", type=float, default=0.0)
    ap.add_argument("--max-seconds", type=float, default=0.0)
    ap.add_argument("--only", default="", help='gameplay windows to keep, e.g. "8-50,105-138" (drops ads/menus)')
    args = ap.parse_args()
    windows = [tuple(float(v) for v in w.split("-")) for w in args.only.split(",") if w]

    if args.target and Path(args.target).is_file():
        video = Path(args.target).resolve()
        session_dir, session, shots = video.parent, {"game": video.stem}, []
    else:
        session_dir = session_arg([None, args.target] if args.target else [None])
        session = json.loads((session_dir / "session.json").read_text(encoding="utf-8"))
        video = session_dir / session["video"]
        shots_path = session_dir / "shots.json"
        shots = json.loads(shots_path.read_text(encoding="utf-8")) if shots_path.exists() else []
    out = session_dir / "analysis"
    out.mkdir(exist_ok=True)

    print(f"Analysing {video} ...")
    rows, blobs, flow_down, full_frames, (H, W) = analyse(video, args.start, args.max_seconds)
    dt = float(np.median(np.diff([r["t"] for r in rows]))) if len(rows) > 1 else 1 / 30
    aspect = W / H
    pans = find_pans(rows, dt)
    zooms = find_zooms(rows)
    flights = track_flights(rows, blobs, aspect)
    activity = find_activity(rows, flow_down, dt)
    if windows:
        inside = lambda e: any(a <= e["t0"] and e["t1"] <= b for a, b in windows)
        pans, zooms = [p for p in pans if inside(p)], [z for z in zooms if inside(z)]
        flights, activity = [f for f in flights if inside(f)], [a for a in activity if inside(a)]

    report = {
        "game": session.get("game"), "video": str(video), "aspect": round(aspect, 3),
        "duration_s": round(rows[-1]["t"] - rows[0]["t"], 1), "fps_analysed": round(1 / dt, 1),
        "summary": {
            "pan_duration_s": summarise([p["duration_s"] for p in pans]),
            "pan_distance_sw": summarise([p["distance_sw"] for p in pans]),
            "pan_peak_speed_sw_s": summarise([p["peak_speed_sw_s"] for p in pans]),
            "parallax_sky": summarise([p["parallax_sky"] for p in pans]),
            "parallax_middle": summarise([p["parallax_middle"] for p in pans]),
            "gravity_sh_s2": summarise([f["gravity_sh_s2"] for f in flights]),
            "launch_speed_sh_s": summarise([f["launch_speed_sh_s"] for f in flights]),
            "flight_s": summarise([f["flight_s"] for f in flights]),
            "flight_range_sw": summarise([f["range_sw"] for f in flights]),
            "projectile_h_sh": summarise([f["projectile_h_sh"] for f in flights]),
            "collapse_duration_s": summarise([a["duration_s"] for a in activity]),
            "settle_s": summarise([a["settle_s"] for a in activity]),
            "debris_fall_speed_sh_s": summarise([a["peak_fall_speed_sh_s"] for a in activity]),
            "zoom_events": len(zooms),
        },
        "pans": pans, "zooms": zooms, "flights": flights, "activity": activity,
        "shots_from_markers": shots,
    }
    if shots:
        def pick(key, field):
            return [s[key][field] for s in shots if key in s and field in s[key]]
        report["summary"]["game_flight_s"] = summarise(pick("impact", "flight_s"))
        report["summary"]["game_settle_s"] = summarise(pick("settle", "settle_s"))
        report["summary"]["game_peak_fall_sh_s"] = summarise(pick("settle", "peak_fall_shps"))
        report["summary"]["game_blocks_per_shot"] = summarise(pick("settle", "destroyed"))

    # Pictures to look at.
    duration = rows[-1]["t"] - rows[0]["t"]
    step = max(1.0, duration / 24)
    sheet(full_frames, [rows[0]["t"] + i * step for i in range(min(24, int(duration / step) + 1))],
          out / "sheet_overview.jpg", cols=6, cell_w=320, with_grid=False)
    segments = sorted([(a["t0"] - 1.0, a["t1"] + 0.5, "activity") for a in activity] +
                      [(f["t0"] - 0.3, f["t1"] + 0.3, "flight") for f in flights[:10]])
    merged = []
    for s in segments:
        if merged and s[0] <= merged[-1][1]:
            merged[-1] = (merged[-1][0], max(merged[-1][1], s[1]), merged[-1][2])
        else:
            merged.append(s)
    for i, (a, b, _) in enumerate(merged[:30]):
        a = max(rows[0]["t"], a)
        times = list(np.linspace(a, min(b, rows[-1]["t"]), 16))
        sheet(full_frames, times, out / f"sheet_seg_{i:02d}.jpg")
        cv2.imwrite(str(out / f"still_{i:02d}.jpg"), label(grid(nearest(full_frames, a), 0.05), f"rest t={a:.2f}s"),
                    [cv2.IMWRITE_JPEG_QUALITY, 88])
    report["segments"] = [{"index": i, "t0": round(a, 2), "t1": round(b, 2)} for i, (a, b, _) in enumerate(merged[:30])]

    (out / "report.json").write_text(json.dumps(report, indent=1), encoding="utf-8")
    lines = [f"# {report['game']} - {report['duration_s']}s analysed", "", "| metric | median | min | max | n |",
             "|---|---|---|---|---|"]
    for k, v in report["summary"].items():
        if isinstance(v, dict):
            lines.append(f"| {k} | {v['median']} | {v['min']} | {v['max']} | {v['n']} |")
        else:
            lines.append(f"| {k} | {v} | | | |")
    (out / "report.md").write_text("\n".join(lines) + "\n", encoding="utf-8")
    print("\n".join(lines))
    print(f"\n{len(merged[:30])} segment sheets + stills in {out}")


if __name__ == "__main__":
    main()
