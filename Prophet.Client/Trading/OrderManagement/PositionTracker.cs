using System;
using System.Collections.Generic;
using System.Linq;
using Prophet.Client.Models;

namespace Prophet.Client.Trading.OrderManagement;

/// <summary>
/// 仓位跟踪器
/// 跟踪和管理所有持仓，提供仓位分析功能
/// </summary>
public class PositionTracker
{
    private readonly Dictionary<string, PositionInfo> _positions = new();
    
    /// <summary>
    /// 添加或更新仓位
    /// </summary>
    public void UpdatePosition(Order order)
    {
        var symbol = GetSymbolFromOrder(order);
        
        if (!_positions.ContainsKey(symbol))
        {
            _positions[symbol] = new PositionInfo
            {
                Symbol = symbol,
                Orders = new List<Order>()
            };
        }
        
        var position = _positions[symbol];
        
        // 添加订单
        position.Orders.Add(order);
        
        // 重新计算仓位
        RecalculatePosition(position);
        
        Console.WriteLine($"📊 [PositionTracker] 更新仓位: {symbol}");
        Console.WriteLine($"   数量: {position.TotalQuantity:F8}");
        Console.WriteLine($"   均价: {position.AveragePrice:F2}");
        Console.WriteLine($"   未实现盈亏: {position.UnrealizedPnl:F2}");
    }
    
    /// <summary>
    /// 关闭仓位
    /// </summary>
    public void ClosePosition(string symbol, Order closeOrder)
    {
        if (!_positions.ContainsKey(symbol))
        {
            return;
        }
        
        var position = _positions[symbol];
        position.Orders.Add(closeOrder);
        
        RecalculatePosition(position);
        
        if (position.TotalQuantity == 0)
        {
            position.IsClosed = true;
            position.CloseTime = DateTime.UtcNow;
            
            Console.WriteLine($"✅ [PositionTracker] 仓位已平: {symbol}");
            Console.WriteLine($"   总盈亏: {position.RealizedPnl:F2} USDT");
        }
    }
    
    /// <summary>
    /// 获取仓位信息
    /// </summary>
    public PositionInfo? GetPosition(string symbol)
    {
        return _positions.TryGetValue(symbol, out var position) ? position : null;
    }
    
    /// <summary>
    /// 获取所有活跃仓位
    /// </summary>
    public List<PositionInfo> GetActivePositions()
    {
        return _positions.Values.Where(p => !p.IsClosed && p.TotalQuantity != 0).ToList();
    }
    
    /// <summary>
    /// 获取所有仓位（包括已平仓）
    /// </summary>
    public List<PositionInfo> GetAllPositions()
    {
        return _positions.Values.ToList();
    }
    
    /// <summary>
    /// 重新计算仓位
    /// </summary>
    private void RecalculatePosition(PositionInfo position)
    {
        var buyOrders = position.Orders.Where(o => o.Side == OrderSide.BUY).ToList();
        var sellOrders = position.Orders.Where(o => o.Side == OrderSide.SELL).ToList();
        
        // 计算总买入和卖出数量
        var totalBuyQty = buyOrders.Sum(o => o.Quantity);
        var totalSellQty = sellOrders.Sum(o => o.Quantity);
        
        // 计算净持仓
        position.TotalQuantity = totalBuyQty - totalSellQty;
        position.Side = position.TotalQuantity > 0 ? OrderSide.BUY : 
                       position.TotalQuantity < 0 ? OrderSide.SELL : 
                       OrderSide.BUY; // 默认
        
        // 计算平均开仓价
        if (Math.Abs(position.TotalQuantity) > 0)
        {
            var totalBuyValue = buyOrders.Sum(o => o.Quantity * o.OpenPrice);
            var totalSellValue = sellOrders.Sum(o => o.Quantity * o.OpenPrice);
            
            if (position.TotalQuantity > 0)
            {
                // 多仓：平均买入价
                position.AveragePrice = totalBuyQty > 0 ? totalBuyValue / totalBuyQty : 0;
            }
            else if (position.TotalQuantity < 0)
            {
                // 空仓：平均卖出价
                position.AveragePrice = totalSellQty > 0 ? totalSellValue / totalSellQty : 0;
            }
        }
        
        // 计算已实现盈亏
        position.RealizedPnl = position.Orders
            .Where(o => o.Profit.HasValue)
            .Sum(o => o.Profit ?? 0m);
        
        // 计算总保证金
        position.TotalMargin = position.Orders
            .Where(o => o.Status != OrderStatus.CLOSED)
            .Sum(o => o.Margin);
        
        // 计算总手续费
        position.TotalFees = position.Orders.Sum(o => o.Fee);
    }
    
    /// <summary>
    /// 从订单中提取交易对（按 Symbol 归集；缺失时回退 UNKNOWN，避免多币种坍缩到同一仓位）
    /// </summary>
    private string GetSymbolFromOrder(Order order)
    {
        return string.IsNullOrWhiteSpace(order.Symbol) ? "UNKNOWN" : order.Symbol;
    }
}

/// <summary>
/// 仓位信息
/// </summary>
public class PositionInfo
{
    /// <summary>
    /// 交易对
    /// </summary>
    public string Symbol { get; set; } = string.Empty;
    
    /// <summary>
    /// 仓位方向
    /// </summary>
    public OrderSide Side { get; set; }
    
    /// <summary>
    /// 总持仓数量（正数=多仓，负数=空仓）
    /// </summary>
    public decimal TotalQuantity { get; set; }
    
    /// <summary>
    /// 平均开仓价
    /// </summary>
    public decimal AveragePrice { get; set; }
    
    /// <summary>
    /// 总保证金
    /// </summary>
    public decimal TotalMargin { get; set; }
    
    /// <summary>
    /// 总手续费
    /// </summary>
    public decimal TotalFees { get; set; }
    
    /// <summary>
    /// 已实现盈亏
    /// </summary>
    public decimal RealizedPnl { get; set; }
    
    /// <summary>
    /// 未实现盈亏
    /// </summary>
    public decimal UnrealizedPnl { get; set; }
    
    /// <summary>
    /// 是否已平仓
    /// </summary>
    public bool IsClosed { get; set; }
    
    /// <summary>
    /// 开仓时间
    /// </summary>
    public DateTime OpenTime { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// 平仓时间
    /// </summary>
    public DateTime? CloseTime { get; set; }
    
    /// <summary>
    /// 相关订单列表
    /// </summary>
    public List<Order> Orders { get; set; } = new();
    
    /// <summary>
    /// 计算当前价格下的未实现盈亏
    /// </summary>
    public decimal CalculateUnrealizedPnl(decimal currentPrice)
    {
        if (TotalQuantity == 0)
        {
            return 0;
        }
        
        if (Side == OrderSide.BUY)
        {
            // 多仓：(当前价 - 平均价) × 数量
            return (currentPrice - AveragePrice) * TotalQuantity;
        }
        else
        {
            // 空仓：(平均价 - 当前价) × 数量
            return (AveragePrice - currentPrice) * Math.Abs(TotalQuantity);
        }
    }
}

