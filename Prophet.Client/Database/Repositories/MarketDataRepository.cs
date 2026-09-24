using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Prophet.Client.Data.Symbols;
using Prophet.Client.Models;

namespace Prophet.Client.Database.Repositories;

/// <summary>
/// 市场数据仓储（K线、资金费率、恐慌指数等）
/// </summary>
public class MarketDataRepository
{
    private readonly InstrumentRepository _instrumentRepository;

    /// <summary>
    /// 默认构造函数（惰性创建依赖仓库）
    /// </summary>
    public MarketDataRepository() : this(null)
    {
    }

    /// <summary>
    /// 依赖注入构造函数
    /// </summary>
    public MarketDataRepository(InstrumentRepository? instrumentRepository = null)
    {
        _instrumentRepository = instrumentRepository ?? new InstrumentRepository();
    }
    private static string NormalizeSymbolKey(string symbolOrKey)
    {
        if (string.IsNullOrWhiteSpace(symbolOrKey))
        {
            return "BTCUSDT-BINANCE-SWAP";
        }

        // 已是 InstrumentKey（例如 BTCUSDT-OKX-SWAP）
        if (InstrumentKey.TryParse(symbolOrKey, out var key))
        {
            return key.ToString().ToUpperInvariant();
        }

        // 兜底：历史调用大量使用 base_symbol（如 BTCUSDT）。统一映射为 BINANCE-SWAP 的 symbol_key
        var upper = symbolOrKey.ToUpperInvariant();
        return $"{upper}-BINANCE-SWAP";
    }
    /// <summary>
    /// 获取K线数据
    /// 策略: 直接查询对应时间框架的数据（数据库中已存储所有时间框架的数据）
    /// </summary>
    public async Task<List<Candlestick>> GetKlinesAsync(
        string symbol,
        string interval,
        DateTime? startTime = null,
        DateTime? endTime = null,
        int limit = 1000)
    {
        symbol = NormalizeSymbolKey(symbol);
        // 🔧 v4.0: 直接查询对应时间框架的数据，不再进行K线合成
        return await GetKlines1mDirectAsync(symbol, interval, startTime, endTime, limit);
    }

    /// <summary>
    /// 直接获取1m K线数据（从数据库）
    /// 逻辑：
    /// - 无时间限制：按时间降序获取最新的N条，然后反转为升序
    /// - 有endTime（加载更多）：获取endTime之前的N条，按时间降序，然后反转为升序
    /// </summary>
    private async Task<List<Candlestick>> GetKlines1mDirectAsync(
        string symbol,
        string interval,
        DateTime? startTime = null,
        DateTime? endTime = null,
        int limit = 1000)
    {
        var sql = @"
            SELECT 
                open_time AS Time,
                close_time AS CloseTime,
                open AS Open,
                high AS High,
                low AS Low,
                close AS Close,
                volume AS Volume
            FROM klines
            WHERE symbol_key = @symbol AND interval = @interval";

        var parameters = new DynamicParameters();
        parameters.Add("symbol", symbol);
        parameters.Add("interval", interval);

        if (startTime.HasValue)
        {
            sql += " AND open_time >= @startTime";
            // 🔧 v4.0: 不进行时区转换，假设输入时间已经是UTC时间
            var utcStartTime = startTime.Value.Kind == DateTimeKind.Utc 
                ? startTime.Value 
                : DateTime.SpecifyKind(startTime.Value, DateTimeKind.Utc);
            parameters.Add("startTime", new DateTimeOffset(utcStartTime, TimeSpan.Zero).ToUnixTimeMilliseconds());
        }

        if (endTime.HasValue)
        {
            // 🔑 关键：endTime 表示"包含"，使用 <= 确保包含结束时间点的K线
            // 回测需要包含结束时间点的K线，所以使用 <=
            sql += " AND open_time <= @endTime";
            // 🔧 v4.0: 不进行时区转换，假设输入时间已经是UTC时间
            var utcEndTime = endTime.Value.Kind == DateTimeKind.Utc 
                ? endTime.Value 
                : DateTime.SpecifyKind(endTime.Value, DateTimeKind.Utc);
            parameters.Add("endTime", new DateTimeOffset(utcEndTime, TimeSpan.Zero).ToUnixTimeMilliseconds());
        }

        // 🔧 v4.0: 当有时间范围限制时，直接按升序返回所有数据，不使用 LIMIT
        // LIMIT 只在无时间限制时使用（用于获取最新N条数据）
        if (startTime.HasValue || endTime.HasValue)
        {
            // 有时间范围：按升序返回所有数据
            sql += " ORDER BY open_time ASC";
        }
        else
        {
            // 无时间范围：按降序获取最新N条，后续会反转
            sql += " ORDER BY open_time DESC LIMIT @limit";
            parameters.Add("limit", limit);
        }

        
        using var connection = DBHelper.CreateConnection();
        var results = await connection.QueryAsync<KlineDbModel>(sql, parameters);
        
        var candlesList = results.Select(k => new Candlestick
        {
            Time = DateTimeOffset.FromUnixTimeMilliseconds(k.Time).UtcDateTime,
            CloseTime = k.CloseTime > 0 ? DateTimeOffset.FromUnixTimeMilliseconds(k.CloseTime).UtcDateTime : null,
            Open = k.Open,
            High = k.High,
            Low = k.Low,
            Close = k.Close,
            Volume = k.Volume,
            IsClosed = true // 数据库中的K线都是已闭合的
        }).ToList();
        
        // 🔧 v4.0: 只在无时间范围限制时才需要反转（因为查询是降序的）
        // 有时间范围时，查询已经是升序的，不需要反转
        if (!startTime.HasValue && !endTime.HasValue)
        {
            candlesList.Reverse();
        }
        
        if (candlesList.Count > 0)
        {
            Console.WriteLine($"   📅 数据时间: {candlesList.First().Time:yyyy-MM-dd HH:mm:ss} -> {candlesList.Last().Time:yyyy-MM-dd HH:mm:ss}");
        }
        
        return candlesList;
    }

