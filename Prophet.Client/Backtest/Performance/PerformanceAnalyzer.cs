using System;
using System.Collections.Generic;
using System.Linq;
using Prophet.Client.Backtest.Models;
using Prophet.Client.Models;

namespace Prophet.Client.Backtest.Performance;

/// <summary>
/// 绩效分析器
/// 计算回测的各项绩效指标
/// </summary>
public class PerformanceAnalyzer : IPerformanceAnalyzer
{
    private BacktestConfig _config = null!;
    private readonly List<EquityPoint> _equityCurve = new();
    private readonly List<Order> _allOrders = new();
    private readonly List<SignalEvent> _allSignals = new();
    private readonly List<DrawdownPeriod> _drawdownPeriods = new();
    
    private decimal _initialCapital;
    private decimal _peakEquity;
    private DateTime _peakEquityTime;
    private decimal _currentDrawdown;
    private decimal _maxDrawdown;
    private DateTime? _drawdownStartTime;
    
    public decimal CurrentReturn { get; private set; }
    public decimal CurrentDrawdown => _currentDrawdown;
    public decimal MaxDrawdown => _maxDrawdown;
    
    // ========== 数据访问（只读）==========
    /// <summary>
    /// 获取所有订单的只读视图
    /// </summary>
    public IReadOnlyList<Order> Orders => _allOrders.AsReadOnly();
    
    /// <summary>
    /// 获取所有信号的只读视图
    /// </summary>
    public IReadOnlyList<SignalEvent> Signals => _allSignals.AsReadOnly();
    
    // ========== 事件 ==========
    /// <summary>
    /// 订单变化事件（新增或更新）
    /// </summary>
    public event EventHandler<OrderChangedEventArgs>? OrderChanged;
    
    public void Initialize(BacktestConfig config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _initialCapital = config.InitialCapital;
        _peakEquity = _initialCapital;
        _currentDrawdown = 0;
        _maxDrawdown = 0;
        CurrentReturn = 0;
        
        _equityCurve.Clear();
        _allOrders.Clear();
        _allSignals.Clear();
        _drawdownPeriods.Clear();
    }
    
    /// <summary>
    /// 更新绩效指标（每根K线调用一次）
    /// </summary>
    public void Update(Candlestick candle, decimal currentEquity, decimal currentCash, decimal currentPosition)
    {
        // 记录权益曲线
        _equityCurve.Add(new EquityPoint
        {
            Time = candle.Time,
            Equity = currentEquity,
            Cash = currentCash,
            Position = currentPosition
        });
        
        // 更新收益率
        CurrentReturn = (_initialCapital > 0) ? (currentEquity - _initialCapital) / _initialCapital : 0;
        
        // 更新回撤
        UpdateDrawdown(candle.Time, currentEquity);
    }
    
    /// <summary>
    /// 更新绩效指标（旧版本，保持向后兼容）
    /// </summary>
    public void Update(Candlestick candle, decimal currentEquity)
    {
        Update(candle, currentEquity, 0, 0);
    }
    
    /// <summary>
    /// 记录订单（由BacktestEngine调用）
    /// 这是订单的唯一数据源，所有订单管理都在这里
    /// </summary>
    public void RecordOrder(Order order)
    {
        if (order == null)
        {
            Console.WriteLine("⚠️ PerformanceAnalyzer.RecordOrder: 收到空订单，跳过");
            return;
        }
        
        // 检查是否已存在相同ID的订单
        var existingOrder = _allOrders.FirstOrDefault(o => o.Id == order.Id);
        if (existingOrder != null)
        {
            // 订单已存在，更新订单信息（主要是状态、平仓价、盈亏等）
            existingOrder.Status = order.Status;
            existingOrder.ClosePrice = order.ClosePrice;
            existingOrder.CloseTime = order.CloseTime;
            existingOrder.Fee = order.Fee;
            existingOrder.FundingFee = order.FundingFee;
            existingOrder.Profit = order.Profit;
            existingOrder.Remarks = order.Remarks;
            
            // 触发更新事件
            OrderChanged?.Invoke(this, new OrderChangedEventArgs
            {
                Order = existingOrder,
                ChangeType = OrderChangeType.Updated
            });
        }
        else
        {
            // 新订单，添加到列表
            _allOrders.Add(order);
            
            // 触发新增事件
            OrderChanged?.Invoke(this, new OrderChangedEventArgs
            {
                Order = order,
                ChangeType = OrderChangeType.Added
            });
        }
    }
    
    /// <summary>
    /// 记录信号事件（由BacktestEngine调用）
    /// </summary>
    public void RecordSignal(SignalEvent signal)
    {
        _allSignals.Add(signal);
    }
    
