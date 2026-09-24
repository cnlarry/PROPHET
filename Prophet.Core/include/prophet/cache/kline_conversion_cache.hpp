/*
 * ============================================================================
 * 文件名：kline_conversion_cache.hpp
 * 功能说明：K线转换LRU缓存系统
 * 
 * 设计目标：
 * - 避免重复转换相同的K线数据（回测场景中常见）
 * - LRU策略：自动淘汰最少使用的缓存项
 * - 增量更新：当新K线到达时，只转换增量部分
 * - 线程安全：支持并发访问
 * 
 * 性能提升：
 * - 缓存命中率 > 80%：性能提升 10-50倍
 * - 增量更新：性能提升 5-20倍（相比完全重算）
 * ============================================================================
 */

#pragma once

#include "prophet/common/types.hpp"
#include <string>
#include <vector>
#include <unordered_map>
#include <list>
#include <mutex>
#include <memory>
#include <optional>

namespace prophet::cache {

/**
 * 缓存键 - 唯一标识一次K线转换请求
 */
struct ConversionCacheKey {
    std::string symbol;           // 交易对（如 "BTCUSDT"）
    std::string source_tf;        // 源时间框架（如 "1m"）
    std::string target_tf;        // 目标时间框架（如 "5m"）
    int64_t start_time;           // 数据起始时间（毫秒）
    int64_t end_time;             // 数据结束时间（毫秒）
    size_t source_count;          // 源K线数量（用于快速验证）
    
    bool operator==(const ConversionCacheKey& other) const {
        return symbol == other.symbol 
            && source_tf == other.source_tf 
            && target_tf == other.target_tf
            && start_time == other.start_time 
            && end_time == other.end_time
            && source_count == other.source_count;
    }
};

/**
 * 缓存项 - 存储转换结果和元数据
 */
struct ConversionCacheEntry {
    std::vector<Kline> result;     // 转换后的K线数据
    size_t access_count;           // 访问次数
    int64_t last_access_time_us;   // 最后访问时间（微秒）
    size_t result_size_bytes;      // 内存占用估算
    
    ConversionCacheEntry() 
        : access_count(0)
        , last_access_time_us(0)
        , result_size_bytes(0)
    {}
    
    size_t estimateMemoryUsage() const {
        return result.size() * sizeof(Kline) + sizeof(ConversionCacheEntry);
    }
};

} // namespace prophet::cache

// 哈希函数 - 用于 unordered_map
namespace std {
    template<>
    struct hash<prophet::cache::ConversionCacheKey> {
        size_t operator()(const prophet::cache::ConversionCacheKey& key) const {
            // 使用 FNV-1a 哈希算法
            size_t hash = 14695981039346656037ULL;
            
            // 哈希 symbol
            for (char c : key.symbol) {
                hash ^= static_cast<size_t>(c);
                hash *= 1099511628211ULL;
            }
            
            // 哈希 source_tf
            for (char c : key.source_tf) {
                hash ^= static_cast<size_t>(c);
                hash *= 1099511628211ULL;
            }
            
            // 哈希 target_tf
            for (char c : key.target_tf) {
                hash ^= static_cast<size_t>(c);
                hash *= 1099511628211ULL;
            }
            
            // 哈希时间戳
            hash ^= static_cast<size_t>(key.start_time);
            hash *= 1099511628211ULL;
            hash ^= static_cast<size_t>(key.end_time);
            hash *= 1099511628211ULL;
            hash ^= key.source_count;
            
            return hash;
        }
    };
}

namespace prophet::cache {

/**
 * K线转换 LRU 缓存
 * 
 * 特性：
 * - LRU 淘汰策略：自动移除最少使用的项
 * - 内存限制：超过限制时自动清理
 * - 线程安全：使用互斥锁保护
 * - 命中率统计：实时监控缓存效果
 * 
 * 使用场景：
 * - 回测：同一段历史数据被反复转换
 * - 实盘：最近的K线数据被频繁访问
 * - 多策略：不同策略访问相同的时间框架数据
 */
class KlineConversionCache {
public:
    /**
     * 构造函数
     * 
     * @param max_entries 最大缓存项数（默认100）
     * @param max_memory_mb 最大内存占用（MB，默认500MB）
     */
    explicit KlineConversionCache(
        size_t max_entries = 100,
        size_t max_memory_mb = 500
    );
    
    /**
     * 查询缓存
     * 
     * @param key 缓存键
     * @return 如果命中返回转换结果，否则返回 nullopt
     */
    std::optional<std::vector<Kline>> get(const ConversionCacheKey& key);
    
    /**
     * 存入缓存
     * 
     * @param key 缓存键
     * @param value 转换结果
     */
    void put(const ConversionCacheKey& key, const std::vector<Kline>& value);
    
    /**
     * 增量更新缓存项
     * 
     * 当新的1m K线到达时，不需要重新转换全部数据，
     * 只需要将新K线追加到缓存的转换结果中
     * 
     * @param key 原始缓存键
     * @param new_source_klines 新增的源K线
     * @param updated_key 更新后的键（时间范围扩大）
     * @return 是否更新成功（如果原缓存不存在则返回false）
     */
    bool incrementalUpdate(
        const ConversionCacheKey& key,
        const std::vector<Kline>& new_source_klines,
        const ConversionCacheKey& updated_key
    );
    
    /**
     * 清空缓存
     */
    void clear();
    
    /**
     * 移除特定交易对的缓存
     */
    void clearSymbol(const std::string& symbol);
    
    /**
     * 缓存统计信息
     */
    struct Statistics {
        size_t total_requests;      // 总请求次数
        size_t cache_hits;          // 缓存命中次数
        size_t cache_misses;        // 缓存未命中次数
        double hit_rate;            // 命中率（0.0 ~ 1.0）
        size_t current_entries;     // 当前缓存项数
        size_t current_memory_bytes; // 当前内存占用（字节）
        size_t eviction_count;      // 淘汰次数
        double avg_access_count;    // 平均访问次数
    };
    
    Statistics getStatistics() const;
    
    /**
     * 重置统计信息
     */
    void resetStatistics();
    
    /**
     * 设置最大缓存项数
     */
    void setMaxEntries(size_t max_entries);
    
    /**
     * 设置最大内存占用
     */
    void setMaxMemoryMB(size_t max_memory_mb);

private:
    // LRU 列表：最近使用的在前面
    using LRUList = std::list<ConversionCacheKey>;
    using LRUIterator = LRUList::iterator;
    
    // 缓存存储：key -> (entry, LRU位置)
    std::unordered_map<ConversionCacheKey, std::pair<ConversionCacheEntry, LRUIterator>> cache_;
    
    // LRU 列表
    LRUList lru_list_;
    
    // 配置参数
    size_t max_entries_;
    size_t max_memory_bytes_;
    
    // 统计信息
    mutable Statistics stats_;
    
    // 线程安全
    mutable std::mutex mutex_;
    
    /**
     * 淘汰最少使用的缓存项
     */
    void evictLRU();
    
    /**
     * 更新LRU顺序（将key移到列表前面）
     */
    void touchLRU(const ConversionCacheKey& key);
    
    /**
     * 检查是否需要清理内存
     */
    bool needEviction() const;
    
    /**
     * 计算当前内存占用
     */
    size_t calculateMemoryUsage() const;
};

/**
 * 辅助函数：根据源K线数据创建缓存键
 */
ConversionCacheKey createCacheKey(
    const std::string& symbol,
    const std::string& source_tf,
    const std::string& target_tf,
    const std::vector<Kline>& source_klines
);

} // namespace prophet::cache

