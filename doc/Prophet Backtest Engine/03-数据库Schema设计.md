# Prophet 回测引擎 - 数据库Schema设计

## 📋 文档信息

- **文档名称**: 数据库Schema设计
- **版本**: 1.0
- **创建日期**: 2025-11-19
- **存储方案**: 本地SQLite（MVP） + 可选云端MySQL

---

## 🗄️ 数据库选择

### 阶段1: 本地SQLite（推荐）

**优点:**
- ✅ 零配置，嵌入式
- ✅ 本地I/O，速度极快
- ✅ 单文件，易于备份
- ✅ 跨平台支持

**用途:**
- 回测结果存储（最近10-50次）
- 订单记录
- 权益曲线数据

**文件位置:**
```
AppData/Prophet/backtest.db
```

### 阶段2: 云端MySQL（可选）

**优点:**
- ✅ 多设备同步
- ✅ 大数据分析
- ✅ 长期存储

**用途:**
- 所有历史回测记录
- 参数优化历史
- 跨设备访问

---

## 📊 核心表设计（SQLite）

### 1. backtest_runs（回测运行记录）

```sql
CREATE TABLE IF NOT EXISTS backtest_runs (
    -- ========== 主键 ==========
    id TEXT PRIMARY KEY,  -- UUID
    
    -- ========== 策略信息 ==========
    strategy_id TEXT NOT NULL,
    strategy_name TEXT NOT NULL,
    
    -- ========== 配置信息 ==========
    symbol TEXT NOT NULL DEFAULT 'BTCUSDT',
    interval TEXT NOT NULL DEFAULT '5m',
    start_date TEXT NOT NULL,  -- ISO 8601
    end_date TEXT NOT NULL,    -- ISO 8601
    initial_capital REAL NOT NULL,
    leverage REAL DEFAULT 1.0,
    taker_fee_rate REAL DEFAULT 0.001,
    slippage_rate REAL DEFAULT 0.0005,
    
    -- ========== 参数JSON ==========
    parameters TEXT,  -- JSON格式: {"macd_fast": 12, "macd_slow": 26, ...}
    
    -- ========== 绩效指标（聚合数据）==========
    total_return REAL,
    annualized_return REAL,
    max_drawdown REAL,
    sharpe_ratio REAL,
    sortino_ratio REAL,
    
    -- ========== 交易统计 ==========
    total_trades INTEGER,
    winning_trades INTEGER,
    losing_trades INTEGER,
    win_rate REAL,
    avg_profit REAL,
    avg_loss REAL,
    profit_factor REAL,
    
    -- ========== 状态 ==========
    status TEXT DEFAULT 'completed',  -- completed, cancelled, failed
    error_message TEXT,
    
    -- ========== 时间信息 ==========
    start_time TEXT NOT NULL,
    end_time TEXT,
    duration_seconds REAL,
    created_at TEXT DEFAULT CURRENT_TIMESTAMP,
    
    -- ========== 索引 ==========
    -- 按策略和创建时间查询
    FOREIGN KEY (strategy_id) REFERENCES strategies(id)
);

CREATE INDEX IF NOT EXISTS idx_backtest_strategy_time 
ON backtest_runs(strategy_id, created_at DESC);

CREATE INDEX IF NOT EXISTS idx_backtest_performance 
ON backtest_runs(total_return DESC, sharpe_ratio DESC);

CREATE INDEX IF NOT EXISTS idx_backtest_status 
ON backtest_runs(status);
```

---

### 2. backtest_orders（订单记录）

