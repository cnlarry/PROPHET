using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Dapper;
using Prophet.Client.Database;
using Prophet.Client.Trading.Models;
using Prophet.Client.Trading.RiskControl;

namespace Prophet.Client.Trading.Storage;

/// <summary>
/// 实盘交易实例存储
/// 负责实例配置和快照的持久化
/// 使用主数据库 PROPHET.db
/// </summary>
public class TradingInstanceStorage
{
    public TradingInstanceStorage()
    {
        Console.WriteLine("[TradingInstanceStorage] 使用主数据库 PROPHET.db");
    }
    
    /// <summary>
    /// 保存实例配置
    /// </summary>
    public async Task SaveInstanceAsync(TradingInstanceConfig config)
    {
        var sql = @"
            INSERT OR REPLACE INTO trading_instances 
            (id, name, exchange, symbol, timeframe, strategy_name, strategy_code, 
             risk_config, capital_config, exchange_config, is_enabled, created_at, last_run_at)
            VALUES 
            (@Id, @Name, @Exchange, @Symbol, @Timeframe, @StrategyName, @StrategyCode,
             @RiskConfig, @CapitalConfig, @ExchangeConfig, @IsEnabled, @CreatedAt, @LastRunAt)
        ";
        
        await DBHelper.ExecuteAsync(sql, new
        {
            Id = config.Id.ToString(),
            config.Name,
            config.Exchange,
            config.Symbol,
            config.Timeframe,
            config.StrategyName,
            config.StrategyCode,
            RiskConfig = JsonSerializer.Serialize(config.RiskConfig),
            CapitalConfig = JsonSerializer.Serialize(config.CapitalConfig),
            ExchangeConfig = JsonSerializer.Serialize(config.ExchangeConfig),
            IsEnabled = config.IsEnabled ? 1 : 0,
            CreatedAt = config.CreatedAt.ToString("O"),
            LastRunAt = config.LastRunAt?.ToString("O")
        });
    }
    
    /// <summary>
    /// 加载实例配置
    /// </summary>
    public async Task<TradingInstanceConfig?> LoadInstanceAsync(Guid id)
    {
        var sql = "SELECT * FROM trading_instances WHERE id = @Id";
        var result = await DBHelper.QueryFirstOrDefaultAsync<TradingInstanceDbModel>(sql, new { Id = id.ToString() });
        
        return result != null ? MapToConfig(result) : null;
    }
    
    /// <summary>
    /// 加载所有实例配置
    /// </summary>
    public async Task<List<TradingInstanceConfig>> LoadAllInstancesAsync()
    {
        var sql = "SELECT * FROM trading_instances ORDER BY created_at DESC";
        var results = await DBHelper.QueryAsync<TradingInstanceDbModel>(sql);
        
        return results.Select(MapToConfig).ToList();
    }
    
    /// <summary>
    /// 删除实例（快照会通过外键级联删除）
    /// </summary>
    public async Task DeleteInstanceAsync(Guid id)
    {
        var sql = "DELETE FROM trading_instances WHERE id = @Id";
        await DBHelper.ExecuteAsync(sql, new { Id = id.ToString() });
    }
    
