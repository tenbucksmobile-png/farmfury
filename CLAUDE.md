# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

# CryptoAlgoBot

Automated crypto trading bot built on freqtrade. Runs 24/7 on a Hetzner VPS, trading BTC/USDT, ETH/USDT, and SOL/USDT on Binance spot market. Long only, spot trading.

## Stack

- **Framework:** freqtrade 2026.4 (open source trading bot), Docker image `freqtradeorg/freqtrade:stable`
- **Exchange:** Binance (spot, via CCXT)
- **Deployment:** Docker on Hetzner VPS (178.105.89.121)
- **Alerts:** Telegram bot
- **Dashboard:** FreqUI at http://178.105.89.121:8080

## Common Commands

All freqtrade subcommands run inside Docker. The bot itself:

```bash
docker compose up -d          # start in background
docker compose down           # stop
docker compose restart        # apply strategy/config changes
docker compose logs -f        # tail live logs
docker compose ps             # check status
```

Analyse trade performance (runs against live DB on VPS, or local `user_data/tradesv3.sqlite` if present):
```bash
docker compose exec freqtrade python /freqtrade/user_data/analyze_trades.py
# or locally (if tradesv3.sqlite is present):
python user_data/analyze_trades.py
```

Backtesting (one-off container, does not interfere with live bot):
```bash
# Download OHLCV data first if not cached (1h required for the MTF gate):
docker compose run --rm freqtrade download-data \
  --config user_data/config.json \
  --timeframes 5m 1h \
  --timerange 20240101-20241231

docker compose run --rm freqtrade backtesting \
  --config user_data/config.json \
  --strategy CombinedStrategy \
  --timerange 20240101-20241231
```

Hyperopt — auto-tune signal thresholds to maximise win rate:
```bash
docker compose run --rm freqtrade hyperopt \
  --config user_data/config.json \
  --strategy CombinedStrategy \
  --hyperopt-loss WinRatioAndProfitRatioLoss \
  --spaces buy sell --epochs 300 \
  --timerange 20240101-20241231
```

Validate strategy loads without errors:
```bash
docker compose run --rm freqtrade list-strategies --config user_data/config.json
```

## Architecture

`docker-compose.yml` mounts `./user_data` → `/freqtrade/user_data` inside the container and hardcodes `--strategy CombinedStrategy`. Editing any file under `user_data/` and running `docker compose restart` is sufficient to apply changes — no image rebuild needed.

The bot uses `StaticPairList`, so the traded pairs (BTC/USDT, ETH/USDT, SOL/USDT) are fixed in `config.json` and not dynamically filtered. `process_only_new_candles = True` means `populate_indicators` / `populate_entry_trend` / `populate_exit_trend` execute once per 5m candle close, not on every tick. `cancel_open_orders_on_exit: true` cancels unfilled orders when the bot stops; `initial_state: running` means it resumes trading immediately on container start. `restart: unless-stopped` in `docker-compose.yml` means the container recovers automatically from crashes and VPS reboots — no cron job needed.

To add a new trading pair: add it to `pair_whitelist` in `config.json` and increase `max_open_trades` by 1. No strategy changes required.

## Strategy Architecture — CombinedStrategy

`user_data/strategies/CombinedStrategy.py` implements `IStrategy` (freqtrade interface version 3). Indicators are computed with **`pandas_ta`** (not `ta-lib`) — use `pandas_ta` for any new indicators.

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

`informative_pairs()` returns `(pair, "1h")` for every pair in the whitelist. In `populate_indicators`, the 1h dataframe is fetched, indicators computed, then merged via `merge_informative_pair(dataframe, informative_1h, "5m", "1h", ffill=True)`. This appends a `_1h` suffix to every column — so `informative_1h["trend_up"]` becomes `dataframe["trend_up_1h"]` after the merge. If you add more 1h indicators, follow the same pattern and reference them with the `_1h` suffix in entry/exit logic.

### Confluence scoring system

Rather than a binary regime switch, the strategy scores independent signals from multiple TA categories and only enters when enough agree simultaneously.

**Trend score (0–12)** — requires ≥ 8 to enter `trend_confluence`:
1. EMA9 > EMA21
2. EMA21 > EMA50
3. Price above rolling 20-period VWAP
4. Supertrend bullish (direction = 1)
5. Ichimoku Tenkan > Kijun
6. MACD histogram positive
7. MACD histogram accelerating
8. RSI in bullish zone (default 48–72)
9. Stochastic K > D, not overbought (< 80)
10. ADX > threshold AND DM+ > DM-
11. OBV above its 10-period MA
12. Chaikin Money Flow > 0

