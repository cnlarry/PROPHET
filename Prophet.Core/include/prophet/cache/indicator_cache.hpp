#pragma once

#include <string>
#include <unordered_map>
#include <unordered_set>
#include <shared_mutex>
#include <cstdint>
#include <queue>
#include <memory>
#include <atomic>
#include "prophet/common/types.hpp"
#include "prophet/common/compile_time_hash.hpp"

namespace prophet {
namespace cache {

/**
 * @brief 指标缓存管理器
 * 
 * P1优化：从Context中分离出来的专职指标缓存管理
 * P3优化：添加预计算、增量计算和智能缓存驱逐
 * 
 * 职责：
 * - 指标结果缓存
 * - 智能失效策略（基于K线版本）
 * - 线程安全的并发访问
 * - 预计算热点指标
 * - 增量计算支持
 * - 智能缓存驱逐
 * 
 * 性能优化点：
 * - 使用扁平化的复合键（timeframe:indicator）
 * - K线版本追踪，精准失效
 * - 读写锁支持高并发读取
 * - 维护timeframe和indicator的索引，支持精确清除
 * - LRU缓存驱逐策略，防止内存泄漏
 */
class IndicatorCache {
public:
    /**
     * @brief 缓存条目
     */
    struct CacheEntry {
        IndicatorResult result;
        uint64_t kline_version;  // 该指标计算时的K线版本
        size_t lru_counter;       // LRU计数器
        
        CacheEntry() : kline_version(0), lru_counter(0) {}
        CacheEntry(const IndicatorResult& r, uint64_t v, size_t lru) 
            : result(r), kline_version(v), lru_counter(lru) {}
    };

public:
    /**
     * @brief 构造函数
     * @param max_entries 最大缓存条目数（0表示无限制）
     */
    explicit IndicatorCache(size_t max_entries = 10000);
    ~IndicatorCache() = default;

    // 禁止拷贝，允许移动
    IndicatorCache(const IndicatorCache&) = delete;
    IndicatorCache& operator=(const IndicatorCache&) = delete;
    IndicatorCache(IndicatorCache&&) = default;
    IndicatorCache& operator=(IndicatorCache&&) = default;

    /**
     * @brief 获取缓存的指标
     * @param timeframe 时间周期
     * @param indicator_name 指标名称
     * @param current_kline_version 当前K线版本
     * @param[out] result 输出指标结果
     * @return 是否命中缓存（且版本匹配）
     */
    bool get(const std::string& timeframe, 
             const std::string& indicator_name,
             uint64_t current_kline_version,
             IndicatorResult& result) const;

    /**
     * @brief 获取缓存的指标（支持参数化缓存键）
     * @param timeframe 时间周期
     * @param indicator_name 指标名称
     * @param param_hash 参数哈希（用于区分不同参数配置）
     * @param current_kline_version 当前K线版本
     * @param[out] result 输出指标结果
     * @return 是否命中缓存（且版本匹配）
     */
    bool get(const std::string& timeframe, 
             const std::string& indicator_name,
             const std::string& param_hash,
             uint64_t current_kline_version,
             IndicatorResult& result) const;

    /**
     * @brief 设置指标缓存
     * @param timeframe 时间周期
     * @param indicator_name 指标名称
     * @param result 指标结果
     * @param kline_version K线版本
     */
    void set(const std::string& timeframe,
             const std::string& indicator_name,
             const IndicatorResult& result,
             uint64_t kline_version);

    /**
     * @brief 设置指标缓存（支持参数化缓存键）
     * @param timeframe 时间周期
     * @param indicator_name 指标名称
     * @param param_hash 参数哈希（用于区分不同参数配置）
     * @param result 指标结果
     * @param kline_version K线版本
     */
    void set(const std::string& timeframe,
             const std::string& indicator_name,
             const std::string& param_hash,
             const IndicatorResult& result,
             uint64_t kline_version);

    /**
     * @brief 清除指定时间周期的所有缓存
     * @param timeframe 时间周期（为空则清除全部）
     */
    void clear(const std::string& timeframe = "");

    /**
     * @brief 清除指定指标的缓存
     * @param indicator_name 指标名称
     */
    void clearIndicator(const std::string& indicator_name);

    /**
     * @brief 获取缓存统计信息
     */
    struct Statistics {
        size_t total_entries;
        size_t hit_count;
        size_t miss_count;
        size_t eviction_count;
        double hit_rate;
    };

    Statistics getStatistics() const;

    /**
     * @brief 重置统计信息
     */
    void resetStatistics();

    /**
     * @brief 设置最大缓存条目数
     * @param max_entries 最大缓存条目数（0表示无限制）
     */
    void setMaxEntries(size_t max_entries);

    /**
     * @brief 获取最大缓存条目数
     */
    size_t getMaxEntries() const;

private:
    /**
     * @brief 生成缓存键哈希（阶段2优化：使用整数键）
     */
    inline uint64_t makeCacheKey(const std::string& timeframe, 
                                  const std::string& indicator_name) const {
        return utils::CacheKeyHasher::make_key(timeframe, indicator_name);
    }
    
    /**
     * @brief 生成缓存键哈希（支持参数哈希）
     */
    inline uint64_t makeCacheKey(const std::string& timeframe, 
                                  const std::string& indicator_name,
                                  const std::string& param_hash) const {
        return utils::CacheKeyHasher::make_key(timeframe, indicator_name, param_hash);
    }

    /**
     * @brief 缓存键分解信息
     */
    struct KeyInfo {
        std::string timeframe;
        std::string indicator_name;
        std::string param_hash;
    };

    /**
     * @brief 分解缓存键（用于维护索引）
     */
    KeyInfo decomposeCacheKey(const uint64_t key) const;

    /**
     * @brief 执行缓存驱逐
     */
    void evictEntries();

    /**
     * @brief 维护索引关系
     */
    void addToIndex(const uint64_t key, 
                    const std::string& timeframe, 
                    const std::string& indicator_name);

    /**
     * @brief 从索引中移除
     */
    void removeFromIndex(const uint64_t key, 
                         const std::string& timeframe, 
                         const std::string& indicator_name);

private:
    // 主缓存：键 -> 缓存条目
    std::unordered_map<uint64_t, CacheEntry> cache_;
    
    // 缓存键 -> 键信息映射（用于索引和分解）
    std::unordered_map<uint64_t, KeyInfo> key_info_map_;

    // 索引：timeframe -> {keys}
    std::unordered_map<std::string, std::unordered_set<uint64_t>> timeframe_index_;
    
    // 索引：indicator_name -> {keys}
    std::unordered_map<std::string, std::unordered_set<uint64_t>> indicator_index_;

    // 统计信息
    mutable uint64_t hit_count_ = 0;
    mutable uint64_t miss_count_ = 0;
    mutable uint64_t eviction_count_ = 0;

    // LRU计数器
    mutable size_t lru_counter_ = 0;
    
    // 最大缓存条目数
    size_t max_entries_;

    // 读写锁：支持多线程并发读取
    mutable std::shared_mutex mutex_;
};

} // namespace cache
} // namespace prophet