    /// <summary>
    /// 批量插入K线数据
    /// </summary>
    public async Task<int> BulkInsertKlinesAsync(string symbol, string interval, List<Candlestick> candles)
    {
        if (!candles.Any())
            return 0;

        symbol = NormalizeSymbolKey(symbol);

        // ✅ 确保 symbol_key 在 instruments 表中存在（解决外键约束问题）
        await _instrumentRepository.EnsureInstrumentExistsAsync(symbol);

        var now = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");

        // 🔧 v4.0: 使用 INSERT OR IGNORE 跳过已存在的数据（主键：symbol + open_time + close_time）
        var sql = @"
            INSERT OR IGNORE INTO klines (
                symbol_key, interval, open_time, open, high, low, close, volume,
                close_time, quote_volume, trade_count, taker_buy_volume, taker_buy_quote_volume, created_at
            ) VALUES (
                @symbol, @interval, @openTime, @open, @high, @low, @close, @volume,
                @closeTime, @quoteVolume, @tradeCount, @takerBuyVolume, @takerBuyQuoteVolume, @createdAt
            )";

        using var connection = DBHelper.CreateConnection();
        using var transaction = connection.BeginTransaction();

        try
        {
            var insertedCount = 0;
            var skippedCount = 0;
            foreach (var candle in candles)
            {
                var openTime = new DateTimeOffset(candle.Time).ToUnixTimeMilliseconds();
                // 🔧 v4.0: 优先使用真实的收盘时间，如果不存在则计算（开盘时间 + 时间框架 - 1毫秒）
                var closeTime = candle.CloseTime.HasValue
                    ? new DateTimeOffset(candle.CloseTime.Value).ToUnixTimeMilliseconds()
                    : openTime + GetIntervalMilliseconds(interval) - 1;

                var affected = await connection.ExecuteAsync(sql, new
                {
                    symbol,
                    interval,
                    openTime,
                    open = candle.Open,
                    high = candle.High,
                    low = candle.Low,
                    close = candle.Close,
                    volume = candle.Volume,
                    closeTime,
                    quoteVolume = candle.QuoteVolume,
                    tradeCount = candle.TradeCount,
                    takerBuyVolume = candle.TakerBuyVolume,
                    takerBuyQuoteVolume = candle.TakerBuyQuoteVolume,
                    createdAt = now
                }, transaction);

                if (affected > 0)
                    insertedCount++;
                else
                    skippedCount++;
            }

            transaction.Commit();
            if (skippedCount > 0)
            {
                Console.WriteLine($"✅ 批量插入K线数据: {insertedCount} 新增, {skippedCount} 跳过（已存在） ({symbol} {interval})");
            }
            else
            {
                Console.WriteLine($"✅ 批量插入K线数据: {insertedCount} 条 ({symbol} {interval})");
            }
            return insertedCount;
        }
        catch (Exception ex)
        {
            transaction.Rollback();
            Console.WriteLine($"❌ 批量插入K线数据失败: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// 获取K线数据的时间范围
    /// </summary>
    public async Task<(DateTime? start, DateTime? end)> GetKlineTimeRangeAsync(string symbol, string interval)
    {
        symbol = NormalizeSymbolKey(symbol);
        var sql = @"
            SELECT MIN(open_time) as MinTime, MAX(open_time) as MaxTime
            FROM klines
            WHERE symbol_key = @symbol AND interval = @interval";

        using var connection = DBHelper.CreateConnection();
        var result = await connection.QueryFirstOrDefaultAsync<(long? MinTime, long? MaxTime)>(
            sql, new { symbol, interval });

        if (result.MinTime.HasValue && result.MaxTime.HasValue)
        {
            return (
                DateTimeOffset.FromUnixTimeMilliseconds(result.MinTime.Value).UtcDateTime,
                DateTimeOffset.FromUnixTimeMilliseconds(result.MaxTime.Value).UtcDateTime
            );
        }

        return (null, null);
    }

    /// <summary>
    /// 获取K线数据数量
    /// </summary>
    public async Task<int> GetKlineCountAsync(string symbol, string interval)
    {
        symbol = NormalizeSymbolKey(symbol);
        var sql = "SELECT COUNT(*) FROM klines WHERE symbol_key = @symbol AND interval = @interval";
        var count = await DBHelper.ExecuteScalarAsync<int?>(sql, new { symbol, interval });
        return count ?? 0;
    }

    /// <summary>
    /// 清空指定交易对和时间框架的K线数据
    /// </summary>
    public async Task<int> ClearKlinesAsync(string symbol, string interval)
    {
        symbol = NormalizeSymbolKey(symbol);
        var sql = "DELETE FROM klines WHERE symbol_key = @symbol AND interval = @interval";
        return await DBHelper.ExecuteAsync(sql, new { symbol, interval });
    }

    /// <summary>
    /// 获取数据库中所有的交易对列表
    /// </summary>
    public async Task<List<string>> GetAvailableSymbolsAsync()
    {
        var sql = "SELECT DISTINCT symbol_key FROM klines ORDER BY symbol_key";
        var symbols = await DBHelper.QueryAsync<string>(sql);
        return symbols.ToList();
    }

    /// <summary>
    /// 获取指定交易对的可用时间框架
    /// </summary>
    public async Task<List<string>> GetAvailableIntervalsAsync(string symbol)
    {
        symbol = NormalizeSymbolKey(symbol);
        var sql = "SELECT DISTINCT interval FROM klines WHERE symbol_key = @symbol ORDER BY interval";
        var intervals = await DBHelper.QueryAsync<string>(sql, new { symbol });
        return intervals.ToList();
    }
    
    /// <summary>
    /// 检查K线数据的连续性，找出缺失的时间段
    /// 返回缺失的时间段列表（开始时间，结束时间）
    /// </summary>
    public async Task<List<(DateTime start, DateTime end)>> CheckDataContinuityAsync(string symbol, string interval)
    {
        var gaps = new List<(DateTime start, DateTime end)>();
        
        try
        {
            symbol = NormalizeSymbolKey(symbol);
            // 获取所有K线的时间戳，按时间排序
            var sql = @"
                SELECT open_time 
                FROM klines 
                WHERE symbol_key = @symbol AND interval = @interval 
                ORDER BY open_time ASC";
            
            using var connection = DBHelper.CreateConnection();
            var timestamps = await connection.QueryAsync<long>(sql, new { symbol, interval });
            var times = timestamps.Select(t => DateTimeOffset.FromUnixTimeMilliseconds(t).UtcDateTime).ToList();
            
            if (times.Count < 2)
            {
                // 数据不足，无法检查连续性
                return gaps;
            }
            
            // 计算时间间隔（毫秒）
            var intervalMs = GetIntervalMilliseconds(interval);
            var toleranceMs = intervalMs * 1.1; // 允许10%的容差
            
            // 检查相邻K线之间的时间差
            for (int i = 1; i < times.Count; i++)
            {
                var timeDiff = (times[i] - times[i - 1]).TotalMilliseconds;
                
                // 如果时间差超过容差，认为有缺失
                if (timeDiff > toleranceMs)
                {
                    var gapStart = times[i - 1].AddMilliseconds(intervalMs);
                    var gapEnd = times[i].AddMilliseconds(-intervalMs);
                    
                    // 确保gapEnd >= gapStart
                    if (gapEnd >= gapStart)
                    {
                        gaps.Add((gapStart, gapEnd));
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ [MarketDataRepository] 检查数据连续性失败: {ex.Message}");
        }
        
        return gaps;
    }

    /// <summary>
    /// 获取时间框架对应的毫秒数
    /// </summary>
    private long GetIntervalMilliseconds(string interval)
    {
        return interval switch
        {
            "1m" => 60 * 1000,
            "3m" => 3 * 60 * 1000,
            "5m" => 5 * 60 * 1000,
            "15m" => 15 * 60 * 1000,
            "30m" => 30 * 60 * 1000,
            "1h" => 60 * 60 * 1000,
            "2h" => 2 * 60 * 60 * 1000,
            "4h" => 4 * 60 * 60 * 1000,
            "6h" => 6 * 60 * 60 * 1000,
            "8h" => 8 * 60 * 60 * 1000,
            "12h" => 12 * 60 * 60 * 1000,
            "1d" => 24 * 60 * 60 * 1000,
            "3d" => 3 * 24 * 60 * 60 * 1000,
            "1w" => 7 * 24 * 60 * 60 * 1000,
            "1M" => 30L * 24 * 60 * 60 * 1000,
            _ => 60 * 1000
        };
    }

    /// <summary>
    /// 获取恐惧与贪婪指数数据
    /// </summary>
    /// <param name="days">加载天数，默认365天</param>
    /// <param name="endDate">结束日期（可选），用于限制数据范围</param>
    public async Task<List<FearGreedData>> GetFearGreedDataAsync(int days = 365, DateTime? endDate = null)
    {
        var sql = @"
            SELECT 
                date AS Date,
                value AS Value,
                classification AS Classification
            FROM fear_greed_index
            WHERE 1=1";

        var parameters = new DynamicParameters();
        
        if (endDate.HasValue)
        {
            sql += " AND date <= @endDate";
            parameters.Add("endDate", endDate.Value.ToString("yyyy-MM-dd"));
        }
        
        sql += " ORDER BY date DESC LIMIT @limit";
        parameters.Add("limit", days);

        using var connection = DBHelper.CreateConnection();
        var results = await connection.QueryAsync<FearGreedData>(sql, parameters);
        
        var data = results.ToList();
        
        // 反转为升序（从旧到新）
        data.Reverse();
        
        return data;
    }

    /// <summary>
    /// 获取资金费率数据
    /// </summary>
    /// <param name="symbol">交易对</param>
    /// <param name="count">加载条数，默认720条（约30天，每8小时一次）</param>
    /// <param name="endTime">结束时间（可选），用于限制数据范围</param>
    public async Task<List<FundingRateData>> GetFundingRateDataAsync(
        string symbol, 
        int count = 720, 
        DateTime? endTime = null)
    {
        symbol = NormalizeSymbolKey(symbol);
        var sql = @"
            SELECT 
                symbol_key AS Symbol,
                calc_time AS CalcTime,
                calc_time_str AS CalcTimeStr,
                funding_interval_hours AS FundingIntervalHours,
                last_funding_rate AS LastFundingRate
            FROM fundingrate
            WHERE symbol_key = @symbol";

        var parameters = new DynamicParameters();
        parameters.Add("symbol", symbol);
        
        if (endTime.HasValue)
        {
            sql += " AND calc_time <= @endTime";
            parameters.Add("endTime", new DateTimeOffset(endTime.Value).ToUnixTimeMilliseconds());
        }
        
        sql += " ORDER BY calc_time DESC LIMIT @limit";
        parameters.Add("limit", count);

        using var connection = DBHelper.CreateConnection();
        var results = await connection.QueryAsync<FundingRateDbModel>(sql, parameters);
        
        var data = results.Select(r => {
            DateTime? calcTimeStr = null;
            if (!string.IsNullOrEmpty(r.CalcTimeStr))
            {
                // 尝试解析字符串格式的时间（yyyy-MM-dd HH:mm:ss）
                if (DateTime.TryParse(r.CalcTimeStr, out var parsedTime))
                {
                    calcTimeStr = parsedTime.Kind == DateTimeKind.Utc 
                        ? parsedTime 
                        : parsedTime.ToUniversalTime();
                }
            }
            
            return new FundingRateData
            {
                Symbol = r.Symbol,
                CalcTime = r.CalcTime,
                CalcTimeStr = calcTimeStr,
                FundingIntervalHours = r.FundingIntervalHours,
                LastFundingRate = (decimal)r.LastFundingRate
            };
        }).ToList();
        
        // 反转为升序（从旧到新）
        data.Reverse();
        
        return data;
    }

    /// <summary>
    /// 获取最新的恐惧与贪婪指数
    /// </summary>
    public async Task<FearGreedData?> GetLatestFearGreedIndexAsync()
    {
        var sql = @"
            SELECT 
                date AS Date,
                value AS Value,
                classification AS Classification
            FROM fear_greed_index
            ORDER BY date DESC
            LIMIT 1";

        using var connection = DBHelper.CreateConnection();
        return await connection.QueryFirstOrDefaultAsync<FearGreedData>(sql);
    }

    /// <summary>
    /// 获取恐惧与贪婪指数历史数据（指定天数）
    /// </summary>
    public async Task<List<FearGreedData>> GetFearGreedHistoryAsync(int days)
    {
        return await GetFearGreedDataAsync(days);
    }

    /// <summary>
    /// 获取最新的资金费率
    /// </summary>
    public async Task<FundingRateData?> GetLatestFundingRateAsync(string symbol)
    {
        symbol = NormalizeSymbolKey(symbol);
        var sql = @"
            SELECT 
                symbol_key AS Symbol,
                calc_time AS CalcTime,
                calc_time_str AS CalcTimeStr,
                funding_interval_hours AS FundingIntervalHours,
                last_funding_rate AS LastFundingRate
            FROM fundingrate
            WHERE symbol_key = @symbol
            ORDER BY calc_time DESC
            LIMIT 1";

        using var connection = DBHelper.CreateConnection();
        var result = await connection.QueryFirstOrDefaultAsync<FundingRateDbModel>(sql, new { symbol });
        
        if (result == null)
            return null;

        DateTime? calcTimeStr = null;
        if (!string.IsNullOrEmpty(result.CalcTimeStr))
        {
            if (DateTime.TryParse(result.CalcTimeStr, out var parsedTime))
            {
                calcTimeStr = parsedTime.Kind == DateTimeKind.Utc 
                    ? parsedTime 
                    : parsedTime.ToUniversalTime();
            }
        }

        return new FundingRateData
        {
            Symbol = result.Symbol,
            CalcTime = result.CalcTime,
            CalcTimeStr = calcTimeStr,
            FundingIntervalHours = result.FundingIntervalHours,
            LastFundingRate = (decimal)result.LastFundingRate
        };
    }

    /// <summary>
    /// 保存24h Ticker数据
    /// </summary>
    public async Task SaveTicker24hAsync(Ticker24hData data)
    {
        // TODO: 实现保存逻辑（如果需要持久化）
        await Task.CompletedTask;
    }

    /// <summary>
    /// 保存持仓量数据
    /// </summary>
    public async Task SaveOpenInterestAsync(OpenInterestData data)
    {
        // TODO: 实现保存逻辑（如果需要持久化）
        await Task.CompletedTask;
    }

    /// <summary>
    /// 保存多空比数据
    /// </summary>
    public async Task SaveLongShortRatioAsync(LongShortRatioData data)
    {
        // TODO: 实现保存逻辑（如果需要持久化）
        await Task.CompletedTask;
    }

    /// <summary>
    /// 获取多空比历史数据
    /// </summary>
    /// <param name="symbol">交易对符号</param>
    /// <param name="period">周期（5m, 15m, 30m, 1h等）</param>
    /// <param name="count">数据条数</param>
    /// <param name="endTime">结束时间（可选）</param>
    /// <returns>多空比数据列表（按时间降序，最新的在前）</returns>
    public async Task<List<LongShortRatioData>> GetLongShortRatioDataAsync(
        string symbol,
        string period = "5m",
        int count = 100,
        DateTime? endTime = null)
    {
        symbol = NormalizeSymbolKey(symbol);
        var sql = @"
            SELECT 
                symbol_key AS Symbol,
                period AS Period,
                timestamp AS Timestamp,
                long_account_ratio AS LongAccountRatio,
                long_position_ratio AS LongPositionRatio,
                long_short_ratio AS LongShortRatio,
                update_time AS UpdateTime
            FROM longshortratio
            WHERE symbol_key = @symbol AND period = @period";

        var parameters = new DynamicParameters();
        parameters.Add("symbol", symbol);
        parameters.Add("period", period);
        
        if (endTime.HasValue)
        {
            sql += " AND timestamp <= @endTime";
            parameters.Add("endTime", new DateTimeOffset(endTime.Value).ToUnixTimeSeconds());
        }
        
        sql += " ORDER BY timestamp DESC LIMIT @limit";
        parameters.Add("limit", count);

        using var connection = DBHelper.CreateConnection();
        
        try
        {
            var results = await connection.QueryAsync<LongShortRatioDbModel>(sql, parameters);
            
            var data = results.Select(r => {
                DateTime? updateTime = null;
                if (!string.IsNullOrEmpty(r.UpdateTime))
                {
                    if (DateTime.TryParse(r.UpdateTime, out var parsedTime))
                    {
                        updateTime = parsedTime.Kind == DateTimeKind.Utc 
                            ? parsedTime 
                            : parsedTime.ToUniversalTime();
                    }
                }
                else if (r.Timestamp > 0)
                {
                    // 如果没有格式化时间，从时间戳转换
                    updateTime = DateTimeOffset.FromUnixTimeSeconds(r.Timestamp).UtcDateTime;
                }
                
                return new LongShortRatioData
                {
                    Symbol = r.Symbol,
                    Period = r.Period,
                    LongAccountRatio = (decimal)(r.LongAccountRatio ?? 0.0),
                    LongPositionRatio = (decimal)(r.LongPositionRatio ?? 0.0),
                    LongShortRatio = (decimal)r.LongShortRatio,
                    UpdateTime = updateTime ?? DateTime.UtcNow
                };
            }).ToList();
            
            // 反转为升序（从旧到新），与资金费率保持一致
            data.Reverse();
            
            return data;
        }
        catch (Exception ex)
        {
            // 如果表不存在，返回空列表（允许表不存在的情况）
            if (ex.Message.Contains("no such table", StringComparison.OrdinalIgnoreCase) ||
                ex.Message.Contains("does not exist", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine($"⚠️ [MarketDataRepository] 多空比表不存在，返回空列表");
                return new List<LongShortRatioData>();
            }
            
            Console.WriteLine($"❌ [MarketDataRepository] 查询多空比数据失败: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// K线数据库模型（用于Dapper映射）
    /// </summary>
    private class KlineDbModel
    {
        public long Time { get; set; }
        public long CloseTime { get; set; }
        public double Open { get; set; }
        public double High { get; set; }
        public double Low { get; set; }
        public double Close { get; set; }
        public double Volume { get; set; }
    }

    /// <summary>
    /// 资金费率数据库模型（用于Dapper映射）
    /// </summary>
    private class FundingRateDbModel
    {
        public string Symbol { get; set; } = string.Empty;
        public long CalcTime { get; set; }
        public string? CalcTimeStr { get; set; }  // 修复：SQLite中是TEXT类型，不是long
        public int FundingIntervalHours { get; set; }
        public double LastFundingRate { get; set; }
    }

    /// <summary>
    /// 多空比数据库模型（用于Dapper映射）
    /// </summary>
    private class LongShortRatioDbModel
    {
        public string Symbol { get; set; } = string.Empty;
        public string Period { get; set; } = string.Empty;
        public long Timestamp { get; set; }
        public double? LongAccountRatio { get; set; }
        public double? LongPositionRatio { get; set; }
        public double LongShortRatio { get; set; }
        public string? UpdateTime { get; set; }
    }
}

