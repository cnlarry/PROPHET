using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Prophet.Client.Database.Repositories;
using Prophet.Client.Models;
using Prophet.Client.Services.Cache;
using Prophet.Client.Services.Market;

namespace Prophet.Client.Services.Market;

/// <summary>
/// 市场数据缓存服务
/// 提供统一的数据访问接口，实现智能缓存策略（先查数据库，再查API）
/// 支持多模块共享数据
/// v2.0: 使用 LRU 缓存，支持内存监控
/// </summary>
public class MarketDataCacheService : IDisposable
{
    private readonly MarketDataRepository _repository;
    private readonly IBinanceExchangeGateway _gateway;
    private readonly MarketDataCollectionService _collectionService;
    private readonly LRUCache<string, CacheEntry> _memoryCache;
    private readonly bool _ownsGateway;
    private bool _disposed;

    // 缓存配置
    private const int MaxCacheItems = 500;  // 最大缓存项数

    // 数据缓存时间配置（秒）
    private static readonly Dictionary<string, int> CacheDurations = new()
    {
        { "FearGreed", 86400 },           // 恐惧与贪婪指数：24小时
        { "FundingRate", 28800 },         // 资金费率：8小时
        { "LongShortRatio", 300 },         // 多空比：5分钟
        { "OpenInterest", 60 },            // 持仓量：1分钟
        { "Ticker24h", 30 },               // 24h ticker：30秒
        { "Liquidation", 60 },             // 爆仓数据：1分钟
        { "Price", 10 }                    // 价格：10秒
    };

    public MarketDataCacheService(
        MarketDataRepository? repository = null,
        IBinanceExchangeGateway? gateway = null,
        MarketDataCollectionService? collectionService = null)
    {
        _repository = repository ?? new MarketDataRepository();
        _gateway = gateway ?? new BinanceExchangeGateway();
        _collectionService = collectionService ?? new MarketDataCollectionService(
            _gateway.HttpClient,
            _repository,
            _gateway);
        _ownsGateway = gateway == null;
        
        // 初始化 LRU 缓存
        _memoryCache = new LRUCache<string, CacheEntry>(MaxCacheItems);
        
        Logger.Info($"市场数据缓存服务初始化完成，缓存容量: {MaxCacheItems}");
    }

    /// <summary>
    /// 获取缓存统计信息
    /// </summary>
    public CacheStatistics GetCacheStatistics()
    {
        return _memoryCache.GetStatistics();
    }

    /// <summary>
    /// 清理过期缓存项
    /// </summary>
    /// <returns>清理的项数</returns>
    public int CleanupExpiredCache()
    {
        return _memoryCache.CleanupExpired();
    }

    /// <summary>
    /// 获取恐惧与贪婪指数（最新值）
    /// </summary>
    public async Task<FearGreedData?> GetFearGreedIndexAsync(bool forceRefresh = false)
    {
        var cacheKey = "FearGreed:Latest";
        
        // 检查内存缓存
        if (!forceRefresh && TryGetFromCache(cacheKey, out FearGreedData? cached))
        {
            return cached;
        }

        // 从数据库获取最新数据
        var data = await _repository.GetLatestFearGreedIndexAsync();
        
        if (data != null)
        {
            SetCache(cacheKey, data, CacheDurations["FearGreed"]);
            return data;
        }

        // 如果数据库没有数据，触发采集
        if (!forceRefresh)
        {
            await _collectionService.TriggerCollectAsync("FearGreed");
            // 等待采集完成后再查询
            await Task.Delay(2000);
            data = await _repository.GetLatestFearGreedIndexAsync();
            if (data != null)
            {
                SetCache(cacheKey, data, CacheDurations["FearGreed"]);
            }
        }

        return data;
    }

    /// <summary>
    /// 获取恐惧与贪婪指数历史数据
    /// </summary>
    public async Task<List<FearGreedData>> GetFearGreedHistoryAsync(int days = 30)
    {
        return await _repository.GetFearGreedHistoryAsync(days);
    }

