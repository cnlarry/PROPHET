/*
 * ============================================================================
 * 文件名：MAX.cpp
 * 指标名：MAX
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

IndicatorResult Calculator::MAX(
    const std::vector<double>& close,
    const IndicatorParams& params
) {
    int period = params.get_int("MAX_PERIOD", 30);
    
    if (close.size() < static_cast<size_t>(period)) {
        throw std::invalid_argument("MAX: 数据不足");
    }
    
    std::vector<double> out(close.size());
    int outBegIdx = 0, outNbElement = 0;
    
    TA_RetCode ret = TA_MAX(0, static_cast<int>(close.size()) - 1, close.data(),
                             period, &outBegIdx, &outNbElement, out.data());
    
    if (ret != TA_SUCCESS || outNbElement == 0) {
        throw std::runtime_error("MAX: TA-Lib计算失败");
    }
    
    IndicatorResult result;
    double value = out[outNbElement - 1];  // 修复：正确的索引
    result.set("value", Value::fromNumber(value));
    
    // 与当前价格的关系
    double current_price = close.back();
    double distance = ((current_price - value) / value) * 100.0;
    result.set("distance", Value::fromNumber(distance));
    
    // 是否创新高
    bool is_new_high = (std::abs(current_price - value) < 0.0001);
    result.set("is_new_high", Value::fromNumber(is_new_high ? 1.0 : 0.0));
    
    return result;
}

} // namespace prophet::indicators
