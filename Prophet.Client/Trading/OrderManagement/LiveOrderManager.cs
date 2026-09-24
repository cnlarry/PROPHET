using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Prophet.Client.Backtest.Models;
using Prophet.Client.Backtest.OrderManagement;
using Prophet.Client.Models;
using Prophet.Client.Trading.Exchanges;
using Prophet.Client.Trading.Models;

namespace Prophet.Client.Trading.OrderManagement;

/// <summary>
/// 实盘订单管理器
/// 实现IOrderManager接口，与交易所实时交互
/// </summary>
public class LiveOrderManager : IOrderManager
{
    private readonly IExchange _exchange;
    private readonly LiveOrderConfig _config;
    private readonly LiveOrderSynchronizer _synchronizer;
    private readonly PositionTracker _positionTracker;
    
    private decimal _currentEquity;
    private decimal _currentCash;
    private decimal _currentPosition;
    
    private readonly List<Order> _openOrders = new();
    private readonly List<Order> _closedOrders = new();
    
    // 事件
    public event EventHandler<OrderFilledEventArgs>? OrderFilled;
#pragma warning disable CS0067 // 事件从未使用（预留给止盈止损触发通知功能）
    public event EventHandler<StopLossTriggeredEventArgs>? StopLossTriggered;
    public event EventHandler<TakeProfitTriggeredEventArgs>? TakeProfitTriggered;
#pragma warning restore CS0067
    
    // 属性
    public decimal CurrentEquity => _currentEquity;
    public decimal CurrentCash => _currentCash;
    public decimal CurrentPosition => _currentPosition;
    public List<Order> OpenOrders => _openOrders.ToList();
    public List<Order> ClosedOrders => _closedOrders.ToList();
    
    public LiveOrderManager(
        IExchange exchange,
        LiveOrderConfig config,
        LiveOrderSynchronizer? synchronizer = null,
        PositionTracker? positionTracker = null)
    {
        _exchange = exchange ?? throw new ArgumentNullException(nameof(exchange));
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _synchronizer = synchronizer ?? new LiveOrderSynchronizer(exchange);
        _positionTracker = positionTracker ?? new PositionTracker();
        
        // 订阅交易所事件
        _exchange.OrderUpdated += OnExchangeOrderUpdated;
        _exchange.PositionUpdated += OnExchangePositionUpdated;
    }
    
    /// <summary>
    /// 初始化订单管理器
    /// </summary>
    public void Initialize(decimal initialCapital)
    {
        _currentCash = initialCapital;
        _currentEquity = initialCapital;
        _currentPosition = 0;
        _openOrders.Clear();
        _closedOrders.Clear();
        
        Console.WriteLine($"💰 [LiveOrderManager] 初始化: 初始资金 {initialCapital:F2} USDT");
    }
    
    /// <summary>
    /// 从交易所同步状态
    /// </summary>
    public async Task SynchronizeStateAsync()
    {
        try
        {
            // 同步账户信息
            var account = await _exchange.GetAccountInfoAsync();
            _currentCash = account.AvailableBalance;
            _currentEquity = account.TotalBalance;
            
            // 同步持仓
            var positions = await _exchange.GetPositionsAsync(_config.Symbol);
            _currentPosition = positions.Sum(p => p.Side == PositionSide.Long ? p.Quantity : -p.Quantity);
            
            // 同步未完成订单
            await _synchronizer.SynchronizeOpenOrdersAsync(_openOrders, _config.Symbol);
            
            Console.WriteLine($"✅ [LiveOrderManager] 状态同步完成");
            Console.WriteLine($"   现金: {_currentCash:F2} USDT");
            Console.WriteLine($"   权益: {_currentEquity:F2} USDT");
            Console.WriteLine($"   持仓: {_currentPosition:F8}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ [LiveOrderManager] 状态同步失败: {ex.Message}");
            throw;
        }
    }
    
    /// <summary>
    /// 处理交易信号
    /// </summary>
    public Order? ProcessSignal(Signal signal, Candlestick candle)
    {
        // 同步调用异步方法（在实盘环境中应该使用异步）
        return ProcessSignalAsync(signal, candle).GetAwaiter().GetResult();
    }
    
