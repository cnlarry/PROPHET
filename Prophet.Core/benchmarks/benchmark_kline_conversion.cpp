/*
 * ============================================================================
 * 文件名：benchmark_kline_conversion.cpp
 * 功能说明：K线转换性能基准测试
 * 
 * 测试场景：
 * 1. 无缓存 vs 有缓存
 * 2. 标量计算 vs SIMD向量化
 * 3. 单线程 vs 多线程
 * 
 * 测试数据：
 * - 100,000根1m K线（约70天）
 * - 转换目标：5m, 15m, 1h
 * ============================================================================
 */

#include "prophet/tools/kline_converter.hpp"
#include "prophet/cache/kline_conversion_cache.hpp"
#include "prophet/parallel/kline_converter.hpp"
#include "prophet/parallel/thread_pool.hpp"
#include "prophet/simd/kline_simd.hpp"
#include <iostream>
#include <chrono>
#include <vector>
#include <random>
#include <iomanip>

using namespace prophet;
using namespace prophet::tools::kline;
using namespace prophet::cache;
using namespace prophet::parallel;
using namespace prophet::simd;

// ============================================================================
// 生成测试数据
// ============================================================================

std::vector<Kline> generateTestKlines(size_t count, int64_t start_time_ms = 1609459200000) {
    std::vector<Kline> klines;
    klines.reserve(count);
    
    std::mt19937 rng(42); // 固定种子，保证可重复
    std::uniform_real_distribution<double> price_dist(40000.0, 50000.0);
    std::uniform_real_distribution<double> volume_dist(10.0, 100.0);
    std::uniform_real_distribution<double> volatility_dist(0.98, 1.02);
    
    double base_price = 45000.0;
    
    for (size_t i = 0; i < count; ++i) {
        Kline k;
        k.open_time = start_time_ms + static_cast<int64_t>(i) * 60 * 1000; // 1分钟间隔
        k.close_time = k.open_time + 59 * 1000;
        
        k.open = base_price;
        k.high = base_price * volatility_dist(rng);
        k.low = base_price / volatility_dist(rng);
        k.close = price_dist(rng);
        k.volume = volume_dist(rng);
        
        base_price = k.close; // 下一根K线从上一根收盘价开始
        
        klines.push_back(k);
    }
    
    return klines;
}

// ============================================================================
// 测试辅助函数
// ============================================================================

struct BenchmarkResult {
    std::string test_name;
    size_t iterations;
    double total_time_ms;
    double avg_time_ms;
    double throughput_klines_per_sec;
    size_t output_count;
    
    void print() const {
        std::cout << "  " << std::left << std::setw(40) << test_name << ": "
                  << std::fixed << std::setprecision(2)
                  << std::right << std::setw(10) << avg_time_ms << " ms  "
                  << "(总计: " << total_time_ms << " ms, "
                  << "吞吐量: " << static_cast<int>(throughput_klines_per_sec) << " K线/秒, "
                  << "输出: " << output_count << " 根)"
                  << std::endl;
    }
};

template<typename Func>
BenchmarkResult runBenchmark(
    const std::string& name,
    Func func,
    size_t iterations,
    size_t input_kline_count
) {
    auto start = std::chrono::high_resolution_clock::now();
    
    size_t output_count = 0;
    for (size_t i = 0; i < iterations; ++i) {
        output_count = func();
    }
    
    auto end = std::chrono::high_resolution_clock::now();
    auto duration_ms = std::chrono::duration<double, std::milli>(end - start).count();
    
    BenchmarkResult result;
    result.test_name = name;
    result.iterations = iterations;
    result.total_time_ms = duration_ms;
    result.avg_time_ms = duration_ms / static_cast<double>(iterations);
    result.throughput_klines_per_sec = (static_cast<double>(input_kline_count * iterations)) / (duration_ms / 1000.0);
    result.output_count = output_count;
    
    return result;
}

// ============================================================================
// 基准测试 1：无缓存转换性能
// ============================================================================

void benchmark_no_cache(const std::vector<Kline>& klines_1m) {
    std::cout << "\n" << std::string(80, '=') << std::endl;
    std::cout << "基准测试 1: 无缓存转换性能" << std::endl;
    std::cout << std::string(80, '=') << std::endl;
    
    const size_t iterations = 100;
    
    // 1m -> 5m
    auto result_5m = runBenchmark(
        "1m -> 5m (100次)",
        [&]() {
            auto result = Converter::convert(klines_1m, 1, 5, 0);
            return result.size();
        },
        iterations,
        klines_1m.size()
    );
    result_5m.print();
    
    // 1m -> 15m
    auto result_15m = runBenchmark(
        "1m -> 15m (100次)",
        [&]() {
            auto result = Converter::convert(klines_1m, 1, 15, 0);
            return result.size();
        },
        iterations,
        klines_1m.size()
    );
    result_15m.print();
    
    // 1m -> 1h (60m)
    auto result_1h = runBenchmark(
        "1m -> 1h (100次)",
        [&]() {
            auto result = Converter::convert(klines_1m, 1, 60, 0);
            return result.size();
        },
        iterations,
        klines_1m.size()
    );
    result_1h.print();
}

