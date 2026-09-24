/*
 * ============================================================================
 * 文件名：simd_config.hpp
 * 功能说明：SIMD配置和CPU特性检测 - P3优化
 * 
 * 目标：检测CPU支持的SIMD指令集，为向量化计算提供基础
 * 
 * 支持的指令集：
 * - SSE2 (基础，几乎所有x86_64 CPU都支持)
 * - AVX (256位寄存器)
 * - AVX2 (增强的256位运算)
 * - AVX512 (512位寄存器，高端CPU)
 * 
 * P3优化核心组件
 * ============================================================================
 */

#pragma once

#include <cstdint>
#include <string>

namespace prophet {
namespace simd {

/**
 * CPU特性标志
 * 
 * 用于运行时检测CPU支持的SIMD指令集
 */
struct CPUFeatures {
    bool has_sse2 = false;     // SSE2 (128位)
    bool has_avx = false;      // AVX (256位)
    bool has_avx2 = false;     // AVX2 (256位增强)
    bool has_avx512f = false;  // AVX-512 Foundation (512位)
    
    /**
     * 获取人类可读的特性描述
     */
    std::string toString() const;
    
    /**
     * 获取最佳可用指令集
     */
    std::string getBestInstructionSet() const {
        if (has_avx512f) return "AVX-512";
        if (has_avx2) return "AVX2";
        if (has_avx) return "AVX";
        if (has_sse2) return "SSE2";
        return "None";
    }
    
    /**
     * 获取向量宽度（可同时处理的double数量）
     */
    int getVectorWidth() const {
        if (has_avx512f) return 8;  // 512位 / 64位 = 8个double
        if (has_avx2 || has_avx) return 4;  // 256位 / 64位 = 4个double
        if (has_sse2) return 2;  // 128位 / 64位 = 2个double
        return 1;  // 标量
    }
};

/**
 * 检测CPU特性
 * 
 * 使用CPUID指令查询CPU支持的功能
 * 
 * @return CPU特性结构体
 */
CPUFeatures detectCPUFeatures();

/**
 * SIMD配置单例
 * 
 * 管理SIMD全局配置：
 * - 是否启用SIMD
 * - CPU特性信息
 * - 运行时统计
 */
class Config {
public:
    /**
     * 获取单例实例
     */
    static Config& instance();
    
    /**
     * 检查SIMD是否启用
     */
    bool isEnabled() const { return enabled_; }
    
    /**
     * 启用/禁用SIMD
     * @param enable true=启用，false=禁用（使用标量回退）
     */
    void setEnabled(bool enable) { enabled_ = enable; }
    
    /**
     * 获取CPU特性
     */
    const CPUFeatures& features() const { return features_; }
    
    /**
     * 获取配置摘要（用于日志）
     */
    std::string getSummary() const;
    
    /**
     * 获取SIMD统计信息
     */
    struct Statistics {
        uint64_t simd_calls = 0;       // SIMD路径调用次数
        uint64_t scalar_calls = 0;     // 标量路径调用次数
        uint64_t elements_processed = 0; // 处理的元素总数
        
        double simd_ratio() const {
            uint64_t total = simd_calls + scalar_calls;
            return total > 0 ? (double)simd_calls / total : 0.0;
        }
    };
    
    Statistics getStatistics() const { return stats_; }
    void recordSIMDCall(uint64_t elements = 0);
    void recordScalarCall(uint64_t elements = 0);
    
private:
    Config();  // 私有构造函数（单例模式）
    ~Config() = default;
    
    // 禁止拷贝和移动
    Config(const Config&) = delete;
    Config& operator=(const Config&) = delete;
    
    bool enabled_;
    CPUFeatures features_;
    Statistics stats_;
};

/**
 * SIMD初始化辅助类（RAII）
 * 
 * 使用示例：
 * ```cpp
 * {
 *     SIMDInitializer init;  // 自动检测并初始化
 *     
 *     if (simd::Config::instance().isEnabled()) {
 *         // 使用SIMD
 *     } else {
 *         // 使用标量回退
 *     }
 * }  // 自动清理
 * ```
 */
class SIMDInitializer {
public:
    SIMDInitializer(bool force_enable = true);
    ~SIMDInitializer();
    
    bool isAvailable() const { return available_; }
    const CPUFeatures& features() const { return features_; }
    
private:
    bool available_;
    CPUFeatures features_;
};

} // namespace simd
} // namespace prophet

