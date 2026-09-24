/**
 * @file LinearRegSlope.cpp
 * @brief Linear Regression Slope indicator
 */

#include "prophet/indicators/calculator.hpp"
#include <ta_libc.h>
#include <cmath>

#ifndef M_PI
#define M_PI 3.14159265358979323846
#endif

namespace prophet {
namespace indicators {

IndicatorResult Calculator::LinearRegSlope(
    const std::vector<double>& close,
    int period
) const {
    IndicatorResult result;
    
    if (close.size() < static_cast<size_t>(period)) {
        result.setSeries("slope", std::vector<Value>{});
        result.setSeries("angle", std::vector<Value>{});
        result.set("trend", Value::fromString("NEUTRAL"));
        result.set("strength", Value::fromString("WEAK"));
        return result;
    }
    
    // Calculate Linear Regression Slope using TA-Lib
    std::vector<double> slope_values(close.size());
    int outBegIdx = 0;
    int outNbElement = 0;
    
    size_t close_size = close.size();
    int endIdx = static_cast<int>(close_size > static_cast<size_t>(INT_MAX) ? INT_MAX : close_size) - 1;
    TA_RetCode retCode = TA_LINEARREG_SLOPE(
        0, endIdx,
        close.data(),
        period,
        &outBegIdx,
        &outNbElement,
        slope_values.data()
    );
    
    if (retCode != TA_SUCCESS || outNbElement == 0) {
        result.setSeries("slope", std::vector<Value>{});
        result.setSeries("angle", std::vector<Value>{});
        result.set("trend", Value::fromString("NEUTRAL"));
        result.set("strength", Value::fromString("WEAK"));
        return result;
    }
    
    // 提取有效数据并计算角度序列
    std::vector<Value> slope_series, angle_series;
    slope_series.reserve(outNbElement);
    angle_series.reserve(outNbElement);
    
    for (int i = 0; i < outNbElement; ++i) {
        double slope_val = slope_values[i];
        slope_series.push_back(Value::fromNumber(slope_val));
        
        // Convert slope to angle (in degrees)
        double angle = std::atan(slope_val) * 180.0 / M_PI;
        angle_series.push_back(Value::fromNumber(angle));
    }
    
    result.setSeries("slope", slope_series);
    result.setSeries("angle", angle_series);
    
    // Determine trend and strength (based on latest value)
    double slope = slope_series.back().toNumber();
    
    std::string trend = "NEUTRAL";
    if (slope > 0.1) {
        trend = "BULLISH";
    } else if (slope < -0.1) {
        trend = "BEARISH";
    }
    
    // Determine strength based on absolute slope
    std::string strength = "WEAK";
    double abs_slope = std::abs(slope);
    if (abs_slope > 1.0) {
        strength = "VERY_STRONG";
    } else if (abs_slope > 0.5) {
        strength = "STRONG";
    } else if (abs_slope > 0.2) {
        strength = "MODERATE";
    }
    
    result.set("trend", Value::fromString(trend));
    result.set("strength", Value::fromString(strength));
    
    return result;
}

} // namespace indicators
} // namespace prophet