// ============================================================================
// 基准测试 2：缓存效果测试
// ============================================================================

void benchmark_with_cache(const std::vector<Kline>& klines_1m) {
    std::cout << "\n" << std::string(80, '=') << std::endl;
    std::cout << "基准测试 2: 缓存效果测试" << std::endl;
    std::cout << std::string(80, '=') << std::endl;
    
    KlineConversionCache cache(100, 500);
    
    // 第一次转换（缓存未命中）
    ConversionCacheKey key_5m = createCacheKey("BTCUSDT", "1m", "5m", klines_1m);
    
    auto start = std::chrono::high_resolution_clock::now();
    auto result_5m = Converter::convert(klines_1m, 1, 5, 0);
    auto end = std::chrono::high_resolution_clock::now();
    auto time_no_cache = std::chrono::duration<double, std::milli>(end - start).count();
    
    cache.put(key_5m, result_5m);
    
    std::cout << "  第一次转换（无缓存）: " << std::fixed << std::setprecision(2) 
              << time_no_cache << " ms" << std::endl;
    
    // 第二次转换（缓存命中）
    start = std::chrono::high_resolution_clock::now();
    auto cached_result = cache.get(key_5m);
    end = std::chrono::high_resolution_clock::now();
    auto time_with_cache = std::chrono::duration<double, std::milli>(end - start).count();
    
    std::cout << "  第二次转换（缓存命中）: " << std::fixed << std::setprecision(2) 
              << time_with_cache << " ms" << std::endl;
    
    double speedup = time_no_cache / time_with_cache;
    std::cout << "  加速比: " << std::fixed << std::setprecision(1) 
              << speedup << "x" << std::endl;
    
    // 测试100次缓存命中的总耗时
    start = std::chrono::high_resolution_clock::now();
    for (int i = 0; i < 100; ++i) {
        auto result = cache.get(key_5m);
        (void)result;  // 避免未使用变量警告
    }
    end = std::chrono::high_resolution_clock::now();
    auto time_100_hits = std::chrono::duration<double, std::milli>(end - start).count();
    
    std::cout << "  100次缓存命中总耗时: " << std::fixed << std::setprecision(2) 
              << time_100_hits << " ms (平均: " << time_100_hits / 100.0 << " ms)" << std::endl;
    
    // 打印缓存统计
    auto stats = cache.getStatistics();
    std::cout << "\n  缓存统计:" << std::endl;
    std::cout << "    总请求: " << stats.total_requests << std::endl;
    std::cout << "    命中: " << stats.cache_hits << std::endl;
    std::cout << "    未命中: " << stats.cache_misses << std::endl;
    std::cout << "    命中率: " << std::fixed << std::setprecision(1) 
              << (stats.hit_rate * 100) << "%" << std::endl;
}

// ============================================================================
// 基准测试 3：SIMD 向量化效果
// ============================================================================

void benchmark_simd(const std::vector<Kline>& klines_1m) {
    std::cout << "\n" << std::string(80, '=') << std::endl;
    std::cout << "基准测试 3: SIMD 向量化效果" << std::endl;
    std::cout << std::string(80, '=') << std::endl;
    
    // 检测CPU特性
    const auto& features = getCPUFeatures();
    std::cout << "  CPU 支持:" << std::endl;
    std::cout << "    SSE4.2: " << (features.sse42_supported ? "是" : "否") << std::endl;
    std::cout << "    AVX2: " << (features.avx2_supported ? "是" : "否") << std::endl;
    std::cout << std::endl;
    
    // 测试不同大小的K线组合并性能
    std::vector<size_t> group_sizes = {5, 15, 60, 240};
    
    for (size_t group_size : group_sizes) {
        if (group_size > klines_1m.size()) continue;
        
        std::vector<Kline> test_group(klines_1m.begin(), klines_1m.begin() + static_cast<std::vector<Kline>::difference_type>(group_size));
        
        // SIMD 版本（自动选择最佳实现）
        auto simd_result = runBenchmark(
            "SIMD (自动) - " + std::to_string(group_size) + " 根K线 (10000次)",
            [&]() {
                mergeKlinesSIMD(test_group);
                return 1;
            },
            10000,
            group_size
        );
        simd_result.print();
        
        // 标量版本
        auto scalar_result = runBenchmark(
            "标量计算 - " + std::to_string(group_size) + " 根K线 (10000次)",
            [&]() {
                mergeKlinesScalar(test_group);
                return 1;
            },
            10000,
            group_size
        );
        scalar_result.print();
        
        double speedup = scalar_result.avg_time_ms / simd_result.avg_time_ms;
        std::cout << "  --> SIMD 加速比: " << std::fixed << std::setprecision(2) 
                  << speedup << "x" << std::endl;
        std::cout << std::endl;
    }
}

