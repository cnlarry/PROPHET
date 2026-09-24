/*
 * ============================================================================
 * 文件名：strategy_api.cpp
 * 功能说明：Prophet Core 策略引擎 C API 实现
 * 
 * 为Prophet.Client (C#)提供策略执行引擎的C风格导出接口
 * 与Python端使用相同的Engine核心，确保行为一致
 * ============================================================================
 */

// 禁用MSVC的strncpy安全警告（我们已经手动处理了缓冲区溢出）
#ifdef _MSC_VER
#define _CRT_SECURE_NO_WARNINGS
#endif

#include "prophet/c_api.h"
#include "prophet/core/engine.hpp"
#include "prophet/common/signal.hpp"
#include "prophet/common/types.hpp"  // 包含异常类型定义
#include "prophet/logging/logger.hpp"  // 统一日志系统
#include "prophet/dsl/lexer.hpp"  // 用于 DSL 验证
#include "prophet/dsl/parser.hpp"  // 用于 DSL 验证
#include <cstring>
#include <cstdio>
#include <cstdarg>
#include <memory>
#include <string>
#include <unordered_map>
#include <iostream>
#include <sstream>

// ============================================================================
// 辅助工具
// ============================================================================

namespace {
    // 最后一次错误信息
    thread_local char g_last_strategy_error[512] = {0};

    // 设置错误信息
    void SetStrategyError(const char* format, ...) {
        va_list args;
        va_start(args, format);
        vsnprintf(g_last_strategy_error, sizeof(g_last_strategy_error), format, args);
        va_end(args);
    }
}

// ============================================================================
// 策略引擎API - 生命周期管理
// ============================================================================

PROPHET_API void* Prophet_CreateEngine(
    const char* dsl_code,
    const char* params_json
) {
    PROPHET_LOG_INFO("Engine", "开始创建引擎实例");
    
    if (!dsl_code) {
        SetStrategyError("DSL code is null");
        PROPHET_LOG_ERROR("Engine", "DSL代码为空");
        return nullptr;
    }

    try {
        std::string dsl_str(dsl_code);
        PROPHET_LOG_DEBUG_FMT("Engine", "DSL代码长度: " << dsl_str.length() << " 字符");
        
        // TODO: 解析params_json为C++参数结构
        // 目前先使用空参数创建引擎
        std::unordered_map<std::string, 
            std::unordered_map<std::string, 
                std::unordered_map<std::string, double>>> params;
        
        if (params_json && std::strlen(params_json) > 0) {
            PROPHET_LOG_DEBUG_FMT("Engine", "参数JSON长度: " << std::strlen(params_json) << " 字符");
            // 后续可以添加JSON解析逻辑
            // 现在暂时使用默认参数
        }
        
        auto engine = std::make_unique<prophet::core::Engine>(dsl_str, params);
        PROPHET_LOG_INFO_FMT("Engine", "引擎创建成功，地址: " << engine.get());
        
        return engine.release();
        
    } catch (const prophet::ParserException& e) {
        // DSL 语法解析错误（最常见）
        SetStrategyError("DSL Syntax Error: %s", e.what());
        PROPHET_LOG_ERROR_FMT("Engine", "DSL语法错误: " << e.what());
        return nullptr;
    } catch (const prophet::LexerException& e) {
        // DSL 词法错误
        SetStrategyError("DSL Lexer Error: %s", e.what());
        PROPHET_LOG_ERROR_FMT("Engine", "DSL词法错误: " << e.what());
        return nullptr;
    } catch (const prophet::DSLException& e) {
        // 其他 DSL 相关错误
        SetStrategyError("DSL Error: %s", e.what());
        PROPHET_LOG_ERROR_FMT("Engine", "DSL错误: " << e.what());
        return nullptr;
    } catch (const std::exception& e) {
        // 通用错误
        SetStrategyError("Failed to create engine: %s", e.what());
        PROPHET_LOG_ERROR_FMT("Engine", "创建引擎失败: " << e.what());
        return nullptr;
    } catch (...) {
        // 未知错误
        SetStrategyError("Failed to create engine: Unknown error");
        PROPHET_LOG_ERROR("Engine", "创建引擎失败: 未知错误");
        return nullptr;
    }
}

