using System;
using System.Collections.Generic;
using Prophet.Client.Backtest.Models;
using Prophet.Client.Models;

namespace Prophet.Client.Backtest.Performance;

/// <summary>
/// 绩效分析接口（回测和实盘共用）
/// </summary>
public interface IPerformanceAnalyzer
{
    // ========== 初始化 ==========
    void Initialize(BacktestConfig config);
    
    // ========== 更新 ==========
    void Update(Candlestick candle, decimal currentEquity, decimal currentCash, decimal currentPosition);
    void Update(Candlestick candle, decimal currentEquity);
    
    // ========== 生成报告 ==========
    BacktestResult GenerateReport();
    
    // ========== 记录数据 ==========
    void RecordOrder(Order order);
    void RecordSignal(SignalEvent signal);
    
    // ========== 实时指标 ==========
    decimal CurrentReturn { get; }
    decimal CurrentDrawdown { get; }
    decimal MaxDrawdown { get; }
    
    // ========== 数据访问（只读）==========
    /// <summary>
    /// 获取所有订单的只读视图
    /// </summary>
    IReadOnlyList<Order> Orders { get; }
    
    /// <summary>
    /// 获取所有信号的只读视图
    /// </summary>
    IReadOnlyList<SignalEvent> Signals { get; }
    
    // ========== 事件 ==========
    /// <summary>
    /// 订单变化事件（新增或更新）
    /// </summary>
    event EventHandler<OrderChangedEventArgs>? OrderChanged;
}

