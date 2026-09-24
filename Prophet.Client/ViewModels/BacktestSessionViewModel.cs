using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Threading;
using Prophet.Client.Backtest.Models;
using Prophet.Client.Backtest.Performance;
using Prophet.Client.Models;
using Prophet.Client.Services.Strategy;
using Prophet.Client.Services.AI.Core;

namespace Prophet.Client.ViewModels;

public class BacktestSessionViewModel : INotifyPropertyChanged
{
    private readonly RelayCommand _cancelCommand;
    private readonly RelayCommand _pauseResumeCommand;
    private readonly int _maxLogEntries;
    private Action? _cancelAction;
    private Action? _pauseResumeAction;

    private bool _isRunning;
    private bool _isPaused;
    private string _status = "等待开始";
    private double _progressPercentage;
    private string _progressText = "等待中...";
    private decimal _currentEquity;
    private DateTime _currentTime;
    private decimal _currentPrice;
    private decimal _maxDrawdown;
    private decimal _peakEquity;
    private BacktestResult? _result;
    private TimeSpan _avgHoldingTime;
    
    // 权益曲线数据点
    private readonly ObservableCollection<EquityPoint> _equityCurve = new();
    
    // 时间刻度数据点
    private readonly ObservableCollection<TimeMarker> _timeMarkers = new();

    private readonly LocalStrategyService _strategyService;
    private readonly AiServiceManager _aiService;

    public BacktestSessionViewModel(
        StrategyInfo strategy,
        VersionListItem version,
        BacktestConfig config,
        LocalStrategyService strategyService,
        AiServiceManager aiService,
        int maxLogEntries = 100)
    {
        StrategyId = strategy.Id;
        StrategyName = strategy.Name;
        Symbol = config.Symbol;
        VersionString = version.VersionString;
        Config = config;
        InitialCapital = config.InitialCapital;
        SignalSamplingInterval = config.SignalSamplingInterval;
        StartDate = config.StartDate;
        EndDate = config.EndDate;
        _maxLogEntries = maxLogEntries;
        _currentEquity = InitialCapital;
        _peakEquity = InitialCapital;
        _cancelCommand = new RelayCommand(Cancel, () => IsRunning);
        _pauseResumeCommand = new RelayCommand(PauseResume, () => IsRunning);

        _strategyService = strategyService ?? throw new ArgumentNullException(nameof(strategyService));
        _aiService = aiService ?? throw new ArgumentNullException(nameof(aiService));
        
        // 订阅订单集合变化事件，实时更新所有统计信息
        Orders.CollectionChanged += (s, e) =>
        {
            RefreshStatistics();
        };
        
        // 计算时间刻度
        CalculateTimeMarkers();
    }

    public Guid SessionId { get; } = Guid.NewGuid();
    public string StrategyId { get; }
    public string StrategyName { get; }
    public string Symbol { get; }
    public string VersionString { get; }
    public BacktestConfig Config { get; }
    public decimal InitialCapital { get; }
    public string SignalSamplingInterval { get; }
    public DateTime StartDate { get; }
    public DateTime EndDate { get; }
    public string SessionTitle => $"{StrategyName} · {Symbol} · {VersionString}";

    public ObservableCollection<BacktestLogEntry> Logs { get; } = new();
    
    /// <summary>
    /// 本次回测的信号列表（内存中的临时数据，回测过程中实时添加）
    /// </summary>
    public ObservableCollection<SignalEvent> Signals { get; } = new();
    
    /// <summary>
    /// 本次回测的订单列表（从PerformanceAnalyzer同步，只读视图）
    /// </summary>
    public ObservableCollection<Order> Orders { get; } = new();
    
    /// <summary>
    /// 关联的PerformanceAnalyzer（用于同步订单数据）
    /// </summary>
    private IPerformanceAnalyzer? _performanceAnalyzer;

