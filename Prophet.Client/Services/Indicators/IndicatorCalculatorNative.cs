using Prophet.Client.Models;
using Prophet.Client.Services.Indicators.Native;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Prophet.Client.Services.Indicators;

/// <summary>
/// 指标计算器（使用Prophet.Core + TA-Lib）
/// 确保与DSL引擎计算结果完全一致
/// </summary>
public static class IndicatorCalculatorNative
{
    // ============================================================================
    // MA 类型指标
    // ============================================================================

    /// <summary>
    /// 计算SMA（简单移动平均）
    /// </summary>
    public static List<MAData> CalculateSMA(
        List<Candlestick> candles, 
        int PERIOD, 
        PriceField field = PriceField.Close)
    {
        if (candles == null || candles.Count < PERIOD || PERIOD <= 0)
            return new List<MAData>();

        double[] prices = ExtractPriceField(candles, field);
        var result = new ProphetCoreNative.IndicatorResult();
        
        int retCode = ProphetCoreNative.Prophet_SMA(prices, prices.Length, PERIOD, ref result);
        if (retCode != 0)
        {
            throw new Exception($"SMA calculation failed (code {retCode}): {result.ErrorMessage}");
        }

        double[] values = ProphetCoreNative.CopyResultToArray(result);
        int outBegin = result.OutBegin;  // 保存outBegin，因为FreeResult会清空它
        ProphetCoreNative.Prophet_FreeResult(ref result);

        return ConvertToMAData(candles, values, outBegin, PERIOD);
    }

    /// <summary>
    /// 计算EMA（指数移动平均）
    /// </summary>
    public static List<MAData> CalculateEMA(
        List<Candlestick> candles, 
        int PERIOD, 
        PriceField field = PriceField.Close)
    {
        if (candles == null || candles.Count < PERIOD || PERIOD <= 0)
            return new List<MAData>();

        double[] prices = ExtractPriceField(candles, field);
        var result = new ProphetCoreNative.IndicatorResult();
        
        int retCode = ProphetCoreNative.Prophet_EMA(prices, prices.Length, PERIOD, ref result);
        if (retCode != 0)
        {
            throw new Exception($"EMA calculation failed (code {retCode}): {result.ErrorMessage}");
        }

        double[] values = ProphetCoreNative.CopyResultToArray(result);
        int outBegin = result.OutBegin;  // 保存outBegin，因为FreeResult会清空它
        ProphetCoreNative.Prophet_FreeResult(ref result);

        return ConvertToMAData(candles, values, outBegin, PERIOD);
    }

    /// <summary>
    /// 计算WMA（加权移动平均）
    /// </summary>
    public static List<MAData> CalculateWMA(
        List<Candlestick> candles, 
        int PERIOD, 
        PriceField field = PriceField.Close)
    {
        if (candles == null || candles.Count < PERIOD || PERIOD <= 0)
            return new List<MAData>();

        double[] prices = ExtractPriceField(candles, field);
        var result = new ProphetCoreNative.IndicatorResult();
        
        int retCode = ProphetCoreNative.Prophet_WMA(prices, prices.Length, PERIOD, ref result);
        if (retCode != 0)
        {
            throw new Exception($"WMA calculation failed (code {retCode}): {result.ErrorMessage}");
        }

        double[] values = ProphetCoreNative.CopyResultToArray(result);
        int outBegin = result.OutBegin;  // 保存outBegin，因为FreeResult会清空它
        ProphetCoreNative.Prophet_FreeResult(ref result);

        return ConvertToMAData(candles, values, outBegin, PERIOD);
    }

    /// <summary>
    /// 计算DEMA（双重指数移动平均）
    /// </summary>
    public static List<MAData> CalculateDEMA(
        List<Candlestick> candles, 
        int PERIOD, 
        PriceField field = PriceField.Close)
    {
        if (candles == null || candles.Count < PERIOD || PERIOD <= 0)
            return new List<MAData>();

        double[] prices = ExtractPriceField(candles, field);
        var result = new ProphetCoreNative.IndicatorResult();
        
        int retCode = ProphetCoreNative.Prophet_DEMA(prices, prices.Length, PERIOD, ref result);
        if (retCode != 0)
        {
            throw new Exception($"DEMA calculation failed (code {retCode}): {result.ErrorMessage}");
        }

        double[] values = ProphetCoreNative.CopyResultToArray(result);
        int outBegin = result.OutBegin;
        ProphetCoreNative.Prophet_FreeResult(ref result);

        return ConvertToMAData(candles, values, outBegin, PERIOD);
    }