    /// <summary>
    /// 生成最终绩效报告
    /// </summary>
    public BacktestResult GenerateReport()
    {
        if (_equityCurve.Count == 0)
        {
            throw new InvalidOperationException("权益曲线为空，无法生成报告");
        }
        
        var startTime = _equityCurve.First().Time;
        var endTime = _equityCurve.Last().Time;
        var finalEquity = _equityCurve.Last().Equity;
        var totalReturn = (finalEquity - _initialCapital) / _initialCapital;
        
        // 计算年化收益（添加边界检查，避免溢出）
        var timeSpan = endTime - startTime;
        var years = timeSpan.TotalDays / 365.0;
        decimal annualizedReturn = 0;
        
        if (years > 0)
        {
            try
            {
                var returnMultiplier = (double)(1 + totalReturn);
                // 检查是否为有效值
                if (returnMultiplier > 0 && !double.IsInfinity(returnMultiplier) && !double.IsNaN(returnMultiplier))
                {
                    var annualizedValue = Math.Pow(returnMultiplier, 1.0 / years) - 1;
                    
                    // 检查结果是否在 Decimal 范围内
                    if (!double.IsInfinity(annualizedValue) && !double.IsNaN(annualizedValue) 
                        && annualizedValue >= (double)decimal.MinValue 
                        && annualizedValue <= (double)decimal.MaxValue)
                    {
                        annualizedReturn = (decimal)annualizedValue;
                    }
                    else
                    {
                        // 如果超出范围，设置为极值
                        annualizedReturn = annualizedValue > 0 ? 100m : -1m;  // 100倍或-100%
                        Console.WriteLine($"⚠️ 年化收益率超出范围，设置为: {annualizedReturn:P2}");
                    }
                }
                else
                {
                    // 亏光了或数据异常
                    annualizedReturn = -1m;  // -100%
                }
            }
            catch (OverflowException)
            {
                annualizedReturn = totalReturn > 0 ? 100m : -1m;
                Console.WriteLine($"⚠️ 年化收益率计算溢出，设置为: {annualizedReturn:P2}");
            }
        }
        
        // 分离盈利和亏损订单
        var closedOrders = _allOrders.Where(o => o.Status == OrderStatus.CLOSED).ToList();
        var winningTrades = closedOrders.Where(o => o.Profit > 0).ToList();
        var losingTrades = closedOrders.Where(o => o.Profit < 0).ToList();
        
        // 计算交易统计
        var totalTrades = closedOrders.Count;
        var winRate = totalTrades > 0 ? (decimal)winningTrades.Count / totalTrades : 0;
        var avgProfit = winningTrades.Count > 0 ? winningTrades.Average(o => o.Profit ?? 0) : 0;
        var avgLoss = losingTrades.Count > 0 ? losingTrades.Average(o => o.Profit ?? 0) : 0;
        var totalProfit = winningTrades.Sum(o => o.Profit ?? 0);
        var totalLoss = Math.Abs(losingTrades.Sum(o => o.Profit ?? 0));
        var profitFactor = totalLoss > 0 ? totalProfit / totalLoss : 0;
        
        // 计算连续盈亏
        var (maxConsecutiveWins, maxConsecutiveLosses) = CalculateConsecutiveWinsLosses(closedOrders);
        
        // 计算最大单笔盈亏
        var maxSingleProfit = closedOrders.Any() ? closedOrders.Max(o => o.Profit ?? 0) : 0;
        var maxSingleLoss = closedOrders.Any() ? closedOrders.Min(o => o.Profit ?? 0) : 0;
        
        // 计算平均持仓时间
        var avgHoldingTime = CalculateAverageHoldingTime(closedOrders);
        
        // 计算夏普比率、索提诺比率、卡玛比率（添加溢出保护）
        var sharpeRatio = CalculateSharpeRatio(_equityCurve);
        var sortinoRatio = CalculateSortinoRatio(_equityCurve);
        
        // 卡玛比率：年化收益 / 最大回撤（添加边界检查）
        decimal calmarRatio = 0;
        if (_maxDrawdown > 0.0001m)  // 避免除以接近0的数
        {
            try
            {
                calmarRatio = annualizedReturn / _maxDrawdown;
                // 限制在合理范围内
                if (calmarRatio > 1000m) calmarRatio = 1000m;
                if (calmarRatio < -1000m) calmarRatio = -1000m;
            }
            catch (OverflowException)
            {
                calmarRatio = annualizedReturn > 0 ? 1000m : -1000m;
            }
        }
        
        // 创建报告
        var result = new BacktestResult
        {
            BacktestId = _config.RunId,
            StrategyName = "DSL Strategy", // 需要从外部传入
            Symbol = _config.Symbol,
            Interval = _config.Interval,
            StartTime = startTime,
            EndTime = endTime,
            Duration = timeSpan,
            Status = BacktestStatus.COMPLETED,
            
            // 资金统计
            InitialCapital = _initialCapital,
            FinalEquity = finalEquity,
            TotalReturn = totalReturn,
            AnnualizedReturn = annualizedReturn,
            MaxDrawdown = _maxDrawdown,
            
            // 风险指标
            SharpeRatio = sharpeRatio,
            SortinoRatio = sortinoRatio,
            CalmarRatio = calmarRatio,
            
            // 交易统计
            TotalTrades = totalTrades,
            WinningTrades = winningTrades.Count,
            LosingTrades = losingTrades.Count,
            WinRate = winRate,
            AvgProfit = avgProfit,
            AvgLoss = avgLoss,
            ProfitFactor = profitFactor,
            MaxConsecutiveWins = maxConsecutiveWins,
            MaxConsecutiveLosses = maxConsecutiveLosses,
            MaxSingleProfit = maxSingleProfit,
            MaxSingleLoss = maxSingleLoss,
            AvgHoldingTime = avgHoldingTime,
            
            // 数据集合
            EquityCurve = _equityCurve,
            Orders = _allOrders,
            DrawdownPeriods = _drawdownPeriods,
            Signals = _allSignals,
            
            CompletedAt = DateTime.UtcNow
        };
        
        return result;
    }
    
