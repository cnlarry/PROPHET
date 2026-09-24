using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Prophet.Client.Data.Abstractions;
using Prophet.Client.Data.Models;
using Prophet.Client.Data.Symbols;
using Prophet.Client.Database.Repositories;
using Prophet.Client.Models;
using Prophet.Client.Services.Market;

namespace Prophet.Client.Data.Services;

/// <summary>
/// Data Plane 读入口（Query Facade）
/// 目标：上层（UI/回测/实盘）只依赖 Data.*，不直接 new Gateway/Repository/采集服务。
///
/// 说明：当前实现先复用既有 Binance 的 MarketDataCacheService/Repository 能力；
/// 后续多交易所时，将按 ExchangeId 进行路由与多 Provider 实现。
/// </summary>
public sealed class DataQueryService : IDisposable
{
    private readonly MarketDataRepository _repository;
    private readonly DataPlaneService _dataPlane;
    private readonly IBinanceExchangeGateway _binanceGateway;

    private MarketDataCollectionService? _binanceCollectionService;
    private MarketDataCacheService? _binanceCache;

    private bool _started;
    private bool _disposed;

    public DataQueryService(MarketDataRepository repository, IBinanceExchangeGateway binanceGateway)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _binanceGateway = binanceGateway ?? throw new ArgumentNullException(nameof(binanceGateway));
        _dataPlane = Prophet.Client.App.DataPlane;
    }

    private IMarketDataProvider GetMarketProvider(ExchangeId? exchangeId = null)
        => _dataPlane.Market(exchangeId);

    private static (string BaseSymbol, ExchangeId? ExchangeOverride) ResolveRoutingFromSymbol(string symbol)
    {
        // AICoin式：Symbol 本身携带交易所信息
        if (InstrumentKey.TryParse(symbol, out var key))
        {
            var exchange = key.Exchange switch
            {
                "BINANCE" => ExchangeId.Binance,
                "OKX" => ExchangeId.Okx,
                "BYBIT" => ExchangeId.Bybit,
                "BITGET" => ExchangeId.Unknown, // 预留：后续接入 Bitget
                _ => ExchangeId.Unknown
            };

            return (key.BaseSymbol, exchange == ExchangeId.Unknown ? null : exchange);
        }

        // 旧形式：BTCUSDT
        return (symbol, null);
    }

    public async Task EnsureStartedAsync()
    {
        if (_started)
        {
            return;
        }

        _binanceCollectionService = new MarketDataCollectionService(
            _binanceGateway.HttpClient,
            _repository,
            _binanceGateway);
        _binanceCollectionService.InitializeCollectors();

        _binanceCache = new MarketDataCacheService(_repository, _binanceGateway, _binanceCollectionService);

        await _binanceCollectionService.StartAsync();
        _started = true;
    }

    public async Task<List<Candlestick>> GetKlinesAsync(
        string symbol,
        string interval,
        DateTime? startTime = null,
        DateTime? endTime = null,
        int limit = 1000)
    {
        // 统一 K 线读入口：直接走 MarketDataRepository（数据库优先）
        // 上层（行情页/回测/实盘）应通过此方法读取 K 线，而非各自 new Repository
        return await _repository.GetKlinesAsync(symbol, interval, startTime, endTime, limit);
    }

    public async Task<FearGreedIndex?> GetFearGreedIndexAsync(ExchangeId? exchangeId = null)
    {
        var resolved = exchangeId ?? _dataPlane.DefaultMarketDataExchange;
        if (resolved != ExchangeId.Binance)
        {
            // 目前恐惧贪婪指数依赖 Binance 这套采集/落库链路（其他交易所可后续扩展为独立数据源）
            return null;
        }

        await EnsureStartedAsync();
        var data = await _binanceCache!.GetFearGreedIndexAsync();
        if (data == null)
        {
            return null;
        }

        return new FearGreedIndex
        {
            Value = data.Value,
            Classification = data.Classification,
            UpdateTime = data.Date.Kind == DateTimeKind.Utc
                ? data.Date
                : DateTime.SpecifyKind(data.Date, DateTimeKind.Utc)
        };
    }

    public async Task<List<Ticker>> GetTickersAsync(List<string> symbols, ExchangeId? exchangeId = null)
    {
        if (symbols == null || symbols.Count == 0)
        {
            return new List<Ticker>();
        }

        // 若 symbols 本身携带交易所（InstrumentKey），以第一个 symbol 的交易所为准（Market模块每次只应选一个Instrument）。
        var first = ResolveRoutingFromSymbol(symbols[0]);
        var resolved = first.ExchangeOverride ?? (exchangeId ?? _dataPlane.DefaultMarketDataExchange);

        // Binance：优先使用既有缓存/采集链路（具备落库与降级逻辑）
        if (resolved == ExchangeId.Binance)
        {
            await EnsureStartedAsync();
            var baseSymbols = symbols.Select(s => ResolveRoutingFromSymbol(s).BaseSymbol).ToList();
            var tickers = await _binanceCache!.GetTickers24hAsync(baseSymbols);

            return tickers.Select(t => new Ticker
            {
                // 对上层保持“输入symbol”的语义（InstrumentKey 或 BTCUSDT）
                Symbol = symbols.FirstOrDefault(x => ResolveRoutingFromSymbol(x).BaseSymbol == t.Symbol) ?? t.Symbol,
                LastPrice = t.Price,
                PriceChangePercent24h = t.PriceChangePercent,
                Volume24h = t.Volume24h,
                QuoteVolume24h = t.QuoteVolume24h,
                BuyVolume24h = t.BuyVolume,
                SellVolume24h = t.SellVolume,
                UpdateTime = t.LastUpdateTime
            }).ToList();
        }

        // 其他交易所：先直连 Provider（后续可加入落库/缓存）
        var provider = GetMarketProvider(resolved);
        var results = new List<Ticker>(symbols.Count);
        foreach (var symbol in symbols)
        {
            var (baseSymbol, _) = ResolveRoutingFromSymbol(symbol);
            var ticker = await provider.FetchTickerAsync(baseSymbol);
            if (ticker != null)
            {
                ticker.Symbol = symbol;
                results.Add(ticker);
            }
        }

        return results;
    }

    public async Task<Ticker?> GetTickerAsync(string symbol, ExchangeId? exchangeId = null)
    {
        var list = await GetTickersAsync(new List<string> { symbol }, exchangeId);
        return list.FirstOrDefault();
    }

    public async Task<List<FundingRateSnapshot>> GetFundingRateSnapshotsAsync(List<string> symbols, ExchangeId? exchangeId = null)
    {
        var resolved = exchangeId ?? _dataPlane.DefaultMarketDataExchange;
        if (symbols == null || symbols.Count == 0)
        {
            return new List<FundingRateSnapshot>();
        }

        if (resolved == ExchangeId.Binance)
        {
            await EnsureStartedAsync();
            var rates = await _binanceCache!.GetFundingRatesAsync(symbols);

            var snapshots = new List<FundingRateSnapshot>(rates.Count);
            foreach (var rate in rates)
            {
                // 计算 24h 变化（沿用旧逻辑：从数据库取历史数据做近似）
                var oldRates = await _repository.GetFundingRateDataAsync(rate.Symbol, 10);
                var oldRate = oldRates.FirstOrDefault(r =>
                    r.CalcTime < rate.CalcTime &&
                    (rate.CalcTime - r.CalcTime) <= 86400000);

                decimal? changePercent = null;
                if (oldRate != null)
                {
                    changePercent = (rate.LastFundingRate - oldRate.LastFundingRate) * 100m;
                }

                snapshots.Add(new FundingRateSnapshot
                {
                    Symbol = rate.Symbol,
                    Rate = rate.LastFundingRate,
                    Change24hPercent = changePercent,
                    UpdateTime = DateTime.UtcNow
                });
            }

            return snapshots;
        }

        // 其他交易所：如果 Provider 支持 funding rate，则直连查询（后续可加入落库与历史变化计算）
        var provider = GetMarketProvider(resolved);
        if (provider is IFundingRateProvider fundingRateProvider)
        {
            var result = new List<FundingRateSnapshot>(symbols.Count);
            foreach (var symbol in symbols)
            {
                var snapshot = await fundingRateProvider.FetchFundingRateSnapshotAsync(symbol);
                if (snapshot != null)
                {
                    result.Add(snapshot);
                }
            }
            return result;
        }

        return new List<FundingRateSnapshot>();
    }

    public async Task<LongShortRatio?> GetLongShortRatioAsync(string symbol, string period, ExchangeId? exchangeId = null)
    {
        var resolved = exchangeId ?? _dataPlane.DefaultMarketDataExchange;
        if (resolved != ExchangeId.Binance)
        {
            var provider = GetMarketProvider(resolved);
            if (provider is ILongShortRatioProvider longShortRatioProvider)
            {
                return await longShortRatioProvider.FetchLongShortRatioAsync(symbol, period);
            }

            return null;
        }

        await EnsureStartedAsync();
        var ratio = await _binanceCache!.GetLongShortRatioAsync(symbol, period);
        if (ratio == null)
        {
            return null;
        }

        return new LongShortRatio
        {
            Symbol = ratio.Symbol,
            Period = ratio.Period,
            LongAccountRatio = ratio.LongAccountRatio,
            LongPositionRatio = ratio.LongPositionRatio,
            LongShortRatioValue = ratio.LongShortRatio,
            UpdateTime = ratio.UpdateTime
        };
    }

    public async Task<TakerLongShortRatio?> GetTakerLongShortRatioAsync(string symbol, string period, ExchangeId? exchangeId = null)
    {
        var resolved = exchangeId ?? _dataPlane.DefaultMarketDataExchange;
        if (resolved != ExchangeId.Binance)
        {
            var provider = GetMarketProvider(resolved);
            if (provider is ITakerLongShortRatioProvider takerProvider)
            {
                return await takerProvider.FetchTakerLongShortRatioAsync(symbol, period);
            }

            return null;
        }

        await EnsureStartedAsync();
        var data = await _binanceCache!.GetTakerLongShortRatioAsync(symbol, period);
        if (data == null)
        {
            return null;
        }

        return new TakerLongShortRatio
        {
            Symbol = data.Symbol,
            Period = data.Period,
            BuyVol = data.BuyVol,
            SellVol = data.SellVol,
            BuySellRatio = data.BuySellRatio,
            UpdateTime = data.UpdateTime
        };
    }

    public async Task<List<Ticker>> GetTopGainersAsync(int count, ExchangeId? exchangeId = null)
    {
        var resolved = exchangeId ?? _dataPlane.DefaultMarketDataExchange;
        if (resolved != ExchangeId.Binance)
        {
            return new List<Ticker>();
        }

        await EnsureStartedAsync();
        var gainers = await _binanceCache!.GetTopGainersAsync(count);
        return gainers.Select(t => new Ticker
        {
            Symbol = t.Symbol,
            LastPrice = t.Price,
            PriceChangePercent24h = t.PriceChangePercent,
            Volume24h = t.Volume24h,
            QuoteVolume24h = t.QuoteVolume24h,
            BuyVolume24h = t.BuyVolume,
            SellVolume24h = t.SellVolume,
            UpdateTime = t.LastUpdateTime
        }).ToList();
    }

    public async Task<List<Ticker>> GetTopLosersAsync(int count, ExchangeId? exchangeId = null)
    {
        var resolved = exchangeId ?? _dataPlane.DefaultMarketDataExchange;
        if (resolved != ExchangeId.Binance)
        {
            return new List<Ticker>();
        }

        await EnsureStartedAsync();
        var losers = await _binanceCache!.GetTopLosersAsync(count);
        return losers.Select(t => new Ticker
        {
            Symbol = t.Symbol,
            LastPrice = t.Price,
            PriceChangePercent24h = t.PriceChangePercent,
            Volume24h = t.Volume24h,
            QuoteVolume24h = t.QuoteVolume24h,
            BuyVolume24h = t.BuyVolume,
            SellVolume24h = t.SellVolume,
            UpdateTime = t.LastUpdateTime
        }).ToList();
    }

    public async Task<OpenInterest?> GetOpenInterestAsync(string symbol, ExchangeId? exchangeId = null)
    {
        var resolved = exchangeId ?? _dataPlane.DefaultMarketDataExchange;
        if (resolved != ExchangeId.Binance)
        {
            var provider = GetMarketProvider(resolved);
            if (provider is IOpenInterestProvider openInterestProvider)
            {
                return await openInterestProvider.FetchOpenInterestAsync(symbol);
            }

            return null;
        }

        await EnsureStartedAsync();
        var oi = await _binanceCache!.GetOpenInterestAsync(symbol);
        if (oi == null)
        {
            return null;
        }

        return new OpenInterest
        {
            Symbol = oi.Symbol,
            Value = oi.OpenInterest,
            UpdateTime = oi.UpdateTime
        };
    }

    public async Task<Liquidation?> GetLiquidationAsync(string symbol, ExchangeId? exchangeId = null)
    {
        var resolved = exchangeId ?? _dataPlane.DefaultMarketDataExchange;
        if (resolved != ExchangeId.Binance)
        {
            return null;
        }

        await EnsureStartedAsync();
        var data = await _binanceCache!.GetLiquidationDataAsync(symbol);
        if (data == null)
        {
            return null;
        }

        return new Liquidation
        {
            Symbol = data.Symbol,
            LongLiquidation = data.LongLiquidation,
            ShortLiquidation = data.ShortLiquidation,
            TotalLiquidation = data.TotalLiquidation,
            UpdateTime = data.UpdateTime
        };
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            _binanceCollectionService?.Dispose();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ [DataQueryService] 释放采集服务失败: {ex.Message}");
        }

        try
        {
            _binanceCache?.Dispose();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ [DataQueryService] 释放缓存服务失败: {ex.Message}");
        }

        _disposed = true;
    }
}


