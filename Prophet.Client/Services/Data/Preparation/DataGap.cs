using System;
using System.Collections.Generic;
using System.Linq;

namespace Prophet.Client.Services.Data.Preparation;

/// <summary>
/// 数据缺口信息
/// </summary>
public class DataGap
{
    /// <summary>
    /// 缺口开始时间
    /// </summary>
    public DateTime StartTime { get; set; }
    
    /// <summary>
    /// 缺口结束时间
    /// </summary>
    public DateTime EndTime { get; set; }
    
    /// <summary>
    /// 缺失的K线数量
    /// </summary>
    public int MissingCount { get; set; }
    
    /// <summary>
    /// 时间跨度
    /// </summary>
    public TimeSpan Duration => EndTime - StartTime;
    
    /// <summary>
    /// 缺口大小（天数）
    /// </summary>
    public double DurationDays => Duration.TotalDays;
    
    public override string ToString()
    {
        return $"[{StartTime:yyyy-MM-dd HH:mm} ~ {EndTime:yyyy-MM-dd HH:mm}] 缺失 {MissingCount} 根 ({DurationDays:F2}天)";
    }
}

/// <summary>
/// 数据完整性检查结果
/// </summary>
public class DataIntegrityResult
{
    /// <summary>
    /// 交易对
    /// </summary>
    public string Symbol { get; set; } = string.Empty;
    
    /// <summary>
    /// 时间框架
    /// </summary>
    public string Timeframe { get; set; } = string.Empty;
    
    /// <summary>
    /// 检查的时间范围开始
    /// </summary>
    public DateTime StartDate { get; set; }
    
    /// <summary>
    /// 检查的时间范围结束
    /// </summary>
    public DateTime EndDate { get; set; }
    
    /// <summary>
    /// 数据库中现有的K线数量
    /// </summary>
    public int ExistingCount { get; set; }
    
    /// <summary>
    /// 期望的K线数量
    /// </summary>
    public int ExpectedCount { get; set; }
    
    /// <summary>
    /// 数据是否完整（无缺口）
    /// </summary>
    public bool IsComplete => Gaps.Count == 0;
    
    /// <summary>
    /// 缺口列表
    /// </summary>
    public List<DataGap> Gaps { get; set; } = new();
    
    /// <summary>
    /// 总缺失数量
    /// </summary>
    public int TotalMissingCount => Gaps.Sum(g => g.MissingCount);
    
    /// <summary>
    /// 完整性百分比
    /// </summary>
    public double CompletenessPercentage
    {
        get
        {
            if (ExpectedCount == 0) return 0;
            return (double)ExistingCount / ExpectedCount * 100;
        }
    }
    
    public override string ToString()
    {
        return $"{Symbol} {Timeframe}: {CompletenessPercentage:F2}% ({ExistingCount}/{ExpectedCount}) " +
               $"缺口: {Gaps.Count}个";
    }
}

