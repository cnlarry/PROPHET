using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Prophet.Client.Trading.Engine;
using Prophet.Client.Trading.Models;
using Prophet.Client.Trading.Statistics;
using Prophet.Client.Trading.Storage;

namespace Prophet.Client.ViewModels;

/// <summary>
/// 实盘交易主界面ViewModel
/// </summary>
public partial class LiveTradingViewModel : ObservableObject, IDisposable
{
    private readonly TradingInstanceManager _instanceManager;
    private readonly CrossInstanceStatistics _statistics;
    private DispatcherTimer? _updateTimer;
    private bool _disposed;
    
    [ObservableProperty]
    private ObservableCollection<TradingInstanceViewModel> _instances = new();
    
    // Alias for Instances to match XAML binding
    public ObservableCollection<TradingInstanceViewModel> TradingInstances => Instances;
    
    [ObservableProperty]
    private TradingInstanceViewModel? _selectedInstance;
    
    [ObservableProperty]
    private int _selectedTabIndex;
    
    [ObservableProperty]
    private bool _isAnyRunning;
    
    [ObservableProperty]
    private bool _hasInstances;
    
    // Computed properties for UI
    public bool IsEmpty => !HasInstances;
    public int RunningInstancesCount => RunningInstances;
    public int TotalInstancesCount => TotalInstances;
    public bool HasRunningInstances => IsAnyRunning;
    public bool HasStoppedInstances => TotalInstances > RunningInstances;
    
    // 全局统计
    [ObservableProperty]
    private int _totalInstances;
    
    [ObservableProperty]
    private int _runningInstances;
    
    [ObservableProperty]
    private decimal _globalEquity;
    
    [ObservableProperty]
    private decimal _globalTodayPnL;
    
    [ObservableProperty]
    private string _globalStatusText = "未运行";
    
    // 命令
    public ICommand CreateInstanceCommand { get; }
    public ICommand StartAllCommand { get; }
    public ICommand StopAllCommand { get; }
    public ICommand ShowGlobalStatsCommand { get; }
    public ICommand RefreshCommand { get; }
    
    public LiveTradingViewModel()
    {
        _instanceManager = new TradingInstanceManager(new TradingInstanceStorage());
        _statistics = new CrossInstanceStatistics(_instanceManager);
        
        CreateInstanceCommand = new RelayCommand(CreateInstance);
        StartAllCommand = new AsyncRelayCommand(StartAllAsync, () => HasInstances && !IsAnyRunning);
        StopAllCommand = new AsyncRelayCommand(StopAllAsync, () => IsAnyRunning);
        ShowGlobalStatsCommand = new RelayCommand(ShowGlobalStats);
        RefreshCommand = new AsyncRelayCommand(RefreshAsync);
        
        _ = InitializeAsync();
    }
    
    /// <summary>
    /// 初始化
    /// </summary>
    private async Task InitializeAsync()
    {
        try
        {
            // 初始化实例管理器
            await _instanceManager.InitializeAsync();
            
            // 订阅事件
            _instanceManager.InstanceCreated += OnInstanceCreated;
            _instanceManager.InstanceDeleted += OnInstanceDeleted;
            _instanceManager.InstanceStatusChanged += OnInstanceStatusChanged;
            
            // 加载实例
            await LoadInstancesAsync();
            
            // 启动定时更新
            StartUpdateTimer();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[LiveTradingViewModel] 初始化失败: {ex.Message}");
        }
    }
    
    /// <summary>
    /// 加载所有实例
    /// </summary>
    private async Task LoadInstancesAsync()
    {
        var configs = _instanceManager.GetAllInstances();
        
        Instances.Clear();
        
        foreach (var config in configs)
        {
            var vm = new TradingInstanceViewModel(config);
            
            // 设置命令
            vm.StartCommand = new AsyncRelayCommand(async () => await StartInstanceAsync(config.Id));
            vm.StopCommand = new AsyncRelayCommand(async () => await StopInstanceAsync(config.Id));
            vm.PauseCommand = new AsyncRelayCommand(async () => await PauseInstanceAsync(config.Id));
            vm.ResumeCommand = new AsyncRelayCommand(async () => await ResumeInstanceAsync(config.Id));
            vm.DeleteCommand = new AsyncRelayCommand(async () => await DeleteInstanceAsync(config.Id));
            
            Instances.Add(vm);
        }
        
        HasInstances = Instances.Any();
        TotalInstances = Instances.Count;
        
        if (Instances.Any() && SelectedInstance == null)
        {
            SelectedInstance = Instances.First();
        }
        
        await UpdateGlobalStatsAsync();
    }
    
