using System;
using Prophet.Client.Models;

namespace Prophet.Client.Backtest.Models;

/// <summary>
/// 订单变化事件参数
/// </summary>
public class OrderChangedEventArgs : EventArgs
{
    /// <summary>
    /// 变化的订单（不应为null，但标记为可空以增加安全性）
    /// </summary>
    public Order? Order { get; set; }
    
    /// <summary>
    /// 变化类型：新增或更新
    /// </summary>
    public OrderChangeType ChangeType { get; set; }
}

/// <summary>
/// 订单变化类型
/// </summary>
public enum OrderChangeType
{
    /// <summary>新增订单</summary>
    Added,
    
    /// <summary>更新订单（如平仓时状态变化）</summary>
    Updated
}
