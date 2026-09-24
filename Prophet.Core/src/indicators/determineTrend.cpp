/*
 * ============================================================================
 * 文件名：determineTrend.cpp
 * 功能：辅助函数 - determineTrend
 * ============================================================================
 */

#include "prophet/indicators/calculator.hpp"
#include "prophet/indicators/registry.hpp"
#include <cmath>
#include <algorithm>
#include <numeric>

namespace prophet::indicators {

std::string Calculator::determineTrend(const std::vector<double>& values) {
    if (values.size() < 3) {
        return "NEUTRAL";
    }

    // 简单趋势判断：基于最近几个值的平均变化
    size_t lookback = std::min(size_t(5), values.size());
    double sum_change = 0.0;

    for (size_t i = values.size() - lookback; i < static_cast<int>(values.size()) - 1; ++i) {
        sum_change += values[i + 1] - values[i];
    }

    double avg_change = sum_change / (lookback - 1);

    if (avg_change > 0.01) {
        return "BULLISH";
    } else if (avg_change < -0.01) {
        return "BEARISH";
    } else {
        return "NEUTRAL";
    }
}

} // namespace prophet::indicators