    /// <summary>
    /// 处理交易信号（异步版本）
    /// </summary>
    public async Task<Order?> ProcessSignalAsync(Signal signal, Candlestick candle)
    {
        try
        {
            // 根据信号动作处理
            return signal.Action switch
            {
                SignalAction.BUY => await ProcessBuySignalAsync(signal, candle),
                SignalAction.SELL => await ProcessSellSignalAsync(signal, candle),
                SignalAction.HOLD => null,
                _ => throw new InvalidOperationException($"未知的信号动作: {signal.Action}")
            };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ [LiveOrderManager] 处理信号失败: {ex.Message}");
            return null;
        }
    }
    
    /// <summary>
    /// 处理买入信号
    /// </summary>
    private async Task<Order?> ProcessBuySignalAsync(Signal signal, Candlestick candle)
    {
        // 如果有空仓，先平仓
        if (_currentPosition < 0)
        {
            return await ClosePositionAsync(candle, "买入信号触发，平空仓");
        }
        
        // 如果已有多仓，检查是否更新止盈止损
        if (_currentPosition > 0)
        {
            if (_config.EnableTrailingStop && signal.StopLoss.HasValue)
            {
                await UpdateStopLossForOpenOrdersAsync(signal.StopLoss.Value);
            }
            
            if (_config.EnableTrailingTakeProfit && signal.TakeProfit.HasValue)
            {
                await UpdateTakeProfitForOpenOrdersAsync(signal.TakeProfit.Value);
            }
            
            Console.WriteLine($"⚠️ [LiveOrderManager] 已有多仓 {_currentPosition:F8}，跳过开仓");
            return null;
        }
        
        // 计算开仓数量
        var quantity = await CalculateOrderQuantityAsync(signal.SignalPrice);
        if (quantity <= 0)
        {
            Console.WriteLine($"⚠️ [LiveOrderManager] 资金不足，无法开仓");
            return null;
        }
        
        // 提交市价买单
        var request = new MarketOrderRequest
        {
            Symbol = _config.Symbol,
            Side = Trading.Exchanges.OrderSide.BUY,
            Quantity = quantity
        };
        
        var result = await _exchange.PlaceMarketOrderAsync(request);
        
        if (!result.Success)
        {
            Console.WriteLine($"❌ [LiveOrderManager] 开仓失败: {result.ErrorMessage}");
            return null;
        }
        
        // 创建订单记录
        var order = new Order
        {
            Id = result.OrderId,
            BacktestId = _config.TradingSessionId,
            Symbol = _config.Symbol,
            Side = Prophet.Client.Models.OrderSide.BUY,
            Type = Prophet.Client.Models.OrderType.MARKET,
            Status = Prophet.Client.Models.OrderStatus.FILLED,
            Leverage = _config.Leverage,
            Quantity = result.FilledQuantity,
            OpenPrice = result.FilledPrice,
            FilledPrice = result.FilledPrice,
            OpenTime = DateTime.UtcNow,
            FilledTime = DateTime.UtcNow,
            TakeProfit = signal.TakeProfit,
            StopLoss = signal.StopLoss,
            Remarks = $"信号价: {signal.SignalPrice:F2}"
        };
        
        // 计算保证金和手续费
        var notionalValue = order.Quantity * order.OpenPrice;
        order.Margin = notionalValue / _config.Leverage;
        order.Fee = notionalValue * _config.TakerFeeRate;
        
        _openOrders.Add(order);
        _currentPosition += order.Quantity;
        _currentCash -= order.Margin + order.Fee;
        
        Console.WriteLine($"✅ [LiveOrderManager] 开多仓成功");
        Console.WriteLine($"   订单ID: {order.Id}");
        Console.WriteLine($"   数量: {order.Quantity:F8}");
        Console.WriteLine($"   成交价: {order.OpenPrice:F2}");
        Console.WriteLine($"   保证金: {order.Margin:F2}");
        Console.WriteLine($"   手续费: {order.Fee:F4}");
        
        // 设置止盈止损
        if (signal.StopLoss.HasValue)
        {
            await _exchange.SetStopLossAsync(_config.Symbol, signal.StopLoss.Value, order.Quantity);
            Console.WriteLine($"   止损价: {signal.StopLoss.Value:F2}");
        }
        
        if (signal.TakeProfit.HasValue)
        {
            await _exchange.SetTakeProfitAsync(_config.Symbol, signal.TakeProfit.Value, order.Quantity);
            Console.WriteLine($"   止盈价: {signal.TakeProfit.Value:F2}");
        }
        
        // 触发事件
        OrderFilled?.Invoke(this, new OrderFilledEventArgs { Order = order });
        
        return order;
    }
    
