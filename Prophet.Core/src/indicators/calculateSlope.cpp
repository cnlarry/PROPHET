/*
 * ============================================================================
 * 文件名：calculateSlope.cpp
 * 功能：辅助函数 - calculateSlope
 * ============================================================================
 */

#include "prophet/indicators/calculator.hpp"
#include "prophet/indicators/registry.hpp"
#include <cmath>
#include <algorithm>
#include <numeric>

namespace prophet::indicators {

double Calculator::calculateSlope(const std::vector<double>& values, int lookback) {
    if (values.size() < size_t(lookback + 1)) {
        return 0.0;
    }

    size_t start = values.size() - lookback - 1;
    size_t end = static_cast<int>(values.size()) - 1;

    double slope = (values[end] - values[start]) / lookback;
    return slope;
}

} // namespace prophet::indicators