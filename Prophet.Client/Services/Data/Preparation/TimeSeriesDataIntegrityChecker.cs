using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Prophet.Client.Database;
using Prophet.Client.Data.Symbols;
using Prophet.Client.Models;
using Prophet.Client.Services.Market;

namespace Prophet.Client.Services.Data.Preparation;

/// <summary>
/// 时间序列数据完整性检查器
/// 检查资金费率和恐惧与贪婪指数的连续性和完整性
/// </summary>
public class TimeSeriesDataIntegrityChecker
{
    private readonly IBinanceExchangeGateway? _gateway;

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

    private static string NormalizeBaseSymbol(string symbolOrKey)
    {
        if (string.IsNullOrWhiteSpace(symbolOrKey))
        {
            return "BTCUSDT";
        }

        if (InstrumentKey.TryParse(symbolOrKey, out var key))
        {
            return key.BaseSymbol.ToUpperInvariant();
        }

        return symbolOrKey.ToUpperInvariant();
    }
    
    public TimeSeriesDataIntegrityChecker(IBinanceExchangeGateway? gateway = null)
    {
        _gateway = gateway;
    }
    
    /// <summary>
    /// 检查资金费率数据完整性
    /// </summary>
    /// <param name="symbol">交易对</param>
    /// <param name="startTime">开始时间</param>
    /// <param name="endTime">结束时间</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>数据完整性检查结果</returns>
    public async Task<TimeSeriesIntegrityResult> CheckFundingRateAsync(
        string symbol,
        DateTime startTime,
        DateTime endTime,
        CancellationToken cancellationToken = default)
    {
        var symbolKey = NormalizeSymbolKey(symbol);
        var result = new TimeSeriesIntegrityResult
        {
            DataType = "FundingRate",
            Symbol = symbolKey,
            StartTime = startTime,
            EndTime = endTime
        };
        
        try
        {
            // 🆕 v11.0: 从数据库获取实际的资金费率结算周期
            // 不同symbol可能有不同的结算周期（如8小时、4小时等）
            var fundingIntervalHours = await GetFundingIntervalHoursAsync(symbolKey, cancellationToken);
            if (fundingIntervalHours <= 0)
            {
                // 如果数据库中没有数据，使用默认值8小时
                fundingIntervalHours = 8;
            }
            
            // 计算期望的数据点数量
            var totalHours = (endTime - startTime).TotalHours;
            var expectedCount = (int)Math.Ceiling(totalHours / fundingIntervalHours) + 1;
            result.ExpectedCount = expectedCount;
            
            // 从数据库查询现有数据（包含结算周期信息）
            var existingData = await LoadExistingFundingRatesWithIntervalAsync(
                symbolKey, startTime, endTime, cancellationToken
            );
            
            result.ExistingCount = existingData.Count;
            
            // 检查连续性，找出缺口
            if (existingData.Count == 0)
            {
                // 完全没有数据
                result.Gaps.Add(new TimeSeriesGap
                {
                    StartTime = startTime,
                    EndTime = endTime,
                    MissingCount = expectedCount
                });
            }
            else
            {
                // 检查数据连续性（使用实际的结算周期）
                result.Gaps = FindFundingRateGaps(
                    existingData, startTime, endTime, fundingIntervalHours
                );
            }
            
            result.IsComplete = result.Gaps.Count == 0 && result.ExistingCount >= expectedCount * 0.95; // 允许5%的容差
            
            return result;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ 检查资金费率数据完整性失败: {symbol}");
            Console.WriteLine($"   错误: {ex.Message}");
            throw;
        }
    }
    
    /// <summary>
    /// 检查恐惧与贪婪指数数据完整性
    /// </summary>
    /// <param name="startDate">开始日期</param>
    /// <param name="endDate">结束日期</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>数据完整性检查结果</returns>
    public async Task<TimeSeriesIntegrityResult> CheckFearGreedAsync(
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default)
    {
        var result = new TimeSeriesIntegrityResult
        {
            DataType = "FearGreed",
            StartTime = startDate.Date,
            EndTime = endDate.Date
        };
        
        try
        {
            // 恐惧与贪婪指数每天更新一次
            var expectedCount = (endDate.Date - startDate.Date).Days + 1;
            result.ExpectedCount = expectedCount;
            
            // 从数据库查询现有数据
            var existingData = await LoadExistingFearGreedAsync(
                startDate.Date, endDate.Date, cancellationToken
            );
            
            result.ExistingCount = existingData.Count;
            
            // 检查连续性，找出缺口
            if (existingData.Count == 0)
            {
                // 完全没有数据
                result.Gaps.Add(new TimeSeriesGap
                {
                    StartTime = startDate.Date,
                    EndTime = endDate.Date,
                    MissingCount = expectedCount
                });
            }
            else
            {
                // 检查数据连续性
                result.Gaps = FindFearGreedGaps(
                    existingData, startDate.Date, endDate.Date
                );
            }
            
            result.IsComplete = result.Gaps.Count == 0 && result.ExistingCount >= expectedCount * 0.95; // 允许5%的容差
            
            return result;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ 检查恐惧与贪婪指数数据完整性失败");
            Console.WriteLine($"   错误: {ex.Message}");
            throw;
        }
    }
    
