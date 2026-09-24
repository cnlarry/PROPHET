/*
 * ============================================================================
 * 文件名：benchmark.hpp
 * 功能说明：性能基准测试框架
 * 
 * 提供微观和宏观基准测试工具
 * ============================================================================
 */

#pragma once

#include <string>
#include <vector>
#include <functional>
#include <chrono>
#include <iostream>
#include <iomanip>
#include <numeric>
#include <algorithm>

namespace prophet::benchmark {

/**
 * 基准测试结果
 */
struct BenchmarkResult {
    std::string name;
    size_t iterations;
    double avg_time_ms;
    double min_time_ms;
    double max_time_ms;
    double stddev_ms;
    double median_ms;
    double throughput;  // 操作/秒
    
    void print(std::ostream& os = std::cout) const {
        os << "【" << name << "】\n";
        os << "  迭代次数: " << iterations << "\n";
        os << "  平均耗时: " << std::fixed << std::setprecision(3) << avg_time_ms << " ms\n";
        os << "  最小耗时: " << std::fixed << std::setprecision(3) << min_time_ms << " ms\n";
        os << "  最大耗时: " << std::fixed << std::setprecision(3) << max_time_ms << " ms\n";
        os << "  标准差:   " << std::fixed << std::setprecision(3) << stddev_ms << " ms\n";
        os << "  中位数:   " << std::fixed << std::setprecision(3) << median_ms << " ms\n";
        if (throughput > 0) {
            os << "  吞吐量:   " << std::fixed << std::setprecision(0) << throughput << " ops/s\n";
        }
        os << "\n";
    }
};

/**
 * 基准测试运行器
 */
class BenchmarkRunner {
public:
    /**
     * 运行基准测试
     * @param name 测试名称
     * @param func 要测试的函数
     * @param iterations 迭代次数
     * @param warmup_iterations 预热迭代次数
     * @return 测试结果
     */
    static BenchmarkResult run(
        const std::string& name,
        std::function<void()> func,
        size_t iterations = 1000,
        size_t warmup_iterations = 100
    ) {
        // 预热
        for (size_t i = 0; i < warmup_iterations; ++i) {
            func();
        }
        
        // 实际测试
        std::vector<double> times_ms;
        times_ms.reserve(iterations);
        
        for (size_t i = 0; i < iterations; ++i) {
            auto start = std::chrono::high_resolution_clock::now();
            func();
            auto end = std::chrono::high_resolution_clock::now();
            
            auto duration_ns = std::chrono::duration_cast<std::chrono::nanoseconds>(
                end - start).count();
            times_ms.push_back(duration_ns / 1e6);
        }
        
        // 计算统计
        BenchmarkResult result;
        result.name = name;
        result.iterations = iterations;
        result.avg_time_ms = std::accumulate(times_ms.begin(), times_ms.end(), 0.0) / times_ms.size();
        result.min_time_ms = *std::min_element(times_ms.begin(), times_ms.end());
        result.max_time_ms = *std::max_element(times_ms.begin(), times_ms.end());
        
        // 标准差
        double sum_sq = 0.0;
        for (double t : times_ms) {
            double diff = t - result.avg_time_ms;
            sum_sq += diff * diff;
        }
        result.stddev_ms = std::sqrt(sum_sq / times_ms.size());
        
        // 中位数
        std::sort(times_ms.begin(), times_ms.end());
        result.median_ms = times_ms[times_ms.size() / 2];
        
        // 吞吐量（操作/秒）
        result.throughput = result.avg_time_ms > 0 
            ? 1000.0 / result.avg_time_ms 
            : 0.0;
        
        return result;
    }
    
    /**
     * 运行吞吐量基准测试
     * @param name 测试名称
     * @param func 要测试的函数（返回处理的元素数量）
     * @param duration_ms 测试持续时间（毫秒）
     * @return 测试结果
     */
    static BenchmarkResult runThroughput(
        const std::string& name,
        std::function<size_t()> func,
        size_t duration_ms = 5000
    ) {
        auto start = std::chrono::high_resolution_clock::now();
        auto end_time = start + std::chrono::milliseconds(duration_ms);
        
        size_t total_operations = 0;
        size_t iterations = 0;
        
        while (std::chrono::high_resolution_clock::now() < end_time) {
            total_operations += func();
            ++iterations;
        }
        
        auto actual_end = std::chrono::high_resolution_clock::now();
        auto actual_duration_ms = std::chrono::duration_cast<std::chrono::milliseconds>(
            actual_end - start).count();
        
        BenchmarkResult result;
        result.name = name;
        result.iterations = iterations;
        result.avg_time_ms = static_cast<double>(actual_duration_ms) / iterations;
        result.throughput = total_operations * 1000.0 / actual_duration_ms;
        result.min_time_ms = 0;
        result.max_time_ms = 0;
        result.stddev_ms = 0;
        result.median_ms = 0;
        
        return result;
    }
    