PROPHET_API void Prophet_DestroyEngine(void* engine) {
    if (engine) {
        PROPHET_LOG_INFO_FMT("Engine", "销毁引擎实例，地址: " << engine);
        delete static_cast<prophet::core::Engine*>(engine);
    }
}

// ============================================================================
// DSL验证API - 只进行词法和语法分析，不创建完整引擎
// ============================================================================

PROPHET_API int Prophet_ValidateDSL(const char* dsl_code, char* error_buffer, int buffer_size) {
    PROPHET_LOG_INFO("Validation", "开始验证DSL代码");
    
    if (!dsl_code) {
        if (error_buffer && buffer_size > 0) {
            snprintf(error_buffer, buffer_size, "[VALIDATION] DSL代码为空");
        }
        PROPHET_LOG_ERROR("Validation", "DSL代码为空");
        return -1;
    }
    
    if (!error_buffer || buffer_size <= 0) {
        PROPHET_LOG_ERROR("Validation", "错误缓冲区无效");
        return -3;
    }
    
    try {
        std::string dsl_str(dsl_code);
        PROPHET_LOG_DEBUG_FMT("Validation", "DSL代码长度: " << dsl_str.length() << " 字符");
        
        // 只进行词法和语法分析，不创建引擎
        prophet::dsl::Lexer lexer(dsl_str);
        std::vector<prophet::dsl::Token> tokens = lexer.tokenize();
        PROPHET_LOG_DEBUG_FMT("Validation", "词法分析成功，Token数量: " << tokens.size());
        
        prophet::dsl::Parser parser(tokens, dsl_str);
        parser.parseStatements();  // 只解析，不执行
        PROPHET_LOG_INFO("Validation", "DSL验证通过");
        
        return 0;  // 验证通过
        
    } catch (const prophet::ParserException& e) {
        // DSL 语法解析错误 - 使用英文格式避免编码问题
        std::string error_msg;
        if (e.line > 0 && e.column > 0) {
            error_msg = "[PARSER] Line " + std::to_string(e.line) + ", Column " + std::to_string(e.column) + ": " + e.what();
        } else if (e.line > 0) {
            error_msg = "[PARSER] Line " + std::to_string(e.line) + ": " + e.what();
        } else {
            error_msg = "[PARSER] " + std::string(e.what());
        }
        
        snprintf(error_buffer, buffer_size, "%s", error_msg.c_str());
        PROPHET_LOG_ERROR_FMT("Validation", "DSL语法错误: " << error_msg);
        return -2;
        
    } catch (const prophet::LexerException& e) {
        // DSL 词法错误 - 使用英文格式避免编码问题
        std::string error_msg;
        if (e.line > 0 && e.column > 0) {
            error_msg = "[LEXER] Line " + std::to_string(e.line) + ", Column " + std::to_string(e.column) + ": " + e.what();
        } else if (e.line > 0) {
            error_msg = "[LEXER] Line " + std::to_string(e.line) + ": " + e.what();
        } else {
            error_msg = "[LEXER] " + std::string(e.what());
        }
        
        snprintf(error_buffer, buffer_size, "%s", error_msg.c_str());
        PROPHET_LOG_ERROR_FMT("Validation", "DSL词法错误: " << error_msg);
        return -1;
        
    } catch (const prophet::DSLException& e) {
        // 其他 DSL 相关错误 - 使用英文格式避免编码问题
        std::string error_msg;
        if (e.line > 0 && e.column > 0) {
            error_msg = "[DSL] Line " + std::to_string(e.line) + ", Column " + std::to_string(e.column) + ": " + e.what();
        } else if (e.line > 0) {
            error_msg = "[DSL] Line " + std::to_string(e.line) + ": " + e.what();
        } else {
            error_msg = "[DSL] " + std::string(e.what());
        }
        
        snprintf(error_buffer, buffer_size, "%s", error_msg.c_str());
        PROPHET_LOG_ERROR_FMT("Validation", "DSL错误: " << error_msg);
        return -2;
        
    } catch (const std::exception& e) {
        // 通用错误
        std::string error_msg = "[UNKNOWN] " + std::string(e.what());
        snprintf(error_buffer, buffer_size, "%s", error_msg.c_str());
        PROPHET_LOG_ERROR_FMT("Validation", "验证失败: " << error_msg);
        return -3;
        
    } catch (...) {
        // 未知错误
        snprintf(error_buffer, buffer_size, "[UNKNOWN] 未知错误");
        PROPHET_LOG_ERROR("Validation", "验证失败: 未知错误");
        return -3;
    }
}

