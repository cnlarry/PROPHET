#include "prophet/cache/indicator_cache.hpp"
#include <shared_mutex>
#include <algorithm>
#include <limits>

namespace prophet {
namespace cache {

IndicatorCache::IndicatorCache(size_t max_entries) 
    : max_entries_(max_entries), lru_counter_(0), eviction_count_(0) {
}

bool IndicatorCache::get(const std::string& timeframe,
                          const std::string& indicator_name,
                          uint64_t current_kline_version,
                          IndicatorResult& result) const {
    uint64_t key = makeCacheKey(timeframe, indicator_name);

    // 读锁：允许多线程并发读取
    std::shared_lock<std::shared_mutex> lock(mutex_);

    auto it = cache_.find(key);
    if (it == cache_.end()) {
        ++miss_count_;
        return false;
    }

    CacheEntry& entry = const_cast<CacheEntry&>(it->second);

    // 检查版本：如果K线已更新，缓存失效
    if (entry.kline_version != current_kline_version) {
        ++miss_count_;
        return false;
    }

    // 缓存命中 - 更新LRU计数器
    entry.lru_counter = ++lru_counter_;
    result = entry.result;
    ++hit_count_;
    return true;
}

bool IndicatorCache::get(const std::string& timeframe,
                          const std::string& indicator_name,
                          const std::string& param_hash,
                          uint64_t current_kline_version,
                          IndicatorResult& result) const {
    uint64_t key = makeCacheKey(timeframe, indicator_name, param_hash);

    // 读锁：允许多线程并发读取
    std::shared_lock<std::shared_mutex> lock(mutex_);

    auto it = cache_.find(key);
    if (it == cache_.end()) {
        ++miss_count_;
        return false;
    }

    CacheEntry& entry = const_cast<CacheEntry&>(it->second);

    // 检查版本：如果K线已更新，缓存失效
    if (entry.kline_version != current_kline_version) {
        ++miss_count_;
        return false;
    }

    // 缓存命中 - 更新LRU计数器
    entry.lru_counter = ++lru_counter_;
    result = entry.result;
    ++hit_count_;
    return true;
}

void IndicatorCache::set(const std::string& timeframe,
                          const std::string& indicator_name,
                          const IndicatorResult& result,
                          uint64_t kline_version) {
    uint64_t key = makeCacheKey(timeframe, indicator_name);

    // 写锁：独占访问
    std::unique_lock<std::shared_mutex> lock(mutex_);

    // 检查是否已存在该键
    bool exists = cache_.find(key) != cache_.end();
    
    // 创建或更新缓存条目
    cache_[key] = CacheEntry(result, kline_version, ++lru_counter_);
    
    // 维护索引
    if (!exists) {
        addToIndex(key, timeframe, indicator_name);
    }
    
    // 执行缓存驱逐
    evictEntries();
}

void IndicatorCache::set(const std::string& timeframe,
                          const std::string& indicator_name,
                          const std::string& param_hash,
                          const IndicatorResult& result,
                          uint64_t kline_version) {
    uint64_t key = makeCacheKey(timeframe, indicator_name, param_hash);

    // 写锁：独占访问
    std::unique_lock<std::shared_mutex> lock(mutex_);

    // 检查是否已存在该键
    bool exists = cache_.find(key) != cache_.end();
    
    // 创建或更新缓存条目
    cache_[key] = CacheEntry(result, kline_version, ++lru_counter_);
    
    // 维护索引
    if (!exists) {
        addToIndex(key, timeframe, indicator_name);
    }
    
    // 执行缓存驱逐
    evictEntries();
}

void IndicatorCache::clear(const std::string& timeframe) {
    std::unique_lock<std::shared_mutex> lock(mutex_);

    if (timeframe.empty()) {
        // 清除全部
        cache_.clear();
        key_info_map_.clear();
        timeframe_index_.clear();
        indicator_index_.clear();
    } else {
        // 按时间周期精确清除
        auto it = timeframe_index_.find(timeframe);
        if (it != timeframe_index_.end()) {
            const auto& keys = it->second;
            for (uint64_t key : keys) {
                // 从其他索引中移除
                auto key_info_it = key_info_map_.find(key);
                if (key_info_it != key_info_map_.end()) {
                    const KeyInfo& info = key_info_it->second;
                    removeFromIndex(key, info.timeframe, info.indicator_name);
                }
                
                // 从主缓存中移除
                cache_.erase(key);
                key_info_map_.erase(key);
            }
            
            // 清除时间周期索引
            timeframe_index_.erase(it);
        }
    }
}

void IndicatorCache::clearIndicator(const std::string& indicator_name) {
    std::unique_lock<std::shared_mutex> lock(mutex_);

    // 按指标名称精确清除
    auto it = indicator_index_.find(indicator_name);
    if (it != indicator_index_.end()) {
        const auto& keys = it->second;
        for (uint64_t key : keys) {
            // 从其他索引中移除
            auto key_info_it = key_info_map_.find(key);
            if (key_info_it != key_info_map_.end()) {
                const KeyInfo& info = key_info_it->second;
                removeFromIndex(key, info.timeframe, info.indicator_name);
            }
            
            // 从主缓存中移除
            cache_.erase(key);
            key_info_map_.erase(key);
        }
        
        // 清除指标索引
        indicator_index_.erase(it);
    }
}

IndicatorCache::Statistics IndicatorCache::getStatistics() const {
    std::shared_lock<std::shared_mutex> lock(mutex_);

    Statistics stats;
    stats.total_entries = cache_.size();
    stats.hit_count = hit_count_;
    stats.miss_count = miss_count_;
    stats.eviction_count = eviction_count_;

    uint64_t total_access = hit_count_ + miss_count_;
    stats.hit_rate = total_access > 0 ? 
        static_cast<double>(hit_count_) / total_access : 0.0;

    return stats;
}

void IndicatorCache::resetStatistics() {
    std::unique_lock<std::shared_mutex> lock(mutex_);

    hit_count_ = 0;
    miss_count_ = 0;
    eviction_count_ = 0;
}

void IndicatorCache::setMaxEntries(size_t max_entries) {
    std::unique_lock<std::shared_mutex> lock(mutex_);
    max_entries_ = max_entries;
    evictEntries();
}

size_t IndicatorCache::getMaxEntries() const {
    std::shared_lock<std::shared_mutex> lock(mutex_);
    return max_entries_;
}

IndicatorCache::KeyInfo IndicatorCache::decomposeCacheKey(const uint64_t key) const {
    // 从key_info_map_中获取分解信息
    auto it = key_info_map_.find(key);
    if (it != key_info_map_.end()) {
        return it->second;
    }
    return KeyInfo{};
}

void IndicatorCache::evictEntries() {
    if (max_entries_ == 0 || cache_.size() <= max_entries_) {
        return; // 无限制或未超过限制
    }

    // 需要驱逐的条目数量
    size_t entries_to_evict = cache_.size() - max_entries_;
    
    // 收集所有条目并按LRU计数器排序
    std::vector<std::pair<size_t, uint64_t>> lru_entries;
    lru_entries.reserve(cache_.size());
    
    for (const auto& [key, entry] : cache_) {
        lru_entries.emplace_back(entry.lru_counter, key);
    }
    
    // 按LRU计数器升序排序（最旧的在前）
    std::sort(lru_entries.begin(), lru_entries.end());
    
    // 驱逐最旧的条目
    for (size_t i = 0; i < entries_to_evict; ++i) {
        uint64_t key_to_evict = lru_entries[i].second;
        
        // 从索引中移除
        auto key_info_it = key_info_map_.find(key_to_evict);
        if (key_info_it != key_info_map_.end()) {
            const KeyInfo& info = key_info_it->second;
            removeFromIndex(key_to_evict, info.timeframe, info.indicator_name);
        }
        
        // 从主缓存中移除
        cache_.erase(key_to_evict);
        key_info_map_.erase(key_to_evict);
        
        ++eviction_count_;
    }
}

void IndicatorCache::addToIndex(const uint64_t key, 
                                const std::string& timeframe, 
                                const std::string& indicator_name) {
    // 添加到时间周期索引
    timeframe_index_[timeframe].insert(key);
    
    // 添加到指标名称索引
    indicator_index_[indicator_name].insert(key);
    
    // 保存键信息
    KeyInfo info;
    info.timeframe = timeframe;
    info.indicator_name = indicator_name;
    key_info_map_[key] = info;
}

void IndicatorCache::removeFromIndex(const uint64_t key, 
                                     const std::string& timeframe, 
                                     const std::string& indicator_name) {
    // 从时间周期索引中移除
    auto tf_it = timeframe_index_.find(timeframe);
    if (tf_it != timeframe_index_.end()) {
        tf_it->second.erase(key);
        if (tf_it->second.empty()) {
            timeframe_index_.erase(tf_it);
        }
    }
    
    // 从指标名称索引中移除
    auto ind_it = indicator_index_.find(indicator_name);
    if (ind_it != indicator_index_.end()) {
        ind_it->second.erase(key);
        if (ind_it->second.empty()) {
            indicator_index_.erase(ind_it);
        }
    }
}

} // namespace cache
} // namespace prophet

