/*
 * ============================================================================
 * 文件名：NATR.cpp
 * 指标名：NATR
 * 类别：波动率指标
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

IndicatorResult Calculator::NATR(
    const std::vector<double>& high,
    const std::vector<double>& low,
    const std::vector<double>& close,
    int NATR_PERIOD
) {
    IndicatorResult result;

    if (close.size() < size_t(NATR_PERIOD)) {
        result.setSeries("value", std::vector<Value>{});
        result.set("volatility", Value::fromString("LOW"));
        return result;
    }

    std::vector<double> natr_values(close.size(), 0.0);
    int outBegIdx, outNbElement;
    TA_RetCode retCode = TA_NATR(
        0, static_cast<int>(close.size()) - 1,
        high.data(), low.data(), close.data(),
        NATR_PERIOD,
        &outBegIdx, &outNbElement,
        natr_values.data()  // 修复：使用正确的指针
    );

    if (retCode != TA_SUCCESS || outNbElement == 0) {
        result.setSeries("value", std::vector<Value>{});
        result.set("volatility", Value::fromString("LOW"));
        return result;
    }

    // 提取有效数据（TA-Lib将有效数据存储在数组开头）
    std::vector<Value> natr_series;
    natr_series.reserve(outNbElement);
    for (int i = 0; i < outNbElement; ++i) {
        natr_series.push_back(Value::fromNumber(natr_values[i]));
    }
    result.setSeries("value", natr_series);

    double curr_value = natr_series.back().toNumber();

    // 判断波动率等级
    std::string volatility = "MEDIUM";
    if (curr_value < 1.0) {
        volatility = "LOW";
    } else if (curr_value > 3.0) {
        volatility = "HIGH";
    }

    result.set("volatility", Value::fromString(volatility));

    return result;
}

} // namespace prophet::indicators