    /// <summary>
    /// 更新回撤
    /// </summary>
    private void UpdateDrawdown(DateTime time, decimal equity)
    {
        // 更新峰值
        if (equity > _peakEquity)
        {
            // 如果从回撤中恢复，记录回撤周期
            if (_drawdownStartTime.HasValue && _currentDrawdown > 0)
            {
                _drawdownPeriods.Add(new DrawdownPeriod
                {
                    StartTime = _drawdownStartTime.Value,
                    EndTime = _peakEquityTime,
                    DrawdownPercentage = _currentDrawdown,
                    Duration = _peakEquityTime - _drawdownStartTime.Value,
                    RecoveryTime = (decimal)(time - _peakEquityTime).TotalDays
                });
            }
            
            _peakEquity = equity;
            _peakEquityTime = time;
            _currentDrawdown = 0;
            _drawdownStartTime = null;
        }
        else
        {
            // 计算当前回撤
            _currentDrawdown = (_peakEquity - equity) / _peakEquity;
            
            if (!_drawdownStartTime.HasValue && _currentDrawdown > 0)
            {
                _drawdownStartTime = time;
            }
            
            // 更新最大回撤
            if (_currentDrawdown > _maxDrawdown)
            {
                _maxDrawdown = _currentDrawdown;
            }
        }
    }
    
    /// <summary>
    /// 计算连续盈亏次数
    /// </summary>
    private (int maxWins, int maxLosses) CalculateConsecutiveWinsLosses(List<Order> orders)
    {
        if (orders.Count == 0)
            return (0, 0);
        
        int maxWins = 0, maxLosses = 0;
        int currentWins = 0, currentLosses = 0;
        
        foreach (var order in orders.OrderBy(o => o.CloseTime))
        {
            if (order.Profit > 0)
            {
                currentWins++;
                currentLosses = 0;
                maxWins = Math.Max(maxWins, currentWins);
            }
            else if (order.Profit < 0)
            {
                currentLosses++;
                currentWins = 0;
                maxLosses = Math.Max(maxLosses, currentLosses);
            }
        }
        
        return (maxWins, maxLosses);
    }
    
    /// <summary>
    /// 计算平均持仓时间
    /// </summary>
    private TimeSpan CalculateAverageHoldingTime(List<Order> orders)
    {
        if (orders.Count == 0)
            return TimeSpan.Zero;
        
        var holdingTimes = orders
            .Where(o => o.OpenTime != default && o.CloseTime.HasValue)
            .Select(o => o.CloseTime!.Value - o.OpenTime)
            .ToList();
        
        if (holdingTimes.Count == 0)
            return TimeSpan.Zero;
        
        var avgTicks = (long)holdingTimes.Average(t => t.Ticks);
        return TimeSpan.FromTicks(avgTicks);
    }
    
