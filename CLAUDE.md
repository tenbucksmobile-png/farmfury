# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

# CryptoAlgoBot

Automated crypto trading bot built on freqtrade. Runs 24/7 on a Hetzner VPS, trading top-volume USDT pairs on Binance spot market. Long only, spot trading. Dynamic pairlist rotates to whichever pairs have the highest recent volume and activity.

## Stack

- **Framework:** freqtrade 2026.4 (open source trading bot), Docker image `freqtradeorg/freqtrade:stable`
- **Exchange:** Binance (spot, via CCXT)
- **Deployment:** Docker on Hetzner VPS (178.105.89.121)
- **Alerts:** Telegram bot
- **Dashboard:** FreqUI at http://178.105.89.121:8080

## Parallel Project

A second bot (`etf-algo`) trades US ETFs on Alpaca paper trading using the same confluence-scoring approach but via the `alpaca-py` SDK directly (not freqtrade). See `github.com/tenbucksmobile-png/etf-algo`.

## First-time Setup

```bash
cp .env.example .env   # fill in API keys, Telegram token, and dashboard password
docker compose up -d
```

Generate the JWT/WS tokens with `openssl rand -hex 32`.

## Common Commands

All freqtrade subcommands run inside Docker. The bot itself:

```bash
docker compose up -d          # start in background
docker compose down           # stop
docker compose restart        # apply strategy/config changes
docker compose logs -f        # tail live logs
docker compose ps             # check status
```

Validate strategy loads without errors:
```bash
docker compose run --rm freqtrade list-strategies --config user_data/config.json
```

Analyse trade performance (runs against live DB on VPS, or local `user_data/tradesv3.sqlite` if present):
```bash
docker compose exec freqtrade python /freqtrade/user_data/analyze_trades.py
# or locally (if tradesv3.sqlite is present):
python user_data/analyze_trades.py
```

Backtesting (one-off container, does not interfere with live bot):
```bash
# Download OHLCV data first — 5m, 1h, and 4h required for the MTF gates:
docker compose run --rm freqtrade download-data \
  --config user_data/config.json \
  --pairs SOL/USDT INJ/USDT NEAR/USDT TON/USDT WLD/USDT XLM/USDT FET/USDT SEI/USDT \
          TRX/USDT ZEC/USDT BNB/USDT LINK/USDT ONDO/USDT ALLO/USDT DASH/USDT \
          DOGE/USDT RENDER/USDT NIL/USDT ALT/USDT \
  --timeframes 5m 1h 4h \
  --timerange 20250101-20261231

# VolumePairList does not support backtesting — pass a second config that
# switches to StaticPairList. user_data/backtest_pairs.json does this.
# If the file doesn't exist, create it (see "backtest_pairs.json" section below).
docker compose run --rm freqtrade backtesting \
  --config user_data/config.json \
  --config user_data/backtest_pairs.json \
  --strategy CombinedStrategy \
  --timerange 20260101-20261231 \
  --enable-protections \
  --export trades
```

Hyperopt — auto-tune signal thresholds to maximise Sortino ratio:
```bash
# Use -j 1 (not --jobs) to stay within the VPS's 3.7 GB RAM (no swap).
# A short timerange keeps memory low; tune to the market regime you care about.
docker compose run --rm freqtrade hyperopt \
  --config user_data/config.json \
  --config user_data/backtest_pairs.json \
  --strategy CombinedStrategy \
  --hyperopt-loss SortinoHyperOptLoss \
  --spaces buy sell --epochs 150 \
  --timerange 20260501-20260529 \
  --enable-protections \
  -j 1
```

### backtest_pairs.json

`user_data/backtest_pairs.json` is not committed — create it when needed. It overrides `config.json` to swap `VolumePairList` for `StaticPairList` (VolumePairList hits the exchange API and cannot run offline):

```json
{
    "pairlists": [
        {"method": "StaticPairList"}
    ],
    "exchange": {
        "pair_whitelist": [
            "SOL/USDT", "INJ/USDT", "NEAR/USDT", "TON/USDT", "WLD/USDT",
            "XLM/USDT", "FET/USDT", "SEI/USDT", "TRX/USDT", "ZEC/USDT",
            "BNB/USDT", "LINK/USDT", "ONDO/USDT", "ALLO/USDT", "DASH/USDT",
            "DOGE/USDT", "RENDER/USDT", "NIL/USDT", "ALT/USDT"
        ]
    }
}
```

Update `pair_whitelist` here to match whatever pairs you downloaded data for.

## Architecture

`docker-compose.yml` mounts `./user_data` → `/freqtrade/user_data` inside the container and hardcodes `--strategy CombinedStrategy`. Editing any file under `user_data/` and running `docker compose restart` is sufficient to apply changes — no image rebuild needed.

