/*
 * ============================================================================
 * 文件名：TRIX.cpp
 * 指标名：TRIX
 * 类别：价格变换
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

IndicatorResult Calculator::TRIX(
    const std::vector<double>& close,
    int period
) {
    IndicatorResult result;

    if (close.size() < size_t(period * 3)) {
        result.setSeries("value", std::vector<Value>{});
        result.setSeries("signal", std::vector<Value>{});
        result.set("crossover_type", Value::fromString("NONE"));
        result.set("zero_cross", Value::fromBoolean(false));
        result.set("trend", Value::fromString("NEUTRAL"));
        return result;
    }

    std::vector<double> trix_values(close.size(), 0.0);
    int outBegIdx = 0;
    int outNbElement = 0;
    TA_RetCode retCode = TA_TRIX(
        0, static_cast<int>(close.size()) - 1,
        close.data(),
        period,
        &outBegIdx, &outNbElement,
        trix_values.data()
    );

    if (retCode != TA_SUCCESS || outNbElement == 0) {
        result.setSeries("value", std::vector<Value>{});
        result.setSeries("signal", std::vector<Value>{});
        result.set("crossover_type", Value::fromString("NONE"));
        result.set("zero_cross", Value::fromBoolean(false));
        result.set("trend", Value::fromString("NEUTRAL"));
        return result;
    }
    
    // 提取TRIX有效数据（TA-Lib将有效数据存储在数组开头）
    std::vector<double> trix_valid(outNbElement);
    for (int i = 0; i < outNbElement; ++i) {
        trix_valid[i] = trix_values[i];
    }

    // 计算信号线（9周期EMA）- 使用 TA-Lib
    std::vector<double> signal_line(trix_valid.size(), 0.0);
    int signal_outBegIdx = 0;
    int signal_outNbElement = 0;
    TA_RetCode signal_retCode = TA_EMA(
        0, static_cast<int>(trix_valid.size()) - 1,
        trix_valid.data(),
        9,
        &signal_outBegIdx, &signal_outNbElement,
        signal_line.data()
    );

    if (signal_retCode != TA_SUCCESS || signal_outNbElement == 0) {
        result.setSeries("value", std::vector<Value>{});
        result.setSeries("signal", std::vector<Value>{});
        result.set("crossover_type", Value::fromString("NONE"));
        result.set("zero_cross", Value::fromBoolean(false));
        result.set("trend", Value::fromString("NEUTRAL"));
        return result;
    }
    
    // 对齐TRIX和信号线的有效数据范围
    int start_idx = std::max(outBegIdx, signal_outBegIdx);
    int end_idx = std::min(outNbElement, signal_outNbElement);
    int valid_count = end_idx - start_idx;
    
    if (valid_count <= 0) {
        result.setSeries("value", std::vector<Value>{});
        result.setSeries("signal", std::vector<Value>{});
        result.set("crossover_type", Value::fromString("NONE"));
        result.set("zero_cross", Value::fromBoolean(false));
        result.set("trend", Value::fromString("NEUTRAL"));
        return result;
    }
    
    // 提取对齐后的序列
    std::vector<Value> trix_series, signal_series;
    trix_series.reserve(valid_count);
    signal_series.reserve(valid_count);
    
    for (int i = 0; i < valid_count; ++i) {
        int trix_idx = start_idx - outBegIdx + i;
        int signal_idx = start_idx - signal_outBegIdx + i;
        
        if (trix_idx >= 0 && trix_idx < outNbElement &&
            signal_idx >= 0 && signal_idx < signal_outNbElement) {
            trix_series.push_back(Value::fromNumber(trix_valid[trix_idx]));
            signal_series.push_back(Value::fromNumber(signal_line[signal_idx]));
        }
    }
    
    if (trix_series.empty()) {
        result.setSeries("value", std::vector<Value>{});
        result.setSeries("signal", std::vector<Value>{});
        result.set("crossover_type", Value::fromString("NONE"));
        result.set("zero_cross", Value::fromBoolean(false));
        result.set("trend", Value::fromString("NEUTRAL"));
        return result;
    }
    
    result.setSeries("value", trix_series);
    result.setSeries("signal", signal_series);

    // 检测交叉和零轴穿越（基于有效数据范围）
    std::vector<double> trix_valid_for_detect(valid_count), signal_valid_for_detect(valid_count);
    for (int i = 0; i < valid_count; ++i) {
        trix_valid_for_detect[i] = trix_series[i].toNumber();
        signal_valid_for_detect[i] = signal_series[i].toNumber();
    }
    
    std::string crossover = detectCrossover(trix_valid_for_detect, signal_valid_for_detect);
    bool zero_cross = detectZeroCross(trix_valid_for_detect);
    std::string trend = determineTrend(trix_valid_for_detect);

    result.set("crossover_type", Value::fromString(crossover));
    result.set("zero_cross", Value::fromBoolean(zero_cross));
    result.set("trend", Value::fromString(trend));

    return result;
}

} // namespace prophet::indicators
