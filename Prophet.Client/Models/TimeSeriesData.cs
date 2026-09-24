using System;
using System.Text.Json.Serialization;

namespace Prophet.Client.Models;

/// <summary>
/// 恐惧与贪婪指数数据
/// </summary>
public class FearGreedData
{
    [JsonPropertyName("date")]
    public DateTime Date { get; set; }
    
    [JsonPropertyName("value")]
    public int Value { get; set; }
    
    [JsonPropertyName("classification")]
    public string Classification { get; set; } = string.Empty;
}

/// <summary>
/// 资金费率数据
/// </summary>
public class FundingRateData
{
    public string Symbol { get; set; } = string.Empty;
    
    public long CalcTime { get; set; }
    
    public DateTime? CalcTimeStr { get; set; }
    
    public int FundingIntervalHours { get; set; }
    
    public decimal LastFundingRate { get; set; }
}

