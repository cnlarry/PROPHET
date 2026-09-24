using System;
using System.Collections.Generic;
using Prophet.Client.Models;

namespace Prophet.Client.Backtest.Models;

/// <summary>
/// 回测结果
/// </summary>
public class BacktestResult
{
    // ========== 基础信息 ==========
    public string BacktestId { get; set; } = string.Empty;
    public string StrategyId { get; set; } = string.Empty;
    public string StrategyName { get; set; } = string.Empty;

    // ========== 版本信息（用于回测追溯/AI分析/版本回归）==========
    public int? VersionId { get; set; }
    public string? VersionString { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public string Interval { get; set; } = string.Empty;
    public BacktestConfig Config { get; set; } = null!;
    public BacktestStatus Status { get; set; }
    public string? ErrorMessage { get; set; }
    
    // ========== 时间信息 ==========
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public TimeSpan Duration { get; set; }
    public DateTime CompletedAt { get; set; }
    
    // ========== 绩效指标（参考backtest.py）==========
    public decimal InitialCapital { get; set; }
    public decimal FinalEquity { get; set; }
    public decimal TotalReturn { get; set; }           // 总收益率
    public decimal AnnualizedReturn { get; set; }      // 年化收益率
    public decimal MaxDrawdown { get; set; }           // 最大回撤
    public decimal SharpeRatio { get; set; }           // 夏普比率
    public decimal SortinoRatio { get; set; }          // 索提诺比率
    public decimal CalmarRatio { get; set; }           // 卡玛比率
    
    // ========== 交易统计 ==========
    public int TotalTrades { get; set; }               // 总交易次数
    public int WinningTrades { get; set; }             // 盈利次数
    public int LosingTrades { get; set; }              // 亏损次数
    public decimal WinRate { get; set; }               // 胜率
    public decimal AvgProfit { get; set; }             // 平均盈利
    public decimal AvgLoss { get; set; }               // 平均亏损
    public decimal ProfitFactor { get; set; }          // 盈亏比
    public int MaxConsecutiveWins { get; set; }        // 最大连续盈利次数
    public int MaxConsecutiveLosses { get; set; }      // 最大连续亏损次数
    public decimal MaxSingleProfit { get; set; }       // 单笔最大盈利
    public decimal MaxSingleLoss { get; set; }         // 单笔最大亏损
    public TimeSpan AvgHoldingTime { get; set; }       // 平均持仓时间
    
    // ========== 时间序列数据（供后续分析）==========
    public List<EquityPoint> EquityCurve { get; set; } = new();        // 权益曲线
    public List<Order> Orders { get; set; } = new();                   // 所有订单明细
    public List<DrawdownPeriod> DrawdownPeriods { get; set; } = new(); // 回撤周期
    public List<SignalEvent> Signals { get; set; } = new();            // 所有信号（含未成交）
    
    // ========== 自定义指标 ==========
    public Dictionary<string, object> CustomMetrics { get; set; } = new();
}

/// <summary>
/// 权益曲线点
/// </summary>
public class EquityPoint
{
    public DateTime Time { get; set; }
    public decimal Equity { get; set; }
    public decimal Cash { get; set; }
    public decimal Position { get; set; }
}

/// <summary>
/// 回撤周期（用于分析最糟糕的时期）
/// </summary>
public class DrawdownPeriod
{
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public decimal DrawdownPercentage { get; set; }
    public TimeSpan Duration { get; set; }
    public decimal RecoveryTime { get; set; }  // 恢复时间（小时）
}

/// <summary>
/// 信号事件（记录所有生成的信号，包括未成交的）
/// v3.0: 整合 Prophet.Core v11.0 的完整信号模型
/// </summary>
public class SignalEvent
{
    // === 基本信息 ===
    public DateTime Time { get; set; }
    public SignalAction Action { get; set; }
    public decimal SignalPrice { get; set; }         // 信号价格
    
    // === 执行状态 ===
    public bool WasExecuted { get; set; }            // 是否成交
    public string? ReasonIfNotExecuted { get; set; } // 未成交原因
    
    // === 信号强度和描述 ===
    public double Confidence { get; set; } = 0.0;    // 信号置信度 (0.0 - 1.0)
    public double Strength { get; set; } = 1.0;      // 信号强度 (0.0 - 1.0，保留字段)
    public string? Description { get; set; }         // 信号描述/触发原因
    
    // === 止损止盈 ===
    public decimal? TakeProfit { get; set; }         // 止盈价格
    public decimal? StopLoss { get; set; }           // 止损价格
    
    // === v11.0 新增：趋势判断 ===
    public string? Trend { get; set; }               // 趋势：BULLISH, BEARISH, NEUTRAL
    
    // === v11.0 新增：JSON 数据快照 ===
    public string? ConfigsJson { get; set; }         // 配置参数快照 (JSON格式)
    public string? IndicatorsJson { get; set; }      // 指标快照 (JSON格式，已废弃)
    public string? IndicatorSnapshotsJson { get; set; }  // 结构化指标快照 (JSON格式，新增)
    public string? DebugJson { get; set; }           // 调试信息 (JSON格式，规则评估详情)
    
    // === v4.0 新增：K线数据快照 ===
    public string? KlinesJson { get; set; }          // K线数据快照 (JSON格式，每个时间框架的当前K线)
    
    // === 关联信息 ===
    public int? CandleIndex { get; set; }            // K线索引
    public int? GlobalIndex { get; set; }            // 全局K线索引
}

