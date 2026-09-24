/*
 * ============================================================================
 * 文件名：TSI.cpp
 * 指标名：True Strength Index（真实强度指标）
 * 类别：动量指标
 * 
 * 创建日期：2025-10-31
 * ============================================================================
 */

#include "prophet/indicators/calculator.hpp"
#include "prophet/indicators/registry.hpp"
#include <ta_libc.h>
#include <vector>
#include <cmath>

namespace prophet::indicators {

IndicatorResult Calculator::TSI(
    const std::vector<double>& close,
    int LONG_PERIOD,
    int SHORT_PERIOD,
    int /* SIGNAL_PERIOD */
) {
    IndicatorResult result;
    
    int required_size = LONG_PERIOD + SHORT_PERIOD + 10;
    if (static_cast<int>(close.size()) < required_size) {
        result.setSeries("tsi", std::vector<Value>{});
        result.setSeries("signal", std::vector<Value>{});
        result.setSeries("histogram", std::vector<Value>{});
        result.set("trend", Value::fromString("NEUTRAL"));
        return result;
    }
    
    // 计算价格变化（momentum）
    std::vector<double> momentum;
    std::vector<double> abs_momentum;
    
    for (size_t i = 1; i < close.size(); ++i) {
        double change = close[i] - close[i - 1];
        momentum.push_back(change);
        abs_momentum.push_back(std::abs(change));
    }
    
    // 双重EMA平滑 momentum
    std::vector<double> ema1_momentum(momentum.size());
    std::vector<double> ema2_momentum(momentum.size());
    int outBegIdx1, outNbElement1;
    
    TA_EMA(0, static_cast<int>(momentum.size()) - 1,
           momentum.data(), LONG_PERIOD,
           &outBegIdx1, &outNbElement1, ema1_momentum.data());
    
    if (outNbElement1 == 0) {
        result.setSeries("tsi", std::vector<Value>{});
        result.setSeries("signal", std::vector<Value>{});
        result.setSeries("histogram", std::vector<Value>{});
        result.set("trend", Value::fromString("NEUTRAL"));
        return result;
    }
    
    std::vector<double> valid_ema1(ema1_momentum.begin() + outBegIdx1, 
                                   ema1_momentum.begin() + outBegIdx1 + outNbElement1);
    int outBegIdx2, outNbElement2;
    TA_EMA(0, static_cast<int>(valid_ema1.size()) - 1,
           valid_ema1.data(), SHORT_PERIOD,
           &outBegIdx2, &outNbElement2, ema2_momentum.data());
    
    if (outNbElement2 == 0) {
        result.setSeries("tsi", std::vector<Value>{});
        result.setSeries("signal", std::vector<Value>{});
        result.setSeries("histogram", std::vector<Value>{});
        result.set("trend", Value::fromString("NEUTRAL"));
        return result;
    }
    
    // 双重EMA平滑 abs_momentum
    std::vector<double> ema1_abs(abs_momentum.size());
    std::vector<double> ema2_abs(abs_momentum.size());
    
    TA_EMA(0, static_cast<int>(abs_momentum.size()) - 1,
           abs_momentum.data(), LONG_PERIOD,
           &outBegIdx1, &outNbElement1, ema1_abs.data());
    
    if (outNbElement1 == 0) {
        result.setSeries("tsi", std::vector<Value>{});
        result.setSeries("signal", std::vector<Value>{});
        result.setSeries("histogram", std::vector<Value>{});
        result.set("trend", Value::fromString("NEUTRAL"));
        return result;
    }
    
    std::vector<double> valid_ema1_abs(ema1_abs.begin() + outBegIdx1, 
                                       ema1_abs.begin() + outBegIdx1 + outNbElement1);
    int outBegIdx2_abs, outNbElement2_abs;
    TA_EMA(0, static_cast<int>(valid_ema1_abs.size()) - 1,
           valid_ema1_abs.data(), SHORT_PERIOD,
           &outBegIdx2_abs, &outNbElement2_abs, ema2_abs.data());
    
    if (outNbElement2_abs == 0) {
        result.setSeries("tsi", std::vector<Value>{});
        result.setSeries("signal", std::vector<Value>{});
        result.setSeries("histogram", std::vector<Value>{});
        result.set("trend", Value::fromString("NEUTRAL"));
        return result;
    }
    
    // 对齐数据：取两者重叠的部分
    int start_idx = std::max(outBegIdx2, outBegIdx2_abs);
    int end_idx = std::min(outNbElement2, outNbElement2_abs);
    int valid_count = end_idx - start_idx;
    
    if (valid_count <= 0) {
        result.setSeries("tsi", std::vector<Value>{});
        result.setSeries("signal", std::vector<Value>{});
        result.setSeries("histogram", std::vector<Value>{});
        result.set("trend", Value::fromString("NEUTRAL"));
        return result;
    }
    
    // 计算TSI序列
    std::vector<Value> tsi_series, signal_series, histogram_series;
    tsi_series.reserve(valid_count);
    signal_series.reserve(valid_count);
    histogram_series.reserve(valid_count);
    
    for (int i = start_idx; i < end_idx; ++i) {
        int idx1 = i - outBegIdx2;
        int idx2 = i - outBegIdx2_abs;
        
        double denominator = ema2_abs[idx2];
        double tsi = 0.0;
        
        if (std::abs(denominator) > 1e-10) {
            tsi = 100.0 * (ema2_momentum[idx1] / denominator);
        }
        
        // 计算信号线（TSI的EMA）- 简化版本，使用TSI本身
        double signal = tsi;
        double histogram = tsi - signal;
        
        tsi_series.push_back(Value::fromNumber(tsi));
        signal_series.push_back(Value::fromNumber(signal));
        histogram_series.push_back(Value::fromNumber(histogram));
    }
    
    result.setSeries("tsi", tsi_series);
    result.setSeries("signal", signal_series);
    result.setSeries("histogram", histogram_series);
    
    // 判断趋势（基于最新值）
    if (tsi_series.empty()) {
        result.set("trend", Value::fromString("NEUTRAL"));
        return result;
    }
    
    double tsi = tsi_series.back().toNumber();
    std::string trend;
    if (tsi > 25.0) {
        trend = "STRONG_BULLISH";
    } else if (tsi > 0.0) {
        trend = "BULLISH";
    } else if (tsi > -25.0) {
        trend = "BEARISH";
    } else {
        trend = "STRONG_BEARISH";
    }
    
    result.set("trend", Value::fromString(trend));
    
    return result;
}

} // namespace prophet::indicators

