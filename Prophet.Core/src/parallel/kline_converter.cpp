/*
 * ============================================================================
 * 文件名：kline_converter.cpp
 * 功能说明：并行K线转换器实现
 * ============================================================================
 */

#include "prophet/parallel/kline_converter.hpp"
#include "prophet/functions/FunctionTypes.hpp"
#include <chrono>
#include <future>
#include <algorithm>

namespace prophet::parallel {

// ============================================================================
// 构造函数
// ============================================================================

ParallelKlineConverter::ParallelKlineConverter(ThreadPool& thread_pool)
    : thread_pool_(thread_pool)
    , stats_{}
{}

// ============================================================================
// 并行转换
// ============================================================================

std::unordered_map<std::string, ImmutableKlineData> 
ParallelKlineConverter::convertMultiple(
    const ImmutableKlineData& source_klines,
    const std::string& source_timeframe,
    const std::vector<std::string>& target_timeframes)
{
    auto start_time = std::chrono::high_resolution_clock::now();
    
    // 提交所有转换任务到线程池
    std::unordered_map<std::string, std::future<ImmutableKlineData>> futures;
    
    for (const auto& target_tf : target_timeframes) {
        // 如果目标时间框架等于源时间框架，直接返回（零拷贝）
        if (target_tf == source_timeframe) {
            futures[target_tf] = std::async(std::launch::deferred, 
                [source_klines]() { return source_klines; }
            );
            continue;
        }
        
        // 提交到线程池
        futures[target_tf] = thread_pool_.submit(
            [source_klines, source_timeframe, target_tf]() {
                return convert_internal(source_klines, source_timeframe, target_tf);
            }
        );
    }
    
    // 收集结果
    std::unordered_map<std::string, ImmutableKlineData> results;
    for (auto& [tf, future] : futures) {
        results[tf] = future.get();
    }
    
    // 更新统计
    auto end_time = std::chrono::high_resolution_clock::now();
    auto elapsed_us = std::chrono::duration_cast<std::chrono::microseconds>(
        end_time - start_time
    ).count();
    
    stats_.total_conversions += target_timeframes.size();
    stats_.parallel_conversions += target_timeframes.size();
    stats_.total_time_us += elapsed_us;
    stats_.avg_time_per_conversion_us = stats_.total_time_us / stats_.total_conversions;
    
    return results;
}

// ============================================================================
// 单个转换（串行）
// ============================================================================

ImmutableKlineData ParallelKlineConverter::convertSingle(
    const ImmutableKlineData& source_klines,
    const std::string& source_timeframe,
    const std::string& target_timeframe)
{
    auto start_time = std::chrono::high_resolution_clock::now();
    
    auto result = convert_internal(source_klines, source_timeframe, target_timeframe);
    
    auto end_time = std::chrono::high_resolution_clock::now();
    auto elapsed_us = std::chrono::duration_cast<std::chrono::microseconds>(
        end_time - start_time
    ).count();
    
    stats_.total_conversions += 1;
    stats_.total_time_us += elapsed_us;
    stats_.avg_time_per_conversion_us = stats_.total_time_us / stats_.total_conversions;
    
    return result;
}

// ============================================================================
// 内部转换实现
// ============================================================================

ImmutableKlineData ParallelKlineConverter::convert_internal(
    const ImmutableKlineData& source,
    const std::string& source_tf,
    const std::string& target_tf)
{
    // 解析时间框架
    int from_minutes = tools::kline::Converter::timeframeToMinutes(source_tf);
    int to_minutes = tools::kline::Converter::timeframeToMinutes(target_tf);
    
    // 转换为旧格式的Kline向量
    std::vector<prophet::Kline> source_vector;
    source_vector.reserve(source.size());
    
    for (size_t i = 0; i < source.size(); ++i) {
        prophet::Kline kline;
        kline.open = source.open(i);
        kline.high = source.high(i);
        kline.low = source.low(i);
        kline.close = source.close(i);
        kline.volume = source.volume(i);
        kline.open_time = source.open_time(i);
        kline.close_time = source.close_time(i);
        source_vector.push_back(kline);
    }
    
    // 使用KlineConverter进行转换
    auto converted = tools::kline::Converter::convert(
        source_vector,
        from_minutes,
        to_minutes,
        0  // 返回全部
    );
    
    // 转换回ImmutableKlineData
    if (converted.empty()) {
        return ImmutableKlineData();
    }
    
    std::vector<double> open, high, low, close, volume;
    std::vector<int64_t> open_time, close_time;
    
    open.reserve(converted.size());
    high.reserve(converted.size());
    low.reserve(converted.size());
    close.reserve(converted.size());
    volume.reserve(converted.size());
    open_time.reserve(converted.size());
    close_time.reserve(converted.size());
    
    for (const auto& kline : converted) {
        open.push_back(kline.open);
        high.push_back(kline.high);
        low.push_back(kline.low);
        close.push_back(kline.close);
        volume.push_back(kline.volume);
        open_time.push_back(kline.open_time);
        close_time.push_back(kline.close_time);
    }
    
    return ImmutableKlineData(open, high, low, close, volume, open_time, close_time);
}

// ============================================================================
// 辅助函数
// ============================================================================

std::vector<prophet::Kline> convertKlinesVector(
    const std::vector<prophet::Kline>& source_klines,
    int from_minutes,
    int to_minutes)
{
    return tools::kline::Converter::convert(source_klines, from_minutes, to_minutes, 0);
}

size_t estimateConvertedKlineCount(
    size_t source_count,
    int from_minutes,
    int to_minutes)
{
    if (to_minutes <= from_minutes) {
        return source_count;
    }
    
    int ratio = to_minutes / from_minutes;
    return (source_count + ratio - 1) / ratio;  // 向上取整
}

} // namespace prophet::parallel

