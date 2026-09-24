using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Prophet.Client.Core;
using Prophet.Client.Models;
using Prophet.Client.Services.Network;
using Prophet.Client.Services.Settings;
using Prophet.Client.Trading.Exchanges.Binance.Models;
using Prophet.Client.Trading.Models;

namespace Prophet.Client.Trading.Exchanges.Binance;

/// <summary>
/// 币安交易所适配器
/// 实现IExchange接口，整合REST API和WebSocket功能
/// </summary>
public class BinanceExchange : IExchange
{
    private readonly BinanceRestClient _restClient;
    private readonly BinanceWebSocketClient _wsClient;
    private ExchangeConfig _config = new();
    private ExchangeStatus _status = ExchangeStatus.Disconnected;
    private string _listenKey = string.Empty;
    private Timer? _listenKeyTimer;
    private bool _disposed;
    
    // 事件
#pragma warning disable CS0067 // 事件从未使用（预留给实时订单更新功能）
    public event EventHandler<OrderUpdateEventArgs>? OrderUpdated;
    public event EventHandler<PositionUpdateEventArgs>? PositionUpdated;
#pragma warning restore CS0067
    public event EventHandler<ExchangeErrorEventArgs>? ErrorOccurred;
    public event EventHandler<ExchangeStatus>? StatusChanged;
    
    public string Name => "Binance";
    public ExchangeStatus Status => _status;
    public bool IsTestnet => _config.UseTestnet;
    
    public BinanceExchange()
    {
        // 创建支持全局代理配置的 HttpClient
        var httpClient = CreateHttpClientWithProxy();
        _restClient = new BinanceRestClient(httpClient);
        
        // WebSocket URL会在Initialize时设置
        _wsClient = new BinanceWebSocketClient("");
        
        // 输出代理配置日志
        var settings = ServiceContainer.GetService<AppSettingsService>().Settings;
        Console.WriteLine($"币安交易所适配器初始化:");
        Console.WriteLine($"   代理配置: EnableProxy={settings.EnableProxy}, ProxyAddress={settings.ProxyAddress ?? "系统默认"}");
    }
    
    // ========== 初始化 ==========
    
