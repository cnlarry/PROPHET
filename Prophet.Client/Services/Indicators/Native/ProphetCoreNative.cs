using System;
using System.Runtime.InteropServices;

namespace Prophet.Client.Services.Indicators.Native;

/// <summary>
/// Prophet Core Native 绑定 - 通过P/Invoke调用prophet_core.dll
/// 所有指标计算统一使用TA-Lib，确保与DSL引擎结果一致
/// </summary>
public static class ProphetCoreNative
{
    private const string DLL_NAME = "prophet_core.dll";

    // ============================================================================
    // 数据结构
    // ============================================================================

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    public struct IndicatorResult
    {
        public IntPtr Values;        // double* 指针
        public int Length;           // 数组长度
        public int OutBegin;         // 输出起始索引
        // 注意：与 C++ IndicatorResult 保持二进制一致（values/length/out_begin/error_message[256]）。
        // Native 函数的 int 返回值即返回码，不要在此结构体中另加字段，否则错位。
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string ErrorMessage;  // 错误信息
    }

    // ============================================================================
    // 版本信息
    // ============================================================================

    [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr Prophet_GetVersion();

    [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr Prophet_GetTALibVersion();

    // ============================================================================
    // MA 类型指标
    // ============================================================================

    [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
    public static extern int Prophet_SMA(
        [In] double[] inReal,
        int length,
        int PERIOD,
        ref IndicatorResult outResult
    );

    [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
    public static extern int Prophet_EMA(
        [In] double[] inReal,
        int length,
        int PERIOD,
        ref IndicatorResult outResult
    );

    [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
    public static extern int Prophet_WMA(
        [In] double[] inReal,
        int length,
        int PERIOD,
        ref IndicatorResult outResult
    );

    [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
    public static extern int Prophet_DEMA(
        [In] double[] inReal,
        int length,
        int PERIOD,
        ref IndicatorResult outResult
    );

    [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
    public static extern int Prophet_TEMA(
        [In] double[] inReal,
        int length,
        int PERIOD,
        ref IndicatorResult outResult
    );

    // ============================================================================
    // 复合指标
    // ============================================================================

    [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
    public static extern int Prophet_BBANDS(
        [In] double[] inReal,
        int length,
        int PERIOD,
        double stdDev,
        ref IndicatorResult outUpper,
        ref IndicatorResult outMiddle,
        ref IndicatorResult outLower
    );

    [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
    public static extern int Prophet_KELTNER(
        [In] double[] inHigh,
        [In] double[] inLow,
        [In] double[] inClose,
        int length,
        int PERIOD,
        double multiplier,
        ref IndicatorResult outUpper,
        ref IndicatorResult outMiddle,
        ref IndicatorResult outLower
    );

    [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
    public static extern int Prophet_ICHIMOKU(
        [In] double[] inHigh,
        [In] double[] inLow,
        [In] double[] inClose,
        int length,
        int tenkanPeriod,
        int kijunPeriod,
        int senkouBPeriod,
        ref IndicatorResult outTenkan,
        ref IndicatorResult outKijun,
        ref IndicatorResult outSenkouA,
        ref IndicatorResult outSenkouB,
        ref IndicatorResult outChikou
    );

    [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
    public static extern int Prophet_SAR(
        [In] double[] inHigh,
        [In] double[] inLow,
        int length,
        double acceleration,
        double maximum,
        ref IndicatorResult outResult
    );

    [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
    public static extern int Prophet_TRIX(
        [In] double[] inReal,
        int length,
        int PERIOD,
        ref IndicatorResult outResult
    );

    [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
    public static extern int Prophet_VWAP(
        [In] double[] inHigh,
        [In] double[] inLow,
        [In] double[] inClose,
        [In] double[] inVolume,
        int length,
        int PERIOD,
        ref IndicatorResult outResult
    );

    [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
    public static extern int Prophet_ATR(
        [In] double[] inHigh,
        [In] double[] inLow,
        [In] double[] inClose,
        int length,
        int PERIOD,
        ref IndicatorResult outResult
    );

    [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
    public static extern int Prophet_MACD(
        [In] double[] inReal,
        int length,
        int fastPeriod,
        int slowPeriod,
        int signalPeriod,
        ref IndicatorResult outMACD,
        ref IndicatorResult outSignal,
        ref IndicatorResult outHistogram
    );

    [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
    public static extern int Prophet_RSI(
        [In] double[] inReal,
        int length,
        int PERIOD,
        ref IndicatorResult outResult
    );

    // ============================================================================
    // 新增副图指标（第一批）
    // ============================================================================

    /// <summary>
    /// MFI 资金流量指标
    /// </summary>
    [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
    public static extern int Prophet_MFI(
        [In] double[] inHigh,
        [In] double[] inLow,
        [In] double[] inClose,
        [In] double[] inVolume,
        int length,
        int PERIOD,
        ref IndicatorResult outResult
    );

    /// <summary>
    /// OBV 能量潮
    /// </summary>
    [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
    public static extern int Prophet_OBV(
        [In] double[] inClose,
        [In] double[] inVolume,
        int length,
        ref IndicatorResult outResult
    );

    /// <summary>
    /// STOCH 随机指标（用于KDJ计算）
    /// </summary>
    [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
    public static extern int Prophet_STOCH(
        [In] double[] inHigh,
        [In] double[] inLow,
        [In] double[] inClose,
        int length,
        int fastKPeriod,
        int slowKPeriod,
        int slowDPeriod,
        ref IndicatorResult outK,
        ref IndicatorResult outD
    );

    /// <summary>
    /// StochRSI 随机RSI
    /// </summary>
    [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
    public static extern int Prophet_STOCHRSI(
        [In] double[] inReal,
        int length,
        int PERIOD,
        int fastKPeriod,
        int fastDPeriod,
        ref IndicatorResult outK,
        ref IndicatorResult outD
    );

    // ============================================================================
    // 新增副图指标（第二批）
    // ============================================================================

    /// <summary>
    /// CCI 商品通道指标
    /// </summary>
    [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
    public static extern int Prophet_CCI(
        [In] double[] inHigh,
        [In] double[] inLow,
        [In] double[] inClose,
        int length,
        int PERIOD,
        ref IndicatorResult outResult
    );

    /// <summary>
    /// ADX/DMI 趋向指标
    /// </summary>
    [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
    public static extern int Prophet_DMI(
        [In] double[] inHigh,
        [In] double[] inLow,
        [In] double[] inClose,
        int length,
        int PERIOD,
        ref IndicatorResult outADX,
        ref IndicatorResult outPlusDI,
        ref IndicatorResult outMinusDI
    );

    /// <summary>
    /// WILLR 威廉指标
    /// </summary>
    [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
    public static extern int Prophet_WILLR(
        [In] double[] inHigh,
        [In] double[] inLow,
        [In] double[] inClose,
        int length,
        int PERIOD,
        ref IndicatorResult outResult
    );

    /// <summary>
    /// CMF 蔡金资金流量
    /// </summary>
    [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
    public static extern int Prophet_CMF(
        [In] double[] inHigh,
        [In] double[] inLow,
        [In] double[] inClose,
        [In] double[] inVolume,
        int length,
        int PERIOD,
        ref IndicatorResult outResult
    );

    /// <summary>
    /// ROC 变动率指标
    /// </summary>
    [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
    public static extern int Prophet_ROC(
        [In] double[] inReal,
        int length,
        int PERIOD,
        ref IndicatorResult outResult
    );

    // ============================================================================
    // 新增副图指标（第三批）
    // ============================================================================

    /// <summary>
    /// EMV 简易波动指标
    /// </summary>
    [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
    public static extern int Prophet_EMV(
        [In] double[] inHigh,
        [In] double[] inLow,
        [In] double[] inVolume,
        int length,
        int PERIOD,
        ref IndicatorResult outResult
    );

    /// <summary>
    /// MTM 动量指标
    /// </summary>
    [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
    public static extern int Prophet_MTM(
        [In] double[] inReal,
        int length,
        int PERIOD,
        ref IndicatorResult outResult
    );

    /// <summary>
    /// CMO Chande动量振荡器
    /// </summary>
    [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
    public static extern int Prophet_CMO(
        [In] double[] inReal,
        int length,
        int PERIOD,
        ref IndicatorResult outResult
    );

    /// <summary>
    /// AROON 阿隆指标
    /// </summary>
    [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
    public static extern int Prophet_AROON(
        [In] double[] inHigh,
        [In] double[] inLow,
        int length,
        int PERIOD,
        ref IndicatorResult outDown,
        ref IndicatorResult outUp
    );

    // ============================================================================
    // 内存管理
    // ============================================================================

    [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
    public static extern void Prophet_FreeResult(ref IndicatorResult result);

    [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
    public static extern void Prophet_FreeResults(
        [In, Out] IndicatorResult[] results,
        int count
    );

    [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr Prophet_GetLastError();

    // ============================================================================
    // 辅助方法
    // ============================================================================

    /// <summary>
    /// 从非托管内存复制数据到托管数组
    /// </summary>
    public static double[] CopyResultToArray(IndicatorResult result)
    {
        if (result.Values == IntPtr.Zero || result.Length <= 0)
            return Array.Empty<double>();

        double[] output = new double[result.Length];
        Marshal.Copy(result.Values, output, 0, result.Length);
        return output;
    }

    /// <summary>
    /// 获取版本信息字符串
    /// </summary>
    public static string GetVersionString()
    {
        IntPtr ptr = Prophet_GetVersion();
        return ptr != IntPtr.Zero ? Marshal.PtrToStringAnsi(ptr) ?? "Unknown" : "Unknown";
    }

    /// <summary>
    /// 获取TA-Lib版本信息字符串
    /// </summary>
    public static string GetTALibVersionString()
    {
        IntPtr ptr = Prophet_GetTALibVersion();
        return ptr != IntPtr.Zero ? Marshal.PtrToStringAnsi(ptr) ?? "Unknown" : "Unknown";
    }

    /// <summary>
    /// 获取最后一次错误信息
    /// </summary>
    public static string GetLastErrorString()
    {
        IntPtr ptr = Prophet_GetLastError();
        return ptr != IntPtr.Zero ? Marshal.PtrToStringAnsi(ptr) ?? "" : "";
    }
}

