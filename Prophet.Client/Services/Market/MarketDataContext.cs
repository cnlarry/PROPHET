using System;
using Prophet.Client.Database.Repositories;
using Prophet.Client.Services.Data;
using Prophet.Client.Services.Data.Preparation;

namespace Prophet.Client.Services.Market;

/// <summary>
/// 市场数据相关的共享上下文（仓储、缓存、网关）
/// </summary>
public sealed class MarketDataContext : IDisposable
{
    public MarketDataRepository Repository { get; }
    public KlineCache Cache { get; }
    public IBinanceExchangeGateway Gateway { get; }
    public MarketStreamHub StreamHub { get; }
    public KlineSyncService KlineSyncService { get; }

    private readonly bool _ownsGateway;
    private bool _disposed;

    public MarketDataContext(
        MarketDataRepository? repository = null,
        KlineCache? cache = null,
        IBinanceExchangeGateway? gateway = null)
    {
        Repository = repository ?? new MarketDataRepository();
        Cache = cache ?? new KlineCache();
        Gateway = gateway ?? new BinanceExchangeGateway();
        StreamHub = new MarketStreamHub(Repository, Gateway);
        var downloader = new BinanceHistoricalDataDownloader(Gateway.HttpClient);
        var gapFiller = new BinanceGapFiller(Gateway, Repository);
        KlineSyncService = new KlineSyncService(downloader, gapFiller);
        _ownsGateway = gateway == null;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        Cache.Dispose();
        StreamHub.Dispose();

        if (_ownsGateway)
        {
            Gateway.Dispose();
        }

        _disposed = true;
    }
}
