/**
 * @file SchaffTrendCycle.cpp
 * @brief Schaff Trend Cycle indicator
 */

#include "prophet/indicators/calculator.hpp"
#include <ta_libc.h>
#include <cmath>
#include <algorithm>
#include <climits>

namespace prophet {
namespace indicators {

IndicatorResult Calculator::SchaffTrendCycle(
    const std::vector<double>& close,
    int FAST_PERIOD,
    int SLOW_PERIOD,
    int cycle_period
) const {
    IndicatorResult result;
    
    int min_size = std::max(SLOW_PERIOD, cycle_period) + 10;
    if (close.size() < static_cast<size_t>(min_size)) {
        result.setSeries("value", std::vector<Value>{});
        result.set("trend", Value::fromString("NEUTRAL"));
        result.set("signal", Value::fromString("NONE"));
        return result;
    }
    
    // Step 1: Calculate MACD
    std::vector<double> macd_values(close.size());
    std::vector<double> macd_signal(close.size());
    std::vector<double> macd_hist(close.size());
    int macd_outBegIdx = 0;
    int macd_outNbElement = 0;
    
    size_t close_size = close.size();
    int endIdx = static_cast<int>(close_size > static_cast<size_t>(INT_MAX) ? INT_MAX : close_size) - 1;
    TA_RetCode macd_retCode = TA_MACD(
        0, endIdx,
        close.data(),
        FAST_PERIOD,
        SLOW_PERIOD,
        9,  // signal period (not used directly)
        &macd_outBegIdx,
        &macd_outNbElement,
        macd_values.data(),
        macd_signal.data(),
        macd_hist.data()
    );
    
    if (macd_retCode != TA_SUCCESS || macd_outNbElement < cycle_period) {
        result.setSeries("value", std::vector<Value>{});
        result.set("trend", Value::fromString("NEUTRAL"));
        result.set("signal", Value::fromString("NONE"));
        return result;
    }
    
    // Step 2: Calculate Stochastic of MACD
    std::vector<double> macd_k(macd_outNbElement);
    for (int i = 0; i < macd_outNbElement; ++i) {
        if (i < cycle_period - 1) {
            macd_k[i] = 0.0;
            continue;
        }
        
        // Find min and max in the period
        double min_macd = macd_values[i];
        double max_macd = macd_values[i];
        for (int j = 0; j < cycle_period; ++j) {
            int idx = i - j;
            if (idx >= 0) {
                min_macd = std::min(min_macd, macd_values[idx]);
                max_macd = std::max(max_macd, macd_values[idx]);
            }
        }
        
        double range = max_macd - min_macd;
        if (range > 1e-10) {
            macd_k[i] = 100.0 * (macd_values[i] - min_macd) / range;
        } else {
            macd_k[i] = 50.0;
        }
    }
    
    // Step 3: Smooth stochastic with EMA (FastK to FastD)
    std::vector<double> macd_d(macd_k.size());
    int d_outBegIdx = 0;
    int d_outNbElement = 0;
    
    size_t macd_k_size = macd_k.size();
    int d_endIdx = static_cast<int>(macd_k_size > static_cast<size_t>(INT_MAX) ? INT_MAX : macd_k_size) - 1;
    TA_RetCode d_retCode = TA_EMA(
        0, d_endIdx,
        macd_k.data(),
        3,  // smoothing period
        &d_outBegIdx,
        &d_outNbElement,
        macd_d.data()
    );
    
    if (d_retCode != TA_SUCCESS || d_outNbElement == 0) {
        result.setSeries("value", std::vector<Value>{});
        result.set("trend", Value::fromString("NEUTRAL"));
        result.set("signal", Value::fromString("NONE"));
        return result;
    }
    
    // Step 4: Calculate Stochastic of Stochastic (final STC)
    std::vector<double> stc_values(d_outNbElement);
    for (int i = 0; i < d_outNbElement; ++i) {
        if (i < cycle_period - 1) {
            stc_values[i] = 50.0;
            continue;
        }
        
        // Find min and max of FastD in the period
        double min_d = macd_d[i];
        double max_d = macd_d[i];
        for (int j = 0; j < cycle_period; ++j) {
            int idx = i - j;
            if (idx >= 0) {
                min_d = std::min(min_d, macd_d[idx]);
                max_d = std::max(max_d, macd_d[idx]);
            }
        }
        
        double range = max_d - min_d;
        if (range > 1e-10) {
            stc_values[i] = 100.0 * (macd_d[i] - min_d) / range;
        } else {
            stc_values[i] = 50.0;
        }
    }
    
    // Final smoothing
    std::vector<double> final_stc(stc_values.size());
    int final_outBegIdx = 0;
    int final_outNbElement = 0;
    
    size_t stc_values_size = stc_values.size();
    int final_endIdx = static_cast<int>(stc_values_size > static_cast<size_t>(INT_MAX) ? INT_MAX : stc_values_size) - 1;
    TA_RetCode final_retCode = TA_EMA(
        0, final_endIdx,
        stc_values.data(),
        3,
        &final_outBegIdx,
        &final_outNbElement,
        final_stc.data()
    );
    
    if (final_retCode != TA_SUCCESS || final_outNbElement == 0) {
        result.setSeries("value", std::vector<Value>{});
        result.set("trend", Value::fromString("NEUTRAL"));
        result.set("signal", Value::fromString("NONE"));
        return result;
    }
    
    // 提取有效数据（从final_outBegIdx开始）
    std::vector<Value> stc_series;
    stc_series.reserve(final_outNbElement);
    for (int i = 0; i < final_outNbElement; ++i) {
        int idx = final_outBegIdx + i;
        stc_series.push_back(Value::fromNumber(final_stc[idx]));
    }
    result.setSeries("value", stc_series);
    
    double stc = stc_series.back().toNumber();
    
    // Determine trend and signal
    std::string trend = "NEUTRAL";
    std::string signal = "NONE";
    
    if (stc > 75.0) {
        trend = "BULLISH";
        signal = "OVERBOUGHT";
    } else if (stc < 25.0) {
        trend = "BEARISH";
        signal = "OVERSOLD";
    } else if (stc > 50.0) {
        trend = "BULLISH";
    } else if (stc < 50.0) {
        trend = "BEARISH";
    }
    
    // Check for crossovers
    if (final_outNbElement >= 2) {
        double prev_stc = stc_series[final_outNbElement - 2].toNumber();
        if (prev_stc < 25.0 && stc > 25.0) {
            signal = "BUY";
        } else if (prev_stc > 75.0 && stc < 75.0) {
            signal = "SELL";
        }
    }
    
    result.set("trend", Value::fromString(trend));
    result.set("signal", Value::fromString(signal));
    
    return result;
}

} // namespace indicators
} // namespace prophet

