/*
 * ============================================================================
 * 文件名：simd_indicators.hpp
 * 功能说明：SIMD优化的技术指标接口（阶段3优化）
 * 
 * 已实现的SIMD指标：
 * - SMA、EMA、RSI、MACD、Bollinger Bands、ATR、Stochastic、CCI、ADX
 * ============================================================================
 */

#pragma once

#include <vector>
#include <cstddef>

namespace prophet {
namespace simd {

/**
 * RSI结果结构
 */
struct RSIResult {
    std::vector<double> rsi;        // RSI值（0-100）
    std::vector<double> avg_gain;   // 平均涨幅
    std::vector<double> avg_loss;   // 平均跌幅
};

/**
 * MACD结果结构
 */
struct MACDResult {
    std::vector<double> macd;       // MACD线
    std::vector<double> signal;     // 信号线
    std::vector<double> histogram;  // 柱状图
};

/**
 * Bollinger Bands结果结构
 */
struct BollingerBandsResult {
    std::vector<double> upper;      // 上轨
    std::vector<double> middle;     // 中轨（SMA）
    std::vector<double> lower;      // 下轨
};

/**
 * ATR结果结构
 */
struct ATRResult {
    std::vector<double> atr;        // ATR值
    std::vector<double> tr;         // 真实波幅
};

/**
 * Stochastic结果结构
 */
struct StochResult {
    std::vector<double> k;          // %K线
    std::vector<double> d;          // %D线（%K的移动平均）
};

/**
 * CCI结果结构
 */
struct CCIResult {
    std::vector<double> cci;            // CCI值
    std::vector<double> typical_price;  // 典型价格
};

/**
 * ADX结果结构
 */
struct ADXResult {
    std::vector<double> adx;        // ADX值
    std::vector<double> plus_di;    // +DI
    std::vector<double> minus_di;   // -DI
};

/**
 * MFI结果结构
 */
struct MFIResult {
    std::vector<double> mfi;        // MFI值（0-100）
    std::vector<double> typical_price;  // 典型价格
    std::vector<double> money_flow;     // 资金流量
};

/**
 * OBV结果结构
 */
struct OBVResult {
    std::vector<double> obv;        // OBV值
    std::vector<double> price_changes;  // 价格变化量
};

/**
 * ROC结果结构
 */
struct ROCResult {
    std::vector<double> roc;        // ROC值
};

// ============================================================================
// 基础指标
// ============================================================================

/**
 * SIMD优化的简单移动平均（SMA）
 */
void calculate_SMA(
    const double* prices,
    size_t length,
    int period,
    double* output
);

/**
 * SIMD优化的指数移动平均（EMA）
 */
void calculate_EMA(
    const double* prices,
    size_t length,
    int period,
    double* output
);

/**
 * SIMD优化的相对强弱指标（RSI）
 */
RSIResult calculate_RSI(
    const double* prices,
    size_t length,
    int period
);

/**
 * SIMD优化的MACD
 */
MACDResult calculate_MACD(
    const double* prices,
    size_t length,
    int FAST_PERIOD,
    int SLOW_PERIOD,
    int SIGNAL_PERIOD
);

/**
 * SIMD优化的布林带
 */
BollingerBandsResult calculate_BollingerBands(
    const double* prices,
    size_t length,
    int period,
    double std_dev
);

/**
 * SIMD优化的ATR（Average True Range）
 */
ATRResult calculate_ATR(
    const double* high,
    const double* low,
    const double* close,
    size_t length,
    int period
);

/**
 * SIMD优化的Stochastic振荡器
 */
StochResult calculate_STOCH(
    const double* high,
    const double* low,
    const double* close,
    size_t length,
    int K_PERIOD,
    int k_smooth,
    int D_PERIOD
);

/**
 * SIMD优化的CCI（Commodity Channel Index）
 */
CCIResult calculate_CCI(
    const double* high,
    const double* low,
    const double* close,
    size_t length,
    int period
);

/**
 * SIMD优化的ADX（Average Directional Index）
 */
ADXResult calculate_ADX(
    const double* high,
    const double* low,
    const double* close,
    size_t length,
    int period
);

/**
 * SIMD优化的MFI（Money Flow Index）
 */
MFIResult calculate_MFI(
    const double* high,
    const double* low,
    const double* close,
    const double* volume,
    size_t length,
    int period
);

/**
 * SIMD优化的ROC（Rate of Change）
 */
ROCResult calculate_ROC(
    const double* prices,
    size_t length,
    int period
);

/**
 * SIMD优化的OBV（On Balance Volume）
 */
OBVResult calculate_OBV(
    const double* close,
    const double* volume,
    size_t length
);

/**
 * SIMD优化的WMA（Weighted Moving Average）
 */
void calculate_WMA(
    const double* prices,
    size_t length,
    int period,
    double* output
);

/**
 * SIMD优化的DEMA（Double Exponential Moving Average）
 */
void calculate_DEMA(
    const double* prices,
    size_t length,
    int period,
    double* output
);

/**
 * SIMD优化的TEMA（Triple Exponential Moving Average）
 */
void calculate_TEMA(
    const double* prices,
    size_t length,
    int period,
    double* output
);

/**
 * SIMD优化的VWMA（Volume Weighted Moving Average）
 */
void calculate_VWMA(
    const double* prices,
    const double* volume,
    size_t length,
    int period,
    double* output
);

/**
 * SIMD优化的TRIX（Triple Exponential Moving Average Oscillator）
 */
struct TRIXResult {
    std::vector<double> trix;        // TRIX线
    std::vector<double> signal;      // 信号线
    std::vector<double> histogram;   // 柱状图
};

TRIXResult calculate_TRIX(
    const double* prices,
    size_t length,
    int period,
    int SIGNAL_PERIOD
);

/**
 * SIMD优化的KDJ（随机指标）
 */
struct KDJResult {
    std::vector<double> k;           // K线
    std::vector<double> d;           // D线
    std::vector<double> j;           // J线
};

KDJResult calculate_KDJ(
    const double* high,
    const double* low,
    const double* close,
    size_t length,
    int K_PERIOD,
    int D_PERIOD
);

/**
 * SIMD优化的Donchian Channel（唐奇安通道）
 */
struct DonchianChannelResult {
    std::vector<double> upper;      // 上轨（N期最高价）
    std::vector<double> middle;     // 中轨（N期平均）
    std::vector<double> lower;      // 下轨（N期最低价）
};

DonchianChannelResult calculate_DonchianChannel(
    const double* high,
    const double* low,
    const double* close,
    size_t length,
    int period
);

} // namespace simd
} // namespace prophet