    /// <summary>
    /// 处理卖出信号
    /// </summary>
    private async Task<Order?> ProcessSellSignalAsync(Signal signal, Candlestick candle)
    {
        // 如果有多仓，先平仓
        if (_currentPosition > 0)
        {
            return await ClosePositionAsync(candle, "卖出信号触发，平多仓");
        }
        
        // 如果已有空仓，检查是否更新止盈止损
        if (_currentPosition < 0)
        {
            if (_config.EnableTrailingStop && signal.StopLoss.HasValue)
            {
                await UpdateStopLossForOpenOrdersAsync(signal.StopLoss.Value);
            }
            
            if (_config.EnableTrailingTakeProfit && signal.TakeProfit.HasValue)
            {
                await UpdateTakeProfitForOpenOrdersAsync(signal.TakeProfit.Value);
            }
            
            Console.WriteLine($"⚠️ [LiveOrderManager] 已有空仓 {_currentPosition:F8}，跳过开仓");
            return null;
        }
        
        // 计算开仓数量
        var quantity = await CalculateOrderQuantityAsync(signal.SignalPrice);
        if (quantity <= 0)
        {
            Console.WriteLine($"⚠️ [LiveOrderManager] 资金不足，无法开仓");
            return null;
        }
        
        // 提交市价卖单
        var request = new MarketOrderRequest
        {
            Symbol = _config.Symbol,
            Side = Trading.Exchanges.OrderSide.SELL,
            Quantity = quantity
        };
        
        var result = await _exchange.PlaceMarketOrderAsync(request);
        
        if (!result.Success)
        {
            Console.WriteLine($"❌ [LiveOrderManager] 开仓失败: {result.ErrorMessage}");
            return null;
        }
        
        // 创建订单记录
        var order = new Order
        {
            Id = result.OrderId,
            BacktestId = _config.TradingSessionId,
            Symbol = _config.Symbol,
            Side = Prophet.Client.Models.OrderSide.SELL,
            Type = Prophet.Client.Models.OrderType.MARKET,
            Status = Prophet.Client.Models.OrderStatus.FILLED,
            Leverage = _config.Leverage,
            Quantity = result.FilledQuantity,
            OpenPrice = result.FilledPrice,
            FilledPrice = result.FilledPrice,
            OpenTime = DateTime.UtcNow,
            FilledTime = DateTime.UtcNow,
            TakeProfit = signal.TakeProfit,
            StopLoss = signal.StopLoss,
            Remarks = $"信号价: {signal.SignalPrice:F2}"
        };
        
        // 计算保证金和手续费
        var notionalValue = order.Quantity * order.OpenPrice;
        order.Margin = notionalValue / _config.Leverage;
        order.Fee = notionalValue * _config.TakerFeeRate;
        
        _openOrders.Add(order);
        _currentPosition -= order.Quantity;
        _currentCash -= order.Margin + order.Fee;
        
        Console.WriteLine($"✅ [LiveOrderManager] 开空仓成功");
        Console.WriteLine($"   订单ID: {order.Id}");
        Console.WriteLine($"   数量: {order.Quantity:F8}");
        Console.WriteLine($"   成交价: {order.OpenPrice:F2}");
        
        // 设置止盈止损（空头用 BUY 平仓）
        if (signal.StopLoss.HasValue)
        {
            await _exchange.SetStopLossAsync(_config.Symbol, signal.StopLoss.Value, order.Quantity, isShortPosition: true);
        }
        
        if (signal.TakeProfit.HasValue)
        {
            await _exchange.SetTakeProfitAsync(_config.Symbol, signal.TakeProfit.Value, order.Quantity, isShortPosition: true);
        }
        
        // 触发事件
        OrderFilled?.Invoke(this, new OrderFilledEventArgs { Order = order });
        
        return order;
    }
    
