using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Prophet.Client.Models;
using Prophet.Client.Trading.Exchanges.Binance.Models;

namespace Prophet.Client.Trading.Exchanges.Binance.Models;

/// <summary>
/// 币安WebSocket客户端
/// 支持K线订阅、账户更新订阅、自动重连
/// </summary>
public class BinanceWebSocketClient : IDisposable
{
    private string _wsUrl;
    private ClientWebSocket? _webSocket;
    private CancellationTokenSource? _cancellationTokenSource;
    private readonly Dictionary<string, bool> _subscriptions = new();
    private bool _disposed;
    private bool _isConnected;
    private Task? _receiveTask;
    
    // 重连参数
    private int _reconnectAttempts = 0;
    private const int MaxReconnectAttempts = 10;
    private TimeSpan _reconnectDelay = TimeSpan.FromSeconds(1);
    private readonly TimeSpan _maxReconnectDelay = TimeSpan.FromMinutes(5);
    
    // 事件
    public event EventHandler<BinanceKlineMessage>? OnKlineReceived;
    public event EventHandler<string>? OnAccountUpdate;
    public event EventHandler<string>? OnOrderTradeUpdate;
    public event EventHandler<string>? OnError;
    public event EventHandler? OnConnected;
    public event EventHandler? OnDisconnected;
    
    public bool IsConnected => _isConnected;
    
    public BinanceWebSocketClient(string wsUrl)
    {
        _wsUrl = wsUrl.TrimEnd('/');
    }
    
    /// <summary>
    /// 设置 WebSocket 基础地址（在 Initialize 后调用）
    /// </summary>
    public void SetBaseUrl(string wsUrl)
    {
        if (string.IsNullOrWhiteSpace(wsUrl)) return;
        _wsUrl = wsUrl.TrimEnd('/');
    }
    
    /// <summary>
    /// 连接WebSocket
    /// </summary>
    public async Task ConnectAsync()
    {
        if (_isConnected)
        {
            Console.WriteLine("WebSocket已连接");
            return;
        }
        
        try
        {
            _webSocket = new ClientWebSocket();
            _cancellationTokenSource = new CancellationTokenSource();
            
            var uri = new Uri(_wsUrl);
            await _webSocket.ConnectAsync(uri, _cancellationTokenSource.Token);
            
            _isConnected = true;
            _reconnectAttempts = 0;
            _reconnectDelay = TimeSpan.FromSeconds(1);
            
            Console.WriteLine($"✅ WebSocket已连接: {_wsUrl}");
            OnConnected?.Invoke(this, EventArgs.Empty);
            
            // 启动接收消息任务
            _receiveTask = Task.Run(ReceiveLoop, _cancellationTokenSource.Token);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ WebSocket连接失败: {ex.Message}");
            OnError?.Invoke(this, ex.Message);
            throw;
        }
    }
    