    public async Task<bool> InitializeAsync(ExchangeConfig config)
    {
        try
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            
            SetStatus(ExchangeStatus.Connecting);
            
            // 设置REST API端点
            var baseUrl = config.UseTestnet
                ? "https://testnet.binancefuture.com"
                : "https://fapi.binance.com";
            
            if (!string.IsNullOrEmpty(config.BaseUrl))
            {
                baseUrl = config.BaseUrl;
            }
            
            _restClient.Initialize(baseUrl, config.ApiKey, config.ApiSecret);
            
            // 设置 WebSocket 组合流端点（市场流 + 用户数据流共用，各自 SUBSCRIBE）
            // 主网: wss://fstream.binance.com/stream  测试网: wss://stream.binancefuture.com/stream
            var wsBase = config.UseTestnet
                ? "wss://stream.binancefuture.com/stream"
                : "wss://fstream.binance.com/stream";
            _wsClient.SetBaseUrl(wsBase);
            
            // 测试API连接
            var accountInfo = await _restClient.GetAccountInfoAsync();
            
            Console.WriteLine($"✅ 币安交易所初始化成功");
            Console.WriteLine($"   模式: {(config.UseTestnet ? "测试网" : "主网")}");
            Console.WriteLine($"   总余额: {accountInfo.TotalWalletBalance} USDT");
            
            SetStatus(ExchangeStatus.Connected);
            
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ 币安交易所初始化失败: {ex.Message}");
            SetStatus(ExchangeStatus.Error);
            ErrorOccurred?.Invoke(this, new ExchangeErrorEventArgs
            {
                ErrorMessage = ex.Message,
                Exception = ex
            });
            return false;
        }
    }
    
    public async Task<bool> AuthenticateAsync(string apiKey, string apiSecret)
    {
        try
        {
            _config.ApiKey = apiKey;
            _config.ApiSecret = apiSecret;
            
            _restClient.Initialize(
                _config.UseTestnet ? "https://testnet.binancefuture.com" : "https://fapi.binance.com",
                apiKey,
                apiSecret);
            
            // 测试认证
            var accountInfo = await _restClient.GetAccountInfoAsync();
            
            SetStatus(ExchangeStatus.Authenticated);
            Console.WriteLine("✅ API密钥认证成功");
            
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ API密钥认证失败: {ex.Message}");
            SetStatus(ExchangeStatus.Error);
            ErrorOccurred?.Invoke(this, new ExchangeErrorEventArgs
            {
                ErrorMessage = ex.Message,
                Exception = ex
            });
            return false;
        }
    }
    
    // ========== 账户信息 ==========
    
    public async Task<AccountInfo> GetAccountInfoAsync()
    {
        var binanceAccount = await _restClient.GetAccountInfoAsync();
        
        return new AccountInfo
        {
            TotalBalance = decimal.Parse(binanceAccount.TotalWalletBalance),
            AvailableBalance = decimal.Parse(binanceAccount.AvailableBalance),
            TotalMargin = decimal.Parse(binanceAccount.TotalMarginBalance),
            TotalUnrealizedPnl = decimal.Parse(binanceAccount.TotalUnrealizedProfit),
            Assets = binanceAccount.Assets.Select(a => new AssetBalance
            {
                Asset = a.Asset,
                Balance = decimal.Parse(a.WalletBalance),
                AvailableBalance = decimal.Parse(a.AvailableBalance)
            }).ToList()
        };
    }
    
    public async Task<decimal> GetBalanceAsync(string asset = "USDT")
    {
        var accountInfo = await _restClient.GetAccountInfoAsync();
        var assetInfo = accountInfo.Assets.FirstOrDefault(a => 
            a.Asset.Equals(asset, StringComparison.OrdinalIgnoreCase));
        
        return assetInfo != null ? decimal.Parse(assetInfo.AvailableBalance) : 0;
    }
    
    public async Task<List<Position>> GetPositionsAsync(string? symbol = null)
    {
        var binancePositions = await _restClient.GetPositionsAsync(symbol);
        
        return binancePositions.Select(p => new Position
        {
            Symbol = p.Symbol,
            Side = decimal.Parse(p.PositionAmt) > 0 ? PositionSide.Long : PositionSide.Short,
            Quantity = Math.Abs(decimal.Parse(p.PositionAmt)),
            EntryPrice = decimal.Parse(p.EntryPrice),
            MarkPrice = decimal.Parse(p.MarkPrice),
            LiquidationPrice = decimal.Parse(p.LiquidationPrice),
            UnrealizedPnl = decimal.Parse(p.UnRealizedProfit),
            Leverage = decimal.Parse(p.Leverage),
            UpdateTime = DateTimeOffset.FromUnixTimeMilliseconds(p.UpdateTime).UtcDateTime
        }).ToList();
    }
    
    // ========== 市场数据 ==========
    
    public async Task<List<Candlestick>> GetHistoricalCandlesAsync(
        string symbol, 
        string timeframe, 
        DateTime start, 
        DateTime end, 
        int limit = 1000)
    {
        var startTime = new DateTimeOffset(start).ToUnixTimeMilliseconds();
        var endTime = new DateTimeOffset(end).ToUnixTimeMilliseconds();
        
        var klines = await _restClient.GetKlinesAsync(
            symbol, 
            timeframe, 
            startTime, 
            endTime, 
            limit);
        
        return klines.Select(k => new Candlestick
        {
            Time = DateTimeOffset.FromUnixTimeMilliseconds(
                Convert.ToInt64(k[0])).UtcDateTime,
            Open = (double)decimal.Parse(k[1].ToString() ?? "0"),
            High = (double)decimal.Parse(k[2].ToString() ?? "0"),
            Low = (double)decimal.Parse(k[3].ToString() ?? "0"),
            Close = (double)decimal.Parse(k[4].ToString() ?? "0"),
            Volume = (double)decimal.Parse(k[5].ToString() ?? "0")
        }).ToList();
    }
    
    public async Task<Candlestick?> GetLatestCandleAsync(string symbol, string timeframe)
    {
        var candles = await GetHistoricalCandlesAsync(
            symbol, 
            timeframe, 
            DateTime.UtcNow.AddHours(-1), 
            DateTime.UtcNow, 
            1);
        
        return candles.LastOrDefault();
    }
    
    public async Task<OrderBook> GetOrderBookAsync(string symbol, int depth = 20)
    {
        var binanceOrderBook = await _restClient.GetOrderBookAsync(symbol, depth);
        
        return new OrderBook
        {
            Symbol = symbol,
            Bids = binanceOrderBook.Bids.Select(b => new OrderBookLevel
            {
                Price = decimal.Parse(b[0]),
                Quantity = decimal.Parse(b[1])
            }).ToList(),
            Asks = binanceOrderBook.Asks.Select(a => new OrderBookLevel
            {
                Price = decimal.Parse(a[0]),
                Quantity = decimal.Parse(a[1])
            }).ToList(),
            UpdateId = binanceOrderBook.LastUpdateId,
            UpdateTime = DateTime.UtcNow
        };
    }
    
    public async Task<Ticker> GetTickerAsync(string symbol)
    {
        var binanceTicker = await _restClient.GetTickerAsync(symbol);
        
        return new Ticker
        {
            Symbol = symbol,
            LastPrice = decimal.Parse(binanceTicker.LastPrice),
            HighPrice = decimal.Parse(binanceTicker.HighPrice),
            LowPrice = decimal.Parse(binanceTicker.LowPrice),
            Volume = decimal.Parse(binanceTicker.Volume),
            QuoteVolume = decimal.Parse(binanceTicker.QuoteVolume),
            PriceChange = decimal.Parse(binanceTicker.PriceChange),
            PriceChangePercent = decimal.Parse(binanceTicker.PriceChangePercent),
            UpdateTime = DateTime.UtcNow
        };
    }
    
    // ========== WebSocket数据流 ==========
    
    public async IAsyncEnumerable<Candlestick> SubscribeCandlesAsync(
        string symbol, 
        string timeframe)
    {
        await foreach (var candle in _wsClient.SubscribeKlineAsync(symbol, timeframe))
        {
            yield return candle;
        }
    }
    
    public async IAsyncEnumerable<AccountUpdateEventArgs> SubscribeAccountUpdatesAsync()
    {
        // 创建并保持 Listen Key
        _listenKey = await _restClient.CreateListenKeyAsync();
        if (string.IsNullOrWhiteSpace(_listenKey))
        {
            throw new InvalidOperationException("创建 Listen Key 失败");
        }
        
        Console.WriteLine($"✅ 创建 Listen Key 成功: {_listenKey}");
        
        // 订阅用户数据流（在组合流上 SUBSCRIBE listenKey）
        await _wsClient.SubscribeUserDataStreamAsync(_listenKey);
        
        // 设置定时器保持 Listen Key 活跃（币安要求每 60 分钟内至少一次，这里每 30 分钟）
        _listenKeyTimer = new Timer(async _ =>
        {
            try
            {
                await _restClient.KeepAliveListenKeyAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ 刷新 Listen Key 失败: {ex.Message}");
            }
        }, null, TimeSpan.FromMinutes(30), TimeSpan.FromMinutes(30));
        
        // 消息队列：将 WebSocket 原始消息转换为账户更新事件
        var queue = new System.Collections.Concurrent.ConcurrentQueue<AccountUpdateEventArgs>();
        var messageReceived = new SemaphoreSlim(0);
        
        void OnAccountMsg(object? sender, string raw)
        {
            try
            {
                var parsed = ParseAccountUpdate(raw);
                if (parsed != null)
                {
                    queue.Enqueue(parsed);
                    messageReceived.Release();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ 解析账户更新消息失败: {ex.Message}");
            }
        }
        
        void OnOrderMsg(object? sender, string raw)
        {
            try
            {
                var orderInfo = ParseOrderTradeUpdate(raw);
                if (orderInfo != null)
                {
                    OrderUpdated?.Invoke(this, new OrderUpdateEventArgs { Order = orderInfo });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ 解析订单更新消息失败: {ex.Message}");
            }
        }
        
        _wsClient.OnAccountUpdate += OnAccountMsg;
        _wsClient.OnOrderTradeUpdate += OnOrderMsg;
        
        try
        {
            while (true)
            {
                await messageReceived.WaitAsync();
                while (queue.TryDequeue(out var update))
                {
                    // 同时触发仓位更新事件（如果有仓位变化）
                    foreach (var pos in update.Positions)
                    {
                        PositionUpdated?.Invoke(this, new PositionUpdateEventArgs { Position = pos });
                    }
                    yield return update;
                }
            }
        }
        finally
        {
            _wsClient.OnAccountUpdate -= OnAccountMsg;
            _wsClient.OnOrderTradeUpdate -= OnOrderMsg;
        }
    }
    
    /// <summary>
    /// 解析 ACCOUNT_UPDATE 消息为账户更新事件
    /// </summary>
    private AccountUpdateEventArgs? ParseAccountUpdate(string raw)
    {
        try
        {
            var msg = System.Text.Json.JsonSerializer.Deserialize<BinanceAccountUpdateMessage>(raw);
            if (msg == null) return null;
            
            var args = new AccountUpdateEventArgs
            {
                UpdateType = msg.EventType,
                Account = new AccountInfo
                {
                    TotalBalance = msg.UpdateData.Balances
                        .Where(b => b.Asset.Equals("USDT", StringComparison.OrdinalIgnoreCase))
                        .Sum(b => decimal.TryParse(b.WalletBalance, out var v) ? v : 0m),
                    AvailableBalance = msg.UpdateData.Balances
                        .Where(b => b.Asset.Equals("USDT", StringComparison.OrdinalIgnoreCase))
                        .Sum(b => decimal.TryParse(b.CrossWalletBalance, out var v) ? v : 0m),
                    Assets = msg.UpdateData.Balances.Select(b => new AssetBalance
                    {
                        Asset = b.Asset,
                        Balance = decimal.TryParse(b.WalletBalance, out var wb) ? wb : 0m,
                        AvailableBalance = decimal.TryParse(b.CrossWalletBalance, out var cw) ? cw : 0m
                    }).ToList()
                }
            };
            
            // 记录仓位变化到事件参数（供上层触发 PositionUpdated）
            foreach (var p in msg.UpdateData.Positions
                .Where(p => decimal.TryParse(p.PositionAmt, out var pa) && pa != 0))
            {
                var side = decimal.Parse(p.PositionAmt) > 0 ? PositionSide.Long : PositionSide.Short;
                args.Positions.Add(new Position
                {
                    Symbol = p.Symbol,
                    Side = side,
                    Quantity = Math.Abs(decimal.Parse(p.PositionAmt)),
                    EntryPrice = decimal.Parse(p.EntryPrice),
                    UnrealizedPnl = decimal.Parse(p.UnrealizedProfit)
                });
            }
            
            return args;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ 解析账户更新消息失败: {ex.Message}");
            return null;
        }
    }
    
    /// <summary>
    /// 解析 ORDER_TRADE_UPDATE 消息为订单信息
    /// </summary>
    private OrderInfo? ParseOrderTradeUpdate(string raw)
    {
        try
        {
            var msg = System.Text.Json.JsonSerializer.Deserialize<BinanceOrderTradeUpdateMessage>(raw);
            if (msg?.Order == null) return null;
            
            var o = msg.Order;
            return new OrderInfo
            {
                OrderId = o.OrderId.ToString(),
                ClientOrderId = o.ClientOrderId,
                Symbol = o.Symbol,
                Side = Enum.TryParse<OrderSide>(o.Side, true, out var side) ? side : OrderSide.BUY,
                Type = Enum.TryParse<OrderType>(o.OrderType, true, out var type) ? type : OrderType.MARKET,
                Status = o.CurrentStatus,
                Price = decimal.Parse(o.Price),
                Quantity = decimal.Parse(o.OriginalQuantity),
                FilledQuantity = decimal.Parse(o.AccumulatedFilledQuantity),
                AvgPrice = decimal.Parse(o.AveragePrice)
            };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ 解析订单更新消息失败: {ex.Message}");
            return null;
        }
    }
    
    public async Task UnsubscribeAsync(string stream)
    {
        // WebSocket取消订阅逻辑
        await Task.CompletedTask;
    }
    
    // ========== 订单管理 ==========
    
    public async Task<OrderResult> PlaceMarketOrderAsync(MarketOrderRequest request)
    {
        try
        {
            var binanceOrder = await _restClient.PlaceOrderAsync(
                request.Symbol,
                request.Side.ToString(),
                "MARKET",
                quantity: request.Quantity,
                clientOrderId: request.ClientOrderId);
            
            return new OrderResult
            {
                Success = true,
                OrderId = binanceOrder.OrderId.ToString(),
                ClientOrderId = binanceOrder.ClientOrderId,
                FilledPrice = decimal.Parse(binanceOrder.AvgPrice),
                FilledQuantity = decimal.Parse(binanceOrder.ExecutedQty)
            };
        }
        catch (Exception ex)
        {
            return new OrderResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }
    
    public async Task<OrderResult> PlaceLimitOrderAsync(LimitOrderRequest request)
    {
        try
        {
            var binanceOrder = await _restClient.PlaceOrderAsync(
                request.Symbol,
                request.Side.ToString(),
                "LIMIT",
                quantity: request.Quantity,
                price: request.Price,
                timeInForce: request.TimeInForce.ToString(),
                clientOrderId: request.ClientOrderId);
            
            return new OrderResult
            {
                Success = true,
                OrderId = binanceOrder.OrderId.ToString(),
                ClientOrderId = binanceOrder.ClientOrderId,
                FilledPrice = decimal.Parse(binanceOrder.AvgPrice),
                FilledQuantity = decimal.Parse(binanceOrder.ExecutedQty)
            };
        }
        catch (Exception ex)
        {
            return new OrderResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }
    
    public async Task<bool> CancelOrderAsync(string orderId, string symbol)
    {
        try
        {
            await _restClient.CancelOrderAsync(symbol, long.Parse(orderId));
            return true;
        }
        catch
        {
            return false;
        }
    }
    
    public async Task<OrderInfo> GetOrderAsync(string orderId, string symbol)
    {
        var binanceOrder = await _restClient.GetOrderAsync(symbol, long.Parse(orderId));
        
        return new OrderInfo
        {
            OrderId = binanceOrder.OrderId.ToString(),
            ClientOrderId = binanceOrder.ClientOrderId,
            Symbol = binanceOrder.Symbol,
            Side = Enum.Parse<OrderSide>(binanceOrder.Side),
            Type = Enum.Parse<OrderType>(binanceOrder.Type),
            Status = binanceOrder.Status,
            Price = decimal.Parse(binanceOrder.Price),
            Quantity = decimal.Parse(binanceOrder.OrigQty),
            FilledQuantity = decimal.Parse(binanceOrder.ExecutedQty),
            AvgPrice = decimal.Parse(binanceOrder.AvgPrice)
        };
    }
    
    public async Task<List<OrderInfo>> GetOpenOrdersAsync(string? symbol = null)
    {
        var binanceOrders = await _restClient.GetOpenOrdersAsync(symbol);
        
        return binanceOrders.Select(o => new OrderInfo
        {
            OrderId = o.OrderId.ToString(),
            ClientOrderId = o.ClientOrderId,
            Symbol = o.Symbol,
            Side = Enum.Parse<OrderSide>(o.Side),
            Type = Enum.Parse<OrderType>(o.Type),
            Status = o.Status,
            Price = decimal.Parse(o.Price),
            Quantity = decimal.Parse(o.OrigQty),
            FilledQuantity = decimal.Parse(o.ExecutedQty),
            AvgPrice = decimal.Parse(o.AvgPrice)
        }).ToList();
    }
    
    // ========== 止盈止损 ==========
    
    public async Task<bool> SetStopLossAsync(string symbol, decimal price, decimal quantity, bool isShortPosition = false)
    {
        try
        {
            await _restClient.PlaceOrderAsync(
                symbol,
                isShortPosition ? "BUY" : "SELL", // 空头止损用BUY平仓，多头用SELL
                "STOP_MARKET",
                quantity: quantity,
                price: price);
            return true;
        }
        catch
        {
            return false;
        }
    }
    
    public async Task<bool> SetTakeProfitAsync(string symbol, decimal price, decimal quantity, bool isShortPosition = false)
    {
        try
        {
            await _restClient.PlaceOrderAsync(
                symbol,
                isShortPosition ? "BUY" : "SELL", // 空头止盈用BUY平仓，多头用SELL
                "TAKE_PROFIT_MARKET",
                quantity: quantity,
                price: price);
            return true;
        }
        catch
        {
            return false;
        }
    }
    
    // ========== 杠杆设置 ==========
    
    public async Task<bool> SetLeverageAsync(string symbol, int leverage)
    {
        return await _restClient.SetLeverageAsync(symbol, leverage);
    }
    
    public async Task<int> GetLeverageAsync(string symbol)
    {
        var positions = await _restClient.GetPositionsAsync(symbol);
        var position = positions.FirstOrDefault();
        
        return position != null ? (int)decimal.Parse(position.Leverage) : 1;
    }
    
    // ========== 私有方法 ==========
    
    private void SetStatus(ExchangeStatus status)
    {
        if (_status != status)
        {
            _status = status;
            StatusChanged?.Invoke(this, status);
            Console.WriteLine($"📊 交易所状态变更: {status}");
        }
    }
    
    /// <summary>
    /// 创建支持全局代理配置的 HttpClient
    /// </summary>
    private static HttpClient CreateHttpClientWithProxy()
    {
        // ✅ 使用ProxyManagementService统一管理代理
        var proxyService = ServiceContainer.GetService<ProxyManagementService>();
        var handler = proxyService.CreateHttpClientHandler();
        
        var settings = ServiceContainer.GetService<AppSettingsService>().Settings;
        var client = new HttpClient(handler, disposeHandler: true)
        {
            Timeout = TimeSpan.FromSeconds(settings.EnableProxy ? 60 : 30) // 使用代理时增加超时时间
        };
        
        return client;
    }
    
    public void Dispose()
    {
        if (!_disposed)
        {
            _listenKeyTimer?.Dispose();
            _restClient?.Dispose();
            _wsClient?.Dispose();
            _disposed = true;
        }
    }
}

