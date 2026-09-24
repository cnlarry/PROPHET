using System;
using System.Collections.Generic;

namespace Prophet.Client.Services.Cache;

/// <summary>
/// LRU（Least Recently Used）缓存实现
/// 使用双向链表 + 字典实现 O(1) 复杂度的读写操作
/// </summary>
/// <typeparam name="TKey">缓存键类型</typeparam>
/// <typeparam name="TValue">缓存值类型</typeparam>
public class LRUCache<TKey, TValue> : IDisposable where TKey : notnull
{
    private readonly int _capacity;
    private readonly Dictionary<TKey, LinkedListNode<CacheItem>> _cache;
    private readonly LinkedList<CacheItem> _lruList;
    private readonly object _lock = new object();
    private bool _disposed;

    // 统计信息
    private long _hits;
    private long _misses;
    private long _evictions;

    public LRUCache(int capacity = 1000)
    {
        if (capacity <= 0)
            throw new ArgumentException("Capacity must be greater than 0", nameof(capacity));

        _capacity = capacity;
        _cache = new Dictionary<TKey, LinkedListNode<CacheItem>>(capacity);
        _lruList = new LinkedList<CacheItem>();
    }

    /// <summary>
    /// 获取缓存项数量
    /// </summary>
    public int Count
    {
        get
        {
            lock (_lock)
            {
                return _cache.Count;
            }
        }
    }

    /// <summary>
    /// 获取缓存容量
    /// </summary>
    public int Capacity => _capacity;

    /// <summary>
    /// 获取缓存统计信息
    /// </summary>
    public CacheStatistics GetStatistics()
    {
        lock (_lock)
        {
            var total = _hits + _misses;
            var hitRate = total > 0 ? (_hits * 100.0 / total) : 0;

            return new CacheStatistics
            {
                Hits = _hits,
                Misses = _misses,
                Evictions = _evictions,
                HitRate = hitRate,
                CurrentSize = _cache.Count,
                Capacity = _capacity
            };
        }
    }

    /// <summary>
    /// 尝试从缓存获取值
    /// </summary>
    public bool TryGet(TKey key, out TValue? value)
    {
        lock (_lock)
        {
            if (_cache.TryGetValue(key, out var node))
            {
                // 检查是否过期
                if (node.Value.ExpireTime.HasValue && DateTime.UtcNow > node.Value.ExpireTime.Value)
                {
                    // 已过期，移除
                    RemoveNode(node);
                    _misses++;
                    value = default;
                    return false;
                }

                // 移到链表头部（最近使用）
                _lruList.Remove(node);
                _lruList.AddFirst(node);

                _hits++;
                value = node.Value.Value;
                return true;
            }

            _misses++;
            value = default;
            return false;
        }
    }

    /// <summary>
    /// 添加或更新缓存项
    /// </summary>
    /// <param name="key">缓存键</param>
    /// <param name="value">缓存值</param>
    /// <param name="expirationSeconds">过期时间（秒），null 表示永不过期</param>
    public void Set(TKey key, TValue value, int? expirationSeconds = null)
    {
        lock (_lock)
        {
            DateTime? expireTime = expirationSeconds.HasValue
                ? DateTime.UtcNow.AddSeconds(expirationSeconds.Value)
                : null;

            if (_cache.TryGetValue(key, out var existingNode))
            {
                // 更新现有项
                existingNode.Value.Value = value;
                existingNode.Value.ExpireTime = expireTime;

                // 移到链表头部
                _lruList.Remove(existingNode);
                _lruList.AddFirst(existingNode);
            }
            else
            {
                // 添加新项
                if (_cache.Count >= _capacity)
                {
                    // 淘汰最久未使用的项（链表尾部）
                    var lruNode = _lruList.Last;
                    if (lruNode != null)
                    {
                        RemoveNode(lruNode);
                        _evictions++;
                    }
                }

                var newItem = new CacheItem
                {
                    Key = key,
                    Value = value,
                    ExpireTime = expireTime
                };

                var newNode = _lruList.AddFirst(newItem);
                _cache[key] = newNode;
            }
        }
    }

    /// <summary>
    /// 移除指定键的缓存项
    /// </summary>
    public bool Remove(TKey key)
    {
        lock (_lock)
        {
            if (_cache.TryGetValue(key, out var node))
            {
                RemoveNode(node);
                return true;
            }

            return false;
        }
    }

    /// <summary>
    /// 清空缓存
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _cache.Clear();
            _lruList.Clear();
        }
    }

    /// <summary>
    /// 清理过期项
    /// </summary>
    /// <returns>清理的项数</returns>
    public int CleanupExpired()
    {
        lock (_lock)
        {
            var now = DateTime.UtcNow;
            var expiredNodes = new List<LinkedListNode<CacheItem>>();

            // 收集过期项
            foreach (var node in _lruList)
            {
                if (node.ExpireTime.HasValue && now > node.ExpireTime.Value)
                {
                    var cacheNode = _cache[node.Key];
                    expiredNodes.Add(cacheNode);
                }
            }

            // 移除过期项
            foreach (var node in expiredNodes)
            {
                RemoveNode(node);
            }

            return expiredNodes.Count;
        }
    }

    /// <summary>
    /// 重置统计信息
    /// </summary>
    public void ResetStatistics()
    {
        lock (_lock)
        {
            _hits = 0;
            _misses = 0;
            _evictions = 0;
        }
    }

    private void RemoveNode(LinkedListNode<CacheItem> node)
    {
        _lruList.Remove(node);
        _cache.Remove(node.Value.Key);
    }

    public void Dispose()
    {
        if (_disposed) return;

        lock (_lock)
        {
            Clear();
            _disposed = true;
        }
    }

    private class CacheItem
    {
        public TKey Key { get; set; } = default!;
        public TValue Value { get; set; } = default!;
        public DateTime? ExpireTime { get; set; }
    }
}

/// <summary>
/// 缓存统计信息
/// </summary>
public class CacheStatistics
{
    /// <summary>
    /// 命中次数
    /// </summary>
    public long Hits { get; set; }

    /// <summary>
    /// 未命中次数
    /// </summary>
    public long Misses { get; set; }

    /// <summary>
    /// 淘汰次数
    /// </summary>
    public long Evictions { get; set; }

    /// <summary>
    /// 命中率（百分比）
    /// </summary>
    public double HitRate { get; set; }

    /// <summary>
    /// 当前缓存大小
    /// </summary>
    public int CurrentSize { get; set; }

    /// <summary>
    /// 缓存容量
    /// </summary>
    public int Capacity { get; set; }

    public override string ToString()
    {
        return $"Hits: {Hits}, Misses: {Misses}, Evictions: {Evictions}, " +
               $"HitRate: {HitRate:F2}%, Size: {CurrentSize}/{Capacity}";
    }
}

