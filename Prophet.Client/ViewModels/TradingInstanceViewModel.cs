using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Prophet.Client.Models;
using Prophet.Client.Trading.Models;
using Prophet.Client.Trading.RiskControl;

namespace Prophet.Client.ViewModels;

/// <summary>
/// 单个实盘交易实例的ViewModel
/// </summary>
public partial class TradingInstanceViewModel : ObservableObject
{
    [ObservableProperty]
    private TradingInstanceConfig _config = new();
    
    [ObservableProperty]
    private TradingInstanceStatus _status = TradingInstanceStatus.Stopped;
    
    [ObservableProperty]
    private TradingInstanceSnapshot? _snapshot;
    
    [ObservableProperty]
    private string _statusText = "已停止";
    
    [ObservableProperty]
    private string _statusColor = "#808080";
    
    [ObservableProperty]
    private bool _canStart = true;
    
    [ObservableProperty]
    private bool _canStop = false;
    
    [ObservableProperty]
    private bool _canPause = false;
    
    [ObservableProperty]
    private bool _canResume = false;
    
    // 实时数据
    [ObservableProperty]
    private decimal _totalEquity;
    
    [ObservableProperty]
    private decimal _todayPnL;
    
    [ObservableProperty]
    private decimal _todayPnLPercent;
    
    [ObservableProperty]
    private int _positionCount;
    
    [ObservableProperty]
    private decimal _currentDrawdown;
    
    [ObservableProperty]
    private string _riskLevelText = "低";
    
    [ObservableProperty]
    private string _riskLevelColor = "#00FF00";
    
    // Additional properties for UI binding
    public string InstanceName => Config?.Name ?? "未命名实例";
    public string ExchangeName => Config?.Exchange ?? "未知";
    public string Symbol => Config?.Symbol ?? "未知";
    public string StrategyName => Config?.StrategyName ?? "未设置策略";
    public decimal PositionSize => Snapshot?.UsedMargin ?? 0m;
    public decimal TotalPnL => Snapshot?.RealizedPnL + Snapshot?.UnrealizedPnL ?? 0m;
    public decimal TotalPnLPercent => Snapshot?.TotalEquity > 0m ? (TotalPnL / Snapshot.TotalEquity * 100m) : 0m;
    public string RunningDuration => Snapshot != null ? TimeSpan.FromSeconds(Snapshot.RunningDurationSeconds).ToString(@"hh\:mm\:ss") : "00:00:00";
    public decimal MaxPosition => Config?.RiskConfig?.MaxOpenPositions ?? 0m;
    public int TodayTradesCount => Snapshot?.TodayTrades ?? 0;
    public int MaxDailyTrades => Config?.RiskConfig?.MaxDailyTrades ?? 0;
    public decimal MaxDrawdown => Snapshot?.MaxDrawdown ?? 0m;
    
    // Status computed properties
    public bool IsRunning => Status == TradingInstanceStatus.Running;
    public bool IsStopped => Status == TradingInstanceStatus.Stopped;
    public bool IsPaused => Status == TradingInstanceStatus.Paused;
    public bool IsError => Status == TradingInstanceStatus.Error;
    
    // 持仓、订单、日志列表
    public ObservableCollection<object> Positions { get; } = new();
    public ObservableCollection<object> Orders { get; set; } = new();
    public ObservableCollection<string> Logs { get; } = new();
    
    // 命令
    public ICommand? StartCommand { get; set; }
    public ICommand? StopCommand { get; set; }
    public ICommand? PauseCommand { get; set; }
    public ICommand? ResumeCommand { get; set; }
    public ICommand? EditCommand { get; set; }
    public ICommand? DeleteCommand { get; set; }
    
    public TradingInstanceViewModel()
    {
    }
    
    public TradingInstanceViewModel(TradingInstanceConfig config)
    {
        _config = config;
        UpdateStatus(TradingInstanceStatus.Stopped);
    }
    
    partial void OnConfigChanged(TradingInstanceConfig value)
    {
        // Notify config-related computed properties
        OnPropertyChanged(nameof(InstanceName));
        OnPropertyChanged(nameof(ExchangeName));
        OnPropertyChanged(nameof(Symbol));
        OnPropertyChanged(nameof(StrategyName));
        OnPropertyChanged(nameof(MaxPosition));
        OnPropertyChanged(nameof(MaxDailyTrades));
    }
    
    /// <summary>
    /// 更新状态
    /// </summary>
    public void UpdateStatus(TradingInstanceStatus newStatus)
    {
        Status = newStatus;
        StatusText = newStatus.GetDisplayText();
        StatusColor = newStatus.GetStatusColor();
        
        CanStart = newStatus.CanStart();
        CanStop = newStatus.CanStop();
        CanPause = newStatus.CanPause();
        CanResume = newStatus.CanResume();
        
        // Notify status-related computed properties
        OnPropertyChanged(nameof(IsRunning));
        OnPropertyChanged(nameof(IsStopped));
        OnPropertyChanged(nameof(IsPaused));
        OnPropertyChanged(nameof(IsError));
    }
    
    /// <summary>
    /// 更新快照
    /// </summary>
    public void UpdateSnapshot(TradingInstanceSnapshot snapshot)
    {
        Snapshot = snapshot;
        
        TotalEquity = snapshot.TotalEquity;
        TodayPnL = snapshot.TodayPnL;
        TodayPnLPercent = snapshot.TodayPnLPercent;
        PositionCount = snapshot.PositionCount;
        CurrentDrawdown = snapshot.CurrentDrawdown;
        
        RiskLevelText = snapshot.RiskLevel.ToString();
        RiskLevelColor = snapshot.RiskLevel switch
        {
            RiskLevel.Low => "#00FF00",
            RiskLevel.Medium => "#FFFF00",
            RiskLevel.High => "#FFA500",
            RiskLevel.Critical => "#FF0000",
            _ => "#808080"
        };
        
        UpdateStatus(snapshot.Status);
        
        // Notify snapshot-related computed properties
        OnPropertyChanged(nameof(PositionSize));
        OnPropertyChanged(nameof(TotalPnL));
        OnPropertyChanged(nameof(TotalPnLPercent));
        OnPropertyChanged(nameof(RunningDuration));
        OnPropertyChanged(nameof(TodayTradesCount));
        OnPropertyChanged(nameof(MaxDrawdown));
    }
    
    /// <summary>
    /// 添加日志
    /// </summary>
    public void AddLog(string message)
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss");
        Logs.Insert(0, $"[{timestamp}] {message}");
        
        // 限制日志数量
        while (Logs.Count > 1000)
        {
            Logs.RemoveAt(Logs.Count - 1);
        }
    }
}

