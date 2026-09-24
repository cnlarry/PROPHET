namespace Prophet.Client.Models;

/// <summary>
/// K线样式枚举
/// </summary>
public enum CandleStyle
{
    /// <summary>
    /// 蜡烛图（默认）
    /// </summary>
    Candlestick,
    
    /// <summary>
    /// 美国线（OHLC线）
    /// </summary>
    OHLC,
    
    /// <summary>
    /// 折线图
    /// </summary>
    Line,
    
    /// <summary>
    /// 面积图
    /// </summary>
    Area
}

