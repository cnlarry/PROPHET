/*
 * ============================================================================
 * 文件名：ROCR100.cpp
 * 指标名：ROCR100
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

IndicatorResult Calculator::ROCR100(
    const std::vector<double>& close,
    const IndicatorParams& params
) {
    int period = params.get_int("ROCR100_PERIOD", 10);
    
    if (close.size() < static_cast<size_t>(period + 1)) {
        throw std::invalid_argument("ROCR100: 数据不足");
    }
    
    std::vector<double> out(close.size());
    int outBegIdx = 0, outNbElement = 0;
    
    TA_RetCode ret = TA_ROCR100(0, static_cast<int>(close.size()) - 1, close.data(),
                                 period, &outBegIdx, &outNbElement, out.data());
    
    if (ret != TA_SUCCESS || outNbElement == 0) {
        throw std::runtime_error("ROCR100: TA-Lib计算失败");
    }
    
    IndicatorResult result;
    double value = out[outNbElement - 1];  // 修复：正确的索引
    result.set("value", Value::fromNumber(value));
    
    // 趋势判断（>100上涨，<100下跌）
    std::string trend = (value > 100.0) ? "BULLISH" : (value < 100.0) ? "BEARISH" : "NEUTRAL";
    result.set("trend", Value::fromString(trend));
    
    return result;
}

} // namespace prophet::indicators
