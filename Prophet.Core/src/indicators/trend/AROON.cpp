/*
 * ============================================================================
 * 文件名：AROON.cpp
 * 指标名：AROON
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

IndicatorResult Calculator::AROON(
    const std::vector<double>& high,
    const std::vector<double>& low,
    int AROON_PERIOD
) {
    IndicatorResult result;

    if (high.size() < size_t(AROON_PERIOD + 1)) {
        result.setSeries("aroon_up", std::vector<Value>{});
        result.setSeries("aroon_down", std::vector<Value>{});
        result.setSeries("oscillator", std::vector<Value>{});
        result.set("trend", Value::fromString("NEUTRAL"));
        return result;
    }

    std::vector<double> aroon_up(high.size(), 50.0);
    std::vector<double> aroon_down(low.size(), 50.0);
    int outBegIdx, outNbElement;
    TA_RetCode retCode = TA_AROON(
        0, static_cast<int>(high.size()) - 1,
        high.data(), low.data(),
        AROON_PERIOD,
        &outBegIdx, &outNbElement,
        aroon_down.data(),  // 修复：使用正确的指针
        aroon_up.data()  // 修复：使用正确的指针
    );

    if (retCode != TA_SUCCESS || outNbElement == 0) {
        result.setSeries("aroon_up", std::vector<Value>{});
        result.setSeries("aroon_down", std::vector<Value>{});
        result.setSeries("oscillator", std::vector<Value>{});
        result.set("trend", Value::fromString("NEUTRAL"));
        return result;
    }
    
    // 提取有效数据（从outBegIdx开始）
    std::vector<Value> aroon_up_series, aroon_down_series, oscillator_series;
    aroon_up_series.reserve(outNbElement);
    aroon_down_series.reserve(outNbElement);
    oscillator_series.reserve(outNbElement);
    
    for (int i = 0; i < outNbElement; ++i) {
        double up_val = aroon_up[i];
        double down_val = aroon_down[i];
        double osc_val = up_val - down_val;
        
        aroon_up_series.push_back(Value::fromNumber(up_val));
        aroon_down_series.push_back(Value::fromNumber(down_val));
        oscillator_series.push_back(Value::fromNumber(osc_val));
    }
    
    result.setSeries("aroon_up", aroon_up_series);
    result.setSeries("aroon_down", aroon_down_series);
    result.setSeries("oscillator", oscillator_series);

    // 判断趋势（基于最新值）
    double oscillator = oscillator_series.back().toNumber();
    std::string trend;
    if (oscillator > 20.0) {
        trend = "BULLISH";
    } else if (oscillator < -20.0) {
        trend = "BEARISH";
    } else {
        trend = "NEUTRAL";
    }

    result.set("trend", Value::fromString(trend));

    return result;
}

} // namespace prophet::indicators
