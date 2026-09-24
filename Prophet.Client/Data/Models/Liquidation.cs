using System;

namespace Prophet.Client.Data.Models;

public sealed class Liquidation
{
    public string Symbol { get; set; } = string.Empty;
    public decimal LongLiquidation { get; set; }
    public decimal ShortLiquidation { get; set; }
    public decimal TotalLiquidation { get; set; }
    public DateTime UpdateTime { get; set; } = DateTime.UtcNow;
}


