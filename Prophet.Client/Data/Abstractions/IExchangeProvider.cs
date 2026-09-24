using System;

namespace Prophet.Client.Data.Abstractions;

/// <summary>
/// Data Plane 的交易所 Provider：同时提供 MarketData + Trading 能力。
/// </summary>
public interface IExchangeProvider : IDisposable
{
    ExchangeId ExchangeId { get; }
    string Name { get; }

    IMarketDataProvider MarketData { get; }
    ITradingProvider Trading { get; }
}


