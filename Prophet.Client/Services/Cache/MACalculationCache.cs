using System;
using System.Collections.Generic;
using System.Linq;
using Prophet.Client.Models;

namespace Prophet.Client.Services.Cache;

/// <summary>
/// MA计算结果缓存管理器
/// 用于避免重复计算相同周期的MA值
/// </summary>
public class MACalculationCache
{
    private static MACalculationCache? _instance;
    private static readonly object _lock = new object();

    /// <summary>
    /// 单例实例
    /// </summary>
    public static MACalculationCache Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    if (_instance == null)
                    {
                        _instance = new MACalculationCache();
                    }
                }
            }
            return _instance;
        }
    }

    /// <summary>
    /// 缓存字典
    /// Key: (indicatorId, dataKey, PERIOD, dataHash)
    /// Value: 计算结果
    /// </summary>
    private readonly Dictionary<string, List<double>> _cache = new();

    /// <summary>
    /// 缓存大小限制（条目数）
    /// </summary>
    private const int MaxCacheSize = 100;

    /// <summary>
    /// 缓存访问时间记录（用于LRU淘汰）
    /// </summary>
    private readonly Dictionary<string, DateTime> _accessTimes = new();

    private MACalculationCache()
    {
    }

    /// <summary>
    /// 获取缓存的MA计算结果
    /// </summary>
    public List<double>? Get(string indicatorId, string dataKey, int PERIOD, int dataHash)
    {
        var cacheKey = BuildCacheKey(indicatorId, dataKey, PERIOD, dataHash);
        
        if (_cache.TryGetValue(cacheKey, out var result))
        {
            // 更新访问时间
            _accessTimes[cacheKey] = DateTime.Now;
            return result;
        }

        return null;
    }

    /// <summary>
    /// 缓存MA计算结果
    /// </summary>
    public void Set(string indicatorId, string dataKey, int PERIOD, int dataHash, List<double> result)
    {
        var cacheKey = BuildCacheKey(indicatorId, dataKey, PERIOD, dataHash);

        // 检查缓存大小，超出限制则清理
        if (_cache.Count >= MaxCacheSize)
        {
            EvictLRU();
        }

        _cache[cacheKey] = result;
        _accessTimes[cacheKey] = DateTime.Now;
        
    }

    /// <summary>
    /// 清空指定指标的缓存
    /// </summary>
    public void ClearIndicator(string indicatorId)
    {
        var keysToRemove = _cache.Keys.Where(k => k.StartsWith($"{indicatorId}:")).ToList();
        foreach (var key in keysToRemove)
        {
            _cache.Remove(key);
            _accessTimes.Remove(key);
        }
        
        if (keysToRemove.Count > 0)
        {
        }
    }

    /// <summary>
    /// 清空所有缓存
    /// </summary>
    public void ClearAll()
    {
        var count = _cache.Count;
        _cache.Clear();
        _accessTimes.Clear();
    }

    /// <summary>
    /// 计算数据哈希（用于检测数据变化）
    /// </summary>
    public static int CalculateDataHash(List<SubChartDataPoint> data, string dataKey)
    {
        if (data == null || data.Count == 0)
            return 0;

        unchecked
        {
            int hash = 17;
            hash = hash * 31 + data.Count;
            
            // 只使用前10个和后10个数据点来计算哈希（性能优化）
            var sampleSize = Math.Min(10, data.Count);
            for (int i = 0; i < sampleSize; i++)
            {
                var value = data[i].GetValue(dataKey);
                if (!double.IsNaN(value))
                {
                    hash = hash * 31 + value.GetHashCode();
                }
            }
            
            if (data.Count > sampleSize)
            {
                for (int i = data.Count - sampleSize; i < data.Count; i++)
                {
                    var value = data[i].GetValue(dataKey);
                    if (!double.IsNaN(value))
                    {
                        hash = hash * 31 + value.GetHashCode();
                    }
                }
            }
            
            return hash;
        }
    }

    /// <summary>
    /// 计算Volume数据哈希
    /// </summary>
    public static int CalculateVolumeDataHash(List<Candlestick> candles)
    {
        if (candles == null || candles.Count == 0)
            return 0;

        unchecked
        {
            int hash = 17;
            hash = hash * 31 + candles.Count;
            
            var sampleSize = Math.Min(10, candles.Count);
            for (int i = 0; i < sampleSize; i++)
            {
                hash = hash * 31 + candles[i].Volume.GetHashCode();
            }
            
            if (candles.Count > sampleSize)
            {
                for (int i = candles.Count - sampleSize; i < candles.Count; i++)
                {
                    hash = hash * 31 + candles[i].Volume.GetHashCode();
                }
            }
            
            return hash;
        }
    }

    /// <summary>
    /// 构建缓存Key
    /// </summary>
    private string BuildCacheKey(string indicatorId, string dataKey, int PERIOD, int dataHash)
    {
        return $"{indicatorId}:{dataKey}:{PERIOD}:{dataHash}";
    }

    /// <summary>
    /// LRU淘汰策略：移除最久未访问的缓存
    /// </summary>
    private void EvictLRU()
    {
        if (_accessTimes.Count == 0)
            return;

        // 找到最久未访问的key
        var oldestKey = _accessTimes.OrderBy(kvp => kvp.Value).First().Key;
        
        _cache.Remove(oldestKey);
        _accessTimes.Remove(oldestKey);
        
    }

    /// <summary>
    /// 获取缓存统计信息
    /// </summary>
    public (int Count, long MemoryBytes) GetStats()
    {
        var count = _cache.Count;
        var memoryBytes = _cache.Sum(kvp => kvp.Value.Count * sizeof(double));
        return (count, memoryBytes);
    }
}

