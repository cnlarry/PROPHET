using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Prophet.Client.Backtest.Models;
using Prophet.Client.Backtest.Strategy;
using Prophet.Client.Trading.Exchanges;
using Prophet.Client.Trading.Models;
using Prophet.Client.Trading.OrderManagement;
using Prophet.Client.Trading.RiskControl;
using Prophet.Client.Trading.Storage;

namespace Prophet.Client.Trading.Engine;

/// <summary>
/// 实盘交易实例管理器
/// 管理所有实盘实例的生命周期
/// </summary>
public class TradingInstanceManager : IDisposable
{
    private readonly ConcurrentDictionary<Guid, LiveTradingEngine> _runningEngines;
    private readonly ConcurrentDictionary<Guid, TradingInstanceConfig> _instanceConfigs;
    private readonly TradingInstanceStorage _storage;
    private bool _disposed;
    
    // 事件
    public event EventHandler<InstanceEventArgs>? InstanceCreated;
    public event EventHandler<InstanceEventArgs>? InstanceDeleted;
    public event EventHandler<InstanceStatusChangedEventArgs>? InstanceStatusChanged;
#pragma warning disable CS0067 // 事件从未使用（预留给未来功能）
    public event EventHandler<InstanceSnapshotEventArgs>? InstanceSnapshotUpdated;
#pragma warning restore CS0067
    public event EventHandler<InstanceErrorEventArgs>? InstanceError;
    
    public TradingInstanceManager(TradingInstanceStorage? storage = null)
    {
        _runningEngines = new ConcurrentDictionary<Guid, LiveTradingEngine>();
        _instanceConfigs = new ConcurrentDictionary<Guid, TradingInstanceConfig>();
        _storage = storage ?? new TradingInstanceStorage();
    }
    
    /// <summary>
    /// 初始化管理器（从数据库加载所有实例配置）
    /// </summary>
    public async Task InitializeAsync()
    {
        Console.WriteLine($"[TradingInstanceManager] 初始化中...");
        
        var configs = await _storage.LoadAllInstancesAsync();
        
        foreach (var config in configs)
        {
            _instanceConfigs[config.Id] = config;
        }
        
        Console.WriteLine($"[TradingInstanceManager] 已加载 {configs.Count} 个实例配置");
    }
    
    /// <summary>
    /// 创建新实例
    /// </summary>
    public async Task<Guid> CreateInstanceAsync(TradingInstanceConfig config)
    {
        if (string.IsNullOrWhiteSpace(config.Name))
        {
            config.Name = config.GenerateDefaultName();
        }
        
        // 保存到数据库
        await _storage.SaveInstanceAsync(config);
        
        // 添加到内存
        _instanceConfigs[config.Id] = config;
        
        Console.WriteLine($"[TradingInstanceManager] 已创建实例: {config.Name} ({config.Id})");
        
        InstanceCreated?.Invoke(this, new InstanceEventArgs { InstanceId = config.Id });
        
        return config.Id;
    }
    
    /// <summary>
    /// 启动指定实例
    /// </summary>
    public async Task<bool> StartInstanceAsync(Guid instanceId)
    {
        try
        {
            if (!_instanceConfigs.TryGetValue(instanceId, out var config))
            {
                Console.WriteLine($"[TradingInstanceManager] 实例不存在: {instanceId}");
                return false;
            }
            
            if (_runningEngines.ContainsKey(instanceId))
            {
                Console.WriteLine($"[TradingInstanceManager] 实例已在运行: {config.Name}");
                return false;
            }
            
            Console.WriteLine($"[TradingInstanceManager] 启动实例: {config.Name}");
            
            // 通知状态变化
            RaiseStatusChanged(instanceId, TradingInstanceStatus.Starting);
            
            // 创建交易所实例
            var exchange = CreateExchange(config);
            await exchange.InitializeAsync(config.ExchangeConfig);
            
            // 创建风险控制器
            var riskController = new RiskController(exchange, config.RiskConfig);
            
            // 创建订单管理器
            var orderConfig = new LiveOrderConfig
            {
                Symbol = config.Symbol,
                Leverage = 10m,
                TradingSessionId = instanceId.ToString()
            };
            var orderManager = new LiveOrderManager(exchange, orderConfig);
            
            // 创建策略生成器
            var strategyGenerator = CreateStrategyGenerator(config);
            
            // 创建交易配置
            var tradingConfig = new LiveTradingConfig
            {
                Symbol = config.Symbol,
                Timeframe = config.Timeframe
            };
            
            // 创建交易引擎
            var engine = new LiveTradingEngine(
                exchange,
                orderManager,
                strategyGenerator,
                tradingConfig,
                riskController)
            {
                InstanceId = instanceId,
                InstanceName = config.Name
            };
            
            // 订阅引擎事件
            SubscribeEngineEvents(engine, instanceId);
            
            // 启动引擎
            await engine.StartAsync();
            
            // 添加到运行中的引擎列表
            _runningEngines[instanceId] = engine;
            
            // 更新最后运行时间
            config.LastRunAt = DateTime.UtcNow;
            await _storage.SaveInstanceAsync(config);
            
            // 通知状态变化
            RaiseStatusChanged(instanceId, TradingInstanceStatus.Running);
            
            Console.WriteLine($"[TradingInstanceManager] 实例启动成功: {config.Name}");
            
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[TradingInstanceManager] 启动实例失败: {ex.Message}");
            RaiseStatusChanged(instanceId, TradingInstanceStatus.Error);
            RaiseError(instanceId, ex.Message);
            return false;
        }
    }
    
