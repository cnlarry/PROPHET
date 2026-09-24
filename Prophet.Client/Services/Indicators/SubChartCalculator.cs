using Prophet.Client.Models;
using Prophet.Client.Services.Indicators.Native;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Prophet.Client.Services.Indicators;

/// <summary>
/// 副图指标计算器（新架构）
/// 
/// 特点：
/// 1. 使用NativeCalculator统一包装
/// 2. 返回SubChartDataPoint通用数据模型
/// 3. 每个指标只需20-30行代码
/// 4. 完善的错误处理和验证
/// 
/// 对比旧架构：
/// - 旧架构：每个指标100+行代码，重复的错误处理和资源管理
/// - 新架构：每个指标20-30行代码，统一的错误处理
/// </summary>
public static class SubChartCalculator
{
    // ==================== 已重构的指标 ====================
    
    /// <summary>
    /// 计算RSI（相对强弱指标）
    /// 
    /// 代码对比：
    /// - 旧版本：~50行（IndicatorCalculatorNative.CalculateRSI）
    /// - 新版本：~25行（减少50%）
    /// </summary>
    /// <param name="candles">K线列表</param>
    /// <param name="PERIOD">周期（默认为14）</param>
    /// <returns>RSI数据点列表</returns>
    public static List<SubChartDataPoint> CalculateRSI(
        List<Candlestick> candles,
        int PERIOD = 14)
    {
        // 参数验证
        if (!NativeCalculator.ValidateCandles(candles, PERIOD, "RSI"))
        {
            return new List<SubChartDataPoint>();
        }
        
        // 提取收盘数据
        double[] closes = candles.Select(c => c.Close).ToArray();
        
        // 调用Native计算（使用统一包装器）
        return NativeCalculator.CalculateSingleOutput(
            candles,
            "RSI",
            () =>
            {
                var result = new ProphetCoreNative.IndicatorResult();
                result.ReturnCode = ProphetCoreNative.Prophet_RSI(
                    closes,
                    closes.Length,
                    PERIOD,
                    ref result
                );
                return result;
            },
            "value"  // RSI使用"value"作为输出key
        );
    }
    
    /// <summary>
    /// 计算多周期RSI（用于多周期对比）
    /// </summary>
    /// <param name="candles">K线列表</param>
    /// <param name="periods">周期数组（如 [7, 14, 21]）</param>
    /// <param name="enabled">是否启用各周期（如[true, true, false]）</param>
    /// <returns>包含多个周期RSI的数据点列表</returns>
    public static List<SubChartDataPoint> CalculateRSIMultiPeriod(
        List<Candlestick> candles,
        int[] periods,
        bool[] enabled)
    {
        if (candles == null || candles.Count == 0 || periods == null || periods.Length == 0)
        {
            return new List<SubChartDataPoint>();
        }
        
        // 初始化结果列表
        var results = candles.Select(c => new SubChartDataPoint { Time = c.Time }).ToList();
        
        // 计算每个周期的RSI
        for (int i = 0; i < periods.Length; i++)
        {
            if (enabled != null && i < enabled.Length && !enabled[i])
            {
                // 如果该周期未启用，填充NaN
                for (int j = 0; j < results.Count; j++)
                {
                    results[j].SetValue($"PERIOD{i + 1}", double.NaN);
                }
                continue;
            }
            
            // 计算该周期的RSI
            var rsiData = CalculateRSI(candles, periods[i]);
            
            // 将RSI数据合并到结果中
            for (int j = 0; j < results.Count && j < rsiData.Count; j++)
            {
                results[j].SetValue($"PERIOD{i + 1}", rsiData[j].GetValue("value"));
            }
        }
        
        return results;
    }
    
