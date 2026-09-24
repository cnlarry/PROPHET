/*
 * ============================================================================
 * 文件名：ATR.cpp
 * 指标名：ATR
 * 类别：波动率指标
 * 
 * 从 calculator.cpp 拆分
 * 拆分日期：2025-10-30
 * 优化：2025-12-03 添加SIMD支持
 * ============================================================================
 */

#include "prophet/indicators/calculator.hpp"
#include "prophet/indicators/registry.hpp"
#include "prophet/simd/simd_math.hpp"
#include "prophet/simd/simd_config.hpp"
#include <ta_libc.h>
#include <cmath>
#include <algorithm>
#include <numeric>
#include <stdexcept>

namespace prophet::indicators {

// 基于SIMD的真实范围计算函数
static void simd_calculate_tr(const std::vector<double>& high, 
                              const std::vector<double>& low,
                              const std::vector<double>& close, 
                              std::vector<double>& tr_values) {
    const size_t n = high.size();
    tr_values.resize(n, 0.0);
    
    if (n < 2) {
        return;
    }
    
    // 计算high - low
    std::vector<double> hl_diff(n, 0.0);
    prophet::simd::sub(high.data(), low.data(), hl_diff.data(), n);
    prophet::simd::abs(hl_diff.data(), hl_diff.data(), n);
    
    // 计算high - close_prev
    std::vector<double> hcp_diff(n, 0.0);
    prophet::simd::sub(high.data() + 1, close.data(), hcp_diff.data() + 1, n - 1);
    prophet::simd::abs(hcp_diff.data(), hcp_diff.data(), n);
    
    // 计算low - close_prev
    std::vector<double> lcp_diff(n, 0.0);
    prophet::simd::sub(low.data() + 1, close.data(), lcp_diff.data() + 1, n - 1);
    prophet::simd::abs(lcp_diff.data(), lcp_diff.data(), n);
    
    // 计算真实范围：max(hl_diff, hcp_diff, lcp_diff)
    for (size_t i = 1; i < n; ++i) {
        tr_values[i] = std::max({hl_diff[i], hcp_diff[i], lcp_diff[i]});
    }
    
    // 第一个元素使用hl_diff[0]
    tr_values[0] = hl_diff[0];
}

IndicatorResult Calculator::ATR(
    const std::vector<double>& high,
    const std::vector<double>& low,
    const std::vector<double>& close,
    int period
) {
    IndicatorResult result;

    if (high.size() < size_t(period + 1) ||
        low.size() < size_t(period + 1) ||
        close.size() < size_t(period + 1)) {
        result.setSeries("value", std::vector<Value>{});
        result.set("volatility", Value::fromString("LOW"));
        return result;
    }

    std::vector<double> atr_values(high.size(), 0.0);
    
    // 使用SIMD优化计算（如果可用）
    if (simd::Config::instance().isEnabled() && 
        simd::Config::instance().features().has_avx2 &&
        high.size() >= 100) {
        
        try {
            // 计算真实范围
            std::vector<double> tr_values;
            simd_calculate_tr(high, low, close, tr_values);
            
            // 使用SIMD滚动平均计算ATR
            std::vector<double> atr_ma(high.size(), 0.0);
            prophet::simd::rolling_mean(tr_values.data(), tr_values.size(), period, atr_ma.data());
            
            // 提取有效数据
            int outBegIdx = period - 1;
            int outNbElement = static_cast<int>(high.size()) - outBegIdx;
            
            std::vector<Value> atr_series;
            atr_series.reserve(outNbElement);
            for (int i = 0; i < outNbElement; ++i) {
                atr_series.push_back(Value::fromNumber(atr_ma[outBegIdx + i]));
            }
            result.setSeries("value", atr_series);
            
            // 判断波动率等级
            double curr_atr = atr_series.back().toNumber();
            std::string volatility = "MEDIUM";
            double avg_price = close.back();
            if (avg_price > 1e-10) {
                double atr_pct = (curr_atr / avg_price) * 100.0;
                if (atr_pct < 1.0) {
                    volatility = "LOW";
                } else if (atr_pct > 3.0) {
                    volatility = "HIGH";
                }
            }
            
            result.set("volatility", Value::fromString(volatility));
            
            return result;
        } catch (...) {
            // SIMD计算失败，回退到TA-Lib
        }
    }
    
    // 使用TA-Lib计算（回退路径）
    int outBegIdx, outNbElement;
    TA_RetCode retCode = TA_ATR(
        0, static_cast<int>(high.size()) - 1,
        high.data(), low.data(), close.data(),
        period,
        &outBegIdx, &outNbElement,
        atr_values.data()
    );

    if (retCode != TA_SUCCESS || outNbElement == 0) {
        result.setSeries("value", std::vector<Value>{});
        result.set("volatility", Value::fromString("LOW"));
        return result;
    }
    
    // 提取有效数据
    std::vector<Value> atr_series;
    atr_series.reserve(outNbElement);
    for (int i = 0; i < outNbElement; ++i) {
        atr_series.push_back(Value::fromNumber(atr_values[i]));
    }
    result.setSeries("value", atr_series);

    // 判断波动率等级（基于最新值）
    double curr_atr = atr_series.back().toNumber();
    std::string volatility = "MEDIUM";
    double avg_price = close.back();
    if (avg_price > 1e-10) {
        double atr_pct = (curr_atr / avg_price) * 100.0;
        if (atr_pct < 1.0) {
            volatility = "LOW";
        } else if (atr_pct > 3.0) {
            volatility = "HIGH";
        }
    }

    result.set("volatility", Value::fromString(volatility));

    return result;
}

} // namespace prophet::indicators
