/*
 * ============================================================================
 * 文件名：PivotPoints.cpp
 * 指标名：Pivot Points（枢轴点）
 * 类别：价格指标
 * 
 * 创建日期：2025-10-31
 * ============================================================================
 */

#include "prophet/indicators/calculator.hpp"
#include "prophet/indicators/registry.hpp"
#include <cmath>

namespace prophet::indicators {

IndicatorResult Calculator::PivotPoints(
    const std::vector<double>& high,
    const std::vector<double>& low,
    const std::vector<double>& close
) {
    IndicatorResult result;
    
    if (close.size() < 2) {
        result.setSeries("pp", std::vector<Value>{});
        result.setSeries("r1", std::vector<Value>{});
        result.setSeries("r2", std::vector<Value>{});
        result.setSeries("r3", std::vector<Value>{});
        result.setSeries("s1", std::vector<Value>{});
        result.setSeries("s2", std::vector<Value>{});
        result.setSeries("s3", std::vector<Value>{});
        return result;
    }
    
    // 计算Pivot Points序列（每根K线基于前一根K线计算）
    std::vector<Value> pp_series, r1_series, r2_series, r3_series, s1_series, s2_series, s3_series;
    int valid_count = static_cast<int>(close.size()) - 1;  // 从第二根K线开始
    pp_series.reserve(valid_count);
    r1_series.reserve(valid_count);
    r2_series.reserve(valid_count);
    r3_series.reserve(valid_count);
    s1_series.reserve(valid_count);
    s2_series.reserve(valid_count);
    s3_series.reserve(valid_count);
    
    for (int i = 1; i < static_cast<int>(close.size()); ++i) {
        // 使用前一根K线的数据
        double prev_high = high[i - 1];
        double prev_low = low[i - 1];
        double prev_close = close[i - 1];
        
        // 计算枢轴点（Standard Pivot Points）
        double pp = (prev_high + prev_low + prev_close) / 3.0;
        
        // 计算支撑和阻力位
        double r1 = 2 * pp - prev_low;
        double s1 = 2 * pp - prev_high;
        double r2 = pp + (prev_high - prev_low);
        double s2 = pp - (prev_high - prev_low);
        double r3 = prev_high + 2 * (pp - prev_low);
        double s3 = prev_low - 2 * (prev_high - pp);
        
        pp_series.push_back(Value::fromNumber(pp));
        r1_series.push_back(Value::fromNumber(r1));
        r2_series.push_back(Value::fromNumber(r2));
        r3_series.push_back(Value::fromNumber(r3));
        s1_series.push_back(Value::fromNumber(s1));
        s2_series.push_back(Value::fromNumber(s2));
        s3_series.push_back(Value::fromNumber(s3));
    }
    
    if (pp_series.empty()) {
        result.setSeries("pp", std::vector<Value>{});
        result.setSeries("r1", std::vector<Value>{});
        result.setSeries("r2", std::vector<Value>{});
        result.setSeries("r3", std::vector<Value>{});
        result.setSeries("s1", std::vector<Value>{});
        result.setSeries("s2", std::vector<Value>{});
        result.setSeries("s3", std::vector<Value>{});
        return result;
    }
    
    result.setSeries("pp", pp_series);
    result.setSeries("r1", r1_series);
    result.setSeries("r2", r2_series);
    result.setSeries("r3", r3_series);
    result.setSeries("s1", s1_series);
    result.setSeries("s2", s2_series);
    result.setSeries("s3", s3_series);
    
    return result;
}

} // namespace prophet::indicators

