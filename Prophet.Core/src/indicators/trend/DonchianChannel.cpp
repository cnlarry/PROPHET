/*
 * ============================================================================
 * 文件名：DonchianChannel.cpp
 * 指标名：Donchian Channel（唐奇安通道）
 * 类别：趋势指标
 * 
 * 创建日期：2025-10-31
 * ============================================================================
 */

#include "prophet/indicators/calculator.hpp"
#include "prophet/indicators/registry.hpp"
#include <algorithm>
#include <cmath>

namespace prophet::indicators {

IndicatorResult Calculator::DonchianChannel(
    const std::vector<double>& high,
    const std::vector<double>& low,
    const std::vector<double>& close,
    int DONCHIAN_PERIOD
) {
    IndicatorResult result;
    
    if (close.size() < static_cast<size_t>(DONCHIAN_PERIOD)) {
        result.setSeries("upper", std::vector<Value>{});
        result.setSeries("middle", std::vector<Value>{});
        result.setSeries("lower", std::vector<Value>{});
        result.set("position", Value::fromString("NEUTRAL"));
        return result;
    }
    
    // 计算Donchian通道序列
    std::vector<Value> upper_series, middle_series, lower_series;
    int valid_count = static_cast<int>(close.size()) - DONCHIAN_PERIOD + 1;
    upper_series.reserve(valid_count);
    middle_series.reserve(valid_count);
    lower_series.reserve(valid_count);
    
    for (int i = 0; i < valid_count; ++i) {
        size_t start_idx = i;
        size_t end_idx = start_idx + DONCHIAN_PERIOD;
        
        double highest = *std::max_element(high.begin() + start_idx, high.begin() + end_idx);
        double lowest = *std::min_element(low.begin() + start_idx, low.begin() + end_idx);
        double middle = (highest + lowest) / 2.0;
        
        upper_series.push_back(Value::fromNumber(highest));
        middle_series.push_back(Value::fromNumber(middle));
        lower_series.push_back(Value::fromNumber(lowest));
    }
    
    if (upper_series.empty()) {
        result.setSeries("upper", std::vector<Value>{});
        result.setSeries("middle", std::vector<Value>{});
        result.setSeries("lower", std::vector<Value>{});
        result.set("position", Value::fromString("NEUTRAL"));
        return result;
    }
    
    result.setSeries("upper", upper_series);
    result.setSeries("middle", middle_series);
    result.setSeries("lower", lower_series);
    
    // 判断当前价格位置（基于最新值）
    double current_price = close.back();
    double curr_upper = upper_series.back().toNumber();
    double curr_middle = middle_series.back().toNumber();
    double curr_lower = lower_series.back().toNumber();
    
    std::string position;
    if (current_price >= curr_upper) {
        position = "UPPER_BREAK";  // 突破上轨
    } else if (current_price <= curr_lower) {
        position = "LOWER_BREAK";  // 突破下轨
    } else if (current_price > curr_middle) {
        position = "UPPER_HALF";   // 上半部
    } else if (current_price < curr_middle) {
        position = "LOWER_HALF";   // 下半部
    } else {
        position = "MIDDLE";       // 中线
    }
    
    result.set("position", Value::fromString(position));
    
    return result;
}

} // namespace prophet::indicators

