using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Prophet.Client.Backtest.DataFeed;
using Prophet.Client.Backtest.Models;
using Prophet.Client.Models;
using Prophet.Client.Backtest.OrderManagement;
using Prophet.Client.Backtest.Performance;
using Prophet.Client.Backtest.Storage;
using Prophet.Client.Backtest.Strategy;
using Prophet.Client.Services;
using Prophet.Client.Services.Data.Preparation;

namespace Prophet.Client.Backtest.Engine;

/// <summary>
/// v10.0 回测引擎实现 - 增量更新模式
/// 核心改进：
/// 1. 自动分析DSL提取时间框架
/// 2. 为每个时间框架准备独立数据
/// 3. 初始化时注入初始300根K线
/// 4. 回测循环中使用增量追加（高性能）
/// 5. 智能计算预热期
/// </summary>
public class BacktestEngine : IBacktestEngine
{
    private readonly IDataFeed _dataFeed;
    private readonly IOrderManager _orderManager;
    private readonly IPerformanceAnalyzer _performanceAnalyzer;
    private readonly IStrategySignalGenerator _strategyGenerator;
    private readonly IBacktestStorage? _storage;
    
    // v10.0: 多时间框架数据缓存
    private Dictionary<string, List<Candlestick>> _multiTimeframeData = new();
    private DateTime _lastProgressTime = DateTime.UtcNow;
    private int _lastProgressIndex = 0;
    
    // 时间序列数据（在PrepareDataAsync中准备）
    private List<Prophet.Client.Models.FearGreedData>? _fearGreedData;
    private List<Prophet.Client.Models.FundingRateData>? _fundingRateData;
    private List<Prophet.Client.Models.LongShortRatioData>? _longShortRatioData;
    
    // UI更新节流：避免过于频繁的UI更新导致阻塞
    private DateTime _lastUIUpdateTime = DateTime.MinValue;
    private const int UI_UPDATE_INTERVAL_MS = 100;  // 每100ms最多更新一次UI

    // 【反前视】挂起信号：本根收盘产生，下一根开盘执行（每次 RunAsync 开始时重置）
    private Signal? _pendingSignal;
    private SignalEvent? _pendingEvent;
    private int _pendingStep;
    private int _pendingGlobalIndex;
    
    // 事件
    public event EventHandler<BacktestProgressEventArgs>? ProgressChanged;
    public event EventHandler<SignalGeneratedEventArgs>? SignalGenerated;
    public event EventHandler<OrderExecutedEventArgs>? OrderExecuted;
    
    public BacktestEngine(
        IDataFeed dataFeed,
        IOrderManager orderManager,
        IPerformanceAnalyzer performanceAnalyzer,
        IStrategySignalGenerator strategyGenerator,
        IBacktestStorage? storage = null)
    {
        _dataFeed = dataFeed ?? throw new ArgumentNullException(nameof(dataFeed));
        _orderManager = orderManager ?? throw new ArgumentNullException(nameof(orderManager));
        _performanceAnalyzer = performanceAnalyzer ?? throw new ArgumentNullException(nameof(performanceAnalyzer));
        _strategyGenerator = strategyGenerator ?? throw new ArgumentNullException(nameof(strategyGenerator));
        _storage = storage;
    }
    
    /// <summary>
    /// v10.0 准备多时间框架数据
    /// </summary>
    public async Task PrepareDataAsync(
        string symbol,
        string strategyDslCode,  // v10.0: 需要DSL代码来分析时间框架
        DateTime startDate,
        DateTime endDate,
        string? signalSamplingInterval = null,  // 信号采样频率（也是时间框架）
        CancellationToken cancellationToken = default)
    {
        // 🔧 v4.0: 确保输入时间是UTC时间，但不进行时区转换
        // 如果Kind不是UTC，假设它是UTC时间（用户选择的日期已经是UTC日期）
        var utcStartDate = startDate.Kind == DateTimeKind.Utc 
            ? startDate 
            : DateTime.SpecifyKind(startDate, DateTimeKind.Utc);
        var utcEndDate = endDate.Kind == DateTimeKind.Utc 
            ? endDate 
            : DateTime.SpecifyKind(endDate, DateTimeKind.Utc);
        
        // 🆕 v4.0: 计算每个时间框架需要的数据起始时间（考虑窗口大小）
        const int REQUIRED_WINDOW_SIZE = 300;
        
        // 1. 分析DSL，提取所有需要的时间框架
        HashSet<string> dslTimeframes;
        try
        {
            dslTimeframes = DSLAnalyzer.ExtractTimeframes(strategyDslCode);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ 分析DSL失败: {ex.Message}");
            throw;
        }
        
        // 🔧 将信号采样频率添加到时间框架集合中（如果提供且不在DSL中）
        if (!string.IsNullOrWhiteSpace(signalSamplingInterval) && !dslTimeframes.Contains(signalSamplingInterval))
        {
            dslTimeframes.Add(signalSamplingInterval);
            Console.WriteLine($"📊 添加信号采样频率到数据准备流程: {signalSamplingInterval}");
        }
        
        // 🆕 v4.0: 计算每个时间框架需要向前偏移的时间
        var dataStartTimes = new Dictionary<string, DateTime>();
        var dataEndTimes = new Dictionary<string, DateTime>();
        
        foreach (var timeframe in dslTimeframes)
        {
            var timeframeMinutes = DSLAnalyzer.TimeframeToMinutes(timeframe);
            // 向前偏移 300 * timeframeMinutes 分钟
            var windowDuration = TimeSpan.FromMinutes(REQUIRED_WINDOW_SIZE * timeframeMinutes);
            dataStartTimes[timeframe] = utcStartDate - windowDuration;
            
            // 🔧 v4.0: 不使用对齐，直接使用utcEndDate（已经是最后一天的23:59:59）
            // SQL查询使用 open_time <= @endTime，所以 utcEndDate (23:59:59) 可以包含最后一根K线
            // 例如：对于5m时间框架，最后一根K线是 23:55:00 (开盘)，23:55:00 <= 23:59:59，会被包含
            dataEndTimes[timeframe] = utcEndDate;
        }
        
        // 使用最早的数据起始时间作为数据完整性检查的起始时间
        var earliestDataStartTime = dataStartTimes.Values.Min();
        
        // v10.0: 数据完整性检查和自动准备
        var dataPreparation = new BacktestDataPreparationService(
            marketDataRepository: App.MarketDataContext.Repository,
            gateway: App.MarketDataContext.Gateway,
            klineSyncService: App.MarketDataContext.KlineSyncService);
        await dataPreparation.PrepareAsync(
            symbol,
            strategyDslCode,
            earliestDataStartTime,  // 使用计算后的数据起始时间
            utcEndDate,
            progress: null,  // 移除进度日志
            signalSamplingInterval: signalSamplingInterval,  // 传递信号采样频率
            cancellationToken
        );
        
        // 保存时间序列数据
        _fearGreedData = dataPreparation.FearGreedData;
        _fundingRateData = dataPreparation.FundingRateData;
        _longShortRatioData = dataPreparation.LongShortRatioData;
        
        // 2. 为每个时间框架加载数据
        _multiTimeframeData.Clear();
        
        foreach (var timeframe in dslTimeframes.OrderBy(DSLAnalyzer.TimeframeToMinutes))
        {
            // 🆕 v4.0: 使用计算后的数据起始时间和结束时间
            var actualStart = dataStartTimes[timeframe];
            var finalEndDate = dataEndTimes[timeframe];
            
            // 加载数据（包含预热期和回测结束时间）
            var candles = await _dataFeed.LoadCandlesAsync(
                symbol, timeframe, actualStart, finalEndDate, cancellationToken
            );
            
            if (candles == null || candles.Count == 0)
            {
                throw new InvalidOperationException(
                    $"未找到 {timeframe} 时间框架的K线数据。\n" +
                    $"请确保本地缓存中有该时间段的数据。"
                );
            }
            
            // 🔧 v4.0: 确保加载的K线数据按时间升序排列
            candles = candles.OrderBy(c => c.Time).ToList();
            
            _multiTimeframeData[timeframe] = candles;
        }
    }
    
