/*
 * ============================================================================
 * 文件名：performance_metrics.hpp
 * 功能说明：增强的性能指标收集系统
 * 
 * 扩展原有的PerformanceMonitor，添加：
 * - 缓存命中率统计
 * - SIMD使用率统计
 * - JIT/VM/AST执行路径统计
 * - 指标计算性能统计
 * - 线程池利用率统计
 * 
 * 阶段1：基础设施建设
 * ============================================================================
 */

#pragma once

#include <string>
#include <unordered_map>
#include <atomic>
#include <chrono>
#include <mutex>
#include <vector>
#include <memory>
#include <cstdint>
#include <iostream>
#include <cmath>

namespace prophet::utils {

/**
 * 性能指标收集器（增强版）
 * 线程安全的性能数据收集
 */
class PerformanceMetrics {
public:
    // ========================================================================
    // 数据结构定义
    // ========================================================================
    
    /**
     * 缓存统计
     */
    struct CacheStats {
        std::atomic<uint64_t> hits{0};
        std::atomic<uint64_t> misses{0};
        std::atomic<uint64_t> evictions{0};
        std::atomic<size_t> current_size{0};
        
        double hit_rate() const {
            uint64_t total = hits.load() + misses.load();
            return total > 0 ? static_cast<double>(hits.load()) / total : 0.0;
        }
        
        void reset() {
            hits = 0;
            misses = 0;
            evictions = 0;
            current_size = 0;
        }
    };
    
    /**
     * SIMD统计
     */
    struct SIMDStats {
        std::atomic<uint64_t> simd_calls{0};
        std::atomic<uint64_t> scalar_calls{0};
        std::atomic<uint64_t> elements_processed{0};
        
        double simd_usage_rate() const {
            uint64_t total = simd_calls.load() + scalar_calls.load();
            return total > 0 ? static_cast<double>(simd_calls.load()) / total : 0.0;
        }
        
        void reset() {
            simd_calls = 0;
            scalar_calls = 0;
            elements_processed = 0;
        }
    };
    
    /**
     * 执行路径统计
     */
    struct ExecutionPathStats {
        std::atomic<uint64_t> jit_executions{0};
        std::atomic<uint64_t> vm_executions{0};
        std::atomic<uint64_t> ast_executions{0};
        
        uint64_t total_executions() const {
            return jit_executions.load() + vm_executions.load() + ast_executions.load();
        }
        
        double jit_ratio() const {
            uint64_t total = total_executions();
            return total > 0 ? static_cast<double>(jit_executions.load()) / total : 0.0;
        }
        
        double vm_ratio() const {
            uint64_t total = total_executions();
            return total > 0 ? static_cast<double>(vm_executions.load()) / total : 0.0;
        }
        
        double ast_ratio() const {
            uint64_t total = total_executions();
            return total > 0 ? static_cast<double>(ast_executions.load()) / total : 0.0;
        }
        
        void reset() {
            jit_executions = 0;
            vm_executions = 0;
            ast_executions = 0;
        }
    };
    
    /**
     * 指标计算统计
     */
    struct IndicatorStats {
        std::atomic<uint64_t> calculation_count{0};
        std::atomic<uint64_t> total_time_ns{0};
        std::atomic<uint64_t> min_time_ns{UINT64_MAX};
        std::atomic<uint64_t> max_time_ns{0};
        
        double avg_time_ns() const {
            uint64_t count = calculation_count.load();
            return count > 0 ? static_cast<double>(total_time_ns.load()) / count : 0.0;
        }
        
        double avg_time_ms() const {
            return avg_time_ns() / 1e6;
        }
        
        void reset() {
            calculation_count = 0;
            total_time_ns = 0;
            min_time_ns = UINT64_MAX;
            max_time_ns = 0;
        }
    };
    
    /**
     * 线程池统计
     */
    struct ThreadPoolStats {
        std::atomic<uint64_t> tasks_submitted{0};
        std::atomic<uint64_t> tasks_completed{0};
        std::atomic<uint64_t> tasks_pending{0};
        std::atomic<uint64_t> active_threads{0};
        size_t total_threads{0};
        
        double utilization_rate() const {
            return total_threads > 0 
                ? static_cast<double>(active_threads.load()) / total_threads 
                : 0.0;
        }
        
        void reset() {
            tasks_submitted = 0;
            tasks_completed = 0;
            tasks_pending = 0;
            active_threads = 0;
        }
    };
    
    /**
     * 内存统计
     */
    struct MemoryStats {
        std::atomic<uint64_t> allocations{0};
        std::atomic<uint64_t> deallocations{0};
        std::atomic<uint64_t> bytes_allocated{0};
        std::atomic<uint64_t> bytes_deallocated{0};
        std::atomic<uint64_t> peak_memory_bytes{0};
        
        uint64_t current_memory_bytes() const {
            return bytes_allocated.load() - bytes_deallocated.load();
        }
        
