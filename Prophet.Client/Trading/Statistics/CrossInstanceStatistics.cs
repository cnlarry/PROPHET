using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Prophet.Client.Trading.Engine;
using Prophet.Client.Trading.Models;
using Prophet.Client.Trading.RiskControl;

namespace Prophet.Client.Trading.Statistics;

/// <summary>
/// 跨实例统计聚合器
/// 聚合所有实盘实例的统计数据，提供全局视图
/// </summary>
public class CrossInstanceStatistics
{
    private readonly TradingInstanceManager _instanceManager;
    
    public CrossInstanceStatistics(TradingInstanceManager instanceManager)
    {
        _instanceManager = instanceManager ?? throw new ArgumentNullException(nameof(instanceManager));
    }
    
    /// <summary>
    /// 获取全局统计摘要
    /// </summary>
    public async Task<GlobalStatisticsSummary> GetGlobalSummaryAsync()
    {
        var allInstances = _instanceManager.GetAllInstances();
        var runningCount = _instanceManager.GetRunningCount();
        
        var summary = new GlobalStatisticsSummary
        {
            TotalInstances = allInstances.Count,
            RunningInstances = runningCount,
            StoppedInstances = allInstances.Count - runningCount,
            GeneratedAt = DateTime.UtcNow
        };
        
        // 收集所有实例的快照
        var snapshots = new List<TradingInstanceSnapshot>();
        
        foreach (var instance in allInstances)
        {
            var snapshot = await _instanceManager.GetInstanceSnapshotAsync(instance.Id);
            if (snapshot != null)
            {
                snapshots.Add(snapshot);
            }
        }
        
        if (snapshots.Any())
        {
            // 计算总权益
            summary.TotalEquity = snapshots.Sum(s => s.TotalEquity);
            summary.TotalAvailableBalance = snapshots.Sum(s => s.AvailableBalance);
            summary.TotalUsedMargin = snapshots.Sum(s => s.UsedMargin);
            
            // 计算总盈亏
            summary.TotalUnrealizedPnL = snapshots.Sum(s => s.UnrealizedPnL);
            summary.TotalRealizedPnL = snapshots.Sum(s => s.RealizedPnL);
            summary.TotalTodayPnL = snapshots.Sum(s => s.TodayPnL);
            
            // 计算总体回撤（使用最大权益）
            var totalInitialCapital = allInstances.Sum(i => i.RiskConfig.InitialCapital);
            if (totalInitialCapital > 0)
            {
                var maxEquity = Math.Max(summary.TotalEquity, totalInitialCapital);
                summary.GlobalDrawdown = (maxEquity - summary.TotalEquity) / maxEquity;
            }
            
            // 计算持仓统计
            summary.TotalPositions = snapshots.Sum(s => s.PositionCount);
            summary.TotalOpenOrders = snapshots.Sum(s => s.OpenOrderCount);
            
            // 计算交易统计
            summary.TotalTrades = snapshots.Sum(s => s.TotalTrades);
            summary.TotalTodayTrades = snapshots.Sum(s => s.TodayTrades);
            
            // 计算平均胜率
            var instancesWithTrades = snapshots.Where(s => s.TotalTrades > 0).ToList();
            if (instancesWithTrades.Any())
            {
                summary.AverageWinRate = instancesWithTrades.Average(s => s.WinRate);
            }
            
            // 计算总体风险等级
            summary.GlobalRiskLevel = CalculateGlobalRiskLevel(snapshots);
            
            // 计算紧急停止数量
            summary.EmergencyStoppedCount = snapshots.Count(s => s.EmergencyStopActivated);
        }
        
        return summary;
    }
    
    /// <summary>
    /// 按交易所汇总
    /// </summary>
    public async Task<List<ExchangeSummary>> GetExchangeSummariesAsync()
    {
        var allInstances = _instanceManager.GetAllInstances();
        var exchangeGroups = allInstances.GroupBy(i => i.Exchange);
        
        var summaries = new List<ExchangeSummary>();
        
        foreach (var group in exchangeGroups)
        {
            var summary = new ExchangeSummary
            {
                ExchangeName = group.Key,
                InstanceCount = group.Count(),
                RunningCount = 0
            };
            
            var snapshots = new List<TradingInstanceSnapshot>();
            
            foreach (var instance in group)
            {
                var snapshot = await _instanceManager.GetInstanceSnapshotAsync(instance.Id);
                if (snapshot != null)
                {
                    snapshots.Add(snapshot);
                    
                    if (snapshot.Status == TradingInstanceStatus.Running)
                    {
                        summary.RunningCount++;
                    }
                }
            }
            
            if (snapshots.Any())
            {
                summary.TotalEquity = snapshots.Sum(s => s.TotalEquity);
                summary.TotalPnL = snapshots.Sum(s => s.RealizedPnL + s.UnrealizedPnL);
                summary.TodayPnL = snapshots.Sum(s => s.TodayPnL);
                summary.TotalPositions = snapshots.Sum(s => s.PositionCount);
                summary.TotalTrades = snapshots.Sum(s => s.TotalTrades);
                
                var instancesWithTrades = snapshots.Where(s => s.TotalTrades > 0).ToList();
                if (instancesWithTrades.Any())
                {
                    summary.AverageWinRate = instancesWithTrades.Average(s => s.WinRate);
                }
            }
            
            summaries.Add(summary);
        }
        
        return summaries.OrderByDescending(s => s.TotalEquity).ToList();
    }
    