    /// <summary>
    /// v10.0 运行回测（增量更新模式）
    /// </summary>
    public async Task<BacktestResult> RunAsync(
        string strategyDslCode,
        BacktestConfig config,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        
        try
        {
            // 1. 分析DSL
            var dslTimeframes = DSLAnalyzer.ExtractTimeframes(strategyDslCode);
            
            // 获取信号采样频率
            var samplingInterval = config.SignalSamplingInterval ?? "1m";
            
            // 🔧 构建所有时间框架集合（包括DSL中的时间框架和采样频率）
            var allTimeframes = new HashSet<string>(dslTimeframes);
            if (!allTimeframes.Contains(samplingInterval))
            {
                allTimeframes.Add(samplingInterval);
            }
            var timeframes = allTimeframes;  // 用于后续代码
            
            // 2. 加载多时间框架数据（如果未预加载）
            // 🔧 将信号采样频率也加入到数据准备流程中
            if (_multiTimeframeData.Count == 0)
            {
                await PrepareDataAsync(
                    config.Symbol,
                    strategyDslCode,  // v10.0: 传递DSL代码
                    config.StartDate,
                    config.EndDate,
                    samplingInterval,  // 传递信号采样频率
                    cancellationToken
                );
            }
            
            // 3. 设置时间序列数据到信号生成器（必须在Initialize之前）
            if (_fearGreedData != null || _fundingRateData != null || _longShortRatioData != null)
            {
                _strategyGenerator.SetTimeSeriesData(
                    _fearGreedData ?? new List<Prophet.Client.Models.FearGreedData>(),
                    _fundingRateData ?? new List<Prophet.Client.Models.FundingRateData>(),
                    _longShortRatioData ?? new List<Prophet.Client.Models.LongShortRatioData>()
                );
            }
            
            // 4. 初始化各个组件（在设置时间序列数据之后）
            _strategyGenerator.Initialize(strategyDslCode, config);
            _orderManager.Initialize(config.InitialCapital);
            _performanceAnalyzer.Initialize(config);
            
            // 🔧 验证信号采样频率的数据已加载（应该在PrepareDataAsync中已加载）
            if (!_multiTimeframeData.ContainsKey(samplingInterval))
            {
                throw new InvalidOperationException(
                    $"信号采样频率 {samplingInterval} 的数据未在数据准备流程中加载。\n" +
                    $"这可能是数据准备流程的bug，请检查 PrepareDataAsync 方法。"
                );
            }
            
            // 4. P1.5: 使用信号采样频率作为回测迭代基准（而不是最小时间框架）
            var minTimeframe = samplingInterval;
            
            if (!_multiTimeframeData.ContainsKey(minTimeframe))
            {
                throw new InvalidOperationException(
                    $"未找到信号采样频率 {minTimeframe} 的K线数据。\n" +
                    $"请确保该时间框架的数据已加载。");
            }
            
            var minTimeframeData = _multiTimeframeData[minTimeframe];
            
            // 🔍 调试：输出实际加载的数据范围
            // 🔧 v4.0: 确保回测时间范围是UTC时间，但不进行时区转换
            // config.StartDate和config.EndDate应该已经是UTC时间（从BacktestConfig传入）
            // 如果Kind不是UTC，假设它是UTC时间（用户选择的日期已经是UTC日期）
            var utcStartDate = config.StartDate.Kind == DateTimeKind.Utc 
                ? config.StartDate 
                : DateTime.SpecifyKind(config.StartDate, DateTimeKind.Utc);
            var utcEndDate = config.EndDate.Kind == DateTimeKind.Utc 
                ? config.EndDate 
                : DateTime.SpecifyKind(config.EndDate, DateTimeKind.Utc);
            
            // 🔧 v4.0: 不使用对齐，直接使用utcEndDate查找最后一根K线
            // 因为utcEndDate已经是最后一天的23:59:59，不需要再对齐
            var timeframeMinutes = DSLAnalyzer.TimeframeToMinutes(minTimeframe);
            
            // 🆕 v4.0: 计算实际回测区间（第一个信号和最后一个信号的收盘时间）
            // 找到第一个 >= utcStartDate 的K线（这是第一个信号的K线）
            DateTime? firstSignalCloseTime = null;
            DateTime? lastSignalCloseTime = null;
            
            for (int i = 0; i < minTimeframeData.Count; i++)
            {
                if (minTimeframeData[i].Time >= utcStartDate)
                {
                    // 找到第一根K线，使用其收盘时间作为第一个信号时间
                    var firstCandleCloseTime = minTimeframeData[i].CloseTime;
                    if (firstCandleCloseTime.HasValue)
                    {
                        firstSignalCloseTime = firstCandleCloseTime.Value;
                    }
                    else
                    {
                        // 如果没有收盘时间，计算：开盘时间 + 时间框架 - 1秒
                        firstSignalCloseTime = minTimeframeData[i].Time.AddMinutes(timeframeMinutes).AddSeconds(-1);
                    }
                    break;
                }
            }
            
            // 🔧 v4.0: 找到最后一个 Time <= utcEndDate 的K线（这是最后一个信号的K线）
            // 不使用alignedEndDate，直接使用utcEndDate（已经是23:59:59）
            for (int i = minTimeframeData.Count - 1; i >= 0; i--)
            {
                if (minTimeframeData[i].Time <= utcEndDate)
                {
                    // 找到最后一根K线，使用其收盘时间作为最后一个信号时间
                    var lastCandleCloseTime = minTimeframeData[i].CloseTime;
                    if (lastCandleCloseTime.HasValue)
                    {
                        lastSignalCloseTime = lastCandleCloseTime.Value;
                    }
                    else
                    {
                        // 如果没有收盘时间，计算：开盘时间 + 时间框架 - 1秒
                        lastSignalCloseTime = minTimeframeData[i].Time.AddMinutes(timeframeMinutes).AddSeconds(-1);
                    }
                    break;
                }
            }
            
            // 🆕 v4.0：严格窗口大小要求
            // 每个时间框架都必须有至少300根K线才能开始回测
            const int REQUIRED_WINDOW_SIZE = 300;  // 必需的窗口大小
            
            // 检查每个时间框架的数据量
            foreach (var kvp in _multiTimeframeData)
            {
                if (kvp.Value.Count < REQUIRED_WINDOW_SIZE)
                {
                    throw new InvalidOperationException(
                        $"数据不足，无法进行回测。\n" +
                        $"时间框架 {kvp.Key}: 当前 {kvp.Value.Count} 根，需要至少 {REQUIRED_WINDOW_SIZE} 根"
                    );
                }
            }
            
            int initialWindowSize = REQUIRED_WINDOW_SIZE;
            
            // 🆕 v4.0: 先找到回测开始时间对应的K线索引，然后对齐窗口
            // 找到第一个 >= utcStartDate 的K线索引（这是回测开始的第一根K线）
            int actualStartIndex = -1;
            for (int i = 0; i < minTimeframeData.Count; i++)
            {
                if (minTimeframeData[i].Time >= utcStartDate)
                {
                    actualStartIndex = i;
                    break;
                }
            }
            
            if (actualStartIndex == -1)
            {
                throw new InvalidOperationException(
                    $"未找到回测开始时间 {utcStartDate:yyyy-MM-dd HH:mm:ss} UTC 对应的K线数据。\n" +
                    $"数据范围: {minTimeframeData[0].Time:yyyy-MM-dd HH:mm:ss} UTC ~ {minTimeframeData[^1].Time:yyyy-MM-dd HH:mm:ss} UTC"
                );
            }
            
            // 🆕 v4.0: 窗口应该对齐到回测开始时间（actualStartIndex对应的K线）
            // 窗口的最后一根K线应该是 actualStartIndex - 1（回测开始前的最后一根）
            // 窗口的第一根K线应该是 actualStartIndex - windowSize（如果可能）
            var timeframeIndices = await InitializeKlineWindowsAsync(timeframes, initialWindowSize, actualStartIndex, cancellationToken);
            
            // 6. 过滤数据到回测时间范围，并开始回测循环
            // actualStartIndex 已经在上面计算过了
            if (actualStartIndex == -1)
            {
                throw new InvalidOperationException(
                    $"未找到回测开始时间 {utcStartDate:yyyy-MM-dd HH:mm:ss} UTC 对应的K线数据。\n" +
                    $"数据范围: {minTimeframeData[0].Time:yyyy-MM-dd HH:mm:ss} UTC ~ {minTimeframeData[^1].Time:yyyy-MM-dd HH:mm:ss} UTC"
                );
            }
            
            // 🔧 v4.0: 找到最后一个 Time <= utcEndDate 的K线索引
            // 不使用alignedEndDate，直接使用utcEndDate（已经是23:59:59）
            int actualEndIndex = -1;
            for (int i = minTimeframeData.Count - 1; i >= actualStartIndex; i--)
            {
                if (minTimeframeData[i].Time <= utcEndDate)
                {
                    actualEndIndex = i;
                    break;
                }
            }
            
            if (actualEndIndex == -1 || actualEndIndex < actualStartIndex)
            {
                throw new InvalidOperationException(
                    $"未找到回测结束时间 {utcEndDate:yyyy-MM-dd HH:mm:ss} UTC 对应的K线数据。\n" +
                    $"数据范围: {minTimeframeData[0].Time:yyyy-MM-dd HH:mm:ss} UTC ~ {minTimeframeData[^1].Time:yyyy-MM-dd HH:mm:ss} UTC"
                );
            }
            
            // 回测循环从actualStartIndex开始，到actualEndIndex结束
            int startIndex = actualStartIndex;
            int totalSteps = actualEndIndex - actualStartIndex + 1;
            
            if (totalSteps <= 0)
            {
                totalSteps = 1; // 至少执行一次，使用现有数据
                startIndex = minTimeframeData.Count - 1;
            }
            
            // 🔧 v4.0: timeframeIndices 已在 InitializeKlineWindowsAsync 中初始化，直接使用
            
            int progressReportInterval = Math.Max(totalSteps / 100, 10);
            
            // 【反前视】重置挂起信号（同一引擎多次运行互不干扰）
            _pendingSignal = null;
            _pendingEvent = null;
            _pendingStep = 0;
            _pendingGlobalIndex = 0;
            
            // 回测循环：从actualStartIndex到actualEndIndex
            for (int i = startIndex; i <= actualEndIndex; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                int currentStep = i - startIndex + 1;
                var currentCandle = minTimeframeData[i];
                var currentTime = currentCandle.Time;
                
                // 🔧 v4.0: 使用K线的真实收盘时间（秒部分应该是59）
                // 如果K线有CloseTime字段，直接使用；否则计算：开盘时间 + 时间框架 - 1秒
                DateTime closeTime;
                if (currentCandle.CloseTime.HasValue)
                {
                    closeTime = currentCandle.CloseTime.Value;
                }
                else
                {
                    // 备选方案：计算收盘时间（开盘时间 + 时间框架 - 1秒）
                    var currentTimeframeMinutes = DSLAnalyzer.TimeframeToMinutes(minTimeframe);
                    closeTime = currentTime.AddMinutes(currentTimeframeMinutes).AddSeconds(-1);
                }
                
                // v10.0 核心：增量追加所有时间框架的新K线
                await AppendNewKlinesAsync(
                    timeframes,
                    timeframeIndices,
                    currentTime,
                    minTimeframe,
                    i,
                    cancellationToken
                );

                // 【反前视】先执行上一根K线收盘产生的挂起信号，以本根开盘价成交；
                // 同根的止盈止损检查在其之后，允许同根触发（真实市场行为）。
                await ExecutePendingSignalAsync(currentCandle, config, cancellationToken);

                // 【重要】先检查现有持仓的止盈止损（使用K线高低价）
                // 这样可以避免新开仓位在同一根K线上立即触发止盈止损
                _orderManager.CheckStopLossAndTakeProfit(currentCandle);
                await _orderManager.UpdateEquityAsync(currentCandle);
                
                // 每根K线都更新当前价格和时间（实时更新，但使用节流避免UI阻塞）
                var now = DateTime.UtcNow;
                var timeSinceLastUpdate = (now - _lastUIUpdateTime).TotalMilliseconds;
                
                // 节流：每100ms最多更新一次UI，避免过于频繁的更新导致UI线程阻塞
                // 但确保第一步和最后一步总是更新
                if (timeSinceLastUpdate >= UI_UPDATE_INTERVAL_MS || currentStep == 1 || currentStep == totalSteps)
                {
                    _lastUIUpdateTime = now;
                    
                    // 触发事件（事件处理已使用 Dispatcher.UIThread.Post 异步处理，不会阻塞回测线程）
                    // 使用收盘时间作为CurrentTime，确保与资金费率、恐惧与贪婪指数对齐
                    ProgressChanged?.Invoke(this, new BacktestProgressEventArgs
                    {
                        BacktestId = config.RunId,
                        Current = currentStep,
                        Total = totalSteps,
                        ProgressPercentage = (double)currentStep / totalSteps,
                        CurrentTime = closeTime,  // 使用收盘时间
                        CurrentPrice = (decimal)currentCandle.Close,  // 当前价格（最新K线的收盘价）
                        CurrentEquity = _orderManager.CurrentEquity,
                        EstimatedTimeRemaining = EstimateRemainingTime(currentStep, totalSteps, sw.Elapsed)
                    });
                }
                
                // 🆕 v11.0: 在滑动窗口中注入时间序列数据（恐惧与贪婪指数和资金费率）
      
                // 注入当前时间的时间序列数据到核心引擎
                InjectTimeSeriesDataForTime(closeTime);
                
                // 然后生成信号（策略引擎内部使用固定300根窗口）
                // 🔧 v4.0: 使用已计算的closeTime传递给GenerateSignal，确保Signal.Time与传递给核心引擎的时间一致
                var signal = _strategyGenerator.GenerateSignal(currentCandle, closeTime, new List<Candlestick>());
                
                // 判断是否是最后一个HOLD信号
                bool isLastStep = (currentStep == totalSteps);
                
                // 最后处理信号和订单（基于收盘价开仓）
                await ProcessSignalAndOrdersAsync(
                    signal, currentCandle, currentStep, i, config, isLastStep, cancellationToken
                );
                _performanceAnalyzer.Update(
                    currentCandle,
                    _orderManager.CurrentEquity,
                    _orderManager.CurrentCash,
                    _orderManager.CurrentPosition
                );
                
                // 进度报告（用于详细进度信息，频率较低）
                if (currentStep % progressReportInterval == 0 || currentStep == 1)
                {
                    await ReportProgressAsync(
                        currentStep, totalSteps, currentCandle, config, sw.Elapsed, progress, cancellationToken
                    );
                }
            }
            
            // 最后一根的挂起信号没有下一根可执行，按未执行记录
            if (_pendingSignal != null && _pendingEvent != null)
            {
                _pendingEvent.WasExecuted = false;
                _pendingEvent.ReasonIfNotExecuted = "回测结束，无下一根K线可执行";
                _performanceAnalyzer.RecordSignal(_pendingEvent);
                _pendingSignal = null;
                _pendingEvent = null;
            }
            
            // 7. 平掉所有持仓
            await CloseFinalPositionsAsync(minTimeframeData, cancellationToken);
            
            // 8. 生成绩效报告
            var result = _performanceAnalyzer.GenerateReport();
            result.StrategyName = "自定义策略";
            result.BacktestId = config.RunId;
            result.Status = BacktestStatus.COMPLETED;
            
            sw.Stop();
            
            // 9. 保存到数据库
            if (_storage != null)
            {
                try
                {
                    await _storage.SaveBacktestResultAsync(result, cancellationToken);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"⚠️ 保存回测结果失败: {ex.Message}");
                }
            }
            
            // 11. 完成进度
            progress?.Report(100);
            ProgressChanged?.Invoke(this, new BacktestProgressEventArgs
            {
                BacktestId = config.RunId,
                Current = totalSteps,
                Total = totalSteps,
                ProgressPercentage = 1.0,
                CurrentTime = minTimeframeData[^1].Time,
                CurrentPrice = (decimal)minTimeframeData[^1].Close,  // 当前价格（最新K线的收盘价）
                CurrentEquity = _orderManager.CurrentEquity,
                EstimatedTimeRemaining = TimeSpan.Zero
            });
            
            return result;
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("⚠️ 回测已取消");
            var partialResult = _performanceAnalyzer.GenerateReport();
            partialResult.Status = BacktestStatus.CANCELLED;
            return partialResult;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ 回测失败: {ex.Message}");
            Console.WriteLine($"   堆栈跟踪: {ex.StackTrace}");
            
            var failedResult = new BacktestResult
            {
                BacktestId = config.RunId,
                Status = BacktestStatus.FAILED,
                ErrorMessage = ex.Message,
                CompletedAt = DateTime.UtcNow
            };
            return failedResult;
        }
    }
    
    /// <summary>
    /// v10.0 初始化K线窗口（动态窗口大小，对齐到回测开始时间）
    /// </summary>
    /// <param name="timeframes">时间框架集合</param>
    /// <param name="windowSize">窗口大小</param>
    /// <param name="backtestStartIndex">回测开始索引（最小时间框架的K线索引）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>每个时间框架的窗口最后一根K线索引</returns>
    private async Task<Dictionary<string, int>> InitializeKlineWindowsAsync(
        HashSet<string> timeframes,
        int windowSize,
        int backtestStartIndex,
        CancellationToken cancellationToken)
    {
        // 🔧 v4.0: 初始化返回值
        var timeframeIndices = new Dictionary<string, int>();
        
        // 🆕 v4.0: 窗口对齐逻辑
        // backtestStartIndex 是第一次请求信号对应的K线索引（开盘时间 >= 回测开始日期）
        // 🔧 v4.0: 窗口的最后一根K线应该是 backtestStartIndex - 1（回测开始前的最后一根K线）
        // 这样可以避免重复：回测第一次迭代追加 backtestStartIndex，不会与窗口最后一根重复
        var minTimeframe = timeframes.OrderBy(DSLAnalyzer.TimeframeToMinutes).First();
        var minTimeframeCandles = _multiTimeframeData[minTimeframe];
        
        if (backtestStartIndex < 0 || backtestStartIndex >= minTimeframeCandles.Count)
        {
            throw new InvalidOperationException(
                $"回测开始索引 {backtestStartIndex} 无效。\n" +
                $"数据总量: {minTimeframeCandles.Count}"
            );
        }
        
        // 🔧 v4.0: 窗口的最后一根K线是 backtestStartIndex - 1（回测开始前的最后一根）
        int windowEndIndex = backtestStartIndex - 1;
        
        if (windowEndIndex < 0)
        {
            throw new InvalidOperationException(
                $"回测开始索引 {backtestStartIndex} 过小，无法创建预热窗口。\n" +
                $"需要至少 {windowSize} 根历史数据，请调整回测开始时间。"
            );
        }
        
        // 🆕 v4.0: 计算窗口的开始索引：向前取 windowSize 根K线（包括最后一根）
        int windowStartIndex = windowEndIndex - windowSize + 1;
        
        // 🆕 v4.0: 严格检查：必须有足够的数据
        if (windowStartIndex < 0)
        {
            throw new InvalidOperationException(
                $"[{minTimeframe}] 数据不足，无法创建 {windowSize} 根K线的窗口。\n" +
                $"回测开始索引: {backtestStartIndex}, 需要的起始索引: {windowStartIndex}, 数据总量: {minTimeframeCandles.Count}\n" +
                $"请调整回测开始时间，确保有足够的历史数据。"
            );
        }
        
        int actualWindowSize = windowEndIndex - windowStartIndex + 1;
        
        // 🆕 v4.0: 严格验证窗口大小
        if (actualWindowSize != windowSize)
        {
            throw new InvalidOperationException(
                $"[{minTimeframe}] 窗口大小不正确: 当前 {actualWindowSize} 根，需要 {windowSize} 根。\n" +
                $"回测开始索引: {backtestStartIndex}, 窗口范围: {windowStartIndex} ~ {windowEndIndex}"
            );
        }
        
        // 🆕 v4.0: 获取对齐时间（窗口最后一根K线的收盘时间）
        // 这个时间用于对齐其他时间框架的窗口
        var alignmentCandle = minTimeframeCandles[windowEndIndex];
        if (!alignmentCandle.CloseTime.HasValue)
        {
            throw new InvalidOperationException(
                $"窗口最后一根K线缺少收盘时间数据。\n" +
                $"K线索引: {windowEndIndex}, 开盘时间: {alignmentCandle.Time:yyyy-MM-dd HH:mm:ss} UTC"
            );
        }
        DateTime alignmentCloseTime = alignmentCandle.CloseTime.Value;
        
        foreach (var timeframe in timeframes.OrderBy(DSLAnalyzer.TimeframeToMinutes))
        {
            var candles = _multiTimeframeData[timeframe];
            
            // 🆕 v4.0: 对齐到最小时间框架的窗口结束时间（收盘时间）
            // 找到收盘时间最接近且 <= alignmentCloseTime 的K线索引
            int alignmentIndex = -1;
            for (int i = candles.Count - 1; i >= 0; i--)
            {
                // 🔧 v4.0: 直接使用数据库中的真实收盘时间
                var candleCloseTimeNullable = candles[i].CloseTime;
                if (!candleCloseTimeNullable.HasValue)
                {
                    throw new InvalidOperationException(
                        $"K线缺少收盘时间数据。时间框架: {timeframe}, 索引: {i}, 开盘时间: {candles[i].Time:yyyy-MM-dd HH:mm:ss} UTC"
                    );
                }
                var candleCloseTime = candleCloseTimeNullable.Value;
                if (candleCloseTime <= alignmentCloseTime)
                {
                    alignmentIndex = i;
                    break;  // 找到最新的一根收盘时间 <= alignmentCloseTime 的K线
                }
            }
            
            if (alignmentIndex < 0)
            {
                throw new InvalidOperationException(
                    $"无法找到 {timeframe} 时间框架对齐到回测开始时间的K线。\n" +
                    $"对齐时间: {alignmentCloseTime:yyyy-MM-dd HH:mm:ss} UTC\n" +
                    $"数据范围: {candles[0].Time:yyyy-MM-dd HH:mm:ss} UTC ~ {candles[^1].Time:yyyy-MM-dd HH:mm:ss} UTC"
                );
            }
            
            // 🆕 v4.0: 从对齐点向前取 windowSize 根K线（包括对齐点这一根）
            int startIndex = alignmentIndex - windowSize + 1;
            int endIndex = alignmentIndex;
            
            // 🆕 v4.0: 严格检查：必须有足够的数据
            if (startIndex < 0)
            {
                throw new InvalidOperationException(
                    $"[{timeframe}] 数据不足，无法创建 {windowSize} 根K线的窗口。\n" +
                    $"对齐索引: {alignmentIndex}, 需要的起始索引: {startIndex}, 数据总量: {candles.Count}\n" +
                    $"对齐时间: {alignmentCloseTime:yyyy-MM-dd HH:mm:ss} UTC\n" +
                    $"请调整回测开始时间，确保有足够的历史数据。"
                );
            }
            
            int actualSize = endIndex - startIndex + 1;
            
            // 🆕 v4.0: 严格验证窗口大小
            if (actualSize != windowSize)
            {
                throw new InvalidOperationException(
                    $"[{timeframe}] 窗口大小不正确: 当前 {actualSize} 根，需要 {windowSize} 根。\n" +
                    $"对齐索引: {alignmentIndex}, 窗口范围: {startIndex} ~ {endIndex}"
                );
            }
            
            List<Candlestick> initialWindow = candles.Skip(startIndex).Take(actualSize).ToList();
            
            // 🔧 v4.0: 确保窗口K线按时间升序排列（防止倒序导致核心引擎报错）
            initialWindow = initialWindow.OrderBy(c => c.Time).ToList();
            
            // 🔍 v4.0: 验证窗口K线顺序
            if (initialWindow.Count > 1)
            {
                for (int i = 1; i < initialWindow.Count; i++)
                {
                    if (initialWindow[i].Time < initialWindow[i - 1].Time)
                    {
                        Console.WriteLine($"⚠️ 警告：[{timeframe}] 窗口K线在排序后仍存在倒序: index {i-1} ({initialWindow[i-1].Time:yyyy-MM-dd HH:mm:ss}) > index {i} ({initialWindow[i].Time:yyyy-MM-dd HH:mm:ss})");
                        break;
                    }
                }
                // 窗口K线顺序验证通过
            }
            
            // 注入到策略引擎
            _strategyGenerator.SetKlines(timeframe, initialWindow);
            
            // 🔧 v4.0: 记录窗口最后一根K线的索引，用于后续追加时避免重复
            // endIndex 是窗口最后一根K线在 _multiTimeframeData[timeframe] 中的索引
            timeframeIndices[timeframe] = endIndex;
            
            var statusIcon = actualWindowSize < windowSize ? "⚠️" : "✅";
            if (initialWindow.Count > 0)
            {
                var firstCandle = initialWindow[0];
                var lastCandle = initialWindow[^1];
                
                // 🔧 v4.0: 验证K线必须有收盘时间
                if (!firstCandle.CloseTime.HasValue || !lastCandle.CloseTime.HasValue)
                {
                    throw new InvalidOperationException(
                        $"K线缺少收盘时间数据。时间框架: {timeframe}"
                    );
                }
                
            }
        }
        
        await Task.CompletedTask;  // 保持方法签名为async，但移除不必要的让出控制权
        
        return timeframeIndices;  // 🔧 v4.0: 返回每个时间框架的窗口最后一根K线索引
    }
    
    /// <summary>
    /// v10.0 增量追加新K线到所有时间框架
    /// </summary>
    private async Task AppendNewKlinesAsync(
        HashSet<string> timeframes,
        Dictionary<string, int> timeframeIndices,
        DateTime currentTime,
        string minTimeframe,
        int minTimeframeIndex,
        CancellationToken cancellationToken)
    {
        foreach (var timeframe in timeframes)
        {
            if (timeframe == minTimeframe)
            {
                // 最小时间框架：直接追加当前K线
                var candle = _multiTimeframeData[minTimeframe][minTimeframeIndex];
                _strategyGenerator.AppendKline(timeframe, candle);
                timeframeIndices[timeframe] = minTimeframeIndex;
            }
            else
            {
                // 其他时间框架：找出所有 <= currentTime 且未处理的新K线
                // 🔧 v4.0: 只追加时间戳大于窗口最后一根K线的新K线，避免倒序
                var tfData = _multiTimeframeData[timeframe];
                var lastIndex = timeframeIndices[timeframe];
                
                // 获取窗口最后一根K线的时间戳（用于判断是否应该追加）
                DateTime? lastWindowKlineTime = null;
                if (lastIndex >= 0 && lastIndex < tfData.Count)
                {
                    lastWindowKlineTime = tfData[lastIndex].Time;
                }
                
                for (int j = lastIndex + 1; j < tfData.Count; j++)
                {
                    var candidateKline = tfData[j];
                    
                    // 🔧 v4.0: 只追加时间戳大于窗口最后一根K线且 <= currentTime 的K线
                    if (candidateKline.Time <= currentTime)
                    {
                        // 如果窗口最后一根K线存在，确保新K线的时间戳大于它
                        if (lastWindowKlineTime.HasValue && candidateKline.Time <= lastWindowKlineTime.Value)
                        {
                            // 跳过时间戳 <= 窗口最后一根K线的K线（这些K线已经在窗口中了）
                            continue;
                        }
                        
                        _strategyGenerator.AppendKline(timeframe, candidateKline);
                        timeframeIndices[timeframe] = j;
                        lastWindowKlineTime = candidateKline.Time;  // 更新窗口最后一根K线时间
                    }
                    else
                    {
                        break;
                    }
                }
            }
        }
        
        await Task.CompletedTask;  // 保持方法签名为async，但移除不必要的让出控制权
    }
    
    /// <summary>
    /// 处理信号和订单
    /// </summary>
    private async Task ProcessSignalAndOrdersAsync(
        Signal? signal,
        Candlestick currentCandle,
        int currentStep,
        int globalIndex,
        BacktestConfig config,
        bool isLastStep,
        CancellationToken cancellationToken)
    {
        // 核心引擎设计：在任何情况下都会返回信号（包括HOLD信号）
        // 如果signal为null，说明是C#层面的异常，应该创建HOLD信号
        if (signal == null)
        {
            // 只在第一个或最后一个时输出
            if (currentStep == 1 || isLastStep)
            {
                var prefix = currentStep == 1 ? "首个" : "最后";
                Console.WriteLine($"⚠️ [信号生成失败-{prefix}] {currentCandle.Time:yyyy-MM-dd HH:mm:ss} | 价格: {currentCandle.Close:F2} | 原因: C#层异常导致信号生成失败");
            }
            
            var signalEvent = new SignalEvent
            {
                Time = currentCandle.Time,
                Action = SignalAction.HOLD,
                SignalPrice = (decimal)currentCandle.Close,
                WasExecuted = false,
                ReasonIfNotExecuted = "C#层异常导致信号生成失败",
                Description = "[C_SHARP_ERROR] Signal generator returned null"
            };
            _performanceAnalyzer.RecordSignal(signalEvent);
            return;
        }
        
        var signalEvent2 = new SignalEvent
        {
            Time = currentCandle.Time,
            Action = signal.Action,
            SignalPrice = signal.SignalPrice,
            WasExecuted = false,
            ReasonIfNotExecuted = null,
            Description = signal.Description,  // 保存reason到Description字段
            Confidence = (double)signal.Strength,
            Trend = signal.Trend,
            TakeProfit = signal.TakeProfit,
            StopLoss = signal.StopLoss,
            ConfigsJson = signal.ConfigsJson,
            IndicatorsJson = signal.IndicatorsJson,
            IndicatorSnapshotsJson = signal.IndicatorSnapshotsJson,
            DebugJson = signal.DebugJson,
            KlinesJson = signal.KlinesJson  // 🆕 v4.0: K线数据快照
        };
        
        // 检查是否为包含错误信息的HOLD信号
        bool isErrorHold = signal.Action == SignalAction.HOLD &&
                          !string.IsNullOrEmpty(signal.Description) &&
                          (signal.Description.StartsWith("[VALIDATION]") ||
                           signal.Description.StartsWith("[EVALUATOR]") ||
                           signal.Description.StartsWith("[DSL]") ||
                           signal.Description.StartsWith("[RUNTIME]") ||
                           signal.Description.StartsWith("[UNKNOWN]") ||
                           signal.Description.StartsWith("[C_API_ERROR]") ||
                           signal.Description.StartsWith("[C_SHARP_ERROR]"));
        
        if (isErrorHold)
        {
            // 记录验证错误信号，但不触发交易
            signalEvent2.ReasonIfNotExecuted = signal.Description;
            _performanceAnalyzer.RecordSignal(signalEvent2);
            return;
        }
        
            // 处理HOLD信号（普通HOLD，非错误HOLD）
            if (signal.Action == SignalAction.HOLD)
            {
                // 只输出最后一个错误HOLD信号
                if (isLastStep && isErrorHold)
                {
                    Console.WriteLine($"⚠️ [HOLD信号-错误-最后] {currentCandle.Time:yyyy-MM-dd HH:mm:ss} | 价格: {currentCandle.Close:F2} | 原因: {signal.Description}");
                }
            
            // 🆕 v4.0: HOLD信号也触发SignalGenerated事件，以便在DataGrid中显示
            SignalGenerated?.Invoke(this, new SignalGeneratedEventArgs
            {
                BacktestId = config.RunId,
                Signal = signal,
                Candle = currentCandle,
                CandleIndex = currentStep,
                GlobalIndex = currentStep
            });
            
            // 记录HOLD信号，但不触发交易
            _performanceAnalyzer.RecordSignal(signalEvent2);
            return;
        }
        
        // 处理有效的交易信号（BUY 或 SELL）
        // 【反前视】本根只记录信号，不成交；成交延迟到下一根K线开盘（见 ExecutePendingSignalAsync）
        if (signal.Action == SignalAction.BUY || signal.Action == SignalAction.SELL)
        {
            SignalGenerated?.Invoke(this, new SignalGeneratedEventArgs
            {
                BacktestId = config.RunId,
                Signal = signal,
                Candle = currentCandle,
                CandleIndex = currentStep,
                GlobalIndex = globalIndex
            });

            // 若上一根的挂起信号因故未执行（不应发生），先按未执行记录，避免丢失
            if (_pendingSignal != null)
            {
                _pendingEvent!.WasExecuted = false;
                _pendingEvent.ReasonIfNotExecuted = "被新信号覆盖，未执行";
                _performanceAnalyzer.RecordSignal(_pendingEvent);
            }

            _pendingSignal = signal;
            _pendingEvent = signalEvent2;
            _pendingEvent.WasExecuted = false;
            _pendingEvent.ReasonIfNotExecuted = "等待下一根K线开盘执行";
            _pendingStep = currentStep;
            _pendingGlobalIndex = globalIndex;
        }
        
        // HOLD 路径在上方已记录；BUY/SELL 的记录推迟到执行时（或回测结束）
        if (signal.Action == SignalAction.HOLD)
        {
            _performanceAnalyzer.RecordSignal(signalEvent2);
        }

        await Task.CompletedTask;  // 保持方法签名为async，但移除不必要的让出控制权
    }

    /// <summary>
    /// 执行挂起信号：以上一根收盘信号、在本根开盘价成交。
    /// 信号事件的时间仍归属信号产生的那根K线，价格记为实际成交价。
    /// </summary>
    private async Task ExecutePendingSignalAsync(
        Candlestick executionCandle,
        BacktestConfig config,
        CancellationToken cancellationToken)
    {
        if (_pendingSignal == null || _pendingEvent == null)
        {
            return;
        }

        var signal = _pendingSignal;
        var signalEvent = _pendingEvent;
        _pendingSignal = null;
        _pendingEvent = null;

        // 以本根开盘价成交（消除“以信号根收盘价成交”的前视偏差）
        var executionPrice = (decimal)executionCandle.Open;
        var execSignal = new Signal
        {
            Action = signal.Action,
            SignalPrice = executionPrice,
            Time = signal.Time,
            Strength = signal.Strength,
            Description = signal.Description,
            Trend = signal.Trend,
            TakeProfit = signal.TakeProfit,
            StopLoss = signal.StopLoss,
            ConfigsJson = signal.ConfigsJson,
            IndicatorsJson = signal.IndicatorsJson,
            IndicatorSnapshotsJson = signal.IndicatorSnapshotsJson,
            DebugJson = signal.DebugJson,
            KlinesJson = signal.KlinesJson
        };

        var executedOrder = await _orderManager.ProcessSignalAsync(execSignal, executionCandle);

        if (executedOrder != null)
        {
            signalEvent.WasExecuted = true;
            signalEvent.SignalPrice = executionPrice;
            signalEvent.ReasonIfNotExecuted = null;
            _performanceAnalyzer.RecordOrder(executedOrder);
            OrderExecuted?.Invoke(this, new OrderExecutedEventArgs
            {
                BacktestId = config.RunId,
                Order = executedOrder
            });
        }
        else
        {
            signalEvent.WasExecuted = false;
            signalEvent.SignalPrice = executionPrice;
            signalEvent.ReasonIfNotExecuted = "资金不足或当前已有持仓";
        }

        _performanceAnalyzer.RecordSignal(signalEvent);
        await Task.CompletedTask;
    }
    
    /// <summary>
    /// 🆕 v11.0: 根据时间获取对应的恐惧与贪婪指数值（用于日志输出）
    /// </summary>
    private int? GetFearGreedValueForTime(DateTime time)
    {
        if (_fearGreedData == null || _fearGreedData.Count == 0)
            return null;
        
        var targetDate = time.Date;
        var data = _fearGreedData.FirstOrDefault(d => d.Date == targetDate);
        return data?.Value;
    }
    
    /// <summary>
    /// 🆕 v11.0: 根据时间获取对应的资金费率值（用于日志输出）
    /// </summary>
    private decimal? GetFundingRateValueForTime(DateTime time)
    {
        if (_fundingRateData == null || _fundingRateData.Count == 0)
            return null;
        
        var targetTimeMs = new DateTimeOffset(time).ToUnixTimeMilliseconds();
        // 找到最接近且 <= targetTime 的资金费率数据
        var data = _fundingRateData
            .Where(d => d.CalcTime <= targetTimeMs)
            .OrderByDescending(d => d.CalcTime)
            .FirstOrDefault();
        
        return data?.LastFundingRate;
    }
    
    /// <summary>
    /// 🆕 v11.0: 在滑动窗口中注入时间序列数据到核心引擎
    /// 每次生成信号前调用，确保核心引擎能获取到当前时间对应的数据
    /// </summary>
    private void InjectTimeSeriesDataForTime(DateTime currentTime)
    {
        // 时间序列数据在Initialize时已经全部注入到核心引擎
        // 核心引擎会根据当前时间自动查找对应的数据
        // 这里不需要重复注入，只需要确保数据已经准备好即可
        // 如果核心引擎支持动态更新，可以在这里实现
    }
    
    /// <summary>
    /// 报告进度
    /// </summary>
    private async Task ReportProgressAsync(
        int currentStep,
        int totalSteps,
        Candlestick currentCandle,
        BacktestConfig config,
        TimeSpan elapsed,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        double progressPercent = (double)currentStep / totalSteps * 100;
        
        progress?.Report(progressPercent);
        
        // Console.WriteLine($"⏳ 回测进度: {progressPercent:F1}% ({currentStep}/{totalSteps}) " + $"| 权益: {_orderManager.CurrentEquity:F2} USDT");
        
        var estimatedRemaining = EstimateRemainingTime(currentStep, totalSteps, elapsed);
        
        ProgressChanged?.Invoke(this, new BacktestProgressEventArgs
        {
            BacktestId = config.RunId,
            Current = currentStep,
            Total = totalSteps,
            ProgressPercentage = progressPercent / 100,
            CurrentTime = currentCandle.Time,
            CurrentPrice = (decimal)currentCandle.Close,  // 当前价格（最新K线的收盘价）
            CurrentEquity = _orderManager.CurrentEquity,
            EstimatedTimeRemaining = estimatedRemaining
        });
        
        _lastProgressTime = DateTime.UtcNow;
        _lastProgressIndex = currentStep;
        
        await Task.Delay(1, cancellationToken);
    }
    
    /// <summary>
    /// 平掉所有持仓
    /// </summary>
    private async Task CloseFinalPositionsAsync(
        List<Candlestick> minTimeframeData,
        CancellationToken cancellationToken)
    {
        if (_orderManager.CurrentPosition != 0)
        {
            var lastCandle = minTimeframeData[^1];
            // 平多仓使用 SELL 信号，平空仓使用 BUY 信号
            var closeSignal = _orderManager.CurrentPosition > 0
                ? new Signal { Action = SignalAction.SELL, SignalPrice = (decimal)lastCandle.Close, Time = lastCandle.Time }
                : new Signal { Action = SignalAction.BUY, SignalPrice = (decimal)lastCandle.Close, Time = lastCandle.Time };
            
            var finalOrder = await _orderManager.ProcessSignalAsync(closeSignal, lastCandle);
            if (finalOrder != null)
            {
                _performanceAnalyzer.RecordOrder(finalOrder);
                // Console.WriteLine($"🔚 回测结束，强制平仓");
            }
        }
        
        await Task.Yield();
    }
    
    /// <summary>
    /// 批量运行回测（参数优化）
    /// 注意：每个配置独占一个信号生成器（内含原生引擎句柄，不可跨线程共享），
    /// 数据源为只读可共享；生成器用完即释放，避免句柄泄漏。
    /// </summary>
    public async Task<List<BacktestResult>> RunBatchAsync(
        string strategyDslCode,
        List<BacktestConfig> configs,
        ParallelOptions? parallelOptions = null,
        CancellationToken cancellationToken = default,
        Func<IStrategySignalGenerator>? generatorFactory = null)
    {
        parallelOptions ??= new ParallelOptions
        {
            MaxDegreeOfParallelism = Environment.ProcessorCount,
            CancellationToken = cancellationToken
        };
        
        var results = new List<BacktestResult>();
        var lockObj = new object();
        
        await Parallel.ForEachAsync(configs, parallelOptions, async (config, ct) =>
        {
            using var generator = generatorFactory?.Invoke() ?? new DslStrategySignalGenerator();
            var engine = new BacktestEngine(
                _dataFeed,
                new SimulatedOrderManager(config),
                new PerformanceAnalyzer(),
                generator
            );
            
            var result = await engine.RunAsync(strategyDslCode, config, null, ct);
            
            lock (lockObj)
            {
                results.Add(result);
            }
        });
        
        return results;
    }
    
    /// <summary>
    /// 对齐结束时间到时间框架边界（向上取整，确保包含最后一根K线）
    /// </summary>
    private DateTime AlignEndDateToTimeframe(DateTime endDate, int intervalMinutes)
    {
        // 🔧 v4.0: 确保endDate是UTC时间，但不进行时区转换
        var utcEndDate = endDate.Kind == DateTimeKind.Utc 
            ? endDate 
            : DateTime.SpecifyKind(endDate, DateTimeKind.Utc);
        
        var timeOffset = new DateTimeOffset(utcEndDate, TimeSpan.Zero);
        long timestampMs = timeOffset.ToUnixTimeMilliseconds();
        long intervalMs = intervalMinutes * 60 * 1000L;
        
        // 向上取整到最近的时间框架边界（确保包含最后一根K线）
        // 例如：2025-11-30 23:59:59 UTC (5m) -> 2025-12-01 00:00:00 UTC
        long alignedTimestamp = ((timestampMs + intervalMs - 1) / intervalMs) * intervalMs;
        
        return DateTimeOffset.FromUnixTimeMilliseconds(alignedTimestamp).UtcDateTime;
    }
    
    private TimeSpan EstimateRemainingTime(int currentIndex, int totalCount, TimeSpan elapsed)
    {
        if (currentIndex == 0 || currentIndex >= totalCount)
            return TimeSpan.Zero;
        
        var remainingCount = totalCount - currentIndex;
        
        // 计算平均每项处理时间
        var timePerItem = elapsed.TotalMilliseconds / currentIndex;
        var estimatedMs = timePerItem * remainingCount;
        
        return TimeSpan.FromMilliseconds(estimatedMs);
    }
    
    private string EvaluateStrategy(BacktestResult result)
    {
        var score = 0;
        
        if (result.AnnualizedReturn > 0.5m) score += 3;
        else if (result.AnnualizedReturn > 0.2m) score += 2;
        else if (result.AnnualizedReturn > 0) score += 1;
        
        if (result.WinRate > 0.6m) score += 2;
        else if (result.WinRate > 0.5m) score += 1;
        
        if (result.SharpeRatio > 2) score += 2;
        else if (result.SharpeRatio > 1) score += 1;
        
        if (result.ProfitFactor > 2) score += 2;
        else if (result.ProfitFactor > 1.5m) score += 1;
        
        if (result.MaxDrawdown < 0.1m) score += 1;
        else if (result.MaxDrawdown > 0.3m) score -= 1;
        
        return score switch
        {
            >= 8 => "⭐⭐⭐⭐⭐ 优秀",
            >= 6 => "⭐⭐⭐⭐ 良好",
            >= 4 => "⭐⭐⭐ 一般",
            >= 2 => "⭐⭐ 较差",
            _ => "⭐ 不推荐"
        };
    }
}
