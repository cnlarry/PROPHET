using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Prophet.Client.Models;

namespace Prophet.Client.Backtest.DataFeed;

/// <summary>
/// 数据馈送接口（回测和实盘共用）
/// </summary>
public interface IDataFeed
{
    /// <summary>
    /// 加载历史K线数据（回测模式）
    /// </summary>
    Task<List<Candlestick>> LoadCandlesAsync(
        string symbol,
        string interval,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default
    );
    
    /// <summary>
    /// 流式获取实时K线数据（实盘模式）
    /// </summary>
    IAsyncEnumerable<Candlestick> StreamCandlesAsync(
        string symbol,
        string interval,
        CancellationToken cancellationToken = default
    );
    
    /// <summary>
    /// 获取最新K线（实盘模式）
    /// </summary>
    Task<Candlestick?> GetLatestCandleAsync(
        string symbol,
        string interval,
        CancellationToken cancellationToken = default
    );
}

