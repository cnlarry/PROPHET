using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Prophet.Client.Database.Repositories;
using Prophet.Client.Models;
using Prophet.Client.Services.Data.Collectors;

namespace Prophet.Client.Services.Market;

/// <summary>
/// 币安市场数据服务（统一封装REST API和WebSocket）
/// 提供给行情、回测、实盘等模块使用
/// </summary>
public class BinanceMarketDataService : IDisposable
{
    private readonly MarketDataRepository _repository;
    private readonly IBinanceExchangeGateway _gateway;
    private readonly Dictionary<string, BinanceWebSocketKlineCollector> _wsCollectors = new();
    private bool _disposed = false;
    private readonly bool _ownsGateway;
    
    // 币安期货REST API地址
    private const string BINANCE_API_BASE_URL = "https://fapi.binance.com/fapi/v1";
    
    /// <summary>
    /// K线数据接收事件
    /// </summary>
    public event EventHandler<KlineReceivedEventArgs>? KlineReceived;
    
    public BinanceMarketDataService(
        IBinanceExchangeGateway? gateway = null,
        MarketDataRepository? repository = null)
    {
        _repository = repository ?? new MarketDataRepository();
        _gateway = gateway ?? new BinanceExchangeGateway();
        _ownsGateway = gateway == null;
    }
    
    /// <summary>
    /// 获取最新的N条K线数据（通过REST API）
    /// </summary>
    /// <param name="symbol">交易对（如BTCUSDT）</param>
    /// <param name="interval">时间框架（如1m, 5m, 1h等）</param>
    /// <param name="limit">获取数量（默认1000，最大1500）</param>
    /// <returns>K线数据列表</returns>
    public async Task<List<Candlestick>> GetLatestKlinesAsync(
        string symbol, 
        string interval, 
        int limit = 1000)
    {
        try
        {
            Console.WriteLine($"📥 [BinanceMarketDataService] 获取最新K线: {symbol} {interval} limit={limit}");
            
            var candles = await _gateway.FetchKlinesAsync(
                symbol,
                interval,
                limit,
                cancellationToken: CancellationToken.None);
            
            Console.WriteLine($"✅ [BinanceMarketDataService] 获取K线成功: {candles.Count} 条");
            
            return candles.ToList();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ [BinanceMarketDataService] 获取最新K线失败: {ex.Message}");
            return new List<Candlestick>();
        }
    }
    
    /// <summary>
    /// 获取指定时间范围的K线数据（通过REST API）
    /// </summary>
    /// <param name="symbol">交易对</param>
    /// <param name="interval">时间框架</param>
    /// <param name="startTime">开始时间</param>
    /// <param name="endTime">结束时间</param>
    /// <param name="limit">获取数量（默认1000）</param>
    /// <returns>K线数据列表</returns>
    public async Task<List<Candlestick>> GetKlinesByTimeRangeAsync(
        string symbol,
        string interval,
        DateTime startTime,
        DateTime endTime,
        int limit = 1000)
    {
        try
        {
            Console.WriteLine($"📥 [BinanceMarketDataService] 获取时间范围K线: {symbol} {interval} {startTime:yyyy-MM-dd HH:mm:ss} -> {endTime:yyyy-MM-dd HH:mm:ss}");
            
            var candles = await _gateway.FetchKlinesAsync(
                symbol,
                interval,
                limit,
                startTime,
                endTime,
                CancellationToken.None);
            
            Console.WriteLine($"✅ [BinanceMarketDataService] 获取K线成功: {candles.Count} 条");
            
            return candles.ToList();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ [BinanceMarketDataService] 获取时间范围K线失败: {ex.Message}");
            return new List<Candlestick>();
        }
    }
    
