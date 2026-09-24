/*
 * ============================================================================
 * 文件名：Supertrend.cpp
 * 指标名：Supertrend
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
#include <stdexcept>

namespace prophet::indicators {

IndicatorResult Calculator::Supertrend(
    const std::vector<double>& high,
    const std::vector<double>& low,
    const std::vector<double>& close,
    int SUPERTREND_PERIOD,
    double SUPERTREND_MULTIPLIER
) {
    IndicatorResult result;
    
    if (close.size() < static_cast<size_t>(SUPERTREND_PERIOD)) {
        result.setSeries("value", std::vector<Value>{});
        result.set("direction", Value::fromString("NEUTRAL"));
        result.set("trend", Value::fromString("NEUTRAL"));
        result.set("islong", Value::fromBoolean(false));
        result.set("isshort", Value::fromBoolean(false));
        return result;
    }
    
    // 计算ATR
    std::vector<double> atr_values(close.size());
    int atr_outBegIdx, atr_outNbElement;
    TA_RetCode atr_retCode = TA_ATR(
        0, static_cast<int>(close.size()) - 1,
        high.data(), low.data(), close.data(),
        SUPERTREND_PERIOD,
        &atr_outBegIdx, &atr_outNbElement,
        atr_values.data()
    );
    
    if (atr_retCode != TA_SUCCESS || atr_outNbElement == 0) {
        result.setSeries("value", std::vector<Value>{});
        result.set("direction", Value::fromString("NEUTRAL"));
        result.set("trend", Value::fromString("NEUTRAL"));
        result.set("islong", Value::fromBoolean(false));
        result.set("isshort", Value::fromBoolean(false));
        return result;
    }
    
    // 提取ATR有效数据
    std::vector<double> atr_valid(atr_outNbElement);
    for (int i = 0; i < atr_outNbElement; ++i) {
        int idx = atr_outBegIdx + i;
        atr_valid[i] = atr_values[idx];
    }
    
    // 计算Supertrend序列
    std::vector<Value> supertrend_series;
    supertrend_series.reserve(atr_outNbElement);
    
    // 初始化前一个Supertrend值
    double prev_supertrend = 0.0;
    std::string prev_direction = "NEUTRAL";
    
    for (int i = 0; i < atr_outNbElement; ++i) {
        int price_idx = atr_outBegIdx + i;
        if (price_idx < 0 || price_idx >= static_cast<int>(high.size())) {
            continue;
        }
        
        double hl_avg = (high[price_idx] + low[price_idx]) / 2.0;
        double basic_upper = hl_avg + SUPERTREND_MULTIPLIER * atr_valid[i];
        double basic_lower = hl_avg - SUPERTREND_MULTIPLIER * atr_valid[i];
        
        double supertrend_value;
        std::string direction;
        
        if (i == 0) {
            // 第一个值：简单判断
            if (close[price_idx] > hl_avg) {
                supertrend_value = basic_lower;
                direction = "UP";
            } else {
                supertrend_value = basic_upper;
                direction = "DOWN";
            }
        } else {
            // 后续值：考虑前一个Supertrend值
            double final_upper = std::max(basic_upper, prev_supertrend);
            double final_lower = std::min(basic_lower, prev_supertrend);
            
            if (close[price_idx] <= prev_supertrend && prev_direction == "UP") {
                // 从上升转为下降
                supertrend_value = final_upper;
                direction = "DOWN";
            } else if (close[price_idx] >= prev_supertrend && prev_direction == "DOWN") {
                // 从下降转为上升
                supertrend_value = final_lower;
                direction = "UP";
            } else {
                // 保持方向
                if (prev_direction == "UP") {
                    supertrend_value = final_lower;
                    direction = "UP";
                } else {
                    supertrend_value = final_upper;
                    direction = "DOWN";
                }
            }
        }
        
        supertrend_series.push_back(Value::fromNumber(supertrend_value));
        prev_supertrend = supertrend_value;
        prev_direction = direction;
    }
    
    if (supertrend_series.empty()) {
        result.setSeries("value", std::vector<Value>{});
        result.set("direction", Value::fromString("NEUTRAL"));
        result.set("trend", Value::fromString("NEUTRAL"));
        result.set("islong", Value::fromBoolean(false));
        result.set("isshort", Value::fromBoolean(false));
        return result;
    }
    
    result.setSeries("value", supertrend_series);
    
    // 判断最新值的方向和趋势
    std::string direction = prev_direction;
    std::string trend = (direction == "UP") ? "BULLISH" : "BEARISH";
    
    result.set("direction", Value::fromString(direction));
    result.set("trend", Value::fromString(trend));
    result.set("islong", Value::fromBoolean(direction == "UP"));
    result.set("isshort", Value::fromBoolean(direction == "DOWN"));
    
    return result;
}

} // namespace prophet::indicators