    /// <summary>
    /// 计算MACD（指数平滑移动平均）
    /// 
    /// 代码对比：
    /// - 旧版本：~90行（IndicatorCalculatorNative.CalculateMACD）
    /// - 新版本：~35行（减少60%）
    /// </summary>
    /// <param name="candles">K线列表</param>
    /// <param name="fastPeriod">快线周期（默认为12）</param>
    /// <param name="slowPeriod">慢线周期（默认为26）</param>
    /// <param name="signalPeriod">信号线周期（默认9）</param>
    /// <returns>MACD数据点列表</returns>
    public static List<SubChartDataPoint> CalculateMACD(
        List<Candlestick> candles,
        int fastPeriod = 12,
        int slowPeriod = 26,
        int signalPeriod = 9)
    {
        // 参数验证
        if (!NativeCalculator.ValidateCandles(candles, slowPeriod, "MACD"))
        {
            return new List<SubChartDataPoint>();
        }
        
        // 提取收盘数据
        double[] closes = candles.Select(c => c.Close).ToArray();
        
        // 调用Native计算（使用多输出包装器）
        return NativeCalculator.CalculateMultiOutput(
            candles,
            "MACD",
            (results) =>
            {
                ProphetCoreNative.Prophet_MACD(
                    closes,
                    closes.Length,
                    fastPeriod,
                    slowPeriod,
                    signalPeriod,
                    ref results[0],  // MACD
                    ref results[1],  // Signal
                    ref results[2]   // Histogram
                );
            },
            new[] { "macd", "signal", "histogram" },  // 输出keys（与核心引擎统一）
            3  // 输出数量
        );
    }
    
    /// <summary>
    /// 计算Volume（成交量）
    /// 注意：Volume不需要Native计算，直接从K线提取
    /// </summary>
    /// <param name="candles">K线列表</param>
    /// <returns>Volume数据点列表</returns>
    public static List<SubChartDataPoint> CalculateVolume(List<Candlestick> candles)
    {
        if (candles == null || candles.Count == 0)
        {
            return new List<SubChartDataPoint>();
        }
        
        // Volume直接从K线提取，不需要Native计算
        return candles.Select(c => new SubChartDataPoint
        {
            Time = c.Time,
            Values = new() { ["value"] = c.Volume }
        }).ToList();
    }
    
    // ==================== 第一批新指标 ====================
    // ⚠️ 注意：以下指标需要C++端（prophet_core.dll）支持
    // 当前状态：Native接口已注释，调用会返回空数据
    
    /// <summary>
    /// 计算ATR（平均真实波幅）
    /// 代码量：~25行
    /// 状态：Native接口已实现，可用
    /// </summary>
    public static List<SubChartDataPoint> CalculateATR(
        List<Candlestick> candles,
        int PERIOD = 14)
    {
        if (!NativeCalculator.ValidateCandles(candles, PERIOD, "ATR"))
        {
            return new List<SubChartDataPoint>();
        }
        
        double[] highs = candles.Select(c => c.High).ToArray();
        double[] lows = candles.Select(c => c.Low).ToArray();
        double[] closes = candles.Select(c => c.Close).ToArray();
        
        return NativeCalculator.CalculateSingleOutput(
            candles,
            "ATR",
            () =>
            {
                var result = new ProphetCoreNative.IndicatorResult();
                result.ReturnCode = ProphetCoreNative.Prophet_ATR(
                    highs, lows, closes,
                    highs.Length,
                    PERIOD,
                    ref result
                );
                return result;
            },
            "value"
        );
    }
    
    /// <summary>
    /// 计算MFI（资金流量指标）
    /// 代码量：~30行
    /// 状态：⚠️ Native接口未实现，暂不可用
    /// </summary>
    public static List<SubChartDataPoint> CalculateMFI(
        List<Candlestick> candles,
        int PERIOD = 14)
    {
        if (!IndicatorFeatureFlags.EnableMFI)
        {
            return CreateEmptyDataPoints(candles, "value");
        }
        
        if (!NativeCalculator.ValidateCandles(candles, PERIOD, "MFI"))
        {
            return new List<SubChartDataPoint>();
        }
        
        double[] highs = candles.Select(c => c.High).ToArray();
        double[] lows = candles.Select(c => c.Low).ToArray();
        double[] closes = candles.Select(c => c.Close).ToArray();
        double[] volumes = candles.Select(c => c.Volume).ToArray();
        
        return NativeCalculator.CalculateSingleOutput(
            candles,
            "MFI",
            () =>
            {
                var result = new ProphetCoreNative.IndicatorResult();
                result.ReturnCode = ProphetCoreNative.Prophet_MFI(
                    highs, lows, closes, volumes,
                    highs.Length,
                    PERIOD,
                    ref result
                );
                return result;
            },
            "value"
        );
    }
    
