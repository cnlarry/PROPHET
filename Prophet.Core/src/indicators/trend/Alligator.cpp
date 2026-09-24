/**
 * @file Alligator.cpp
 * @brief Alligator indicator by Bill Williams
 */

#include "prophet/indicators/calculator.hpp"
#include <ta_libc.h>
#include <cmath>
#include <climits>

namespace prophet {
namespace indicators {

IndicatorResult Calculator::Alligator(
    const std::vector<double>& high,
    const std::vector<double>& low,
    int jaw_period,
    int teeth_period,
    int lips_period
) const {
    IndicatorResult result;
    
    if (high.size() < static_cast<size_t>(jaw_period) || 
        low.size() < static_cast<size_t>(jaw_period)) {
        result.setSeries("jaw", std::vector<Value>{});
        result.setSeries("teeth", std::vector<Value>{});
        result.setSeries("lips", std::vector<Value>{});
        result.set("state", Value::fromString("SLEEPING"));
        result.set("trend", Value::fromString("NEUTRAL"));
        return result;
    }
    
    // Calculate median price (HL/2)
    std::vector<double> median(high.size());
    for (size_t i = 0; i < high.size(); ++i) {
        median[i] = (high[i] + low[i]) / 2.0;
    }
    
    // Calculate Jaw (blue line): SMMA 13, shifted forward by 8 bars
    std::vector<double> jaw_values(median.size());
    int jaw_outBegIdx = 0;
    int jaw_outNbElement = 0;
    
    size_t median_size = median.size();
    int endIdx = static_cast<int>(median_size > static_cast<size_t>(INT_MAX) ? INT_MAX : median_size) - 1;
    TA_RetCode jaw_retCode = TA_SMA(
        0, endIdx,
        median.data(),
        jaw_period,
        &jaw_outBegIdx,
        &jaw_outNbElement,
        jaw_values.data()
    );
    
    // Calculate Teeth (red line): SMMA 8, shifted forward by 5 bars
    std::vector<double> teeth_values(median.size());
    int teeth_outBegIdx = 0;
    int teeth_outNbElement = 0;
    
    TA_RetCode teeth_retCode = TA_SMA(
        0, endIdx,
        median.data(),
        teeth_period,
        &teeth_outBegIdx,
        &teeth_outNbElement,
        teeth_values.data()
    );
    
    // Calculate Lips (green line): SMMA 5, shifted forward by 3 bars
    std::vector<double> lips_values(median.size());
    int lips_outBegIdx = 0;
    int lips_outNbElement = 0;
    
    TA_RetCode lips_retCode = TA_SMA(
        0, endIdx,
        median.data(),
        lips_period,
        &lips_outBegIdx,
        &lips_outNbElement,
        lips_values.data()
    );
    
    if (jaw_retCode != TA_SUCCESS || jaw_outNbElement == 0 ||
        teeth_retCode != TA_SUCCESS || teeth_outNbElement == 0 ||
        lips_retCode != TA_SUCCESS || lips_outNbElement == 0) {
        result.setSeries("jaw", std::vector<Value>{});
        result.setSeries("teeth", std::vector<Value>{});
        result.setSeries("lips", std::vector<Value>{});
        result.set("state", Value::fromString("SLEEPING"));
        result.set("trend", Value::fromString("NEUTRAL"));
        return result;
    }
    
    // 对齐三个SMA的有效数据范围
    int start_idx = std::max({jaw_outBegIdx, teeth_outBegIdx, lips_outBegIdx});
    int end_idx = std::min({jaw_outNbElement, teeth_outNbElement, lips_outNbElement});
    int valid_count = end_idx - start_idx;
    
    if (valid_count <= 0) {
        result.setSeries("jaw", std::vector<Value>{});
        result.setSeries("teeth", std::vector<Value>{});
        result.setSeries("lips", std::vector<Value>{});
        result.set("state", Value::fromString("SLEEPING"));
        result.set("trend", Value::fromString("NEUTRAL"));
        return result;
    }
    
    // 提取有效数据序列
    std::vector<Value> jaw_series, teeth_series, lips_series;
    jaw_series.reserve(valid_count);
    teeth_series.reserve(valid_count);
    lips_series.reserve(valid_count);
    
    for (int i = 0; i < valid_count; ++i) {
        int jaw_idx = start_idx - jaw_outBegIdx + i;
        int teeth_idx = start_idx - teeth_outBegIdx + i;
        int lips_idx = start_idx - lips_outBegIdx + i;
        
        if (jaw_idx >= 0 && jaw_idx < jaw_outNbElement &&
            teeth_idx >= 0 && teeth_idx < teeth_outNbElement &&
            lips_idx >= 0 && lips_idx < lips_outNbElement) {
            jaw_series.push_back(Value::fromNumber(jaw_values[jaw_idx]));
            teeth_series.push_back(Value::fromNumber(teeth_values[teeth_idx]));
            lips_series.push_back(Value::fromNumber(lips_values[lips_idx]));
        }
    }
    
    if (jaw_series.empty()) {
        result.setSeries("jaw", std::vector<Value>{});
        result.setSeries("teeth", std::vector<Value>{});
        result.setSeries("lips", std::vector<Value>{});
        result.set("state", Value::fromString("SLEEPING"));
        result.set("trend", Value::fromString("NEUTRAL"));
        return result;
    }
    
    result.setSeries("jaw", jaw_series);
    result.setSeries("teeth", teeth_series);
    result.setSeries("lips", lips_series);
    
    // Determine alligator state and trend (based on latest values)
    double jaw = jaw_series.back().toNumber();
    double teeth = teeth_series.back().toNumber();
    double lips = lips_series.back().toNumber();
    
    std::string state = "SLEEPING";
    std::string trend = "NEUTRAL";
    
    // Check if lines are properly ordered (awake and trending)
    if (lips > teeth && teeth > jaw) {
        state = "EATING";
        trend = "BULLISH";
    } else if (lips < teeth && teeth < jaw) {
        state = "EATING";
        trend = "BEARISH";
    } else {
        // Lines are intertwined (sleeping/consolidating)
        double range = std::max({jaw, teeth, lips}) - std::min({jaw, teeth, lips});
        double avg = (jaw + teeth + lips) / 3.0;
        double relative_range = avg > 0 ? range / avg : 0;
        
        if (relative_range < 0.01) {
            state = "SLEEPING";  // Very tight range
        } else {
            state = "WAKING";  // Starting to separate
            
            // Try to determine direction
            if (lips > jaw) {
                trend = "BULLISH";
            } else if (lips < jaw) {
                trend = "BEARISH";
            }
        }
    }
    
    result.set("state", Value::fromString(state));
    result.set("trend", Value::fromString(trend));
    
    return result;
}

} // namespace indicators
} // namespace prophet

