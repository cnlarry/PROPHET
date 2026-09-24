using System.Collections.Generic;

namespace Prophet.Client.Models;

/// <summary>
/// 市场数据包（简化版 - 新框架）
/// 只包含K线数据，指标由框架自己计算
/// </summary>
public class MarketDataPackage
{
    /// <summary>
    /// 交易对符号
    /// </summary>
    public string Symbol { get; set; } = "BTCUSDT";
    
    /// <summary>
    /// 时间周期
    /// </summary>
    public TimeFrame TimeFrame { get; set; } = TimeFrame.M5;
    
    /// <summary>
    /// K线数据（唯一的数据源）
    /// </summary>
    public List<Candlestick> Candles { get; set; } = new();
}

