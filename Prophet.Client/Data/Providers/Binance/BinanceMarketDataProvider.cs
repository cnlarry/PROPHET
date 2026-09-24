using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Prophet.Client.Data.Abstractions;
using Prophet.Client.Data.Models;
using Prophet.Client.Services.Market;

namespace Prophet.Client.Data.Providers.Binance;

/// <summary>
/// Binance 行情 Provider（适配现有 <see cref="BinanceExchangeGateway"/>）
/// </summary>
public sealed class BinanceMarketDataProvider : IMarketDataProvider
{
    private readonly BinanceExchangeGateway _gateway;
    private bool _disposed;

    public BinanceMarketDataProvider(BinanceExchangeGateway? gateway = null)
    {
        _gateway = gateway ?? new BinanceExchangeGateway();
    }

    public ExchangeId ExchangeId => ExchangeId.Binance;
    public string Name => "Binance";

    public async Task<IReadOnlyList<Kline>> FetchKlinesAsync(
        string symbol,
        string interval,
        int limit = 1000,
        DateTime? startTime = null,
        DateTime? endTime = null,
        CancellationToken cancellationToken = default)
    {
        var candles = await _gateway.FetchKlinesAsync(symbol, interval, limit, startTime, endTime, cancellationToken);
        if (candles.Count == 0)
        {
            return Array.Empty<Kline>();
        }

        return candles.Select(c => new Kline
        {
            Symbol = symbol,
            Interval = interval,
            OpenTime = c.Time,
            CloseTime = c.CloseTime,
            Open = (decimal)c.Open,
            High = (decimal)c.High,
            Low = (decimal)c.Low,
            Close = (decimal)c.Close,
            Volume = (decimal)c.Volume,
            IsClosed = c.IsClosed
        }).ToList();
    }

    public async Task<Ticker?> FetchTickerAsync(string symbol, CancellationToken cancellationToken = default)
    {
        var data = await _gateway.FetchTicker24hAsync(symbol, cancellationToken);
        if (data == null)
        {
            return null;
        }

        // 当前 BinanceGateway 只实现了 24h ticker；先映射到 Data Plane Ticker 模型
        return new Ticker
        {
            Symbol = data.Symbol,
            LastPrice = data.Price,
            PriceChangePercent24h = data.PriceChangePercent,
            Volume24h = data.Volume24h,
            QuoteVolume24h = data.QuoteVolume24h,
            BuyVolume24h = data.BuyVolume,
            SellVolume24h = data.SellVolume,
            UpdateTime = data.LastUpdateTime
        };
    }

    public Task<OrderBook?> FetchOrderBookAsync(string symbol, int depth = 20, CancellationToken cancellationToken = default)
    {
        // BinanceExchangeGateway 当前未实现订单簿（后续可补齐）
        return Task.FromResult<OrderBook?>(null);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _gateway.Dispose();
        _disposed = true;
    }
}


