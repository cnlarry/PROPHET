using System;
using System.Collections.Generic;
using Prophet.Client.Models;

namespace Prophet.Client.Backtest.Models;

/// <summary>
/// 订单-信号关联 (🔥 Python backtest.py 核心设计)
/// 记录订单与信号之间的关系: 开仓信号、平仓信号、持仓确认信号
/// </summary>
public class OrderSignal
{
    public string OrderId { get; set; } = string.Empty;
    public int SignalId { get; set; }
    
    /// <summary>
    /// 信号动作类型:
    /// OPEN - 触发开仓
    /// CLOSE - 触发平仓
    /// HOLDING - 持仓期间的确认信号
    /// </summary>
    public OrderSignalAction Action { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

/// <summary>
/// 订单-信号关联动作类型
/// </summary>
public enum OrderSignalAction
{
    /// <summary>触发开仓</summary>
    OPEN,
    
    /// <summary>触发平仓</summary>
    CLOSE,
    
    /// <summary>持仓期间的确认信号</summary>
    HOLDING
}

/// <summary>
/// K线形态 (🔥 Python backtest.py 核心设计)
/// 存储 TA-Lib 识别的 K线形态
/// </summary>
public class KlinePattern
{
    public string Id { get; set; } = string.Empty;
    public int SignalId { get; set; }
    public string BacktestId { get; set; } = string.Empty;
    
    /// <summary>形态名称 (ENGULFING, HAMMER, DOJI 等)</summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>中文描述</summary>
    public string? Description { get; set; }
    
    /// <summary>多空方向</summary>
    public PatternSide Side { get; set; }
    
    /// <summary>TA-Lib 原始值 (±100 或 ±200)</summary>
    public int Raw { get; set; }
    
    /// <summary>绝对分值 (0-200)</summary>
    public int Score { get; set; }
    
    /// <summary>按强度排序 (从1开始)</summary>
    public int Rank { get; set; }
    
    /// <summary>K线开盘时间 (毫秒时间戳)</summary>
    public long KlineOpenTime { get; set; }
    
    /// <summary>K线收盘时间 (毫秒时间戳)</summary>
    public long KlineCloseTime { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

/// <summary>
/// K线形态多空方向
/// </summary>
public enum PatternSide
{
    /// <summary>看涨形态</summary>
    BULLISH,
    
    /// <summary>看跌形态</summary>
    BEARISH,
    
    /// <summary>中性形态</summary>
    NEUTRAL
}

/// <summary>
/// 扩展的信号事件 (与Python backtest.py对齐)
/// 注意：大部分字段已在基类 SignalEvent 中定义，这里只保留扩展字段
/// </summary>
public class SignalEventExtended : SignalEvent
{
    /// <summary>信号ID (用于关联订单)</summary>
    public int? SignalId { get; set; }
    
    /// <summary>策略ID</summary>
    public string? Strategy { get; set; }
    
    /// <summary>关联的K线形态</summary>
    public List<KlinePattern> Patterns { get; set; } = new();
    
    // 以下字段已在基类 SignalEvent 中定义，无需重复：
    // - Confidence (基类)
    // - Description (基类，对应原 Reason)
    // - Trend (基类)
    // - StopLoss (基类)
    // - TakeProfit (基类)
    // - ConfigsJson (基类，对应原 Configs)
    // - IndicatorsJson (基类，对应原 Indicators)
    // - DebugJson (基类，对应原 Debug)
}

/// <summary>
/// 扩展的订单 (与Python backtest.py对齐)
/// </summary>
public class OrderExtended : Order
{
    // Symbol 已在基类 Order 中定义，此处不再重复
    
    /// <summary>保证金（与Python对齐，使用double）</summary>
    public new double? Margin { get; set; }
    
    /// <summary>杠杆倍数（与Python对齐，使用int）</summary>
    public new int? Leverage { get; set; }
    
    /// <summary>手续费 (明确字段)</summary>
    public double HandlingFee { get; set; } = 0;
    
    /// <summary>资金费（与Python对齐，使用double）</summary>
    public new double FundingFee { get; set; } = 0;
    
    /// <summary>净盈亏 (profit - fees)</summary>
    public double Surplus { get; set; } = 0;
    
    /// <summary>是否已平仓</summary>
    public bool Closed { get; set; } = false;
    
    /// <summary>关联的信号</summary>
    public List<OrderSignal> Signals { get; set; } = new();
}

