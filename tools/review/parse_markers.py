"""Place a FarmFury session's [Highlight] log lines on the video timeline.

Writes markers.json (every marker, t = seconds from video start) and shots.json (one entry per shot:
shot -> impact -> settle, with the game's own measurements). Usage: python parse_markers.py [sessions/<id>]
"""

import json
import re
import sys

from common import session_arg

LINE = re.compile(r"\[Highlight\] type=(\S+) ms=(\d+)(.*)")
DETAIL = re.compile(r"(\w+)=(\S+)")


def num(v):
    try:
        return float(v.replace(",", ".")) if re.fullmatch(r"-?\d+[.,]?\d*", v) else v
    except ValueError:
        return v


def load_markers(session_dir):
    session = json.loads((session_dir / "session.json").read_text(encoding="utf-8"))
    log = session_dir / session.get("logcat", "logcat.txt")
    if not log.exists() or "recording_start_pc_ms" not in session:
        return []
    start, offset = session["recording_start_pc_ms"], session.get("clock_offset_ms", 0)
    markers = []
    for raw in log.read_text(encoding="utf-8", errors="replace").splitlines():
        m = LINE.search(raw)
        if not m:
            continue
        t = (int(m.group(2)) - offset - start) / 1000.0
        if t >= 0:
            markers.append({"t": round(t, 2), "type": m.group(1),
                            **{k: num(v) for k, v in DETAIL.findall(m.group(3))}})
    return sorted(markers, key=lambda x: x["t"])


def group_shots(markers):
    shots, current, layout = [], None, None
    for m in markers:
        if m["type"] == "layout":
            layout = m
        elif m["type"] == "shot":
            current = {"t_shot": m["t"], "shot": m, "layout": layout, "events": []}
            shots.append(current)
        elif current is not None:
            if m["type"] == "impact" and "impact" not in current:
                current["impact"], current["t_impact"] = m, m["t"]
            elif m["type"] == "settle" and "settle" not in current:
                current["settle"], current["t_settle"] = m, m["t"]
            elif m["type"] in ("block_destroyed", "robot_defeated"):
                current["events"].append(m)
    return shots


def main():
    session_dir = session_arg(sys.argv)
    markers = load_markers(session_dir)
    shots = group_shots(markers)
    (session_dir / "markers.json").write_text(json.dumps(markers, indent=1), encoding="utf-8")
    (session_dir / "shots.json").write_text(json.dumps(shots, indent=1), encoding="utf-8")
    print(f"{len(markers)} markers, {len(shots)} shots -> {session_dir}")


if __name__ == "__main__":
    main()
