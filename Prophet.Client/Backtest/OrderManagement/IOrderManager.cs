using System;
using System.Collections.Generic;
using Prophet.Client.Backtest.Models;
using Prophet.Client.Models;

namespace Prophet.Client.Backtest.OrderManagement;

/// <summary>
/// 订单管理接口（回测和实盘共用）
/// </summary>
public interface IOrderManager
{
    // ========== 初始化 ==========
    void Initialize(decimal initialCapital);
    
    // ========== 订单处理 ==========
    Order? ProcessSignal(Signal signal, Candlestick candle);
    
    void UpdateStopLoss(Order order, decimal newStopLoss);
    void UpdateTakeProfit(Order order, decimal newTakeProfit);
    
    // ========== 止盈止损检查 ==========
    void CheckStopLossAndTakeProfit(Candlestick candle);
    void UpdateEquity(Candlestick candle);
    
    // ========== 状态查询 ==========
    decimal CurrentEquity { get; }
    decimal CurrentCash { get; }
    decimal CurrentPosition { get; }
    List<Order> OpenOrders { get; }
    List<Order> ClosedOrders { get; }
    
    // ========== 事件 ==========
    event EventHandler<OrderFilledEventArgs>? OrderFilled;
    event EventHandler<StopLossTriggeredEventArgs>? StopLossTriggered;
    event EventHandler<TakeProfitTriggeredEventArgs>? TakeProfitTriggered;
}

