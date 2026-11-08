"""Side-by-side table of analysed sessions (reference game(s) vs FarmFury) against the framework's targets.

Usage:
    python compare.py sessions/ref-angrybirds-... sessions/ff-...      # any number of analysed sessions
    python compare.py                                                   # every analysed session
Writes tools/review/comparison.md and prints it. The ratio column is FarmFury / reference (first reference
session); anything outside 0.8-1.25 is flagged for the framework's tuning pass.
"""

import json
import sys
from pathlib import Path

from common import SESSIONS_DIR

HERE = Path(__file__).resolve().parent
METRICS = [
    ("gravity_sh_s2", "projectile gravity (sh/s²)"),
    ("launch_speed_sh_s", "launch speed (sh/s)"),
    ("flight_s", "flight time (s)"),
    ("flight_range_sw", "flight range (sw)"),
    ("projectile_h_sh", "projectile size (sh)"),
    ("pan_duration_s", "camera pan duration (s)"),
    ("pan_distance_sw", "camera pan distance (sw)"),
    ("pan_peak_speed_sw_s", "camera pan peak speed (sw/s)"),
    ("parallax_sky", "parallax: sky band"),
    ("parallax_middle", "parallax: middle band"),
    ("collapse_duration_s", "collapse duration (s)"),
    ("settle_s", "settle time after peak (s)"),
    ("debris_fall_speed_sh_s", "debris fall speed (sh/s)"),
    ("zoom_events", "zoom events"),
    ("game_flight_s", "FarmFury markers: flight (s)"),
    ("game_settle_s", "FarmFury markers: settle (s)"),
    ("game_peak_fall_sh_s", "FarmFury markers: peak fall (sh/s)"),
    ("game_blocks_per_shot", "FarmFury markers: blocks/shot"),
]


def value(summary, key):
    v = summary.get(key)
    if isinstance(v, dict):
        return v["median"]
    return v


def main():
    dirs = [Path(a).resolve() for a in sys.argv[1:]] or sorted(p.parent.parent for p in SESSIONS_DIR.glob("*/analysis/report.json"))
    reports = []
    for d in dirs:
        rp = d / "analysis" / "report.json"
        if not rp.exists():
            print(f"skip {d.name}: run analyze_video.py on it first")
            continue
        r = json.loads(rp.read_text(encoding="utf-8"))
        kind = json.loads((d / "session.json").read_text(encoding="utf-8")).get("kind", "reference") \
            if (d / "session.json").exists() else "reference"
        reports.append((d.name, kind, r))
    if not reports:
        sys.exit("No analysed sessions.")
    ref = next((r for r in reports if r[1] == "reference"), None)
    ff = next((r for r in reports if r[1] == "farmfury"), None)

    head = "| metric | " + " | ".join(n for n, _, _ in reports) + (" | FF/ref |" if ref and ff else " |")
    lines = [head, "|" + "---|" * (len(reports) + 1 + (1 if ref and ff else 0))]
    for key, label in METRICS:
        vals = [value(r["summary"], key) for _, _, r in reports]
        if all(v is None for v in vals):
            continue
        row = f"| {label} | " + " | ".join("–" if v is None else f"{v}" for v in vals)
        if ref and ff:
            a, b = value(ff[2]["summary"], key), value(ref[2]["summary"], key)
            if isinstance(a, (int, float)) and isinstance(b, (int, float)) and b:
                ratio = a / b
                flag = "" if 0.8 <= ratio <= 1.25 else " ⚠"
                row += f" | {ratio:.2f}{flag}"
            else:
                row += " | "
        lines.append(row + " |")
    text = "\n".join(lines) + "\n"
    (HERE / "comparison.md").write_text(text, encoding="utf-8")
    print(text)


if __name__ == "__main__":
    main()