    /// <summary>
    /// 保存K线数据到本地数据库（使用INSERT OR REPLACE更新）
    /// </summary>
    /// <param name="symbol">交易对</param>
    /// <param name="interval">时间框架</param>
    /// <param name="candles">K线数据</param>
    /// <returns>实际插入/更新的数量</returns>
    public async Task<int> SaveKlinesToDatabaseAsync(
        string symbol,
        string interval,
        List<Candlestick> candles)
    {
        if (candles == null || candles.Count == 0)
            return 0;
            
        try
        {
            // 使用 INSERT OR REPLACE 实现更新
            var count = await _repository.BulkInsertKlinesAsync(
                symbol.ToUpperInvariant(), 
                interval, 
                candles);
                
            return count;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ [BinanceMarketDataService] 保存K线到数据库失败: {ex.Message}");
            return 0;
        }
    }
    
    /// <summary>
    /// 启动WebSocket实时数据接收（行情模块专用，不执行补缺口逻辑）
    /// </summary>
    /// <param name="symbol">交易对</param>
    /// <param name="interval">时间框架</param>
    /// <returns>是否启动成功</returns>
    public async Task<bool> StartWebSocketAsync(string symbol, string interval)
    {
        try
        {
            var key = $"{symbol}_{interval}";
            
            // 如果已经存在，先停止
            if (_wsCollectors.ContainsKey(key))
            {
                await StopWebSocketAsync(symbol, interval);
            }
            
            Console.WriteLine($"🚀 [BinanceMarketDataService] 启动WebSocket: {symbol} {interval}");
            
            var collector = new BinanceWebSocketKlineCollector(
                _repository,
                _gateway,
                symbol.ToLowerInvariant(),
                interval,
                enableGapFill: false);
            
            // 订阅K线接收事件
            collector.KlineReceived += OnKlineReceivedFromWebSocket;
            
            // 启动WebSocket（由于没有HttpClient，不会执行补缺口逻辑）
            await collector.StartAsync();
            
            // 保存到字典
            _wsCollectors[key] = collector;
            
            Console.WriteLine($"✅ [BinanceMarketDataService] WebSocket启动成功: {symbol} {interval}");
            
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ [BinanceMarketDataService] 启动WebSocket失败: {ex.Message}");
            return false;
        }
    }
    
    /// <summary>
    /// 启动WebSocket实时数据接收（支持自动补缺口，供回测等模块使用）
    /// </summary>
    /// <param name="symbol">交易对</param>
    /// <param name="interval">时间框架</param>
    /// <returns>是否启动成功</returns>
    public async Task<bool> StartWebSocketWithDataFillAsync(string symbol, string interval)
    {
        try
        {
            var key = $"{symbol}_{interval}";
            
            // 如果已经存在，先停止
            if (_wsCollectors.ContainsKey(key))
            {
                await StopWebSocketAsync(symbol, interval);
            }
            
            Console.WriteLine($"🚀 [BinanceMarketDataService] 启动WebSocket（含补缺口）: {symbol} {interval}");
            
            var collector = new BinanceWebSocketKlineCollector(
                _repository,
                _gateway,
                symbol.ToLowerInvariant(),
                interval,
                enableGapFill: true);
            
            // 订阅K线接收事件
            collector.KlineReceived += OnKlineReceivedFromWebSocket;
            
            // 启动WebSocket（会执行补缺口逻辑）
            await collector.StartAsync();
            
            // 保存到字典
            _wsCollectors[key] = collector;
            
            Console.WriteLine($"✅ [BinanceMarketDataService] WebSocket启动成功（含补缺口）: {symbol} {interval}");
            
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ [BinanceMarketDataService] 启动WebSocket失败: {ex.Message}");
            return false;
        }
    }
    
