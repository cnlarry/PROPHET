using System;
using System.Collections.Generic;
using Prophet.Client.Data.Abstractions;

namespace Prophet.Client.Data;

/// <summary>
/// Data Plane 路由中心：统一管理多个交易所 Provider，并提供按交易所获取能力的入口。
/// </summary>
public sealed class DataPlaneService : IDisposable
{
    private readonly Dictionary<ExchangeId, IExchangeProvider> _providers = new();
    private bool _disposed;

    public ExchangeId DefaultMarketDataExchange { get; private set; } = ExchangeId.Binance;
    public ExchangeId DefaultTradingExchange { get; private set; } = ExchangeId.Binance;

    public void SetDefaults(ExchangeId marketDataExchange, ExchangeId tradingExchange)
    {
        DefaultMarketDataExchange = marketDataExchange;
        DefaultTradingExchange = tradingExchange;
    }

    public void Register(IExchangeProvider provider)
    {
        if (provider == null) throw new ArgumentNullException(nameof(provider));
        _providers[provider.ExchangeId] = provider;
    }

    public bool TryGetProvider(ExchangeId exchangeId, out IExchangeProvider provider)
        => _providers.TryGetValue(exchangeId, out provider!);

    public IExchangeProvider GetProvider(ExchangeId exchangeId)
    {
        if (_providers.TryGetValue(exchangeId, out var provider))
        {
            return provider;
        }

        throw new KeyNotFoundException($"未注册交易所 Provider: {exchangeId}");
    }

    public IMarketDataProvider Market(ExchangeId? exchangeId = null)
        => GetProvider(exchangeId ?? DefaultMarketDataExchange).MarketData;

    public ITradingProvider Trading(ExchangeId? exchangeId = null)
        => GetProvider(exchangeId ?? DefaultTradingExchange).Trading;

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        foreach (var provider in _providers.Values)
        {
            provider.Dispose();
        }

        _providers.Clear();
        _disposed = true;
    }
}


