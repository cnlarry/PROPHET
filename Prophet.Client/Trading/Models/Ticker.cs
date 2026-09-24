using System;

namespace Prophet.Client.Trading.Models;

/// <summary>
/// 行情信息
/// </summary>
public class Ticker
{
    /// <summary>
    /// 交易对
    /// </summary>
    public string Symbol { get; set; } = string.Empty;
    
    /// <summary>
    /// 最新价格
    /// </summary>
    public decimal LastPrice { get; set; }
    
    /// <summary>
    /// 24小时最高价
    /// </summary>
    public decimal HighPrice { get; set; }
    
    /// <summary>
    /// 24小时最低价
    /// </summary>
    public decimal LowPrice { get; set; }
    
    /// <summary>
    /// 24小时成交量
    /// </summary>
    public decimal Volume { get; set; }
    
    /// <summary>
    /// 24小时成交额
    /// </summary>
    public decimal QuoteVolume { get; set; }
    
    /// <summary>
    /// 24小时价格变化
    /// </summary>
    public decimal PriceChange { get; set; }
    
    /// <summary>
    /// 24小时价格变化百分比
    /// </summary>
    public decimal PriceChangePercent { get; set; }
    
    /// <summary>
    /// 更新时间
    /// </summary>
    public DateTime UpdateTime { get; set; }
}