    /// <summary>
    /// 订阅K线流
    /// </summary>
    public async IAsyncEnumerable<Candlestick> SubscribeKlineAsync(
        string symbol, 
        string interval,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var stream = $"{symbol.ToLower()}@kline_{interval}";
        
        if (!_isConnected)
        {
            await ConnectAsync();
        }
        
        // 发送订阅消息
        await SubscribeToStreamAsync(stream);
        
        var queue = new System.Collections.Concurrent.ConcurrentQueue<Candlestick>();
        var messageReceived = new SemaphoreSlim(0);
        
        EventHandler<BinanceKlineMessage> handler = (sender, klineMsg) =>
        {
            if (klineMsg.Symbol.Equals(symbol, StringComparison.OrdinalIgnoreCase))
            {
                var candle = ConvertToCandlestick(klineMsg.Kline, symbol);
                queue.Enqueue(candle);
                messageReceived.Release();
            }
        };
        
        OnKlineReceived += handler;
        
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await messageReceived.WaitAsync(cancellationToken);
                
                while (queue.TryDequeue(out var candle))
                {
                    yield return candle;
                }
            }
        }
        finally
        {
            OnKlineReceived -= handler;
            await UnsubscribeFromStreamAsync(stream);
        }
    }
    
    /// <summary>
    /// 订阅用户数据流（账户更新）
    /// </summary>
    public async Task SubscribeUserDataStreamAsync(string listenKey)
    {
        if (!_isConnected)
        {
            await ConnectAsync();
        }
        
        var stream = listenKey;
        await SubscribeToStreamAsync(stream);
    }
    
    /// <summary>
    /// 订阅流
    /// </summary>
    private async Task SubscribeToStreamAsync(string stream)
    {
        if (_subscriptions.ContainsKey(stream))
        {
            Console.WriteLine($"已订阅流: {stream}");
            return;
        }
        
        var subscribeMessage = new
        {
            method = "SUBSCRIBE",
            @params = new[] { stream },
            id = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };
        
        await SendMessageAsync(JsonSerializer.Serialize(subscribeMessage));
        _subscriptions[stream] = true;
        
        Console.WriteLine($"✅ 已订阅流: {stream}");
    }
    
    /// <summary>
    /// 取消订阅流
    /// </summary>
    private async Task UnsubscribeFromStreamAsync(string stream)
    {
        if (!_subscriptions.ContainsKey(stream))
        {
            return;
        }
        
        var unsubscribeMessage = new
        {
            method = "UNSUBSCRIBE",
            @params = new[] { stream },
            id = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };
        
        await SendMessageAsync(JsonSerializer.Serialize(unsubscribeMessage));
        _subscriptions.Remove(stream);
        
        Console.WriteLine($"✅ 已取消订阅流: {stream}");
    }
    
    /// <summary>
    /// 发送消息
    /// </summary>
    private async Task SendMessageAsync(string message)
    {
        if (_webSocket == null || _webSocket.State != WebSocketState.Open)
        {
            throw new InvalidOperationException("WebSocket未连接");
        }
        
        var bytes = Encoding.UTF8.GetBytes(message);
        await _webSocket.SendAsync(
            new ArraySegment<byte>(bytes), 
            WebSocketMessageType.Text, 
            true, 
            _cancellationTokenSource?.Token ?? CancellationToken.None);
    }
    
    /// <summary>
    /// 接收消息循环
    /// </summary>
    private async Task ReceiveLoop()
    {
        var buffer = new byte[8192];
        var messageBuilder = new StringBuilder();
        
        try
        {
            while (_webSocket != null && 
                   _webSocket.State == WebSocketState.Open && 
                   _cancellationTokenSource != null && 
                   !_cancellationTokenSource.Token.IsCancellationRequested)
            {
                var result = await _webSocket.ReceiveAsync(
                    new ArraySegment<byte>(buffer), 
                    _cancellationTokenSource.Token);
                
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    Console.WriteLine("WebSocket收到关闭消息");
                    await HandleDisconnectAsync();
                    break;
                }
                
                messageBuilder.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
                
                if (result.EndOfMessage)
                {
                    var message = messageBuilder.ToString();
                    messageBuilder.Clear();
                    
                    // 处理消息
                    ProcessMessage(message);
                }
            }
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("WebSocket接收任务已取消");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ WebSocket接收错误: {ex.Message}");
            OnError?.Invoke(this, ex.Message);
            await HandleDisconnectAsync();
        }
    }
    
    /// <summary>
    /// 处理接收到的消息
    /// </summary>
    private void ProcessMessage(string message)
    {
        try
        {
            using var doc = JsonDocument.Parse(message);
            var root = doc.RootElement;
            
            // 检查是否为K线消息
            if (root.TryGetProperty("e", out var eventType))
            {
                if (eventType.GetString() == "kline")
                {
                    var klineMsg = JsonSerializer.Deserialize<BinanceKlineMessage>(message);
                    if (klineMsg != null)
                    {
                        OnKlineReceived?.Invoke(this, klineMsg);
                    }
                }
                else if (eventType.GetString() == "ACCOUNT_UPDATE")
                {
                    // 账户/仓位更新消息（余额、持仓变化）
                    OnAccountUpdate?.Invoke(this, message);
                }
                else if (eventType.GetString() == "ORDER_TRADE_UPDATE")
                {
                    // 订单状态更新消息
                    OnOrderTradeUpdate?.Invoke(this, message);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ 消息处理错误: {ex.Message}");
            OnError?.Invoke(this, ex.Message);
        }
    }
    
    /// <summary>
    /// 处理断开连接
    /// </summary>
    private async Task HandleDisconnectAsync()
    {
        _isConnected = false;
        OnDisconnected?.Invoke(this, EventArgs.Empty);
        
        // 尝试重连
        if (_reconnectAttempts < MaxReconnectAttempts)
        {
            _reconnectAttempts++;
            Console.WriteLine($"🔄 尝试重连 ({_reconnectAttempts}/{MaxReconnectAttempts})，延迟: {_reconnectDelay.TotalSeconds}秒");
            
            await Task.Delay(_reconnectDelay);
            
            try
            {
                await ReconnectAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 重连失败: {ex.Message}");
                
                // 指数退避
                _reconnectDelay = TimeSpan.FromTicks(
                    Math.Min(_reconnectDelay.Ticks * 2, _maxReconnectDelay.Ticks));
            }
        }
        else
        {
            Console.WriteLine($"❌ 已达到最大重连次数 ({MaxReconnectAttempts})");
            OnError?.Invoke(this, "已达到最大重连次数");
        }
    }
    
    /// <summary>
    /// 重新连接
    /// </summary>
    private async Task ReconnectAsync()
    {
        // 清理旧连接
        _webSocket?.Dispose();
        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource?.Dispose();
        
        // 重新连接
        await ConnectAsync();
        
        // 重新订阅所有流
        var streams = new List<string>(_subscriptions.Keys);
        _subscriptions.Clear();
        
        foreach (var stream in streams)
        {
            await SubscribeToStreamAsync(stream);
        }
    }
    
    /// <summary>
    /// 转换币安K线数据为Candlestick
    /// </summary>
    private Candlestick ConvertToCandlestick(BinanceKlineData kline, string symbol)
    {
        return new Candlestick
        {
            Time = DateTimeOffset.FromUnixTimeMilliseconds(kline.OpenTime).UtcDateTime,
            Open = (double)decimal.Parse(kline.Open),
            High = (double)decimal.Parse(kline.High),
            Low = (double)decimal.Parse(kline.Low),
            Close = (double)decimal.Parse(kline.Close),
            Volume = (double)decimal.Parse(kline.Volume),
            IsClosed = kline.IsClosed
        };
    }
    
    /// <summary>
    /// 关闭连接
    /// </summary>
    public async Task DisconnectAsync()
    {
        if (_webSocket != null && _webSocket.State == WebSocketState.Open)
        {
            _cancellationTokenSource?.Cancel();
            
            await _webSocket.CloseAsync(
                WebSocketCloseStatus.NormalClosure, 
                "Client closing", 
                CancellationToken.None);
        }
        
        _isConnected = false;
        Console.WriteLine("WebSocket已断开");
    }
    
    public void Dispose()
    {
        if (!_disposed)
        {
            _cancellationTokenSource?.Cancel();
            _webSocket?.Dispose();
            _cancellationTokenSource?.Dispose();
            _disposed = true;
        }
    }
}

