/*
 * ============================================================================
 * 文件名：VWAPBands.cpp
 * 指标名：VWAP Bands（VWAP通道）
 * 类别：成交量指标
 * 
 * 创建日期：2025-10-31
 * ============================================================================
 */

#include "prophet/indicators/calculator.hpp"
#include "prophet/indicators/registry.hpp"
#include <cmath>
#include <numeric>

namespace prophet::indicators {

IndicatorResult Calculator::VWAPBands(
    const std::vector<double>& high,
    const std::vector<double>& low,
    const std::vector<double>& close,
    const std::vector<double>& volume,
    double VWAPBANDS_STD_DEV
) {
    IndicatorResult result;
    
    if (close.size() < 2 || volume.size() != close.size()) {
        result.setSeries("vwap", std::vector<Value>{});
        result.setSeries("upper", std::vector<Value>{});
        result.setSeries("lower", std::vector<Value>{});
        result.set("position", Value::fromString("MIDDLE"));
        return result;
    }
    
    // 计算VWAP序列（累积计算）
    std::vector<Value> vwap_series, upper_series, lower_series;
    vwap_series.reserve(close.size());
    upper_series.reserve(close.size());
    lower_series.reserve(close.size());
    
    double cumulative_pv = 0.0;
    double cumulative_v = 0.0;
    
    for (size_t i = 0; i < close.size(); ++i) {
        double typical_price = (high[i] + low[i] + close[i]) / 3.0;
        cumulative_pv += typical_price * volume[i];
        cumulative_v += volume[i];
        
        double vwap = (cumulative_v > 0.0) ? (cumulative_pv / cumulative_v) : close[i];
        
        // 计算标准差（基于当前累积数据）
        double sum_sq_diff = 0.0;
        for (size_t j = 0; j <= i; ++j) {
            double tp = (high[j] + low[j] + close[j]) / 3.0;
            double diff = tp - vwap;
            sum_sq_diff += (diff * diff) * volume[j];
        }
        
        double variance = (cumulative_v > 0.0) ? (sum_sq_diff / cumulative_v) : 0.0;
        double std_dev = std::sqrt(variance);
        
        // 计算上下轨
        double upper = vwap + VWAPBANDS_STD_DEV * std_dev;
        double lower = vwap - VWAPBANDS_STD_DEV * std_dev;
        
        vwap_series.push_back(Value::fromNumber(vwap));
        upper_series.push_back(Value::fromNumber(upper));
        lower_series.push_back(Value::fromNumber(lower));
    }
    
    if (vwap_series.empty()) {
        result.setSeries("vwap", std::vector<Value>{});
        result.setSeries("upper", std::vector<Value>{});
        result.setSeries("lower", std::vector<Value>{});
        result.set("position", Value::fromString("MIDDLE"));
        return result;
    }
    
    result.setSeries("vwap", vwap_series);
    result.setSeries("upper", upper_series);
    result.setSeries("lower", lower_series);
    
    // 判断价格位置（基于最新值）
    double vwap = vwap_series.back().toNumber();
    double upper = upper_series.back().toNumber();
    double lower = lower_series.back().toNumber();
    double current_price = close.back();
    
    std::string position;
    if (current_price >= upper) {
        position = "ABOVE_UPPER";
    } else if (current_price <= lower) {
        position = "BELOW_LOWER";
    } else if (current_price > vwap) {
        position = "ABOVE_VWAP";
    } else if (current_price < vwap) {
        position = "BELOW_VWAP";
    } else {
        position = "AT_VWAP";
    }
    
    result.set("position", Value::fromString(position));
    
    return result;
}

} // namespace prophet::indicators

