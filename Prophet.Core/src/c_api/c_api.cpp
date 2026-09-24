/*
 * ============================================================================
 * 文件名：c_api.cpp
 * 功能说明：Prophet Core C API 实现
 * 
 * 统一指标计算架构：
 * - 所有指标函数统一调用CalculateIndicatorUnified()
 * - 通过IndicatorRegistry动态调用Calculator类
 * - 确保与DSL引擎计算结果完全一致
 * - 支持序列结果和偏移量访问
 * ============================================================================
 */

#include "prophet/c_api.h"
#include "prophet/tools/kline_converter.hpp"
#include "prophet/indicators/calculator.hpp"
#include "prophet/indicators/registry.hpp"
#include "prophet/common/types.hpp"
#include <ta_libc.h>
#include <cstring>
#include <cstdio>
#include <cstdarg>
#include <algorithm>
#include <vector>
#include <map>

// 使用命名空间别名避免冲突
using CppIndicatorResult = prophet::IndicatorResult;

// ============================================================================
// 全局初始化和错误信息
// ============================================================================

namespace {
    // TA-Lib 全局初始化
    struct TALibInitializer {
        TALibInitializer() {
            TA_Initialize();
        }
        ~TALibInitializer() {
            TA_Shutdown();
        }
    };
    static TALibInitializer g_talib_init;

    // 最后一次错误信息
    thread_local char g_last_error[512] = {0};

    // 设置错误信息
    void SetLastError(const char* format, ...) {
        va_list args;
        va_start(args, format);
        vsnprintf(g_last_error, sizeof(g_last_error), format, args);
        va_end(args);
    }
}

// ============================================================================
// 版本信息
// ============================================================================

PROPHET_API const char* Prophet_GetVersion() {
    return "4.0.0";
}

PROPHET_API const char* Prophet_GetTALibVersion() {
    return TA_GetVersionString();
}

// ============================================================================
// 辅助函数
// ============================================================================

// 参数校验
static bool ValidateParams(const double* inReal, int length, int period, IndicatorResult* outResult) {
    if (!inReal) {
        SetLastError("Input array is null");
        return false;
    }
    if (length <= 0) {
        SetLastError("Invalid length: %d", length);
        return false;
    }
    if (period <= 0) {
        SetLastError("Invalid period: %d", period);
        return false;
    }
    if (length < period) {
        SetLastError("Length (%d) must be >= period (%d)", length, period);
        return false;
    }
    if (!outResult) {
        SetLastError("Output result pointer is null");
        return false;
    }
    return true;
}

// 分配结果内存
static bool AllocateResult(IndicatorResult* result, int outBegin, int outNBElement) {
    result->values = new (std::nothrow) double[outNBElement];
    if (!result->values) {
        SetLastError("Memory allocation failed");
        return false;
    }
    result->length = outNBElement;
    result->out_begin = outBegin;
    result->error_message[0] = '\0';
    return true;
}

// ============================================================================
// 统一指标计算辅助函数（调用Calculator类）
// ============================================================================

/**
 * 将C++的IndicatorResult转换为C API的IndicatorResult
 * @param cpp_result C++的IndicatorResult（包含序列数据）
 * @param field_name 要提取的字段名（如"value", "macd", "histogram"等）
 * @param input_length 输入K线数据长度（用于计算out_begin）
 * @param c_result C API的IndicatorResult输出
 * @return 0=成功, -1=字段不存在, -2=内存分配失败
 */
static int ConvertIndicatorResultToC(
    const CppIndicatorResult& cpp_result,
    const std::string& field_name,
    int input_length,
    IndicatorResult* c_result
) {
    if (!c_result) {
        SetLastError("Output result pointer is null");
        return -1;
    }

    // 检查字段是否存在
    if (!cpp_result.has(field_name)) {
        SetLastError("Field '%s' not found in indicator result", field_name.c_str());
        snprintf(c_result->error_message, sizeof(c_result->error_message),
                "Field '%s' not found", field_name.c_str());
        return -1;
    }

    // 获取字段序列
    const auto& series = cpp_result.field_series.at(field_name);
    if (series.empty()) {
        SetLastError("Field '%s' has empty series", field_name.c_str());
        snprintf(c_result->error_message, sizeof(c_result->error_message),
                "Field '%s' has empty series", field_name.c_str());
        return -1;
    }

    // 计算out_begin：TA-Lib风格的输出起始索引
    // 根据TA-Lib标准：outBegIdx + outNbElement = input_length
    // 所以：out_begin = input_length - series.size()
    // 如果序列长度小于输入长度，说明前面有NaN填充（数据不足）
    // out_begin表示从输入数组的哪个索引开始有有效数据
    int out_begin = static_cast<int>(input_length) - static_cast<int>(series.size());
    if (out_begin < 0) {
        // 如果序列长度大于输入长度（不应该发生），设为0
        out_begin = 0;
    }
    
    // 🔑 关键验证：out_begin + series.size()应该等于input_length（TA-Lib标准）
    // 如果不等于，说明数据对齐有问题，需要调试
    int expected_end = out_begin + static_cast<int>(series.size());
    if (expected_end != input_length) {
        // 这种情况不应该发生，但为了安全起见，我们记录错误
        SetLastError("Data alignment error: out_begin(%d) + series.size(%zu) != input_length(%d)", 
                     out_begin, series.size(), input_length);
    }

    // 分配内存并复制数据
    int out_nb_element = static_cast<int>(series.size());
    if (!AllocateResult(c_result, out_begin, out_nb_element)) {
        return -2;
    }

    // 将Value序列转换为double数组
    for (int i = 0; i < out_nb_element; ++i) {
        c_result->values[i] = series[i].toNumber();
    }

    return 0;
}

