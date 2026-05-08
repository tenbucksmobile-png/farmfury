from freqtrade.strategy import IStrategy, IntParameter, DecimalParameter
from pandas import DataFrame
import pandas_ta as ta


class CombinedStrategy(IStrategy):
    """
    Comprehensive TA confluence strategy — long only, spot trading.

    Scores 12 independent trend/momentum signals and 8 mean-reversion signals
    drawn from trend, momentum, volume, volatility, and chart-pattern categories.
    Enters only when enough signals agree simultaneously (confluence threshold).

    Entry tags:
      trend_confluence — broad multi-indicator agreement in a trending market
      mean_reversion   — deep oversold confluence in a ranging market
    """

    INTERFACE_VERSION = 3
    timeframe = "5m"

    minimal_roi = {
        "0":   0.03,
        "30":  0.02,
        "60":  0.015,
        "120": 0.01,
    }

    stoploss = -0.03

    trailing_stop = True
    trailing_stop_positive = 0.01
    trailing_stop_positive_offset = 0.02
    trailing_only_offset_is_reached = True

    process_only_new_candles = True
    use_exit_signal = True
    exit_profit_only = False
    ignore_roi_if_entry_signal = False

    # Covers EMA100 (longest period used) + Ichimoku + all shorter indicators
    startup_candle_count = 100

    # ── Hyperopt search spaces ────────────────────────────────────────────────
    buy_adx_threshold       = IntParameter(25, 40, default=28, space="buy",  optimize=True)
    buy_trend_score_min     = IntParameter(6,  12, default=8,  space="buy",  optimize=True)
    buy_reversion_score_min = IntParameter(4,  7,  default=5,  space="buy",  optimize=True)
    buy_rsi_trend_min       = IntParameter(44, 58, default=48, space="buy",  optimize=True)
    buy_rsi_trend_max       = IntParameter(65, 78, default=72, space="buy",  optimize=True)
    buy_rsi_reversion_max   = IntParameter(20, 35, default=30, space="buy",  optimize=True)
    sell_rsi_reversion_min  = IntParameter(50, 65, default=55, space="sell", optimize=True)
    sell_trend_score_exit   = IntParameter(2,  6,  default=4,  space="sell", optimize=True)

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

        return dataframe

    def populate_entry_trend(self, dataframe: DataFrame, metadata: dict) -> DataFrame:

        trend_entry = (
            (dataframe["trend_score"] >= self.buy_trend_score_min.value) &
            (dataframe["close"] > dataframe["ema50"]) &                                    # price above medium-term trend
            (dataframe["higher_lows"]) &                                                    # uptrend structure confirmed
            (dataframe["volume"] > dataframe["volume_ma"])                                  # volume confirmation
        )

        reversion_entry = (
            (dataframe["reversion_score"] >= self.buy_reversion_score_min.value) &
            (dataframe["adx"] < self.buy_adx_threshold.value) &                            # only in ranging (non-trending) market
            (dataframe["rsi"] < dataframe["rsi"].shift(1)) &                               # RSI still falling — not yet bouncing
            (dataframe["close"] > dataframe["ema100"])                                      # above long-term baseline (avoids downtrend)
        )

        dataframe.loc[trend_entry,     "enter_long"] = 1
        dataframe.loc[reversion_entry, "enter_long"] = 1
        dataframe.loc[trend_entry,     "enter_tag"]  = "trend_confluence"
        dataframe.loc[reversion_entry, "enter_tag"]  = "mean_reversion"

        return dataframe

    def populate_exit_trend(self, dataframe: DataFrame, metadata: dict) -> DataFrame:

        trend_exit = (
            (dataframe["trend_score"] <= self.sell_trend_score_exit.value) &               # confluence collapsed
            (dataframe["ema9"] < dataframe["ema21"]) &                                     # EMA cross down (AND, not OR)
            (dataframe["macd_hist"] < 0)                                                    # momentum negative
        )

        reversion_exit = (
            (dataframe["close"]   > dataframe["bb_middle"]) &                              # price returned to mean
            (dataframe["rsi"]     > self.sell_rsi_reversion_min.value) &                   # momentum normalized
            (dataframe["stoch_k"] > 50)                                                     # stochastic recovered
        )

        dataframe.loc[trend_exit | reversion_exit, "exit_long"] = 1

        return dataframe
