using System;

namespace Prophet.Client.Data.Models;

public sealed class TakerLongShortRatio
{
    public string Symbol { get; set; } = string.Empty;
    public string Period { get; set; } = "5m";

    public decimal BuyVol { get; set; }
    public decimal SellVol { get; set; }
    public decimal BuySellRatio { get; set; }

    public DateTime UpdateTime { get; set; } = DateTime.UtcNow;
}


