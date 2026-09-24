/*
 * ============================================================================
 * 文件名：MAMA.cpp
 * 指标名：MAMA
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

IndicatorResult Calculator::MAMA(
    const std::vector<double>& close,
    double MAMA_FASTLIMIT,
    double MAMA_SLOWLIMIT
) {
    IndicatorResult result;

    if (close.size() < 32) {  // MAMA需要至少32个样本
        result.setSeries("mama", std::vector<Value>{});
        result.setSeries("fama", std::vector<Value>{});
        result.set("crossover_type", Value::fromString("NONE"));
        result.set("trend", Value::fromString("NEUTRAL"));
        return result;
    }

    std::vector<double> mama_values(close.size(), 0.0);
    std::vector<double> fama_values(close.size(), 0.0);
    int outBegIdx, outNbElement;
    TA_RetCode retCode = TA_MAMA(
        0, static_cast<int>(close.size()) - 1,
        close.data(),
        MAMA_FASTLIMIT,
        MAMA_SLOWLIMIT,
        &outBegIdx, &outNbElement,
        mama_values.data(),  // 修复：使用正确的指针
        fama_values.data()  // 修复：使用正确的指针
    );

    if (retCode != TA_SUCCESS || outNbElement == 0) {
        result.setSeries("mama", std::vector<Value>{});
        result.setSeries("fama", std::vector<Value>{});
        result.set("crossover_type", Value::fromString("NONE"));
        result.set("trend", Value::fromString("NEUTRAL"));
        return result;
    }

    // 提取有效数据（从outBegIdx开始）
    std::vector<Value> mama_series, fama_series;
    mama_series.reserve(outNbElement);
    fama_series.reserve(outNbElement);
    
    for (int i = 0; i < outNbElement; ++i) {
        mama_series.push_back(Value::fromNumber(mama_values[i]));
        fama_series.push_back(Value::fromNumber(fama_values[i]));
    }
    
    result.setSeries("mama", mama_series);
    result.setSeries("fama", fama_series);

    // 检测交叉（基于有效数据范围）
    std::vector<double> mama_valid(outNbElement), fama_valid(outNbElement);
    for (int i = 0; i < outNbElement; ++i) {
        mama_valid[i] = mama_series[i].toNumber();
        fama_valid[i] = fama_series[i].toNumber();
    }
    std::string crossover = detectCrossover(mama_valid, fama_valid);

    // 判断趋势（基于MAMA序列）
    std::string trend = determineTrend(mama_valid);

    result.set("crossover_type", Value::fromString(crossover));
    result.set("trend", Value::fromString(trend));

    return result;
}

} // namespace prophet::indicators
