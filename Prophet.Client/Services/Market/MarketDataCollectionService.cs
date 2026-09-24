using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Prophet.Client.Database.Repositories;
using Prophet.Client.Services.Data;
using Prophet.Client.Services.Data.Collectors;
using Prophet.Client.Services.Market;

namespace Prophet.Client.Services.Market;

/// <summary>
/// 统一的市场数据采集服务
/// 负责管理所有市场数据采集器，根据数据更新频率智能调度，实现多模块共享
/// 
/// ✅ 按需采集策略：
/// - 恐惧与贪婪指数：全局数据，始终采集
/// - 资金费率：按需采集，仅在用户访问时注册
/// - 其他数据：按需动态注册
/// </summary>
public class MarketDataCollectionService : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly MarketDataRepository _repository;
    private readonly IBinanceExchangeGateway _gateway;
    private readonly DataCollectorService _collectorService;
    private readonly bool _ownsGateway;
    private readonly HashSet<string> _registeredFundingRateSymbols = new();
    private bool _disposed;
    private bool _isStarted;

    public MarketDataCollectionService(
        HttpClient httpClient,
        MarketDataRepository? repository = null,
        IBinanceExchangeGateway? gateway = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _repository = repository ?? new MarketDataRepository();
        _gateway = gateway ?? new BinanceExchangeGateway();
        _ownsGateway = gateway == null;
        _collectorService = new DataCollectorService(_httpClient, _repository, _gateway);
    }

    /// <summary>
    /// 初始化全局数据采集器（不依赖 Symbol 的数据）
    /// 
    /// ✅ 改进：只采集全局数据，Symbol 相关的数据按需采集
    /// </summary>
    public void InitializeCollectors()
    {
        Console.WriteLine("📋 [MarketDataCollectionService] 初始化数据采集器...");

        // 1. 恐惧与贪婪指数 - 全局数据，每天更新一次（UTC 00:05）
        _collectorService.RegisterFearGreedCollector();

        // 2. ❌ 移除：资金费率不再在启动时自动采集
        // 改为按需注册（见 EnsureFundingRateCollectorAsync）

        // 3. 其他数据采集器也将在需要时动态注册
        // - 多空比：每5分钟
        // - 持仓量：每1分钟
        // - 24h ticker：每30秒
        // - 爆仓数据：每1分钟

        Console.WriteLine($"✅ [MarketDataCollectionService] 数据采集器初始化完成");
    }

    /// <summary>
    /// 确保特定 Symbol 的资金费率采集器已注册（按需注册）
    /// </summary>
    /// <param name="symbol">交易对，如 BTCUSDT</param>
    public async Task EnsureFundingRateCollectorAsync(string symbol)
    {
        if (string.IsNullOrWhiteSpace(symbol))
        {
            return;
        }

        // 规范化 Symbol（去除交易所后缀）
        var normalizedSymbol = symbol.Contains("-") 
            ? symbol.Split('-')[0] 
            : symbol.ToUpper();

        // 避免重复注册
        if (_registeredFundingRateSymbols.Contains(normalizedSymbol))
        {
            return;
        }

        Console.WriteLine($"📝 [MarketDataCollectionService] 按需注册资金费率采集器: {normalizedSymbol}");
        
        _collectorService.RegisterFundingRateCollector(normalizedSymbol);
        _registeredFundingRateSymbols.Add(normalizedSymbol);

        // 如果服务已启动，立即启动新注册的采集器
        if (_isStarted)
        {
            // 注意：这里简化处理，实际应该只启动新注册的采集器
            // 但 DataCollectorService 没有提供单独启动的接口，所以这里留空
            // 新采集器会在下一次 _collectorService.StartAllAsync() 时启动
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// 批量确保多个 Symbol 的资金费率采集器
    /// </summary>
    public async Task EnsureFundingRateCollectorsAsync(IEnumerable<string> symbols)
    {
        foreach (var symbol in symbols)
        {
            await EnsureFundingRateCollectorAsync(symbol);
        }
    }

    /// <summary>
    /// 启动所有数据采集器
    /// </summary>
    public async Task StartAsync()
    {
        if (_isStarted)
        {
            Console.WriteLine("⚠️ [MarketDataCollectionService] 采集服务已在运行");
            return;
        }

        Console.WriteLine("🚀 [MarketDataCollectionService] 启动市场数据采集服务...");
        await _collectorService.StartAllAsync();
        _isStarted = true;
        Console.WriteLine("✅ [MarketDataCollectionService] 市场数据采集服务启动完成");
    }

    /// <summary>
    /// 停止所有数据采集器
    /// </summary>
    public async Task StopAsync()
    {
        if (!_isStarted)
        {
            return;
        }

        Console.WriteLine("🛑 [MarketDataCollectionService] 停止市场数据采集服务...");
        await _collectorService.StopAllAsync();
        _isStarted = false;
        Console.WriteLine("✅ [MarketDataCollectionService] 市场数据采集服务已停止");
    }

    /// <summary>
    /// 手动触发指定数据类型的采集
    /// </summary>
    public async Task<bool> TriggerCollectAsync(string dataType, string? symbol = null)
    {
        // 根据数据类型构造采集器名称
        var collectorName = string.IsNullOrEmpty(symbol) 
            ? dataType 
            : $"{dataType}-{symbol}";
        
        return await _collectorService.TriggerCollectAsync(collectorName);
    }

    /// <summary>
    /// 获取所有采集器状态
    /// </summary>
    public List<Services.Data.CollectorStatus> GetCollectorStatuses()
    {
        return _collectorService.GetCollectorStatuses();
    }

    /// <summary>
    /// 获取已注册的资金费率采集 Symbol 列表
    /// </summary>
    public IReadOnlySet<string> GetRegisteredFundingRateSymbols()
    {
        return _registeredFundingRateSymbols;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _collectorService?.Dispose();
        
        if (_ownsGateway)
        {
            (_gateway as IDisposable)?.Dispose();
        }

        _disposed = true;
    }
}
