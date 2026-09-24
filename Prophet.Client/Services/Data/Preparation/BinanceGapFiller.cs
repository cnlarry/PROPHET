using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Prophet.Client.Database.Repositories;
using Prophet.Client.Models;
using Prophet.Client.Services.Market;
using Prophet.Client.Services.Data.Preparation;

namespace Prophet.Client.Services.Data.Preparation;

/// <summary>
/// 币安API数据补齐器
/// 使用币安公开API补齐小缺口（< 7天）
/// </summary>
public class BinanceGapFiller
{
    private readonly IBinanceExchangeGateway _gateway;
    private readonly MarketDataRepository _repository;
    private const int MAX_LIMIT = 1000; // 币安API单次最多返回1000条
    private const int RATE_LIMIT_DELAY_MS = 100; // 限流：每100ms一次请求
    
    public BinanceGapFiller(
        IBinanceExchangeGateway gateway,
        MarketDataRepository repository)
    {
        _gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }
    
    /// <summary>
    /// 补齐数据缺口
    /// </summary>
    public async Task FillGapsAsync(
        string symbol,
        string timeframe,
        List<DataGap> gaps,
        IProgress<(string Message, double Percent)>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (gaps.Count == 0)
        {
            Console.WriteLine($"✅ {symbol} {timeframe}: 无需补齐");
            return;
        }
        
        Console.WriteLine($"🔧 开始补齐 {symbol} {timeframe} 数据缺口...");
        Console.WriteLine($"   缺口数量: {gaps.Count}");
        
        int totalGaps = gaps.Count;
        int completedGaps = 0;
        
        foreach (var gap in gaps)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            double baseProgress = 70 + (completedGaps * 20.0 / totalGaps);
            progress?.Report(($"补齐缺口 {gap.StartTime:MM-dd HH:mm}...", baseProgress));
            
            Console.WriteLine($"   📌 补齐: {gap}");
            
            await FillSingleGapAsync(symbol, timeframe, gap, cancellationToken);
            
            completedGaps++;
            
            // 限流：避免触发币安API限制
            await Task.Delay(RATE_LIMIT_DELAY_MS, cancellationToken);
        }
        
