using System;
using System.Collections.Generic;
using System.Linq;

namespace Prophet.Client.Models;

/// <summary>
/// 副图指标数据点（通用模型）
/// 使用字典存储多个值，避免为每个指标创建单独的类
/// 
/// 使用示例：
/// - RSI: Values = { ["value"] = 65.5 }
/// - MACD: Values = { ["dif"] = 0.5, ["dea"] = 0.3, ["histogram"] = 0.2 }
/// - KDJ: Values = { ["k"] = 80, ["d"] = 75, ["j"] = 90 }
/// </summary>
public class SubChartDataPoint
{
    /// <summary>
    /// 时间戳
    /// </summary>
    public DateTime Time { get; set; }
    
    /// <summary>
    /// 指标值（支持多个值）
    /// 使用字典存储，key为值的名称（如"value"、"dif"、"k"等），value为实际数值
    /// </summary>
    public Dictionary<string, double> Values { get; set; } = new();
    
    /// <summary>
    /// 获取单个值（便捷方法）
    /// </summary>
    /// <param name="key">值的key，默认为"value"</param>
    /// <returns>对应的值，如果不存在返回NaN</returns>
    public double GetValue(string key = "value")
    {
        return Values.TryGetValue(key, out var val) ? val : double.NaN;
    }
    
    /// <summary>
    /// 设置单个值（便捷方法）
    /// </summary>
    /// <param name="key">值的key</param>
    /// <param name="value">要设置的值</param>
    public void SetValue(string key, double value)
    {
        Values[key] = value;
    }
    
    /// <summary>
    /// 检查是否包含指定的key
    /// </summary>
    public bool HasValue(string key)
    {
        return Values.ContainsKey(key);
    }
    
    /// <summary>
    /// 检查指定key的值是否有效（非NaN）
    /// </summary>
    public bool IsValid(string key = "value")
    {
        return Values.TryGetValue(key, out var val) && !double.IsNaN(val);
    }
}

/// <summary>
/// 副图指标数据系列
/// </summary>
public class SubChartSeries
{
    /// <summary>
    /// 指标唯一标识（如"rsi"、"macd"、"mfi"）
    /// </summary>
    public string Id { get; set; } = "";
    
    /// <summary>
    /// 指标显示名称（如"RSI 相对强弱指标"、"MACD"）
    /// </summary>
    public string Name { get; set; } = "";
    
    /// <summary>
    /// 指标数据点列表
    /// </summary>
    public List<SubChartDataPoint> Data { get; set; } = new();
    
    /// <summary>
    /// 指标类型（决定绘制方式）
    /// </summary>
    public SubChartIndicatorType Type { get; set; }
    
    /// <summary>
    /// 指标配置
    /// </summary>
    public SubChartIndicatorConfig? Config { get; set; }
}

/// <summary>
/// 副图指标类型（决定绘制方式）
/// </summary>
public enum SubChartIndicatorType
{
    /// <summary>
    /// 单线图（RSI、MFI、ATR、CCI、WR等）
    /// 使用Values["value"]或Values["line"]
    /// </summary>
    Line,
    
    /// <summary>
    /// 多线图（KDJ的K/D/J、DMI的ADX/+DI/-DI、StochRSI的K/D等）
    /// 使用Values["k"]、Values["d"]、Values["j"]等
    /// </summary>
    MultiLine,
    
    /// <summary>
    /// 柱状图（Volume、OBV等）
    /// 使用Values["value"]，颜色根据涨跌变化
    /// </summary>
    Histogram,
    
    /// <summary>
    /// 线+柱图（MACD完整版）
    /// 使用Values["dif"]、Values["dea"]（线）和Values["histogram"]（柱）
    /// </summary>
    LineWithHistogram,
    
    /// <summary>
    /// 面积图（某些成交量指标）
    /// 使用Values["value"]，填充区域
    /// </summary>
    Area,
    
    /// <summary>
    /// 双面积图（如Aroon的上涨和下跌区域）
    /// 使用Values["up"]、Values["down"]
    /// </summary>
    DualArea
}

/// <summary>
/// 副图指标配置（通用配置）
/// </summary>
public class SubChartIndicatorConfig
{
    /// <summary>
    /// 指标唯一标识（如"rsi"、"macd"、"mfi"）
    /// </summary>
    public string Id { get; set; } = "";
    
    /// <summary>
    /// 指标显示名称（如"RSI 相对强弱指标"）
    /// </summary>
    public string Name { get; set; } = "";
    
    /// <summary>
    /// 是否启用该指标
    /// </summary>
    public bool IsEnabled { get; set; }
    
