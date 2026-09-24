/*
 * ============================================================================
 * 文件名：MACDEXT.cpp
 * 指标名：MACDEXT
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

IndicatorResult Calculator::MACDEXT(
    const std::vector<double>& close,
    const IndicatorParams& params
) {
    int fastperiod = params.get_int("MACDEXT_FAST_PERIOD", 12);
    int fastmatype = params.get_int("MACDEXT_FAST_MATYPE", 0); // 0=SMA
    int slowperiod = params.get_int("MACDEXT_SLOW_PERIOD", 26);
    int slowmatype = params.get_int("MACDEXT_SLOW_MATYPE", 0);
    int signalperiod = params.get_int("MACDEXT_SIGNAL_PERIOD", 9);
    int signalmatype = params.get_int("MACDEXT_SIGNAL_MATYPE", 0);
    
    if (close.size() < static_cast<size_t>(slowperiod + signalperiod)) {
        throw std::invalid_argument("MACDEXT: 数据不足");
    }
    
    std::vector<double> macd_out(close.size());
    std::vector<double> signal_out(close.size());
    std::vector<double> hist_out(close.size());
    int outBegIdx = 0, outNbElement = 0;
    
    TA_RetCode ret = TA_MACDEXT(0, static_cast<int>(close.size()) - 1, close.data(),
                                 fastperiod, static_cast<TA_MAType>(fastmatype),
                                 slowperiod, static_cast<TA_MAType>(slowmatype),
                                 signalperiod, static_cast<TA_MAType>(signalmatype),
                                 &outBegIdx, &outNbElement,
                                 macd_out.data(), signal_out.data(), hist_out.data());
    
    if (ret != TA_SUCCESS || outNbElement == 0) {
        throw std::runtime_error("MACDEXT: TA-Lib计算失败");
    }
    
    IndicatorResult result;
    double macd = macd_out[outNbElement - 1];  // 修复：正确的索引
    double signal = signal_out[outNbElement - 1];  // 修复：正确的索引
    double histogram = hist_out[outNbElement - 1];  // 修复：正确的索引
    
    result.set("macd", Value::fromNumber(macd));
    result.set("signal", Value::fromNumber(signal));
    result.set("histogram", Value::fromNumber(histogram));
    
    // 交叉检测
    if (outNbElement >= 2) {
        std::vector<double> macd_vec(macd_out.end() - 2, macd_out.end());
        std::vector<double> signal_vec(signal_out.end() - 2, signal_out.end());
        result.set("crossover_type", Value::fromString(detectCrossover(macd_vec, signal_vec)));
    }
    
    // 趋势判断
    std::string trend = (histogram > 0) ? "BULLISH" : (histogram < 0) ? "BEARISH" : "NEUTRAL";
    result.set("trend", Value::fromString(trend));
    
    return result;
}

} // namespace prophet::indicators
