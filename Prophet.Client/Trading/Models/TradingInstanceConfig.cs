using System;
using Prophet.Client.Trading.RiskControl;

namespace Prophet.Client.Trading.Models;

/// <summary>
/// 实盘交易实例配置
/// 定义单个实盘实例的完整配置信息
/// </summary>
public class TradingInstanceConfig
{
    /// <summary>
    /// 实例唯一标识
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();
    
    /// <summary>
    /// 实例名称（用户自定义）
    /// </summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// 交易所类型
    /// </summary>
    public string Exchange { get; set; } = "Binance";
    
    /// <summary>
    /// 交易对
    /// </summary>
    public string Symbol { get; set; } = "BTCUSDT";
    
    /// <summary>
    /// 时间周期
    /// </summary>
    public string Timeframe { get; set; } = "5m";
    
    /// <summary>
    /// 策略名称
    /// </summary>
    public string StrategyName { get; set; } = string.Empty;
    
    /// <summary>
    /// 策略代码（DSL代码）
    /// </summary>
    public string StrategyCode { get; set; } = string.Empty;
    
    /// <summary>
    /// 策略版本（版本号字符串，如 "v1.0.0"）
    /// </summary>
    public string StrategyVersion { get; set; } = string.Empty;
    
    /// <summary>
    /// 风险配置
    /// </summary>
    public RiskConfig RiskConfig { get; set; } = new();
    
    /// <summary>
    /// 资金配置
    /// </summary>
    public CapitalConfig CapitalConfig { get; set; } = new();
    
    /// <summary>
    /// 交易所配置
    /// </summary>
    public ExchangeConfig ExchangeConfig { get; set; } = new();
    
    /// <summary>
    /// 是否启用
    /// </summary>
    public bool IsEnabled { get; set; } = true;
    
    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// 最后运行时间
    /// </summary>
    public DateTime? LastRunAt { get; set; }
    
    /// <summary>
    /// 自动生成实例名称
    /// </summary>
    public string GenerateDefaultName()
    {
        return $"{Exchange}-{Symbol}-{StrategyName}";
    }
    
    /// <summary>
    /// 克隆配置
    /// </summary>
    public TradingInstanceConfig Clone()
    {
        return new TradingInstanceConfig
        {
            Id = Guid.NewGuid(), // 新ID
            Name = $"{Name} (副本)",
            Exchange = Exchange,
            Symbol = Symbol,
            Timeframe = Timeframe,
            StrategyName = StrategyName,
            StrategyCode = StrategyCode,
            StrategyVersion = StrategyVersion,
            RiskConfig = new RiskConfig
            {
                InitialCapital = RiskConfig.InitialCapital,
                MaxOpenPositions = RiskConfig.MaxOpenPositions,
                MaxDrawdown = RiskConfig.MaxDrawdown,
                MaxRiskPerTrade = RiskConfig.MaxRiskPerTrade,
                PositionSizePercent = RiskConfig.PositionSizePercent,
                MaxDailyTrades = RiskConfig.MaxDailyTrades,
                MinBalance = RiskConfig.MinBalance,
                MaxConsecutiveLosses = RiskConfig.MaxConsecutiveLosses,
                EnableEmergencyStopOnMaxDrawdown = RiskConfig.EnableEmergencyStopOnMaxDrawdown,
                EnableEmergencyStopOnConsecutiveLosses = RiskConfig.EnableEmergencyStopOnConsecutiveLosses,
                CloseAllPositionsOnEmergencyStop = RiskConfig.CloseAllPositionsOnEmergencyStop,
                MaxPositionRiskPercent = RiskConfig.MaxPositionRiskPercent,
                MaxPositionPerSymbol = RiskConfig.MaxPositionPerSymbol,
                EnableRealtimeMonitoring = RiskConfig.EnableRealtimeMonitoring,
                MonitoringIntervalSeconds = RiskConfig.MonitoringIntervalSeconds,
                MaxStopLossPercent = RiskConfig.MaxStopLossPercent,
                MinTakeProfitPercent = RiskConfig.MinTakeProfitPercent,
                MarginMode = RiskConfig.MarginMode,
                Leverage = RiskConfig.Leverage
            },
            CapitalConfig = new CapitalConfig
            {
                InitialCapital = CapitalConfig.InitialCapital,
                MaxCapital = CapitalConfig.MaxCapital,
                ReservedCapital = CapitalConfig.ReservedCapital
            },
            ExchangeConfig = new ExchangeConfig
            {
                ExchangeName = ExchangeConfig.ExchangeName,
                UseTestnet = ExchangeConfig.UseTestnet,
                ApiKey = ExchangeConfig.ApiKey,
                ApiSecret = ExchangeConfig.ApiSecret
            },
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow
        };
    }
}

/// <summary>
/// 资金配置
/// </summary>
public class CapitalConfig
{
    /// <summary>
    /// 初始资金（USDT）
    /// </summary>
    public decimal InitialCapital { get; set; } = 1000m;
    
    /// <summary>
    /// 最大资金限制（USDT）
    /// 0表示不限制
    /// </summary>
    public decimal MaxCapital { get; set; } = 0m;
    
    /// <summary>
    /// 保留资金（USDT）
    /// 不参与交易的预留资金
    /// </summary>
    public decimal ReservedCapital { get; set; } = 0m;
    
    /// <summary>
    /// 可用于交易的资金
    /// </summary>
    public decimal AvailableCapital => InitialCapital - ReservedCapital;
}