    /// <summary>
    /// 停止指定实例
    /// </summary>
    public async Task<bool> StopInstanceAsync(Guid instanceId)
    {
        try
        {
            if (!_instanceConfigs.TryGetValue(instanceId, out var config))
            {
                return false;
            }
            
            if (!_runningEngines.TryRemove(instanceId, out var engine))
            {
                Console.WriteLine($"[TradingInstanceManager] 实例未运行: {config.Name}");
                return false;
            }
            
            Console.WriteLine($"[TradingInstanceManager] 停止实例: {config.Name}");
            
            RaiseStatusChanged(instanceId, TradingInstanceStatus.Stopping);
            
            await engine.StopAsync();
            engine.Dispose();
            
            RaiseStatusChanged(instanceId, TradingInstanceStatus.Stopped);
            
            Console.WriteLine($"[TradingInstanceManager] 实例已停止: {config.Name}");
            
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[TradingInstanceManager] 停止实例失败: {ex.Message}");
            RaiseError(instanceId, ex.Message);
            return false;
        }
    }
    
    /// <summary>
    /// 暂停指定实例
    /// </summary>
    public async Task<bool> PauseInstanceAsync(Guid instanceId)
    {
        if (!_runningEngines.TryGetValue(instanceId, out var engine))
        {
            return false;
        }
        
        await engine.PauseAsync();
        RaiseStatusChanged(instanceId, TradingInstanceStatus.Paused);
        
        return true;
    }
    
    /// <summary>
    /// 恢复指定实例
    /// </summary>
    public async Task<bool> ResumeInstanceAsync(Guid instanceId)
    {
        if (!_runningEngines.TryGetValue(instanceId, out var engine))
        {
            return false;
        }
        
        await engine.ResumeAsync();
        RaiseStatusChanged(instanceId, TradingInstanceStatus.Running);
        
        return true;
    }
    
    /// <summary>
    /// 删除实例
    /// </summary>
    public async Task<bool> DeleteInstanceAsync(Guid instanceId)
    {
        // 先停止（如果正在运行）
        if (_runningEngines.ContainsKey(instanceId))
        {
            await StopInstanceAsync(instanceId);
        }
        
        // 从内存中移除
        if (!_instanceConfigs.TryRemove(instanceId, out var config))
        {
            return false;
        }
        
        // 从数据库删除
        await _storage.DeleteInstanceAsync(instanceId);
        
        Console.WriteLine($"[TradingInstanceManager] 已删除实例: {config.Name}");
        
        InstanceDeleted?.Invoke(this, new InstanceEventArgs { InstanceId = instanceId });
        
        return true;
    }
    
    /// <summary>
    /// 更新实例配置
    /// </summary>
    public async Task<bool> UpdateInstanceConfigAsync(TradingInstanceConfig config)
    {
        if (!_instanceConfigs.ContainsKey(config.Id))
        {
            return false;
        }
        
        // 如果实例正在运行，不允许修改
        if (_runningEngines.ContainsKey(config.Id))
        {
            Console.WriteLine($"[TradingInstanceManager] 无法更新运行中的实例配置: {config.Name}");
            return false;
        }
        
        _instanceConfigs[config.Id] = config;
        await _storage.SaveInstanceAsync(config);
        
        return true;
    }
    
    // ========== 批量操作 ==========
    
