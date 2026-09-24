using System;
using System.Collections.Generic;

namespace Prophet.Client.Data.Models;

/// <summary>
/// Data Plane 标准订单簿模型（跨交易所统一）
/// </summary>
public sealed class OrderBook
{
    public string Symbol { get; set; } = string.Empty;

    /// <summary>
    /// 买盘（价格从高到低）
    /// </summary>
    public List<OrderBookLevel> Bids { get; set; } = new();

    /// <summary>
    /// 卖盘（价格从低到高）
    /// </summary>
    public List<OrderBookLevel> Asks { get; set; } = new();

    public DateTime UpdateTime { get; set; } = DateTime.UtcNow;
}

public sealed class OrderBookLevel
{
    public decimal Price { get; set; }
    public decimal Quantity { get; set; }
}