    /// <summary>
    /// 计算多周期MFI（用于多周期对比）
    /// </summary>
    /// <param name="candles">K线列表</param>
    /// <param name="periods">周期数组（如 [7, 14, 21]）</param>
    /// <param name="enabled">是否启用各周期（如[true, true, false]）</param>
    /// <returns>包含多个周期MFI的数据点列表</returns>
    public static List<SubChartDataPoint> CalculateMFIMultiPeriod(
        List<Candlestick> candles,
        int[] periods,
        bool[] enabled)
    {
        if (candles == null || candles.Count == 0 || periods == null || periods.Length == 0)
        {
            return new List<SubChartDataPoint>();
        }
        
        // 初始化结果列表
        var results = candles.Select(c => new SubChartDataPoint { Time = c.Time }).ToList();
        
        // 计算每个周期的MFI
        for (int i = 0; i < periods.Length; i++)
        {
            if (enabled != null && i < enabled.Length && !enabled[i])
            {
                // 如果该周期未启用，填充NaN
                for (int j = 0; j < results.Count; j++)
                {
                    results[j].SetValue($"PERIOD{i + 1}", double.NaN);
                }
                continue;
            }
            
            // 计算该周期的MFI
            var mfiData = CalculateMFI(candles, periods[i]);
            
            // 将MFI数据合并到结果中
            for (int j = 0; j < results.Count && j < mfiData.Count; j++)
            {
                results[j].SetValue($"PERIOD{i + 1}", mfiData[j].GetValue("value"));
            }
        }
        
        return results;
    }
    
    /// <summary>
    /// 计算OBV（能量潮）
    /// 代码量：~25行
    /// 状态：⚠️ Native接口未实现，暂不可用
    /// </summary>
    public static List<SubChartDataPoint> CalculateOBV(List<Candlestick> candles)
    {
        if (!IndicatorFeatureFlags.EnableOBV)
        {
            return CreateEmptyDataPoints(candles, "value");
        }
        
        if (!NativeCalculator.ValidateCandles(candles, 1, "OBV"))
        {
            return new List<SubChartDataPoint>();
        }
        
        double[] closes = candles.Select(c => c.Close).ToArray();
        double[] volumes = candles.Select(c => c.Volume).ToArray();
        
        return NativeCalculator.CalculateSingleOutput(
            candles,
            "OBV",
            () =>
            {
                var result = new ProphetCoreNative.IndicatorResult();
                result.ReturnCode = ProphetCoreNative.Prophet_OBV(
                    closes, volumes,
                    closes.Length,
                    ref result
                );
                return result;
            },
            "value"
        );
    }
    
    /// <summary>
    /// 计算KDJ（随机指标）
    /// 代码量：~40行
    /// </summary>
    public static List<SubChartDataPoint> CalculateKDJ(
        List<Candlestick> candles,
        int PERIOD = 9,
        int kPeriod = 3,
        int dPeriod = 3)
    {
        if (!IndicatorFeatureFlags.EnableKDJ)
        {
            return CreateEmptyDataPoints(candles, "k", "d", "j");
        }
        
        if (!NativeCalculator.ValidateCandles(candles, PERIOD, "KDJ"))
        {
            return new List<SubChartDataPoint>();
        }
        
        double[] highs = candles.Select(c => c.High).ToArray();
        double[] lows = candles.Select(c => c.Low).ToArray();
        double[] closes = candles.Select(c => c.Close).ToArray();
        
        // 使用STOCH计算K和D
        var kdjData = NativeCalculator.CalculateMultiOutput(
            candles,
            "KDJ",
            (results) =>
            {
                ProphetCoreNative.Prophet_STOCH(
                    highs, lows, closes,
                    highs.Length,
                    PERIOD,      // fastK
                    kPeriod,     // slowK
                    dPeriod,     // slowD
                    ref results[0],  // K
                    ref results[1]   // D
                );
            },
            new[] { "k", "d" },
            2
        );
        
        // 计算J值：J = 3K - 2D
        foreach (var point in kdjData)
        {
            if (point.IsValid("k") && point.IsValid("d"))
            {
                double k = point.GetValue("k");
                double d = point.GetValue("d");
                point.SetValue("j", 3 * k - 2 * d);
            }
            else
            {
                point.SetValue("j", double.NaN);
            }
        }
        
        return kdjData;
    }
    
