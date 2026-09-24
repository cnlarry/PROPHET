/*
 * ============================================================================
 * 文件名：parallel_calculator.cpp
 * 功能说明：并行指标计算器实现（阶段4优化）
 * ============================================================================
 */

#include "prophet/indicators/parallel_calculator.hpp"
#include <algorithm>
#include <regex>

namespace prophet::indicators {

// ============================================================================
// ParallelIndicatorCalculator 实现
// ============================================================================

ParallelIndicatorCalculator::ParallelIndicatorCalculator(
    CalculateFunction calculate_func,
    parallel::ThreadPool& thread_pool
)
    : calculate_func_(calculate_func)
    , thread_pool_(thread_pool)
    , stats_()
{}

std::string ParallelIndicatorCalculator::makeResultKey(const IndicatorRequest& req) {
    std::string key = req.timeframe + ":" + req.indicator_name;
    if (!req.param_hash.empty()) {
        key += ":" + req.param_hash;
    }
    if (!req.symbol.empty()) {
        key += ":" + req.symbol;
    }
    return key;
}

std::vector<IndicatorRequest> ParallelIndicatorCalculator::deduplicateRequests(
    const std::vector<IndicatorRequest>& requests
) const {
    std::unordered_set<IndicatorRequest> unique_requests(requests.begin(), requests.end());
    return std::vector<IndicatorRequest>(unique_requests.begin(), unique_requests.end());
}

std::unordered_map<std::string, IndicatorResult> 
ParallelIndicatorCalculator::calculateBatch(
    const std::vector<IndicatorRequest>& requests
) {
    if (requests.empty()) {
        return {};
    }
    
    // 统计
    stats_.total_requests += requests.size();
    
    // 去重
    auto unique_requests = deduplicateRequests(requests);
    stats_.total_calculated += unique_requests.size();
    stats_.avg_batch_size = (stats_.avg_batch_size * (stats_.total_requests - requests.size()) + unique_requests.size()) 
                           / stats_.total_requests;
    
    // 并行提交所有指标计算任务
    std::unordered_map<std::string, std::future<IndicatorResult>> futures;
    
    for (const auto& req : unique_requests) {
        std::string key = makeResultKey(req);
        
        // 提交计算任务
        futures[key] = thread_pool_.submit([this, req]() {
            return calculate_func_(req);
        });
    }
    
    // 收集结果
    std::unordered_map<std::string, IndicatorResult> results;
    results.reserve(futures.size());
    
    for (auto& [key, future] : futures) {
        try {
            results[key] = future.get();
        } catch (const std::exception& e) {
            // 显式标记异常变量（用于日志或调试）
            (void)e;
            // 计算失败，返回空结果
            results[key] = IndicatorResult();
        }
    }
    
    // 计算平均并行度
    double parallelism = static_cast<double>(unique_requests.size());
    stats_.avg_parallelism = (stats_.avg_parallelism * (stats_.total_requests - requests.size()) + parallelism) 
                            / stats_.total_requests;
    
    return results;
}

std::unordered_map<std::string, IndicatorResult>
ParallelIndicatorCalculator::calculateBatchWithCache(
    const std::vector<IndicatorRequest>& requests,
    std::function<bool(const IndicatorRequest&, IndicatorResult&)> cache_lookup
) {
    if (requests.empty()) {
        return {};
    }
    
    std::unordered_map<std::string, IndicatorResult> results;
    std::vector<IndicatorRequest> cache_misses;
    
    // 第一轮：尝试从缓存读取
    for (const auto& req : requests) {
        IndicatorResult cached_result;
        if (cache_lookup(req, cached_result)) {
            // 缓存命中
            std::string key = makeResultKey(req);
            results[key] = cached_result;
            ++stats_.cache_hits;
        } else {
            // 缓存未命中，记录需要计算的指标
            cache_misses.push_back(req);
        }
    }
    
    // 第二轮：并行计算缓存未命中的指标
    if (!cache_misses.empty()) {
        auto calculated_results = calculateBatch(cache_misses);
        
        // 合并结果
        for (auto& [key, result] : calculated_results) {
            results[key] = std::move(result);
        }
    }
    
    return results;
}

std::unordered_map<std::string, IndicatorResult>
ParallelIndicatorCalculator::calculateMultiTimeframe(
    const std::string& indicator_name,
    const std::vector<std::string>& timeframes,
    const std::string& symbol
) {
    // 构建请求列表
    std::vector<IndicatorRequest> requests;
    requests.reserve(timeframes.size());
    
    for (const auto& tf : timeframes) {
        requests.emplace_back(tf, indicator_name, "", symbol);
    }
    
    // 批量计算
    auto results = calculateBatch(requests);
    
    // 转换键格式：从"tf:indicator:hash:symbol"到"tf"
    std::unordered_map<std::string, IndicatorResult> timeframe_results;
    for (const auto& tf : timeframes) {
        std::string key = tf + ":" + indicator_name;
        if (!symbol.empty()) {
            key += "::" + symbol;
        }
        
        auto it = results.find(key);
        if (it != results.end()) {
            timeframe_results[tf] = it->second;
        }
    }
    
    return timeframe_results;
}

std::unordered_map<std::string, IndicatorResult>
ParallelIndicatorCalculator::calculateMultiSymbol(
    const std::string& indicator_name,
    const std::string& timeframe,
    const std::vector<std::string>& symbols
) {
    // 构建请求列表
    std::vector<IndicatorRequest> requests;
    requests.reserve(symbols.size());
    
    for (const auto& symbol : symbols) {
        requests.emplace_back(timeframe, indicator_name, "", symbol);
    }
    
    // 批量计算
    auto results = calculateBatch(requests);
    
    // 转换键格式
    std::unordered_map<std::string, IndicatorResult> symbol_results;
    for (const auto& symbol : symbols) {
        std::string key = timeframe + ":" + indicator_name + "::" + symbol;
        
        auto it = results.find(key);
        if (it != results.end()) {
            symbol_results[symbol] = it->second;
        }
    }
    
    return symbol_results;
}

std::unordered_map<std::string, IndicatorResult>
ParallelIndicatorCalculator::calculateMatrix(
    const std::string& indicator_name,
    const std::vector<std::string>& timeframes,
    const std::vector<std::string>& symbols
) {
    // 构建笛卡尔积请求列表
    std::vector<IndicatorRequest> requests;
    requests.reserve(timeframes.size() * symbols.size());
    
    for (const auto& tf : timeframes) {
        for (const auto& symbol : symbols) {
            requests.emplace_back(tf, indicator_name, "", symbol);
        }
    }
    
    // 批量计算
    auto results = calculateBatch(requests);
    
    // 转换键格式：使用"timeframe:symbol"作为键
    std::unordered_map<std::string, IndicatorResult> matrix_results;
    for (const auto& tf : timeframes) {
        for (const auto& symbol : symbols) {
            std::string source_key = tf + ":" + indicator_name + "::" + symbol;
            std::string matrix_key = tf + ":" + symbol;
            
            auto it = results.find(source_key);
            if (it != results.end()) {
                matrix_results[matrix_key] = it->second;
            }
        }
    }
    
    return matrix_results;
}

// ============================================================================
// IndicatorPreloader 实现
// ============================================================================

IndicatorPreloader::IndicatorPreloader(
    CalculateFunction calculate_func,
    parallel::ThreadPool& thread_pool
)
    : calculate_func_(calculate_func)
    , parallel_calc_(calculate_func, thread_pool)
{}

std::vector<IndicatorRequest> IndicatorPreloader::extractIndicatorReferences(
    const std::string& dsl_code
) const {
    std::vector<IndicatorRequest> requests;
    
    // 正则表达式匹配指标调用
    // 格式：INDICATOR(timeframe, param1, param2, ...)
    // 例如：RSI(5m, 14)、EMA(1h, 20)、MACD(15m, 12, 26, 9)
    
    std::regex indicator_pattern(
        R"(([A-Z_]+)\s*\(\s*([a-z0-9]+)\s*(?:,\s*[^)]+)?\))"
    );
    
    std::sregex_iterator iter(dsl_code.begin(), dsl_code.end(), indicator_pattern);
    std::sregex_iterator end;
    
    for (; iter != end; ++iter) {
        std::smatch match = *iter;
        
        IndicatorRequest req;
        req.indicator_name = match[1].str();   // 指标名称（如RSI、EMA）
        req.timeframe = match[2].str();        // 时间框架（如5m、1h）
        
        // 参数哈希暂时留空，实际应用中需要根据完整参数生成
        // 这里简化实现
        req.param_hash = "";
        
        requests.push_back(req);
    }
    
    return requests;
}

void IndicatorPreloader::preload(const std::vector<IndicatorRequest>& requests) {
    // 直接调用并行计算器
    parallel_calc_.calculateBatch(requests);
    
    // 注意：计算结果会自动缓存在Calculator的缓存中
    // 因此这里不需要保存结果
}

} // namespace prophet::indicators

