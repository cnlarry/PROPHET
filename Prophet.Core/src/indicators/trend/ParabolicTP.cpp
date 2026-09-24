/**
 * @file ParabolicTP.cpp
 * @brief Parabolic Time/Price indicator (Enhanced SAR)
 */

#include "prophet/indicators/calculator.hpp"
#include <ta_libc.h>
#include <cmath>

namespace prophet {
namespace indicators {

IndicatorResult Calculator::ParabolicTP(
    const std::vector<double>& high,
    const std::vector<double>& low,
    const std::vector<double>& close,
    double acceleration,
    double maximum
) const {
    IndicatorResult result;
    
    int min_size = 5;
    if (high.size() < static_cast<size_t>(min_size) || 
        low.size() < static_cast<size_t>(min_size) ||
        close.size() < static_cast<size_t>(min_size)) {
        result.setSeries("value", std::vector<Value>{});
        result.set("trend", Value::fromString("NEUTRAL"));
        result.set("time_factor", Value::fromNumber(1.0));
        result.set("confidence", Value::fromNumber(0.0));
        return result;
    }
    
    // Calculate standard SAR
    std::vector<double> sar_values(high.size());
    int outBegIdx = 0;
    int outNbElement = 0;
    
    size_t high_size = high.size();
    int endIdx = static_cast<int>(high_size > static_cast<size_t>(INT_MAX) ? INT_MAX : high_size) - 1;
    TA_RetCode retCode = TA_SAR(
        0, endIdx,
        high.data(),
        low.data(),
        acceleration,
        maximum,
        &outBegIdx,
        &outNbElement,
        sar_values.data()
    );
    
    if (retCode != TA_SUCCESS || outNbElement == 0) {
        result.setSeries("value", std::vector<Value>{});
        result.set("trend", Value::fromString("NEUTRAL"));
        result.set("time_factor", Value::fromNumber(1.0));
        result.set("confidence", Value::fromNumber(0.0));
        return result;
    }
    
    // 提取SAR有效数据并计算ParabolicTP序列
    std::vector<Value> ptp_series;
    ptp_series.reserve(outNbElement);
    
    for (int i = 0; i < outNbElement; ++i) {
        // 计算实际K线索引
        int candleIdx = outBegIdx + i;
        if (candleIdx < 0 || candleIdx >= static_cast<int>(close.size())) {
            continue;
        }
        
        double sar = sar_values[i];
        double current_close = close[candleIdx];
        
        // Calculate time factor (how long in current trend)
        int time_in_trend = 1;
        bool is_uptrend = current_close > sar;
        
        // Count consecutive bars in same direction
        for (int j = i - 1; j >= 0 && j >= i - 20; --j) {
            int prev_close_idx = outBegIdx + j;
            if (prev_close_idx >= 0 && prev_close_idx < static_cast<int>(close.size())) {
                bool was_uptrend = close[prev_close_idx] > sar_values[j];
                if (was_uptrend == is_uptrend) {
                    time_in_trend++;
                } else {
                    break;
                }
            }
        }
        
        // Time factor: increases with time in trend (max 2.0)
        double time_factor = 1.0 + std::min(time_in_trend / 20.0, 1.0);
        
        // Adjust SAR with time factor
        double adjusted_sar = sar;
        if (is_uptrend) {
            double adjustment = (current_close - sar) * (time_factor - 1.0) * 0.1;
            adjusted_sar += adjustment;
        } else {
            double adjustment = (sar - current_close) * (time_factor - 1.0) * 0.1;
            adjusted_sar -= adjustment;
        }
        
        ptp_series.push_back(Value::fromNumber(adjusted_sar));
    }
    
    if (ptp_series.empty()) {
        result.setSeries("value", std::vector<Value>{});
        result.set("trend", Value::fromString("NEUTRAL"));
        result.set("time_factor", Value::fromNumber(1.0));
        result.set("confidence", Value::fromNumber(0.0));
        return result;
    }
    
    result.setSeries("value", ptp_series);
    
    // Calculate latest values for derived fields
    double adjusted_sar = ptp_series.back().toNumber();
    double current_close = close.back();
    bool is_uptrend = current_close > adjusted_sar;
    
    // Calculate time factor for latest value
    int time_in_trend = 1;
    for (int i = static_cast<int>(ptp_series.size()) - 2; i >= 0 && i >= static_cast<int>(ptp_series.size()) - 21; --i) {
        int prev_close_idx = outBegIdx + i;
        if (prev_close_idx >= 0 && prev_close_idx < static_cast<int>(close.size())) {
            bool was_uptrend = close[prev_close_idx] > ptp_series[i].toNumber();
            if (was_uptrend == is_uptrend) {
                time_in_trend++;
            } else {
                break;
            }
        }
    }
    
    double time_factor = 1.0 + std::min(time_in_trend / 20.0, 1.0);
    double distance = std::abs(current_close - adjusted_sar) / current_close;
    double confidence = std::min(distance * 100.0, 100.0) * (time_factor / 2.0);
    confidence = std::min(confidence, 100.0);
    
    std::string trend = is_uptrend ? "BULLISH" : "BEARISH";
    
    result.set("trend", Value::fromString(trend));
    result.set("time_factor", Value::fromNumber(time_factor));
    result.set("confidence", Value::fromNumber(confidence));
    
    return result;
}

} // namespace indicators
} // namespace prophet