**Reversion score (0–8)** — requires ≥ 5 to enter `mean_reversion`:
1. RSI oversold (default < 30)
2. Price below Bollinger lower band
3. Stochastic K < 20
4. Williams %R < -80
5. CCI < -100
6. MFI < 25
7. Volume spike > 1.5× average (capitulation)
8. Price near Fibonacci support (38.2%, 50%, or 61.8% of 50-bar swing)

**Additional entry filters:**
- `trend_confluence`: price > EMA50, higher lows structure confirmed, volume > MA, **1h EMA9 > EMA21** (hourly trend gate — prevents buying 5m micro-rallies counter to the hourly direction)
- `mean_reversion`: ADX below threshold (ranging market only), RSI still falling, price > EMA100 (avoids downtrend)

**Exit conditions:**
- Trend: score ≤ 4 AND EMA9 < EMA21 AND MACD histogram < 0 (all three required)
- Reversion: price > BB middle AND RSI > 55 AND Stochastic K > 50
- Hard stoploss: -3%; trailing stop activates at +2%, trails at 1%; ROI ladder: 3% → 2% (30m) → 1.5% (1h) → 1% (2h)

**`startup_candle_count = 100`** — covers EMA100 (longest period used). If you add an indicator with a period longer than 100, increase this value accordingly.

VWAP is computed as a rolling 20-period average (not session-anchored) to remain stable for 24/7 crypto markets that have no daily open.

### Hyperopt parameters

All key thresholds are exposed as `IntParameter` / `DecimalParameter` for automated tuning:

| Parameter | Default | Controls |
|---|---|---|
| `buy_adx_threshold` | 28 | ADX regime switch point |
| `buy_trend_score_min` | 10 | Minimum trend confluence to enter |
| `buy_reversion_score_min` | 5 | Minimum reversion confluence to enter |
| `buy_rsi_trend_min/max` | 48 / 72 | RSI zone for trend entries |
| `buy_rsi_reversion_max` | 30 | RSI oversold threshold |
| `sell_rsi_reversion_min` | 55 | RSI level to exit reversion trades |
| `sell_trend_score_exit` | 4 | Score floor that triggers trend exit |

### Performance analysis

`user_data/analyze_trades.py` queries the SQLite DB and reports:
- Win rates by entry tag, pair, exit reason, and hour of day
- Max drawdown and current drawdown (USDT + % of 1000 USDT baseline wallet)
- Max consecutive losses and current loss streak
- 10 worst trades

It probes both `/freqtrade/user_data/tradesv3.sqlite` (container path) and `user_data/tradesv3.sqlite` (local), so it works in both environments.

**Current target: 70% overall win rate.**

## Protections (live circuit breakers)

Defined as a `protections` property in `CombinedStrategy.py` (not in config.json — that placement is deprecated in freqtrade 2026.x). Active during live/dry-run trading; add `--enable-protections` to backtest commands to simulate them:

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
| `max_open_trades` | `3` | One per pair |
| `stake_amount` | `"unlimited"` | Divides balance by max_open_trades |
| `tradable_balance_ratio` | `0.99` | 99% of wallet committed; 1% held as buffer |
| `timeframe` | `5m` | Strategy candle interval |
| `unfilledtimeout` | `10 min` | Cancels unfilled entry/exit orders after 10 min |
| `force_entry_enable` | `false` | `/forcebuy` Telegram command is disabled; set to `true` to enable manual entries |

Most config.json changes apply without a full restart — send `/reload_config` via Telegram.

## Environment Variables

Secrets are injected via `.env` using freqtrade's `FREQTRADE__` double-underscore prefix convention (maps to nested JSON keys):

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

The weekly report is generated in `_maybe_send_weekly_report()`, called from `bot_loop_start()` (which freqtrade calls every ~5 seconds via `process_throttle_secs`). The method itself gates on day/hour/minute so it only fires once per Friday. It shows total trades, win rate, per-pair breakdown (trade count, % share, win rate, net USDT), cumulative P&L, and estimated balance. A marker file at `/freqtrade/user_data/.last_weekly_report` prevents duplicate sends within the 5-minute fire window. To change the send time, edit the `ct.hour != 15` check in the strategy.

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
# From local machine (Windows):
scp "C:\Users\Personel\Desktop\crypto-algo\user_data\strategies\CombinedStrategy.py" root@178.105.89.121:/root/crypto-algo/user_data/strategies/CombinedStrategy.py
scp "C:\Users\Personel\Desktop\crypto-algo\user_data\analyze_trades.py" root@178.105.89.121:/root/crypto-algo/user_data/analyze_trades.py
```

Then restart on the VPS:
```bash
cd /root/crypto-algo && docker compose restart
```

## VPS Access

```bash
ssh root@178.105.89.121
cd /root/crypto-algo
```
