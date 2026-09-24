/*
 * ============================================================================
 * 文件名：compiler_cache.cpp
 * 功能说明：DSL编译缓存实现
 * ============================================================================
 */

#include "prophet/bytecode/compiler_cache.hpp"
#include "prophet/common/compile_time_hash.hpp"
#include <chrono>

namespace prophet {
namespace bytecode {

std::vector<Instruction> CompilationCache::get(const std::string& dsl_str) {
    uint64_t dsl_hash = hashDSL(dsl_str);
    
    // 使用读锁
    std::shared_lock<std::shared_mutex> lock(rw_mutex_);
    
    auto it = cache_.find(dsl_hash);
    if (it != cache_.end()) {
        // 缓存命中
        hit_count_++;
        return it->second.bytecode;
    }
    
    // 缓存未命中
    miss_count_++;
    return {};
}

void CompilationCache::put(const std::string& dsl_str, const std::vector<Instruction>& bytecode) {
    uint64_t dsl_hash = hashDSL(dsl_str);
    
    // 使用写锁
    std::unique_lock<std::shared_mutex> lock(rw_mutex_);
    
    CompilationCacheItem item;
    item.bytecode = bytecode;
    item.dsl_hash = std::to_string(dsl_hash);
    item.code_size = bytecode.size();
    
    cache_[dsl_hash] = std::move(item);
}

void CompilationCache::clear() {
    std::unique_lock<std::shared_mutex> lock(rw_mutex_);
    
    cache_.clear();
    hit_count_ = 0;
    miss_count_ = 0;
}

CompilationCache::Stats CompilationCache::getStats() const {
    std::shared_lock<std::shared_mutex> lock(rw_mutex_);
    
    Stats stats;
    stats.total_entries = cache_.size();
    stats.hit_count = hit_count_;
    stats.miss_count = miss_count_;
    
    size_t total_access = hit_count_ + miss_count_;
    stats.hit_rate = total_access > 0 ? 
        static_cast<double>(hit_count_) / total_access : 0.0;
    
    return stats;
}

uint64_t CompilationCache::hashDSL(const std::string& dsl_str) const {
    // 使用编译时哈希函数计算DSL字符串的哈希值
    return utils::CompileTimeHash::hash(dsl_str.c_str(), dsl_str.length());
}

} // namespace bytecode
} // namespace prophet