    /// <summary>
    /// 计算StochRSI（随机RSI）
    /// 代码量：~35行
    /// </summary>
    public static List<SubChartDataPoint> CalculateStochRSI(
        List<Candlestick> candles,
        int rsiPeriod = 14,
        int stochPeriod = 14,
        int kPeriod = 3,
        int dPeriod = 3)
    {
        if (!IndicatorFeatureFlags.EnableStochRSI)
        {
            return CreateEmptyDataPoints(candles, "k", "d");
        }
        
        if (!NativeCalculator.ValidateCandles(candles, rsiPeriod, "StochRSI"))
        {
            return new List<SubChartDataPoint>();
        }
        
        double[] closes = candles.Select(c => c.Close).ToArray();
        
        return NativeCalculator.CalculateMultiOutput(
            candles,
            "StochRSI",
            (results) =>
            {
                ProphetCoreNative.Prophet_STOCHRSI(
                    closes,
                    closes.Length,
                    rsiPeriod,
                    kPeriod,
                    dPeriod,
                    ref results[0],  // K
                    ref results[1]   // D
                );
            },
            new[] { "k", "d" },
            2
        );
    }
    
    // ==================== 第二批新指标 ====================
    // ⚠️ 以下所有指标的Native接口都已注释，暂不可用
    // 在工厂方法中有统一的可用性检查，会返回空数据
    
    /// <summary>
    /// 计算CCI（商品通道指标）
    /// 代码量：~30行
    /// 状态：⚠️ Native接口未实现，暂不可用
    /// </summary>
    public static List<SubChartDataPoint> CalculateCCI(
        List<Candlestick> candles,
        int PERIOD = 20)
    {
        if (!IndicatorFeatureFlags.EnableCCI)
        {
            return CreateEmptyDataPoints(candles, "value");
        }
        
        if (!NativeCalculator.ValidateCandles(candles, PERIOD, "CCI"))
        {
            return new List<SubChartDataPoint>();
        }
        
        double[] highs = candles.Select(c => c.High).ToArray();
        double[] lows = candles.Select(c => c.Low).ToArray();
        double[] closes = candles.Select(c => c.Close).ToArray();
        
        return NativeCalculator.CalculateSingleOutput(
            candles,
            "CCI",
            () =>
            {
                var result = new ProphetCoreNative.IndicatorResult();
                result.ReturnCode = ProphetCoreNative.Prophet_CCI(
                    highs, lows, closes,
                    highs.Length,
                    PERIOD,
                    ref result
                );
                return result;
            },
            "value"
        );
    }
    
    /// <summary>
    /// 计算多周期CCI（用于多周期对比）
    /// </summary>
    /// <param name="candles">K线列表</param>
    /// <param name="periods">周期数组（如 [14, 20, 50]）</param>
    /// <param name="enabled">是否启用各周期（如[true, true, false]）</param>
    /// <returns>包含多个周期CCI的数据点列表</returns>
    public static List<SubChartDataPoint> CalculateCCIMultiPeriod(
        List<Candlestick> candles,
        int[] periods,
        bool[] enabled)
    {
        if (candles == null || candles.Count == 0 || periods == null || periods.Length == 0)
        {
            return new List<SubChartDataPoint>();
        }
        
        // 初始化结果列表
        var results = candles.Select(c => new SubChartDataPoint { Time = c.Time }).ToList();
        
        // 计算每个周期的CCI
        for (int i = 0; i < periods.Length; i++)
        {
            if (enabled != null && i < enabled.Length && !enabled[i])
            {
                // 如果该周期未启用，填充NaN
                for (int j = 0; j < results.Count; j++)
                {
                    results[j].SetValue($"PERIOD{i + 1}", double.NaN);
                }
                continue;
            }
            
            // 计算该周期的CCI
            var cciData = CalculateCCI(candles, periods[i]);
            
            // 将CCI数据合并到结果中
            for (int j = 0; j < results.Count && j < cciData.Count; j++)
            {
                results[j].SetValue($"PERIOD{i + 1}", cciData[j].GetValue("value"));
            }
        }
        
        return results;
    }
    
    /// <summary>
    /// 计算DMI（趋向指标）
    /// 代码量：~35行
    /// </summary>
    public static List<SubChartDataPoint> CalculateDMI(
        List<Candlestick> candles,
        int PERIOD = 14)
    {
        if (!IndicatorFeatureFlags.EnableDMI)
        {
            return CreateEmptyDataPoints(candles, "adx", "plus_di", "minus_di");
        }
        
        if (!NativeCalculator.ValidateCandles(candles, PERIOD, "DMI"))
        {
            return new List<SubChartDataPoint>();
        }
        
        double[] highs = candles.Select(c => c.High).ToArray();
        double[] lows = candles.Select(c => c.Low).ToArray();
        double[] closes = candles.Select(c => c.Close).ToArray();
        
        return NativeCalculator.CalculateMultiOutput(
            candles,
            "DMI",
            (results) =>
            {
                ProphetCoreNative.Prophet_DMI(
                    highs, lows, closes,
                    highs.Length,
                    PERIOD,
                    ref results[0],  // ADX
                    ref results[1],  // +DI
                    ref results[2]   // -DI
                );
            },
            new[] { "adx", "plus_di", "minus_di" },  // 与核心引擎统一
            3
        );
    }
    
