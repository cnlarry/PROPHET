/*
 * ============================================================================
 * 文件名：STOCHF.cpp
 * 指标名：STOCHF
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

IndicatorResult Calculator::STOCHF(
    const std::vector<double>& high,
    const std::vector<double>& low,
    const std::vector<double>& close,
    int STOCHF_FASTK_PERIOD,
    int STOCHF_FASTD_PERIOD
) {
    IndicatorResult result;

    if (high.empty() || low.empty() || close.empty()) {
        result.setSeries("k", std::vector<Value>{});
        result.setSeries("d", std::vector<Value>{});
        return result;
    }

    #ifdef USE_TALIB
    int size = static_cast<int>(high.size());
    std::vector<double> outFastK(size);
    std::vector<double> outFastD(size);
    int outBegIdx = 0;
    int outNBElement = 0;

    TA_RetCode retCode = TA_STOCHF(
        0,                          // startIdx
        size - 1,                   // endIdx
        high.data(),                // high
        low.data(),                 // low
        close.data(),               // close
        STOCHF_FASTK_PERIOD,        // fastK period
        STOCHF_FASTD_PERIOD,        // fastD period
        TA_MAType_SMA,              // fastD MA type
        &outBegIdx,
        &outNBElement,
        outFastK.data(),
        outFastD.data()
    );

    if (retCode == TA_SUCCESS && outNBElement > 0) {
        // 提取有效数据（TA-Lib将有效数据存储在数组开头）
        std::vector<Value> k_series, d_series;
        k_series.reserve(outNBElement);
        d_series.reserve(outNBElement);
        
        for (int i = 0; i < outNBElement; ++i) {
            k_series.push_back(Value::fromNumber(outFastK[i]));
            d_series.push_back(Value::fromNumber(outFastD[i]));
        }
        
        result.setSeries("k", k_series);
        result.setSeries("d", d_series);
    } else {
        result.setSeries("k", std::vector<Value>{});
        result.setSeries("d", std::vector<Value>{});
    }
    #else
    // 简化实现：使用 STOCH 的快速版本
    // Fast Stochastic 不对 %K 进行平滑
    if (size_t(STOCHF_FASTK_PERIOD) > close.size()) {
        result.setSeries("k", std::vector<Value>{});
        result.setSeries("d", std::vector<Value>{});
        return result;
    }

    // 计算最近N个周期的最高价和最低价
    int start = static_cast<int>(close.size()) - STOCHF_FASTK_PERIOD;
    double highest = *std::max_element(high.begin() + start, high.end());
    double lowest = *std::min_element(low.begin() + start, low.end());

    // 计算 %K（只计算最新值，简化实现）
    double k = 0.0;
    if (highest > lowest) {
        k = ((close.back() - lowest) / (highest - lowest)) * 100.0;
    }

    // 计算 %D (对 %K 的简单移动平均)
    double d = k;  // 简化实现：%D = %K

    // 简化实现：只保存最新值
    result.setSeries("k", std::vector<Value>{Value::fromNumber(k)});
    result.setSeries("d", std::vector<Value>{Value::fromNumber(d)});
    #endif

    return result;
}

} // namespace prophet::indicators