    /// <summary>
    /// 计算TEMA（三重指数移动平均）
    /// </summary>
    public static List<MAData> CalculateTEMA(
        List<Candlestick> candles, 
        int PERIOD, 
        PriceField field = PriceField.Close)
    {
        if (candles == null || candles.Count < PERIOD || PERIOD <= 0)
            return new List<MAData>();

        double[] prices = ExtractPriceField(candles, field);
        var result = new ProphetCoreNative.IndicatorResult();
        
        int retCode = ProphetCoreNative.Prophet_TEMA(prices, prices.Length, PERIOD, ref result);
        if (retCode != 0)
        {
            throw new Exception($"TEMA calculation failed (code {retCode}): {result.ErrorMessage}");
        }

        double[] values = ProphetCoreNative.CopyResultToArray(result);
        int outBegin = result.OutBegin;
        ProphetCoreNative.Prophet_FreeResult(ref result);

        return ConvertToMAData(candles, values, outBegin, PERIOD);
    }

    // ============================================================================
    // 复合指标
    // ============================================================================

    /// <summary>
    /// 计算BOLL（布林带）
    /// </summary>
    public static List<BOLLData> CalculateBOLL(
        List<Candlestick> candles, 
        int PERIOD, 
        double stdDev = 2.0)
    {
        if (candles == null || candles.Count < PERIOD || PERIOD <= 0)
            return new List<BOLLData>();

        double[] closes = candles.Select(c => c.Close).ToArray();
        
        var upper = new ProphetCoreNative.IndicatorResult();
        var middle = new ProphetCoreNative.IndicatorResult();
        var lower = new ProphetCoreNative.IndicatorResult();
        
        int retCode = ProphetCoreNative.Prophet_BBANDS(
            closes, closes.Length, PERIOD, stdDev,
            ref upper, ref middle, ref lower
        );

        if (retCode != 0)
        {
            throw new Exception($"BBANDS calculation failed (code {retCode})");
        }

        double[] upperValues = ProphetCoreNative.CopyResultToArray(upper);
        double[] middleValues = ProphetCoreNative.CopyResultToArray(middle);
        double[] lowerValues = ProphetCoreNative.CopyResultToArray(lower);
        int outBegin = upper.OutBegin;  // 保存outBegin，因为FreeResult会清空它

        ProphetCoreNative.Prophet_FreeResult(ref upper);
        ProphetCoreNative.Prophet_FreeResult(ref middle);
        ProphetCoreNative.Prophet_FreeResult(ref lower);

        var result = new List<BOLLData>();
        
        // 🔑 关键修复：确保所有K线都有对应的数据点
        // 遍历所有K线，根据outBegin和values.Length判断每个K线是否有有效值
        for (int candleIndex = 0; candleIndex < candles.Count; candleIndex++)
        {
            double upperValue = double.NaN;
            double middleValue = double.NaN;
            double lowerValue = double.NaN;
            
            // 检查当前K线是否在有效数据范围内
            if (candleIndex >= outBegin)
            {
                int dataIndex = candleIndex - outBegin;
                if (dataIndex < upperValues.Length)
                {
                    // 在有效数据范围内，使用实际值
                    upperValue = upperValues[dataIndex];
                    middleValue = middleValues[dataIndex];
                    lowerValue = lowerValues[dataIndex];
                }
                // 如果dataIndex >= values.Length，说明数据不足，保持NaN
            }
            // 如果candleIndex < outBegin，说明前面没有足够历史数据，保持NaN
            
            result.Add(new BOLLData
            {
                Time = candles[candleIndex].Time,
                Upper = upperValue,
                Middle = middleValue,
                Lower = lowerValue
            });
        }

        return result;
    }

