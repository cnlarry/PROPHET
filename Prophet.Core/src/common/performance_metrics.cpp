/*
 * ============================================================================
 * 文件名：performance_metrics.cpp
 * 功能说明：性能指标收集器实现
 * ============================================================================
 */

#include "prophet/common/performance_metrics.hpp"
#include <algorithm>
#include <sstream>
#include <iomanip>
#include <ctime>

namespace prophet::utils {

// ============================================================================
// 辅助函数
// ============================================================================

static std::string formatTimestamp(uint64_t timestamp_ms) {
    time_t seconds = timestamp_ms / 1000;
    std::tm tm_info;
#ifdef _WIN32
    localtime_s(&tm_info, &seconds);
#else
    localtime_r(&seconds, &tm_info);
#endif
    
    char buffer[64];
    strftime(buffer, sizeof(buffer), "%Y-%m-%d %H:%M:%S", &tm_info);
    return std::string(buffer);
}

static uint64_t getCurrentTimestampMs() {
    return std::chrono::duration_cast<std::chrono::milliseconds>(
        std::chrono::system_clock::now().time_since_epoch()
    ).count();
}

// ============================================================================
// 获取统计对象
// ============================================================================

const PerformanceMetrics::CacheStats& PerformanceMetrics::getCacheStats(
    const std::string& cache_name) const {
    std::lock_guard<std::mutex> lock(mutex_);
    auto it = cache_stats_.find(cache_name);
    if (it != cache_stats_.end()) {
        return it->second;
    }
    // 返回默认值
    static CacheStats default_stats;
    return default_stats;
}

PerformanceMetrics::CacheStats& PerformanceMetrics::getCacheStats(
    const std::string& cache_name) {
    std::lock_guard<std::mutex> lock(mutex_);
    return cache_stats_[cache_name];
}

const PerformanceMetrics::IndicatorStats& PerformanceMetrics::getIndicatorStats(
    const std::string& indicator_name) const {
    std::lock_guard<std::mutex> lock(mutex_);
    auto it = indicator_stats_.find(indicator_name);
    if (it != indicator_stats_.end()) {
        return it->second;
    }
    static IndicatorStats default_stats;
    return default_stats;
}

PerformanceMetrics::IndicatorStats& PerformanceMetrics::getIndicatorStats(
    const std::string& indicator_name) {
    std::lock_guard<std::mutex> lock(mutex_);
    return indicator_stats_[indicator_name];
}

std::unordered_map<std::string, std::pair<uint64_t, double>>
PerformanceMetrics::getAllIndicatorStatsSnapshot() const {
    std::lock_guard<std::mutex> lock(mutex_);
    std::unordered_map<std::string, std::pair<uint64_t, double>> snapshot;
    for (const auto& [name, stats] : indicator_stats_) {
        snapshot[name] = {stats.calculation_count.load(), stats.avg_time_ms()};
    }
    return snapshot;
}

// ============================================================================
// 快照
// ============================================================================

PerformanceMetrics::Snapshot PerformanceMetrics::takeSnapshot() const {
    Snapshot snapshot;
    snapshot.timestamp_ms = getCurrentTimestampMs();
    
    // 缓存统计（合并所有缓存）
    uint64_t total_hits = 0;
    uint64_t total_misses = 0;
    {
        std::lock_guard<std::mutex> lock(mutex_);
        for (const auto& [name, stats] : cache_stats_) {
            total_hits += stats.hits.load();
            total_misses += stats.misses.load();
        }
    }
    snapshot.cache.hits = total_hits;
    snapshot.cache.misses = total_misses;
    snapshot.cache.hit_rate = total_hits + total_misses > 0 
        ? static_cast<double>(total_hits) / (total_hits + total_misses) 
        : 0.0;
    
    // SIMD统计
    snapshot.simd.simd_calls = simd_stats_.simd_calls.load();
    snapshot.simd.scalar_calls = simd_stats_.scalar_calls.load();
    snapshot.simd.usage_rate = simd_stats_.simd_usage_rate();
    
    // 执行路径
    snapshot.execution_path.jit_executions = execution_path_stats_.jit_executions.load();
    snapshot.execution_path.vm_executions = execution_path_stats_.vm_executions.load();
    snapshot.execution_path.ast_executions = execution_path_stats_.ast_executions.load();
    snapshot.execution_path.jit_ratio = execution_path_stats_.jit_ratio();
    snapshot.execution_path.vm_ratio = execution_path_stats_.vm_ratio();
    snapshot.execution_path.ast_ratio = execution_path_stats_.ast_ratio();
    
    // 线程池
    snapshot.thread_pool.tasks_completed = thread_pool_stats_.tasks_completed.load();
    snapshot.thread_pool.tasks_pending = thread_pool_stats_.tasks_pending.load();
    snapshot.thread_pool.utilization_rate = thread_pool_stats_.utilization_rate();
    
    // 内存
    snapshot.memory.current_bytes = memory_stats_.current_memory_bytes();
    snapshot.memory.peak_bytes = memory_stats_.peak_memory_bytes.load();
    
    // Top 10 指标（按平均耗时排序）
    {
        std::lock_guard<std::mutex> lock(mutex_);
        std::vector<std::pair<std::string, double>> indicators;
        for (const auto& [name, stats] : indicator_stats_) {
            if (stats.calculation_count.load() > 0) {
                indicators.push_back({name, stats.avg_time_ms()});
            }
        }
        
        std::sort(indicators.begin(), indicators.end(),
            [](const auto& a, const auto& b) { return a.second > b.second; });
        
        snapshot.top_indicators.reserve(std::min(size_t(10), indicators.size()));
        for (size_t i = 0; i < std::min(size_t(10), indicators.size()); ++i) {
            snapshot.top_indicators.push_back(indicators[i]);
        }
    }
    
    return snapshot;
}

// ============================================================================
// 报告生成
// ============================================================================

void PerformanceMetrics::printReport(std::ostream& os) const {
    os << "\n";
    os << "╔════════════════════════════════════════════════════════════════════════╗\n";
    os << "║          PROPHET 核心引擎性能监控报告 (增强版)                         ║\n";
    os << "╠════════════════════════════════════════════════════════════════════════╣\n";
    
    // 1. 缓存统计
    os << "║ 【缓存统计】                                                            ║\n";
    os << "╠════════════════════════════════════════════════════════════════════════╣\n";
    {
        std::lock_guard<std::mutex> lock(mutex_);
        for (const auto& [name, stats] : cache_stats_) {
            uint64_t hits = stats.hits.load();
            uint64_t misses = stats.misses.load();
            double hit_rate = stats.hit_rate();
            
            os << "║ " << std::left << std::setw(20) << name
               << " 命中: " << std::setw(8) << hits
               << " 未命中: " << std::setw(8) << misses
               << " 命中率: " << std::fixed << std::setprecision(2) 
               << std::setw(6) << (hit_rate * 100) << "% ║\n";
        }
    }
    
    // 2. SIMD统计
    os << "╠════════════════════════════════════════════════════════════════════════╣\n";
    os << "║ 【SIMD统计】                                                            ║\n";
    os << "╠════════════════════════════════════════════════════════════════════════╣\n";
    {
        uint64_t simd_calls = simd_stats_.simd_calls.load();
        uint64_t scalar_calls = simd_stats_.scalar_calls.load();
        double usage_rate = simd_stats_.simd_usage_rate();
        
        os << "║ SIMD调用:   " << std::setw(10) << simd_calls << " 次"
           << "    标量调用:   " << std::setw(10) << scalar_calls << " 次       ║\n";
        os << "║ SIMD使用率: " << std::fixed << std::setprecision(2) 
           << std::setw(6) << (usage_rate * 100) << "%"
           << "                                                      ║\n";
    }
    
    // 3. 执行路径统计
    os << "╠════════════════════════════════════════════════════════════════════════╣\n";
    os << "║ 【执行路径统计】                                                        ║\n";
    os << "╠════════════════════════════════════════════════════════════════════════╣\n";
    {
        uint64_t jit = execution_path_stats_.jit_executions.load();
        uint64_t vm = execution_path_stats_.vm_executions.load();
        uint64_t ast = execution_path_stats_.ast_executions.load();
        uint64_t total = jit + vm + ast;
        
        os << "║ JIT执行:    " << std::setw(10) << jit 
           << " 次  (" << std::fixed << std::setprecision(1) 
           << std::setw(5) << (execution_path_stats_.jit_ratio() * 100) << "%)              ║\n";
        os << "║ VM执行:     " << std::setw(10) << vm 
           << " 次  (" << std::fixed << std::setprecision(1) 
           << std::setw(5) << (execution_path_stats_.vm_ratio() * 100) << "%)              ║\n";
        os << "║ AST执行:    " << std::setw(10) << ast 
           << " 次  (" << std::fixed << std::setprecision(1) 
           << std::setw(5) << (execution_path_stats_.ast_ratio() * 100) << "%)              ║\n";
        os << "║ 总执行次数: " << std::setw(10) << total << " 次                                    ║\n";
    }
    
    // 4. 线程池统计
    os << "╠════════════════════════════════════════════════════════════════════════╣\n";
    os << "║ 【线程池统计】                                                          ║\n";
    os << "╠════════════════════════════════════════════════════════════════════════╣\n";
    {
        uint64_t submitted = thread_pool_stats_.tasks_submitted.load();
        uint64_t completed = thread_pool_stats_.tasks_completed.load();
        uint64_t pending = thread_pool_stats_.tasks_pending.load();
        double utilization = thread_pool_stats_.utilization_rate();
        
        os << "║ 已提交: " << std::setw(10) << submitted 
           << "  已完成: " << std::setw(10) << completed 
           << "  待处理: " << std::setw(10) << pending << "║\n";
        os << "║ 线程利用率: " << std::fixed << std::setprecision(2) 
           << std::setw(6) << (utilization * 100) << "%"
           << "                                                ║\n";
    }
    
    // 5. 内存统计
    os << "╠════════════════════════════════════════════════════════════════════════╣\n";
    os << "║ 【内存统计】                                                            ║\n";
    os << "╠════════════════════════════════════════════════════════════════════════╣\n";
    {
        uint64_t current = memory_stats_.current_memory_bytes();
        uint64_t peak = memory_stats_.peak_memory_bytes.load();
        
        os << "║ 当前内存: " << std::setw(10) << (current / 1024 / 1024) << " MB"
           << "  峰值内存: " << std::setw(10) << (peak / 1024 / 1024) << " MB           ║\n";
    }
    
    // 6. Top 10 指标
    os << "╠════════════════════════════════════════════════════════════════════════╣\n";
    os << "║ 【Top 10 最耗时指标】                                                   ║\n";
    os << "╠════════════════════════════════════════════════════════════════════════╣\n";
    os << "║ " << std::left << std::setw(25) << "指标名称"
       << std::setw(15) << "调用次数"
       << std::setw(15) << "平均耗时(ms)" 
       << std::setw(15) << "总耗时(ms)" << "║\n";
    os << "╠════════════════════════════════════════════════════════════════════════╣\n";
    {
        std::lock_guard<std::mutex> lock(mutex_);
        std::vector<std::tuple<std::string, uint64_t, double, double>> indicators;
        for (const auto& [name, stats] : indicator_stats_) {
            uint64_t count = stats.calculation_count.load();
            if (count > 0) {
                double avg_ms = stats.avg_time_ms();
                double total_ms = stats.total_time_ns.load() / 1e6;
                indicators.push_back({name, count, avg_ms, total_ms});
            }
        }
        
        std::sort(indicators.begin(), indicators.end(),
            [](const auto& a, const auto& b) { return std::get<3>(a) > std::get<3>(b); });
        
        for (size_t i = 0; i < std::min(size_t(10), indicators.size()); ++i) {
            const auto& [name, count, avg_ms, total_ms] = indicators[i];
            os << "║ " << std::left << std::setw(25) << name
               << std::right << std::setw(15) << count
               << std::setw(15) << std::fixed << std::setprecision(3) << avg_ms
               << std::setw(15) << std::fixed << std::setprecision(2) << total_ms << "║\n";
        }
    }
    
    os << "╚════════════════════════════════════════════════════════════════════════╝\n";
    os << "\n";
}

void PerformanceMetrics::exportJSON(std::ostream& os) const {
    Snapshot snapshot = takeSnapshot();
    
    os << "{\n";
    os << "  \"timestamp\": " << snapshot.timestamp_ms << ",\n";
    os << "  \"timestamp_str\": \"" << formatTimestamp(snapshot.timestamp_ms) << "\",\n";
    
    // 缓存
    os << "  \"cache\": {\n";
    os << "    \"hits\": " << snapshot.cache.hits << ",\n";
    os << "    \"misses\": " << snapshot.cache.misses << ",\n";
    os << "    \"hit_rate\": " << std::fixed << std::setprecision(4) << snapshot.cache.hit_rate << "\n";
    os << "  },\n";
    
    // SIMD
    os << "  \"simd\": {\n";
    os << "    \"simd_calls\": " << snapshot.simd.simd_calls << ",\n";
    os << "    \"scalar_calls\": " << snapshot.simd.scalar_calls << ",\n";
    os << "    \"usage_rate\": " << std::fixed << std::setprecision(4) << snapshot.simd.usage_rate << "\n";
    os << "  },\n";
    
    // 执行路径
    os << "  \"execution_path\": {\n";
    os << "    \"jit_executions\": " << snapshot.execution_path.jit_executions << ",\n";
    os << "    \"vm_executions\": " << snapshot.execution_path.vm_executions << ",\n";
    os << "    \"ast_executions\": " << snapshot.execution_path.ast_executions << ",\n";
    os << "    \"jit_ratio\": " << std::fixed << std::setprecision(4) << snapshot.execution_path.jit_ratio << ",\n";
    os << "    \"vm_ratio\": " << std::fixed << std::setprecision(4) << snapshot.execution_path.vm_ratio << ",\n";
    os << "    \"ast_ratio\": " << std::fixed << std::setprecision(4) << snapshot.execution_path.ast_ratio << "\n";
    os << "  },\n";
    
    // 线程池
    os << "  \"thread_pool\": {\n";
    os << "    \"tasks_completed\": " << snapshot.thread_pool.tasks_completed << ",\n";
    os << "    \"tasks_pending\": " << snapshot.thread_pool.tasks_pending << ",\n";
    os << "    \"utilization_rate\": " << std::fixed << std::setprecision(4) << snapshot.thread_pool.utilization_rate << "\n";
    os << "  },\n";
    
    // 内存
    os << "  \"memory\": {\n";
    os << "    \"current_bytes\": " << snapshot.memory.current_bytes << ",\n";
    os << "    \"peak_bytes\": " << snapshot.memory.peak_bytes << "\n";
    os << "  },\n";
    
    // Top指标
    os << "  \"top_indicators\": [\n";
    for (size_t i = 0; i < snapshot.top_indicators.size(); ++i) {
        const auto& [name, avg_ms] = snapshot.top_indicators[i];
        os << "    {\"name\": \"" << name << "\", \"avg_time_ms\": " 
           << std::fixed << std::setprecision(3) << avg_ms << "}";
        if (i < snapshot.top_indicators.size() - 1) os << ",";
        os << "\n";
    }
    os << "  ]\n";
    
    os << "}\n";
}

void PerformanceMetrics::exportCSV(std::ostream& os) const {
    Snapshot snapshot = takeSnapshot();
    
    // 表头
    os << "timestamp,cache_hits,cache_misses,cache_hit_rate,";
    os << "simd_calls,scalar_calls,simd_usage_rate,";
    os << "jit_executions,vm_executions,ast_executions,";
    os << "tasks_completed,tasks_pending,thread_utilization,";
    os << "memory_current_mb,memory_peak_mb\n";
    
    // 数据
    os << snapshot.timestamp_ms << ","
       << snapshot.cache.hits << ","
       << snapshot.cache.misses << ","
       << std::fixed << std::setprecision(4) << snapshot.cache.hit_rate << ","
       << snapshot.simd.simd_calls << ","
       << snapshot.simd.scalar_calls << ","
       << std::fixed << std::setprecision(4) << snapshot.simd.usage_rate << ","
       << snapshot.execution_path.jit_executions << ","
       << snapshot.execution_path.vm_executions << ","
       << snapshot.execution_path.ast_executions << ","
       << snapshot.thread_pool.tasks_completed << ","
       << snapshot.thread_pool.tasks_pending << ","
       << std::fixed << std::setprecision(4) << snapshot.thread_pool.utilization_rate << ","
       << (snapshot.memory.current_bytes / 1024 / 1024) << ","
       << (snapshot.memory.peak_bytes / 1024 / 1024) << "\n";
}

void PerformanceMetrics::reset() {
    std::lock_guard<std::mutex> lock(mutex_);
    
    for (auto& [name, stats] : cache_stats_) {
        stats.reset();
    }
    
    simd_stats_.reset();
    execution_path_stats_.reset();
    
    for (auto& [name, stats] : indicator_stats_) {
        stats.reset();
    }
    
    thread_pool_stats_.reset();
    memory_stats_.reset();
    
    snapshots_.clear();
}

void PerformanceMetrics::enableAutoSnapshot(uint64_t interval_ms) {
    snapshot_interval_ms_.store(interval_ms, std::memory_order_relaxed);
    auto_snapshot_enabled_.store(true, std::memory_order_relaxed);
}

void PerformanceMetrics::disableAutoSnapshot() {
    auto_snapshot_enabled_.store(false, std::memory_order_relaxed);
}

} // namespace prophet::utils

