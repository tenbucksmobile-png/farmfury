from freqtrade.strategy import IStrategy
from pandas import DataFrame
import pandas_ta as ta


class CombinedStrategy(IStrategy):
    """
    Combined Mean Reversion + Trend Following strategy.

    Regime detection via ADX:
      ADX > 25  → trending market  → EMA crossover + MACD signals
      ADX <= 25 → ranging market   → RSI + Bollinger Band mean reversion

    Risk:
      - 3% hard stoploss
      - Trailing stop activates once 2% profit is reached
      - ROI ladder exits positions progressively
    """

    INTERFACE_VERSION = 3
    timeframe = "5m"

    # Exit at these profit levels (key = minutes since entry)
    minimal_roi = {
        "0":   0.03,    # exit immediately at 3%
        "30":  0.02,    # exit at 2% after 30 min
        "60":  0.015,   # exit at 1.5% after 1 hour
        "120": 0.01,    # exit at 1% after 2 hours
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

    # Enough candles to calculate all indicators reliably
    startup_candle_count = 50

    # ADX threshold — above this the market is considered trending
    ADX_TREND_THRESHOLD = 25

    def populate_indicators(self, dataframe: DataFrame, metadata: dict) -> DataFrame:

        # Regime detection
        adx_data = ta.adx(dataframe["high"], dataframe["low"], dataframe["close"], length=14)
        dataframe["adx"] = adx_data["ADX_14"]

        # Trend following — EMA crossover
        dataframe["ema9"]  = ta.ema(dataframe["close"], length=9)
        dataframe["ema21"] = ta.ema(dataframe["close"], length=21)
        dataframe["ema50"] = ta.ema(dataframe["close"], length=50)

        # Trend following — MACD
        macd = ta.macd(dataframe["close"])
        dataframe["macd"]        = macd["MACD_12_26_9"]
        dataframe["macd_signal"] = macd["MACDs_12_26_9"]
        dataframe["macd_hist"]   = macd["MACDh_12_26_9"]

        # Mean reversion — RSI
        dataframe["rsi"] = ta.rsi(dataframe["close"], length=14)

        # Mean reversion — Bollinger Bands
        bb = ta.bbands(dataframe["close"], length=20, std=2.0)
        dataframe["bb_upper"]  = bb["BBU_20_2.0"]
        dataframe["bb_middle"] = bb["BBM_20_2.0"]
        dataframe["bb_lower"]  = bb["BBL_20_2.0"]

        # Volume filter — require above-average volume on entries
        dataframe["volume_ma"] = ta.sma(dataframe["volume"], length=20)

        # Boolean flag used in entry/exit logic
        dataframe["is_trending"] = dataframe["adx"] > self.ADX_TREND_THRESHOLD

        return dataframe

    def populate_entry_trend(self, dataframe: DataFrame, metadata: dict) -> DataFrame:

        trend_entry = (
            (dataframe["is_trending"]) &
            (dataframe["ema9"]  > dataframe["ema21"]) &           # fast EMA above slow
            (dataframe["close"] > dataframe["ema50"]) &           # price above long trend
            (dataframe["macd_hist"] > 0) &                        # MACD momentum positive
            (dataframe["rsi"] > 50) & (dataframe["rsi"] < 75) &  # not overbought
            (dataframe["volume"] > dataframe["volume_ma"])        # confirmed by volume
        )

        reversion_entry = (
            (~dataframe["is_trending"]) &
            (dataframe["close"] < dataframe["bb_lower"]) &        # price below lower band
            (dataframe["rsi"] < 35) &                             # oversold
            (dataframe["volume"] > dataframe["volume_ma"] * 0.8)  # adequate volume
        )

        dataframe.loc[trend_entry,     "enter_long"] = 1
        dataframe.loc[reversion_entry, "enter_long"] = 1

        # Tags appear in trade logs and the FreqUI dashboard
        dataframe.loc[trend_entry,     "enter_tag"] = "trend_follow"
        dataframe.loc[reversion_entry, "enter_tag"] = "mean_reversion"

        return dataframe

    def populate_exit_trend(self, dataframe: DataFrame, metadata: dict) -> DataFrame:

        trend_exit = (
            (dataframe["is_trending"]) &
            (
                (dataframe["ema9"] < dataframe["ema21"]) |        # EMA cross down
                (dataframe["macd_hist"] < 0)                      # momentum gone
            )
        )

        reversion_exit = (
            (~dataframe["is_trending"]) &
            (dataframe["close"]  > dataframe["bb_middle"]) &      # price returned to mean
            (dataframe["rsi"] > 50)                               # momentum normalized
        )

        dataframe.loc[trend_exit | reversion_exit, "exit_long"] = 1

        return dataframe