```sql
CREATE TABLE IF NOT EXISTS backtest_orders (
    -- ========== 主键 ==========
    id TEXT PRIMARY KEY,  -- UUID
    
    -- ========== 外键 ==========
    backtest_id TEXT NOT NULL,
    
    -- ========== 订单信息 ==========
    side TEXT NOT NULL,  -- BUY, SELL
    type TEXT NOT NULL DEFAULT 'MARKET',  -- MARKET, LIMIT
    status TEXT NOT NULL,  -- OPEN, FILLED, CLOSED, CANCELLED
    
    -- ========== 价格与数量 ==========
    quantity REAL NOT NULL,
    open_price REAL NOT NULL,
    open_time TEXT NOT NULL,  -- ISO 8601
    close_price REAL,
    close_time TEXT,  -- ISO 8601
    
    -- ========== 盈亏 ==========
    profit REAL,
    fee REAL,
    
    -- ========== 止盈止损 ==========
    take_profit REAL,
    stop_loss REAL,
    liquidation_price REAL,
    
    -- ========== 备注 ==========
    remarks TEXT,
    
    -- ========== 时间戳 ==========
    created_at TEXT DEFAULT CURRENT_TIMESTAMP,
    
    -- ========== 外键约束 ==========
    FOREIGN KEY (backtest_id) REFERENCES backtest_runs(id) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS idx_orders_backtest 
ON backtest_orders(backtest_id);

CREATE INDEX IF NOT EXISTS idx_orders_time 
ON backtest_orders(open_time);
```

---

### 3. backtest_equity_curve（权益曲线）

```sql
CREATE TABLE IF NOT EXISTS backtest_equity_curve (
    -- ========== 主键 ==========
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    
    -- ========== 外键 ==========
    backtest_id TEXT NOT NULL,
    
    -- ========== 数据 ==========
    timestamp TEXT NOT NULL,  -- ISO 8601
    equity REAL NOT NULL,
    
    -- ========== 外键约束 ==========
    FOREIGN KEY (backtest_id) REFERENCES backtest_runs(id) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS idx_equity_backtest_time 
ON backtest_equity_curve(backtest_id, timestamp);
```

---

### 4. optimization_runs（参数优化记录）

```sql
CREATE TABLE IF NOT EXISTS optimization_runs (
    -- ========== 主键 ==========
    id TEXT PRIMARY KEY,  -- UUID
    
    -- ========== 策略信息 ==========
    strategy_id TEXT NOT NULL,
    strategy_name TEXT NOT NULL,
    
    -- ========== 优化配置 ==========
    optimizer_type TEXT NOT NULL,  -- GRID_SEARCH, BAYESIAN, GENETIC
    parameter_space TEXT NOT NULL,  -- JSON: {"macd_fast": [10, 12, 14], ...}
    
    -- ========== 优化结果 ==========
    total_iterations INTEGER,
    completed_iterations INTEGER,
    best_backtest_id TEXT,  -- 最佳回测的ID
    best_parameters TEXT,   -- JSON
    best_total_return REAL,
    best_sharpe_ratio REAL,
    
    -- ========== 状态 ==========
    status TEXT DEFAULT 'running',  -- running, completed, cancelled, failed
    
    -- ========== 时间信息 ==========
    start_time TEXT NOT NULL,
    end_time TEXT,
    duration_seconds REAL,
    created_at TEXT DEFAULT CURRENT_TIMESTAMP,
    
    -- ========== 外键 ==========
    FOREIGN KEY (best_backtest_id) REFERENCES backtest_runs(id)
);

CREATE INDEX IF NOT EXISTS idx_optimization_strategy 
ON optimization_runs(strategy_id, created_at DESC);
```

---

## 🔧 数据访问层设计

### IBacktestStorage 接口