The bot uses a **dynamic pairlist** that refreshes every 30 minutes, passing pairs through a chain: VolumePairList (top 30 by 24h quoteVolume, min 10M USDT) → AgeFilter (≥60 days listed) → SpreadFilter (≤0.2% spread) → RangeStabilityFilter (≥3% rate-of-change in 24h) → PerformanceFilter (3-day lookback, min 2 trades). Stablecoins (USDC, BUSD, TUSD, FDUSD, DAI, USDP, USDD), leveraged tokens (`.*UP`, `.*DOWN`, `.*BULL`, `.*BEAR`), and large-caps that don't respond to TA confluence (BTC, ETH, SUI, FIL) are blacklisted in `config.json`.

`process_only_new_candles = True` means `populate_indicators` / `populate_entry_trend` execute once per 5m candle close, not on every tick. `restart: unless-stopped` means the container recovers automatically from crashes and VPS reboots.

To add a static pair: change `pairlists` in `config.json` back to `StaticPairList` and list pairs in `pair_whitelist`. No strategy changes required.

## Strategy Architecture — CombinedStrategy

`user_data/strategies/CombinedStrategy.py` implements `IStrategy` (freqtrade interface version 3). Indicators are computed with **`pandas_ta`** (not `ta-lib`) — use `pandas_ta` for any new indicators.

### Exit architecture

`use_exit_signal = False`, so `populate_exit_trend` is intentionally empty and never called by freqtrade. **All signal-driven exits live in `custom_exit`**, which is tag-aware:

- `trend_confluence` entries exit via `"trend_exit"` when score ≤ threshold AND EMA9 < EMA21 AND MACD histogram < 0 (all three required).
- `mean_reversion` entries exit via `"reversion_exit"` when price > BB middle AND RSI > threshold AND Stochastic K > 50.
- Any trade open >24h with < 0.5% profit exits via `"max_hold_exit"` to free the slot for better opportunities.

Other exit paths: ROI ladder, hard stoploss (−2.0% per JSON), trailing stop (activates at +5%, trails at 1%), and circuit-breaker protections. Do not add logic to `populate_exit_trend` — it will never fire.

`confirm_trade_entry` enforces pair cooldowns: no re-entry on a pair for 4h after a `stop_loss`, or 2h after a `trailing_stop_loss`. Uses `Trade.get_trades_proxy()` which works in both live and backtesting, so cooldowns are now simulated in backtesting too.

`ignore_roi_if_entry_signal = True` — the ROI ladder is suppressed while the entry signal remains active, allowing winners to run. ROI only exits a trade when the entry conditions are no longer met.

### pandas_ta column names

Some `pandas_ta` functions return **stable** column names regardless of parameters (use direct lookup):
- `ta.macd()` → `MACDh_12_26_9`
- `ta.adx()` → `ADX_14`, `DMP_14`, `DMN_14`
- `ta.bbands()` → `BBU_20_2.0`, `BBM_20_2.0`, `BBL_20_2.0`

Others return **parameter-suffixed** column names that must be discovered at runtime:
- `ta.supertrend()` → `SUPERTd_7_3.0` (direction column)
- `ta.ichimoku()` → `ITS_*` (Tenkan), `IKS_*` (Kijun)
- `ta.stoch()` → `STOCHk_14_3_3`, `STOCHd_14_3_3`

Runtime lookup pattern used throughout `populate_indicators`:
```python
st = ta.supertrend(...)
col = next((c for c in st.columns if c.startswith("SUPERTd")), None)
```

Follow this pattern when adding any `pandas_ta` function that returns multiple named columns. The Ichimoku call also has a try/except because some versions return a tuple instead of a DataFrame.

### Informative pair (multi-timeframe) pattern

`informative_pairs()` returns `(pair, "1h")` and `(pair, "4h")` for every pair in the whitelist.

**1h gate:** In `populate_indicators`, the 1h dataframe is merged via `merge_informative_pair(dataframe, informative_1h, "5m", "1h", ffill=True)`. This appends a `_1h` suffix — so `informative_1h["trend_up"]` becomes `dataframe["trend_up_1h"]`. All trend entries require `trend_up_1h = True` (EMA9 > EMA21 on 1h) to prevent buying 5m micro-rallies counter to the hourly direction.

**4h macro gate:** The 4h dataframe computes `ema50_4h` and `ema200_4h`. `macro_bull_4h = ema50_4h > ema200_4h`. Both `trend_confluence` and `mean_reversion` entries require `macro_bull_4h = True` — no new positions are opened when the 4h trend is bearish. This prevents entering during macro downtrends (e.g., Jan–Apr 2026, −23.78%).

If you add more informative indicators, follow the same merge pattern and reference them with the appropriate `_1h` or `_4h` suffix.

