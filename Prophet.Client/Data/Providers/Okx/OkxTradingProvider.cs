using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Prophet.Client.Data.Abstractions;
using Prophet.Client.Trading.Exchanges;
using Prophet.Client.Trading.Models;

namespace Prophet.Client.Data.Providers.Okx;

/// <summary>
/// OKX 交易 Provider（占位实现）
/// 后续将对接 OKX 私有交易 API（账户/持仓/下单等）
/// </summary>
public sealed class OkxTradingProvider : ITradingProvider
{
    public ExchangeId ExchangeId => ExchangeId.Okx;
    public string Name => "OKX";

#pragma warning disable CS0067 // 事件从未使用（占位实现，后续对接 OKX 私有交易 API 后会触发）
    public event EventHandler<OrderUpdateEventArgs>? OrderUpdated;
    public event EventHandler<PositionUpdateEventArgs>? PositionUpdated;
    public event EventHandler<ExchangeErrorEventArgs>? ErrorOccurred;
    public event EventHandler<ExchangeStatus>? StatusChanged;
#pragma warning restore CS0067

    public Task<bool> InitializeAsync(ExchangeConfig config) => ThrowNotSupported<bool>();
    public Task<bool> AuthenticateAsync(string apiKey, string apiSecret) => ThrowNotSupported<bool>();

    public Task<AccountInfo> GetAccountInfoAsync() => ThrowNotSupported<AccountInfo>();
    public Task<decimal> GetBalanceAsync(string asset = "USDT") => ThrowNotSupported<decimal>();
    public Task<List<Position>> GetPositionsAsync(string? symbol = null) => ThrowNotSupported<List<Position>>();

    public Task<OrderResult> PlaceMarketOrderAsync(MarketOrderRequest request) => ThrowNotSupported<OrderResult>();
    public Task<OrderResult> PlaceLimitOrderAsync(LimitOrderRequest request) => ThrowNotSupported<OrderResult>();
    public Task<bool> CancelOrderAsync(string orderId, string symbol) => ThrowNotSupported<bool>();
    public Task<OrderInfo> GetOrderAsync(string orderId, string symbol) => ThrowNotSupported<OrderInfo>();
    public Task<List<OrderInfo>> GetOpenOrdersAsync(string? symbol = null) => ThrowNotSupported<List<OrderInfo>>();

    public Task<bool> SetStopLossAsync(string symbol, decimal price, decimal quantity, bool isShortPosition = false) => ThrowNotSupported<bool>();
    public Task<bool> SetTakeProfitAsync(string symbol, decimal price, decimal quantity, bool isShortPosition = false) => ThrowNotSupported<bool>();

    public Task<bool> SetLeverageAsync(string symbol, int leverage) => ThrowNotSupported<bool>();
    public Task<int> GetLeverageAsync(string symbol) => ThrowNotSupported<int>();

    public void Dispose()
    {
    }

    private static Task<T> ThrowNotSupported<T>()
        => Task.FromException<T>(new NotSupportedException("OKX TradingProvider 尚未实现（当前仅支持 OKX 行情Ticker）。"));
}


