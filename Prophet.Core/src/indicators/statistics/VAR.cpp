/*
 * ============================================================================
 * 文件名：VAR.cpp
 * 指标名：VAR
 * 类别：统计函数
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

IndicatorResult Calculator::VAR(
    const std::vector<double>& close,
    const IndicatorParams& params
) {
    int period = params.get_int("VAR_PERIOD", 5);
    double nbdev = params.get_double("VAR_NBDEV", 1.0);
    
    if (close.size() < static_cast<size_t>(period)) {
        throw std::invalid_argument("VAR: 数据不足");
    }
    
    std::vector<double> out(close.size());
    int outBegIdx = 0;
    int outNbElement = 0;
    
    TA_RetCode ret = TA_VAR(
        0, static_cast<int>(close.size()) - 1,
        close.data(),
        period, nbdev,
        &outBegIdx, &outNbElement,
        out.data()
    );
    
    if (ret != TA_SUCCESS || outNbElement == 0) {
        throw std::runtime_error("VAR: TA-Lib计算失败");
    }
    
    IndicatorResult result;
    double value = out[outNbElement - 1];  // 修复：正确的索引
    result.set("value", Value::fromNumber(value));
    
    // 波动性等级
    std::string volatility = "LOW";
    if (value > 10000) volatility = "VERY_HIGH";
    else if (value > 5000) volatility = "HIGH";
    else if (value > 1000) volatility = "MODERATE";
    result.set("volatility", Value::fromString(volatility));
    
    return result;
}

} // namespace prophet::indicators