    /// <summary>
    /// 获取资金费率（指定交易对）
    /// 
    /// ✅ 改进：按需注册资金费率采集器
    /// </summary>
    public async Task<FundingRateData?> GetFundingRateAsync(string symbol, bool forceRefresh = false)
    {
        // ✅ 按需注册：首次访问时才注册该 Symbol 的采集器
        await _collectionService.EnsureFundingRateCollectorAsync(symbol);
        
        var cacheKey = $"FundingRate:{symbol}";
        
        if (!forceRefresh && TryGetFromCache(cacheKey, out FundingRateData? cached))
        {
            return cached;
        }

        var data = await _repository.GetLatestFundingRateAsync(symbol);
        
        if (data != null)
        {
            SetCache(cacheKey, data, CacheDurations["FundingRate"]);
            return data;
        }

        // 如果数据库没有数据，触发采集
        if (!forceRefresh)
        {
            await _collectionService.TriggerCollectAsync("FundingRate", symbol);
            await Task.Delay(2000);
            data = await _repository.GetLatestFundingRateAsync(symbol);
            if (data != null)
            {
                SetCache(cacheKey, data, CacheDurations["FundingRate"]);
            }
        }

        return data;
    }

    /// <summary>
    /// 获取多个交易对的资金费率
    /// </summary>
    public async Task<List<FundingRateData>> GetFundingRatesAsync(List<string> symbols, bool forceRefresh = false)
    {
        var results = new List<FundingRateData>();
        
        foreach (var symbol in symbols)
        {
            var rate = await GetFundingRateAsync(symbol, forceRefresh);
            if (rate != null)
            {
                results.Add(rate);
            }
        }

        return results;
    }

