using System;
using System.Threading.Tasks;
using Prophet.Client.Trading.Exchanges;
using Prophet.Client.Trading.Exchanges.Binance;
using Prophet.Client.Trading.Models;

namespace Prophet.Client.Trading.Examples;

/// <summary>
/// 交易所使用示例
/// 演示如何使用BinanceExchange进行实盘交易操作
/// </summary>
public class ExchangeUsageExample
{
    /// <summary>
    /// 基础示例：初始化交易所并获取账户信息
    /// </summary>
    public static async Task BasicExample()
    {
        // 1. 创建币安交易所实例
        using var exchange = new BinanceExchange();
        
        // 2. 配置交易所
        var config = new ExchangeConfig
        {
            ExchangeName = "Binance",
            ApiKey = "your_api_key_here",
            ApiSecret = "your_api_secret_here",
            UseTestnet = true  // 建议先在测试网测试
        };
        
        // 3. 初始化连接
        var success = await exchange.InitializeAsync(config);
        if (!success)
        {
            Console.WriteLine("交易所初始化失败");
            return;
        }
        
        // 4. 获取账户信息
        var accountInfo = await exchange.GetAccountInfoAsync();
        Console.WriteLine($"总余额: {accountInfo.TotalBalance} USDT");
        Console.WriteLine($"可用余额: {accountInfo.AvailableBalance} USDT");
        
        // 5. 获取持仓
        var positions = await exchange.GetPositionsAsync();
        foreach (var position in positions)
        {
            Console.WriteLine($"持仓: {position.Symbol} {position.Side} {position.Quantity} @ {position.EntryPrice}");
        }
    }
    
    /// <summary>
    /// 市场数据示例：获取K线和订单簿
    /// </summary>
    public static async Task MarketDataExample(IExchange exchange)
    {
        // 获取历史K线
        var candles = await exchange.GetHistoricalCandlesAsync(
            "BTCUSDT", 
            "5m", 
            DateTime.UtcNow.AddHours(-1), 
            DateTime.UtcNow);
        
        Console.WriteLine($"获取到 {candles.Count} 条K线数据");
        
        // 获取订单簿
        var orderBook = await exchange.GetOrderBookAsync("BTCUSDT", 10);
        Console.WriteLine($"最佳买价: {orderBook.BestBid}");
        Console.WriteLine($"最佳卖价: {orderBook.BestAsk}");
        Console.WriteLine($"买卖价差: {orderBook.Spread}");
        
        // 获取行情信息
        var ticker = await exchange.GetTickerAsync("BTCUSDT");
        Console.WriteLine($"最新价: {ticker.LastPrice}");
        Console.WriteLine($"24h涨跌: {ticker.PriceChangePercent}%");
    }
    
    /// <summary>
    /// 订单操作示例：下单和查询
    /// </summary>
    public static async Task OrderExample(IExchange exchange)
    {
        // 下市价单
        var marketOrder = new MarketOrderRequest
        {
            Symbol = "BTCUSDT",
            Side = OrderSide.BUY,
            Quantity = 0.001m
        };
        
        var result = await exchange.PlaceMarketOrderAsync(marketOrder);
        if (result.Success)
        {
            Console.WriteLine($"订单成功: {result.OrderId}");
            Console.WriteLine($"成交价: {result.FilledPrice}");
            Console.WriteLine($"成交量: {result.FilledQuantity}");
        }
        
        // 下限价单
        var limitOrder = new LimitOrderRequest
        {
            Symbol = "BTCUSDT",
            Side = OrderSide.BUY,
            Quantity = 0.001m,
            Price = 30000m,
            TimeInForce = TimeInForce.GTC
        };
        
        var limitResult = await exchange.PlaceLimitOrderAsync(limitOrder);
        if (limitResult.Success)
        {
            Console.WriteLine($"限价单已挂: {limitResult.OrderId}");
        }
        
        // 查询未完成订单
        var openOrders = await exchange.GetOpenOrdersAsync("BTCUSDT");
        Console.WriteLine($"未完成订单数: {openOrders.Count}");
    }
    
    /// <summary>
    /// WebSocket实时数据示例
    /// </summary>
    public static async Task WebSocketExample(IExchange exchange)
    {
        Console.WriteLine("开始订阅K线数据流...");
        
        // 订阅K线数据
        await foreach (var candle in exchange.SubscribeCandlesAsync("BTCUSDT", "1m"))
        {
            if (candle.IsClosed)
            {
                Console.WriteLine($"新K线: {candle.Time:HH:mm} " +
                    $"O:{candle.Open} H:{candle.High} L:{candle.Low} C:{candle.Close}");
            }
        }
    }
    
    /// <summary>
    /// 完整交易流程示例
    /// </summary>
    public static async Task CompleteTradeExample()
    {
        using var exchange = new BinanceExchange();
        
        // 初始化
        var config = new ExchangeConfig
        {
            ApiKey = "your_api_key",
            ApiSecret = "your_api_secret",
            UseTestnet = true
        };
        
        await exchange.InitializeAsync(config);
        
        // 设置杠杆
        await exchange.SetLeverageAsync("BTCUSDT", 10);
        
        // 获取最新价格
        var ticker = await exchange.GetTickerAsync("BTCUSDT");
        var currentPrice = ticker.LastPrice;
        
        // 计算止损止盈
        var stopLoss = currentPrice * 0.98m;  // 2%止损
        var takeProfit = currentPrice * 1.04m; // 4%止盈
        
        // 下市价单
        var order = new MarketOrderRequest
        {
            Symbol = "BTCUSDT",
            Side = OrderSide.BUY,
            Quantity = 0.001m
        };
        
        var result = await exchange.PlaceMarketOrderAsync(order);
        
        if (result.Success)
        {
            Console.WriteLine($"开仓成功: {result.OrderId}");
            
            // 设置止损止盈
            await exchange.SetStopLossAsync("BTCUSDT", stopLoss, 0.001m);
            await exchange.SetTakeProfitAsync("BTCUSDT", takeProfit, 0.001m);
            
            Console.WriteLine($"止损价: {stopLoss}");
            Console.WriteLine($"止盈价: {takeProfit}");
        }
    }
}