    /// <summary>
    /// 计算夏普比率（Sharpe Ratio）
    /// </summary>
    private decimal CalculateSharpeRatio(List<EquityPoint> equityCurve)
    {
        if (equityCurve.Count < 2)
            return 0;
        
        // 计算日收益率
        var returns = new List<decimal>();
        for (int i = 1; i < equityCurve.Count; i++)
        {
            var prevEquity = equityCurve[i - 1].Equity;
            var currEquity = equityCurve[i].Equity;
            if (prevEquity > 0)
            {
                returns.Add((currEquity - prevEquity) / prevEquity);
            }
        }
        
        if (returns.Count == 0)
            return 0;
        
        // 平均收益率
        var avgReturn = returns.Average();
        
        // 收益率标准差（添加溢出保护）
        var variance = returns.Sum(r => (r - avgReturn) * (r - avgReturn)) / returns.Count;
        
        // 检查方差是否有效
        if (variance < 0 || double.IsNaN((double)variance) || double.IsInfinity((double)variance))
            return 0;
        
        var sqrtVariance = Math.Sqrt((double)variance);
        if (double.IsNaN(sqrtVariance) || double.IsInfinity(sqrtVariance))
            return 0;
        
        var stdDev = (decimal)sqrtVariance;
        
        if (stdDev == 0 || stdDev < 0.0000001m)
            return 0;
        
        // 年化夏普比率（假设无风险利率为0，添加边界检查）
        try
        {
            // 根据K线时间框架计算年化因子（一年有多少根K线）
            var annualizationFactor = CalculateAnnualizationFactor(equityCurve);
            var sharpe = (avgReturn / stdDev) * (decimal)Math.Sqrt((double)annualizationFactor);
            
            // 限制在合理范围内（-100到100）
            if (sharpe > 100m) return 100m;
            if (sharpe < -100m) return -100m;
            if (double.IsNaN((double)sharpe) || double.IsInfinity((double)sharpe))
                return 0;
            
            return sharpe;
        }
        catch (OverflowException)
        {
            return avgReturn > 0 ? 100m : -100m;
        }
    }
    
    /// <summary>
    /// 计算索提诺比率（Sortino Ratio）
    /// 只考虑下行波动率
    /// </summary>
    private decimal CalculateSortinoRatio(List<EquityPoint> equityCurve)
    {
        if (equityCurve.Count < 2)
            return 0;
        
        // 计算日收益率
        var returns = new List<decimal>();
        for (int i = 1; i < equityCurve.Count; i++)
        {
            var prevEquity = equityCurve[i - 1].Equity;
            var currEquity = equityCurve[i].Equity;
            if (prevEquity > 0)
            {
                returns.Add((currEquity - prevEquity) / prevEquity);
            }
        }
        
        if (returns.Count == 0)
            return 0;
        
        // 平均收益率
        var avgReturn = returns.Average();
        
        // 下行偏差（只统计负收益，添加溢出保护）
        var downside = returns.Where(r => r < 0).ToList();
        if (downside.Count == 0)
            return 100m; // 无下行风险，返回高值
        
        var downsideVariance = downside.Sum(r => r * r) / downside.Count;
        
        // 检查方差是否有效
        if (downsideVariance < 0 || double.IsNaN((double)downsideVariance) || double.IsInfinity((double)downsideVariance))
            return 0;
        
        var sqrtDownsideVar = Math.Sqrt((double)downsideVariance);
        if (double.IsNaN(sqrtDownsideVar) || double.IsInfinity(sqrtDownsideVar))
            return 0;
        
        var downsideDeviation = (decimal)sqrtDownsideVar;
        
        if (downsideDeviation == 0 || downsideDeviation < 0.0000001m)
            return 0;
        
        // 年化索提诺比率（添加边界检查）
        try
        {
            // 根据K线时间框架计算年化因子（一年有多少根K线）
            var annualizationFactor = CalculateAnnualizationFactor(equityCurve);
            var sortino = (avgReturn / downsideDeviation) * (decimal)Math.Sqrt((double)annualizationFactor);
            
            // 限制在合理范围内（-100到100）
            if (sortino > 100m) return 100m;
            if (sortino < -100m) return -100m;
            if (double.IsNaN((double)sortino) || double.IsInfinity((double)sortino))
                return 0;
            
            return sortino;
        }
        catch (OverflowException)
        {
            return avgReturn > 0 ? 100m : -100m;
        }
    }
    
    /// <summary>
    /// 计算年化因子（一年有多少根K线）
    /// 根据权益曲线的时间间隔动态计算
    /// </summary>
    private decimal CalculateAnnualizationFactor(List<EquityPoint> equityCurve)
    {
        if (equityCurve.Count < 2)
            return 365m; // 默认值
        
        try
        {
            // 计算平均K线间隔（秒）
            var totalDuration = (equityCurve.Last().Time - equityCurve.First().Time).TotalSeconds;
            var avgInterval = totalDuration / (equityCurve.Count - 1);
            
            // 计算一年有多少根K线
            var secondsPerYear = 365.25 * 24 * 3600; // 考虑闰年
            var klinesPerYear = (decimal)(secondsPerYear / avgInterval);
            
            // 返回合理范围内的值
            return Math.Max(1m, Math.Min(klinesPerYear, 1000000m));
        }
        catch
        {
            // 如果计算失败，使用默认值
            return 365m;
        }
    }
}

