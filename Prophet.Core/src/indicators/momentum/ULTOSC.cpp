/*
 * ============================================================================
 * 文件名：ULTOSC.cpp
 * 指标名：ULTOSC
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

IndicatorResult Calculator::ULTOSC(
    const std::vector<double>& high,
    const std::vector<double>& low,
    const std::vector<double>& close,
    int PERIOD1,
    int PERIOD2,
    int PERIOD3
) {
    IndicatorResult result;

    if (close.size() < size_t(PERIOD3 + 1)) {
        result.setSeries("value", std::vector<Value>{});
        result.set("overbought", Value::fromBoolean(false));
        result.set("oversold", Value::fromBoolean(false));
        result.set("trend", Value::fromString("NEUTRAL"));
        return result;
    }

    std::vector<double> ultosc_values(close.size(), 0.0);
    int outBegIdx, outNbElement;
    TA_RetCode retCode = TA_ULTOSC(
        0, static_cast<int>(close.size()) - 1,
        high.data(), low.data(), close.data(),
        PERIOD1,
        PERIOD2,
        PERIOD3,
        &outBegIdx, &outNbElement,
        ultosc_values.data()  // 修复：使用正确的指针
    );

    if (retCode != TA_SUCCESS || outNbElement == 0) {
        result.setSeries("value", std::vector<Value>{});
        result.set("overbought", Value::fromBoolean(false));
        result.set("oversold", Value::fromBoolean(false));
        result.set("trend", Value::fromString("NEUTRAL"));
        return result;
    }

    // 提取有效数据（TA-Lib将有效数据存储在数组开头）
    std::vector<Value> ultosc_series;
    ultosc_series.reserve(outNbElement);
    for (int i = 0; i < outNbElement; ++i) {
        ultosc_series.push_back(Value::fromNumber(ultosc_values[i]));
    }
    result.setSeries("value", ultosc_series);

    double curr_value = ultosc_series.back().toNumber();
    bool overbought = curr_value > 70.0;
    bool oversold = curr_value < 30.0;

    // 判断趋势（基于有效数据范围）
    std::vector<double> ultosc_valid(outNbElement);
    for (int i = 0; i < outNbElement; ++i) {
        ultosc_valid[i] = ultosc_series[i].toNumber();
    }
    std::string trend = determineTrend(ultosc_valid);

    result.set("overbought", Value::fromBoolean(overbought));
    result.set("oversold", Value::fromBoolean(oversold));
    result.set("trend", Value::fromString(trend));

    return result;
}

} // namespace prophet::indicators
