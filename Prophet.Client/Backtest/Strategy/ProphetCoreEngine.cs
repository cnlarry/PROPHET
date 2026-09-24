using System;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.IO;
using Prophet.Client.Services;

namespace Prophet.Client.Backtest.Strategy;

/// <summary>
/// Prophet.Core C API 绑定
/// 基于 prophet::core::Engine
/// </summary>
public static class ProphetCoreEngine
{
    private const string DLL_NAME = "prophet_core.dll";
    
    private static bool _isInitialized = false;
    private static string? _initializationError = null;
    
    /// <summary>
    /// 静态构造函数：检查DLL是否可用
    /// </summary>
    static ProphetCoreEngine()
    {
        try
        {
            // 检查DLL文件是否存在
            var dllPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, DLL_NAME);
            
            if (!File.Exists(dllPath))
            {
                _initializationError = $"找不到 {DLL_NAME}，请确保DLL文件在程序目录中";
                Console.WriteLine($"❌ {_initializationError}");
                Console.WriteLine($"   搜索路径: {dllPath}");
                return;
            }
            
            _isInitialized = true;
        }
        catch (Exception ex)
        {
            _initializationError = $"初始化 Prophet.Core 绑定失败: {ex.Message}";
            Console.WriteLine($"❌ {_initializationError}");
            Console.WriteLine($"   堆栈跟踪: {ex.StackTrace}");
        }
    }
    
    /// <summary>
    /// 检查引擎绑定是否已初始化
    /// </summary>
    public static void EnsureInitialized()
    {
        if (!_isInitialized)
        {
            throw new InvalidOperationException(_initializationError ?? "Prophet.Core 引擎绑定未初始化");
        }
    }
    
    #region 数据结构
    
    /// <summary>
    /// 交易信号（对应C++ NativeSignal结构，字段顺序和大小必须严格匹配）
    /// v11.0: 新增调试信息支持（Trend, DebugJson）
    /// </summary>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    public struct Signal
    {
        // === 基本信息 ===
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 20)]
        public string Action;           // "BUY", "SELL", "HOLD"
        
        public double Confidence;       // 0.0 - 1.0
        
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 20)]
        public string Trend;            // v11.0: 趋势（BULLISH/BEARISH/NEUTRAL）
        
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 512)]
        public string Reason;           // 触发原因
        
        // === 止损止盈 ===
        public double TakeProfit;       // 止盈价格
        public double StopLoss;         // 止损价格
        
        // === 数据快照（JSON格式） ===
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 2048)]
        public string ConfigsJson;      // 配置参数快照
        
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 4096)]
        public string IndicatorsJson;   // 指标快照（已废弃）
        
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 8192)]
        public string IndicatorSnapshotsJson;  // 结构化指标快照（新增）
        
        // === v11.0 调试信息（JSON格式） ===
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 8192)]
        public string DebugJson;        // 调试信息数组（规则评估过程、决策详情）
        
        // === v4.0 K线数据快照（JSON格式） ===
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 8192)]
        public string KlinesJson;       // K线数据快照（每个时间框架的当前K线）
    }
    
    /// <summary>
    /// 环境变量值（对应C++ Value）
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct EnvValue
    {
        public double NumberValue;
        public int IsNumber;  // 1 = number, 0 = other
    }
    
    #endregion
    
    #region 引擎生命周期
    
    /// <summary>
    /// 创建引擎实例
    /// </summary>
    /// <param name="dslCode">DSL策略代码</param>
    /// <param name="paramsJson">参数JSON字符串（可为空）</param>
    /// <returns>引擎句柄</returns>
    [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern IntPtr Prophet_CreateEngine(
        [MarshalAs(UnmanagedType.LPStr)] string dslCode,
        [MarshalAs(UnmanagedType.LPStr)] string? paramsJson
    );
    
    /// <summary>
    /// 销毁引擎实例
    /// </summary>
    [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
    public static extern void Prophet_DestroyEngine(IntPtr engineHandle);
    
    /// <summary>
    /// 获取规则数量
    /// </summary>
    [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
    public static extern int Prophet_GetRuleCount(IntPtr engineHandle);
    
    /// <summary>
    /// 验证DSL代码（只进行词法和语法分析，不创建完整引擎）
    /// </summary>
    /// <param name="dslCode">DSL策略代码</param>
    /// <param name="errorBuffer">错误信息缓冲区</param>
    /// <param name="bufferSize">缓冲区大小</param>
    /// <returns>0=验证通过, -1=词法错误, -2=语法错误, -3=其他错误</returns>
    [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    private static extern int Prophet_ValidateDSL(
        [MarshalAs(UnmanagedType.LPStr)] string dslCode,
        [MarshalAs(UnmanagedType.LPStr)] System.Text.StringBuilder errorBuffer,
        int bufferSize
    );
    
    /// <summary>
    /// 获取最后的错误消息（策略API）
    /// </summary>
    [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl, EntryPoint = "Prophet_GetLastStrategyError")]
    private static extern IntPtr Prophet_GetLastStrategyError_Internal();
    
    /// <summary>
    /// 获取最后的错误消息（安全包装版本）
    /// </summary>
    public static string? Prophet_GetLastStrategyError()
    {
        try
        {
            IntPtr ptr = Prophet_GetLastStrategyError_Internal();
            if (ptr == IntPtr.Zero)
                return null;
            
            // 使用 PtrToStringAnsi 安全地转换
            return Marshal.PtrToStringAnsi(ptr);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ 获取C++错误信息时发生异常: {ex.Message}");
            return null;
        }
    }
    
    /// <summary>
    /// 验证DSL代码（只进行词法和语法分析，不创建完整引擎）
    /// </summary>
    /// <param name="dslCode">DSL策略代码</param>
    /// <returns>验证结果，包含错误信息（如果有）</returns>
    public static ValidationResult ValidateDSL(string dslCode)
    {
        var result = new ValidationResult();
        
        if (string.IsNullOrWhiteSpace(dslCode))
        {
            result.AddError(0, 0, "[VALIDATION] DSL代码为空");
            return result;
        }
        
        try
        {
            EnsureInitialized();
            
            var errorBuffer = new System.Text.StringBuilder(1024);
            int ret = Prophet_ValidateDSL(dslCode, errorBuffer, errorBuffer.Capacity);
            
            if (ret != 0)
            {
                // 解析错误信息（格式：[类型] 第X行, 第Y列: 描述 或 [类型] 第X行: 描述）
                string errorMsg = errorBuffer.ToString();
                ParseEngineError(errorMsg, result);
            }
            
            return result;
        }
        catch (Exception ex)
        {
            result.AddError(0, 0, $"[EXCEPTION] 验证过程发生异常: {ex.Message}");
            Console.WriteLine($"❌ DSL验证异常: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
            return result;
        }
    }
    
    /// <summary>
    /// 解析核心引擎返回的错误信息
    /// 支持格式：
    /// 1. [类型] Line X, Column Y: 描述 (英文格式)
    /// 2. [类型] Line X: 描述 (英文格式)
    /// 3. [类型] 第X行, 第Y列: 描述 (中文格式，兼容旧版本)
    /// 4. [类型] 第X行: 描述 (中文格式，兼容旧版本)
    /// </summary>
    private static void ParseEngineError(string errorMsg, ValidationResult result)
    {
        if (string.IsNullOrWhiteSpace(errorMsg))
            return;
        
        // 格式1: [类型] Line X, Column Y: 描述 (英文格式，优先匹配)
        var matchWithColumnEN = System.Text.RegularExpressions.Regex.Match(
            errorMsg, 
            @"\[(\w+)\]\s*Line\s+(\d+),\s*Column\s+(\d+):\s*(.+)",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase
        );
        
        if (matchWithColumnEN.Success)
        {
            string type = matchWithColumnEN.Groups[1].Value;
            int line = int.Parse(matchWithColumnEN.Groups[2].Value);
            int column = int.Parse(matchWithColumnEN.Groups[3].Value);
            string message = matchWithColumnEN.Groups[4].Value.Trim();
            
            result.AddError(line, column, $"[{type}] {message}");
            return;
        }
        
        // 格式2: [类型] Line X: 描述 (英文格式)
        var matchWithoutColumnEN = System.Text.RegularExpressions.Regex.Match(
            errorMsg,
            @"\[(\w+)\]\s*Line\s+(\d+):\s*(.+)",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase
        );
        
        if (matchWithoutColumnEN.Success)
        {
            string type = matchWithoutColumnEN.Groups[1].Value;
            int line = int.Parse(matchWithoutColumnEN.Groups[2].Value);
            string message = matchWithoutColumnEN.Groups[3].Value.Trim();
            
            result.AddError(line, 0, $"[{type}] {message}");
            return;
        }
        
        // 格式3: [类型] 第X行, 第Y列: 描述 (中文格式，兼容旧版本)
        var matchWithColumnCN = System.Text.RegularExpressions.Regex.Match(
            errorMsg, 
            @"\[(\w+)\]\s*第(\d+)行,\s*第(\d+)列:\s*(.+)"
        );
        
        if (matchWithColumnCN.Success)
        {
            string type = matchWithColumnCN.Groups[1].Value;
            int line = int.Parse(matchWithColumnCN.Groups[2].Value);
            int column = int.Parse(matchWithColumnCN.Groups[3].Value);
            string message = matchWithColumnCN.Groups[4].Value.Trim();
            
            result.AddError(line, column, $"[{type}] {message}");
            return;
        }
        
        // 格式4: [类型] 第X行: 描述 (中文格式，兼容旧版本)
        var matchWithoutColumnCN = System.Text.RegularExpressions.Regex.Match(
            errorMsg,
            @"\[(\w+)\]\s*第(\d+)行:\s*(.+)"
        );
        
        if (matchWithoutColumnCN.Success)
        {
            string type = matchWithoutColumnCN.Groups[1].Value;
            int line = int.Parse(matchWithoutColumnCN.Groups[2].Value);
            string message = matchWithoutColumnCN.Groups[3].Value.Trim();
            
            result.AddError(line, 0, $"[{type}] {message}");
            return;
        }
        
        // 如果所有格式都无法匹配，尝试提取行号（如果有）
        // 格式: [类型] ... Line X ... 或 ... 第X行 ...
        var lineMatch = System.Text.RegularExpressions.Regex.Match(
            errorMsg,
            @"(?:Line\s+|第)(\d+)(?:\s|行|,|:)"
        );
        
        if (lineMatch.Success)
        {
            int line = int.Parse(lineMatch.Groups[1].Value);
            result.AddError(line, 0, errorMsg);
        }
        else
        {
            // 完全无法解析，直接添加为错误（行号0表示未知位置）
            result.AddError(0, 0, errorMsg);
        }
    }
    
    #endregion
    
    #region K线数据设置 - v10.0 重构：必须指定时间框架 + 增量更新支持
    
    /// <summary>
    /// v10.0 设置K线数据（必须指定时间框架）
    /// 用于初始化时设置完整的300根K线窗口
    /// </summary>
    /// <param name="engineHandle">引擎句柄</param>
    /// <param name="timeframe">时间框架（如"5m", "1h", "1d"）</param>
    /// <param name="open">开盘价数组</param>
    /// <param name="high">最高价数组</param>
    /// <param name="low">最低价数组</param>
    /// <param name="close">收盘价数组</param>
    /// <param name="volume">成交量数组</param>
    /// <param name="openTime">开盘时间数组（UTC毫秒）</param>
    /// <param name="closeTime">收盘时间数组（UTC毫秒）</param>
    /// <param name="count">K线数量（建议300根）</param>
    /// <returns>0=成功，非0=失败</returns>
    [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern int Prophet_SetKlines(
        IntPtr engineHandle,
        [MarshalAs(UnmanagedType.LPStr)] string timeframe,  // v10.0: 新增
        [In] double[] open,
        [In] double[] high,
        [In] double[] low,
        [In] double[] close,
        [In] double[] volume,
        [In] long[] openTime,
        [In] long[] closeTime,
        int count
    );
    
    /// <summary>
    /// v10.0 增量追加单根K线（高性能，回测循环使用）
    /// 自动维护300根窗口：追加新K线，丢弃最旧的
    /// </summary>
    /// <param name="engineHandle">引擎句柄</param>
    /// <param name="timeframe">时间框架</param>
    /// <param name="open">开盘价</param>
    /// <param name="high">最高价</param>
    /// <param name="low">最低价</param>
    /// <param name="close">收盘价</param>
    /// <param name="volume">成交量</param>
    /// <param name="openTime">开盘时间（UTC毫秒）</param>
    /// <param name="closeTime">收盘时间（UTC毫秒）</param>
    /// <returns>0=成功，非0=失败</returns>
    [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern int Prophet_AppendKline(
        IntPtr engineHandle,
        [MarshalAs(UnmanagedType.LPStr)] string timeframe,
        double open,
        double high,
        double low,
        double close,
        double volume,
        long openTime,
        long closeTime
    );
    
    /// <summary>
    /// v10.0 增量追加多根K线（批量追加，处理跳跃场景）
    /// </summary>
    /// <param name="engineHandle">引擎句柄</param>
    /// <param name="timeframe">时间框架</param>
    /// <param name="open">开盘价数组</param>
    /// <param name="high">最高价数组</param>
    /// <param name="low">最低价数组</param>
    /// <param name="close">收盘价数组</param>
    /// <param name="volume">成交量数组</param>
    /// <param name="openTime">开盘时间数组（UTC毫秒）</param>
    /// <param name="closeTime">收盘时间数组（UTC毫秒）</param>
    /// <param name="count">K线数量</param>
    /// <returns>0=成功，非0=失败</returns>
    [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern int Prophet_AppendKlines(
        IntPtr engineHandle,
        [MarshalAs(UnmanagedType.LPStr)] string timeframe,
        [In] double[] open,
        [In] double[] high,
        [In] double[] low,
        [In] double[] close,
        [In] double[] volume,
        [In] long[] openTime,
        [In] long[] closeTime,
        int count
    );
    
    #endregion
    
    #region 信号生成
    
    /// <summary>
    /// 获取交易信号（简单版本，无环境变量）
    /// </summary>
    /// <param name="engineHandle">引擎句柄</param>
    /// <param name="currentPrice">当前价格</param>
    /// <param name="currentTime">当前时间（UTC毫秒）</param>
    /// <param name="signal">输出信号</param>
    /// <returns>0=成功，非0=失败</returns>
    [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
    public static extern int Prophet_GetSignal(
        IntPtr engineHandle,
        double currentPrice,
        long currentTime,
        ref Signal signal
    );
    
    #endregion
    
    #region 时间序列数据注入
    
    /// <summary>
    /// 设置恐惧与贪婪指数时间序列数据
    /// </summary>
    /// <param name="engineHandle">引擎句柄</param>
    /// <param name="dateTimestamps">日期时间戳数组（UTC秒）</param>
    /// <param name="values">指数值数组（0-100）</param>
    /// <param name="classifications">分类标签数组</param>
    /// <param name="count">数据条数</param>
    /// <returns>0=成功，非0=失败</returns>
    [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern int Prophet_SetFearGreedSeries(
        IntPtr engineHandle,
        [In] long[] dateTimestamps,
        [In] int[] values,
        [In, MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.LPStr)] string[] classifications,
        int count
    );
    
    /// <summary>
    /// 设置资金费率时间序列数据
    /// </summary>
    /// <param name="engineHandle">引擎句柄</param>
    /// <param name="timestamps">时间戳数组（UTC毫秒）</param>
    /// <param name="values">资金费率值数组</param>
    /// <param name="count">数据条数</param>
    /// <returns>0=成功，非0=失败</returns>
    [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
    public static extern int Prophet_SetFundingRateSeries(
        IntPtr engineHandle,
        [In] long[] timestamps,
        [In] double[] values,
        int count
    );
    
    /// <summary>
    /// 设置多空比时间序列数据
    /// </summary>
    /// <param name="engineHandle">引擎句柄</param>
    /// <param name="timestamps">时间戳数组（UTC秒）</param>
    /// <param name="longRatios">多头比例数组（0~1）</param>
    /// <param name="shortRatios">空头比例数组（0~1）</param>
    /// <param name="ratios">多空比率数组（long/short）</param>
    /// <param name="count">数据条数</param>
    /// <returns>0=成功，非0=失败</returns>
    [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
    public static extern int Prophet_SetLongShortRatioSeries(
        IntPtr engineHandle,
        [In] long[] timestamps,
        [In] double[] longRatios,
        [In] double[] shortRatios,
        [In] double[] ratios,
        int count
    );
    
    #endregion
    
    #region 辅助函数
    
    /// <summary>
    /// 将C#字符串指针转换为托管字符串
    /// </summary>
    public static string? PtrToString(IntPtr ptr)
    {
        if (ptr == IntPtr.Zero)
            return null;
        return Marshal.PtrToStringAnsi(ptr);
    }
    
    /// <summary>
    /// 获取最后的错误消息
    /// </summary>
    public static string? GetLastErrorMessage()
    {
        return Prophet_GetLastStrategyError();
    }
    
    #endregion
    
    #region v10.0 C# Helper Methods（包装方法，简化调用）
    
    /// <summary>
    /// v10.0: 设置指定时间框架的K线数据（初始化300根）
    /// </summary>
    public static void SetKlines(
        IntPtr engineHandle,
        string timeframe,
        double[] open,
        double[] high,
        double[] low,
        double[] close,
        double[] volume,
        long[] openTime,
        long[] closeTime,
        int count)
    {
        int result = Prophet_SetKlines(
            engineHandle,
            timeframe,
            open, high, low, close, volume,
            openTime, closeTime,
            count
        );
        
        if (result != 0)
        {
            var error = GetLastErrorMessage();
            throw new Exception($"设置{timeframe}K线数据失败: {error}");
        }
    }
    
    /// <summary>
    /// v10.0: 追加单根K线到指定时间框架
    /// </summary>
    public static void AppendKline(
        IntPtr engineHandle,
        string timeframe,
        double open,
        double high,
        double low,
        double close,
        double volume,
        long openTime,
        long closeTime)
    {
        int result = Prophet_AppendKline(
            engineHandle,
            timeframe,
            open, high, low, close, volume,
            openTime, closeTime
        );
        
        if (result != 0)
        {
            var error = GetLastErrorMessage();
            throw new Exception($"追加{timeframe}K线失败: {error}");
        }
    }
    
    /// <summary>
    /// v10.0: 追加多根K线到指定时间框架
    /// </summary>
    public static void AppendKlines(
        IntPtr engineHandle,
        string timeframe,
        double[] open,
        double[] high,
        double[] low,
        double[] close,
        double[] volume,
        long[] openTime,
        long[] closeTime,
        int count)
    {
        int result = Prophet_AppendKlines(
            engineHandle,
            timeframe,
            open, high, low, close, volume,
            openTime, closeTime,
            count
        );
        
        if (result != 0)
        {
            var error = GetLastErrorMessage();
            throw new Exception($"批量追加{timeframe}K线失败: {error}");
        }
    }
    
    #endregion
}

