using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Prophet.Client.Models;
using Prophet.Client.Trading.Exchanges;

namespace Prophet.Client.Trading.RiskControl;

/// <summary>
/// 风险控制器
/// 在交易信号执行前进行风险评估和控制
/// </summary>
public class RiskController
{
    private readonly IExchange _exchange;
    private readonly RiskConfig _config;
    private readonly AccountMonitor _accountMonitor;
    private readonly PositionMonitor _positionMonitor;
    private readonly DrawdownTracker _drawdownTracker;
    private readonly TradingStatistics _statistics;
    
    private bool _emergencyStopActivated;
    private DateTime _lastCheckTime = DateTime.MinValue;
    
    // 事件
    public event EventHandler<RiskViolationEventArgs>? RiskViolationDetected;
    public event EventHandler<EmergencyStopEventArgs>? EmergencyStopTriggered;
    public event EventHandler<RiskWarningEventArgs>? RiskWarningIssued;
    
    public bool IsEmergencyStopActivated => _emergencyStopActivated;
    
    public RiskController(
        IExchange exchange,
        RiskConfig config,
        AccountMonitor? accountMonitor = null,
        PositionMonitor? positionMonitor = null)
    {
        _exchange = exchange ?? throw new ArgumentNullException(nameof(exchange));
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _accountMonitor = accountMonitor ?? new AccountMonitor(exchange, config);
        _positionMonitor = positionMonitor ?? new PositionMonitor(exchange, config);
        _drawdownTracker = new DrawdownTracker(config.InitialCapital);
        _statistics = new TradingStatistics();
    }
    
    /// <summary>
    /// 检查信号是否通过风险控制
    /// </summary>
    public async Task<RiskCheckResult> CheckSignalAsync(Signal signal, decimal currentEquity)
    {
        var result = new RiskCheckResult { IsApproved = true };
        
        try
        {
            // 1. 紧急停止检查
            if (_emergencyStopActivated)
            {
                result.IsApproved = false;
                result.RejectReason = "紧急停止已激活";
                result.RiskLevel = RiskLevel.Critical;
                Console.WriteLine($"❌ [RiskController] 信号被拒绝: 紧急停止已激活");
                return result;
            }
            
            // 2. 检查最大持仓数量
            var checkResult = await CheckMaxPositionsAsync();
            if (!checkResult.Passed)
            {
                result.IsApproved = false;
                result.RejectReason = checkResult.Message;
                result.RiskLevel = RiskLevel.High;
                RaiseRiskViolation("最大持仓数量", checkResult.Message);
                return result;
            }
            
            // 3. 检查最大回撤
            checkResult = await CheckMaxDrawdownAsync(currentEquity);
            if (!checkResult.Passed)
            {
                result.IsApproved = false;
                result.RejectReason = checkResult.Message;
                result.RiskLevel = RiskLevel.Critical;
                
                // 触发紧急停止
                if (_config.EnableEmergencyStopOnMaxDrawdown)
                {
                    await TriggerEmergencyStopAsync("超过最大回撤限制");
                }
                
                RaiseRiskViolation("最大回撤", checkResult.Message);
                return result;
            }
            
            // 4. 检查单笔风险
            checkResult = await CheckSingleTradeRiskAsync(signal, currentEquity);
            if (!checkResult.Passed)
            {
                result.IsApproved = false;
                result.RejectReason = checkResult.Message;
                result.RiskLevel = RiskLevel.Medium;
                RaiseRiskViolation("单笔风险", checkResult.Message);
                return result;
            }
            
            // 5. 检查每日交易次数
            checkResult = CheckDailyTradeLimit();
            if (!checkResult.Passed)
            {
                result.IsApproved = false;
                result.RejectReason = checkResult.Message;
                result.RiskLevel = RiskLevel.Low;
                RaiseRiskViolation("每日交易次数", checkResult.Message);
                return result;
            }
            
            // 6. 检查账户余额
            checkResult = await CheckMinimumBalanceAsync();
            if (!checkResult.Passed)
            {
                result.IsApproved = false;
                result.RejectReason = checkResult.Message;
                result.RiskLevel = RiskLevel.High;
                RaiseRiskViolation("最小余额", checkResult.Message);
                return result;
            }
            
            // 7. 检查连续亏损
            checkResult = CheckConsecutiveLosses();
            if (!checkResult.Passed)
            {
                result.IsApproved = false;
                result.RejectReason = checkResult.Message;
                result.RiskLevel = RiskLevel.High;
                
                if (_config.EnableEmergencyStopOnConsecutiveLosses)
                {
                    await TriggerEmergencyStopAsync("连续亏损次数过多");
                }
                
                RaiseRiskViolation("连续亏损", checkResult.Message);
                return result;
            }
            
            // 8. 检查仓位风险度
            checkResult = await CheckPositionRiskAsync();
            if (!checkResult.Passed)
            {
                // 仓位风险度过高只发出警告，不拒绝交易
                result.RiskLevel = RiskLevel.Medium;
                RaiseRiskWarning("仓位风险度", checkResult.Message);
            }
            
            Console.WriteLine($"✅ [RiskController] 信号通过风险检查: {signal.Action}");
            return result;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ [RiskController] 风险检查异常: {ex.Message}");
            result.IsApproved = false;
            result.RejectReason = $"风险检查异常: {ex.Message}";
            result.RiskLevel = RiskLevel.Critical;
            return result;
        }
    }
    
