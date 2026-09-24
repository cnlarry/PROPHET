using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Prophet.Client.Backtest.OrderManagement;
using Prophet.Client.Backtest.Strategy;
using Prophet.Client.Models;
using Prophet.Client.Trading.Exchanges;
using Prophet.Client.Trading.OrderManagement;
using Prophet.Client.Trading.RiskControl;
using Prophet.Client.Trading.Storage;

namespace Prophet.Client.Trading.Engine;

/// <summary>
/// 实盘交易引擎
/// 整合策略、订单管理、风险控制，提供完整的实盘交易功能
/// </summary>
public class LiveTradingEngine : IDisposable
{
    private readonly IExchange _exchange;
    private readonly LiveOrderManager _orderManager;
    private readonly IStrategySignalGenerator _strategyGenerator;
    private readonly RiskController? _riskController;
    private readonly LiveOrderStorage _storage;
    private readonly LiveTradingConfig _config;
    
    /// <summary>
    /// 实例ID（用于多实盘管理）
    /// </summary>
    public Guid InstanceId { get; set; } = Guid.Empty;
    
    /// <summary>
    /// 实例名称（用于日志输出）
    /// </summary>
    public string InstanceName { get; set; } = "默认实例";
    
    /// <summary>
    /// 引擎状态
    /// </summary>
    public TradingEngineStatus Status { get; private set; } = TradingEngineStatus.Stopped;
    
    private LiveSession? _currentSession;
    private CancellationTokenSource? _cancellationTokenSource;
    private bool _isRunning;
    private bool _disposed;
    
    // 实时数据缓存
    private readonly Queue<Candlestick> _candleBuffer = new();
    private const int MaxCandleBuffer = 1000;
    
    // 事件
    public event EventHandler<TradingStatusChangedEventArgs>? StatusChanged;
    public event EventHandler<SignalGeneratedEventArgs>? SignalGenerated;
    public event EventHandler<OrderExecutedEventArgs>? OrderExecuted;
    public event EventHandler<TradingErrorEventArgs>? ErrorOccurred;
    
    public bool IsRunning => _isRunning;
    public LiveSession? CurrentSession => _currentSession;
    
    public LiveTradingEngine(
        IExchange exchange,
        LiveOrderManager orderManager,
        IStrategySignalGenerator strategyGenerator,
        LiveTradingConfig config,
        RiskController? riskController = null,
        LiveOrderStorage? storage = null)
    {
        _exchange = exchange ?? throw new ArgumentNullException(nameof(exchange));
        _orderManager = orderManager ?? throw new ArgumentNullException(nameof(orderManager));
        _strategyGenerator = strategyGenerator ?? throw new ArgumentNullException(nameof(strategyGenerator));
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _riskController = riskController;
        _storage = storage ?? new LiveOrderStorage();
        
        // 订阅风险事件
        if (_riskController != null)
        {
            _riskController.EmergencyStopTriggered += OnEmergencyStopTriggered;
            _riskController.RiskViolationDetected += OnRiskViolationDetected;
        }
    }
    
