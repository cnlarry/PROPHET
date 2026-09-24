/*
 * ============================================================================
 * 文件名：kline_converter.hpp
 * 功能说明：并行K线转换器 - Day 3-4
 * 
 * 设计理念：
 * - 多个时间框架的K线转换完全独立
 * - 可以并行执行，充分利用多核CPU
 * - 使用ThreadPool管理线程资源
 * - 返回不可变的K线数据（ImmutableKlineData）
 * 
 * 使用场景：
 * - Engine收到1分钟K线后，并行转换为5m、15m、1h等
 * - 多时间框架DSL语法：$.RSI[5m,15m,1h].value > 70
 * - 减少K线转换的串行等待时间
 * ============================================================================
 */

#pragma once

#include "prophet/parallel/thread_pool.hpp"
#include "prophet/parallel/immutable_kline.hpp"
#include "prophet/tools/kline_converter.hpp"
#include <string>
#include <vector>
#include <unordered_map>
#include <memory>

namespace prophet::parallel {

/**
 * 并行K线转换器
 * 
 * 职责：
 * - 接收源时间框架的K线数据（通常是1分钟）
 * - 并行转换为多个目标时间框架
 * - 返回不可变的K线数据映射
 * 
 * 特性：
 * - 自动并行：多个时间框架同时转换
 * - 零拷贝：源数据和结果都是不可变的
 * - 线程安全：可以在多线程环境中使用
 * 
 * 性能：
 * - 4个时间框架：理论加速比 ~3x
 * - 8个时间框架：理论加速比 ~5x
 */
class ParallelKlineConverter {
public:
    /**
     * 构造函数
     * @param thread_pool 线程池（外部管理）
     */
    explicit ParallelKlineConverter(ThreadPool& thread_pool);
    
    /**
     * 批量并行转换多个时间框架
     * 
     * @param source_klines 源K线数据（1分钟）
     * @param source_timeframe 源时间框架（如"1m"）
     * @param target_timeframes 目标时间框架列表（如["5m", "15m", "1h"]）
     * @return 时间框架到K线数据的映射
     * 
     * 示例：
     *   auto source = ImmutableKlineData(...);
     *   auto results = converter.convertMultiple(
     *       source, "1m", {"5m", "15m", "1h", "4h"}
     *   );
     *   // results["5m"] -> 5分钟K线
     *   // results["15m"] -> 15分钟K线
     */
    std::unordered_map<std::string, ImmutableKlineData> convertMultiple(
        const ImmutableKlineData& source_klines,
        const std::string& source_timeframe,
        const std::vector<std::string>& target_timeframes
    );
    
    /**
     * 单个时间框架转换（串行，用于对比）
     */
    ImmutableKlineData convertSingle(
        const ImmutableKlineData& source_klines,
        const std::string& source_timeframe,
        const std::string& target_timeframe
    );
    
    /**
     * 获取转换统计信息
     */
    struct ConversionStats {
        size_t total_conversions;      // 总转换次数
        size_t parallel_conversions;   // 并行转换次数
        double total_time_us;          // 总耗时（微秒）
        double avg_time_per_conversion_us;  // 平均每次转换耗时
    };
    
    ConversionStats getStats() const { return stats_; }
    void resetStats() { stats_ = ConversionStats(); }
    
private:
    ThreadPool& thread_pool_;
    ConversionStats stats_;
    
    /**
     * 内部转换函数（静态，纯函数）
     */
    static ImmutableKlineData convert_internal(
        const ImmutableKlineData& source,
        const std::string& source_tf,
        const std::string& target_tf
    );
};

/**
 * 辅助函数：从已有向量转换K线
 * 
 * @param source_klines 源K线向量
 * @param from_minutes 源时间框架（分钟数）
 * @param to_minutes 目标时间框架（分钟数）
 * @return 转换后的K线向量
 */
std::vector<prophet::Kline> convertKlinesVector(
    const std::vector<prophet::Kline>& source_klines,
    int from_minutes,
    int to_minutes
);

/**
 * 辅助函数：计算转换后的K线数量（估算）
 */
size_t estimateConvertedKlineCount(
    size_t source_count,
    int from_minutes,
    int to_minutes
);

} // namespace prophet::parallel

