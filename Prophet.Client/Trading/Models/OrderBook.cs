using System;
using System.Collections.Generic;

namespace Prophet.Client.Trading.Models;

/// <summary>
/// 订单簿
/// </summary>
public class OrderBook
{
    /// <summary>
    /// 交易对
    /// </summary>
    public string Symbol { get; set; } = string.Empty;
    
    /// <summary>
    /// 买单（价格从高到低）
    /// </summary>
    public List<OrderBookLevel> Bids { get; set; } = new();
    
    /// <summary>
    /// 卖单（价格从低到高）
    /// </summary>
    public List<OrderBookLevel> Asks { get; set; } = new();
    
    /// <summary>
    /// 更新时间
    /// </summary>
    public DateTime UpdateTime { get; set; }
    
    /// <summary>
    /// 更新ID
    /// </summary>
    public long UpdateId { get; set; }
    
    /// <summary>
    /// 获取最佳买价
    /// </summary>
    public decimal BestBid => Bids.Count > 0 ? Bids[0].Price : 0;
    
    /// <summary>
    /// 获取最佳卖价
    /// </summary>
    public decimal BestAsk => Asks.Count > 0 ? Asks[0].Price : 0;
    
    /// <summary>
    /// 获取买卖价差
    /// </summary>
    public decimal Spread => BestAsk - BestBid;
}

/// <summary>
/// 订单簿档位
/// </summary>
public class OrderBookLevel
{
    /// <summary>
    /// 价格
    /// </summary>
    public decimal Price { get; set; }
    
    /// <summary>
    /// 数量
    /// </summary>
    public decimal Quantity { get; set; }
}

