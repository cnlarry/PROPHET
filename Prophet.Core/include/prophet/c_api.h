/*
 * ============================================================================
 * 文件名：c_api.h
 * 功能说明：Prophet Core C API 导出层
 * 
 * 为Prophet.Client (C#)提供C风格的导出接口
 * 所有指标计算统一使用TA-Lib，确保与DSL引擎结果一致
 * 
 * 使用方式：
 *   1. C#通过P/Invoke调用这些函数
 *   2. 调用者负责释放返回的内存（调用Prophet_FreeResult）
 * ============================================================================
 */

#ifndef PROPHET_C_API_H
#define PROPHET_C_API_H

#include <stdint.h>  // 添加int64_t类型支持

#ifdef __cplusplus
extern "C" {
#endif

// ============================================================================
// DLL 导出宏定义
// ============================================================================
#ifdef _WIN32
    #ifdef PROPHET_CORE_EXPORTS
        #define PROPHET_API __declspec(dllexport)
    #else
        #define PROPHET_API __declspec(dllimport)
    #endif
#else
    #define PROPHET_API __attribute__((visibility("default")))
#endif

// ============================================================================
// 数据结构
// ============================================================================

/**
 * 指标结果结构（单数组版本）
 * 用于返回单条指标线的数据
 */
typedef struct {
    double* values;      // 指标值数组（由C++分配，调用者负责释放）
    int length;          // 数组长度
    int out_begin;       // 输出起始索引（对应输入数组的偏移）
    char error_message[256];  // 错误信息（如果返回值非0）
} IndicatorResult;

// ============================================================================
// 版本信息
// ============================================================================

/**
 * 获取Prophet Core版本号
 * @return 版本字符串（例如："4.0.0"）
 */
PROPHET_API const char* Prophet_GetVersion();

/**
 * 获取TA-Lib版本号
 * @return TA-Lib版本字符串
 */
PROPHET_API const char* Prophet_GetTALibVersion();

// ============================================================================
// MA 类型指标（移动平均线）
// ============================================================================

/**
 * SMA - 简单移动平均（Simple Moving Average）
 * 
 * 算法：SMA = (P1 + P2 + ... + Pn) / n
 * 
 * @param inReal 输入价格数组
 * @param length 输入数据长度
 * @param period MA周期
 * @param outResult 输出结果（调用者负责释放）
 * @return 0=成功, -1=参数错误, -2=计算失败
 */
PROPHET_API int Prophet_SMA(
    const double* inReal,
    int length,
    int period,
    IndicatorResult* outResult
);

/**
 * EMA - 指数移动平均（Exponential Moving Average）
 * 
 * 算法：EMA = Price(t) * k + EMA(y) * (1-k)
 *       k = 2 / (N + 1)
 * 
 * @param inReal 输入价格数组
 * @param length 输入数据长度
 * @param period EMA周期
 * @param outResult 输出结果
 * @return 0=成功, -1=参数错误, -2=计算失败
 */
PROPHET_API int Prophet_EMA(
    const double* inReal,
    int length,
    int period,
    IndicatorResult* outResult
);

/**
 * WMA - 加权移动平均（Weighted Moving Average）
 * 
 * 算法：WMA = (P1*n + P2*(n-1) + ... + Pn*1) / (n + (n-1) + ... + 1)
 * 
 * @param inReal 输入价格数组
 * @param length 输入数据长度
 * @param period WMA周期
 * @param outResult 输出结果
 * @return 0=成功, -1=参数错误, -2=计算失败
 */
PROPHET_API int Prophet_WMA(
    const double* inReal,
    int length,
    int period,
    IndicatorResult* outResult
);

// ============================================================================
// DEMA - 双重指数移动平均（Double Exponential Moving Average）
// ============================================================================

/**
 * DEMA - 双重指数移动平均
 * 
 * 算法：DEMA = 2 * EMA - EMA(EMA)
 * 特点：比 EMA 更快响应价格变化，减少滞后性
 * 
 * @param inReal 输入价格数组
 * @param length 数组长度
 * @param period 周期（必须 >= 2）
 * @param outResult 输出结果
 * @return 0=成功, -1=参数错误, -2=计算失败
 */
PROPHET_API int Prophet_DEMA(
    const double* inReal,
    int length,
    int period,
    IndicatorResult* outResult
);

// ============================================================================
// TEMA - 三重指数移动平均（Triple Exponential Moving Average）
// ============================================================================

/**
 * TEMA - 三重指数移动平均
 * 
 * 算法：TEMA = 3 * EMA - 3 * EMA(EMA) + EMA(EMA(EMA))
 * 特点：比 DEMA 更快响应价格变化，几乎消除滞后性
 * 
 * @param inReal 输入价格数组
 * @param length 数组长度
 * @param period 周期（必须 >= 2）
 * @param outResult 输出结果
 * @return 0=成功, -1=参数错误, -2=计算失败
 */
PROPHET_API int Prophet_TEMA(
    const double* inReal,
    int length,
    int period,
    IndicatorResult* outResult
);

// ============================================================================
// BOLL - 布林带（Bollinger Bands）
// ============================================================================

/**
 * BBANDS - 布林带
 * 
 * 算法：
 *   中轨 = n周期MA
 *   上轨 = 中轨 + k倍标准差
 *   下轨 = 中轨 - k倍标准差
 * 
 * @param inReal 输入价格数组
 * @param length 输入数据长度
 * @param period 周期
 * @param stdDev 标准差倍数（通常为2.0）
 * @param outUpper 上轨输出
 * @param outMiddle 中轨输出
 * @param outLower 下轨输出
 * @return 0=成功, -1=参数错误, -2=计算失败
 */
PROPHET_API int Prophet_BBANDS(
    const double* inReal,
    int length,
    int period,
    double stdDev,
    IndicatorResult* outUpper,
    IndicatorResult* outMiddle,
    IndicatorResult* outLower
);

// ============================================================================
// Keltner Channel - 肯特纳通道
// ============================================================================

/**
 * Keltner Channel - 肯特纳通道
 * 
 * 算法：
 *   中线 = EMA(close, period)
 *   上轨 = 中线 + multiplier * ATR(period)
 *   下轨 = 中线 - multiplier * ATR(period)
 * 
 * 特点：基于ATR的波动通道，与布林带类似但使用ATR代替标准差
 * 
 * @param inHigh 最高价数组
 * @param inLow 最低价数组
 * @param inClose 收盘价数组
 * @param length 数据长度
 * @param period EMA和ATR周期（默认20）
 * @param multiplier ATR倍数（默认2.0）
 * @param outUpper 上轨输出
 * @param outMiddle 中线输出
 * @param outLower 下轨输出
 * @return 0=成功, -1=参数错误, -2=计算失败
 */
PROPHET_API int Prophet_KELTNER(
    const double* inHigh,
    const double* inLow,
    const double* inClose,
    int length,
    int period,
    double multiplier,
    IndicatorResult* outUpper,
    IndicatorResult* outMiddle,
    IndicatorResult* outLower
);

// ============================================================================
// Ichimoku Cloud - 一目均衡表
// ============================================================================

/**
 * Ichimoku Cloud - 一目均衡表
 * 
 * 算法：
 *   转换线(Tenkan-sen) = (9期最高 + 9期最低) / 2
 *   基准线(Kijun-sen) = (26期最高 + 26期最低) / 2
 *   先行带A(Senkou Span A) = (转换线 + 基准线) / 2
 *   先行带B(Senkou Span B) = (52期最高 + 52期最低) / 2
 *   延迟线(Chikou Span) = 收盘价
 * 
 * 注意：先行带A和B通常需要前移26期，延迟线需要后移26期
 *      这些偏移需要在调用方处理，本函数只返回原始计算值
 * 
 * @param inHigh 最高价数组
 * @param inLow 最低价数组
 * @param inClose 收盘价数组
 * @param length 数据长度
 * @param tenkanPeriod 转换线周期（默认9）
 * @param kijunPeriod 基准线周期（默认26）
 * @param senkouBPeriod 先行带B周期（默认52）
 * @param outTenkan 转换线输出
 * @param outKijun 基准线输出
 * @param outSenkouA 先行带A输出
 * @param outSenkouB 先行带B输出
 * @param outChikou 延迟线输出
 * @return 0=成功, -1=参数错误, -2=计算失败
 */
PROPHET_API int Prophet_ICHIMOKU(
    const double* inHigh,
    const double* inLow,
    const double* inClose,
    int length,
    int tenkanPeriod,
    int kijunPeriod,
    int senkouBPeriod,
    IndicatorResult* outTenkan,
    IndicatorResult* outKijun,
    IndicatorResult* outSenkouA,
    IndicatorResult* outSenkouB,
    IndicatorResult* outChikou
);

// ============================================================================
// SAR - 抛物线转向指标（Parabolic SAR）
// ============================================================================

/**
 * SAR - 抛物线转向指标
 * 
 * 算法：SAR(n) = SAR(n-1) + AF * (EP - SAR(n-1))
 *       AF: 加速因子
 *       EP: 极值点
 * 
 * @param inHigh 最高价数组
 * @param inLow 最低价数组
 * @param length 数据长度
 * @param acceleration 加速因子（默认0.02）
 * @param maximum 最大加速因子（默认0.20）
 * @param outResult 输出结果
 * @return 0=成功, -1=参数错误, -2=计算失败
 */
PROPHET_API int Prophet_SAR(
    const double* inHigh,
    const double* inLow,
    int length,
    double acceleration,
    double maximum,
    IndicatorResult* outResult
);

// ============================================================================
// TRIX - 三重指数平滑移动平均（Triple Exponential Average）
// ============================================================================

/**
 * TRIX - 三重指数平滑移动平均
 * 
 * 算法：
 *   EMA1 = EMA(Price, n)
 *   EMA2 = EMA(EMA1, n)
 *   EMA3 = EMA(EMA2, n)
 *   TRIX = (EMA3 - EMA3昨日) / EMA3昨日 * 100
 * 
 * @param inReal 输入价格数组
 * @param length 输入数据长度
 * @param period TRIX周期
 * @param outResult 输出结果
 * @return 0=成功, -1=参数错误, -2=计算失败
 */
PROPHET_API int Prophet_TRIX(
    const double* inReal,
    int length,
    int period,
    IndicatorResult* outResult
);

// ============================================================================
// VWAP - 成交量加权平均价（Volume Weighted Average Price）
// ============================================================================

/**
 * VWAP - 成交量加权平均价
 * 
 * 算法：VWAP = Σ(典型价格 × 成交量) / Σ成交量
 *       典型价格 = (最高价 + 最低价 + 收盘价) / 3
 * 
 * 注意：TA-Lib没有内置VWAP，这里使用自定义实现
 * 
 * @param inHigh 最高价数组
 * @param inLow 最低价数组
 * @param inClose 收盘价数组
 * @param inVolume 成交量数组
 * @param length 数据长度
 * @param period 周期（0表示累计VWAP）
 * @param outResult 输出结果
 * @return 0=成功, -1=参数错误, -2=计算失败
 */
PROPHET_API int Prophet_VWAP(
    const double* inHigh,
    const double* inLow,
    const double* inClose,
    const double* inVolume,
    int length,
    int period,
    IndicatorResult* outResult
);

// ============================================================================
// ATR - 平均真实波幅（Average True Range）
// ============================================================================

/**
 * ATR - 平均真实波幅
 * 
 * 算法：
 *   TR = Max(H-L, |H-PC|, |L-PC|)
 *   ATR = MA(TR, n)
 * 
 * @param inHigh 最高价数组
 * @param inLow 最低价数组
 * @param inClose 收盘价数组
 * @param length 数据长度
 * @param period ATR周期
 * @param outResult 输出结果
 * @return 0=成功, -1=参数错误, -2=计算失败
 */
PROPHET_API int Prophet_ATR(
    const double* inHigh,
    const double* inLow,
    const double* inClose,
    int length,
    int period,
    IndicatorResult* outResult
);

// ============================================================================
// MACD - 移动平均收敛发散指标
// ============================================================================

/**
 * MACD - 移动平均收敛发散指标
 * 
 * 算法：
 *   DIF = EMA(Close, 12) - EMA(Close, 26)
 *   DEA = EMA(DIF, 9)
 *   Histogram = DIF - DEA
 * 
 * @param inReal 输入价格数组
 * @param length 输入数据长度
 * @param fastPeriod 快线周期（默认12）
 * @param slowPeriod 慢线周期（默认26）
 * @param signalPeriod 信号线周期（默认9）
 * @param outMACD MACD线输出
 * @param outSignal 信号线输出
 * @param outHistogram 柱状图输出
 * @return 0=成功, -1=参数错误, -2=计算失败
 */
PROPHET_API int Prophet_MACD(
    const double* inReal,
    int length,
    int fastPeriod,
    int slowPeriod,
    int signalPeriod,
    IndicatorResult* outMACD,
    IndicatorResult* outSignal,
    IndicatorResult* outHistogram
);

// ============================================================================
// RSI - 相对强弱指标（Relative Strength Index）
// ============================================================================

/**
 * RSI - 相对强弱指标
 * 
 * 算法：RSI = 100 - (100 / (1 + RS))
 *       RS = 平均涨幅 / 平均跌幅
 * 
 * @param inReal 输入价格数组
 * @param length 输入数据长度
 * @param period RSI周期（默认14）
 * @param outResult 输出结果
 * @return 0=成功, -1=参数错误, -2=计算失败
 */
PROPHET_API int Prophet_RSI(
    const double* inReal,
    int length,
    int period,
    IndicatorResult* outResult
);

// ============================================================================
// MFI - 资金流量指标（Money Flow Index）
// ============================================================================

/**
 * MFI - 资金流量指标
 * 
 * 算法：类似RSI，但考虑成交量
 * 特点：0-100之间，>80超买，<20超卖
 * 
 * @param inHigh 最高价数组
 * @param inLow 最低价数组
 * @param inClose 收盘价数组
 * @param inVolume 成交量数组
 * @param length 数据长度
 * @param period MFI周期（默认14）
 * @param outResult 输出结果
 * @return 0=成功, -1=参数错误, -2=计算失败
 */
PROPHET_API int Prophet_MFI(
    const double* inHigh,
    const double* inLow,
    const double* inClose,
    const double* inVolume,
    int length,
    int period,
    IndicatorResult* outResult
);

// ============================================================================
// OBV - 能量潮（On Balance Volume）
// ============================================================================

/**
 * OBV - 能量潮
 * 
 * 算法：累计成交量（涨加跌减）
 * 特点：趋势确认指标
 * 
 * @param inReal 收盘价数组
 * @param inVolume 成交量数组
 * @param length 数据长度
 * @param outResult 输出结果
 * @return 0=成功, -1=参数错误, -2=计算失败
 */
PROPHET_API int Prophet_OBV(
    const double* inReal,
    const double* inVolume,
    int length,
    IndicatorResult* outResult
);

// ============================================================================
// STOCH - KDJ随机指标（Stochastic Oscillator）
// ============================================================================

/**
 * STOCH - KDJ随机指标
 * 
 * 算法：
 *   K = (Close - Lowest Low) / (Highest High - Lowest Low) * 100
 *   D = MA(K)
 * 
 * @param inHigh 最高价数组
 * @param inLow 最低价数组
 * @param inClose 收盘价数组
 * @param length 数据长度
 * @param fastK_Period FastK周期（默认5）
 * @param slowK_Period SlowK周期（默认3）
 * @param slowD_Period SlowD周期（默认3）
 * @param outSlowK K线输出
 * @param outSlowD D线输出
 * @return 0=成功, -1=参数错误, -2=计算失败
 */
PROPHET_API int Prophet_STOCH(
    const double* inHigh,
    const double* inLow,
    const double* inClose,
    int length,
    int fastK_Period,
    int slowK_Period,
    int slowD_Period,
    IndicatorResult* outSlowK,
    IndicatorResult* outSlowD
);

// ============================================================================
// STOCHRSI - 随机RSI（Stochastic RSI）
// ============================================================================

/**
 * STOCHRSI - 随机RSI
 * 
 * 算法：对RSI应用随机指标计算
 * 特点：更灵敏的超买超卖信号
 * 
 * @param inReal 收盘价数组
 * @param length 数据长度
 * @param period RSI周期（默认14）
 * @param fastK_Period FastK周期（默认5）
 * @param fastD_Period FastD周期（默认3）
 * @param outFastK FastK线输出
 * @param outFastD FastD线输出
 * @return 0=成功, -1=参数错误, -2=计算失败
 */
PROPHET_API int Prophet_STOCHRSI(
    const double* inReal,
    int length,
    int period,
    int fastK_Period,
    int fastD_Period,
    IndicatorResult* outFastK,
    IndicatorResult* outFastD
);

// ============================================================================
// CCI - 商品通道指标（Commodity Channel Index）
// ============================================================================

/**
 * CCI - 商品通道指标
 * 
 * 算法：CCI = (TP - SMA(TP)) / (0.015 * MD)
 *       TP = (High + Low + Close) / 3
 * 
 * @param inHigh 最高价数组
 * @param inLow 最低价数组
 * @param inClose 收盘价数组
 * @param length 数据长度
 * @param period CCI周期（默认14）
 * @param outResult 输出结果
 * @return 0=成功, -1=参数错误, -2=计算失败
 */
PROPHET_API int Prophet_CCI(
    const double* inHigh,
    const double* inLow,
    const double* inClose,
    int length,
    int period,
    IndicatorResult* outResult
);

// ============================================================================
// DMI - 趋向指标（Directional Movement Index）
// ============================================================================

/**
 * DMI - 趋向指标
 * 
 * 算法：
 *   ADX = 趋势强度
 *   +DI = 上升方向力量
 *   -DI = 下降方向力量
 * 
 * @param inHigh 最高价数组
 * @param inLow 最低价数组
 * @param inClose 收盘价数组
 * @param length 数据长度
 * @param period DMI周期（默认14）
 * @param outADX ADX输出
 * @param outPlusDI +DI输出
 * @param outMinusDI -DI输出
 * @return 0=成功, -1=参数错误, -2=计算失败
 */
PROPHET_API int Prophet_DMI(
    const double* inHigh,
    const double* inLow,
    const double* inClose,
    int length,
    int period,
    IndicatorResult* outADX,
    IndicatorResult* outPlusDI,
    IndicatorResult* outMinusDI
);

// ============================================================================
// WILLR - 威廉指标（Williams %R）
// ============================================================================

/**
 * WILLR - 威廉指标
 * 
 * 算法：%R = (Highest High - Close) / (Highest High - Lowest Low) * -100
 * 特点：-100到0之间，-20以上超买，-80以下超卖
 * 
 * @param inHigh 最高价数组
 * @param inLow 最低价数组
 * @param inClose 收盘价数组
 * @param length 数据长度
 * @param period WR周期（默认14）
 * @param outResult 输出结果
 * @return 0=成功, -1=参数错误, -2=计算失败
 */
PROPHET_API int Prophet_WILLR(
    const double* inHigh,
    const double* inLow,
    const double* inClose,
    int length,
    int period,
    IndicatorResult* outResult
);

// ============================================================================
// CMF - 蔡金资金流量（Chaikin Money Flow）
// ============================================================================

/**
 * CMF - 蔡金资金流量
 * 
 * 算法：基于A/D线的振荡器
 * 特点：>0买盘强，<0卖盘强
 * 
 * @param inHigh 最高价数组
 * @param inLow 最低价数组
 * @param inClose 收盘价数组
 * @param inVolume 成交量数组
 * @param length 数据长度
 * @param period CMF周期（默认20）
 * @param outResult 输出结果
 * @return 0=成功, -1=参数错误, -2=计算失败
 */
PROPHET_API int Prophet_CMF(
    const double* inHigh,
    const double* inLow,
    const double* inClose,
    const double* inVolume,
    int length,
    int period,
    IndicatorResult* outResult
);

// ============================================================================
// ROC - 变动率指标（Rate of Change）
// ============================================================================

/**
 * ROC - 变动率指标
 * 
 * 算法：ROC = (Price - Price[n]) / Price[n] * 100
 * 特点：价格变化百分比
 * 
 * @param inReal 收盘价数组
 * @param length 数据长度
 * @param period ROC周期（默认10）
 * @param outResult 输出结果
 * @return 0=成功, -1=参数错误, -2=计算失败
 */
PROPHET_API int Prophet_ROC(
    const double* inReal,
    int length,
    int period,
    IndicatorResult* outResult
);

// ============================================================================
// EMV - 简易波动指标（Ease of Movement Value）
// ============================================================================

/**
 * EMV - 简易波动指标
 * 
 * 算法：EMV = 价格变动 / 成交量
 * 特点：价格移动容易程度
 * 
 * @param inHigh 最高价数组
 * @param inLow 最低价数组
 * @param inVolume 成交量数组
 * @param length 数据长度
 * @param period EMV周期（默认14）
 * @param outResult 输出结果
 * @return 0=成功, -1=参数错误, -2=计算失败
 */
PROPHET_API int Prophet_EMV(
    const double* inHigh,
    const double* inLow,
    const double* inVolume,
    int length,
    int period,
    IndicatorResult* outResult
);

// ============================================================================
// MTM - 动量指标（Momentum）
// ============================================================================

/**
 * MTM - 动量指标
 * 
 * 算法：MTM = Close - Close[n]
 * 特点：价格绝对变化
 * 
 * @param inReal 收盘价数组
 * @param length 数据长度
 * @param period MTM周期（默认10）
 * @param outResult 输出结果
 * @return 0=成功, -1=参数错误, -2=计算失败
 */
PROPHET_API int Prophet_MTM(
    const double* inReal,
    int length,
    int period,
    IndicatorResult* outResult
);

// ============================================================================
// CMO - 钱德动量摆动指标（Chande Momentum Oscillator）
// ============================================================================

/**
 * CMO - 钱德动量摆动指标
 * 
 * 算法：CMO = (Su - Sd) / (Su + Sd) * 100
 * 特点：-100到+100，类似RSI但不压缩范围
 * 
 * @param inReal 收盘价数组
 * @param length 数据长度
 * @param period CMO周期（默认14）
 * @param outResult 输出结果
 * @return 0=成功, -1=参数错误, -2=计算失败
 */
PROPHET_API int Prophet_CMO(
    const double* inReal,
    int length,
    int period,
    IndicatorResult* outResult
);

// ============================================================================
// AROON - 阿隆指标（Aroon Indicator）
// ============================================================================

/**
 * AROON - 阿隆指标
 * 
 * 算法：
 *   Aroon Up = ((period - 最高点距离) / period) * 100
 *   Aroon Down = ((period - 最低点距离) / period) * 100
 * 
 * @param inHigh 最高价数组
 * @param inLow 最低价数组
 * @param length 数据长度
 * @param period Aroon周期（默认25）
 * @param outAroonDown Aroon Down输出
 * @param outAroonUp Aroon Up输出
 * @return 0=成功, -1=参数错误, -2=计算失败
 */
PROPHET_API int Prophet_AROON(
    const double* inHigh,
    const double* inLow,
    int length,
    int period,
    IndicatorResult* outAroonDown,
    IndicatorResult* outAroonUp
);

// ============================================================================
// 内存管理
// ============================================================================

/**
 * 释放指标结果内存
 * 
 * 必须调用此函数释放Prophet_XXX函数返回的内存
 * 
 * @param result 要释放的结果结构
 */
PROPHET_API void Prophet_FreeResult(IndicatorResult* result);

/**
 * 批量释放多个结果
 * 
 * @param results 结果数组
 * @param count 数组长度
 */
PROPHET_API void Prophet_FreeResults(IndicatorResult* results, int count);

// ============================================================================
// 错误处理
// ============================================================================

/**
 * 获取最后一次错误信息
 * @return 错误信息字符串
 */
PROPHET_API const char* Prophet_GetLastError();

// ============================================================================
// 策略引擎API
// ============================================================================

/**
 * 策略引擎信号结构（C#端使用）
 * 
 * v11.0: 新增调试信息支持
 * - trend: 趋势判断（BULLISH/BEARISH/NEUTRAL）
 * - PatternsJson: K线形态数组（JSON格式）
 * - DebugJson: 调试信息数组（JSON格式，包含规则评估详情）
 */
typedef struct {
    // === 基本信息 ===
    char Action[20];              // 信号动作："BUY", "SELL", "HOLD"
    double Confidence;            // 信号置信度 (0.0 - 1.0)
    char Trend[20];               // v11.0 新增：趋势（BULLISH/BEARISH/NEUTRAL）
    char Reason[512];             // 信号生成原因
    
    // === 止损止盈 ===
    double TakeProfit;            // 止盈价格
    double StopLoss;              // 止损价格
    
    // === 数据快照（JSON格式） ===
    char ConfigsJson[2048];       // 配置参数快照
    char IndicatorsJson[4096];    // 指标快照（已废弃）
    char IndicatorSnapshotsJson[8192];  // 结构化指标快照（新增）
    
    // === v11.0 调试信息（JSON格式） ===
    char DebugJson[8192];         // 调试信息数组（规则评估过程、决策详情）
    
    // === v4.0 K线数据快照（JSON格式） ===
    char KlinesJson[8192];        // K线数据快照（每个时间框架的当前K线）
} NativeSignal;

/**
 * 环境变量值结构（用于传递环境变量）
 */
typedef struct {
    double Value;                 // 值
    int Type;                     // 类型: 0=Number, 1=Bool, 2=String (暂不支持)
} NativeEnvValue;

/**
 * 创建策略引擎实例
 * 
 * @param dsl_code DSL策略代码字符串
 * @param params_json 参数配置JSON字符串（可选，传NULL使用默认参数）
 * @return 引擎实例指针，失败返回NULL
 */
PROPHET_API void* Prophet_CreateEngine(
    const char* dsl_code,
    const char* params_json
);

/**
 * 销毁策略引擎实例
 * 
 * @param engine 引擎实例指针
 */
PROPHET_API void Prophet_DestroyEngine(void* engine);

/**
 * v10.0 设置K线数据（必须指定时间框架）
 * 
 * 用于初始化时设置完整的300根K线窗口
 * 注意：如果传入超过300根，只保留最后300根
 * 
 * @param engine 引擎实例指针
 * @param timeframe 时间框架（如"5m", "1h", "1d"）
 * @param open 开盘价数组
 * @param high 最高价数组
 * @param low 最低价数组
 * @param close 收盘价数组
 * @param volume 成交量数组
 * @param open_time 开盘时间数组（UTC毫秒）
 * @param close_time 收盘时间数组（UTC毫秒）
 * @param count K线数量（建议300根）
 * @return 0=成功, -1=参数错误, -2=设置失败
 * 
 * 示例（C#）：
 *   double[] open = ...;  // 300 根K线数据
 *   ...
 *   Prophet_SetKlines(engine, "5m", open, high, low, close, volume, 
 *                     open_time, close_time, 300);
 *   Prophet_SetKlines(engine, "1h", open_1h, high_1h, ..., 300);  // 注入多个时间框架
 */
PROPHET_API int Prophet_SetKlines(
    void* engine,
    const char* timeframe,      // v10.0: 新增，必需参数
    const double* open,
    const double* high,
    const double* low,
    const double* close,
    const double* volume,
    const int64_t* open_time,
    const int64_t* close_time,
    int count
);

/**
 * v10.0 增量追加单根K线（高性能，回测循环使用）
 * 
 * 自动维护300根窗口：追加新K线，丢弃最旧的
 * 注意：必须先调用 Prophet_SetKlines() 初始化窗口
 * 
 * @param engine 引擎实例指针
 * @param timeframe 时间框架
 * @param open 开盘价
 * @param high 最高价
 * @param low 最低价
 * @param close 收盘价
 * @param volume 成交量
 * @param open_time 开盘时间（UTC毫秒）
 * @param close_time 收盘时间（UTC毫秒）
 * @return 0=成功, -1=参数错误, -2=追加失败
 * 
 * 性能优势：相比全量设置快300倍，内存分配减少99.7%
 * 
 * 示例（C# 回测循环）：
 *   for (int i = 300; i < totalCandles; i++) {
 *       Prophet_AppendKline(engine, "5m", 
 *           candles[i].open, candles[i].high, candles[i].low,
 *           candles[i].close, candles[i].volume,
 *           candles[i].open_time, candles[i].close_time);
 *       
 *       Prophet_GetSignal(engine, candles[i].close, candles[i].time, &signal);
 *   }
 */
PROPHET_API int Prophet_AppendKline(
    void* engine,
    const char* timeframe,
    double open,
    double high,
    double low,
    double close,
    double volume,
    int64_t open_time,
    int64_t close_time
);

/**
 * v10.0 增量追加多根K线（批量追加，处理跳跃场景）
 * 
 * @param engine 引擎实例指针
 * @param timeframe 时间框架
 * @param open 开盘价数组
 * @param high 最高价数组
 * @param low 最低价数组
 * @param close 收盘价数组
 * @param volume 成交量数组
 * @param open_time 开盘时间数组（UTC毫秒）
 * @param close_time 收盘时间数组（UTC毫秒）
 * @param count K线数量
 * @return 0=成功, -1=参数错误, -2=追加失败
 */
PROPHET_API int Prophet_AppendKlines(
    void* engine,
    const char* timeframe,
    const double* open,
    const double* high,
    const double* low,
    const double* close,
    const double* volume,
    const int64_t* open_time,
    const int64_t* close_time,
    int count
);

/**
 * 设置恐惧与贪婪指数序列数据
 * 
 * @param engine 引擎实例指针
 * @param date_timestamps 日期时间戳数组（UTC秒，降序排列，最新在前）
 * @param values 指数值数组（0-100）
 * @param classifications 分类标签数组（字符串）
 * @param count 数据条数
 * @return 0=成功, -1=参数错误, -2=设置失败
 * 
 * 示例（C#）：
 *   long[] dates = { 1700000000, 1699914400, ... };
 *   int[] values = { 75, 70, ... };
 *   string[] classifications = { "Greed", "Fear", ... };
 *   Prophet_SetFearGreedSeries(engine, dates, values, classifications, 365);
 */
PROPHET_API int Prophet_SetFearGreedSeries(
    void* engine,
    const int64_t* date_timestamps,
    const int* values,
    const char** classifications,
    int count
);

/**
 * 设置资金费率序列数据
 * 
 * @param engine 引擎实例指针
 * @param timestamps 时间戳数组（UTC毫秒，降序排列，最新在前）
 * @param values 资金费率值数组（浮点数）
 * @param count 数据数量
 * @return 0=成功, -1=参数错误, -2=设置失败
 * 
 * 示例（C#）：
 *   long[] timestamps = { 1700000000000, 1699992000000, ... };
 *   double[] values = { 0.0001, -0.0002, ... };
 *   Prophet_SetFundingRateSeries(engine, timestamps, values, 720);
 */
PROPHET_API int Prophet_SetFundingRateSeries(
    void* engine,
    const int64_t* timestamps,
    const double* values,
    int count
);

/**
 * 设置多空比序列数据
 * 
 * @param engine 引擎实例指针
 * @param timestamps 时间戳数组（UTC秒，降序排列，最新在前）
 * @param long_ratios 多头比例数组（0~1）
 * @param short_ratios 空头比例数组（0~1）
 * @param ratios 多空比率数组（long/short）
 * @param count 数据数量
 * @return 0=成功, -1=参数错误, -2=设置失败
 * 
 * 示例（C#）：
 *   long[] timestamps = { 1700000000, 1699992000, ... };
 *   double[] longRatios = { 0.6, 0.55, ... };
 *   double[] shortRatios = { 0.4, 0.45, ... };
 *   double[] ratios = { 1.5, 1.22, ... };
 *   Prophet_SetLongShortRatioSeries(engine, timestamps, longRatios, shortRatios, ratios, 100);
 */
PROPHET_API int Prophet_SetLongShortRatioSeries(
    void* engine,
    const int64_t* timestamps,
    const double* long_ratios,
    const double* short_ratios,
    const double* ratios,
    int count
);

/**
 * 生成交易信号
 * 
 * @param engine 引擎实例指针
 * @param current_price 当前价格
 * @param current_time 当前时间戳（UTC毫秒）
 * @param out_signal 输出信号结构
 * @return 0=成功, -1=参数错误, -2=计算失败
 */
PROPHET_API int Prophet_GetSignal(
    void* engine,
    double current_price,
    int64_t current_time,
    NativeSignal* out_signal
);

/**
 * 获取规则数量（调试用）
 * 
 * @param engine 引擎实例指针
 * @return 规则数量，失败返回-1
 */
PROPHET_API int Prophet_GetRuleCount(void* engine);

/**
 * 获取规则字符串（调试用）
 * 
 * @param engine 引擎实例指针
 * @return 规则字符串（内部缓存，不需要释放）
 */
PROPHET_API const char* Prophet_GetRulesString(void* engine);

/**
 * 获取最后一次策略引擎错误信息
 * @return 错误信息字符串
 */
PROPHET_API const char* Prophet_GetLastStrategyError();

/**
 * 验证DSL代码（只进行词法和语法分析，不创建完整引擎）
 * 
 * 用于在保存策略时验证代码是否可以被核心引擎正确解析
 * 
 * @param dsl_code DSL策略代码字符串
 * @param error_buffer 输出：错误信息缓冲区（如果验证失败）
 * @param buffer_size 输入：缓冲区大小
 * @return 0=验证通过, -1=词法错误, -2=语法错误, -3=其他错误
 * 
 * 错误信息格式：[类型] 第X行, 第Y列: 错误描述
 * 
 * 示例（C#）：
 *   StringBuilder errorBuffer = new StringBuilder(1024);
 *   int result = Prophet_ValidateDSL(dslCode, errorBuffer, errorBuffer.Capacity);
 *   if (result != 0) {
 *       string error = errorBuffer.ToString();
 *       // 解析错误信息...
 *   }
 */
PROPHET_API int Prophet_ValidateDSL(
    const char* dsl_code,
    char* error_buffer,
    int buffer_size
);

// ============================================================================
// 日志系统API
// ============================================================================

/**
 * 日志级别枚举
 */
typedef enum {
    PROPHET_LOG_TRACE = 0,
    PROPHET_LOG_DEBUG = 1,
    PROPHET_LOG_INFO  = 2,
    PROPHET_LOG_WARN  = 3,
    PROPHET_LOG_ERROR = 4,
    PROPHET_LOG_FATAL = 5,
    PROPHET_LOG_OFF   = 6
} ProphetLogLevel;

/**
 * 设置日志级别
 * 
 * @param level 日志级别
 * 
 * 示例（C#）：
 *   Prophet_SetLogLevel(PROPHET_LOG_DEBUG);  // 设置为DEBUG级别
 */
PROPHET_API void Prophet_SetLogLevel(ProphetLogLevel level);

/**
 * 获取当前日志级别
 * 
 * @return 当前日志级别
 */
PROPHET_API ProphetLogLevel Prophet_GetLogLevel();

/**
 * 启用/禁用控制台输出
 * 
 * @param enabled 1=启用, 0=禁用
 */
PROPHET_API void Prophet_SetConsoleOutput(int enabled);

/**
 * 启用/禁用文件输出
 * 
 * @param enabled 1=启用, 0=禁用
 */
PROPHET_API void Prophet_SetFileOutput(int enabled);

/**
 * 设置日志文件路径
 * 
 * @param filepath 日志文件路径
 * @return 0=成功, -1=失败
 * 
 * 示例（C#）：
 *   Prophet_SetLogFile("C:/logs/prophet_core.log");
 */
PROPHET_API int Prophet_SetLogFile(const char* filepath);

/**
 * 关闭日志文件
 */
PROPHET_API void Prophet_CloseLogFile();

/**
 * 写入日志（通用方法）
 * 
 * @param level 日志级别
 * @param module 模块名称
 * @param message 日志消息
 * 
 * 示例（C#）：
 *   Prophet_Log(PROPHET_LOG_INFO, "Engine", "引擎初始化成功");
 */
PROPHET_API void Prophet_Log(ProphetLogLevel level, const char* module, const char* message);

/**
 * 刷新日志缓冲区
 */
PROPHET_API void Prophet_FlushLog();

// ============================================================================
// K线转换工具（供 C# 客户端绘图使用）
// ============================================================================

/**
 * K线数据结构（用于转换）
 */
typedef struct {
    double open;
    double high;
    double low;
    double close;
    double volume;
    int64_t open_time;    // UTC毫秒
    int64_t close_time;   // UTC毫秒
} NativeKline;

/**
 * K线转换结果
 */
typedef struct {
    NativeKline* klines;  // K线数组（调用者负责释放）
    int length;           // 数组长度
    char error_message[256];
} KlineConversionResult;

/**
 * 转换K线时间框架
 * 
 * 用于 C# 客户端绘制不同时间框架的 K线图
 * 
 * @param klines 源K线数据数组
 * @param count K线数量
 * @param from_minutes 源时间框架（分钟）
 * @param to_minutes 目标时间框架（分钟）
 * @param period 返回最近N根（0=全部）
 * @param out_result 输出结果
 * @return 0=成功, -1=参数错误, -2=转换失败
 * 
 * 示例（C#）：
 *   NativeKline[] klines1m = ...; // 1分钟数据
 *   KlineConversionResult result;
 *   Prophet_ConvertKlines(klines1m, klines1m.Length, 1, 5, 0, &result);
 *   // result.klines 包含5分钟K线数据
 *   Prophet_FreeKlineResult(&result);
 */
PROPHET_API int Prophet_ConvertKlines(
    const NativeKline* klines,
    int count,
    int from_minutes,
    int to_minutes,
    int period,
    KlineConversionResult* out_result
);

/**
 * 释放K线转换结果
 * 
 * @param result 要释放的结果
 */
PROPHET_API void Prophet_FreeKlineResult(KlineConversionResult* result);

/**
 * 时间框架字符串转分钟数
 * 
 * @param timeframe 时间框架字符串（如 "1m", "5m", "1h", "1d"）
 * @return 对应的分钟数，失败返回-1
 * 
 * 示例：
 *   int minutes = Prophet_TimeframeToMinutes("1h");  // 返回 60
 */
PROPHET_API int Prophet_TimeframeToMinutes(const char* timeframe);

/**
 * 分钟数转时间框架字符串
 * 
 * @param minutes 分钟数
 * @param out_buffer 输出缓冲区
 * @param buffer_size 缓冲区大小
 * @return 0=成功, -1=缓冲区太小
 * 
 * 示例：
 *   char buffer[16];
 *   Prophet_MinutesToTimeframe(60, buffer, sizeof(buffer));  // buffer="1h"
 */
PROPHET_API int Prophet_MinutesToTimeframe(
    int minutes,
    char* out_buffer,
    int buffer_size
);

#ifdef __cplusplus
}
#endif

#endif // PROPHET_C_API_H