/**
 * 通用指标计算函数（通过IndicatorRegistry动态调用）
 * 这是统一架构的核心：所有指标都通过这个函数计算
 * 
 * @param indicator_name 指标名称（如"RSI", "MACD"等）
 * @param close 收盘价数组
 * @param high 最高价数组（可为NULL）
 * @param low 最低价数组（可为NULL）
 * @param volume 成交量数组（可为NULL）
 * @param length 数据长度
 * @param params 参数字典（key-value对，如"PERIOD"=14，键名须与 indicator_registrations.cpp 一致）
 * @param param_count 参数数量
 * @param field_name 要提取的字段名（如"value", "macd"等）
 * @param outResult 输出结果
 * @return 0=成功, -1=参数错误, -2=计算失败
 */
static int CalculateIndicatorUnified(
    const char* indicator_name,
    const double* close,
    const double* high,
    const double* low,
    const double* volume,
    int length,
    const char** param_keys,
    const double* param_values,
    int param_count,
    const char* field_name,
    IndicatorResult* outResult
) {
    if (!indicator_name || !close || length <= 0 || !outResult) {
        SetLastError("Invalid input parameters");
        return -1;
    }

    try {
        // 获取IndicatorRegistry实例
        auto& registry = prophet::indicators::IndicatorRegistry::getInstance();
        
        // 检查指标是否已注册
        if (!registry.hasIndicator(indicator_name)) {
            SetLastError("Indicator '%s' not registered", indicator_name);
            snprintf(outResult->error_message, sizeof(outResult->error_message),
                    "Indicator '%s' not registered", indicator_name);
            return -1;
        }

        // 准备K线数据向量
        std::vector<double> close_vec(close, close + length);
        std::vector<double> high_vec;
        std::vector<double> low_vec;
        std::vector<double> volume_vec;
        
        if (high) {
            high_vec.assign(high, high + length);
        } else {
            high_vec.assign(length, 0.0);
        }
        
        if (low) {
            low_vec.assign(low, low + length);
        } else {
            low_vec.assign(length, 0.0);
        }
        
        if (volume) {
            volume_vec.assign(volume, volume + length);
        } else {
            volume_vec.assign(length, 0.0);
        }

        // 准备参数
        prophet::indicators::IndicatorParams params;
        for (int i = 0; i < param_count; ++i) {
            if (param_keys[i] && param_values) {
                params.set(param_keys[i], param_values[i]);
            }
        }

        // 调用指标计算函数
        prophet::indicators::Calculator calculator;
        auto callable = registry.getIndicator(indicator_name);
        auto cpp_result = callable(calculator, close_vec, high_vec, low_vec, volume_vec, params);

        // 提取指定字段并转换为C API格式
        std::string field_str = field_name ? field_name : "value";
        int ret = ConvertIndicatorResultToC(cpp_result, field_str, length, outResult);
        if (ret != 0) {
            return ret;
        }

        return 0;
    } catch (const std::exception& e) {
        SetLastError("Indicator '%s' calculation failed: %s", indicator_name, e.what());
        snprintf(outResult->error_message, sizeof(outResult->error_message),
                "Indicator '%s' calculation failed: %s", indicator_name, e.what());
        return -2;
    } catch (...) {
        SetLastError("Indicator '%s' calculation failed: unknown error", indicator_name);
        snprintf(outResult->error_message, sizeof(outResult->error_message),
                "Indicator '%s' calculation failed: unknown error", indicator_name);
        return -2;
    }
}

// ============================================================================
// SMA - 简单移动平均
// ============================================================================

PROPHET_API int Prophet_SMA(
    const double* inReal,
    int length,
    int period,
    IndicatorResult* outResult
) {
    if (!ValidateParams(inReal, length, period, outResult)) {
        return -1;
    }

    // 统一调用通用计算函数（通过IndicatorRegistry）
    const char* param_keys[] = {"PERIOD"};
    double param_values[] = {static_cast<double>(period)};
    
    return CalculateIndicatorUnified(
        "MA",
        inReal,
        nullptr, nullptr, nullptr,
        length,
        param_keys, param_values, 1,
        "value",
        outResult
    );
}

// ============================================================================
// EMA - 指数移动平均
// ============================================================================

PROPHET_API int Prophet_EMA(
    const double* inReal,
    int length,
    int period,
    IndicatorResult* outResult
) {
    if (!ValidateParams(inReal, length, period, outResult)) {
        return -1;
    }

    // 统一调用通用计算函数（通过IndicatorRegistry）
    const char* param_keys[] = {"PERIOD"};
    double param_values[] = {static_cast<double>(period)};
    
    return CalculateIndicatorUnified(
        "EMA",
        inReal,
        nullptr, nullptr, nullptr,
        length,
        param_keys, param_values, 1,
        "value",
        outResult
    );
}

// ============================================================================
// WMA - 加权移动平均
// ============================================================================

PROPHET_API int Prophet_WMA(
    const double* inReal,
    int length,
    int period,
    IndicatorResult* outResult
) {
    if (!ValidateParams(inReal, length, period, outResult)) {
        return -1;
    }

    // 统一调用通用计算函数（通过IndicatorRegistry）
    const char* param_keys[] = {"PERIOD"};
    double param_values[] = {static_cast<double>(period)};
    
    return CalculateIndicatorUnified(
        "WMA",
        inReal,
        nullptr, nullptr, nullptr,
        length,
        param_keys, param_values, 1,
        "value",
        outResult
    );
}