    /// <summary>
    /// 补齐资金费率缺失数据
    /// </summary>
    public async Task<int> FillFundingRateGapsAsync(
        string symbol,
        List<TimeSeriesGap> gaps,
        CancellationToken cancellationToken = default)
    {
        if (_gateway == null)
        {
            Console.WriteLine("⚠️ 未提供BinanceGateway，无法补齐资金费率数据");
            return 0;
        }
        
        if (gaps.Count == 0)
            return 0;

        var baseSymbol = NormalizeBaseSymbol(symbol);
        var symbolKey = NormalizeSymbolKey(symbol);
        
        int totalFilled = 0;
        
        foreach (var gap in gaps)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            try
            {
                Console.WriteLine($"📥 补齐资金费率缺口: {gap.StartTime:yyyy-MM-dd HH:mm} ~ {gap.EndTime:yyyy-MM-dd HH:mm}");
                
                // 币安API限制：每次最多1000条
                // 资金费率每8小时一次，1000条 ≈ 333天
                // 如果缺口超过1000条，需要分多次请求
                var hoursDiff = (gap.EndTime - gap.StartTime).TotalHours;
                var estimatedCount = (int)Math.Ceiling(hoursDiff / 8);
                
                if (estimatedCount > 1000)
                {
                    // 需要分批请求
                    var batchStart = gap.StartTime;
                    while (batchStart < gap.EndTime)
                    {
                        var batchEnd = batchStart.AddHours(8 * 1000); // 1000条数据的时间范围
                        if (batchEnd > gap.EndTime)
                            batchEnd = gap.EndTime;
                        
                        var data = await _gateway.FetchFundingRatesAsync(
                            baseSymbol,
                            limit: 1000,
                            startTime: batchStart,
                            endTime: batchEnd,
                            cancellationToken
                        );
                        
                        if (data.Count > 0)
                        {
                            await SaveFundingRatesAsync(symbolKey, data);
                            totalFilled += data.Count;
                        }
                        
                        // 等待一下，避免触发频率限制（500请求/5分钟）
                        await Task.Delay(1000, cancellationToken);
                        
                        batchStart = batchEnd;
                    }
                }
                else
                {
                    // 单次请求即可
                    var data = await _gateway.FetchFundingRatesAsync(
                        baseSymbol,
                        limit: 1000,
                        startTime: gap.StartTime,
                        endTime: gap.EndTime,
                        cancellationToken
                    );
                    
                    if (data.Count > 0)
                    {
                        await SaveFundingRatesAsync(symbolKey, data);
                        totalFilled += data.Count;
                    }
                    
                    // 等待一下，避免触发频率限制
                    await Task.Delay(1000, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ 补齐资金费率缺口失败: {ex.Message}");
                // 继续处理下一个缺口
            }
        }
        
        return totalFilled;
    }
    
    /// <summary>
    /// 补齐恐惧与贪婪指数缺失数据
    /// </summary>
    public async Task<int> FillFearGreedGapsAsync(
        List<TimeSeriesGap> gaps,
        CancellationToken cancellationToken = default)
    {
        if (gaps.Count == 0)
            return 0;
        
        try
        {
            // 恐惧与贪婪指数API可以一次性获取所有历史数据
            // 使用FearGreedCollector的逻辑
            var allData = await FetchAllFearGreedDataAsync(cancellationToken);
            
            if (allData == null || allData.Count == 0)
                return 0;
            
            // 筛选出缺失日期的数据
            var missingDateStrs = new HashSet<string>();
            foreach (var gap in gaps)
            {
                var currentDate = gap.StartTime.Date;
                while (currentDate <= gap.EndTime.Date)
                {
                    missingDateStrs.Add(currentDate.ToString("yyyy-MM-dd"));
                    currentDate = currentDate.AddDays(1);
                }
            }
            
            var dataToFill = allData.Where(d => 
                missingDateStrs.Contains(d.Date.ToString("yyyy-MM-dd"))
            ).ToList();
            
            if (dataToFill.Count > 0)
            {
                await SaveFearGreedDataAsync(dataToFill);
            }
            
            return dataToFill.Count;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ 补齐恐惧与贪婪指数缺口失败: {ex.Message}");
            return 0;
        }
    }
    
    /// <summary>
    /// 检查多空比数据完整性
    /// </summary>
    /// <param name="symbol">交易对</param>
    /// <param name="period">周期（5m, 15m, 30m, 1h等）</param>
    /// <param name="startTime">开始时间</param>
    /// <param name="endTime">结束时间</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>数据完整性检查结果</returns>
    public async Task<TimeSeriesIntegrityResult> CheckLongShortRatioAsync(
        string symbol,
        string period,
        DateTime startTime,
        DateTime endTime,
        CancellationToken cancellationToken = default)
    {
        var result = new TimeSeriesIntegrityResult
        {
            DataType = "LongShortRatio",
            Symbol = symbol,
            Period = period,
            StartTime = startTime,
            EndTime = endTime
        };
        
        try
        {
            // 计算周期对应的分钟数
            var periodMinutes = ParsePeriodToMinutes(period);
            if (periodMinutes <= 0)
            {
                throw new ArgumentException($"无效的周期: {period}");
            }
            
            // 计算期望的数据点数量
            var totalMinutes = (endTime - startTime).TotalMinutes;
            var expectedCount = (int)Math.Ceiling(totalMinutes / periodMinutes) + 1;
            result.ExpectedCount = expectedCount;
            
            // 从数据库查询现有数据
            var existingData = await LoadExistingLongShortRatioAsync(
                symbol, period, startTime, endTime, cancellationToken
            );
            
            result.ExistingCount = existingData.Count;
            
            // 检查连续性，找出缺口
            if (existingData.Count == 0)
            {
                // 完全没有数据
                result.Gaps.Add(new TimeSeriesGap
                {
                    StartTime = startTime,
                    EndTime = endTime,
                    MissingCount = expectedCount
                });
            }
            else
            {
                // 检查数据连续性
                result.Gaps = FindLongShortRatioGaps(
                    existingData, startTime, endTime, periodMinutes
                );
            }
            
            result.IsComplete = result.Gaps.Count == 0 && result.ExistingCount >= expectedCount * 0.95; // 允许5%的容差
            
            return result;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ 检查多空比数据完整性失败: {symbol} - {period}");
            Console.WriteLine($"   错误: {ex.Message}");
            throw;
        }
    }
    
    /// <summary>
    /// 补齐多空比缺失数据
    /// </summary>
    public async Task<int> FillLongShortRatioGapsAsync(
        string symbol,
        string period,
        List<TimeSeriesGap> gaps,
        CancellationToken cancellationToken = default)
    {
        if (_gateway == null || gaps.Count == 0)
            return 0;
        
        var totalFilled = 0;
        
        foreach (var gap in gaps)
        {
            try
            {
                // 计算需要的数据量
                var periodMinutes = ParsePeriodToMinutes(period);
                var totalMinutes = (gap.EndTime - gap.StartTime).TotalMinutes;
                var estimatedCount = (int)Math.Ceiling(totalMinutes / periodMinutes);
                
                if (estimatedCount > 1000)
                {
                    // 需要分批请求
                    var batchStart = gap.StartTime;
                    while (batchStart < gap.EndTime)
                    {
                        var batchEnd = batchStart.AddMinutes(periodMinutes * 1000); // 1000条数据的时间范围
                        if (batchEnd > gap.EndTime)
                            batchEnd = gap.EndTime;
                        
                        var data = await _gateway.FetchLongShortRatioHistoryAsync(
                            symbol,
                            period,
                            limit: 1000,
                            startTime: batchStart,
                            endTime: batchEnd,
                            ratioType: "global", // 使用global类型
                            cancellationToken
                        );
                        
                        if (data.Count > 0)
                        {
                            await SaveLongShortRatioAsync(symbol, period, data);
                            totalFilled += data.Count;
                        }
                        
                        // 等待一下，避免触发频率限制
                        await Task.Delay(1000, cancellationToken);
                        
                        batchStart = batchEnd;
                    }
                }
                else
                {
                    // 单次请求即可
                    var data = await _gateway.FetchLongShortRatioHistoryAsync(
                        symbol,
                        period,
                        limit: estimatedCount + 50, // 加50条缓冲
                        startTime: gap.StartTime,
                        endTime: gap.EndTime,
                        ratioType: "global",
                        cancellationToken
                    );
                    
                    if (data.Count > 0)
                    {
                        await SaveLongShortRatioAsync(symbol, period, data);
                        totalFilled += data.Count;
                    }
                    
                    // 等待一下，避免触发频率限制
                    await Task.Delay(1000, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ 补齐多空比缺口失败: {ex.Message}");
                // 继续处理下一个缺口
            }
        }
        
        return totalFilled;
    }
    
    /// <summary>
    /// 从数据库获取资金费率的结算周期（小时）
    /// </summary>
    public async Task<int> GetFundingIntervalHoursAsync(string symbol, CancellationToken cancellationToken = default)
    {
        try
        {
            var symbolKey = NormalizeSymbolKey(symbol);
            var result = await Task.Run(() =>
            {
                using var db = DBHelper.CreateConnection();
                using var cmd = (SqliteCommand)db.CreateCommand();
                
                cmd.CommandText = @"
                    SELECT DISTINCT funding_interval_hours
                    FROM fundingrate
                    WHERE symbol_key = @symbolKey
                      AND funding_interval_hours IS NOT NULL
                      AND funding_interval_hours > 0
                    ORDER BY calc_time DESC
                    LIMIT 1";
                
                cmd.Parameters.AddWithValue("@symbolKey", symbolKey);
                
                var value = cmd.ExecuteScalar();
                if (value != null && value != DBNull.Value)
                {
                    return Convert.ToInt32(value);
                }
                return 0;
            }, cancellationToken);
            
            return result;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ 获取资金费率结算周期失败: {ex.Message}");
            return 0;
        }
    }
    
    /// <summary>
    /// 从数据库加载现有资金费率数据（包含时间戳和结算周期）
    /// </summary>
    private async Task<List<(long CalcTime, int IntervalHours)>> LoadExistingFundingRatesWithIntervalAsync(
        string symbol,
        DateTime startTime,
        DateTime endTime,
        CancellationToken cancellationToken)
    {
        var result = new List<(long, int)>();
        
        var startMs = new DateTimeOffset(startTime).ToUnixTimeMilliseconds();
        var endMs = new DateTimeOffset(endTime).ToUnixTimeMilliseconds();
        
        await Task.Run(() =>
        {
            using var db = DBHelper.CreateConnection();
            using var cmd = (SqliteCommand)db.CreateCommand();
            
            cmd.CommandText = @"
                SELECT calc_time, funding_interval_hours
                FROM fundingrate
                WHERE symbol_key = @symbolKey
                  AND calc_time >= @start_time
                  AND calc_time <= @end_time
                ORDER BY calc_time ASC";
            
            cmd.Parameters.AddWithValue("@symbolKey", NormalizeSymbolKey(symbol));
            cmd.Parameters.AddWithValue("@start_time", startMs);
            cmd.Parameters.AddWithValue("@end_time", endMs);
            
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                cancellationToken.ThrowIfCancellationRequested();
                var calcTime = reader.GetInt64(0);
                var intervalHours = reader.IsDBNull(1) ? 8 : reader.GetInt32(1); // 默认8小时
                result.Add((calcTime, intervalHours));
            }
        }, cancellationToken);
        
        return result;
    }
    
    /// <summary>
    /// 从数据库加载现有资金费率数据（仅时间戳，用于向后兼容）
    /// </summary>
    private async Task<List<long>> LoadExistingFundingRatesAsync(
        string symbol,
        DateTime startTime,
        DateTime endTime,
        CancellationToken cancellationToken)
    {
        var data = await LoadExistingFundingRatesWithIntervalAsync(symbol, startTime, endTime, cancellationToken);
        return data.Select(d => d.CalcTime).ToList();
    }
    
    /// <summary>
    /// 从数据库加载现有恐惧与贪婪指数数据
    /// </summary>
    private async Task<List<DateTime>> LoadExistingFearGreedAsync(
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken)
    {
        var result = new List<DateTime>();
        
        await Task.Run(() =>
        {
            using var db = DBHelper.CreateConnection();
            using var cmd = (SqliteCommand)db.CreateCommand();
            
            cmd.CommandText = @"
                SELECT date
                FROM fear_greed_index
                WHERE date >= @start_date
                  AND date <= @end_date
                ORDER BY date ASC";
            
            cmd.Parameters.AddWithValue("@start_date", startDate.ToString("yyyy-MM-dd"));
            cmd.Parameters.AddWithValue("@end_date", endDate.ToString("yyyy-MM-dd"));
            
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                cancellationToken.ThrowIfCancellationRequested();
                var dateStr = reader.GetString(0);
                if (DateTime.TryParse(dateStr, out var date))
                {
                    result.Add(date);
                }
            }
        }, cancellationToken);
        
        return result;
    }
    
    /// <summary>
    /// 查找资金费率数据缺口
    /// </summary>
    private List<TimeSeriesGap> FindFundingRateGaps(
        List<(long CalcTime, int IntervalHours)> existingData,
        DateTime startTime,
        DateTime endTime,
        int defaultIntervalHours)
    {
        var gaps = new List<TimeSeriesGap>();
        
        if (existingData.Count == 0)
            return gaps;
        
        var startMs = new DateTimeOffset(startTime).ToUnixTimeMilliseconds();
        var endMs = new DateTimeOffset(endTime).ToUnixTimeMilliseconds();
        
        // 使用第一条数据的结算周期，如果不存在则使用默认值
        var intervalHours = existingData[0].IntervalHours > 0 
            ? existingData[0].IntervalHours 
            : defaultIntervalHours;
        var intervalMs = intervalHours * 3600 * 1000L;
        
        // 对齐到结算周期边界
        var alignedStartMs = AlignToFundingRateTime(startMs, intervalHours);
        var alignedEndMs = AlignToFundingRateTime(endMs, intervalHours);
        
        // 检查开头是否有缺口
        if (existingData.Count > 0 && existingData[0].CalcTime > alignedStartMs)
        {
            var gapEnd = DateTimeOffset.FromUnixTimeMilliseconds(existingData[0].CalcTime).UtcDateTime;
            var missingCount = (int)((existingData[0].CalcTime - alignedStartMs) / intervalMs);
            
            gaps.Add(new TimeSeriesGap
            {
                StartTime = DateTimeOffset.FromUnixTimeMilliseconds(alignedStartMs).UtcDateTime,
                EndTime = gapEnd,
                MissingCount = missingCount
            });
        }
        
        // 检查中间的缺口（考虑结算周期可能变化）
        for (int i = 0; i < existingData.Count - 1; i++)
        {
            var current = existingData[i];
            var next = existingData[i + 1];
            
            // 使用当前记录的结算周期
            var currentIntervalHours = current.IntervalHours > 0 ? current.IntervalHours : defaultIntervalHours;
            var currentIntervalMs = currentIntervalHours * 3600 * 1000L;
            
            var expectedNext = current.CalcTime + currentIntervalMs;
            var gap = next.CalcTime - expectedNext;
            
            if (gap >= currentIntervalMs)
            {
                var missingCount = (int)(gap / currentIntervalMs);
                var gapEndMs = next.CalcTime - currentIntervalMs;
                
                gaps.Add(new TimeSeriesGap
                {
                    StartTime = DateTimeOffset.FromUnixTimeMilliseconds(expectedNext).UtcDateTime,
                    EndTime = DateTimeOffset.FromUnixTimeMilliseconds(gapEndMs).UtcDateTime,
                    MissingCount = missingCount
                });
            }
        }
        
        // 检查结尾是否有缺口
        if (existingData.Count > 0)
        {
            var last = existingData[existingData.Count - 1];
            var lastIntervalHours = last.IntervalHours > 0 ? last.IntervalHours : defaultIntervalHours;
            var lastIntervalMs = lastIntervalHours * 3600 * 1000L;
            
            var expectedNext = last.CalcTime + lastIntervalMs;
            
            if (expectedNext <= alignedEndMs)
            {
                var missingCount = (int)((alignedEndMs - expectedNext) / lastIntervalMs) + 1;
                
                gaps.Add(new TimeSeriesGap
                {
                    StartTime = DateTimeOffset.FromUnixTimeMilliseconds(expectedNext).UtcDateTime,
                    EndTime = DateTimeOffset.FromUnixTimeMilliseconds(alignedEndMs).UtcDateTime,
                    MissingCount = missingCount
                });
            }
        }
        
        return gaps;
    }
    
    /// <summary>
    /// 查找恐惧与贪婪指数数据缺口
    /// </summary>
    private List<TimeSeriesGap> FindFearGreedGaps(
        List<DateTime> existingDates,
        DateTime startDate,
        DateTime endDate)
    {
        var gaps = new List<TimeSeriesGap>();
        
        if (existingDates.Count == 0)
        {
            gaps.Add(new TimeSeriesGap
            {
                StartTime = startDate,
                EndTime = endDate,
                MissingCount = (endDate - startDate).Days + 1
            });
            return gaps;
        }
        
        // 检查开头是否有缺口
        var firstDate = existingDates[0];
        if (firstDate > startDate)
        {
            gaps.Add(new TimeSeriesGap
            {
                StartTime = startDate,
                EndTime = firstDate.AddDays(-1),
                MissingCount = (firstDate - startDate).Days
            });
        }
        
        // 检查中间的缺口
        for (int i = 0; i < existingDates.Count - 1; i++)
        {
            var current = existingDates[i];
            var next = existingDates[i + 1];
            var expectedNext = current.AddDays(1);
            
            if (next > expectedNext)
            {
                gaps.Add(new TimeSeriesGap
                {
                    StartTime = expectedNext,
                    EndTime = next.AddDays(-1),
                    MissingCount = (next - expectedNext).Days
                });
            }
        }
        
        // 检查结尾是否有缺口
        var lastDate = existingDates[existingDates.Count - 1];
        if (lastDate < endDate)
        {
            gaps.Add(new TimeSeriesGap
            {
                StartTime = lastDate.AddDays(1),
                EndTime = endDate,
                MissingCount = (endDate - lastDate).Days
            });
        }
        
        return gaps;
    }
    
    /// <summary>
    /// 对齐时间到资金费率更新时间点（根据结算周期）
    /// </summary>
    private long AlignToFundingRateTime(long timestampMs, int intervalHours)
    {
        var dt = DateTimeOffset.FromUnixTimeMilliseconds(timestampMs).UtcDateTime;
        var hour = dt.Hour;
        
        // 根据结算周期找到最近的更新时间点（向下取整）
        // 例如：8小时周期 -> 00:00, 08:00, 16:00
        //      4小时周期 -> 00:00, 04:00, 08:00, 12:00, 16:00, 20:00
        // 计算当前小时属于哪个周期段
        var targetHour = (hour / intervalHours) * intervalHours;
        
        var aligned = new DateTime(dt.Year, dt.Month, dt.Day, targetHour, 0, 0, DateTimeKind.Utc);
        return new DateTimeOffset(aligned).ToUnixTimeMilliseconds();
    }
    
    /// <summary>
    /// 保存资金费率数据到数据库
    /// </summary>
    private async Task SaveFundingRatesAsync(string symbol, IEnumerable<FundingRateData> data)
    {
        var sql = @"
            INSERT OR IGNORE INTO fundingrate (
                symbol_key, calc_time, calc_time_str, funding_interval_hours, last_funding_rate, created_at
            ) VALUES (
                @symbolKey, @calcTime, @calcTimeStr, @fundingIntervalHours, @lastFundingRate, @createdAt
            )";
        
        var now = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
        var count = 0;
        
        foreach (var item in data)
        {
            await DBHelper.ExecuteAsync(sql, new
            {
                symbolKey = NormalizeSymbolKey(symbol),
                calcTime = item.CalcTime,
                calcTimeStr = item.CalcTimeStr?.ToString("yyyy-MM-dd HH:mm:ss"),
                fundingIntervalHours = item.FundingIntervalHours,
                lastFundingRate = item.LastFundingRate,
                createdAt = now
            });
            count++;
        }
        
        Console.WriteLine($"✅ 保存资金费率数据: {count} 条");
    }
    
    /// <summary>
    /// 从API获取所有恐惧与贪婪指数数据
    /// </summary>
    private async Task<List<FearGreedData>?> FetchAllFearGreedDataAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var httpClient = new System.Net.Http.HttpClient();
            httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (compatible; fear-greed-index-collector/1.0)");
            
            var response = await httpClient.GetAsync(
                "https://api.alternative.me/fng/?limit=0&format=json",
                cancellationToken
            );
            
            if (!response.IsSuccessStatusCode)
                return null;
            
            var jsonContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var apiResponse = System.Text.Json.JsonSerializer.Deserialize<FearGreedApiResponse>(jsonContent);
            
            if (apiResponse?.Data == null || apiResponse.Data.Count == 0)
                return null;
            
            var result = new List<FearGreedData>();
            foreach (var item in apiResponse.Data)
            {
                if (!long.TryParse(item.Timestamp, out var timestamp))
                    continue;
                
                if (!int.TryParse(item.Value, out var value))
                    continue;
                
                var date = DateTimeOffset.FromUnixTimeSeconds(timestamp).UtcDateTime.Date;
                
                result.Add(new FearGreedData
                {
                    Date = date,
                    Value = value,
                    Classification = item.ValueClassification ?? string.Empty
                });
            }
            
            return result;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ 获取恐惧与贪婪指数数据失败: {ex.Message}");
            return null;
        }
    }
    
    /// <summary>
    /// 保存恐惧与贪婪指数数据到数据库
    /// </summary>
    private async Task SaveFearGreedDataAsync(List<FearGreedData> data)
    {
        var sql = @"
            INSERT OR IGNORE INTO fear_greed_index (date, value, classification)
            VALUES (@date, @value, @classification)";
        
        var count = 0;
        foreach (var item in data)
        {
            await DBHelper.ExecuteAsync(sql, new
            {
                date = item.Date.ToString("yyyy-MM-dd"),
                value = item.Value,
                classification = item.Classification
            });
            count++;
        }
        
        Console.WriteLine($"✅ 保存恐惧与贪婪指数数据: {count} 条");
    }
    
    private class FearGreedApiResponse
    {
        [System.Text.Json.Serialization.JsonPropertyName("data")]
        public List<FearGreedApiItem>? Data { get; set; }
    }
    
    /// <summary>
    /// 从数据库加载现有多空比数据
    /// </summary>
    private async Task<List<long>> LoadExistingLongShortRatioAsync(
        string symbol,
        string period,
        DateTime startTime,
        DateTime endTime,
        CancellationToken cancellationToken)
    {
        var result = new List<long>();
        
        var startSeconds = new DateTimeOffset(startTime).ToUnixTimeSeconds();
        var endSeconds = new DateTimeOffset(endTime).ToUnixTimeSeconds();
        
        await Task.Run(() =>
        {
            using var db = DBHelper.CreateConnection();
            using var cmd = (SqliteCommand)db.CreateCommand();
            
            cmd.CommandText = @"
                SELECT timestamp
                FROM longshortratio
                WHERE symbol = @symbol
                  AND period = @period
                  AND timestamp >= @start_time
                  AND timestamp <= @end_time
                ORDER BY timestamp ASC";
            
            cmd.Parameters.AddWithValue("@symbol", symbol);
            cmd.Parameters.AddWithValue("@period", period);
            cmd.Parameters.AddWithValue("@start_time", startSeconds);
            cmd.Parameters.AddWithValue("@end_time", endSeconds);
            
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                cancellationToken.ThrowIfCancellationRequested();
                var timestamp = reader.GetInt64(0);
                result.Add(timestamp);
            }
        }, cancellationToken);
        
        return result;
    }
    
    /// <summary>
    /// 查找多空比数据缺口
    /// </summary>
    private List<TimeSeriesGap> FindLongShortRatioGaps(
        List<long> existingData,
        DateTime startTime,
        DateTime endTime,
        int periodMinutes)
    {
        var gaps = new List<TimeSeriesGap>();
        
        if (existingData.Count == 0)
            return gaps;
        
        var startSeconds = new DateTimeOffset(startTime).ToUnixTimeSeconds();
        var endSeconds = new DateTimeOffset(endTime).ToUnixTimeSeconds();
        var periodSeconds = periodMinutes * 60L;
        
        // 对齐到周期边界
        var alignedStartSeconds = (startSeconds / periodSeconds) * periodSeconds;
        var alignedEndSeconds = ((endSeconds / periodSeconds) + 1) * periodSeconds;
        
        // 检查开头是否有缺口
        if (existingData[0] > alignedStartSeconds)
        {
            var gapEnd = DateTimeOffset.FromUnixTimeSeconds(existingData[0]).UtcDateTime;
            var missingCount = (int)((existingData[0] - alignedStartSeconds) / periodSeconds);
            
            gaps.Add(new TimeSeriesGap
            {
                StartTime = DateTimeOffset.FromUnixTimeSeconds(alignedStartSeconds).UtcDateTime,
                EndTime = gapEnd,
                MissingCount = missingCount
            });
        }
        
        // 检查中间是否有缺口
        for (int i = 0; i < existingData.Count - 1; i++)
        {
            var current = existingData[i];
            var next = existingData[i + 1];
            var expectedNext = current + periodSeconds;
            
            if (next > expectedNext + periodSeconds) // 允许一个周期的容差
            {
                var gapStart = DateTimeOffset.FromUnixTimeSeconds(current + periodSeconds).UtcDateTime;
                var gapEnd = DateTimeOffset.FromUnixTimeSeconds(next).UtcDateTime;
                var missingCount = (int)((next - expectedNext) / periodSeconds);
                
                gaps.Add(new TimeSeriesGap
                {
                    StartTime = gapStart,
                    EndTime = gapEnd,
                    MissingCount = missingCount
                });
            }
        }
        
        // 检查结尾是否有缺口
        if (existingData.Count > 0 && existingData[^1] < alignedEndSeconds)
        {
            var gapStart = DateTimeOffset.FromUnixTimeSeconds(existingData[^1] + periodSeconds).UtcDateTime;
            var missingCount = (int)((alignedEndSeconds - existingData[^1]) / periodSeconds);
            
            gaps.Add(new TimeSeriesGap
            {
                StartTime = gapStart,
                EndTime = DateTimeOffset.FromUnixTimeSeconds(alignedEndSeconds).UtcDateTime,
                MissingCount = missingCount
            });
        }
        
        return gaps;
    }
    
    /// <summary>
    /// 保存多空比数据到数据库
    /// </summary>
    private async Task SaveLongShortRatioAsync(
        string symbol,
        string period,
        IEnumerable<LongShortRatioData> data)
    {
        var sql = @"
            INSERT OR IGNORE INTO longshortratio (
                symbol, period, timestamp, long_account_ratio, long_position_ratio, 
                long_short_ratio, update_time, created_at
            ) VALUES (
                @symbol, @period, @timestamp, @longAccountRatio, @longPositionRatio,
                @longShortRatio, @updateTime, @createdAt
            )";
        
        var now = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
        var count = 0;
        
        foreach (var item in data)
        {
            var timestamp = new DateTimeOffset(item.UpdateTime).ToUnixTimeSeconds();
            
            await DBHelper.ExecuteAsync(sql, new
            {
                symbol = symbol,
                period = period,
                timestamp = timestamp,
                longAccountRatio = (double?)item.LongAccountRatio,
                longPositionRatio = (double?)item.LongPositionRatio,
                longShortRatio = (double)item.LongShortRatio,
                updateTime = item.UpdateTime.ToString("yyyy-MM-dd HH:mm:ss"),
                createdAt = now
            });
            count++;
        }
        
        Console.WriteLine($"✅ 保存多空比数据: {symbol} - {period} - {count} 条");
    }
    
    /// <summary>
    /// 解析周期字符串为分钟数
    /// </summary>
    private int ParsePeriodToMinutes(string period)
    {
        if (string.IsNullOrWhiteSpace(period))
            return 0;
        
        period = period.ToLowerInvariant().Trim();
        
        if (period.EndsWith("m"))
        {
            if (int.TryParse(period.Substring(0, period.Length - 1), out var minutes))
                return minutes;
        }
        else if (period.EndsWith("h"))
        {
            if (int.TryParse(period.Substring(0, period.Length - 1), out var hours))
                return hours * 60;
        }
        else if (period.EndsWith("d"))
        {
            if (int.TryParse(period.Substring(0, period.Length - 1), out var days))
                return days * 24 * 60;
        }
        
        return 0;
    }
    
    private class FearGreedApiItem
    {
        [System.Text.Json.Serialization.JsonPropertyName("value")]
        public string? Value { get; set; }
        
        [System.Text.Json.Serialization.JsonPropertyName("value_classification")]
        public string? ValueClassification { get; set; }
        
        [System.Text.Json.Serialization.JsonPropertyName("timestamp")]
        public string? Timestamp { get; set; }
    }
}

/// <summary>
/// 时间序列数据完整性检查结果
/// </summary>
public class TimeSeriesIntegrityResult
{
    public string DataType { get; set; } = string.Empty;
    public string? Symbol { get; set; }
    public string? Period { get; set; }  // 多空比需要周期字段
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public int ExpectedCount { get; set; }
    public int ExistingCount { get; set; }
    public List<TimeSeriesGap> Gaps { get; set; } = new();
    public bool IsComplete { get; set; }
    
    public double CompletenessPercentage => ExpectedCount > 0 
        ? (double)ExistingCount / ExpectedCount * 100 
        : 0;
}

/// <summary>
/// 时间序列数据缺口
/// </summary>
public class TimeSeriesGap
{
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public int MissingCount { get; set; }
}
