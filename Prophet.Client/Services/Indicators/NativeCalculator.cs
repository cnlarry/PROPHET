using Prophet.Client.Models;
using Prophet.Client.Services.Indicators.Native;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Prophet.Client.Services.Indicators;

/// <summary>
/// Native计算器包- 统一的Native调用模式
/// 
/// 功能：
/// 1. 统一错误处理和资源释放
/// 2. 统一NaN填充逻辑
/// 3. 消除90%的重复代码
/// 
/// 使用示例：
/// <code>
/// // 单输出指标（RSI、MFI等）
/// var data = NativeCalculator.CalculateSingleOutput(
///     candles, "RSI",
///     () => {
///         var result = new IndicatorResult();
///         result.ReturnCode = Native.Prophet_RSI(closes, length, PERIOD, ref result);
///         return result;
///     },
///     "value"
/// );
/// 
/// // 多输出指标（MACD等）
/// var data = NativeCalculator.CalculateMultiOutput(
///     candles, "MACD",
///     (results) => {
///         Native.Prophet_MACD(closes, length, fast, slow, signal,
///             ref results[0], ref results[1], ref results[2]);
///     },
///     new[] { "dif", "dea", "histogram" },
///     3
/// );
/// </code>
/// </summary>
public static class NativeCalculator
{
    /// <summary>
    /// 调用单输出Native函数（RSI、MFI、ATR、CCI、WR等）
    /// </summary>
    /// <param name="candles">K线列表</param>
    /// <param name="indicatorId">指标ID（用于日志）</param>
    /// <param name="nativeCall">Native调用委托</param>
    /// <param name="outputKey">输出值的key（默认为"value"）</param>
    /// <returns>指标数据点列表</returns>
    public static List<SubChartDataPoint> CalculateSingleOutput(
        List<Candlestick> candles,
        string indicatorId,
        Func<ProphetCoreNative.IndicatorResult> nativeCall,
        string outputKey = "value")
    {
        if (candles == null || candles.Count == 0)
        {
            return new List<SubChartDataPoint>();
        }
        
        var result = new ProphetCoreNative.IndicatorResult();
        
        try
        {
            // 调用Native函数
            result = nativeCall();
            
            // 检查返回码
            if (result.ReturnCode != 0)
            {
                return CreateEmptyDataPoints(candles);
            }

            // 提取结果数据
            double[] values = ProphetCoreNative.CopyResultToArray(result);
            int outBegin = result.OutBegin;

            // 验证数据
            if (values == null || values.Length == 0)
            {
                return CreateEmptyDataPoints(candles);
            }

            // 🔑 关键验证：outBegin + values.Length应该等于candles.Count（TA-Lib标准行为）
            // 如果不等，说明数据对齐有问题，需要调试
            int expectedEnd = outBegin + values.Length;
            if (expectedEnd != candles.Count)
            {
                System.Diagnostics.Debug.WriteLine($"警告[{indicatorId}]：数据对齐异常 - outBegin={outBegin}, values.Length={values.Length}, candles.Count={candles.Count}, expectedEnd={expectedEnd}");
            }

            // 构建数据点列表（确保每个K线都有对应的数据点）
            var data = new List<SubChartDataPoint>(candles.Count);
            
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
                    // 注意：根据TA-Lib标准，这不应该发生（outBegin + values.Length应该等于candles.Count）
                }
                // 如果candleIndex < outBegin，说明前面没有足够历史数据，保持NaN
                
                data.Add(new SubChartDataPoint
                {
                    Time = candles[candleIndex].Time,
                    Values = new() { [outputKey] = value }
                });
            }

            // 验证结果（现在应该总是相等）
            if (data.Count != candles.Count)
            {
                System.Diagnostics.Debug.WriteLine($"警告[{indicatorId}]：数据点数量({data.Count})与K线数量({candles.Count})不匹配");
            }