    /// <summary>
    /// 计算Keltner Channel（肯特纳通道）
    /// </summary>
    public static List<KeltnerData> CalculateKeltner(
        List<Candlestick> candles,
        int PERIOD = 20,
        double multiplier = 2.0)
    {
        if (candles == null || candles.Count < PERIOD || PERIOD <= 0)
            return new List<KeltnerData>();

        double[] highs = candles.Select(c => c.High).ToArray();
        double[] lows = candles.Select(c => c.Low).ToArray();
        double[] closes = candles.Select(c => c.Close).ToArray();
        
        var upper = new ProphetCoreNative.IndicatorResult();
        var middle = new ProphetCoreNative.IndicatorResult();
        var lower = new ProphetCoreNative.IndicatorResult();
        
        int retCode = ProphetCoreNative.Prophet_KELTNER(
            highs, lows, closes, closes.Length, PERIOD, multiplier,
            ref upper, ref middle, ref lower
        );

        if (retCode != 0)
        {
            throw new Exception($"Keltner calculation failed (code {retCode})");
        }

        double[] upperValues = ProphetCoreNative.CopyResultToArray(upper);
        double[] middleValues = ProphetCoreNative.CopyResultToArray(middle);
        double[] lowerValues = ProphetCoreNative.CopyResultToArray(lower);
        int outBegin = upper.OutBegin;

        ProphetCoreNative.Prophet_FreeResult(ref upper);
        ProphetCoreNative.Prophet_FreeResult(ref middle);
        ProphetCoreNative.Prophet_FreeResult(ref lower);

        var result = new List<KeltnerData>();
        
        // 🔑 关键修复：确保所有K线都有对应的数据点
        // 遍历所有K线，根据outBegin和values.Length判断每个K线是否有有效值
        for (int candleIndex = 0; candleIndex < candles.Count; candleIndex++)
        {
            double upperValue = double.NaN;
            double middleValue = double.NaN;
            double lowerValue = double.NaN;
            
            // 检查当前K线是否在有效数据范围内
            if (candleIndex >= outBegin)
            {
                int dataIndex = candleIndex - outBegin;
                if (dataIndex < upperValues.Length)
                {
                    // 在有效数据范围内，使用实际值
                    upperValue = upperValues[dataIndex];
                    middleValue = middleValues[dataIndex];
                    lowerValue = lowerValues[dataIndex];
                }
                // 如果dataIndex >= values.Length，说明数据不足，保持NaN
            }
            // 如果candleIndex < outBegin，说明前面没有足够历史数据，保持NaN
            
            result.Add(new KeltnerData
            {
                Time = candles[candleIndex].Time,
                Upper = upperValue,
                Middle = middleValue,
                Lower = lowerValue
            });
        }

        return result;
    }

    /// <summary>
    /// 计算Ichimoku Cloud（一目均衡表）
    /// </summary>
    public static List<IchimokuData> CalculateIchimoku(
        List<Candlestick> candles,
        int tenkanPeriod = 9,
        int kijunPeriod = 26,
        int senkouBPeriod = 52)
    {
        if (candles == null || candles.Count < senkouBPeriod)
            return new List<IchimokuData>();

        double[] highs = candles.Select(c => c.High).ToArray();
        double[] lows = candles.Select(c => c.Low).ToArray();
        double[] closes = candles.Select(c => c.Close).ToArray();
        
        var tenkan = new ProphetCoreNative.IndicatorResult();
        var kijun = new ProphetCoreNative.IndicatorResult();
        var senkouA = new ProphetCoreNative.IndicatorResult();
        var senkouB = new ProphetCoreNative.IndicatorResult();
        var chikou = new ProphetCoreNative.IndicatorResult();
        
        int retCode = ProphetCoreNative.Prophet_ICHIMOKU(
            highs, lows, closes, closes.Length,
            tenkanPeriod, kijunPeriod, senkouBPeriod,
            ref tenkan, ref kijun, ref senkouA, ref senkouB, ref chikou
        );

        if (retCode != 0)
        {
            throw new Exception($"Ichimoku calculation failed (code {retCode})");
        }

        double[] tenkanValues = ProphetCoreNative.CopyResultToArray(tenkan);
        double[] kijunValues = ProphetCoreNative.CopyResultToArray(kijun);
        double[] senkouAValues = ProphetCoreNative.CopyResultToArray(senkouA);
        double[] senkouBValues = ProphetCoreNative.CopyResultToArray(senkouB);
        double[] chikouValues = ProphetCoreNative.CopyResultToArray(chikou);
        int outBegin = tenkan.OutBegin;

        ProphetCoreNative.Prophet_FreeResult(ref tenkan);
        ProphetCoreNative.Prophet_FreeResult(ref kijun);
        ProphetCoreNative.Prophet_FreeResult(ref senkouA);
        ProphetCoreNative.Prophet_FreeResult(ref senkouB);
        ProphetCoreNative.Prophet_FreeResult(ref chikou);

        var result = new List<IchimokuData>();
        
        // 🔑 关键修复：确保所有K线都有对应的数据点
        // 遍历所有K线，根据outBegin和values.Length判断每个K线是否有有效值
        for (int candleIndex = 0; candleIndex < candles.Count; candleIndex++)
        {
            double tenkanValue = double.NaN;
            double kijunValue = double.NaN;
            double senkouAValue = double.NaN;
            double senkouBValue = double.NaN;
            double chikouValue = double.NaN;
            
            // 检查当前K线是否在有效数据范围内
            if (candleIndex >= outBegin)
            {
                int dataIndex = candleIndex - outBegin;
                if (dataIndex < tenkanValues.Length)
                {
                    // 在有效数据范围内，使用实际值
                    tenkanValue = tenkanValues[dataIndex];
                    kijunValue = kijunValues[dataIndex];
                    senkouAValue = senkouAValues[dataIndex];
                    senkouBValue = senkouBValues[dataIndex];
                    chikouValue = chikouValues[dataIndex];
                }
                // 如果dataIndex >= values.Length，说明数据不足，保持NaN
            }
            // 如果candleIndex < outBegin，说明前面没有足够历史数据，保持NaN
            
            result.Add(new IchimokuData
            {
                Time = candles[candleIndex].Time,
                Tenkan = tenkanValue,
                Kijun = kijunValue,
                SenkouA = senkouAValue,
                SenkouB = senkouBValue,
                Chikou = chikouValue
            });
        }

        return result;
    }

