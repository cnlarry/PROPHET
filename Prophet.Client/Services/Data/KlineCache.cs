using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Prophet.Client.Models;
using Prophet.Client.Database;
using Prophet.Client.Data.Symbols;

namespace Prophet.Client.Services.Data;

/// <summary>
/// 本地数据缓存（SQLite）
/// 用于缓存K线数据及其他市场数据
/// </summary>
public class KlineCache : IDisposable
{
    private bool _disposed;

    public KlineCache()
    {
        // ✅ 表初始化已迁移到 ClientMigrations (2026-01-04_03_core_market_tables_symbol_to_symbol_key)
        // 不再需要在构造函数中初始化表
    }

    private static string NormalizeSymbolKey(string symbolOrKey)
    {
        if (string.IsNullOrWhiteSpace(symbolOrKey))
        {
            return "BTCUSDT-BINANCE-SWAP";
        }

        if (InstrumentKey.TryParse(symbolOrKey, out var key))
        {
            return key.ToString().ToUpperInvariant();
        }

        return $"{symbolOrKey.ToUpperInvariant()}-BINANCE-SWAP";
    }

    /// <summary>
    /// 保存K线数据到缓存（从API响应直接存储）
    /// </summary>
    public async Task SaveKlinesFromApiAsync(string symbol, string interval, List<KlineCacheDto> klines)
    {
        if (klines == null || klines.Count == 0)
            return;

        var symbolKey = NormalizeSymbolKey(symbol);
        using var connection = DBHelper.CreateConnection();

        using var transaction = connection.BeginTransaction();
        
        try
        {
            var sql = @"
                INSERT OR REPLACE INTO klines 
                (symbol_key, interval, open_time, open, high, low, close, volume, 
                 close_time, quote_volume, trade_count, taker_buy_volume, 
                 taker_buy_quote_volume, created_at)
                VALUES 
                (@SymbolKey, @Interval, @OpenTime, @Open, @High, @Low, @Close, @Volume,
                 @CloseTime, @QuoteVolume, @TradeCount, @TakerBuyVolume,
                 @TakerBuyQuoteVolume, @CreatedAt)
            ";

            var createdAt = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
            
            foreach (var kline in klines)
            {
                await connection.ExecuteAsync(sql, new
                {
                    SymbolKey = symbolKey,
                    Interval = interval,
                    kline.OpenTime,
                    kline.Open,
                    kline.High,
                    kline.Low,
                    kline.Close,
                    kline.Volume,
                    kline.CloseTime,
                    QuoteVolume = kline.QuoteVolume > 0 ? (double?)kline.QuoteVolume : null,
                    TradeCount = kline.Count > 0 ? (int?)kline.Count : null,
                    TakerBuyVolume = kline.TakerBuyVolume > 0 ? (double?)kline.TakerBuyVolume : null,
                    TakerBuyQuoteVolume = kline.TakerBuyQuoteVolume > 0 ? (double?)kline.TakerBuyQuoteVolume : null,
                    CreatedAt = createdAt
                }, transaction);
            }

            transaction.Commit();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ [KlineCache] 缓存K线失败: {ex.Message}");
            transaction.Rollback();
            throw;
        }
    }

    /// <summary>
    /// 获取最新的K线时间（用于增量更新）
    /// </summary>
    public async Task<long?> GetLatestKlineTimeAsync(string symbol, string interval)
    {
        var symbolKey = NormalizeSymbolKey(symbol);
        using var connection = DBHelper.CreateConnection();

        var result = await connection.ExecuteScalarAsync<long?>(
            "SELECT MAX(open_time) FROM klines WHERE symbol_key = @SymbolKey AND interval = @Interval",
            new { SymbolKey = symbolKey, Interval = interval }
        );

        return result;
    }

    /// <summary>
    /// 检查缓存中是否有数据
    /// </summary>
    public async Task<bool> HasCachedDataAsync(string symbol, string interval)
    {
        var symbolKey = NormalizeSymbolKey(symbol);
        using var connection = DBHelper.CreateConnection();

        var count = await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM klines WHERE symbol_key = @SymbolKey AND interval = @Interval",
            new { SymbolKey = symbolKey, Interval = interval }
        );

