/*
 * ============================================================================
 * 文件名：compile_time_hash.hpp
 * 功能说明：编译期字符串哈希工具
 * 
 * 阶段2优化：使用constexpr哈希替代运行时字符串拼接
 * 预期提升：10-15%
 * ============================================================================
 */

#pragma once

#include <cstdint>
#include <cstddef>

namespace prophet::utils {

/**
 * FNV-1a 哈希算法（编译期版本）
 * 快速且分布均匀的哈希算法
 */
class CompileTimeHash {
public:
    // FNV-1a 64位常量
    static constexpr uint64_t FNV_OFFSET_BASIS = 14695981039346656037ULL;
    static constexpr uint64_t FNV_PRIME = 1099511628211ULL;
    
    /**
     * 编译期字符串哈希
     */
    static constexpr uint64_t hash(const char* str, size_t len) {
        return hash_impl(str, len, FNV_OFFSET_BASIS);
    }
    
    /**
     * 运行时字符串哈希（与编译期版本兼容）
     */
    static inline uint64_t hash_runtime(const char* str, size_t len) {
        uint64_t hash = FNV_OFFSET_BASIS;
        for (size_t i = 0; i < len; ++i) {
            hash ^= static_cast<uint64_t>(str[i]);
            hash *= FNV_PRIME;
        }
        return hash;
    }
    
    /**
     * std::string 版本
     */
    static inline uint64_t hash_runtime(const std::string& str) {
        return hash_runtime(str.data(), str.length());
    }
    
    /**
     * 组合两个哈希值
     */
    static constexpr uint64_t combine(uint64_t h1, uint64_t h2) {
        // boost::hash_combine 算法
        return h1 ^ (h2 + 0x9e3779b9 + (h1 << 6) + (h1 >> 2));
    }
    
    /**
     * 组合多个哈希值
     */
    template<typename... Hashes>
    static constexpr uint64_t combine_multi(uint64_t first, Hashes... rest) {
        if constexpr (sizeof...(rest) == 0) {
            return first;
        } else {
            return combine(first, combine_multi(rest...));
        }
    }

private:
    static constexpr uint64_t hash_impl(const char* str, size_t len, uint64_t hash) {
        return len == 0 ? hash : hash_impl(str + 1, len - 1, (hash ^ static_cast<uint64_t>(*str)) * FNV_PRIME);
    }
};

/**
 * 缓存键哈希器
 * 用于替代字符串拼接的缓存键生成
 */
class CacheKeyHasher {
public:
    /**
     * 生成缓存键哈希
     * @param timeframe 时间框架（如 "5m"）
     * @param indicator 指标名称（如 "MACD"）
     * @return 唯一哈希值
     */
    static inline uint64_t make_key(const std::string& timeframe, const std::string& indicator) {
        uint64_t h1 = CompileTimeHash::hash_runtime(timeframe);
        uint64_t h2 = CompileTimeHash::hash_runtime(indicator);
        return CompileTimeHash::combine(h1, h2);
    }
    
    /**
     * 生成缓存键哈希（带参数哈希）
     */
    static inline uint64_t make_key(const std::string& timeframe, const std::string& indicator, const std::string& param_hash) {
        uint64_t h1 = CompileTimeHash::hash_runtime(timeframe);
        uint64_t h2 = CompileTimeHash::hash_runtime(indicator);
        uint64_t h3 = CompileTimeHash::hash_runtime(param_hash);
        return CompileTimeHash::combine_multi(h1, h2, h3);
    }
    
    /**
     * 编译期版本（用于常量字符串）
     */
    template<size_t N1, size_t N2>
    static constexpr uint64_t make_key_const(const char (&timeframe)[N1], const char (&indicator)[N2]) {
        uint64_t h1 = CompileTimeHash::hash(timeframe, N1 - 1);
        uint64_t h2 = CompileTimeHash::hash(indicator, N2 - 1);
        return CompileTimeHash::combine(h1, h2);
    }
};

} // namespace prophet::utils

// 便捷宏
#define CACHE_KEY_HASH(tf, ind) \
    prophet::utils::CacheKeyHasher::make_key_const(tf, ind)

