/*
 * ============================================================================ 
 * 文件名：ROC.cpp
 * 指标名：ROC
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

IndicatorResult Calculator::ROC(
    const std::vector<double>& close,
    int ROC_PERIOD
) {
    IndicatorResult result;

    if (close.size() < size_t(ROC_PERIOD + 1)) {
        result.setSeries("value", std::vector<Value>{});
        result.set("trend", Value::fromString("NEUTRAL"));
        return result;
    }

    std::vector<double> roc_values(close.size(), 0.0);
    
    // P3优化：如果SIMD可用且数据量足够，使用SIMD加速（2-3倍）
    if (simd::Config::instance().isEnabled() && 
        simd::Config::instance().features().has_avx2 &&
        close.size() >= 100) {  // 数据量阈值：100+
        
        try {
            // 使用SIMD计算ROC
            auto simd_result = simd::calculate_ROC(
                close.data(), close.size(), ROC_PERIOD
            );
            
            // 🔑 关键修复：SIMD返回的数组可能包含前面的0值，需要提取有效数据
            // 计算outBegIdx（与TA-Lib保持一致）
            int outBegIdx = ROC_PERIOD;
            int outNbElement = static_cast<int>(close.size()) - outBegIdx;
            
            if (outNbElement <= 0) {
                result.setSeries("value", std::vector<Value>{});
                result.set("trend", Value::fromString("NEUTRAL"));
                return result;
            }
            
            // 提取有效数据
            std::vector<Value> roc_series;
            roc_series.reserve(outNbElement);
            for (int i = 0; i < outNbElement; ++i) {
                int idx = outBegIdx + i;
                if (idx < static_cast<int>(simd_result.roc.size())) {
                    roc_series.push_back(Value::fromNumber(simd_result.roc[idx]));
                }
            }
            
            if (roc_series.empty()) {
                result.setSeries("value", std::vector<Value>{});
                result.set("trend", Value::fromString("NEUTRAL"));
                return result;
            }
            
            result.setSeries("value", roc_series);
            
            // 判断趋势（基于有效数据范围）
            std::vector<double> valid_roc_values(outNbElement);
            for (int i = 0; i < outNbElement; ++i) {
                valid_roc_values[i] = roc_series[i].toNumber();
            }
            std::string trend = determineTrend(valid_roc_values);

            result.set("trend", Value::fromString(trend));
            
            return result;
        } catch (...) {
            // SIMD计算失败，回退到TA-Lib
        }
    }
    
    // 使用 TA-Lib 计算（回退路径）
    int outBegIdx, outNbElement;
    TA_RetCode retCode = TA_ROC(
        0, static_cast<int>(close.size()) - 1,
        close.data(),
        ROC_PERIOD,
        &outBegIdx, &outNbElement,
        roc_values.data()  // 修复：使用正确的指针
    );

    if (retCode != TA_SUCCESS || outNbElement == 0) {
        result.setSeries("value", std::vector<Value>{});
        result.set("trend", Value::fromString("NEUTRAL"));
        return result;
    }
    
    // 提取有效数据（TA-Lib将有效数据存储在数组开头）
    std::vector<Value> roc_series;
    roc_series.reserve(outNbElement);
    for (int i = 0; i < outNbElement; ++i) {
        roc_series.push_back(Value::fromNumber(roc_values[i]));
    }
    result.setSeries("value", roc_series);
    
    // 判断趋势（基于有效数据范围）
    std::vector<double> valid_roc_values(outNbElement);
    for (int i = 0; i < outNbElement; ++i) {
        valid_roc_values[i] = roc_series[i].toNumber();
    }
    std::string trend = determineTrend(valid_roc_values);

    result.set("trend", Value::fromString(trend));

    return result;
}

} // namespace prophet::indicators
