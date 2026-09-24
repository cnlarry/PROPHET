using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Prophet.Client.Database;
using Prophet.Client.Data.Symbols;
using Prophet.Client.Services;

namespace Prophet.Client.Services.Data.Preparation;

/// <summary>
/// 数据完整性检查器
/// 检查数据库中K线数据的连续性和完整性
/// </summary>
public class DataIntegrityChecker
{
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
    /// 检查指定时间范围内的数据完整性
    /// </summary>
    /// <param name="symbol">交易对</param>
    /// <param name="timeframe">时间框架（5m, 15m, 1h等）</param>
    /// <param name="startDate">开始时间</param>
    /// <param name="endDate">结束时间</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>数据完整性检查结果</returns>
    public async Task<DataIntegrityResult> CheckAsync(
        string symbol,
        string timeframe,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default)
    {
        var result = new DataIntegrityResult
        {
            Symbol = NormalizeSymbolKey(symbol),
            Timeframe = timeframe,
            StartDate = startDate,
            EndDate = endDate
        };
        
        try
        {
            // 🔧 v4.0: 确保输入时间是UTC时间，但不进行时区转换
            var utcStartDate = startDate.Kind == DateTimeKind.Utc 
                ? startDate 
                : DateTime.SpecifyKind(startDate, DateTimeKind.Utc);
            var utcEndDate = endDate.Kind == DateTimeKind.Utc 
                ? endDate 
                : DateTime.SpecifyKind(endDate, DateTimeKind.Utc);
            
            // 1. 计算时间框架的毫秒数
            int intervalMinutes = DSLAnalyzer.TimeframeToMinutes(timeframe);
            long intervalMs = intervalMinutes * 60 * 1000L;
            
            // 2. 对齐时间边界（向下取整到时间框架边界）
            var alignedStart = AlignToTimeframe(utcStartDate, intervalMinutes);
            var alignedEnd = AlignToTimeframe(utcEndDate, intervalMinutes);
            
            // 3. 计算期望的K线数量
            result.ExpectedCount = (int)((alignedEnd - alignedStart).TotalMinutes / intervalMinutes) + 1;
            
            // 4. 从数据库查询现有数据
            var existingKlines = await LoadExistingKlinesAsync(
                NormalizeSymbolKey(symbol), timeframe, alignedStart, alignedEnd, cancellationToken
            );
            
            result.ExistingCount = existingKlines.Count;
            
            // 5. 检查连续性，找出缺口
            if (existingKlines.Count == 0)
            {
                // 完全没有数据
                result.Gaps.Add(new DataGap
                {
                    StartTime = alignedStart,
                    EndTime = alignedEnd,
                    MissingCount = result.ExpectedCount
                });
            }
            else
            {
                // 检查数据连续性
                result.Gaps = FindGaps(existingKlines, alignedStart, alignedEnd, intervalMs);
            }
            
            return result;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ 检查数据完整性失败: {symbol} {timeframe}");
            Console.WriteLine($"   错误: {ex.Message}");
            throw;
        }
    }
    
    /// <summary>
    /// 将时间对齐到时间框架边界
    /// 确保使用UTC时间，避免时区问题
    /// </summary>
    private DateTime AlignToTimeframe(DateTime time, int intervalMinutes)
    {
        // 🔧 v4.0: 确保time被当作UTC时间处理，但不进行时区转换
        DateTimeOffset timeOffset;
        if (time.Kind == DateTimeKind.Unspecified || time.Kind == DateTimeKind.Utc)
        {
            // 如果Kind是Unspecified或Utc，假设它是UTC时间，不进行转换
            timeOffset = new DateTimeOffset(time, TimeSpan.Zero);
        }
        else
        {
            // 其他情况（Local），也不进行转换，直接指定为UTC
            timeOffset = new DateTimeOffset(DateTime.SpecifyKind(time, DateTimeKind.Utc), TimeSpan.Zero);
        }
        
        long timestamp = timeOffset.ToUnixTimeMilliseconds();
        long intervalMs = intervalMinutes * 60 * 1000L;
        
        // 向下取整到最近的时间框架边界
        long alignedTimestamp = (timestamp / intervalMs) * intervalMs;
        
        // 返回UTC时间
        return DateTimeOffset.FromUnixTimeMilliseconds(alignedTimestamp).UtcDateTime;
    }
    
    /// <summary>
    /// 从数据库加载现有的K线数据
    /// </summary>
    private async Task<List<(DateTime Time, long OpenTime)>> LoadExistingKlinesAsync(
        string symbol,
        string timeframe,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken)
    {
        var result = new List<(DateTime Time, long OpenTime)>();
        
        var startMs = new DateTimeOffset(startDate).ToUnixTimeMilliseconds();
        var endMs = new DateTimeOffset(endDate).ToUnixTimeMilliseconds();
        
        await Task.Run(() =>
        {
            using var db = DBHelper.CreateConnection();
            using var cmd = (SqliteCommand)db.CreateCommand();
            
            // 兼容SQLite和MySQL：不依赖 open_time_str 字段
            cmd.CommandText = @"
                SELECT open_time
                FROM klines
                WHERE symbol_key = @symbolKey
                  AND `interval` = @interval
                  AND open_time >= @start_time
                  AND open_time <= @end_time
                ORDER BY open_time ASC";
            
            cmd.Parameters.AddWithValue("@symbolKey", NormalizeSymbolKey(symbol));
            cmd.Parameters.AddWithValue("@interval", timeframe);
            cmd.Parameters.AddWithValue("@start_time", startMs);
            cmd.Parameters.AddWithValue("@end_time", endMs);
            
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                long openTime = reader.GetInt64(0);
                // 确保使用UTC时间，避免时区问题
                DateTime timeStr = DateTimeOffset.FromUnixTimeMilliseconds(openTime).UtcDateTime;
                
                result.Add((timeStr, openTime));
            }
        }, cancellationToken);
        
