using System;
using System.Collections.Generic;
using System.Linq;

namespace Prophet.Client.Trading.RiskControl;

/// <summary>
/// 回撤跟踪器
/// 跟踪账户权益变化，计算回撤指标
/// </summary>
public class DrawdownTracker
{
    private decimal _peakEquity;
    private decimal _currentEquity;
    private decimal _maxDrawdown;
    private DateTime _peakTime;
    private DateTime _troughTime;
    
    private readonly List<EquityPoint> _equityHistory = new();
    private const int MaxHistorySize = 10000;
    
    public decimal PeakEquity => _peakEquity;
    public decimal CurrentEquity => _currentEquity;
    public decimal MaxDrawdown => _maxDrawdown;
    public decimal CurrentDrawdown => CalculateCurrentDrawdown();
    public DateTime PeakTime => _peakTime;
    public DateTime TroughTime => _troughTime;
    
    public DrawdownTracker(decimal initialEquity)
    {
        _peakEquity = initialEquity;
        _currentEquity = initialEquity;
        _maxDrawdown = 0;
        _peakTime = DateTime.UtcNow;
        _troughTime = DateTime.UtcNow;
        
        _equityHistory.Add(new EquityPoint
        {
            Timestamp = DateTime.UtcNow,
            Equity = initialEquity
        });
    }
    
    /// <summary>
    /// 更新权益
    /// </summary>
    public void Update(decimal equity)
    {
        _currentEquity = equity;
        
        // 记录历史
        _equityHistory.Add(new EquityPoint
        {
            Timestamp = DateTime.UtcNow,
            Equity = equity
        });
        
        // 限制历史大小
        if (_equityHistory.Count > MaxHistorySize)
        {
            _equityHistory.RemoveAt(0);
        }
        
        // 更新峰值
        if (equity > _peakEquity)
        {
            _peakEquity = equity;
            _peakTime = DateTime.UtcNow;
        }
        
        // 计算当前回撤
        var currentDrawdown = CalculateCurrentDrawdown();
        
        // 更新最大回撤
        if (currentDrawdown > _maxDrawdown)
        {
            _maxDrawdown = currentDrawdown;
            _troughTime = DateTime.UtcNow;
        }
    }
    
    /// <summary>
    /// 计算当前回撤
    /// </summary>
    private decimal CalculateCurrentDrawdown()
    {
        if (_peakEquity == 0)
        {
            return 0;
        }
        
        var drawdown = (_peakEquity - _currentEquity) / _peakEquity;
        return Math.Max(0, drawdown);
    }
    
    /// <summary>
    /// 获取回撤统计
    /// </summary>
    public DrawdownStatistics GetStatistics()
    {
        var drawdownPeriods = CalculateDrawdownPeriods();
        
        return new DrawdownStatistics
        {
            CurrentEquity = _currentEquity,
            PeakEquity = _peakEquity,
            CurrentDrawdown = CurrentDrawdown,
            MaxDrawdown = _maxDrawdown,
            PeakTime = _peakTime,
            TroughTime = _troughTime,
            DrawdownDuration = DateTime.UtcNow - _peakTime,
            AverageDrawdown = drawdownPeriods.Any() ? drawdownPeriods.Average(p => p.MaxDrawdown) : 0,
            DrawdownCount = drawdownPeriods.Count,
            RecoveryRate = CalculateRecoveryRate()
        };
    }
    
    /// <summary>
    /// 计算回撤期
    /// </summary>
    private List<DrawdownPeriod> CalculateDrawdownPeriods()
    {
        var periods = new List<DrawdownPeriod>();
        
        if (_equityHistory.Count < 2)
        {
            return periods;
        }
        
        DrawdownPeriod? currentPeriod = null;
        decimal localPeak = _equityHistory[0].Equity;
        
        for (int i = 1; i < _equityHistory.Count; i++)
        {
            var point = _equityHistory[i];
            
            if (point.Equity > localPeak)
            {
                // 新高点
                if (currentPeriod != null)
                {
                    // 结束当前回撤期
                    currentPeriod.EndTime = point.Timestamp;
                    currentPeriod.RecoveryTime = point.Timestamp;
                    periods.Add(currentPeriod);
                    currentPeriod = null;
                }
                
                localPeak = point.Equity;
            }
            else if (point.Equity < localPeak)
            {
                // 回撤中
                var drawdown = (localPeak - point.Equity) / localPeak;
                
                if (currentPeriod == null)
                {
                    // 开始新的回撤期
                    currentPeriod = new DrawdownPeriod
                    {
                        StartTime = _equityHistory[i - 1].Timestamp,
                        PeakEquity = localPeak,
                        MaxDrawdown = drawdown,
                        TroughEquity = point.Equity
                    };
                }
                else
                {
                    // 更新当前回撤期
                    if (drawdown > currentPeriod.MaxDrawdown)
                    {
                        currentPeriod.MaxDrawdown = drawdown;
                        currentPeriod.TroughEquity = point.Equity;
                        currentPeriod.TroughTime = point.Timestamp;
                    }
                }
            }
        }
        
        // 如果还在回撤中，添加当前回撤期
        if (currentPeriod != null)
        {
            currentPeriod.EndTime = DateTime.UtcNow;
            periods.Add(currentPeriod);
        }
        
        return periods;
    }
    
    /// <summary>
    /// 计算恢复率
    /// </summary>
    private decimal CalculateRecoveryRate()
    {
        var periods = CalculateDrawdownPeriods();
        var recoveredPeriods = periods.Where(p => p.RecoveryTime.HasValue).ToList();
        
        if (periods.Count == 0)
        {
            return 1.0m;
        }
        
        return (decimal)recoveredPeriods.Count / periods.Count;
    }
    
    /// <summary>
    /// 获取权益曲线
    /// </summary>
    public List<EquityPoint> GetEquityCurve(int maxPoints = 1000)
    {
        if (_equityHistory.Count <= maxPoints)
        {
            return _equityHistory.ToList();
        }
        
        // 采样
        var step = _equityHistory.Count / maxPoints;
        return _equityHistory.Where((_, i) => i % step == 0).ToList();
    }
}

/// <summary>
/// 权益点
/// </summary>
public class EquityPoint
{
    public DateTime Timestamp { get; set; }
    public decimal Equity { get; set; }
}

/// <summary>
/// 回撤期
/// </summary>
public class DrawdownPeriod
{
    public DateTime StartTime { get; set; }
    public DateTime? TroughTime { get; set; }
    public DateTime? EndTime { get; set; }
    public DateTime? RecoveryTime { get; set; }
    public decimal PeakEquity { get; set; }
    public decimal TroughEquity { get; set; }
    public decimal MaxDrawdown { get; set; }
    public TimeSpan Duration => (EndTime ?? DateTime.UtcNow) - StartTime;
}

/// <summary>
/// 回撤统计
/// </summary>
public class DrawdownStatistics
{
    public decimal CurrentEquity { get; set; }
    public decimal PeakEquity { get; set; }
    public decimal CurrentDrawdown { get; set; }
    public decimal MaxDrawdown { get; set; }
    public DateTime PeakTime { get; set; }
    public DateTime TroughTime { get; set; }
    public TimeSpan DrawdownDuration { get; set; }
    public decimal AverageDrawdown { get; set; }
    public int DrawdownCount { get; set; }
    public decimal RecoveryRate { get; set; }
}