    /// <summary>
    /// 计算SAR（抛物线转向指标）
    /// </summary>
    public static List<SARValue> CalculateSAR(
        List<Candlestick> candles, 
        double acceleration = 0.02, 
        double maximum = 0.20)
    {
        if (candles == null || candles.Count < 2)
            return new List<SARValue>();

        double[] highs = candles.Select(c => c.High).ToArray();
        double[] lows = candles.Select(c => c.Low).ToArray();
        
        var result = new ProphetCoreNative.IndicatorResult();
        
        int retCode = ProphetCoreNative.Prophet_SAR(
            highs, lows, highs.Length,
            acceleration, maximum,
            ref result
        );

        if (retCode != 0)
        {
            throw new Exception($"SAR calculation failed (code {retCode}): {result.ErrorMessage}");
        }

        double[] values = ProphetCoreNative.CopyResultToArray(result);
        int outBegin = result.OutBegin;  // 保存outBegin，因为FreeResult会清空它
        ProphetCoreNative.Prophet_FreeResult(ref result);

        var sarDataList = new List<SARValue>();
        
        // 🔑 关键修复：确保所有K线都有对应的数据点
        // 遍历所有K线，根据outBegin和values.Length判断每个K线是否有有效值
        for (int candleIndex = 0; candleIndex < candles.Count; candleIndex++)
        {
            double value = double.NaN;
            bool isUpTrend = false;
            
            // 检查当前K线是否在有效数据范围内
            if (candleIndex >= outBegin)
            {
                int dataIndex = candleIndex - outBegin;
                if (dataIndex < values.Length)
                {
                    // 在有效数据范围内，使用实际值
                    value = values[dataIndex];
                    isUpTrend = value < candles[candleIndex].Close;
                }
                // 如果dataIndex >= values.Length，说明数据不足，保持NaN
            }
            // 如果candleIndex < outBegin，说明前面没有足够历史数据，保持NaN
            
            sarDataList.Add(new SARValue
            {
                Time = candles[candleIndex].Time,
                Value = value,
                IsUpTrend = isUpTrend
            });
        }

        return sarDataList;
    }

