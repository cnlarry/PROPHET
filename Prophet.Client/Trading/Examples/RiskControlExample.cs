using System;
using System.Threading.Tasks;
using Prophet.Client.Models;
using Prophet.Client.Trading.Exchanges.Binance;
using Prophet.Client.Trading.Models;
using Prophet.Client.Trading.RiskControl;

namespace Prophet.Client.Trading.Examples;

/// <summary>
/// 风险控制使用示例
/// </summary>
public class RiskControlExample
{
    /// <summary>
    /// 完整的风险控制示例
    /// </summary>
    public static async Task CompleteExample()
    {
        // 1. 初始化交易所
        var exchange = new BinanceExchange();
        await exchange.InitializeAsync(new ExchangeConfig
        {
            ApiKey = "your_api_key",
            ApiSecret = "your_api_secret",
            UseTestnet = true
        });
        
        // 2. 配置风险参数
        var riskConfig = new RiskConfig
        {
            InitialCapital = 1000m,
            MaxOpenPositions = 3,
            MaxDrawdown = 0.15m, // 15%
            MaxRiskPerTrade = 0.02m, // 2%
            MaxDailyTrades = 10,
            MinBalance = 100m,
            MaxConsecutiveLosses = 5,
            EnableEmergencyStopOnMaxDrawdown = true,
            EnableEmergencyStopOnConsecutiveLosses = true,
            CloseAllPositionsOnEmergencyStop = true
        };
        
        // 3. 创建风险控制器
        var riskController = new RiskController(exchange, riskConfig);
        
        // 4. 订阅风险事件
        riskController.RiskViolationDetected += (sender, e) =>
        {
            Console.WriteLine($"⚠️ 风险违规: {e.Category} - {e.Message}");
        };
        
        riskController.EmergencyStopTriggered += (sender, e) =>
        {
            Console.WriteLine($"🚨 紧急停止: {e.Reason}");
            Console.WriteLine($"   当前权益: {e.CurrentEquity:F2}");
            Console.WriteLine($"   最大回撤: {e.MaxDrawdown:P2}");
        };
        
        riskController.RiskWarningIssued += (sender, e) =>
        {
            Console.WriteLine($"⚠️ 风险警告: {e.Category} - {e.Message}");
        };
        
        // 5. 模拟交易信号检查
        var signal = new Signal
        {
            Action = SignalAction.BUY,
            SignalPrice = 30000m,
            StopLoss = 29400m, // 2% 止损
            TakeProfit = 31200m // 4% 止盈
        };
        
        var currentEquity = 1000m;
        
        // 6. 检查信号
        var checkResult = await riskController.CheckSignalAsync(signal, currentEquity);
        
        if (checkResult.IsApproved)
        {
            Console.WriteLine($"✅ 信号通过风险检查");
        }
        else
        {
            Console.WriteLine($"❌ 信号被拒绝: {checkResult.RejectReason}");
            Console.WriteLine($"   风险等级: {checkResult.RiskLevel}");
        }
        
        // 7. 获取风险报告
        var report = await riskController.GetRiskReportAsync();
        Console.WriteLine(report.GenerateReport());
    }
    
    /// <summary>
    /// 账户监控示例
    /// </summary>
    public static async Task AccountMonitoringExample()
    {
        var exchange = new BinanceExchange();
        // ... 初始化 ...
        
        var riskConfig = new RiskConfig
        {
            EnableRealtimeMonitoring = true,
            MonitoringIntervalSeconds = 60
        };
        
        var accountMonitor = new AccountMonitor(exchange, riskConfig);
        
        // 订阅异常事件
        accountMonitor.AnomalyDetected += (sender, e) =>
        {
            Console.WriteLine($"🚨 检测到异常: {e.Type}");
            Console.WriteLine($"   描述: {e.Description}");
            Console.WriteLine($"   严重程度: {e.Severity}");
        };
        
        // 订阅余额变化事件
        accountMonitor.BalanceChanged += (sender, e) =>
        {
            Console.WriteLine($"💰 余额变化: {e.OldBalance:F2} → {e.NewBalance:F2}");
            Console.WriteLine($"   变化: {e.Change:F2} ({e.ChangePercent:P2})");
        };
        
        // 启动监控
        accountMonitor.StartMonitoring();
        
        // 运行一段时间...
        await Task.Delay(TimeSpan.FromMinutes(5));
        
        // 停止监控
        accountMonitor.StopMonitoring();
    }
    
