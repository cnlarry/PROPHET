/**
 * @file VolumePriceTrend.cpp
 * @brief Volume Price Trend indicator
 */

#include "prophet/indicators/calculator.hpp"
#include <cmath>

namespace prophet {
namespace indicators {

IndicatorResult Calculator::VolumePriceTrend(
    const std::vector<double>& close,
    const std::vector<double>& volume
) const {
    IndicatorResult result;
    
    if (close.size() < 2 || volume.size() < 2 || close.size() != volume.size()) {
        result.setSeries("value", std::vector<Value>{});
        result.set("trend", Value::fromString("NEUTRAL"));
        return result;
    }
    
    // Calculate VPT
    // VPT = VPT[1] + Volume * (Close - Close[1]) / Close[1]
    std::vector<double> vpt_values(close.size());
    vpt_values[0] = 0.0;
    
    for (size_t i = 1; i < close.size(); ++i) {
        double prev_close = close[i - 1];
        double curr_close = close[i];
        double curr_volume = volume[i];
        
        if (prev_close > 1e-10) {
            double price_change_pct = (curr_close - prev_close) / prev_close;
            vpt_values[i] = vpt_values[i - 1] + curr_volume * price_change_pct;
        } else {
            vpt_values[i] = vpt_values[i - 1];
        }
    }
    
    // 提取有效数据序列（从索引1开始，因为索引0是初始值0）
    std::vector<Value> vpt_series;
    vpt_series.reserve(vpt_values.size() - 1);
    for (size_t i = 1; i < vpt_values.size(); ++i) {
        vpt_series.push_back(Value::fromNumber(vpt_values[i]));
    }
    
    if (vpt_series.empty()) {
        result.setSeries("value", std::vector<Value>{});
        result.set("trend", Value::fromString("NEUTRAL"));
        return result;
    }
    
    result.setSeries("value", vpt_series);
    
    // Determine trend (compare with moving average of VPT)
    double vpt = vpt_series.back().toNumber();
    std::string trend = "NEUTRAL";
    int lookback = std::min(static_cast<int>(vpt_series.size()), 20);
    
    if (lookback >= 2) {
        double vpt_sum = 0.0;
        for (int i = 0; i < lookback; ++i) {
            vpt_sum += vpt_series[vpt_series.size() - 1 - i].toNumber();
        }
        double vpt_avg = vpt_sum / lookback;
        
        if (vpt > vpt_avg * 1.05) {
            trend = "BULLISH";
        } else if (vpt < vpt_avg * 0.95) {
            trend = "BEARISH";
        }
    }
    
    result.set("trend", Value::fromString(trend));
    
    return result;
}

} // namespace indicators
} // namespace prophet