// ============================================================================
// DEMA - 双重指数移动平均
// ============================================================================

PROPHET_API int Prophet_DEMA(
    const double* inReal,
    int length,
    int period,
    IndicatorResult* outResult
) {
    if (!ValidateParams(inReal, length, period, outResult)) {
        return -1;
    }

    // 统一调用通用计算函数（通过IndicatorRegistry）
    const char* param_keys[] = {"PERIOD"};
    double param_values[] = {static_cast<double>(period)};
    
    return CalculateIndicatorUnified(
        "DEMA",
        inReal,
        nullptr, nullptr, nullptr,
        length,
        param_keys, param_values, 1,
        "value",
        outResult
    );
}

// ============================================================================
// TEMA - 三重指数移动平均
// ============================================================================

PROPHET_API int Prophet_TEMA(
    const double* inReal,
    int length,
    int period,
    IndicatorResult* outResult
) {
    if (!ValidateParams(inReal, length, period, outResult)) {
        return -1;
    }

    // 统一调用通用计算函数（通过IndicatorRegistry）
    const char* param_keys[] = {"PERIOD"};
    double param_values[] = {static_cast<double>(period)};
    
    return CalculateIndicatorUnified(
        "TEMA",
        inReal,
        nullptr, nullptr, nullptr,
        length,
        param_keys, param_values, 1,
        "value",
        outResult
    );
}

// ============================================================================
// BBANDS - 布林带
// ============================================================================

PROPHET_API int Prophet_BBANDS(
    const double* inReal,
    int length,
    int period,
    double stdDev,
    IndicatorResult* outUpper,
    IndicatorResult* outMiddle,
    IndicatorResult* outLower
) {
    if (!inReal || length <= 0 || period <= 0 || length < period) {
        SetLastError("Invalid input parameters");
        return -1;
    }
    if (!outUpper || !outMiddle || !outLower) {
        SetLastError("Output pointers are null");
        return -1;
    }

    // 统一调用通用计算函数（通过IndicatorRegistry）
    const char* param_keys[] = {"PERIOD", "STD_DEV"};
    double param_values[] = {
        static_cast<double>(period),
        stdDev
    };
    
    // 提取upper字段
    int ret1 = CalculateIndicatorUnified(
        "BOLL",
        inReal,
        nullptr, nullptr, nullptr,
        length,
        param_keys, param_values, 2,
        "upper",
        outUpper
    );
    if (ret1 != 0) {
        return ret1;
    }
    
    // 提取middle字段
    int ret2 = CalculateIndicatorUnified(
        "BOLL",
        inReal,
        nullptr, nullptr, nullptr,
        length,
        param_keys, param_values, 2,
        "middle",
        outMiddle
    );
    if (ret2 != 0) {
        Prophet_FreeResult(outUpper);
        return ret2;
    }
    
    // 提取lower字段
    int ret3 = CalculateIndicatorUnified(
        "BOLL",
        inReal,
        nullptr, nullptr, nullptr,
        length,
        param_keys, param_values, 2,
        "lower",
        outLower
    );
    if (ret3 != 0) {
        Prophet_FreeResult(outUpper);
        Prophet_FreeResult(outMiddle);
        return ret3;
    }
    
    return 0;
}

// ============================================================================
// Keltner Channel - 肯特纳通道
// ============================================================================

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
) {
    if (!inHigh || !inLow || !inClose || length <= 0 || period <= 0 || length < period) {
        SetLastError("Invalid input parameters");
        return -1;
    }
    if (!outUpper || !outMiddle || !outLower) {
        SetLastError("Output pointers are null");
        return -1;
    }

    // 统一调用通用计算函数（通过IndicatorRegistry）
    const char* param_keys[] = {"PERIOD", "MULTIPLIER"};
    double param_values[] = {
        static_cast<double>(period),
        multiplier
    };
    
    // 提取upper字段
    int ret1 = CalculateIndicatorUnified(
        "Keltner",
        inClose,
        inHigh, inLow, nullptr,
        length,
        param_keys, param_values, 2,
        "upper",
        outUpper
    );
    if (ret1 != 0) {
        return ret1;
    }
    
    // 提取middle字段
    int ret2 = CalculateIndicatorUnified(
        "Keltner",
        inClose,
        inHigh, inLow, nullptr,
        length,
        param_keys, param_values, 2,
        "middle",
        outMiddle
    );
    if (ret2 != 0) {
        Prophet_FreeResult(outUpper);
        return ret2;
    }
    
    // 提取lower字段
    int ret3 = CalculateIndicatorUnified(
        "Keltner",
        inClose,
        inHigh, inLow, nullptr,
        length,
        param_keys, param_values, 2,
        "lower",
        outLower
    );
    if (ret3 != 0) {
        Prophet_FreeResult(outUpper);
        Prophet_FreeResult(outMiddle);
        return ret3;
    }
    
    return 0;
}