    /// <summary>
    /// 获取24h ticker数据（价格、涨跌幅、成交量等）
    /// </summary>
    public async Task<Ticker24hData?> GetTicker24hAsync(string symbol, bool forceRefresh = false)
    {
        var cacheKey = $"Ticker24h:{symbol}";
        
        if (!forceRefresh && TryGetFromCache(cacheKey, out Ticker24hData? cached))
        {
            return cached;
        }

        // 从API获取（币安API，无需认证）
        try
        {
            var data = await FetchTicker24hFromApiAsync(symbol);
            if (data != null)
            {
                SetCache(cacheKey, data, CacheDurations["Ticker24h"]);
                // 可选：保存到数据库
                await _repository.SaveTicker24hAsync(data);
            }
            return data;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ [MarketDataCacheService] 获取Ticker24h失败: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// 获取多个交易对的24h ticker数据
    /// </summary>
    public async Task<List<Ticker24hData>> GetTickers24hAsync(List<string> symbols, bool forceRefresh = false)
    {
        var tasks = symbols.Select(s => GetTicker24hAsync(s, forceRefresh));
        var results = await Task.WhenAll(tasks);
        return results.Where(t => t != null).ToList()!;
    }

    /// <summary>
    /// 获取持仓量（Open Interest）
    /// </summary>
    public async Task<OpenInterestData?> GetOpenInterestAsync(string symbol, bool forceRefresh = false)
    {
        var cacheKey = $"OpenInterest:{symbol}";
        
        if (!forceRefresh && TryGetFromCache(cacheKey, out OpenInterestData? cached))
        {
            return cached;
        }

        try
        {
            var data = await FetchOpenInterestFromApiAsync(symbol);
            if (data != null)
            {
                SetCache(cacheKey, data, CacheDurations["OpenInterest"]);
                await _repository.SaveOpenInterestAsync(data);
            }
            return data;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ [MarketDataCacheService] 获取持仓量失败: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// 获取多空比
    /// </summary>
    public async Task<LongShortRatioData?> GetLongShortRatioAsync(string symbol, string period = "5m", bool forceRefresh = false)
    {
        var cacheKey = $"LongShortRatio:{symbol}:{period}";
        
        if (!forceRefresh && TryGetFromCache(cacheKey, out LongShortRatioData? cached))
        {
            return cached;
        }

        try
        {
            var data = await FetchLongShortRatioFromApiAsync(symbol, period);
            if (data != null)
            {
                SetCache(cacheKey, data, CacheDurations["LongShortRatio"]);
                await _repository.SaveLongShortRatioAsync(data);
                return data;
            }
            
            // 如果API失败，返回模拟数据（避免UI显示错误）
            Console.WriteLine($"⚠️ [MarketDataCacheService] 多空比API不可用，返回默认值");
            return new LongShortRatioData
            {
                Symbol = symbol,
                Period = period,
                LongAccountRatio = 1.0m,
                LongPositionRatio = 1.0m,
                LongShortRatio = 1.0m,
                UpdateTime = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ [MarketDataCacheService] 获取多空比失败: {ex.Message}");
            // 返回默认值，避免UI显示错误
            return new LongShortRatioData
            {
                Symbol = symbol,
                Period = period,
                LongAccountRatio = 1.0m,
                LongPositionRatio = 1.0m,
                LongShortRatio = 1.0m,
                UpdateTime = DateTime.UtcNow
            };
        }
    }

    /// <summary>
    /// 获取24h涨跌幅排行榜
    /// </summary>
    public async Task<List<Ticker24hData>> GetTopGainersAsync(int limit = 5, bool forceRefresh = false)
    {
        // 获取所有交易对的24h数据，然后排序
        var allTickers = await GetAllTickers24hAsync(forceRefresh);
        return allTickers
            .Where(t => t.PriceChangePercent > 0)
            .OrderByDescending(t => t.PriceChangePercent)
            .Take(limit)
            .ToList();
    }

    /// <summary>
    /// 获取24h跌幅排行榜
    /// </summary>
    public async Task<List<Ticker24hData>> GetTopLosersAsync(int limit = 5, bool forceRefresh = false)
    {
        var allTickers = await GetAllTickers24hAsync(forceRefresh);
        return allTickers
            .Where(t => t.PriceChangePercent < 0)
            .OrderBy(t => t.PriceChangePercent)
            .Take(limit)
            .ToList();
    }

    /// <summary>
    /// 获取所有交易对的24h数据（用于排行榜）
    /// </summary>
    private async Task<List<Ticker24hData>> GetAllTickers24hAsync(bool forceRefresh = false)
    {
        // 从API获取所有交易对的24h数据
        try
        {
            return await FetchAllTickers24hFromApiAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ [MarketDataCacheService] 获取所有Ticker24h失败: {ex.Message}");
            return new List<Ticker24hData>();
        }
    }

    // ========== 私有方法：从API获取数据 ==========

    private async Task<Ticker24hData?> FetchTicker24hFromApiAsync(string symbol)
    {
        try
        {
            return await _gateway.FetchTicker24hAsync(symbol);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ [MarketDataCacheService] 获取Ticker24h失败: {ex.Message}");
            return null;
        }
    }

    private async Task<List<Ticker24hData>> FetchAllTickers24hFromApiAsync()
    {
        try
        {
            var result = await _gateway.FetchAllTickers24hAsync();
            return result.ToList();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ [MarketDataCacheService] 获取所有Ticker24h失败: {ex.Message}");
            return new List<Ticker24hData>();
        }
    }

    private async Task<OpenInterestData?> FetchOpenInterestFromApiAsync(string symbol)
    {
        try
        {
            return await _gateway.FetchOpenInterestAsync(symbol);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ [MarketDataCacheService] 获取持仓量失败: {ex.Message}");
            return null;
        }
    }

    private async Task<LongShortRatioData?> FetchLongShortRatioFromApiAsync(string symbol, string period)
    {
        try
        {
            // 获取三种多空比数据：
            // 1. globalLongShortAccountRatio - 多空持仓人数比（所有账户）
            // 2. topLongShortAccountRatio - 大户账户数多空比
            // 3. topLongShortPositionRatio - 大户持仓量多空比
            var globalRatio = await _gateway.FetchLongShortRatioAsync(symbol, period, "global");
            var topAccountRatio = await _gateway.FetchLongShortRatioAsync(symbol, period, "top");
            var topPositionRatio = await _gateway.FetchLongShortRatioAsync(symbol, period, "position");

            // 合并数据：优先使用global作为基础（多空持仓人数比）
            if (globalRatio != null)
            {
                // 使用top账户数多空比（大户账户数多空比）
                if (topAccountRatio != null)
                {
                    globalRatio.LongAccountRatio = topAccountRatio.LongAccountRatio;
                }
                // 使用top持仓量多空比（大户持仓量多空比）
                if (topPositionRatio != null)
                {
                    globalRatio.LongPositionRatio = topPositionRatio.LongPositionRatio;
                }
                // LongShortRatio使用global的值（多空持仓人数比）
                return globalRatio;
            }

            // 如果global失败，尝试合并top数据
            if (topAccountRatio != null && topPositionRatio != null)
            {
                return new LongShortRatioData
                {
                    Symbol = symbol,
                    Period = period,
                    LongAccountRatio = topAccountRatio.LongAccountRatio,      // 大户账户数多空比
                    LongPositionRatio = topPositionRatio.LongPositionRatio,   // 大户持仓量多空比
                    LongShortRatio = topAccountRatio.LongShortRatio,          // 使用账户数多空比作为综合多空比
                    UpdateTime = DateTime.UtcNow
                };
            }

            // 如果只有部分数据，返回可用的
            return topAccountRatio ?? topPositionRatio;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ [MarketDataCacheService] 获取多空比失败: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// 获取爆仓数据
    /// </summary>
    public async Task<LiquidationData?> GetLiquidationDataAsync(string symbol, bool forceRefresh = false)
    {
        var cacheKey = $"Liquidation:{symbol}";
        
        if (!forceRefresh && TryGetFromCache(cacheKey, out LiquidationData? cached))
        {
            return cached;
        }

        try
        {
            var data = await _gateway.FetchLiquidationDataAsync(symbol);
            if (data != null)
            {
                SetCache(cacheKey, data, CacheDurations["Liquidation"]);
                // 可选：保存到数据库
                // await _repository.SaveLiquidationDataAsync(data);
            }
            return data;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ [MarketDataCacheService] 获取爆仓数据失败: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// 获取合约主动买卖量
    /// </summary>
    public async Task<TakerLongShortRatioData?> GetTakerLongShortRatioAsync(string symbol, string period = "5m", bool forceRefresh = false)
    {
        var cacheKey = $"TakerLongShortRatio:{symbol}:{period}";
        
        if (!forceRefresh && TryGetFromCache(cacheKey, out TakerLongShortRatioData? cached))
        {
            return cached;
        }

        try
        {
            var data = await _gateway.FetchTakerLongShortRatioAsync(symbol, period);
            if (data != null)
            {
                SetCache(cacheKey, data, CacheDurations["Ticker24h"]); // 使用相同的缓存时间
            }
            return data;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ [MarketDataCacheService] 获取主动买卖量失败: {ex.Message}");
            return null;
        }
    }

    // ========== 缓存管理 ==========

    private bool TryGetFromCache<T>(string key, out T? value) where T : class
    {
        if (_memoryCache.TryGet(key, out var entry) && entry != null)
        {
            value = entry.Value as T;
            return value != null;
        }

        value = null;
        return false;
    }

    private void SetCache<T>(string key, T value, int durationSeconds) where T : class
    {
        var entry = new CacheEntry { Value = value };
        _memoryCache.Set(key, entry, durationSeconds);
    }

    private class CacheEntry
    {
        public object? Value { get; set; }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _memoryCache.Dispose();

        if (_ownsGateway)
        {
            _gateway.Dispose();
        }

        _disposed = true;
    }
}