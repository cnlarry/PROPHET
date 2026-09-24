using System;
using System.Collections.Generic;
using System.Linq;
using Prophet.Client.Models;

namespace Prophet.Client.Trading.RiskControl;

/// <summary>
/// 交易统计
/// 跟踪交易记录，计算统计指标
/// </summary>
public class TradingStatistics
{
    private readonly List<TradeRecord> _trades = new();
    private int _consecutiveLosses;
    private int _consecutiveWins;
    
    public int TotalTrades => _trades.Count;
    public int WinningTrades => _trades.Count(t => t.Profit > 0);
    public int LosingTrades => _trades.Count(t => t.Profit < 0);
    public decimal WinRate => TotalTrades > 0 ? (decimal)WinningTrades / TotalTrades : 0;
    public int ConsecutiveLosses => _consecutiveLosses;
    public int ConsecutiveWins => _consecutiveWins;
    
    /// <summary>
    /// 记录交易
    /// </summary>
    public void RecordTrade(Order order)
    {
        if (!order.Profit.HasValue || order.Status != OrderStatus.CLOSED)
        {
            return;
        }
        
        var trade = new TradeRecord
        {
            OrderId = order.Id,
            OpenTime = order.OpenTime,
            CloseTime = order.CloseTime ?? DateTime.UtcNow,
            Side = order.Side,
            Quantity = order.Quantity,
            EntryPrice = order.OpenPrice,
            ExitPrice = order.ClosePrice ?? 0,
            Profit = order.Profit.Value,
            Fee = order.Fee,
            IsWin = order.Profit.Value > 0
        };
        
        _trades.Add(trade);
        
        // 更新连续盈亏
        if (trade.IsWin)
        {
            _consecutiveWins++;
            _consecutiveLosses = 0;
        }
        else
        {
            _consecutiveLosses++;
            _consecutiveWins = 0;
        }
        
        Console.WriteLine($"📊 [TradingStatistics] 记录交易: " +
            $"{(trade.IsWin ? "盈利" : "亏损")} {trade.Profit:F2} USDT");
    }
    
    /// <summary>
    /// 获取今日交易次数
    /// </summary>
    public int GetTodayTradeCount()
    {
        var today = DateTime.UtcNow.Date;
        return _trades.Count(t => t.CloseTime.Date == today);
    }
    
    /// <summary>
    /// 获取统计摘要
    /// </summary>
    public StatisticsSummary GetSummary()
    {
        if (_trades.Count == 0)
        {
            return new StatisticsSummary();
        }
        
        var winningTrades = _trades.Where(t => t.IsWin).ToList();
        var losingTrades = _trades.Where(t => !t.IsWin).ToList();
        
        return new StatisticsSummary
        {
            TotalTrades = TotalTrades,
            WinningTrades = WinningTrades,
            LosingTrades = LosingTrades,
            WinRate = WinRate,
            TotalProfit = _trades.Sum(t => t.Profit),
            TotalFees = _trades.Sum(t => t.Fee),
            AverageProfit = _trades.Average(t => t.Profit),
            AverageWin = winningTrades.Any() ? winningTrades.Average(t => t.Profit) : 0,
            AverageLoss = losingTrades.Any() ? losingTrades.Average(t => t.Profit) : 0,
            LargestWin = winningTrades.Any() ? winningTrades.Max(t => t.Profit) : 0,
            LargestLoss = losingTrades.Any() ? losingTrades.Min(t => t.Profit) : 0,
            ProfitFactor = CalculateProfitFactor(),
            ConsecutiveLosses = _consecutiveLosses,
            ConsecutiveWins = _consecutiveWins,
            MaxConsecutiveLosses = CalculateMaxConsecutiveLosses(),
            MaxConsecutiveWins = CalculateMaxConsecutiveWins(),
            TodayTrades = GetTodayTradeCount()
        };
    }
    
    /// <summary>
    /// 计算盈利因子
    /// </summary>
    private decimal CalculateProfitFactor()
    {
        var totalWin = _trades.Where(t => t.IsWin).Sum(t => t.Profit);
        var totalLoss = Math.Abs(_trades.Where(t => !t.IsWin).Sum(t => t.Profit));
        
        return totalLoss > 0 ? totalWin / totalLoss : 0;
    }
    
    /// <summary>
    /// 计算最大连续亏损
    /// </summary>
    private int CalculateMaxConsecutiveLosses()
    {
        int maxLosses = 0;
        int currentLosses = 0;
        
        foreach (var trade in _trades)
        {
            if (trade.IsWin)
            {
                currentLosses = 0;
            }
            else
            {
                currentLosses++;
                maxLosses = Math.Max(maxLosses, currentLosses);
            }
        }
        
        return maxLosses;
    }
    
    /// <summary>
    /// 计算最大连续盈利
    /// </summary>
    private int CalculateMaxConsecutiveWins()
    {
        int maxWins = 0;
        int currentWins = 0;
        
        foreach (var trade in _trades)
        {
            if (!trade.IsWin)
            {
                currentWins = 0;
            }
            else
            {
                currentWins++;
                maxWins = Math.Max(maxWins, currentWins);
            }
        }
        
        return maxWins;
    }
    
    /// <summary>
    /// 获取最近的交易记录
    /// </summary>
    public List<TradeRecord> GetRecentTrades(int count = 10)
    {
        return _trades.TakeLast(count).ToList();
    }
    
    /// <summary>
    /// 重置统计
    /// </summary>
    public void Reset()
    {
        _trades.Clear();
        _consecutiveLosses = 0;
        _consecutiveWins = 0;
        Console.WriteLine("📊 [TradingStatistics] 统计已重置");
    }
}

/// <summary>
/// 交易记录
/// </summary>
public class TradeRecord
{
    public string OrderId { get; set; } = string.Empty;
    public DateTime OpenTime { get; set; }
    public DateTime CloseTime { get; set; }
    public OrderSide Side { get; set; }
    public decimal Quantity { get; set; }
    public decimal EntryPrice { get; set; }
    public decimal ExitPrice { get; set; }
    public decimal Profit { get; set; }
    public decimal Fee { get; set; }
    public bool IsWin { get; set; }
}

/// <summary>
/// 统计摘要
/// </summary>
public class StatisticsSummary
{
    public int TotalTrades { get; set; }
    public int WinningTrades { get; set; }
    public int LosingTrades { get; set; }
    public decimal WinRate { get; set; }
    public decimal TotalProfit { get; set; }
    public decimal TotalFees { get; set; }
    public decimal AverageProfit { get; set; }
    public decimal AverageWin { get; set; }
    public decimal AverageLoss { get; set; }
    public decimal LargestWin { get; set; }
    public decimal LargestLoss { get; set; }
    public decimal ProfitFactor { get; set; }
    public int ConsecutiveLosses { get; set; }
    public int ConsecutiveWins { get; set; }
    public int MaxConsecutiveLosses { get; set; }
    public int MaxConsecutiveWins { get; set; }
    public int TodayTrades { get; set; }
}