// ============================================================================
// Ichimoku Cloud - 一目均衡表
// ============================================================================

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
) {
    if (!inHigh || !inLow || !inClose || length <= 0) {
        SetLastError("Invalid input parameters");
        return -1;
    }
    if (!outTenkan || !outKijun || !outSenkouA || !outSenkouB || !outChikou) {
        SetLastError("Output pointers are null");
        return -1;
    }

    int maxPeriod = std::max({tenkanPeriod, kijunPeriod, senkouBPeriod});
    if (length < maxPeriod) {
        SetLastError("Insufficient data length");
        return -1;
    }

    // 统一调用通用计算函数（通过IndicatorRegistry）
    const char* param_keys[] = {"TENKAN", "KIJUN", "SENKOU"};
    double param_values[] = {
        static_cast<double>(tenkanPeriod),
        static_cast<double>(kijunPeriod),
        static_cast<double>(senkouBPeriod)
    };
    
    // 提取tenkan字段
    int ret1 = CalculateIndicatorUnified(
        "Ichimoku",
        inClose,
        inHigh, inLow, nullptr,
        length,
        param_keys, param_values, 3,
        "tenkan",
        outTenkan
    );
    if (ret1 != 0) {
        return ret1;
    }
    
    // 提取kijun字段
    int ret2 = CalculateIndicatorUnified(
        "Ichimoku",
        inClose,
        inHigh, inLow, nullptr,
        length,
        param_keys, param_values, 3,
        "kijun",
        outKijun
    );
    if (ret2 != 0) {
        Prophet_FreeResult(outTenkan);
        return ret2;
    }
    
    // 提取senkou_a字段
    int ret3 = CalculateIndicatorUnified(
        "Ichimoku",
        inClose,
        inHigh, inLow, nullptr,
        length,
        param_keys, param_values, 3,
        "senkou_a",
        outSenkouA
    );
    if (ret3 != 0) {
        Prophet_FreeResult(outTenkan);
        Prophet_FreeResult(outKijun);
        return ret3;
    }
    
    // 提取senkou_b字段
    int ret4 = CalculateIndicatorUnified(
        "Ichimoku",
        inClose,
        inHigh, inLow, nullptr,
        length,
        param_keys, param_values, 3,
        "senkou_b",
        outSenkouB
    );
    if (ret4 != 0) {
        Prophet_FreeResult(outTenkan);
        Prophet_FreeResult(outKijun);
        Prophet_FreeResult(outSenkouA);
        return ret4;
    }
    
    // 提取chikou字段
    int ret5 = CalculateIndicatorUnified(
        "Ichimoku",
        inClose,
        inHigh, inLow, nullptr,
        length,
        param_keys, param_values, 3,
        "chikou",
        outChikou
    );
    if (ret5 != 0) {
        Prophet_FreeResult(outTenkan);
        Prophet_FreeResult(outKijun);
        Prophet_FreeResult(outSenkouA);
        Prophet_FreeResult(outSenkouB);
        return ret5;
    }
    
    return 0;
}

// ============================================================================
// SAR - 抛物线转向指标
// ============================================================================

PROPHET_API int Prophet_SAR(
    const double* inHigh,
    const double* inLow,
    int length,
    double acceleration,
    double maximum,
    IndicatorResult* outResult
) {
    if (!inHigh || !inLow || length <= 0 || !outResult) {
        SetLastError("Invalid input parameters");
        return -1;
    }

    // 统一调用通用计算函数（通过IndicatorRegistry）
    const char* param_keys[] = {"ACCELERATION", "MAXIMUM"};
    double param_values[] = {acceleration, maximum};
    
    // 需要构造close数组（SAR只需要high/low，但统一函数需要close）
    std::vector<double> close_vec(length);
    for (int i = 0; i < length; ++i) {
        close_vec[i] = (inHigh[i] + inLow[i]) / 2.0;
    }
    
    return CalculateIndicatorUnified(
        "SAR",
        close_vec.data(),
        inHigh, inLow, nullptr,
        length,
        param_keys, param_values, 2,
        "value",
        outResult
    );
}

// ============================================================================
// TRIX - 三重指数平滑移动平均
// ============================================================================

PROPHET_API int Prophet_TRIX(
    const double* inReal,
    int length,
    int period,
    IndicatorResult* outResult
) {
    if (!ValidateParams(inReal, length, period, outResult)) {
        return -1;
    }

    // 统一调用通用计算函数（通过IndicatorRegistry）
    const char* param_keys[] = {"PERIOD"};
    double param_values[] = {static_cast<double>(period)};
    
    return CalculateIndicatorUnified(
        "TRIX",
        inReal,
        nullptr, nullptr, nullptr,
        length,
        param_keys, param_values, 1,
        "value",
        outResult
    );
}

// ============================================================================
// VWAP - 成交量加权平均价（自定义实现）
// ============================================================================

PROPHET_API int Prophet_VWAP(
    const double* inHigh,
    const double* inLow,
    const double* inClose,
    const double* inVolume,
    int length,
    int period,
    IndicatorResult* outResult
) {
    if (!inHigh || !inLow || !inClose || !inVolume || length <= 0 || !outResult) {
        SetLastError("Invalid input parameters");
        return -1;
    }

    // 统一调用通用计算函数（通过IndicatorRegistry）
    // VWAP使用PERIOD参数（PERIOD=0表示累积VWAP，PERIOD>0表示滑动窗口VWAP）
    const char* param_keys[] = {"PERIOD"};
    double param_values[] = {static_cast<double>(period)};
    
    return CalculateIndicatorUnified(
        "VWAP",
        inClose,
        inHigh, inLow, inVolume,
        length,
        param_keys, param_values, 1,
        "value",
        outResult
    );
}

// ============================================================================
// ATR - 平均真实波幅
// ============================================================================

