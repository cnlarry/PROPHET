/*
 * ============================================================================
 * 文件名：logger.hpp
 * 功能说明：Prophet Core 统一日志系统
 * 
 * 特性：
 *   - 多日志级别（TRACE, DEBUG, INFO, WARN, ERROR, FATAL）
 *   - 同时输出到控制台和文件
 *   - 线程安全
 *   - 可配置的日志级别过滤
 *   - 高性能（带缓冲区）
 * ============================================================================
 */

#ifndef PROPHET_LOGGER_HPP
#define PROPHET_LOGGER_HPP

#include <string>
#include <fstream>
#include <mutex>
#include <memory>
#include <sstream>
#include <chrono>
#include <iomanip>

namespace prophet {

// ============================================================================
// 日志级别枚举
// ============================================================================
enum class LogLevel {
    TRACE = 0,  // 最详细的追踪信息
    DEBUG = 1,  // 调试信息
    INFO  = 2,  // 一般信息
    WARN  = 3,  // 警告
    ERROR = 4,  // 错误
    FATAL = 5,  // 致命错误
    OFF   = 6   // 关闭日志
};

// ============================================================================
// 日志器类
// ============================================================================
class Logger {
public:
    // 获取全局日志器实例（单例）
    static Logger& instance();
    
    // 禁用拷贝和移动
    Logger(const Logger&) = delete;
    Logger& operator=(const Logger&) = delete;
    Logger(Logger&&) = delete;
    Logger& operator=(Logger&&) = delete;
    
    // 设置日志级别
    void set_level(LogLevel level);
    
    // 获取当前日志级别
    LogLevel get_level() const;
    
    // 启用/禁用控制台输出
    void set_console_output(bool enabled);
    
    // 启用/禁用文件输出
    void set_file_output(bool enabled);
    
    // 设置日志文件路径
    bool set_log_file(const std::string& filepath);
    
    // 关闭日志文件
    void close_log_file();
    
    // 日志记录方法
    void trace(const std::string& module, const std::string& message);
    void debug(const std::string& module, const std::string& message);
    void info(const std::string& module, const std::string& message);
    void warn(const std::string& module, const std::string& message);
    void error(const std::string& module, const std::string& message);
    void fatal(const std::string& module, const std::string& message);
    
    // 通用日志方法
    void log(LogLevel level, const std::string& module, const std::string& message);
    
    // 刷新缓冲区
    void flush();
    
private:
    Logger();
    ~Logger();
    
    // 格式化日志消息
    std::string format_message(LogLevel level, const std::string& module, const std::string& message);
    
    // 获取日志级别字符串
    static const char* level_to_string(LogLevel level);
    
    // 获取当前时间戳字符串
    static std::string get_timestamp();
    
    // 成员变量
    LogLevel current_level_;
    bool console_output_;
    bool file_output_;
    std::ofstream log_file_;
    mutable std::mutex mutex_;
};

// ============================================================================
// 便捷宏定义
// ============================================================================
#define PROPHET_LOG_TRACE(module, msg) \
    prophet::Logger::instance().trace(module, msg)

#define PROPHET_LOG_DEBUG(module, msg) \
    prophet::Logger::instance().debug(module, msg)

#define PROPHET_LOG_INFO(module, msg) \
    prophet::Logger::instance().info(module, msg)

#define PROPHET_LOG_WARN(module, msg) \
    prophet::Logger::instance().warn(module, msg)

#define PROPHET_LOG_ERROR(module, msg) \
    prophet::Logger::instance().error(module, msg)

#define PROPHET_LOG_FATAL(module, msg) \
    prophet::Logger::instance().fatal(module, msg)

// 带格式化支持的宏
#define PROPHET_LOG_TRACE_FMT(module, ...) \
    do { \
        std::ostringstream oss; \
        oss << __VA_ARGS__; \
        prophet::Logger::instance().trace(module, oss.str()); \
    } while(0)

#define PROPHET_LOG_DEBUG_FMT(module, ...) \
    do { \
        std::ostringstream oss; \
        oss << __VA_ARGS__; \
        prophet::Logger::instance().debug(module, oss.str()); \
    } while(0)

#define PROPHET_LOG_INFO_FMT(module, ...) \
    do { \
        std::ostringstream oss; \
        oss << __VA_ARGS__; \
        prophet::Logger::instance().info(module, oss.str()); \
    } while(0)

#define PROPHET_LOG_WARN_FMT(module, ...) \
    do { \
        std::ostringstream oss; \
        oss << __VA_ARGS__; \
        prophet::Logger::instance().warn(module, oss.str()); \
    } while(0)

#define PROPHET_LOG_ERROR_FMT(module, ...) \
    do { \
        std::ostringstream oss; \
        oss << __VA_ARGS__; \
        prophet::Logger::instance().error(module, oss.str()); \
    } while(0)

#define PROPHET_LOG_FATAL_FMT(module, ...) \
    do { \
        std::ostringstream oss; \
        oss << __VA_ARGS__; \
        prophet::Logger::instance().fatal(module, oss.str()); \
    } while(0)

} // namespace prophet

#endif // PROPHET_LOGGER_HPP