    /// <summary>
    /// 启动实盘交易
    /// </summary>
    public async Task StartAsync()
    {
        if (_isRunning)
        {
            throw new InvalidOperationException("交易引擎已在运行中");
        }
        
        try
        {
            Status = TradingEngineStatus.Starting;
            Console.WriteLine("🚀 [LiveTradingEngine] 启动实盘交易引擎");
            
            // 1. 初始化交易所连接
            if (_exchange.Status != ExchangeStatus.Authenticated)
            {
                throw new InvalidOperationException("交易所未认证");
            }
            
            // 2. 同步账户状态
            await _orderManager.SynchronizeStateAsync();
            
            // 3. 创建交易会话
            _currentSession = new LiveSession
            {
                Id = Guid.NewGuid().ToString(),
                StrategyName = _config.StrategyName,
                Symbol = _config.Symbol,
                Leverage = _config.Leverage,
                InitialCapital = _orderManager.CurrentEquity,
                StartTime = DateTime.UtcNow,
                Status = "Active"
            };
            
            await _storage.CreateSessionAsync(_currentSession);
            
            Console.WriteLine($"✅ 交易会话已创建: {_currentSession.Id}");
            Console.WriteLine($"   策略: {_config.StrategyName}");
            Console.WriteLine($"   交易对: {_config.Symbol}");
            Console.WriteLine($"   杠杆: {_config.Leverage}x");
            Console.WriteLine($"   初始资金: {_currentSession.InitialCapital:F2} USDT");
            
            // 4. 加载历史K线数据
            var startTime = DateTime.UtcNow.AddDays(-7);
            var historicalCandles = await _exchange.GetHistoricalCandlesAsync(
                _config.Symbol,
                _config.Timeframe,
                startTime,
                DateTime.UtcNow,
                1000);
            
            foreach (var candle in historicalCandles)
            {
                _candleBuffer.Enqueue(candle);
                if (_candleBuffer.Count > MaxCandleBuffer)
                {
                    _candleBuffer.Dequeue();
                }
            }
            
            Console.WriteLine($"✅ 已加载 {historicalCandles.Count} 条历史K线数据");
            
            // 4.1 预热策略引擎：注入历史K线到核心引擎窗口（否则指标无数据，信号恒为错误HOLD）
            if (historicalCandles.Count > 0)
            {
                try
                {
                    _strategyGenerator.SetKlines(_config.Timeframe, historicalCandles);
                    Console.WriteLine($"✅ 策略引擎已预热 {historicalCandles.Count} 根K线 ({_config.Timeframe})");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"⚠️ 策略引擎预热失败: {ex.Message}");
                    throw;
                }
            }
            
            // 5. 启动订单同步
            if (_config.EnableOrderSync)
            {
                var synchronizer = new LiveOrderSynchronizer(_exchange);
                synchronizer.StartAutoSync(_config.OrderSyncIntervalSeconds);
            }
            
            // 6. 启动实时数据流
            _cancellationTokenSource = new CancellationTokenSource();
            _isRunning = true;
            Status = TradingEngineStatus.Running;
            
            RaiseStatusChanged("Started");
            
            // 订阅实时K线数据并开始交易循环
            await RunTradingLoopAsync(_cancellationTokenSource.Token);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ [LiveTradingEngine] 启动失败: {ex.Message}");
            _isRunning = false;
            Status = TradingEngineStatus.Error;
            RaiseError("启动失败", ex);
            throw;
        }
    }
    
    /// <summary>
    /// 停止实盘交易
    /// </summary>
    public async Task StopAsync()
    {
        if (!_isRunning)
        {
            return;
        }
        
        Console.WriteLine("⏹️ [LiveTradingEngine] 停止实盘交易引擎");
        Status = TradingEngineStatus.Stopping;
        
        try
        {
            // 1. 取消交易循环
            _cancellationTokenSource?.Cancel();
            _isRunning = false;
            
            // 2. 平掉所有持仓（可选，根据配置决定）
            if (_config.CloseAllPositionsOnStop)
            {
                await CloseAllPositionsAsync();
            }
            
            // 3. 更新交易会话
            if (_currentSession != null)
            {
                _currentSession.EndTime = DateTime.UtcNow;
                _currentSession.Status = "Stopped";
                _currentSession.FinalEquity = _orderManager.CurrentEquity;
                _currentSession.TotalProfit = _orderManager.CurrentEquity - _currentSession.InitialCapital;
                
                // 计算交易统计
                var closedOrders = _orderManager.ClosedOrders;
                _currentSession.TotalTrades = closedOrders.Count;
                _currentSession.WinningTrades = closedOrders.Count(o => o.Profit > 0);
                _currentSession.LosingTrades = closedOrders.Count(o => o.Profit < 0);
                _currentSession.TotalFees = closedOrders.Sum(o => o.Fee);
                
                await _storage.UpdateSessionAsync(_currentSession);
                
                Console.WriteLine($"✅ 交易会话已结束");
                Console.WriteLine($"   总交易次数: {_currentSession.TotalTrades}");
                Console.WriteLine($"   盈利次数: {_currentSession.WinningTrades}");
                Console.WriteLine($"   亏损次数: {_currentSession.LosingTrades}");
                Console.WriteLine($"   最终权益: {_currentSession.FinalEquity:F2} USDT");
                Console.WriteLine($"   总盈亏: {_currentSession.TotalProfit:F2} USDT");
            }
            
            Status = TradingEngineStatus.Stopped;
            RaiseStatusChanged("Stopped");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ [LiveTradingEngine] 停止失败: {ex.Message}");
            Status = TradingEngineStatus.Error;
            RaiseError("停止失败", ex);
        }
    }
    
    /// <summary>
    /// 暂停交易
    /// </summary>
    public async Task PauseAsync()
    {
        if (!_isRunning)
        {
            return;
        }
        
        Console.WriteLine("⏸️ [LiveTradingEngine] 暂停交易");
        
        _cancellationTokenSource?.Cancel();
        _isRunning = false;
        Status = TradingEngineStatus.Paused;
        
        if (_currentSession != null)
        {
            _currentSession.Status = "Paused";
            await _storage.UpdateSessionAsync(_currentSession);
        }
        
        RaiseStatusChanged("Paused");
    }
    
    /// <summary>
    /// 恢复交易
    /// </summary>
    public async Task ResumeAsync()
    {
        if (_isRunning)
        {
            return;
        }
        
        Console.WriteLine("▶️ [LiveTradingEngine] 恢复交易");
        
        if (_currentSession != null)
        {
            _currentSession.Status = "Active";
            await _storage.UpdateSessionAsync(_currentSession);
        }
        
        _cancellationTokenSource = new CancellationTokenSource();
        _isRunning = true;
        Status = TradingEngineStatus.Running;
        
        RaiseStatusChanged("Resumed");
        
        await RunTradingLoopAsync(_cancellationTokenSource.Token);
    }
    
    /// <summary>
    /// 交易循环
    /// </summary>
    private async Task RunTradingLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            Console.WriteLine($"📈 [LiveTradingEngine] 开始订阅K线数据流: {_config.Symbol} {_config.Timeframe}");
            
            await foreach (var candle in _exchange.SubscribeCandlesAsync(_config.Symbol, _config.Timeframe)
                .WithCancellation(cancellationToken))
            {
                // 处理新K线
                await ProcessCandleAsync(candle, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("🛑 [LiveTradingEngine] 交易循环已取消");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ [LiveTradingEngine] 交易循环错误: {ex.Message}");
            RaiseError("交易循环错误", ex);
        }
    }
    
    /// <summary>
    /// 处理新K线
    /// </summary>
    private async Task ProcessCandleAsync(Candlestick candle, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        
        // 添加到缓冲区
        _candleBuffer.Enqueue(candle);
        if (_candleBuffer.Count > MaxCandleBuffer)
        {
            _candleBuffer.Dequeue();
        }
        
        Console.WriteLine($"📊 [LiveTradingEngine] 新K线: {candle.Time:HH:mm} " +
            $"O:{candle.Open:F2} H:{candle.High:F2} L:{candle.Low:F2} C:{candle.Close:F2}");
        
        // 1. 追加K线到策略引擎（保持核心引擎K线窗口最新，否则信号恒为错误HOLD）
        try
        {
            _strategyGenerator.AppendKline(_config.Timeframe, candle);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ [LiveTradingEngine] 追加K线到引擎失败: {ex.Message}");
        }
        
        // 2. 检查止盈止损
        _orderManager.CheckStopLossAndTakeProfit(candle);
        _orderManager.UpdateEquity(candle);
        
        // 3. 生成交易信号
        var signal = _strategyGenerator.GenerateSignal(candle, _candleBuffer.ToList());
        
        if (signal != null && signal.Action != SignalAction.HOLD)
        {
            Console.WriteLine($"💡 [LiveTradingEngine] 信号触发: {signal.Action} @ {signal.SignalPrice:F2}");
            
            SignalGenerated?.Invoke(this, new SignalGeneratedEventArgs
            {
                Signal = signal,
                Candle = candle
            });
            
            // 3. 风险检查
            if (_riskController != null)
            {
                var riskCheck = await _riskController.CheckSignalAsync(signal, _orderManager.CurrentEquity);
                
                if (!riskCheck.IsApproved)
                {
                    Console.WriteLine($"❌ [LiveTradingEngine] 信号被风险控制拒绝: {riskCheck.RejectReason}");
                    Console.WriteLine($"   风险等级: {riskCheck.RiskLevel}");
                    return;
                }
            }
            
            // 4. 执行订单
            var order = await _orderManager.ProcessSignalAsync(signal, candle);
            
            if (order != null)
            {
                // 保存订单到数据库
                await _storage.SaveOrderAsync(order);
                
                // 记录交易（用于统计）
                if (order.Status == OrderStatus.CLOSED)
                {
                    _riskController?.RecordTrade(order);
                }
                
                // 更新权益（用于回撤计算）
                _riskController?.UpdateEquity(_orderManager.CurrentEquity);
                
                OrderExecuted?.Invoke(this, new OrderExecutedEventArgs
                {
                    Order = order,
                    Signal = signal
                });
            }
        }
        
        // 4. 定期保存状态（每100根K线）
        if (_candleBuffer.Count % 100 == 0 && _currentSession != null)
        {
            _currentSession.FinalEquity = _orderManager.CurrentEquity;
            _currentSession.TotalProfit = _orderManager.CurrentEquity - _currentSession.InitialCapital;
            await _storage.UpdateSessionAsync(_currentSession);
        }
    }
    
    /// <summary>
    /// 平掉所有持仓
    /// </summary>
    private async Task CloseAllPositionsAsync()
    {
        Console.WriteLine("🔄 [LiveTradingEngine] 平掉所有持仓...");
        
        var positions = await _exchange.GetPositionsAsync(_config.Symbol);
        
        foreach (var position in positions)
        {
            try
            {
                var closeSide = position.Side == PositionSide.Long 
                    ? Trading.Exchanges.OrderSide.SELL 
                    : Trading.Exchanges.OrderSide.BUY;
                
                var request = new Trading.Models.MarketOrderRequest
                {
                    Symbol = position.Symbol,
                    Side = closeSide,
                    Quantity = position.Quantity
                };
                
                var result = await _exchange.PlaceMarketOrderAsync(request);
                
                if (result.Success)
                {
                    Console.WriteLine($"✅ 已平仓: {position.Symbol} {position.Side} {position.Quantity:F8}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 平仓失败: {position.Symbol} - {ex.Message}");
            }
        }
    }
    
    private void RaiseStatusChanged(string status)
    {
        StatusChanged?.Invoke(this, new TradingStatusChangedEventArgs 
        { 
            Status = status,
            Timestamp = DateTime.UtcNow
        });
    }
    
    private void RaiseError(string message, Exception exception)
    {
        ErrorOccurred?.Invoke(this, new TradingErrorEventArgs
        {
            Message = message,
            Exception = exception,
            Timestamp = DateTime.UtcNow
        });
    }
    
    /// <summary>
    /// 紧急停止事件处理
    /// </summary>
    private async void OnEmergencyStopTriggered(object? sender, EmergencyStopEventArgs e)
    {
        Console.WriteLine($"🚨 [LiveTradingEngine] 收到紧急停止信号: {e.Reason}");
        
        // 自动停止交易引擎
        await StopAsync();
    }
    
    /// <summary>
    /// 风险违规事件处理
    /// </summary>
    private void OnRiskViolationDetected(object? sender, RiskViolationEventArgs e)
    {
        Console.WriteLine($"⚠️ [LiveTradingEngine] 风险违规: {e.Category} - {e.Message}");
    }
    
    /// <summary>
    /// 获取风险报告
    /// </summary>
    public async Task<RiskReport?> GetRiskReportAsync()
    {
        if (_riskController == null)
        {
            return null;
        }
        
        return await _riskController.GetRiskReportAsync();
    }
    
    public void Dispose()
    {
        if (!_disposed)
        {
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
            _disposed = true;
        }
    }
}

/// <summary>
/// 实盘交易配置
/// </summary>
public class LiveTradingConfig
{
    public string StrategyName { get; set; } = string.Empty;
    public string Symbol { get; set; } = "BTCUSDT";
    public string Timeframe { get; set; } = "5m";
    public decimal Leverage { get; set; } = 10m;
    public bool EnableOrderSync { get; set; } = true;
    public int OrderSyncIntervalSeconds { get; set; } = 30;
    public bool CloseAllPositionsOnStop { get; set; } = false;
}

// 事件参数
public class TradingStatusChangedEventArgs : EventArgs
{
    public string Status { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
}

public class SignalGeneratedEventArgs : EventArgs
{
    public Signal Signal { get; set; } = new();
    public Candlestick Candle { get; set; } = new();
}

public class OrderExecutedEventArgs : EventArgs
{
    public Order Order { get; set; } = new();
    public Signal Signal { get; set; } = new();
}

public class TradingErrorEventArgs : EventArgs
{
    public string Message { get; set; } = string.Empty;
    public Exception? Exception { get; set; }
    public DateTime Timestamp { get; set; }
}

