/*
 * ============================================================================
 * 文件名：ADOSC.cpp
 * 指标名：ADOSC
 * 类别：成交量指标
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

IndicatorResult Calculator::ADOSC(
    const std::vector<double>& high,
    const std::vector<double>& low,
    const std::vector<double>& close,
    const std::vector<double>& volume,
    int FAST_PERIOD,
    int SLOW_PERIOD
) {
    IndicatorResult result;

    if (close.size() < size_t(SLOW_PERIOD)) {
        result.setSeries("value", std::vector<Value>{});
        result.set("crossover_type", Value::fromString("NONE"));
        result.set("trend", Value::fromString("NEUTRAL"));
        return result;
    }

    std::vector<double> adosc_values(close.size(), 0.0);
    int outBegIdx, outNbElement;
    TA_RetCode retCode = TA_ADOSC(
        0, static_cast<int>(close.size()) - 1,
        high.data(), low.data(), close.data(), volume.data(),
        FAST_PERIOD,
        SLOW_PERIOD,
        &outBegIdx, &outNbElement,
        adosc_values.data()  // 修复：使用正确的指针
    );

    if (retCode != TA_SUCCESS || outNbElement == 0) {
        result.setSeries("value", std::vector<Value>{});
        result.set("crossover_type", Value::fromString("NONE"));
        result.set("trend", Value::fromString("NEUTRAL"));
        return result;
    }

    // 提取有效数据（TA-Lib将有效数据存储在数组开头）
    std::vector<Value> adosc_series;
    adosc_series.reserve(outNbElement);
    for (int i = 0; i < outNbElement; ++i) {
        adosc_series.push_back(Value::fromNumber(adosc_values[i]));
    }
    result.setSeries("value", adosc_series);

    // 检测零轴穿越（基于有效数据范围）
    std::vector<double> adosc_valid(outNbElement);
    for (int i = 0; i < outNbElement; ++i) {
        adosc_valid[i] = adosc_series[i].toNumber();
    }
    std::string crossover = detectZeroCross(adosc_valid) ? 
        (adosc_series.back().toNumber() > 0.0 ? "GOLDEN_CROSS" : "DEATH_CROSS") : "NONE";

    // 判断趋势（基于有效数据范围）
    std::string trend = determineTrend(adosc_valid);

    result.set("crossover_type", Value::fromString(crossover));
    result.set("trend", Value::fromString(trend));

    return result;
}

} // namespace prophet::indicators
