/*
 * ============================================================================
 * 文件名：STOCHRSI.cpp
 * 指标名：STOCHRSI
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

IndicatorResult Calculator::STOCHRSI(
    const std::vector<double>& close,
    int RSI_PERIOD,
    int STOCH_PERIOD,
    int K_PERIOD,
    int D_PERIOD
) {
    IndicatorResult result;

    if (close.size() < size_t(RSI_PERIOD + STOCH_PERIOD)) {
        result.setSeries("k", std::vector<Value>{});
        result.setSeries("d", std::vector<Value>{});
        result.set("crossover_type", Value::fromString("NONE"));
        result.set("overbought", Value::fromBoolean(false));
        result.set("oversold", Value::fromBoolean(false));
        return result;
    }

    // 使用 TA-Lib 计算 StochRSI
    // 第一步：计算 RSI
    std::vector<double> rsi_values(close.size());
    int rsi_outBegIdx, rsi_outNbElement;
    TA_RetCode rsi_retCode = TA_RSI(
        0, static_cast<int>(close.size()) - 1,
        close.data(),
        RSI_PERIOD,
        &rsi_outBegIdx, &rsi_outNbElement,
        rsi_values.data()  // 修复：TA-Lib从索引0开始填充
    );

    if (rsi_retCode != TA_SUCCESS || rsi_outNbElement == 0) {
        result.setSeries("k", std::vector<Value>{});
        result.setSeries("d", std::vector<Value>{});
        result.set("crossover_type", Value::fromString("NONE"));
        result.set("overbought", Value::fromBoolean(false));
        result.set("oversold", Value::fromBoolean(false));
        return result;
    }

    // 第二步：对 RSI 应用 STOCH
    // 注意：TA_STOCH需要对RSI的有效数据进行计算
    std::vector<double> k_values(close.size());
    std::vector<double> d_values(close.size());

    int outBegIdx, outNbElement;
    TA_RetCode retCode = TA_STOCH(
        0, rsi_outNbElement - 1,  // 使用RSI的有效输出范围
        rsi_values.data(),  // high
        rsi_values.data(),  // low
        rsi_values.data(),  // close
        STOCH_PERIOD,
        K_PERIOD,
        TA_MAType_SMA,
        D_PERIOD,
        TA_MAType_SMA,
        &outBegIdx, &outNbElement,
        k_values.data(),
        d_values.data()
    );

    if (retCode != TA_SUCCESS || outNbElement == 0) {
        result.setSeries("k", std::vector<Value>{});
        result.setSeries("d", std::vector<Value>{});
        result.set("crossover_type", Value::fromString("NONE"));
        result.set("overbought", Value::fromBoolean(false));
        result.set("oversold", Value::fromBoolean(false));
        return result;
    }

    // 提取有效数据（TA-Lib将有效数据存储在数组开头）
    std::vector<Value> k_series, d_series;
    k_series.reserve(outNbElement);
    d_series.reserve(outNbElement);
    
    for (int i = 0; i < outNbElement; ++i) {
        k_series.push_back(Value::fromNumber(k_values[i]));
        d_series.push_back(Value::fromNumber(d_values[i]));
    }
    
    result.setSeries("k", k_series);
    result.setSeries("d", d_series);

    // 检测 K/D 交叉（基于有效数据范围）
    std::vector<double> k_valid(outNbElement), d_valid(outNbElement);
    for (int i = 0; i < outNbElement; ++i) {
        k_valid[i] = k_series[i].toNumber();
        d_valid[i] = d_series[i].toNumber();
    }
    std::string crossover = detectCrossover(k_valid, d_valid);

    // 判断超买超卖（基于最新值）
    double curr_k = k_series.back().toNumber();
    bool overbought = curr_k > 80.0;
    bool oversold = curr_k < 20.0;

    // 填充派生字段
    result.set("crossover_type", Value::fromString(crossover));
    result.set("overbought", Value::fromBoolean(overbought));
    result.set("oversold", Value::fromBoolean(oversold));

    return result;
}

} // namespace prophet::indicators
