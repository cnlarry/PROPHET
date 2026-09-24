using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Prophet.Client.Database;
using Prophet.Client.Models;

namespace Prophet.Client.Trading.Storage;

/// <summary>
/// 实盘订单存储
/// 将实盘订单持久化到主数据库 PROPHET.db
/// </summary>
public class LiveOrderStorage
{
    public LiveOrderStorage()
    {
        Console.WriteLine("[LiveOrderStorage] 使用主数据库 PROPHET.db");
    }
    
    /// <summary>
    /// 保存订单
    /// </summary>
    public async Task SaveOrderAsync(Order order)
    {
        var sql = @"
            INSERT OR REPLACE INTO live_orders (
                id, session_id, symbol, side, type, status,
                leverage, margin, quantity, open_price, open_time,
                filled_price, filled_time, close_price, close_time,
                fee, funding_fee, profit,
                take_profit, stop_loss, liquidation_price,
                remarks, updated_at
            ) VALUES (
                @Id, @BacktestId, @Symbol, @Side, @Type, @Status,
                @Leverage, @Margin, @Quantity, @OpenPrice, @OpenTime,
                @FilledPrice, @FilledTime, @ClosePrice, @CloseTime,
                @Fee, @FundingFee, @Profit,
                @TakeProfit, @StopLoss, @LiquidationPrice,
                @Remarks, datetime('now')
            )
        ";
        
        await DBHelper.ExecuteAsync(sql, new
        {
            order.Id,
            order.BacktestId,
            Symbol = ExtractSymbol(order),
            Side = order.Side.ToString(),
            Type = order.Type.ToString(),
            Status = order.Status.ToString(),
            order.Leverage,
            order.Margin,
            order.Quantity,
            order.OpenPrice,
            OpenTime = order.OpenTime.ToString("yyyy-MM-dd HH:mm:ss"),
            order.FilledPrice,
            FilledTime = order.FilledTime?.ToString("yyyy-MM-dd HH:mm:ss"),
            order.ClosePrice,
            CloseTime = order.CloseTime?.ToString("yyyy-MM-dd HH:mm:ss"),
            order.Fee,
            order.FundingFee,
            order.Profit,
            order.TakeProfit,
            order.StopLoss,
            order.LiquidationPrice,
            order.Remarks
        });
        
        Console.WriteLine($"💾 [LiveOrderStorage] 订单已保存: {order.Id}");
    }
    
    /// <summary>
    /// 批量保存订单
    /// </summary>
    public async Task SaveOrdersAsync(IEnumerable<Order> orders)
    {
        foreach (var order in orders)
        {
            await SaveOrderAsync(order);
        }
    }
    
    /// <summary>
    /// 获取会话的所有订单
    /// </summary>
    public async Task<List<Order>> GetOrdersBySessionAsync(string sessionId)
    {
        var sql = @"
            SELECT * FROM live_orders 
            WHERE session_id = @SessionId 
            ORDER BY open_time
        ";
        
        var rows = await DBHelper.QueryAsync<dynamic>(sql, new { SessionId = sessionId });
        
        return rows.Select(MapToOrder).ToList();
    }
    
    /// <summary>
    /// 获取未完成订单
    /// </summary>
    public async Task<List<Order>> GetOpenOrdersAsync(string sessionId)
    {
        var sql = @"
            SELECT * FROM live_orders 
            WHERE session_id = @SessionId 
              AND status NOT IN ('Closed', 'Cancelled')
            ORDER BY open_time
        ";
        
        var rows = await DBHelper.QueryAsync<dynamic>(sql, new { SessionId = sessionId });
        
        return rows.Select(MapToOrder).ToList();
    }
    
    /// <summary>
    /// 创建交易会话
    /// </summary>
    public async Task CreateSessionAsync(LiveSession session)
    {
        var sql = @"
            INSERT INTO live_sessions (
                id, strategy_name, symbol, leverage, initial_capital,
                start_time, status
            ) VALUES (
                @Id, @StrategyName, @Symbol, @Leverage, @InitialCapital,
                @StartTime, @Status
            )
        ";
        
        await DBHelper.ExecuteAsync(sql, new
        {
            session.Id,
            session.StrategyName,
            session.Symbol,
            session.Leverage,
            session.InitialCapital,
            StartTime = session.StartTime.ToString("yyyy-MM-dd HH:mm:ss"),
            Status = session.Status
        });
        
        Console.WriteLine($"✅ [LiveOrderStorage] 交易会话已创建: {session.Id}");
    }
    