```csharp
namespace Prophet.Client.Backtest.Storage;

public interface IBacktestStorage
{
    // ========== 回测记录 ==========
    Task<string> SaveBacktestResultAsync(
        BacktestResult result, 
        CancellationToken cancellationToken = default
    );
    
    Task<BacktestResult?> GetBacktestResultAsync(
        string backtestId, 
        CancellationToken cancellationToken = default
    );
    
    Task<List<BacktestResult>> GetRecentBacktestsAsync(
        int count = 10, 
        CancellationToken cancellationToken = default
    );
    
    Task<List<BacktestResult>> GetBacktestsByStrategyAsync(
        string strategyId, 
        CancellationToken cancellationToken = default
    );
    
    Task DeleteBacktestAsync(
        string backtestId, 
        CancellationToken cancellationToken = default
    );
    
    // ========== 订单记录 ==========
    Task SaveOrdersAsync(
        string backtestId, 
        List<Order> orders, 
        CancellationToken cancellationToken = default
    );
    
    Task<List<Order>> GetOrdersAsync(
        string backtestId, 
        CancellationToken cancellationToken = default
    );
    
    // ========== 权益曲线 ==========
    Task SaveEquityCurveAsync(
        string backtestId, 
        List<EquityPoint> equityCurve, 
        CancellationToken cancellationToken = default
    );
    
    Task<List<EquityPoint>> GetEquityCurveAsync(
        string backtestId, 
        CancellationToken cancellationToken = default
    );
    
    // ========== 数据清理 ==========
    Task CleanupOldBacktestsAsync(
        int keepCount = 50, 
        CancellationToken cancellationToken = default
    );
}
```

---

### LocalBacktestStorage 实现

```csharp
namespace Prophet.Client.Backtest.Storage;

public class LocalBacktestStorage : IBacktestStorage
{
    private readonly string _dbPath;
    private readonly SemaphoreSlim _lock = new(1, 1);
    
    public LocalBacktestStorage(string dbPath)
    {
        _dbPath = dbPath ?? throw new ArgumentNullException(nameof(dbPath));
        InitializeDatabase();
    }
    
    private void InitializeDatabase()
    {
        using var connection = new SQLiteConnection($"Data Source={_dbPath}");
        connection.Open();
        
        // 创建表（SQL见上文）
        using var command = connection.CreateCommand();
        command.CommandText = @"
            -- 创建backtest_runs表
            CREATE TABLE IF NOT EXISTS backtest_runs (...);
            
            -- 创建backtest_orders表
            CREATE TABLE IF NOT EXISTS backtest_orders (...);
            
            -- 创建backtest_equity_curve表
            CREATE TABLE IF NOT EXISTS backtest_equity_curve (...);
            
            -- 创建索引
            CREATE INDEX IF NOT EXISTS idx_backtest_strategy_time ...;
        ";
        command.ExecuteNonQuery();
    }
    
    public async Task<string> SaveBacktestResultAsync(
        BacktestResult result, 
        CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            using var connection = new SQLiteConnection($"Data Source={_dbPath}");
            await connection.OpenAsync(cancellationToken);
            
            using var transaction = connection.BeginTransaction();
            try
            {
                // 1. 保存回测记录
                using var cmd1 = connection.CreateCommand();
                cmd1.CommandText = @"
                    INSERT INTO backtest_runs (
                        id, strategy_id, strategy_name,
                        symbol, interval, start_date, end_date,
                        initial_capital, leverage, taker_fee_rate, slippage_rate,
                        parameters,
                        total_return, annualized_return, max_drawdown, sharpe_ratio, sortino_ratio,
                        total_trades, winning_trades, losing_trades, win_rate,
                        avg_profit, avg_loss, profit_factor,
                        status, error_message,
                        start_time, end_time, duration_seconds
                    ) VALUES (
                        @Id, @StrategyId, @StrategyName,
                        @Symbol, @Interval, @StartDate, @EndDate,
                        @InitialCapital, @Leverage, @TakerFeeRate, @SlippageRate,
                        @Parameters,
                        @TotalReturn, @AnnualizedReturn, @MaxDrawdown, @SharpeRatio, @SortinoRatio,
                        @TotalTrades, @WinningTrades, @LosingTrades, @WinRate,
                        @AvgProfit, @AvgLoss, @ProfitFactor,
                        @Status, @ErrorMessage,
                        @StartTime, @EndTime, @DurationSeconds
                    )
                ";
                
                // 添加参数
                cmd1.Parameters.AddWithValue("@Id", result.BacktestId);
                cmd1.Parameters.AddWithValue("@StrategyId", result.StrategyId);
                cmd1.Parameters.AddWithValue("@StrategyName", result.StrategyName);
                // ... 其他参数
                
                await cmd1.ExecuteNonQueryAsync(cancellationToken);
                
                // 2. 批量保存订单
                await SaveOrdersAsync(result.BacktestId, result.Orders, cancellationToken);
                
                // 3. 批量保存权益曲线
                await SaveEquityCurveAsync(result.BacktestId, result.EquityCurve, cancellationToken);
                
                transaction.Commit();
                return result.BacktestId;
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }
        finally
        {
            _lock.Release();
        }
    }
    
    public async Task SaveOrdersAsync(
        string backtestId, 
        List<Order> orders, 
        CancellationToken cancellationToken = default)
    {
        if (orders == null || orders.Count == 0)
            return;
        
        using var connection = new SQLiteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync(cancellationToken);
        
        using var transaction = connection.BeginTransaction();
        try
        {
            foreach (var order in orders)
            {
                using var cmd = connection.CreateCommand();
                cmd.CommandText = @"
                    INSERT INTO backtest_orders (
                        id, backtest_id, side, type, status,
                        quantity, open_price, open_time, close_price, close_time,
                        profit, fee, take_profit, stop_loss, liquidation_price, remarks
                    ) VALUES (
                        @Id, @BacktestId, @Side, @Type, @Status,
                        @Quantity, @OpenPrice, @OpenTime, @ClosePrice, @CloseTime,
                        @Profit, @Fee, @TakeProfit, @StopLoss, @LiquidationPrice, @Remarks
                    )
                ";
                
                cmd.Parameters.AddWithValue("@Id", order.Id);
                cmd.Parameters.AddWithValue("@BacktestId", backtestId);
                cmd.Parameters.AddWithValue("@Side", order.Side.ToString());
                // ... 其他参数
                
                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }
            
            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }
    
    // ... 其他方法实现
}
```