    /**
     * 对比两个实现
     * @param name1 实现1名称
     * @param func1 实现1
     * @param name2 实现2名称
     * @param func2 实现2
     * @param iterations 迭代次数
     */
    static void compare(
        const std::string& name1,
        std::function<void()> func1,
        const std::string& name2,
        std::function<void()> func2,
        size_t iterations = 1000
    ) {
        std::cout << "\n╔════════════════════════════════════════════════════════════════════════╗\n";
        std::cout << "║                       性能对比测试                                      ║\n";
        std::cout << "╚════════════════════════════════════════════════════════════════════════╝\n\n";
        
        auto result1 = run(name1, func1, iterations);
        auto result2 = run(name2, func2, iterations);
        
        result1.print();
        result2.print();
        
        // 对比
        double speedup = result1.avg_time_ms / result2.avg_time_ms;
        std::cout << "╔════════════════════════════════════════════════════════════════════════╗\n";
        std::cout << "║ 对比结果                                                                ║\n";
        std::cout << "╠════════════════════════════════════════════════════════════════════════╣\n";
        
        if (speedup > 1.0) {
            std::cout << "║ " << name2 << " 比 " << name1 << " 快 " 
                     << std::fixed << std::setprecision(2) << speedup << "x"
                     << std::string(40, ' ') << "║\n";
        } else {
            std::cout << "║ " << name1 << " 比 " << name2 << " 快 " 
                     << std::fixed << std::setprecision(2) << (1.0/speedup) << "x"
                     << std::string(40, ' ') << "║\n";
        }
        
        std::cout << "╚════════════════════════════════════════════════════════════════════════╝\n\n";
    }
};

/**
 * 基准测试套件
 */
class BenchmarkSuite {
public:
    /**
     * 添加基准测试
     */
    void add(const std::string& name, std::function<void()> func, 
             size_t iterations = 1000) {
        benchmarks_.push_back({name, func, iterations});
    }
    
    /**
     * 运行所有基准测试
     */
    std::vector<BenchmarkResult> runAll() {
        std::vector<BenchmarkResult> results;
        
        std::cout << "\n╔════════════════════════════════════════════════════════════════════════╗\n";
        std::cout << "║                   基准测试套件运行中...                                 ║\n";
        std::cout << "╚════════════════════════════════════════════════════════════════════════╝\n\n";
        
        for (size_t i = 0; i < benchmarks_.size(); ++i) {
            const auto& [name, func, iterations] = benchmarks_[i];
            
            std::cout << "[" << (i+1) << "/" << benchmarks_.size() << "] 运行: " 
                     << name << "...\n";
            
            auto result = BenchmarkRunner::run(name, func, iterations);
            results.push_back(result);
            
            result.print();
        }
        
        return results;
    }
    
    /**
     * 生成报告
     */
    void generateReport(const std::vector<BenchmarkResult>& results,
                       std::ostream& os = std::cout) {
        os << "\n╔════════════════════════════════════════════════════════════════════════╗\n";
        os << "║                      基准测试总结报告                                   ║\n";
        os << "╠════════════════════════════════════════════════════════════════════════╣\n";
        os << "║ " << std::left << std::setw(35) << "测试名称"
           << std::setw(15) << "平均耗时(ms)"
           << std::setw(18) << "吞吐量(ops/s)" << "║\n";
        os << "╠════════════════════════════════════════════════════════════════════════╣\n";
        
        for (const auto& result : results) {
            os << "║ " << std::left << std::setw(35) << result.name
               << std::right << std::setw(15) << std::fixed << std::setprecision(3) 
               << result.avg_time_ms
               << std::setw(18) << std::fixed << std::setprecision(0) 
               << result.throughput << "║\n";
        }
        
        os << "╚════════════════════════════════════════════════════════════════════════╝\n\n";
    }
    
private:
    struct Benchmark {
        std::string name;
        std::function<void()> func;
        size_t iterations;
    };
    
    std::vector<Benchmark> benchmarks_;
};

} // namespace prophet::benchmark

