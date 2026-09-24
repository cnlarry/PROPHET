using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Prophet.Client.Backtest.DataFeed;
using Prophet.Client.Data.Symbols;
using Prophet.Client.Database.Repositories;
using Prophet.Client.Models;
using Prophet.Client.Services.Market;

namespace Prophet.Client.Backtest.DataFeeds;

/// <summary>
/// 本地数据源（从SQLite数据库加载K线数据，如果数据库中没有数据则从API获取）
/// </summary>
public class LocalDataFeed : IDataFeed
{
    private readonly MarketDataRepository _marketDataRepository;
    private readonly BinanceMarketDataService? _marketDataService;

    public LocalDataFeed(MarketDataRepository? repository = null, BinanceMarketDataService? marketDataService = null)
    {
        _marketDataRepository = repository ?? App.MarketDataContext.Repository;
        // 如果提供了marketDataService则使用，否则通过Gateway创建（用于从API获取数据）
        _marketDataService = marketDataService ?? (App.MarketDataContext != null 
            ? new BinanceMarketDataService(App.MarketDataContext.Gateway, _marketDataRepository) 
            : null);
    }

    /// <summary>
    /// 从本地数据库加载历史K线数据（回测模式）
    /// 如果数据库中没有数据，则尝试从API获取并保存到数据库
    /// </summary>
    public async Task<List<Candlestick>> LoadCandlesAsync(
        string symbol,
        string interval,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default)
    {
        // symbol 语义升级：允许传入 InstrumentKey（例如 BTCUSDT-OKX-SWAP）
        // 目前离线回测的数据源仍是 Binance K线缓存（后续会迁移为 symbol_key + 多交易所路由）
        var querySymbol = symbol;
        if (InstrumentKey.TryParse(symbol, out var instrumentKey))
        {
            if (!string.Equals(instrumentKey.Exchange, "BINANCE", StringComparison.OrdinalIgnoreCase))
            {
                throw new NotSupportedException(
                    $"当前离线回测 K线数据源仅支持 BINANCE（收到: {instrumentKey.Exchange}）。" +
                    "请先选择 BINANCE 的标的，或等待 OKX/Bybit/Bitget K线接入后再回测该交易所。"
                );
            }

            // 旧的 klines 表目前仍按 base_symbol 存储（迁移到 symbol_key 后会改为 instrumentKey.Value）
            querySymbol = instrumentKey.BaseSymbol;
        }

        // 首先尝试从数据库加载
        var candles = await _marketDataRepository.GetKlinesAsync(
            querySymbol,
            interval,
            startDate,
            endDate,
            limit: 100000); // 设置一个足够大的limit
        
        // 如果数据库中没有数据或数据不足，尝试从API获取
        if ((candles == null || candles.Count == 0) && _marketDataService != null)
        {
            Console.WriteLine($"⚠️ [LocalDataFeed] 数据库中未找到 {querySymbol} {interval} 的K线数据，尝试从API获取...");
            Console.WriteLine($"   时间范围: {startDate:yyyy-MM-dd HH:mm:ss} ~ {endDate:yyyy-MM-dd HH:mm:ss}");
            
            try
            {
                // 从API获取指定时间范围的K线数据
                var apiCandles = await _marketDataService.GetKlinesByTimeRangeAsync(
                    querySymbol,
                    interval,
                    startDate,
                    endDate,
                    limit: 1500); // 币安API最大支持1500条
                
                if (apiCandles != null && apiCandles.Count > 0)
                {
                    Console.WriteLine($"✅ [LocalDataFeed] 从API获取到 {apiCandles.Count} 条K线数据，正在保存到数据库...");
                    
                    // 保存到数据库
                    var savedCount = await _marketDataService.SaveKlinesToDatabaseAsync(querySymbol, interval, apiCandles);
                    Console.WriteLine($"✅ [LocalDataFeed] 已保存 {savedCount} 条K线数据到数据库");
                    
                    // 如果API返回的数据覆盖了请求的时间范围，直接返回API数据
                    // 否则再次从数据库查询（可能API返回的数据不够完整）
                    var apiDataCoversRange = apiCandles.Count > 0 
                        && apiCandles.First().Time <= startDate 
                        && apiCandles.Last().Time >= endDate;
                    
                    if (apiDataCoversRange)
                    {
                        // 过滤出请求时间范围内的数据
                        candles = apiCandles
                            .Where(c => c.Time >= startDate && c.Time <= endDate)
                            .OrderBy(c => c.Time)
                            .ToList();
                    }
                    else
                    {
                        // API数据可能不完整，再次从数据库查询（可能包含更多数据）
                        candles = await _marketDataRepository.GetKlinesAsync(
                            querySymbol,
                            interval,
                            startDate,
                            endDate,
                            limit: 100000);
                    }
                }
                else
                {
                    Console.WriteLine($"⚠️ [LocalDataFeed] API返回空数据，可能该时间范围内没有K线数据");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ [LocalDataFeed] 从API获取K线数据失败: {ex.Message}");
                // 如果API获取失败，返回数据库中的数据（可能为空）
            }
        }
        
        return candles ?? new List<Candlestick>();
    }

    /// <summary>
    /// 流式获取实时K线数据（实盘模式）
    /// 离线版：不支持实时流式数据
    /// </summary>
    public async IAsyncEnumerable<Candlestick> StreamCandlesAsync(
        string symbol,
        string interval,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // 离线版不支持流式数据
        await Task.CompletedTask;
        yield break;
    }

    /// <summary>
    /// 获取最新K线（实盘模式）
    /// 离线版：返回数据库中最新的K线
    /// </summary>
    public async Task<Candlestick?> GetLatestCandleAsync(
        string symbol,
        string interval,
        CancellationToken cancellationToken = default)
    {
        // 获取最近1根K线
        var endTime = DateTime.UtcNow;
        var startTime = endTime.AddDays(-1); // 往前推1天

        var klines = await _marketDataRepository.GetKlinesAsync(
            symbol,
            interval,
            startTime,
            endTime,
            limit: 1);

        return klines.LastOrDefault();
    }
}
