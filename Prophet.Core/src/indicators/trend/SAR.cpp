/*
 * ============================================================================
 * 文件名：SAR.cpp
 * 指标名：SAR
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

IndicatorResult Calculator::SAR(
    const std::vector<double>& high,
    const std::vector<double>& low,
    double SAR_ACCELERATION,
    double SAR_MAXIMUM
) {
    IndicatorResult result;

    if (high.size() < 10) {
        result.setSeries("value", std::vector<Value>{});
        result.set("trend", Value::fromString("NEUTRAL"));
        return result;
    }

    std::vector<double> sar_values(high.size(), 0.0);
    int outBegIdx, outNbElement;
    TA_RetCode retCode = TA_SAR(
        0, static_cast<int>(high.size()) - 1,
        high.data(), low.data(),
        SAR_ACCELERATION, SAR_MAXIMUM,
        &outBegIdx, &outNbElement,
        sar_values.data()  // 修复：使用正确的指针
    );

    if (retCode != TA_SUCCESS || outNbElement == 0) {
        result.setSeries("value", std::vector<Value>{});
        result.set("trend", Value::fromString("NEUTRAL"));
        return result;
    }
    
    // 提取有效数据（TA-Lib将有效数据存储在数组开头）
    std::vector<Value> sar_series;
    sar_series.reserve(outNbElement);
    for (int i = 0; i < outNbElement; ++i) {
        sar_series.push_back(Value::fromNumber(sar_values[i]));
    }
    result.setSeries("value", sar_series);
    
    double curr_sar = sar_series.back().toNumber();
    double curr_price = (high.back() + low.back()) / 2.0;
    std::string trend = (curr_price > curr_sar) ? "BULLISH" : "BEARISH";

    result.set("trend", Value::fromString(trend));

    return result;
}

} // namespace prophet::indicators
