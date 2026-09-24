/*
 * ============================================================================
 * 文件名：VWAP.cpp
 * 指标名：VWAP (Volume Weighted Average Price)
 * 类别：价格指标
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
#include <limits>
#include <stdexcept>

namespace prophet::indicators {

IndicatorResult Calculator::VWAP(
    const std::vector<double>& high,
    const std::vector<double>& low,
    const std::vector<double>& close,
    const std::vector<double>& volume,
    int VWAP_PERIOD
) {
    IndicatorResult result;

    if (close.empty() ||
        high.size() != close.size() ||
        low.size() != close.size() ||
        volume.size() != close.size()) {
        result.setSeries("value", std::vector<Value>{});
        result.set("distance", Value::fromNumber(0.0));
        result.set("above_vwap", Value::fromBoolean(false));
        return result;
    }

    // VWAP = Sum(Typical Price × Volume) / Sum(Volume)
    // Typical Price = (High + Low + Close) / 3
    
    // 🔑 关键修复：只返回有效数据，不填充NaN（与TA-Lib保持一致）
    // 这样ConvertIndicatorResultToC才能正确计算outBegin
    std::vector<Value> vwap_series;
    
    if (VWAP_PERIOD > 0) {
        // 滑动窗口VWAP：每个点计算最近VWAP_PERIOD根K线的VWAP
        // 前VWAP_PERIOD-1根K线没有足够数据，不包含在结果中
        int outBegin = VWAP_PERIOD - 1;
        int validCount = static_cast<int>(close.size()) - outBegin;
        
        if (validCount <= 0) {
            result.setSeries("value", std::vector<Value>{});
            result.set("distance", Value::fromNumber(0.0));
            result.set("above_vwap", Value::fromBoolean(false));
            return result;
        }
        
        vwap_series.reserve(validCount);
        
        // 只计算有效VWAP值（不填充前面的NaN）
        for (int i = outBegin; i < static_cast<int>(close.size()); ++i) {
            size_t start_idx = i - VWAP_PERIOD + 1;
            size_t end_idx = i + 1;
            
            double sum_pv = 0.0;  // Sum of (Price × Volume)
            double sum_v = 0.0;   // Sum of Volume
            
            for (size_t j = start_idx; j < end_idx; ++j) {
                double typical_price = (high[j] + low[j] + close[j]) / 3.0;
                sum_pv += typical_price * volume[j];
                sum_v += volume[j];
            }
            
            double vwap = 0.0;
            if (sum_v > 1e-10) {
                vwap = sum_pv / sum_v;
            } else {
                vwap = close[i];
            }
            
            vwap_series.push_back(Value::fromNumber(vwap));
        }
    } else {
        // 累积VWAP：从第一根K线开始累积计算，所有K线都有有效值
        vwap_series.reserve(close.size());
        
        double cumulative_pv = 0.0;  // Cumulative Sum of (Price × Volume)
        double cumulative_v = 0.0;   // Cumulative Sum of Volume
        
        for (size_t i = 0; i < close.size(); ++i) {
            double typical_price = (high[i] + low[i] + close[i]) / 3.0;
            cumulative_pv += typical_price * volume[i];
            cumulative_v += volume[i];
            
            double vwap = 0.0;
            if (cumulative_v > 1e-10) {
                vwap = cumulative_pv / cumulative_v;
            } else {
                vwap = close[i];
            }
            
            vwap_series.push_back(Value::fromNumber(vwap));
        }
    }
    
    if (vwap_series.empty()) {
        result.setSeries("value", std::vector<Value>{});
        result.set("distance", Value::fromNumber(0.0));
        result.set("above_vwap", Value::fromBoolean(false));
        return result;
    }
    
    result.setSeries("value", vwap_series);

    // 计算距离和位置（基于最新值）
    double vwap = vwap_series.back().toNumber();
    double current_price = close.back();
    double distance = 0.0;
    if (vwap > 1e-10) {
        distance = ((current_price - vwap) / vwap) * 100.0;
    }
    bool above_vwap = current_price > vwap;

    result.set("distance", Value::fromNumber(distance));
    result.set("above_vwap", Value::fromBoolean(above_vwap));

    return result;
}

} // namespace prophet::indicators