PROPHET_API int Prophet_ATR(
    const double* inHigh,
    const double* inLow,
    const double* inClose,
    int length,
    int period,
    IndicatorResult* outResult
) {
    if (!inHigh || !inLow || !inClose || length <= 0 || period <= 0 || !outResult) {
        SetLastError("Invalid input parameters");
        return -1;
    }

    // 统一调用通用计算函数（通过IndicatorRegistry）
    const char* param_keys[] = {"PERIOD"};
    double param_values[] = {static_cast<double>(period)};
    
    return CalculateIndicatorUnified(
        "ATR",
        inClose,
        inHigh, inLow, nullptr,
        length,
        param_keys, param_values, 1,
        "value",
        outResult
    );
}

// ============================================================================
// MACD - 移动平均收敛发散指标
// ============================================================================

PROPHET_API int Prophet_MACD(
    const double* inReal,
    int length,
    int fastPeriod,
    int slowPeriod,
    int signalPeriod,
    IndicatorResult* outMACD,
    IndicatorResult* outSignal,
    IndicatorResult* outHistogram
) {
    if (!inReal || length <= 0 || !outMACD || !outSignal || !outHistogram) {
        SetLastError("Invalid input parameters");
        return -1;
    }

    // 统一调用通用计算函数（通过IndicatorRegistry）
    const char* param_keys[] = {"FAST_PERIOD", "SLOW_PERIOD", "SIGNAL_PERIOD"};
    double param_values[] = {
        static_cast<double>(fastPeriod),
        static_cast<double>(slowPeriod),
        static_cast<double>(signalPeriod)
    };
    
    // 提取macd字段
    int ret1 = CalculateIndicatorUnified(
        "MACD",
        inReal,
        nullptr, nullptr, nullptr,
        length,
        param_keys, param_values, 3,
        "macd",
        outMACD
    );
    if (ret1 != 0) {
        return ret1;
    }
    
    // 提取signal字段
    int ret2 = CalculateIndicatorUnified(
        "MACD",
        inReal,
        nullptr, nullptr, nullptr,
        length,
        param_keys, param_values, 3,
        "signal",
        outSignal
    );
    if (ret2 != 0) {
        Prophet_FreeResult(outMACD);
        return ret2;
    }
    
    // 提取histogram字段
    int ret3 = CalculateIndicatorUnified(
        "MACD",
        inReal,
        nullptr, nullptr, nullptr,
        length,
        param_keys, param_values, 3,
        "histogram",
        outHistogram
    );
    if (ret3 != 0) {
        Prophet_FreeResult(outMACD);
        Prophet_FreeResult(outSignal);
        return ret3;
    }
    
    return 0;
}

// ============================================================================
// RSI - 相对强弱指标
// ============================================================================

PROPHET_API int Prophet_RSI(
    const double* inReal,
    int length,
    int period,
    IndicatorResult* outResult
) {
    if (!ValidateParams(inReal, length, period, outResult)) {
        return -1;
    }

    // 统一调用通用计算函数（通过IndicatorRegistry）
    // RSI 注册表读取 "PERIOD"（见 indicator_registrations.cpp），此处必须同名
    const char* param_keys[] = {"PERIOD"};
    double param_values[] = {static_cast<double>(period)};
    
    return CalculateIndicatorUnified(
        "RSI",
        inReal,
        nullptr,  // high
        nullptr,   // low
        nullptr,   // volume
        length,
        param_keys,
        param_values,
        1,         // param_count
        "value",   // field_name
        outResult
    );
}

// ============================================================================
// MFI - 资金流量指标
// ============================================================================

PROPHET_API int Prophet_MFI(
    const double* inHigh,
    const double* inLow,
    const double* inClose,
    const double* inVolume,
    int length,
    int period,
    IndicatorResult* outResult
) {
    if (!inHigh || !inLow || !inClose || !inVolume || length <= 0 || period <= 0 || !outResult) {
        SetLastError("Invalid input parameters");
        return -1;
    }

    // 统一调用通用计算函数（通过IndicatorRegistry）
    const char* param_keys[] = {"PERIOD"};
    double param_values[] = {static_cast<double>(period)};
    
    return CalculateIndicatorUnified(
        "MFI",
        inClose,
        inHigh, inLow, inVolume,
        length,
        param_keys, param_values, 1,
        "value",
        outResult
    );
}

// ============================================================================
// OBV - 能量潮
// ============================================================================

PROPHET_API int Prophet_OBV(
    const double* inReal,
    const double* inVolume,
    int length,
    IndicatorResult* outResult
) {
    if (!inReal || !inVolume || length <= 0 || !outResult) {
        SetLastError("Invalid input parameters");
        return -1;
    }

    // 统一调用通用计算函数（通过IndicatorRegistry）
    // OBV无参数
    return CalculateIndicatorUnified(
        "OBV",
        inReal,
        nullptr, nullptr, inVolume,
        length,
        nullptr, nullptr, 0,
        "value",
        outResult
    );
}

// ============================================================================
// STOCH - KDJ随机指标
// ============================================================================

