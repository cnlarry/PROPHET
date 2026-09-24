/*
 * ============================================================================
 * 文件名：CCI.cpp
 * 指标名：CCI
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

IndicatorResult Calculator::CCI(
    const std::vector<double>& high,
    const std::vector<double>& low,
    const std::vector<double>& close,
    int CCI_PERIOD
) {
    IndicatorResult result;

    if (close.size() < size_t(CCI_PERIOD)) {
        result.setSeries("value", std::vector<Value>{});
        result.set("overbought", Value::fromBoolean(false));
        result.set("oversold", Value::fromBoolean(false));
        result.set("trend", Value::fromString("NEUTRAL"));
        return result;
    }

    std::vector<double> cci_values(close.size(), 0.0);
    int outBegIdx, outNbElement;
    TA_RetCode retCode = TA_CCI(
        0, static_cast<int>(close.size()) - 1,
        high.data(), low.data(), close.data(),
        CCI_PERIOD,
        &outBegIdx, &outNbElement,
        cci_values.data()  // 修复：使用正确的指针
    );

    if (retCode != TA_SUCCESS || outNbElement == 0) {
        result.setSeries("value", std::vector<Value>{});
        result.set("overbought", Value::fromBoolean(false));
        result.set("oversold", Value::fromBoolean(false));
        result.set("trend", Value::fromString("NEUTRAL"));
        return result;
    }
    
    // 提取有效数据（TA-Lib将有效数据存储在数组开头）
    std::vector<Value> cci_series;
    cci_series.reserve(outNbElement);
    for (int i = 0; i < outNbElement; ++i) {
        cci_series.push_back(Value::fromNumber(cci_values[i]));
    }
    result.setSeries("value", cci_series);
    
    double curr_cci = cci_series.back().toNumber();
    bool overbought = curr_cci > 100.0;
    bool oversold = curr_cci < -100.0;
    
    // 判断趋势（基于有效数据范围）
    std::vector<double> valid_cci_values(outNbElement);
    for (int i = 0; i < outNbElement; ++i) {
        valid_cci_values[i] = cci_series[i].toNumber();
    }
    std::string trend = determineTrend(valid_cci_values);

    result.set("overbought", Value::fromBoolean(overbought));
    result.set("oversold", Value::fromBoolean(oversold));
    result.set("trend", Value::fromString(trend));

    return result;
}

} // namespace prophet::indicators
