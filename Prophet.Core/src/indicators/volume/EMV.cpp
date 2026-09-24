/*
 * ============================================================================
 * 文件名：EMV.cpp
 * 指标名：EMV (Ease of Movement)
 * 类别：成交量指标
 * 
 * 创建日期：2025-10-31
 * 说明：简易波动指标，衡量价格变动相对于成交量的难易程度
 * 公式：
 *   Distance Moved = ((High + Low) / 2) - ((Prior High + Prior Low) / 2)
 *   Box Ratio = (Volume / 100000000) / (High - Low)
 *   EMV(1-period) = Distance Moved / Box Ratio
 *   EMV(n-period) = SMA of EMV(1-period) over n periods
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

IndicatorResult Calculator::EMV(
    const std::vector<double>& high,
    const std::vector<double>& low,
    const std::vector<double>& volume,
    int EMV_PERIOD
) {
    IndicatorResult result;

    if (high.size() < 2 ||
        low.size() != high.size() ||
        volume.size() != high.size()) {
        result.setSeries("value", std::vector<Value>{});
        result.setSeries("signal", std::vector<Value>{});
        result.set("is_positive", Value::fromBoolean(false));
        return result;
    }

    // 计算单周期EMV值
    std::vector<double> emv_raw(high.size(), 0.0);
    
    for (size_t i = 1; i < high.size(); ++i) {
        // 1. Distance Moved (中点移动距离)
        double current_midpoint = (high[i] + low[i]) / 2.0;
        double prior_midpoint = (high[i-1] + low[i-1]) / 2.0;
        double distance_moved = current_midpoint - prior_midpoint;
        
        // 2. Box Ratio (成交量相对于波动幅度的比率)
        double high_low_diff = high[i] - low[i];
        if (high_low_diff < 1e-10) {
            high_low_diff = 1e-10;  // 避免除零
        }
        
        // 使用100000000作为标准化因子（可以根据市场调整）
        double box_ratio = (volume[i] / 100000000.0) / high_low_diff;
        
        // 3. EMV = Distance Moved / Box Ratio
        if (box_ratio > 1e-10) {
            emv_raw[i] = distance_moved / box_ratio;
        }
    }

    // 对EMV原始值进行移动平均
    if (emv_raw.size() < static_cast<size_t>(EMV_PERIOD)) {
        result.setSeries("value", std::vector<Value>{});
        result.setSeries("signal", std::vector<Value>{});
        result.set("is_positive", Value::fromBoolean(false));
        return result;
    }

    std::vector<double> emv_smoothed(emv_raw.size(), 0.0);
    int outBegIdx, outNbElement;
    
    TA_RetCode ret = TA_SMA(
        0, static_cast<int>(emv_raw.size()) - 1,
        emv_raw.data(),
        EMV_PERIOD,
        &outBegIdx, &outNbElement,
        emv_smoothed.data()
    );

    if (ret != TA_SUCCESS || outNbElement == 0) {
        result.setSeries("value", std::vector<Value>{});
        result.setSeries("signal", std::vector<Value>{});
        result.set("is_positive", Value::fromBoolean(false));
        return result;
    }

    // 提取有效数据并计算信号线序列
    std::vector<Value> emv_series, signal_series;
    emv_series.reserve(outNbElement);
    signal_series.reserve(outNbElement);
    
    for (int i = 0; i < outNbElement; ++i) {
        double emv_val = emv_smoothed[i];
        emv_series.push_back(Value::fromNumber(emv_val));
        
        // 计算信号线（EMV的移动平均）
        double signal = 0.0;
        if (i >= EMV_PERIOD - 1) {
            double sum = 0.0;
            for (int j = i - EMV_PERIOD + 1; j <= i; ++j) {
                sum += emv_smoothed[j];
            }
            signal = sum / EMV_PERIOD;
        } else {
            signal = emv_val;
        }
        signal_series.push_back(Value::fromNumber(signal));
    }
    
    if (emv_series.empty()) {
        result.setSeries("value", std::vector<Value>{});
        result.setSeries("signal", std::vector<Value>{});
        result.set("is_positive", Value::fromBoolean(false));
        return result;
    }
    
    result.setSeries("value", emv_series);
    result.setSeries("signal", signal_series);

    // 判断最新值
    double emv_value = emv_series.back().toNumber();
    bool is_positive = emv_value > 0.0;

    result.set("is_positive", Value::fromBoolean(is_positive));

    return result;
}

} // namespace prophet::indicators

