using System;
using System.Threading.Tasks;
using Prophet.Client.Backtest.Models;
using Prophet.Client.Backtest.Strategy;
using Prophet.Client.Trading.Engine;
using Prophet.Client.Trading.Exchanges;
using Prophet.Client.Trading.Exchanges.Binance;
using Prophet.Client.Trading.Models;
using Prophet.Client.Trading.OrderManagement;
using Prophet.Client.Trading.RiskControl;
using Prophet.Client.Trading.Storage;

namespace Prophet.Client.Trading.Examples;

/// <summary>
/// 实盘交易使用示例
/// </summary>
public class LiveTradingExample
{
    /// <summary>
    /// 完整的实盘交易流程示例
    /// </summary>
    public static async Task CompleteExample()
    {
        // 1. 初始化交易所
        var exchange = new BinanceExchange();
        var exchangeConfig = new ExchangeConfig
        {
            ApiKey = "your_api_key",
            ApiSecret = "your_api_secret",
            UseTestnet = true  // 建议先在测试网测试
        };
        
        await exchange.InitializeAsync(exchangeConfig);
        
        // 2. 配置订单管理器
        var orderConfig = new LiveOrderConfig
        {
            Symbol = "BTCUSDT",
            Leverage = 10m,
            PositionSizePercent = 0.95m,
            TakerFeeRate = 0.0004m,
            EnableTrailingStop = false,
            MaxOpenPositions = 1
        };
        
        var orderManager = new LiveOrderManager(
            exchange,
            orderConfig);
        
        // 初始化资金
        var account = await exchange.GetAccountInfoAsync();
        orderManager.Initialize(account.AvailableBalance);
        
        // 3. 配置策略生成器
        var strategyGenerator = new DslStrategySignalGenerator();
        var backtestConfig = new BacktestConfig
        {
            Symbol = "BTCUSDT",
            StartDate = DateTime.UtcNow.AddDays(-7),
            EndDate = DateTime.UtcNow,
            InitialCapital = 10000m,
            Interval = "5m"
        };
        
        string strategyCode = @"
            ALL {
                $(5m).MACD().macd > $(5m).MACD().signal,
                $(5m).RSI().value > 50,
                $(5m).RSI().value < 70
            } = BUY
            
            ALL {
                $(5m).MACD().macd < $(5m).MACD().signal,
                $(5m).RSI().value < 50
            } = SELL
        ";
        
        strategyGenerator.Initialize(strategyCode, backtestConfig);
        
        // 4. 配置实盘交易引擎
        var tradingConfig = new LiveTradingConfig
        {
            StrategyName = "MACD_RSI_Strategy",
            Symbol = "BTCUSDT",
            Timeframe = "5m",
            Leverage = 10m,
            EnableOrderSync = true,
            OrderSyncIntervalSeconds = 30,
            CloseAllPositionsOnStop = false
        };
        
        var storage = new LiveOrderStorage();
        
        // 可选：创建风险控制器
        var riskConfig = new RiskConfig
        {
            InitialCapital = 10000m,
            MaxDrawdown = 0.15m,
            MaxOpenPositions = 3,
            MaxDailyTrades = 20
        };
        var riskController = new RiskController(exchange, riskConfig);
        
        var tradingEngine = new LiveTradingEngine(
            exchange,
            orderManager,
            strategyGenerator,
            tradingConfig,
            riskController,
            storage);
        
        // 5. 订阅事件
        tradingEngine.StatusChanged += (sender, e) =>
        {
            Console.WriteLine($"📊 状态变更: {e.Status} at {e.Timestamp:HH:mm:ss}");
        };
        
        tradingEngine.SignalGenerated += (sender, e) =>
        {
            Console.WriteLine($"💡 信号生成: {e.Signal.Action} @ {e.Signal.SignalPrice:F2}");
        };
        
        tradingEngine.OrderExecuted += (sender, e) =>
        {
            Console.WriteLine($"✅ 订单执行: {e.Order.Id} {e.Order.Side} {e.Order.Quantity:F8} @ {e.Order.OpenPrice:F2}");
        };
        
        tradingEngine.ErrorOccurred += (sender, e) =>
        {
            Console.WriteLine($"❌ 错误: {e.Message}");
        };
        
        // 6. 启动实盘交易
        Console.WriteLine("🚀 启动实盘交易...");
        await tradingEngine.StartAsync();
        
        // 7. 运行一段时间后停止（实际使用中可以持续运行）
        await Task.Delay(TimeSpan.FromHours(1));
        
        // 8. 停止交易
        Console.WriteLine("⏹️ 停止实盘交易...");
        await tradingEngine.StopAsync();
        
        // 9. 清理资源
        tradingEngine.Dispose();
        exchange.Dispose();
        
        Console.WriteLine("✅ 实盘交易完成");
    }
    