PROPHET_API int Prophet_STOCH(
    const double* inHigh,
    const double* inLow,
    const double* inClose,
    int length,
    int FASTK_PERIOD,
    int SLOWK_PERIOD,
    int SLOWD_PERIOD,
    IndicatorResult* outSlowK,
    IndicatorResult* outSlowD
) {
    if (!inHigh || !inLow || !inClose || length <= 0 || !outSlowK || !outSlowD) {
        SetLastError("Invalid input parameters");
        return -1;
    }

    // 统一调用通用计算函数（通过IndicatorRegistry）
    // STOCH对应KDJ指标
    // STOCH 对应 KDJ 指标，注册表读取 N_PERIOD/M1_PERIOD/M2_PERIOD
    const char* param_keys[] = {"N_PERIOD", "M1_PERIOD", "M2_PERIOD"};
    double param_values[] = {
        static_cast<double>(FASTK_PERIOD),
        static_cast<double>(SLOWK_PERIOD),
        static_cast<double>(SLOWD_PERIOD)
    };
    
    // 提取k字段
    int ret1 = CalculateIndicatorUnified(
        "KDJ",
        inClose,
        inHigh, inLow, nullptr,
        length,
        param_keys, param_values, 3,
        "k",
        outSlowK
    );
    if (ret1 != 0) {
        return ret1;
    }
    
    // 提取d字段
    int ret2 = CalculateIndicatorUnified(
        "KDJ",
        inClose,
        inHigh, inLow, nullptr,
        length,
        param_keys, param_values, 3,
        "d",
        outSlowD
    );
    if (ret2 != 0) {
        Prophet_FreeResult(outSlowK);
        return ret2;
    }
    
    return 0;
}

// ============================================================================
// STOCHRSI - 随机RSI
// ============================================================================

PROPHET_API int Prophet_STOCHRSI(
    const double* inReal,
    int length,
    int period,
    int FASTK_PERIOD,
    int fastD_Period,
    IndicatorResult* outFastK,
    IndicatorResult* outFastD
) {
    if (!inReal || length <= 0 || !outFastK || !outFastD) {
        SetLastError("Invalid input parameters");
        return -1;
    }

    // 统一调用通用计算函数（通过IndicatorRegistry）
    const char* param_keys[] = {"RSI_PERIOD", "STOCH_PERIOD", "K_PERIOD", "D_PERIOD"};
    double param_values[] = {
        static_cast<double>(period),
        static_cast<double>(period),  // STOCH周期通常等于RSI周期
        static_cast<double>(FASTK_PERIOD),
        static_cast<double>(fastD_Period)
    };
    
    // 提取k字段
    int ret1 = CalculateIndicatorUnified(
        "STOCHRSI",
        inReal,
        nullptr, nullptr, nullptr,
        length,
        param_keys, param_values, 4,
        "k",
        outFastK
    );
    if (ret1 != 0) {
        return ret1;
    }
    
    // 提取d字段
    int ret2 = CalculateIndicatorUnified(
        "STOCHRSI",
        inReal,
        nullptr, nullptr, nullptr,
        length,
        param_keys, param_values, 4,
        "d",
        outFastD
    );
    if (ret2 != 0) {
        Prophet_FreeResult(outFastK);
        return ret2;
    }
    
    return 0;
}

// ============================================================================
// CCI - 商品通道指标
// ============================================================================

PROPHET_API int Prophet_CCI(
    const double* inHigh,
    const double* inLow,
    const double* inClose,
    int length,
    int period,
    IndicatorResult* outResult
) {
    if (!inHigh || !inLow || !inClose || length <= 0 || period <= 0 || !outResult) {
        SetLastError("Invalid input parameters");
        return -1;
    }

    // 统一调用通用计算函数（通过IndicatorRegistry）
    const char* param_keys[] = {"PERIOD"};
    double param_values[] = {static_cast<double>(period)};
    
    return CalculateIndicatorUnified(
        "CCI",
        inClose,
        inHigh, inLow, nullptr,
        length,
        param_keys, param_values, 1,
        "value",
        outResult
    );
}

// ============================================================================
// DMI - 趋向指标（ADX + DI）
// ============================================================================

PROPHET_API int Prophet_DMI(
    const double* inHigh,
    const double* inLow,
    const double* inClose,
    int length,
    int period,
    IndicatorResult* outADX,
    IndicatorResult* outPlusDI,
    IndicatorResult* outMinusDI
) {
    if (!inHigh || !inLow || !inClose || length <= 0 || period <= 0 || 
        !outADX || !outPlusDI || !outMinusDI) {
        SetLastError("Invalid input parameters");
        return -1;
    }

    // 统一调用通用计算函数（通过IndicatorRegistry）
    const char* param_keys[] = {"PERIOD"};
    double param_values[] = {static_cast<double>(period)};
    
    // 提取adx字段
    int ret1 = CalculateIndicatorUnified(
        "DMI",
        inClose,
        inHigh, inLow, nullptr,
        length,
        param_keys, param_values, 1,
        "adx",
        outADX
    );
    if (ret1 != 0) {
        return ret1;
    }
    
    // 提取plus_di字段
    int ret2 = CalculateIndicatorUnified(
        "DMI",
        inClose,
        inHigh, inLow, nullptr,
        length,
        param_keys, param_values, 1,
        "plus_di",
        outPlusDI
    );
    if (ret2 != 0) {
        Prophet_FreeResult(outADX);
        return ret2;
    }
    
    // 提取minus_di字段
    int ret3 = CalculateIndicatorUnified(
        "DMI",
        inClose,
        inHigh, inLow, nullptr,
        length,
        param_keys, param_values, 1,
        "minus_di",
        outMinusDI
    );
    if (ret3 != 0) {
        Prophet_FreeResult(outADX);
        Prophet_FreeResult(outPlusDI);
        return ret3;
    }
    
    return 0;
}

// ============================================================================
// WILLR - 威廉指标
// ============================================================================

