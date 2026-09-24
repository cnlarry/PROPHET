using System;

namespace Prophet.Client.Data.Models;

public sealed class LongShortRatio
{
    public string Symbol { get; set; } = string.Empty;
    public string Period { get; set; } = "5m";

    public decimal LongAccountRatio { get; set; }
    public decimal LongPositionRatio { get; set; }
    public decimal LongShortRatioValue { get; set; }

    public DateTime UpdateTime { get; set; } = DateTime.UtcNow;
}