// ============================================================================
// K线数据设置 - v10.0 重构：必须指定时间框架 + 增量更新支持
// ============================================================================

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
) {
    if (!engine) {
        SetStrategyError("Engine pointer is null");
        PROPHET_LOG_ERROR("Kline", "引擎指针为空");
        return -1;
    }
    
    if (!timeframe) {
        SetStrategyError("Timeframe is null");
        PROPHET_LOG_ERROR("Kline", "时间框架为空");
        return -1;
    }
    
    if (!open || !high || !low || !close || !volume || !open_time || !close_time) {
        SetStrategyError("K-line data arrays are null");
        PROPHET_LOG_ERROR("Kline", "K线数据数组为空");
        return -1;
    }
    
    if (count <= 0) {
        SetStrategyError("Invalid count: %d", count);
        PROPHET_LOG_ERROR_FMT("Kline", "无效的K线数量: " << count);
        return -1;
    }
    
    try {
        PROPHET_LOG_DEBUG_FMT("Kline", "设置K线数据: " << timeframe << ", 数量: " << count);
        auto* eng = static_cast<prophet::core::Engine*>(engine);
        eng->set_klines(
            std::string(timeframe),
            open, high, low, close, volume, 
            open_time, close_time, 
            static_cast<size_t>(count)
        );
        PROPHET_LOG_INFO_FMT("Kline", "成功设置" << timeframe << "K线数据，共" << count << "根");
        return 0;
    } catch (const std::exception& e) {
        SetStrategyError("Failed to set klines: %s", e.what());
        PROPHET_LOG_ERROR_FMT("Kline", "设置K线失败: " << e.what());
        return -2;
    }
}

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
) {
    if (!engine) {
        SetStrategyError("Engine pointer is null");
        return -1;
    }
    
    if (!timeframe) {
        SetStrategyError("Timeframe is null");
        return -1;
    }
    
    try {
        auto* eng = static_cast<prophet::core::Engine*>(engine);
        eng->append_kline(
            std::string(timeframe),
            open, high, low, close, volume,
            open_time, close_time
        );
        return 0;
    } catch (const std::exception& e) {
        SetStrategyError("Failed to append kline: %s", e.what());
        return -2;
    }
}

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
) {
    if (!engine) {
        SetStrategyError("Engine pointer is null");
        return -1;
    }
    
    if (!timeframe) {
        SetStrategyError("Timeframe is null");
        return -1;
    }
    
    if (!open || !high || !low || !close || !volume || !open_time || !close_time) {
        SetStrategyError("K-line data arrays are null");
        return -1;
    }
    
    if (count <= 0) {
        SetStrategyError("Invalid count: %d", count);
        return -1;
    }
    
    try {
        auto* eng = static_cast<prophet::core::Engine*>(engine);
        eng->append_klines(
            std::string(timeframe),
            open, high, low, close, volume,
            open_time, close_time,
            static_cast<size_t>(count)
        );
        return 0;
    } catch (const std::exception& e) {
        SetStrategyError("Failed to append klines: %s", e.what());
        return -2;
    }
}

// ============================================================================
// 时间序列数据设置
// ============================================================================