### Confluence scoring system

Rather than a binary regime switch, the strategy scores independent signals from multiple TA categories and only enters when enough agree simultaneously.

**Trend score (0–12)** — requires ≥ 12 to enter `trend_confluence` (all signals):
1. EMA9 > EMA21
2. EMA21 > EMA50
3. Price above rolling 20-period VWAP
4. Supertrend bullish (direction = 1)
5. Ichimoku Tenkan > Kijun
6. MACD histogram positive
7. MACD histogram accelerating
8. RSI in bullish zone (50–72)
9. Stochastic K > D, not overbought (< 80)
10. ADX > threshold AND DM+ > DM-
11. OBV above its 10-period MA
12. Chaikin Money Flow > 0

**Reversion score (0–8)** — requires ≥ 7 to enter `mean_reversion`:
1. RSI oversold (< 25)
2. Price below Bollinger lower band
3. Stochastic K < 20
4. Williams %R < -80
5. CCI < -100
6. MFI < 25
7. Volume spike > 1.5× average (capitulation)
8. Price near Fibonacci support (38.2%, 50%, or 61.8% of 50-bar swing)

**Additional entry filters:**
- `trend_confluence`: price > EMA50, higher lows structure confirmed, volume > MA, **1h EMA9 > EMA21**, **4h EMA50 > EMA200**, **score ≥ threshold for 2 consecutive candles** (prevents spike-and-collapse entries), **RSI rising** (not at momentum peak), **session gate 08:xx, 15:xx, 17:xx UTC only** (refined from 110 live trades: 08:xx 62.5% WR, 15:xx 77.8%, 17:xx 75.0%; dropped 09:xx 0%, 14:xx 45.5%, 16:xx 25%)
- `mean_reversion`: ADX below threshold (ranging market only), RSI still falling, price > EMA100, **4h EMA50 > EMA200**, session gate 08:xx, 15:xx, 17:xx UTC. Currently disabled — `buy_reversion_score_min = 8` exceeds the maximum reversion score of 8.

**`startup_candle_count = 100`** — covers EMA100 (longest period used). If you add an indicator with a period longer than 100, increase this value accordingly.

VWAP is computed as a rolling 20-period average (not session-anchored) to remain stable for 24/7 crypto markets that have no daily open.

### Hyperopt parameters

All key thresholds are exposed as `IntParameter` / `DecimalParameter` for automated tuning:

| Parameter | Current | Range | Controls |
|---|---|---|---|
| `buy_adx_threshold` | 39 | 25–40 | ADX regime switch — trend requires ADX > 39; reversion requires ADX < 39 |
| `buy_trend_score_min` | 12 | 6–12 | Minimum trend confluence to enter (12 = all signals required) |
| `buy_reversion_score_min` | 8 | 4–7 | Minimum reversion confluence to enter (8 = effectively disabled — above max score of 8) |
| `buy_rsi_trend_min` | 50 | 44–58 | RSI lower bound for trend entries |
| `buy_rsi_trend_max` | 72 | 65–78 | RSI upper bound for trend entries |
| `buy_rsi_reversion_max` | 25 | 20–35 | RSI oversold threshold |
| `sell_rsi_reversion_min` | 65 | 50–65 | RSI level to exit reversion trades |
| `sell_trend_score_exit` | 4 | 2–6 | Score floor that triggers trend exit |

Values tuned via hyperopt over the May 2026 bull period (epoch 103/150: 75% win rate, 0.08% max drawdown, Sortino 107, 8 trades). Small sample — treat as directional guidance and re-run hyperopt over a longer window once more data accumulates.

**`CombinedStrategy.json`** — freqtrade auto-loads this file on startup, overriding the Python `default=` values. The "Current" values in the table reflect the JSON, not the Python defaults. The JSON also carries `roi`, `stoploss`, and `trailing` blocks which override the strategy class values; keep these in sync when editing either file. After running hyperopt, inspect results before applying — if the best epoch is still net-negative, do not restart the live bot with those parameters.

**Important:** The Python class `stoploss`, `minimal_roi`, and `trailing_stop*` values are never used at runtime — the JSON block always overrides them on startup. Treat the JSON as the single source of truth for those parameters. When running backtesting or hyperopt without the JSON being loaded, Python `default=` values apply (e.g. `buy_trend_score_min` defaults to `12`, matching the JSON, but `stoploss` defaults to `-0.05` instead of the live `-0.025`).

### Performance analysis

`user_data/analyze_trades.py` queries the SQLite DB and reports:
- Win rates by entry tag, pair, exit reason, and hour of day
- Max drawdown and current drawdown (USDT + % of 1000 USDT baseline wallet)
- Max consecutive losses and current loss streak
- 10 worst trades

