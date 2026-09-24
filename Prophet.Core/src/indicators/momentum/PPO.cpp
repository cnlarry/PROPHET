/*
 * ============================================================================
 * 文件名：PPO.cpp
 * 指标名：PPO
 * 类别：动量指标
 * 
 * 从 calculator.cpp 拆分
 * 拆分日期：2025-10-30
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

IndicatorResult Calculator::PPO(
    const std::vector<double>& close,
    int FAST_PERIOD,
    int SLOW_PERIOD,
    int SIGNAL_PERIOD
) {
    IndicatorResult result;

    if (close.size() < size_t(SLOW_PERIOD + SIGNAL_PERIOD)) {
        result.setSeries("ppo", std::vector<Value>{});
        result.setSeries("signal", std::vector<Value>{});
        result.setSeries("histogram", std::vector<Value>{});
        result.set("crossover_type", Value::fromString("NONE"));
        result.set("trend", Value::fromString("NEUTRAL"));
        return result;
    }

    std::vector<double> ppo_values(close.size(), 0.0);
    int outBegIdx, outNbElement;
    TA_RetCode retCode = TA_PPO(
        0, static_cast<int>(close.size()) - 1,
        close.data(),
        FAST_PERIOD,
        SLOW_PERIOD,
        TA_MAType_EMA,
        &outBegIdx, &outNbElement,
        ppo_values.data()  // 修复：使用正确的指针
    );

    if (retCode != TA_SUCCESS || outNbElement == 0) {
        result.setSeries("ppo", std::vector<Value>{});
        result.setSeries("signal", std::vector<Value>{});
        result.setSeries("histogram", std::vector<Value>{});
        result.set("crossover_type", Value::fromString("NONE"));
        result.set("trend", Value::fromString("NEUTRAL"));
        return result;
    }

    // 提取PPO有效数据（从outBegIdx开始）
    std::vector<double> ppo_valid(outNbElement);
    for (int i = 0; i < outNbElement; ++i) {
        ppo_valid[i] = ppo_values[outBegIdx + i];
    }

    // 计算信号线（PPO的EMA）
    std::vector<double> signal_values(outNbElement, 0.0);
    int sig_outBegIdx, sig_outNbElement;
    TA_EMA(
        0, outNbElement - 1,
        ppo_valid.data(),
        SIGNAL_PERIOD,
        &sig_outBegIdx, &sig_outNbElement,
        signal_values.data()
    );

    if (sig_outNbElement == 0) {
        result.setSeries("ppo", std::vector<Value>{});
        result.setSeries("signal", std::vector<Value>{});
        result.setSeries("histogram", std::vector<Value>{});
        result.set("crossover_type", Value::fromString("NONE"));
        result.set("trend", Value::fromString("NEUTRAL"));
        return result;
    }

    // 对齐数据：PPO和signal的有效数据范围可能不同
    // 取两者重叠的部分
    int start_idx = std::max(sig_outBegIdx, 0);
    int end_idx = std::min(sig_outNbElement, outNbElement);
    int valid_count = end_idx - start_idx;

    std::vector<Value> ppo_series, signal_series, histogram_series;
    ppo_series.reserve(valid_count);
    signal_series.reserve(valid_count);
    histogram_series.reserve(valid_count);

    for (int i = start_idx; i < end_idx; ++i) {
        int ppo_idx = outBegIdx + i;
        int sig_idx = i;
        double ppo_val = ppo_values[ppo_idx];
        double sig_val = signal_values[sig_idx];
        double hist_val = ppo_val - sig_val;
        
        ppo_series.push_back(Value::fromNumber(ppo_val));
        signal_series.push_back(Value::fromNumber(sig_val));
        histogram_series.push_back(Value::fromNumber(hist_val));
    }

    result.setSeries("ppo", ppo_series);
    result.setSeries("signal", signal_series);
    result.setSeries("histogram", histogram_series);

    // 检测交叉（基于有效数据范围）
    std::vector<double> ppo_for_cross(valid_count), sig_for_cross(valid_count);
    for (int i = 0; i < valid_count; ++i) {
        ppo_for_cross[i] = ppo_series[i].toNumber();
        sig_for_cross[i] = signal_series[i].toNumber();
    }
    std::string crossover = detectCrossover(ppo_for_cross, sig_for_cross);

    // 判断趋势（基于PPO序列）
    std::string trend = determineTrend(ppo_for_cross);

    result.set("crossover_type", Value::fromString(crossover));
    result.set("trend", Value::fromString(trend));

    return result;
}

} // namespace prophet::indicators
