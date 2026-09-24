using System;

namespace Prophet.Client.Models;

/// <summary>
/// K线数据模型
/// </summary>
public class Candlestick
{
    /// <summary>
    /// 时间戳
    /// </summary>
    public DateTime Time { get; set; }
    
    /// <summary>
    /// 开盘价
    /// </summary>
    public double Open { get; set; }
    
    /// <summary>
    /// 最高价
    /// </summary>
    public double High { get; set; }
    
    /// <summary>
    /// 最低价
    /// </summary>
    public double Low { get; set; }
    
    /// <summary>
    /// 收盘价
    /// </summary>
    public double Close { get; set; }
    
    /// <summary>
    /// 成交量
    /// </summary>
    public double Volume { get; set; }
    
    /// <summary>
    /// 成交额（以报价货币计价）
    /// </summary>
    public double? QuoteVolume { get; set; }
    
    /// <summary>
    /// 交易笔数
    /// </summary>
    public long? TradeCount { get; set; }
    
    /// <summary>
    /// 主动买入成交量（以基础货币计价）
    /// </summary>
    public double? TakerBuyVolume { get; set; }
    
    /// <summary>
    /// 主动买入成交额（以报价货币计价）
    /// </summary>
    public double? TakerBuyQuoteVolume { get; set; }
    
    /// <summary>
    /// 是否为阳线（收盘价 >= 开盘价）
    /// </summary>
    public bool IsRising => Close >= Open;
    
    /// <summary>
    /// 实体高度（收盘价和开盘价之间的距离）
    /// </summary>
    public double BodyHeight => Math.Abs(Close - Open);
    
    /// <summary>
    /// 上影线长度
    /// </summary>
    public double UpperShadow => High - Math.Max(Open, Close);
    
    /// <summary>
    /// 下影线长度
    /// </summary>
    public double LowerShadow => Math.Min(Open, Close) - Low;
    
    /// <summary>
    /// 是否已闭合（false表示当前正在进行的K线）
    /// </summary>
    public bool IsClosed { get; set; } = true;
    
    /// <summary>
    /// 🆕 v4.0: 收盘时间（从数据库的close_time字段读取）
    /// </summary>
    public DateTime? CloseTime { get; set; }
}

