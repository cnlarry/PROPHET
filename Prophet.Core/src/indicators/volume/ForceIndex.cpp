/**
 * @file ForceIndex.cpp
 * @brief Force Index indicator by Alexander Elder
 */

#include "prophet/indicators/calculator.hpp"
#include <ta_libc.h>
#include <cmath>
#include <climits>

namespace prophet {
namespace indicators {

IndicatorResult Calculator::ForceIndex(
    const std::vector<double>& close,
    const std::vector<double>& volume,
    int period
) const {
    IndicatorResult result;
    
    if (close.size() < 2 || volume.size() < 2 || 
        close.size() < static_cast<size_t>(period)) {
        result.setSeries("value", std::vector<Value>{});
        result.set("trend", Value::fromString("NEUTRAL"));
        result.set("strength", Value::fromString("WEAK"));
        return result;
    }
    
    // Calculate raw force index: (Close - Close[1]) * Volume
    std::vector<double> raw_force(close.size());
    raw_force[0] = 0.0;
    for (size_t i = 1; i < close.size(); ++i) {
        raw_force[i] = (close[i] - close[i - 1]) * volume[i];
    }
    
    // Smooth with EMA
    std::vector<double> smoothed_force(raw_force.size());
    int outBegIdx = 0;
    int outNbElement = 0;
    
    size_t raw_force_size = raw_force.size();
    int endIdx = static_cast<int>(raw_force_size > static_cast<size_t>(INT_MAX) ? INT_MAX : raw_force_size) - 1;
    TA_RetCode retCode = TA_EMA(
        0, endIdx,
        raw_force.data(),
        period,
        &outBegIdx,
        &outNbElement,
        smoothed_force.data()
    );
    
    if (retCode != TA_SUCCESS || outNbElement == 0) {
        result.setSeries("value", std::vector<Value>{});
        result.set("trend", Value::fromString("NEUTRAL"));
        result.set("strength", Value::fromString("WEAK"));
        return result;
    }
    
    // 提取有效数据（TA-Lib将有效数据存储在数组开头）
    std::vector<Value> force_series;
    force_series.reserve(outNbElement);
    for (int i = 0; i < outNbElement; ++i) {
        force_series.push_back(Value::fromNumber(smoothed_force[i]));
    }
    result.setSeries("value", force_series);
    
    // Determine trend (based on latest value)
    double force_value = force_series.back().toNumber();
    std::string trend = "NEUTRAL";
    if (force_value > 0) {
        trend = "BULLISH";
    } else if (force_value < 0) {
        trend = "BEARISH";
    }
    
    // Determine strength based on magnitude
    double abs_force = std::abs(force_value);
    double avg_volume = 0.0;
    int count = std::min(static_cast<int>(volume.size()), period);
    for (int i = 0; i < count; ++i) {
        avg_volume += volume[volume.size() - 1 - i];
    }
    avg_volume /= count;
    
    double normalized_force = avg_volume > 0 ? abs_force / avg_volume : 0;
    
    std::string strength = "WEAK";
    if (normalized_force > 2.0) {
        strength = "VERY_STRONG";
    } else if (normalized_force > 1.0) {
        strength = "STRONG";
    } else if (normalized_force > 0.5) {
        strength = "MODERATE";
    }
    
    result.set("trend", Value::fromString(trend));
    result.set("strength", Value::fromString(strength));
    
    return result;
}

} // namespace indicators
} // namespace prophet

