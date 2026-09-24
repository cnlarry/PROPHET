/*
 * ============================================================================
 * 文件名：WR.cpp
 * 指标名：WR (Williams %R)
 * 类别：动量指标
 * 
 * 从 calculator.cpp 拆分
 * 拆分日期：2025-10-30
 * 重命名：2025-10-31 (WILLR -> WR)
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

IndicatorResult Calculator::WR(
    const std::vector<double>& high,
    const std::vector<double>& low,
    const std::vector<double>& close,
    int WR_PERIOD
) {
    IndicatorResult result;

    if (close.size() < size_t(WR_PERIOD)) {
        result.setSeries("value", std::vector<Value>{});
        result.set("overbought", Value::fromBoolean(false));
        result.set("oversold", Value::fromBoolean(false));
        return result;
    }

    std::vector<double> willr_values(close.size(), -50.0);
    int outBegIdx, outNbElement;
    TA_RetCode retCode = TA_WILLR(
        0, static_cast<int>(close.size()) - 1,
        high.data(), low.data(), close.data(),
        WR_PERIOD,
        &outBegIdx, &outNbElement,
        willr_values.data()  // 修复：使用正确的指针
    );

    if (retCode != TA_SUCCESS || outNbElement == 0) {
        result.setSeries("value", std::vector<Value>{});
        result.set("overbought", Value::fromBoolean(false));
        result.set("oversold", Value::fromBoolean(false));
        return result;
    }
    
    // 提取有效数据（TA-Lib将有效数据存储在数组开头）
    std::vector<Value> wr_series;
    wr_series.reserve(outNbElement);
    for (int i = 0; i < outNbElement; ++i) {
        wr_series.push_back(Value::fromNumber(willr_values[i]));
    }
    result.setSeries("value", wr_series);
    
    double curr_willr = wr_series.back().toNumber();
    bool overbought = curr_willr > -20.0;
    bool oversold = curr_willr < -80.0;

    result.set("overbought", Value::fromBoolean(overbought));
    result.set("oversold", Value::fromBoolean(oversold));

    return result;
}

} // namespace prophet::indicators
