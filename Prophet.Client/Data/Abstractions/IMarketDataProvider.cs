using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Prophet.Client.Data.Models;

namespace Prophet.Client.Data.Abstractions;

/// <summary>
/// 行情/衍生数据能力（K线、Ticker、订单簿等）。
/// 注意：在 Prophet 中，“数据采集器”会优先落库；上层读数据应优先走缓存/仓储，
/// 但 Provider 仍是数据源的统一抽象（HTTP/WS）。
/// </summary>
public interface IMarketDataProvider : IDisposable
{
    ExchangeId ExchangeId { get; }
    string Name { get; }

    Task<IReadOnlyList<Kline>> FetchKlinesAsync(
        string symbol,
        string interval,
        int limit = 1000,
        DateTime? startTime = null,
        DateTime? endTime = null,
        CancellationToken cancellationToken = default);

    Task<Ticker?> FetchTickerAsync(string symbol, CancellationToken cancellationToken = default);
    Task<OrderBook?> FetchOrderBookAsync(string symbol, int depth = 20, CancellationToken cancellationToken = default);
}