PROPHET_API int Prophet_SetFearGreedSeries(
    void* engine,
    const int64_t* date_timestamps,
    const int* values,
    const char** classifications,
    int count
) {
    if (!engine) {
        SetStrategyError("Engine pointer is null");
        return -1;
    }
    
    if (!date_timestamps || !values || !classifications) {
        SetStrategyError("FearGreed data arrays are null");
        return -1;
    }
    
    if (count <= 0) {
        SetStrategyError("Invalid count: %d", count);
        return -1;
    }
    
    try {
        auto* eng = static_cast<prophet::core::Engine*>(engine);
        
        // 构造FearGreedData向量
        std::vector<prophet::FearGreedData> series;
        series.reserve(count);
        
        for (int i = 0; i < count; ++i) {
            series.emplace_back(
                date_timestamps[i],
                values[i],
                classifications[i] ? std::string(classifications[i]) : std::string()
            );
        }
        
        // 设置到引擎
        eng->set_fear_greed_series(series);
        return 0;
        
    } catch (const std::exception& e) {
        SetStrategyError("Failed to set fear greed series: %s", e.what());
        return -2;
    }
}

PROPHET_API int Prophet_SetFundingRateSeries(
    void* engine,
    const int64_t* timestamps,
    const double* values,
    int count
) {
    if (!engine) {
        SetStrategyError("Engine pointer is null");
        return -1;
    }
    
    if (!timestamps || !values) {
        SetStrategyError("FundingRate data arrays are null");
        return -1;
    }
    
    if (count <= 0) {
        SetStrategyError("Invalid count: %d", count);
        return -1;
    }
    
    try {
        auto* eng = static_cast<prophet::core::Engine*>(engine);
        
        // 构造FundingRateData向量
        std::vector<prophet::FundingRateData> series;
        series.reserve(count);
        
        for (int i = 0; i < count; ++i) {
            series.emplace_back(timestamps[i], values[i]);
        }
        
        // 设置到引擎
        eng->set_funding_rate_series(series);
        return 0;
        
    } catch (const std::exception& e) {
        SetStrategyError("Failed to set funding rate series: %s", e.what());
        return -2;
    }
}

PROPHET_API int Prophet_SetLongShortRatioSeries(
    void* engine,
    const int64_t* timestamps,
    const double* long_ratios,
    const double* short_ratios,
    const double* ratios,
    int count
) {
    if (!engine) {
        SetStrategyError("Engine pointer is null");
        return -1;
    }
    
    if (!timestamps || !long_ratios || !short_ratios || !ratios) {
        SetStrategyError("LongShortRatio data arrays are null");
        return -1;
    }
    
    if (count <= 0) {
        SetStrategyError("Invalid count: %d", count);
        return -1;
    }
    
    try {
        auto* eng = static_cast<prophet::core::Engine*>(engine);
        
        // 构造LongShortRatioData向量
        std::vector<prophet::LongShortRatioData> series;
        series.reserve(count);
        
        for (int i = 0; i < count; ++i) {
            series.emplace_back(
                timestamps[i],      // 时间戳（秒）
                long_ratios[i],     // 多头比例
                short_ratios[i],    // 空头比例
                ratios[i]           // 多空比率
            );
        }
        
        // 设置到引擎
        eng->set_long_short_ratio_series(series);
        return 0;
        
    } catch (const std::exception& e) {
        SetStrategyError("Failed to set long short ratio series: %s", e.what());
        return -2;
    }
}

// ============================================================================
// 信号生成
// ============================================================================

