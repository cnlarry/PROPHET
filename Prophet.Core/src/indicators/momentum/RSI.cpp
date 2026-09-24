/*
 * ============================================================================
 * 文件名：RSI.cpp
 * 指标名：RSI
 * 类别：动量指标
 * 
 * 从 calculator.cpp 拆分
 * 拆分日期：2025-10-30
 * ============================================================================
 */

#include "prophet/indicators/calculator.hpp"
#include "prophet/indicators/registry.hpp"
#include "prophet/simd/simd_indicators.hpp"  // P3优化：SIMD指标
#include "prophet/simd/simd_config.hpp"      // P3优化：SIMD配置
#include <ta_libc.h>
#include <cmath>
#include <algorithm>
#include <numeric>
#include <stdexcept>

namespace prophet::indicators {

IndicatorResult Calculator::RSI(
    const std::vector<double>& close,
    int RSI_PERIOD,
    double RSI_OVERBOUGHT_THRESHOLD,
    double RSI_OVERSOLD_THRESHOLD
) {
    IndicatorResult result;

    if (close.size() < size_t(RSI_PERIOD + 1)) {
        result.setSeries("value", std::vector<Value>{});
        result.set("overbought", Value::fromBoolean(false));
        result.set("oversold", Value::fromBoolean(false));
        result.set("trend", Value::fromString("NEUTRAL"));
        result.set("momentum", Value::fromNumber(0.0));
        return result;
    }

    std::vector<double> rsi_values(close.size(), 50.0);
    
    // P3优化：如果SIMD可用且数据量足够，使用SIMD加速（2-3倍）
    if (simd::Config::instance().isEnabled() && 
        simd::Config::instance().features().has_avx2 &&
        close.size() >= 100) {  // 数据量阈值：100+
        
        try {
            // 使用SIMD计算RSI（向量化涨跌计算）
            auto simd_result = simd::calculate_RSI(
                close.data(), close.size(), RSI_PERIOD
            );
            
            // 🔑 关键修复：SIMD返回的数组可能包含前面的NaN，需要提取有效数据
            // 计算outBegIdx（与TA-Lib保持一致）
            int outBegIdx = RSI_PERIOD;  // RSI需要period+1根K线，所以outBegIdx = period
            int outNbElement = static_cast<int>(close.size()) - outBegIdx;
            
            if (outNbElement <= 0) {
                result.setSeries("value", std::vector<Value>{});
                result.set("overbought", Value::fromBoolean(false));
                result.set("oversold", Value::fromBoolean(false));
                result.set("trend", Value::fromString("NEUTRAL"));
                result.set("momentum", Value::fromNumber(0.0));
                return result;
            }
            
            // 提取有效数据（从outBegIdx开始，只包含有效值）
            std::vector<Value> rsi_series;
            rsi_series.reserve(outNbElement);
            for (int i = 0; i < outNbElement; ++i) {
                int idx = outBegIdx + i;
                if (idx < static_cast<int>(simd_result.rsi.size())) {
                    rsi_series.push_back(Value::fromNumber(simd_result.rsi[idx]));
                }
            }
            
            if (rsi_series.empty()) {
                result.setSeries("value", std::vector<Value>{});
                result.set("overbought", Value::fromBoolean(false));
                result.set("oversold", Value::fromBoolean(false));
                result.set("trend", Value::fromString("NEUTRAL"));
                result.set("momentum", Value::fromNumber(0.0));
                return result;
            }
            
            result.setSeries("value", rsi_series);
            
            // 业务逻辑（基于有效数据范围计算）
            std::vector<double> rsi_valid(outNbElement);
            for (int i = 0; i < outNbElement; ++i) {
                rsi_valid[i] = rsi_series[i].toNumber();
            }
            
            double curr_rsi = rsi_series.back().toNumber();
            bool overbought = curr_rsi > RSI_OVERBOUGHT_THRESHOLD;
            bool oversold = curr_rsi < RSI_OVERSOLD_THRESHOLD;
            std::string trend = determineTrend(rsi_valid);
            double momentum = calculateMomentum(rsi_valid);
            
            // 填充派生字段（只保存最新值）
            result.set("overbought", Value::fromBoolean(overbought));
            result.set("oversold", Value::fromBoolean(oversold));
            result.set("trend", Value::fromString(trend));
            result.set("momentum", Value::fromNumber(momentum));
            
            return result;
        } catch (...) {
            // SIMD计算失败，回退到TA-Lib
        }
    }
    
    // 使用 TA-Lib 计算（回退路径）
    int outBegIdx, outNbElement;
    TA_RetCode retCode = TA_RSI(
        0, static_cast<int>(close.size()) - 1,
        close.data(),
        RSI_PERIOD,
        &outBegIdx, &outNbElement,
        rsi_values.data()  // 修复：从数组开头写入，不加outBegIdx
    );

    if (retCode != TA_SUCCESS || outNbElement == 0) {
        result.setSeries("value", std::vector<Value>{});
        result.set("overbought", Value::fromBoolean(false));
        result.set("oversold", Value::fromBoolean(false));
        result.set("trend", Value::fromString("NEUTRAL"));
        result.set("momentum", Value::fromNumber(0.0));
        return result;
    }
    
    // 提取有效数据（TA-Lib将有效数据存储在数组开头）
    std::vector<Value> rsi_series;
    rsi_series.reserve(outNbElement);
    for (int i = 0; i < outNbElement; ++i) {
        rsi_series.push_back(Value::fromNumber(rsi_values[i]));
    }
    result.setSeries("value", rsi_series);
    
    // 判断超买超卖 - 使用参数阈值而非硬编码
    double curr_rsi = rsi_series.back().toNumber();
    bool overbought = curr_rsi > RSI_OVERBOUGHT_THRESHOLD;
    bool oversold = curr_rsi < RSI_OVERSOLD_THRESHOLD;

    // 判断趋势（基于有效数据范围）
    std::vector<double> rsi_valid(outNbElement);
    for (int i = 0; i < outNbElement; ++i) {
        rsi_valid[i] = rsi_series[i].toNumber();
    }
    std::string trend = determineTrend(rsi_valid);

    // 计算动量
    double momentum = calculateMomentum(rsi_valid);

    // 填充派生字段（只保存最新值）
    result.set("overbought", Value::fromBoolean(overbought));
    result.set("oversold", Value::fromBoolean(oversold));
    result.set("trend", Value::fromString(trend));
    result.set("momentum", Value::fromNumber(momentum));

    return result;
}

} // namespace prophet::indicators
