using System;
using System.IO;
using System.Runtime.InteropServices;

namespace Prophet.Client.Services;

/// <summary>
/// Prophet Core 日志服务
/// 封装C++核心引擎的日志功能
/// </summary>
public static class LogService
{
    private const string DLL_NAME = "prophet_core.dll";
    
    /// <summary>
    /// 日志级别枚举
    /// </summary>
    public enum LogLevel
    {
        TRACE = 0,  // 最详细的追踪信息
        DEBUG = 1,  // 调试信息
        INFO  = 2,  // 一般信息
        WARN  = 3,  // 警告
        ERROR = 4,  // 错误
        FATAL = 5,  // 致命错误
        OFF   = 6   // 关闭日志
    }
    
    #region P/Invoke 声明
    
    [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
    private static extern void Prophet_SetLogLevel(LogLevel level);
    
    [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
    private static extern LogLevel Prophet_GetLogLevel();
    
    [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
    private static extern void Prophet_SetConsoleOutput(int enabled);
    
    [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
    private static extern void Prophet_SetFileOutput(int enabled);
    
    [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    private static extern int Prophet_SetLogFile([MarshalAs(UnmanagedType.LPUTF8Str)] string filepath);
    
    [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
    private static extern void Prophet_CloseLogFile();
    
    [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    private static extern void Prophet_Log(
        LogLevel level,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string module,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string message
    );
    
    [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
    private static extern void Prophet_FlushLog();
    
    #endregion
    
    #region 公共方法
    
    /// <summary>
    /// 设置日志级别
    /// </summary>
    public static void SetLevel(LogLevel level)
    {
        Prophet_SetLogLevel(level);
    }
    
    /// <summary>
    /// 获取当前日志级别
    /// </summary>
    public static LogLevel GetLevel()
    {
        return Prophet_GetLogLevel();
    }
    
    /// <summary>
    /// 启用/禁用控制台输出
    /// </summary>
    public static void SetConsoleOutput(bool enabled)
    {
        Prophet_SetConsoleOutput(enabled ? 1 : 0);
    }
    
    /// <summary>
    /// 启用/禁用文件输出
    /// </summary>
    public static void SetFileOutput(bool enabled)
    {
        Prophet_SetFileOutput(enabled ? 1 : 0);
    }
    
    /// <summary>
    /// 设置日志文件路径
    /// </summary>
    public static bool SetLogFile(string filepath)
    {
        try
        {
            // 确保目录存在
            var directory = Path.GetDirectoryName(filepath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            
            int result = Prophet_SetLogFile(filepath);
            if (result == 0)
            {
                Console.WriteLine($"✅ 日志文件已设置: {filepath}");
                return true;
            }
            else
            {
                Console.WriteLine($"❌ 无法设置日志文件: {filepath}");
                return false;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ 设置日志文件失败: {ex.Message}");
            return false;
        }
    }
    
    /// <summary>
    /// 关闭日志文件
    /// </summary>
    public static void CloseLogFile()
    {
        Prophet_CloseLogFile();
    }
    
    /// <summary>
    /// 写入TRACE级别日志
    /// </summary>
    public static void Trace(string module, string message)
    {
        Prophet_Log(LogLevel.TRACE, module, message);
    }
    
    /// <summary>
    /// 写入DEBUG级别日志
    /// </summary>
    public static void Debug(string module, string message)
    {
        Prophet_Log(LogLevel.DEBUG, module, message);
    }
    
    /// <summary>
    /// 写入INFO级别日志
    /// </summary>
    public static void Info(string module, string message)
    {
        Prophet_Log(LogLevel.INFO, module, message);
    }
    
    /// <summary>
    /// 写入WARN级别日志
    /// </summary>
    public static void Warn(string module, string message)
    {
        Prophet_Log(LogLevel.WARN, module, message);
    }
    
    /// <summary>
    /// 写入ERROR级别日志
    /// </summary>
    public static void Error(string module, string message)
    {
        Prophet_Log(LogLevel.ERROR, module, message);
    }
    
    /// <summary>
    /// 写入FATAL级别日志
    /// </summary>
    public static void Fatal(string module, string message)
    {
        Prophet_Log(LogLevel.FATAL, module, message);
    }
    
    /// <summary>
    /// 写入日志（通用方法）
    /// </summary>
    public static void Log(LogLevel level, string module, string message)
    {
        Prophet_Log(level, module, message);
    }
    
    /// <summary>
    /// 刷新日志缓冲区
    /// </summary>
    public static void Flush()
    {
        Prophet_FlushLog();
    }
    
    /// <summary>
    /// 初始化日志系统（推荐配置）
    /// </summary>
    /// <param name="level">日志级别</param>
    /// <param name="enableConsole">是否启用控制台输出</param>
    /// <param name="enableFile">是否启用文件输出</param>
    /// <param name="logFilePath">日志文件路径（可选）</param>
    public static void Initialize(
        LogLevel level = LogLevel.INFO,
        bool enableConsole = false,
        bool enableFile = true,
        string? logFilePath = null)
    {
        if (enableConsole)
        {
            Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            Console.WriteLine("🔧 初始化 Prophet Core 日志系统");
            Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        }
        
        SetLevel(level);
        SetConsoleOutput(enableConsole);
        
        if (enableFile)
        {
            if (string.IsNullOrEmpty(logFilePath))
            {
                // 默认日志路径：应用程序目录/logs/prophet_core.log
                var logDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
                logFilePath = Path.Combine(logDir, $"prophet_core_{DateTime.Now:yyyyMMdd}.log");
            }
            
            SetLogFile(logFilePath);
            
            if (!enableConsole)
            {
                // 如果控制台输出关闭，提示日志文件位置
                Console.WriteLine($"📝 Prophet Core 日志文件: {logFilePath}");
            }
        }
        
        SetFileOutput(enableFile);
        
        if (enableConsole)
        {
            Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            Console.WriteLine("✅ 日志系统初始化完成");
            Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        }
        
        // 写入第一条日志
        Info("LogService", "Prophet Core 日志系统已启动");
    }
    
    #endregion
}

