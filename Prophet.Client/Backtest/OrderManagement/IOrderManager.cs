using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Prophet.Client.Backtest.Models;
using Prophet.Client.Models;

namespace Prophet.Client.Backtest.OrderManagement;

/// <summary>
/// 订单管理接口（回测和实盘共用，全异步：实盘路径含网络 IO，禁止同步阻塞）
/// </summary>
public interface IOrderManager
{
    // ========== 初始化 ==========
    void Initialize(decimal initialCapital);
    
    // ========== 订单处理 ==========
    Task<Order?> ProcessSignalAsync(Signal signal, Candlestick candle);
    
    Task UpdateStopLossAsync(Order order, decimal newStopLoss);
    Task UpdateTakeProfitAsync(Order order, decimal newTakeProfit);
    
    // ========== 止盈止损检查 ==========
    void CheckStopLossAndTakeProfit(Candlestick candle);
    Task UpdateEquityAsync(Candlestick candle);
    
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

