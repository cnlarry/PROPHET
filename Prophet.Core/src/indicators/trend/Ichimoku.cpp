/*
 * ============================================================================
 * 文件名：Ichimoku.cpp
 * 指标名：Ichimoku (Ichimoku Cloud / 一目均衡表)
 * 类别：趋势指标
 * 
 * 创建日期：2025-10-31
 * 说明：
 *   转换线 (Tenkan-sen) = (9日最高 + 9日最低) / 2
 *   基准线 (Kijun-sen) = (26日最高 + 26日最低) / 2
 *   先行跨度A (Senkou Span A) = (转换线 + 基准线) / 2 [向前偏移26日]
 *   先行跨度B (Senkou Span B) = (52日最高 + 52日最低) / 2 [向前偏移26日]
 *   滞后跨度 (Chikou Span) = 当前收盘价 [向后偏移26日]
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

IndicatorResult Ichimoku(
    const std::vector<double>& high,
    const std::vector<double>& low,
    const std::vector<double>& close,
    int ICHIMOKU_TENKAN,
    int ICHIMOKU_KIJUN,
    int ICHIMOKU_SENKOU
) {
    IndicatorResult result;

    size_t data_size = high.size();
    
    if (data_size < static_cast<size_t>(ICHIMOKU_SENKOU) ||
        low.size() != data_size ||
        close.size() != data_size) {
        result.setSeries("tenkan", std::vector<Value>{});
        result.setSeries("kijun", std::vector<Value>{});
        result.setSeries("senkou_a", std::vector<Value>{});
        result.setSeries("senkou_b", std::vector<Value>{});
        result.setSeries("chikou", std::vector<Value>{});
        return result;
    }

    // 辅助函数：计算周期内的最高价和最低价的中点
    auto calculate_midpoint = [&](size_t end_idx, int period) -> double {
        if (end_idx < static_cast<size_t>(period - 1)) {
            return 0.0;
        }
        
        size_t start_idx = end_idx - period + 1;
        double highest = *std::max_element(high.begin() + start_idx, high.begin() + end_idx + 1);
        double lowest = *std::min_element(low.begin() + start_idx, low.begin() + end_idx + 1);
        
        return (highest + lowest) / 2.0;
    };

    // 计算Ichimoku序列
    int valid_count = static_cast<int>(data_size) - ICHIMOKU_SENKOU + 1;
    std::vector<Value> tenkan_series, kijun_series, senkou_a_series, senkou_b_series, chikou_series;
    tenkan_series.reserve(valid_count);
    kijun_series.reserve(valid_count);
    senkou_a_series.reserve(valid_count);
    senkou_b_series.reserve(valid_count);
    chikou_series.reserve(valid_count);
    
    for (int i = 0; i < valid_count; ++i) {
        size_t idx = ICHIMOKU_SENKOU - 1 + i;
        
        // 1. 转换线 (Tenkan-sen)
        double tenkan = calculate_midpoint(idx, ICHIMOKU_TENKAN);
        
        // 2. 基准线 (Kijun-sen)
        double kijun = calculate_midpoint(idx, ICHIMOKU_KIJUN);
        
        // 3. 先行跨度A (Senkou Span A) = (转换线 + 基准线) / 2
        double senkou_a = (tenkan + kijun) / 2.0;
        
        // 4. 先行跨度B (Senkou Span B)
        double senkou_b = calculate_midpoint(idx, ICHIMOKU_SENKOU);
        
        // 5. 滞后跨度 (Chikou Span) = 当前收盘价
        double chikou = close[idx];
        
        tenkan_series.push_back(Value::fromNumber(tenkan));
        kijun_series.push_back(Value::fromNumber(kijun));
        senkou_a_series.push_back(Value::fromNumber(senkou_a));
        senkou_b_series.push_back(Value::fromNumber(senkou_b));
        chikou_series.push_back(Value::fromNumber(chikou));
    }
    
    if (tenkan_series.empty()) {
        result.setSeries("tenkan", std::vector<Value>{});
        result.setSeries("kijun", std::vector<Value>{});
        result.setSeries("senkou_a", std::vector<Value>{});
        result.setSeries("senkou_b", std::vector<Value>{});
        result.setSeries("chikou", std::vector<Value>{});
        return result;
    }
    
    result.setSeries("tenkan", tenkan_series);
    result.setSeries("kijun", kijun_series);
    result.setSeries("senkou_a", senkou_a_series);
    result.setSeries("senkou_b", senkou_b_series);
    result.setSeries("chikou", chikou_series);

    return result;
}

IndicatorResult Calculator::Ichimoku(
    const std::vector<double>& high,
    const std::vector<double>& low,
    const std::vector<double>& close,
    int ICHIMOKU_TENKAN,
    int ICHIMOKU_KIJUN,
    int ICHIMOKU_SENKOU
) {
    return prophet::indicators::Ichimoku(high, low, close, ICHIMOKU_TENKAN, ICHIMOKU_KIJUN, ICHIMOKU_SENKOU);
}

} // namespace prophet::indicators