        return result;
    }
    
    /// <summary>
    /// 查找数据缺口
    /// </summary>
    private List<DataGap> FindGaps(
        List<(DateTime Time, long OpenTime)> existingKlines,
        DateTime startDate,
        DateTime endDate,
        long intervalMs)
    {
        var gaps = new List<DataGap>();
        
        // 确保使用UTC时间计算时间戳
        var startOffset = startDate.Kind == DateTimeKind.Utc 
            ? new DateTimeOffset(startDate, TimeSpan.Zero)
            : new DateTimeOffset(startDate.ToUniversalTime(), TimeSpan.Zero);
        var endOffset = endDate.Kind == DateTimeKind.Utc
            ? new DateTimeOffset(endDate, TimeSpan.Zero)
            : new DateTimeOffset(endDate.ToUniversalTime(), TimeSpan.Zero);
        
        var startMs = startOffset.ToUnixTimeMilliseconds();
        var endMs = endOffset.ToUnixTimeMilliseconds();
        
        // 检查开头是否有缺口
        var firstKline = existingKlines.First();
        if (firstKline.OpenTime > startMs)
        {
            var gapEnd = DateTimeOffset.FromUnixTimeMilliseconds(firstKline.OpenTime).UtcDateTime;
            int missingCount = (int)((firstKline.OpenTime - startMs) / intervalMs);
            
            // 确保StartTime是UTC时间
            var gapStart = DateTimeOffset.FromUnixTimeMilliseconds(startMs).UtcDateTime;
            
            gaps.Add(new DataGap
            {
                StartTime = gapStart,
                EndTime = gapEnd,
                MissingCount = missingCount
            });
        }
        
        // 检查中间的缺口
        for (int i = 0; i < existingKlines.Count - 1; i++)
        {
            var current = existingKlines[i];
            var next = existingKlines[i + 1];
            
            long expectedNextTime = current.OpenTime + intervalMs;
            long gap = next.OpenTime - expectedNextTime;
            
            // 如果时间差大于一个时间框架，说明有缺口
            if (gap >= intervalMs)
            {
                int missingCount = (int)(gap / intervalMs);
                
                // 缺口结束时间应该是下一个K线开始时间之前的一个时间框架边界
                // 即：next.OpenTime - intervalMs
                long gapEndTime = next.OpenTime - intervalMs;
                
                gaps.Add(new DataGap
                {
                    // 确保使用UTC时间，避免时区问题
                    StartTime = DateTimeOffset.FromUnixTimeMilliseconds(expectedNextTime).UtcDateTime,
                    EndTime = DateTimeOffset.FromUnixTimeMilliseconds(gapEndTime).UtcDateTime,
                    MissingCount = missingCount
                });
            }
        }
        
        // 检查结尾是否有缺口
        var lastKline = existingKlines.Last();
        if (lastKline.OpenTime < endMs)
        {
            long expectedNextTime = lastKline.OpenTime + intervalMs;
            
            // 对齐结束时间到时间框架边界
            long alignedEndMs = (endMs / intervalMs) * intervalMs;
            
            // 如果对齐后的结束时间小于期望的下一个时间，说明没有缺口
            if (alignedEndMs < expectedNextTime)
            {
                return gaps;
            }
            
            int missingCount = (int)((alignedEndMs - expectedNextTime) / intervalMs) + 1;
            
            if (missingCount > 0)
            {
                gaps.Add(new DataGap
                {
                    // 确保使用UTC时间，避免时区问题
                    StartTime = DateTimeOffset.FromUnixTimeMilliseconds(expectedNextTime).UtcDateTime,
                    EndTime = DateTimeOffset.FromUnixTimeMilliseconds(alignedEndMs).UtcDateTime,
                    MissingCount = missingCount
                });
            }
        }
        
        return gaps;
    }
    
    /// <summary>
    /// 批量检查多个时间框架的数据完整性
    /// </summary>
    public async Task<List<DataIntegrityResult>> CheckMultipleAsync(
        string symbol,
        IEnumerable<string> timeframes,
        DateTime startDate,
        DateTime endDate,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var results = new List<DataIntegrityResult>();
        
        foreach (var timeframe in timeframes)
        {
            progress?.Report($"检查 {timeframe} 数据完整性...");
            
            var result = await CheckAsync(symbol, timeframe, startDate, endDate, cancellationToken);
            results.Add(result);
            
            Console.WriteLine($"✅ {result}");
        }
        
        return results;
    }
}

