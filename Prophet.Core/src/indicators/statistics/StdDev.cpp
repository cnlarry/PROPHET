/*
 * ============================================================================
 * 文件名：StdDev.cpp
 * 指标名：Standard Deviation（标准差）
 * 类别：统计指标
 * 
 * 创建日期：2025-10-31
 * ============================================================================
 */

#include "prophet/indicators/calculator.hpp"
#include "prophet/indicators/registry.hpp"
#include <ta_libc.h>
#include <cmath>
#include <numeric>

namespace prophet::indicators {

IndicatorResult Calculator::StdDev(
    const std::vector<double>& close,
    int STDDEV_PERIOD,
    double STDDEV_NBDEV
) {
    IndicatorResult result;
    
    if (close.size() < static_cast<size_t>(STDDEV_PERIOD)) {
        result.setSeries("value", std::vector<Value>{});
        result.setSeries("percent", std::vector<Value>{});
        result.set("volatility", Value::fromString("LOW"));
        return result;
    }
    
    // 使用TA-Lib计算标准差
    std::vector<double> stddev_values(close.size());
    int outBegIdx, outNbElement;
    
    TA_RetCode retCode = TA_STDDEV(
        0, static_cast<int>(close.size()) - 1,
        close.data(),
        STDDEV_PERIOD,
        STDDEV_NBDEV,
        &outBegIdx, &outNbElement,
        stddev_values.data()
    );
    
    if (retCode != TA_SUCCESS || outNbElement == 0) {
        result.setSeries("value", std::vector<Value>{});
        result.setSeries("percent", std::vector<Value>{});
        result.set("volatility", Value::fromString("LOW"));
        return result;
    }
    
    // 提取有效数据并计算百分比序列
    std::vector<Value> stddev_series, percent_series;
    stddev_series.reserve(outNbElement);
    percent_series.reserve(outNbElement);
    
    for (int i = 0; i < outNbElement; ++i) {
        double stddev_val = stddev_values[i];
        stddev_series.push_back(Value::fromNumber(stddev_val));
        
        // 计算标准差占价格的百分比
        int candleIdx = outBegIdx + i;
        double price = close[candleIdx];
        double stddev_pct = (price > 1e-10) ? (stddev_val / price) * 100.0 : 0.0;
        percent_series.push_back(Value::fromNumber(stddev_pct));
    }
    
    result.setSeries("value", stddev_series);
    result.setSeries("percent", percent_series);
    
    // 判断波动率等级（基于最新值）
    double stddev_pct = percent_series.back().toNumber();
    std::string volatility;
    if (stddev_pct < 1.0) {
        volatility = "LOW";
    } else if (stddev_pct < 2.0) {
        volatility = "MEDIUM";
    } else if (stddev_pct < 3.0) {
        volatility = "HIGH";
    } else {
        volatility = "VERY_HIGH";
    }
    
    result.set("volatility", Value::fromString(volatility));
    
    return result;
}

} // namespace prophet::indicators

