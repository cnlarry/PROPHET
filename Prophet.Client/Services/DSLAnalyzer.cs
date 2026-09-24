using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Prophet.Client.Services;

/// <summary>
/// DSL 代码分析器
/// 用于从 DSL 代码中提取时间框架等元信息
/// </summary>
public static class DSLAnalyzer
{
    /// <summary>
    /// 从 DSL 代码中提取所有使用的时间框架
    /// </summary>
    /// <param name="dslCode">DSL 代码</param>
    /// <returns>时间框架集合（如 ["5m", "15m", "1h"]）</returns>
    /// <example>
    /// var timeframes = DSLAnalyzer.ExtractTimeframes("ALL { $(5m).RSI().value < 30, $(1h).MACD().trend = BULLISH } = BUY;");
    /// // 结果: ["5m", "1h"]
    /// </example>
    public static HashSet<string> ExtractTimeframes(string dslCode)
    {
        if (string.IsNullOrWhiteSpace(dslCode))
        {
            throw new ArgumentException("DSL 代码不能为空", nameof(dslCode));
        }
        
        var timeframes = new HashSet<string>();
        
        // 正则匹配 $(5m), $(15m), $(1h), $(4h), $(1d) 等模式
        // 支持的时间单位：m(分钟), h(小时), d(天), w(周), M(月)
        var regex = new Regex(@"\$\((\d+[mhdwM])\)");
        var matches = regex.Matches(dslCode);
        
        foreach (Match match in matches)
        {
            timeframes.Add(match.Groups[1].Value);
        }
        
        if (timeframes.Count == 0)
        {
            throw new InvalidOperationException(
                "DSL代码中未找到任何时间框架标记。\n" +
                "请确保使用正确的语法，例如：$(5m), $(1h), $(1d) 等"
            );
        }
        
        return timeframes;
    }
    
    /// <summary>
    /// 计算实际需要加载的时间范围（包含300根K线窗口）
    /// </summary>
    /// <param name="dslCode">DSL 代码（已不再使用，保留以保持接口兼容性）</param>
    /// <param name="timeframe">时间框架</param>
    /// <param name="backtestStart">回测开始时间</param>
    /// <param name="backtestEnd">回测结束时间</param>
    /// <returns>实际加载的开始和结束时间</returns>
    public static (DateTime start, DateTime end) CalculateDateRange(
        string dslCode,
        string timeframe,
        DateTime backtestStart,
        DateTime backtestEnd)
    {
        // v10.0: 固定窗口大小为300根K线（完全足够计算任何指标周期）
        const int WINDOW_SIZE = 300;
        
        // 转换为时间跨度
        int timeframeMinutes = TimeframeToMinutes(timeframe);
        int windowMinutes = WINDOW_SIZE * timeframeMinutes;
        
        // 计算实际开始时间（向前扩展窗口）
        DateTime actualStart = backtestStart.AddMinutes(-windowMinutes);
        
        return (actualStart, backtestEnd);
    }
    
    /// <summary>
    /// 时间框架转换为分钟数
    /// </summary>
    /// <param name="timeframe">时间框架（如"5m", "1h", "1d"）</param>
    /// <returns>分钟数</returns>
    public static int TimeframeToMinutes(string timeframe)
    {
        var match = Regex.Match(timeframe, @"^(\d+)([mhdwM])$");
        if (!match.Success)
        {
            throw new ArgumentException($"无效的时间框架格式: {timeframe}。支持的格式：5m, 1h, 1d, 1w, 1M", nameof(timeframe));
        }
        
        int value = int.Parse(match.Groups[1].Value);
        string unit = match.Groups[2].Value;
        
        return unit switch
        {
            "m" => value,                  // 分钟
            "h" => value * 60,            // 小时
            "d" => value * 1440,          // 天
            "w" => value * 10080,         // 周
            "M" => value * 43200,         // 月（近似30天）
            _ => throw new ArgumentException($"不支持的时间单位: {unit}")
        };
    }
    
    /// <summary>
    /// 时间框架转换为可读的描述
    /// </summary>
    public static string TimeframeToDescription(string timeframe)
    {
        var match = Regex.Match(timeframe, @"^(\d+)([mhdwM])$");
        if (!match.Success)
        {
            return timeframe;
        }
        
        int value = int.Parse(match.Groups[1].Value);
        string unit = match.Groups[2].Value;
        
        string unitName = unit switch
        {
            "m" => "分钟",
            "h" => "小时",
            "d" => "天",
            "w" => "周",
            "M" => "月",
            _ => unit
        };
        
        return $"{value}{unitName}";
    }
}