    /// <summary>
    /// 更新交易会话
    /// </summary>
    public async Task UpdateSessionAsync(LiveSession session)
    {
        var sql = @"
            UPDATE live_sessions SET
                end_time = @EndTime,
                status = @Status,
                final_equity = @FinalEquity,
                total_profit = @TotalProfit,
                total_fees = @TotalFees,
                total_trades = @TotalTrades,
                winning_trades = @WinningTrades,
                losing_trades = @LosingTrades
            WHERE id = @Id
        ";
        
        await DBHelper.ExecuteAsync(sql, new
        {
            session.Id,
            EndTime = session.EndTime?.ToString("yyyy-MM-dd HH:mm:ss"),
            Status = session.Status,
            session.FinalEquity,
            session.TotalProfit,
            session.TotalFees,
            session.TotalTrades,
            session.WinningTrades,
            session.LosingTrades
        });
    }
    
    /// <summary>
    /// 获取所有会话
    /// </summary>
    public async Task<List<LiveSession>> GetAllSessionsAsync()
    {
        var sql = "SELECT * FROM live_sessions ORDER BY start_time DESC";
        var rows = await DBHelper.QueryAsync<dynamic>(sql);
        
        return rows.Select(MapToSession).ToList();
    }
    
    /// <summary>
    /// 映射到Order对象
    /// </summary>
    private Order MapToOrder(dynamic row)
    {
        return new Order
        {
            Id = row.id,
            BacktestId = row.session_id,
            Side = Enum.Parse<OrderSide>(row.side),
            Type = Enum.Parse<OrderType>(row.type),
            Status = Enum.Parse<OrderStatus>(row.status),
            Leverage = row.leverage,
            Margin = row.margin,
            Quantity = row.quantity,
            OpenPrice = row.open_price,
            OpenTime = DateTime.Parse(row.open_time),
            FilledPrice = row.filled_price,
            FilledTime = row.filled_time != null ? DateTime.Parse(row.filled_time) : null,
            ClosePrice = row.close_price,
            CloseTime = row.close_time != null ? DateTime.Parse(row.close_time) : null,
            Fee = row.fee,
            FundingFee = row.funding_fee,
            Profit = row.profit,
            TakeProfit = row.take_profit,
            StopLoss = row.stop_loss,
            LiquidationPrice = row.liquidation_price,
            Remarks = row.remarks
        };
    }
    
    /// <summary>
    /// 映射到LiveSession对象
    /// </summary>
    private LiveSession MapToSession(dynamic row)
    {
        return new LiveSession
        {
            Id = row.id,
            StrategyName = row.strategy_name,
            Symbol = row.symbol,
            Leverage = row.leverage,
            InitialCapital = row.initial_capital,
            StartTime = DateTime.Parse(row.start_time),
            EndTime = row.end_time != null ? DateTime.Parse(row.end_time) : null,
            Status = row.status,
            FinalEquity = row.final_equity,
            TotalProfit = row.total_profit,
            TotalFees = row.total_fees,
            TotalTrades = row.total_trades,
            WinningTrades = row.winning_trades,
            LosingTrades = row.losing_trades
        };
    }
    
    /// <summary>
    /// 从订单中提取交易对
    /// </summary>
    private string ExtractSymbol(Order order)
    {
        // 从Remarks或其他字段提取交易对
        // 实际实现需要根据具体情况调整
        return order.Remarks?.Split(' ').FirstOrDefault() ?? "UNKNOWN";
    }
}

/// <summary>
/// 实盘交易会话
/// </summary>
public class LiveSession
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string StrategyName { get; set; } = string.Empty;
    public string Symbol { get; set; } = string.Empty;
    public decimal Leverage { get; set; }
    public decimal InitialCapital { get; set; }
    public DateTime StartTime { get; set; } = DateTime.UtcNow;
    public DateTime? EndTime { get; set; }
    public string Status { get; set; } = "Active"; // Active, Paused, Stopped
    public decimal? FinalEquity { get; set; }
    public decimal? TotalProfit { get; set; }
    public decimal? TotalFees { get; set; }
    public int TotalTrades { get; set; }
    public int WinningTrades { get; set; }
    public int LosingTrades { get; set; }
}

