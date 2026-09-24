/*
 * ============================================================================
 * 文件名：KAMA.cpp
 * 指标名：KAMA
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

IndicatorResult Calculator::KAMA(
    const std::vector<double>& close,
    int KAMA_PERIOD
) {
    IndicatorResult result;

    // 参数验证
    if (close.size() < size_t(KAMA_PERIOD + 10)) {
        result.setSeries("value", std::vector<Value>{});
        result.set("slope", Value::fromNumber(0.0));
        result.set("distance", Value::fromNumber(0.0));
        result.set("trend", Value::fromString("NEUTRAL"));
        return result;
    }

    // 调用TA-Lib KAMA
    std::vector<double> kama_values(close.size(), 0.0);
    int outBegIdx, outNbElement;
    TA_RetCode retCode = TA_KAMA(
        0, static_cast<int>(close.size()) - 1,
        close.data(),
        KAMA_PERIOD,
        &outBegIdx, &outNbElement,
        kama_values.data()  // 修复：使用正确的指针
    );

    if (retCode != TA_SUCCESS || outNbElement == 0) {
        result.setSeries("value", std::vector<Value>{});
        result.set("slope", Value::fromNumber(0.0));
        result.set("distance", Value::fromNumber(0.0));
        result.set("trend", Value::fromString("NEUTRAL"));
        return result;
    }

    // 提取有效数据（TA-Lib将有效数据存储在数组开头）
    std::vector<Value> kama_series;
    kama_series.reserve(outNbElement);
    for (int i = 0; i < outNbElement; ++i) {
        kama_series.push_back(Value::fromNumber(kama_values[i]));
    }
    result.setSeries("value", kama_series);

    double curr_kama = kama_series.back().toNumber();
    double curr_price = close.back();

    // 计算斜率和趋势（基于有效数据范围）
    std::vector<double> kama_valid(outNbElement);
    for (int i = 0; i < outNbElement; ++i) {
        kama_valid[i] = kama_series[i].toNumber();
    }
    double slope = calculateSlope(kama_valid, 3);

    // 计算与当前价格的距离百分比
    double distance = 0.0;
    if (std::abs(curr_kama) > 1e-10) {
        distance = ((curr_price - curr_kama) / curr_kama) * 100.0;
    }

    // 判断趋势
    std::string trend = determineTrend(kama_valid);

    result.set("slope", Value::fromNumber(slope));
    result.set("distance", Value::fromNumber(distance));
    result.set("trend", Value::fromString(trend));

    return result;
}

} // namespace prophet::indicators