    /// <summary>
    /// 停止WebSocket实时数据接收
    /// </summary>
    /// <param name="symbol">交易对</param>
    /// <param name="interval">时间框架</param>
    public async Task StopWebSocketAsync(string symbol, string interval)
    {
        try
        {
            var key = $"{symbol}_{interval}";
            
            if (_wsCollectors.TryGetValue(key, out var collector))
            {
                Console.WriteLine($"🛑 [BinanceMarketDataService] 停止WebSocket: {symbol} {interval}");
                
                collector.KlineReceived -= OnKlineReceivedFromWebSocket;
                await collector.StopAsync();
                collector.Dispose();
                
                _wsCollectors.Remove(key);
                
                Console.WriteLine($"✅ [BinanceMarketDataService] WebSocket已停止: {symbol} {interval}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ [BinanceMarketDataService] 停止WebSocket失败: {ex.Message}");
        }
    }
    
    /// <summary>
    /// 停止所有WebSocket连接
    /// </summary>
    public async Task StopAllWebSocketsAsync()
    {
        foreach (var kvp in _wsCollectors.ToList())
        {
            var collector = kvp.Value;
            collector.KlineReceived -= OnKlineReceivedFromWebSocket;
            await collector.StopAsync();
            collector.Dispose();
        }
        
        _wsCollectors.Clear();
        Console.WriteLine($"✅ [BinanceMarketDataService] 所有WebSocket已停止");
    }
    
    /// <summary>
    /// WebSocket接收到K线数据时的回调
    /// </summary>
    private void OnKlineReceivedFromWebSocket(object? sender, KlineReceivedEventArgs e)
    {
        // 转发事件给外部订阅者
        KlineReceived?.Invoke(this, e);
    }
    
    /// <summary>
    /// 从REST API响应解析K线数据
    /// 币安API返回格式：[开盘时间, 开盘价, 最高价, 最低价, 收盘价, 成交量, 收盘时间, 成交额, 交易笔数, 主动买入成交量, 主动买入成交额, 忽略字段]
    /// </summary>
    private Candlestick? ParseKlineFromApi(List<JsonElement> raw)
    {
        try
        {
            if (raw.Count < 6)
                return null;
            
            // 开盘时间（毫秒时间戳）
            var openTimeMs = raw[0].GetInt64();
            var openTime = DateTimeOffset.FromUnixTimeMilliseconds(openTimeMs).UtcDateTime;
            
            // OHLCV数据
            var open = raw[1].GetString() ?? "0";
            var high = raw[2].GetString() ?? "0";
            var low = raw[3].GetString() ?? "0";
            var close = raw[4].GetString() ?? "0";
            var volume = raw[5].GetString() ?? "0";
            
            // 可选字段（REST API返回完整数据）
            double? quoteVolume = null;
            long? tradeCount = null;
            double? takerBuyVolume = null;
            double? takerBuyQuoteVolume = null;
            
            if (raw.Count >= 8 && raw[7].ValueKind != JsonValueKind.Null)
            {
                quoteVolume = double.Parse(raw[7].GetString() ?? "0");
            }
            
            if (raw.Count >= 9 && raw[8].ValueKind != JsonValueKind.Null)
            {
                tradeCount = raw[8].GetInt64();
            }
            
            if (raw.Count >= 10 && raw[9].ValueKind != JsonValueKind.Null)
            {
                takerBuyVolume = double.Parse(raw[9].GetString() ?? "0");
            }
            
            if (raw.Count >= 11 && raw[10].ValueKind != JsonValueKind.Null)
            {
                takerBuyQuoteVolume = double.Parse(raw[10].GetString() ?? "0");
            }
            
            return new Candlestick
            {
                Time = openTime,
                Open = double.Parse(open),
                High = double.Parse(high),
                Low = double.Parse(low),
                Close = double.Parse(close),
                Volume = double.Parse(volume),
                QuoteVolume = quoteVolume,
                TradeCount = tradeCount,
                TakerBuyVolume = takerBuyVolume,
                TakerBuyQuoteVolume = takerBuyQuoteVolume
            };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ [BinanceMarketDataService] 解析REST API K线数据失败: {ex.Message}");
            return null;
        }
    }
    
    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;
            
        StopAllWebSocketsAsync().Wait(5000);
        if (_ownsGateway)
        {
            _gateway.Dispose();
        }
        
        _disposed = true;
    }
}
