/*
 * ============================================================================
 * 文件名：compiler_cache.hpp
 * 功能说明：DSL编译缓存 - 缓存DSL到字节码的编译结果
 * 
 * P3优化：添加编译缓存，避免重复编译相同的DSL代码
 * ============================================================================
 */

#pragma once

#include <string>
#include <unordered_map>
#include <vector>
#include <mutex>
#include <shared_mutex>
#include "prophet/bytecode/vm.hpp"

namespace prophet {
namespace bytecode {

/**
 * @brief 编译缓存项
 */
struct CompilationCacheItem {
    std::vector<Instruction> bytecode;      // 编译后的字节码
    std::string dsl_hash;                  // DSL字符串的哈希值
    uint64_t compilation_time;             // 编译耗时（毫秒）
    size_t code_size;                      // 字节码大小
};

/**
 * @brief DSL编译缓存管理器
 * 
 * 功能：
 * 1. 缓存DSL字符串到字节码的编译结果
 * 2. 基于DSL哈希值进行查找
 * 3. 支持并发访问
 */
class CompilationCache {
public:
    CompilationCache() = default;
    ~CompilationCache() = default;
    
    // 禁止拷贝和移动
    CompilationCache(const CompilationCache&) = delete;
    CompilationCache& operator=(const CompilationCache&) = delete;
    CompilationCache(CompilationCache&&) = delete;
    CompilationCache& operator=(CompilationCache&&) = delete;
    
    /**
     * @brief 从缓存中获取编译结果
     * @param dsl_str DSL字符串
     * @return 编译后的字节码，如果未命中返回空vector
     */
    std::vector<Instruction> get(const std::string& dsl_str);
    
    /**
     * @brief 添加编译结果到缓存
     * @param dsl_str DSL字符串
     * @param bytecode 编译后的字节码
     */
    void put(const std::string& dsl_str, const std::vector<Instruction>& bytecode);
    
    /**
     * @brief 清除所有缓存
     */
    void clear();
    
    /**
     * @brief 获取缓存统计信息
     */
    struct Stats {
        size_t total_entries;
        size_t hit_count;
        size_t miss_count;
        double hit_rate;
    };
    
    Stats getStats() const;
    
private:
    /**
     * @brief 计算DSL字符串的哈希值
     * @param dsl_str DSL字符串
     * @return 64位哈希值
     */
    uint64_t hashDSL(const std::string& dsl_str) const;
    
private:
    // 缓存存储：DSL哈希 -> 编译结果
    std::unordered_map<uint64_t, CompilationCacheItem> cache_;
    
    // 读写锁，支持并发访问
    mutable std::shared_mutex rw_mutex_;
    
    // 统计信息
    mutable size_t hit_count_ = 0;
    mutable size_t miss_count_ = 0;
};

} // namespace bytecode
} // namespace prophet