    /// <summary>
    /// 计算TRIX（三重指数平滑移动平均）
    /// </summary>
    public static List<MAData> CalculateTRIX(
        List<Candlestick> candles, 
        int PERIOD)
    {
        if (candles == null || candles.Count < PERIOD * 3 || PERIOD <= 0)
            return new List<MAData>();

        double[] closes = candles.Select(c => c.Close).ToArray();
        var result = new ProphetCoreNative.IndicatorResult();
        
        int retCode = ProphetCoreNative.Prophet_TRIX(closes, closes.Length, PERIOD, ref result);
        if (retCode != 0)
        {
            throw new Exception($"TRIX calculation failed (code {retCode}): {result.ErrorMessage}");
        }

        double[] values = ProphetCoreNative.CopyResultToArray(result);
        int outBegin = result.OutBegin;  // 保存outBegin，因为FreeResult会清空它
        ProphetCoreNative.Prophet_FreeResult(ref result);

        return ConvertToMAData(candles, values, outBegin, PERIOD);
    }

    /// <summary>
    /// 计算VWAP（成交量加权平均价）
    /// </summary>
    public static List<MAData> CalculateVWAP(
        List<Candlestick> candles, 
        int PERIOD = 0)
    {
        if (candles == null || candles.Count < 1)
            return new List<MAData>();

        double[] highs = candles.Select(c => c.High).ToArray();
        double[] lows = candles.Select(c => c.Low).ToArray();
        double[] closes = candles.Select(c => c.Close).ToArray();
        double[] volumes = candles.Select(c => c.Volume).ToArray();
        
        var result = new ProphetCoreNative.IndicatorResult();
        
        int retCode = ProphetCoreNative.Prophet_VWAP(
            highs, lows, closes, volumes,
            highs.Length, PERIOD,
            ref result
        );

        if (retCode != 0)
        {
            throw new Exception($"VWAP calculation failed (code {retCode}): {result.ErrorMessage}");
        }

        double[] values = ProphetCoreNative.CopyResultToArray(result);
        int outBegin = result.OutBegin;  // 保存outBegin，因为FreeResult会清空它
        ProphetCoreNative.Prophet_FreeResult(ref result);

        // 🔍 VWAP调试输出
        if (values.Length >= 5)
        {
        }
        
        // 特别关注最后一个值（用于与币安对比）
        if (values.Length > 0)
        {
            var lastCandle = candles[candles.Count - 1];
            var lastValue = values[values.Length - 1];
        }

        return ConvertToMAData(candles, values, outBegin, PERIOD > 0 ? PERIOD : 1);
    }

    /// <summary>
    /// 计算AVL（平均成交量线，使用ATR实现）
    /// </summary>
    public static List<MAData> CalculateAVL(
        List<Candlestick> candles, 
        int PERIOD = 14)
    {
        if (candles == null || candles.Count < PERIOD || PERIOD <= 0)
            return new List<MAData>();

        // AVL可以用简单的成交量移动平均
        double[] volumes = candles.Select(c => c.Volume).ToArray();
        var result = new ProphetCoreNative.IndicatorResult();
        
        int retCode = ProphetCoreNative.Prophet_SMA(volumes, volumes.Length, PERIOD, ref result);
        if (retCode != 0)
        {
            throw new Exception($"AVL calculation failed (code {retCode}): {result.ErrorMessage}");
        }

        double[] values = ProphetCoreNative.CopyResultToArray(result);
        int outBegin = result.OutBegin;  // 保存outBegin，因为FreeResult会清空它
        ProphetCoreNative.Prophet_FreeResult(ref result);

        return ConvertToMAData(candles, values, outBegin, PERIOD);
    }

