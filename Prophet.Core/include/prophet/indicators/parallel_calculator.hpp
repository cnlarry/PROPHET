/*
 * ============================================================================
 * 文件名：parallel_indicator_calculator.hpp
 * 功能说明：并行指标计算器（阶段4优化）
 * 
 * 性能优化：
 * - 批量并行计算多个指标
 * - 自动依赖分析和任务调度
 * - 预期复杂策略提升3-5x
 * ============================================================================
 */

#pragma once

#include "prophet/common/types.hpp"
#include "prophet/parallel/thread_pool.hpp"
#include <vector>
#include <string>
#include <unordered_map>
#include <unordered_set>
#include <functional>
#include <future>
#include <memory>

namespace prophet::indicators {

// 前向声明
class Calculator;

/**
 * 指标计算请求
 */
struct IndicatorRequest {
    std::string timeframe;      // 时间框架
    std::string indicator_name; // 指标名称
    std::string param_hash;     // 参数哈希（用于缓存）
    std::string symbol;         // 交易对符号（可选，用于多符号批处理）
    
    IndicatorRequest() = default;
    
    IndicatorRequest(const std::string& tf, const std::string& name, 
                     const std::string& hash = "", const std::string& sym = "")
        : timeframe(tf), indicator_name(name), param_hash(hash), symbol(sym) {}
    
    // 用于去重
    bool operator==(const IndicatorRequest& other) const {
        return timeframe == other.timeframe && 
               indicator_name == other.indicator_name &&
               param_hash == other.param_hash &&
               symbol == other.symbol;
    }
};

} // namespace prophet::indicators

// 为IndicatorRequest提供哈希函数
namespace std {
    template<>
    struct hash<prophet::indicators::IndicatorRequest> {
        size_t operator()(const prophet::indicators::IndicatorRequest& req) const {
            size_t h1 = hash<string>()(req.timeframe);
            size_t h2 = hash<string>()(req.indicator_name);
            size_t h3 = hash<string>()(req.param_hash);
            size_t h4 = hash<string>()(req.symbol);
            return h1 ^ (h2 << 1) ^ (h3 << 2) ^ (h4 << 3);
        }
    };
}

namespace prophet::indicators {

/**
 * 并行指标计算器
 * 
 * 特性：
 * - 批量并行计算多个指标
 * - 自动去重，避免重复计算
 * - 支持跨时间框架批处理
 * - 使用共享线程池
 */
class ParallelIndicatorCalculator {
public:
    /**
     * 指标计算函数类型
     * @param request 指标请求
     * @return 指标结果
     */
    using CalculateFunction = std::function<IndicatorResult(const IndicatorRequest&)>;
    
    /**
     * 构造函数
     * @param calculate_func 指标计算函数
     * @param thread_pool 共享线程池
     */
    ParallelIndicatorCalculator(
        CalculateFunction calculate_func,
        parallel::ThreadPool& thread_pool
    );
    
    /**
     * 批量并行计算指标
     * 
     * @param requests 指标请求列表
     * @return 指标结果映射（key = timeframe:indicator:param_hash）
     */
    std::unordered_map<std::string, IndicatorResult> calculateBatch(
        const std::vector<IndicatorRequest>& requests
    );
    
    /**
     * 批量并行计算指标（带缓存优先）
     * 
     * 优先从缓存读取，只计算缓存未命中的指标
     * 
     * @param requests 指标请求列表
     * @param cache_lookup 缓存查找函数
     * @return 指标结果映射
     */
    std::unordered_map<std::string, IndicatorResult> calculateBatchWithCache(
        const std::vector<IndicatorRequest>& requests,
        std::function<bool(const IndicatorRequest&, IndicatorResult&)> cache_lookup
    );
    
    /**
     * 批处理模式：跨时间框架批量计算
     * 
     * 一次性对多个时间框架执行相同指标的计算
     * 优化：避免重复扫描K线数据
     * 
     * @param indicator_name 指标名称
     * @param timeframes 时间框架列表
     * @param symbol 交易对符号
     * @return 时间框架到结果的映射
     */
    std::unordered_map<std::string, IndicatorResult> calculateMultiTimeframe(
        const std::string& indicator_name,
        const std::vector<std::string>& timeframes,
        const std::string& symbol = ""
    );
    
    /**
     * 批处理模式：跨符号批量计算
     * 
     * 一次性对多个交易对执行相同指标的计算
     * 
     * @param indicator_name 指标名称
     * @param timeframe 时间框架
     * @param symbols 交易对符号列表
     * @return 符号到结果的映射
     */
    std::unordered_map<std::string, IndicatorResult> calculateMultiSymbol(
        const std::string& indicator_name,
        const std::string& timeframe,
        const std::vector<std::string>& symbols
    );
    
    /**
     * 批处理模式：矩阵式批量计算
     * 
     * 对多个时间框架 × 多个符号执行指标计算
     * 
     * @param indicator_name 指标名称
     * @param timeframes 时间框架列表
     * @param symbols 交易对符号列表
     * @return 键="timeframe:symbol"的结果映射
     */
    std::unordered_map<std::string, IndicatorResult> calculateMatrix(
        const std::string& indicator_name,
        const std::vector<std::string>& timeframes,
        const std::vector<std::string>& symbols
    );
    
    /**
     * 获取统计信息
     */
    struct Statistics {
        uint64_t total_requests = 0;      // 总请求数
        uint64_t total_calculated = 0;    // 实际计算数
        uint64_t cache_hits = 0;          // 缓存命中数
        double avg_batch_size = 0.0;      // 平均批次大小
        double avg_parallelism = 0.0;     // 平均并行度
    };
    
    Statistics getStatistics() const {
        return stats_;
    }
    
    void resetStatistics() {
        stats_ = Statistics();
    }

private:
    /**
     * 生成结果键
     */
    static std::string makeResultKey(const IndicatorRequest& req);
    
    /**
     * 去重请求
     */
    std::vector<IndicatorRequest> deduplicateRequests(
        const std::vector<IndicatorRequest>& requests
    ) const;

private:
    CalculateFunction calculate_func_;
    parallel::ThreadPool& thread_pool_;
    Statistics stats_;
};

/**
 * 指标预热器
 * 
 * 在策略执行前预计算所有可能需要的指标
 */
class IndicatorPreloader {
public:
    using CalculateFunction = ParallelIndicatorCalculator::CalculateFunction;
    
    IndicatorPreloader(
        CalculateFunction calculate_func,
        parallel::ThreadPool& thread_pool
    );
    
    /**
     * 从DSL代码中提取所有指标引用
     * 
     * @param dsl_code DSL源代码
     * @return 指标请求列表
     */
    std::vector<IndicatorRequest> extractIndicatorReferences(
        const std::string& dsl_code
    ) const;
    
    /**
     * 预加载指标
     * 
     * @param requests 指标请求列表
     */
    void preload(const std::vector<IndicatorRequest>& requests);

private:
    CalculateFunction calculate_func_;
    ParallelIndicatorCalculator parallel_calc_;
};

} // namespace prophet::indicators

