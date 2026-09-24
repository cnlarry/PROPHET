using System;
using Prophet.Client.Models;

namespace Prophet.Client.Backtest.Models;

/// <summary>
/// 回测进度事件参数（用于UI进度展示）
/// </summary>
public class BacktestProgressEventArgs : EventArgs
{
    public string BacktestId { get; set; } = string.Empty;
    public int Current { get; set; }                    // 当前K线索引
    public int Total { get; set; }                      // 总K线数量
    public double ProgressPercentage { get; set; }      // 进度百分比 (0-1.0)
    public DateTime CurrentTime { get; set; }           // 当前回测的时间点
    public decimal CurrentPrice { get; set; }           // 当前价格（最新K线的收盘价）
    public decimal CurrentEquity { get; set; }          // 当前权益
    public TimeSpan EstimatedTimeRemaining { get; set; } // 预计剩余时间
    
    /// <summary>
    /// 格式化显示进度信息
    /// </summary>
    public string FormatProgress()
    {
        return $"{Current:N0} / {Total:N0} ({ProgressPercentage * 100:F1}%) - " +
               $"当前时间: {CurrentTime:yyyy-MM-dd HH:mm} - " +
               $"权益: ${CurrentEquity:N2} - " +
               $"剩余: {FormatTimeSpan(EstimatedTimeRemaining)}";
    }
    
    private string FormatTimeSpan(TimeSpan time)
    {
        if (time.TotalMinutes < 1)
            return $"{time.TotalSeconds:F0}秒";
        else if (time.TotalHours < 1)
            return $"{time.TotalMinutes:F0}分钟";
        else
            return $"{time.TotalHours:F1}小时";
    }
}

/// <summary>
/// 信号生成事件参数
/// </summary>
public class SignalGeneratedEventArgs : EventArgs
{
    public string BacktestId { get; set; } = string.Empty;
    public Signal Signal { get; set; } = null!;
    public Candlestick Candle { get; set; } = null!;
    public int CandleIndex { get; set; }
    public int GlobalIndex { get; set; }  // 在全局K线列表中的索引
}

/// <summary>
/// 订单执行事件参数
/// </summary>
public class OrderExecutedEventArgs : EventArgs
{
    public string BacktestId { get; set; } = string.Empty;
    public Order? Order { get; set; }  // 改为可空类型，避免空值问题
}

/// <summary>
/// 订单成交事件参数
/// </summary>
public class OrderFilledEventArgs : EventArgs
{
    public Order Order { get; set; } = null!;
}

/// <summary>
/// 止损触发事件参数
/// </summary>
public class StopLossTriggeredEventArgs : EventArgs
{
    public Order Order { get; set; } = null!;
    public decimal TriggerPrice { get; set; }
}

/// <summary>
/// 止盈触发事件参数
/// </summary>
public class TakeProfitTriggeredEventArgs : EventArgs
{
    public Order Order { get; set; } = null!;
    public decimal TriggerPrice { get; set; }
}

