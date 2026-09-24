/*
 * ============================================================================
 * 文件名：performance_monitor.hpp
 * 功能说明：性能监控工具类
 * 
 * P0优化：添加性能监控系统
 * 用于追踪各组件的执行时间，识别性能瓶颈
 * 
 * 使用方法：
 * ```cpp
 * PerformanceMonitor& monitor = PerformanceMonitor::instance();
 * 
 * // 方式1：使用RAII自动计时
 * {
 *     PerformanceMonitor::Timer timer(monitor, "component_name");
 *     // ... 执行代码 ...
 * }  // 析构时自动记录耗时
 * 
 * // 方式2：手动记录
 * auto start = std::chrono::high_resolution_clock::now();
 * // ... 执行代码 ...
 * auto end = std::chrono::high_resolution_clock::now();
 * monitor.record("component_name", duration_ns);
 * 
 * // 查看报告
 * monitor.report();
 * monitor.exportToJson("perf_report.json");
 * ```
 * ============================================================================
 */

#pragma once

#include <string>
#include <unordered_map>
#include <chrono>
#include <iostream>
#include <iomanip>
#include <cstdint>

namespace prophet::utils {

/**
 * 性能监控器（单例模式）
 * 线程安全：当前实现不是线程安全的，如需多线程使用请添加互斥锁
 */
class PerformanceMonitor {
public:
    /**
     * 性能统计数据
     */
    struct Stats {
        uint64_t call_count = 0;      // 调用次数
        uint64_t total_time_ns = 0;   // 总耗时（纳秒）
        uint64_t min_time_ns = UINT64_MAX;  // 最小耗时
        uint64_t max_time_ns = 0;     // 最大耗时
        
        // 计算平均耗时（纳秒）
        double avg_time_ns() const {
            return call_count > 0 ? static_cast<double>(total_time_ns) / call_count : 0.0;
        }
        
        // 转换为毫秒
        double total_ms() const { return total_time_ns / 1e6; }
        double avg_ms() const { return avg_time_ns() / 1e6; }
        double min_ms() const { return min_time_ns / 1e6; }
        double max_ms() const { return max_time_ns / 1e6; }
    };
    
    /**
     * RAII计时器
     * 构造时开始计时，析构时自动记录结果
     */
    class Timer {
    public:
        Timer(PerformanceMonitor& monitor, const std::string& name)
            : monitor_(monitor), name_(name) {
            start_ = std::chrono::high_resolution_clock::now();
        }
        
        ~Timer() {
            auto end = std::chrono::high_resolution_clock::now();
            auto duration_ns = std::chrono::duration_cast<std::chrono::nanoseconds>(
                end - start_).count();
            monitor_.record(name_, static_cast<uint64_t>(duration_ns));
        }
        
    private:
        PerformanceMonitor& monitor_;
        std::string name_;
        std::chrono::high_resolution_clock::time_point start_;
    };
    
    /**
     * 获取单例实例
     */
    static PerformanceMonitor& instance() {
        static PerformanceMonitor instance;
        return instance;
    }
    
    /**
     * 记录性能数据
     * @param name 组件名称
     * @param time_ns 耗时（纳秒）
     */
    void record(const std::string& name, uint64_t time_ns) {
        auto& stat = stats_[name];
        stat.call_count++;
        stat.total_time_ns += time_ns;
        stat.min_time_ns = std::min(stat.min_time_ns, time_ns);
        stat.max_time_ns = std::max(stat.max_time_ns, time_ns);
    }
    
    /**
     * 获取指定组件的统计信息
     */
    const Stats* getStats(const std::string& name) const {
        auto it = stats_.find(name);
        return it != stats_.end() ? &it->second : nullptr;
    }
    
    /**
     * 获取所有统计信息
     */
    const std::unordered_map<std::string, Stats>& getAllStats() const {
        return stats_;
    }
    
    /**
     * 清除所有统计数据
     */
    void reset() {
        stats_.clear();
    }
    
    /**
     * 打印性能报告到控制台
     */
    void report(std::ostream& os = std::cout) const {
        os << "\n╔══════════════════════════════════════════════════════════════════════╗\n";
        os << "║               PROPHET 核心引擎性能监控报告                            ║\n";
        os << "╠══════════════════════════════════════════════════════════════════════╣\n";
        os << std::left
           << std::setw(30) << "║ 组件名称"
           << std::setw(12) << "调用次数"
           << std::setw(12) << "总耗时(ms)"
           << std::setw(12) << "平均(ms)" << " ║\n";
        os << "╠══════════════════════════════════════════════════════════════════════╣\n";
        
        for (const auto& [name, stat] : stats_) {
            os << "║ " 
               << std::left << std::setw(28) << name
               << std::right << std::setw(10) << stat.call_count
               << std::setw(14) << std::fixed << std::setprecision(2) << stat.total_ms()
               << std::setw(14) << std::fixed << std::setprecision(3) << stat.avg_ms()
               << " ║\n";
        }
        
        os << "╠══════════════════════════════════════════════════════════════════════╣\n";
        os << "║ 详细统计（最小/最大耗时）                                            ║\n";
        os << "╠══════════════════════════════════════════════════════════════════════╣\n";
        
        for (const auto& [name, stat] : stats_) {
            os << "║ " << std::left << std::setw(28) << name
               << " Min: " << std::setw(8) << std::fixed << std::setprecision(3) << stat.min_ms() << "ms"
               << " Max: " << std::setw(8) << std::fixed << std::setprecision(3) << stat.max_ms() << "ms ║\n";
        }
        
        os << "╚══════════════════════════════════════════════════════════════════════╝\n\n";
    }
    
    /**
     * 导出为JSON格式
     * @param os 输出流
     */
    void exportToJson(std::ostream& os) const {
        os << "{\n";
        os << "  \"performance_report\": [\n";
        
        bool first = true;
        for (const auto& [name, stat] : stats_) {
            if (!first) os << ",\n";
            first = false;
            
            os << "    {\n";
            os << "      \"component\": \"" << name << "\",\n";
            os << "      \"call_count\": " << stat.call_count << ",\n";
            os << "      \"total_time_ms\": " << std::fixed << std::setprecision(3) << stat.total_ms() << ",\n";
            os << "      \"avg_time_ms\": " << std::fixed << std::setprecision(3) << stat.avg_ms() << ",\n";
            os << "      \"min_time_ms\": " << std::fixed << std::setprecision(3) << stat.min_ms() << ",\n";
            os << "      \"max_time_ms\": " << std::fixed << std::setprecision(3) << stat.max_ms() << "\n";
            os << "    }";
        }
        
        os << "\n  ]\n";
        os << "}\n";
    }
    
private:
    PerformanceMonitor() = default;
    ~PerformanceMonitor() = default;
    
    // 禁止拷贝和赋值
    PerformanceMonitor(const PerformanceMonitor&) = delete;
    PerformanceMonitor& operator=(const PerformanceMonitor&) = delete;
    
    std::unordered_map<std::string, Stats> stats_;
};

} // namespace prophet::utils

