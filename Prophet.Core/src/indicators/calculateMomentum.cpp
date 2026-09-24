/*
 * ============================================================================
 * 文件名：calculateMomentum.cpp
 * 功能：辅助函数 - calculateMomentum
 * ============================================================================
 */

#include "prophet/indicators/calculator.hpp"
#include "prophet/indicators/registry.hpp"
#include <cmath>
#include <algorithm>
#include <numeric>

namespace prophet::indicators {

double Calculator::calculateMomentum(const std::vector<double>& values) {
    if (values.size() < 2) {
        return 0.0;
    }

    // 计算动量：最近变化的绝对值，归一化到 0-1
    size_t idx = static_cast<int>(values.size()) - 1;
    double change = std::abs(values[idx] - values[idx - 1]);
    double avg_value = (std::abs(values[idx]) + std::abs(values[idx - 1])) / 2.0;

    if (avg_value < 1e-10) {
        return 0.0;
    }

    double momentum = change / avg_value;
    return std::min(1.0, momentum);
}

} // namespace prophet::indicators