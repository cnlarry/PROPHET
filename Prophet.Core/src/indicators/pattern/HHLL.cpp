/*
 * ============================================================================
 * 文件名：HHLL.cpp
 * 指标名：Higher Highs/Lower Lows（更高高点/更低低点）
 * 类别：价格模式
 * 
 * 创建日期：2025-10-31
 * ============================================================================
 */

#include "prophet/indicators/calculator.hpp"
#include "prophet/indicators/registry.hpp"
#include <algorithm>
#include <vector>

namespace prophet::indicators {

IndicatorResult Calculator::HHLL(
    const std::vector<double>& high,
    const std::vector<double>& low,
    int HHLL_PERIOD
) {
    IndicatorResult result;
    
    if (static_cast<int>(high.size()) < HHLL_PERIOD * 2) {
        result.setSeries("higher_high", std::vector<Value>{});
        result.setSeries("lower_low", std::vector<Value>{});
        result.setSeries("higher_low", std::vector<Value>{});
        result.setSeries("lower_high", std::vector<Value>{});
        result.set("is_higher_high", Value::fromBoolean(false));
        result.set("is_lower_low", Value::fromBoolean(false));
        result.set("is_higher_low", Value::fromBoolean(false));
        result.set("is_lower_high", Value::fromBoolean(false));
        result.set("trend_structure", Value::fromString("NEUTRAL"));
        return result;
    }
    
    // 计算HHLL序列（滑动窗口，每两个周期比较一次）
    std::vector<Value> higher_high_series, lower_low_series, higher_low_series, lower_high_series;
    int valid_count = static_cast<int>(high.size()) - HHLL_PERIOD * 2 + 1;
    higher_high_series.reserve(valid_count);
    lower_low_series.reserve(valid_count);
    higher_low_series.reserve(valid_count);
    lower_high_series.reserve(valid_count);
    
    for (int i = 0; i < valid_count; ++i) {
        size_t mid_point = HHLL_PERIOD + i;
        
        // 前一个周期的高低点
        double prev_high = *std::max_element(high.begin() + i,
                                             high.begin() + mid_point);
        double prev_low = *std::min_element(low.begin() + i,
                                            low.begin() + mid_point);
        
        // 当前周期的高低点
        double curr_high = *std::max_element(high.begin() + mid_point,
                                            high.begin() + mid_point + HHLL_PERIOD);
        double curr_low = *std::min_element(low.begin() + mid_point,
                                           low.begin() + mid_point + HHLL_PERIOD);
        
        // 计算差值（用于序列存储）
        double higher_high = (curr_high > prev_high) ? (curr_high - prev_high) : 0.0;
        double lower_low = (curr_low < prev_low) ? (prev_low - curr_low) : 0.0;
        double higher_low = (curr_low > prev_low) ? (curr_low - prev_low) : 0.0;
        double lower_high = (curr_high < prev_high) ? (prev_high - curr_high) : 0.0;
        
        higher_high_series.push_back(Value::fromNumber(higher_high));
        lower_low_series.push_back(Value::fromNumber(lower_low));
        higher_low_series.push_back(Value::fromNumber(higher_low));
        lower_high_series.push_back(Value::fromNumber(lower_high));
    }
    
    if (higher_high_series.empty()) {
        result.setSeries("higher_high", std::vector<Value>{});
        result.setSeries("lower_low", std::vector<Value>{});
        result.setSeries("higher_low", std::vector<Value>{});
        result.setSeries("lower_high", std::vector<Value>{});
        result.set("is_higher_high", Value::fromBoolean(false));
        result.set("is_lower_low", Value::fromBoolean(false));
        result.set("is_higher_low", Value::fromBoolean(false));
        result.set("is_lower_high", Value::fromBoolean(false));
        result.set("trend_structure", Value::fromString("NEUTRAL"));
        return result;
    }
    
    result.setSeries("higher_high", higher_high_series);
    result.setSeries("lower_low", lower_low_series);
    result.setSeries("higher_low", higher_low_series);
    result.setSeries("lower_high", lower_high_series);
    
    // 判断价格结构（基于最新值）
    bool is_higher_high = (higher_high_series.back().toNumber() > 0.0);
    bool is_lower_low = (lower_low_series.back().toNumber() > 0.0);
    bool is_higher_low = (higher_low_series.back().toNumber() > 0.0);
    bool is_lower_high = (lower_high_series.back().toNumber() > 0.0);
    
    // 判断趋势结构
    std::string trend_structure;
    if (is_higher_high && is_higher_low) {
        trend_structure = "UPTREND";         // 上升趋势
    } else if (is_lower_high && is_lower_low) {
        trend_structure = "DOWNTREND";       // 下降趋势
    } else if (is_higher_high && is_lower_low) {
        trend_structure = "EXPANDING";       // 扩张
    } else if (is_lower_high && is_higher_low) {
        trend_structure = "CONTRACTING";     // 收缩
    } else {
        trend_structure = "NEUTRAL";         // 中性
    }
    
    result.set("is_higher_high", Value::fromBoolean(is_higher_high));
    result.set("is_lower_low", Value::fromBoolean(is_lower_low));
    result.set("is_higher_low", Value::fromBoolean(is_higher_low));
    result.set("is_lower_high", Value::fromBoolean(is_lower_high));
    result.set("trend_structure", Value::fromString(trend_structure));
    
    return result;
}

} // namespace prophet::indicators

