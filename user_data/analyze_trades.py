"""
Trade performance analysis script.

Run on the VPS:
  docker compose exec freqtrade python /freqtrade/user_data/analyze_trades.py

Or against a local copy of the DB:
  python user_data/analyze_trades.py
"""

import sqlite3
import os
import sys
from datetime import datetime

DB_PATHS = [
    "/freqtrade/user_data/tradesv3.sqlite",   # inside container
    "user_data/tradesv3.sqlite",              # local / relative
]

def find_db():
    for path in DB_PATHS:
        if os.path.exists(path):
            return path
    sys.exit("ERROR: tradesv3.sqlite not found. Check paths in DB_PATHS.")

def pct(n, total):
    return f"{n / total * 100:.1f}%" if total else "n/a"

def print_table(title, rows, headers):
    print(f"\n{title}")
    col_widths = [max(len(str(r[i])) for r in [headers] + rows) for i in range(len(headers))]
    fmt = "  ".join(f"{{:<{w}}}" for w in col_widths)
    print(fmt.format(*headers))
    print("  ".join("-" * w for w in col_widths))
    for row in rows:
        print(fmt.format(*row))

def main():
    db_path = find_db()
    conn = sqlite3.connect(db_path)
    conn.row_factory = sqlite3.Row
    cur = conn.cursor()

    # Only closed trades
    cur.execute("""
        SELECT id, pair, open_date, close_date,
               close_profit        AS profit_ratio,
               close_profit_abs    AS profit_abs,
               enter_tag, exit_reason, is_open
        FROM trades
        WHERE is_open = 0
        ORDER BY close_date
    """)
    trades = cur.fetchall()
    conn.close()

    if not trades:
        print("No closed trades found in the database yet.")
        return

    total = len(trades)
    wins  = sum(1 for t in trades if t["profit_ratio"] > 0)
    losses = total - wins

    total_profit = sum(t["profit_abs"] for t in trades)
    avg_win   = sum(t["profit_ratio"] for t in trades if t["profit_ratio"] > 0) / wins if wins else 0
    avg_loss  = sum(t["profit_ratio"] for t in trades if t["profit_ratio"] <= 0) / losses if losses else 0

    print("=" * 60)
    print("OVERALL PERFORMANCE")
    print("=" * 60)
    print(f"  Total trades : {total}")
    print(f"  Win rate     : {pct(wins, total)}  ({wins}W / {losses}L)")
    print(f"  Net profit   : {total_profit:+.2f} USDT")
    print(f"  Avg win      : {avg_win * 100:+.2f}%")
    print(f"  Avg loss     : {avg_loss * 100:+.2f}%")
    if avg_loss != 0:
        print(f"  Profit factor: {abs(avg_win * wins) / abs(avg_loss * losses):.2f}")

    # --- Drawdown and consecutive loss streak ---
    sorted_by_close = sorted(trades, key=lambda t: t["close_date"])
    cumulative, peak, max_dd, current_dd = 0.0, 0.0, 0.0, 0.0
    consec_streak, max_consec = 0, 0
    for t in sorted_by_close:
        cumulative += t["profit_abs"]
        peak = max(peak, cumulative)
        current_dd = cumulative - peak
        max_dd = min(max_dd, current_dd)
        if t["profit_ratio"] <= 0:
            consec_streak += 1
            max_consec = max(max_consec, consec_streak)
        else:
            consec_streak = 0
    wallet = 1000.0
    print(f"  Max drawdown : {max_dd:+.2f} USDT ({max_dd / wallet * 100:.2f}%)")
    print(f"  Current DD   : {current_dd:+.2f} USDT")
    print(f"  Max consec L : {max_consec}  (current streak: {consec_streak})")

    # --- Win rate by entry tag ---
    tags = {}
    for t in trades:
        tag = t["enter_tag"] or "unknown"
        tags.setdefault(tag, {"w": 0, "l": 0, "profit": 0.0})
        if t["profit_ratio"] > 0:
            tags[tag]["w"] += 1
        else:
            tags[tag]["l"] += 1
        tags[tag]["profit"] += t["profit_abs"]

    rows = []
    for tag, d in sorted(tags.items()):
        n = d["w"] + d["l"]
        rows.append((tag, n, d["w"], d["l"], pct(d["w"], n), f"{d['profit']:+.2f}"))
    print_table(
        "WIN RATE BY ENTRY TAG",
        rows,
        ["Tag", "Trades", "Wins", "Losses", "Win%", "Net USDT"],
    )

    # --- Win rate by pair ---
    pairs = {}
    for t in trades:
        p = t["pair"]
        pairs.setdefault(p, {"w": 0, "l": 0, "profit": 0.0})
        if t["profit_ratio"] > 0:
            pairs[p]["w"] += 1
        else:
            pairs[p]["l"] += 1
        pairs[p]["profit"] += t["profit_abs"]

    rows = []
    for pair, d in sorted(pairs.items()):
        n = d["w"] + d["l"]
        rows.append((pair, n, d["w"], d["l"], pct(d["w"], n), f"{d['profit']:+.2f}"))
    print_table(
        "WIN RATE BY PAIR",
        rows,
        ["Pair", "Trades", "Wins", "Losses", "Win%", "Net USDT"],
    )

    # --- Win rate by exit reason ---
    exits = {}
    for t in trades:
        reason = t["exit_reason"] or "unknown"
        exits.setdefault(reason, {"w": 0, "l": 0})
        if t["profit_ratio"] > 0:
            exits[reason]["w"] += 1
        else:
            exits[reason]["l"] += 1

    rows = []
    for reason, d in sorted(exits.items()):
        n = d["w"] + d["l"]
        rows.append((reason, n, d["w"], d["l"], pct(d["w"], n)))
    print_table(
        "EXIT REASON BREAKDOWN",
        rows,
        ["Exit reason", "Trades", "Wins", "Losses", "Win%"],
    )

    # --- Win rate by hour of entry ---
    hours = {}
    for t in trades:
        try:
            hour = datetime.fromisoformat(t["open_date"]).hour
        except Exception:
            continue
        hours.setdefault(hour, {"w": 0, "l": 0})
        if t["profit_ratio"] > 0:
            hours[hour]["w"] += 1
        else:
            hours[hour]["l"] += 1

    rows = []
    for hour in sorted(hours):
        d = hours[hour]
        n = d["w"] + d["l"]
        rows.append((f"{hour:02d}:00", n, d["w"], d["l"], pct(d["w"], n)))
    print_table(
        "WIN RATE BY ENTRY HOUR (UTC)",
        rows,
        ["Hour", "Trades", "Wins", "Losses", "Win%"],
    )

    # --- 10 worst trades ---
    worst = sorted(trades, key=lambda t: t["profit_ratio"])[:10]
    rows = [
        (
            t["pair"],
            t["enter_tag"] or "?",
            t["exit_reason"] or "?",
            f"{t['profit_ratio'] * 100:+.2f}%",
            f"{t['profit_abs']:+.2f}",
        )
        for t in worst
    ]
    print_table(
        "10 WORST TRADES",
        rows,
        ["Pair", "Entry tag", "Exit reason", "Return%", "USDT"],
    )

    print("\n" + "=" * 60)
    print("TARGET: 70% win rate")
    below_70 = [tag for tag, d in tags.items() if (d["w"] / (d["w"] + d["l"])) < 0.70]
    if below_70:
        print(f"Tags below 70%: {', '.join(below_70)}")
        print("Run hyperopt to tighten thresholds for those tags:")
        print("  docker compose run --rm freqtrade hyperopt \\")
        print("    --config user_data/config.json \\")
        print("    --strategy CombinedStrategy \\")
        print("    --hyperopt-loss WinRatioAndProfitRatioLoss \\")
        print("    --spaces buy --epochs 200")
    else:
        print("All entry tags are at or above 70%. Nice work.")
    print("=" * 60)

if __name__ == "__main__":
    main()
