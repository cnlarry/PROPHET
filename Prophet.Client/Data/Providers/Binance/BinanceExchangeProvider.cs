using System;
using Prophet.Client.Data.Abstractions;

namespace Prophet.Client.Data.Providers.Binance;

/// <summary>
/// Binance 交易所 Provider（先用适配器桥接现有实现；后续可合并重复代码）
/// </summary>
public sealed class BinanceExchangeProvider : IExchangeProvider
{
    private readonly BinanceMarketDataProvider _marketData;
    private readonly BinanceTradingProvider _trading;

    public BinanceExchangeProvider()
    {
        _marketData = new BinanceMarketDataProvider();
        _trading = new BinanceTradingProvider();
    }

    public ExchangeId ExchangeId => ExchangeId.Binance;
    public string Name => "Binance";

    public IMarketDataProvider MarketData => _marketData;
    public ITradingProvider Trading => _trading;

    public void Dispose()
    {
        _marketData.Dispose();
        _trading.Dispose();
    }
}


