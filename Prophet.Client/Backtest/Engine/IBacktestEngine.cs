using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Prophet.Client.Backtest.Models;

namespace Prophet.Client.Backtest.Engine;

/// <summary>
/// 回测引擎接口
/// </summary>
public interface IBacktestEngine
{
    /// <summary>
    /// 准备数据（预加载K线数据）
    /// v10.0: 需要DSL代码来分析时间框架，并支持信号采样频率
    /// </summary>
    Task PrepareDataAsync(
        string symbol,
        string strategyDslCode,  // v10.0: 需要DSL代码来分析时间框架
        DateTime startDate,
        DateTime endDate,
        string? signalSamplingInterval = null,  // 信号采样频率（也是时间框架）
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 运行单次回测
    /// </summary>
    Task<BacktestResult> RunAsync(
        string strategyDslCode,
        BacktestConfig config,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default
    );
    
    /// <summary>
    /// 批量运行回测（参数优化）
    /// </summary>
    Task<List<BacktestResult>> RunBatchAsync(
        string strategyDslCode,
        List<BacktestConfig> configs,
        ParallelOptions? parallelOptions = null,
        CancellationToken cancellationToken = default
    );
    
    // ========== 事件 ==========
    event EventHandler<BacktestProgressEventArgs>? ProgressChanged;
    event EventHandler<SignalGeneratedEventArgs>? SignalGenerated;
    event EventHandler<OrderExecutedEventArgs>? OrderExecuted;
}