    /// <summary>
    /// 启动所有实例
    /// </summary>
    public async Task<int> StartAllAsync()
    {
        int successCount = 0;
        
        foreach (var config in _instanceConfigs.Values.Where(c => c.IsEnabled))
        {
            if (await StartInstanceAsync(config.Id))
            {
                successCount++;
            }
        }
        
        return successCount;
    }
    
    /// <summary>
    /// 停止所有实例
    /// </summary>
    public async Task<int> StopAllAsync()
    {
        int successCount = 0;
        
        var runningIds = _runningEngines.Keys.ToList();
        
        foreach (var instanceId in runningIds)
        {
            if (await StopInstanceAsync(instanceId))
            {
                successCount++;
            }
        }
        
        return successCount;
    }
    
    /// <summary>
    /// 按交易所批量启动
    /// </summary>
    public async Task<int> StartByExchangeAsync(string exchange)
    {
        int successCount = 0;
        
        var configs = _instanceConfigs.Values
            .Where(c => c.Exchange.Equals(exchange, StringComparison.OrdinalIgnoreCase) && c.IsEnabled);
        
        foreach (var config in configs)
        {
            if (await StartInstanceAsync(config.Id))
            {
                successCount++;
            }
        }
        
        return successCount;
    }
    
    /// <summary>
    /// 按交易所批量停止
    /// </summary>
    public async Task<int> StopByExchangeAsync(string exchange)
    {
        int successCount = 0;
        
        var configs = _instanceConfigs.Values
            .Where(c => c.Exchange.Equals(exchange, StringComparison.OrdinalIgnoreCase));
        
        foreach (var config in configs)
        {
            if (_runningEngines.ContainsKey(config.Id))
            {
                if (await StopInstanceAsync(config.Id))
                {
                    successCount++;
                }
            }
        }
        
        return successCount;
    }
    
    // ========== 查询方法 ==========
    
    /// <summary>
    /// 获取所有实例配置
    /// </summary>
    public List<TradingInstanceConfig> GetAllInstances()
    {
        return _instanceConfigs.Values.ToList();
    }
    
    /// <summary>
    /// 获取实例配置
    /// </summary>
    public TradingInstanceConfig? GetInstanceConfig(Guid instanceId)
    {
        return _instanceConfigs.TryGetValue(instanceId, out var config) ? config : null;
    }
    
    /// <summary>
    /// 获取实例状态
    /// </summary>
    public TradingInstanceStatus GetInstanceStatus(Guid instanceId)
    {
        if (!_instanceConfigs.ContainsKey(instanceId))
        {
            return TradingInstanceStatus.Error;
        }
        
        if (!_runningEngines.TryGetValue(instanceId, out var engine))
        {
            return TradingInstanceStatus.Stopped;
        }
        
        return engine.Status switch
        {
            TradingEngineStatus.Running => TradingInstanceStatus.Running,
            TradingEngineStatus.Paused => TradingInstanceStatus.Paused,
            TradingEngineStatus.Stopping => TradingInstanceStatus.Stopping,
            TradingEngineStatus.Starting => TradingInstanceStatus.Starting,
            _ => TradingInstanceStatus.Stopped
        };
    }
    
    /// <summary>
    /// 获取运行中的实例数量
    /// </summary>
    public int GetRunningCount()
    {
        return _runningEngines.Count;
    }
    
    /// <summary>
    /// 获取实例的当前快照
    /// </summary>
    public async Task<TradingInstanceSnapshot?> GetInstanceSnapshotAsync(Guid instanceId)
    {
        if (!_runningEngines.TryGetValue(instanceId, out var engine))
        {
            return null;
        }
        
        // 从引擎获取实时数据
        var riskReport = await engine.GetRiskReportAsync();
        
        if (riskReport == null)
        {
            return null;
        }
        
        return new TradingInstanceSnapshot
        {
            InstanceId = instanceId,
            Timestamp = DateTime.UtcNow,
            Status = GetInstanceStatus(instanceId),
            TotalEquity = riskReport.CurrentEquity,
            AvailableBalance = riskReport.AvailableBalance,
            UsedMargin = riskReport.TotalMargin,
            UnrealizedPnL = 0, // TODO: 从engine获取
            TodayPnL = 0, // TODO: 计算
            TodayPnLPercent = 0,
            PositionCount = riskReport.ActivePositions,
            CurrentDrawdown = riskReport.CurrentDrawdown,
            MaxDrawdown = riskReport.MaxDrawdown,
            RiskLevel = riskReport.RiskLevel,
            EmergencyStopActivated = riskReport.EmergencyStopActivated,
            TotalTrades = 0, // TODO: 从统计获取
            TodayTrades = riskReport.TodayTrades,
            WinRate = riskReport.WinRate,
            ConsecutiveLosses = riskReport.ConsecutiveLosses
        };
    }
    