        Console.WriteLine($"✅ {symbol} {timeframe} 缺口补齐完成");
    }
    
    /// <summary>
    /// 补齐单个缺口
    /// </summary>
    private async Task FillSingleGapAsync(
        string symbol,
        string timeframe,
        DataGap gap,
        CancellationToken cancellationToken)
    {
        var normalizedSymbol = symbol.ToUpperInvariant();
        var totalInserted = 0;
        
        int intervalMinutes = DSLAnalyzer.TimeframeToMinutes(timeframe);
        long intervalMs = intervalMinutes * 60 * 1000L;
        
        // 对齐缺口边界到时间框架边界
        // gap.StartTime 和 gap.EndTime 已经是正确对齐的缺口范围（不包含下一个K线）
        var alignedGapStart = AlignToTimeframe(gap.StartTime, intervalMinutes);
        var alignedGapEnd = AlignToTimeframe(gap.EndTime, intervalMinutes);
        
        // gap.EndTime 已经是最后一个缺失K线的时间（不包含下一个K线）
        // 所以实际缺口结束时间应该是 alignedGapEnd（包含这个时间点的K线）
        var actualGapEnd = alignedGapEnd;
        
        Console.WriteLine($"      🔍 缺口范围: {alignedGapStart:yyyy-MM-dd HH:mm} ~ {actualGapEnd:yyyy-MM-dd HH:mm}");
        Console.WriteLine($"      📊 期望补齐: {gap.MissingCount} 根");
        
        var currentStart = alignedGapStart;
        
        while (currentStart <= actualGapEnd)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            // 计算本次请求的结束时间：最多请求MAX_LIMIT根K线
            var maxEndTime = currentStart.AddMinutes(MAX_LIMIT * intervalMinutes);
            var currentEnd = maxEndTime < actualGapEnd ? maxEndTime : actualGapEnd;
            
            // 确保currentEnd不超过缺口范围
            if (currentEnd > actualGapEnd)
            {
                currentEnd = actualGapEnd;
            }

            // 对齐开始时间到时间框架边界（再次确认）
            var alignedStart = AlignToTimeframe(currentStart, intervalMinutes);
            
            // 确保传递给API的时间是UTC时间
            var candles = await _gateway.FetchKlinesAsync(
                normalizedSymbol,
                timeframe,
                MAX_LIMIT,
                alignedStart.ToUniversalTime(),
                currentEnd.ToUniversalTime(),
                cancellationToken);
            
            if (candles.Count == 0)
            {
                Console.WriteLine($"      ⚠️ 未获取到数据: {alignedStart:yyyy-MM-dd HH:mm}");
                // 如果获取不到数据，跳过这个时间段
                currentStart = alignedStart.AddMinutes(intervalMinutes);
                continue;
            }
            
            // 过滤出缺口范围内的K线（避免API返回超出缺口范围的数据）
            // gap.EndTime 已经是缺口结束时间（不包含下一个K线），所以使用 <= 包含它
            // 确保使用UTC时间计算时间戳
            var gapStartMs = new DateTimeOffset(alignedGapStart.ToUniversalTime(), TimeSpan.Zero).ToUnixTimeMilliseconds();
            var gapEndMs = new DateTimeOffset(actualGapEnd.ToUniversalTime(), TimeSpan.Zero).ToUnixTimeMilliseconds();
            
            var filteredCandles = candles
                .Where(c => 
                {
                    var candleMs = new DateTimeOffset(c.Time).ToUnixTimeMilliseconds();
                    // 包含缺口开始和结束时间点的K线
                    return candleMs >= gapStartMs && candleMs <= gapEndMs;
                })
                .ToList();
            
            if (filteredCandles.Count > 0)
            {
                await _repository.BulkInsertKlinesAsync(normalizedSymbol, timeframe, filteredCandles);
                totalInserted += filteredCandles.Count;
                Console.WriteLine($"      📥 本次插入: {filteredCandles.Count} 根 (累计: {totalInserted}/{gap.MissingCount})");
            }
            else if (candles.Count > 0)
            {
                Console.WriteLine($"      ⚠️ API返回 {candles.Count} 根，但都被过滤（不在缺口范围内）");
            }

            // 根据时间框架计算下一个时间点
            var lastCandleTime = candles[^1].Time;
            var nextStart = lastCandleTime.AddMinutes(intervalMinutes);
            
            // 如果下一个时间点已经超出缺口范围，结束循环
            // 注意：如果 lastCandleTime 正好是 actualGapEnd，那么 nextStart = actualGapEnd + intervalMs，会超出范围
            if (nextStart > actualGapEnd)
            {
                break;
            }
            
            currentStart = nextStart;
            
            // 如果返回的数据少于MAX_LIMIT，说明已经到达数据边界，结束循环
            if (candles.Count < MAX_LIMIT)
            {
                break;
            }

            await Task.Delay(RATE_LIMIT_DELAY_MS, cancellationToken);
        }
        
        if (totalInserted > 0)
        {
            Console.WriteLine($"      ✅ 补齐完成: {totalInserted} 根");
        }
        else
        {
            Console.WriteLine($"      ⚠️ 未插入任何数据，可能API返回的数据不在缺口范围内");
        }
    }
    
    /// <summary>
    /// 将时间对齐到时间框架边界
    /// 确保使用UTC时间，避免时区问题
    /// </summary>
    private DateTime AlignToTimeframe(DateTime time, int intervalMinutes)
    {
        // 确保time被当作UTC时间处理
        DateTimeOffset timeOffset;
        if (time.Kind == DateTimeKind.Unspecified)
        {
            // 如果Kind是Unspecified，假设它是UTC时间
            timeOffset = new DateTimeOffset(time, TimeSpan.Zero);
        }
        else
        {
            // 转换为UTC
            timeOffset = new DateTimeOffset(time.ToUniversalTime(), TimeSpan.Zero);
        }
        
        var timestamp = timeOffset.ToUnixTimeMilliseconds();
        var intervalMs = intervalMinutes * 60 * 1000L;
        var alignedTimestamp = (timestamp / intervalMs) * intervalMs;
        
        // 返回UTC时间
        return DateTimeOffset.FromUnixTimeMilliseconds(alignedTimestamp).UtcDateTime;
    }
}

