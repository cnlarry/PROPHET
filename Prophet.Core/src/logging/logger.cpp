/*
 * ============================================================================
 * 文件名：logger.cpp
 * 功能说明：Prophet Core 统一日志系统实现
 * ============================================================================
 */

#include "prophet/logging/logger.hpp"
#include <iostream>
#include <ctime>
#include <algorithm>
#include <locale>
#include <codecvt>

#ifdef _WIN32
// 防止Windows.h的min/max/ERROR宏冲突
#ifndef NOMINMAX
#define NOMINMAX
#endif
#include <windows.h>
// 取消ERROR宏定义（Windows.h定义的宏与LogLevel::ERROR冲突）
#ifdef ERROR
#undef ERROR
#endif
#endif

namespace prophet {

// ============================================================================
// 静态辅助函数
// ============================================================================

const char* Logger::level_to_string(LogLevel level) {
    switch (level) {
        case LogLevel::TRACE: return "TRACE";
        case LogLevel::DEBUG: return "DEBUG";
        case LogLevel::INFO:  return "INFO ";
        case LogLevel::WARN:  return "WARN ";
        case LogLevel::ERROR: return "ERROR";
        case LogLevel::FATAL: return "FATAL";
        case LogLevel::OFF:   return "OFF  ";
        default:              return "?????";
    }
}

std::string Logger::get_timestamp() {
    auto now = std::chrono::system_clock::now();
    auto now_time_t = std::chrono::system_clock::to_time_t(now);
    auto now_ms = std::chrono::duration_cast<std::chrono::milliseconds>(
        now.time_since_epoch()) % 1000;
    
    std::tm tm_buf;
#ifdef _WIN32
    localtime_s(&tm_buf, &now_time_t);
#else
    localtime_r(&now_time_t, &tm_buf);
#endif
    
    std::ostringstream oss;
    oss << std::put_time(&tm_buf, "%Y-%m-%d %H:%M:%S")
        << '.' << std::setfill('0') << std::setw(3) << now_ms.count();
    return oss.str();
}

// ============================================================================
// Logger 实现
// ============================================================================

Logger& Logger::instance() {
    static Logger instance;
    return instance;
}

Logger::Logger()
    : current_level_(LogLevel::INFO)
    , console_output_(true)
    , file_output_(false)
{
    // 默认配置：INFO级别，仅控制台输出
    
#ifdef _WIN32
    // Windows控制台设置UTF-8输出
    SetConsoleOutputCP(CP_UTF8);
#endif
}

Logger::~Logger() {
    flush();
    close_log_file();
}

void Logger::set_level(LogLevel level) {
    std::lock_guard<std::mutex> lock(mutex_);
    current_level_ = level;
}

LogLevel Logger::get_level() const {
    std::lock_guard<std::mutex> lock(mutex_);
    return current_level_;
}

void Logger::set_console_output(bool enabled) {
    std::lock_guard<std::mutex> lock(mutex_);
    console_output_ = enabled;
}

void Logger::set_file_output(bool enabled) {
    std::lock_guard<std::mutex> lock(mutex_);
    file_output_ = enabled;
}

bool Logger::set_log_file(const std::string& filepath) {
    std::lock_guard<std::mutex> lock(mutex_);
    
    // 关闭旧文件
    if (log_file_.is_open()) {
        log_file_.close();
    }
    
    // 打开新文件（追加模式）
    log_file_.open(filepath, std::ios::out | std::ios::app | std::ios::binary);
    
    if (!log_file_.is_open()) {
        std::cerr << "[Logger] 无法打开日志文件: " << filepath << std::endl;
        file_output_ = false;
        return false;
    }
    
    // 设置为UTF-8编码（Windows）
#ifdef _WIN32
    // Windows下设置UTF-8输出
    log_file_.imbue(std::locale(""));
#endif
    
    file_output_ = true;
    
    // 如果是新文件，写入UTF-8 BOM（帮助Windows识别编码）
    auto current_pos = log_file_.tellp();
    if (current_pos == 0) {
        // 写入UTF-8 BOM: EF BB BF
        const unsigned char bom[] = { 0xEF, 0xBB, 0xBF };
        log_file_.write(reinterpret_cast<const char*>(bom), sizeof(bom));
    }
    
    // 写入分隔符
    log_file_ << "\n========================================\n";
    log_file_ << "日志会话开始: " << get_timestamp() << "\n";
    log_file_ << "========================================\n";
    log_file_.flush();
    
    return true;
}

void Logger::close_log_file() {
    std::lock_guard<std::mutex> lock(mutex_);
    
    if (log_file_.is_open()) {
        log_file_ << "========================================\n";
        log_file_ << "日志会话结束: " << get_timestamp() << "\n";
        log_file_ << "========================================\n\n";
        log_file_.close();
    }
    
    file_output_ = false;
}

void Logger::trace(const std::string& module, const std::string& message) {
    log(LogLevel::TRACE, module, message);
}

void Logger::debug(const std::string& module, const std::string& message) {
    log(LogLevel::DEBUG, module, message);
}

void Logger::info(const std::string& module, const std::string& message) {
    log(LogLevel::INFO, module, message);
}

void Logger::warn(const std::string& module, const std::string& message) {
    log(LogLevel::WARN, module, message);
}

void Logger::error(const std::string& module, const std::string& message) {
    log(LogLevel::ERROR, module, message);
}

void Logger::fatal(const std::string& module, const std::string& message) {
    log(LogLevel::FATAL, module, message);
}

void Logger::log(LogLevel level, const std::string& module, const std::string& message) {
    std::lock_guard<std::mutex> lock(mutex_);
    
    // 级别过滤
    if (level < current_level_) {
        return;
    }
    
    // 格式化消息
    std::string formatted = format_message(level, module, message);
    
    // 输出到控制台
    if (console_output_) {
        if (level >= LogLevel::ERROR) {
            std::cerr << formatted << std::endl;
        } else {
            std::cout << formatted << std::endl;
        }
    }
    
    // 输出到文件
    if (file_output_ && log_file_.is_open()) {
        log_file_ << formatted << std::endl;
    }
}

void Logger::flush() {
    std::lock_guard<std::mutex> lock(mutex_);
    
    if (console_output_) {
        std::cout.flush();
        std::cerr.flush();
    }
    
    if (file_output_ && log_file_.is_open()) {
        log_file_.flush();
    }
}

std::string Logger::format_message(LogLevel level, const std::string& module, const std::string& message) {
    std::ostringstream oss;
    
    // [时间戳] [级别] [模块] 消息
    oss << "[" << get_timestamp() << "] "
        << "[" << level_to_string(level) << "] "
        << "[" << std::setw(8) << std::left << module.substr(0, 8) << "] "
        << message;
    
    return oss.str();
}

} // namespace prophet

