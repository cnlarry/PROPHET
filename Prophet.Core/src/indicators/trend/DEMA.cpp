/*
 * ============================================================================
 * 文件名：DEMA.cpp
 * 指标名：DEMA
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

IndicatorResult Calculator::DEMA(
    const std::vector<double>& close,
    int DEMA_PERIOD
) {
    IndicatorResult result;

    if (close.size() < size_t(DEMA_PERIOD * 2)) {
        result.setSeries("value", std::vector<Value>{});
        result.set("slope", Value::fromNumber(0.0));
        result.set("distance", Value::fromNumber(0.0));
        result.set("trend", Value::fromString("NEUTRAL"));
        return result;
    }

    std::vector<double> dema_values(close.size(), 0.0);
    
    // P3优化：如果SIMD可用且数据量足够，使用SIMD加速（2-3倍）
    if (simd::Config::instance().isEnabled() && 
        simd::Config::instance().features().has_avx2 &&
        close.size() >= 100) {  // 数据量阈值：100+
        
        try {
            // 使用SIMD计算DEMA
            simd::calculate_DEMA(
                close.data(), close.size(), DEMA_PERIOD,
                dema_values.data()
            );
            
            // 🔑 关键修复：SIMD返回的数组可能包含前面的无效值，需要提取有效数据
            // 计算outBegIdx（与TA-Lib保持一致）
            int outBegIdx = DEMA_PERIOD * 2 - 2;
            int outNbElement = static_cast<int>(close.size()) - outBegIdx;
            
            if (outNbElement <= 0) {
                result.setSeries("value", std::vector<Value>{});
                result.set("slope", Value::fromNumber(0.0));
                result.set("distance", Value::fromNumber(0.0));
                result.set("trend", Value::fromString("NEUTRAL"));
                return result;
            }
            
            // 提取有效数据
            std::vector<Value> dema_series;
            dema_series.reserve(outNbElement);
            for (int i = 0; i < outNbElement; ++i) {
                int idx = outBegIdx + i;
                if (idx < static_cast<int>(dema_values.size())) {
                    dema_series.push_back(Value::fromNumber(dema_values[idx]));
                }
            }
            
            if (dema_series.empty()) {
                result.setSeries("value", std::vector<Value>{});
                result.set("slope", Value::fromNumber(0.0));
                result.set("distance", Value::fromNumber(0.0));
                result.set("trend", Value::fromString("NEUTRAL"));
                return result;
            }
            
            result.setSeries("value", dema_series);
            
            double curr_dema = dema_series.back().toNumber();
            double curr_price = close.back();

            // 计算斜率和趋势（基于有效数据范围）
            std::vector<double> dema_valid(outNbElement);
            for (int i = 0; i < outNbElement; ++i) {
                dema_valid[i] = dema_series[i].toNumber();
            }
            double slope = calculateSlope(dema_valid, 3);

            // 计算距离
            double distance = 0.0;
            if (curr_dema > 1e-10) {
                distance = ((curr_price - curr_dema) / curr_dema) * 100.0;
            }

            // 判断趋势
            std::string trend = determineTrend(dema_valid);

            result.set("slope", Value::fromNumber(slope));
            result.set("distance", Value::fromNumber(distance));
            result.set("trend", Value::fromString(trend));
            
            return result;
        } catch (...) {
            // SIMD计算失败，回退到TA-Lib
        }
    }
    
    // 使用 TA-Lib 计算（回退路径）
    int outBegIdx, outNbElement;
    TA_RetCode retCode = TA_DEMA(
        0, static_cast<int>(close.size()) - 1,
        close.data(),
        DEMA_PERIOD,
        &outBegIdx, &outNbElement,
        dema_values.data()  // 修复：使用正确的指针
    );

    if (retCode != TA_SUCCESS || outNbElement == 0) {
        result.setSeries("value", std::vector<Value>{});
        result.set("slope", Value::fromNumber(0.0));
        result.set("distance", Value::fromNumber(0.0));
        result.set("trend", Value::fromString("NEUTRAL"));
        return result;
    }
    
    // 提取有效数据（TA-Lib将有效数据存储在数组开头）
    std::vector<Value> dema_series;
    dema_series.reserve(outNbElement);
    for (int i = 0; i < outNbElement; ++i) {
        dema_series.push_back(Value::fromNumber(dema_values[i]));
    }
    result.setSeries("value", dema_series);
    
    double curr_dema = dema_series.back().toNumber();
    double curr_price = close.back();

    // 计算斜率和趋势（基于有效数据范围）
    std::vector<double> dema_valid(outNbElement);
    for (int i = 0; i < outNbElement; ++i) {
        dema_valid[i] = dema_series[i].toNumber();
    }
    double slope = calculateSlope(dema_valid, 3);

    // 计算距离
    double distance = 0.0;
    if (curr_dema > 1e-10) {
        distance = ((curr_price - curr_dema) / curr_dema) * 100.0;
    }

    // 判断趋势
    std::string trend = determineTrend(dema_valid);

    result.set("slope", Value::fromNumber(slope));
    result.set("distance", Value::fromNumber(distance));
    result.set("trend", Value::fromString(trend));

    return result;
}

} // namespace prophet::indicators
