/*
 * ============================================================================
 * 文件名：Keltner.cpp
 * 指标名：Keltner (Keltner Channel)
 * 类别：波动率指标
 * 
 * 创建日期：2025-10-31
 * 说明：Keltner通道 = EMA ± (ATR × multiplier)
 * ============================================================================
 */

#include "prophet/indicators/calculator.hpp"
#include "prophet/indicators/registry.hpp"
#include <ta_libc.h>
#include <cmath>
#include <algorithm>
#include <numeric>
#include <stdexcept>

namespace prophet::indicators {

IndicatorResult Calculator::Keltner(
    const std::vector<double>& high,
    const std::vector<double>& low,
    const std::vector<double>& close,
    int KELTNER_PERIOD,
    double KELTNER_MULTIPLIER
) {
    IndicatorResult result;

    if (close.size() < static_cast<size_t>(KELTNER_PERIOD) ||
        high.size() != close.size() ||
        low.size() != close.size()) {
        result.setSeries("upper", std::vector<Value>{});
        result.setSeries("middle", std::vector<Value>{});
        result.setSeries("lower", std::vector<Value>{});
        result.set("width", Value::fromNumber(0.0));
        result.set("percent_k", Value::fromNumber(0.5));
        return result;
    }

    // 1. 计算中轨（EMA）
    std::vector<double> ema_values(close.size(), 0.0);
    int ema_outBegIdx, ema_outNbElement;
    
    TA_RetCode ema_ret = TA_EMA(
        0, static_cast<int>(close.size()) - 1,
        close.data(),
        KELTNER_PERIOD,
        &ema_outBegIdx, &ema_outNbElement,
        ema_values.data()
    );

    if (ema_ret != TA_SUCCESS || ema_outNbElement == 0) {
        result.setSeries("upper", std::vector<Value>{});
        result.setSeries("middle", std::vector<Value>{});
        result.setSeries("lower", std::vector<Value>{});
        result.set("width", Value::fromNumber(0.0));
        result.set("percent_k", Value::fromNumber(0.5));
        return result;
    }

    // 2. 计算ATR
    std::vector<double> atr_values(close.size(), 0.0);
    int atr_outBegIdx, atr_outNbElement;
    
    TA_RetCode atr_ret = TA_ATR(
        0, static_cast<int>(close.size()) - 1,
        high.data(), low.data(), close.data(),
        KELTNER_PERIOD,
        &atr_outBegIdx, &atr_outNbElement,
        atr_values.data()
    );

    if (atr_ret != TA_SUCCESS || atr_outNbElement == 0) {
        result.setSeries("upper", std::vector<Value>{});
        result.setSeries("middle", std::vector<Value>{});
        result.setSeries("lower", std::vector<Value>{});
        result.set("width", Value::fromNumber(0.0));
        result.set("percent_k", Value::fromNumber(0.5));
        return result;
    }

    // 对齐EMA和ATR的有效数据范围
    int start_idx = std::max(ema_outBegIdx, atr_outBegIdx);
    int end_idx = std::min(ema_outNbElement, atr_outNbElement);
    int valid_count = end_idx - start_idx;
    
    if (valid_count <= 0) {
        result.setSeries("upper", std::vector<Value>{});
        result.setSeries("middle", std::vector<Value>{});
        result.setSeries("lower", std::vector<Value>{});
        result.set("width", Value::fromNumber(0.0));
        result.set("percent_k", Value::fromNumber(0.5));
        return result;
    }

    // 3. 计算上下轨序列
    std::vector<Value> upper_series, middle_series, lower_series;
    upper_series.reserve(valid_count);
    middle_series.reserve(valid_count);
    lower_series.reserve(valid_count);
    
    for (int i = 0; i < valid_count; ++i) {
        int ema_idx = start_idx - ema_outBegIdx + i;
        int atr_idx = start_idx - atr_outBegIdx + i;
        int price_idx = start_idx + i;
        
        if (ema_idx >= 0 && ema_idx < ema_outNbElement &&
            atr_idx >= 0 && atr_idx < atr_outNbElement &&
            price_idx >= 0 && price_idx < static_cast<int>(close.size())) {
            double middle = ema_values[ema_idx];
            double atr = atr_values[atr_idx];
            double band_width = atr * KELTNER_MULTIPLIER;
            
            double upper = middle + band_width;
            double lower = middle - band_width;
            
            upper_series.push_back(Value::fromNumber(upper));
            middle_series.push_back(Value::fromNumber(middle));
            lower_series.push_back(Value::fromNumber(lower));
        }
    }
    
    if (upper_series.empty()) {
        result.setSeries("upper", std::vector<Value>{});
        result.setSeries("middle", std::vector<Value>{});
        result.setSeries("lower", std::vector<Value>{});
        result.set("width", Value::fromNumber(0.0));
        result.set("percent_k", Value::fromNumber(0.5));
        return result;
    }
    
    result.setSeries("upper", upper_series);
    result.setSeries("middle", middle_series);
    result.setSeries("lower", lower_series);
    
    // 4. 计算通道宽度和位置（基于最新值）
    double curr_upper = upper_series.back().toNumber();
    double curr_middle = middle_series.back().toNumber();
    double curr_lower = lower_series.back().toNumber();
    
    double width = 0.0;
    if (curr_middle > 1e-10) {
        width = ((curr_upper - curr_lower) / curr_middle) * 100.0;
    }

    // 5. 计算价格在通道中的位置
    double percent_k = 0.5;
    double channel_range = curr_upper - curr_lower;
    if (channel_range > 1e-10) {
        percent_k = (close.back() - curr_lower) / channel_range;
    }

    result.set("width", Value::fromNumber(width));
    result.set("percent_k", Value::fromNumber(percent_k));

    return result;
}

} // namespace prophet::indicators

