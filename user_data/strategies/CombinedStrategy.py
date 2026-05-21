from freqtrade.strategy import IStrategy, IntParameter, DecimalParameter, merge_informative_pair
from freqtrade.persistence import Trade
from pandas import DataFrame
from pathlib import Path
from datetime import datetime, timezone, timedelta
import pandas_ta as ta


class CombinedStrategy(IStrategy):
    """
    Comprehensive TA confluence strategy — long only, spot trading.

    Scores 12 independent trend/momentum signals and 8 mean-reversion signals
    drawn from trend, momentum, volume, volatility, and chart-pattern categories.
    Enters only when enough signals agree simultaneously (confluence threshold).

    A 1h EMA9 > EMA21 gate is required for all trend_confluence entries to avoid
    buying 5m micro-rallies that are counter to the hourly trend direction.

    Entry tags:
      trend_confluence — broad multi-indicator agreement in a trending market
      mean_reversion   — deep oversold confluence in a ranging market
    """

    INTERFACE_VERSION = 3
    timeframe = "5m"

    minimal_roi = {
        "0":   0.08,
        "60":  0.05,
        "120": 0.03,
        "240": 0.01,
    }

    stoploss = -0.05

    trailing_stop = True
    trailing_stop_positive = 0.015
    trailing_stop_positive_offset = 0.03
    trailing_only_offset_is_reached = True

    process_only_new_candles = True
    use_exit_signal = False
    exit_profit_only = False
    ignore_roi_if_entry_signal = False

    # Covers EMA100 (longest period used) + Ichimoku + all shorter indicators
    startup_candle_count = 100

    # ── Live circuit breakers ─────────────────────────────────────────────────
    @property
    def protections(self):
        return [
            {
                # Pause 24h if account drops 10% in any rolling 24h window
                "method": "MaxDrawdown",
                "lookback_period_candles": 288,
                "trade_limit": 1,
                "stop_duration_candles": 288,
                "max_allowed_drawdown": 0.10,
            },
            {
                # Pause 5h if 4+ losing trades occur in any 6h window
                "method": "StoplossGuard",
                "lookback_period_candles": 72,
                "trade_limit": 4,
                "stop_duration_candles": 60,
                "only_per_side": False,
            },
        ]

    # ── Hyperopt search spaces ────────────────────────────────────────────────
    buy_adx_threshold       = IntParameter(25, 40, default=26, space="buy",  optimize=True)
    buy_trend_score_min     = IntParameter(6,  12, default=12, space="buy",  optimize=True)
    buy_reversion_score_min = IntParameter(4,  7,  default=6,  space="buy",  optimize=True)
    buy_rsi_trend_min       = IntParameter(44, 58, default=54, space="buy",  optimize=True)
    buy_rsi_trend_max       = IntParameter(65, 78, default=70, space="buy",  optimize=True)
    buy_rsi_reversion_max   = IntParameter(20, 35, default=23, space="buy",  optimize=True)
    sell_rsi_reversion_min  = IntParameter(50, 65, default=58, space="sell", optimize=True)
    sell_trend_score_exit   = IntParameter(2,  6,  default=4,  space="sell", optimize=True)

    def informative_pairs(self):
        pairs = self.dp.current_whitelist()
        return [(pair, "1h") for pair in pairs] + [(pair, "4h") for pair in pairs]

    def populate_indicators(self, dataframe: DataFrame, metadata: dict) -> DataFrame:

        # ── Trend indicators ──────────────────────────────────────────────────
        dataframe["ema9"]   = ta.ema(dataframe["close"], length=9)
        dataframe["ema21"]  = ta.ema(dataframe["close"], length=21)
        dataframe["ema50"]  = ta.ema(dataframe["close"], length=50)
        dataframe["ema100"] = ta.ema(dataframe["close"], length=100)

        # Supertrend (direction: 1 = bullish, -1 = bearish)
        st = ta.supertrend(dataframe["high"], dataframe["low"], dataframe["close"], length=7, multiplier=3.0)
        supert_dir_col = next((c for c in st.columns if c.startswith("SUPERTd")), None)
        dataframe["supertrend_dir"] = st[supert_dir_col] if supert_dir_col else 0

        # Ichimoku — Tenkan/Kijun cross as signal; defensive against version differences
        try:
            ichi_result = ta.ichimoku(dataframe["high"], dataframe["low"], dataframe["close"])
            ichi_df = ichi_result[0] if isinstance(ichi_result, tuple) else ichi_result
            tenkan_col = next((c for c in ichi_df.columns if c.startswith("ITS")), None)
            kijun_col  = next((c for c in ichi_df.columns if c.startswith("IKS")), None)
            dataframe["ichi_tenkan"] = ichi_df[tenkan_col] if tenkan_col else dataframe["ema9"]
            dataframe["ichi_kijun"]  = ichi_df[kijun_col]  if kijun_col  else dataframe["ema21"]
        except Exception:
            dataframe["ichi_tenkan"] = dataframe["ema9"]
            dataframe["ichi_kijun"]  = dataframe["ema21"]

        # Rolling 20-period VWAP — stable for 24/7 crypto (avoids daily-anchor resets)
        typical_price = (dataframe["high"] + dataframe["low"] + dataframe["close"]) / 3
        dataframe["vwap"] = (
            (typical_price * dataframe["volume"]).rolling(20).sum()
            / dataframe["volume"].rolling(20).sum()
        )

        # ── Momentum indicators ───────────────────────────────────────────────
        adx_data = ta.adx(dataframe["high"], dataframe["low"], dataframe["close"], length=14)
        dataframe["adx"] = adx_data["ADX_14"]
        dataframe["dmp"] = adx_data["DMP_14"]
        dataframe["dmn"] = adx_data["DMN_14"]

        macd = ta.macd(dataframe["close"])
        dataframe["macd_hist"] = macd["MACDh_12_26_9"]

        dataframe["rsi"] = ta.rsi(dataframe["close"], length=14)

        stoch = ta.stoch(dataframe["high"], dataframe["low"], dataframe["close"])
        stoch_k_col = next((c for c in stoch.columns if c.startswith("STOCHk")), None)
        stoch_d_col = next((c for c in stoch.columns if c.startswith("STOCHd")), None)
        dataframe["stoch_k"] = stoch[stoch_k_col] if stoch_k_col else 50
        dataframe["stoch_d"] = stoch[stoch_d_col] if stoch_d_col else 50

        dataframe["cci"]   = ta.cci(dataframe["high"], dataframe["low"], dataframe["close"], length=14)
        dataframe["willr"] = ta.willr(dataframe["high"], dataframe["low"], dataframe["close"], length=14)

        # ── Volume indicators ─────────────────────────────────────────────────
        dataframe["volume_ma"] = ta.sma(dataframe["volume"], length=20)
        dataframe["obv"]       = ta.obv(dataframe["close"], dataframe["volume"])
        dataframe["obv_ma"]    = ta.sma(dataframe["obv"], length=10)
        dataframe["cmf"]       = ta.cmf(
            dataframe["high"], dataframe["low"], dataframe["close"], dataframe["volume"], length=20
        )
        dataframe["mfi"] = ta.mfi(
            dataframe["high"], dataframe["low"], dataframe["close"], dataframe["volume"], length=14
        )

        # ── Volatility / Bollinger Bands ──────────────────────────────────────
        bb = ta.bbands(dataframe["close"], length=20, std=2.0)
        dataframe["bb_upper"]  = bb["BBU_20_2.0"]
        dataframe["bb_middle"] = bb["BBM_20_2.0"]
        dataframe["bb_lower"]  = bb["BBL_20_2.0"]
        dataframe["bb_width"]  = (dataframe["bb_upper"] - dataframe["bb_lower"]) / dataframe["bb_middle"]

        # ── Chart patterns ────────────────────────────────────────────────────
        # Bollinger Band squeeze: width near its rolling 20-bar minimum (volatility compression)
        dataframe["bb_squeeze"] = (
            dataframe["bb_width"] < dataframe["bb_width"].rolling(20).min().shift(1) * 1.05
        )

        # Higher lows: each 10-bar pivot low is above the previous (uptrend structure)
        dataframe["higher_lows"] = (
            (dataframe["low"] > dataframe["low"].shift(10)) &
            (dataframe["low"].shift(10) > dataframe["low"].shift(20))
        )

        # Fibonacci retracement support: price within 0.5% of 38.2%, 50%, or 61.8% of 50-bar swing
        swing_high  = dataframe["high"].rolling(50).max()
        swing_low   = dataframe["low"].rolling(50).min()
        swing_range = swing_high - swing_low
        dataframe["near_fib_support"] = (
            (abs(dataframe["close"] - (swing_high - swing_range * 0.382)) / dataframe["close"] < 0.005) |
            (abs(dataframe["close"] - (swing_high - swing_range * 0.500)) / dataframe["close"] < 0.005) |
            (abs(dataframe["close"] - (swing_high - swing_range * 0.618)) / dataframe["close"] < 0.005)
        )

        # ── Confluence scores ─────────────────────────────────────────────────
        # Trend score (0–12): broad agreement across trend, momentum, and volume categories
        dataframe["trend_score"] = (
            (dataframe["ema9"]  > dataframe["ema21"]).astype(int) +                         # 1. fast EMA above slow
            (dataframe["ema21"] > dataframe["ema50"]).astype(int) +                         # 2. mid EMA above long
            (dataframe["close"] > dataframe["vwap"]).astype(int) +                         # 3. above rolling VWAP
            (dataframe["supertrend_dir"] == 1).astype(int) +                                # 4. Supertrend bullish
            (dataframe["ichi_tenkan"] > dataframe["ichi_kijun"]).astype(int) +              # 5. Ichimoku TK bullish
            (dataframe["macd_hist"] > 0).astype(int) +                                     # 6. MACD histogram positive
            (dataframe["macd_hist"] > dataframe["macd_hist"].shift(1)).astype(int) +        # 7. MACD momentum accelerating
            (
                (dataframe["rsi"] > self.buy_rsi_trend_min.value) &
                (dataframe["rsi"] < self.buy_rsi_trend_max.value)
            ).astype(int) +                                                                 # 8. RSI in bullish zone
            (
                (dataframe["stoch_k"] > dataframe["stoch_d"]) &
                (dataframe["stoch_k"] < 80)
            ).astype(int) +                                                                 # 9. Stochastic bullish, not overbought
            (
                (dataframe["adx"] > self.buy_adx_threshold.value) &
                (dataframe["dmp"] > dataframe["dmn"])
            ).astype(int) +                                                                 # 10. ADX trending + DM+ dominant
            (dataframe["obv"] > dataframe["obv_ma"]).astype(int) +                         # 11. OBV above its MA
            (dataframe["cmf"] > 0).astype(int)                                             # 12. Chaikin Money Flow positive
        )

        # Reversion score (0–8): independent oversold signals from different indicator families
        dataframe["reversion_score"] = (
            (dataframe["rsi"]    < self.buy_rsi_reversion_max.value).astype(int) +         # 1. RSI oversold
            (dataframe["close"]  < dataframe["bb_lower"]).astype(int) +                    # 2. Below Bollinger lower band
            (dataframe["stoch_k"] < 20).astype(int) +                                      # 3. Stochastic oversold
            (dataframe["willr"]  < -80).astype(int) +                                      # 4. Williams %R oversold
            (dataframe["cci"]    < -100).astype(int) +                                     # 5. CCI oversold
            (dataframe["mfi"]    < 25).astype(int) +                                       # 6. MFI: selling pressure
            (dataframe["volume"] > dataframe["volume_ma"] * 1.5).astype(int) +             # 7. Volume spike (capitulation)
            dataframe["near_fib_support"].astype(int)                                       # 8. Near Fibonacci support level
        )

        # ── 1h trend gate ─────────────────────────────────────────────────────
        # Prevents 5m trend entries that are counter to the hourly trend direction.
        # merge_informative_pair forward-fills the 1h value across all 5m candles
        # in that hour, so trend_up_1h reflects the most recent closed 1h candle.
        informative_1h = self.dp.get_pair_dataframe(pair=metadata["pair"], timeframe="1h")
        informative_1h["ema9"]     = ta.ema(informative_1h["close"], length=9)
        informative_1h["ema21"]    = ta.ema(informative_1h["close"], length=21)
        informative_1h["trend_up"] = informative_1h["ema9"] > informative_1h["ema21"]
        dataframe = merge_informative_pair(dataframe, informative_1h, self.timeframe, "1h", ffill=True)

        # ── 4h macro regime gate ───────────────────────────────────────────────
        # Blocks all entries when the 4h trend is bearish (EMA50 < EMA200).
        # In a sustained bear market this goes False and capital is preserved in
        # USDT until the macro trend flips. Uses the classic "golden/death cross"
        # on the 4h chart as the regime switch signal.
        informative_4h = self.dp.get_pair_dataframe(pair=metadata["pair"], timeframe="4h")
        informative_4h["ema50_4h"]   = ta.ema(informative_4h["close"], length=50)
        informative_4h["ema200_4h"]  = ta.ema(informative_4h["close"], length=200)
        informative_4h["macro_bull"] = informative_4h["ema50_4h"] > informative_4h["ema200_4h"]
        dataframe = merge_informative_pair(dataframe, informative_4h, self.timeframe, "4h", ffill=True)

        return dataframe

    def populate_entry_trend(self, dataframe: DataFrame, metadata: dict) -> DataFrame:

        trend_entry = (
            (dataframe["trend_score"] >= self.buy_trend_score_min.value) &
            (dataframe["close"] > dataframe["ema50"]) &                                    # price above medium-term trend
            (dataframe["higher_lows"]) &                                                    # uptrend structure confirmed
            (dataframe["volume"] > dataframe["volume_ma"]) &                               # volume confirmation
            (dataframe["trend_up_1h"]) &                                                   # 1h EMA9 > EMA21 gate
            (dataframe["macro_bull_4h"])                                                    # 4h EMA50 > EMA200 macro regime gate
        )

        reversion_entry = (
            (dataframe["reversion_score"] >= self.buy_reversion_score_min.value) &
            (dataframe["adx"] < self.buy_adx_threshold.value) &                            # only in ranging (non-trending) market
            (dataframe["rsi"] < dataframe["rsi"].shift(1)) &                               # RSI still falling — not yet bouncing
            (dataframe["close"] > dataframe["ema100"]) &                                   # above long-term baseline (avoids downtrend)
            (dataframe["macro_bull_4h"])                                                    # 4h macro regime gate — no catching falling knives in a bear market
        )

        dataframe.loc[trend_entry,     "enter_long"] = 1
        dataframe.loc[reversion_entry, "enter_long"] = 1
        dataframe.loc[trend_entry,     "enter_tag"]  = "trend_confluence"
        dataframe.loc[reversion_entry, "enter_tag"]  = "mean_reversion"

        return dataframe

    def populate_exit_trend(self, dataframe: DataFrame, metadata: dict) -> DataFrame:
        return dataframe

    def custom_exit(self, pair: str, trade, current_time, current_rate: float,
                    current_profit: float, **kwargs):
        dataframe, _ = self.dp.get_analyzed_dataframe(pair, self.timeframe)
        if dataframe.empty:
            return None
        c = dataframe.iloc[-1]

        if trade.enter_tag == "trend_confluence":
            if (
                c["trend_score"] <= self.sell_trend_score_exit.value
                and c["ema9"] < c["ema21"]
                and c["macd_hist"] < 0
            ):
                return "trend_exit"

        elif trade.enter_tag == "mean_reversion":
            if (
                c["close"] > c["bb_middle"]
                and c["rsi"] > self.sell_rsi_reversion_min.value
                and c["stoch_k"] > 50
            ):
                return "reversion_exit"

        return None

    def bot_loop_start(self, current_time: datetime, **kwargs) -> None:
        self._maybe_send_weekly_report(current_time)

    def _maybe_send_weekly_report(self, current_time: datetime) -> None:
        """Send a weekly performance summary every Friday at 15:00 UTC (3 pm UTC).
        Adjust the hour below if you prefer a different local time."""
        ct = current_time.astimezone(timezone.utc) if current_time.tzinfo else current_time.replace(tzinfo=timezone.utc)

        # Only fire on Fridays between 15:00 and 15:04 UTC
        if ct.weekday() != 4 or ct.hour != 15 or ct.minute >= 5:
            return

        # One send per Friday — marker file prevents duplicate sends within the 5-min window
        marker = Path("/freqtrade/user_data/.last_weekly_report")
        today_str = ct.strftime("%Y-%m-%d")
        if marker.exists() and marker.read_text().strip() == today_str:
            return

        # Week window: last Saturday 00:00 UTC → now (Friday 15:00)
        week_start = (ct - timedelta(days=6)).replace(hour=0, minute=0, second=0, microsecond=0)
        week_label = f"{week_start.strftime('%a %d %b')} → {ct.strftime('%a %d %b %Y')}"

        try:
            week_trades = Trade.get_trades([
                Trade.is_open.is_(False),
                Trade.close_date >= week_start,
            ]).all()
            all_trades = Trade.get_trades([Trade.is_open.is_(False)]).all()
        except Exception:
            return  # DB not ready

        total = len(week_trades)
        if total == 0:
            self.dp.send_msg(f"📊 *Weekly Report — {week_label}*\n\nNo closed trades this week.")
            marker.write_text(today_str)
            return

        wins   = sum(1 for t in week_trades if t.close_profit > 0)
        losses = total - wins
        week_pnl = sum(t.close_profit_abs for t in week_trades)

        # Per-pair breakdown
        pairs: dict = {}
        for t in week_trades:
            d = pairs.setdefault(t.pair, {"trades": 0, "wins": 0, "pnl": 0.0})
            d["trades"] += 1
            d["wins"]   += 1 if t.close_profit > 0 else 0
            d["pnl"]    += t.close_profit_abs

        pair_lines = []
        for pair, d in sorted(pairs.items()):
            pct_of_total = d["trades"] / total * 100
            wr  = d["wins"] / d["trades"] * 100
            sgn = "+" if d["pnl"] >= 0 else ""
            pair_lines.append(
                f"  {pair}  {d['trades']} trades ({pct_of_total:.0f}%)  "
                f"{wr:.0f}% WR  {sgn}{d['pnl']:.2f} USDT"
            )

        # Cumulative balance from 1 000 USDT starting wallet
        all_pnl  = sum(t.close_profit_abs for t in all_trades)
        balance  = 1000.0 + all_pnl
        sgn_week = "+" if week_pnl >= 0 else ""
        sgn_all  = "+" if all_pnl  >= 0 else ""

        msg = (
            f"📊 *Weekly Report — {week_label}*\n\n"
            f"Trades: {total}  |  Win rate: {wins/total*100:.1f}% ({wins}W / {losses}L)\n"
            f"Week P&L: {sgn_week}{week_pnl:.2f} USDT\n\n"
            f"*By pair:*\n" + "\n".join(pair_lines) + "\n\n"
            f"Cumulative P&L: {sgn_all}{all_pnl:.2f} USDT\n"
            f"Est. balance: {balance:.2f} USDT"
        )

        self.dp.send_msg(msg)
        marker.write_text(today_str)
