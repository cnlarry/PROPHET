/*
 * ============================================================================
 * 文件名：KDJ.cpp
 * 指标名：KDJ (KDJ Stochastic)
 * 类别：动量指标
 * 
 * 创建日期：2025-10-31
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

IndicatorResult Calculator::KDJ(
    const std::vector<double>& high,
    const std::vector<double>& low,
    const std::vector<double>& close,
    int KDJ_N_PERIOD,
    int KDJ_M1_PERIOD,
    int KDJ_M2_PERIOD
) {
    IndicatorResult result;

    if (close.size() < static_cast<size_t>(KDJ_N_PERIOD) ||
        high.size() != close.size() ||
        low.size() != close.size()) {
        result.setSeries("k", std::vector<Value>{});
        result.setSeries("d", std::vector<Value>{});
        result.setSeries("j", std::vector<Value>{});
        result.set("overbought", Value::fromBoolean(false));
        result.set("oversold", Value::fromBoolean(false));
        return result;
    }

    // 使用TA-Lib的STOCH计算，但用KDJ的参数映射
    // KDJ中：K = STOCH中的slowk, D = STOCH中的slowd
    std::vector<double> k_values(close.size(), 50.0);
    std::vector<double> d_values(close.size(), 50.0);
    int outBegIdx, outNbElement;
    
    TA_RetCode retCode = TA_STOCH(
        0, static_cast<int>(close.size()) - 1,
        high.data(), low.data(), close.data(),
        KDJ_N_PERIOD,      // fastK period
        KDJ_M1_PERIOD,     // slowK period (K)
        TA_MAType_SMA,     // slowK MA type
        KDJ_M2_PERIOD,     // slowD period (D)
        TA_MAType_SMA,     // slowD MA type
        &outBegIdx, &outNbElement,
        k_values.data(),
        d_values.data()
    );

    if (retCode != TA_SUCCESS || outNbElement == 0) {
        result.setSeries("k", std::vector<Value>{});
        result.setSeries("d", std::vector<Value>{});
        result.setSeries("j", std::vector<Value>{});
        result.set("overbought", Value::fromBoolean(false));
        result.set("oversold", Value::fromBoolean(false));
        return result;
    }

    // 提取有效数据（TA-Lib将有效数据存储在数组开头）
    std::vector<Value> k_series, d_series, j_series;
    k_series.reserve(outNbElement);
    d_series.reserve(outNbElement);
    j_series.reserve(outNbElement);
    
    for (int i = 0; i < outNbElement; ++i) {
        double k_val = k_values[i];
        double d_val = d_values[i];
        double j_val = 3.0 * k_val - 2.0 * d_val;  // J = 3*K - 2*D
        
        k_series.push_back(Value::fromNumber(k_val));
        d_series.push_back(Value::fromNumber(d_val));
        j_series.push_back(Value::fromNumber(j_val));
    }
    
    result.setSeries("k", k_series);
    result.setSeries("d", d_series);
    result.setSeries("j", j_series);

    // 判断超买超卖（基于最新值）
    double k = k_series.back().toNumber();
    double d = d_series.back().toNumber();
    bool overbought = (k > 80.0 && d > 80.0);
    bool oversold = (k < 20.0 && d < 20.0);

    result.set("overbought", Value::fromBoolean(overbought));
    result.set("oversold", Value::fromBoolean(oversold));

    return result;
}

} // namespace prophet::indicators

