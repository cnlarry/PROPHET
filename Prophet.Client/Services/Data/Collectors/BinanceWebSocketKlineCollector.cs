using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Prophet.Client.Database.Repositories;
using Prophet.Client.Models;
using Prophet.Client.Services.Data.Preparation;
using Prophet.Client.Services.Market;

namespace Prophet.Client.Services.Data.Collectors;

/// <summary>
/// 币安WebSocket K线数据采集器
/// 通过WebSocket实时接收K线数据更新，相比REST API具有以下优势：
/// 1. 实时性更好：数据更新时立即推送，无需轮询
/// 2. 减少API调用：避免频繁的REST请求
/// 3. 降低延迟：实时推送比轮询更快
/// 4. 减少服务器压力：对币安服务器和我们的服务器都更友好
/// 
/// 数据补齐策略：
/// 1. 大规模数据缺失（超过1天）：使用Download下载
/// 2. 短期数据缺失（1天内）：使用REST API补充
/// 3. 实时数据：通过WebSocket接收
/// 
/// WebSocket流格式：wss://fstream.binance.com/ws/{symbol}@kline_{interval}
/// 例如：wss://fstream.binance.com/ws/btcusdt@kline_1m
/// </summary>
public class BinanceWebSocketKlineCollector : IDisposable
{
    private readonly MarketDataRepository _repository;
    private readonly IBinanceExchangeGateway _gateway;
    private readonly BinanceDataDownloader? _dataDownloader;
    private readonly KlineSyncService? _klineSyncService;
    private readonly string _symbol;
    private readonly string _interval;
    private readonly bool _enableGapFill;
    
    private ClientWebSocket? _webSocket;
    private CancellationTokenSource? _cancellationTokenSource;
    private Task? _receiveTask;
    private bool _isRunning = false;
    private bool _isDisposed = false;
    private DateTime? _lastKlineTime;
    private int _reconnectAttempts = 0;
    private const int MAX_RECONNECT_ATTEMPTS = 10;
    private const int RECONNECT_DELAY_SECONDS = 5;
    
    // 事件
    public event EventHandler<KlineReceivedEventArgs>? KlineReceived;
    public event EventHandler<ConnectionStatusEventArgs>? ConnectionStatusChanged;
    public event EventHandler<ErrorEventArgs>? ErrorOccurred;
    
    public string Name => $"BinanceWebSocket-{_symbol}-{_interval}";
    public bool IsRunning => _isRunning;
    public DateTime? LastKlineTime => _lastKlineTime;
    