    /// <summary>
    /// 按策略汇总
    /// </summary>
    public async Task<List<StrategySummary>> GetStrategySummariesAsync()
    {
        var allInstances = _instanceManager.GetAllInstances();
        var strategyGroups = allInstances.GroupBy(i => i.StrategyName);
        
        var summaries = new List<StrategySummary>();
        
        foreach (var group in strategyGroups)
        {
            var summary = new StrategySummary
            {
                StrategyName = group.Key,
                InstanceCount = group.Count(),
                RunningCount = 0
            };
            
            var snapshots = new List<TradingInstanceSnapshot>();
            
            foreach (var instance in group)
            {
                var snapshot = await _instanceManager.GetInstanceSnapshotAsync(instance.Id);
                if (snapshot != null)
                {
                    snapshots.Add(snapshot);
                    
                    if (snapshot.Status == TradingInstanceStatus.Running)
                    {
                        summary.RunningCount++;
                    }
                }
            }
            
            if (snapshots.Any())
            {
                summary.TotalEquity = snapshots.Sum(s => s.TotalEquity);
                summary.TotalPnL = snapshots.Sum(s => s.RealizedPnL + s.UnrealizedPnL);
                summary.TodayPnL = snapshots.Sum(s => s.TodayPnL);
                summary.TotalTrades = snapshots.Sum(s => s.TotalTrades);
                
                var instancesWithTrades = snapshots.Where(s => s.TotalTrades > 0).ToList();
                if (instancesWithTrades.Any())
                {
                    summary.AverageWinRate = instancesWithTrades.Average(s => s.WinRate);
                }
            }
            
            summaries.Add(summary);
        }
        
        return summaries.OrderByDescending(s => s.TotalPnL).ToList();
    }
    
    /// <summary>
    /// 获取实例排行榜
    /// </summary>
    public async Task<List<InstanceRanking>> GetInstanceRankingsAsync(RankingType rankingType = RankingType.ByProfit)
    {
        var allInstances = _instanceManager.GetAllInstances();
        var rankings = new List<InstanceRanking>();
        
        foreach (var instance in allInstances)
        {
            var snapshot = await _instanceManager.GetInstanceSnapshotAsync(instance.Id);
            
            rankings.Add(new InstanceRanking
            {
                InstanceId = instance.Id,
                InstanceName = instance.Name,
                Exchange = instance.Exchange,
                Symbol = instance.Symbol,
                StrategyName = instance.StrategyName,
                Status = snapshot?.Status ?? TradingInstanceStatus.Stopped,
                TotalEquity = snapshot?.TotalEquity ?? 0,
                TotalPnL = snapshot != null ? snapshot.RealizedPnL + snapshot.UnrealizedPnL : 0,
                TodayPnL = snapshot?.TodayPnL ?? 0,
                WinRate = snapshot?.WinRate ?? 0,
                TotalTrades = snapshot?.TotalTrades ?? 0,
                CurrentDrawdown = snapshot?.CurrentDrawdown ?? 0,
                RiskLevel = snapshot?.RiskLevel ?? RiskLevel.Low
            });
        }
        
        // 根据排名类型排序
        return rankingType switch
        {
            RankingType.ByProfit => rankings.OrderByDescending(r => r.TotalPnL).ToList(),
            RankingType.ByEquity => rankings.OrderByDescending(r => r.TotalEquity).ToList(),
            RankingType.ByWinRate => rankings.OrderByDescending(r => r.WinRate).ToList(),
            RankingType.ByTrades => rankings.OrderByDescending(r => r.TotalTrades).ToList(),
            _ => rankings
        };
    }
    
