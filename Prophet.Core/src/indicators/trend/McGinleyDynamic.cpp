/**
 * @file McGinleyDynamic.cpp
 * @brief McGinley Dynamic indicator
 */

#include "prophet/indicators/calculator.hpp"
#include <cmath>
#include <algorithm>

namespace prophet {
namespace indicators {

IndicatorResult Calculator::McGinleyDynamic(
    const std::vector<double>& close,
    int period
) const {
    IndicatorResult result;
    
    if (close.size() < static_cast<size_t>(period)) {
        result.setSeries("value", std::vector<Value>{});
        result.set("trend", Value::fromString("NEUTRAL"));
        result.set("distance", Value::fromNumber(0.0));
        return result;
    }
    
    std::vector<double> md_values(close.size());
    
    // Initialize with SMA
    double sum = 0.0;
    for (int i = 0; i < period; ++i) {
        sum += close[i];
    }
    md_values[period - 1] = sum / period;
    
    // Calculate McGinley Dynamic
    // MD = MD[1] + (Price - MD[1]) / (period * (Price/MD[1])^4)
    for (size_t i = period; i < close.size(); ++i) {
        double prev_md = md_values[i - 1];
        double price = close[i];
        
        if (prev_md > 1e-10) {
            double ratio = price / prev_md;
            double factor = period * std::pow(ratio, 4.0);
            
            // Prevent division by zero and extreme values
            factor = std::max(0.1, std::min(factor, 1000.0));
            
            md_values[i] = prev_md + (price - prev_md) / factor;
        } else {
            md_values[i] = price;
        }
    }
    
    // 提取有效数据序列（从period-1开始）
    std::vector<Value> md_series;
    md_series.reserve(md_values.size() - period + 1);
    for (size_t i = period - 1; i < md_values.size(); ++i) {
        md_series.push_back(Value::fromNumber(md_values[i]));
    }
    
    if (md_series.empty()) {
        result.setSeries("value", std::vector<Value>{});
        result.set("trend", Value::fromString("NEUTRAL"));
        result.set("distance", Value::fromNumber(0.0));
        return result;
    }
    
    result.setSeries("value", md_series);
    
    double md = md_series.back().toNumber();
    double current_price = close.back();
    
    // Calculate distance percentage
    double distance = 0.0;
    if (md > 1e-10) {
        distance = ((current_price - md) / md) * 100.0;
    }
    
    // Determine trend (based on latest values)
    std::string trend = "NEUTRAL";
    if (md_series.size() >= 2) {
        double prev_md = md_series[md_series.size() - 2].toNumber();
        if (md > prev_md * 1.001) {
            trend = "BULLISH";
        } else if (md < prev_md * 0.999) {
            trend = "BEARISH";
        }
    }
    
    result.set("trend", Value::fromString(trend));
    result.set("distance", Value::fromNumber(distance));
    
    return result;
}

} // namespace indicators
} // namespace prophet

