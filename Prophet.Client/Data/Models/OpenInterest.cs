using System;

namespace Prophet.Client.Data.Models;

public sealed class OpenInterest
{
    public string Symbol { get; set; } = string.Empty;
    public decimal Value { get; set; }
    public DateTime UpdateTime { get; set; } = DateTime.UtcNow;
}


