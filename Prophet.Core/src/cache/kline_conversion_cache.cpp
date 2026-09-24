/*
 * ============================================================================
 * 文件名：kline_conversion_cache.cpp
 * 功能说明：K线转换LRU缓存实现
 * ============================================================================
 */

#include "prophet/cache/kline_conversion_cache.hpp"
#include "prophet/tools/kline_converter.hpp"
#include <chrono>
#include <algorithm>
#include <iostream>

namespace prophet::cache {

// ============================================================================
// 构造函数
// ============================================================================

KlineConversionCache::KlineConversionCache(size_t max_entries, size_t max_memory_mb)
    : max_entries_(max_entries)
    , max_memory_bytes_(max_memory_mb * 1024 * 1024)
    , stats_{}
{
    stats_.total_requests = 0;
    stats_.cache_hits = 0;
    stats_.cache_misses = 0;
    stats_.hit_rate = 0.0;
    stats_.current_entries = 0;
    stats_.current_memory_bytes = 0;
    stats_.eviction_count = 0;
    stats_.avg_access_count = 0.0;
}

// ============================================================================
// 查询缓存
// ============================================================================

std::optional<std::vector<Kline>> KlineConversionCache::get(const ConversionCacheKey& key) {
    std::lock_guard<std::mutex> lock(mutex_);
    
    stats_.total_requests++;
    
    auto it = cache_.find(key);
    if (it != cache_.end()) {
        // 缓存命中
        stats_.cache_hits++;
        stats_.hit_rate = static_cast<double>(stats_.cache_hits) / stats_.total_requests;
        
        // 更新访问信息
        auto& entry = it->second.first;
        entry.access_count++;
        entry.last_access_time_us = std::chrono::duration_cast<std::chrono::microseconds>(
            std::chrono::high_resolution_clock::now().time_since_epoch()
        ).count();
        
        // 更新LRU顺序（移到前面）
        touchLRU(key);
        
        return entry.result;
    }
    
    // 缓存未命中
    stats_.cache_misses++;
    stats_.hit_rate = static_cast<double>(stats_.cache_hits) / stats_.total_requests;
    
    return std::nullopt;
}

// ============================================================================
// 存入缓存
// ============================================================================

void KlineConversionCache::put(const ConversionCacheKey& key, const std::vector<Kline>& value) {
    std::lock_guard<std::mutex> lock(mutex_);
    
    // 检查是否已存在
    if (cache_.find(key) != cache_.end()) {
        // 已存在，更新即可
        touchLRU(key);
        cache_[key].first.result = value;
        cache_[key].first.access_count++;
        cache_[key].first.last_access_time_us = std::chrono::duration_cast<std::chrono::microseconds>(
            std::chrono::high_resolution_clock::now().time_since_epoch()
        ).count();
        return;
    }
    
    // 检查是否需要淘汰
    while (needEviction()) {
        evictLRU();
    }
    
    // 创建新条目
    ConversionCacheEntry entry;
    entry.result = value;
    entry.access_count = 1;
    entry.last_access_time_us = std::chrono::duration_cast<std::chrono::microseconds>(
        std::chrono::high_resolution_clock::now().time_since_epoch()
    ).count();
    entry.result_size_bytes = entry.estimateMemoryUsage();
    
    // 添加到LRU列表前面
    lru_list_.push_front(key);
    
    // 添加到缓存
    cache_[key] = {entry, lru_list_.begin()};
    
    // 更新统计
    stats_.current_entries = cache_.size();
    stats_.current_memory_bytes = calculateMemoryUsage();
}

// ============================================================================
// 增量更新
// ============================================================================

bool KlineConversionCache::incrementalUpdate(
    const ConversionCacheKey& key,
    const std::vector<Kline>& new_source_klines,
    const ConversionCacheKey& updated_key
) {
    std::lock_guard<std::mutex> lock(mutex_);
    
    auto it = cache_.find(key);
    if (it == cache_.end()) {
        return false; // 原缓存不存在
    }
    
    // 获取原缓存结果
    auto& old_entry = it->second.first;
    std::vector<Kline> updated_result = old_entry.result;
    
    // 转换新增的K线
    // 注意：这里假设 new_source_klines 已经是正确的增量数据
    const int from_minutes = tools::kline::Converter::timeframeToMinutes(updated_key.source_tf);
    const int to_minutes = tools::kline::Converter::timeframeToMinutes(updated_key.target_tf);
    
    try {
        auto incremental_result = tools::kline::Converter::convert(
            new_source_klines, from_minutes, to_minutes, 0
        );
        
        // 合并结果
        updated_result.insert(updated_result.end(), incremental_result.begin(), incremental_result.end());
        
        // 删除旧缓存
        lru_list_.erase(it->second.second);
        cache_.erase(it);
        
        // 创建新缓存项
        ConversionCacheEntry new_entry;
        new_entry.result = std::move(updated_result);
        new_entry.access_count = old_entry.access_count + 1;
        new_entry.last_access_time_us = std::chrono::duration_cast<std::chrono::microseconds>(
            std::chrono::high_resolution_clock::now().time_since_epoch()
        ).count();
        new_entry.result_size_bytes = new_entry.estimateMemoryUsage();
        
        // 添加到LRU列表前面
        lru_list_.push_front(updated_key);
        
        // 添加到缓存
        cache_[updated_key] = {std::move(new_entry), lru_list_.begin()};
        
        // 更新统计
        stats_.current_memory_bytes = calculateMemoryUsage();
        
        return true;
    } catch (...) {
        return false;
    }
}

// ============================================================================
// 清空缓存
// ============================================================================

void KlineConversionCache::clear() {
    std::lock_guard<std::mutex> lock(mutex_);
    
    cache_.clear();
    lru_list_.clear();
    
    stats_.current_entries = 0;
    stats_.current_memory_bytes = 0;
}

void KlineConversionCache::clearSymbol(const std::string& symbol) {
    std::lock_guard<std::mutex> lock(mutex_);
    
    // 收集要删除的键
    std::vector<ConversionCacheKey> keys_to_remove;
    for (const auto& pair : cache_) {
        if (pair.first.symbol == symbol) {
            keys_to_remove.push_back(pair.first);
        }
    }
    
    // 删除
    for (const auto& key : keys_to_remove) {
        auto it = cache_.find(key);
        if (it != cache_.end()) {
            lru_list_.erase(it->second.second);
            cache_.erase(it);
        }
    }
    
    // 更新统计
    stats_.current_entries = cache_.size();
    stats_.current_memory_bytes = calculateMemoryUsage();
}

// ============================================================================
// 统计信息
// ============================================================================

KlineConversionCache::Statistics KlineConversionCache::getStatistics() const {
    std::lock_guard<std::mutex> lock(mutex_);
    
    Statistics stats = stats_;
    stats.current_entries = cache_.size();
    stats.current_memory_bytes = calculateMemoryUsage();
    
    // 计算平均访问次数
    if (!cache_.empty()) {
        size_t total_access = 0;
        for (const auto& pair : cache_) {
            total_access += pair.second.first.access_count;
        }
        stats.avg_access_count = static_cast<double>(total_access) / cache_.size();
    } else {
        stats.avg_access_count = 0.0;
    }
    
    return stats;
}

void KlineConversionCache::resetStatistics() {
    std::lock_guard<std::mutex> lock(mutex_);
    
    stats_.total_requests = 0;
    stats_.cache_hits = 0;
    stats_.cache_misses = 0;
    stats_.hit_rate = 0.0;
    stats_.eviction_count = 0;
    stats_.avg_access_count = 0.0;
}

void KlineConversionCache::setMaxEntries(size_t max_entries) {
    std::lock_guard<std::mutex> lock(mutex_);
    max_entries_ = max_entries;
    
    // 如果当前缓存项超过限制，进行淘汰
    while (cache_.size() > max_entries_) {
        evictLRU();
    }
}

void KlineConversionCache::setMaxMemoryMB(size_t max_memory_mb) {
    std::lock_guard<std::mutex> lock(mutex_);
    max_memory_bytes_ = max_memory_mb * 1024 * 1024;
    
    // 如果当前内存超过限制，进行淘汰
    while (needEviction()) {
        evictLRU();
    }
}

// ============================================================================
// 私有方法
// ============================================================================

void KlineConversionCache::evictLRU() {
    if (lru_list_.empty()) {
        return;
    }
    
    // 获取最少使用的键（列表末尾）
    auto victim_key = lru_list_.back();
    
    // 从缓存中删除
    cache_.erase(victim_key);
    
    // 从LRU列表中删除
    lru_list_.pop_back();
    
    // 更新统计
    stats_.eviction_count++;
    stats_.current_entries = cache_.size();
}

void KlineConversionCache::touchLRU(const ConversionCacheKey& key) {
    auto it = cache_.find(key);
    if (it == cache_.end()) {
        return;
    }
    
    // 从当前位置移除
    lru_list_.erase(it->second.second);
    
    // 添加到前面
    lru_list_.push_front(key);
    
    // 更新迭代器
    it->second.second = lru_list_.begin();
}

bool KlineConversionCache::needEviction() const {
    // 检查数量限制
    if (cache_.size() >= max_entries_) {
        return true;
    }
    
    // 检查内存限制
    if (calculateMemoryUsage() >= max_memory_bytes_) {
        return true;
    }
    
    return false;
}

size_t KlineConversionCache::calculateMemoryUsage() const {
    size_t total = 0;
    for (const auto& pair : cache_) {
        total += pair.second.first.estimateMemoryUsage();
    }
    return total;
}

// ============================================================================
// 辅助函数
// ============================================================================

ConversionCacheKey createCacheKey(
    const std::string& symbol,
    const std::string& source_tf,
    const std::string& target_tf,
    const std::vector<Kline>& source_klines
) {
    ConversionCacheKey key;
    key.symbol = symbol;
    key.source_tf = source_tf;
    key.target_tf = target_tf;
    key.source_count = source_klines.size();
    
    if (!source_klines.empty()) {
        key.start_time = source_klines.front().open_time;
        key.end_time = source_klines.back().close_time;
    } else {
        key.start_time = 0;
        key.end_time = 0;
    }
    
    return key;
}

} // namespace prophet::cache

