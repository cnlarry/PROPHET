/*
 * ============================================================================
 * 文件名：ChandelierExit.cpp
 * 指标名：Chandelier Exit（吊灯止损）
 * 类别：趋势指标 / 风险管理
 * 
 * 创建日期：2025-10-31
 * ============================================================================
 */

#include "prophet/indicators/calculator.hpp"
#include "prophet/indicators/registry.hpp"
#include <ta_libc.h>
#include <algorithm>
#include <cmath>

namespace prophet::indicators {

IndicatorResult Calculator::ChandelierExit(
    const std::vector<double>& high,
    const std::vector<double>& low,
    const std::vector<double>& close,
    int CHANDELIER_PERIOD,
    double CHANDELIER_MULTIPLIER
) {
    IndicatorResult result;
    
    if (close.size() < static_cast<size_t>(CHANDELIER_PERIOD)) {
        result.setSeries("long_stop", std::vector<Value>{});
        result.setSeries("short_stop", std::vector<Value>{});
        result.set("direction", Value::fromString("NEUTRAL"));
        return result;
    }
    
    // 计算ATR
    std::vector<double> atr_values(close.size());
    int atr_outBegIdx, atr_outNbElement;
    
    TA_RetCode atr_retCode = TA_ATR(
        0, static_cast<int>(close.size()) - 1,
        high.data(), low.data(), close.data(),
        CHANDELIER_PERIOD,
        &atr_outBegIdx, &atr_outNbElement,
        atr_values.data()
    );
    
    if (atr_retCode != TA_SUCCESS || atr_outNbElement == 0) {
        result.setSeries("long_stop", std::vector<Value>{});
        result.setSeries("short_stop", std::vector<Value>{});
        result.set("direction", Value::fromString("NEUTRAL"));
        return result;
    }
    
    // 提取ATR有效数据
    std::vector<double> atr_valid(atr_outNbElement);
    for (int i = 0; i < atr_outNbElement; ++i) {
        int idx = atr_outBegIdx + i;
        atr_valid[i] = atr_values[idx];
    }
    
    // 计算Chandelier Exit序列
    std::vector<Value> long_stop_series, short_stop_series;
    int valid_count = std::min(atr_outNbElement, static_cast<int>(close.size()) - CHANDELIER_PERIOD + 1);
    long_stop_series.reserve(valid_count);
    short_stop_series.reserve(valid_count);
    
    for (int i = 0; i < valid_count; ++i) {
        int atr_idx = i;
        int price_start_idx = atr_outBegIdx + i - CHANDELIER_PERIOD + 1;
        int price_end_idx = atr_outBegIdx + i + 1;
        
        if (price_start_idx >= 0 && price_end_idx <= static_cast<int>(high.size())) {
            double highest = *std::max_element(high.begin() + price_start_idx, high.begin() + price_end_idx);
            double lowest = *std::min_element(low.begin() + price_start_idx, low.begin() + price_end_idx);
            
            double long_stop = highest - CHANDELIER_MULTIPLIER * atr_valid[atr_idx];
            double short_stop = lowest + CHANDELIER_MULTIPLIER * atr_valid[atr_idx];
            
            long_stop_series.push_back(Value::fromNumber(long_stop));
            short_stop_series.push_back(Value::fromNumber(short_stop));
        }
    }
    
    if (long_stop_series.empty()) {
        result.setSeries("long_stop", std::vector<Value>{});
        result.setSeries("short_stop", std::vector<Value>{});
        result.set("direction", Value::fromString("NEUTRAL"));
        return result;
    }
    
    result.setSeries("long_stop", long_stop_series);
    result.setSeries("short_stop", short_stop_series);
    
    // 判断当前方向（基于最新值）
    double curr_long_stop = long_stop_series.back().toNumber();
    double curr_short_stop = short_stop_series.back().toNumber();
    std::string direction;
    if (close.back() > curr_long_stop) {
        direction = "LONG";
    } else if (close.back() < curr_short_stop) {
        direction = "SHORT";
    } else {
        direction = "NEUTRAL";
    }
    
    result.set("direction", Value::fromString(direction));
    
    return result;
}

} // namespace prophet::indicators