    /// <summary>
    /// 计算WR（威廉指标）
    /// 代码量：~30行
    /// </summary>
    public static List<SubChartDataPoint> CalculateWR(
        List<Candlestick> candles,
        int PERIOD = 14)
    {
        if (!IndicatorFeatureFlags.EnableWR)
        {
            return CreateEmptyDataPoints(candles, "value");
        }
        
        if (!NativeCalculator.ValidateCandles(candles, PERIOD, "WR"))
        {
            return new List<SubChartDataPoint>();
        }
        
        double[] highs = candles.Select(c => c.High).ToArray();
        double[] lows = candles.Select(c => c.Low).ToArray();
        double[] closes = candles.Select(c => c.Close).ToArray();
        
        return NativeCalculator.CalculateSingleOutput(
            candles,
            "WR",
            () =>
            {
                var result = new ProphetCoreNative.IndicatorResult();
                result.ReturnCode = ProphetCoreNative.Prophet_WILLR(
                    highs, lows, closes,
                    highs.Length,
                    PERIOD,
                    ref result
                );
                return result;
            },
            "value"
        );
    }
    
    /// <summary>
    /// 计算多周期WR（用于多周期对比）
    /// </summary>
    /// <param name="candles">K线列表</param>
    /// <param name="periods">周期数组（如 [9, 14, 21]）</param>
    /// <param name="enabled">是否启用各周期（如[true, true, false]）</param>
    /// <returns>包含多个周期WR的数据点列表</returns>
    public static List<SubChartDataPoint> CalculateWRMultiPeriod(
        List<Candlestick> candles,
        int[] periods,
        bool[] enabled)
    {
        if (candles == null || candles.Count == 0 || periods == null || periods.Length == 0)
        {
            return new List<SubChartDataPoint>();
        }
        
        // 初始化结果列表
        var results = candles.Select(c => new SubChartDataPoint { Time = c.Time }).ToList();
        
        // 计算每个周期的WR
        for (int i = 0; i < periods.Length; i++)
        {
            if (enabled != null && i < enabled.Length && !enabled[i])
            {
                // 如果该周期未启用，填充NaN
                for (int j = 0; j < results.Count; j++)
                {
                    results[j].SetValue($"PERIOD{i + 1}", double.NaN);
                }
                continue;
            }
            
            // 计算该周期的WR
            var wrData = CalculateWR(candles, periods[i]);
            
            // 将WR数据合并到结果中
            for (int j = 0; j < results.Count && j < wrData.Count; j++)
            {
                results[j].SetValue($"PERIOD{i + 1}", wrData[j].GetValue("value"));
            }
        }
        
        return results;
    }
    
    /// <summary>
    /// 计算CMF（蔡金资金流量）
    /// 代码量：~30行
    /// </summary>
    public static List<SubChartDataPoint> CalculateCMF(
        List<Candlestick> candles,
        int PERIOD = 20)
    {
        if (!IndicatorFeatureFlags.EnableCMF)
        {
            return CreateEmptyDataPoints(candles, "value");
        }
        
        if (!NativeCalculator.ValidateCandles(candles, PERIOD, "CMF"))
        {
            return new List<SubChartDataPoint>();
        }
        
        double[] highs = candles.Select(c => c.High).ToArray();
        double[] lows = candles.Select(c => c.Low).ToArray();
        double[] closes = candles.Select(c => c.Close).ToArray();
        double[] volumes = candles.Select(c => c.Volume).ToArray();
        
        return NativeCalculator.CalculateSingleOutput(
            candles,
            "CMF",
            () =>
            {
                var result = new ProphetCoreNative.IndicatorResult();
                result.ReturnCode = ProphetCoreNative.Prophet_CMF(
                    highs, lows, closes, volumes,
                    highs.Length,
                    PERIOD,
                    ref result
                );
                return result;
            },
            "value"
        );
    }
    
