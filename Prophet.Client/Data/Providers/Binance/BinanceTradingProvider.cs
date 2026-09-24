using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Prophet.Client.Data.Abstractions;
using Prophet.Client.Models;
using Prophet.Client.Trading.Exchanges;
using Prophet.Client.Trading.Exchanges.Binance;
using Prophet.Client.Trading.Models;

namespace Prophet.Client.Data.Providers.Binance;

/// <summary>
/// Binance 交易 Provider（适配现有 <see cref="BinanceExchange"/>）
/// </summary>
public sealed class BinanceTradingProvider : ITradingProvider
{
    private readonly BinanceExchange _exchange;
    private bool _disposed;

    public BinanceTradingProvider(BinanceExchange? exchange = null)
    {
        _exchange = exchange ?? new BinanceExchange();

        // 事件直通
        _exchange.OrderUpdated += (_, e) => OrderUpdated?.Invoke(this, e);
        _exchange.PositionUpdated += (_, e) => PositionUpdated?.Invoke(this, e);
        _exchange.ErrorOccurred += (_, e) => ErrorOccurred?.Invoke(this, e);
        _exchange.StatusChanged += (_, s) => StatusChanged?.Invoke(this, s);
    }

    public ExchangeId ExchangeId => ExchangeId.Binance;
    public string Name => "Binance";

    public Task<bool> InitializeAsync(ExchangeConfig config) => _exchange.InitializeAsync(config);

    public Task<bool> AuthenticateAsync(string apiKey, string apiSecret) => _exchange.AuthenticateAsync(apiKey, apiSecret);

    public Task<AccountInfo> GetAccountInfoAsync() => _exchange.GetAccountInfoAsync();

    public Task<decimal> GetBalanceAsync(string asset = "USDT") => _exchange.GetBalanceAsync(asset);

    public Task<List<Position>> GetPositionsAsync(string? symbol = null) => _exchange.GetPositionsAsync(symbol);

    public Task<OrderResult> PlaceMarketOrderAsync(MarketOrderRequest request) => _exchange.PlaceMarketOrderAsync(request);

    public Task<OrderResult> PlaceLimitOrderAsync(LimitOrderRequest request) => _exchange.PlaceLimitOrderAsync(request);

    public Task<bool> CancelOrderAsync(string orderId, string symbol) => _exchange.CancelOrderAsync(orderId, symbol);

    public Task<OrderInfo> GetOrderAsync(string orderId, string symbol) => _exchange.GetOrderAsync(orderId, symbol);

    public Task<List<OrderInfo>> GetOpenOrdersAsync(string? symbol = null) => _exchange.GetOpenOrdersAsync(symbol);

    public Task<bool> SetStopLossAsync(string symbol, decimal price, decimal quantity, bool isShortPosition = false) => _exchange.SetStopLossAsync(symbol, price, quantity, isShortPosition);

    public Task<bool> SetTakeProfitAsync(string symbol, decimal price, decimal quantity, bool isShortPosition = false) => _exchange.SetTakeProfitAsync(symbol, price, quantity, isShortPosition);

    public Task<bool> SetLeverageAsync(string symbol, int leverage) => _exchange.SetLeverageAsync(symbol, leverage);

    public Task<int> GetLeverageAsync(string symbol) => _exchange.GetLeverageAsync(symbol);

    public event EventHandler<OrderUpdateEventArgs>? OrderUpdated;
    public event EventHandler<PositionUpdateEventArgs>? PositionUpdated;
    public event EventHandler<ExchangeErrorEventArgs>? ErrorOccurred;
    public event EventHandler<ExchangeStatus>? StatusChanged;

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _exchange.Dispose();
        _disposed = true;
    }
}


