using System;

namespace Prophet.Client.Data.Models;

public sealed class FundingRate
{
    public string Symbol { get; set; } = string.Empty;
    public decimal Rate { get; set; }
    public DateTime UpdateTime { get; set; } = DateTime.UtcNow;
}

public sealed class FundingRateSnapshot
{
    public string Symbol { get; set; } = string.Empty;
    public decimal Rate { get; set; }
    public decimal? Change24hPercent { get; set; }
    public DateTime UpdateTime { get; set; } = DateTime.UtcNow;
}