    /// <summary>
    /// 动态参数（避免为每个指标添加固定属性）
    /// 
    /// 使用示例：
    /// - RSI: { ["PERIOD"] = 14 }
    /// - MACD: { ["fastPeriod"] = 12, ["slowPeriod"] = 26, ["signalPeriod"] = 9 }
    /// - KDJ: { ["PERIOD"] = 9, ["kPeriod"] = 3, ["dPeriod"] = 3 }
    /// </summary>
    public Dictionary<string, object> Parameters { get; set; } = new();
    
    /// <summary>
    /// 颜色配置（支持多种颜色）
    /// 
    /// 使用示例：
    /// - RSI: { ["line"] = "#F0B90B" }
    /// - MACD: { ["dif"] = "#2196F3", ["dea"] = "#FF9800", ["histogram"] = "#26A69A" }
    /// - KDJ: { ["k"] = "#FFFFFF", ["d"] = "#FFD700", ["j"] = "#FF00FF" }
    /// - Volume: { ["up"] = "#26A69A", ["down"] = "#EF5350" }
    /// </summary>
    public Dictionary<string, string> Colors { get; set; } = new();
    
    /// <summary>
    /// 指标类型
    /// </summary>
    public SubChartIndicatorType Type { get; set; }
    
    /// <summary>
    /// Y轴最小值（NaN表示自动计算）
    /// </summary>
    public double MinValue { get; set; } = double.NaN;
    
    /// <summary>
    /// Y轴最大值（NaN表示自动计算）
    /// </summary>
    public double MaxValue { get; set; } = double.NaN;
    
    /// <summary>
    /// 显示顺序（用于副图排序）
    /// </summary>
    public int DisplayOrder { get; set; }
    
    /// <summary>
    /// 获取参数值（泛型方法）
    /// </summary>
    /// <typeparam name="T">参数类型</typeparam>
    /// <param name="key">参数名</param>
    /// <param name="defaultValue">默认值</param>
    /// <returns>参数值</returns>
    public T GetParameter<T>(string key, T defaultValue = default!)
    {
        if (Parameters.TryGetValue(key, out var value))
        {
            try
            {
                // 处理类型转换
                if (value is T typedValue)
                {
                    return typedValue;
                }
                
                // 尝试转换
                return (T)Convert.ChangeType(value, typeof(T));
            }
            catch
            {
                return defaultValue;
            }
        }
        
        return defaultValue;
    }
    
    /// <summary>
    /// 设置参数值
    /// </summary>
    /// <param name="key">参数名</param>
    /// <param name="value">参数值</param>
    public void SetParameter(string key, object value)
    {
        Parameters[key] = value;
    }
    
    /// <summary>
    /// 获取颜色值
    /// </summary>
    /// <param name="key">颜色key（如"line"、"dif"、"k"等）</param>
    /// <param name="defaultColor">默认颜色</param>
    /// <returns>颜色十六进制字符串</returns>
    public string GetColor(string key, string defaultColor = "#FFFFFF")
    {
        return Colors.TryGetValue(key, out var color) ? color : defaultColor;
    }
    
    /// <summary>
    /// 设置颜色值
    /// </summary>
    /// <param name="key">颜色key</param>
    /// <param name="color">颜色十六进制字符串</param>
    public void SetColor(string key, string color)
    {
        Colors[key] = color;
    }
}

/// <summary>
/// 副图指标集合（用于管理所有启用的副图指标）
/// </summary>
public class SubChartIndicatorCollection
{
    /// <summary>
    /// 所有启用的指标配置
    /// Key: 指标ID（如"rsi"、"macd"）
    /// Value: 指标配置
    /// </summary>
    public Dictionary<string, SubChartIndicatorConfig> Indicators { get; set; } = new();
    
    /// <summary>
    /// 添加或更新指标配置
    /// </summary>
    public void AddOrUpdate(SubChartIndicatorConfig config)
    {
        Indicators[config.Id] = config;
    }
    
    /// <summary>
    /// 移除指标
    /// </summary>
    public void Remove(string indicatorId)
    {
        Indicators.Remove(indicatorId);
    }
    
    /// <summary>
    /// 获取指标配置
    /// </summary>
    public SubChartIndicatorConfig? Get(string indicatorId)
    {
        return Indicators.TryGetValue(indicatorId, out var config) ? config : null;
    }
    
    /// <summary>
    /// 获取所有启用的指标
    /// </summary>
    public IEnumerable<SubChartIndicatorConfig> GetEnabledIndicators()
    {
        return Indicators.Values
            .Where(c => c.IsEnabled)
            .OrderBy(c => c.DisplayOrder);
    }
    
    /// <summary>
    /// 启用的指标数量
    /// </summary>
    public int EnabledCount => Indicators.Values.Count(c => c.IsEnabled);
}

