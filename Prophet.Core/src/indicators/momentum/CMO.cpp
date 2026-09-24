/*
 * ============================================================================
 * 文件名：CMO.cpp
 * 指标名：CMO
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

IndicatorResult Calculator::CMO(
    const std::vector<double>& close,
    int CMO_PERIOD
) {
    IndicatorResult result;

    if (close.size() < size_t(CMO_PERIOD + 1)) {
        result.setSeries("value", std::vector<Value>{});
        result.set("overbought", Value::fromBoolean(false));
        result.set("oversold", Value::fromBoolean(false));
        result.set("zero_cross", Value::fromBoolean(false));
        result.set("trend", Value::fromString("NEUTRAL"));
        return result;
    }

    std::vector<double> cmo_values(close.size(), 0.0);
    int outBegIdx, outNbElement;
    TA_RetCode retCode = TA_CMO(
        0, static_cast<int>(close.size()) - 1,
        close.data(),
        CMO_PERIOD,
        &outBegIdx, &outNbElement,
        cmo_values.data()  // 修复：使用正确的指针
    );

    if (retCode != TA_SUCCESS || outNbElement == 0) {
        result.setSeries("value", std::vector<Value>{});
        result.set("overbought", Value::fromBoolean(false));
        result.set("oversold", Value::fromBoolean(false));
        result.set("zero_cross", Value::fromBoolean(false));
        result.set("trend", Value::fromString("NEUTRAL"));
        return result;
    }
    
    // 提取有效数据（TA-Lib将有效数据存储在数组开头）
    std::vector<Value> cmo_series;
    cmo_series.reserve(outNbElement);
    for (int i = 0; i < outNbElement; ++i) {
        cmo_series.push_back(Value::fromNumber(cmo_values[i]));
    }
    result.setSeries("value", cmo_series);

    double curr_cmo = cmo_series.back().toNumber();
    bool overbought = curr_cmo > 50.0;
    bool oversold = curr_cmo < -50.0;
    
    // 检测零轴穿越和趋势（基于有效数据范围）
    std::vector<double> valid_cmo_values(outNbElement);
    for (int i = 0; i < outNbElement; ++i) {
        valid_cmo_values[i] = cmo_series[i].toNumber();
    }
    bool zero_cross = detectZeroCross(valid_cmo_values);
    std::string trend = determineTrend(valid_cmo_values);

    result.set("overbought", Value::fromBoolean(overbought));
    result.set("oversold", Value::fromBoolean(oversold));
    result.set("zero_cross", Value::fromBoolean(zero_cross));
    result.set("trend", Value::fromString(trend));

    return result;
}

} // namespace prophet::indicators
