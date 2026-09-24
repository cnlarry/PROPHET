/**
 * @file ElderRay.cpp
 * @brief Elder Ray indicator (Bull Power & Bear Power) by Alexander Elder
 */

#include "prophet/indicators/calculator.hpp"
#include <ta_libc.h>
#include <cmath>
#include <algorithm>
#include <climits>

namespace prophet {
namespace indicators {

IndicatorResult Calculator::ElderRay(
    const std::vector<double>& high,
    const std::vector<double>& low,
    const std::vector<double>& close,
    int period
) const {
    IndicatorResult result;
    
    if (high.size() < static_cast<size_t>(period) || 
        low.size() < static_cast<size_t>(period) ||
        close.size() < static_cast<size_t>(period)) {
        result.setSeries("bull_power", std::vector<Value>{});
        result.setSeries("bear_power", std::vector<Value>{});
        result.setSeries("net_power", std::vector<Value>{});
        result.set("trend", Value::fromString("NEUTRAL"));
        return result;
    }
    
    // Calculate EMA
    std::vector<double> ema_values(close.size());
    int ema_outBegIdx = 0;
    int ema_outNbElement = 0;
    
    size_t close_size = close.size();
    int endIdx = static_cast<int>(close_size > static_cast<size_t>(INT_MAX) ? INT_MAX : close_size) - 1;
    TA_RetCode ema_retCode = TA_EMA(
        0, endIdx,
        close.data(),
        period,
        &ema_outBegIdx,
        &ema_outNbElement,
        ema_values.data()
    );
    
    if (ema_retCode != TA_SUCCESS || ema_outNbElement == 0) {
        result.setSeries("bull_power", std::vector<Value>{});
        result.setSeries("bear_power", std::vector<Value>{});
        result.setSeries("net_power", std::vector<Value>{});
        result.set("trend", Value::fromString("NEUTRAL"));
        return result;
    }
    
    // 计算Bull Power和Bear Power序列
    // 需要对齐EMA和high/low的数据范围
    int start_idx = ema_outBegIdx;
    int valid_count = ema_outNbElement;
    
    std::vector<Value> bull_power_series, bear_power_series, net_power_series;
    bull_power_series.reserve(valid_count);
    bear_power_series.reserve(valid_count);
    net_power_series.reserve(valid_count);
    
    for (int i = 0; i < valid_count; ++i) {
        int ema_idx = start_idx + i;
        int price_idx = ema_idx;  // EMA和价格数据对齐
        
        if (price_idx >= 0 && price_idx < static_cast<int>(high.size()) && 
            price_idx >= 0 && price_idx < static_cast<int>(low.size())) {
            double ema_val = ema_values[i];
            double high_val = high[price_idx];
            double low_val = low[price_idx];
            
            double bull_power = high_val - ema_val;
            double bear_power = low_val - ema_val;
            double net_power = bull_power + bear_power;
            
            bull_power_series.push_back(Value::fromNumber(bull_power));
            bear_power_series.push_back(Value::fromNumber(bear_power));
            net_power_series.push_back(Value::fromNumber(net_power));
        }
    }
    
    if (bull_power_series.empty()) {
        result.setSeries("bull_power", std::vector<Value>{});
        result.setSeries("bear_power", std::vector<Value>{});
        result.setSeries("net_power", std::vector<Value>{});
        result.set("trend", Value::fromString("NEUTRAL"));
        return result;
    }
    
    result.setSeries("bull_power", bull_power_series);
    result.setSeries("bear_power", bear_power_series);
    result.setSeries("net_power", net_power_series);
    
    // Determine trend based on latest powers
    double bull_power = bull_power_series.back().toNumber();
    double bear_power = bear_power_series.back().toNumber();
    
    std::string trend = "NEUTRAL";
    if (bull_power > 0 && bear_power > 0) {
        trend = "STRONG_BULLISH";  // Both above EMA
    } else if (bull_power > 0 && bear_power < 0 && bull_power > std::abs(bear_power)) {
        trend = "BULLISH";  // Bulls stronger
    } else if (bull_power < 0 && bear_power < 0) {
        trend = "STRONG_BEARISH";  // Both below EMA
    } else if (bear_power < 0 && bull_power > 0 && std::abs(bear_power) > bull_power) {
        trend = "BEARISH";  // Bears stronger
    }
    
    result.set("trend", Value::fromString(trend));
    
    return result;
}

} // namespace indicators
} // namespace prophet

