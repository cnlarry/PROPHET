/**
 * @file FisherTransform.cpp
 * @brief Fisher Transform indicator
 */

#include "prophet/indicators/calculator.hpp"
#include <ta_libc.h>
#include <cmath>
#include <algorithm>

namespace prophet {
namespace indicators {

IndicatorResult Calculator::FisherTransform(
    const std::vector<double>& high,
    const std::vector<double>& low,
    int period
) const {
    IndicatorResult result;
    
    if (high.size() < static_cast<size_t>(period) || 
        low.size() < static_cast<size_t>(period)) {
        result.setSeries("fisher", std::vector<Value>{});
        result.setSeries("trigger", std::vector<Value>{});
        result.set("signal", Value::fromString("NONE"));
        return result;
    }
    
    std::vector<double> fisher_values(high.size());
    std::vector<double> normalized_values(high.size());
    std::vector<double> value_values(high.size());
    
    // Initialize
    fisher_values[0] = 0.0;
    value_values[0] = 0.0;
    normalized_values[0] = 0.0;
    
    // Calculate Fisher Transform
    for (size_t i = 1; i < high.size(); ++i) {
        // Find min and max in period
        size_t start_idx = i >= static_cast<size_t>(period) ? i - period + 1 : 0;
        double max_high = high[start_idx];
        double min_low = low[start_idx];
        
        for (size_t j = start_idx; j <= i; ++j) {
            max_high = std::max(max_high, high[j]);
            min_low = std::min(min_low, low[j]);
        }
        
        // Normalize to -1 to 1
        double range = max_high - min_low;
        double normalized = 0.0;
        if (range > 1e-10) {
            double mid = (high[i] + low[i]) / 2.0;
            normalized = 2.0 * ((mid - min_low) / range - 0.5);
            
            // Constrain to ±0.999 to avoid log(0)
            normalized = std::max(-0.999, std::min(0.999, normalized));
        }
        normalized_values[i] = normalized;
        
        // Smooth the normalized value
        value_values[i] = 0.33 * normalized + 0.67 * value_values[i - 1];
        
        // Apply Fisher Transform: 0.5 * ln((1+x)/(1-x))
        double value = value_values[i];
        value = std::max(-0.999, std::min(0.999, value));
        
        double fisher = 0.5 * std::log((1.0 + value) / (1.0 - value));
        fisher_values[i] = 0.5 * fisher + 0.5 * fisher_values[i - 1];
    }
    
    // 保存序列（从索引1开始，因为索引0是初始值）
    std::vector<Value> fisher_series, trigger_series;
    fisher_series.reserve(fisher_values.size() - 1);
    trigger_series.reserve(fisher_values.size() - 1);
    
    for (size_t i = 1; i < fisher_values.size(); ++i) {
        fisher_series.push_back(Value::fromNumber(fisher_values[i]));
        trigger_series.push_back(Value::fromNumber(fisher_values[i - 1]));  // trigger是前一个fisher值
    }
    
    result.setSeries("fisher", fisher_series);
    result.setSeries("trigger", trigger_series);
    
    // Determine signal (based on latest values)
    if (fisher_series.empty()) {
        result.set("signal", Value::fromString("NONE"));
        return result;
    }
    
    double fisher = fisher_series.back().toNumber();
    double trigger = trigger_series.back().toNumber();
    
    std::string signal = "NONE";
    if (fisher > trigger && fisher > 0) {
        signal = "STRONG_BUY";
    } else if (fisher > trigger) {
        signal = "BUY";
    } else if (fisher < trigger && fisher < 0) {
        signal = "STRONG_SELL";
    } else if (fisher < trigger) {
        signal = "SELL";
    }
    
    // Check for extreme values
    if (fisher > 2.0) {
        signal = "OVERBOUGHT";
    } else if (fisher < -2.0) {
        signal = "OVERSOLD";
    }
    
    result.set("signal", Value::fromString(signal));
    
    return result;
}

} // namespace indicators
} // namespace prophet

