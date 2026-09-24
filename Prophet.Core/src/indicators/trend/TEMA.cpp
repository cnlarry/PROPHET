/*
 * ============================================================================
 * 文件名：TEMA.cpp
 * 指标名：TEMA
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

IndicatorResult Calculator::TEMA(
    const std::vector<double>& close,
    int TEMA_PERIOD
) {
    IndicatorResult result;

    if (close.size() < size_t(TEMA_PERIOD * 3)) {
        result.setSeries("value", std::vector<Value>{});
        result.set("slope", Value::fromNumber(0.0));
        result.set("distance", Value::fromNumber(0.0));
        result.set("trend", Value::fromString("NEUTRAL"));
        return result;
    }

    std::vector<double> tema_values(close.size(), 0.0);
    int outBegIdx, outNbElement;
    TA_RetCode retCode = TA_TEMA(
        0, static_cast<int>(close.size()) - 1,
        close.data(),
        TEMA_PERIOD,
        &outBegIdx, &outNbElement,
        tema_values.data()  // 修复：使用正确的指针
    );

    if (retCode != TA_SUCCESS || outNbElement == 0) {
        result.setSeries("value", std::vector<Value>{});
        result.set("slope", Value::fromNumber(0.0));
        result.set("distance", Value::fromNumber(0.0));
        result.set("trend", Value::fromString("NEUTRAL"));
        return result;
    }
    
    // 提取有效数据（TA-Lib将有效数据存储在数组开头）
    std::vector<Value> tema_series;
    tema_series.reserve(outNbElement);
    for (int i = 0; i < outNbElement; ++i) {
        tema_series.push_back(Value::fromNumber(tema_values[i]));
    }
    result.setSeries("value", tema_series);
    
    double curr_tema = tema_series.back().toNumber();
    double curr_price = close.back();

    // 计算斜率和趋势（基于有效数据范围）
    std::vector<double> tema_valid(outNbElement);
    for (int i = 0; i < outNbElement; ++i) {
        tema_valid[i] = tema_series[i].toNumber();
    }
    double slope = calculateSlope(tema_valid, 3);

    // 计算距离
    double distance = 0.0;
    if (curr_tema > 1e-10) {
        distance = ((curr_price - curr_tema) / curr_tema) * 100.0;
    }

    // 判断趋势
    std::string trend = determineTrend(tema_valid);

    result.set("slope", Value::fromNumber(slope));
    result.set("distance", Value::fromNumber(distance));
    result.set("trend", Value::fromString(trend));

    return result;
}

} // namespace prophet::indicators