    // ========== 私有方法 ==========
    
    private IExchange CreateExchange(TradingInstanceConfig config)
    {
        // 根据配置创建对应的交易所实例
        return config.Exchange.ToLower() switch
        {
            "binance" => new Exchanges.Binance.BinanceExchange(),
            _ => throw new NotSupportedException($"不支持的交易所: {config.Exchange}")
        };
    }
    
    private IStrategySignalGenerator CreateStrategyGenerator(TradingInstanceConfig config)
    {
        if (string.IsNullOrWhiteSpace(config.StrategyCode))
        {
            throw new InvalidOperationException($"实例 {config.Name} 未配置策略代码 (StrategyCode)");
        }
        
        Console.WriteLine($"[TradingInstanceManager] 创建策略生成器: {config.StrategyName} (v{config.StrategyVersion})");
        
        // 复用回测的 DSL 策略信号生成器（基于 Prophet.Core 引擎）
        var backtestConfig = new BacktestConfig
        {
            Symbol = config.Symbol,
            Interval = config.Timeframe,
            SignalSamplingInterval = config.Timeframe,
            Leverage = config.RiskConfig?.Leverage ?? 10m,
            InitialCapital = config.CapitalConfig?.InitialCapital ?? config.RiskConfig?.InitialCapital ?? 1000m,
            PositionSizePercent = config.RiskConfig?.PositionSizePercent ?? 0.05m
        };
        
        var generator = new DslStrategySignalGenerator();
        generator.Initialize(config.StrategyCode, backtestConfig);
        
        Console.WriteLine($"[TradingInstanceManager] 策略生成器创建成功");
        return generator;
    }
    
    private void SubscribeEngineEvents(LiveTradingEngine engine, Guid instanceId)
    {
        engine.StatusChanged += (s, e) =>
        {
            // 根据引擎状态更新实例状态
            var engineSender = s as LiveTradingEngine;
            if (engineSender == null) return;
            
            var status = engineSender.Status switch
            {
                TradingEngineStatus.Running => TradingInstanceStatus.Running,
                TradingEngineStatus.Paused => TradingInstanceStatus.Paused,
                TradingEngineStatus.Stopping => TradingInstanceStatus.Stopping,
                TradingEngineStatus.Starting => TradingInstanceStatus.Starting,
                TradingEngineStatus.Stopped => TradingInstanceStatus.Stopped,
                TradingEngineStatus.Error => TradingInstanceStatus.Error,
                _ => TradingInstanceStatus.Stopped
            };
            
            RaiseStatusChanged(instanceId, status);
        };
        
        engine.ErrorOccurred += (s, e) =>
        {
            RaiseError(instanceId, e.Message);
        };
    }
    
    private void RaiseStatusChanged(Guid instanceId, TradingInstanceStatus newStatus)
    {
        InstanceStatusChanged?.Invoke(this, new InstanceStatusChangedEventArgs
        {
            InstanceId = instanceId,
            NewStatus = newStatus,
            Timestamp = DateTime.UtcNow
        });
    }
    
    private void RaiseError(Guid instanceId, string errorMessage)
    {
        InstanceError?.Invoke(this, new InstanceErrorEventArgs
        {
            InstanceId = instanceId,
            ErrorMessage = errorMessage,
            Timestamp = DateTime.UtcNow
        });
    }
    
    public void Dispose()
    {
        if (!_disposed)
        {
            // 停止所有运行中的实例
            StopAllAsync().Wait();
            
            _runningEngines.Clear();
            _instanceConfigs.Clear();
            
            _disposed = true;
        }
    }
}

// ========== 事件参数 ==========

public class InstanceEventArgs : EventArgs
{
    public Guid InstanceId { get; set; }
}

public class InstanceStatusChangedEventArgs : EventArgs
{
    public Guid InstanceId { get; set; }
    public TradingInstanceStatus NewStatus { get; set; }
    public DateTime Timestamp { get; set; }
}

public class InstanceSnapshotEventArgs : EventArgs
{
    public Guid InstanceId { get; set; }
    public TradingInstanceSnapshot Snapshot { get; set; } = new();
}

public class InstanceErrorEventArgs : EventArgs
{
    public Guid InstanceId { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
}

