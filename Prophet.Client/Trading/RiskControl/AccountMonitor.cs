using System;
using System.Threading;
using System.Threading.Tasks;
using Prophet.Client.Trading.Exchanges;

namespace Prophet.Client.Trading.RiskControl;

/// <summary>
/// 账户监控器
/// 实时监控账户状态，检测异常情况
/// </summary>
public class AccountMonitor : IDisposable
{
    private readonly IExchange _exchange;
    private readonly RiskConfig _config;
    private Timer? _monitorTimer;
    private AccountStatus? _lastStatus;
    private bool _disposed;
    
    public event EventHandler<AccountAnomalyEventArgs>? AnomalyDetected;
    public event EventHandler<BalanceChangedEventArgs>? BalanceChanged;
    
    public AccountMonitor(IExchange exchange, RiskConfig config)
    {
        _exchange = exchange ?? throw new ArgumentNullException(nameof(exchange));
        _config = config ?? throw new ArgumentNullException(nameof(config));
    }
    
    /// <summary>
    /// 启动监控
    /// </summary>
    public void StartMonitoring()
    {
        if (!_config.EnableRealtimeMonitoring)
        {
            return;
        }
        
        StopMonitoring();
        
        _monitorTimer = new Timer(
            async _ => await MonitorCallback(),
            null,
            TimeSpan.Zero,
            TimeSpan.FromSeconds(_config.MonitoringIntervalSeconds));
        
        Console.WriteLine($"✅ [AccountMonitor] 账户监控已启动，间隔: {_config.MonitoringIntervalSeconds}秒");
    }
    
    /// <summary>
    /// 停止监控
    /// </summary>
    public void StopMonitoring()
    {
        _monitorTimer?.Dispose();
        _monitorTimer = null;
        Console.WriteLine($"⏹️ [AccountMonitor] 账户监控已停止");
    }
    
    /// <summary>
    /// 监控回调
    /// </summary>
    private async Task MonitorCallback()
    {
        try
        {
            var currentStatus = await GetAccountStatusAsync();
            
            // 检查异常
            if (_lastStatus != null)
            {
                await CheckForAnomaliesAsync(_lastStatus, currentStatus);
            }
            
            _lastStatus = currentStatus;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ [AccountMonitor] 监控异常: {ex.Message}");
        }
    }
    
    /// <summary>
    /// 获取账户状态
    /// </summary>
    public async Task<AccountStatus> GetAccountStatusAsync()
    {
        var accountInfo = await _exchange.GetAccountInfoAsync();
        
        return new AccountStatus
        {
            Timestamp = DateTime.UtcNow,
            TotalBalance = accountInfo.TotalBalance,
            AvailableBalance = accountInfo.AvailableBalance,
            TotalMargin = accountInfo.TotalMargin,
            UnrealizedPnl = accountInfo.TotalUnrealizedPnl,
            RealizedPnl = accountInfo.TotalRealizedPnl
        };
    }
    
    /// <summary>
    /// 检查异常情况
    /// </summary>
    private Task CheckForAnomaliesAsync(AccountStatus last, AccountStatus current)
    {
        // 1. 检查余额突然大幅下降
        var balanceChange = current.TotalBalance - last.TotalBalance;
        var balanceChangePercent = balanceChange / last.TotalBalance;
        
        if (balanceChangePercent < -0.10m) // 下降超过10%
        {
            RaiseAnomaly("余额大幅下降", 
                $"余额下降 {balanceChangePercent:P2}: {last.TotalBalance:F2} → {current.TotalBalance:F2} USDT",
                AnomalySeverity.High);
        }
        
        // 2. 检查可用余额异常
        if (current.AvailableBalance < _config.MinBalance)
        {
            RaiseAnomaly("可用余额不足",
                $"可用余额 {current.AvailableBalance:F2} USDT 低于最小限制 {_config.MinBalance:F2} USDT",
                AnomalySeverity.Critical);
        }
        
        // 3. 检查保证金占比过高
        var marginPercent = current.TotalMargin / current.TotalBalance;
        if (marginPercent > 0.90m) // 超过90%
        {
            RaiseAnomaly("保证金占比过高",
                $"保证金占比 {marginPercent:P2}，接近强平风险",
                AnomalySeverity.High);
        }
        
        // 4. 触发余额变化事件
        if (Math.Abs(balanceChange) > 0.01m)
        {
            BalanceChanged?.Invoke(this, new BalanceChangedEventArgs
            {
                OldBalance = last.TotalBalance,
                NewBalance = current.TotalBalance,
                Change = balanceChange,
                ChangePercent = balanceChangePercent,
                Timestamp = DateTime.UtcNow
            });
        }

        return Task.CompletedTask;
    }
    
    private void RaiseAnomaly(string type, string description, AnomalySeverity severity)
    {
        Console.WriteLine($"⚠️ [AccountMonitor] 检测到异常: {type} - {description}");
        
        AnomalyDetected?.Invoke(this, new AccountAnomalyEventArgs
        {
            Type = type,
            Description = description,
            Severity = severity,
            Timestamp = DateTime.UtcNow
        });
    }
    
    public void Dispose()
    {
        if (!_disposed)
        {
            StopMonitoring();
            _disposed = true;
        }
    }
}

/// <summary>
/// 账户状态
/// </summary>
public class AccountStatus
{
    public DateTime Timestamp { get; set; }
    public decimal TotalBalance { get; set; }
    public decimal AvailableBalance { get; set; }
    public decimal TotalMargin { get; set; }
    public decimal UnrealizedPnl { get; set; }
    public decimal RealizedPnl { get; set; }
}

/// <summary>
/// 异常严重程度
/// </summary>
public enum AnomalySeverity
{
    Low,
    Medium,
    High,
    Critical
}

// 事件参数
public class AccountAnomalyEventArgs : EventArgs
{
    public string Type { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public AnomalySeverity Severity { get; set; }
    public DateTime Timestamp { get; set; }
}

public class BalanceChangedEventArgs : EventArgs
{
    public decimal OldBalance { get; set; }
    public decimal NewBalance { get; set; }
    public decimal Change { get; set; }
    public decimal ChangePercent { get; set; }
    public DateTime Timestamp { get; set; }
}

