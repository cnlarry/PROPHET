/*
 * ============================================================================
 * 文件名：MTM.cpp
 * 指标名：MTM (Momentum)
 * 类别：动量指标
 * 
 * 创建日期：2025-10-31
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

IndicatorResult Calculator::MTM(
    const std::vector<double>& close,
    int MTM_PERIOD
) {
    IndicatorResult result;

    if (close.size() <= static_cast<size_t>(MTM_PERIOD)) {
        result.setSeries("value", std::vector<Value>{});
        result.set("is_positive", Value::fromBoolean(false));
        result.set("trend", Value::fromString("NEUTRAL"));
        return result;
    }

    // 使用TA-Lib的MOM函数
    std::vector<double> mtm_values(close.size(), 0.0);
    int outBegIdx, outNbElement;
    
    TA_RetCode retCode = TA_MOM(
        0, static_cast<int>(close.size()) - 1,
        close.data(),
        MTM_PERIOD,
        &outBegIdx, &outNbElement,
        mtm_values.data()
    );

    if (retCode != TA_SUCCESS || outNbElement == 0) {
        result.setSeries("value", std::vector<Value>{});
        result.set("is_positive", Value::fromBoolean(false));
        result.set("trend", Value::fromString("NEUTRAL"));
        return result;
    }

    // 提取有效数据（TA-Lib将有效数据存储在数组开头）
    std::vector<Value> mtm_series;
    mtm_series.reserve(outNbElement);
    for (int i = 0; i < outNbElement; ++i) {
        mtm_series.push_back(Value::fromNumber(mtm_values[i]));
    }
    result.setSeries("value", mtm_series);

    double mtm_current = mtm_series.back().toNumber();
    bool is_positive = mtm_current > 0.0;
    
    // 判断趋势（基于最新值）
    std::string trend = "NEUTRAL";
    if (outNbElement >= 2) {
        double mtm_prev = mtm_series[outNbElement - 2].toNumber();
        if (mtm_current > mtm_prev && mtm_current > 0) {
            trend = "BULLISH";
        } else if (mtm_current < mtm_prev && mtm_current < 0) {
            trend = "BEARISH";
        }
    }

    result.set("is_positive", Value::fromBoolean(is_positive));
    result.set("trend", Value::fromString(trend));

    return result;
}

} // namespace prophet::indicators