    /// <summary>
    /// 构造函数
    /// </summary>
    public BinanceWebSocketKlineCollector(
        MarketDataRepository repository,
        IBinanceExchangeGateway gateway,
        string symbol,
        string interval = "1m",
        bool enableGapFill = false)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
        _symbol = symbol?.ToLowerInvariant() ?? throw new ArgumentNullException(nameof(symbol));
        _interval = interval ?? throw new ArgumentNullException(nameof(interval));
        _enableGapFill = enableGapFill;
        if (enableGapFill)
        {
            _dataDownloader = new BinanceDataDownloader(gateway.HttpClient, repository);
            var historicalDownloader = new BinanceHistoricalDataDownloader(gateway.HttpClient);
            var gapFiller = new BinanceGapFiller(gateway, repository);
            _klineSyncService = new KlineSyncService(historicalDownloader, gapFiller);
        }
        else
        {
            _dataDownloader = null;
            _klineSyncService = null;
        }
    }
    
    /// <summary>
    /// 启动WebSocket连接并开始接收数据
    /// 启动前会先检查并补齐缺失数据
    /// </summary>
    public async Task StartAsync()
    {
        if (_isRunning)
        {
            Console.WriteLine($"⚠️ [{Name}] WebSocket已在运行");
            return;
        }
        
        if (_isDisposed)
        {
            throw new ObjectDisposedException(nameof(BinanceWebSocketKlineCollector));
        }
        
        _isRunning = true;
        _cancellationTokenSource = new CancellationTokenSource();
        _reconnectAttempts = 0;
        
        Console.WriteLine($"🚀 [{Name}] 启动WebSocket连接...");
        
        // 🔑 启动前先检查并补齐缺失数据
        if (_enableGapFill)
        {
            await CheckAndFillDataGapsAsync();
        }
        
        await ConnectAndReceiveAsync();
    }
    
    /// <summary>
    /// 停止WebSocket连接
    /// </summary>
    public async Task StopAsync()
    {
        if (!_isRunning)
        {
            return;
        }
        
        Console.WriteLine($"🛑 [{Name}] 停止WebSocket连接");
        
        _isRunning = false;
        _cancellationTokenSource?.Cancel();
        
        if (_webSocket != null)
        {
            try
            {
                if (_webSocket.State == WebSocketState.Open)
                {
                    await _webSocket.CloseAsync(
                        WebSocketCloseStatus.NormalClosure,
                        "正常关闭",
                        CancellationToken.None);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ [{Name}] 关闭WebSocket时出错: {ex.Message}");
            }
            finally
            {
                _webSocket?.Dispose();
                _webSocket = null;
            }
        }
        
        if (_receiveTask != null)
        {
            try
            {
                await _receiveTask;
            }
            catch (OperationCanceledException)
            {
                // 正常取消，忽略
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ [{Name}] 接收任务异常: {ex.Message}");
            }
        }
        
        _cancellationTokenSource?.Dispose();
        _cancellationTokenSource = null;
        
        OnConnectionStatusChanged(false, "已停止");
    }
    
    /// <summary>
    /// 连接WebSocket并开始接收数据
    /// </summary>
    private async Task ConnectAndReceiveAsync()
    {
        while (_isRunning && !_cancellationTokenSource!.Token.IsCancellationRequested)
        {
            try
            {
                // 构建WebSocket URL
                var wsUri = _gateway.BuildKlineStreamUri(_symbol, _interval);
                
                Console.WriteLine($"🔌 [{Name}] 连接到: {wsUri}");
                
                // 创建WebSocket客户端
                _webSocket = _gateway.CreateWebSocketClient();
                
                // 连接到WebSocket服务器
                await _webSocket.ConnectAsync(wsUri, _cancellationTokenSource.Token);
                
                Console.WriteLine($"✅ [{Name}] WebSocket连接成功");
                _reconnectAttempts = 0;
                
                OnConnectionStatusChanged(true, "已连接");
                
                // WebSocket连接成功后，等待一小段时间让数据开始流动
                // 然后再次检查数据连续性（因为WebSocket会实时接收数据）
                _ = Task.Run(async () =>
                {
                    await Task.Delay(5000); // 等待5秒
                    if (_isRunning && !_cancellationTokenSource!.Token.IsCancellationRequested)
                    {
                        await VerifyDataContinuityAfterConnectionAsync();
                    }
                }, _cancellationTokenSource.Token);
                
                // 开始接收数据
                _receiveTask = Task.Run(() => ReceiveLoopAsync(_cancellationTokenSource.Token), _cancellationTokenSource.Token);
                
                // 等待接收任务完成（如果异常退出会重新连接）
                await _receiveTask;
            }
            catch (OperationCanceledException)
            {
                // 正常取消，退出循环
                break;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ [{Name}] WebSocket连接失败: {ex.Message}");
                OnErrorOccurred(ex);
                
                // 如果不是正常停止，尝试重连
                if (_isRunning && !_cancellationTokenSource!.Token.IsCancellationRequested)
                {
                    _reconnectAttempts++;
                    
                    if (_reconnectAttempts <= MAX_RECONNECT_ATTEMPTS)
                    {
                        Console.WriteLine($"🔄 [{Name}] {RECONNECT_DELAY_SECONDS}秒后尝试重连（第{_reconnectAttempts}/{MAX_RECONNECT_ATTEMPTS}次）...");
                        OnConnectionStatusChanged(false, $"重连中（{_reconnectAttempts}/{MAX_RECONNECT_ATTEMPTS}）");
                        
                        await Task.Delay(RECONNECT_DELAY_SECONDS * 1000, _cancellationTokenSource.Token);
                    }
                    else
                    {
                        Console.WriteLine($"❌ [{Name}] 达到最大重连次数，停止重连");
                        OnConnectionStatusChanged(false, "连接失败");
                        break;
                    }
                }
            }
            finally
            {
                if (_webSocket != null)
                {
                    try
                    {
                        if (_webSocket.State == WebSocketState.Open)
                        {
                            await _webSocket.CloseAsync(
                                WebSocketCloseStatus.NormalClosure,
                                "关闭连接",
                                CancellationToken.None);
                        }
                    }
                    catch
                    {
                        // 忽略关闭时的异常
                    }
                    
                    _webSocket.Dispose();
                    _webSocket = null;
                }
            }
        }
        
        _isRunning = false;
    }
    
    /// <summary>
    /// 接收数据循环
    /// </summary>
    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        var buffer = new byte[4096];
        var messageBuffer = new List<byte>();
        
        try
        {
            while (_webSocket != null && 
                   _webSocket.State == WebSocketState.Open && 
                   !cancellationToken.IsCancellationRequested)
            {
                // 接收数据
                var result = await _webSocket.ReceiveAsync(
                    new ArraySegment<byte>(buffer),
                    cancellationToken);
                
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    Console.WriteLine($"🔌 [{Name}] WebSocket收到关闭消息");
                    break;
                }
                
                // 将接收到的数据添加到消息缓冲区
                messageBuffer.AddRange(buffer.Take(result.Count));
                
                // 如果消息接收完成，处理消息
                if (result.EndOfMessage)
                {
                    if (messageBuffer.Count > 0)
                    {
                        await ProcessMessageAsync(messageBuffer.ToArray());
                        messageBuffer.Clear();
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            // 正常取消
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ [{Name}] 接收数据时出错: {ex.Message}");
            OnErrorOccurred(ex);
            throw; // 重新抛出异常以触发重连
        }
    }
    
    /// <summary>
    /// 处理接收到的WebSocket消息
    /// </summary>
    private async Task ProcessMessageAsync(byte[] messageBytes)
    {
        try
        {
            var message = Encoding.UTF8.GetString(messageBytes);
            
            // 解析JSON消息
            using var doc = JsonDocument.Parse(message);
            var root = doc.RootElement;
            
            // 币安WebSocket K线数据格式：
            // {
            //   "e": "kline",     // 事件类型
            //   "E": 123456789,   // 事件时间
            //   "s": "BTCUSDT",   // 交易对
            //   "k": {
            //     "t": 123400000, // K线开始时间
            //     "T": 123400599, // K线结束时间
            //     "s": "BTCUSDT", // 交易对
            //     "i": "1m",      // 时间间隔
            //     "o": "100",     // 开盘价
            //     "c": "101",     // 收盘价
            //     "h": "102",     // 最高价
            //     "l": "99",      // 最低价
            //     "v": "1000",    // 成交量
            //     "x": true       // 是否K线已结束
            //   }
            // }
            
            if (root.TryGetProperty("e", out var eventType) && 
                eventType.GetString() == "kline" &&
                root.TryGetProperty("k", out var klineData))
            {
                var k = klineData;
                var candlestick = KlineParser.ParseWebSocketKline(k);
                
                if (candlestick != null)
                {
                    _lastKlineTime = candlestick.Time;
                    
                    // 只有已闭合的K线才保存到数据库
                    if (candlestick.IsClosed)
                    {
                        await SaveKlineAsync(candlestick);
                        Console.WriteLine($"📊 [{Name}] 收到已闭合K线: {candlestick.Time:yyyy-MM-dd HH:mm:ss} " + $"O:{candlestick.Open} H:{candlestick.High} " + $"L:{candlestick.Low} C:{candlestick.Close} V:{candlestick.Volume}");
                    }
                    else
                    {
                        // 暂时屏蔽
                        // Console.WriteLine($"📈 [{Name}] 收到进行中K线: {candlestick.Time:yyyy-MM-dd HH:mm:ss} " + $"O:{candlestick.Open} H:{candlestick.High} " + $"L:{candlestick.Low} C:{candlestick.Close} V:{candlestick.Volume}");
                    }
                    
                    // 无论是否闭合，都触发事件以实时更新图表
                    OnKlineReceived(candlestick);
                }
            }
        }
        catch (JsonException ex)
        {
            Console.WriteLine($"⚠️ [{Name}] JSON解析失败: {ex.Message}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ [{Name}] 处理消息时出错: {ex.Message}");
            OnErrorOccurred(ex);
        }
    }
    
    /// <summary>
    /// 检查并补齐数据缺失
    /// 策略：
    /// 1. 检查连续性：从第一根open_time到最后一根open_time
    /// 2. 缺失超过1000分钟且隔天的 -> 按日下载
    /// 3. 缺失超过3天且隔月的 -> 按月下载
    /// 4. 其他缺失 -> REST API补齐
    /// </summary>
    private async Task CheckAndFillDataGapsAsync()
    {
        if (_klineSyncService == null)
        {
            Console.WriteLine($"⚠️ [{Name}] 未启用数据补齐，跳过连续性检查");
            return;
        }
        
        try
        {
            Console.WriteLine($"🔍 [{Name}] 检查数据连续性...");
            
            // 获取数据时间范围（第一根和最后一根）
            var (firstTime, lastTime) = await _repository.GetKlineTimeRangeAsync(_symbol.ToUpperInvariant(), _interval);
            
            if (!firstTime.HasValue || !lastTime.HasValue)
            {
                Console.WriteLine($"📊 [{Name}] 数据库中没有数据或数据不足，使用REST API获取初始数据");
                await FillInitialDataViaApiAsync();
                return;
            }
            
            Console.WriteLine($"📊 [{Name}] 数据时间范围: {firstTime.Value:yyyy-MM-dd HH:mm:ss} -> {lastTime.Value:yyyy-MM-dd HH:mm:ss} UTC");
            
            await _klineSyncService.EnsureCoverageAsync(
                _symbol.ToUpperInvariant(),
                _interval,
                firstTime.Value,
                DateTime.UtcNow,
                null,
                CancellationToken.None);
            
            Console.WriteLine($"✅ [{Name}] 数据补齐完成");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ [{Name}] 数据补齐失败: {ex.Message}");
            Console.WriteLine($"   堆栈跟踪: {ex.StackTrace}");
            // 不抛出异常，允许继续启动WebSocket
        }
    }
    
    /// <summary>
    /// 合并相邻或接近的缺失时间段，减少API请求次数
    /// 策略：
    /// 1. 如果两个时间段之间的间隔小于5分钟，合并它们
    /// 2. 如果合并后的时间段超过1000分钟，需要拆分
    /// </summary>
    private List<(DateTime start, DateTime end)> MergeGapsForApiFill(List<(DateTime start, DateTime end)> gaps)
    {
        if (gaps.Count == 0)
            return new List<(DateTime start, DateTime end)>();
        
        // 按开始时间排序
        var sortedGaps = gaps.OrderBy(g => g.start).ToList();
        var mergedGaps = new List<(DateTime start, DateTime end)>();
        
        var currentGap = sortedGaps[0];
        const int mergeThresholdMinutes = 5; // 如果两个时间段间隔小于5分钟，合并它们
        const int maxGapMinutes = 1000; // 一次API请求最多获取1000条（1000分钟）
        
        for (int i = 1; i < sortedGaps.Count; i++)
        {
            var nextGap = sortedGaps[i];
            var gapBetween = (nextGap.start - currentGap.end).TotalMinutes;
            var mergedDuration = (nextGap.end - currentGap.start).TotalMinutes;
            
            // 如果间隔小于阈值，且合并后的总时长不超过1000分钟，则合并
            if (gapBetween <= mergeThresholdMinutes && mergedDuration <= maxGapMinutes)
            {
                // 合并：扩展当前时间段的结束时间
                currentGap = (currentGap.start, nextGap.end);
            }
            else
            {
                // 不能合并，保存当前时间段
                // 如果当前时间段超过1000分钟，需要拆分
                if ((currentGap.end - currentGap.start).TotalMinutes > maxGapMinutes)
                {
                    mergedGaps.AddRange(SplitLargeGap(currentGap, maxGapMinutes));
                }
                else
                {
                    mergedGaps.Add(currentGap);
                }
                
                // 开始新的时间段
                currentGap = nextGap;
            }
        }
        
        // 处理最后一个时间段
        if ((currentGap.end - currentGap.start).TotalMinutes > maxGapMinutes)
        {
            mergedGaps.AddRange(SplitLargeGap(currentGap, maxGapMinutes));
        }
        else
        {
            mergedGaps.Add(currentGap);
        }
        
        return mergedGaps;
    }
    
    /// <summary>
    /// 拆分大的缺失时间段（超过1000分钟）
    /// </summary>
    private List<(DateTime start, DateTime end)> SplitLargeGap((DateTime start, DateTime end) gap, int maxMinutes)
    {
        var splits = new List<(DateTime start, DateTime end)>();
        var currentStart = gap.start;
        
        while (currentStart < gap.end)
        {
            var currentEnd = currentStart.AddMinutes(maxMinutes);
            if (currentEnd > gap.end)
            {
                currentEnd = gap.end;
            }
            
            splits.Add((currentStart, currentEnd));
            currentStart = currentEnd.AddMinutes(1); // 下一段的开始时间
        }
        
        return splits;
    }
    
    /// <summary>
    /// 为指定时间段下载缺失的月数据
    /// </summary>
    private async Task DownloadMissingMonthlyDataForGapAsync(DateTime gapStart, DateTime gapEnd)
    {
        if (_klineSyncService == null) return;
        await _klineSyncService.EnsureCoverageAsync(
            _symbol.ToUpperInvariant(),
            _interval,
            gapStart,
            gapEnd,
            null,
            CancellationToken.None);
    }
    
    /// <summary>
    /// 为指定时间段下载缺失的日数据
    /// </summary>
    private async Task DownloadMissingDailyDataForGapAsync(DateTime gapStart, DateTime gapEnd)
    {
        if (_klineSyncService == null) return;
        await _klineSyncService.EnsureCoverageAsync(
            _symbol.ToUpperInvariant(),
            _interval,
            gapStart,
            gapEnd,
            null,
            CancellationToken.None);
    }
    
    /// <summary>
    /// 使用REST API补齐指定时间段的缺失数据（循环补齐，直到补齐完成）
    /// </summary>
    private async Task FillGapViaApiAsync(DateTime gapStart, DateTime gapEnd)
    {
        const int maxRequests = 100;
        var currentStart = gapStart;
        var requestCount = 0;

        while (currentStart < gapEnd && requestCount < maxRequests)
        {
            try
            {
                if (requestCount == 0)
                {
                    Console.WriteLine($"   📥 [{Name}] REST API请求时间范围: {gapStart:yyyy-MM-dd HH:mm:ss} -> {gapEnd:yyyy-MM-dd HH:mm:ss}");
                }
                else
                {
                    Console.WriteLine($"   📥 [{Name}] REST API继续请求: {currentStart:yyyy-MM-dd HH:mm:ss} -> {gapEnd:yyyy-MM-dd HH:mm:ss}");
                }

                var candles = await _gateway.FetchKlinesAsync(
                    _symbol.ToUpperInvariant(),
                    _interval,
                    1000,
                    currentStart,
                    gapEnd);

                if (candles.Count == 0)
                {
                    Console.WriteLine($"   ⚠️ [{Name}] REST API返回空数据（可能该时间段没有数据）");
                    return;
                }

                await _repository.BulkInsertKlinesAsync(_symbol.ToUpperInvariant(), _interval, candles.ToList());

                var lastCandle = candles[^1];
                currentStart = lastCandle.Time.AddMinutes(1);
                requestCount++;

                if (candles.Count < 1000)
                {
                    Console.WriteLine($"   ✅ [{Name}] REST API补齐完成，共请求 {requestCount} 次");
                    return;
                }

                await Task.Delay(100);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"   ⚠️ [{Name}] REST API补齐失败（第 {requestCount + 1} 次请求）: {ex.Message}");
                return;
            }
        }

        if (requestCount >= maxRequests)
        {
            Console.WriteLine($"   ⚠️ [{Name}] REST API补齐达到最大请求次数（{maxRequests}），停止补齐");
        }
    }
    
    /// <summary>
    /// 使用REST API补充短期缺失数据（循环补齐，直到补齐完成或达到最大重试次数）
    /// </summary>
    private async Task FillShortTermGapViaApiAsync()
    {
        const int maxRetries = 50;
        const int maxMinutesPerRequest = 1000;
        var retryCount = 0;

        while (retryCount < maxRetries)
        {
            try
            {
                var (_, latestTime) = await _repository.GetKlineTimeRangeAsync(_symbol.ToUpperInvariant(), _interval);
                var now = DateTime.UtcNow;

                if (!latestTime.HasValue)
                {
                    var latestCandles = await _gateway.FetchKlinesAsync(_symbol.ToUpperInvariant(), _interval, 1000);
                    if (latestCandles.Count == 0)
                    {
                        Console.WriteLine($"⚠️ [{Name}] REST API返回空数据");
                        return;
                    }

                    var inserted = await _repository.BulkInsertKlinesAsync(_symbol.ToUpperInvariant(), _interval, latestCandles.ToList());
                    Console.WriteLine($"✅ [{Name}] REST API补充完成，共 {latestCandles.Count} 条，实际插入 {inserted} 条");
                    return;
                }

                var startTime = latestTime.Value.AddMinutes(1);
                var endTime = now.AddMinutes(-1);

                if ((endTime - startTime).TotalMinutes < 2)
                {
                    if (retryCount == 0)
                    {
                        Console.WriteLine($"✅ [{Name}] 数据很新（缺失少于2分钟），无需补充");
                    }
                    return;
                }

                if ((endTime - startTime).TotalMinutes > maxMinutesPerRequest)
                {
                    endTime = startTime.AddMinutes(maxMinutesPerRequest);
                }

                Console.WriteLine($"📥 [{Name}] 请求时间范围: {startTime:yyyy-MM-dd HH:mm:ss} -> {endTime:yyyy-MM-dd HH:mm:ss}");

                var candles = await _gateway.FetchKlinesAsync(
                    _symbol.ToUpperInvariant(),
                    _interval,
                    1000,
                    startTime,
                    endTime);

                if (candles.Count == 0)
                {
                    Console.WriteLine($"⚠️ [{Name}] REST API返回空数据（可能该时间段没有数据）");
                    return;
                }

                var insertedCount = await _repository.BulkInsertKlinesAsync(_symbol.ToUpperInvariant(), _interval, candles.ToList());
                Console.WriteLine($"✅ [{Name}] REST API补充完成，尝试插入 {candles.Count} 条，实际插入 {insertedCount} 条");

                var newestTime = candles[^1].Time;
                var remaining = now - newestTime;
                if (remaining.TotalMinutes <= 5)
                {
                    Console.WriteLine($"✅ [{Name}] 数据已补齐到最新时间");
                    return;
                }

                retryCount++;
                if (retryCount >= maxRetries)
                {
                    Console.WriteLine($"⚠️ [{Name}] 已达到最大重试次数（{maxRetries}），停止补齐。仍有 {remaining.TotalMinutes:F0} 分钟缺失");
                    return;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ [{Name}] REST API补充失败（第 {retryCount + 1} 次尝试）: {ex.Message}");
                retryCount++;
                if (retryCount >= maxRetries)
                {
                    Console.WriteLine($"⚠️ [{Name}] 已达到最大重试次数（{maxRetries}），停止补齐");
                    return;
                }
                await Task.Delay(1000);
            }
        }
    }
    
    /// <summary>
    /// 使用REST API获取初始数据
    /// </summary>
    private async Task FillInitialDataViaApiAsync()
    {
        await FillShortTermGapViaApiAsync();
    }
    
    /// <summary>
    /// WebSocket连接成功后验证数据连续性
    /// </summary>
    private async Task VerifyDataContinuityAfterConnectionAsync()
    {
        if (!_enableGapFill) return;
        
        try
        {
            var (_, latestTime) = await _repository.GetKlineTimeRangeAsync(_symbol.ToUpperInvariant(), _interval);
            if (!latestTime.HasValue) return;
            
            var now = DateTime.UtcNow;
            var timeDiff = now - latestTime.Value;
            
            // 如果仍有超过5分钟的缺失，尝试再次补充
            if (timeDiff.TotalMinutes > 5)
            {
                Console.WriteLine($"⚠️ [{Name}] WebSocket连接后检查：仍有 {timeDiff.TotalMinutes:F0} 分钟缺失，尝试补充...");
                await FillShortTermGapViaApiAsync();
            }
            else
            {
                Console.WriteLine($"✅ [{Name}] WebSocket连接后检查：数据连续性良好");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ [{Name}] WebSocket连接后数据验证失败: {ex.Message}");
        }
    }
    
    /// <summary>
    /// 保存K线数据到数据库
    /// </summary>
    private async Task SaveKlineAsync(Candlestick candlestick)
    {
        try
        {
            var klines = new List<Candlestick> { candlestick };
            await _repository.BulkInsertKlinesAsync(_symbol.ToUpperInvariant(), _interval, klines);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ [{Name}] 保存K线数据失败: {ex.Message}");
            throw;
        }
    }
    
    /// <summary>
    /// 触发K线接收事件
    /// </summary>
    private void OnKlineReceived(Candlestick candlestick)
    {
        KlineReceived?.Invoke(this, new KlineReceivedEventArgs
        {
            Candlestick = candlestick,
            Symbol = _symbol,
            Interval = _interval
        });
    }
    
    /// <summary>
    /// 触发连接状态变化事件
    /// </summary>
    private void OnConnectionStatusChanged(bool isConnected, string status)
    {
        ConnectionStatusChanged?.Invoke(this, new ConnectionStatusEventArgs
        {
            IsConnected = isConnected,
            Status = status
        });
    }
    
    /// <summary>
    /// 触发错误事件
    /// </summary>
    private void OnErrorOccurred(Exception exception)
    {
        ErrorOccurred?.Invoke(this, new ErrorEventArgs
        {
            Exception = exception,
            Message = exception.Message
        });
    }
    
    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        if (_isDisposed)
            return;
        
        StopAsync().Wait(5000);
        
        _cancellationTokenSource?.Dispose();
        _webSocket?.Dispose();
        
        _isDisposed = true;
    }
}

/// <summary>
/// K线接收事件参数
/// </summary>
public class KlineReceivedEventArgs : EventArgs
{
    public Candlestick Candlestick { get; set; } = null!;
    public string Symbol { get; set; } = string.Empty;
    public string Interval { get; set; } = string.Empty;
}

/// <summary>
/// 连接状态事件参数
/// </summary>
public class ConnectionStatusEventArgs : EventArgs
{
    public bool IsConnected { get; set; }
    public string Status { get; set; } = string.Empty;
}

/// <summary>
/// 错误事件参数
/// </summary>
public class ErrorEventArgs : EventArgs
{
    public Exception Exception { get; set; } = null!;
    public string Message { get; set; } = string.Empty;
}