It probes both `/freqtrade/user_data/tradesv3.sqlite` (container path) and `user_data/tradesv3.sqlite` (local), so it works in both environments.

**Current target: 70% overall win rate.**

## Protections (live circuit breakers)

Defined as a `protections` property in `CombinedStrategy.py` (not in config.json — that placement is deprecated in freqtrade 2026.x). Add `--enable-protections` to backtest/hyperopt commands to simulate them:

| Protection | Trigger | Pause duration |
|---|---|---|
| `MaxDrawdown` | 10% drawdown in any 24h window (288 × 5m candles) | 24h |
| `StoplossGuard` | 4 losing trades in any 6h window (72 × 5m candles) | 5h |

After a protection triggers, freqtrade holds open positions but stops new entries. Resume automatically when the pause expires, or send `/start` via Telegram to override early.

## Configuration

Key settings in `user_data/config.json`:

| Setting | Value | Notes |
|---|---|---|
| `dry_run` | `true` | Set to `false` for live trading |
| `dry_run_wallet` | `1000` | Starting paper balance in USDT |
| `max_open_trades` | `3` | Reduced from 5 — limits simultaneous stop-loss exposure in bear market |
| `stake_amount` | `"unlimited"` | Divides balance by max_open_trades |
| `tradable_balance_ratio` | `0.99` | 99% of wallet committed; 1% held as buffer |
| `timeframe` | `5m` | Strategy candle interval |
| `unfilledtimeout` | `10 min` | Cancels unfilled entry/exit orders after 10 min |
| `force_entry_enable` | `false` | `/forcebuy` Telegram command is disabled |
| `initial_state` | `"running"` | Bot starts trading immediately on container launch — no manual `/start` needed |
| `cancel_open_orders_on_exit` | `true` | Cancels unfilled entry/exit orders when bot stops cleanly |

Most config.json changes apply without a full restart — send `/reload_config` via Telegram.

## Environment Variables

Secrets are injected via `.env` (copy from `.env.example`) using freqtrade's `FREQTRADE__` double-underscore prefix convention:

```
FREQTRADE__EXCHANGE__KEY
FREQTRADE__EXCHANGE__SECRET
FREQTRADE__TELEGRAM__TOKEN
FREQTRADE__TELEGRAM__CHAT_ID
FREQTRADE__API_SERVER__USERNAME
FREQTRADE__API_SERVER__PASSWORD
FREQTRADE__API_SERVER__JWT_SECRET_KEY
FREQTRADE__API_SERVER__WS_TOKEN
```

## Telegram Notifications

Per-trade entry/exit notifications are **disabled** — the bot sends only:
- Bot startup, status changes, and warnings
- Protection triggers (MaxDrawdown / StoplossGuard fired)
- **Weekly report** — every Friday at 15:00 UTC, covering the preceding Saturday–Friday window

The weekly report is generated in `_maybe_send_weekly_report()`, called from `bot_loop_start()`. A marker file at `/freqtrade/user_data/.last_weekly_report` prevents duplicate sends. To change the send time, edit the `ct.hour != 15` check in the strategy.

## Telegram Commands

```
/balance       — wallet balance
/status        — open trades
/profit        — P&L summary
/trades        — trade history
/stop          — pause bot (keeps open positions)
/start         — resume bot
/stopbuy       — stop new entries, manage existing
/forcesell all — emergency close all positions
/reload_config — apply config changes without restart
```

## Going Live

When paper trading is consistently profitable at ≥ 70% win rate (recommend 30 days minimum):

1. Edit `user_data/config.json` — change `"dry_run": true` to `"dry_run": false`
2. Run `docker compose restart`

## Deploying Changes to VPS

No git remote is configured on the VPS — deploy files directly via scp:

```bash
# From local machine (Windows) — deploy whichever files changed:
scp "C:\Users\Personel\Desktop\crypto-algo\user_data\strategies\CombinedStrategy.py"   root@178.105.89.121:/root/crypto-algo/user_data/strategies/CombinedStrategy.py
scp "C:\Users\Personel\Desktop\crypto-algo\user_data\strategies\CombinedStrategy.json" root@178.105.89.121:/root/crypto-algo/user_data/strategies/CombinedStrategy.json
scp "C:\Users\Personel\Desktop\crypto-algo\user_data\config.json"                      root@178.105.89.121:/root/crypto-algo/user_data/config.json
scp "C:\Users\Personel\Desktop\crypto-algo\user_data\analyze_trades.py"                root@178.105.89.121:/root/crypto-algo/user_data/analyze_trades.py
```

Then restart on the VPS:
```bash
cd /root/crypto-algo && docker compose restart
```

## VPS Access

```bash
ssh root@178.105.89.121
cd /root/crypto-algo      # crypto bot
cd /root/etf-algo         # ETF bot
```
