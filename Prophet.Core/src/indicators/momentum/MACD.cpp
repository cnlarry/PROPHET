/*
 * ============================================================================
 * 文件名：MACD.cpp
 * 指标名：MACD
 * 类别：动量指标
 * 
 * 从 calculator.cpp 拆分
 * 拆分日期：2025-10-30
 * ============================================================================
 */

#include "prophet/indicators/calculator.hpp"
#include "prophet/indicators/registry.hpp"
#include "prophet/simd/simd_indicators.hpp"  // P3优化：SIMD指标
#include "prophet/simd/simd_config.hpp"      // P3优化：SIMD配置
#include <ta_libc.h>
#include <cmath>
#include <algorithm>
#include <numeric>
#include <stdexcept>

namespace prophet::indicators {

IndicatorResult Calculator::MACD(
    const std::vector<double>& close,
    int FAST_PERIOD,
    int SLOW_PERIOD,
    int SIGNAL_PERIOD
) {
    IndicatorResult result;

    if (close.size() < size_t(SLOW_PERIOD + SIGNAL_PERIOD)) {
        // 数据不足，返回默认值（空序列）
        result.setSeries("macd", std::vector<Value>{});
        result.setSeries("signal", std::vector<Value>{});
        result.setSeries("histogram", std::vector<Value>{});
        result.set("crossover_type", Value::fromString("NONE"));
        result.set("zero_cross", Value::fromBoolean(false));
        result.set("trend", Value::fromString("NEUTRAL"));
        result.set("momentum", Value::fromNumber(0.0));
        return result;
    }

    std::vector<double> macd_line(close.size(), 0.0);
    std::vector<double> signal_line(close.size(), 0.0);
    std::vector<double> histogram(close.size(), 0.0);
    
    // P3优化：如果SIMD可用且数据量足够，使用SIMD加速
    if (simd::Config::instance().isEnabled() && 
        simd::Config::instance().features().has_avx2 &&
        close.size() >= 100) {  // 数据量阈值：100+
        
        try {
            // 使用SIMD计算MACD（2-3倍加速）
            auto simd_result = simd::calculate_MACD(
                close.data(), close.size(),
                FAST_PERIOD, SLOW_PERIOD, SIGNAL_PERIOD
            );
            
            // 🔑 关键修复：SIMD返回的数组可能包含前面的NaN，需要提取有效数据
            // 计算outBegIdx（与TA-Lib保持一致）
            // MACD需要slowPeriod + signalPeriod根K线才能开始计算
            int outBegIdx = SLOW_PERIOD + SIGNAL_PERIOD - 1;
            int outNbElement = static_cast<int>(close.size()) - outBegIdx;
            
            if (outNbElement <= 0) {
                result.setSeries("macd", std::vector<Value>{});
                result.setSeries("signal", std::vector<Value>{});
                result.setSeries("histogram", std::vector<Value>{});
                result.set("crossover_type", Value::fromString("NONE"));
                result.set("zero_cross", Value::fromBoolean(false));
                result.set("trend", Value::fromString("NEUTRAL"));
                result.set("momentum", Value::fromNumber(0.0));
                return result;
            }
            
            // 提取有效数据（从outBegIdx开始，只包含有效值）
            std::vector<Value> macd_series, signal_series, histogram_series;
            macd_series.reserve(outNbElement);
            signal_series.reserve(outNbElement);
            histogram_series.reserve(outNbElement);
            
            for (int i = 0; i < outNbElement; ++i) {
                int idx = outBegIdx + i;
                if (idx < static_cast<int>(simd_result.macd.size())) {
                    macd_series.push_back(Value::fromNumber(simd_result.macd[idx]));
                    signal_series.push_back(Value::fromNumber(simd_result.signal[idx]));
                    histogram_series.push_back(Value::fromNumber(simd_result.histogram[idx]));
                }
            }
            
            if (macd_series.empty()) {
                result.setSeries("macd", std::vector<Value>{});
                result.setSeries("signal", std::vector<Value>{});
                result.setSeries("histogram", std::vector<Value>{});
                result.set("crossover_type", Value::fromString("NONE"));
                result.set("zero_cross", Value::fromBoolean(false));
                result.set("trend", Value::fromString("NEUTRAL"));
                result.set("momentum", Value::fromNumber(0.0));
                return result;
            }
            
            // 保存序列数据（支持偏移量访问）
            result.setSeries("macd", macd_series);
            result.setSeries("signal", signal_series);
            result.setSeries("histogram", histogram_series);
            
            // 业务逻辑（交叉检测、趋势判断等）- 基于有效数据范围计算
            std::vector<double> macd_valid(outNbElement), signal_valid(outNbElement), histogram_valid(outNbElement);
            for (int i = 0; i < outNbElement; ++i) {
                macd_valid[i] = macd_series[i].toNumber();
                signal_valid[i] = signal_series[i].toNumber();
                histogram_valid[i] = histogram_series[i].toNumber();
            }
            
            std::string crossover = detectCrossover(macd_valid, signal_valid);
            bool zero_cross = detectZeroCross(histogram_valid);
            std::string trend = determineTrend(histogram_valid);
            double momentum = calculateMomentum(histogram_valid);
            
            // 填充派生字段（只保存最新值）
            result.set("crossover_type", Value::fromString(crossover));
            result.set("zero_cross", Value::fromBoolean(zero_cross));
            result.set("trend", Value::fromString(trend));
            result.set("momentum", Value::fromNumber(momentum));
            
            return result;
        } catch (...) {
            // SIMD计算失败，回退到TA-Lib
        }
    }
    
    // 使用 TA-Lib 计算（回退路径）
    int outBegIdx, outNbElement;
    TA_RetCode retCode = TA_MACD(
        0, static_cast<int>(close.size()) - 1,
        close.data(),
        FAST_PERIOD, SLOW_PERIOD, SIGNAL_PERIOD,
        &outBegIdx, &outNbElement,
        macd_line.data(),       // 修复：从数组开头写入
        signal_line.data(),     // 修复：从数组开头写入
        histogram.data()        // 修复：从数组开头写入
    );

    if (retCode != TA_SUCCESS || outNbElement == 0) {
        // TA-Lib 计算失败，返回默认值（空序列）
        result.setSeries("macd", std::vector<Value>{});
        result.setSeries("signal", std::vector<Value>{});
        result.setSeries("histogram", std::vector<Value>{});
        result.set("crossover_type", Value::fromString("NONE"));
        result.set("zero_cross", Value::fromBoolean(false));
        result.set("trend", Value::fromString("NEUTRAL"));
        result.set("momentum", Value::fromNumber(0.0));
        return result;
    }
    
    // TA-Lib返回的数据：outBegIdx是起始索引，outNbElement是有效元素数量
    // 需要提取有效的数据范围：从outBegIdx开始，共outNbElement个元素
    std::vector<Value> macd_series, signal_series, histogram_series;
    macd_series.reserve(outNbElement);
    signal_series.reserve(outNbElement);
    histogram_series.reserve(outNbElement);
    
    // 提取有效数据（TA-Lib将有效数据存储在数组开头）
    for (int i = 0; i < outNbElement; ++i) {
        macd_series.push_back(Value::fromNumber(macd_line[i]));
        signal_series.push_back(Value::fromNumber(signal_line[i]));
        histogram_series.push_back(Value::fromNumber(histogram[i]));
    }
    
    // 保存序列数据（支持偏移量访问）
    result.setSeries("macd", macd_series);
    result.setSeries("signal", signal_series);
    result.setSeries("histogram", histogram_series);

    // 检测交叉（基于有效数据范围）
    std::vector<double> macd_valid(macd_series.size());
    std::vector<double> signal_valid(signal_series.size());
    std::vector<double> histogram_valid(histogram_series.size());
    for (size_t i = 0; i < macd_series.size(); ++i) {
        macd_valid[i] = macd_series[i].toNumber();
        signal_valid[i] = signal_series[i].toNumber();
        histogram_valid[i] = histogram_series[i].toNumber();
    }
    
    std::string crossover = detectCrossover(macd_valid, signal_valid);
    bool zero_cross = detectZeroCross(histogram_valid);
    std::string trend = determineTrend(histogram_valid);
    double momentum = calculateMomentum(histogram_valid);

    // 填充派生字段（只保存最新值，因为这些是基于整个序列计算的）
    result.set("crossover_type", Value::fromString(crossover));
    result.set("zero_cross", Value::fromBoolean(zero_cross));
    result.set("trend", Value::fromString(trend));
    result.set("momentum", Value::fromNumber(momentum));

    return result;
}

} // namespace prophet::indicators
