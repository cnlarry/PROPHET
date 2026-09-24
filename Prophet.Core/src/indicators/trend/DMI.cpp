/*
 * ============================================================================
 * 文件名：DMI.cpp
 * 指标名：DMI
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

IndicatorResult Calculator::DMI(
    const std::vector<double>& high,
    const std::vector<double>& low,
    const std::vector<double>& close,
    int DMI_PERIOD
) {
    IndicatorResult result;

    if (high.size() < size_t(DMI_PERIOD * 2)) {
        result.setSeries("plus_di", std::vector<Value>{});
        result.setSeries("minus_di", std::vector<Value>{});
        result.setSeries("adx", std::vector<Value>{});
        result.set("crossover_type", Value::fromString("NONE"));
        result.set("trend", Value::fromString("NEUTRAL"));
        result.set("trend_strength", Value::fromString("WEAK"));
        return result;
    }

    std::vector<double> plus_di(high.size(), 50.0);
    std::vector<double> minus_di(high.size(), 50.0);
    std::vector<double> adx_values(high.size(), 0.0);
    int outBegIdx, outNbElement;
    TA_RetCode retCode = TA_PLUS_DI(
        0, static_cast<int>(high.size()) - 1,
        high.data(), low.data(), close.data(),
        DMI_PERIOD,
        &outBegIdx, &outNbElement,
        plus_di.data()  // 修复：使用正确的指针
    );

    if (retCode == TA_SUCCESS && outNbElement > 0) {
        retCode = TA_MINUS_DI(
            0, static_cast<int>(high.size()) - 1,
            high.data(), low.data(), close.data(),
            DMI_PERIOD,
            &outBegIdx, &outNbElement,
            minus_di.data()  // 修复：使用正确的指针
        );
    }

    if (retCode == TA_SUCCESS && outNbElement > 0) {
        retCode = TA_ADX(
            0, static_cast<int>(high.size()) - 1,
            high.data(), low.data(), close.data(),
            DMI_PERIOD,
            &outBegIdx, &outNbElement,
            adx_values.data()  // 修复：使用正确的指针
        );
    }

    if (retCode != TA_SUCCESS || outNbElement == 0) {
        result.setSeries("plus_di", std::vector<Value>{});
        result.setSeries("minus_di", std::vector<Value>{});
        result.setSeries("adx", std::vector<Value>{});
        result.set("crossover_type", Value::fromString("NONE"));
        result.set("trend", Value::fromString("NEUTRAL"));
        result.set("trend_strength", Value::fromString("WEAK"));
        return result;
    }
    
    // 提取有效数据（从outBegIdx开始）
    std::vector<Value> plus_di_series, minus_di_series, adx_series;
    plus_di_series.reserve(outNbElement);
    minus_di_series.reserve(outNbElement);
    adx_series.reserve(outNbElement);
    
    for (int i = 0; i < outNbElement; ++i) {
        plus_di_series.push_back(Value::fromNumber(plus_di[i]));
        minus_di_series.push_back(Value::fromNumber(minus_di[i]));
        adx_series.push_back(Value::fromNumber(adx_values[i]));
    }
    
    result.setSeries("plus_di", plus_di_series);
    result.setSeries("minus_di", minus_di_series);
    result.setSeries("adx", adx_series);

    // 检测+DI/-DI交叉（基于有效数据范围）
    std::vector<double> plus_di_valid(outNbElement), minus_di_valid(outNbElement);
    for (int i = 0; i < outNbElement; ++i) {
        plus_di_valid[i] = plus_di_series[i].toNumber();
        minus_di_valid[i] = minus_di_series[i].toNumber();
    }
    std::string crossover = detectCrossover(plus_di_valid, minus_di_valid);

    // 判断趋势（基于最新值）
    double curr_plus_di = plus_di_series.back().toNumber();
    double curr_minus_di = minus_di_series.back().toNumber();
    std::string trend;
    if (curr_plus_di > curr_minus_di) {
        trend = "BULLISH";
    } else if (curr_minus_di > curr_plus_di) {
        trend = "BEARISH";
    } else {
        trend = "NEUTRAL";
    }

    // 判断趋势强度（基于最新值）
    double curr_adx = adx_series.back().toNumber();
    std::string strength;
    if (curr_adx < 20.0) {
        strength = "WEAK";
    } else if (curr_adx < 40.0) {
        strength = "MODERATE";
    } else if (curr_adx < 60.0) {
        strength = "STRONG";
    } else {
        strength = "VERY_STRONG";
    }

    result.set("crossover_type", Value::fromString(crossover));
    result.set("trend", Value::fromString(trend));
    result.set("trend_strength", Value::fromString(strength));

    return result;
}

} // namespace prophet::indicators
