using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Prophet.Client.Models;

namespace Prophet.Client.Core;

/// <summary>
/// K线时间框架转换器（调用C++核心引擎）
/// 
/// 本地K线合成，避免频繁请求API
/// </summary>
public static class KlineConverter
{
    private const string DllName = "prophet_core.dll";

    #region P/Invoke 声明（与 Prophet.Core/include/prophet/c_api.h 保持一致）

    /// <summary>
    /// K线数据结构（字段顺序必须与 NativeKline 一致：先 5 个 double，再 2 个 int64）
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct ProphetKline
    {
        public double Open;
        public double High;
        public double Low;
        public double Close;
        public double Volume;
        public long OpenTime;    // UTC毫秒
        public long CloseTime;   // UTC毫秒
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    private struct KlineConversionResult
    {
        public IntPtr Klines;    // NativeKline*（C++分配，需 Free）
        public int Length;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string ErrorMessage;
    }

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "Prophet_ConvertKlines")]
    private static extern int ProphetConvertKlines(
        [In] ProphetKline[] klines,
        int count,
        int fromMinutes,
        int toMinutes,
        int PERIOD,
        ref KlineConversionResult outResult
    );

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "Prophet_FreeKlineResult")]
    private static extern void ProphetFreeKlineResult(ref KlineConversionResult result);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "Prophet_TimeframeToMinutes")]
    private static extern int ProphetTimeframeToMinutes(
        [MarshalAs(UnmanagedType.LPStr)] string timeframe
    );

    #endregion

    /// <summary>
    /// 转换K线时间框架
    /// </summary>
    /// <param name="klines">源K线数据（必须按时间升序）</param>
    /// <param name="fromInterval">源时间框架（如"1m"）</param>
    /// <param name="toInterval">目标时间框架（如"5m", "1h"）</param>
    /// <param name="PERIOD">返回最近N根（0=全部）</param>
    /// <returns>转换后的K线列表</returns>
    public static List<Candlestick> Convert(
        List<Candlestick> klines, 
        string fromInterval, 
        string toInterval,
        int PERIOD = 0)
    {
        if (klines == null || klines.Count == 0)
            return new List<Candlestick>();

        try
        {
            // 转换时间框架字符串为分钟
            int fromMinutes = TimeframeToMinutes(fromInterval);
            int toMinutes = TimeframeToMinutes(toInterval);

            // 转换为C结构体数组
            var inputArray = klines.Select(k => new ProphetKline
            {
                OpenTime = new DateTimeOffset(k.Time.ToUniversalTime()).ToUnixTimeMilliseconds(),
                CloseTime = new DateTimeOffset(k.Time.ToUniversalTime()).ToUnixTimeMilliseconds() + fromMinutes * 60000 - 1,
                Open = k.Open,
                High = k.High,
                Low = k.Low,
                Close = k.Close,
                Volume = k.Volume
            }).ToArray();

            // 调用C++实现（结果由C++分配，需释放）
            var nativeResult = new KlineConversionResult();
            int result = ProphetConvertKlines(
                inputArray,
                inputArray.Length,
                fromMinutes,
                toMinutes,
                PERIOD,
                ref nativeResult
            );

            if (result != 0)
            {
                return new List<Candlestick>();
            }

            try
            {
                // 转换回C#对象（管线使用 UTC 时间，避免 LocalDateTime 混入）
                var resultList = new List<Candlestick>(nativeResult.Length);
                int stride = Marshal.SizeOf<ProphetKline>();
                for (int i = 0; i < nativeResult.Length; i++)
                {
                    var k = Marshal.PtrToStructure<ProphetKline>(
                        IntPtr.Add(nativeResult.Klines, i * stride));
                    resultList.Add(new Candlestick
                    {
                        Time = DateTimeOffset.FromUnixTimeMilliseconds(k.OpenTime).UtcDateTime,
                        Open = k.Open,
                        High = k.High,
                        Low = k.Low,
                        Close = k.Close,
                        Volume = k.Volume
                    });
                }

                return resultList;
            }
            finally
            {
                ProphetFreeKlineResult(ref nativeResult);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"K线转换异常: {ex.Message}");
            return new List<Candlestick>();
        }
    }

    /// <summary>
    /// 时间框架字符串转分钟
    /// </summary>
    public static int TimeframeToMinutes(string timeframe)
    {
        if (string.IsNullOrEmpty(timeframe))
            throw new ArgumentException("时间框架不能为空", nameof(timeframe));

        int result = ProphetTimeframeToMinutes(timeframe);
        if (result < 0)
            throw new ArgumentException($"无效的时间框架 {timeframe}", nameof(timeframe));

        return result;
    }

    /// <summary>
    /// 检查时间框架是否有数据
    /// </summary>
    public static bool IsValidTimeframe(string timeframe)
    {
        try
        {
            TimeframeToMinutes(timeframe);
            return true;
        }
        catch
        {
            return false;
        }
    }
}

