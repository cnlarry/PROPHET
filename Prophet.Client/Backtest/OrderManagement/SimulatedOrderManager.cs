using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Prophet.Client.Backtest.Models;
using Prophet.Client.Models;
using Prophet.Client.Backtest.Services;

namespace Prophet.Client.Backtest.OrderManagement;

/// <summary>
/// 模拟订单管理器（回测模式）
/// 参考 backtest.py 实现滑点、资金费用等真实交易成本
/// </summary>
public class SimulatedOrderManager : IOrderManager
{
    private readonly BacktestConfig _config;
    private readonly SlippageSimulator _slippageSimulator;
    private readonly FundingFeeCalculator _fundingFeeCalculator;
    private decimal _currentCash;
    private decimal _currentPosition; // 当前持仓数量（正数=多头，负数=空头，0=无持仓）
    private decimal _avgEntryPrice;   // 平均开仓价格
    private readonly List<Order> _openOrders = new();
    private readonly List<Order> _closedOrders = new();
    
    // P2.6: 用于仓位计算的历史数据缓存
    private readonly Queue<Candlestick> _recentCandles = new();
    private const int MAX_CANDLE_HISTORY = 100;  // 保留最近100根K线
    
    public decimal CurrentEquity { get; private set; }
    public decimal CurrentCash => _currentCash;
    public decimal CurrentPosition => _currentPosition;
    public List<Order> OpenOrders => _openOrders.ToList();
    public List<Order> ClosedOrders => _closedOrders.ToList();
    
    // 事件
    public event EventHandler<OrderFilledEventArgs>? OrderFilled;
    public event EventHandler<StopLossTriggeredEventArgs>? StopLossTriggered;
    public event EventHandler<TakeProfitTriggeredEventArgs>? TakeProfitTriggered;
    