    /// <summary>
    /// 仓位监控示例
    /// </summary>
    public static async Task PositionMonitoringExample()
    {
        var exchange = new BinanceExchange();
        // ... 初始化 ...
        
        var riskConfig = new RiskConfig
        {
            MaxPositionPerSymbol = 0.30m,
            MaxPositionRiskPercent = 0.50m
        };
        
        var positionMonitor = new PositionMonitor(exchange, riskConfig);
        
        // 订阅高风险事件
        positionMonitor.HighRiskDetected += (sender, e) =>
        {
            Console.WriteLine($"⚠️ 高风险警告: {e.Type}");
            Console.WriteLine($"   {e.Description}");
        };
        
        // 获取风险指标
        var metrics = await positionMonitor.GetRiskMetricsAsync();
        
        Console.WriteLine($"📊 仓位风险指标:");
        Console.WriteLine($"   活跃仓位: {metrics.ActivePositionCount}");
        Console.WriteLine($"   总仓位价值: {metrics.TotalPositionValue:F2} USDT");
        Console.WriteLine($"   总保证金: {metrics.TotalMargin:F2} USDT");
        Console.WriteLine($"   未实现盈亏: {metrics.TotalUnrealizedPnl:F2} USDT");
        Console.WriteLine($"   风险占比: {metrics.TotalRiskPercent:P2}");
        Console.WriteLine($"   杠杆利用率: {metrics.LeverageUtilization:F2}x");
        
        // 检查各个仓位
        foreach (var position in metrics.PositionDetails)
        {
            Console.WriteLine($"\n📦 {position.Symbol}:");
            Console.WriteLine($"   方向: {position.Side}");
            Console.WriteLine($"   数量: {position.Quantity:F8}");
            Console.WriteLine($"   开仓价: {position.EntryPrice:F2}");
            Console.WriteLine($"   标记价: {position.MarkPrice:F2}");
            Console.WriteLine($"   未实现盈亏: {position.UnrealizedPnl:F2}");
            Console.WriteLine($"   风险占比: {position.RiskPercent:P2}");
            Console.WriteLine($"   强平价: {position.LiquidationPrice:F2}");
        }
        
        // 检查是否接近强平
        var nearLiquidation = await positionMonitor.IsNearLiquidationAsync(0.05m);
        if (nearLiquidation)
        {
            Console.WriteLine($"\n🚨 警告: 某些仓位接近强平价!");
        }
    }
    
    /// <summary>
    /// 回撤跟踪示例
    /// </summary>
    public static void DrawdownTrackingExample()
    {
        var tracker = new DrawdownTracker(1000m);
        
        // 模拟权益变化
        tracker.Update(1050m); // +5%
        tracker.Update(1100m); // +10% (新高)
        tracker.Update(1050m); // -4.5%
        tracker.Update(1000m); // -9%
        tracker.Update(950m);  // -13.6%
        tracker.Update(1020m); // 恢复
        
        var stats = tracker.GetStatistics();
        
        Console.WriteLine($"📉 回撤统计:");
        Console.WriteLine($"   当前权益: {stats.CurrentEquity:F2}");
        Console.WriteLine($"   峰值权益: {stats.PeakEquity:F2}");
        Console.WriteLine($"   当前回撤: {stats.CurrentDrawdown:P2}");
        Console.WriteLine($"   最大回撤: {stats.MaxDrawdown:P2}");
        Console.WriteLine($"   回撤持续时间: {stats.DrawdownDuration}");
        Console.WriteLine($"   平均回撤: {stats.AverageDrawdown:P2}");
        Console.WriteLine($"   回撤次数: {stats.DrawdownCount}");
        Console.WriteLine($"   恢复率: {stats.RecoveryRate:P2}");
    }
    