    /// <summary>
    /// 计算ROC（变动率指标）
    /// 代码量：~25行
    /// </summary>
    public static List<SubChartDataPoint> CalculateROC(
        List<Candlestick> candles,
        int PERIOD = 12)
    {
        if (!IndicatorFeatureFlags.EnableROC)
        {
            return CreateEmptyDataPoints(candles, "value");
        }
        
        if (!NativeCalculator.ValidateCandles(candles, PERIOD, "ROC"))
        {
            return new List<SubChartDataPoint>();
        }
        
        double[] closes = candles.Select(c => c.Close).ToArray();
        
        return NativeCalculator.CalculateSingleOutput(
            candles,
            "ROC",
            () =>
            {
                var result = new ProphetCoreNative.IndicatorResult();
                result.ReturnCode = ProphetCoreNative.Prophet_ROC(
                    closes,
                    closes.Length,
                    PERIOD,
                    ref result
                );
                return result;
            },
            "value"
        );
    }
    
    // ==================== 第三批新指标 ====================
    
    /// <summary>
    /// 计算EMV（简易波动指标）
    /// 代码量：~30行
    /// </summary>
    public static List<SubChartDataPoint> CalculateEMV(
        List<Candlestick> candles,
        int PERIOD = 14)
    {
        if (!IndicatorFeatureFlags.EnableEMV)
        {
            return CreateEmptyDataPoints(candles, "value");
        }
        
        if (!NativeCalculator.ValidateCandles(candles, PERIOD, "EMV"))
        {
            return new List<SubChartDataPoint>();
        }
        
        double[] highs = candles.Select(c => c.High).ToArray();
        double[] lows = candles.Select(c => c.Low).ToArray();
        double[] volumes = candles.Select(c => c.Volume).ToArray();
        
        return NativeCalculator.CalculateSingleOutput(
            candles,
            "EMV",
            () =>
            {
                var result = new ProphetCoreNative.IndicatorResult();
                result.ReturnCode = ProphetCoreNative.Prophet_EMV(
                    highs, lows, volumes,
                    highs.Length,
                    PERIOD,
                    ref result
                );
                return result;
            },
            "value"
        );
    }
    
    /// <summary>
    /// 计算MTM（动量指标）
    /// 代码量：~25行
    /// </summary>
    public static List<SubChartDataPoint> CalculateMTM(
        List<Candlestick> candles,
        int PERIOD = 12)
    {
        if (!IndicatorFeatureFlags.EnableMTM)
        {
            return CreateEmptyDataPoints(candles, "value");
        }
        
        if (!NativeCalculator.ValidateCandles(candles, PERIOD, "MTM"))
        {
            return new List<SubChartDataPoint>();
        }
        
        double[] closes = candles.Select(c => c.Close).ToArray();
        
        return NativeCalculator.CalculateSingleOutput(
            candles,
            "MTM",
            () =>
            {
                var result = new ProphetCoreNative.IndicatorResult();
                result.ReturnCode = ProphetCoreNative.Prophet_MTM(
                    closes,
                    closes.Length,
                    PERIOD,
                    ref result
                );
                return result;
            },
            "value"
        );
    }
    
    /// <summary>
    /// 计算CMO（Chande动量振荡器）
    /// 代码量：~25行
    /// </summary>
    public static List<SubChartDataPoint> CalculateCMO(
        List<Candlestick> candles,
        int PERIOD = 14)
    {
        if (!IndicatorFeatureFlags.EnableCMO)
        {
            return CreateEmptyDataPoints(candles, "value");
        }
        
        if (!NativeCalculator.ValidateCandles(candles, PERIOD, "CMO"))
        {
            return new List<SubChartDataPoint>();
        }
        
        double[] closes = candles.Select(c => c.Close).ToArray();
        
        return NativeCalculator.CalculateSingleOutput(
            candles,
            "CMO",
            () =>
            {
                var result = new ProphetCoreNative.IndicatorResult();
                result.ReturnCode = ProphetCoreNative.Prophet_CMO(
                    closes,
                    closes.Length,
                    PERIOD,
                    ref result
                );
                return result;
            },
            "value"
        );
    }
    
