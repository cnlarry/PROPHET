/*
 * ============================================================================
 * 文件名：simd_indicators.cpp
 * 功能说明：SIMD指标计算实现
 * ============================================================================
 */

#include "prophet/simd/simd_indicators.hpp"
#include "prophet/simd/simd_math.hpp"
#include "prophet/simd/simd_config.hpp"
#include <algorithm>
#include <cmath>

namespace prophet {
namespace simd {

/**
 * ============================================================================
 * SMA实现
 * ============================================================================
 */

void calculate_SMA(
    const double* prices,
    size_t length,
    int period,
    double* output
) {
    if (length == 0 || period <= 0) return;
    
    // 直接使用simd_math的rolling_mean实现
    // 它已经包含了滑动窗口优化和SIMD加速
    rolling_mean(prices, length, period, output);
}

/**
 * ============================================================================
 * EMA实现
 * ============================================================================
 */

void calculate_EMA(
    const double* prices,
    size_t length,
    int period,
    double* output
) {
    if (length == 0 || period <= 0) return;
    
    // EMA平滑系数
    double alpha = 2.0 / (period + 1);
    double one_minus_alpha = 1.0 - alpha;
    
    // 第一个值使用价格本身
    output[0] = prices[0];
    
    // EMA计算有串行依赖，但可以向量化乘法部分
    // EMA[i] = price[i] * α + EMA[i-1] * (1-α)
    
    #ifdef HAS_AVX2_SUPPORT
    if (should_use_simd() && length >= 8) {
        Config::instance().recordSIMDCall(length);
        
        __m256d valpha = _mm256_set1_pd(alpha);
        __m256d vone_minus_alpha = _mm256_set1_pd(one_minus_alpha);
        
        // 由于串行依赖，我们仍然需要逐个计算
        // 但可以向量化预处理：price[i] * alpha
        std::vector<double> price_contrib(length);
        
        size_t i = 0;
        for (; i + 3 < length; i += 4) {
            __m256d vprice = _mm256_loadu_pd(prices + i);
            __m256d vcontrib = _mm256_mul_pd(vprice, valpha);
            _mm256_storeu_pd(price_contrib.data() + i, vcontrib);
        }
        
        for (; i < length; i++) {
            price_contrib[i] = prices[i] * alpha;
        }
        
        // 串行计算EMA（使用预计算的贡献）
        for (size_t i = 1; i < length; i++) {
            output[i] = price_contrib[i] + output[i-1] * one_minus_alpha;
        }
        
        return;
    }
    #endif
    
    // 标量回退
    Config::instance().recordScalarCall(length);
    for (size_t i = 1; i < length; i++) {
        output[i] = prices[i] * alpha + output[i-1] * one_minus_alpha;
    }
}

/**
 * ============================================================================
 * RSI实现（P3稳定版 - 依赖编译器RVO优化）
 * ============================================================================
 */

RSIResult calculate_RSI(
    const double* prices,
    size_t length,
    int period
) {
    RSIResult result;
    
    if (length < 2) {
        result.rsi.resize(length, 50.0);  // 默认中性值
        result.avg_gain.resize(length, 0.0);
        result.avg_loss.resize(length, 0.0);
        return result;
    }
    
    // 分配内存
    result.rsi.resize(length);
    result.avg_gain.resize(length);
    result.avg_loss.resize(length);
    
    // 临时缓冲区
    std::vector<double> changes(length);
    std::vector<double> gains(length);
    std::vector<double> losses(length);
    
    changes[0] = 0.0;
    for (size_t i = 1; i < length; i++) {
        changes[i] = prices[i] - prices[i-1];
    }
    
    // 2. 分离涨跌（可向量化）
    #ifdef HAS_AVX2_SUPPORT
    if (should_use_simd() && length >= 4) {
        Config::instance().recordSIMDCall(length);
        
        __m256d vzero = _mm256_setzero_pd();
        
        size_t i = 0;
        for (; i + 3 < length; i += 4) {
            __m256d vchange = _mm256_loadu_pd(changes.data() + i);
            
            // gain = max(change, 0)
            __m256d vgain = _mm256_max_pd(vchange, vzero);
            _mm256_storeu_pd(gains.data() + i, vgain);
            
            // loss = -min(change, 0) = max(-change, 0)
            __m256d vneg_change = _mm256_sub_pd(vzero, vchange);
            __m256d vloss = _mm256_max_pd(vneg_change, vzero);
            _mm256_storeu_pd(losses.data() + i, vloss);
        }
        
        for (; i < length; i++) {
            gains[i] = std::max(changes[i], 0.0);
            losses[i] = std::max(-changes[i], 0.0);
        }
    } else
    #endif
    {
        Config::instance().recordScalarCall(length);
        for (size_t i = 0; i < length; i++) {
            gains[i] = std::max(changes[i], 0.0);
            losses[i] = std::max(-changes[i], 0.0);
        }
    }
    
    // 3. 使用EMA平滑涨跌
    calculate_EMA(gains.data(), length, period, result.avg_gain.data());
    calculate_EMA(losses.data(), length, period, result.avg_loss.data());
    
    // 4. 计算RSI
    for (size_t i = 0; i < length; i++) {
        if (result.avg_loss[i] == 0.0) {
            result.rsi[i] = 100.0;  // 没有跌幅 => RSI = 100
        } else {
            double rs = result.avg_gain[i] / result.avg_loss[i];
            result.rsi[i] = 100.0 - 100.0 / (1.0 + rs);
        }
    }
    
    return result;  // 编译器RVO优化
}

/**
 * ============================================================================
 * MACD实现（P3稳定版 - 依赖编译器RVO优化）
 * ============================================================================
 */

MACDResult calculate_MACD(
    const double* prices,
    size_t length,
    int FAST_PERIOD,
    int SLOW_PERIOD,
    int SIGNAL_PERIOD
) {
    MACDResult result;
    
    if (length == 0) return result;
    
    result.macd.resize(length);
    result.signal.resize(length);
    result.histogram.resize(length);
    
    // 临时缓冲区
    std::vector<double> fast_ema(length);
    std::vector<double> slow_ema(length);
    
    // 1. 计算快速和慢速EMA
    calculate_EMA(prices, length, FAST_PERIOD, fast_ema.data());
    calculate_EMA(prices, length, SLOW_PERIOD, slow_ema.data());
    
    // 2. MACD线 = fast_EMA - slow_EMA（可向量化）
    sub(fast_ema.data(), slow_ema.data(), result.macd.data(), length);
    
    // 3. 信号线 = EMA(MACD, SIGNAL_PERIOD)
    calculate_EMA(result.macd.data(), length, SIGNAL_PERIOD, result.signal.data());
    
    // 4. 柱状图 = MACD - Signal（可向量化）
    sub(result.macd.data(), result.signal.data(), result.histogram.data(), length);
    
    return result;  // 编译器RVO优化
}

/**
 * ============================================================================
 * 布林带实现
 * ============================================================================
 */

BollingerBandsResult calculate_BollingerBands(
    const double* prices,
    size_t length,
    int period,
    double k
) {
    BollingerBandsResult result;
    
    if (length == 0) return result;
    
    result.middle.resize(length);
    result.upper.resize(length);
    result.lower.resize(length);
    
    // 1. 中轨 = SMA
    calculate_SMA(prices, length, period, result.middle.data());
    
    // 2. 计算滑动标准差
    std::vector<double> stddev_values(length);
    rolling_stddev(prices, length, period, stddev_values.data());
    
    // 3. 上下轨 = 中轨 ± k * 标准差（可向量化）
    std::vector<double> k_stddev(length);
    mul_scalar(stddev_values.data(), k, k_stddev.data(), length);
    
    add(result.middle.data(), k_stddev.data(), result.upper.data(), length);
    sub(result.middle.data(), k_stddev.data(), result.lower.data(), length);
    
    return result;
}

/**
 * ============================================================================
 * ATR实现 - Average True Range（平均真实波幅）
 * ============================================================================
 */

ATRResult calculate_ATR(
    const double* high,
    const double* low,
    const double* close,
    size_t length,
    int period
) {
    ATRResult result;
    
    if (length < 2) return result;
    
    result.atr.resize(length);
    result.tr.resize(length);
    
    // 1. 计算True Range（可向量化）
    // TR = max(H-L, abs(H-C_prev), abs(L-C_prev))
    
    result.tr[0] = high[0] - low[0];  // 第一根K线没有前收盘价
    
    std::vector<double> hl(length);
    std::vector<double> hc(length);
    std::vector<double> lc(length);
    
    // 计算H-L
    sub(high, low, hl.data(), length);
    
    // 计算abs(H-C_prev) 和 abs(L-C_prev)
    for (size_t i = 1; i < length; i++) {
        hc[i] = std::abs(high[i] - close[i-1]);
        lc[i] = std::abs(low[i] - close[i-1]);
    }
    
    // TR = max(hl, hc, lc) - 使用SIMD加速
    max3(hl.data() + 1, hc.data() + 1, lc.data() + 1, 
         result.tr.data() + 1, length - 1);
    
    // 2. 对TR应用EMA平滑得到ATR
    calculate_EMA(result.tr.data(), length, period, result.atr.data());
    
    return result;
}

/**
 * ============================================================================
 * WMA实现 - Weighted Moving Average（加权移动平均）
 * ============================================================================
 */

void calculate_WMA(
    const double* prices,
    size_t length,
    int period,
    double* output
) {
    if (length == 0 || period <= 0) return;
    
    // WMA权重：1, 2, 3, ..., period
    // WMA[i] = (price[i-period+1]*1 + price[i-period+2]*2 + ... + price[i]*period) / sum_weights
    // sum_weights = 1 + 2 + ... + period = period * (period + 1) / 2
    // 注意：实际计算中使用动态计算的 actual_sum_weights，因为窗口大小可能小于 period
    
    for (size_t i = 0; i < length; i++) {
        int start = std::max(0, static_cast<int>(i) - period + 1);
        int window_size = static_cast<int>(i) - start + 1;
        
        double weighted_sum = 0.0;
        double actual_sum_weights = 0.0;
        
        for (int j = 0; j < window_size; j++) {
            int weight = j + 1;
            weighted_sum += prices[start + j] * weight;
            actual_sum_weights += weight;
        }
        
        output[i] = weighted_sum / actual_sum_weights;
    }
}

/**
 * ============================================================================
 * STOCH实现 - Stochastic Oscillator（随机指标）
 * ============================================================================
 */

StochResult calculate_STOCH(
    const double* high,
    const double* low,
    const double* close,
    size_t length,
    int K_PERIOD,
    int k_smooth,
    int D_PERIOD
) {
    StochResult result;
    
    if (length == 0) return result;
    
    result.k.resize(length);
    result.d.resize(length);
    
    // 1. 计算滚动最高价和最低价
    std::vector<double> highest(length);
    std::vector<double> lowest(length);
    
    rolling_max(high, length, K_PERIOD, highest.data());
    rolling_min(low, length, K_PERIOD, lowest.data());
    
    // 2. 计算Fast %K = (Close - Lowest) / (Highest - Lowest) * 100
    std::vector<double> fast_k(length);
    
    for (size_t i = 0; i < length; i++) {
        double range = highest[i] - lowest[i];
        if (range > 1e-10) {
            fast_k[i] = ((close[i] - lowest[i]) / range) * 100.0;
        } else {
            fast_k[i] = 50.0;  // 默认中性值
        }
    }
    
    // 3. 平滑Fast %K得到Slow %K
    if (k_smooth > 1) {
        calculate_SMA(fast_k.data(), length, k_smooth, result.k.data());
    } else {
        std::copy(fast_k.begin(), fast_k.end(), result.k.begin());
    }
    
    // 4. 对%K应用SMA得到%D
    calculate_SMA(result.k.data(), length, D_PERIOD, result.d.data());
    
    return result;
}

/**
 * ============================================================================
 * CCI实现 - Commodity Channel Index（商品通道指标）
 * ============================================================================
 */

CCIResult calculate_CCI(
    const double* high,
    const double* low,
    const double* close,
    size_t length,
    int period
) {
    CCIResult result;
    
    if (length == 0) return result;
    
    result.cci.resize(length);
    result.typical_price.resize(length);
    
    // 1. 计算典型价格 TP = (H + L + C) / 3（可向量化）
    for (size_t i = 0; i < length; i++) {
        result.typical_price[i] = (high[i] + low[i] + close[i]) / 3.0;
    }
    
    // 2. 计算TP的SMA
    std::vector<double> sma_tp(length);
    calculate_SMA(result.typical_price.data(), length, period, sma_tp.data());
    
    // 3. 计算Mean Deviation
    std::vector<double> mean_dev(length);
    
    for (size_t i = 0; i < length; i++) {
        int start = std::max(0, static_cast<int>(i) - period + 1);
        int window_size = static_cast<int>(i) - start + 1;
        
        double sum_abs_dev = 0.0;
        for (int j = 0; j < window_size; j++) {
            sum_abs_dev += std::abs(result.typical_price[start + j] - sma_tp[i]);
        }
        mean_dev[i] = sum_abs_dev / window_size;
    }
    
    // 4. 计算CCI = (TP - SMA_TP) / (0.015 * Mean_Deviation)
    const double factor = 0.015;
    
    for (size_t i = 0; i < length; i++) {
        if (mean_dev[i] > 1e-10) {
            result.cci[i] = (result.typical_price[i] - sma_tp[i]) / (factor * mean_dev[i]);
        } else {
            result.cci[i] = 0.0;
        }
    }
    
    return result;
}

/**
 * ============================================================================
 * ADX实现 - Average Directional Index（平均趋向指标）
 * ============================================================================
 */

ADXResult calculate_ADX(
    const double* high,
    const double* low,
    const double* close,
    size_t length,
    int period
) {
    ADXResult result;
    
    if (length < 2) return result;
    
    result.adx.resize(length);
    result.plus_di.resize(length);
    result.minus_di.resize(length);
    
    // 1. 计算ATR（已有SIMD优化）
    ATRResult atr_result = calculate_ATR(high, low, close, length, period);
    
    // 2. 计算+DM和-DM
    std::vector<double> plus_dm(length, 0.0);
    std::vector<double> minus_dm(length, 0.0);
    
    for (size_t i = 1; i < length; i++) {
        double up_move = high[i] - high[i-1];
        double down_move = low[i-1] - low[i];
        
        if (up_move > down_move && up_move > 0) {
            plus_dm[i] = up_move;
        }
        
        if (down_move > up_move && down_move > 0) {
            minus_dm[i] = down_move;
        }
    }
    
    // 3. 平滑DM
    std::vector<double> smooth_plus_dm(length);
    std::vector<double> smooth_minus_dm(length);
    
    calculate_EMA(plus_dm.data(), length, period, smooth_plus_dm.data());
    calculate_EMA(minus_dm.data(), length, period, smooth_minus_dm.data());
    
    // 4. 计算DI
    for (size_t i = 0; i < length; i++) {
        if (atr_result.atr[i] > 1e-10) {
            result.plus_di[i] = (smooth_plus_dm[i] / atr_result.atr[i]) * 100.0;
            result.minus_di[i] = (smooth_minus_dm[i] / atr_result.atr[i]) * 100.0;
        } else {
            result.plus_di[i] = 0.0;
            result.minus_di[i] = 0.0;
        }
    }
    
    // 5. 计算DX
    std::vector<double> dx(length);
    
    for (size_t i = 0; i < length; i++) {
        double di_sum = result.plus_di[i] + result.minus_di[i];
        if (di_sum > 1e-10) {
            double di_diff = std::abs(result.plus_di[i] - result.minus_di[i]);
            dx[i] = (di_diff / di_sum) * 100.0;
        } else {
            dx[i] = 0.0;
        }
    }
    
    // 6. ADX = EMA(DX, period)
    calculate_EMA(dx.data(), length, period, result.adx.data());
    
    return result;
}

/**
 * ============================================================================ 
 * MFI实现 - Money Flow Index（资金流量指标）
 * ============================================================================
 */

MFIResult calculate_MFI(
    const double* high,
    const double* low,
    const double* close,
    const double* volume,
    size_t length,
    int period
) {
    MFIResult result;
    
    if (length < 2) return result;
    
    result.mfi.resize(length);
    result.typical_price.resize(length);
    result.money_flow.resize(length);
    
    // 1. 计算典型价格 TP = (H + L + C) / 3（可向量化）
    #ifdef HAS_AVX2_SUPPORT
    if (should_use_simd() && length >= 4) {
        Config::instance().recordSIMDCall(length);
        
        __m256d vthree = _mm256_set1_pd(3.0);
        __m256d vthird = _mm256_set1_pd(1.0 / 3.0);
        
        size_t i = 0;
        for (; i + 3 < length; i += 4) {
            __m256d vhigh = _mm256_loadu_pd(high + i);
            __m256d vlow = _mm256_loadu_pd(low + i);
            __m256d vclose = _mm256_loadu_pd(close + i);
            
            // TP = (H + L + C) / 3
            __m256d vtp = _mm256_add_pd(_mm256_add_pd(vhigh, vlow), vclose);
            vtp = _mm256_mul_pd(vtp, vthird);
            
            _mm256_storeu_pd(result.typical_price.data() + i, vtp);
        }
    } else
    #endif
    {
        for (size_t i = 0; i < length; i++) {
            result.typical_price[i] = (high[i] + low[i] + close[i]) / 3.0;
        }
    }
    
    // 2. 计算资金流量 Money Flow = TP * Volume（可向量化）
    mul(result.typical_price.data(), volume, result.money_flow.data(), length);
    
    // 3. 计算MFI
    for (size_t i = 0; i < length; i++) {
        if (i < static_cast<size_t>(period)) {
            result.mfi[i] = 50.0;  // 默认中性值
            continue;
        }
        
        double pmf_sum = 0.0;  // Positive Money Flow Sum
        double nmf_sum = 0.0;  // Negative Money Flow Sum
        
        // 计算period期内的资金流量和
        for (int j = 0; j < period; j++) {
            size_t idx = i - j;
            if (idx > 0) {
                if (result.typical_price[idx] > result.typical_price[idx - 1]) {
                    pmf_sum += result.money_flow[idx];
                } else if (result.typical_price[idx] < result.typical_price[idx - 1]) {
                    nmf_sum += result.money_flow[idx];
                }
            }
        }
        
        // 计算MFI
        if (nmf_sum > 1e-10) {
            double money_ratio = pmf_sum / nmf_sum;
            result.mfi[i] = 100.0 - (100.0 / (1.0 + money_ratio));
        } else if (pmf_sum > 1e-10) {
            result.mfi[i] = 100.0;  // 只有正资金流
        } else {
            result.mfi[i] = 50.0;  // 无资金流
        }
    }
    
    return result;
}

/**
 * ============================================================================
 * ROC实现 - Rate of Change（变化率指标）
 * ============================================================================
 */

ROCResult calculate_ROC(
    const double* prices,
    size_t length,
    int period
) {
    ROCResult result;
    
    if (length == 0) return result;
    
    result.roc.resize(length);
    
    // ROC = ((Close - Close[period]) / Close[period]) * 100
    // 前period个值设置为0
    for (size_t i = 0; i < static_cast<size_t>(period); i++) {
        result.roc[i] = 0.0;
    }
    
    // 从period位置开始计算ROC（可向量化）
    if (length > static_cast<size_t>(period)) {
        size_t valid_length = length - period;
        
        #ifdef HAS_AVX2_SUPPORT
        if (should_use_simd() && valid_length >= 4) {
            Config::instance().recordSIMDCall(valid_length);
            
            __m256d vhundred = _mm256_set1_pd(100.0);
            
            size_t i = 0;
            for (; i + 3 < valid_length; i += 4) {
                // 加载当前价格和period期前的价格
                __m256d vcurrent = _mm256_loadu_pd(prices + period + i);
                __m256d vprevious = _mm256_loadu_pd(prices + i);
                
                // 计算分子：current - previous
                __m256d vdiff = _mm256_sub_pd(vcurrent, vprevious);
                
                // 计算ROC：((current - previous) / previous) * 100
                __m256d vroc = _mm256_mul_pd(
                    _mm256_div_pd(vdiff, vprevious),
                    vhundred
                );
                
                _mm256_storeu_pd(result.roc.data() + period + i, vroc);
            }
        } else
        #endif
        {
            // 标量实现
            for (size_t i = 0; i < valid_length; i++) {
                double current = prices[period + i];
                double previous = prices[i];
                
                if (previous != 0.0) {
                    result.roc[period + i] = ((current - previous) / previous) * 100.0;
                } else {
                    result.roc[period + i] = 0.0;
                }
            }
        }
    }
    
    return result;
}

/**
 * ============================================================================
 * OBV实现 - On Balance Volume（平衡交易量）
 * ============================================================================
 */

OBVResult calculate_OBV(
    const double* close,
    const double* volume,
    size_t length
) {
    OBVResult result;
    
    if (length == 0) return result;
    
    result.obv.resize(length);
    result.price_changes.resize(length);
    
    // 初始化第一个值
    result.obv[0] = 0.0;
    result.price_changes[0] = 0.0;
    
    if (length < 2) {
        return result;
    }
    
    // 计算价格变化（可向量化）
    #ifdef HAS_AVX2_SUPPORT
    if (should_use_simd() && length >= 5) {
        Config::instance().recordSIMDCall(length - 1);
        
        // 计算close[1..n] - close[0..n-1]（价格变化）
        sub(close + 1, close, result.price_changes.data() + 1, length - 1);
    } else
    #endif
    {
        // 标量实现价格变化
        for (size_t i = 1; i < length; i++) {
            result.price_changes[i] = close[i] - close[i-1];
        }
    }
    
    // 串行计算OBV（因为有累积依赖）
    for (size_t i = 1; i < length; i++) {
        if (result.price_changes[i] > 0.0) {
            result.obv[i] = result.obv[i-1] + volume[i];
        } else if (result.price_changes[i] < 0.0) {
            result.obv[i] = result.obv[i-1] - volume[i];
        } else {
            result.obv[i] = result.obv[i-1];
        }
    }
    
    return result;
}

/**
 * ============================================================================
 * DEMA实现 - Double Exponential Moving Average（双重指数移动平均）
 * ============================================================================
 */

void calculate_DEMA(
    const double* prices,
    size_t length,
    int period,
    double* output
) {
    if (length == 0 || period <= 0) return;
    
    // DEMA = 2*EMA - EMA(EMA)
    
    std::vector<double> ema1(length);
    std::vector<double> ema2(length);
    
    calculate_EMA(prices, length, period, ema1.data());
    calculate_EMA(ema1.data(), length, period, ema2.data());
    
    // output = 2*ema1 - ema2
    for (size_t i = 0; i < length; i++) {
        output[i] = 2.0 * ema1[i] - ema2[i];
    }
}

/**
 * ============================================================================
 * TEMA实现 - Triple Exponential Moving Average（三重指数移动平均）
 * ============================================================================
 */

void calculate_TEMA(
    const double* prices,
    size_t length,
    int period,
    double* output
) {
    if (length == 0 || period <= 0) return;
    
    // TEMA = 3*EMA - 3*EMA(EMA) + EMA(EMA(EMA))
    
    std::vector<double> ema1(length);
    std::vector<double> ema2(length);
    std::vector<double> ema3(length);
    
    calculate_EMA(prices, length, period, ema1.data());
    calculate_EMA(ema1.data(), length, period, ema2.data());
    calculate_EMA(ema2.data(), length, period, ema3.data());
    
    // output = 3*ema1 - 3*ema2 + ema3
    #ifdef HAS_AVX2_SUPPORT
    if (should_use_simd() && length >= 4) {
        Config::instance().recordSIMDCall(length);
        
        __m256d vthree = _mm256_set1_pd(3.0);
        
        size_t i = 0;
        for (; i + 3 < length; i += 4) {
            __m256d vema1 = _mm256_loadu_pd(ema1.data() + i);
            __m256d vema2 = _mm256_loadu_pd(ema2.data() + i);
            __m256d vema3 = _mm256_loadu_pd(ema3.data() + i);
            
            // 3*ema1 - 3*ema2 + ema3
            __m256d vresult = _mm256_add_pd(
                _mm256_sub_pd(
                    _mm256_mul_pd(vema1, vthree),
                    _mm256_mul_pd(ema2, vthree)
                ),
                ema3
            );
            
            _mm256_storeu_pd(output + i, vresult);
        }
    }
    #endif
    
    // 标量回退或剩余部分
    for (size_t i = 0; i < length; i++) {
        output[i] = 3.0 * ema1[i] - 3.0 * ema2[i] + ema3[i];
    }
}

/**
 * ============================================================================
 * VWMA实现 - Volume Weighted Moving Average（成交量加权移动平均）
 * ============================================================================
 */

void calculate_VWMA(
    const double* prices,
    const double* volume,
    size_t length,
    int period,
    double* output
) {
    if (length == 0 || period <= 0) return;
    
    // VWMA = sum(price[i] * volume[i]) / sum(volume[i])  over period
    
    // 预计算价格*成交量
    std::vector<double> price_volume(length);
    mul(prices, volume, price_volume.data(), length);
    
    // 计算滑动窗口的价格*成交量总和
    std::vector<double> sum_pv(length);
    rolling_sum(price_volume.data(), length, period, sum_pv.data());
    
    // 计算滑动窗口的成交量总和
    std::vector<double> sum_v(length);
    rolling_sum(volume, length, period, sum_v.data());
    
    // VWMA = sum_pv / sum_v
    #ifdef HAS_AVX2_SUPPORT
    if (should_use_simd() && length >= 4) {
        Config::instance().recordSIMDCall(length);
        
        size_t i = 0;
        for (; i + 3 < length; i += 4) {
            __m256d vpv = _mm256_loadu_pd(sum_pv.data() + i);
            __m256d vv = _mm256_loadu_pd(sum_v.data() + i);
            
            __m256d vresult = _mm256_div_pd(vpv, vv);
            _mm256_storeu_pd(output + i, vresult);
        }
    }
    #endif
    
    // 标量回退或剩余部分
    for (size_t i = 0; i < length; i++) {
        if (sum_v[i] > 1e-10) {
            output[i] = sum_pv[i] / sum_v[i];
        } else {
            output[i] = prices[i];
        }
    }
}

/**
 * ============================================================================
 * TRIX实现 - Triple Exponential Moving Average Oscillator
 * ============================================================================
 */

TRIXResult calculate_TRIX(
    const double* prices,
    size_t length,
    int period,
    int SIGNAL_PERIOD
) {
    TRIXResult result;
    
    if (length == 0) return result;
    
    result.trix.resize(length);
    result.signal.resize(length);
    result.histogram.resize(length);
    
    // TRIX = 100 * (EMA3 - EMA3_prev) / EMA3_prev
    // 其中 EMA3 = EMA(EMA(EMA(prices)))
    
    std::vector<double> ema1(length);
    std::vector<double> ema2(length);
    std::vector<double> ema3(length);
    
    // 计算三重EMA
    calculate_EMA(prices, length, period, ema1.data());
    calculate_EMA(ema1.data(), length, period, ema2.data());
    calculate_EMA(ema2.data(), length, period, ema3.data());
    
    // 计算EMA3的变化率
    #ifdef HAS_AVX2_SUPPORT
    if (should_use_simd() && length >= 5) {
        Config::instance().recordSIMDCall(length - 1);
        
        __m256d vhundred = _mm256_set1_pd(100.0);
        
        // 第一根TRIX设为0
        result.trix[0] = 0.0;
        
        size_t i = 1;
        for (; i + 3 < length; i += 4) {
            // 加载当前EMA3和前一根EMA3
            __m256d vcurrent = _mm256_loadu_pd(ema3.data() + i);
            __m256d vprev = _mm256_loadu_pd(ema3.data() + i - 1);
            
            // 计算(EMA3 - EMA3_prev) / EMA3_prev * 100
            __m256d vdiff = _mm256_sub_pd(vcurrent, vprev);
            __m256d vtrix = _mm256_mul_pd(
                _mm256_div_pd(vdiff, vprev),
                vhundred
            );
            
            _mm256_storeu_pd(result.trix.data() + i, vtrix);
        }
    } else
    #endif
    {
        // 标量实现
        for (size_t i = 0; i < length; i++) {
            if (i == 0 || ema3[i-1] == 0.0) {
                result.trix[i] = 0.0;
            } else {
                result.trix[i] = 100.0 * (ema3[i] - ema3[i-1]) / ema3[i-1];
            }
        }
    }
    
    // 计算信号线（EMA of TRIX）
    calculate_EMA(result.trix.data(), length, SIGNAL_PERIOD, result.signal.data());
    
    // 计算柱状图（TRIX - Signal）
    sub(result.trix.data(), result.signal.data(), result.histogram.data(), length);
    
    return result;
}

/**
 * ============================================================================
 * KDJ实现 - 随机指标
 * ============================================================================
 */

KDJResult calculate_KDJ(
    const double* high,
    const double* low,
    const double* close,
    size_t length,
    int K_PERIOD,
    int D_PERIOD
) {
    KDJResult result;
    
    if (length == 0) return result;
    
    result.k.resize(length);
    result.d.resize(length);
    result.j.resize(length);
    
    // KDJ是在Stochastic基础上的扩展
    // K = 50 + (StochK - 50) / 3
    // D = SMA(K, D_PERIOD)
    // J = 3*K - 2*D
    
    // 1. 计算Stochastic %K
    std::vector<double> highest(length);
    std::vector<double> lowest(length);
    
    rolling_max(high, length, K_PERIOD, highest.data());
    rolling_min(low, length, K_PERIOD, lowest.data());
    
    // 2. 计算%K
    #ifdef HAS_AVX2_SUPPORT
    if (should_use_simd() && length >= 4) {
        Config::instance().recordSIMDCall(length);
        
        __m256d vhundred = _mm256_set1_pd(100.0);
        
        size_t i = 0;
        for (; i + 3 < length; i += 4) {
            __m256d vh = _mm256_loadu_pd(highest.data() + i);
            __m256d vl = _mm256_loadu_pd(lowest.data() + i);
            __m256d vc = _mm256_loadu_pd(close + i);
            
            // %K = (close - lowest) / (highest - lowest) * 100
            __m256d vrange = _mm256_sub_pd(vh, vl);
            __m256d vk = _mm256_mul_pd(
                _mm256_div_pd(_mm256_sub_pd(vc, vl), vrange),
                vhundred
            );
            
            _mm256_storeu_pd(result.k.data() + i, vk);
        }
    }
    #endif
    
    // 标量回退或剩余部分
    for (size_t i = 0; i < length; i++) {
        double range = highest[i] - lowest[i];
        if (range > 1e-10) {
            result.k[i] = ((close[i] - lowest[i]) / range) * 100.0;
        } else {
            result.k[i] = 50.0;
        }
    }
    
    // 3. 计算%D = SMA(K, D_PERIOD)
    calculate_SMA(result.k.data(), length, D_PERIOD, result.d.data());
    
    // 4. 计算%J = 3*K - 2*D
    #ifdef HAS_AVX2_SUPPORT
    if (should_use_simd() && length >= 4) {
        Config::instance().recordSIMDCall(length);
        
        __m256d vthree = _mm256_set1_pd(3.0);
        __m256d vtwo = _mm256_set1_pd(2.0);
        
        size_t i = 0;
        for (; i + 3 < length; i += 4) {
            __m256d vk = _mm256_loadu_pd(result.k.data() + i);
            __m256d vd = _mm256_loadu_pd(result.d.data() + i);
            
            // J = 3*K - 2*D
            __m256d vj = _mm256_sub_pd(
                _mm256_mul_pd(vk, vthree),
                _mm256_mul_pd(vd, vtwo)
            );
            
            _mm256_storeu_pd(result.j.data() + i, vj);
        }
    }
    #endif
    
    // 标量回退或剩余部分
    for (size_t i = 0; i < length; i++) {
        result.j[i] = 3.0 * result.k[i] - 2.0 * result.d[i];
    }
    
    return result;
}

/**
 * ============================================================================
 * Donchian Channel实现 - 唐奇安通道
 * ============================================================================
 */

DonchianChannelResult calculate_DonchianChannel(
    const double* high,
    const double* low,
    const double* close,
    size_t length,
    int period
) {
    DonchianChannelResult result;
    
    if (length == 0) return result;
    
    result.upper.resize(length);
    result.middle.resize(length);
    result.lower.resize(length);
    
    // 上轨 = N期最高价
    rolling_max(high, length, period, result.upper.data());
    
    // 下轨 = N期最低价
    rolling_min(low, length, period, result.lower.data());
    
    // 中轨 = (上轨 + 下轨 + 收盘价) / 3 或 (上轨 + 下轨) / 2
    // 这里使用 (上轨 + 下轨 + 收盘价) / 3
    
    #ifdef HAS_AVX2_SUPPORT
    if (should_use_simd() && length >= 4) {
        Config::instance().recordSIMDCall(length);
        
        __m256d vthird = _mm256_set1_pd(1.0 / 3.0);
        
        size_t i = 0;
        for (; i + 3 < length; i += 4) {
            __m256d vupper = _mm256_loadu_pd(result.upper.data() + i);
            __m256d vlower = _mm256_loadu_pd(result.lower.data() + i);
            __m256d vclose = _mm256_loadu_pd(close + i);
            
            // 中轨 = (上轨 + 下轨 + 收盘价) / 3
            __m256d vmiddle = _mm256_mul_pd(
                _mm256_add_pd(_mm256_add_pd(vupper, vlower), vclose),
                vthird
            );
            
            _mm256_storeu_pd(result.middle.data() + i, vmiddle);
        }
    }
    #endif
    
    // 标量回退或剩余部分
    for (size_t i = 0; i < length; i++) {
        result.middle[i] = (result.upper[i] + result.lower[i] + close[i]) / 3.0;
    }
    
    return result;
}

} // namespace simd
} // namespace prophet

