-- ========================================
-- 彻底清理数据库中的所有问题
-- 删除所有可能引用 backtest_signals_old 的对象
-- ========================================

-- 1. 删除所有可能的旧表
DROP TABLE IF EXISTS backtest_signals_old;
DROP TABLE IF EXISTS backtest_signals;

-- 2. 删除所有视图（可能引用了旧表）
DROP VIEW IF EXISTS backtest_with_stats;
DROP VIEW IF EXISTS backtest_orders_detail;

-- 3. 删除所有触发器（如果有）
DROP TRIGGER IF EXISTS trg_backtest_signals_update;
DROP TRIGGER IF EXISTS trg_backtest_signals_insert;

-- 4. 重新创建 backtest_signals 表（V3.0 结构）
CREATE TABLE backtest_signals (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    backtest_id TEXT NOT NULL,
    
    -- 基本信息
    time TEXT NOT NULL,
    action TEXT NOT NULL,
    signal_price REAL NOT NULL,
    
    -- 执行状态
    was_executed INTEGER NOT NULL DEFAULT 0,
    reason_if_not_executed TEXT,
    
    -- 信号强度和描述
    confidence REAL DEFAULT 0.0,
    strength REAL DEFAULT 1.0,
    description TEXT,
    
    -- 止损止盈
    take_profit REAL,
    stop_loss REAL,
    
    -- v11.0 新增：趋势判断
    trend TEXT,
    
    -- v11.0 新增：JSON 数据快照
    configs_json TEXT,
    indicators_json TEXT,
    patterns_json TEXT,
    debug_json TEXT,
    
    -- 关联信息
    candle_index INTEGER,
    global_index INTEGER,
    
    -- 时间戳
    created_at TEXT DEFAULT CURRENT_TIMESTAMP,
    
    FOREIGN KEY (backtest_id) REFERENCES backtest_runs(id) ON DELETE CASCADE
);

-- 5. 创建索引
CREATE INDEX idx_backtest_signals_backtest_id ON backtest_signals(backtest_id);
CREATE INDEX idx_backtest_signals_time ON backtest_signals(backtest_id, time);
CREATE INDEX idx_backtest_signals_action ON backtest_signals(backtest_id, action);
CREATE INDEX idx_backtest_signals_executed ON backtest_signals(backtest_id, was_executed);

-- 6. 重新创建视图
CREATE VIEW backtest_with_stats AS
SELECT 
    br.id,
    br.strategy_id,
    br.strategy_name,
    br.symbol,
    br.interval,
    br.start_date,
    br.end_date,
    br.status,
    br.created_at,
    br.completed_at,
    br.duration_seconds,
    COUNT(DISTINCT bo.id) AS total_trades,
    SUM(CASE WHEN bo.profit > 0 THEN 1 ELSE 0 END) AS winning_trades,
    SUM(CASE WHEN bo.profit < 0 THEN 1 ELSE 0 END) AS losing_trades,
    AVG(bo.profit) AS avg_profit_per_trade,
    MAX(bo.profit) AS max_profit,
    MIN(bo.profit) AS max_loss,
    SUM(bo.fee) AS total_fees,
    COALESCE(br.final_equity, br.initial_capital + SUM(bo.profit)) AS final_equity
FROM backtest_runs br
LEFT JOIN backtest_orders bo ON br.id = bo.backtest_id
GROUP BY br.id;

CREATE VIEW backtest_orders_detail AS
SELECT 
    o.id,
    o.backtest_id,
    o.side,
    o.quantity,
    o.open_price,
    o.open_time,
    o.close_price,
    o.close_time,
    o.profit,
    o.fee,
    CAST((julianday(o.close_time) - julianday(o.open_time)) * 24 * 60 AS INTEGER) AS holding_minutes
FROM backtest_orders o;

-- 7. 更新数据库版本号
INSERT OR REPLACE INTO backtest_db_meta (key, value, updated_at) 
VALUES ('schema_version', '3.0', datetime('now', 'localtime'));

-- 8. 验证结果
SELECT '✅ 数据库清理完成！' AS status;
SELECT name, type FROM sqlite_master WHERE name LIKE '%signal%';