        void reset() {
            allocations = 0;
            deallocations = 0;
            bytes_allocated = 0;
            bytes_deallocated = 0;
            peak_memory_bytes = 0;
        }
    };
    
    /**
     * 快照数据（某一时刻的完整性能指标）
     */
    struct Snapshot {
        uint64_t timestamp_ms;
        
        // 缓存数据
        struct {
            uint64_t hits;
            uint64_t misses;
            double hit_rate;
        } cache;
        
        // SIMD数据
        struct {
            uint64_t simd_calls;
            uint64_t scalar_calls;
            double usage_rate;
        } simd;
        
        // 执行路径
        struct {
            uint64_t jit_executions;
            uint64_t vm_executions;
            uint64_t ast_executions;
            double jit_ratio;
            double vm_ratio;
            double ast_ratio;
        } execution_path;
        
        // 线程池
        struct {
            uint64_t tasks_completed;
            uint64_t tasks_pending;
            double utilization_rate;
        } thread_pool;
        
        // 内存
        struct {
            uint64_t current_bytes;
            uint64_t peak_bytes;
        } memory;
        
        // 指标计算（top 10）
        std::vector<std::pair<std::string, double>> top_indicators;
    };
    
    // ========================================================================
    // 公共接口
    // ========================================================================
    
    /**
     * 获取单例实例
     */
    static PerformanceMetrics& instance() {
        static PerformanceMetrics instance;
        return instance;
    }
    
    // ------------------------------------------------------------------------
    // 缓存指标
    // ------------------------------------------------------------------------
    
    void recordCacheHit(const std::string& cache_name = "default") {
        getCacheStats(cache_name).hits.fetch_add(1, std::memory_order_relaxed);
    }
    
    void recordCacheMiss(const std::string& cache_name = "default") {
        getCacheStats(cache_name).misses.fetch_add(1, std::memory_order_relaxed);
    }
    
    void recordCacheEviction(const std::string& cache_name = "default") {
        getCacheStats(cache_name).evictions.fetch_add(1, std::memory_order_relaxed);
    }
    
    void updateCacheSize(size_t size, const std::string& cache_name = "default") {
        getCacheStats(cache_name).current_size.store(size, std::memory_order_relaxed);
    }
    
    const CacheStats& getCacheStats(const std::string& cache_name = "default") const;
    
    // ------------------------------------------------------------------------
    // SIMD指标
    // ------------------------------------------------------------------------
    
    void recordSIMDCall(uint64_t elements = 0) {
        simd_stats_.simd_calls.fetch_add(1, std::memory_order_relaxed);
        if (elements > 0) {
            simd_stats_.elements_processed.fetch_add(elements, std::memory_order_relaxed);
        }
    }
    
    void recordScalarCall(uint64_t elements = 0) {
        simd_stats_.scalar_calls.fetch_add(1, std::memory_order_relaxed);
        if (elements > 0) {
            simd_stats_.elements_processed.fetch_add(elements, std::memory_order_relaxed);
        }
    }
    
    const SIMDStats& getSIMDStats() const { return simd_stats_; }
    
    // ------------------------------------------------------------------------
    // 执行路径指标
    // ------------------------------------------------------------------------
    
    void recordJITExecution() {
        execution_path_stats_.jit_executions.fetch_add(1, std::memory_order_relaxed);
    }
    
    void recordVMExecution() {
        execution_path_stats_.vm_executions.fetch_add(1, std::memory_order_relaxed);
    }
    
    void recordASTExecution() {
        execution_path_stats_.ast_executions.fetch_add(1, std::memory_order_relaxed);
    }
    
    const ExecutionPathStats& getExecutionPathStats() const { return execution_path_stats_; }
    
    // ------------------------------------------------------------------------
    // 指标计算性能
    // ------------------------------------------------------------------------
    
    void recordIndicatorCalculation(const std::string& indicator_name, uint64_t time_ns) {
        auto& stats = getIndicatorStats(indicator_name);
        stats.calculation_count.fetch_add(1, std::memory_order_relaxed);
        stats.total_time_ns.fetch_add(time_ns, std::memory_order_relaxed);
        
        // 更新最小值
        uint64_t current_min = stats.min_time_ns.load(std::memory_order_relaxed);
        while (time_ns < current_min && 
               !stats.min_time_ns.compare_exchange_weak(current_min, time_ns, std::memory_order_relaxed));
        
        // 更新最大值
        uint64_t current_max = stats.max_time_ns.load(std::memory_order_relaxed);
        while (time_ns > current_max && 
               !stats.max_time_ns.compare_exchange_weak(current_max, time_ns, std::memory_order_relaxed));
    }
    
    const IndicatorStats& getIndicatorStats(const std::string& indicator_name) const;
    
    // 获取所有指标的快照（拷贝原子值）
    std::unordered_map<std::string, std::pair<uint64_t, double>> getAllIndicatorStatsSnapshot() const;
    
    // ------------------------------------------------------------------------
    // 线程池指标
    // ------------------------------------------------------------------------
    
