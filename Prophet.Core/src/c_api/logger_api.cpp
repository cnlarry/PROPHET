/*
 * ============================================================================
 * 文件名：logger_api.cpp
 * 功能说明：Prophet Core 日志系统 C API 实现
 * 
 * 注意：所有字符串参数都假定为UTF-8编码
 * ============================================================================
 */

#include "prophet/c_api.h"
#include "prophet/logging/logger.hpp"
#include <string>

// ============================================================================
// 日志级别转换
// ============================================================================

namespace {
    prophet::LogLevel to_cpp_level(ProphetLogLevel level) {
        switch (level) {
            case PROPHET_LOG_TRACE: return prophet::LogLevel::TRACE;
            case PROPHET_LOG_DEBUG: return prophet::LogLevel::DEBUG;
            case PROPHET_LOG_INFO:  return prophet::LogLevel::INFO;
            case PROPHET_LOG_WARN:  return prophet::LogLevel::WARN;
            case PROPHET_LOG_ERROR: return prophet::LogLevel::ERROR;
            case PROPHET_LOG_FATAL: return prophet::LogLevel::FATAL;
            case PROPHET_LOG_OFF:   return prophet::LogLevel::OFF;
            default:                return prophet::LogLevel::INFO;
        }
    }
    
    ProphetLogLevel to_c_level(prophet::LogLevel level) {
        switch (level) {
            case prophet::LogLevel::TRACE: return PROPHET_LOG_TRACE;
            case prophet::LogLevel::DEBUG: return PROPHET_LOG_DEBUG;
            case prophet::LogLevel::INFO:  return PROPHET_LOG_INFO;
            case prophet::LogLevel::WARN:  return PROPHET_LOG_WARN;
            case prophet::LogLevel::ERROR: return PROPHET_LOG_ERROR;
            case prophet::LogLevel::FATAL: return PROPHET_LOG_FATAL;
            case prophet::LogLevel::OFF:   return PROPHET_LOG_OFF;
            default:                       return PROPHET_LOG_INFO;
        }
    }
}

// ============================================================================
// 日志系统API实现
// ============================================================================

PROPHET_API void Prophet_SetLogLevel(ProphetLogLevel level) {
    prophet::Logger::instance().set_level(to_cpp_level(level));
}

PROPHET_API ProphetLogLevel Prophet_GetLogLevel() {
    return to_c_level(prophet::Logger::instance().get_level());
}

PROPHET_API void Prophet_SetConsoleOutput(int enabled) {
    prophet::Logger::instance().set_console_output(enabled != 0);
}

PROPHET_API void Prophet_SetFileOutput(int enabled) {
    prophet::Logger::instance().set_file_output(enabled != 0);
}

PROPHET_API int Prophet_SetLogFile(const char* filepath) {
    if (!filepath) {
        return -1;
    }
    
    bool success = prophet::Logger::instance().set_log_file(std::string(filepath));
    return success ? 0 : -1;
}

PROPHET_API void Prophet_CloseLogFile() {
    prophet::Logger::instance().close_log_file();
}

PROPHET_API void Prophet_Log(ProphetLogLevel level, const char* module, const char* message) {
    if (!module || !message) {
        return;
    }
    
    prophet::Logger::instance().log(
        to_cpp_level(level),
        std::string(module),
        std::string(message)
    );
}

PROPHET_API void Prophet_FlushLog() {
    prophet::Logger::instance().flush();
}

