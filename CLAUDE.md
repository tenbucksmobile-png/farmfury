# CryptoAlgoBot

Automated crypto trading bot built on freqtrade. Runs 24/7 on a Hetzner VPS, trading BTC/USDT, ETH/USDT, and SOL/USDT on Binance spot market.

## Stack

- **Framework:** freqtrade 2026.4 (open source trading bot)
- **Exchange:** Binance (spot, via CCXT)
- **Deployment:** Docker on Hetzner VPS (178.105.89.121)
- **Alerts:** Telegram bot
- **Dashboard:** FreqUI at http://178.105.89.121:8080

## Project Structure

```
crypto-algo/
├── docker-compose.yml                  # container definition — start/stop here
├── .env                                # API keys (gitignored — never commit)
├── .env.example                        # template for .env
└── user_data/
    ├── config.json                     # bot config: pairs, risk, stake sizing
    ├── tradesv3.sqlite                 # trade history database (auto-created)
    └── strategies/
        └── CombinedStrategy.py         # the trading strategy
```

## Strategy — CombinedStrategy

Regime-switching strategy on 5-minute candles.

**Regime detection:** ADX indicator
- ADX > 25 → trending market → trend following mode
- ADX ≤ 25 → ranging market → mean reversion mode

**Trend following signals:**
- Entry: EMA9 > EMA21, price > EMA50, MACD histogram positive, RSI 50–75, above-average volume
- Exit: EMA9 crosses below EMA21 OR MACD histogram turns negative

**Mean reversion signals:**
- Entry: price below Bollinger lower band, RSI < 35, adequate volume
- Exit: price returns to Bollinger middle band, RSI > 50

**Risk management:**
- Hard stoploss: -3%
- Trailing stop: activates at +2% profit, trails at 1%
- ROI targets: 3% (instant) → 2% (30min) → 1.5% (1h) → 1% (2h)
- Max open trades: 3 (one per pair)
- Stake per trade: 1/3 of available balance (auto-compounding)

## Configuration

Key settings in `user_data/config.json`:

| Setting | Value | Notes |
|---|---|---|
| `dry_run` | `true` | Set to `false` for live trading |
| `dry_run_wallet` | `1000` | Starting paper balance in USDT |
| `max_open_trades` | `3` | One per pair |
| `stake_amount` | `"unlimited"` | Divides balance by max_open_trades |
| `timeframe` | `5m` | Strategy candle interval |

## Environment Variables

All secrets are injected via `.env` using freqtrade's `FREQTRADE__` prefix convention:

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

## Running the Bot

```bash
# Start (background)
docker compose up -d

# Stop
docker compose down

# Restart after config changes
docker compose restart

# View live logs
docker compose logs -f

# Check status
docker compose ps
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

When paper trading is consistently profitable (recommend 30 days minimum):

1. Edit `user_data/config.json`
2. Change `"dry_run": true` to `"dry_run": false`
3. Run `docker compose restart`

The bot will now trade with real Binance balance.

## VPS Access

```bash
ssh root@178.105.89.121
cd /root/crypto-algo
```
