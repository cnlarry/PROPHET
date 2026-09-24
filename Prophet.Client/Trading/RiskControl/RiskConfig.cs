namespace Prophet.Client.Trading.RiskControl;

/// <summary>
/// 风险控制配置
/// </summary>
public class RiskConfig
{
    /// <summary>
    /// 初始资金
    /// </summary>
    public decimal InitialCapital { get; set; } = 1000m;
    
    /// <summary>
    /// 最大持仓数量
    /// </summary>
    public int MaxOpenPositions { get; set; } = 3;
    
    /// <summary>
    /// 最大回撤限制（百分比）
    /// </summary>
    public decimal MaxDrawdown { get; set; } = 0.15m; // 15%
    
    /// <summary>
    /// 单笔最大风险（占权益的百分比）
    /// </summary>
    public decimal MaxRiskPerTrade { get; set; } = 0.02m; // 2%
    
    /// <summary>
    /// 仓位大小百分比
    /// </summary>
    public decimal PositionSizePercent { get; set; } = 0.95m; // 95%
    
    /// <summary>
    /// 每日最大交易次数
    /// </summary>
    public int MaxDailyTrades { get; set; } = 10;
    
    /// <summary>
    /// 最小余额（USDT）
    /// </summary>
    public decimal MinBalance { get; set; } = 100m;
    
    /// <summary>
    /// 最大连续亏损次数
    /// </summary>
    public int MaxConsecutiveLosses { get; set; } = 5;
    
    /// <summary>
    /// 超过最大回撤时启用紧急停止
    /// </summary>
    public bool EnableEmergencyStopOnMaxDrawdown { get; set; } = true;
    
    /// <summary>
    /// 连续亏损过多时启用紧急停止
    /// </summary>
    public bool EnableEmergencyStopOnConsecutiveLosses { get; set; } = true;
    
    /// <summary>
    /// 紧急停止时平掉所有持仓
    /// </summary>
    public bool CloseAllPositionsOnEmergencyStop { get; set; } = true;
    
    /// <summary>
    /// 最大仓位风险度（百分比）
    /// </summary>
    public decimal MaxPositionRiskPercent { get; set; } = 0.50m; // 50%
    
    /// <summary>
    /// 单个交易对最大仓位占比
    /// </summary>
    public decimal MaxPositionPerSymbol { get; set; } = 0.30m; // 30%
    
    /// <summary>
    /// 启用实时风险监控
    /// </summary>
    public bool EnableRealtimeMonitoring { get; set; } = true;
    
    /// <summary>
    /// 风险监控间隔（秒）
    /// </summary>
    public int MonitoringIntervalSeconds { get; set; } = 60;
    
    /// <summary>
    /// 最大止损比例（基于保证金的百分比）
    /// 例如：20% 表示最多亏损保证金的20%
    /// </summary>
    public decimal MaxStopLossPercent { get; set; } = 0.20m; // 20%
    
    /// <summary>
    /// 最小止盈比例（基于保证金的百分比）
    /// 例如：30% 表示至少盈利保证金的30%
    /// </summary>
    public decimal MinTakeProfitPercent { get; set; } = 0.30m; // 30%
    
    /// <summary>
    /// 仓位模式：Isolated（逐仓）或 Cross（全仓）
    /// </summary>
    public string MarginMode { get; set; } = "Isolated"; // 或 "Cross"
    
    /// <summary>
    /// 杠杆倍数（1-125倍）
    /// </summary>
    public decimal Leverage { get; set; } = 10m;
}