        return count > 0;
    }

    /// <summary>
    /// 获取缓存的数据时间范围
    /// </summary>
    /// <summary>
    /// 获取K线数量
    /// </summary>
    public async Task<int> GetKlineCountAsync(string symbol, string interval)
    {
        var symbolKey = NormalizeSymbolKey(symbol);
        using var connection = DBHelper.CreateConnection();

        var sql = "SELECT COUNT(*) FROM klines WHERE symbol_key = @SymbolKey AND interval = @Interval";
        var count = await connection.ExecuteScalarAsync<int>(sql, new { SymbolKey = symbolKey, Interval = interval });
        return count;
    }
    
    public async Task<(DateTime? EarliestTime, DateTime? LatestTime, int Count)> GetCacheStatsAsync(
        string symbol, 
        string interval)
    {
        var symbolKey = NormalizeSymbolKey(symbol);
        using var connection = DBHelper.CreateConnection();

        var sql = @"
            SELECT 
                MIN(open_time) AS EarliestTime,
                MAX(open_time) AS LatestTime,
                COUNT(*) AS Count
            FROM klines
            WHERE symbol_key = @SymbolKey AND interval = @Interval
        ";

        var result = await connection.QuerySingleOrDefaultAsync<CacheStatsDto>(sql, 
            new { SymbolKey = symbolKey, Interval = interval });

        if (result == null || result.Count == 0)
        {
            return (null, null, 0);
        }

        return (
            result.EarliestTime.HasValue 
                ? DateTimeOffset.FromUnixTimeMilliseconds(result.EarliestTime.Value).UtcDateTime // 保持UTC时间
                : null,
            result.LatestTime.HasValue 
                ? DateTimeOffset.FromUnixTimeMilliseconds(result.LatestTime.Value).UtcDateTime // 保持UTC时间
                : null,
            result.Count
        );
    }

    /// <summary>
    /// 清除指定交易对的缓存
    /// </summary>
    public async Task ClearCacheAsync(string symbol, string interval)
    {
        var symbolKey = NormalizeSymbolKey(symbol);
        using var connection = DBHelper.CreateConnection();

        await connection.ExecuteAsync(
            "DELETE FROM klines WHERE symbol_key = @SymbolKey AND interval = @Interval",
            new { SymbolKey = symbolKey, Interval = interval }
        );

    }

    /// <summary>
    /// 清除所有缓存
    /// </summary>
    public async Task ClearAllCacheAsync()
    {
        using var connection = DBHelper.CreateConnection();

        await connection.ExecuteAsync("DELETE FROM klines");
    }

    /// <summary>
    /// 清除过期缓存（超过指定天数的数据）
    /// </summary>
    public async Task CleanupOldCacheAsync(int daysToKeep = 30)
    {
        using var connection = DBHelper.CreateConnection();

        var cutoffDate = DateTime.UtcNow.AddDays(-daysToKeep).ToString("yyyy-MM-dd HH:mm:ss");
        
        var deletedCount = await connection.ExecuteAsync(
            "DELETE FROM klines WHERE created_at < @CutoffDate",
            new { CutoffDate = cutoffDate }
        );

    }

    public void Dispose()
    {
        if (!_disposed)
        {
            // SQLite连接已在using中释放
            _disposed = true;
        }
    }

    #region DTO模型

    /// <summary>
    /// K线缓存DTO - 与API返回的KlineDto字段完全一致
    /// </summary>
    public class KlineCacheDto
    {
        public long OpenTime { get; set; }
        public string OpenTimeStr { get; set; } = string.Empty;
        public double Open { get; set; }
        public double High { get; set; }
        public double Low { get; set; }
        public double Close { get; set; }
        public double Volume { get; set; }
        public long CloseTime { get; set; }
        public string CloseTimeStr { get; set; } = string.Empty;
        public double QuoteVolume { get; set; }
        public int Count { get; set; }
        public double TakerBuyVolume { get; set; }
        public double TakerBuyQuoteVolume { get; set; }
        public bool Ignore { get; set; }
    }

    /// <summary>
    /// 用于转换为Candlestick的轻量DTO
    /// </summary>
    private class CandlestickCacheDto
    {
        public long OpenTime { get; set; }
        public long CloseTime { get; set; }  // 🔧 v4.0: 添加close_time字段
        public double Open { get; set; }
        public double High { get; set; }
        public double Low { get; set; }
        public double Close { get; set; }
        public double Volume { get; set; }
    }

    private class CacheStatsDto
    {
        public long? EarliestTime { get; set; }
        public long? LatestTime { get; set; }
        public int Count { get; set; }
    }

    #endregion
}
