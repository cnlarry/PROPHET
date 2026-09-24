using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Threading;
using Prophet.Client.Backtest.Engine;
using Prophet.Client.Backtest.Models;
using Prophet.Client.Models;
using Prophet.Client.Backtest.DataFeeds;
using Prophet.Client.Backtest.OrderManagement;
using Prophet.Client.Backtest.Performance;
using Prophet.Client.Backtest.Storage;
using Prophet.Client.Backtest.Strategy;
using Prophet.Client.Database.Repositories;
using Prophet.Client.Services;
using Prophet.Client.Services.Strategy;

namespace Prophet.Client.ViewModels;

/// <summary>
/// 回测模块ViewModel
/// <summary>
/// 回测ViewModel（离线版）
/// </summary>
public class BacktestViewModel : INotifyPropertyChanged
{
    private readonly LocalStrategyService _strategyService;
    private readonly BacktestSessionFactory _sessionFactory;
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _sessionTokens = new();

    #region 属性

    // 策略列表
    private ObservableCollection<StrategyInfo> _strategies = new();
    public ObservableCollection<StrategyInfo> Strategies
    {
        get => _strategies;
        set
        {
            if (_strategies != value)
            {
                _strategies = value;
                OnPropertyChanged();
            }
        }
    }

    // 选中的策略
    private StrategyInfo? _selectedStrategy;
    public StrategyInfo? SelectedStrategy
    {
        get => _selectedStrategy;
        set
        {
            if (_selectedStrategy != value)
            {
                _selectedStrategy = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanStartBacktest));
                _ = LoadStrategyVersionsAsync();
            }
        }
    }

    // 策略版本列表
    private ObservableCollection<VersionListItem> _strategyVersions = new();
    public ObservableCollection<VersionListItem> StrategyVersions
    {
        get => _strategyVersions;
        set
        {
            if (_strategyVersions != value)
            {
                _strategyVersions = value;
                OnPropertyChanged();
            }
        }
    }

    // 选中的版本
    private VersionListItem? _selectedVersion;
    public VersionListItem? SelectedVersion
    {
        get => _selectedVersion;
        set
        {
            if (_selectedVersion != value)
            {
                _selectedVersion = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanStartBacktest));
            }
        }
    }

    // 回测配置
    private string _symbol = "BTCUSDT-BINANCE-SWAP";
    public string Symbol
    {
        get => _symbol;
        set
        {
            if (_symbol != value)
            {
                _symbol = value;
                OnPropertyChanged();
            }
        }
    }

    private string _interval = "1m";
    public string Interval
    {
        get => _interval;
        set
        {
            if (_interval != value)
            {
                _interval = value;
                OnPropertyChanged();
            }
        }
    }

    private DateTime _startDate = DateTime.Now.AddMonths(-1);
    public DateTimeOffset StartDate
    {
        get => _startDate;
        set
        {
            var dateTime = value.DateTime;
            if (_startDate != dateTime)
            {
                _startDate = dateTime;
                OnPropertyChanged();
            }
        }
    }

    private DateTime _endDate = DateTime.Now;
    public DateTimeOffset EndDate
    {
        get => _endDate;
        set
        {
            var dateTime = value.DateTime;
            if (_endDate != dateTime)
            {
                _endDate = dateTime;
                OnPropertyChanged();
            }
        }
    }

    private decimal _initialCapital = 10000;
    public decimal InitialCapital
    {
        get => _initialCapital;
        set
        {
            if (_initialCapital != value)
            {
                _initialCapital = value;
                OnPropertyChanged();
            }
        }
    }

    private decimal _feeRate = 0.001m;
    public decimal FeeRate
    {
        get => _feeRate;
        set
        {
            if (_feeRate != value)
            {
                _feeRate = value;
                OnPropertyChanged();
            }
        }
    }

    private decimal _slippageRate = 0.0005m;
    public decimal SlippageRate
    {
        get => _slippageRate;
        set
        {
            if (_slippageRate != value)
            {
                _slippageRate = value;
                OnPropertyChanged();
            }
        }
    }

    // 信号采样频率选项（使用所有标准时间框架，排除月线）
    public ObservableCollection<SignalIntervalOption> SignalSamplingIntervals { get; } = new();
    
    private SignalIntervalOption? _signalSamplingInterval;
    public SignalIntervalOption? SignalSamplingInterval
    {
        get => _signalSamplingInterval;
        set
        {
            if (_signalSamplingInterval != value)
            {
                _signalSamplingInterval = value;
                OnPropertyChanged();
            }
        }
    }

    // 活动回测会话
    public ObservableCollection<BacktestSessionViewModel> Sessions { get; } = new();
    private BacktestSessionViewModel? _selectedSession;
    public BacktestSessionViewModel? SelectedSession
    {
        get => _selectedSession;
        set
        {
            if (_selectedSession != value)
            {
                _selectedSession = value;
                OnPropertyChanged();
            }
        }
    }
    public bool HasSessions => Sessions.Count > 0;
    public bool HasNoSessions => !HasSessions;

    private ObservableCollection<BacktestResult> _backtestHistory = new();
    public ObservableCollection<BacktestResult> BacktestHistory
    {
        get => _backtestHistory;
        set
        {
            if (_backtestHistory != value)
            {
                _backtestHistory.CollectionChanged -= OnHistoryCollectionChanged;
                _backtestHistory = value;
                _backtestHistory.CollectionChanged += OnHistoryCollectionChanged;
                OnPropertyChanged();
                OnHistoryCollectionChanged(this, null);
            }
        }
    }

    public bool HasHistory => BacktestHistory.Count > 0;
    public bool HasNoHistory => !HasHistory;

    // 筛选后的回测历史
    private ObservableCollection<BacktestResult> _filteredBacktestHistory = new();
    public ObservableCollection<BacktestResult> FilteredBacktestHistory
    {
        get => _filteredBacktestHistory;
        set
        {
            if (_filteredBacktestHistory != value)
            {
                _filteredBacktestHistory = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasFilteredHistory));
                OnPropertyChanged(nameof(HasNoFilteredHistory));
            }
        }
    }

    public bool HasFilteredHistory => FilteredBacktestHistory.Count > 0;
    public bool HasNoFilteredHistory => !HasFilteredHistory;
    public bool HasRunningSessions => Sessions.Any(s => s.IsRunning);
    
    /// <summary>
    /// 获取正在运行的任务列表
    /// </summary>
    public IEnumerable<BacktestSessionViewModel> RunningSessions => Sessions.Where(s => s.IsRunning);

    // 筛选器 - 策略列表
    private ObservableCollection<StrategyInfo> _filterStrategies = new();
    public ObservableCollection<StrategyInfo> FilterStrategies
    {
        get => _filterStrategies;
        set
        {
            if (_filterStrategies != value)
            {
                _filterStrategies = value;
                OnPropertyChanged();
            }
        }
    }

    private StrategyInfo? _selectedFilterStrategy;
    public StrategyInfo? SelectedFilterStrategy
    {
        get => _selectedFilterStrategy;
        set
        {
            if (_selectedFilterStrategy != value)
            {
                _selectedFilterStrategy = value;
                OnPropertyChanged();
                ApplyFiltersAndSort();
            }
        }
    }

    // 筛选器 - 状态
    public ObservableCollection<string> StatusFilters { get; } = new()
    {
        "全部状态",
        "已完成",
        "进行中",
        "失败"
    };

    private string _selectedStatusFilter = "全部状态";
    public string SelectedStatusFilter
    {
        get => _selectedStatusFilter;
        set
        {
            if (_selectedStatusFilter != value)
            {
                _selectedStatusFilter = value;
                OnPropertyChanged();
                ApplyFiltersAndSort();
            }
        }
    }

    // 排序选项
    public ObservableCollection<string> SortOptions { get; } = new()
    {
        "时间（最新）",
        "时间（最旧）",
        "收益率（高到低）",
        "收益率（低到高）",
        "胜率（高到低）",
        "回撤（小到大）",
        "夏普比率（高到低）"
    };

    private string _selectedSortOption = "时间（最新）";
    public string SelectedSortOption
    {
        get => _selectedSortOption;
        set
        {
            if (_selectedSortOption != value)
            {
                _selectedSortOption = value;
                OnPropertyChanged();
                ApplyFiltersAndSort();
            }
        }
    }

    // UI状态
    public bool CanStartBacktest => SelectedStrategy != null && SelectedVersion != null;

    // 状态消息
    private string _statusMessage = "准备就绪";
    public string StatusMessage
    {
        get => _statusMessage;
        set
        {
            if (_statusMessage != value)
            {
                _statusMessage = value;
                OnPropertyChanged();
            }
        }
    }

    #endregion

    #region 命令

    public ICommand LoadStrategiesCommand { get; }
    public ICommand StartBacktestCommand { get; }
    public ICommand LoadHistoryCommand { get; }
    
    // 命令实现（用于更新状态）
    private RelayCommand? _startBacktestCommandImpl;

    #endregion

    public BacktestViewModel(LocalStrategyService strategyService, BacktestSessionFactory sessionFactory)
    {
        try
        {
            _strategyService = strategyService ?? throw new ArgumentNullException(nameof(strategyService));
            _sessionFactory = sessionFactory ?? throw new ArgumentNullException(nameof(sessionFactory));
            Sessions.CollectionChanged += OnSessionsCollectionChanged;
            BacktestHistory.CollectionChanged += OnHistoryCollectionChanged;
        
            // 初始化命令
            LoadStrategiesCommand = new RelayCommand(async () => await LoadStrategiesAsync());
        
            _startBacktestCommandImpl = new RelayCommand(async () => await StartBacktestAsync(), () => CanStartBacktest);
            StartBacktestCommand = _startBacktestCommandImpl;
        
            LoadHistoryCommand = new RelayCommand(async () => await LoadBacktestHistoryAsync());
        
            // 🔧 初始化信号采样频率选项（使用所有标准时间框架，排除月线）
            var allIntervals = TimeFrameExtensions.GetBacktestSamplingIntervals();
            foreach (var interval in allIntervals)
            {
                SignalSamplingIntervals.Add(interval);
            }
            
            // 设置默认的信号采样频率为 5m
            SignalSamplingInterval = SignalSamplingIntervals.FirstOrDefault(i => i.Value == "5m") 
                                     ?? SignalSamplingIntervals.FirstOrDefault();
        
            // 从数据库加载真实的历史记录（异步加载，不阻塞UI）
            _ = Task.Run(async () =>
            {
                try
                {
                    await LoadBacktestHistoryAsync();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ 加载历史记录失败: {ex.Message}");
                    Console.WriteLine($"   堆栈跟踪: {ex.StackTrace}");
                }
            });
        
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ BacktestViewModel 初始化失败: {ex.Message}");
            Console.WriteLine($"   堆栈跟踪: {ex.StackTrace}");
            StatusMessage = $"初始化失败: {ex.Message}";
            throw;
        }
    }

    #region 方法

    private void OnSessionsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(HasSessions));
        OnPropertyChanged(nameof(HasNoSessions));
        OnPropertyChanged(nameof(HasRunningSessions));
        OnPropertyChanged(nameof(RunningSessions));

        if (Sessions.Count == 0)
        {
            SelectedSession = null;
            return;
        }

        // 订阅新添加的 session 的 PropertyChanged 事件，以便在 IsRunning 变化时更新 RunningSessions
        if (e.Action == NotifyCollectionChangedAction.Add && e.NewItems != null)
        {
            foreach (var item in e.NewItems)
            {
                if (item is BacktestSessionViewModel newSession)
                {
                    // 订阅 PropertyChanged 事件，当 IsRunning 变化时更新 RunningSessions
                    newSession.PropertyChanged += OnSessionPropertyChanged;
                    
                    if (e.NewStartingIndex == 0 || SelectedSession == null)
                    {
                        SelectedSession = newSession;
                    }
                }
            }
        }
        
        // 取消订阅被移除的 session 的 PropertyChanged 事件
        if (e.Action == NotifyCollectionChangedAction.Remove && e.OldItems != null)
        {
            foreach (var item in e.OldItems)
            {
                if (item is BacktestSessionViewModel oldSession)
                {
                    oldSession.PropertyChanged -= OnSessionPropertyChanged;
                }
            }
            
            // 如果被移除的是当前选中的 session，选择第一个可用的 session
            if (e.OldItems.Contains(SelectedSession))
            {
                SelectedSession = Sessions.FirstOrDefault();
            }
        }
        
        // 处理替换操作（Replace）
        if (e.Action == NotifyCollectionChangedAction.Replace)
        {
            // 取消订阅被替换的 session
            if (e.OldItems != null)
            {
                foreach (var item in e.OldItems)
                {
                    if (item is BacktestSessionViewModel oldSession)
                    {
                        oldSession.PropertyChanged -= OnSessionPropertyChanged;
                    }
                }
            }
            
            // 订阅新替换的 session
            if (e.NewItems != null)
            {
                foreach (var item in e.NewItems)
                {
                    if (item is BacktestSessionViewModel newSession)
                    {
                        newSession.PropertyChanged += OnSessionPropertyChanged;
                    }
                }
            }
        }
        
        // 处理重置操作（Reset）- 取消所有订阅
        if (e.Action == NotifyCollectionChangedAction.Reset)
        {
            // 注意：Reset 操作时，OldItems 可能为 null，所以我们需要取消所有现有 session 的订阅
            // 但由于我们已经清空了集合，实际上不需要做任何操作
        }
    }
    
    /// <summary>
    /// 当 session 的属性变化时触发，特别是 IsRunning 属性
    /// </summary>
    private void OnSessionPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(BacktestSessionViewModel.IsRunning))
        {
            // 当 IsRunning 变化时，更新 RunningSessions 和 HasRunningSessions
            OnPropertyChanged(nameof(HasRunningSessions));
            OnPropertyChanged(nameof(RunningSessions));
        }
    }

    private void OnHistoryCollectionChanged(object? sender, NotifyCollectionChangedEventArgs? e)
    {
        OnPropertyChanged(nameof(HasHistory));
        OnPropertyChanged(nameof(HasNoHistory));
        
        // 历史记录变化时，重新应用筛选
        if (e != null)
        {
            UpdateFilterOptions();
            ApplyFiltersAndSort();
        }
    }

    /// <summary>
    /// 加载策略列表
    /// </summary>
    private async Task LoadStrategiesAsync()
    {
        try
        {
            StatusMessage = "加载策略列表...";
            var strategies = await _strategyService.GetMyStrategiesAsync();

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                Strategies.Clear();
                foreach (var strategy in strategies)
                {
                    Strategies.Add(strategy);
                }

                StatusMessage = $"已加载 {strategies.Count} 个策略";
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ [BacktestViewModel] 加载策略失败: {ex.Message}");
            Console.WriteLine($"   堆栈跟踪: {ex.StackTrace}");
            StatusMessage = $"加载策略失败: {ex.Message}";
        }
    }

    /// <summary>
    /// 加载策略版本列表
    /// </summary>
    private async Task LoadStrategyVersionsAsync()
    {
        if (SelectedStrategy == null) return;

        try
        {
            var versions = await _strategyService.GetStrategyVersionsAsync(SelectedStrategy.Id);

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                StrategyVersions.Clear();
                foreach (var version in versions)
                {
                    StrategyVersions.Add(version);
                }

                // 优先选择active版本，如果没有则选择最新版本（第一个）
                SelectedVersion = StrategyVersions.FirstOrDefault(v => v.Status == "active") 
                               ?? StrategyVersions.FirstOrDefault();
                
                // 自动选择版本（静默处理）
            });
        }
        catch (Exception ex)
        {
            StatusMessage = $"加载版本失败: {ex.Message}";
        }
    }

    /// <summary>
    /// 开始回测
    /// </summary>
    public async Task StartBacktestAsync(BacktestConfigRequest? request = null)
    {
        var strategy = request?.Strategy ?? SelectedStrategy;
        var versionListItem = request?.Version ?? SelectedVersion;
        if (strategy == null || versionListItem == null)
        {
            StatusMessage = "请选择策略和版本";
            return;
        }

        try
        {
            StatusMessage = "加载策略DSL...";
            var version = await _strategyService.GetStrategyVersionAsync(strategy.Id, versionListItem.VersionString);

            if (version == null || string.IsNullOrEmpty(version.Dsl))
            {
                var errorMsg = "加载策略DSL失败：版本数据为空";
                Console.WriteLine($"❌ {errorMsg}");
                StatusMessage = errorMsg;
                return;
            }

            var config = request?.Config ?? CreateBacktestConfig(strategy);

            if (!string.IsNullOrWhiteSpace(version.Parameters))
            {
                try
                {
                    var paramsDict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(version.Parameters);
                    if (paramsDict != null)
                    {
                        config.Parameters = paramsDict;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"⚠️ 参数解析失败: {ex.Message}，将使用默认参数");
                }
            }

            // 统一由工厂创建（依赖注入）
            var session = _sessionFactory.Create(strategy, versionListItem, config);
            session.AppendLog("回测任务已创建");
            session.RegisterCancelAction(() => CancelSession(session.SessionId));
            session.RegisterPauseResumeAction(() => PauseResumeSession(session.SessionId));
            Sessions.Insert(0, session);
            SelectedSession = session;

            StatusMessage = "回测任务已加入队列";

            _ = RunBacktestSessionAsync(session, version);
        }
        catch (Exception ex)
        {
            StatusMessage = $"回测初始化失败: {ex.Message}";
            Console.WriteLine($"❌ 回测初始化失败: {ex.Message}");
            Console.WriteLine($"   堆栈跟踪: {ex.StackTrace}");
        }
    }

    private BacktestConfig CreateBacktestConfig(StrategyInfo strategy)
    {
        // 🔧 v4.0: 正确处理日期，不进行时区转换
        // 用户选择的日期应该被当作UTC日期处理，不进行时区转换
        // 开始日期：使用日期的 00:00:00 UTC
        // 注意：StartDate 是 DateTimeOffset，使用 UtcDateTime 获取UTC日期部分
        var startDateUtc = StartDate.UtcDateTime;
        var utcStartDate = DateTime.SpecifyKind(
            new DateTime(startDateUtc.Year, startDateUtc.Month, startDateUtc.Day, 0, 0, 0),
            DateTimeKind.Utc
        );
        
        // 结束日期：使用日期的 23:59:59 UTC（当天的最后一秒）
        // 注意：EndDate 属性返回 DateTimeOffset，但内部存储的是 DateTime
        // 需要从 _endDate 直接获取日期部分，并假设它是UTC日期
        var endDateUtc = _endDate.Kind == DateTimeKind.Utc 
            ? _endDate 
            : DateTime.SpecifyKind(_endDate, DateTimeKind.Utc);
        var utcEndDate = DateTime.SpecifyKind(
            new DateTime(endDateUtc.Year, endDateUtc.Month, endDateUtc.Day, 23, 59, 59),
            DateTimeKind.Utc
        );
        
        var config = new BacktestConfig
        {
            // 策略不绑定标的；回测标的由回测配置决定
            Symbol = Symbol,
            Interval = "1m",
            StartDate = utcStartDate,
            EndDate = utcEndDate,
            InitialCapital = InitialCapital,
            TakerFeeRate = FeeRate,
            SlippageRate = SlippageRate,
            SignalSamplingInterval = SignalSamplingInterval?.Value ?? "5m",
            Parameters = new Dictionary<string, object>()
        };

        return config;
    }

    private void CancelSession(Guid sessionId)
    {
        if (_sessionTokens.TryGetValue(sessionId, out var cts))
        {
            cts.Cancel();
        }
    }

    private void PauseResumeSession(Guid sessionId)
    {
        var session = Sessions.FirstOrDefault(s => s.SessionId == sessionId);
        if (session == null) return;

        // 切换暂停状态
        session.IsPaused = !session.IsPaused;
        
        if (session.IsPaused)
        {
            session.Status = "已暂停";
            session.AppendLog("回测已暂停", "WARN");
            // TODO: 实现实际的暂停逻辑（需要回测引擎支持）
        }
        else
        {
            session.Status = "回测中...";
            session.AppendLog("回测已继续", "INFO");
            // TODO: 实现实际的继续逻辑（需要回测引擎支持）
        }
    }

    private async Task RunBacktestSessionAsync(BacktestSessionViewModel session, StrategyVersion version)
    {
        var cts = new CancellationTokenSource();
        if (!_sessionTokens.TryAdd(session.SessionId, cts))
        {
            session.AppendLog("取消令牌创建失败", "ERROR");
            return;
        }

        var config = session.Config;

        try
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                // 清空内存中的临时数据，确保只显示本次回测的数据
                // 注意：这些数据是回测过程中的临时数据，不会从数据库读取
                session.Signals.Clear();
                session.Orders.Clear();
                // EquityCurve 是 List，需要通过属性访问
                var equityCurve = session.EquityCurve;
                if (equityCurve != null)
                {
                    equityCurve.Clear();
                }
                
                session.IsRunning = true;
                session.Status = "初始化回测引擎...";
                session.ProgressPercentage = 0;
                session.ProgressText = "准备中...";
            });

            // 初始化引擎组件
            var dataFeed = new LocalDataFeed();
            var orderManager = new SimulatedOrderManager(config);
            var performanceAnalyzer = new PerformanceAnalyzer();
            var signalGenerator = new DslStrategySignalGenerator();
            // 注意：不传storage参数，避免BacktestEngine内部重复保存
            // 由BacktestViewModel统一管理保存逻辑
            var engine = new BacktestEngine(dataFeed, orderManager, performanceAnalyzer, signalGenerator);
            
            // 重构：关联PerformanceAnalyzer，让ViewModel从单一数据源同步订单
            session.AttachPerformanceAnalyzer(performanceAnalyzer);

            engine.ProgressChanged += (_, e) =>
            {
                Dispatcher.UIThread.Post(() =>
                {
                    session.ProgressPercentage = e.ProgressPercentage * 100;
                    session.ProgressText = e.FormatProgress();
                    session.CurrentEquity = e.CurrentEquity;
                    session.CurrentTime = e.CurrentTime;
                    session.CurrentPrice = e.CurrentPrice;
                });
            };

            engine.SignalGenerated += (_, e) =>
            {
                session.AppendLog($"信号 {e.Signal.Action} @ {e.Signal.SignalPrice:F2}", "SIGNAL");
                
                // 记录信号到ViewModel（包含完整的 v11.0 信息）
                session.RecordSignal(new SignalEvent
                {
                    Time = e.Signal.Time,
                    Action = e.Signal.Action,
                    SignalPrice = e.Signal.SignalPrice,
                    WasExecuted = false, // 默认未成交，后续根据订单执行情况更新
                    Confidence = e.Signal.Strength,
                    Strength = e.Signal.Strength,
                    Description = e.Signal.Description,
                    TakeProfit = e.Signal.TakeProfit,
                    StopLoss = e.Signal.StopLoss,
                    Trend = e.Signal.Trend,
                    ConfigsJson = e.Signal.ConfigsJson,
                    IndicatorsJson = e.Signal.IndicatorsJson,
                    IndicatorSnapshotsJson = e.Signal.IndicatorSnapshotsJson,  // 修复：添加指标快照JSON
                    DebugJson = e.Signal.DebugJson,
                    KlinesJson = e.Signal.KlinesJson,  // 🆕 v4.0: K线数据快照
                    CandleIndex = e.CandleIndex,
                    GlobalIndex = e.GlobalIndex
                });
            };

            // 重构：OrderExecuted事件现在只用于日志，订单数据通过PerformanceAnalyzer同步
            engine.OrderExecuted += (_, e) =>
            {
                // 空值检查：防止空订单导致崩溃
                if (e?.Order == null)
                {
                    Console.WriteLine("⚠️ OrderExecuted事件收到空订单，跳过日志");
                    return;
                }

                // 只记录日志，订单数据已通过PerformanceAnalyzer同步到ViewModel
                session.AppendLog($"订单 {e.Order.Side} 数量 {e.Order.Quantity:F4}，状态 {e.Order.Status}", "ORDER");
            };

            session.AppendLog("🏃 开始执行回测");
            StatusMessage = $"正在运行 {session.SessionTitle}";

            var progressReporter = new Progress<double>(percent =>
            {
                Dispatcher.UIThread.Post(() =>
                {
                    session.ProgressPercentage = percent;
                    session.ProgressText = $"回测中... {percent:F1}%";
                });
            });

            var result = await Task.Run(async () =>
                await engine.RunAsync(version.Dsl, config, progressReporter, cts.Token), cts.Token);

            if (result.Status == BacktestStatus.FAILED)
            {
                throw new Exception(result.ErrorMessage ?? "回测执行失败");
            }

            // ✅ 绑定版本信息到回测结果（用于追溯/AI分析/回归）
            result.VersionId = (int)version.Id;
            result.VersionString = version.VersionString;
            result.Config ??= config;

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                session.Result = result;
                session.IsRunning = false; // 这会触发PropertyChanged，更新HasRunningSessions
                session.Status = "回测完成";
                session.ProgressPercentage = 100;
                session.ProgressText = "已完成";
                session.CurrentEquity = result.FinalEquity;
                
                // 重构：所有统计信息现在都基于Orders集合实时计算
                // 订单已经通过事件同步到ViewModel，统计信息会自动更新
                // 只需要更新平均持仓时长（从BacktestResult获取）
                session.AvgHoldingTime = result.AvgHoldingTime;
                
                // 触发所有统计属性的重新计算
                session.RefreshStatistics();
                
                // 更新HasRunningSessions状态
                OnPropertyChanged(nameof(HasRunningSessions));
                OnPropertyChanged(nameof(RunningSessions));
            });

            session.AppendLog("✅ 回测执行成功", "SUCCESS");

            await SaveBacktestResultAsync(session, result, version);
        }
        catch (OperationCanceledException)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                session.Status = "已取消";
                session.IsRunning = false;
                session.ProgressText = "已取消";
            });
            session.AppendLog("回测已取消", "WARN");
        }
        catch (Exception ex)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                session.Status = "回测失败";
                session.IsRunning = false;
                session.ProgressText = "失败";
            });

            session.AppendLog($"回测失败: {ex.Message}", "ERROR");

            Console.WriteLine();
            Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            Console.WriteLine($"❌ 回测流程失败");
            Console.WriteLine($"   错误类型: {ex.GetType().Name}");
            Console.WriteLine($"   错误信息: {ex.Message}");
            Console.WriteLine($"   堆栈跟踪:");
            Console.WriteLine($"{ex.StackTrace}");
            Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        }
        finally
        {
            if (_sessionTokens.TryRemove(session.SessionId, out var token))
            {
                token.Dispose();
            }
        }
    }

    /// <summary>
    /// 加载回测历史
    /// </summary>
    private async Task LoadBacktestHistoryAsync()
    {
        try
        {
            using var storage = new LocalBacktestStorage();
            var records = await storage.GetRecentBacktestsAsync(100);

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                // 先取消事件订阅，避免频繁触发筛选
                BacktestHistory.CollectionChanged -= OnHistoryCollectionChanged;
                
                BacktestHistory.Clear();
                foreach (var record in records)
                {
                    BacktestHistory.Add(record);
                }
                
                // 重新订阅事件
                BacktestHistory.CollectionChanged += OnHistoryCollectionChanged;

                // 更新筛选器选项
                UpdateFilterOptions();
                
                // 应用筛选和排序（只执行一次）
                ApplyFiltersAndSort();
                
                // 强制触发UI更新
                OnPropertyChanged(nameof(FilteredBacktestHistory));
                OnPropertyChanged(nameof(HasFilteredHistory));
                OnPropertyChanged(nameof(HasNoFilteredHistory));
            });

            StatusMessage = $"已加载 {records.Count} 条回测记录";
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ [BacktestViewModel] 加载历史记录失败: {ex.Message}");
            Console.WriteLine($"   堆栈跟踪: {ex.StackTrace}");
            StatusMessage = $"加载历史失败: {ex.Message}";
        }
    }

    /// <summary>
    /// 更新筛选器选项
    /// </summary>
    private void UpdateFilterOptions()
    {
        // 更新策略筛选列表
        FilterStrategies.Clear();
        
        // 添加"全部策略"选项
        FilterStrategies.Add(new StrategyInfo 
        { 
            Id = string.Empty, 
            Name = "全部策略"
        });

        // 从历史记录中提取唯一的策略
        var uniqueStrategies = BacktestHistory
            .Where(r => !string.IsNullOrEmpty(r.StrategyId))
            .GroupBy(r => r.StrategyId)
            .Select(g => new StrategyInfo
            {
                Id = g.Key,
                Name = g.First().StrategyName
            })
            .OrderBy(s => s.Name);

        foreach (var strategy in uniqueStrategies)
        {
            FilterStrategies.Add(strategy);
        }

        // 默认选中"全部策略"
        if (SelectedFilterStrategy == null && FilterStrategies.Count > 0)
        {
            SelectedFilterStrategy = FilterStrategies[0];
        }
    }

    /// <summary>
    /// 应用筛选和排序
    /// </summary>
    private void ApplyFiltersAndSort()
    {
        var filtered = BacktestHistory.AsEnumerable();

        // 策略筛选
        if (SelectedFilterStrategy != null && !string.IsNullOrEmpty(SelectedFilterStrategy.Id))
        {
            var strategyId = SelectedFilterStrategy.Id;
            filtered = filtered.Where(r => r.StrategyId == strategyId);
        }

        // 状态筛选
        if (SelectedStatusFilter != "全部状态")
        {
            filtered = SelectedStatusFilter switch
            {
                "已完成" => filtered.Where(r => r.Status == BacktestStatus.COMPLETED),
                "进行中" => filtered.Where(r => r.Status == BacktestStatus.RUNNING),
                "失败" => filtered.Where(r => r.Status == BacktestStatus.FAILED),
                _ => filtered
            };
        }

        // 排序（排除默认值DateTime.MinValue）
        filtered = SelectedSortOption switch
        {
            "时间（最新）" => filtered.OrderByDescending(r => r.CompletedAt == DateTime.MinValue ? DateTime.MinValue : r.CompletedAt),
            "时间（最旧）" => filtered.OrderBy(r => r.CompletedAt == DateTime.MinValue ? DateTime.MaxValue : r.CompletedAt),
            "收益率（高到低）" => filtered.OrderByDescending(r => r.TotalReturn),
            "收益率（低到高）" => filtered.OrderBy(r => r.TotalReturn),
            "胜率（高到低）" => filtered.OrderByDescending(r => r.WinRate),
            "回撤（小到大）" => filtered.OrderBy(r => Math.Abs(r.MaxDrawdown)),
            "夏普比率（高到低）" => filtered.OrderByDescending(r => r.SharpeRatio),
            _ => filtered.OrderByDescending(r => r.CompletedAt == DateTime.MinValue ? DateTime.MinValue : r.CompletedAt)
        };

        // 更新筛选后的集合
        var results = filtered.ToList();
        
        // 批量更新，避免频繁触发UI更新
        FilteredBacktestHistory.Clear();
        foreach (var result in results)
        {
            FilteredBacktestHistory.Add(result);
        }
        
        // 触发UI更新通知（确保UI刷新）
        OnPropertyChanged(nameof(FilteredBacktestHistory));
        OnPropertyChanged(nameof(HasFilteredHistory));
        OnPropertyChanged(nameof(HasNoFilteredHistory));
    }
    
    /// <summary>
    /// 保存回测结果 (🔥 P0-5 新增)
    /// </summary>
    private async Task SaveBacktestResultAsync(BacktestSessionViewModel session, BacktestResult result, StrategyVersion version)
    {
        try
        {
            // 1. 保存到本地SQLite
            using var localStorage = new LocalBacktestStorage();
            
            // 填充version_id和version_string
            result.BacktestId = result.BacktestId ?? $"bt_{DateTime.Now:yyyyMMdd_HHmmss}";
            result.StrategyId = session.StrategyId;
            result.StrategyName = session.StrategyName;
            result.VersionId = (int)version.Id;
            result.VersionString = version.VersionString;
            
            await localStorage.SaveBacktestResultAsync(result);
            
            await Dispatcher.UIThread.InvokeAsync(() => UpsertHistoryResult(result));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ 保存回测结果失败: {ex.Message}");
            Console.WriteLine($"   堆栈跟踪: {ex.StackTrace}");
            // 保存失败不影响回测结果展示，只记录错误
        }
    }
    
    private void UpsertHistoryResult(BacktestResult result)
    {
        if (string.IsNullOrEmpty(result.BacktestId))
        {
            return;
        }

        var existing = BacktestHistory.FirstOrDefault(r => r.BacktestId == result.BacktestId);
        if (existing != null)
        {
            BacktestHistory.Remove(existing);
        }

        BacktestHistory.Insert(0, result);
        
        // 更新筛选器选项（可能新增了策略）
        UpdateFilterOptions();
        
        // 重新应用筛选和排序，更新FilteredBacktestHistory（UI实际显示的列表）
        ApplyFiltersAndSort();
    }

    #endregion

    #region INotifyPropertyChanged

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        
        // 当影响命令状态的属性变化时，更新命令状态
        if (propertyName == nameof(CanStartBacktest))
        {
            _startBacktestCommandImpl?.RaiseCanExecuteChanged();
        }
    }


    #endregion
}

/// <summary>
/// 信号采样频率选项
/// </summary>
public class SignalIntervalOption
{
    public string Value { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
}