    /// <summary>
    /// 检查最大持仓数量
    /// </summary>
    private async Task<CheckResult> CheckMaxPositionsAsync()
    {
        var positions = await _exchange.GetPositionsAsync();
        var activePositions = positions.Count(p => p.Quantity > 0);
        
        if (activePositions >= _config.MaxOpenPositions)
        {
            return new CheckResult
            {
                Passed = false,
                Message = $"已达到最大持仓数量限制: {activePositions}/{_config.MaxOpenPositions}"
            };
        }
        
        return new CheckResult { Passed = true };
    }
    
    /// <summary>
    /// 检查最大回撤
    /// </summary>
    private async Task<CheckResult> CheckMaxDrawdownAsync(decimal currentEquity)
    {
        _drawdownTracker.Update(currentEquity);
        var drawdown = _drawdownTracker.CurrentDrawdown;
        
        if (drawdown > _config.MaxDrawdown)
        {
            return new CheckResult
            {
                Passed = false,
                Message = $"回撤超过限制: {drawdown:P2} > {_config.MaxDrawdown:P2}"
            };
        }
        
        // 发出警告（回撤超过80%限制时）
        if (drawdown > _config.MaxDrawdown * 0.8m)
        {
            RaiseRiskWarning("回撤接近限制", $"当前回撤: {drawdown:P2}, 限制: {_config.MaxDrawdown:P2}");
        }
        
        return new CheckResult { Passed = true };
    }
    
    /// <summary>
    /// 检查单笔交易风险
    /// </summary>
    private async Task<CheckResult> CheckSingleTradeRiskAsync(Signal signal, decimal currentEquity)
    {
        // 计算潜在损失
        if (!signal.StopLoss.HasValue)
        {
            // 如果没有止损，使用默认风险比例
            return new CheckResult
            {
                Passed = false,
                Message = "信号缺少止损价，无法评估风险"
            };
        }
        
        var riskDistance = Math.Abs(signal.SignalPrice - signal.StopLoss.Value);
        var riskPercent = riskDistance / signal.SignalPrice;
        
        // 计算风险金额（基于当前权益和仓位比例）
        var positionSize = currentEquity * _config.PositionSizePercent;
        var riskAmount = positionSize * riskPercent;
        var riskPercentOfEquity = riskAmount / currentEquity;
        
        if (riskPercentOfEquity > _config.MaxRiskPerTrade)
        {
            return new CheckResult
            {
                Passed = false,
                Message = $"单笔风险过高: {riskPercentOfEquity:P2} > {_config.MaxRiskPerTrade:P2}"
            };
        }
        
        return new CheckResult { Passed = true };
    }
    
