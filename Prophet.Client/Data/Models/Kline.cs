using System;

namespace Prophet.Client.Data.Models;

/// <summary>
/// Data Plane 标准K线模型（跨交易所统一）
/// 时间统一使用 UTC
/// </summary>
public sealed class Kline
{
    public string Symbol { get; set; } = string.Empty;
    public string Interval { get; set; } = string.Empty; // 统一使用 Prophet DSL 时间框架字符串，如 1m/5m/1h

    public DateTime OpenTime { get; set; }
    public DateTime? CloseTime { get; set; }

    public decimal Open { get; set; }
    public decimal High { get; set; }
    public decimal Low { get; set; }
    public decimal Close { get; set; }
    public decimal Volume { get; set; }

    public bool IsClosed { get; set; } = true;
}