---

## 🧹 数据清理策略

### 自动清理旧数据

```csharp
public async Task CleanupOldBacktestsAsync(
    int keepCount = 50, 
    CancellationToken cancellationToken = default)
{
    using var connection = new SQLiteConnection($"Data Source={_dbPath}");
    await connection.OpenAsync(cancellationToken);
    
    using var command = connection.CreateCommand();
    command.CommandText = @"
        DELETE FROM backtest_runs 
        WHERE id NOT IN (
            SELECT id FROM backtest_runs 
            ORDER BY created_at DESC 
            LIMIT @KeepCount
        )
    ";
    command.Parameters.AddWithValue("@KeepCount", keepCount);
    
    var deleted = await command.ExecuteNonQueryAsync(cancellationToken);
    Console.WriteLine($"✅ 已清理 {deleted} 条旧回测记录");
}
```

---

## 📈 数据分析查询

### 常用查询示例

```sql
-- 1. 获取最佳回测结果（按总收益率）
SELECT * FROM backtest_runs 
WHERE strategy_id = 'MyStrategy' AND status = 'completed'
ORDER BY total_return DESC 
LIMIT 10;

-- 2. 获取最稳定的回测结果（按夏普比率）
SELECT * FROM backtest_runs 
WHERE strategy_id = 'MyStrategy' AND status = 'completed'
ORDER BY sharpe_ratio DESC 
LIMIT 10;

-- 3. 参数优化历史分析
SELECT 
    parameters,
    AVG(total_return) as avg_return,
    AVG(sharpe_ratio) as avg_sharpe,
    COUNT(*) as run_count
FROM backtest_runs 
WHERE strategy_id = 'MyStrategy'
GROUP BY parameters
ORDER BY avg_sharpe DESC;

-- 4. 获取订单统计
SELECT 
    b.strategy_name,
    COUNT(o.id) as total_orders,
    SUM(CASE WHEN o.profit > 0 THEN 1 ELSE 0 END) as winning_orders,
    AVG(o.profit) as avg_profit
FROM backtest_runs b
JOIN backtest_orders o ON b.id = o.backtest_id
WHERE b.strategy_id = 'MyStrategy'
GROUP BY b.id;
```

---

**文档版本**: 1.0  
**创建日期**: 2025-11-19