    /// <summary>
    /// 检查每日交易次数限制
    /// </summary>
    private CheckResult CheckDailyTradeLimit()
    {
        var todayTrades = _statistics.GetTodayTradeCount();
        
        if (todayTrades >= _config.MaxDailyTrades)
        {
            return new CheckResult
            {
                Passed = false,
                Message = $"已达到每日交易次数限制: {todayTrades}/{_config.MaxDailyTrades}"
            };
        }
        
        return new CheckResult { Passed = true };
    }
    
    /// <summary>
    /// 检查最小余额
    /// </summary>
    private async Task<CheckResult> CheckMinimumBalanceAsync()
    {
        var balance = await _exchange.GetBalanceAsync("USDT");
        
        if (balance < _config.MinBalance)
        {
            return new CheckResult
            {
                Passed = false,
                Message = $"余额低于最小限制: {balance:F2} < {_config.MinBalance:F2} USDT"
            };
        }
        
        return new CheckResult { Passed = true };
    }
    
    /// <summary>
    /// 检查连续亏损
    /// </summary>
    private CheckResult CheckConsecutiveLosses()
    {
        var consecutiveLosses = _statistics.ConsecutiveLosses;
        
        if (consecutiveLosses >= _config.MaxConsecutiveLosses)
        {
            return new CheckResult
            {
                Passed = false,
                Message = $"连续亏损次数过多: {consecutiveLosses}/{_config.MaxConsecutiveLosses}"
            };
        }
        
        // 发出警告（连续亏损接近限制时）
        if (consecutiveLosses >= _config.MaxConsecutiveLosses - 2)
        {
            RaiseRiskWarning("连续亏损", $"已连续亏损 {consecutiveLosses} 次");
        }
        
        return new CheckResult { Passed = true };
    }
    
    /// <summary>
    /// 检查仓位风险度
    /// </summary>
    private async Task<CheckResult> CheckPositionRiskAsync()
    {
        var riskMetrics = await _positionMonitor.GetRiskMetricsAsync();
        
        if (riskMetrics.TotalRiskPercent > 0.50m) // 50%
        {
            return new CheckResult
            {
                Passed = false,
                Message = $"仓位风险度过高: {riskMetrics.TotalRiskPercent:P2}"
            };
        }
        
        return new CheckResult { Passed = true };
    }
    
    /// <summary>
    /// 触发紧急停止
    /// </summary>
    public async Task TriggerEmergencyStopAsync(string reason)
    {
        if (_emergencyStopActivated)
        {
            return;
        }
        
        _emergencyStopActivated = true;
        
        Console.WriteLine($"🚨 [RiskController] 紧急停止已触发: {reason}");
        
        EmergencyStopTriggered?.Invoke(this, new EmergencyStopEventArgs
        {
            Reason = reason,
            Timestamp = DateTime.UtcNow,
            CurrentEquity = _drawdownTracker.CurrentEquity,
            MaxDrawdown = _drawdownTracker.MaxDrawdown
        });
        
        // 可选：自动平掉所有持仓
        if (_config.CloseAllPositionsOnEmergencyStop)
        {
            await CloseAllPositionsAsync();
        }
    }
    
    /// <summary>
    /// 重置紧急停止
    /// </summary>
    public void ResetEmergencyStop()
    {
        _emergencyStopActivated = false;
        Console.WriteLine($"✅ [RiskController] 紧急停止已重置");
    }
    
