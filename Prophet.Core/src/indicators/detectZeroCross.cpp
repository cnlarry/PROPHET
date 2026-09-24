/*
 * ============================================================================
 * 文件名：detectZeroCross.cpp
 * 功能：辅助函数 - detectZeroCross
 * ============================================================================
 */

#include "prophet/indicators/calculator.hpp"
#include "prophet/indicators/registry.hpp"
#include <cmath>
#include <algorithm>

namespace prophet::indicators {

bool Calculator::detectZeroCross(const std::vector<double>& values) {
    if (values.size() < 2) {
        return false;
    }

    size_t idx = static_cast<int>(values.size()) - 1;
    double curr = values[idx];
    double prev = values[idx - 1];

    // 检测零值穿越：前一个值和当前值符号不同
    return (prev < 0 && curr > 0) || (prev > 0 && curr < 0) ||
           (std::abs(curr) < 1e-10 && std::abs(prev) > 1e-10);
}

} // namespace prophet::indicators