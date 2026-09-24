/*
 * ============================================================================ 
 * 文件名：BOLL.cpp
 * 指标名：BOLL (Bollinger Bands)
 * 类别：波动率指标
 * 
 * 从 calculator.cpp 拆分
 * 拆分日期：2025-10-30
 * 重命名：2025-10-31 (BBANDS -> BOLL)
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

IndicatorResult Calculator::BOLL(
    const std::vector<double>& close,
    int BOLL_PERIOD,
    double BOLL_STD_DEV
) {
    IndicatorResult result;

    if (close.size() < size_t(BOLL_PERIOD)) {
        result.setSeries("upper", std::vector<Value>{});
        result.setSeries("middle", std::vector<Value>{});
        result.setSeries("lower", std::vector<Value>{});
        result.set("width", Value::fromNumber(0.0));
        result.set("percent_b", Value::fromNumber(0.5));
        return result;
    }

    // 使用 TA-Lib 计算
    std::vector<double> upper_band(close.size(), 0.0);
    std::vector<double> middle_band(close.size(), 0.0);
    std::vector<double> lower_band(close.size(), 0.0);
    
    // P3优化：如果SIMD可用且数据量足够，使用SIMD加速（2-3倍）
    if (simd::Config::instance().isEnabled() && 
        simd::Config::instance().features().has_avx2 &&
        close.size() >= 100) {  // 数据量阈值：100+
        
        try {
            // 使用SIMD计算布林带
            auto simd_result = simd::calculate_BollingerBands(
                close.data(), close.size(), BOLL_PERIOD, BOLL_STD_DEV
            );
            
            // 🔑 关键修复：SIMD返回的数组可能包含前面的无效值，需要提取有效数据
            // 计算outBegIdx（与TA-Lib保持一致）
            int outBegIdx = BOLL_PERIOD - 1;
            int outNbElement = static_cast<int>(close.size()) - outBegIdx;
            
            if (outNbElement <= 0) {
                result.setSeries("upper", std::vector<Value>{});
                result.setSeries("middle", std::vector<Value>{});
                result.setSeries("lower", std::vector<Value>{});
                result.set("width", Value::fromNumber(0.0));
                result.set("percent_b", Value::fromNumber(0.5));
                return result;
            }
            
            // 提取有效数据
            std::vector<Value> upper_series;
            std::vector<Value> middle_series;
            std::vector<Value> lower_series;
            
            upper_series.reserve(outNbElement);
            middle_series.reserve(outNbElement);
            lower_series.reserve(outNbElement);
            
            for (int i = 0; i < outNbElement; ++i) {
                int idx = outBegIdx + i;
                upper_series.push_back(Value::fromNumber(simd_result.upper[idx]));
                middle_series.push_back(Value::fromNumber(simd_result.middle[idx]));
                lower_series.push_back(Value::fromNumber(simd_result.lower[idx]));
            }
            
            result.setSeries("upper", upper_series);
            result.setSeries("middle", middle_series);
            result.setSeries("lower", lower_series);
            
            // 计算带宽和 %B（基于最新值）
            double curr_upper = upper_series.back().toNumber();
            double curr_middle = middle_series.back().toNumber();
            double curr_lower = lower_series.back().toNumber();
            
            double width = 0.0;
            if (curr_middle > 1e-10) {
                width = ((curr_upper - curr_lower) / curr_middle) * 100.0;
            }

            // 计算 %B
            double percent_b = 0.5;
            double band_range = curr_upper - curr_lower;
            if (band_range > 1e-10) {
                percent_b = (close.back() - curr_lower) / band_range;
            }

            result.set("width", Value::fromNumber(width));
            result.set("percent_b", Value::fromNumber(percent_b));
            
            return result;
        } catch (...) {
            // SIMD计算失败，回退到TA-Lib
        }
    }

    // 使用TA-Lib计算（回退路径）
    int outBegIdx, outNbElement;
    TA_RetCode retCode = TA_BBANDS(
        0, static_cast<int>(close.size()) - 1,
        close.data(),
        BOLL_PERIOD,
        BOLL_STD_DEV, BOLL_STD_DEV,  // 上下标准差倍数
        TA_MAType_SMA,     // 使用 SMA
        &outBegIdx, &outNbElement,
        upper_band.data(),  // 修复：使用正确的指针
        middle_band.data(),  // 修复：使用正确的指针
        lower_band.data()  // 修复：使用正确的指针
    );

    if (retCode != TA_SUCCESS || outNbElement == 0) {
        result.setSeries("upper", std::vector<Value>{});
        result.setSeries("middle", std::vector<Value>{});
        result.setSeries("lower", std::vector<Value>{});
        result.set("width", Value::fromNumber(0.0));
        result.set("percent_b", Value::fromNumber(0.5));
        return result;
    }
    
    // 提取有效数据（TA-Lib将有效数据存储在数组开头）
    std::vector<Value> upper_series, middle_series, lower_series;
    upper_series.reserve(outNbElement);
    middle_series.reserve(outNbElement);
    lower_series.reserve(outNbElement);
    
    for (int i = 0; i < outNbElement; ++i) {
        upper_series.push_back(Value::fromNumber(upper_band[i]));
        middle_series.push_back(Value::fromNumber(middle_band[i]));
        lower_series.push_back(Value::fromNumber(lower_band[i]));
    }
    
    result.setSeries("upper", upper_series);
    result.setSeries("middle", middle_series);
    result.setSeries("lower", lower_series);
    
    // 计算带宽和 %B（基于最新值）
    double curr_upper = upper_series.back().toNumber();
    double curr_middle = middle_series.back().toNumber();
    double curr_lower = lower_series.back().toNumber();
    
    double width = 0.0;
    if (curr_middle > 1e-10) {
        width = ((curr_upper - curr_lower) / curr_middle) * 100.0;
    }

    // 计算 %B
    double percent_b = 0.5;
    double band_range = curr_upper - curr_lower;
    if (band_range > 1e-10) {
        percent_b = (close.back() - curr_lower) / band_range;
    }

    result.set("width", Value::fromNumber(width));
    result.set("percent_b", Value::fromNumber(percent_b));

    return result;
}

} // namespace prophet::indicators
