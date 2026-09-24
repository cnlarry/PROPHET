/*
 * ============================================================================
 * 文件名：VWMA.cpp
 * 指标名：Volume Weighted Moving Average（成交量加权移动平均）
 * 类别：趋势指标
 * 
 * 创建日期：2025-10-31
 * ============================================================================
 */

#include "prophet/indicators/calculator.hpp"
#include "prophet/indicators/registry.hpp"
#include <numeric>
#include <cmath>

namespace prophet::indicators {

IndicatorResult Calculator::VWMA(
    const std::vector<double>& close,
    const std::vector<double>& volume,
    int VWMA_PERIOD
) {
    IndicatorResult result;
    
    if (close.size() < static_cast<size_t>(VWMA_PERIOD) || 
        volume.size() != close.size()) {
        result.setSeries("value", std::vector<Value>{});
        result.set("trend", Value::fromString("NEUTRAL"));
        result.set("slope", Value::fromNumber(0.0));
        return result;
    }
    
    // 计算VWMA序列
    std::vector<Value> vwma_series;
    int valid_count = static_cast<int>(close.size()) - VWMA_PERIOD + 1;
    vwma_series.reserve(valid_count);
    
    for (int i = 0; i < valid_count; ++i) {
        size_t start_idx = i;
        size_t end_idx = start_idx + VWMA_PERIOD;
        
        double sum_pv = 0.0;  // price * volume
        double sum_v = 0.0;   // volume
        
        for (size_t j = start_idx; j < end_idx; ++j) {
            sum_pv += close[j] * volume[j];
            sum_v += volume[j];
        }
        
        double vwma = (sum_v > 0.0) ? (sum_pv / sum_v) : close[end_idx - 1];
        vwma_series.push_back(Value::fromNumber(vwma));
    }
    
    if (vwma_series.empty()) {
        result.setSeries("value", std::vector<Value>{});
        result.set("trend", Value::fromString("NEUTRAL"));
        result.set("slope", Value::fromNumber(0.0));
        return result;
    }
    
    result.setSeries("value", vwma_series);
    
    // 计算斜率和趋势（基于有效数据范围）
    std::vector<double> vwma_valid(vwma_series.size());
    for (size_t i = 0; i < vwma_series.size(); ++i) {
        vwma_valid[i] = vwma_series[i].toNumber();
    }
    double slope = calculateSlope(vwma_valid, 3);
    std::string trend = determineTrend(vwma_valid);
    
    result.set("trend", Value::fromString(trend));
    result.set("slope", Value::fromNumber(slope));
    
    return result;
}

} // namespace prophet::indicators

