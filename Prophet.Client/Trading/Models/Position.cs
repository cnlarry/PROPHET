using System;
using Prophet.Client.Trading.Exchanges;

namespace Prophet.Client.Trading.Models;

/// <summary>
/// 持仓信息
/// </summary>
public class Position
{
    /// <summary>
    /// 交易对
    /// </summary>
    public string Symbol { get; set; } = string.Empty;
    
    /// <summary>
    /// 持仓方向
    /// </summary>
    public PositionSide Side { get; set; }
    
    /// <summary>
    /// 持仓数量
    /// </summary>
    public decimal Quantity { get; set; }
    
    /// <summary>
    /// 开仓均价
    /// </summary>
    public decimal EntryPrice { get; set; }
    
    /// <summary>
    /// 标记价格
    /// </summary>
    public decimal MarkPrice { get; set; }
    
    /// <summary>
    /// 强平价格
    /// </summary>
    public decimal LiquidationPrice { get; set; }
    
    /// <summary>
    /// 未实现盈亏
    /// </summary>
    public decimal UnrealizedPnl { get; set; }
    
    /// <summary>
    /// 杠杆倍数
    /// </summary>
    public decimal Leverage { get; set; }
    
    /// <summary>
    /// 保证金
    /// </summary>
    public decimal Margin { get; set; }
    
    /// <summary>
    /// 更新时间
    /// </summary>
    public DateTime UpdateTime { get; set; }
}

