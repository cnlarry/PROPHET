using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Prophet.Client.Models;
using Prophet.Client.Trading.Models;

namespace Prophet.Client.Trading.Exchanges;

/// <summary>
/// 交易所统一接口
/// 用于抽象不同交易所的实现，支持币安、OKX、Bybit等
/// </summary>
public interface IExchange : IDisposable
{
    // ========== 基础信息 ==========
    
    /// <summary>
    /// 交易所名称
    /// </summary>
    string Name { get; }
    
    /// <summary>
    /// 连接状态
    /// </summary>
    ExchangeStatus Status { get; }
    
    /// <summary>
    /// 是否使用测试网
    /// </summary>
    bool IsTestnet { get; }
    
    // ========== 认证与初始化 ==========
    
    /// <summary>
    /// 初始化交易所连接
    /// </summary>
    Task<bool> InitializeAsync(ExchangeConfig config);
    
    /// <summary>
    /// 验证API密钥
    /// </summary>
    Task<bool> AuthenticateAsync(string apiKey, string apiSecret);
    
    // ========== 账户信息 ==========
    
    /// <summary>
    /// 获取账户信息
    /// </summary>
    Task<AccountInfo> GetAccountInfoAsync();
    
    /// <summary>
    /// 获取指定资产余额
    /// </summary>
    Task<decimal> GetBalanceAsync(string asset = "USDT");
    
    /// <summary>
    /// 获取持仓列表
    /// </summary>
    Task<List<Position>> GetPositionsAsync(string? symbol = null);
    
    // ========== 市场数据 ==========
    
    /// <summary>
    /// 获取历史K线数据
    /// </summary>
    Task<List<Candlestick>> GetHistoricalCandlesAsync(
        string symbol, 
        string timeframe, 
        DateTime start, 
        DateTime end,
        int limit = 1000);
    
    /// <summary>
    /// 获取最新K线
    /// </summary>
    Task<Candlestick?> GetLatestCandleAsync(string symbol, string timeframe);
    
    /// <summary>
    /// 获取订单簿
    /// </summary>
    Task<OrderBook> GetOrderBookAsync(string symbol, int depth = 20);
    
    /// <summary>
    /// 获取行情信息
    /// </summary>
    Task<Ticker> GetTickerAsync(string symbol);
    
    // ========== WebSocket 实时数据流 ==========
    
    /// <summary>
    /// 订阅K线数据流
    /// </summary>
    IAsyncEnumerable<Candlestick> SubscribeCandlesAsync(string symbol, string timeframe);
    
    /// <summary>
    /// 订阅账户更新流（余额、仓位等）
    /// </summary>
    IAsyncEnumerable<AccountUpdateEventArgs> SubscribeAccountUpdatesAsync();
    
    /// <summary>
    /// 取消订阅
    /// </summary>
    Task UnsubscribeAsync(string stream);
    
    // ========== 订单管理 ==========
    
    /// <summary>
    /// 下市价单
    /// </summary>
    Task<OrderResult> PlaceMarketOrderAsync(MarketOrderRequest request);
    
    /// <summary>
    /// 下限价单
    /// </summary>
    Task<OrderResult> PlaceLimitOrderAsync(LimitOrderRequest request);
    
    /// <summary>
    /// 撤销订单
    /// </summary>
    Task<bool> CancelOrderAsync(string orderId, string symbol);
    
    /// <summary>
    /// 查询订单
    /// </summary>
    Task<OrderInfo> GetOrderAsync(string orderId, string symbol);
    
    /// <summary>
    /// 获取未完成订单列表
    /// </summary>
    Task<List<OrderInfo>> GetOpenOrdersAsync(string? symbol = null);
    
    // ========== 止盈止损 ==========
    
    /// <summary>
    /// 设置止损价（空头持仓传 isShortPosition=true，此时用 BUY 平仓；多头用 SELL）
    /// </summary>
    Task<bool> SetStopLossAsync(string symbol, decimal price, decimal quantity, bool isShortPosition = false);
    
    /// <summary>
    /// 设置止盈价（方向规则同止损）
    /// </summary>
    Task<bool> SetTakeProfitAsync(string symbol, decimal price, decimal quantity, bool isShortPosition = false);
    
    // ========== 杠杆设置 ==========
    
    /// <summary>
    /// 设置杠杆倍数
    /// </summary>
    Task<bool> SetLeverageAsync(string symbol, int leverage);
    
    /// <summary>
    /// 获取当前杠杆倍数
    /// </summary>
    Task<int> GetLeverageAsync(string symbol);
    
    // ========== 事件通知 ==========
    
    /// <summary>
    /// 订单更新事件
    /// </summary>
    event EventHandler<OrderUpdateEventArgs>? OrderUpdated;
    
    /// <summary>
    /// 仓位更新事件
    /// </summary>
    event EventHandler<PositionUpdateEventArgs>? PositionUpdated;
    
    /// <summary>
    /// 错误事件
    /// </summary>
    event EventHandler<ExchangeErrorEventArgs>? ErrorOccurred;
    
    /// <summary>
    /// 连接状态变化事件
    /// </summary>
    event EventHandler<ExchangeStatus>? StatusChanged;
}