// ============================================================================
// 基准测试 4：多线程并行转换
// ============================================================================

void benchmark_parallel(const std::vector<Kline>& klines_1m) {
    std::cout << "\n" << std::string(80, '=') << std::endl;
    std::cout << "基准测试 4: 多线程并行转换" << std::endl;
    std::cout << std::string(80, '=') << std::endl;
    
    ThreadPool thread_pool(4);
    ParallelKlineConverter parallel_converter(thread_pool);
    
    // 转换为不可变格式
    ImmutableKlineData immutable_klines(
        std::vector<double>(),
        std::vector<double>(),
        std::vector<double>(),
        std::vector<double>(),
        std::vector<double>(),
        std::vector<int64_t>(),
        std::vector<int64_t>()
    );
    
    // 填充数据
    std::vector<double> open, high, low, close, volume;
    std::vector<int64_t> open_time, close_time;
    
    for (const auto& k : klines_1m) {
        open.push_back(k.open);
        high.push_back(k.high);
        low.push_back(k.low);
        close.push_back(k.close);
        volume.push_back(k.volume);
        open_time.push_back(k.open_time);
        close_time.push_back(k.close_time);
    }
    
    immutable_klines = ImmutableKlineData(open, high, low, close, volume, open_time, close_time);
    
    std::vector<std::string> target_timeframes = {"5m", "15m", "1h"};
    
    // 串行转换
    auto serial_result = runBenchmark(
        "串行转换 (5m, 15m, 1h) - 100次",
        [&]() {
            size_t total = 0;
            for (const auto& tf : target_timeframes) {
                auto result = parallel_converter.convertSingle(immutable_klines, "1m", tf);
                total += result.size();
            }
            return total;
        },
        100,
        klines_1m.size()
    );
    serial_result.print();
    
    // 并行转换
    auto parallel_result = runBenchmark(
        "并行转换 (5m, 15m, 1h) - 100次",
        [&]() {
            auto results = parallel_converter.convertMultiple(immutable_klines, "1m", target_timeframes);
            size_t total = 0;
            for (const auto& pair : results) {
                total += pair.second.size();
            }
            return total;
        },
        100,
        klines_1m.size()
    );
    parallel_result.print();
    
    double speedup = serial_result.avg_time_ms / parallel_result.avg_time_ms;
    std::cout << "  --> 并行加速比: " << std::fixed << std::setprecision(2) 
              << speedup << "x" << std::endl;
    
    // 打印统计
    auto stats = parallel_converter.getStats();
    std::cout << "\n  并行转换统计:" << std::endl;
    std::cout << "    总转换次数: " << stats.total_conversions << std::endl;
    std::cout << "    并行转换次数: " << stats.parallel_conversions << std::endl;
    std::cout << "    总耗时: " << std::fixed << std::setprecision(2) 
              << stats.total_time_us / 1000.0 << " ms" << std::endl;
    std::cout << "    平均耗时: " << std::fixed << std::setprecision(2) 
              << stats.avg_time_per_conversion_us / 1000.0 << " ms" << std::endl;
}

// ============================================================================
// 主程序
// ============================================================================

int main() {
    std::cout << "\n";
    std::cout << "╔════════════════════════════════════════════════════════════════════════════╗\n";
    std::cout << "║          Prophet.Core K线转换性能基准测试                                  ║\n";
    std::cout << "╚════════════════════════════════════════════════════════════════════════════╝\n";
    std::cout << std::endl;
    
    // 生成测试数据
    std::cout << "生成测试数据..." << std::endl;
    const size_t kline_count = 100000; // 100,000根1m K线（约70天）
    auto klines_1m = generateTestKlines(kline_count);
    std::cout << "  生成 " << klines_1m.size() << " 根1分钟K线" << std::endl;
    std::cout << "  时间范围: " << klines_1m.front().open_time 
              << " - " << klines_1m.back().close_time << std::endl;
    std::cout << "  总天数: " << static_cast<int>(klines_1m.size() / (24 * 60)) << " 天" << std::endl;
    
    // 运行基准测试
    benchmark_no_cache(klines_1m);
    benchmark_with_cache(klines_1m);
    benchmark_simd(klines_1m);
    benchmark_parallel(klines_1m);
    
    // 总结
    std::cout << "\n" << std::string(80, '=') << std::endl;
    std::cout << "测试完成！" << std::endl;
    std::cout << std::string(80, '=') << std::endl;
    
    return 0;
}

