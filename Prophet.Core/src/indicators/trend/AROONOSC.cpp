/*
 * ============================================================================
 * 文件名：AROONOSC.cpp
 * 指标名：AROONOSC
 * 类别：趋势指标
 * 
 * 从 calculator.cpp 拆分
 * 拆分日期：2025-10-30
 * ============================================================================
 */

#include "prophet/indicators/calculator.hpp"
#include "prophet/indicators/registry.hpp"
#include <ta_libc.h>
#include <cmath>
#include <algorithm>
#include <numeric>
#include <stdexcept>

namespace prophet::indicators {

IndicatorResult Calculator::AROONOSC(
    const std::vector<double>& high,
    const std::vector<double>& low,
    const IndicatorParams& params
) {
    int period = params.get_int("AROONOSC_PERIOD", 14);
    
    if (high.size() < static_cast<size_t>(period + 1)) {
        throw std::invalid_argument("AROONOSC: 数据不足");
    }
    
    std::vector<double> out(high.size());
    int outBegIdx = 0, outNbElement = 0;
    
    TA_RetCode ret = TA_AROONOSC(0, static_cast<int>(high.size()) - 1,
                                  high.data(), low.data(),
                                  period, &outBegIdx, &outNbElement, out.data());
    
    if (ret != TA_SUCCESS || outNbElement == 0) {
        throw std::runtime_error("AROONOSC: TA-Lib计算失败");
    }
    
    IndicatorResult result;
    double value = out[outNbElement - 1];  // 修复：正确的索引
    result.set("value", Value::fromNumber(value));
    
    // 趋势判断和强度
    std::string trend = "NEUTRAL";
    std::string strength = "WEAK";
    
    if (value > 50) {
        trend = "BULLISH";
        strength = "STRONG";
    } else if (value > 25) {
        trend = "BULLISH";
        strength = "MODERATE";
    } else if (value < -50) {
        trend = "BEARISH";
        strength = "STRONG";
    } else if (value < -25) {
        trend = "BEARISH";
        strength = "MODERATE";
    }
    
    result.set("trend", Value::fromString(trend));
    result.set("strength", Value::fromString(strength));
    
    // 零轴穿越检测
    if (outNbElement >= 2) {
        double prev_value = out[outBegIdx + outNbElement - 2];
        bool zero_cross = (prev_value * value < 0);
        result.set("zero_cross", Value::fromNumber(zero_cross ? 1.0 : 0.0));
    }
    
    return result;
}

} // namespace prophet::indicators
