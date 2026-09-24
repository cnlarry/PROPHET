/*
 * ============================================================================
 * 文件名：MFI.cpp
 * 指标名：MFI
 * 类别：动量指标
 * 
 * 从 calculator.cpp 拆分
 * 拆分日期：2025-10-30
 * ============================================================================
 */

#include "prophet/indicators/calculator.hpp"
#include "prophet/indicators/registry.hpp"
#include "prophet/simd/simd_indicators.hpp"  // P3优化：SIMD指标
#include "prophet/simd/simd_config.hpp"      // P3优化：SIMD配置
#include <ta_libc.h>
#include <cmath>
#include <algorithm>
#include <numeric>
#include <stdexcept>

namespace prophet::indicators {

IndicatorResult Calculator::MFI(
    const std::vector<double>& high,
    const std::vector<double>& low,
    const std::vector<double>& close,
    const std::vector<double>& volume,
    int MFI_PERIOD
) {
    IndicatorResult result;

    if (close.size() < size_t(MFI_PERIOD + 1)) {
        result.setSeries("value", std::vector<Value>{});
        result.set("overbought", Value::fromBoolean(false));
        result.set("oversold", Value::fromBoolean(false));
        return result;
    }

    std::vector<double> mfi_values(close.size(), 50.0);
    
    // P3优化：如果SIMD可用且数据量足够，使用SIMD加速（2-3倍）
    if (simd::Config::instance().isEnabled() && 
        simd::Config::instance().features().has_avx2 &&
        close.size() >= 100) {  // 数据量阈值：100+
        
        try {
            // 使用SIMD计算MFI
            auto simd_result = simd::calculate_MFI(
                high.data(), low.data(), close.data(), volume.data(), 
                close.size(), MFI_PERIOD
            );
            
            // 🔑 关键修复：SIMD返回的数组可能包含前面的NaN，需要提取有效数据
            // 计算outBegIdx（与TA-Lib保持一致）
            int outBegIdx = MFI_PERIOD;
            int outNbElement = static_cast<int>(close.size()) - outBegIdx;
            
            if (outNbElement <= 0) {
                result.setSeries("value", std::vector<Value>{});
                result.set("overbought", Value::fromBoolean(false));
                result.set("oversold", Value::fromBoolean(false));
                return result;
            }
            
            // 提取有效数据
            std::vector<Value> mfi_series;
            mfi_series.reserve(outNbElement);
            for (int i = 0; i < outNbElement; ++i) {
                int idx = outBegIdx + i;
                if (idx < static_cast<int>(simd_result.mfi.size())) {
                    mfi_series.push_back(Value::fromNumber(simd_result.mfi[idx]));
                }
            }
            
            if (mfi_series.empty()) {
                result.setSeries("value", std::vector<Value>{});
                result.set("overbought", Value::fromBoolean(false));
                result.set("oversold", Value::fromBoolean(false));
                return result;
            }
            
            result.setSeries("value", mfi_series);
            
            double curr_mfi = mfi_series.back().toNumber();
            bool overbought = curr_mfi > 80.0;
            bool oversold = curr_mfi < 20.0;

            result.set("overbought", Value::fromBoolean(overbought));
            result.set("oversold", Value::fromBoolean(oversold));
            
            return result;
        } catch (...) {
            // SIMD计算失败，回退到TA-Lib
        }
    }
    
    // 使用 TA-Lib 计算（回退路径）
    int outBegIdx, outNbElement;
    TA_RetCode retCode = TA_MFI(
        0, static_cast<int>(close.size()) - 1,
        high.data(), low.data(), close.data(), volume.data(),
        MFI_PERIOD,
        &outBegIdx, &outNbElement,
        mfi_values.data()  // 修复：使用正确的指针
    );

    if (retCode != TA_SUCCESS || outNbElement == 0) {
        result.setSeries("value", std::vector<Value>{});
        result.set("overbought", Value::fromBoolean(false));
        result.set("oversold", Value::fromBoolean(false));
        return result;
    }
    
    // 提取有效数据（TA-Lib将有效数据存储在数组开头）
    std::vector<Value> mfi_series;
    mfi_series.reserve(outNbElement);
    for (int i = 0; i < outNbElement; ++i) {
        mfi_series.push_back(Value::fromNumber(mfi_values[i]));
    }
    result.setSeries("value", mfi_series);
    
    double curr_mfi = mfi_series.back().toNumber();
    bool overbought = curr_mfi > 80.0;
    bool oversold = curr_mfi < 20.0;

    result.set("overbought", Value::fromBoolean(overbought));
    result.set("oversold", Value::fromBoolean(oversold));

    return result;
}

} // namespace prophet::indicators
