/*
 * ============================================================================
 * 文件名：ADL.cpp
 * 指标名：Accumulation/Distribution Line（累积派发线）
 * 类别：成交量指标
 * 
 * 创建日期：2025-10-31
 * ============================================================================
 */

#include "prophet/indicators/calculator.hpp"
#include "prophet/indicators/registry.hpp"
#include <ta_libc.h>
#include <cmath>

namespace prophet::indicators {

IndicatorResult Calculator::ADL(
    const std::vector<double>& high,
    const std::vector<double>& low,
    const std::vector<double>& close,
    const std::vector<double>& volume
) {
    IndicatorResult result;
    
    if (close.empty() || volume.size() != close.size()) {
        result.setSeries("value", std::vector<Value>{});
        result.set("trend", Value::fromString("NEUTRAL"));
        return result;
    }
    
    // 使用TA-Lib计算AD线
    std::vector<double> ad_values(close.size());
    int outBegIdx, outNbElement;
    
    TA_RetCode retCode = TA_AD(
        0, static_cast<int>(close.size()) - 1,
        high.data(),
        low.data(),
        close.data(),
        volume.data(),
        &outBegIdx, &outNbElement,
        ad_values.data()
    );
    
    if (retCode != TA_SUCCESS || outNbElement == 0) {
        result.setSeries("value", std::vector<Value>{});
        result.set("trend", Value::fromString("NEUTRAL"));
        return result;
    }
    
    // 提取有效数据（TA-Lib将有效数据存储在数组开头）
    std::vector<Value> ad_series;
    ad_series.reserve(outNbElement);
    for (int i = 0; i < outNbElement; ++i) {
        ad_series.push_back(Value::fromNumber(ad_values[i]));
    }
    result.setSeries("value", ad_series);
    
    // 判断趋势（基于最新值）
    double curr_ad = ad_series.back().toNumber();
    std::string trend = "NEUTRAL";
    if (outNbElement >= 2) {
        double prev_ad = ad_series[outNbElement - 2].toNumber();
        if (curr_ad > prev_ad) {
            trend = "BULLISH";    // 累积
        } else if (curr_ad < prev_ad) {
            trend = "BEARISH";    // 派发
        }
    }
    
    result.set("trend", Value::fromString(trend));
    
    return result;
}

} // namespace prophet::indicators

