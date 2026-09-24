using System;
using System.Collections.Generic;
using Prophet.Client.Trading.RiskControl;

namespace Prophet.Client.Trading.Models;

/// <summary>
/// 实盘交易实例状态快照
/// 用于记录实例在某个时间点的完整状态
/// </summary>
public class TradingInstanceSnapshot
{
    /// <summary>
    /// 快照ID
    /// </summary>
    public long Id { get; set; }
    
    /// <summary>
    /// 实例ID
    /// </summary>
    public Guid InstanceId { get; set; }
    
    /// <summary>
    /// 快照时间
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// 实例状态
    /// </summary>
    public TradingInstanceStatus Status { get; set; }
    
    // ========== 账户信息 ==========
    
    /// <summary>
    /// 总权益（USDT）
    /// </summary>
    public decimal TotalEquity { get; set; }
    
    /// <summary>
    /// 可用余额（USDT）
    /// </summary>
    public decimal AvailableBalance { get; set; }
    
    /// <summary>
    /// 占用保证金（USDT）
    /// </summary>
    public decimal UsedMargin { get; set; }
    
    /// <summary>
    /// 未实现盈亏（USDT）
    /// </summary>
    public decimal UnrealizedPnL { get; set; }
    
    /// <summary>
    /// 已实现盈亏（USDT）
    /// </summary>
    public decimal RealizedPnL { get; set; }
    
    /// <summary>
    /// 今日盈亏（USDT）
    /// </summary>
    public decimal TodayPnL { get; set; }
    
    /// <summary>
    /// 今日盈亏百分比
    /// </summary>
    public decimal TodayPnLPercent { get; set; }
    
    // ========== 持仓信息 ==========
    
    /// <summary>
    /// 持仓数量
    /// </summary>
    public int PositionCount { get; set; }
    
    /// <summary>
    /// 挂单数量
    /// </summary>
    public int OpenOrderCount { get; set; }
    
    // ========== 风险指标 ==========
    
    /// <summary>
    /// 当前回撤
    /// </summary>
    public decimal CurrentDrawdown { get; set; }
    
    /// <summary>
    /// 最大回撤
    /// </summary>
    public decimal MaxDrawdown { get; set; }
    
    /// <summary>
    /// 风险等级
    /// </summary>
    public RiskLevel RiskLevel { get; set; }
    
    /// <summary>
    /// 是否触发紧急停止
    /// </summary>
    public bool EmergencyStopActivated { get; set; }
    
    // ========== 交易统计 ==========
    
    /// <summary>
    /// 总交易次数
    /// </summary>
    public int TotalTrades { get; set; }
    
    /// <summary>
    /// 今日交易次数
    /// </summary>
    public int TodayTrades { get; set; }
    
    /// <summary>
    /// 胜率
    /// </summary>
    public decimal WinRate { get; set; }
    
    /// <summary>
    /// 连续亏损次数
    /// </summary>
    public int ConsecutiveLosses { get; set; }
    
    /// <summary>
    /// 连续盈利次数
    /// </summary>
    public int ConsecutiveWins { get; set; }
    
    // ========== 其他信息 ==========
    
    /// <summary>
    /// 错误消息（如果状态为Error）
    /// </summary>
    public string? ErrorMessage { get; set; }
    
    /// <summary>
    /// 运行时长（秒）
    /// </summary>
    public long RunningDurationSeconds { get; set; }
    
    /// <summary>
    /// 创建简化快照（用于列表显示）
    /// </summary>
    public TradingInstanceSnapshotSummary ToSummary()
    {
        return new TradingInstanceSnapshotSummary
        {
            InstanceId = InstanceId,
            Timestamp = Timestamp,
            Status = Status,
            TotalEquity = TotalEquity,
            TodayPnL = TodayPnL,
            TodayPnLPercent = TodayPnLPercent,
            PositionCount = PositionCount,
            RiskLevel = RiskLevel,
            CurrentDrawdown = CurrentDrawdown
        };
    }
}

/// <summary>
/// 实例快照摘要（用于列表显示）
/// </summary>
public class TradingInstanceSnapshotSummary
{
    public Guid InstanceId { get; set; }
    public DateTime Timestamp { get; set; }
    public TradingInstanceStatus Status { get; set; }
    public decimal TotalEquity { get; set; }
    public decimal TodayPnL { get; set; }
    public decimal TodayPnLPercent { get; set; }
    public int PositionCount { get; set; }
    public RiskLevel RiskLevel { get; set; }
    public decimal CurrentDrawdown { get; set; }
}

/// <summary>
/// 实例性能指标
/// </summary>
public class InstancePerformanceMetrics
{
    public Guid InstanceId { get; set; }
    public string InstanceName { get; set; } = string.Empty;
    
    // 盈亏指标
    public decimal TotalReturn { get; set; }
    public decimal TotalReturnPercent { get; set; }
    public decimal MaxDrawdown { get; set; }
    
    // 交易统计
    public int TotalTrades { get; set; }
    public decimal WinRate { get; set; }
    public decimal ProfitFactor { get; set; }
    
    // 风险指标
    public decimal SharpeRatio { get; set; }
    public decimal AverageDailyReturn { get; set; }
    
    // 时间信息
    public TimeSpan TotalRunningTime { get; set; }
    public DateTime FirstTradeAt { get; set; }
    public DateTime LastTradeAt { get; set; }
}