    /// <summary>
    /// 平仓
    /// </summary>
    private async Task<Order?> ClosePositionAsync(Candlestick candle, string reason)
    {
        var openOrder = _openOrders.FirstOrDefault();
        if (openOrder == null)
        {
            return null;
        }
        
        // 确定平仓方向
        var closeSide = openOrder.Side == Prophet.Client.Models.OrderSide.BUY 
            ? Trading.Exchanges.OrderSide.SELL 
            : Trading.Exchanges.OrderSide.BUY;
        
        // 提交平仓订单
        var request = new MarketOrderRequest
        {
            Symbol = _config.Symbol,
            Side = closeSide,
            Quantity = openOrder.Quantity
        };
        
        var result = await _exchange.PlaceMarketOrderAsync(request);
        
        if (!result.Success)
        {
            Console.WriteLine($"❌ [LiveOrderManager] 平仓失败: {result.ErrorMessage}");
            return null;
        }
        
        // 更新订单
        openOrder.ClosePrice = result.FilledPrice;
        openOrder.CloseTime = DateTime.UtcNow;
        openOrder.Status = Prophet.Client.Models.OrderStatus.CLOSED;
        openOrder.Remarks = reason;
        
        // 计算盈亏
        var profit = openOrder.Side == Prophet.Client.Models.OrderSide.BUY
            ? (openOrder.ClosePrice.Value - openOrder.OpenPrice) * openOrder.Quantity
            : (openOrder.OpenPrice - openOrder.ClosePrice.Value) * openOrder.Quantity;
        
        var closeFee = openOrder.Quantity * openOrder.ClosePrice.Value * _config.TakerFeeRate;
        openOrder.Fee += closeFee;
        openOrder.Profit = profit - openOrder.Fee;
        
        // 更新状态
        _currentCash += openOrder.Margin + (openOrder.Profit ?? 0);
        _currentPosition -= openOrder.Side == Prophet.Client.Models.OrderSide.BUY 
            ? openOrder.Quantity 
            : -openOrder.Quantity;
        
        _openOrders.Remove(openOrder);
        _closedOrders.Add(openOrder);
        
        Console.WriteLine($"✅ [LiveOrderManager] 平仓成功");
        Console.WriteLine($"   订单ID: {openOrder.Id}");
        Console.WriteLine($"   开仓价: {openOrder.OpenPrice:F2}");
        Console.WriteLine($"   平仓价: {openOrder.ClosePrice:F2}");
        Console.WriteLine($"   盈亏: {openOrder.Profit:F2} USDT");
        Console.WriteLine($"   原因: {reason}");
        
        return openOrder;
    }
    
    /// <summary>
    /// 计算开仓数量
    /// </summary>
    private async Task<decimal> CalculateOrderQuantityAsync(decimal price)
    {
        // 脏信号或错误配置可能给出 0/负价格与 0 精度，直接除零会炸，归零并由调用方按资金不足处理
        if (price <= 0 || _config.QuantityPrecision <= 0)
        {
            return await Task.FromResult(0m);
        }

        // 基于可用资金和配置的仓位比例计算
        var availableCash = _currentCash;
        var positionValue = availableCash * _config.PositionSizePercent;
        var quantity = (positionValue * _config.Leverage) / price;
        
        // 向下取整到交易所允许的精度
        quantity = Math.Floor(quantity / _config.QuantityPrecision) * _config.QuantityPrecision;
        
        return await Task.FromResult(quantity);
    }
    
    /// <summary>
    /// 更新止损
    /// </summary>
    public void UpdateStopLoss(Order order, decimal newStopLoss)
    {
        UpdateStopLossAsync(order, newStopLoss).GetAwaiter().GetResult();
    }
    
    public async Task UpdateStopLossAsync(Order order, decimal newStopLoss)
    {
        order.StopLoss = newStopLoss;
        bool isShort = order.Side == Prophet.Client.Models.OrderSide.SELL;
        await _exchange.SetStopLossAsync(_config.Symbol, newStopLoss, order.Quantity, isShort);
        Console.WriteLine($"✅ [LiveOrderManager] 更新止损: {newStopLoss:F2}");
    }
    
