/*
 * ============================================================================
 * 文件名：MSB.cpp
 * 指标名：Market Structure Break（市场结构破坏）
 * 类别：价格模式
 * 
 * 创建日期：2025-10-31
 * ============================================================================
 */

#include "prophet/indicators/calculator.hpp"
#include "prophet/indicators/registry.hpp"
#include <algorithm>
#include <cmath>

namespace prophet::indicators {

IndicatorResult Calculator::MSB(
    const std::vector<double>& high,
    const std::vector<double>& low,
    const std::vector<double>& close,
    int MSB_LOOKBACK
) {
    IndicatorResult result;
    
    if (close.size() < static_cast<size_t>(MSB_LOOKBACK + 5)) {
        result.setSeries("break_level", std::vector<Value>{});
        result.set("msb_type", Value::fromString("NONE"));
        result.set("is_bullish_msb", Value::fromBoolean(false));
        result.set("is_bearish_msb", Value::fromBoolean(false));
        return result;
    }
    
    // 计算MSB序列（滑动窗口）
    std::vector<Value> break_level_series;
    int valid_count = static_cast<int>(close.size()) - MSB_LOOKBACK - MSB_LOOKBACK;
    break_level_series.reserve(valid_count);
    
    for (int i = 0; i < valid_count; ++i) {
        size_t start_idx = MSB_LOOKBACK + i;
        size_t prev_start = i;
        
        // 找到前一个摆动高点和低点
        double prev_swing_high = *std::max_element(high.begin() + prev_start, 
                                                   high.begin() + start_idx);
        double prev_swing_low = *std::min_element(low.begin() + prev_start, 
                                                  low.begin() + start_idx);
        
        // 检测结构破坏
        double break_level = 0.0;
        double current_close = close[start_idx];
        
        // 看涨结构破坏：价格突破前一个摆动高点
        if (current_close > prev_swing_high && prev_swing_high > 0.0) {
            break_level = prev_swing_high;
        }
        // 看跌结构破坏：价格跌破前一个摆动低点
        else if (current_close < prev_swing_low && prev_swing_low > 0.0) {
            break_level = prev_swing_low;
        }
        
        break_level_series.push_back(Value::fromNumber(break_level));
    }
    
    if (break_level_series.empty()) {
        result.setSeries("break_level", std::vector<Value>{});
        result.set("msb_type", Value::fromString("NONE"));
        result.set("is_bullish_msb", Value::fromBoolean(false));
        result.set("is_bearish_msb", Value::fromBoolean(false));
        return result;
    }
    
    result.setSeries("break_level", break_level_series);
    
    // 判断最新值（基于最新值）
    size_t latest_idx = close.size() - 1;
    size_t prev_start = (latest_idx > MSB_LOOKBACK * 2) ? latest_idx - MSB_LOOKBACK * 2 : 0;
    size_t start_idx = latest_idx - MSB_LOOKBACK;
    
    double prev_swing_high = *std::max_element(high.begin() + prev_start, 
                                               high.begin() + start_idx);
    double prev_swing_low = *std::min_element(low.begin() + prev_start, 
                                              low.begin() + start_idx);
    
    std::string msb_type = "NONE";
    bool is_bullish_msb = false;
    bool is_bearish_msb = false;
    
    if (close.back() > prev_swing_high && prev_swing_high > 0.0) {
        msb_type = "BULLISH";
        is_bullish_msb = true;
    } else if (close.back() < prev_swing_low && prev_swing_low > 0.0) {
        msb_type = "BEARISH";
        is_bearish_msb = true;
    }
    
    result.set("msb_type", Value::fromString(msb_type));
    result.set("is_bullish_msb", Value::fromBoolean(is_bullish_msb));
    result.set("is_bearish_msb", Value::fromBoolean(is_bearish_msb));
    
    return result;
}

} // namespace prophet::indicators

