/*
 * ============================================================================
 * 文件名：MINUS_DM.cpp
 * 指标名：MINUS_DM
 * 类别：趋势指标
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

IndicatorResult Calculator::MINUS_DM(
    const std::vector<double>& high,
    const std::vector<double>& low,
    const IndicatorParams& params
) {
    int period = params.get_int("MINUS_DM_PERIOD", 14);
    
    if (high.size() < static_cast<size_t>(period + 1)) {
        throw std::invalid_argument("MINUS_DM: 数据不足");
    }
    
    std::vector<double> out(high.size());
    int outBegIdx = 0, outNbElement = 0;
    
    TA_RetCode ret = TA_MINUS_DM(0, static_cast<int>(high.size()) - 1,
                                  high.data(), low.data(),
                                  period, &outBegIdx, &outNbElement, out.data());
    
    if (ret != TA_SUCCESS || outNbElement == 0) {
        throw std::runtime_error("MINUS_DM: TA-Lib计算失败");
    }
    
    IndicatorResult result;
    double value = out[outNbElement - 1];  // 修复：正确的索引
    result.set("value", Value::fromNumber(value));
    
    // 趋势判断
    std::vector<double> last_values(out.end() - std::min(3, outNbElement), out.end());
    result.set("trend", Value::fromString(determineTrend(last_values)));
    
    return result;
}

} // namespace prophet::indicators