    public SimulatedOrderManager(
        BacktestConfig config,
        SlippageSimulator? slippageSimulator = null,
        FundingFeeCalculator? fundingFeeCalculator = null)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        // P1.3: 使用配置创建滑点模拟器（支持多种模式）
        _slippageSimulator = slippageSimulator ?? new SlippageSimulator(config);
        _fundingFeeCalculator = fundingFeeCalculator ?? new FundingFeeCalculator();
    }
    
    /// <summary>
    /// 初始化资金
    /// </summary>
    public void Initialize(decimal initialCapital)
    {
        _currentCash = initialCapital;
        CurrentEquity = initialCapital;
        _currentPosition = 0;
        _avgEntryPrice = 0;
        _openOrders.Clear();
        _closedOrders.Clear();
        
        Console.WriteLine($"💰 初始化订单管理器: 初始资金 {initialCapital:F2} USDT");
    }
    
    /// <summary>
    /// 处理信号并生成订单
    /// 注意：核心引擎只返回 BUY、SELL、HOLD 三种信号
    /// </summary>
    public Task<Order?> ProcessSignalAsync(Signal signal, Candlestick candle)
    {
        // 回测为纯内存计算，无 IO：同步执行后返回已完成任务
        Order? result = signal.Action switch
        {
            SignalAction.BUY => ProcessBuySignal(signal, candle),
            SignalAction.SELL => ProcessSellSignal(signal, candle),
            SignalAction.HOLD => null,  // HOLD信号不触发交易
            _ => throw new InvalidOperationException($"Unknown signal action: {signal.Action}")
        };
        return Task.FromResult(result);
    }
    
    /// <summary>
    /// 处理买入信号（开多仓）
    /// 参考 backtest.py 第829-848行的逻辑
    /// 禁止加仓：如果有同方向持仓，不开仓，跳过
    /// </summary>
    private Order? ProcessBuySignal(Signal signal, Candlestick candle)
    {
        // 如果有空仓（反方向持仓），先平空仓并返回平仓订单
        // 参考 backtest.py 第879-895行：反向信号时只平仓，不开新仓
        if (_currentPosition < 0)
        {
            return ClosePosition(candle, "买入信号触发，平空仓");
        }
        
        // 如果已经有多仓（同方向持仓），禁止加仓，跳过不开仓
        // 只允许更新止盈止损（P2.8: 移动止损/追踪止盈）
        if (_currentPosition > 0)
        {
            // P2.8: 如果启用了移动止损或追踪止盈，更新现有订单的TP/SL
            if (_config.EnableTrailingStop || _config.EnableTrailingTakeProfit)
            {
                UpdateTrailingStopAndTakeProfit(signal, signal.SignalPrice);
            }
            // 禁止加仓：即使有移动止损功能，也不开新仓
            // Console.WriteLine($"⚠️ 忽略买入信号: 当前已有多仓 {_currentPosition:F8}，禁止加仓");
            return null;
        }
        
        // 计算可开仓数量
        var quantity = CalculateOrderQuantity(signal.SignalPrice);
        if (quantity <= 0)
        {
            // Console.WriteLine($"⚠️ 忽略买入信号: 资金不足，当前现金 {_currentCash:F2} USDT");
            return null;
        }
        
        // 应用滑点模拟（参考 backtest.py）
        var executionPrice = _slippageSimulator.CalculateExecutionPrice(
            OrderSide.BUY,
            signal.SignalPrice,
            quantity,
            candle,
            isOpening: true);
        
        // 记录滑点信息
        var slippageStats = _slippageSimulator.GetStatistics(
            OrderSide.BUY,
            signal.SignalPrice,
            executionPrice,
            quantity);
        
        if (slippageStats.SlippageAmount > 0)
        {
            // Console.WriteLine($"   💫 开仓滑点: {slippageStats.SlippageBps:F1}bps " + $"({slippageStats.Direction}, 成本: {slippageStats.SlippageCost:F2})");
        }
        
        // 计算保证金和手续费（币安合约标准）
        var fee = quantity * executionPrice * _config.TakerFeeRate; // 开仓手续费
        var margin = MarginCalculator.CalculateMargin(quantity, executionPrice, _config.Leverage, _config.TakerFeeRate);
        
        // 计算止盈止损（基于保证金比例）
        decimal takeProfit, stopLoss;
        if (signal.TakeProfit.HasValue && signal.TakeProfit.Value > 0 &&
            signal.StopLoss.HasValue && signal.StopLoss.Value > 0)
        {
            // 信号提供了TP/SL，优先使用
            takeProfit = signal.TakeProfit.Value;
            stopLoss = signal.StopLoss.Value;
        }
        else
        {
            // 使用配置的默认值（基于保证金比例）
            if (_config.EnableDefaultTPSL)
            {
                takeProfit = MarginCalculator.CalculateTakeProfit(executionPrice, OrderSide.BUY, _config.Leverage, _config.DefaultTakeProfitPercent);
                stopLoss = MarginCalculator.CalculateStopLoss(executionPrice, OrderSide.BUY, _config.Leverage, _config.DefaultStopLossPercent);
            }
            else
            {
                takeProfit = 0;
                stopLoss = 0;
            }
        }
        
        // 计算强平价（币安合约标准）
        var liquidationPrice = MarginCalculator.CalculateLiquidationPrice(executionPrice, OrderSide.BUY, margin);
        
        // 创建订单
        var order = new Order
        {
            Id = Guid.NewGuid().ToString(),
            BacktestId = _config.RunId,
            Side = OrderSide.BUY,
            Type = OrderType.MARKET,
            Status = OrderStatus.FILLED,
            Leverage = _config.Leverage,  // 记录杠杆倍数
            Margin = margin,  // 记录保证金
            Quantity = quantity,
            OpenPrice = signal.SignalPrice,
            OpenTime = candle.Time,
            FilledPrice = executionPrice,
            FilledTime = candle.Time,
            Fee = fee,  // 开仓手续费
            FundingFee = 0,  // 开仓时资金费用为0
            TakeProfit = takeProfit > 0 ? takeProfit : null,
            StopLoss = stopLoss > 0 ? stopLoss : null,
            LiquidationPrice = liquidationPrice,
            Remarks = $"买入开多 | 杠杆{_config.Leverage}x | 保证金{margin:F2} | 滑点{slippageStats.SlippageBps:F1}bps | 强平{liquidationPrice:F2}"
        };
        
        // 更新持仓和资金（扣除保证金，而不是仓位价值）
        _currentPosition += quantity;
        _avgEntryPrice = executionPrice;
        _currentCash -= margin;  // 扣除保证金（已包含开仓手续费）
        _openOrders.Add(order);
        
        // Console.WriteLine($"📈 买入开多: 数量 {quantity:F8} @ {executionPrice:F2} USDT, 手续费 {fee:F2} USDT");
        // Console.WriteLine($"   剩余资金: {_currentCash:F2} USDT, 持仓: {_currentPosition:F8}");
        
        // 触发事件
        OrderFilled?.Invoke(this, new OrderFilledEventArgs { Order = order });
        
        return order;
    }
    
    /// <summary>
    /// 处理卖出信号（开空仓）
    /// 注意: 现货交易不支持做空，此方法仅用于合约交易
    /// 在现货模式下，SELL信号会平多仓（如果有）
    /// 禁止加仓：如果有同方向持仓，不开仓，跳过
    /// </summary>
    private Order? ProcessSellSignal(Signal signal, Candlestick candle)
    {
        // 如果有多仓（反方向持仓），平多仓并返回订单（现货模式下SELL=平多）
        if (_currentPosition > 0)
        {
            return ClosePosition(candle, "卖出信号触发，平多仓");
        }
        
        // 现货交易不支持做空
        if (!_config.AllowShort)
        {
            // Console.WriteLine($"⚠️ 忽略卖出信号: 当前配置不支持做空（现货模式），且无多仓可平");
            return null;
        }
        
        // 如果已经有空仓（同方向持仓），禁止加仓，跳过不开仓
        // 只允许更新止盈止损（P2.8: 移动止损/追踪止盈）
        if (_currentPosition < 0)
        {
            // P2.8: 如果启用了移动止损或追踪止盈，更新现有订单的TP/SL
            if (_config.EnableTrailingStop || _config.EnableTrailingTakeProfit)
            {
                UpdateTrailingStopAndTakeProfit(signal, signal.SignalPrice);
            }
            // 禁止加仓：即使有移动止损功能，也不开新仓
            // Console.WriteLine($"⚠️ 忽略卖出信号: 当前已有空仓 {_currentPosition:F8}，禁止加仓");
            return null;
        }
        
        // 合约交易开空仓逻辑（参考 backtest.py 第932-948行）
        var quantity = CalculateOrderQuantity(signal.SignalPrice);
        if (quantity <= 0)
        {
            // Console.WriteLine($"⚠️ 忽略卖出信号: 资金不足，当前现金 {_currentCash:F2} USDT");
            return null;
        }
        
        // 应用滑点模拟
        var executionPrice = _slippageSimulator.CalculateExecutionPrice(
            OrderSide.SELL,
            signal.SignalPrice,
            quantity,
            candle,
            isOpening: true);
        
        // 记录滑点信息
        var slippageStats = _slippageSimulator.GetStatistics(
            OrderSide.SELL,
            signal.SignalPrice,
            executionPrice,
            quantity);
        
        if (slippageStats.SlippageAmount > 0)
        {
            // Console.WriteLine($"   💫 开仓滑点: {slippageStats.SlippageBps:F1}bps " + $"({slippageStats.Direction}, 成本: {slippageStats.SlippageCost:F2})");
        }
        
        // 计算保证金和手续费（币安合约标准）
        var fee = quantity * executionPrice * _config.TakerFeeRate; // 开仓手续费
        var margin = MarginCalculator.CalculateMargin(quantity, executionPrice, _config.Leverage, _config.TakerFeeRate);
        
        // 计算止盈止损（基于保证金比例）
        decimal takeProfit, stopLoss;
        if (signal.TakeProfit.HasValue && signal.TakeProfit.Value > 0 &&
            signal.StopLoss.HasValue && signal.StopLoss.Value > 0)
        {
            // 信号提供了TP/SL，优先使用
            takeProfit = signal.TakeProfit.Value;
            stopLoss = signal.StopLoss.Value;
        }
        else
        {
            // 使用配置的默认值（基于保证金比例）
            if (_config.EnableDefaultTPSL)
            {
                takeProfit = MarginCalculator.CalculateTakeProfit(executionPrice, OrderSide.SELL, _config.Leverage, _config.DefaultTakeProfitPercent);
                stopLoss = MarginCalculator.CalculateStopLoss(executionPrice, OrderSide.SELL, _config.Leverage, _config.DefaultStopLossPercent);
            }
            else
            {
                takeProfit = 0;
                stopLoss = 0;
            }
        }
        
        // 计算强平价（币安合约标准）
        var liquidationPrice = MarginCalculator.CalculateLiquidationPrice(executionPrice, OrderSide.SELL, margin);
        
        // 创建订单
        var order = new Order
        {
            Id = Guid.NewGuid().ToString(),
            BacktestId = _config.RunId,
            Side = OrderSide.SELL,
            Type = OrderType.MARKET,
            Status = OrderStatus.FILLED,
            Leverage = _config.Leverage,  // 记录杠杆倍数
            Margin = margin,  // 记录保证金
            Quantity = quantity,
            OpenPrice = signal.SignalPrice,
            OpenTime = candle.Time,
            FilledPrice = executionPrice,
            FilledTime = candle.Time,
            Fee = fee,  // 开仓手续费
            FundingFee = 0,  // 开仓时资金费用为0
            TakeProfit = takeProfit > 0 ? takeProfit : null,
            StopLoss = stopLoss > 0 ? stopLoss : null,
            LiquidationPrice = liquidationPrice,
            Remarks = $"卖出开空 | 杠杆{_config.Leverage}x | 保证金{margin:F2} | 滑点{slippageStats.SlippageBps:F1}bps | 强平{liquidationPrice:F2}"
        };
        
        // 更新持仓和资金（扣除保证金，而不是仓位价值）
        // 空仓：持仓数量为负数
        _currentPosition -= quantity;
        _avgEntryPrice = executionPrice;
        _currentCash -= margin;  // 扣除保证金（已包含开仓手续费）
        _openOrders.Add(order);
        
        // Console.WriteLine($"📉 卖出开空: 数量 {quantity:F8} @ {executionPrice:F2} USDT, 手续费 {fee:F2} USDT");
        // Console.WriteLine($"   剩余资金: {_currentCash:F2} USDT, 持仓: {_currentPosition:F8}");
        
        // 触发事件
        OrderFilled?.Invoke(this, new OrderFilledEventArgs { Order = order });
        
        return order;
    }
    
    /// <summary>
    /// 平仓（无论多空）
    /// 集成滑点模拟和资金费用计算（参考 backtest.py）
    /// 注意：平多仓通过 SELL 信号触发（ProcessSellSignal 会检测到多仓并平仓）
    ///       平空仓通过 BUY 信号触发（ProcessBuySignal 会检测到空仓并平仓）
    /// </summary>
    private Order? ClosePosition(Candlestick candle, string reason)
    {
        if (_currentPosition == 0 || _openOrders.Count == 0)
            return null;
        
        // 找到最近的开仓订单
        var openOrder = _openOrders.Last();
        
        // 应用滑点模拟
        var closePrice = (decimal)candle.Close;
        var closeSide = _currentPosition > 0 ? OrderSide.SELL : OrderSide.BUY;
        
        var executionPrice = _slippageSimulator.CalculateExecutionPrice(
            closeSide,
            closePrice,
            Math.Abs(_currentPosition),
            candle,
            isOpening: false);
        
        // 记录滑点信息
        var slippageStats = _slippageSimulator.GetStatistics(
            closeSide,
            closePrice,
            executionPrice,
            Math.Abs(_currentPosition));
        
        if (slippageStats.SlippageAmount > 0)
        {
            // Console.WriteLine($"   💫 平仓滑点: {slippageStats.SlippageBps:F1}bps " + $"({slippageStats.Direction}, 成本: {slippageStats.SlippageCost:F2})");
        }
        
        // 计算平仓手续费
        var closingFee = Math.Abs(_currentPosition) * executionPrice * _config.TakerFeeRate;
        
        // 计算资金费用（基于真实费率数据）
        var positionValue = Math.Abs(_currentPosition) * _avgEntryPrice;
        var fundingFee = _fundingFeeCalculator.CalculateFundingFee(
            _config.Symbol,
            openOrder.OpenTime,
            candle.Time,
            positionValue,
            openOrder.Side);
        
        // 计算净盈亏（使用MarginCalculator统一计算）
        var netProfit = MarginCalculator.CalculateNetProfit(
            entryPrice: _avgEntryPrice,
            exitPrice: executionPrice,
            quantity: Math.Abs(_currentPosition),
            side: openOrder.Side,
            openingFee: openOrder.Fee,  // 开仓手续费
            closingFee: closingFee,
            fundingFee: fundingFee);
        
        // 更新订单
        openOrder.ClosePrice = executionPrice;
        openOrder.CloseTime = candle.Time;
        openOrder.Fee += closingFee;  // 累加平仓手续费
        openOrder.FundingFee = fundingFee;  // 记录资金费用
        openOrder.Profit = netProfit;
        openOrder.Status = OrderStatus.CLOSED;
        
        var fundingFeeInfo = fundingFee != 0 ? $", 资金费{fundingFee:+F4}" : "";
        openOrder.Remarks += $" | {reason} | 盈亏{netProfit:F2}{fundingFeeInfo}";
        
        // 归还资金（归还保证金 + 盈亏 - 平仓手续费）
        var returnAmount = MarginCalculator.CalculateReturnAmount(
            margin: openOrder.Margin,
            profit: netProfit,
            closingFee: closingFee);
        _currentCash += returnAmount;
        
        // 移动到已平仓列表
        _openOrders.Remove(openOrder);
        _closedOrders.Add(openOrder);
        
        // Console.WriteLine($"📉 平仓: {(_currentPosition > 0 ? "多仓" : "空仓")} " + $"{Math.Abs(_currentPosition):F8} @ {executionPrice:F2} USDT");
        // Console.WriteLine($"   盈亏: {netProfit:F2} USDT, 手续费: {fee:F2} USDT{fundingFeeInfo}, " + $"剩余资金: {_currentCash:F2} USDT");
        
        // 重置持仓
        _currentPosition = 0;
        _avgEntryPrice = 0;
        
        // 触发事件
        OrderFilled?.Invoke(this, new OrderFilledEventArgs { Order = openOrder });
        
        return openOrder;
    }
    
    /// <summary>
    /// 更新止损价格
    /// </summary>
    public Task UpdateStopLossAsync(Order order, decimal newStopLoss)
    {
        if (!_openOrders.Contains(order))
        {
            // Console.WriteLine($"⚠️ 更新止损失败: 订单不在持仓列表中");
            return Task.CompletedTask;
        }
        
        order.StopLoss = newStopLoss;
        // Console.WriteLine($"✏️ 更新止损: {order.Id} -> {newStopLoss:F2} USDT");
        return Task.CompletedTask;
    }
    
    /// <summary>
    /// 更新止盈价格
    /// </summary>
    public Task UpdateTakeProfitAsync(Order order, decimal newTakeProfit)
    {
        if (!_openOrders.Contains(order))
        {
            // Console.WriteLine($"⚠️ 更新止盈失败: 订单不在持仓列表中");
            return Task.CompletedTask;
        }
        
        order.TakeProfit = newTakeProfit;
        // Console.WriteLine($"✏️ 更新止盈: {order.Id} -> {newTakeProfit:F2} USDT");
        return Task.CompletedTask;
    }
    
    /// <summary>
    /// 检查止盈止损（每根K线调用一次）
    /// 使用K线的高低价进行精确检查，并考虑价格运动方向
    /// 参考 backtest.py 的 _check_sl_tp
    /// </summary>
    public void CheckStopLossAndTakeProfit(Candlestick candle)
    {
        if (_openOrders.Count == 0 || _currentPosition == 0)
            return;
        
        var openOrder = _openOrders.Last();
        var open = (decimal)candle.Open;
        var low = (decimal)candle.Low;
        var high = (decimal)candle.High;
        var close = (decimal)candle.Close;
        
        if (_currentPosition > 0) // 多仓
        {
            // P1.4: 检查强平（优先级最高）
            if (openOrder.LiquidationPrice.HasValue && low <= openOrder.LiquidationPrice.Value)
            {
                // Console.WriteLine($"⚠️ 强平触发: low({low:F2}) <= 强平价({openOrder.LiquidationPrice.Value:F2})");
                var closedOrder = ClosePositionAtPrice(candle, openOrder.LiquidationPrice.Value, "强制平仓");
                if (closedOrder != null)
                {
                    StopLossTriggered?.Invoke(this, new StopLossTriggeredEventArgs { Order = closedOrder });
                }
                return;
            }
            
            // 检查是否同时触及止盈和止损（需要判断触发顺序）
            bool tpTriggered = openOrder.TakeProfit.HasValue && high >= openOrder.TakeProfit.Value;
            bool slTriggered = openOrder.StopLoss.HasValue && low <= openOrder.StopLoss.Value;
            
            if (tpTriggered && slTriggered)
            {
                // 根据开盘价判断价格先向哪个方向运动
                // 如果开盘价接近低点，说明先下跌（触发止损），再上涨
                // 如果开盘价接近高点，说明先上涨（触发止盈），再下跌
                var distanceToLow = Math.Abs(open - low);
                var distanceToHigh = Math.Abs(open - high);
                
                if (distanceToLow < distanceToHigh)
                {
                    // 开盘价更接近低点，先触发止损
                    // Console.WriteLine($"🛑 止损优先触发（开盘价{open:F2}接近低点{low:F2}）");
                    var closedOrder = ClosePositionAtPrice(candle, openOrder.StopLoss!.Value, "止损");
                    if (closedOrder != null)
                    {
                        StopLossTriggered?.Invoke(this, new StopLossTriggeredEventArgs { Order = closedOrder });
                    }
                    return;
                }
                else
                {
                    // 开盘价更接近高点，先触发止盈
                    // Console.WriteLine($"💰 止盈优先触发（开盘价{open:F2}接近高点{high:F2}）");
                    var closedOrder = ClosePositionAtPrice(candle, openOrder.TakeProfit!.Value, "止盈");
                    if (closedOrder != null)
                    {
                        TakeProfitTriggered?.Invoke(this, new TakeProfitTriggeredEventArgs { Order = closedOrder });
                    }
                    return;
                }
            }
            
            // 单独检查止盈
            if (tpTriggered)
            {
                // Console.WriteLine($"💰 止盈触发: high({high:F2}) >= TP({openOrder.TakeProfit.Value:F2})");
                var closedOrder = ClosePositionAtPrice(candle, openOrder.TakeProfit!.Value, "止盈");
                if (closedOrder != null)
                {
                    TakeProfitTriggered?.Invoke(this, new TakeProfitTriggeredEventArgs { Order = closedOrder });
                }
                return;
            }
            
            // 单独检查止损
            if (slTriggered)
            {
                // Console.WriteLine($"🛑 止损触发: low({low:F2}) <= SL({openOrder.StopLoss.Value:F2})");
                var closedOrder = ClosePositionAtPrice(candle, openOrder.StopLoss!.Value, "止损");
                if (closedOrder != null)
                {
                    StopLossTriggered?.Invoke(this, new StopLossTriggeredEventArgs { Order = closedOrder });
                }
                return;
            }
        }
        else if (_currentPosition < 0) // 空仓
        {
            // P1.4: 检查强平（优先级最高）
            if (openOrder.LiquidationPrice.HasValue && high >= openOrder.LiquidationPrice.Value)
            {
                // Console.WriteLine($"⚠️ 强平触发: high({high:F2}) >= 强平价({openOrder.LiquidationPrice.Value:F2})");
                var closedOrder = ClosePositionAtPrice(candle, openOrder.LiquidationPrice.Value, "强制平仓");
                if (closedOrder != null)
                {
                    StopLossTriggered?.Invoke(this, new StopLossTriggeredEventArgs { Order = closedOrder });
                }
                return;
            }
            
            // 检查是否同时触及止盈和止损（需要判断触发顺序）
            bool tpTriggered = openOrder.TakeProfit.HasValue && low <= openOrder.TakeProfit.Value;
            bool slTriggered = openOrder.StopLoss.HasValue && high >= openOrder.StopLoss.Value;
            
            if (tpTriggered && slTriggered)
            {
                // 根据开盘价判断价格先向哪个方向运动
                // 如果开盘价接近高点，说明先上涨（触发止损），再下跌
                // 如果开盘价接近低点，说明先下跌（触发止盈），再上涨
                var distanceToHigh = Math.Abs(open - high);
                var distanceToLow = Math.Abs(open - low);
                
                if (distanceToHigh < distanceToLow)
                {
                    // 开盘价更接近高点，先触发止损
                    // Console.WriteLine($"🛑 止损优先触发（开盘价{open:F2}接近高点{high:F2}）");
                    var closedOrder = ClosePositionAtPrice(candle, openOrder.StopLoss!.Value, "止损");
                    if (closedOrder != null)
                    {
                        StopLossTriggered?.Invoke(this, new StopLossTriggeredEventArgs { Order = closedOrder });
                    }
                    return;
                }
                else
                {
                    // 开盘价更接近低点，先触发止盈
                    // Console.WriteLine($"💰 止盈优先触发（开盘价{open:F2}接近低点{low:F2}）");
                    var closedOrder = ClosePositionAtPrice(candle, openOrder.TakeProfit!.Value, "止盈");
                    if (closedOrder != null)
                    {
                        TakeProfitTriggered?.Invoke(this, new TakeProfitTriggeredEventArgs { Order = closedOrder });
                    }
                    return;
                }
            }
            
            // 单独检查止盈
            if (tpTriggered)
            {
                // Console.WriteLine($"💰 止盈触发: low({low:F2}) <= TP({openOrder.TakeProfit.Value:F2})");
                var closedOrder = ClosePositionAtPrice(candle, openOrder.TakeProfit!.Value, "止盈");
                if (closedOrder != null)
                {
                    TakeProfitTriggered?.Invoke(this, new TakeProfitTriggeredEventArgs { Order = closedOrder });
                }
                return;
            }
            
            // 单独检查止损
            if (slTriggered)
            {
                // Console.WriteLine($"🛑 止损触发: high({high:F2}) >= SL({openOrder.StopLoss.Value:F2})");
                var closedOrder = ClosePositionAtPrice(candle, openOrder.StopLoss!.Value, "止损");
                if (closedOrder != null)
                {
                    StopLossTriggered?.Invoke(this, new StopLossTriggeredEventArgs { Order = closedOrder });
                }
                return;
            }
        }
    }
    
    /// <summary>
    /// 按指定价格平仓（用于止盈止损）
    /// </summary>
    private Order? ClosePositionAtPrice(Candlestick candle, decimal closePrice, string reason)
    {
        if (_currentPosition == 0 || _openOrders.Count == 0)
            return null;
        
        // 找到最近的开仓订单
        var openOrder = _openOrders.Last();
        
        // 使用指定的价格（止盈/止损价）而不是收盘价
        var executionPrice = closePrice;
        
        // 计算平仓手续费
        var closingFee = Math.Abs(_currentPosition) * executionPrice * _config.TakerFeeRate;
        
        // 计算资金费用（基于真实费率数据）
        var positionValue = Math.Abs(_currentPosition) * _avgEntryPrice;
        var fundingFee = _fundingFeeCalculator.CalculateFundingFee(
            _config.Symbol,
            openOrder.OpenTime,
            candle.Time,
            positionValue,
            openOrder.Side);
        
        // 计算净盈亏（使用MarginCalculator统一计算）
        var netProfit = MarginCalculator.CalculateNetProfit(
            entryPrice: _avgEntryPrice,
            exitPrice: executionPrice,
            quantity: Math.Abs(_currentPosition),
            side: openOrder.Side,
            openingFee: openOrder.Fee,  // 开仓手续费
            closingFee: closingFee,
            fundingFee: fundingFee);
        
        // 更新订单
        openOrder.ClosePrice = executionPrice;
        openOrder.CloseTime = candle.Time;
        openOrder.Fee += closingFee;  // 累加平仓手续费
        openOrder.FundingFee = fundingFee;  // 记录资金费用
        openOrder.Profit = netProfit;
        openOrder.Status = OrderStatus.CLOSED;
        
        var fundingFeeInfo = fundingFee != 0 ? $", 资金费{fundingFee:+F4}" : "";
        openOrder.Remarks += $" | {reason} | 盈亏{netProfit:F2}{fundingFeeInfo}";
        
        // 归还资金（归还保证金 + 盈亏 - 平仓手续费）
        var returnAmount = MarginCalculator.CalculateReturnAmount(
            margin: openOrder.Margin,
            profit: netProfit,
            closingFee: closingFee);
        _currentCash += returnAmount;
        
        // 移动到已平仓列表
        _openOrders.Remove(openOrder);
        _closedOrders.Add(openOrder);
        
        // Console.WriteLine($"📉 平仓: {(_currentPosition > 0 ? "多仓" : "空仓")} " + $"{Math.Abs(_currentPosition):F8} @ {executionPrice:F2} USDT");
        // Console.WriteLine($"   盈亏: {netProfit:F2} USDT, 手续费: {fee:F2} USDT, " + $"剩余资金: {_currentCash:F2} USDT");
        
        // 重置持仓
        _currentPosition = 0;
        _avgEntryPrice = 0;
        
        // 触发事件
        OrderFilled?.Invoke(this, new OrderFilledEventArgs { Order = openOrder });
        
        return openOrder;
    }
    
    /// <summary>
    /// 更新当前权益（每根K线调用一次）
    /// 对于合约交易，权益 = 现金 + 未实现盈亏
    /// 未实现盈亏 = (当前价格 - 开仓价格) * 持仓数量 * 方向系数
    /// </summary>
    public Task UpdateEquityAsync(Candlestick candle)
    {
        decimal unrealizedPnL = 0;
        
        if (_currentPosition != 0 && _openOrders.Count > 0)
        {
            // 计算未实现盈亏
            var openOrder = _openOrders.Last();
            var currentPrice = (decimal)candle.Close;
            
            if (_currentPosition > 0)
            {
                // 多仓：未实现盈亏 = (当前价格 - 开仓价格) * 持仓数量
                unrealizedPnL = (currentPrice - _avgEntryPrice) * _currentPosition;
            }
            else
            {
                // 空仓：未实现盈亏 = (开仓价格 - 当前价格) * |持仓数量|
                unrealizedPnL = (_avgEntryPrice - currentPrice) * Math.Abs(_currentPosition);
            }
        }
        
        // 权益 = 现金 + 未实现盈亏
        // 注意：开仓时已经扣除了保证金，所以_currentCash不包含保证金
        // 权益应该反映当前持仓的盈亏情况
        CurrentEquity = _currentCash + unrealizedPnL;
        
        // P2.6: 维护历史K线缓存（用于ATR计算）
        _recentCandles.Enqueue(candle);
        if (_recentCandles.Count > MAX_CANDLE_HISTORY)
        {
            _recentCandles.Dequeue();
        }

        return Task.CompletedTask;
    }
    
    /// <summary>
    /// P2.6: 计算订单数量（支持多种方法：fixed, ATR, Kelly）
    /// </summary>
    private decimal CalculateOrderQuantity(decimal price)
    {
        decimal positionSize;
        
        switch (_config.PositionSizeMethod.ToLower())
        {
            case "atr":
                positionSize = CalculateATRPositionSize(price);
                // Console.WriteLine($"   📊 ATR仓位计算: {positionSize:F8}");
                break;
                
            case "kelly":
                positionSize = CalculateKellyPositionSize(price);
                // Console.WriteLine($"   📊 Kelly仓位计算: {positionSize:F8}");
                break;
                
            case "fixed":
            default:
                positionSize = CalculateFixedPositionSize(price);
                break;
        }
        
        return positionSize;
    }
    
    /// <summary>
    /// P2.6: 固定比例仓位计算（基于初始资金和杠杆）
    /// </summary>
    private decimal CalculateFixedPositionSize(decimal price)
    {
        // 使用初始资金作为基准（而非当前资金），保持仓位大小一致性
        return MarginCalculator.CalculatePositionSize(
            availableCash: _config.InitialCapital,
            positionSizePercent: _config.PositionSizePercent,
            leverage: _config.Leverage,
            price: price,
            feeRate: _config.TakerFeeRate,
            slippageRate: _config.SlippageRate);
    }
    
    /// <summary>
    /// P2.6: 基于ATR的仓位大小计算
    /// 风险金额 = 总资金 * 风险百分比
    /// 仓位大小 = 风险金额 / (ATR * 止损倍数)
    /// </summary>
    private decimal CalculateATRPositionSize(decimal price)
    {
        if (_recentCandles.Count < _config.ATRPeriod)
        {
            // 数据不足，使用固定比例
            // Console.WriteLine($"   ⚠️ ATR数据不足（{_recentCandles.Count}/{_config.ATRPeriod}），使用固定仓位");
            return CalculateFixedPositionSize(price);
        }
        
        // 1. 计算ATR
        var atr = CalculateATR(_config.ATRPeriod);
        
        if (atr == 0)
        {
            // Console.WriteLine($"   ⚠️ ATR为0，使用固定仓位");
            return CalculateFixedPositionSize(price);
        }
        
        // 2. 风险金额 = 初始资金 * 风险百分比（保持一致性）
        var riskAmount = _config.InitialCapital * _config.RiskPercentPerTrade;
        
        // 3. 每单位风险 = ATR * 止损倍数
        var riskPerUnit = atr * _config.ATRMultiplier;
        
        // 4. 仓位大小 = 风险金额 / 每单位风险
        var quantity = riskAmount / riskPerUnit;
        
        // 5. 确保不超过最大仓位（基于初始资金，考虑杠杆）
        var maxQuantity = (_config.InitialCapital * _config.PositionSizePercent * _config.Leverage) / price;
        quantity = Math.Min(quantity, maxQuantity);
        
        // Console.WriteLine($"   ATR: {atr:F2}, 风险金额: {riskAmount:F2}, 单位风险: {riskPerUnit:F2}");
        
        return Math.Round(quantity, 8);
    }
    
    /// <summary>
    /// P2.6: 基于Kelly公式的仓位大小计算
    /// Kelly公式: f = (bp - q) / b
    /// b = 赔率（平均盈利/平均亏损）
    /// p = 胜率
    /// q = 败率 (1-p)
    /// </summary>
    private decimal CalculateKellyPositionSize(decimal price)
    {
        if (_closedOrders.Count < _config.MinKellySampleSize)
        {
            // 样本数不足，使用固定比例
            // Console.WriteLine($"   ⚠️ Kelly样本不足（{_closedOrders.Count}/{_config.MinKellySampleSize}），使用固定仓位");
            return CalculateFixedPositionSize(price);
        }
        
        // 取最近100笔订单计算
        var recentOrders = _closedOrders.TakeLast(100).ToList();
        
        // 1. 计算胜率
        var winningOrders = recentOrders.Where(o => o.Profit > 0).ToList();
        var losingOrders = recentOrders.Where(o => o.Profit < 0).ToList();
        
        if (losingOrders.Count == 0)
        {
            // 全胜（不太可能），使用最大Kelly比例（基于初始资金）
            var maxKellyAmount = _config.InitialCapital * _config.MaxKellyFraction;
            return Math.Round(maxKellyAmount / price, 8);
        }
        
        var winRate = (decimal)winningOrders.Count / recentOrders.Count;
        
        // 2. 计算平均盈亏
        var avgWin = winningOrders.Any() ? winningOrders.Average(o => o.Profit ?? 0) : 0;
        var avgLoss = losingOrders.Any() ? Math.Abs(losingOrders.Average(o => o.Profit ?? 0)) : 0;
        
        if (avgLoss == 0)
        {
            var maxKellyAmount = _config.InitialCapital * _config.MaxKellyFraction;
            return Math.Round(maxKellyAmount / price, 8);
        }
        
        // 3. Kelly公式
        var b = avgWin / avgLoss;  // 赔率
        var q = 1 - winRate;       // 败率
        var kellyFraction = (b * winRate - q) / b;
        
        // 4. 使用Half-Kelly（更保守）
        kellyFraction = kellyFraction * 0.5m;
        
        // 5. 限制在最大比例内
        kellyFraction = Math.Max(0, Math.Min(kellyFraction, _config.MaxKellyFraction));
        
        // 6. 计算仓位（基于初始资金）
        var kellyAmount = _config.InitialCapital * kellyFraction;
        var quantity = kellyAmount / price;
        
        // Console.WriteLine($"   Kelly: 胜率{winRate:P1}, 赔率{b:F2}, Kelly值{kellyFraction:P1}");
        
        return Math.Round(quantity, 8);
    }
    
    /// <summary>
    /// P2.6: 计算ATR（Average True Range）
    /// </summary>
    private decimal CalculateATR(int PERIOD)
    {
        if (_recentCandles.Count < PERIOD + 1)
            return 0;
        
        var candles = _recentCandles.TakeLast(PERIOD + 1).ToArray();
        var trValues = new List<decimal>();
        
        for (int i = 1; i < candles.Length; i++)
        {
            var high = (decimal)candles[i].High;
            var low = (decimal)candles[i].Low;
            var prevClose = (decimal)candles[i - 1].Close;
            
            // True Range = Max(High-Low, |High-PrevClose|, |Low-PrevClose|)
            var tr = Math.Max(
                high - low,
                Math.Max(
                    Math.Abs(high - prevClose),
                    Math.Abs(low - prevClose)
                )
            );
            
            trValues.Add(tr);
        }
        
        // ATR = 平均真实波幅
        return trValues.Average();
    }
    
    
    /// <summary>
    /// P2.8: 更新现有持仓的移动止损/追踪止盈
    /// 当信号持续提供新的TP/SL值时，动态更新止损止盈价格
    /// 安全约束：止损只能往更安全的方向移动，止盈可以往更有利的方向移动
    /// </summary>
    /// <param name="signal">新信号（可能包含新的TP/SL）</param>
    /// <param name="currentPrice">当前价格</param>
    private void UpdateTrailingStopAndTakeProfit(Signal signal, decimal currentPrice)
    {
        // 如果没有未平仓订单，直接返回
        if (_openOrders.Count == 0)
            return;
        
        // 获取最新的持仓订单（假设一次只有一个持仓）
        var order = _openOrders.Last();
        
        // 判断是多单还是空单
        bool isLongPosition = order.Side == OrderSide.BUY;
        
        // 更新止损（EnableTrailingStop）
        if (_config.EnableTrailingStop && signal.StopLoss.HasValue && signal.StopLoss.Value > 0)
        {
            var newStopLoss = signal.StopLoss.Value;
            
            if (isLongPosition)
            {
                // 多单：止损只能上移（更安全），不能下移
                if (!order.StopLoss.HasValue || newStopLoss > order.StopLoss.Value)
                {
                    var oldSL = order.StopLoss;
                    order.StopLoss = newStopLoss;
                    // Console.WriteLine($"🔼 移动止损（多单）: {oldSL?.ToString("F2") ?? "无"} → {newStopLoss:F2} " + $"(当前价: {currentPrice:F2}, 保护利润: {((newStopLoss - order.FilledPrice) / order.FilledPrice * 100):F2}%)");
                }
            }
            else
            {
                // 空单：止损只能下移（更安全），不能上移
                if (!order.StopLoss.HasValue || newStopLoss < order.StopLoss.Value)
                {
                    var oldSL = order.StopLoss;
                    order.StopLoss = newStopLoss;
                    // Console.WriteLine($"🔽 移动止损（空单）: {oldSL?.ToString("F2") ?? "无"} → {newStopLoss:F2} " + $"(当前价: {currentPrice:F2}, 保护利润: {((order.FilledPrice - newStopLoss) / order.FilledPrice * 100):F2}%)");
                }
            }
        }
        
        // 更新止盈（EnableTrailingTakeProfit）
        if (_config.EnableTrailingTakeProfit && signal.TakeProfit.HasValue && signal.TakeProfit.Value > 0)
        {
            var newTakeProfit = signal.TakeProfit.Value;
            
            if (isLongPosition)
            {
                // 多单：止盈可以上移（更有利），也可以下移（防止过度贪婪）
                // 但通常我们只允许止盈上移
                if (!order.TakeProfit.HasValue || newTakeProfit > order.TakeProfit.Value)
                {
                    var oldTP = order.TakeProfit;
                    order.TakeProfit = newTakeProfit;
                    // Console.WriteLine($"📈 追踪止盈（多单）: {oldTP?.ToString("F2") ?? "无"} → {newTakeProfit:F2} " + $"(当前价: {currentPrice:F2}, 目标利润: {((newTakeProfit - order.FilledPrice) / order.FilledPrice * 100):F2}%)");
                }
            }
            else
            {
                // 空单：止盈可以下移（更有利），也可以上移（防止过度贪婪）
                // 但通常我们只允许止盈下移
                if (!order.TakeProfit.HasValue || newTakeProfit < order.TakeProfit.Value)
                {
                    var oldTP = order.TakeProfit;
                    order.TakeProfit = newTakeProfit;
                    // Console.WriteLine($"📉 追踪止盈（空单）: {oldTP?.ToString("F2") ?? "无"} → {newTakeProfit:F2} " + $"(当前价: {currentPrice:F2}, 目标利润: {((order.FilledPrice - newTakeProfit) / order.FilledPrice * 100):F2}%)");
                }
            }
        }
    }
    
    /// <summary>
    /// P1.4: 计算强平价（开仓时计算）
    /// </summary>
    /// <param name="entryPrice">开仓价格</param>
    /// <param name="side">订单方向</param>
    /// <param name="margin">保证金</param>
    /// <returns>强平价格</returns>
}

