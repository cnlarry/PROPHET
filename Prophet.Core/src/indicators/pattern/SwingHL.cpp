/*
 * ============================================================================
 * 文件名：SwingHL.cpp
 * 指标名：Swing High/Low（摆动高低点）
 * 类别：价格模式
 * 
 * 创建日期：2025-10-31
 * ============================================================================
 */

#include "prophet/indicators/calculator.hpp"
#include "prophet/indicators/registry.hpp"
#include <algorithm>

namespace prophet::indicators {

IndicatorResult Calculator::SwingHL(
    const std::vector<double>& high,
    const std::vector<double>& low,
    int SWING_PERIOD
) {
    IndicatorResult result;
    
    int required_bars = 2 * SWING_PERIOD + 1;
    if (static_cast<int>(high.size()) < required_bars) {
        result.setSeries("swing_high", std::vector<Value>{});
        result.setSeries("swing_low", std::vector<Value>{});
        result.set("is_swing_high", Value::fromBoolean(false));
        result.set("is_swing_low", Value::fromBoolean(false));
        return result;
    }
    
    // 计算Swing High/Low序列（滑动窗口）
    std::vector<Value> swing_high_series, swing_low_series;
    int valid_count = static_cast<int>(high.size()) - required_bars + 1;
    swing_high_series.reserve(valid_count);
    swing_low_series.reserve(valid_count);
    
    for (int i = 0; i < valid_count; ++i) {
        size_t center_idx = SWING_PERIOD + i;
        
        double center_high = high[center_idx];
        double center_low = low[center_idx];
        
        bool is_swing_high = true;
        bool is_swing_low = true;
        
        // 检查左右两侧
        for (int j = 1; j <= SWING_PERIOD; ++j) {
            // 检查摆动高点：中间的高点必须高于左右两侧
            if (high[center_idx - j] >= center_high || 
                high[center_idx + j] >= center_high) {
                is_swing_high = false;
            }
            
            // 检查摆动低点：中间的低点必须低于左右两侧
            if (low[center_idx - j] <= center_low || 
                low[center_idx + j] <= center_low) {
                is_swing_low = false;
            }
        }
        
        swing_high_series.push_back(Value::fromNumber(is_swing_high ? center_high : 0.0));
        swing_low_series.push_back(Value::fromNumber(is_swing_low ? center_low : 0.0));
    }
    
    if (swing_high_series.empty()) {
        result.setSeries("swing_high", std::vector<Value>{});
        result.setSeries("swing_low", std::vector<Value>{});
        result.set("is_swing_high", Value::fromBoolean(false));
        result.set("is_swing_low", Value::fromBoolean(false));
        return result;
    }
    
    result.setSeries("swing_high", swing_high_series);
    result.setSeries("swing_low", swing_low_series);
    
    // 判断最新值是否为摆动点
    double latest_swing_high = swing_high_series.back().toNumber();
    double latest_swing_low = swing_low_series.back().toNumber();
    bool is_swing_high = (latest_swing_high > 0.0);
    bool is_swing_low = (latest_swing_low > 0.0);
    
    result.set("is_swing_high", Value::fromBoolean(is_swing_high));
    result.set("is_swing_low", Value::fromBoolean(is_swing_low));
    
    return result;
}

} // namespace prophet::indicators

