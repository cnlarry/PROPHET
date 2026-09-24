using System;
using System.Collections.Generic;
using Prophet.Client.ViewModels;

namespace Prophet.Client.Models;

/// <summary>
/// 时间周期枚举
/// </summary>
public enum TimeFrame
{
    /// <summary>
    /// 1分钟
    /// </summary>
    M1,
    
    /// <summary>
    /// 3分钟
    /// </summary>
    M3,
    
    /// <summary>
    /// 5分钟
    /// </summary>
    M5,
    
    /// <summary>
    /// 15分钟
    /// </summary>
    M15,
    
    /// <summary>
    /// 30分钟
    /// </summary>
    M30,
    
    /// <summary>
    /// 1小时
    /// </summary>
    H1,
    
    /// <summary>
    /// 2小时
    /// </summary>
    H2,
    
    /// <summary>
    /// 4小时
    /// </summary>
    H4,
    
    /// <summary>
    /// 6小时
    /// </summary>
    H6,
    
    /// <summary>
    /// 8小时
    /// </summary>
    H8,
    
    /// <summary>
    /// 12小时
    /// </summary>
    H12,
    
    /// <summary>
    /// 1日
    /// </summary>
    D1,
    
    /// <summary>
    /// 1周
    /// </summary>
    W1,
    
    /// <summary>
    /// 1月
    /// </summary>
    MN1
}

/// <summary>
/// 时间周期扩展方法
/// </summary>
public static class TimeFrameExtensions
{
    /// <summary>
    /// 转换为显示字符串
    /// </summary>
    public static string ToDisplayString(this TimeFrame timeFrame)
    {
        return timeFrame switch
        {
            TimeFrame.M1 => "1m",
            TimeFrame.M3 => "3m",
            TimeFrame.M5 => "5m",
            TimeFrame.M15 => "15m",
            TimeFrame.M30 => "30m",
            TimeFrame.H1 => "1h",
            TimeFrame.H2 => "2h",
            TimeFrame.H4 => "4h",
            TimeFrame.H6 => "6h",
            TimeFrame.H8 => "8h",
            TimeFrame.H12 => "12h",
            TimeFrame.D1 => "1d",
            TimeFrame.W1 => "1w",
            TimeFrame.MN1 => "1M",
            _ => timeFrame.ToString()
        };
    }
    
    /// <summary>
    /// 获取对应的分钟数
    /// </summary>
    public static int ToMinutes(this TimeFrame timeFrame)
    {
        return timeFrame switch
        {
            TimeFrame.M1 => 1,
            TimeFrame.M3 => 3,
            TimeFrame.M5 => 5,
            TimeFrame.M15 => 15,
            TimeFrame.M30 => 30,
            TimeFrame.H1 => 60,
            TimeFrame.H2 => 120,
            TimeFrame.H4 => 240,
            TimeFrame.H6 => 360,
            TimeFrame.H8 => 480,
            TimeFrame.H12 => 720,
            TimeFrame.D1 => 1440,
            TimeFrame.W1 => 10080,
            TimeFrame.MN1 => 43200,
            _ => 5
        };
    }
    
    /// <summary>
    /// 获取用于回测的采样频率选项列表
    /// 返回所有支持的时间框架（排除月线，因为通常不用于回测）
    /// </summary>
    public static List<SignalIntervalOption> GetBacktestSamplingIntervals()
    {
        var intervals = new List<SignalIntervalOption>();
        
        // 遍历所有时间框架，排除月线（MN1）
        foreach (TimeFrame tf in Enum.GetValues(typeof(TimeFrame)))
        {
            if (tf == TimeFrame.MN1)
                continue; // 跳过月线，通常不用于回测
            
            var intervalString = tf.ToDisplayString();
            var minutes = tf.ToMinutes();
            
            // 生成友好的显示名称
            string displayName;
            if (minutes < 60)
            {
                displayName = $"{minutes}分钟 ({intervalString})";
            }
            else if (minutes < 1440)
            {
                var hours = minutes / 60;
                displayName = $"{hours}小时 ({intervalString})";
            }
            else if (minutes < 10080)
            {
                var days = minutes / 1440;
                displayName = $"{days}天 ({intervalString})";
            }
            else
            {
                var weeks = minutes / 10080;
                displayName = $"{weeks}周 ({intervalString})";
            }
            
            intervals.Add(new SignalIntervalOption
            {
                Value = intervalString,
                DisplayName = displayName
            });
        }
        
        // 按分钟数排序
        intervals.Sort((a, b) =>
        {
            var tfA = GetTimeFrameFromString(a.Value);
            var tfB = GetTimeFrameFromString(b.Value);
            return tfA.ToMinutes().CompareTo(tfB.ToMinutes());
        });
        
        return intervals;
    }
    
    /// <summary>
    /// 从字符串获取时间框架枚举值
    /// </summary>
    private static TimeFrame GetTimeFrameFromString(string intervalString)
    {
        return intervalString switch
        {
            "1m" => TimeFrame.M1,
            "3m" => TimeFrame.M3,
            "5m" => TimeFrame.M5,
            "15m" => TimeFrame.M15,
            "30m" => TimeFrame.M30,
            "1h" => TimeFrame.H1,
            "2h" => TimeFrame.H2,
            "4h" => TimeFrame.H4,
            "6h" => TimeFrame.H6,
            "8h" => TimeFrame.H8,
            "12h" => TimeFrame.H12,
            "1d" => TimeFrame.D1,
            "1w" => TimeFrame.W1,
            "1M" => TimeFrame.MN1,
            _ => TimeFrame.M5
        };
    }
}

