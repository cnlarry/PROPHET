using System;
using System.Threading;
using System.Threading.Tasks;
using Prophet.Client.Services.Market;

namespace Prophet.Client.Services.Cache;

/// <summary>
/// 缓存监控和自动清理服务
/// 定期监控缓存状态并自动清理过期项
/// </summary>
public class CacheMonitorService : IDisposable
{
    private readonly MarketDataCacheService? _marketDataCache;
    private Timer? _cleanupTimer;
    private Timer? _statisticsTimer;
    private bool _disposed;

    // 配置
    private const int CleanupIntervalMinutes = 10;  // 每10分钟清理一次过期项
    private const int StatisticsIntervalMinutes = 30; // 每30分钟输出一次统计信息

    public CacheMonitorService(MarketDataCacheService? marketDataCache = null)
    {
        _marketDataCache = marketDataCache;
    }

    /// <summary>
    /// 启动监控
    /// </summary>
    public void Start()
    {
        if (_disposed)
        {
            Logger.Warn("缓存监控服务已释放，无法启动");
            return;
        }

        Logger.Info("启动缓存监控服务");

        // 启动清理定时器
        _cleanupTimer = new Timer(
            OnCleanupTimer,
            null,
            TimeSpan.FromMinutes(CleanupIntervalMinutes),
            TimeSpan.FromMinutes(CleanupIntervalMinutes)
        );

        // 启动统计定时器
        _statisticsTimer = new Timer(
            OnStatisticsTimer,
            null,
            TimeSpan.FromMinutes(StatisticsIntervalMinutes),
            TimeSpan.FromMinutes(StatisticsIntervalMinutes)
        );

        Logger.Info($"缓存监控已启动：清理间隔={CleanupIntervalMinutes}分钟，统计间隔={StatisticsIntervalMinutes}分钟");
    }

    /// <summary>
    /// 停止监控
    /// </summary>
    public void Stop()
    {
        _cleanupTimer?.Change(Timeout.Infinite, Timeout.Infinite);
        _statisticsTimer?.Change(Timeout.Infinite, Timeout.Infinite);
        Logger.Info("缓存监控服务已停止");
    }

    /// <summary>
    /// 手动触发清理
    /// </summary>
    public void TriggerCleanup()
    {
        PerformCleanup();
    }

    /// <summary>
    /// 手动获取统计信息
    /// </summary>
    public void LogStatistics()
    {
        LogCacheStatistics();
    }

    private void OnCleanupTimer(object? state)
    {
        try
        {
            PerformCleanup();
        }
        catch (Exception ex)
        {
            Logger.Error("缓存清理失败", ex);
        }
    }

    private void OnStatisticsTimer(object? state)
    {
        try
        {
            LogCacheStatistics();
        }
        catch (Exception ex)
        {
            Logger.Error("获取缓存统计失败", ex);
        }
    }

    private void PerformCleanup()
    {
        if (_marketDataCache == null)
            return;

        var cleaned = _marketDataCache.CleanupExpiredCache();
        if (cleaned > 0)
        {
            Logger.Info($"缓存清理完成：移除 {cleaned} 个过期项");
        }
    }

    private void LogCacheStatistics()
    {
        if (_marketDataCache == null)
            return;

        var stats = _marketDataCache.GetCacheStatistics();
        Logger.Info($"📊 缓存统计: {stats}");

        // 内存使用情况（粗略估计）
        var memoryMB = GC.GetTotalMemory(false) / (1024.0 * 1024.0);
        Logger.Info($"💾 内存使用: {memoryMB:F2} MB");

        // 如果命中率过低，记录警告
        if (stats.Hits + stats.Misses > 100 && stats.HitRate < 50)
        {
            Logger.Warn($"⚠️ 缓存命中率偏低: {stats.HitRate:F2}%，考虑增加缓存容量");
        }
    }

    public void Dispose()
    {
        if (_disposed) return;

        Stop();
        _cleanupTimer?.Dispose();
        _statisticsTimer?.Dispose();
        _disposed = true;

        Logger.Info("缓存监控服务已释放");
    }
}