    /// <summary>
    /// 保存快照
    /// </summary>
    public async Task SaveSnapshotAsync(TradingInstanceSnapshot snapshot)
    {
        var sql = @"
            INSERT INTO instance_snapshots 
            (instance_id, timestamp, status, total_equity, available_balance, used_margin,
             unrealized_pnl, realized_pnl, today_pnl, today_pnl_percent, position_count, open_order_count,
             current_drawdown, max_drawdown, risk_level, emergency_stop_activated,
             total_trades, today_trades, win_rate, consecutive_losses, consecutive_wins,
             error_message, running_duration_seconds)
            VALUES 
            (@InstanceId, @Timestamp, @Status, @TotalEquity, @AvailableBalance, @UsedMargin,
             @UnrealizedPnL, @RealizedPnL, @TodayPnL, @TodayPnLPercent, @PositionCount, @OpenOrderCount,
             @CurrentDrawdown, @MaxDrawdown, @RiskLevel, @EmergencyStopActivated,
             @TotalTrades, @TodayTrades, @WinRate, @ConsecutiveLosses, @ConsecutiveWins,
             @ErrorMessage, @RunningDurationSeconds)
        ";
        
        await DBHelper.ExecuteAsync(sql, new
        {
            InstanceId = snapshot.InstanceId.ToString(),
            Timestamp = snapshot.Timestamp.ToString("O"),
            Status = (int)snapshot.Status,
            TotalEquity = snapshot.TotalEquity,
            AvailableBalance = snapshot.AvailableBalance,
            UsedMargin = snapshot.UsedMargin,
            UnrealizedPnL = snapshot.UnrealizedPnL,
            RealizedPnL = snapshot.RealizedPnL,
            TodayPnL = snapshot.TodayPnL,
            TodayPnLPercent = snapshot.TodayPnLPercent,
            PositionCount = snapshot.PositionCount,
            OpenOrderCount = snapshot.OpenOrderCount,
            CurrentDrawdown = snapshot.CurrentDrawdown,
            MaxDrawdown = snapshot.MaxDrawdown,
            RiskLevel = (int)snapshot.RiskLevel,
            EmergencyStopActivated = snapshot.EmergencyStopActivated ? 1 : 0,
            TotalTrades = snapshot.TotalTrades,
            TodayTrades = snapshot.TodayTrades,
            WinRate = snapshot.WinRate,
            ConsecutiveLosses = snapshot.ConsecutiveLosses,
            ConsecutiveWins = snapshot.ConsecutiveWins,
            ErrorMessage = snapshot.ErrorMessage,
            RunningDurationSeconds = snapshot.RunningDurationSeconds
        });
    }
    
    /// <summary>
    /// 获取快照历史
    /// </summary>
    public async Task<List<TradingInstanceSnapshot>> GetSnapshotsAsync(
        Guid instanceId, 
        DateTime from, 
        DateTime to,
        int limit = 1000)
    {
        var sql = @"
            SELECT * FROM instance_snapshots 
            WHERE instance_id = @InstanceId 
            AND timestamp >= @From 
            AND timestamp <= @To 
            ORDER BY timestamp DESC
            LIMIT @Limit
        ";
        
        var results = await DBHelper.QueryAsync<SnapshotDbModel>(sql, new
        {
            InstanceId = instanceId.ToString(),
            From = from.ToString("O"),
            To = to.ToString("O"),
            Limit = limit
        });
        
        return results.Select(MapToSnapshot).ToList();
    }
    
    /// <summary>
    /// 获取最新快照
    /// </summary>
    public async Task<TradingInstanceSnapshot?> GetLatestSnapshotAsync(Guid instanceId)
    {
        var sql = @"
            SELECT * FROM instance_snapshots 
            WHERE instance_id = @InstanceId 
            ORDER BY timestamp DESC 
            LIMIT 1
        ";
        
        var result = await DBHelper.QueryFirstOrDefaultAsync<SnapshotDbModel>(sql, new { InstanceId = instanceId.ToString() });
        
        return result != null ? MapToSnapshot(result) : null;
    }
    
    /// <summary>
    /// 清理旧快照（保留指定天数）
    /// </summary>
    public async Task<int> CleanupOldSnapshotsAsync(int keepDays = 30)
    {
        var sql = @"
            DELETE FROM instance_snapshots 
            WHERE timestamp < @CutoffDate
        ";
        
        var cutoffDate = DateTime.UtcNow.AddDays(-keepDays);
        return await DBHelper.ExecuteAsync(sql, new { CutoffDate = cutoffDate.ToString("O") });
    }
    
    // ========== 私有辅助方法 ==========
    
