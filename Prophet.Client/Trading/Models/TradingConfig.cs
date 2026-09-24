namespace Prophet.Client.Trading.Models;

/// <summary>
/// 交易配置
/// </summary>
public class TradingConfig
{
    /// <summary>
    /// 交易对
    /// </summary>
    public string Symbol { get; set; } = "BTCUSDT";
    
    /// <summary>
    /// K线周期
    /// </summary>
    public string Timeframe { get; set; } = "5m";
    
    /// <summary>
    /// 默认杠杆倍数
    /// </summary>
    public int Leverage { get; set; } = 10;
    
    /// <summary>
    /// 交易所配置
    /// </summary>
    public ExchangeConfig ExchangeConfig { get; set; } = new();
}

