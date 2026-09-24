using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using Prophet.Client.Database.Repositories;
using Prophet.Client.Models;
using Prophet.Client.Services.Data.Collectors;

namespace Prophet.Client.Services.Market;

/// <summary>
/// WebSocket行情流分发中心，保证同一symbol/interval只建立一条连接
/// </summary>
public sealed class MarketStreamHub : IDisposable
{
    private readonly MarketDataRepository _repository;
    private readonly IBinanceExchangeGateway _gateway;
    private readonly ConcurrentDictionary<string, StreamChannel> _channels = new();
    private bool _disposed;

    public MarketStreamHub(MarketDataRepository repository, IBinanceExchangeGateway gateway)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
    }

    /// <summary>
    /// 订阅实时K线
    /// </summary>
    public IDisposable Subscribe(string symbol, string interval, Action<KlineReceivedEventArgs> handler, bool enableGapFill = true)
    {
        if (handler == null) throw new ArgumentNullException(nameof(handler));
        var key = BuildKey(symbol, interval);
        var channel = _channels.GetOrAdd(key, _ => new StreamChannel(_repository, _gateway, symbol, interval, enableGapFill));
        channel.AddSubscriber(handler);
        channel.StartIfNeeded();
        return new ChannelSubscription(this, key, handler);
    }

    private void RemoveSubscriber(string key, Action<KlineReceivedEventArgs> handler)
    {
        if (!_channels.TryGetValue(key, out var channel))
            return;

        var isEmpty = channel.RemoveSubscriber(handler);
        if (isEmpty && _channels.TryRemove(key, out _))
        {
            _ = channel.StopAndDisposeAsync();
        }
    }

    private static string BuildKey(string symbol, string interval)
        => $"{symbol.ToUpperInvariant()}_{interval}";

    public void Dispose()
    {
        if (_disposed) return;

        foreach (var kvp in _channels)
        {
            kvp.Value.StopAndDisposeAsync().GetAwaiter().GetResult();
        }

        _channels.Clear();
        _disposed = true;
    }

    private sealed class StreamChannel : IDisposable
    {
        private readonly List<Action<KlineReceivedEventArgs>> _subscribers = new();
        private readonly BinanceWebSocketKlineCollector _collector;
        private readonly string _symbol;
        private readonly object _gate = new();
        private bool _isStarted;
        private bool _disposed;

        public StreamChannel(
            MarketDataRepository repository,
            IBinanceExchangeGateway gateway,
            string symbol,
            string interval,
            bool enableGapFill)
        {
            _symbol = symbol;
            _collector = new BinanceWebSocketKlineCollector(repository, gateway, symbol.ToLowerInvariant(), interval, enableGapFill: enableGapFill);
            _collector.KlineReceived += OnKlineReceived;
        }

        public void AddSubscriber(Action<KlineReceivedEventArgs> handler)
        {
            lock (_gate)
            {
                _subscribers.Add(handler);
            }
        }

        public bool RemoveSubscriber(Action<KlineReceivedEventArgs> handler)
        {
            bool isEmpty;
            lock (_gate)
            {
                _subscribers.Remove(handler);
                isEmpty = _subscribers.Count == 0;
            }
            return isEmpty;
        }

        public void StartIfNeeded()
        {
            lock (_gate)
            {
                if (_isStarted || _disposed)
                    return;
                _isStarted = true;
            }

            _ = _collector.StartAsync();
        }

        public async Task StopAndDisposeAsync()
        {
            if (_disposed)
                return;

            try
            {
                await _collector.StopAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ [MarketStreamHub] 停止 {_symbol} WebSocket 失败: {ex.Message}");
            }
            finally
            {
                _collector.KlineReceived -= OnKlineReceived;
                _collector.Dispose();
                lock (_gate)
                {
                    _isStarted = false;
                    _disposed = true;
                    _subscribers.Clear();
                }
            }
        }

        private void OnKlineReceived(object? sender, KlineReceivedEventArgs e)
        {
            Action<KlineReceivedEventArgs>[] handlers;
            lock (_gate)
            {
                if (_subscribers.Count == 0)
                    return;
                handlers = _subscribers.ToArray();
            }

            foreach (var handler in handlers)
            {
                try
                {
                    handler(e);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"⚠️ [MarketStreamHub] 分发K线事件失败: {ex.Message}");
                }
            }
        }

        public void Dispose()
        {
            StopAndDisposeAsync().GetAwaiter().GetResult();
        }
    }

    private sealed class ChannelSubscription : IDisposable
    {
        private readonly MarketStreamHub _hub;
        private readonly string _key;
        private readonly Action<KlineReceivedEventArgs> _handler;
        private bool _disposed;

        public ChannelSubscription(MarketStreamHub hub, string key, Action<KlineReceivedEventArgs> handler)
        {
            _hub = hub;
            _key = key;
            _handler = handler;
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _hub.RemoveSubscriber(_key, _handler);
        }
    }
}
