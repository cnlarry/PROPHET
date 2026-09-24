/*
 * ============================================================================
 * 文件名：MINMAXINDEX.cpp
 * 指标名：MINMAXINDEX
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

IndicatorResult Calculator::MINMAXINDEX(
    const std::vector<double>& close,
    const IndicatorParams& params
) {
    int period = params.get_int("MINMAXINDEX_PERIOD", 30);
    
    if (close.size() < static_cast<size_t>(period)) {
        throw std::invalid_argument("MINMAXINDEX: 数据不足");
    }
    
    std::vector<int> min_idx_out(close.size());
    std::vector<int> max_idx_out(close.size());
    int outBegIdx = 0, outNbElement = 0;
    
    TA_RetCode ret = TA_MINMAXINDEX(0, static_cast<int>(close.size()) - 1, close.data(),
                                     period, &outBegIdx, &outNbElement,
                                     min_idx_out.data(), max_idx_out.data());
    
    if (ret != TA_SUCCESS || outNbElement == 0) {
        throw std::runtime_error("MINMAXINDEX: TA-Lib计算失败");
    }
    
    IndicatorResult result;
    int min_index = min_idx_out[outNbElement - 1];  // 修复：正确的索引
    int max_index = max_idx_out[outNbElement - 1];  // 修复：正确的索引
    
    result.set("min_index", Value::fromNumber(static_cast<double>(min_index)));
    result.set("max_index", Value::fromNumber(static_cast<double>(max_index)));
    
    // 距离当前K线的位置
    int min_bars_ago = period - 1 - min_index;
    int max_bars_ago = period - 1 - max_index;
    result.set("min_bars_ago", Value::fromNumber(static_cast<double>(min_bars_ago)));
    result.set("max_bars_ago", Value::fromNumber(static_cast<double>(max_bars_ago)));
    
    return result;
}

} // namespace prophet::indicators
