using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Prophet.Client.Services;

/// <summary>
/// 统一日志辅助类
/// 简化 LogService 调用，提供更便捷的日志记录接口
/// </summary>
/// <remarks>
/// <para>
/// 使用示例：
/// <code>
/// Logger.Info("编辑器初始化完成");
/// Logger.Error("验证失败", ex);
/// Logger.Debug("当前偏移量: {0}", offset);
/// </code>
/// </para>
/// <para>
/// 特性：
/// <list type="bullet">
/// <item><description>自动获取调用者类名作为模块名</description></item>
/// <item><description>支持格式化字符串</description></item>
/// <item><description>DEBUG 模式同时输出到控制台和日志文件</description></item>
/// <item><description>RELEASE 模式只输出到日志文件</description></item>
/// <item><description>异常自动附加堆栈跟踪</description></item>
/// </list>
/// </para>
/// </remarks>
public static class Logger
{
    private static bool _isInitialized = false;
    private static readonly object _initLock = new object();

    /// <summary>
    /// 确保日志系统已初始化
    /// </summary>
    private static void EnsureInitialized()
    {
        if (_isInitialized) return;

        lock (_initLock)
        {
            if (_isInitialized) return;

            try
            {
#if DEBUG
                // 开发模式：控制台 + 文件
                LogService.Initialize(
                    level: LogService.LogLevel.DEBUG,
                    enableConsole: true,
                    enableFile: true
                );
#else
                // 生产模式：仅文件
                LogService.Initialize(
                    level: LogService.LogLevel.INFO,
                    enableConsole: false,
                    enableFile: true
                );
#endif
                _isInitialized = true;
            }
            catch (Exception ex)
            {
                // LogService 初始化失败（例如 prophet_core.dll 不存在），降级到纯控制台模式
                Console.WriteLine($"⚠️ [Logger] LogService 初始化失败，降级到控制台模式: {ex.Message}");
                _isInitialized = true; // 标记为已初始化，避免重复尝试
            }
        }
    }

    /// <summary>
    /// 获取调用者的类名作为模块名
    /// </summary>
    private static string GetCallerModule([CallerFilePath] string filePath = "")
    {
        if (string.IsNullOrEmpty(filePath))
            return "Unknown";

        // 从文件路径提取类名（去掉路径和扩展名）
        var fileName = System.IO.Path.GetFileNameWithoutExtension(filePath);
        return fileName;
    }

    /// <summary>
    /// 写入 TRACE 级别日志
    /// </summary>
    public static void Trace(string message, [CallerFilePath] string filePath = "")
    {
        EnsureInitialized();
        var module = GetCallerModule(filePath);

#if DEBUG
        Console.WriteLine($"[TRACE] [{module}] {message}");
#endif

        try
        {
            LogService.Trace(module, message);
        }
        catch
        {
            // LogService 调用失败，忽略（已降级到控制台）
        }
    }

    /// <summary>
    /// 写入 DEBUG 级别日志
    /// </summary>
    public static void Debug(string message, [CallerFilePath] string filePath = "")
    {
        EnsureInitialized();
        var module = GetCallerModule(filePath);

#if DEBUG
        Console.WriteLine($"[DEBUG] [{module}] {message}");
#endif

        try
        {
            LogService.Debug(module, message);
        }
        catch
        {
            // LogService 调用失败，忽略
        }
    }

    /// <summary>
    /// 写入 DEBUG 级别日志（支持格式化）
    /// </summary>
    public static void Debug(string format, params object[] args)
    {
        Debug(string.Format(format, args));
    }

    /// <summary>
    /// 写入 INFO 级别日志
    /// </summary>
    public static void Info(string message, [CallerFilePath] string filePath = "")
    {
        EnsureInitialized();
        var module = GetCallerModule(filePath);

#if DEBUG
        Console.WriteLine($"[INFO] [{module}] {message}");
#endif

        try
        {
            LogService.Info(module, message);
        }
        catch
        {
            // 降级到控制台
            Console.WriteLine($"[INFO] [{module}] {message}");
        }
    }

    /// <summary>
    /// 写入 INFO 级别日志（支持格式化）
    /// </summary>
    public static void Info(string format, params object[] args)
    {
        Info(string.Format(format, args));
    }

    /// <summary>
    /// 写入 WARN 级别日志
    /// </summary>
    public static void Warn(string message, [CallerFilePath] string filePath = "")
    {
        EnsureInitialized();
        var module = GetCallerModule(filePath);

        // 警告始终输出到控制台
        Console.WriteLine($"⚠️ [WARN] [{module}] {message}");

        try
        {
            LogService.Warn(module, message);
        }
        catch
        {
            // 已输出到控制台
        }
    }

    /// <summary>
    /// 写入 WARN 级别日志（支持格式化）
    /// </summary>
    public static void Warn(string format, params object[] args)
    {
        Warn(string.Format(format, args));
    }

    /// <summary>
    /// 写入 ERROR 级别日志
    /// </summary>
    public static void Error(string message, [CallerFilePath] string filePath = "")
    {
        EnsureInitialized();
        var module = GetCallerModule(filePath);

        // 错误始终输出到控制台
        Console.WriteLine($"❌ [ERROR] [{module}] {message}");

        try
        {
            LogService.Error(module, message);
        }
        catch
        {
            // 已输出到控制台
        }
    }

    /// <summary>
    /// 写入 ERROR 级别日志（带异常）
    /// </summary>
    public static void Error(string message, Exception ex, [CallerFilePath] string filePath = "")
    {
        var fullMessage = $"{message}\n异常类型: {ex.GetType().Name}\n异常信息: {ex.Message}\n堆栈跟踪:\n{ex.StackTrace}";
        Error(fullMessage, filePath);
    }

    /// <summary>
    /// 写入 ERROR 级别日志（仅异常）
    /// </summary>
    public static void Error(Exception ex, [CallerFilePath] string filePath = "")
    {
        Error("发生异常", ex, filePath);
    }

    /// <summary>
    /// 写入 FATAL 级别日志
    /// </summary>
    public static void Fatal(string message, [CallerFilePath] string filePath = "")
    {
        EnsureInitialized();
        var module = GetCallerModule(filePath);

        // 致命错误始终输出到控制台（红色突出）
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"💀 [FATAL] [{module}] {message}");
        Console.ResetColor();

        try
        {
            LogService.Fatal(module, message);
        }
        catch
        {
            // 已输出到控制台
        }
    }

    /// <summary>
    /// 写入 FATAL 级别日志（带异常）
    /// </summary>
    public static void Fatal(string message, Exception ex, [CallerFilePath] string filePath = "")
    {
        var fullMessage = $"{message}\n异常类型: {ex.GetType().Name}\n异常信息: {ex.Message}\n堆栈跟踪:\n{ex.StackTrace}";
        Fatal(fullMessage, filePath);
    }

    /// <summary>
    /// 刷新日志缓冲区
    /// </summary>
    public static void Flush()
    {
        try
        {
            LogService.Flush();
        }
        catch
        {
            // 忽略
        }
    }

    /// <summary>
    /// 应用退出时调用，确保所有日志已写入
    /// </summary>
    public static void Shutdown()
    {
        try
        {
            Flush();
            LogService.CloseLogFile();
        }
        catch
        {
            // 忽略
        }
    }
}