PROPHET_API int Prophet_WILLR(
    const double* inHigh,
    const double* inLow,
    const double* inClose,
    int length,
    int period,
    IndicatorResult* outResult
) {
    if (!inHigh || !inLow || !inClose || length <= 0 || period <= 0 || !outResult) {
        SetLastError("Invalid input parameters");
        return -1;
    }

    // 统一调用通用计算函数（通过IndicatorRegistry）
    // WILLR对应WR指标
    const char* param_keys[] = {"PERIOD"};
    double param_values[] = {static_cast<double>(period)};
    
    return CalculateIndicatorUnified(
        "WR",
        inClose,
        inHigh, inLow, nullptr,
        length,
        param_keys, param_values, 1,
        "value",
        outResult
    );
}

// ============================================================================
// CMF - 蔡金资金流量（注：TA-Lib有AD和ADOSC，CMF需要自定义）
// ============================================================================

PROPHET_API int Prophet_CMF(
    const double* inHigh,
    const double* inLow,
    const double* inClose,
    const double* inVolume,
    int length,
    int period,
    IndicatorResult* outResult
) {
    if (!inHigh || !inLow || !inClose || !inVolume || length <= 0 || period <= 0 || !outResult) {
        SetLastError("Invalid input parameters");
        return -1;
    }

    // 统一调用通用计算函数（通过IndicatorRegistry）
    const char* param_keys[] = {"PERIOD"};
    double param_values[] = {static_cast<double>(period)};
    
    return CalculateIndicatorUnified(
        "CMF",
        inClose,
        inHigh, inLow, inVolume,
        length,
        param_keys, param_values, 1,
        "value",
        outResult
    );
}

// ============================================================================
// ROC - 变动率指标
// ============================================================================

PROPHET_API int Prophet_ROC(
    const double* inReal,
    int length,
    int period,
    IndicatorResult* outResult
) {
    if (!ValidateParams(inReal, length, period, outResult)) {
        return -1;
    }

    // 统一调用通用计算函数（通过IndicatorRegistry）
    const char* param_keys[] = {"PERIOD"};
    double param_values[] = {static_cast<double>(period)};
    
    return CalculateIndicatorUnified(
        "ROC",
        inReal,
        nullptr, nullptr, nullptr,
        length,
        param_keys, param_values, 1,
        "value",
        outResult
    );
}

// ============================================================================
// EMV - 简易波动指标（注：TA-Lib没有EMV，使用自定义实现）
// ============================================================================

PROPHET_API int Prophet_EMV(
    const double* inHigh,
    const double* inLow,
    const double* inVolume,
    int length,
    int period,
    IndicatorResult* outResult
) {
    if (!inHigh || !inLow || !inVolume || length <= 0 || period <= 0 || !outResult) {
        SetLastError("Invalid input parameters");
        return -1;
    }

    // 统一调用通用计算函数（通过IndicatorRegistry）
    // 需要构造close数组（EMV只需要high/low/volume，但统一函数需要close）
    std::vector<double> close_vec(length);
    for (int i = 0; i < length; ++i) {
        close_vec[i] = (inHigh[i] + inLow[i]) / 2.0;
    }
    
    const char* param_keys[] = {"PERIOD"};
    double param_values[] = {static_cast<double>(period)};
    
    return CalculateIndicatorUnified(
        "EMV",
        close_vec.data(),
        inHigh, inLow, inVolume,
        length,
        param_keys, param_values, 1,
        "value",
        outResult
    );
}

// ============================================================================
// MTM - 动量指标
// ============================================================================

PROPHET_API int Prophet_MTM(
    const double* inReal,
    int length,
    int period,
    IndicatorResult* outResult
) {
    if (!ValidateParams(inReal, length, period, outResult)) {
        return -1;
    }

    // 统一调用通用计算函数（通过IndicatorRegistry）
    const char* param_keys[] = {"PERIOD"};
    double param_values[] = {static_cast<double>(period)};
    
    return CalculateIndicatorUnified(
        "MTM",
        inReal,
        nullptr, nullptr, nullptr,
        length,
        param_keys, param_values, 1,
        "value",
        outResult
    );
}

// ============================================================================
// CMO - 钱德动量摆动指标
// ============================================================================

PROPHET_API int Prophet_CMO(
    const double* inReal,
    int length,
    int period,
    IndicatorResult* outResult
) {
    if (!ValidateParams(inReal, length, period, outResult)) {
        return -1;
    }

    // 统一调用通用计算函数（通过IndicatorRegistry）
    const char* param_keys[] = {"PERIOD"};
    double param_values[] = {static_cast<double>(period)};
    
    return CalculateIndicatorUnified(
        "CMO",
        inReal,
        nullptr, nullptr, nullptr,
        length,
        param_keys, param_values, 1,
        "value",
        outResult
    );
}

// ============================================================================
// AROON - 阿隆指标
// ============================================================================

PROPHET_API int Prophet_AROON(
    const double* inHigh,
    const double* inLow,
    int length,
    int period,
    IndicatorResult* outAroonDown,
    IndicatorResult* outAroonUp
) {
    if (!inHigh || !inLow || length <= 0 || period <= 0 || !outAroonDown || !outAroonUp) {
        SetLastError("Invalid input parameters");
        return -1;
    }

    // 统一调用通用计算函数（通过IndicatorRegistry）
    // 需要构造close数组（AROON只需要high/low，但统一函数需要close）
    std::vector<double> close_vec(length);
    for (int i = 0; i < length; ++i) {
        close_vec[i] = (inHigh[i] + inLow[i]) / 2.0;
    }
    
    const char* param_keys[] = {"PERIOD"};
    double param_values[] = {static_cast<double>(period)};
    
    // 提取aroon_down字段
    int ret1 = CalculateIndicatorUnified(
        "AROON",
        close_vec.data(),
        inHigh, inLow, nullptr,
        length,
        param_keys, param_values, 1,
        "aroon_down",
        outAroonDown
    );
    if (ret1 != 0) {
        return ret1;
    }
    
    // 提取aroon_up字段
    int ret2 = CalculateIndicatorUnified(
        "AROON",
        close_vec.data(),
        inHigh, inLow, nullptr,
        length,
        param_keys, param_values, 1,
        "aroon_up",
        outAroonUp
    );
    if (ret2 != 0) {
        Prophet_FreeResult(outAroonDown);
        return ret2;
    }
    
    return 0;
}