    /// <summary>
    /// 计算Aroon（阿隆指标）
    /// 代码量：~35行
    /// </summary>
    public static List<SubChartDataPoint> CalculateAroon(
        List<Candlestick> candles,
        int PERIOD = 25)
    {
        if (!IndicatorFeatureFlags.EnableAroon)
        {
            return CreateEmptyDataPoints(candles, "aroon_up", "aroon_down");
        }
        
        if (!NativeCalculator.ValidateCandles(candles, PERIOD, "Aroon"))
        {
            return new List<SubChartDataPoint>();
        }
        
        double[] highs = candles.Select(c => c.High).ToArray();
        double[] lows = candles.Select(c => c.Low).ToArray();
        
        return NativeCalculator.CalculateMultiOutput(
            candles,
            "Aroon",
            (results) =>
            {
                ProphetCoreNative.Prophet_AROON(
                    highs, lows,
                    highs.Length,
                    PERIOD,
                    ref results[0],  // Down (注意：C++端先返回Down)
                    ref results[1]   // Up
                );
            },
            new[] { "aroon_down", "aroon_up" },  // 与核心引擎统一（注意：C++端先返回Down）
            2
        );
    }
    
    // ==================== 工具方法 ====================
    
    /// <summary>
    /// 创建空数据点列表（用于不可用的指标）
    /// 支持单个或多个输出值字段
    /// </summary>
    private static List<SubChartDataPoint> CreateEmptyDataPoints(
        List<Candlestick> candles,
        params string[] outputKeys)
    {
        // 如果没有指定字段名，使用默认"value"
        if (outputKeys == null || outputKeys.Length == 0)
        {
            outputKeys = new[] { "value" };
        }
        
        return candles.Select(c =>
        {
            var point = new SubChartDataPoint { Time = c.Time };
            foreach (var key in outputKeys)
            {
                point.Values[key] = double.NaN;
            }
            return point;
        }).ToList();
    }
    
