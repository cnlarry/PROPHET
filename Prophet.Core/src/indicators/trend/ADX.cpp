/*
 * ============================================================================
 * 文件名：ADX.cpp
 * 指标名：ADX
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

IndicatorResult Calculator::ADX(
    const std::vector<double>& high,
    const std::vector<double>& low,
    const std::vector<double>& close,
    int ADX_PERIOD
) {
    IndicatorResult result;

    if (close.size() < size_t(ADX_PERIOD * 2)) {
        result.setSeries("value", std::vector<Value>{});
        result.set("trend_strength", Value::fromString("WEAK"));
        return result;
    }

    std::vector<double> adx_values(close.size(), 0.0);
    int outBegIdx, outNbElement;
    TA_RetCode retCode = TA_ADX(
        0, static_cast<int>(close.size()) - 1,
        high.data(), low.data(), close.data(),
        ADX_PERIOD,
        &outBegIdx, &outNbElement,
        adx_values.data()  // 修复：使用正确的指针
    );

    if (retCode != TA_SUCCESS || outNbElement == 0) {
        result.setSeries("value", std::vector<Value>{});
        result.set("trend_strength", Value::fromString("WEAK"));
        return result;
    }
    
    // 提取有效数据（TA-Lib将有效数据存储在数组开头）
    std::vector<Value> adx_series;
    adx_series.reserve(outNbElement);
    for (int i = 0; i < outNbElement; ++i) {
        adx_series.push_back(Value::fromNumber(adx_values[i]));
    }
    result.setSeries("value", adx_series);

    double curr_adx = adx_series.back().toNumber();

    std::string strength;
    if (curr_adx < 20.0) {
        strength = "WEAK";
    } else if (curr_adx < 40.0) {
        strength = "MODERATE";
    } else if (curr_adx < 60.0) {
        strength = "STRONG";
    } else {
        strength = "VERY_STRONG";
    }

    result.set("trend_strength", Value::fromString(strength));

    return result;
}

} // namespace prophet::indicators
