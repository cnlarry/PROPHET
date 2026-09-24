using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Prophet.Client.Trading.Exchanges;

namespace Prophet.Client.Trading.RiskControl;

/// <summary>
/// 仓位监控器
/// 监控持仓风险，计算风险指标
/// </summary>
public class PositionMonitor
{
    private readonly IExchange _exchange;
    private readonly RiskConfig _config;
    
    public event EventHandler<PositionRiskEventArgs>? HighRiskDetected;
    
    public PositionMonitor(IExchange exchange, RiskConfig config)
    {
        _exchange = exchange ?? throw new ArgumentNullException(nameof(exchange));
        _config = config ?? throw new ArgumentNullException(nameof(config));
    }
    
    /// <summary>
    /// 获取风险指标
    /// </summary>
    public async Task<PositionRiskMetrics> GetRiskMetricsAsync()
    {
        var positions = await _exchange.GetPositionsAsync();
        var account = await _exchange.GetAccountInfoAsync();
        
        var metrics = new PositionRiskMetrics
        {
            ActivePositionCount = positions.Count(p => p.Quantity > 0),
            TotalPositionValue = 0,
            TotalMargin = 0,
            TotalUnrealizedPnl = 0,
            PositionDetails = new List<PositionRiskDetail>()
        };
        
        foreach (var position in positions.Where(p => p.Quantity > 0))
        {
            var positionValue = position.Quantity * position.MarkPrice;
            var unrealizedPnl = position.UnrealizedPnl;
            
            metrics.TotalPositionValue += positionValue;
            metrics.TotalMargin += position.Margin;
            metrics.TotalUnrealizedPnl += unrealizedPnl;
            
            // 计算单个仓位的风险指标
            var detail = new PositionRiskDetail
            {
                Symbol = position.Symbol,
                Side = position.Side.ToString(),
                Quantity = position.Quantity,
                EntryPrice = position.EntryPrice,
                MarkPrice = position.MarkPrice,
                UnrealizedPnl = unrealizedPnl,
                Leverage = position.Leverage,
                Margin = position.Margin,
                LiquidationPrice = position.LiquidationPrice,
                RiskPercent = CalculatePositionRisk(position, account.TotalBalance)
            };
            
            metrics.PositionDetails.Add(detail);
            
            // 检查单个仓位风险
            if (detail.RiskPercent > _config.MaxPositionPerSymbol)
            {
                RaiseHighRisk($"单个仓位风险过高: {position.Symbol}",
                    $"风险占比 {detail.RiskPercent:P2} 超过限制 {_config.MaxPositionPerSymbol:P2}");
            }
        }
        
        // 计算总体风险
        metrics.TotalRiskPercent = metrics.TotalMargin / account.TotalBalance;
        metrics.LeverageUtilization = metrics.TotalPositionValue / account.TotalBalance;
        
        // 检查总体风险
        if (metrics.TotalRiskPercent > _config.MaxPositionRiskPercent)
        {
            RaiseHighRisk("总体仓位风险过高",
                $"风险占比 {metrics.TotalRiskPercent:P2} 超过限制 {_config.MaxPositionRiskPercent:P2}");
        }
        
        return metrics;
    }
    
    /// <summary>
    /// 计算单个仓位的风险
    /// </summary>
    private decimal CalculatePositionRisk(Models.Position position, decimal totalBalance)
    {
        // 风险 = 保证金 / 总余额
        return position.Margin / totalBalance;
    }
    
    /// <summary>
    /// 计算到强平价的距离
    /// </summary>
    public decimal CalculateLiquidationDistance(Models.Position position)
    {
        var distance = Math.Abs(position.MarkPrice - position.LiquidationPrice);
        return distance / position.MarkPrice;
    }
    
    /// <summary>
    /// 检查是否接近强平
    /// </summary>
    public async Task<bool> IsNearLiquidationAsync(decimal warningThreshold = 0.05m)
    {
        var positions = await _exchange.GetPositionsAsync();
        
        foreach (var position in positions.Where(p => p.Quantity > 0))
        {
            var distance = CalculateLiquidationDistance(position);
            
            if (distance < warningThreshold)
            {
                Console.WriteLine($"⚠️ [PositionMonitor] 接近强平: {position.Symbol} " +
                    $"距离强平价 {distance:P2}");
                
                RaiseHighRisk("接近强平价",
                    $"{position.Symbol} 距离强平价仅 {distance:P2}");
                
                return true;
            }
        }
        
        return false;
    }
    
    private void RaiseHighRisk(string type, string description)
    {
        Console.WriteLine($"⚠️ [PositionMonitor] 高风险: {type} - {description}");
        
        HighRiskDetected?.Invoke(this, new PositionRiskEventArgs
        {
            Type = type,
            Description = description,
            Timestamp = DateTime.UtcNow
        });
    }
}

/// <summary>
/// 仓位风险指标
/// </summary>
public class PositionRiskMetrics
{
    public int ActivePositionCount { get; set; }
    public decimal TotalPositionValue { get; set; }
    public decimal TotalMargin { get; set; }
    public decimal TotalUnrealizedPnl { get; set; }
    public decimal TotalRiskPercent { get; set; }
    public decimal LeverageUtilization { get; set; }
    public List<PositionRiskDetail> PositionDetails { get; set; } = new();
}

/// <summary>
/// 单个仓位风险详情
/// </summary>
public class PositionRiskDetail
{
    public string Symbol { get; set; } = string.Empty;
    public string Side { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal EntryPrice { get; set; }
    public decimal MarkPrice { get; set; }
    public decimal UnrealizedPnl { get; set; }
    public decimal Leverage { get; set; }
    public decimal Margin { get; set; }
    public decimal LiquidationPrice { get; set; }
    public decimal RiskPercent { get; set; }
}

// 事件参数
public class PositionRiskEventArgs : EventArgs
{
    public string Type { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
}

