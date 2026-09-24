/*
 * ============================================================================
 * 文件名：ChoppinessIndex.cpp
 * 指标名：Choppiness Index（震荡指标）
 * 类别：趋势指标
 * 
 * 创建日期：2025-10-31
 * ============================================================================
 */

#include "prophet/indicators/calculator.hpp"
#include "prophet/indicators/registry.hpp"
#include <ta_libc.h>
#include <cmath>
#include <algorithm>
#include <numeric>

namespace prophet::indicators {

IndicatorResult Calculator::ChoppinessIndex(
    const std::vector<double>& high,
    const std::vector<double>& low,
    const std::vector<double>& close,
    int CHOP_PERIOD
) {
    IndicatorResult result;
    
    if (close.size() < static_cast<size_t>(CHOP_PERIOD + 1)) {
        result.setSeries("value", std::vector<Value>{});
        result.set("market_state", Value::fromString("NEUTRAL"));
        return result;
    }
    
    // 计算ATR和
    std::vector<double> atr_values(close.size());
    int atr_outBegIdx, atr_outNbElement;
    
    TA_RetCode atr_retCode = TA_ATR(
        0, static_cast<int>(close.size()) - 1,
        high.data(), low.data(), close.data(),
        1,  // 单期ATR（即真实波幅）
        &atr_outBegIdx, &atr_outNbElement,
        atr_values.data()
    );
    
    if (atr_retCode != TA_SUCCESS || atr_outNbElement < CHOP_PERIOD) {
        result.setSeries("value", std::vector<Value>{});
        result.set("market_state", Value::fromString("NEUTRAL"));
        return result;
    }
    
    // 提取ATR有效数据（TA-Lib 将有效值写在输出数组开头，outBegIdx 是价格对齐偏移）
    std::vector<double> atr_valid(atr_outNbElement);
    for (int i = 0; i < atr_outNbElement; ++i) {
        atr_valid[i] = atr_values[i];
    }
    
    // 计算Choppiness Index序列
    std::vector<Value> chop_series;
    int valid_count = atr_outNbElement - CHOP_PERIOD + 1;
    chop_series.reserve(valid_count);
    
    for (int i = 0; i < valid_count; ++i) {
        // 计算最近N期ATR的和
        double sum_atr = 0.0;
        for (int j = 0; j < CHOP_PERIOD; ++j) {
            sum_atr += atr_valid[i + j];
        }
        
        // 计算最近N期的最高价和最低价
        int price_start_idx = atr_outBegIdx + i;
        int price_end_idx = price_start_idx + CHOP_PERIOD;
        
        if (price_start_idx >= 0 && price_end_idx <= static_cast<int>(high.size())) {
            double highest = *std::max_element(high.begin() + price_start_idx, high.begin() + price_end_idx);
            double lowest = *std::min_element(low.begin() + price_start_idx, low.begin() + price_end_idx);
            double range = highest - lowest;
            
            // 计算Choppiness Index
            double chop = 0.0;
            if (range > 0.0 && sum_atr > 0.0) {
                chop = 100.0 * std::log10(sum_atr / range) / std::log10(static_cast<double>(CHOP_PERIOD));
            } else {
                chop = 50.0;
            }
            chop_series.push_back(Value::fromNumber(chop));
        }
    }
    
    if (chop_series.empty()) {
        result.setSeries("value", std::vector<Value>{});
        result.set("market_state", Value::fromString("NEUTRAL"));
        return result;
    }
    
    result.setSeries("value", chop_series);
    
    // 判断市场状态（基于最新值）
    double chop = chop_series.back().toNumber();
    std::string market_state;
    if (chop >= 61.8) {
        market_state = "CHOPPY";      // 震荡市场
    } else if (chop <= 38.2) {
        market_state = "TRENDING";    // 趋势市场
    } else {
        market_state = "TRANSITIONAL"; // 过渡状态
    }
    
    result.set("market_state", Value::fromString(market_state));
    
    return result;
}

} // namespace prophet::indicators