    void recordTaskSubmitted() {
        thread_pool_stats_.tasks_submitted.fetch_add(1, std::memory_order_relaxed);
        thread_pool_stats_.tasks_pending.fetch_add(1, std::memory_order_relaxed);
    }
    
    void recordTaskCompleted() {
        thread_pool_stats_.tasks_completed.fetch_add(1, std::memory_order_relaxed);
        thread_pool_stats_.tasks_pending.fetch_sub(1, std::memory_order_relaxed);
    }
    
    void setThreadPoolSize(size_t size) {
        thread_pool_stats_.total_threads = size;
    }
    
    void updateActiveThreads(uint64_t count) {
        thread_pool_stats_.active_threads.store(count, std::memory_order_relaxed);
    }
    
    const ThreadPoolStats& getThreadPoolStats() const { return thread_pool_stats_; }
    
    // ------------------------------------------------------------------------
    // 内存指标
    // ------------------------------------------------------------------------
    
    void recordAllocation(uint64_t bytes) {
        memory_stats_.allocations.fetch_add(1, std::memory_order_relaxed);
        memory_stats_.bytes_allocated.fetch_add(bytes, std::memory_order_relaxed);
        
        // 更新峰值
        uint64_t current = memory_stats_.current_memory_bytes();
        uint64_t peak = memory_stats_.peak_memory_bytes.load(std::memory_order_relaxed);
        while (current > peak && 
               !memory_stats_.peak_memory_bytes.compare_exchange_weak(peak, current, std::memory_order_relaxed));
    }
    
    void recordDeallocation(uint64_t bytes) {
        memory_stats_.deallocations.fetch_add(1, std::memory_order_relaxed);
        memory_stats_.bytes_deallocated.fetch_add(bytes, std::memory_order_relaxed);
    }
    
    const MemoryStats& getMemoryStats() const { return memory_stats_; }
    
    // ------------------------------------------------------------------------
    // 快照和报告
    // ------------------------------------------------------------------------
    
    /**
     * 创建当前性能指标快照
     */
    Snapshot takeSnapshot() const;
    
    /**
     * 打印详细报告
     */
    void printReport(std::ostream& os = std::cout) const;
    
    /**
     * 导出为JSON
     */
    void exportJSON(std::ostream& os) const;
    
    /**
     * 导出为CSV
     */
    void exportCSV(std::ostream& os) const;
    
    /**
     * 重置所有统计数据
     */
    void reset();
    
    /**
     * 启用/禁用自动快照
     * @param interval_ms 快照间隔（毫秒）
     */
    void enableAutoSnapshot(uint64_t interval_ms);
    void disableAutoSnapshot();
    
    /**
     * 获取历史快照
     */
    const std::vector<Snapshot>& getSnapshots() const { return snapshots_; }
    
private:
    PerformanceMetrics() = default;
    ~PerformanceMetrics() = default;
    
    // 禁止拷贝
    PerformanceMetrics(const PerformanceMetrics&) = delete;
    PerformanceMetrics& operator=(const PerformanceMetrics&) = delete;
    
    // 获取或创建缓存统计
    CacheStats& getCacheStats(const std::string& cache_name);
    
    // 获取或创建指标统计
    IndicatorStats& getIndicatorStats(const std::string& indicator_name);
    
    // 成员变量
    mutable std::mutex mutex_;
    
    // 各类统计数据
    std::unordered_map<std::string, CacheStats> cache_stats_;
    SIMDStats simd_stats_;
    ExecutionPathStats execution_path_stats_;
    std::unordered_map<std::string, IndicatorStats> indicator_stats_;
    ThreadPoolStats thread_pool_stats_;
    MemoryStats memory_stats_;
    
    // 快照历史
    std::vector<Snapshot> snapshots_;
    static constexpr size_t MAX_SNAPSHOTS = 1000;
    
    // 自动快照
    std::atomic<bool> auto_snapshot_enabled_{false};
    std::atomic<uint64_t> snapshot_interval_ms_{0};
};

/**
 * RAII计时器 - 用于指标计算
 */
class IndicatorTimer {
public:
    IndicatorTimer(const std::string& indicator_name)
        : indicator_name_(indicator_name)
        , start_(std::chrono::high_resolution_clock::now()) {}
    
    ~IndicatorTimer() {
        auto end = std::chrono::high_resolution_clock::now();
        auto duration_ns = std::chrono::duration_cast<std::chrono::nanoseconds>(
            end - start_).count();
        PerformanceMetrics::instance().recordIndicatorCalculation(
            indicator_name_, static_cast<uint64_t>(duration_ns));
    }
    
private:
    std::string indicator_name_;
    std::chrono::high_resolution_clock::time_point start_;
};

// 便捷宏
#define PERF_INDICATOR_TIMER(name) \
    prophet::utils::IndicatorTimer __perf_timer_##__LINE__(name)

} // namespace prophet::utils