            return data;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"计算指标失败: {ex.Message}");
            return CreateEmptyDataPoints(candles);
        }
        finally
        {
            // 确保释放Native资源
            try
            {
                ProphetCoreNative.Prophet_FreeResult(ref result);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"释放原生资源失败: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// 调用多输出Native函数（MACD、KDJ、DMI、StochRSI等）
    /// </summary>
    /// <param name="candles">K线列表</param>
    /// <param name="indicatorId">指标ID（用于日志）</param>
    /// <param name="nativeCall">Native调用委托（传入结果数组）</param>
    /// <param name="outputKeys">输出值的key列表（如["dif", "dea", "histogram"]）</param>
    /// <param name="outputCount">输出数量（必须与outputKeys长度一致）</param>
    /// <returns>指标数据点列表</returns>
    public static List<SubChartDataPoint> CalculateMultiOutput(
        List<Candlestick> candles,
        string indicatorId,
        Action<ProphetCoreNative.IndicatorResult[]> nativeCall,
        string[] outputKeys,
        int outputCount)
    {
        if (candles == null || candles.Count == 0)
        {
            return new List<SubChartDataPoint>();
        }
        
        if (outputKeys == null || outputKeys.Length != outputCount)
        {
            return CreateEmptyDataPoints(candles);
        }
        
        var results = new ProphetCoreNative.IndicatorResult[outputCount];
        
        try
        {
            // 初始化结果数组
            for (int i = 0; i < outputCount; i++)
            {
                results[i] = new ProphetCoreNative.IndicatorResult();
            }
            
            // 调用Native函数
            nativeCall(results);
            
            // 检查返回码（只检查第一个结果）
            if (results[0].ReturnCode != 0)
            {
                return CreateEmptyDataPoints(candles, outputKeys);
            }

            // 提取所有输出数据
            var allValues = new double[outputCount][];
            for (int i = 0; i < outputCount; i++)
            {
                allValues[i] = ProphetCoreNative.CopyResultToArray(results[i]);
                
                if (allValues[i] == null || allValues[i].Length == 0)
                {
                    return CreateEmptyDataPoints(candles, outputKeys);
                }
            }
            
            // 🔑 关键修复：每个输出可能有不同的OutBegin（例如DMI中ADX的OutBegin大于DI）
            // 我们需要分别处理每个输出的OutBegin和长度
            int[] outBegins = new int[outputCount];
            for (int i = 0; i < outputCount; i++)
            {
                outBegins[i] = results[i].OutBegin;
            }
            
            // 验证每个输出的数据完整性（OutBegin + Length 应该等于 candles.Count）
            for (int i = 0; i < outputCount; i++)
            {
                int expectedLength = candles.Count - outBegins[i];
                if (allValues[i].Length != expectedLength)
                {
                }
            }
            
            // 构建数据点列表
            var data = new List<SubChartDataPoint>(candles.Count);
            
            // 逐个K线填充数据
            for (int candleIndex = 0; candleIndex < candles.Count; candleIndex++)
            {
                var point = new SubChartDataPoint { Time = candles[candleIndex].Time };
                
                // 为每个输出填充数据
                for (int j = 0; j < outputKeys.Length; j++)
                {
                    // 检查当前K线索引是否在该输出的有效范围
                    if (candleIndex < outBegins[j])
                    {
                        // 该输出还没开始，填充NaN
                        point.Values[outputKeys[j]] = double.NaN;
                    }
                    else
                    {
                        int dataIndex = candleIndex - outBegins[j];
                        if (dataIndex < allValues[j].Length)
                        {
                            // 填充有效数据
                            point.Values[outputKeys[j]] = allValues[j][dataIndex];
                        }
                        else
                        {
                            // 数据索引超出范围，填充NaN
                            point.Values[outputKeys[j]] = double.NaN;
                        }
                    }
                }
                
                data.Add(point);
            }

            // 验证结果
            if (data.Count != candles.Count)
            {
            }

            return data;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"批量计算指标失败: {ex.Message}");
            return CreateEmptyDataPoints(candles, outputKeys);
        }
        finally
        {
            // 确保释放所有Native资源
            for (int i = 0; i < results.Length; i++)
            {
                try
                {
                    ProphetCoreNative.Prophet_FreeResult(ref results[i]);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"释放原生资源失败[{i}]: {ex.Message}");
                }
            }
        }
    }
    
    /// <summary>
    /// 创建空数据点列表（单输出）
    /// </summary>
    private static List<SubChartDataPoint> CreateEmptyDataPoints(
        List<Candlestick> candles,
        string outputKey = "value")
    {
        return candles.Select(c => new SubChartDataPoint
        {
            Time = c.Time,
            Values = new() { [outputKey] = double.NaN }
        }).ToList();
    }
    
    /// <summary>
    /// 创建空数据点列表（多输出）
    /// </summary>
    private static List<SubChartDataPoint> CreateEmptyDataPoints(
        List<Candlestick> candles,
        string[] outputKeys)
    {
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
    /// 验证K线数量
    /// </summary>
    public static bool ValidateCandles(List<Candlestick> candles, int minCount, string indicatorId)
    {
        if (candles == null)
        {
            return false;
        }
        
        if (candles.Count < minCount)
        {
            return false;
        }
        
        return true;
    }
    
    /// <summary>
    /// 计算动态Y轴范围
    /// </summary>
    /// <param name="data">数据点列表</param>
    /// <param name="outputKey">要计算的输出key</param>
    /// <param name="margin">边距比例（默认为0.1）</param>
    /// <returns>(minValue, maxValue)</returns>
    public static (double min, double max) CalculateYRange(
        List<SubChartDataPoint> data,
        string outputKey = "value",
        double margin = 0.1)
    {
        if (data == null || data.Count == 0)
        {
            return (0, 100);
        }
        
        // 提取所有有效数据
        var validValues = data
            .Select(d => d.GetValue(outputKey))
            .Where(v => !double.IsNaN(v) && !double.IsInfinity(v))
            .ToList();
        
        if (validValues.Count == 0)
        {
            return (0, 100);
        }
        
        double min = validValues.Min();
        double max = validValues.Max();
        
        // 添加边距
        double range = max - min;
        if (range < 0.0001) // 避免除零
        {
            range = Math.Abs(max) * 0.1;
            if (range < 0.1) range = 1.0;
        }
        
        min -= range * margin;
        max += range * margin;
        
        return (min, max);
    }
    
    /// <summary>
    /// 计算多个输出的Y轴范围
    /// </summary>
    /// <param name="data">数据点列表</param>
    /// <param name="outputKeys">要计算的输出key列表</param>
    /// <param name="margin">边距比例（默认为0.1）</param>
    /// <returns>(minValue, maxValue)</returns>
    public static (double min, double max) CalculateMultiYRange(
        List<SubChartDataPoint> data,
        string[] outputKeys,
        double margin = 0.1)
    {
        if (data == null || data.Count == 0 || outputKeys == null || outputKeys.Length == 0)
        {
            return (0, 100);
        }
        
        // 提取所有输出的有效数据
        var allValues = new List<double>();
        foreach (var key in outputKeys)
        {
            allValues.AddRange(
                data.Select(d => d.GetValue(key))
                    .Where(v => !double.IsNaN(v) && !double.IsInfinity(v))
            );
        }
        
        if (allValues.Count == 0)
        {
            return (0, 100);
        }
        
        double min = allValues.Min();
        double max = allValues.Max();
        
        // 添加边距
        double range = max - min;
        if (range < 0.0001)
        {
            range = Math.Abs(max) * 0.1;
            if (range < 0.1) range = 1.0;
        }
        
        min -= range * margin;
        max += range * margin;
        
        return (min, max);
    }
}

