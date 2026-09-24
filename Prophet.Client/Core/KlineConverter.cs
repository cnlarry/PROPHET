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

    #region P/Invoke 声明

    /// <summary>
    /// K线数据结构（C互操作）
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct ProphetKline
    {
        public long OpenTime;    // Unix毫秒时间
        public long CloseTime;   // Unix毫秒时间
        public double Open;
        public double High;
        public double Low;
        public double Close;
        public double Volume;
    }

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    private static extern int prophet_kline_convert(
        [In] ProphetKline[] input,
        int inputCount,
        int fromMinutes,
        int toMinutes,
        [Out] ProphetKline[] output,
        out int outputCount,
        int PERIOD
    );

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    private static extern int prophet_kline_timeframe_to_minutes(
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

            // 准备输出数组
            var outputArray = new ProphetKline[klines.Count];

            // 调用C++实现
            int result = prophet_kline_convert(
                inputArray, 
                inputArray.Length,
                fromMinutes,
                toMinutes,
                outputArray,
                out int outputCount,
                PERIOD
            );

            if (result != 0)
            {
                return new List<Candlestick>();
            }

            // 转换回C#对象
            var resultList = new List<Candlestick>();
            for (int i = 0; i < outputCount; i++)
            {
                var k = outputArray[i];
                resultList.Add(new Candlestick
                {
                    Time = DateTimeOffset.FromUnixTimeMilliseconds(k.OpenTime).LocalDateTime,
                    Open = k.Open,
                    High = k.High,
                    Low = k.Low,
                    Close = k.Close,
                    Volume = k.Volume
                });
            }

            return resultList;
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

        int result = prophet_kline_timeframe_to_minutes(timeframe);
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

