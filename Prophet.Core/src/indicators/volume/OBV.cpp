/*
 * ============================================================================
 * 文件名：OBV.cpp
 * 指标名：OBV (On Balance Volume)
 * 类别：成交量指标
 * 
 * 创建日期：2025-10-31
 * 优化：2025-12-03 添加SIMD支持
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

IndicatorResult Calculator::OBV(
    const std::vector<double>& close,
    const std::vector<double>& volume
) {
    IndicatorResult result;

    if (close.size() < 2 || volume.size() != close.size()) {
        result.setSeries("value", std::vector<Value>{});
        result.set("trend", Value::fromString("NEUTRAL"));
        result.set("change", Value::fromNumber(0.0));
        return result;
    }

    // 使用TA-Lib计算OBV
    std::vector<double> obv_values(close.size(), 0.0);
    
    // P3优化：如果SIMD可用且数据量足够，使用SIMD加速（2-3倍）
    if (simd::Config::instance().isEnabled() && 
        simd::Config::instance().features().has_avx2 &&
        close.size() >= 100) {  // 数据量阈值：100+
        
        try {
            // 使用SIMD计算OBV
            auto simd_result = simd::calculate_OBV(
                close.data(), volume.data(), close.size()
            );
            
            // 提取所有数据（OBV从第一个值开始有效）
            std::vector<Value> obv_series;
            obv_series.reserve(close.size());
            for (size_t i = 0; i < close.size(); ++i) {
                obv_series.push_back(Value::fromNumber(simd_result.obv[i]));
            }
            result.setSeries("value", obv_series);
            
            // 计算变化量和趋势（基于最新值）
            double obv_current = obv_series.back().toNumber();
            double change = 0.0;
            std::string trend = "NEUTRAL";
            
            if (close.size() >= 2) {
                double obv_prev = obv_series[close.size() - 2].toNumber();
                change = obv_current - obv_prev;
                
                if (change > 0) {
                    trend = "BULLISH";
                } else if (change < 0) {
                    trend = "BEARISH";
                }
            }

            result.set("trend", Value::fromString(trend));
            result.set("change", Value::fromNumber(change));
            
            return result;
        } catch (...) {
            // SIMD计算失败，回退到TA-Lib
        }
    }
    
    // 使用TA-Lib计算（回退路径）
    int outBegIdx, outNbElement;
    
    TA_RetCode retCode = TA_OBV(
        0, static_cast<int>(close.size()) - 1,
        close.data(),
        volume.data(),
        &outBegIdx, &outNbElement,
        obv_values.data()
    );

    if (retCode != TA_SUCCESS || outNbElement == 0) {
        result.setSeries("value", std::vector<Value>{});
        result.set("trend", Value::fromString("NEUTRAL"));
        result.set("change", Value::fromNumber(0.0));
        return result;
    }

    // 提取有效数据（TA-Lib将有效数据存储在数组开头）
    std::vector<Value> obv_series;
    obv_series.reserve(outNbElement);
    for (int i = 0; i < outNbElement; ++i) {
        obv_series.push_back(Value::fromNumber(obv_values[i]));
    }
    result.setSeries("value", obv_series);
    
    // 计算变化量和趋势（基于最新值）
    double obv_current = obv_series.back().toNumber();
    double change = 0.0;
    std::string trend = "NEUTRAL";
    
    if (outNbElement >= 2) {
        double obv_prev = obv_series[outNbElement - 2].toNumber();
        change = obv_current - obv_prev;
        
        if (change > 0) {
            trend = "BULLISH";
        } else if (change < 0) {
            trend = "BEARISH";
        }
    }

    result.set("trend", Value::fromString(trend));
    result.set("change", Value::fromNumber(change));

    return result;
}

} // namespace prophet::indicators