    /// <summary>
    /// 获取总权益曲线
    /// </summary>
    public async Task<List<EquityCurvePoint>> GetGlobalEquityCurveAsync(DateTime from, DateTime to)
    {
        // TODO: 实现从数据库聚合所有实例的权益曲线
        // 这需要从instance_snapshots表中按时间聚合数据
        
        await Task.CompletedTask;
        return new List<EquityCurvePoint>();
    }
    
    /// <summary>
    /// 计算全局风险等级
    /// </summary>
    private RiskLevel CalculateGlobalRiskLevel(List<TradingInstanceSnapshot> snapshots)
    {
        if (!snapshots.Any())
        {
            return RiskLevel.Low;
        }
        
        // 如果有任何实例处于严重风险，全局也是严重
        if (snapshots.Any(s => s.RiskLevel == RiskLevel.Critical))
        {
            return RiskLevel.Critical;
        }
        
        // 如果有任何实例处于高风险，全局也是高风险
        if (snapshots.Any(s => s.RiskLevel == RiskLevel.High))
        {
            return RiskLevel.High;
        }
        
        // 如果有任何实例处于中等风险，全局也是中等
        if (snapshots.Any(s => s.RiskLevel == RiskLevel.Medium))
        {
            return RiskLevel.Medium;
        }
        
        return RiskLevel.Low;
    }
}

// ========== 数据类型 ==========

/// <summary>
/// 全局统计摘要
/// </summary>
public class GlobalStatisticsSummary
{
    public DateTime GeneratedAt { get; set; }
    
    // 实例统计
    public int TotalInstances { get; set; }
    public int RunningInstances { get; set; }
    public int StoppedInstances { get; set; }
    public int EmergencyStoppedCount { get; set; }
    
    // 权益统计
    public decimal TotalEquity { get; set; }
    public decimal TotalAvailableBalance { get; set; }
    public decimal TotalUsedMargin { get; set; }
    
    // 盈亏统计
    public decimal TotalUnrealizedPnL { get; set; }
    public decimal TotalRealizedPnL { get; set; }
    public decimal TotalTodayPnL { get; set; }
    public decimal TotalPnL => TotalUnrealizedPnL + TotalRealizedPnL;
    
    // 风险指标
    public decimal GlobalDrawdown { get; set; }
    public RiskLevel GlobalRiskLevel { get; set; }
    
    // 持仓统计
    public int TotalPositions { get; set; }
    public int TotalOpenOrders { get; set; }
    
    // 交易统计
    public int TotalTrades { get; set; }
    public int TotalTodayTrades { get; set; }
    public decimal AverageWinRate { get; set; }
}

/// <summary>
/// 交易所摘要
/// </summary>
public class ExchangeSummary
{
    public string ExchangeName { get; set; } = string.Empty;
    public int InstanceCount { get; set; }
    public int RunningCount { get; set; }
    public decimal TotalEquity { get; set; }
    public decimal TotalPnL { get; set; }
    public decimal TodayPnL { get; set; }
    public int TotalPositions { get; set; }
    public int TotalTrades { get; set; }
    public decimal AverageWinRate { get; set; }
}

/// <summary>
/// 策略摘要
/// </summary>
public class StrategySummary
{
    public string StrategyName { get; set; } = string.Empty;
    public int InstanceCount { get; set; }
    public int RunningCount { get; set; }
    public decimal TotalEquity { get; set; }
    public decimal TotalPnL { get; set; }
    public decimal TodayPnL { get; set; }
    public int TotalTrades { get; set; }
    public decimal AverageWinRate { get; set; }
}

/// <summary>
/// 实例排名
/// </summary>
public class InstanceRanking
{
    public Guid InstanceId { get; set; }
    public string InstanceName { get; set; } = string.Empty;
    public string Exchange { get; set; } = string.Empty;
    public string Symbol { get; set; } = string.Empty;
    public string StrategyName { get; set; } = string.Empty;
    public TradingInstanceStatus Status { get; set; }
    public decimal TotalEquity { get; set; }
    public decimal TotalPnL { get; set; }
    public decimal TodayPnL { get; set; }
    public decimal WinRate { get; set; }
    public int TotalTrades { get; set; }
    public decimal CurrentDrawdown { get; set; }
    public RiskLevel RiskLevel { get; set; }
}

/// <summary>
/// 排名类型
/// </summary>
public enum RankingType
{
    ByProfit,
    ByEquity,
    ByWinRate,
    ByTrades
}

/// <summary>
/// 权益曲线点
/// </summary>
public class EquityCurvePoint
{
    public DateTime Timestamp { get; set; }
    public decimal Equity { get; set; }
}

