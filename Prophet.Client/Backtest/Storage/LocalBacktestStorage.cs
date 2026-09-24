using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Data.Sqlite;
using Prophet.Client.Database;
using Prophet.Client.Backtest.Models;
using Prophet.Client.Models;

namespace Prophet.Client.Backtest.Storage;

/// <summary>
/// 本地SQLite回测数据存储
/// </summary>
public class LocalBacktestStorage : IBacktestStorage, IDisposable
{
    private readonly string _dbPath;
    private bool _disposed;
    private int _debugMapCount = 0; // 用于限制调试日志输出
    
    public LocalBacktestStorage(string? dbPath = null)
    {
        // 使用统一的主数据库 PROPHET.db
        _dbPath = dbPath ?? Prophet.Client.Core.AppConfig.Database.BacktestDatabasePath;
        
        // ✅ 表初始化已迁移到 ClientMigrations，不再需要 InitializeDatabase()
    }
    
    /// <summary>
    /// 安全地添加列（如果不存在）
    /// </summary>
    private void AddColumnIfNotExists(SqliteConnection connection, string tableName, string columnName, string columnType)
    {
        try
        {
            // 检查列是否存在
            var columnExists = connection.QueryFirstOrDefault<int>(
                $"SELECT COUNT(*) FROM pragma_table_info('{tableName}') WHERE name = @ColumnName",
                new { ColumnName = columnName }
            );
            
            if (columnExists == 0)
            {
                var sql = $"ALTER TABLE {tableName} ADD COLUMN {columnName} {columnType}";
                connection.Execute(sql);
                Console.WriteLine($"   ✅ 添加列: {tableName}.{columnName}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"   ⚠️ 添加列失败: {tableName}.{columnName} - {ex.Message}");
        }
    }
    
    #region 回测结果保存
    
    /// <summary>
    /// 保存回测结果
    /// </summary>
    public async Task<string> SaveBacktestResultAsync(
        BacktestResult result,
        CancellationToken cancellationToken = default)
    {
        using var connection = (SqliteConnection)DBHelper.CreateConnection(_dbPath);
        
        using var transaction = connection.BeginTransaction();
        
        try
        {
            // 1. 保存主记录
            var sql = @"
                INSERT INTO backtest_runs (
                    id, strategy_id, strategy_name, symbol_key, interval,
                    start_date, end_date, initial_capital, leverage,
                    taker_fee_rate, maker_fee_rate, slippage_rate, parameters,
                    final_equity, total_return, annualized_return, max_drawdown,
                    sharpe_ratio, sortino_ratio, calmar_ratio,
                    total_trades, winning_trades, losing_trades, win_rate,
                    avg_profit, avg_loss, profit_factor,
                    max_consecutive_wins, max_consecutive_losses,
                    max_single_profit, max_single_loss, avg_holding_time_seconds,
                    status, error_message,
                    start_time, end_time, duration_seconds, completed_at,
                    version_id, version_string
                ) VALUES (
                    @Id, @StrategyId, @StrategyName, @SymbolKey, @Interval,
                    @StartDate, @EndDate, @InitialCapital, @Leverage,
                    @TakerFeeRate, @MakerFeeRate, @SlippageRate, @Parameters,
                    @FinalEquity, @TotalReturn, @AnnualizedReturn, @MaxDrawdown,
                    @SharpeRatio, @SortinoRatio, @CalmarRatio,
                    @TotalTrades, @WinningTrades, @LosingTrades, @WinRate,
                    @AvgProfit, @AvgLoss, @ProfitFactor,
                    @MaxConsecutiveWins, @MaxConsecutiveLosses,
                    @MaxSingleProfit, @MaxSingleLoss, @AvgHoldingTimeSeconds,
                    @Status, @ErrorMessage,
                    @StartTime, @EndTime, @DurationSeconds, @CompletedAt,
                    @VersionId, @VersionString
                )
            ";

            await connection.ExecuteAsync(sql, new
            {
                Id = result.BacktestId ?? Guid.NewGuid().ToString(),
                StrategyId = result.StrategyId ?? "unknown",
                StrategyName = result.StrategyName ?? "未命名策略",
                SymbolKey = result.Symbol ?? "BTCUSDT-BINANCE-SWAP",
                Interval = result.Interval ?? "1m",
                StartDate = result.StartTime.ToString("O"),
                EndDate = result.EndTime.ToString("O"),
                InitialCapital = (double)result.InitialCapital,
                Leverage = (double)(result.Config?.Leverage ?? 1m),
                TakerFeeRate = (double)(result.Config?.TakerFeeRate ?? 0.001m),
                MakerFeeRate = (double)(result.Config?.MakerFeeRate ?? 0.0005m),
                SlippageRate = (double)(result.Config?.SlippageRate ?? 0.0005m),
                Parameters = JsonSerializer.Serialize(result.Config?.Parameters ?? new Dictionary<string, object>()),
                FinalEquity = (double)result.FinalEquity,
                TotalReturn = (double)result.TotalReturn,
                AnnualizedReturn = (double)result.AnnualizedReturn,
                MaxDrawdown = (double)result.MaxDrawdown,
                SharpeRatio = (double)result.SharpeRatio,
                SortinoRatio = (double)result.SortinoRatio,
                CalmarRatio = (double)result.CalmarRatio,
                TotalTrades = result.TotalTrades,
                WinningTrades = result.WinningTrades,
                LosingTrades = result.LosingTrades,
                WinRate = (double)result.WinRate,
                AvgProfit = (double)result.AvgProfit,
                AvgLoss = (double)result.AvgLoss,
                ProfitFactor = (double)result.ProfitFactor,
                MaxConsecutiveWins = result.MaxConsecutiveWins,
                MaxConsecutiveLosses = result.MaxConsecutiveLosses,
                MaxSingleProfit = (double)result.MaxSingleProfit,
                MaxSingleLoss = (double)result.MaxSingleLoss,
                AvgHoldingTimeSeconds = result.AvgHoldingTime.TotalSeconds,
                Status = result.Status.ToString(),
                ErrorMessage = result.ErrorMessage,
                StartTime = result.StartTime.ToString("O"),
                EndTime = result.EndTime.ToString("O"),
                DurationSeconds = result.Duration.TotalSeconds,
                CompletedAt = result.CompletedAt.ToString("O"),
                VersionId = result.VersionId,
                VersionString = result.VersionString
            }, transaction);
            
            // 2. 批量保存订单
            if (result.Orders.Count > 0)
            {
                var backtestId = result.BacktestId ?? throw new ArgumentNullException(nameof(result.BacktestId), "回测ID不能为null");
                await SaveOrdersInternalAsync(connection, transaction, backtestId, result.Symbol ?? "BTCUSDT-BINANCE-SWAP", result.Orders);
            }
            
            // 3. 批量保存权益曲线
            if (result.EquityCurve.Count > 0)
            {
                var backtestId = result.BacktestId ?? throw new ArgumentNullException(nameof(result.BacktestId), "回测ID不能为null");
                await SaveEquityCurveInternalAsync(connection, transaction, backtestId, result.EquityCurve);
            }
            
            // 4. 批量保存信号事件（需要返回信号ID以便建立关联）
            var signalIdMap = new Dictionary<SignalEvent, int>(); // 信号对象 -> 数据库ID的映射
            if (result.Signals.Count > 0)
            {
                var backtestId = result.BacktestId ?? throw new ArgumentNullException(nameof(result.BacktestId), "回测ID不能为null");
                signalIdMap = await SaveSignalsInternalAsync(connection, transaction, backtestId, result.Signals);
            }
            
            // 5. 批量保存回撤周期
            if (result.DrawdownPeriods.Count > 0)
            {
                var backtestId = result.BacktestId ?? throw new ArgumentNullException(nameof(result.BacktestId), "回测ID不能为null");
                await SaveDrawdownPeriodsInternalAsync(connection, transaction, backtestId, result.DrawdownPeriods);
            }
            
            // 6. 批量保存订单-信号关联
            // 根据订单的开仓/平仓时间和信号的时间建立关联关系
            var orderSignals = new List<OrderSignal>();
            foreach (var order in result.Orders)
            {
                // 如果订单是OrderExtended类型且已有关联关系，直接使用
                if (order is OrderExtended orderExt && orderExt.Signals.Count > 0)
                {
                    // 更新SignalId为数据库中的实际ID
                    foreach (var orderSignal in orderExt.Signals)
                    {
                        // 需要找到对应的信号对象来获取数据库ID
                        var relatedSignal = result.Signals.FirstOrDefault(s => 
                            Math.Abs((s.Time - order.OpenTime).TotalSeconds) < 1 || 
                            (order.CloseTime.HasValue && Math.Abs((s.Time - order.CloseTime.Value).TotalSeconds) < 1));
                        
                        if (relatedSignal != null && signalIdMap.TryGetValue(relatedSignal, out var signalId))
                        {
                            orderSignals.Add(new OrderSignal
                            {
                                OrderId = order.Id,
                                SignalId = signalId,
                                Action = orderSignal.Action,
                                CreatedAt = orderSignal.CreatedAt
                            });
                        }
                    }
                }
                else
                {
                    // 根据时间匹配建立关联关系
                    // 查找开仓信号（在开仓时间附近）
                    var openSignal = result.Signals
                        .Where(s => s.WasExecuted && 
                                    s.Action == (order.Side == OrderSide.BUY ? SignalAction.BUY : SignalAction.SELL) &&
                                    Math.Abs((s.Time - order.OpenTime).TotalSeconds) < 60) // 1分钟内
                        .OrderBy(s => Math.Abs((s.Time - order.OpenTime).TotalSeconds))
                        .FirstOrDefault();
                    
                    if (openSignal != null && signalIdMap.TryGetValue(openSignal, out var openSignalId))
                    {
                        orderSignals.Add(new OrderSignal
                        {
                            OrderId = order.Id,
                            SignalId = openSignalId,
                            Action = OrderSignalAction.OPEN,
                            CreatedAt = openSignal.Time
                        });
                    }
                    
                    // 查找平仓信号（在平仓时间附近）
                    if (order.CloseTime.HasValue)
                    {
                        var closeSignal = result.Signals
                            .Where(s => s.WasExecuted &&
                                        Math.Abs((s.Time - order.CloseTime.Value).TotalSeconds) < 60) // 1分钟内
                            .OrderBy(s => Math.Abs((s.Time - order.CloseTime.Value).TotalSeconds))
                            .FirstOrDefault();
                        
                        if (closeSignal != null && signalIdMap.TryGetValue(closeSignal, out var closeSignalId))
                        {
                            orderSignals.Add(new OrderSignal
                            {
                                OrderId = order.Id,
                                SignalId = closeSignalId,
                                Action = OrderSignalAction.CLOSE,
                                CreatedAt = closeSignal.Time
                            });
                        }
                    }
                }
            }
            if (orderSignals.Count > 0)
            {
                await SaveOrderSignalsInternalAsync(connection, transaction, orderSignals);
            }
            
            transaction.Commit();
            
            return result.BacktestId ?? throw new InvalidOperationException("回测ID为null");
        }
        catch (Exception ex)
        {
            transaction.Rollback();
            Console.WriteLine($"❌ 保存回测结果失败: {ex.Message}");
            Console.WriteLine($"   异常类型: {ex.GetType().Name}");
            Console.WriteLine($"   堆栈跟踪: {ex.StackTrace}");
            
            // 详细诊断信息
            Console.WriteLine($"   回测ID: {result?.BacktestId ?? "null"}");
            Console.WriteLine($"   策略ID: {result?.StrategyId ?? "null"}");
            Console.WriteLine($"   策略名称: {result?.StrategyName ?? "null"}");
            
            throw new Exception($"保存回测结果到数据库失败: {ex.Message}", ex);
        }
    }
    
    #endregion
    
    #region 批量保存内部方法
    
    private async Task SaveOrdersInternalAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string backtestId,
        string symbolKey,
        List<Order> orders)
    {
        if (orders.Count == 0)
            return;
        
        // 检查是否有重复的订单ID
        var duplicateIds = orders.GroupBy(o => o.Id)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();
        
        if (duplicateIds.Count > 0)
        {
            Console.WriteLine($"⚠️ 警告：发现 {duplicateIds.Count} 个重复的订单ID");
            Console.WriteLine($"   重复ID: {string.Join(", ", duplicateIds.Take(5))}");
            
            // 去重：只保留每个ID的第一个订单
            orders = orders.GroupBy(o => o.Id)
                .Select(g => g.First())
                .ToList();
            
            Console.WriteLine($"   去重后订单数量: {orders.Count}");
        }
        
        var sql = @"
            INSERT OR REPLACE INTO backtest_orders (
                id, backtest_id, symbol_key, side, type, status,
                leverage, margin, quantity,
                open_price, open_time, filled_price, filled_time,
                close_price, close_time,
                fee, funding_fee, profit,
                take_profit, stop_loss, liquidation_price, remarks
            ) VALUES (
                @Id, @BacktestId, @SymbolKey, @Side, @Type, @Status,
                @Leverage, @Margin, @Quantity,
                @OpenPrice, @OpenTime, @FilledPrice, @FilledTime,
                @ClosePrice, @CloseTime,
                @Fee, @FundingFee, @Profit,
                @TakeProfit, @StopLoss, @LiquidationPrice, @Remarks
            )
        ";
        
        var parameters = orders.Select(o => new
        {
            Id = o.Id,
            BacktestId = backtestId,
            SymbolKey = symbolKey,
            Leverage = o.Leverage,
            Margin = o.Margin,
            Side = o.Side.ToString(),
            Type = o.Type.ToString(),
            Status = o.Status.ToString(),
            Quantity = (double)o.Quantity,
            OpenPrice = (double)o.OpenPrice,
            OpenTime = o.OpenTime.ToString("O"),
            FilledPrice = o.FilledPrice.HasValue ? (double?)o.FilledPrice.Value : null,
            FilledTime = o.FilledTime?.ToString("O"),
            ClosePrice = o.ClosePrice.HasValue ? (double?)o.ClosePrice.Value : null,
            CloseTime = o.CloseTime?.ToString("O"),
            Fee = (double)o.Fee,
            FundingFee = (double)o.FundingFee,
            Profit = o.Profit.HasValue ? (double?)o.Profit.Value : null,
            TakeProfit = o.TakeProfit.HasValue ? (double?)o.TakeProfit.Value : null,
            StopLoss = o.StopLoss.HasValue ? (double?)o.StopLoss.Value : null,
            LiquidationPrice = o.LiquidationPrice.HasValue ? (double?)o.LiquidationPrice.Value : null,
            Remarks = o.Remarks
        });
        
        await connection.ExecuteAsync(sql, parameters, transaction);
    }
    
    private async Task SaveEquityCurveInternalAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string backtestId,
        List<EquityPoint> equityCurve)
    {
        var sql = @"
            INSERT INTO backtest_equity_curve (
                backtest_id, time, equity, cash, position
            ) VALUES (
                @BacktestId, @Time, @Equity, @Cash, @Position
            )
        ";
        
        var parameters = equityCurve.Select(e => new
        {
            BacktestId = backtestId,
            Time = e.Time.ToString("O"),
            Equity = (double)e.Equity,
            Cash = (double)e.Cash,
            Position = (double)e.Position
        });
        
        await connection.ExecuteAsync(sql, parameters, transaction);
    }
    
