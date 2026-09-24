using System;
using System.Collections.Generic;
using Prophet.Client.Backtest.Models;
using Prophet.Client.Models;

namespace Prophet.Client.Backtest.Strategy;

/// <summary>
/// 策略信号生成器接口
/// 用于从K线数据生成交易信号
/// </summary>
public interface IStrategySignalGenerator : IDisposable
{
    /// <summary>
    /// 初始化策略（加载DSL代码等）
    /// </summary>
    void Initialize(string dslCode, BacktestConfig config);
    
    /// <summary>
    /// 生成交易信号
    /// </summary>
    /// <param name="candle">当前K线</param>
    /// <param name="historicalCandles">历史K线（用于指标计算）</param>
    /// <returns>交易信号（如果没有信号返回null）</returns>
    Signal? GenerateSignal(Candlestick candle, List<Candlestick> historicalCandles);
    
    /// <summary>
    /// 生成交易信号（使用指定的收盘时间）
    /// </summary>
    /// <param name="candle">当前K线</param>
    /// <param name="closeTime">K线收盘时间（UTC时间，用于时间对齐，传递给核心引擎）</param>
    /// <param name="historicalCandles">历史K线（用于指标计算）</param>
    /// <returns>交易信号（如果没有信号返回null）</returns>
    Signal? GenerateSignal(Candlestick candle, DateTime closeTime, List<Candlestick> historicalCandles);
    
    /// <summary>
    /// v10.0: 设置指定时间框架的K线数据（初始化300根）
    /// </summary>
    void SetKlines(string timeframe, List<Candlestick> candles);
    
    /// <summary>
    /// v10.0: 追加单根K线到指定时间框架
    /// </summary>
    void AppendKline(string timeframe, Candlestick candle);
    
    /// <summary>
    /// 设置时间序列数据（在Initialize之前调用）
    /// </summary>
    /// <param name="fearGreedData">恐惧与贪婪指数数据</param>
    /// <param name="fundingRateData">资金费率数据</param>
    /// <param name="longShortRatioData">多空比数据（可选）</param>
    void SetTimeSeriesData(List<FearGreedData> fearGreedData, List<FundingRateData> fundingRateData, List<LongShortRatioData>? longShortRatioData = null);

    // 清理资源（继承自 IDisposable，此处不再重复声明）
}

