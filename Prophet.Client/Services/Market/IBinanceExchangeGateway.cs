using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using Prophet.Client.Models;

namespace Prophet.Client.Services.Market;

/// <summary>
/// 封装对币安期货 REST/WebSocket 接口的统一访问
/// </summary>
public interface IBinanceExchangeGateway : IDisposable
{
    /// <summary>
    /// 暴露底层 <see cref="HttpClient"/>（供下载器等场景复用）
    /// </summary>
    HttpClient HttpClient { get; }

    /// <summary>
    /// 获取K线数据（支持最新/时间范围）
    /// </summary>
    Task<IReadOnlyList<Candlestick>> FetchKlinesAsync(
        string symbol,
        string interval,
        int limit = 1000,
        DateTime? startTime = null,
        DateTime? endTime = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取资金费率数据
    /// </summary>
    Task<IReadOnlyList<FundingRateData>> FetchFundingRatesAsync(
        string symbol,
        int limit = 1000,
        DateTime? startTime = null,
        DateTime? endTime = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 创建已配置好的 WebSocket 客户端
    /// </summary>
    ClientWebSocket CreateWebSocketClient();

    /// <summary>
    /// 构建 WebSocket 订阅 URL
    /// </summary>
    Uri BuildKlineStreamUri(string symbol, string interval);

    /// <summary>
    /// 获取24小时Ticker数据
    /// </summary>
    Task<Ticker24hData?> FetchTicker24hAsync(string symbol, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取所有交易对的24小时Ticker数据
    /// </summary>
    Task<IReadOnlyList<Ticker24hData>> FetchAllTickers24hAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取持仓量（Open Interest）
    /// </summary>
    Task<OpenInterestData?> FetchOpenInterestAsync(string symbol, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取多空比数据（单条最新数据）
    /// </summary>
    Task<LongShortRatioData?> FetchLongShortRatioAsync(
        string symbol, 
        string period = "5m", 
        string ratioType = "global", 
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 获取多空比历史数据（批量查询）
    /// </summary>
    /// <param name="symbol">交易对符号</param>
    /// <param name="period">周期（5m, 15m, 30m, 1h等）</param>
    /// <param name="limit">返回条数（最大1000）</param>
    /// <param name="startTime">开始时间（可选）</param>
    /// <param name="endTime">结束时间（可选）</param>
    /// <param name="ratioType">多空比类型（global/top/position）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>多空比数据列表（按时间降序，最新的在前）</returns>
    Task<IReadOnlyList<LongShortRatioData>> FetchLongShortRatioHistoryAsync(
        string symbol,
        string period = "5m",
        int limit = 1000,
        DateTime? startTime = null,
        DateTime? endTime = null,
        string ratioType = "global",
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取爆仓数据（最近24小时）
    /// </summary>
    Task<LiquidationData?> FetchLiquidationDataAsync(string symbol, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取合约主动买卖量（Taker Long/Short Ratio）
    /// </summary>
    Task<TakerLongShortRatioData?> FetchTakerLongShortRatioAsync(
        string symbol, 
        string period = "5m", 
        CancellationToken cancellationToken = default);
}
