using System;

namespace Prophet.Client.Models;

/// <summary>
/// 交易信号
/// </summary>
public class Signal
{
    /// <summary>信号动作</summary>
    public SignalAction Action { get; set; }
    
    /// <summary>信号价格</summary>
    public decimal SignalPrice { get; set; }
    
    /// <summary>止盈价格</summary>
    public decimal? TakeProfit { get; set; }
    
    /// <summary>止损价格</summary>
    public decimal? StopLoss { get; set; }
    
    /// <summary>信号生成时间</summary>
    public DateTime Time { get; set; }
    
    /// <summary>信号强度（0-1，可选）</summary>
    public double Strength { get; set; } = 1.0;
    
    /// <summary>信号描述</summary>
    public string? Description { get; set; }
    
    /// <summary>相关指标数据（可选）</summary>
    public System.Collections.Generic.Dictionary<string, double>? Indicators { get; set; }
    
    // ========== v11.0: 新增调试信息 ==========
    
    /// <summary>趋势判断（BULLISH/BEARISH/NEUTRAL）</summary>
    public string? Trend { get; set; }
    
    /// <summary>调试信息（JSON格式，包含规则评估详情）</summary>
    public string? DebugJson { get; set; }
    
    /// <summary>配置参数快照（JSON格式）</summary>
    public string? ConfigsJson { get; set; }
    
    /// <summary>指标快照（JSON格式，已废弃，使用IndicatorSnapshotsJson）</summary>
    public string? IndicatorsJson { get; set; }
    
    /// <summary>结构化指标快照（JSON格式，新增）</summary>
    public string? IndicatorSnapshotsJson { get; set; }
    
    /// <summary>K线数据快照（JSON格式，v4.0新增）</summary>
    public string? KlinesJson { get; set; }
}