    /// <summary>
    /// 映射数据库模型到配置对象
    /// </summary>
    private static TradingInstanceConfig MapToConfig(TradingInstanceDbModel model)
    {
        return new TradingInstanceConfig
        {
            Id = Guid.Parse(model.id),
            Name = model.name,
            Exchange = model.exchange,
            Symbol = model.symbol,
            Timeframe = model.timeframe,
            StrategyName = model.strategy_name,
            StrategyCode = model.strategy_code,
            RiskConfig = JsonSerializer.Deserialize<RiskConfig>(model.risk_config) ?? new RiskConfig(),
            CapitalConfig = JsonSerializer.Deserialize<CapitalConfig>(model.capital_config) ?? new CapitalConfig(),
            ExchangeConfig = JsonSerializer.Deserialize<ExchangeConfig>(model.exchange_config) ?? new ExchangeConfig(),
            IsEnabled = model.is_enabled == 1,
            CreatedAt = DateTime.Parse(model.created_at),
            LastRunAt = string.IsNullOrEmpty(model.last_run_at) ? null : DateTime.Parse(model.last_run_at)
        };
    }
    
    /// <summary>
    /// 映射数据库模型到快照对象
    /// </summary>
    private static TradingInstanceSnapshot MapToSnapshot(SnapshotDbModel model)
    {
        return new TradingInstanceSnapshot
        {
            Id = model.id,
            InstanceId = Guid.Parse(model.instance_id),
            Timestamp = DateTime.Parse(model.timestamp),
            Status = (TradingInstanceStatus)model.status,
            TotalEquity = model.total_equity,
            AvailableBalance = model.available_balance,
            UsedMargin = model.used_margin,
            UnrealizedPnL = model.unrealized_pnl,
            RealizedPnL = model.realized_pnl,
            TodayPnL = model.today_pnl,
            TodayPnLPercent = model.today_pnl_percent,
            PositionCount = model.position_count,
            OpenOrderCount = model.open_order_count,
            CurrentDrawdown = model.current_drawdown,
            MaxDrawdown = model.max_drawdown,
            RiskLevel = (RiskLevel)model.risk_level,
            EmergencyStopActivated = model.emergency_stop_activated == 1,
            TotalTrades = model.total_trades,
            TodayTrades = model.today_trades,
            WinRate = model.win_rate,
            ConsecutiveLosses = model.consecutive_losses,
            ConsecutiveWins = model.consecutive_wins,
            ErrorMessage = model.error_message,
            RunningDurationSeconds = model.running_duration_seconds
        };
    }
    
    // ========== 数据库模型 ==========
    
    /// <summary>
    /// 实例配置数据库模型
    /// </summary>
    private class TradingInstanceDbModel
    {
        public string id { get; set; } = string.Empty;
        public string name { get; set; } = string.Empty;
        public string exchange { get; set; } = string.Empty;
        public string symbol { get; set; } = string.Empty;
        public string timeframe { get; set; } = string.Empty;
        public string strategy_name { get; set; } = string.Empty;
        public string strategy_code { get; set; } = string.Empty;
        public string risk_config { get; set; } = string.Empty;
        public string capital_config { get; set; } = string.Empty;
        public string exchange_config { get; set; } = string.Empty;
        public int is_enabled { get; set; }
        public string created_at { get; set; } = string.Empty;
        public string? last_run_at { get; set; }
    }
    
    /// <summary>
    /// 快照数据库模型
    /// </summary>
    private class SnapshotDbModel
    {
        public long id { get; set; }
        public string instance_id { get; set; } = string.Empty;
        public string timestamp { get; set; } = string.Empty;
        public int status { get; set; }
        public decimal total_equity { get; set; }
        public decimal available_balance { get; set; }
        public decimal used_margin { get; set; }
        public decimal unrealized_pnl { get; set; }
        public decimal realized_pnl { get; set; }
        public decimal today_pnl { get; set; }
        public decimal today_pnl_percent { get; set; }
        public int position_count { get; set; }
        public int open_order_count { get; set; }
        public decimal current_drawdown { get; set; }
        public decimal max_drawdown { get; set; }
        public int risk_level { get; set; }
        public int emergency_stop_activated { get; set; }
        public int total_trades { get; set; }
        public int today_trades { get; set; }
        public decimal win_rate { get; set; }
        public int consecutive_losses { get; set; }
        public int consecutive_wins { get; set; }
        public string? error_message { get; set; }
        public long running_duration_seconds { get; set; }
    }
}