    /// <summary>
    /// 更新止盈
    /// </summary>
    public void UpdateTakeProfit(Order order, decimal newTakeProfit)
    {
        UpdateTakeProfitAsync(order, newTakeProfit).GetAwaiter().GetResult();
    }
    
    public async Task UpdateTakeProfitAsync(Order order, decimal newTakeProfit)
    {
        order.TakeProfit = newTakeProfit;
        bool isShort = order.Side == Prophet.Client.Models.OrderSide.SELL;
        await _exchange.SetTakeProfitAsync(_config.Symbol, newTakeProfit, order.Quantity, isShort);
        Console.WriteLine($"✅ [LiveOrderManager] 更新止盈: {newTakeProfit:F2}");
    }
    
    /// <summary>
    /// 更新所有未完成订单的止损
    /// </summary>
    private async Task UpdateStopLossForOpenOrdersAsync(decimal newStopLoss)
    {
        foreach (var order in _openOrders)
        {
            await UpdateStopLossAsync(order, newStopLoss);
        }
    }
    
    /// <summary>
    /// 更新所有未完成订单的止盈
    /// </summary>
    private async Task UpdateTakeProfitForOpenOrdersAsync(decimal newTakeProfit)
    {
        foreach (var order in _openOrders)
        {
            await UpdateTakeProfitAsync(order, newTakeProfit);
        }
    }
    
    /// <summary>
    /// 检查止盈止损（实盘模式下由交易所自动处理，这里主要用于监控）
    /// </summary>
    public void CheckStopLossAndTakeProfit(Candlestick candle)
    {
        // 实盘模式下，止盈止损由交易所自动触发
        // 这里仅做监控和日志记录
        foreach (var order in _openOrders.ToList())
        {
            if (order.StopLoss.HasValue && 
                ((order.Side == Prophet.Client.Models.OrderSide.BUY && (decimal)candle.Low <= order.StopLoss.Value) ||
                 (order.Side == Prophet.Client.Models.OrderSide.SELL && (decimal)candle.High >= order.StopLoss.Value)))
            {
                Console.WriteLine($"⚠️ [LiveOrderManager] 止损可能已触发: 订单 {order.Id}");
            }
            
            if (order.TakeProfit.HasValue &&
                ((order.Side == Prophet.Client.Models.OrderSide.BUY && (decimal)candle.High >= order.TakeProfit.Value) ||
                 (order.Side == Prophet.Client.Models.OrderSide.SELL && (decimal)candle.Low <= order.TakeProfit.Value)))
            {
                Console.WriteLine($"✅ [LiveOrderManager] 止盈可能已触发: 订单 {order.Id}");
            }
        }
    }
    
    /// <summary>
    /// 更新权益
    /// </summary>
    public void UpdateEquity(Candlestick candle)
    {
        // 同步获取最新账户信息
        var account = _exchange.GetAccountInfoAsync().GetAwaiter().GetResult();
        _currentEquity = account.TotalBalance;
        _currentCash = account.AvailableBalance;
    }
    
    /// <summary>
    /// 交易所订单更新事件处理
    /// </summary>
    private void OnExchangeOrderUpdated(object? sender, OrderUpdateEventArgs e)
    {
        Console.WriteLine($"📬 [LiveOrderManager] 收到订单更新: {e.Order.OrderId} - {e.Order.Status}");
        
        // 同步本地订单状态
        var localOrder = _openOrders.FirstOrDefault(o => o.Id == e.Order.OrderId);
        if (localOrder != null)
        {
            // 更新订单状态
            // TODO: 根据交易所订单状态更新本地订单
        }
    }
    
    /// <summary>
    /// 交易所仓位更新事件处理
    /// </summary>
    private void OnExchangePositionUpdated(object? sender, PositionUpdateEventArgs e)
    {
        Console.WriteLine($"📬 [LiveOrderManager] 收到仓位更新: {e.Position.Symbol} - {e.Position.Quantity}");
        
        // 更新本地仓位
        _currentPosition = e.Position.Side == PositionSide.Long 
            ? e.Position.Quantity 
            : -e.Position.Quantity;
    }
}

