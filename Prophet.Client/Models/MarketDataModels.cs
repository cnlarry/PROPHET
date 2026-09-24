using System;

namespace Prophet.Client.Models;

/// <summary>
/// 24小时Ticker数据
/// </summary>
public class Ticker24hData
{
    public string Symbol { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public decimal PriceChangePercent { get; set; }
    public decimal Volume24h { get; set; }
    public decimal QuoteVolume24h { get; set; }
    public decimal BuyVolume { get; set; }
    public decimal SellVolume { get; set; }
    public long OpenTime { get; set; }
    public long CloseTime { get; set; }
    public DateTime LastUpdateTime { get; set; }
}

/// <summary>
/// 持仓量（Open Interest）数据
/// </summary>
public class OpenInterestData
{
    public string Symbol { get; set; } = string.Empty;
    public decimal OpenInterest { get; set; }
    public DateTime UpdateTime { get; set; }
}

/// <summary>
/// 多空比数据
/// </summary>
public class LongShortRatioData
{
    public string Symbol { get; set; } = string.Empty;
    public string Period { get; set; } = "5m"; // 5m, 15m, 30m, 1h, 2h, 4h, 6h, 12h, 1d
    public decimal LongAccountRatio { get; set; }      // 账户数多空比
    public decimal LongPositionRatio { get; set; }     // 持仓量多空比
    public decimal LongShortRatio { get; set; }        // 综合多空比
    public DateTime UpdateTime { get; set; }
}

/// <summary>
/// 爆仓数据
/// </summary>
public class LiquidationData
{
    public string Symbol { get; set; } = string.Empty;
    public decimal LongLiquidation { get; set; }   // 多单爆仓金额
    public decimal ShortLiquidation { get; set; }  // 空单爆仓金额
    public decimal TotalLiquidation { get; set; }  // 总爆仓金额
    public DateTime UpdateTime { get; set; }
}

/// <summary>
/// 合约主动买卖量数据（Taker Long/Short Ratio）
/// </summary>
public class TakerLongShortRatioData
{
    public string Symbol { get; set; } = string.Empty;
    public string Period { get; set; } = "5m"; // 5m, 15m, 30m, 1h, 2h, 4h, 6h, 12h, 1d
    public decimal BuyVol { get; set; }        // 主动买入量
    public decimal SellVol { get; set; }       // 主动卖出量
    public decimal BuySellRatio { get; set; }  // 买卖比（买入/卖出）
    public DateTime UpdateTime { get; set; }
}

