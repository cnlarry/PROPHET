/*
 * ============================================================================
 * 文件名：BollingerBW.cpp
 * 指标名：Bollinger Bandwidth（布林带宽度）
 * 类别：波动率指标
 * 
 * 创建日期：2025-10-31
 * ============================================================================
 */

#include "prophet/indicators/calculator.hpp"
#include "prophet/indicators/registry.hpp"
#include <ta_libc.h>
#include <cmath>

namespace prophet::indicators {

IndicatorResult Calculator::BollingerBW(
    const std::vector<double>& close,
    int BBWIDTH_PERIOD,
    double BBWIDTH_STD_DEV
) {
    IndicatorResult result;
    
    if (close.size() < static_cast<size_t>(BBWIDTH_PERIOD)) {
        result.setSeries("bandwidth", std::vector<Value>{});
        result.setSeries("percent", std::vector<Value>{});
        result.set("squeeze", Value::fromBoolean(false));
        return result;
    }
    
    // 计算布林带
    std::vector<double> upper(close.size());
    std::vector<double> middle(close.size());
    std::vector<double> lower(close.size());
    int outBegIdx, outNbElement;
    
    TA_RetCode retCode = TA_BBANDS(
        0, static_cast<int>(close.size()) - 1,
        close.data(),
        BBWIDTH_PERIOD,
        BBWIDTH_STD_DEV,
        BBWIDTH_STD_DEV,
        TA_MAType_SMA,
        &outBegIdx, &outNbElement,
        upper.data(),
        middle.data(),
        lower.data()
    );
    
    if (retCode != TA_SUCCESS || outNbElement == 0) {
        result.setSeries("bandwidth", std::vector<Value>{});
        result.setSeries("percent", std::vector<Value>{});
        result.set("squeeze", Value::fromBoolean(false));
        return result;
    }
    
    // 提取有效数据并计算带宽序列
    std::vector<Value> bandwidth_series, percent_series;
    bandwidth_series.reserve(outNbElement);
    percent_series.reserve(outNbElement);
    
    for (int i = 0; i < outNbElement; ++i) {
        double curr_upper = upper[i];
        double curr_middle = middle[i];
        double curr_lower = lower[i];
        
        // 计算带宽
        double bandwidth = curr_upper - curr_lower;
        
        // 计算带宽百分比
        double bandwidth_pct = (curr_middle > 0.0) ? 
                              (bandwidth / curr_middle) * 100.0 : 0.0;
        
        bandwidth_series.push_back(Value::fromNumber(bandwidth));
        percent_series.push_back(Value::fromNumber(bandwidth_pct));
    }
    
    result.setSeries("bandwidth", bandwidth_series);
    result.setSeries("percent", percent_series);
    
    // 判断是否处于"挤压"状态（基于最新值）
    double bandwidth_pct = percent_series.back().toNumber();
    bool is_squeeze = (bandwidth_pct < 2.0);
    
    result.set("squeeze", Value::fromBoolean(is_squeeze));
    
    return result;
}

} // namespace prophet::indicators

