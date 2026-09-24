using System;

namespace Prophet.Client.Data.Models;

/// <summary>
/// Data Plane 标准Ticker模型（跨交易所统一）
/// </summary>
public sealed class Ticker
{
    public string Symbol { get; set; } = string.Empty;

    public decimal LastPrice { get; set; }
    public decimal PriceChangePercent24h { get; set; }

    public decimal Volume24h { get; set; }
    public decimal QuoteVolume24h { get; set; }

    /// <summary>
    /// 主动买入量（若交易所提供）
    /// </summary>
    public decimal BuyVolume24h { get; set; }

    /// <summary>
    /// 主动卖出量（若交易所提供）
    /// </summary>
    public decimal SellVolume24h { get; set; }

    public decimal HighPrice24h { get; set; }
    public decimal LowPrice24h { get; set; }

    public DateTime UpdateTime { get; set; } = DateTime.UtcNow;
}


