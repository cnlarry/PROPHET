/*
 * ============================================================================
 * 文件名：Base.cpp
 * 功能说明：基础技术指标计算实现
 * 
 * 这个文件实现了各种常用的技术分析指标计算
 * 使用TA-Lib库进行专业的技术指标计算
 * 
 * 包含的指标：
 * - 趋势指标：SMA、EMA、WMA、TEMA、DEMA、KAMA、MAMA、T3
 * - 动量指标：RSI、STOCH、CCI、MFI、ROC、MOM、PPO、APO
 * - 波动率指标：ATR、NATR、TRANGE、BBANDS
 * - 成交量指标：OBV、AD、ADOSC
 * - 综合指标：MACD、STOCHRSI、ADX、AROON、SAR、WILLR
 * 
 * 这些是量化交易中最常用的技术指标
 * ============================================================================
 */

#include "prophet/functions/Base.hpp"

#include <algorithm>
#include <cmath>
#include <limits>
#include <numeric>
#include <stdexcept>
#include "ta_libc.h"

namespace prophet::functions {
namespace {

const std::vector<double>& selectField(const std::vector<double>& open,
                                      const std::vector<double>& high,
                                      const std::vector<double>& low,
                                      const std::vector<double>& close,
                                      const std::vector<double>& volume,
                                      const std::string& field) {
    if (field == "open") return open;
    if (field == "high") return high;
    if (field == "low") return low;
    if (field == "close") return close;
    if (field == "volume") return volume;
    throw std::invalid_argument("Invalid field: " + field + ". Must be one of: open, high, low, close, volume");
}

int clampPeriod(int requested, size_t data_size) {
    if (requested <= 0) return 0;
    if (static_cast<size_t>(requested) > data_size) {
        return static_cast<int>(data_size);
    }
    return requested;
}

} // namespace

IndicatorResult Base::KLINE(const std::vector<double>& open,
                             const std::vector<double>& high,
                             const std::vector<double>& low,
                             const std::vector<double>& close,
                             const std::vector<double>& volume,
                             const std::string& field,
                             int offset) {
    IndicatorResult result;
    const auto& series = selectField(open, high, low, close, volume, field);

    if (series.empty()) {
        result.set("value", Value::fromNumber(0.0));
        return result;
    }

    // 边界检查：偏移量范围 [-100, 0]
    if (offset < -100 || offset > 0) {
        result.set("value", Value::fromNumber(0.0));
        return result;
    }

    int index = static_cast<int>(series.size()) - 1 + offset;
    if (index < 0) {
        result.set("value", Value::fromNumber(series.front()));
        return result;
    }

    double value = series[static_cast<size_t>(index)];
    result.set("value", Value::fromNumber(value));
    return result;
}

IndicatorResult Base::HIGHEST(const std::vector<double>& open,
                               const std::vector<double>& high,
                               const std::vector<double>& low,
                               const std::vector<double>& close,
                               const std::vector<double>& volume,
                               const std::string& field,
                               int period) {
    IndicatorResult result;
    const auto& series = selectField(open, high, low, close, volume, field);

    if (series.empty() || period <= 0) {
        result.set("value", Value::fromNumber(0.0));
        return result;
    }

    int clamped = clampPeriod(period, series.size());
    int start = static_cast<int>(series.size()) - clamped;
    double max_val = *std::max_element(series.begin() + start, series.end());

    result.set("value", Value::fromNumber(max_val));
    return result;
}

IndicatorResult Base::LOWEST(const std::vector<double>& open,
                              const std::vector<double>& high,
                              const std::vector<double>& low,
                              const std::vector<double>& close,
                              const std::vector<double>& volume,
                              const std::string& field,
                              int period) {
    IndicatorResult result;
    const auto& series = selectField(open, high, low, close, volume, field);

    if (series.empty() || period <= 0) {
        result.set("value", Value::fromNumber(0.0));
        return result;
    }

    int clamped = clampPeriod(period, series.size());
    int start = static_cast<int>(series.size()) - clamped;
    double min_val = *std::min_element(series.begin() + start, series.end());

    result.set("value", Value::fromNumber(min_val));
    return result;
}

IndicatorResult Base::AVERAGE(const std::vector<double>& open,
                               const std::vector<double>& high,
                               const std::vector<double>& low,
                               const std::vector<double>& close,
                               const std::vector<double>& volume,
                               const std::string& field,
                               int period) {
    IndicatorResult result;
    const auto& series = selectField(open, high, low, close, volume, field);

    if (series.empty() || period <= 0) {
        result.set("value", Value::fromNumber(0.0));
        return result;
    }

    int clamped = clampPeriod(period, series.size());
    int start = static_cast<int>(series.size()) - clamped;
    double sum = std::accumulate(series.begin() + start, series.end(), 0.0);
    double avg = sum / clamped;

    result.set("value", Value::fromNumber(avg));
    return result;
}

IndicatorResult Base::STD(const std::vector<double>& open,
                           const std::vector<double>& high,
                           const std::vector<double>& low,
                           const std::vector<double>& close,
                           const std::vector<double>& volume,
                           const std::string& field,
                           int period) {
    IndicatorResult result;
    const auto& series = selectField(open, high, low, close, volume, field);

    if (series.size() < 2 || period <= 0) {
        result.set("value", Value::fromNumber(0.0));
        return result;
    }

    int clamped = clampPeriod(period, series.size());
    int start = static_cast<int>(series.size()) - clamped;
    double sum = std::accumulate(series.begin() + start, series.end(), 0.0);
    double mean = sum / clamped;

    double variance = 0.0;
    for (auto it = series.begin() + start; it != series.end(); ++it) {
        variance += (*it - mean) * (*it - mean);
    }
    variance /= clamped;

    result.set("value", Value::fromNumber(std::sqrt(variance)));
    return result;
}

IndicatorResult Base::CHANGE(const std::vector<double>& open,
                              const std::vector<double>& high,
                              const std::vector<double>& low,
                              const std::vector<double>& close,
                              const std::vector<double>& volume,
                              const std::string& field,
                              int period) {
    IndicatorResult result;
    const auto& series = selectField(open, high, low, close, volume, field);

    if (series.size() <= static_cast<size_t>(period) || period <= 0) {
        result.set("value", Value::fromNumber(0.0));
        result.set("pct", Value::fromNumber(0.0));
        return result;
    }

    double current = series.back();
    double previous = series[series.size() - 1 - period];
    
    // 绝对变化量
    double change_value = current - previous;
    result.set("value", Value::fromNumber(change_value));
    
    // 百分比变化率
    if (std::abs(previous) < std::numeric_limits<double>::epsilon()) {
        result.set("pct", Value::fromNumber(0.0));
    } else {
        double change_pct = (change_value / previous) * 100.0;
        result.set("pct", Value::fromNumber(change_pct));
    }
    
    return result;
}

IndicatorResult Base::SUM(const std::vector<double>& open,
                           const std::vector<double>& high,
                           const std::vector<double>& low,
                           const std::vector<double>& close,
                           const std::vector<double>& volume,
                           const std::string& field,
                           int period) {
    IndicatorResult result;
    const auto& series = selectField(open, high, low, close, volume, field);

    if (series.empty() || period <= 0) {
        result.set("value", Value::fromNumber(0.0));
        return result;
    }

    int clamped = clampPeriod(period, series.size());
    int start = static_cast<int>(series.size()) - clamped;
    double sum = std::accumulate(series.begin() + start, series.end(), 0.0);
    result.set("value", Value::fromNumber(sum));
    return result;
}

IndicatorResult Base::SLOPE(const std::vector<double>& open,
                             const std::vector<double>& high,
                             const std::vector<double>& low,
                             const std::vector<double>& close,
                             const std::vector<double>& volume,
                             const std::string& field,
                             int period) {
    IndicatorResult result;
    const auto& series = selectField(open, high, low, close, volume, field);

    if (series.size() < 2 || period <= 0) {
        result.set("value", Value::fromNumber(0.0));
        return result;
    }

    int clamped = clampPeriod(period, series.size());
    int start = static_cast<int>(series.size()) - clamped;
    std::vector<double> window(series.begin() + start, series.end());
    int n = static_cast<int>(window.size());

    double sum_x = 0.0;
    double sum_y = 0.0;
    double sum_xy = 0.0;
    double sum_x2 = 0.0;

    for (int i = 0; i < n; ++i) {
        double x = static_cast<double>(i);
        double y = window[static_cast<size_t>(i)];
        sum_x += x;
        sum_y += y;
        sum_xy += x * y;
        sum_x2 += x * x;
    }

    double denominator = n * sum_x2 - sum_x * sum_x;
    double slope = 0.0;
    if (std::abs(denominator) > std::numeric_limits<double>::epsilon()) {
        slope = (n * sum_xy - sum_x * sum_y) / denominator;
    }

    result.set("value", Value::fromNumber(slope));
    return result;
}

IndicatorResult Base::RANK(const std::vector<double>& open,
                            const std::vector<double>& high,
                            const std::vector<double>& low,
                            const std::vector<double>& close,
                            const std::vector<double>& volume,
                            const std::string& field,
                            int period) {
    IndicatorResult result;
    const auto& series = selectField(open, high, low, close, volume, field);

    if (series.empty() || period <= 0) {
        result.set("value", Value::fromNumber(1.0));
        result.set("pct", Value::fromNumber(0.5));
        return result;
    }

    int clamped = clampPeriod(period, series.size());
    int start = static_cast<int>(series.size()) - clamped;
    std::vector<double> window(series.begin() + start, series.end());
    double current = series.back();

    int rank = 1;
    for (double value : window) {
        if (value < current) {
            rank++;
        }
    }

    // 绝对排名
    result.set("value", Value::fromNumber(static_cast<double>(rank)));
    
    // 百分位排名
    double pct = 0.5;
    if (clamped > 1) {
        pct = static_cast<double>(rank - 1) / static_cast<double>(clamped - 1);
    }
    result.set("pct", Value::fromNumber(pct));
    
    return result;
}

IndicatorResult Base::MEDIAN(const std::vector<double>& open,
                              const std::vector<double>& high,
                              const std::vector<double>& low,
                              const std::vector<double>& close,
                              const std::vector<double>& volume,
                              const std::string& field,
                              int period) {
    IndicatorResult result;
    const auto& series = selectField(open, high, low, close, volume, field);

    if (series.empty() || period <= 0) {
        result.set("value", Value::fromNumber(0.0));
        return result;
    }

    int clamped = clampPeriod(period, series.size());
    int start = static_cast<int>(series.size()) - clamped;
    std::vector<double> window(series.begin() + start, series.end());
    std::sort(window.begin(), window.end());

    double median = 0.0;
    size_t n = window.size();
    if (n % 2 == 0) {
        median = (window[n / 2 - 1] + window[n / 2]) / 2.0;
    } else {
        median = window[n / 2];
    }

    result.set("value", Value::fromNumber(median));
    return result;
}

IndicatorResult Base::VARIANCE(const std::vector<double>& open,
                                const std::vector<double>& high,
                                const std::vector<double>& low,
                                const std::vector<double>& close,
                                const std::vector<double>& volume,
                                const std::string& field,
                                int period) {
    IndicatorResult result;
    const auto& series = selectField(open, high, low, close, volume, field);

    if (series.size() < 2 || period <= 0) {
        result.set("value", Value::fromNumber(0.0));
        return result;
    }

    int clamped = clampPeriod(period, series.size());
    int start = static_cast<int>(series.size()) - clamped;
    double sum = std::accumulate(series.begin() + start, series.end(), 0.0);
    double mean = sum / clamped;

    double variance = 0.0;
    for (auto it = series.begin() + start; it != series.end(); ++it) {
        variance += (*it - mean) * (*it - mean);
    }
    variance /= clamped;

    result.set("value", Value::fromNumber(variance));
    return result;
}

IndicatorResult Base::BOP(const std::vector<double>& open,
                           const std::vector<double>& high,
                           const std::vector<double>& low,
                           const std::vector<double>& close,
                           int offset) {
    IndicatorResult result;

    if (close.empty() || open.empty() || high.empty() || low.empty()) {
        result.set("value", Value::fromNumber(0.0));
        return result;
    }

    // 计算所有的BOP值
    std::vector<double> bop_values(close.size(), 0.0);
    int outBegIdx, outNbElement;
    TA_RetCode retCode = TA_BOP(
        0, static_cast<int>(close.size()) - 1,
        open.data(), high.data(), low.data(), close.data(),
        &outBegIdx, &outNbElement,
        bop_values.data()
    );

    if (retCode != TA_SUCCESS || outNbElement == 0) {
        result.set("value", Value::fromNumber(0.0));
        return result;
    }

    // 边界检查：偏移量范围 [-100, 0]
    if (offset < -100 || offset > 0) {
        result.set("value", Value::fromNumber(0.0));
        return result;
    }

    // 根据offset获取对应的值（offset为负数：0=当前，-1=上一个）
    int index = static_cast<int>(outBegIdx + outNbElement - 1) + offset;
    if (index < outBegIdx || index >= static_cast<int>(outBegIdx + outNbElement)) {
        result.set("value", Value::fromNumber(0.0));
        return result;
    }

    double value = bop_values[index];
    result.set("value", Value::fromNumber(value));
    return result;
}

// ============================================================================
// CROSS - 交叉检测
// ============================================================================

IndicatorResult Base::CROSS(const std::vector<double>& open,
                             const std::vector<double>& high,
                             const std::vector<double>& low,
                             const std::vector<double>& close,
                             const std::vector<double>& volume,
                             const std::string& field,
                             double threshold) {
    IndicatorResult result;
    const auto& series = selectField(open, high, low, close, volume, field);

    if (series.size() < 2) {
        result.set("above", Value::fromBoolean(false));
        result.set("below", Value::fromBoolean(false));
        result.set("any", Value::fromBoolean(false));
        return result;
    }

    double current = series.back();
    double previous = series[series.size() - 2];

    // 向上穿越：前一期 <= 阈值，当前期 > 阈值
    bool cross_above = (previous <= threshold) && (current > threshold);
    
    // 向下穿越：前一期 >= 阈值，当前期 < 阈值
    bool cross_below = (previous >= threshold) && (current < threshold);
    
    // 任意穿越
    bool cross_any = cross_above || cross_below;

    result.set("above", Value::fromBoolean(cross_above));
    result.set("below", Value::fromBoolean(cross_below));
    result.set("any", Value::fromBoolean(cross_any));
    
    return result;
}

// ============================================================================
// ZSCORE - 标准化得分
// ============================================================================

IndicatorResult Base::ZSCORE(const std::vector<double>& open,
                              const std::vector<double>& high,
                              const std::vector<double>& low,
                              const std::vector<double>& close,
                              const std::vector<double>& volume,
                              const std::string& field,
                              int period) {
    IndicatorResult result;
    const auto& series = selectField(open, high, low, close, volume, field);

    if (series.empty() || period <= 0) {
        result.set("value", Value::fromNumber(0.0));
        return result;
    }

    int clamped = clampPeriod(period, series.size());
    int start = static_cast<int>(series.size()) - clamped;
    
    // 计算均值
    double sum = 0.0;
    for (int i = start; i < static_cast<int>(series.size()); ++i) {
        sum += series[i];
    }
    double mean = sum / clamped;
    
    // 计算标准差
    double variance_sum = 0.0;
    for (int i = start; i < static_cast<int>(series.size()); ++i) {
        double diff = series[i] - mean;
        variance_sum += diff * diff;
    }
    double std_dev = std::sqrt(variance_sum / clamped);
    
    // 计算 Z-Score
    double current = series.back();
    double zscore = 0.0;
    if (std_dev > std::numeric_limits<double>::epsilon()) {
        zscore = (current - mean) / std_dev;
    }
    
    result.set("value", Value::fromNumber(zscore));
    return result;
}

// ============================================================================
// PERCENTILE - 百分位数
// ============================================================================

IndicatorResult Base::PERCENTILE(const std::vector<double>& open,
                                  const std::vector<double>& high,
                                  const std::vector<double>& low,
                                  const std::vector<double>& close,
                                  const std::vector<double>& volume,
                                  const std::string& field,
                                  int period,
                                  double quantile) {
    IndicatorResult result;
    const auto& series = selectField(open, high, low, close, volume, field);

    if (series.empty() || period <= 0 || quantile < 0.0 || quantile > 1.0) {
        result.set("value", Value::fromNumber(0.0));
        return result;
    }

    int clamped = clampPeriod(period, series.size());
    int start = static_cast<int>(series.size()) - clamped;
    
    // 复制数据到临时数组并排序
    std::vector<double> sorted_data(series.begin() + start, series.end());
    std::sort(sorted_data.begin(), sorted_data.end());
    
    // 计算百分位数
    double percentile_value = 0.0;
    if (sorted_data.size() == 1) {
        percentile_value = sorted_data[0];
    } else {
        // 使用线性插值方法
        double index = quantile * (sorted_data.size() - 1);
        int lower_index = static_cast<int>(std::floor(index));
        int upper_index = static_cast<int>(std::ceil(index));
        
        if (lower_index == upper_index) {
            percentile_value = sorted_data[lower_index];
        } else {
            double weight = index - lower_index;
            percentile_value = sorted_data[lower_index] * (1.0 - weight) + 
                             sorted_data[upper_index] * weight;
        }
    }
    
    result.set("value", Value::fromNumber(percentile_value));
    return result;
}

// ============================================================================
// VOLA - 波动率分析
// ============================================================================

IndicatorResult Base::VOLA(const std::vector<double>& open,
                            const std::vector<double>& high,
                            const std::vector<double>& low,
                            const std::vector<double>& close,
                            const std::vector<double>& volume,
                            const std::string& field,
                            int period) {
    IndicatorResult result;
    const auto& series = selectField(open, high, low, close, volume, field);

    if (series.empty() || period <= 0) {
        result.set("value", Value::fromNumber(0.0));
        result.set("pct", Value::fromNumber(0.0));
        return result;
    }

    int clamped = clampPeriod(period, series.size());
    int start = static_cast<int>(series.size()) - clamped;
    
    // 计算均值
    double sum = 0.0;
    for (int i = start; i < static_cast<int>(series.size()); ++i) {
        sum += series[i];
    }
    double mean = sum / clamped;
    
    // 计算标准差（波动率）
    double variance_sum = 0.0;
    for (int i = start; i < static_cast<int>(series.size()); ++i) {
        double diff = series[i] - mean;
        variance_sum += diff * diff;
    }
    double std_dev = std::sqrt(variance_sum / clamped);
    
    // 绝对波动率
    result.set("value", Value::fromNumber(std_dev));
    
    // 百分比波动率（变异系数 CV）
    double pct = 0.0;
    if (std::abs(mean) > std::numeric_limits<double>::epsilon()) {
        pct = (std_dev / mean) * 100.0;
    }
    result.set("pct", Value::fromNumber(pct));
    
    return result;
}

// ============================================================================
// ATR - 平均真实波动幅度
// ============================================================================

IndicatorResult Base::ATR(const std::vector<double>& /* open */,
                           const std::vector<double>& high,
                           const std::vector<double>& low,
                           const std::vector<double>& close,
                           int period) {
    IndicatorResult result;

    if (high.empty() || low.empty() || close.empty() || period <= 0) {
        result.set("value", Value::fromNumber(0.0));
        result.set("pct", Value::fromNumber(0.0));
        return result;
    }

    // 计算 True Range 序列
    std::vector<double> tr_series;
    tr_series.reserve(high.size());
    
    // 第一根K线的TR = high - low
    if (!high.empty() && !low.empty()) {
        tr_series.push_back(high[0] - low[0]);
    }
    
    // 后续K线的TR = max(high-low, abs(high-prev_close), abs(low-prev_close))
    for (size_t i = 1; i < high.size(); ++i) {
        double hl = high[i] - low[i];
        double hc = std::abs(high[i] - close[i-1]);
        double lc = std::abs(low[i] - close[i-1]);
        double tr = std::max({hl, hc, lc});
        tr_series.push_back(tr);
    }
    
    if (tr_series.empty()) {
        result.set("value", Value::fromNumber(0.0));
        result.set("pct", Value::fromNumber(0.0));
        return result;
    }
    
    // 计算ATR（TR的简单移动平均）
    int clamped = clampPeriod(period, tr_series.size());
    int start = static_cast<int>(tr_series.size()) - clamped;
    
    double atr_sum = 0.0;
    for (int i = start; i < static_cast<int>(tr_series.size()); ++i) {
        atr_sum += tr_series[i];
    }
    double atr = atr_sum / clamped;
    
    // 绝对ATR
    result.set("value", Value::fromNumber(atr));
    
    // 百分比ATR（相对于当前价格）
    double current_price = close.back();
    double atr_pct = 0.0;
    if (std::abs(current_price) > std::numeric_limits<double>::epsilon()) {
        atr_pct = (atr / current_price) * 100.0;
    }
    result.set("pct", Value::fromNumber(atr_pct));
    
    return result;
}

} // namespace prophet::functions