    /// <summary>
    /// 保存信号事件，返回信号对象到数据库ID的映射
    /// </summary>
    private async Task<Dictionary<SignalEvent, int>> SaveSignalsInternalAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string backtestId,
        List<SignalEvent> signals)
    {
        var signalIdMap = new Dictionary<SignalEvent, int>();
        
        if (signals.Count == 0)
            return signalIdMap;
        
        var sql = @"
            INSERT INTO backtest_signals (
                backtest_id, time, action, signal_price,
                was_executed, reason_if_not_executed,
                confidence, strength, description,
                take_profit, stop_loss,
                trend,
                configs_json, indicators_json, indicator_snapshots_json, debug_json,
                candle_index, global_index
            ) VALUES (
                @BacktestId, @Time, @Action, @SignalPrice,
                @WasExecuted, @ReasonIfNotExecuted,
                @Confidence, @Strength, @Description,
                @TakeProfit, @StopLoss,
                @Trend,
                @ConfigsJson, @IndicatorsJson, @IndicatorSnapshotsJson, @DebugJson,
                @CandleIndex, @GlobalIndex
            );
            SELECT last_insert_rowid();
        ";
        
        // 逐个插入信号以获取ID
        foreach (var signal in signals)
        {
            var signalId = await connection.QuerySingleAsync<int>(sql, new
            {
                BacktestId = backtestId,
                Time = signal.Time.ToString("O"),
                Action = signal.Action.ToString(),
                SignalPrice = (double)signal.SignalPrice,
                WasExecuted = signal.WasExecuted ? 1 : 0,
                ReasonIfNotExecuted = signal.ReasonIfNotExecuted,
                Confidence = signal.Confidence,
                Strength = signal.Strength,
                Description = signal.Description,
                TakeProfit = signal.TakeProfit.HasValue ? (double)signal.TakeProfit.Value : (double?)null,
                StopLoss = signal.StopLoss.HasValue ? (double)signal.StopLoss.Value : (double?)null,
                Trend = signal.Trend,
                ConfigsJson = signal.ConfigsJson,
                IndicatorsJson = signal.IndicatorsJson,
                IndicatorSnapshotsJson = signal.IndicatorSnapshotsJson,
                DebugJson = signal.DebugJson,
                CandleIndex = signal.CandleIndex,
                GlobalIndex = signal.GlobalIndex
            }, transaction);
            
            signalIdMap[signal] = signalId;
        }
        
        return signalIdMap;
    }
    
    private async Task SaveDrawdownPeriodsInternalAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string backtestId,
        List<DrawdownPeriod> drawdowns)
    {
        var sql = @"
            INSERT INTO backtest_drawdown_periods (
                backtest_id, start_time, end_time, drawdown_percentage, duration_seconds, recovery_time
            ) VALUES (
                @BacktestId, @StartTime, @EndTime, @DrawdownPercentage, @DurationSeconds, @RecoveryTime
            )
        ";
        
        var parameters = drawdowns.Select(d => new
        {
            BacktestId = backtestId,
            StartTime = d.StartTime.ToString("O"),
            EndTime = d.EndTime.ToString("O"),
            DrawdownPercentage = (double)d.DrawdownPercentage,
            DurationSeconds = d.Duration.TotalSeconds,
            RecoveryTime = (double)d.RecoveryTime
        });
        
        await connection.ExecuteAsync(sql, parameters, transaction);
    }
    
    /// <summary>
    /// 批量保存订单-信号关联 (🔥 Python backtest.py 核心设计)
    /// </summary>
    private async Task SaveOrderSignalsInternalAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        List<OrderSignal> orderSignals)
    {
        if (orderSignals.Count == 0)
            return;
        
        var sql = @"
            INSERT OR REPLACE INTO backtest_order_signals (
                order_id, signal_id, action, created_at
            ) VALUES (
                @OrderId, @SignalId, @Action, @CreatedAt
            )
        ";
        
        var parameters = orderSignals.Select(os => new
        {
            OrderId = os.OrderId,
            SignalId = os.SignalId,
            Action = os.Action.ToString(),
            CreatedAt = os.CreatedAt.ToString("O")
        });
        
        await connection.ExecuteAsync(sql, parameters, transaction);
        Console.WriteLine($"   📎 保存订单-信号关联: {orderSignals.Count} 条");
    }
    
    #endregion
    
    #region 单独保存方法（用于手动保存）
    
    public async Task SaveOrdersAsync(
        string backtestId,
        List<Order> orders,
        CancellationToken cancellationToken = default)
    {
        using var connection = (SqliteConnection)DBHelper.CreateConnection(_dbPath);
        
        using var transaction = connection.BeginTransaction();
        
        try
        {
            var symbolKey = await connection.ExecuteScalarAsync<string?>(
                "SELECT symbol_key FROM backtest_runs WHERE id = @Id",
                new { Id = backtestId },
                transaction
            );

            await SaveOrdersInternalAsync(
                connection,
                transaction,
                backtestId,
                symbolKey ?? "BTCUSDT-BINANCE-SWAP",
                orders
            );
            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }
    
    public async Task SaveEquityCurveAsync(
        string backtestId,
        List<EquityPoint> equityCurve,
        CancellationToken cancellationToken = default)
    {
        using var connection = (SqliteConnection)DBHelper.CreateConnection(_dbPath);
        
        using var transaction = connection.BeginTransaction();
        
        try
        {
            await SaveEquityCurveInternalAsync(connection, transaction, backtestId, equityCurve);
            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }
    
    #endregion
    
    #region 查询方法
    
    /// <summary>
    /// 获取单个回测结果
    /// </summary>
    public async Task<BacktestResult?> GetBacktestResultAsync(
        string backtestId,
        CancellationToken cancellationToken = default)
    {
        using var connection = (SqliteConnection)DBHelper.CreateConnection(_dbPath);
        
        // 1. 查询主记录
        // 显式指定列名，确保Dapper正确映射
        var sql = @"
            SELECT 
                id AS Id, strategy_id AS StrategyId, strategy_name AS StrategyName,
                symbol_key AS Symbol, interval AS Interval,
                start_date AS StartDate, end_date AS EndDate,
                initial_capital AS InitialCapital, final_equity AS FinalEquity,
                total_return AS TotalReturn, annualized_return AS AnnualizedReturn,
                max_drawdown AS MaxDrawdown, sharpe_ratio AS SharpeRatio,
                sortino_ratio AS SortinoRatio, calmar_ratio AS CalmarRatio,
                total_trades AS TotalTrades, winning_trades AS WinningTrades,
                losing_trades AS LosingTrades, win_rate AS WinRate,
                avg_profit AS AvgProfit, avg_loss AS AvgLoss, profit_factor AS ProfitFactor,
                max_consecutive_wins AS MaxConsecutiveWins, max_consecutive_losses AS MaxConsecutiveLosses,
                max_single_profit AS MaxSingleProfit, max_single_loss AS MaxSingleLoss,
                avg_holding_time_seconds AS AvgHoldingTimeSeconds,
                status AS Status, error_message AS ErrorMessage,
                start_time AS StartTime, end_time AS EndTime,
                duration_seconds AS DurationSeconds, completed_at AS CompletedAt,
                version_id AS VersionId, version_string AS VersionString
            FROM backtest_runs 
            WHERE id = @Id";
        var run = await connection.QueryFirstOrDefaultAsync<BacktestRunDto>(sql, new { Id = backtestId });
        
        if (run == null)
            return null;
        
        // 2. 查询订单
        var orders = await GetOrdersAsync(backtestId, cancellationToken);
        
        // 3. 查询权益曲线
        var equityCurve = await GetEquityCurveAsync(backtestId, cancellationToken);
        
        // 4. 查询信号事件
        var signals = await GetSignalsAsync(backtestId, cancellationToken);
        
        // 5. 查询回撤周期
        var drawdowns = await GetDrawdownPeriodsAsync(backtestId, cancellationToken);
        
        // 6. 转换为BacktestResult
        return MapToBacktestResult(run, orders, equityCurve, signals, drawdowns);
    }
    
    /// <summary>
    /// 获取最近的回测记录
    /// </summary>
    public async Task<List<BacktestResult>> GetRecentBacktestsAsync(
        int count = 10,
        CancellationToken cancellationToken = default)
    {
        using var connection = (SqliteConnection)DBHelper.CreateConnection(_dbPath);
        
        // 显式指定列名，确保Dapper正确映射（SQLite列名可能是下划线格式）
        var sql = @"
            SELECT 
                id AS Id, strategy_id AS StrategyId, strategy_name AS StrategyName,
                symbol_key AS Symbol, interval AS Interval,
                start_date AS StartDate, end_date AS EndDate,
                initial_capital AS InitialCapital, final_equity AS FinalEquity,
                total_return AS TotalReturn, annualized_return AS AnnualizedReturn,
                max_drawdown AS MaxDrawdown, sharpe_ratio AS SharpeRatio,
                sortino_ratio AS SortinoRatio, calmar_ratio AS CalmarRatio,
                total_trades AS TotalTrades, winning_trades AS WinningTrades,
                losing_trades AS LosingTrades, win_rate AS WinRate,
                avg_profit AS AvgProfit, avg_loss AS AvgLoss, profit_factor AS ProfitFactor,
                max_consecutive_wins AS MaxConsecutiveWins, max_consecutive_losses AS MaxConsecutiveLosses,
                max_single_profit AS MaxSingleProfit, max_single_loss AS MaxSingleLoss,
                avg_holding_time_seconds AS AvgHoldingTimeSeconds,
                status AS Status, error_message AS ErrorMessage,
                start_time AS StartTime, end_time AS EndTime,
                duration_seconds AS DurationSeconds, completed_at AS CompletedAt,
                version_id AS VersionId, version_string AS VersionString
            FROM backtest_runs 
            ORDER BY created_at DESC 
            LIMIT @Count";
        var runs = await connection.QueryAsync<BacktestRunDto>(sql, new { Count = count });
        
        var results = new List<BacktestResult>();
        
        foreach (var run in runs)
        {
            try
            {
                // 对于列表查询，只加载主记录，不加载详细数据（性能优化）
                results.Add(MapToBacktestResult(run, new List<Order>(), new List<EquityPoint>(), new List<SignalEvent>(), new List<DrawdownPeriod>()));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ 跳过无效的回测记录 {run.Id}: {ex.Message}");
                // 跳过无效记录，继续处理其他记录
            }
        }
        
        return results;
    }
    
    /// <summary>
    /// 按策略ID查询回测记录
    /// </summary>
    public async Task<List<BacktestResult>> GetBacktestsByStrategyAsync(
        string strategyId,
        CancellationToken cancellationToken = default)
    {
        using var connection = (SqliteConnection)DBHelper.CreateConnection(_dbPath);
        
        // 显式指定列名，确保Dapper正确映射
        var sql = @"
            SELECT 
                id AS Id, strategy_id AS StrategyId, strategy_name AS StrategyName,
                symbol_key AS Symbol, interval AS Interval,
                start_date AS StartDate, end_date AS EndDate,
                initial_capital AS InitialCapital, final_equity AS FinalEquity,
                total_return AS TotalReturn, annualized_return AS AnnualizedReturn,
                max_drawdown AS MaxDrawdown, sharpe_ratio AS SharpeRatio,
                sortino_ratio AS SortinoRatio, calmar_ratio AS CalmarRatio,
                total_trades AS TotalTrades, winning_trades AS WinningTrades,
                losing_trades AS LosingTrades, win_rate AS WinRate,
                avg_profit AS AvgProfit, avg_loss AS AvgLoss, profit_factor AS ProfitFactor,
                max_consecutive_wins AS MaxConsecutiveWins, max_consecutive_losses AS MaxConsecutiveLosses,
                max_single_profit AS MaxSingleProfit, max_single_loss AS MaxSingleLoss,
                avg_holding_time_seconds AS AvgHoldingTimeSeconds,
                status AS Status, error_message AS ErrorMessage,
                start_time AS StartTime, end_time AS EndTime,
                duration_seconds AS DurationSeconds, completed_at AS CompletedAt,
                version_id AS VersionId, version_string AS VersionString
            FROM backtest_runs 
            WHERE strategy_id = @StrategyId 
            ORDER BY created_at DESC";
        var runs = await connection.QueryAsync<BacktestRunDto>(sql, new { StrategyId = strategyId });
        
        var results = new List<BacktestResult>();
        
        foreach (var run in runs)
        {
            try
            {
                results.Add(MapToBacktestResult(run, new List<Order>(), new List<EquityPoint>(), new List<SignalEvent>(), new List<DrawdownPeriod>()));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ 跳过无效的回测记录 {run.Id}: {ex.Message}");
                // 跳过无效记录，继续处理其他记录
            }
        }
        
        return results;
    }
    
    /// <summary>
    /// 删除回测记录（级联删除所有相关数据）
    /// </summary>
    public async Task DeleteBacktestAsync(
        string backtestId,
        CancellationToken cancellationToken = default)
    {
        using var connection = (SqliteConnection)DBHelper.CreateConnection(_dbPath);
        
        var sql = "DELETE FROM backtest_runs WHERE id = @Id";
        var affected = await connection.ExecuteAsync(sql, new { Id = backtestId });
        
        if (affected > 0)
        {
            Console.WriteLine($"✅ 已删除回测记录: {backtestId}");
        }
        else
        {
            Console.WriteLine($"⚠️ 未找到回测记录: {backtestId}");
        }
    }
    
    /// <summary>
    /// 获取订单列表
    /// </summary>
    public async Task<List<Order>> GetOrdersAsync(
        string backtestId,
        CancellationToken cancellationToken = default)
    {
        using var connection = (SqliteConnection)DBHelper.CreateConnection(_dbPath);
        
        var sql = @"
            SELECT 
                id AS Id, backtest_id AS BacktestId, symbol_key AS Symbol,
                side AS Side, type AS Type, status AS Status,
                margin AS Margin, leverage AS Leverage, quantity AS Quantity,
                open_price AS OpenPrice, open_time AS OpenTime,
                filled_price AS FilledPrice, filled_time AS FilledTime,
                close_price AS ClosePrice, close_time AS CloseTime,
                fee AS Fee, funding_fee AS FundingFee, profit AS Profit,
                handling_fee AS HandlingFee, surplus AS Surplus,
                take_profit AS TakeProfit, stop_loss AS StopLoss,
                liquidation_price AS LiquidationPrice, closed AS Closed,
                remarks AS Remarks
            FROM backtest_orders 
            WHERE backtest_id = @BacktestId 
            ORDER BY open_time";
        var orderDtos = await connection.QueryAsync<OrderDto>(sql, new { BacktestId = backtestId });
        
        var orders = orderDtos.Select(MapToOrder).ToList();
        
        // 加载订单-信号关联关系
        var orderSignals = await GetOrderSignalsAsync(backtestId, cancellationToken);
        var orderSignalsDict = orderSignals.GroupBy(os => os.OrderId)
            .ToDictionary(g => g.Key, g => g.ToList());
        
        // 将关联关系附加到订单（如果订单是OrderExtended类型）
        foreach (var order in orders)
        {
            if (orderSignalsDict.TryGetValue(order.Id, out var signals))
            {
                // 如果订单不是OrderExtended类型，创建一个扩展版本
                if (order is not OrderExtended orderExt)
                {
                    // 注意：这里我们不能直接转换，因为Order和OrderExtended是不同的类型
                    // 但我们可以通过反射或者创建一个新的OrderExtended实例
                    // 为了简化，我们暂时不处理，因为UI可能期望OrderExtended类型
                }
            }
        }
        
        return orders;
    }
    
    /// <summary>
    /// 获取订单-信号关联列表
    /// </summary>
    private async Task<List<OrderSignal>> GetOrderSignalsAsync(
        string backtestId,
        CancellationToken cancellationToken = default)
    {
        using var connection = (SqliteConnection)DBHelper.CreateConnection(_dbPath);
        
        var sql = @"
            SELECT os.order_id AS OrderId, os.signal_id AS SignalId, os.action AS Action, os.created_at AS CreatedAt
            FROM backtest_order_signals os
            INNER JOIN backtest_orders o ON os.order_id = o.id
            WHERE o.backtest_id = @BacktestId";
        
        var dtos = await connection.QueryAsync<OrderSignalDto>(sql, new { BacktestId = backtestId });
        
        return dtos.Select(dto => new OrderSignal
        {
            OrderId = dto.OrderId,
            SignalId = dto.SignalId,
            Action = Enum.TryParse<OrderSignalAction>(dto.Action, out var action) ? action : OrderSignalAction.OPEN,
            CreatedAt = DateTime.TryParse(dto.CreatedAt, out var createdAt) ? createdAt : DateTime.Now
        }).ToList();
    }
    
    /// <summary>
    /// 获取权益曲线
    /// </summary>
    public async Task<List<EquityPoint>> GetEquityCurveAsync(
        string backtestId,
        CancellationToken cancellationToken = default)
    {
        using var connection = (SqliteConnection)DBHelper.CreateConnection(_dbPath);
        
        var sql = "SELECT * FROM backtest_equity_curve WHERE backtest_id = @BacktestId ORDER BY time";
        var equityDtos = await connection.QueryAsync<EquityPointDto>(sql, new { BacktestId = backtestId });
        
        return equityDtos
            .Where(e => !string.IsNullOrWhiteSpace(e.Time))
            .Select(e => new EquityPoint
            {
                Time = DateTime.TryParse(e.Time, out var time) ? time : DateTime.MinValue,
                Equity = (decimal)e.Equity,
                Cash = (decimal)e.Cash,
                Position = (decimal)e.Position
            })
            .ToList();
    }
    
    /// <summary>
    /// 获取信号事件列表
    /// </summary>
    private async Task<List<SignalEvent>> GetSignalsAsync(
        string backtestId,
        CancellationToken cancellationToken = default)
    {
        using var connection = (SqliteConnection)DBHelper.CreateConnection(_dbPath);
        
        var sql = @"
            SELECT 
                id AS Id, backtest_id AS BacktestId,
                time AS Time, action AS Action, signal_price AS SignalPrice,
                was_executed AS WasExecuted, reason_if_not_executed AS ReasonIfNotExecuted,
                confidence AS Confidence, strength AS Strength, description AS Description,
                take_profit AS TakeProfit, stop_loss AS StopLoss,
                trend AS Trend,
                configs_json AS ConfigsJson, indicators_json AS IndicatorsJson,
                indicator_snapshots_json AS IndicatorSnapshotsJson,
                debug_json AS DebugJson,
                candle_index AS CandleIndex, global_index AS GlobalIndex,
                created_at AS CreatedAt
            FROM backtest_signals 
            WHERE backtest_id = @BacktestId 
            ORDER BY time";
        var signalDtos = await connection.QueryAsync<SignalEventDto>(sql, new { BacktestId = backtestId });
        
        // 日期解析辅助方法
        DateTime ParseSignalDateTime(string dateStr)
        {
            if (string.IsNullOrWhiteSpace(dateStr))
            {
                Console.WriteLine($"⚠️ 信号日期时间字符串为空");
                return DateTime.MinValue;
            }
            
            // 尝试ISO 8601格式（O格式，如：2024-01-01T00:00:00.0000000+00:00）
            if (DateTime.TryParseExact(dateStr, "O", null, System.Globalization.DateTimeStyles.RoundtripKind, out var result))
                return result;
            
            // 尝试标准日期时间格式
            if (DateTime.TryParse(dateStr, null, System.Globalization.DateTimeStyles.RoundtripKind, out result))
                return result;
            
            // 尝试SQLite的日期时间格式（如：2024-01-01 00:00:00）
            if (DateTime.TryParseExact(dateStr, "yyyy-MM-dd HH:mm:ss", null, System.Globalization.DateTimeStyles.None, out result))
                return result;
            
            // 尝试SQLite的日期格式（如：2024-01-01）
            if (DateTime.TryParseExact(dateStr, "yyyy-MM-dd", null, System.Globalization.DateTimeStyles.None, out result))
                return result;
            
            Console.WriteLine($"⚠️ 无法解析信号日期时间: {dateStr}");
            return DateTime.MinValue;
        }
        
        var signals = signalDtos
            .Where(s => !string.IsNullOrWhiteSpace(s.Time))
            .Select(s =>
            {
                var signal = new SignalEvent
                {
                    Time = ParseSignalDateTime(s.Time),
                    Action = Enum.TryParse<SignalAction>(s.Action, out var action) ? action : SignalAction.HOLD,
                    SignalPrice = (decimal)s.SignalPrice,
                    WasExecuted = s.WasExecuted == 1,
                    ReasonIfNotExecuted = s.ReasonIfNotExecuted,
                    Confidence = s.Confidence,
                    Strength = s.Strength,
                    Description = s.Description,
                    TakeProfit = s.TakeProfit.HasValue ? (decimal)s.TakeProfit.Value : null,
                    StopLoss = s.StopLoss.HasValue ? (decimal)s.StopLoss.Value : null,
                    Trend = s.Trend,
                    ConfigsJson = s.ConfigsJson,
                    IndicatorsJson = s.IndicatorsJson,
                    IndicatorSnapshotsJson = s.IndicatorSnapshotsJson,
                    DebugJson = s.DebugJson,
                CandleIndex = s.CandleIndex,
                GlobalIndex = s.GlobalIndex
            };
            
            return signal;
            })
            .ToList();
        
        return signals;
    }
    
    /// <summary>
    /// 获取回撤周期列表
    /// </summary>
    private async Task<List<DrawdownPeriod>> GetDrawdownPeriodsAsync(
        string backtestId,
        CancellationToken cancellationToken = default)
    {
        using var connection = (SqliteConnection)DBHelper.CreateConnection(_dbPath);
        
        var sql = "SELECT * FROM backtest_drawdown_periods WHERE backtest_id = @BacktestId ORDER BY start_time";
        var drawdownDtos = await connection.QueryAsync<DrawdownPeriodDto>(sql, new { BacktestId = backtestId });
        
        return drawdownDtos
            .Where(d => !string.IsNullOrWhiteSpace(d.StartTime) && !string.IsNullOrWhiteSpace(d.EndTime))
            .Select(d => new DrawdownPeriod
            {
                StartTime = DateTime.TryParse(d.StartTime, out var startTime) ? startTime : DateTime.MinValue,
                EndTime = DateTime.TryParse(d.EndTime, out var endTime) ? endTime : DateTime.MinValue,
                DrawdownPercentage = (decimal)d.DrawdownPercentage,
                Duration = TimeSpan.FromSeconds(d.DurationSeconds),
                RecoveryTime = (decimal)d.RecoveryTime
            })
            .ToList();
    }
    
    /// <summary>
    /// 清理旧的回测记录（只保留最近N条）
    /// </summary>
    public async Task CleanupOldBacktestsAsync(
        int keepCount = 50,
        CancellationToken cancellationToken = default)
    {
        using var connection = (SqliteConnection)DBHelper.CreateConnection(_dbPath);
        
        // 获取要保留的记录ID
        var sqlKeep = @"
            SELECT id FROM backtest_runs 
            ORDER BY created_at DESC 
            LIMIT @KeepCount
        ";
        var keepIds = (await connection.QueryAsync<string>(sqlKeep, new { KeepCount = keepCount })).ToList();
        
        if (keepIds.Count == 0)
            return;
        
        // 删除不在保留列表中的记录
        var sqlDelete = @"
            DELETE FROM backtest_runs 
            WHERE id NOT IN @KeepIds
        ";
        var affected = await connection.ExecuteAsync(sqlDelete, new { KeepIds = keepIds });
        
        if (affected > 0)
        {
            Console.WriteLine($"✅ 已清理 {affected} 条旧回测记录（保留最近 {keepCount} 条）");
        }
    }
    
    #endregion
    
    #region 映射方法
    
    private BacktestResult MapToBacktestResult(
        BacktestRunDto run,
        List<Order> orders,
        List<EquityPoint> equityCurve,
        List<SignalEvent> signals,
        List<DrawdownPeriod> drawdowns)
    {
        // 安全解析日期时间字段（处理空字符串和NULL）
        DateTime ParseDateTimeSafe(string? dateStr, DateTime defaultValue)
        {
            if (string.IsNullOrWhiteSpace(dateStr))
                return defaultValue;
            
            if (DateTime.TryParse(dateStr, out var result))
                return result;
            
            // 尝试ISO 8601格式
            if (DateTime.TryParseExact(dateStr, "O", null, System.Globalization.DateTimeStyles.RoundtripKind, out result))
                return result;
            
            return defaultValue;
        }
        
        // 优先使用StartTime/EndTime，如果为空则使用StartDate/EndDate
        var startTime = !string.IsNullOrWhiteSpace(run.StartTime) 
            ? ParseDateTimeSafe(run.StartTime, DateTime.MinValue)
            : ParseDateTimeSafe(run.StartDate, DateTime.MinValue);
        
        var endTime = !string.IsNullOrWhiteSpace(run.EndTime)
            ? ParseDateTimeSafe(run.EndTime, DateTime.MinValue)
            : ParseDateTimeSafe(run.EndDate, DateTime.MinValue);
        
        var completedAt = ParseDateTimeSafe(run.CompletedAt, DateTime.UtcNow);
        
        // 数据验证：如果关键字段缺失，记录警告（仅记录前3条）
        if (_debugMapCount < 3 && (run.TotalReturn == null || run.MaxDrawdown == null || run.SharpeRatio == null || 
            string.IsNullOrWhiteSpace(run.StartTime) || string.IsNullOrWhiteSpace(run.EndTime)))
        {
            _debugMapCount++;
            // 仅在关键数据缺失时记录警告
        }
        
        return new BacktestResult
        {
            BacktestId = run.Id,
            StrategyId = run.StrategyId,
            StrategyName = run.StrategyName,
            VersionId = run.VersionId,
            VersionString = run.VersionString,
            Symbol = run.Symbol,
            Interval = run.Interval,
            StartTime = startTime,
            EndTime = endTime,
            Duration = TimeSpan.FromSeconds(run.DurationSeconds ?? 0),
            CompletedAt = completedAt,
            Status = Enum.TryParse<BacktestStatus>(run.Status, out var status) ? status : BacktestStatus.COMPLETED,
            ErrorMessage = run.ErrorMessage,
            
            InitialCapital = (decimal)run.InitialCapital,
            FinalEquity = (decimal)(run.FinalEquity ?? 0),
            TotalReturn = (decimal)(run.TotalReturn ?? 0),
            AnnualizedReturn = (decimal)(run.AnnualizedReturn ?? 0),
            MaxDrawdown = (decimal)(run.MaxDrawdown ?? 0),
            SharpeRatio = (decimal)(run.SharpeRatio ?? 0),
            SortinoRatio = (decimal)(run.SortinoRatio ?? 0),
            CalmarRatio = (decimal)(run.CalmarRatio ?? 0),
            
            TotalTrades = run.TotalTrades ?? 0,
            WinningTrades = run.WinningTrades ?? 0,
            LosingTrades = run.LosingTrades ?? 0,
            WinRate = (decimal)(run.WinRate ?? 0),
            AvgProfit = (decimal)(run.AvgProfit ?? 0),
            AvgLoss = (decimal)(run.AvgLoss ?? 0),
            ProfitFactor = (decimal)(run.ProfitFactor ?? 0),
            MaxConsecutiveWins = run.MaxConsecutiveWins ?? 0,
            MaxConsecutiveLosses = run.MaxConsecutiveLosses ?? 0,
            MaxSingleProfit = (decimal)(run.MaxSingleProfit ?? 0),
            MaxSingleLoss = (decimal)(run.MaxSingleLoss ?? 0),
            AvgHoldingTime = TimeSpan.FromSeconds(run.AvgHoldingTimeSeconds ?? 0),
            
            Orders = orders,
            EquityCurve = equityCurve,
            Signals = signals,
            DrawdownPeriods = drawdowns,
            
            Config = null! // 可以从parameters字段反序列化，暂时不实现
        };
    }
    
    private Order MapToOrder(OrderDto dto)
    {
        // 安全解析日期时间
        DateTime? ParseDateTimeNullable(string? dateStr)
        {
            if (string.IsNullOrWhiteSpace(dateStr))
                return null;
            
            // 尝试ISO 8601格式（O格式，如：2024-01-01T00:00:00.0000000+00:00）
            if (DateTime.TryParseExact(dateStr, "O", null, System.Globalization.DateTimeStyles.RoundtripKind, out var result))
                return result;
            
            // 尝试标准日期时间格式
            if (DateTime.TryParse(dateStr, null, System.Globalization.DateTimeStyles.RoundtripKind, out result))
                return result;
            
            // 尝试SQLite的日期时间格式（如：2024-01-01 00:00:00）
            if (DateTime.TryParseExact(dateStr, "yyyy-MM-dd HH:mm:ss", null, System.Globalization.DateTimeStyles.None, out result))
                return result;
            
            // 尝试SQLite的日期格式（如：2024-01-01）
            if (DateTime.TryParseExact(dateStr, "yyyy-MM-dd", null, System.Globalization.DateTimeStyles.None, out result))
                return result;
            
            Console.WriteLine($"⚠️ 无法解析日期时间: {dateStr}");
            return null;
        }
        
        DateTime ParseDateTimeRequired(string dateStr)
        {
            if (string.IsNullOrWhiteSpace(dateStr))
            {
                Console.WriteLine($"⚠️ 日期时间字符串为空");
                return DateTime.MinValue;
            }
            
            // 尝试ISO 8601格式（O格式，如：2024-01-01T00:00:00.0000000+00:00）
            if (DateTime.TryParseExact(dateStr, "O", null, System.Globalization.DateTimeStyles.RoundtripKind, out var result))
                return result;
            
            // 尝试标准日期时间格式
            if (DateTime.TryParse(dateStr, null, System.Globalization.DateTimeStyles.RoundtripKind, out result))
                return result;
            
            // 尝试SQLite的日期时间格式（如：2024-01-01 00:00:00）
            if (DateTime.TryParseExact(dateStr, "yyyy-MM-dd HH:mm:ss", null, System.Globalization.DateTimeStyles.None, out result))
                return result;
            
            // 尝试SQLite的日期格式（如：2024-01-01）
            if (DateTime.TryParseExact(dateStr, "yyyy-MM-dd", null, System.Globalization.DateTimeStyles.None, out result))
                return result;
            
            Console.WriteLine($"⚠️ 无法解析日期时间: {dateStr}");
            return DateTime.MinValue;
        }
        
        return new Order
        {
            Id = dto.Id,
            BacktestId = dto.BacktestId,
            Side = Enum.TryParse<OrderSide>(dto.Side, out var side) ? side : OrderSide.BUY,
            Type = Enum.TryParse<OrderType>(dto.Type, out var type) ? type : OrderType.MARKET,
            Status = Enum.TryParse<OrderStatus>(dto.Status, out var status) ? status : OrderStatus.PENDING,
            Margin = (decimal)dto.Margin,
            Leverage = (decimal)dto.Leverage,
            Quantity = (decimal)dto.Quantity,
            OpenPrice = (decimal)dto.OpenPrice,
            OpenTime = ParseDateTimeRequired(dto.OpenTime),
            FilledPrice = dto.FilledPrice.HasValue ? (decimal)dto.FilledPrice.Value : null,
            FilledTime = ParseDateTimeNullable(dto.FilledTime),
            ClosePrice = dto.ClosePrice.HasValue ? (decimal)dto.ClosePrice.Value : null,
            CloseTime = ParseDateTimeNullable(dto.CloseTime),
            Fee = (decimal)dto.Fee,
            FundingFee = (decimal)dto.FundingFee,
            Profit = dto.Profit.HasValue ? (decimal)dto.Profit.Value : null,
            TakeProfit = dto.TakeProfit.HasValue ? (decimal)dto.TakeProfit.Value : null,
            StopLoss = dto.StopLoss.HasValue ? (decimal)dto.StopLoss.Value : null,
            LiquidationPrice = dto.LiquidationPrice.HasValue ? (decimal)dto.LiquidationPrice.Value : null,
            Remarks = dto.Remarks
        };
    }
    
    #endregion
    
    #region 数据传输对象 (DTOs)
    
    private class BacktestRunDto
    {
        public string Id { get; set; } = string.Empty;
        public string StrategyId { get; set; } = string.Empty;
        public string StrategyName { get; set; } = string.Empty;
        public int? VersionId { get; set; }
        public string? VersionString { get; set; }
        public string Symbol { get; set; } = string.Empty;
        public string Interval { get; set; } = string.Empty;
        public string StartDate { get; set; } = string.Empty;
        public string EndDate { get; set; } = string.Empty;
        public double InitialCapital { get; set; }
        public double? FinalEquity { get; set; }
        public double? TotalReturn { get; set; }
        public double? AnnualizedReturn { get; set; }
        public double? MaxDrawdown { get; set; }
        public double? SharpeRatio { get; set; }
        public double? SortinoRatio { get; set; }
        public double? CalmarRatio { get; set; }
        public int? TotalTrades { get; set; }
        public int? WinningTrades { get; set; }
        public int? LosingTrades { get; set; }
        public double? WinRate { get; set; }
        public double? AvgProfit { get; set; }
        public double? AvgLoss { get; set; }
        public double? ProfitFactor { get; set; }
        public int? MaxConsecutiveWins { get; set; }
        public int? MaxConsecutiveLosses { get; set; }
        public double? MaxSingleProfit { get; set; }
        public double? MaxSingleLoss { get; set; }
        public double? AvgHoldingTimeSeconds { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? ErrorMessage { get; set; }
        public string StartTime { get; set; } = string.Empty;
        public string EndTime { get; set; } = string.Empty;
        public double? DurationSeconds { get; set; }
        public string CompletedAt { get; set; } = string.Empty;
    }
    
    private class OrderDto
    {
        public string Id { get; set; } = string.Empty;
        public string BacktestId { get; set; } = string.Empty;
        public string? Symbol { get; set; }
        public string Side { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public double Margin { get; set; }
        public int Leverage { get; set; }
        public double Quantity { get; set; }
        public double OpenPrice { get; set; }
        public string OpenTime { get; set; } = string.Empty;
        public double? FilledPrice { get; set; }
        public string? FilledTime { get; set; }
        public double? ClosePrice { get; set; }
        public string? CloseTime { get; set; }
        public double Fee { get; set; }
        public double FundingFee { get; set; }
        public double? Profit { get; set; }
        public double? HandlingFee { get; set; }
        public double? Surplus { get; set; }
        public double? TakeProfit { get; set; }
        public double? StopLoss { get; set; }
        public double? LiquidationPrice { get; set; }
        public int Closed { get; set; }
        public string? Remarks { get; set; }
    }
    
    private class EquityPointDto
    {
        public string Time { get; set; } = string.Empty;
        public double Equity { get; set; }
        public double Cash { get; set; }
        public double Position { get; set; }
    }
    
    private class SignalEventDto
    {
        // 主键ID（用于关联K线形态）
        public int Id { get; set; }
        
        // 基本信息
        public string Time { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public double SignalPrice { get; set; }
        
        // 执行状态
        public int WasExecuted { get; set; }
        public string? ReasonIfNotExecuted { get; set; }
        
        // 信号强度和描述
        public double Confidence { get; set; }
        public double Strength { get; set; }
        public string? Description { get; set; }
        
        // 止损止盈
        public double? TakeProfit { get; set; }
        public double? StopLoss { get; set; }
        
        // v11.0 新增：趋势判断
        public string? Trend { get; set; }
        
        // v11.0 新增：JSON 数据快照
        public string? ConfigsJson { get; set; }
        public string? IndicatorsJson { get; set; }
        public string? IndicatorSnapshotsJson { get; set; }
        public string? DebugJson { get; set; }
        
        // 关联信息
        public int? CandleIndex { get; set; }
        public int? GlobalIndex { get; set; }
        
        // 时间戳（用于调试）
        public string? CreatedAt { get; set; }
    }
    
    private class DrawdownPeriodDto
    {
        public string StartTime { get; set; } = string.Empty;
        public string EndTime { get; set; } = string.Empty;
        public double DrawdownPercentage { get; set; }
        public double DurationSeconds { get; set; }
        public double RecoveryTime { get; set; }
    }
    
    private class OrderSignalDto
    {
        public string OrderId { get; set; } = string.Empty;
        public int SignalId { get; set; }
        public string Action { get; set; } = string.Empty;
        public string CreatedAt { get; set; } = string.Empty;
    }
    
    #endregion
    
    public void Dispose()
    {
        if (_disposed)
            return;
        
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}

