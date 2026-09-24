/*
 * ============================================================================
 * 文件名：detectCrossover.cpp
 * 功能：辅助函数 - detectCrossover
 * ============================================================================
 */

#include "prophet/indicators/calculator.hpp"
#include "prophet/indicators/registry.hpp"
#include <cmath>
#include <algorithm>
#include <numeric>

namespace prophet::indicators {

std::string Calculator::detectCrossover(
    const std::vector<double>& line1,
    const std::vector<double>& line2
) {
    if (line1.size() < 2 || line2.size() < 2) {
        return "NONE";
    }

    size_t idx = static_cast<int>(line1.size()) - 1;
    double curr1 = line1[idx];
    double curr2 = line2[idx];
    double prev1 = line1[idx - 1];
    double prev2 = line2[idx - 1];

    // 金叉：line1 上穿 line2
    if (prev1 <= prev2 && curr1 > curr2) {
        return "GOLDEN_CROSS";
    }

    // 死叉：line1 下穿 line2
    if (prev1 >= prev2 && curr1 < curr2) {
        return "DEATH_CROSS";
    }

    return "NONE";
}

} // namespace prophet::indicators