PROPHET_API int Prophet_GetSignal(
    void* engine,
    double current_price,
    int64_t current_time,
    NativeSignal* out_signal
) {
    // 参数验证
    if (!engine) {
        SetStrategyError("Engine pointer is null");
        PROPHET_LOG_ERROR("Signal", "引擎指针为空");
        return -1;
    }
    
    if (!out_signal) {
        SetStrategyError("Output signal pointer is null");
        PROPHET_LOG_ERROR("Signal", "输出信号指针为空");
        return -1;
    }
    
    PROPHET_LOG_TRACE_FMT("Signal", "获取信号: 价格=" << current_price << ", 时间=" << current_time);
    
    try {
        auto* eng = static_cast<prophet::core::Engine*>(engine);
        
        prophet::Signal signal = eng->get_signal(current_price, current_time);
        
        PROPHET_LOG_DEBUG_FMT("Signal", "信号生成成功: " << signal.action 
                              << ", 置信度=" << signal.confidence);
        
        // 初始化输出结构
        std::memset(out_signal, 0, sizeof(NativeSignal));
        
        // 转换数据
        std::strncpy(out_signal->Action, signal.action.c_str(), 
                    sizeof(out_signal->Action) - 1);
        out_signal->Action[sizeof(out_signal->Action) - 1] = '\0';
        
        out_signal->Confidence = signal.confidence;
        out_signal->TakeProfit = signal.take_profit;
        out_signal->StopLoss = signal.stop_loss;
        
        std::strncpy(out_signal->Reason, signal.reason.c_str(), 
                    sizeof(out_signal->Reason) - 1);
        out_signal->Reason[sizeof(out_signal->Reason) - 1] = '\0';
        
        // Configs JSON
        std::string configs_json = "{";
        bool first = true;
        for (const auto& [key, value] : signal.configs) {
            if (!first) configs_json += ",";
            first = false;
            
            configs_json += "\"" + key + "\":";
            if (value.type == prophet::ValueType::NUMBER) {
                configs_json += std::to_string(value.toNumber());
            } else if (value.type == prophet::ValueType::STRING) {
                configs_json += "\"" + value.toString() + "\"";
            } else if (value.type == prophet::ValueType::BOOLEAN) {
                configs_json += value.toBool() ? "true" : "false";
            }
        }
        configs_json += "}";
        
        std::strncpy(out_signal->ConfigsJson, configs_json.c_str(), 
                    sizeof(out_signal->ConfigsJson) - 1);
        out_signal->ConfigsJson[sizeof(out_signal->ConfigsJson) - 1] = '\0';
        
        // Indicators JSON
        std::string indicators_json = "{";
        first = true;
        for (const auto& [key, value] : signal.indicators) {
            if (!first) indicators_json += ",";
            first = false;
            
            indicators_json += "\"" + key + "\":";
            if (value.type == prophet::ValueType::NUMBER) {
                indicators_json += std::to_string(value.toNumber());
            } else if (value.type == prophet::ValueType::STRING) {
                indicators_json += "\"" + value.toString() + "\"";
            } else if (value.type == prophet::ValueType::BOOLEAN) {
                indicators_json += value.toBool() ? "true" : "false";
            }
        }
        indicators_json += "}";
        
        std::strncpy(out_signal->IndicatorsJson, indicators_json.c_str(), 
                    sizeof(out_signal->IndicatorsJson) - 1);
        out_signal->IndicatorsJson[sizeof(out_signal->IndicatorsJson) - 1] = '\0';
        
        // v11.0: Trend
        std::strncpy(out_signal->Trend, signal.trend.c_str(), 
                    sizeof(out_signal->Trend) - 1);
        out_signal->Trend[sizeof(out_signal->Trend) - 1] = '\0';
        
        // v11.0: Debug JSON
        // debug是一个vector<unordered_map<string, Value>>，需要序列化为JSON数组
        std::string debug_json = "[";
        first = true;
        for (const auto& debug_entry : signal.debug) {
            if (!first) debug_json += ",";
            first = false;
            
            debug_json += "{";
            bool first_field = true;
            for (const auto& [key, value] : debug_entry) {
                if (!first_field) debug_json += ",";
                first_field = false;
                
                debug_json += "\"" + key + "\":";
                if (value.type == prophet::ValueType::NUMBER) {
                    debug_json += std::to_string(value.toNumber());
                } else if (value.type == prophet::ValueType::STRING) {
                    // 转义JSON字符串中的特殊字符
                    std::string escaped_str = value.toString();
                    // 简单的转义处理（引号和反斜杠）
                    size_t pos = 0;
                    while ((pos = escaped_str.find("\\", pos)) != std::string::npos) {
                        escaped_str.replace(pos, 1, "\\\\");
                        pos += 2;
                    }
                    pos = 0;
                    while ((pos = escaped_str.find("\"", pos)) != std::string::npos) {
                        escaped_str.replace(pos, 1, "\\\"");
                        pos += 2;
                    }
                    debug_json += "\"" + escaped_str + "\"";
                } else if (value.type == prophet::ValueType::BOOLEAN) {
                    debug_json += value.toBool() ? "true" : "false";
                }
            }
            debug_json += "}";
        }
        debug_json += "]";
        
        std::strncpy(out_signal->DebugJson, debug_json.c_str(), 
                    sizeof(out_signal->DebugJson) - 1);
        out_signal->DebugJson[sizeof(out_signal->DebugJson) - 1] = '\0';
        
        // 🆕 v4.0: Klines JSON
        // klines是一个unordered_map<string, Kline>，需要序列化为JSON对象
        std::string klines_json = "{";
        first = true;
        for (const auto& [timeframe, kline] : signal.klines) {
            if (!first) klines_json += ",";
            first = false;
            
            // 转义timeframe中的特殊字符
            std::string escaped_tf = timeframe;
            size_t pos = 0;
            while ((pos = escaped_tf.find("\"", pos)) != std::string::npos) {
                escaped_tf.replace(pos, 1, "\\\"");
                pos += 2;
            }
            
            klines_json += "\"" + escaped_tf + "\":{";
            klines_json += "\"open\":" + std::to_string(kline.open) + ",";
            klines_json += "\"high\":" + std::to_string(kline.high) + ",";
            klines_json += "\"low\":" + std::to_string(kline.low) + ",";
            klines_json += "\"close\":" + std::to_string(kline.close) + ",";
            klines_json += "\"volume\":" + std::to_string(kline.volume) + ",";
            klines_json += "\"open_time\":" + std::to_string(kline.open_time) + ",";
            klines_json += "\"close_time\":" + std::to_string(kline.close_time);
            klines_json += "}";
        }
        klines_json += "}";
        
        std::strncpy(out_signal->KlinesJson, klines_json.c_str(), 
                    sizeof(out_signal->KlinesJson) - 1);
        out_signal->KlinesJson[sizeof(out_signal->KlinesJson) - 1] = '\0';
        
        PROPHET_LOG_TRACE_FMT("Signal", "信号完成: Trend=" << signal.trend 
                              << ", Debug=" << signal.debug.size()
                              << ", Klines=" << signal.klines.size());
        
        return 0;
        
    } catch (const prophet::DSLException& e) {
        SetStrategyError("DSL Error: %s", e.what());
        PROPHET_LOG_ERROR_FMT("Signal", "DSL异常: " << e.what());
        return -2;
    } catch (const std::exception& e) {
        SetStrategyError("Exception: %s", e.what());
        PROPHET_LOG_ERROR_FMT("Signal", "标准异常: " << e.what());
        return -2;
    } catch (...) {
        SetStrategyError("Unknown error occurred while getting signal");
        PROPHET_LOG_ERROR("Signal", "未知异常");
        return -1;
    }
}

// ============================================================================
// 辅助方法
// ============================================================================

PROPHET_API int Prophet_GetRuleCount(void* engine) {
    if (!engine) {
        return -1;
    }
    
    try {
        auto* eng = static_cast<prophet::core::Engine*>(engine);
        return static_cast<int>(eng->get_rule_count());
    } catch (...) {
        return -1;
    }
}

PROPHET_API const char* Prophet_GetRulesString(void* engine) {
    if (!engine) {
        return "";
    }
    
    try {
        auto* eng = static_cast<prophet::core::Engine*>(engine);
        // 注意：这里返回的字符串生命周期需要小心处理
        // 实际应该使用静态buffer或让调用者提供buffer
        static thread_local std::string rules_str;
        rules_str = eng->get_rules_string();
        return rules_str.c_str();
    } catch (...) {
        return "";
    }
}

// ============================================================================
// 错误处理
// ============================================================================

PROPHET_API const char* Prophet_GetLastStrategyError() {
    return g_last_strategy_error;
}

