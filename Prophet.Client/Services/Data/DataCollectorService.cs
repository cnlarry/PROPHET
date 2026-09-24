using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Prophet.Client.Database.Repositories;
using Prophet.Client.Services.Data.Collectors;
using Prophet.Client.Services.Market;

namespace Prophet.Client.Services.Data;

/// <summary>
/// 数据采集器基接口（非泛型，用于统一管理）
/// </summary>
internal interface IDataCollectorBase
{
    string Name { get; }
    bool IsEnabled { get; set; }
    bool IsRunning { get; }
    int IntervalSeconds { get; }
    DateTime? LastCollectTime { get; }
    bool LastCollectSuccess { get; }
    string? LastError { get; }
    Task StartAsync();
    Task StopAsync();
    Task<bool> CollectAsync();
}

/// <summary>
/// 数据采集服务
/// 统一管理所有数据采集器，负责启动、停止、监控等
/// </summary>
public class DataCollectorService : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly MarketDataRepository _marketDataRepository;
    private readonly IBinanceExchangeGateway _gateway;
    private readonly bool _ownsGateway;
    private readonly List<IDataCollectorBase> _collectors = new();
    private bool _isStarted = false;
    
    public DataCollectorService(
        HttpClient httpClient,
        MarketDataRepository marketDataRepository,
        IBinanceExchangeGateway? gateway = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _marketDataRepository = marketDataRepository ?? throw new ArgumentNullException(nameof(marketDataRepository));
        _gateway = gateway ?? new BinanceExchangeGateway();
        _ownsGateway = gateway == null;
        
        // 配置HttpClient超时
        // 注意：如果 HttpClient 已经开始发送请求，再修改 Timeout 会抛 InvalidOperationException。
        // 这里做保护，避免影响调用方（例如共享 HttpClient 的场景）。
        try
        {
            _httpClient.Timeout = TimeSpan.FromSeconds(30);
        }
        catch (InvalidOperationException)
        {
            // 忽略：保持调用方原始配置
        }
    }
    
    /// <summary>
    /// 注册K线数据采集器（REST API方式）
    /// </summary>
    public void RegisterKlineCollector(string symbol, string interval = "1m", int limit = 1000, int intervalSeconds = 60)
    {
        var collector = new BinanceKlineCollector(
            _gateway,
            _marketDataRepository,
            symbol,
            interval,
            limit,
            intervalSeconds
        );
        
        _collectors.Add(new DataCollectorWrapper<List<Prophet.Client.Models.Candlestick>>(collector));
        Console.WriteLine($"📝 [DataCollectorService] 注册K线采集器（REST）: {symbol} {interval}");
    }
    
    /// <summary>
    /// 注册WebSocket K线数据采集器（实时推送方式）
    /// </summary>
    public BinanceWebSocketKlineCollector RegisterWebSocketKlineCollector(string symbol, string interval = "1m")
    {
        var collector = new BinanceWebSocketKlineCollector(
            _marketDataRepository,
            _gateway,
            symbol,
            interval,
            enableGapFill: true
        );
        
        // WebSocket采集器不使用IDataCollectorBase接口，需要单独管理
        // 这里返回采集器实例，由调用者管理生命周期
        
        Console.WriteLine($"📝 [DataCollectorService] 注册WebSocket K线采集器: {symbol} {interval}");
        return collector;
    }
    
    /// <summary>
    /// 注册恐惧与贪婪指数采集器
    /// </summary>
    public void RegisterFearGreedCollector()
    {
        var collector = new FearGreedCollector(_httpClient);
        _collectors.Add(new DataCollectorWrapper<List<Prophet.Client.Models.FearGreedData>>(collector));
        Console.WriteLine($"📝 [DataCollectorService] 注册恐惧与贪婪指数采集器");
    }
    
    /// <summary>
    /// 注册资金费率采集器
    /// </summary>
    public void RegisterFundingRateCollector(string symbol)
    {
        var collector = new FundingRateCollector(_gateway, symbol);
        _collectors.Add(new DataCollectorWrapper<List<Prophet.Client.Models.FundingRateData>>(collector));
        Console.WriteLine($"📝 [DataCollectorService] 注册资金费率采集器: {symbol}");
    }
    
    /// <summary>
    /// 获取所有采集器状态
    /// </summary>
    public List<CollectorStatus> GetCollectorStatuses()
    {
        return _collectors.Select(c => new CollectorStatus
        {
            Name = c.Name,
            IsEnabled = c.IsEnabled,
            IsRunning = c.IsRunning,
            IntervalSeconds = c.IntervalSeconds,
            LastCollectTime = c.LastCollectTime,
            LastCollectSuccess = c.LastCollectSuccess,
            LastError = c.LastError
        }).ToList();
    }
    
    /// <summary>
    /// 启动所有采集器
    /// </summary>
    public async Task StartAllAsync()
    {
        if (_isStarted)
        {
            Console.WriteLine("⚠️ [DataCollectorService] 采集服务已在运行");
            return;
        }
        
        Console.WriteLine($"🚀 [DataCollectorService] 启动数据采集服务（共 {_collectors.Count} 个采集器）");
        
        var tasks = _collectors
            .Where(c => c.IsEnabled)
            .Select(c => c.StartAsync());
        
        await Task.WhenAll(tasks);
        
        _isStarted = true;
        Console.WriteLine($"✅ [DataCollectorService] 数据采集服务启动完成");
    }
    
    /// <summary>
    /// 停止所有采集器
    /// </summary>
    public async Task StopAllAsync()
    {
        if (!_isStarted)
        {
            return;
        }
        
        Console.WriteLine($"🛑 [DataCollectorService] 停止数据采集服务");
        
        var tasks = _collectors.Select(c => c.StopAsync());
        await Task.WhenAll(tasks);
        
        _isStarted = false;
        Console.WriteLine($"✅ [DataCollectorService] 数据采集服务已停止");
    }
    
    /// <summary>
    /// 手动触发指定采集器采集一次
    /// </summary>
    public async Task<bool> TriggerCollectAsync(string collectorName)
    {
        var collector = _collectors.FirstOrDefault(c => c.Name == collectorName);
        if (collector == null)
        {
            Console.WriteLine($"❌ [DataCollectorService] 未找到采集器: {collectorName}");
            return false;
        }
        
        return await collector.CollectAsync();
    }
    
    /// <summary>
    /// 启用/禁用指定采集器
    /// </summary>
    public void SetCollectorEnabled(string collectorName, bool enabled)
    {
        var collector = _collectors.FirstOrDefault(c => c.Name == collectorName);
        if (collector == null)
        {
            Console.WriteLine($"❌ [DataCollectorService] 未找到采集器: {collectorName}");
            return;
        }
        
        collector.IsEnabled = enabled;
        
        if (enabled && _isStarted && !collector.IsRunning)
        {
            collector.StartAsync().Wait(5000);
        }
        else if (!enabled && collector.IsRunning)
        {
            collector.StopAsync().Wait(5000);
        }
    }
    
    public void Dispose()
    {
        StopAllAsync().Wait(5000);
        if (_ownsGateway)
        {
            _gateway.Dispose();
        }
    }
    
    /// <summary>
    /// 采集器包装类（用于统一管理不同类型的采集器）
    /// </summary>
    private class DataCollectorWrapper<T> : IDataCollectorBase
    {
        private readonly IDataCollector<T> _collector;
        
        public DataCollectorWrapper(IDataCollector<T> collector)
        {
            _collector = collector;
        }
        
        public string Name => _collector.Name;
        public bool IsEnabled { get => _collector.IsEnabled; set => _collector.IsEnabled = value; }
        public bool IsRunning => _collector.IsRunning;
        public int IntervalSeconds => _collector.IntervalSeconds;
        public DateTime? LastCollectTime => _collector.LastCollectTime;
        public bool LastCollectSuccess => _collector.LastCollectSuccess;
        public string? LastError => _collector.LastError;
        
        public Task StartAsync() => _collector.StartAsync();
        public Task StopAsync() => _collector.StopAsync();
        
        public async Task<bool> CollectAsync()
        {
            var result = await _collector.CollectAsync();
            return result.Success;
        }
    }
    
}

/// <summary>
/// 采集器状态信息
/// </summary>
public class CollectorStatus
{
    public string Name { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public bool IsRunning { get; set; }
    public int IntervalSeconds { get; set; }
    public DateTime? LastCollectTime { get; set; }
    public bool LastCollectSuccess { get; set; }
    public string? LastError { get; set; }
}

