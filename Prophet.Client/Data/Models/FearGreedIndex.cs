using System;

namespace Prophet.Client.Data.Models;

public sealed class FearGreedIndex
{
    public int Value { get; set; }
    public string Classification { get; set; } = string.Empty;
    public DateTime UpdateTime { get; set; } = DateTime.UtcNow;
}