// ============================================================================
// 内存管理
// ============================================================================

PROPHET_API void Prophet_FreeResult(IndicatorResult* result) {
    if (result && result->values) {
        delete[] result->values;
        result->values = nullptr;
        result->length = 0;
        result->out_begin = 0;
    }
}

PROPHET_API void Prophet_FreeResults(IndicatorResult* results, int count) {
    if (results) {
        for (int i = 0; i < count; ++i) {
            Prophet_FreeResult(&results[i]);
        }
    }
}

// ============================================================================
// 错误处理
// ============================================================================

PROPHET_API const char* Prophet_GetLastError() {
    return g_last_error;
}

// ============================================================================
// K线转换工具实现（供 C# 客户端使用）
// ============================================================================

PROPHET_API int Prophet_ConvertKlines(
    const NativeKline* klines,
    int count,
    int from_minutes,
    int to_minutes,
    int period,
    KlineConversionResult* out_result
) {
    if (!klines || count <= 0 || !out_result) {
        SetLastError("Invalid parameters for kline conversion");
        if (out_result) {
            snprintf(out_result->error_message, sizeof(out_result->error_message),
                    "Invalid parameters");
        }
        return -1;
    }

    if (from_minutes <= 0 || to_minutes <= 0 || to_minutes <= from_minutes) {
        SetLastError("Invalid timeframe: from=%d, to=%d", from_minutes, to_minutes);
        snprintf(out_result->error_message, sizeof(out_result->error_message),
                "Invalid timeframe parameters");
        return -1;
    }

    try {
        // 转换为 C++ Kline 结构
        std::vector<prophet::Kline> cpp_klines;
        cpp_klines.reserve(count);
        
        for (int i = 0; i < count; ++i) {
            // 使用聚合初始化（Kline 是 POD 结构）
            cpp_klines.push_back({
                klines[i].open,
                klines[i].high,
                klines[i].low,
                klines[i].close,
                klines[i].volume,
                klines[i].open_time,
                klines[i].close_time
            });
        }

        // 调用 K线转换器
        auto result = prophet::tools::kline::Converter::convert(
            cpp_klines,
            from_minutes,
            to_minutes,
            period
        );

        // 分配结果内存
        out_result->klines = new (std::nothrow) NativeKline[result.size()];
        if (!out_result->klines) {
            SetLastError("Memory allocation failed");
            snprintf(out_result->error_message, sizeof(out_result->error_message),
                    "Memory allocation failed");
            return -2;
        }

        // 转换为 C 结构
        out_result->length = static_cast<int>(result.size());
        for (size_t i = 0; i < result.size(); ++i) {
            out_result->klines[i].open = result[i].open;
            out_result->klines[i].high = result[i].high;
            out_result->klines[i].low = result[i].low;
            out_result->klines[i].close = result[i].close;
            out_result->klines[i].volume = result[i].volume;
            out_result->klines[i].open_time = result[i].open_time;
            out_result->klines[i].close_time = result[i].close_time;
        }

        out_result->error_message[0] = '\0';
        return 0;

    } catch (const std::exception& e) {
        SetLastError("Kline conversion failed: %s", e.what());
        snprintf(out_result->error_message, sizeof(out_result->error_message),
                "Conversion failed: %s", e.what());
        return -2;
    }
}

PROPHET_API void Prophet_FreeKlineResult(KlineConversionResult* result) {
    if (result && result->klines) {
        delete[] result->klines;
        result->klines = nullptr;
        result->length = 0;
    }
}

PROPHET_API int Prophet_TimeframeToMinutes(const char* timeframe) {
    if (!timeframe) {
        SetLastError("Timeframe string is null");
        return -1;
    }

    try {
        return prophet::tools::kline::Converter::timeframeToMinutes(timeframe);
    } catch (const std::exception& e) {
        SetLastError("Invalid timeframe: %s", e.what());
        return -1;
    }
}

PROPHET_API int Prophet_MinutesToTimeframe(
    int minutes,
    char* out_buffer,
    int buffer_size
) {
    if (!out_buffer || buffer_size <= 0) {
        SetLastError("Invalid output buffer");
        return -1;
    }

    if (minutes <= 0) {
        SetLastError("Invalid minutes: %d", minutes);
        return -1;
    }

    try {
        std::string result = prophet::tools::kline::Converter::minutesToTimeframe(minutes);
        
        if (static_cast<int>(result.length()) >= buffer_size) {
            SetLastError("Buffer too small: need %d bytes", static_cast<int>(result.length()) + 1);
            return -1;
        }

        // 使用安全的字符串复制（兼容 MSVC 和其他编译器）
        size_t copy_len = std::min(result.length(), static_cast<size_t>(buffer_size - 1));
        std::memcpy(out_buffer, result.c_str(), copy_len);
        out_buffer[copy_len] = '\0';
        
        return 0;

    } catch (const std::exception& e) {
        SetLastError("Conversion failed: %s", e.what());
        return -1;
    }
}

