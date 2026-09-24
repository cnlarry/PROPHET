/*
 * ============================================================================
 * 文件名：kline_converter.hpp
 * 功能说明：K线时间框架转换器（优化版本）
 * 
 * 命名空间：prophet::tools::kline
 * 
 * 这是Prophet系统中K线合成的统一实现，所有模块都应使用此实现：
 * - 核心引擎内部
 * - Python脚本（通过pybind11）
 * - C# API（通过C导出）
 * - C# 客户端（通过C导出）
 * 
 * 核心算法：
 * 1. 时间对齐：使用UTC纪元对齐（与Binance等交易所一致）
 * 2. OHLCV合并：
 *    - Open: 窗口内第一根K线的开盘价
 *    - High: 窗口内所有K线的最高价（SIMD加速）
 *    - Low: 窗口内所有K线的最低价（SIMD加速）
 *    - Close: 窗口内最后一根K线的收盘价
 *    - Volume: 窗口内所有K线的成交量之和（SIMD加速）
 * 3. 完整性检查：只返回完整的K线窗口
 * 
 * 性能优化：
 * - SIMD向量化：使用AVX2/SSE4.2加速批量计算（2-8倍提升）
 * - LRU缓存：避免重复转换（10-50倍提升）
 * - 对象池：减少内存分配开销
 * ============================================================================
 */

#pragma once

#include "prophet/common/types.hpp"
#include "prophet/common/object_pool.hpp"
#include <vector>
#include <string>
#include <cstdint>
#include <memory>

namespace prophet::tools::kline {

/**
 * K线时间框架转换器（优化版本）
 * 
 * 将低时间框架的K线合并成高时间框架的K线
 * 
 * 性能优化：
 * - SIMD向量化：AVX2/SSE4.2 加速 (mergeKlines 内部自动使用)
 * - 缓存机制：需配合 prophet::cache::KlineConversionCache 使用
 * - 多线程：需配合 prophet::parallel::ParallelKlineConverter 使用
 */
class Converter {
public:
    /**
     * 转换K线时间框架（UTC纪元对齐）
     * 
     * @param records 源K线数据（必须按时间升序排列）
     * @param from_minutes 源时间框架（分钟）
     * @param to_minutes 目标时间框架（分钟）
     * @param period 返回最近N根目标K线（0=返回全部）
     * @return 转换后的K线列表
     * 
     * @throws std::invalid_argument 如果参数无效
     * 
     * 要求：
     * - to_minutes 必须大于 from_minutes
     * - to_minutes 必须是 from_minutes 的整数倍
     * - records 必须按 open_time 升序排列
     * 
     * 示例：
     *   convert(klines_1m, 1, 5)     // 1分钟 -> 5分钟
     *   convert(klines_1m, 1, 60)    // 1分钟 -> 1小时
     *   convert(klines_5m, 5, 15)    // 5分钟 -> 15分钟
     */
    static std::vector<Kline> convert(
        const std::vector<Kline>& records,
        int from_minutes,
        int to_minutes,
        int period = 0
    );

    /**
     * 时间框架字符串转分钟数
     * 
     * @param timeframe 时间框架字符串（如 "1m", "5m", "1h", "1d"）
     * @return 对应的分钟数
     * 
     * @throws std::invalid_argument 如果时间框架格式无效
     */
    static int timeframeToMinutes(const std::string& timeframe);

    /**
     * 分钟数转时间框架字符串
     * 
     * @param minutes 分钟数
     * @return 时间框架字符串
     */
    static std::string minutesToTimeframe(int minutes);

private:
    /**
     * 检查K线是否按时间升序排列
     */
    static bool isSorted(const std::vector<Kline>& klines);

    /**
     * 合并一组K线为单根K线
     */
    static Kline mergeKlines(
        const std::vector<Kline>& group,
        int64_t window_start_ms,
        int64_t window_end_ms
    );
    
    // 阶段2优化：K线向量对象池
    static utils::VectorPool<Kline>& getKlineVectorPool() {
        static utils::VectorPool<Kline> pool(16, 128);
        return pool;
    }
};

} // namespace prophet::tools::kline

// ============================================================================
// C API 导出（供C#、Python ctypes等调用）
// ============================================================================

extern "C" {

/**
 * K线数据结构（C兼容）
 */
typedef struct {
    int64_t open_time;   // 开盘时间（Unix毫秒时间戳）
    int64_t close_time;  // 收盘时间（Unix毫秒时间戳）
    double open;         // 开盘价
    double high;         // 最高价
    double low;          // 最低价
    double close;        // 收盘价
    double volume;       // 成交量
} ProphetKline;

/**
 * 转换K线时间框架（C API）
 */
#ifdef _WIN32
__declspec(dllexport)
#endif
int prophet_kline_convert(
    const ProphetKline* input,
    int input_count,
    int from_minutes,
    int to_minutes,
    ProphetKline* output,
    int* output_count,
    int period
);

/**
 * 时间框架字符串转分钟数（C API）
 */
#ifdef _WIN32
__declspec(dllexport)
#endif
int prophet_kline_timeframe_to_minutes(const char* timeframe);

/**
 * 分钟数转时间框架字符串（C API）
 */
#ifdef _WIN32
__declspec(dllexport)
#endif
int prophet_kline_minutes_to_timeframe(int minutes, char* output, int output_size);

} // extern "C"