    /// <summary>
    /// 计算MACD
    /// </summary>
    public static List<MACDData> CalculateMACD(
        List<Candlestick> candles,
        int fastPeriod = 12,
        int slowPeriod = 26,
        int signalPeriod = 9)
    {
        if (candles == null || candles.Count < slowPeriod)
            return new List<MACDData>();

        double[] closes = candles.Select(c => c.Close).ToArray();
        
        var macdResult = new ProphetCoreNative.IndicatorResult();
        var signalResult = new ProphetCoreNative.IndicatorResult();
        var histResult = new ProphetCoreNative.IndicatorResult();
        
        int retCode = ProphetCoreNative.Prophet_MACD(
            closes, closes.Length,
            fastPeriod, slowPeriod, signalPeriod,
            ref macdResult, ref signalResult, ref histResult
        );

        if (retCode != 0)
        {
            throw new Exception($"MACD calculation failed (code {retCode})");
        }

        double[] macdValues = ProphetCoreNative.CopyResultToArray(macdResult);
        double[] signalValues = ProphetCoreNative.CopyResultToArray(signalResult);
        double[] histValues = ProphetCoreNative.CopyResultToArray(histResult);
        int outBegin = macdResult.OutBegin;  // 保存outBegin，因为FreeResult会清空它

        ProphetCoreNative.Prophet_FreeResult(ref macdResult);
        ProphetCoreNative.Prophet_FreeResult(ref signalResult);
        ProphetCoreNative.Prophet_FreeResult(ref histResult);

        var result = new List<MACDData>();
        
        // 🔑 关键修复：确保所有K线都有对应的数据点
        // 遍历所有K线，根据outBegin和values.Length判断每个K线是否有有效值
        for (int candleIndex = 0; candleIndex < candles.Count; candleIndex++)
        {
            double dif = double.NaN;
            double dea = double.NaN;
            double histogram = double.NaN;
            
            // 检查当前K线是否在有效数据范围内
            if (candleIndex >= outBegin)
            {
                int dataIndex = candleIndex - outBegin;
                if (dataIndex < macdValues.Length)
                {
                    // 在有效数据范围内，使用实际值
                    dif = macdValues[dataIndex];
                    dea = signalValues[dataIndex];
                    histogram = histValues[dataIndex];
                }
                // 如果dataIndex >= values.Length，说明数据不足，保持NaN
            }
            // 如果candleIndex < outBegin，说明前面没有足够历史数据，保持NaN
            
            result.Add(new MACDData
            {
                Time = candles[candleIndex].Time,
                DIF = dif,
                DEA = dea,
                Histogram = histogram
            });
        }

        return result;
    }

    /// <summary>
    /// 计算RSI
    /// </summary>
    public static List<MAData> CalculateRSI(
        List<Candlestick> candles,
        int PERIOD = 14)
    {
        if (candles == null || candles.Count < PERIOD || PERIOD <= 0)
            return new List<MAData>();

        double[] closes = candles.Select(c => c.Close).ToArray();
        var result = new ProphetCoreNative.IndicatorResult();
        
        int retCode = ProphetCoreNative.Prophet_RSI(closes, closes.Length, PERIOD, ref result);
        if (retCode != 0)
        {
            throw new Exception($"RSI calculation failed (code {retCode}): {result.ErrorMessage}");
        }

        double[] values = ProphetCoreNative.CopyResultToArray(result);
        int outBegin = result.OutBegin;  // 保存outBegin，因为FreeResult会清空它
        ProphetCoreNative.Prophet_FreeResult(ref result);

        return ConvertToMAData(candles, values, outBegin, PERIOD);
    }

    // ============================================================================
    // 辅助方法
    // ============================================================================

    private static double[] ExtractPriceField(List<Candlestick> candles, PriceField field)
    {
        return field switch
        {
            PriceField.Open => candles.Select(c => c.Open).ToArray(),
            PriceField.High => candles.Select(c => c.High).ToArray(),
            PriceField.Low => candles.Select(c => c.Low).ToArray(),
            PriceField.Close => candles.Select(c => c.Close).ToArray(),
            _ => candles.Select(c => c.Close).ToArray()
        };
    }

    private static List<MAData> ConvertToMAData(
        List<Candlestick> candles, 
        double[] values, 
        int outBegin,
        int PERIOD)
    {
        var result = new List<MAData>();
        
        // 🔑 关键修复：确保所有K线都有对应的数据点
        // 遍历所有K线，根据outBegin和values.Length判断每个K线是否有有效值
        for (int candleIndex = 0; candleIndex < candles.Count; candleIndex++)
        {
            double value = double.NaN;
            
            // 检查当前K线是否在有效数据范围内
            if (candleIndex >= outBegin)
            {
                int dataIndex = candleIndex - outBegin;
                if (dataIndex < values.Length)
                {
                    // 在有效数据范围内，使用实际值
                    value = values[dataIndex];
                }
                // 如果dataIndex >= values.Length，说明数据不足，保持NaN
            }
            // 如果candleIndex < outBegin，说明前面没有足够历史数据，保持NaN
            
            result.Add(new MAData
            {
                Time = candles[candleIndex].Time,
                PERIOD = PERIOD,
                Value = value
            });
        }
        
        return result;
    }
}

