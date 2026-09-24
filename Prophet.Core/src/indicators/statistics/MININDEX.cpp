/*
 * ============================================================================
 * 文件名：MININDEX.cpp
 * 指标名：MININDEX
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

IndicatorResult Calculator::MININDEX(
    const std::vector<double>& close,
    const IndicatorParams& params
) {
    int period = params.get_int("MININDEX_PERIOD", 30);
    
    if (close.size() < static_cast<size_t>(period)) {
        throw std::invalid_argument("MININDEX: 数据不足");
    }
    
    std::vector<int> out(close.size());
    int outBegIdx = 0, outNbElement = 0;
    
    TA_RetCode ret = TA_MININDEX(0, static_cast<int>(close.size()) - 1, close.data(),
                                  period, &outBegIdx, &outNbElement, out.data());
    
    if (ret != TA_SUCCESS || outNbElement == 0) {
        throw std::runtime_error("MININDEX: TA-Lib计算失败");
    }
    
    IndicatorResult result;
    int index = out[outNbElement - 1];  // 修复：正确的索引
    result.set("index", Value::fromNumber(static_cast<double>(index)));
    
    // 距离当前K线的位置
    int bars_ago = period - 1 - index;
    result.set("bars_ago", Value::fromNumber(static_cast<double>(bars_ago)));
    
    return result;
}

} // namespace prophet::indicators
