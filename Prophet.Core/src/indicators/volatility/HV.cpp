/*
 * ============================================================================
 * 文件名：HV.cpp
 * 指标名：Historical Volatility（历史波动率）
 * 类别：波动率指标
 * 
 * 创建日期：2025-10-31
 * ============================================================================
 */

#include "prophet/indicators/calculator.hpp"
#include "prophet/indicators/registry.hpp"
#include <cmath>
#include <numeric>

namespace prophet::indicators {

IndicatorResult Calculator::HV(
    const std::vector<double>& close,
    int HV_PERIOD
) {
    IndicatorResult result;
    
    if (close.size() < static_cast<size_t>(HV_PERIOD + 1)) {
        result.setSeries("value", std::vector<Value>{});
        result.setSeries("annual", std::vector<Value>{});
        result.set("volatility", Value::fromString("LOW"));
        return result;
    }
    
    // 计算历史波动率序列（滑动窗口）
    std::vector<Value> hv_series, annual_series;
    int valid_count = static_cast<int>(close.size()) - HV_PERIOD;
    hv_series.reserve(valid_count);
    annual_series.reserve(valid_count);
    
    for (int i = 0; i < valid_count; ++i) {
        // 计算对数收益率
        std::vector<double> log_returns;
        for (int j = 0; j < HV_PERIOD; ++j) {
            int idx = i + j + 1;
            if (idx < static_cast<int>(close.size())) {
                // 价格必须为正，否则 log 无定义（脏数据直接跳过该窗口）
                if (close[idx] <= 0.0 || close[idx - 1] <= 0.0) {
                    log_returns.clear();
                    break;
                }
                double ret = std::log(close[idx] / close[idx - 1]);
                log_returns.push_back(ret);
            }
        }
        
        if (log_returns.size() < 2) {
            continue;
        }
        
        // 计算标准差
        double mean = std::accumulate(log_returns.begin(), log_returns.end(), 0.0) / log_returns.size();
        
        double sum_sq_diff = 0.0;
        for (double ret : log_returns) {
            double diff = ret - mean;
            sum_sq_diff += diff * diff;
        }
        
        double variance = sum_sq_diff / (log_returns.size() - 1);
        double std_dev = std::sqrt(variance);
        
        // 日波动率转年化波动率（假设252个交易日）
        double annual_vol = std_dev * std::sqrt(252.0) * 100.0;  // 转为百分比
        
        hv_series.push_back(Value::fromNumber(std_dev * 100.0));  // 日波动率百分比
        annual_series.push_back(Value::fromNumber(annual_vol));   // 年化波动率
    }
    
    if (hv_series.empty()) {
        result.setSeries("value", std::vector<Value>{});
        result.setSeries("annual", std::vector<Value>{});
        result.set("volatility", Value::fromString("LOW"));
        return result;
    }
    
    result.setSeries("value", hv_series);
    result.setSeries("annual", annual_series);
    
    // 判断波动率等级（基于最新值）
    double annual_vol = annual_series.back().toNumber();
    std::string volatility;
    if (annual_vol < 15.0) {
        volatility = "LOW";
    } else if (annual_vol < 25.0) {
        volatility = "MEDIUM";
    } else if (annual_vol < 40.0) {
        volatility = "HIGH";
    } else {
        volatility = "VERY_HIGH";
    }
    
    result.set("volatility", Value::fromString(volatility));
    
    return result;
}

} // namespace prophet::indicators

