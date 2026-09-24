/**
 * @file KaufmanER.cpp
 * @brief Kaufman Efficiency Ratio indicator
 */

#include "prophet/indicators/calculator.hpp"
#include <cmath>
#include <algorithm>

namespace prophet {
namespace indicators {

IndicatorResult Calculator::KaufmanER(
    const std::vector<double>& close,
    int period
) const {
    IndicatorResult result;
    
    if (close.size() < static_cast<size_t>(period + 1)) {
        result.setSeries("value", std::vector<Value>{});
        result.set("market_state", Value::fromString("NEUTRAL"));
        result.set("efficiency", Value::fromString("LOW"));
        return result;
    }
    
    // Calculate Efficiency Ratio序列（滑动窗口）
    std::vector<Value> er_series;
    int valid_count = static_cast<int>(close.size()) - period;
    er_series.reserve(valid_count);
    
    for (int i = 0; i < valid_count; ++i) {
        size_t idx = period + i;
        size_t start_idx = i;
        
        // Change = abs(Close - Close[period])
        double change = std::abs(close[idx] - close[start_idx]);
        
        // Volatility = sum(abs(Close[i] - Close[i-1])) for period
        double volatility = 0.0;
        for (size_t j = start_idx + 1; j <= idx; ++j) {
            volatility += std::abs(close[j] - close[j - 1]);
        }
        
        double er = 0.0;
        if (volatility > 1e-10) {
            er = change / volatility;
        }
        
        er_series.push_back(Value::fromNumber(er));
    }
    
    if (er_series.empty()) {
        result.setSeries("value", std::vector<Value>{});
        result.set("market_state", Value::fromString("NEUTRAL"));
        result.set("efficiency", Value::fromString("LOW"));
        return result;
    }
    
    result.setSeries("value", er_series);
    
    // Determine market state and efficiency (based on latest value)
    double er = er_series.back().toNumber();
    
    std::string market_state = "NEUTRAL";
    std::string efficiency = "LOW";
    
    if (er > 0.7) {
        market_state = "TRENDING";
        efficiency = "VERY_HIGH";
    } else if (er > 0.5) {
        market_state = "TRENDING";
        efficiency = "HIGH";
    } else if (er > 0.3) {
        market_state = "TRANSITIONAL";
        efficiency = "MEDIUM";
    } else {
        market_state = "CHOPPY";
        efficiency = "LOW";
    }
    
    result.set("market_state", Value::fromString(market_state));
    result.set("efficiency", Value::fromString(efficiency));
    
    return result;
}

} // namespace indicators
} // namespace prophet

