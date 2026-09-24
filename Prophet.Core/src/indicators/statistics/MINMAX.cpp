/*
 * ============================================================================
 * 文件名：MINMAX.cpp
 * 指标名：MINMAX
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

IndicatorResult Calculator::MINMAX(
    const std::vector<double>& close,
    const IndicatorParams& params
) {
    int period = params.get_int("MINMAX_PERIOD", 30);
    
    if (close.size() < static_cast<size_t>(period)) {
        throw std::invalid_argument("MINMAX: 数据不足");
    }
    
    std::vector<double> min_out(close.size());
    std::vector<double> max_out(close.size());
    int outBegIdx = 0, outNbElement = 0;
    
    TA_RetCode ret = TA_MINMAX(0, static_cast<int>(close.size()) - 1, close.data(),
                                period, &outBegIdx, &outNbElement,
                                min_out.data(), max_out.data());
    
    if (ret != TA_SUCCESS || outNbElement == 0) {
        throw std::runtime_error("MINMAX: TA-Lib计算失败");
    }
    
    IndicatorResult result;
    double min_value = min_out[outNbElement - 1];  // 修复：正确的索引
    double max_value = max_out[outNbElement - 1];  // 修复：正确的索引
    
    result.set("min", Value::fromNumber(min_value));
    result.set("max", Value::fromNumber(max_value));
    result.set("range", Value::fromNumber(max_value - min_value));
    
    // 当前价格在范围内的位置（0-1）；横盘时 max==min，除零会产生 inf/NaN 并污染后续比较
    double current_price = close.back();
    double range = max_value - min_value;
    double position = (range == 0.0) ? 0.5 : (current_price - min_value) / range;
    result.set("position", Value::fromNumber(position));
    
    return result;
}

} // namespace prophet::indicators
