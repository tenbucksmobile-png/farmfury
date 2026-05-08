# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

# CryptoAlgoBot

Automated crypto trading bot built on freqtrade. Runs 24/7 on a Hetzner VPS, trading BTC/USDT, ETH/USDT, and SOL/USDT on Binance spot market. Long only, spot trading.

## Stack

- **Framework:** freqtrade 2026.4 (open source trading bot)
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

Analyse live trade performance (run on VPS):
```bash
docker compose exec freqtrade python /freqtrade/user_data/analyze_trades.py
```

Backtesting (one-off container, does not interfere with live bot):
```bash
# Download OHLCV data first if not cached:
docker compose run --rm freqtrade download-data \
  --config user_data/config.json \
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

## Project Structure

```
crypto-algo/
├── docker-compose.yml                  # container definition — start/stop here
├── .env                                # API keys (gitignored — never commit)
├── .env.example                        # template for .env
└── user_data/
    ├── config.json                     # bot config: pairs, risk, stake sizing
    ├── analyze_trades.py               # win rate analysis script
    ├── tradesv3.sqlite                 # trade history database (auto-created)
    └── strategies/
        └── CombinedStrategy.py         # the trading strategy
```

## Strategy Architecture — CombinedStrategy

`user_data/strategies/CombinedStrategy.py` implements `IStrategy` (freqtrade interface version 3). Indicators are computed with **`pandas_ta`** (not `ta-lib`) — use `pandas_ta` for any new indicators.

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
- `trend_confluence`: price > EMA50, higher lows structure confirmed, volume > MA
- `mean_reversion`: ADX below threshold (ranging market only), RSI still falling, price > EMA100 (avoids downtrend)

**Exit conditions:**
- Trend: score ≤ 4 AND EMA9 < EMA21 AND MACD histogram < 0 (all three required)
- Reversion: price > BB middle AND RSI > 55 AND Stochastic K > 50
- Hard stoploss: -3%; trailing stop activates at +2%, trails at 1%; ROI ladder: 3% → 2% (30m) → 1.5% (1h) → 1% (2h)

**`startup_candle_count = 100`** — covers EMA100 (longest period used). If you add an indicator with a period longer than 100, increase this value accordingly.

### Hyperopt parameters

All key thresholds are exposed as `IntParameter` / `DecimalParameter` for automated tuning:

| Parameter | Default | Controls |
|---|---|---|
| `buy_adx_threshold` | 28 | ADX regime switch point |
| `buy_trend_score_min` | 8 | Minimum trend confluence to enter |
| `buy_reversion_score_min` | 5 | Minimum reversion confluence to enter |
| `buy_rsi_trend_min/max` | 48 / 72 | RSI zone for trend entries |
| `buy_rsi_reversion_max` | 30 | RSI oversold threshold |
| `sell_rsi_reversion_min` | 55 | RSI level to exit reversion trades |
| `sell_trend_score_exit` | 4 | Score floor that triggers trend exit |

### Performance analysis

`user_data/analyze_trades.py` queries the live SQLite DB and reports win rates broken down by entry tag, pair, exit reason, and hour of day. Run it on the VPS after accumulating trades, then share output to identify which mode needs tuning.

**Current target: 70% overall win rate.** As of the last analysis (15 trades, early data): trend_confluence 0%, mean_reversion 33% — confluence strategy deployed to address this.

## Configuration

Key settings in `user_data/config.json`:

| Setting | Value | Notes |
|---|---|---|
| `dry_run` | `true` | Set to `false` for live trading |
| `dry_run_wallet` | `1000` | Starting paper balance in USDT |
| `max_open_trades` | `3` | One per pair |
| `stake_amount` | `"unlimited"` | Divides balance by max_open_trades |
| `timeframe` | `5m` | Strategy candle interval |

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