    /// <summary>
    /// 平掉所有持仓
    /// </summary>
    private async Task CloseAllPositionsAsync()
    {
        try
        {
            var positions = await _exchange.GetPositionsAsync();
            
            foreach (var position in positions.Where(p => p.Quantity > 0))
            {
                var closeSide = position.Side == PositionSide.Long 
                    ? Exchanges.OrderSide.SELL 
                    : Exchanges.OrderSide.BUY;
                
                var request = new Models.MarketOrderRequest
                {
                    Symbol = position.Symbol,
                    Side = closeSide,
                    Quantity = position.Quantity
                };
                
                await _exchange.PlaceMarketOrderAsync(request);
                
                Console.WriteLine($"⚠️ [RiskController] 已平仓: {position.Symbol}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ [RiskController] 平仓失败: {ex.Message}");
        }
    }
    
    /// <summary>
    /// 记录交易结果
    /// </summary>
    public void RecordTrade(Order order)
    {
        _statistics.RecordTrade(order);
        
        if (order.Profit.HasValue)
        {
            _drawdownTracker.Update(_drawdownTracker.CurrentEquity + order.Profit.Value);
        }
    }
    
    /// <summary>
    /// 更新账户权益
    /// </summary>
    public void UpdateEquity(decimal equity)
    {
        _drawdownTracker.Update(equity);
    }
    
    /// <summary>
    /// 获取风险报告
    /// </summary>
    public async Task<RiskReport> GetRiskReportAsync()
    {
        var account = await _accountMonitor.GetAccountStatusAsync();
        var positions = await _positionMonitor.GetRiskMetricsAsync();
        
        return new RiskReport
        {
            GeneratedAt = DateTime.UtcNow,
            EmergencyStopActivated = _emergencyStopActivated,
            CurrentEquity = _drawdownTracker.CurrentEquity,
            PeakEquity = _drawdownTracker.PeakEquity,
            CurrentDrawdown = _drawdownTracker.CurrentDrawdown,
            MaxDrawdown = _drawdownTracker.MaxDrawdown,
            AvailableBalance = account.AvailableBalance,
            TotalMargin = account.TotalMargin,
            ActivePositions = positions.ActivePositionCount,
            TotalRiskPercent = positions.TotalRiskPercent,
            TodayTrades = _statistics.GetTodayTradeCount(),
            ConsecutiveLosses = _statistics.ConsecutiveLosses,
            WinRate = _statistics.WinRate,
            RiskLevel = CalculateOverallRiskLevel()
        };
    }
    
    /// <summary>
    /// 计算总体风险等级
    /// </summary>
    private RiskLevel CalculateOverallRiskLevel()
    {
        if (_emergencyStopActivated)
            return RiskLevel.Critical;
        
        if (_drawdownTracker.CurrentDrawdown > _config.MaxDrawdown * 0.8m)
            return RiskLevel.Critical;
        
        if (_statistics.ConsecutiveLosses >= _config.MaxConsecutiveLosses - 1)
            return RiskLevel.High;
        
        if (_drawdownTracker.CurrentDrawdown > _config.MaxDrawdown * 0.5m)
            return RiskLevel.Medium;
        
        return RiskLevel.Low;
    }
    
    private void RaiseRiskViolation(string category, string message)
    {
        Console.WriteLine($"⚠️ [RiskController] 风险违规: {category} - {message}");
        
        RiskViolationDetected?.Invoke(this, new RiskViolationEventArgs
        {
            Category = category,
            Message = message,
            Timestamp = DateTime.UtcNow
        });
    }
    
    private void RaiseRiskWarning(string category, string message)
    {
        Console.WriteLine($"⚠️ [RiskController] 风险警告: {category} - {message}");
        
        RiskWarningIssued?.Invoke(this, new RiskWarningEventArgs
        {
            Category = category,
            Message = message,
            Timestamp = DateTime.UtcNow
        });
    }
}

/// <summary>
/// 风险检查结果
/// </summary>
public class RiskCheckResult
{
    public bool IsApproved { get; set; }
    public string? RejectReason { get; set; }
    public RiskLevel RiskLevel { get; set; }
}

/// <summary>
/// 检查结果
/// </summary>
public class CheckResult
{
    public bool Passed { get; set; }
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// 风险等级
/// </summary>
public enum RiskLevel
{
    Low,
    Medium,
    High,
    Critical
}

// 事件参数
public class RiskViolationEventArgs : EventArgs
{
    public string Category { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
}

public class EmergencyStopEventArgs : EventArgs
{
    public string Reason { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public decimal CurrentEquity { get; set; }
    public decimal MaxDrawdown { get; set; }
}

public class RiskWarningEventArgs : EventArgs
{
    public string Category { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
}