    public bool IsRunning
    {
        get => _isRunning;
        set
        {
            if (SetProperty(ref _isRunning, value))
            {
                _cancelCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string Status
    {
        get => _status;
        set => SetProperty(ref _status, value);
    }

    public double ProgressPercentage
    {
        get => _progressPercentage;
        set
        {
            if (SetProperty(ref _progressPercentage, value))
            {
                OnPropertyChanged(nameof(EstimatedTimeRemaining));
                OnPropertyChanged(nameof(ProgressPercentageDisplay));
            }
        }
    }

    public string ProgressText
    {
        get => _progressText;
        set => SetProperty(ref _progressText, value);
    }

    public decimal CurrentEquity
    {
        get => _currentEquity;
        set
        {
            if (SetProperty(ref _currentEquity, value))
            {
                OnPropertyChanged(nameof(CurrentProfit));
                UpdateEquityCurve();
                UpdateMaxDrawdown();
            }
        }
    }

    private void UpdateEquityCurve()
    {
        if (CurrentTime != default)
        {
            _equityCurve.Add(new EquityPoint
            {
                Time = CurrentTime,
                Equity = CurrentEquity
            });
            // ObservableCollection 会自动通知绑定系统，不需要手动调用 OnPropertyChanged
        }
    }

    private void UpdateMaxDrawdown()
    {
        if (CurrentEquity > PeakEquity)
        {
            PeakEquity = CurrentEquity;
        }
        else if (PeakEquity > 0)
        {
            var drawdown = (PeakEquity - CurrentEquity) / PeakEquity;
            if (drawdown > MaxDrawdown)
            {
                MaxDrawdown = drawdown;
            }
        }
    }

    public decimal CurrentProfit => CurrentEquity - InitialCapital;

    /// <summary>
    /// 当前胜率（实时计算，基于Orders集合）
    /// </summary>
    public decimal CurrentWinRate
    {
        get
        {
            var totalTrades = TotalTrades;
            if (totalTrades > 0)
            {
                return (decimal)WinningTrades / totalTrades;
            }
            return 0;
        }
    }

    public DateTime CurrentTime
    {
        get => _currentTime;
        set
        {
            if (SetProperty(ref _currentTime, value))
            {
                OnPropertyChanged(nameof(CurrentTimeDisplay));
                OnPropertyChanged(nameof(EstimatedTimeRemaining));
                OnPropertyChanged(nameof(ProgressPercentageDisplay));
            }
        }
    }

    public string CurrentTimeDisplay => CurrentTime == default ? "-" : CurrentTime.ToString("yyyy-MM-dd HH:mm");
    
    public decimal CurrentPrice
    {
        get => _currentPrice;
        set
        {
            if (SetProperty(ref _currentPrice, value))
            {
                OnPropertyChanged(nameof(CurrentPriceDisplay));
            }
        }
    }
    
    public string CurrentPriceDisplay => CurrentPrice == 0 ? "-" : $"${CurrentPrice:N2}";
    
    public TimeSpan AvgHoldingTime
    {
        get => _avgHoldingTime;
        set
        {
            if (_avgHoldingTime != value)
            {
                _avgHoldingTime = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(AvgHoldingTimeDisplay));
            }
        }
    }
    
    public string AvgHoldingTimeDisplay
    {
        get
        {
            if (_avgHoldingTime == TimeSpan.Zero)
                return "-";
            
            if (_avgHoldingTime.TotalDays >= 1)
                return $"{_avgHoldingTime.TotalDays:F1} 天";
            else if (_avgHoldingTime.TotalHours >= 1)
                return $"{_avgHoldingTime.TotalHours:F1} 小时";
            else if (_avgHoldingTime.TotalMinutes >= 1)
                return $"{_avgHoldingTime.TotalMinutes:F0} 分钟";
            else
                return $"{_avgHoldingTime.TotalSeconds:F0} 秒";
        }
    }
    
    /// <summary>
    /// 单笔最大盈利（实时计算，基于Orders集合）
    /// </summary>
    public decimal MaxSingleProfit
    {
        get
        {
            var closedOrders = Orders.Where(o => o != null && o.Status == OrderStatus.CLOSED && o.Profit.HasValue).ToList();
            if (closedOrders.Count == 0)
            {
                return 0;
            }
            return closedOrders.Max(o => o.Profit!.Value);
        }
    }
    
    /// <summary>
    /// 单笔最大亏损（实时计算，基于Orders集合）
    /// </summary>
    public decimal MaxSingleLoss
    {
        get
        {
            var closedOrders = Orders.Where(o => o != null && o.Status == OrderStatus.CLOSED && o.Profit.HasValue).ToList();
            if (closedOrders.Count == 0)
            {
                return 0;
            }
            return closedOrders.Min(o => o.Profit!.Value);
        }
    }
    
    /// <summary>
    /// 合计手续费（实时计算，基于Orders集合）
    /// </summary>
    public decimal TotalFees
    {
        get
        {
            return Orders.Where(o => o != null && o.Status == OrderStatus.CLOSED)
                .Sum(o => o.Fee + o.FundingFee);
        }
    }
    
    /// <summary>
    /// 日均订单数（实时计算）
    /// 重构：基于实际订单数据源（Orders集合）实时计算，而不是依赖TotalTrades
    /// 基于用户配置的回测区间（StartDate到EndDate），而不是实际数据加载的区间
    /// </summary>
    public decimal DailyAverageOrders
    {
        get
        {
            // 确保使用正确的日期范围（用户配置的回测区间）
            var startDate = StartDate.Date;  // 只取日期部分，忽略时间
            var endDate = EndDate.Date;       // 只取日期部分，忽略时间
            
            // 计算天数：结束日期 - 开始日期 + 1（包含开始和结束日期）
            var days = (endDate - startDate).TotalDays + 1;
            
            if (days <= 0)
            {
                return 0;
            }
            
            // 从实际订单数据源计算已平仓订单数量（更准确）
            var closedOrdersCount = Orders.Count(o => o != null && o.Status == OrderStatus.CLOSED);
            
            if (closedOrdersCount > 0)
            {
                return (decimal)closedOrdersCount / (decimal)days;
            }
            
            return 0;
        }
    }

    public string EstimatedTimeRemaining
    {
        get
        {
            if (ProgressPercentage <= 0 || ProgressPercentage >= 100)
            {
                return "-";
            }

            if (CurrentTime == default || StartDate == default)
            {
                return "计算中...";
            }

            try
            {
                var elapsed = CurrentTime - StartDate;
                var totalDuration = EndDate - StartDate;
                var remainingDuration = totalDuration - elapsed;
                
                if (remainingDuration.TotalSeconds <= 0)
                {
                    return "即将完成";
                }

                if (remainingDuration.TotalDays >= 1)
                {
                    return $"{(int)remainingDuration.TotalDays}天 {remainingDuration.Hours}小时";
                }
                else if (remainingDuration.TotalHours >= 1)
                {
                    return $"{(int)remainingDuration.TotalHours}小时 {remainingDuration.Minutes}分钟";
                }
                else
                {
                    return $"{(int)remainingDuration.TotalMinutes}分钟";
                }
            }
            catch
            {
                return "计算中...";
            }
        }
    }
    
    /// <summary>
    /// 进度百分比显示（包含剩余时间）
    /// 格式：89%(剩余时间：3天5小时35分)
    /// </summary>
    public string ProgressPercentageDisplay
    {
        get
        {
            if (ProgressPercentage >= 100)
            {
                return "100% (已完成)";
            }
            
            if (ProgressPercentage <= 0)
            {
                return "0% (准备中...)";
            }
            
            if (CurrentTime == default || StartDate == default)
            {
                return $"{ProgressPercentage:F1}% (计算中...)";
            }

            try
            {
                var elapsed = CurrentTime - StartDate;
                var totalDuration = EndDate - StartDate;
                var remainingDuration = totalDuration - elapsed;
                
                if (remainingDuration.TotalSeconds <= 0)
                {
                    return $"{ProgressPercentage:F1}% (即将完成)";
                }

                string timeText;
                if (remainingDuration.TotalDays >= 1)
                {
                    var days = (int)remainingDuration.TotalDays;
                    var hours = remainingDuration.Hours;
                    var minutes = remainingDuration.Minutes;
                    timeText = $"{days}天{hours}小时{minutes}分";
                }
                else if (remainingDuration.TotalHours >= 1)
                {
                    var hours = (int)remainingDuration.TotalHours;
                    var minutes = remainingDuration.Minutes;
                    timeText = $"{hours}小时{minutes}分";
                }
                else if (remainingDuration.TotalMinutes >= 1)
                {
                    var minutes = (int)remainingDuration.TotalMinutes;
                    var seconds = remainingDuration.Seconds;
                    timeText = $"{minutes}分{seconds}秒";
                }
                else
                {
                    timeText = $"{(int)remainingDuration.TotalSeconds}秒";
                }

                return $"{ProgressPercentage:F1}% (剩余时间：{timeText})";
            }
            catch
            {
                return $"{ProgressPercentage:F1}% (计算中...)";
            }
        }
    }

    /// <summary>
    /// 多单数量（实时计算，基于Orders集合）
    /// </summary>
    public int LongOrderCount
    {
        get
        {
            return Orders.Count(o => o != null && o.Status == OrderStatus.CLOSED && o.Side == OrderSide.BUY);
        }
    }

    /// <summary>
    /// 空单数量（实时计算，基于Orders集合）
    /// </summary>
    public int ShortOrderCount
    {
        get
        {
            return Orders.Count(o => o != null && o.Status == OrderStatus.CLOSED && o.Side == OrderSide.SELL);
        }
    }

    /// <summary>
    /// 总交易数（实时计算，基于Orders集合）
    /// </summary>
    public int TotalTrades
    {
        get
        {
            return Orders.Count(o => o != null && o.Status == OrderStatus.CLOSED);
        }
    }

    /// <summary>
    /// 盈利订单数（实时计算，基于Orders集合）
    /// </summary>
    public int WinningTrades
    {
        get
        {
            return Orders.Count(o => o != null && o.Status == OrderStatus.CLOSED && o.Profit.HasValue && o.Profit.Value > 0);
        }
    }

    /// <summary>
    /// 多单盈利数（实时计算，基于Orders集合）
    /// </summary>
    public int LongWinningTrades
    {
        get
        {
            return Orders.Count(o => o != null && o.Status == OrderStatus.CLOSED && o.Side == OrderSide.BUY && o.Profit.HasValue && o.Profit.Value > 0);
        }
    }

    /// <summary>
    /// 空单盈利数（实时计算，基于Orders集合）
    /// </summary>
    public int ShortWinningTrades
    {
        get
        {
            return Orders.Count(o => o != null && o.Status == OrderStatus.CLOSED && o.Side == OrderSide.SELL && o.Profit.HasValue && o.Profit.Value > 0);
        }
    }

    /// <summary>
    /// 多单胜率（实时计算）
    /// </summary>
    public decimal LongWinRate => LongOrderCount > 0 ? (decimal)LongWinningTrades / LongOrderCount : 0m;
    
    /// <summary>
    /// 空单胜率（实时计算）
    /// </summary>
    public decimal ShortWinRate => ShortOrderCount > 0 ? (decimal)ShortWinningTrades / ShortOrderCount : 0m;

    public decimal MaxDrawdown
    {
        get => _maxDrawdown;
        set => SetProperty(ref _maxDrawdown, value);
    }

    public decimal PeakEquity
    {
        get => _peakEquity;
        set => SetProperty(ref _peakEquity, value);
    }

    public ObservableCollection<EquityPoint> EquityCurve => _equityCurve;
    
    public ObservableCollection<TimeMarker> TimeMarkers => _timeMarkers;

    public BacktestResult? Result
    {
        get => _result;
        set
        {
            if (SetProperty(ref _result, value))
            {
                OnPropertyChanged(nameof(HasResult));
            }
        }
    }

    public bool HasResult => Result != null;

    public bool IsPaused
    {
        get => _isPaused;
        set
        {
            if (SetProperty(ref _isPaused, value))
            {
                OnPropertyChanged(nameof(PauseResumeButtonText));
                _pauseResumeCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string PauseResumeButtonText => IsPaused ? "继续" : "暂停";

    public ICommand CancelCommand => _cancelCommand;
    public ICommand PauseResumeCommand => _pauseResumeCommand;

    public event PropertyChangedEventHandler? PropertyChanged;

    #region AI 分析相关属性和方法

    private bool _isAnalyzing = false;
    /// <summary>
    /// 是否正在进行AI分析
    /// </summary>
    public bool IsAnalyzing
    {
        get => _isAnalyzing;
        set => SetProperty(ref _isAnalyzing, value);
    }

    private string? _aiAnalysisResult = null;
    /// <summary>
    /// AI分析结果
    /// </summary>
    public string? AiAnalysisResult
    {
        get => _aiAnalysisResult;
        set => SetProperty(ref _aiAnalysisResult, value);
    }

    private string? _aiOptimizedCode = null;
    /// <summary>
    /// AI优化后的策略代码
    /// </summary>
    public string? AiOptimizedCode
    {
        get => _aiOptimizedCode;
        set => SetProperty(ref _aiOptimizedCode, value);
    }

    private bool _hasAiAnalysis = false;
    /// <summary>
    /// 是否有AI分析结果
    /// </summary>
    public bool HasAiAnalysis
    {
        get => _hasAiAnalysis;
        set => SetProperty(ref _hasAiAnalysis, value);
    }

    /// <summary>
    /// 执行AI分析（在回测页面内直接生成分析结论 + 优化DSL）
    /// </summary>
    public async Task RunAiAnalysisAsync()
    {
        if (Result == null)
        {
            AiAnalysisResult = "无回测结果可供分析";
            return;
        }

        try
        {
            IsAnalyzing = true;
            AiAnalysisResult = "AI正在分析回测结果...";
            HasAiAnalysis = false;

            // 1) 初始化AI Provider（需要用户在设置中配置Key）
            await _aiService.LoadFromSettingsAsync();
            if (!_aiService.IsInitialized)
            {
                AiAnalysisResult = "请先在设置中配置 AI API 密钥，然后再进行AI分析。";
                AiOptimizedCode = null;
                HasAiAnalysis = false;
                return;
            }

            // 2) 获取用于分析的策略DSL（优先使用本次回测的版本号，否则回退到最新可用版本）
            var strategyDsl = await LoadStrategyDslForAiAsync();
            if (string.IsNullOrWhiteSpace(strategyDsl))
            {
                AiAnalysisResult = "无法加载策略DSL（用于AI分析）。请确认该策略版本存在且已保存。";
                AiOptimizedCode = null;
                HasAiAnalysis = false;
                return;
            }

            var analysis = await _aiService.AnalyzeBacktestResultAsync(Result, strategyDsl);
            
            // 解析AI响应，分离分析结果和优化代码
            var (text, code) = ParseAiResponse(analysis);
            
            AiAnalysisResult = text;
            AiOptimizedCode = code;
            HasAiAnalysis = true;
        }
        catch (Exception ex)
        {
            AiAnalysisResult = $"AI分析失败: {ex.Message}";
        }
        finally
        {
            IsAnalyzing = false;
        }
    }

    /// <summary>
    /// 加载用于 AI 分析的策略 DSL
    /// </summary>
    private async Task<string?> LoadStrategyDslForAiAsync()
    {
        // 优先：本次回测结果携带的版本号
        var preferredVersion = Result?.VersionString;
        if (string.IsNullOrWhiteSpace(preferredVersion) || preferredVersion == "历史记录")
        {
            preferredVersion = VersionString;
        }

        if (!string.IsNullOrWhiteSpace(preferredVersion) && preferredVersion != "历史记录")
        {
            try
            {
                var version = await _strategyService.GetStrategyVersionAsync(StrategyId, preferredVersion);
                if (version != null && !string.IsNullOrWhiteSpace(version.Dsl))
                {
                    return version.Dsl;
                }
            }
            catch
            {
                // 回退到后续策略：取最新可用版本
            }
        }

        // 回退：取 active 版本（或最新版本）作为上下文
        try
        {
            var versions = await _strategyService.GetStrategyVersionsAsync(StrategyId);
            var candidate = versions.FirstOrDefault(v => v.Status == "active") ?? versions.FirstOrDefault();
            if (candidate == null || string.IsNullOrWhiteSpace(candidate.VersionString))
            {
                return null;
            }

            var latest = await _strategyService.GetStrategyVersionAsync(StrategyId, candidate.VersionString);
            return latest?.Dsl;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 解析AI响应，分离文本和代码
    /// </summary>
    /// <param name="response">AI响应</param>
    /// <returns>文本和代码的元组</returns>
    private (string text, string code) ParseAiResponse(string response)
    {
        // 查找代码块（```...```）
        int codeStart = response.IndexOf("```");
        if (codeStart == -1)
        {
            return (response, string.Empty);
        }
        
        // 跳过语言标识（如 ```dsl）
        int codeContentStart = response.IndexOf('\n', codeStart) + 1;
        int codeEnd = response.IndexOf("```", codeContentStart);
        
        if (codeEnd == -1)
        {
            return (response, string.Empty);
        }
        
        string text = response.Substring(0, codeStart).Trim() + "\n\n" + 
                     response.Substring(codeEnd + 3).Trim();
        string code = response.Substring(codeContentStart, codeEnd - codeContentStart).Trim();
        
        return (text, code);
    }

    #endregion

    public void RegisterCancelAction(Action cancelAction)
    {
        _cancelAction = cancelAction;
    }

    public void RegisterPauseResumeAction(Action pauseResumeAction)
    {
        _pauseResumeAction = pauseResumeAction;
    }

    public void AppendLog(string message, string level = "INFO")
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            AppendLogInternal(message, level);
        }
        else
        {
            Dispatcher.UIThread.Post(() => AppendLogInternal(message, level));
        }
    }

    /// <summary>
    /// 关联PerformanceAnalyzer，订阅订单变化事件
    /// 这是重构后的新方法：ViewModel不再独立管理订单，而是从PerformanceAnalyzer同步
    /// </summary>
    public void AttachPerformanceAnalyzer(IPerformanceAnalyzer performanceAnalyzer)
    {
        if (performanceAnalyzer == null)
        {
            throw new ArgumentNullException(nameof(performanceAnalyzer));
        }
        
        // 取消之前的订阅（如果有）
        if (_performanceAnalyzer is PerformanceAnalyzer oldAnalyzer)
        {
            oldAnalyzer.OrderChanged -= OnOrderChanged;
        }
        
        _performanceAnalyzer = performanceAnalyzer;
        
        // 订阅订单变化事件
        if (performanceAnalyzer is PerformanceAnalyzer analyzer)
        {
            analyzer.OrderChanged += OnOrderChanged;
            
            // 同步已有订单
            Dispatcher.UIThread.Post(() =>
            {
                Orders.Clear();
                foreach (var order in analyzer.Orders)
                {
                    if (order != null)
                    {
                        Orders.Add(order);
                    }
                }
                
                // 同步后，重新计算所有统计信息
                RefreshStatistics();
            });
        }
    }
    
    /// <summary>
    /// 订单变化事件处理（从PerformanceAnalyzer同步订单）
    /// </summary>
    private void OnOrderChanged(object? sender, OrderChangedEventArgs e)
    {
        if (e?.Order == null)
        {
            return;
        }
        
        Dispatcher.UIThread.Post(() =>
        {
            if (e.ChangeType == OrderChangeType.Added)
            {
                // 新增订单
                Orders.Add(e.Order);
            }
            else if (e.ChangeType == OrderChangeType.Updated)
            {
                // 更新订单：找到现有订单并更新属性
                var existingOrder = Orders.FirstOrDefault(o => o.Id == e.Order.Id);
                if (existingOrder != null)
                {
                    // 更新订单属性（保持对象引用，只更新属性值）
                    existingOrder.Status = e.Order.Status;
                    existingOrder.ClosePrice = e.Order.ClosePrice;
                    existingOrder.CloseTime = e.Order.CloseTime;
                    existingOrder.Fee = e.Order.Fee;
                    existingOrder.FundingFee = e.Order.FundingFee;
                    existingOrder.Profit = e.Order.Profit;
                    existingOrder.Remarks = e.Order.Remarks;
                    
                    // 触发集合变化通知（通过移除和重新添加）
                    var index = Orders.IndexOf(existingOrder);
                    Orders.RemoveAt(index);
                    Orders.Insert(index, existingOrder);
                }
            }
            
            // 重构：每次订单变化时都重新计算所有统计信息（基于实际数据源）
            RefreshStatistics();
            
            // 只在订单平仓时才更新平均持仓时长（需要重新计算）
            if (e.Order.Status == OrderStatus.CLOSED)
            {
                _ = Task.Run(() =>
                {
                    try
                    {
                        UpdateAvgHoldingTime();
                    }
                    catch
                    {
                        // 静默忽略错误，不影响主流程
                    }
                });
            }
        });
    }
    
    /// <summary>
    /// 刷新所有统计信息（基于Orders集合实时计算）
    /// </summary>
    public void RefreshStatistics()
    {
        // 通知所有计算属性重新计算
        OnPropertyChanged(nameof(DailyAverageOrders));
        OnPropertyChanged(nameof(TotalTrades));
        OnPropertyChanged(nameof(LongOrderCount));
        OnPropertyChanged(nameof(ShortOrderCount));
        OnPropertyChanged(nameof(WinningTrades));
        OnPropertyChanged(nameof(LongWinningTrades));
        OnPropertyChanged(nameof(ShortWinningTrades));
        OnPropertyChanged(nameof(CurrentWinRate));
        OnPropertyChanged(nameof(LongWinRate));
        OnPropertyChanged(nameof(ShortWinRate));
        OnPropertyChanged(nameof(MaxSingleProfit));
        OnPropertyChanged(nameof(MaxSingleLoss));
        OnPropertyChanged(nameof(TotalFees));
    }
    
    /// <summary>
    /// 更新平均持仓时长（后台计算）
    /// </summary>
    private void UpdateAvgHoldingTime()
    {
        var closedOrders = Orders
            .Where(o => o.Status == OrderStatus.CLOSED && o.CloseTime.HasValue)
            .ToList();
        
        if (closedOrders.Count == 0)
        {
            return;
        }
        
        var holdingTimes = closedOrders
            .Select(o => o.CloseTime!.Value - o.OpenTime)
            .ToList();
        
        var avgTicks = (long)holdingTimes.Average(t => t.Ticks);
        var avgTime = TimeSpan.FromTicks(avgTicks);
        
        Dispatcher.UIThread.Post(() =>
        {
            AvgHoldingTime = avgTime;
        });
    }

    public void RecordSignal(SignalEvent signal)
    {
        Dispatcher.UIThread.Post(() =>
        {
            // 插入到正确位置以保持时间倒序（最新的在前面）
            int insertIndex = 0;
            for (int i = 0; i < Signals.Count; i++)
            {
                if (signal.Time > Signals[i].Time)
                {
                    insertIndex = i;
                    break;
                }
                insertIndex = i + 1;
            }
            Signals.Insert(insertIndex, signal);
        });
    }

    private void AppendLogInternal(string message, string level)
    {
        Logs.Add(new BacktestLogEntry
        {
            Timestamp = DateTime.Now,
            Level = level,
            Message = message
        });

        while (Logs.Count > _maxLogEntries)
        {
            Logs.RemoveAt(0);
        }
    }

    private void Cancel()
    {
        _cancelAction?.Invoke();
    }

    private void PauseResume()
    {
        _pauseResumeAction?.Invoke();
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(storage, value))
        {
            return false;
        }

        storage = value;
        OnPropertyChanged(propertyName);
        return true;
    }
    
    /// <summary>
    /// 计算时间刻度（数据准备阶段 + 回测时间段划分）
    /// 固定11个刻度：0%, 10%, 20%, ..., 100%
    /// </summary>
    private void CalculateTimeMarkers()
    {
        _timeMarkers.Clear();
        
        var totalDays = (EndDate - StartDate).TotalDays;
        
        // 根据总天数决定日期格式
        string dateFormat;
        if (totalDays <= 30)
        {
            dateFormat = "MM-dd";
        }
        else if (totalDays <= 365)
        {
            dateFormat = "MM/dd";
        }
        else
        {
            dateFormat = "yy/MM";
        }
        
        // 固定11个刻度点（0%, 10%, 20%, ..., 100%）
        const int totalMarkers = 11;
        
        for (int i = 0; i < totalMarkers; i++)
        {
            var position = i * 10.0; // 0%, 10%, 20%, ..., 100%
            
            if (i == 0)
            {
                // 第一个标记：数据准备（0%）
                _timeMarkers.Add(new TimeMarker
                {
                    Label = "准备",
                    Position = position,
                    PositionPercent = $"{position}%",
                    IsPhaseLabel = true
                });
            }
            else
            {
                // 时间刻度点：10% 对应开始时间，100% 对应结束时间
                // 将10%-100%映射到StartDate-EndDate
                var ratio = (double)(i - 1) / (totalMarkers - 2); // (i-1)/9
                var markerDate = StartDate.AddDays(totalDays * ratio);
                
                _timeMarkers.Add(new TimeMarker
                {
                    Label = markerDate.ToString(dateFormat),
                    Position = position,
                    PositionPercent = $"{position}%",
                    Date = markerDate,
                    IsPhaseLabel = false
                });
            }
        }
    }
}

public class BacktestLogEntry
{
    public DateTime Timestamp { get; init; }
    public string Level { get; init; } = "INFO";
    public string Message { get; init; } = string.Empty;
    public string DisplayText => $"[{Timestamp:HH:mm:ss}] [{Level}] {Message}";
}

/// <summary>
/// 时间刻度标记
/// </summary>
public class TimeMarker
{
    /// <summary>
    /// 显示标签（日期或阶段名称）
    /// </summary>
    public string Label { get; set; } = string.Empty;
    
    /// <summary>
    /// 位置（0-100百分比）
    /// </summary>
    public double Position { get; set; }
    
    /// <summary>
    /// 位置百分比字符串（用于Grid.Column定位）
    /// </summary>
    public string PositionPercent { get; set; } = "0%";
    
    /// <summary>
    /// 对应的日期（可选）
    /// </summary>
    public DateTime? Date { get; set; }
    
    /// <summary>
    /// 是否是阶段标签（如"数据准备"）
    /// </summary>
    public bool IsPhaseLabel { get; set; }
}