    /// <summary>
    /// 简单的订单管理示例
    /// </summary>
    public static async Task SimpleOrderManagementExample()
    {
        // 初始化
        var exchange = new BinanceExchange();
        var config = new ExchangeConfig
        {
            ApiKey = "your_api_key",
            ApiSecret = "your_api_secret",
            UseTestnet = true
        };
        await exchange.InitializeAsync(config);
        
        // 创建订单管理器
        var orderConfig = new LiveOrderConfig
        {
            Symbol = "BTCUSDT",
            Leverage = 10m
        };
        
        var orderManager = new LiveOrderManager(exchange, orderConfig);
        orderManager.Initialize(1000m); // 初始资金1000 USDT
        
        // 同步状态
        await orderManager.SynchronizeStateAsync();
        
        Console.WriteLine($"当前权益: {orderManager.CurrentEquity:F2} USDT");
        Console.WriteLine($"当前现金: {orderManager.CurrentCash:F2} USDT");
        Console.WriteLine($"当前持仓: {orderManager.CurrentPosition:F8}");
        
        // 查看未完成订单
        var openOrders = orderManager.OpenOrders;
        Console.WriteLine($"未完成订单数: {openOrders.Count}");
        
        // 查看已完成订单
        var closedOrders = orderManager.ClosedOrders;
        Console.WriteLine($"已完成订单数: {closedOrders.Count}");
    }
    
    /// <summary>
    /// 订单同步示例
    /// </summary>
    public static async Task OrderSyncExample()
    {
        var exchange = new BinanceExchange();
        // ... 初始化代码 ...
        
        var synchronizer = new LiveOrderSynchronizer(exchange);
        
        // 订阅同步事件
        synchronizer.OrderSynced += (sender, e) =>
        {
            Console.WriteLine($"✅ 订单同步完成");
            Console.WriteLine($"   时间: {e.SyncTime:HH:mm:ss}");
            Console.WriteLine($"   本地订单: {e.LocalOrderCount}");
            Console.WriteLine($"   交易所订单: {e.ExchangeOrderCount}");
        };
        
        // 启动自动同步（每30秒）
        synchronizer.StartAutoSync(30);
        
        // 运行一段时间...
        await Task.Delay(TimeSpan.FromMinutes(5));
        
        // 停止同步
        synchronizer.StopAutoSync();
    }
    
    /// <summary>
    /// 数据库存储示例
    /// </summary>
    public static async Task StorageExample()
    {
        var storage = new LiveOrderStorage();
        
        // 创建交易会话
        var session = new LiveSession
        {
            StrategyName = "Test_Strategy",
            Symbol = "BTCUSDT",
            Leverage = 10m,
            InitialCapital = 1000m,
            StartTime = DateTime.UtcNow,
            Status = "Active"
        };
        
        await storage.CreateSessionAsync(session);
        
        // 获取所有会话
        var allSessions = await storage.GetAllSessionsAsync();
        Console.WriteLine($"总会话数: {allSessions.Count}");
        
        foreach (var s in allSessions)
        {
            Console.WriteLine($"会话: {s.Id}");
            Console.WriteLine($"  策略: {s.StrategyName}");
            Console.WriteLine($"  开始时间: {s.StartTime:yyyy-MM-dd HH:mm}");
            Console.WriteLine($"  状态: {s.Status}");
            
            if (s.TotalProfit.HasValue)
            {
                Console.WriteLine($"  总盈亏: {s.TotalProfit:F2} USDT");
            }
        }
    }
}

