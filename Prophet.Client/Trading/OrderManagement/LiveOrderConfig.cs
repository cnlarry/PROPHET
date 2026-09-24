using System;

namespace Prophet.Client.Trading.OrderManagement;

/// <summary>
/// 实盘订单配置
/// </summary>
public class LiveOrderConfig
{
    /// <summary>
    /// 交易会话ID
    /// </summary>
    public string TradingSessionId { get; set; } = Guid.NewGuid().ToString();
    
    /// <summary>
    /// 交易对
    /// </summary>
    public string Symbol { get; set; } = "BTCUSDT";
    
    /// <summary>
    /// 杠杆倍数
    /// </summary>
    public decimal Leverage { get; set; } = 10m;
    
    /// <summary>
    /// 仓位大小百分比（占可用资金的比例）
    /// </summary>
    public decimal PositionSizePercent { get; set; } = 0.95m; // 95%
    
    /// <summary>
    /// Taker手续费率
    /// </summary>
    public decimal TakerFeeRate { get; set; } = 0.0004m; // 0.04%
    
    /// <summary>
    /// Maker手续费率
    /// </summary>
    public decimal MakerFeeRate { get; set; } = 0.0002m; // 0.02%
    
    /// <summary>
    /// 数量精度（最小下单数量）
    /// </summary>
    public decimal QuantityPrecision { get; set; } = 0.001m;
    
    /// <summary>
    /// 价格精度
    /// </summary>
    public decimal PricePrecision { get; set; } = 0.01m;
    
    /// <summary>
    /// 启用移动止损
    /// </summary>
    public bool EnableTrailingStop { get; set; } = false;
    
    /// <summary>
    /// 启用移动止盈
    /// </summary>
    public bool EnableTrailingTakeProfit { get; set; } = false;
    
    /// <summary>
    /// 最大持仓数量
    /// </summary>
    public int MaxOpenPositions { get; set; } = 1;
    
    /// <summary>
    /// 最大回撤限制（超过此值停止交易）
    /// </summary>
    public decimal MaxDrawdownLimit { get; set; } = 0.20m; // 20%
    
    /// <summary>
    /// 每日最大交易次数
    /// </summary>
    public int MaxDailyTrades { get; set; } = 20;
    
    /// <summary>
    /// 启用订单同步
    /// </summary>
    public bool EnableOrderSync { get; set; } = true;
    
    /// <summary>
    /// 订单同步间隔（秒）
    /// </summary>
    public int OrderSyncIntervalSeconds { get; set; } = 30;
}

