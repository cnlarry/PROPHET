/*
 * ============================================================================
 * 文件名：CMF.cpp
 * 指标名：CMF (Chaikin Money Flow)
 * 类别：成交量指标
 * 
 * 创建日期：2025-10-31
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

IndicatorResult Calculator::CMF(
    const std::vector<double>& high,
    const std::vector<double>& low,
    const std::vector<double>& close,
    const std::vector<double>& volume,
    int CMF_PERIOD
) {
    IndicatorResult result;

    if (close.size() < static_cast<size_t>(CMF_PERIOD) ||
        high.size() != close.size() ||
        low.size() != close.size() ||
        volume.size() != close.size()) {
        result.setSeries("value", std::vector<Value>{});
        result.set("is_positive", Value::fromBoolean(false));
        result.set("is_strong", Value::fromBoolean(false));
        return result;
    }

    // CMF = Sum(Money Flow Volume over n periods) / Sum(Volume over n periods)
    // Money Flow Multiplier = [(Close - Low) - (High - Close)] / (High - Low)
    // Money Flow Volume = Money Flow Multiplier × Volume
    
    std::vector<double> mf_volume(close.size(), 0.0);
    
    for (size_t i = 0; i < close.size(); ++i) {
        double hl_diff = high[i] - low[i];
        if (hl_diff > 1e-10) {
            double mf_multiplier = ((close[i] - low[i]) - (high[i] - close[i])) / hl_diff;
            mf_volume[i] = mf_multiplier * volume[i];
        }
    }

    // 计算CMF序列（滑动窗口）
    std::vector<Value> cmf_series;
    int valid_count = static_cast<int>(close.size()) - CMF_PERIOD + 1;
    cmf_series.reserve(valid_count);
    
    for (int i = 0; i < valid_count; ++i) {
        double sum_mf_volume = 0.0;
        double sum_volume = 0.0;
        
        for (int j = 0; j < CMF_PERIOD; ++j) {
            int idx = i + j;
            sum_mf_volume += mf_volume[idx];
            sum_volume += volume[idx];
        }
        
        double cmf_value = 0.0;
        if (sum_volume > 1e-10) {
            cmf_value = sum_mf_volume / sum_volume;
        }
        cmf_series.push_back(Value::fromNumber(cmf_value));
    }
    
    if (cmf_series.empty()) {
        result.setSeries("value", std::vector<Value>{});
        result.set("is_positive", Value::fromBoolean(false));
        result.set("is_strong", Value::fromBoolean(false));
        return result;
    }
    
    result.setSeries("value", cmf_series);

    // 判断最新值
    double cmf_value = cmf_series.back().toNumber();
    bool is_positive = cmf_value > 0.0;
    bool is_strong = std::abs(cmf_value) > 0.25;

    result.set("is_positive", Value::fromBoolean(is_positive));
    result.set("is_strong", Value::fromBoolean(is_strong));

    return result;
}

} // namespace prophet::indicators

