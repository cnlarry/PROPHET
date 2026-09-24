using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Prophet.Client.Models;
using Prophet.Client.Trading.Models;

namespace Prophet.Client.Data.Abstractions;

/// <summary>
/// 交易能力（账户/持仓/下单等）。属于 Data Plane：对上层统一，对下层由各交易所实现。
/// </summary>
public interface ITradingProvider : IDisposable
{
    ExchangeId ExchangeId { get; }
    string Name { get; }

    Task<bool> InitializeAsync(ExchangeConfig config);
    Task<bool> AuthenticateAsync(string apiKey, string apiSecret);

    Task<AccountInfo> GetAccountInfoAsync();
    Task<decimal> GetBalanceAsync(string asset = "USDT");
    Task<List<Position>> GetPositionsAsync(string? symbol = null);

    Task<OrderResult> PlaceMarketOrderAsync(MarketOrderRequest request);
    Task<OrderResult> PlaceLimitOrderAsync(LimitOrderRequest request);
    Task<bool> CancelOrderAsync(string orderId, string symbol);
    Task<OrderInfo> GetOrderAsync(string orderId, string symbol);
    Task<List<OrderInfo>> GetOpenOrdersAsync(string? symbol = null);

    Task<bool> SetStopLossAsync(string symbol, decimal price, decimal quantity, bool isShortPosition = false);
    Task<bool> SetTakeProfitAsync(string symbol, decimal price, decimal quantity, bool isShortPosition = false);

    Task<bool> SetLeverageAsync(string symbol, int leverage);
    Task<int> GetLeverageAsync(string symbol);

    event EventHandler<OrderUpdateEventArgs>? OrderUpdated;
    event EventHandler<PositionUpdateEventArgs>? PositionUpdated;
    event EventHandler<ExchangeErrorEventArgs>? ErrorOccurred;
    event EventHandler<Prophet.Client.Trading.Exchanges.ExchangeStatus>? StatusChanged;
}