    /// <summary>
    /// 创建新实例
    /// </summary>
    private async void CreateInstance()
    {
        try
        {
            var dialog = new Views.Dialogs.CreateTradingInstanceDialog();
            
            // 获取主窗口作为父窗口
            var mainWindow = Avalonia.Application.Current?.ApplicationLifetime is
                Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
                ? desktop.MainWindow
                : null;
            
            if (mainWindow == null)
            {
                Console.WriteLine("[LiveTradingViewModel] 无法获取主窗口");
                return;
            }
            
            var result = await dialog.ShowDialog<Trading.Models.TradingInstanceConfig?>(mainWindow);
            
            if (result != null)
            {
                // 创建实例
                var instanceId = await _instanceManager.CreateInstanceAsync(result);
                
                if (instanceId != Guid.Empty)
                {
                    Console.WriteLine($"[LiveTradingViewModel] 实例创建成功: {result.Name} ({instanceId})");
                    await LoadInstancesAsync();
                }
                else
                {
                    Console.WriteLine("[LiveTradingViewModel] 实例创建失败");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[LiveTradingViewModel] 创建实例异常: {ex.Message}");
        }
    }
    
    /// <summary>
    /// 启动指定实例
    /// </summary>
    private async Task StartInstanceAsync(Guid instanceId)
    {
        var success = await _instanceManager.StartInstanceAsync(instanceId);
        
        if (success)
        {
            await UpdateInstanceAsync(instanceId);
            await UpdateGlobalStatsAsync();
        }
    }
    
    /// <summary>
    /// 停止指定实例
    /// </summary>
    private async Task StopInstanceAsync(Guid instanceId)
    {
        var success = await _instanceManager.StopInstanceAsync(instanceId);
        
        if (success)
        {
            await UpdateInstanceAsync(instanceId);
            await UpdateGlobalStatsAsync();
        }
    }
    
    /// <summary>
    /// 暂停指定实例
    /// </summary>
    private async Task PauseInstanceAsync(Guid instanceId)
    {
        await _instanceManager.PauseInstanceAsync(instanceId);
        await UpdateInstanceAsync(instanceId);
    }
    
    /// <summary>
    /// 恢复指定实例
    /// </summary>
    private async Task ResumeInstanceAsync(Guid instanceId)
    {
        await _instanceManager.ResumeInstanceAsync(instanceId);
        await UpdateInstanceAsync(instanceId);
    }
    
    /// <summary>
    /// 删除实例
    /// </summary>
    private async Task DeleteInstanceAsync(Guid instanceId)
    {
        // TODO: 确认对话框
        var success = await _instanceManager.DeleteInstanceAsync(instanceId);
        
        if (success)
        {
            await LoadInstancesAsync();
        }
    }
    
    /// <summary>
    /// 启动所有实例
    /// </summary>
    private async Task StartAllAsync()
    {
        var count = await _instanceManager.StartAllAsync();
        Console.WriteLine($"[LiveTradingViewModel] 已启动 {count} 个实例");
        
        await RefreshAsync();
    }
    
    /// <summary>
    /// 停止所有实例
    /// </summary>
    private async Task StopAllAsync()
    {
        var count = await _instanceManager.StopAllAsync();
        Console.WriteLine($"[LiveTradingViewModel] 已停止 {count} 个实例");
        
        await RefreshAsync();
    }
    
    /// <summary>
    /// 显示全局统计
    /// </summary>
    private void ShowGlobalStats()
    {
        // TODO: 打开全局统计对话框
        Console.WriteLine("[LiveTradingViewModel] 显示全局统计（对话框待实现）");
    }
    
    /// <summary>
    /// 刷新
    /// </summary>
    private async Task RefreshAsync()
    {
        foreach (var instance in Instances)
        {
            await UpdateInstanceAsync(instance.Config.Id);
        }
        
        await UpdateGlobalStatsAsync();
    }
    
    /// <summary>
    /// 更新指定实例
    /// </summary>
    private async Task UpdateInstanceAsync(Guid instanceId)
    {
        var vm = Instances.FirstOrDefault(i => i.Config.Id == instanceId);
        if (vm == null) return;
        
        var snapshot = await _instanceManager.GetInstanceSnapshotAsync(instanceId);
        if (snapshot != null)
        {
            Dispatcher.UIThread.Post(() => vm.UpdateSnapshot(snapshot));
        }
        else
        {
            var status = _instanceManager.GetInstanceStatus(instanceId);
            Dispatcher.UIThread.Post(() => vm.UpdateStatus(status));
        }
    }
    
    /// <summary>
    /// 更新全局统计
    /// </summary>
    private async Task UpdateGlobalStatsAsync()
    {
        try
        {
            var summary = await _statistics.GetGlobalSummaryAsync();
            
            Dispatcher.UIThread.Post(() =>
            {
                TotalInstances = summary.TotalInstances;
                RunningInstances = summary.RunningInstances;
                GlobalEquity = summary.TotalEquity;
                GlobalTodayPnL = summary.TotalTodayPnL;
                IsAnyRunning = summary.RunningInstances > 0;
                
                GlobalStatusText = IsAnyRunning 
                    ? $"运行中 ({RunningInstances}/{TotalInstances})" 
                    : "未运行";
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[LiveTradingViewModel] 更新全局统计失败: {ex.Message}");
        }
    }
    
    /// <summary>
    /// 启动定时更新
    /// </summary>
    private void StartUpdateTimer()
    {
        _updateTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        
        _updateTimer.Tick += async (s, e) =>
        {
            await RefreshAsync();
        };
        
        _updateTimer.Start();
    }
    
    // 事件处理
    private void OnInstanceCreated(object? sender, InstanceEventArgs e)
    {
        Dispatcher.UIThread.Post(async () => await LoadInstancesAsync());
    }
    
    private void OnInstanceDeleted(object? sender, InstanceEventArgs e)
    {
        Dispatcher.UIThread.Post(async () => await LoadInstancesAsync());
    }
    
    private void OnInstanceStatusChanged(object? sender, InstanceStatusChangedEventArgs e)
    {
        Dispatcher.UIThread.Post(async () => await UpdateInstanceAsync(e.InstanceId));
    }
    
    public void Dispose()
    {
        if (!_disposed)
        {
            _updateTimer?.Stop();
            _instanceManager?.Dispose();
            _disposed = true;
        }
    }
}