    /// <summary>
    /// 根据指标ID和配置计算指标数据
    /// 这是一个工厂方法，根据指标ID自动调用对应的计算方法
    /// </summary>
    /// <param name="indicatorId">指标ID（如"rsi"或"macd"）</param>
    /// <param name="candles">K线列表</param>
    /// <param name="config">指标配置</param>
    /// <returns>指标数据点列表</returns>
    public static List<SubChartDataPoint> Calculate(
        string indicatorId,
        List<Candlestick> candles,
        SubChartIndicatorConfig config)
    {
        if (candles == null || candles.Count == 0)
        {
            return new List<SubChartDataPoint>();
        }
        
        try
        {
            // 检查指标是否可用
            if (!IndicatorFeatureFlags.IsIndicatorAvailable(indicatorId))
            {
                // 返回空数据而不是抛出异常
                var metadata = SubChartIndicatorRegistry.GetMetadata(indicatorId);
                if (metadata != null && metadata.OutputKeys.Count > 1)
                {
                    return CreateEmptyDataPoints(candles, metadata.OutputKeys.ToArray());
                }
                return CreateEmptyDataPoints(candles, "value");
            }
            
            return indicatorId.ToLower() switch
            {
                // 已重构的指标（可用）
                "rsi" => config.GetParameter("multiPeriod", false)
                    ? CalculateRSIMultiPeriod(
                        candles,
                        new[] {
                            config.GetParameter("PERIOD1", 7),
                            config.GetParameter("PERIOD2", 14),
                            config.GetParameter("PERIOD3", 21)
                        },
                        new[] {
                            config.GetParameter("enabled1", true),
                            config.GetParameter("enabled2", true),
                            config.GetParameter("enabled3", true)
                        })
                    : CalculateRSI(candles, config.GetParameter("PERIOD", 14)),
                
                "macd" => CalculateMACD(
                    candles,
                    config.GetParameter("fastPeriod", 12),
                    config.GetParameter("slowPeriod", 26),
                    config.GetParameter("signalPeriod", 9)
                ),
                
                "volume" => CalculateVolume(candles),
                
                // 第一批新指标
                "atr" => CalculateATR(
                    candles,
                    config.GetParameter("PERIOD", 14)
                ),
                
                "mfi" => config.GetParameter("multiPeriod", false)
                    ? CalculateMFIMultiPeriod(
                        candles,
                        new[] {
                            config.GetParameter("PERIOD1", 7),
                            config.GetParameter("PERIOD2", 14),
                            config.GetParameter("PERIOD3", 21)
                        },
                        new[] {
                            config.GetParameter("enabled1", true),
                            config.GetParameter("enabled2", true),
                            config.GetParameter("enabled3", true)
                        })
                    : CalculateMFI(candles, config.GetParameter("PERIOD", 14)),
                
                "obv" => CalculateOBV(candles),
                
                "kdj" => CalculateKDJ(
                    candles,
                    config.GetParameter("PERIOD", 9),
                    config.GetParameter("kPeriod", 3),
                    config.GetParameter("dPeriod", 3)
                ),
                
                "stochrsi" => CalculateStochRSI(
                    candles,
                    config.GetParameter("rsiPeriod", 14),
                    config.GetParameter("stochPeriod", 14),
                    config.GetParameter("kPeriod", 3),
                    config.GetParameter("dPeriod", 3)
                ),
                
                // 第二批新指标
                "cci" => config.GetParameter("multiPeriod", false)
                    ? CalculateCCIMultiPeriod(
                        candles,
                        new[] {
                            config.GetParameter("PERIOD1", 14),
                            config.GetParameter("PERIOD2", 20),
                            config.GetParameter("PERIOD3", 50)
                        },
                        new[] {
                            config.GetParameter("enabled1", true),
                            config.GetParameter("enabled2", true),
                            config.GetParameter("enabled3", true)
                        })
                    : CalculateCCI(
                        candles,
                        config.GetParameter("PERIOD", 20)
                    ),
                
                "dmi" => CalculateDMI(
                    candles,
                    config.GetParameter("PERIOD", 14)
                ),
                
                "wr" => config.GetParameter("multiPeriod", false)
                    ? CalculateWRMultiPeriod(
                        candles,
                        new[] {
                            config.GetParameter("PERIOD1", 9),
                            config.GetParameter("PERIOD2", 14),
                            config.GetParameter("PERIOD3", 21)
                        },
                        new[] {
                            config.GetParameter("enabled1", true),
                            config.GetParameter("enabled2", true),
                            config.GetParameter("enabled3", true)
                        })
                    : CalculateWR(
                        candles,
                        config.GetParameter("PERIOD", 14)
                    ),
                
                "cmf" => CalculateCMF(
                    candles,
                    config.GetParameter("PERIOD", 20)
                ),
                
                "roc" => CalculateROC(
                    candles,
                    config.GetParameter("PERIOD", 12)
                ),
                
                // 第三批新指标
                "emv" => CalculateEMV(
                    candles,
                    config.GetParameter("PERIOD", 14)
                ),
                
                "mtm" => CalculateMTM(
                    candles,
                    config.GetParameter("PERIOD", 12)
                ),
                
                "cmo" => CalculateCMO(
                    candles,
                    config.GetParameter("PERIOD", 14)
                ),
                
                "aroon" => CalculateAroon(
                    candles,
                    config.GetParameter("PERIOD", 25)
                ),
                
                _ => throw new NotImplementedException($"指标 {indicatorId} 尚未实现")
            };
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"计算副图指标失败: {ex.Message}");
            return new List<SubChartDataPoint>();
        }
    }
    
    /// <summary>
    /// 批量计算多个指标
    /// </summary>
    /// <param name="candles">K线列表</param>
    /// <param name="configs">指标配置列表</param>
    /// <returns>指标ID -> 数据点列表的字典</returns>
    public static Dictionary<string, List<SubChartDataPoint>> CalculateMultiple(
        List<Candlestick> candles,
        IEnumerable<SubChartIndicatorConfig> configs)
    {
        var results = new Dictionary<string, List<SubChartDataPoint>>();
        
        foreach (var config in configs.Where(c => c.IsEnabled))
        {
            try
            {
                var data = Calculate(config.Id, candles, config);
                if (data != null && data.Count > 0)
                {
                    results[config.Id] = data;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"清理副图数据失败: {ex.Message}");
            }
        }
        
        return results;
    }
    
    /// <summary>
    /// 创建带配置的数据系列
    /// </summary>
    public static SubChartSeries CreateSeries(
        string indicatorId,
        List<Candlestick> candles,
        SubChartIndicatorConfig config)
    {
        var metadata = SubChartIndicatorRegistry.GetMetadata(indicatorId);
        if (metadata == null)
        {
            throw new ArgumentException($"未找到指标元数据: {indicatorId}");
        }
        
        var data = Calculate(indicatorId, candles, config);
        
        return new SubChartSeries
        {
            Id = indicatorId,
            Name = metadata.Name,
            Data = data,
            Type = metadata.Type,
            Config = config
        };
    }
}

