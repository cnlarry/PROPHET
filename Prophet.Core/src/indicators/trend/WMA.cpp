/*
 * ============================================================================
 * 文件名：WMA.cpp
 * 指标名：WMA
 * 类别：趋势指标
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

IndicatorResult Calculator::WMA(
    const std::vector<double>& close,
    int WMA_PERIOD
) {
    IndicatorResult result;

    if (close.size() < size_t(WMA_PERIOD)) {
        result.setSeries("value", std::vector<Value>{});
        result.set("slope", Value::fromNumber(0.0));
        result.set("distance", Value::fromNumber(0.0));
        return result;
    }

    std::vector<double> wma_values(close.size(), 0.0);
    
    // P3优化：如果SIMD可用且数据量足够，使用SIMD加速（2-3倍）
    if (simd::Config::instance().isEnabled() && 
        simd::Config::instance().features().has_avx2 &&
        close.size() >= 100) {  // 数据量阈值：100+
        
        try {
            // 使用SIMD计算WMA
            simd::calculate_WMA(
                close.data(), close.size(), WMA_PERIOD,
                wma_values.data()
            );
            
            // 🔑 关键修复：SIMD返回的数组可能包含前面的无效值，需要提取有效数据
            // 计算outBegIdx（与TA-Lib保持一致）
            int outBegIdx = WMA_PERIOD - 1;
            int outNbElement = static_cast<int>(close.size()) - outBegIdx;
            
            if (outNbElement <= 0) {
                result.setSeries("value", std::vector<Value>{});
                result.set("slope", Value::fromNumber(0.0));
                result.set("distance", Value::fromNumber(0.0));
                return result;
            }
            
            // 提取有效数据
            std::vector<Value> wma_series;
            wma_series.reserve(outNbElement);
            for (int i = 0; i < outNbElement; ++i) {
                int idx = outBegIdx + i;
                if (idx < static_cast<int>(wma_values.size())) {
                    wma_series.push_back(Value::fromNumber(wma_values[idx]));
                }
            }
            
            if (wma_series.empty()) {
                result.setSeries("value", std::vector<Value>{});
                result.set("slope", Value::fromNumber(0.0));
                result.set("distance", Value::fromNumber(0.0));
                return result;
            }
            
            result.setSeries("value", wma_series);
            
            double curr_wma = wma_series.back().toNumber();
            double curr_price = close.back();
            
            // 计算斜率（基于有效数据范围）
            std::vector<double> wma_valid(outNbElement);
            for (int i = 0; i < outNbElement; ++i) {
                wma_valid[i] = wma_series[i].toNumber();
            }
            double slope = calculateSlope(wma_valid, 3);

            double distance = 0.0;
            if (curr_wma > 1e-10) {
                distance = ((curr_price - curr_wma) / curr_wma) * 100.0;
            }

            result.set("slope", Value::fromNumber(slope));
            result.set("distance", Value::fromNumber(distance));
            
            return result;
        } catch (...) {
            // SIMD计算失败，回退到TA-Lib
        }
    }
    
    // 使用 TA-Lib 计算（回退路径）
    int outBegIdx, outNbElement;
    TA_RetCode retCode = TA_WMA(
        0, static_cast<int>(close.size()) - 1,
        close.data(),
        WMA_PERIOD,
        &outBegIdx, &outNbElement,
        wma_values.data()  // 修复：使用正确的指针
    );

    if (retCode != TA_SUCCESS || outNbElement == 0) {
        result.setSeries("value", std::vector<Value>{});
        result.set("slope", Value::fromNumber(0.0));
        result.set("distance", Value::fromNumber(0.0));
        return result;
    }
    
    // 提取有效数据（TA-Lib将有效数据存储在数组开头）
    std::vector<Value> wma_series;
    wma_series.reserve(outNbElement);
    for (int i = 0; i < outNbElement; ++i) {
        wma_series.push_back(Value::fromNumber(wma_values[i]));
    }
    result.setSeries("value", wma_series);
    
    double curr_wma = wma_series.back().toNumber();
    double curr_price = close.back();
    
    // 计算斜率（基于有效数据范围）
    std::vector<double> wma_valid(outNbElement);
    for (int i = 0; i < outNbElement; ++i) {
        wma_valid[i] = wma_series[i].toNumber();
    }
    double slope = calculateSlope(wma_valid, 3);

    double distance = 0.0;
    if (curr_wma > 1e-10) {
        distance = ((curr_price - curr_wma) / curr_wma) * 100.0;
    }

    result.set("slope", Value::fromNumber(slope));
    result.set("distance", Value::fromNumber(distance));

    return result;
}

} // namespace prophet::indicators
