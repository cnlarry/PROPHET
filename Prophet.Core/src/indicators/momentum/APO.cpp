/*
 * ============================================================================
 * 文件名：APO.cpp
 * 指标名：APO
 * 类别：动量指标
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

IndicatorResult Calculator::APO(
    const std::vector<double>& close,
    int APO_FAST_PERIOD,
    int APO_SLOW_PERIOD
) {
    IndicatorResult result;

    if (close.size() < size_t(APO_SLOW_PERIOD)) {
        result.setSeries("value", std::vector<Value>{});
        result.set("trend", Value::fromString("NEUTRAL"));
        return result;
    }

    std::vector<double> apo_values(close.size(), 0.0);
    int outBegIdx, outNbElement;
    TA_RetCode retCode = TA_APO(
        0, static_cast<int>(close.size()) - 1,
        close.data(),
        APO_FAST_PERIOD,
        APO_SLOW_PERIOD,
        TA_MAType_EMA,
        &outBegIdx, &outNbElement,
        apo_values.data()  // 修复：使用正确的指针
    );

    if (retCode != TA_SUCCESS || outNbElement == 0) {
        result.setSeries("value", std::vector<Value>{});
        result.set("trend", Value::fromString("NEUTRAL"));
        return result;
    }

    // 提取有效数据（TA-Lib将有效数据存储在数组开头）
    std::vector<Value> apo_series;
    apo_series.reserve(outNbElement);
    for (int i = 0; i < outNbElement; ++i) {
        apo_series.push_back(Value::fromNumber(apo_values[i]));
    }
    result.setSeries("value", apo_series);

    // 判断趋势（基于有效数据范围）
    std::vector<double> apo_valid(outNbElement);
    for (int i = 0; i < outNbElement; ++i) {
        apo_valid[i] = apo_series[i].toNumber();
    }
    std::string trend = determineTrend(apo_valid);

    result.set("trend", Value::fromString(trend));

    return result;
}

} // namespace prophet::indicators
