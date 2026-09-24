using System;

namespace Prophet.Client.Models;

/// <summary>
/// 订单
/// </summary>
public class Order
{
    // ========== 基础信息 ==========
    public string Id { get; set; } = string.Empty;
    public string BacktestId { get; set; } = string.Empty; // 所属回测ID
    public OrderSide Side { get; set; }
    public OrderType Type { get; set; }
    public OrderStatus Status { get; set; }
    
    // ========== 杠杆和保证金（币安合约标准）==========
    public decimal Leverage { get; set; } = 10m; // 杠杆倍数，默认10倍
    public decimal Margin { get; set; } // 保证金（quantity × price / leverage + fee）
    
    // ========== 数量和价格 ==========
    public decimal Quantity { get; set; }
    public decimal OpenPrice { get; set; }
    public DateTime OpenTime { get; set; }
    
    public decimal? FilledPrice { get; set; } // 实际成交价格（含滑点）
    public DateTime? FilledTime { get; set; } // 实际成交时间
    
    public decimal? ClosePrice { get; set; }
    public DateTime? CloseTime { get; set; }
    
    // ========== 费用和盈亏 ==========
    public decimal Fee { get; set; } // 手续费（开仓+平仓）
    public decimal FundingFee { get; set; } // 资金费用
    public decimal? Profit { get; set; } // 净盈亏（已扣除手续费和资金费用）
    
    // ========== 止盈止损 ==========
    public decimal? TakeProfit { get; set; }
    public decimal? StopLoss { get; set; }
    public decimal? LiquidationPrice { get; set; }
    
    // ========== 备注 ==========
    public string? Remarks { get; set; }
}
