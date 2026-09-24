/*
 * ============================================================================
 * 文件名：MA.cpp
 * 指标名：MA (Moving Average - Simple)
 * 类别：趋势指标
 * 
 * 从 calculator.cpp 拆分
 * 拆分日期：2025-10-30
 * 重命名：2025-10-31 (SMA -> MA)
 * 优化：2025-12-03 添加SIMD支持
 * ============================================================================
 */

#include "prophet/indicators/calculator.hpp"
#include "prophet/indicators/registry.hpp"
#include "prophet/simd/simd_math.hpp"
#include <ta_libc.h>
#include <cmath>
#include <algorithm>
#include <numeric>
#include <stdexcept>
#include <iostream>

namespace prophet::indicators {

// 基于SIMD的滑动窗口求和函数
static void simd_rolling_sum(const std::vector<double>& input, size_t period, std::vector<double>& output) {
    const size_t n = input.size();
    output.resize(n, 0.0);
    
    if (n < period) {
        return;
    }
    
    // 使用prophet::simd::rolling_sum实现
    prophet::simd::rolling_sum(input.data(), n, static_cast<int>(period), output.data());
}

IndicatorResult Calculator::MA(
    const std::vector<double>& close,
    int MA_PERIOD
) {
    IndicatorResult result;

    if (close.size() < size_t(MA_PERIOD)) {
        result.setSeries("value", std::vector<Value>{});
        result.set("slope", Value::fromNumber(0.0));
        result.set("distance", Value::fromNumber(0.0));
        return result;
    }

    std::vector<double> sma_values(close.size());
    
    // 使用SIMD优化的滑动窗口求和
    simd_rolling_sum(close, static_cast<size_t>(MA_PERIOD), sma_values);
    
    // 计算平均值
    double inv_period = 1.0 / static_cast<double>(MA_PERIOD);
    for (size_t i = MA_PERIOD - 1; i < close.size(); ++i) {
        sma_values[i] *= inv_period;
    }
    
    // 填充前MA_PERIOD-1个元素为0
    for (size_t i = 0; i < MA_PERIOD - 1; ++i) {
        sma_values[i] = 0.0;
    }
    
    // 提取有效数据
    std::vector<Value> ma_series;
    ma_series.reserve(close.size());
    for (double val : sma_values) {
        ma_series.push_back(Value::fromNumber(val));
    }
    result.setSeries("value", ma_series);

    double curr_sma = ma_series.back().toNumber();

    // 计算斜率（基于有效数据范围）
    std::vector<double> ma_valid(close.size());
    for (size_t i = 0; i < close.size(); ++i) {
        ma_valid[i] = ma_series[i].toNumber();
    }
    double slope = calculateSlope(ma_valid, 3);

    // 计算与当前价格的距离百分比
    double curr_price = close.back();
    double distance = 0.0;
    if (curr_sma > 1e-10) {
        distance = ((curr_price - curr_sma) / curr_sma) * 100.0;
    }

    result.set("slope", Value::fromNumber(slope));
    result.set("distance", Value::fromNumber(distance));

    return result;
}

} // namespace prophet::indicators
