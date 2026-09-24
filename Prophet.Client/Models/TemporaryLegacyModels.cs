using System;
using System.Collections.Generic;

namespace Prophet.Client.Models;

// ========================================
// 临时保留的旧数据模型（仅供Native计算器使用）
// 这些将在完全迁移到新框架后删除
// ========================================

/// <summary>
/// MA数据点（临时）
/// </summary>
public class MAData
{
    public DateTime Time { get; set; }
    public int PERIOD { get; set; }
    public double Value { get; set; }
}

/// <summary>
/// BOLL数据点（临时）
/// </summary>
public class BOLLData
{
    public DateTime Time { get; set; }
    public double Upper { get; set; }
    public double Middle { get; set; }
    public double Lower { get; set; }
}

/// <summary>
/// Keltner数据点（临时）
/// </summary>
public class KeltnerData
{
    public DateTime Time { get; set; }
    public double Upper { get; set; }
    public double Middle { get; set; }
    public double Lower { get; set; }
}

/// <summary>
/// Ichimoku数据点（临时）
/// </summary>
public class IchimokuData
{
    public DateTime Time { get; set; }
    public double Tenkan { get; set; }
    public double Kijun { get; set; }
    public double SenkouA { get; set; }
    public double SenkouB { get; set; }
    public double Chikou { get; set; }
}

/// <summary>
/// SAR数据点（临时）
/// </summary>
public class SARValue
{
    public DateTime Time { get; set; }
    public double Value { get; set; }
    public bool IsUpTrend { get; set; }
}

/// <summary>
/// MACD数据点（临时）
/// </summary>
public class MACDData
{
    public DateTime Time { get; set; }
    public double DIF { get; set; }
    public double DEA { get; set; }
    public double Histogram { get; set; }
}

/// <summary>
/// RSI数据点（临时）
/// </summary>
public class RSIData
{
    public DateTime Time { get; set; }
    public double Value { get; set; }
}

/// <summary>
/// ATR数据点（临时）
/// </summary>
public class ATRData
{
    public DateTime Time { get; set; }
    public double Value { get; set; }
}