    /// <summary>
    /// 交易统计示例
    /// </summary>
    public static void TradingStatisticsExample()
    {
        var statistics = new TradingStatistics();
        
        // 模拟一些交易记录
        for (int i = 0; i < 10; i++)
        {
            var order = new Order
            {
                Id = $"ORDER_{i}",
                OpenTime = DateTime.UtcNow.AddHours(-i),
                CloseTime = DateTime.UtcNow.AddHours(-i).AddMinutes(30),
                    Status = OrderStatus.CLOSED,
                Profit = (i % 3 == 0) ? -50m : 100m, // 模拟盈亏
                Fee = 2m
            };
            
            statistics.RecordTrade(order);
        }
        
        var summary = statistics.GetSummary();
        
        Console.WriteLine($"📊 交易统计:");
        Console.WriteLine($"   总交易次数: {summary.TotalTrades}");
        Console.WriteLine($"   盈利次数: {summary.WinningTrades}");
        Console.WriteLine($"   亏损次数: {summary.LosingTrades}");
        Console.WriteLine($"   胜率: {summary.WinRate:P2}");
        Console.WriteLine($"   总盈亏: {summary.TotalProfit:F2}");
        Console.WriteLine($"   总手续费: {summary.TotalFees:F2}");
        Console.WriteLine($"   平均盈利: {summary.AverageProfit:F2}");
        Console.WriteLine($"   平均盈利交易: {summary.AverageWin:F2}");
        Console.WriteLine($"   平均亏损交易: {summary.AverageLoss:F2}");
        Console.WriteLine($"   最大盈利: {summary.LargestWin:F2}");
        Console.WriteLine($"   最大亏损: {summary.LargestLoss:F2}");
        Console.WriteLine($"   盈利因子: {summary.ProfitFactor:F2}");
        Console.WriteLine($"   连续亏损: {summary.ConsecutiveLosses}");
        Console.WriteLine($"   最大连续亏损: {summary.MaxConsecutiveLosses}");
        Console.WriteLine($"   最大连续盈利: {summary.MaxConsecutiveWins}");
    }
    
    /// <summary>
    /// 紧急停止示例
    /// </summary>
    public static async Task EmergencyStopExample()
    {
        var exchange = new BinanceExchange();
        // ... 初始化 ...
        
        var riskConfig = new RiskConfig
        {
            MaxDrawdown = 0.15m,
            EnableEmergencyStopOnMaxDrawdown = true,
            CloseAllPositionsOnEmergencyStop = true
        };
        
        var riskController = new RiskController(exchange, riskConfig);
        
        // 订阅紧急停止事件
        riskController.EmergencyStopTriggered += (sender, e) =>
        {
            Console.WriteLine($"🚨🚨🚨 紧急停止已触发!");
            Console.WriteLine($"   原因: {e.Reason}");
            Console.WriteLine($"   时间: {e.Timestamp:yyyy-MM-dd HH:mm:ss}");
            Console.WriteLine($"   当前权益: {e.CurrentEquity:F2}");
            Console.WriteLine($"   最大回撤: {e.MaxDrawdown:P2}");
            Console.WriteLine($"\n🔴 所有交易已停止，仓位已平仓");
        };
        
        // 模拟触发紧急停止
        await riskController.TriggerEmergencyStopAsync("测试紧急停止机制");
        
        // 检查状态
        if (riskController.IsEmergencyStopActivated)
        {
            Console.WriteLine($"\n⚠️ 紧急停止已激活，所有新信号将被拒绝");
        }
        
        // 重置紧急停止
        riskController.ResetEmergencyStop();
        Console.WriteLine($"\n✅ 紧急停止已重置，可以继续交易");
    }
}

