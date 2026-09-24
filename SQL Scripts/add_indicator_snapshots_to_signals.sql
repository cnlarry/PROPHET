-- ========================================
-- SQLite数据库迁移脚本
-- 为 backtest_signals 表添加 indicator_snapshots_json 字段
-- 时间: 2024-12-01
-- 用途: 存储结构化的指标快照，用于详细分析条件触发
-- ========================================

-- 注意：SQLite 3.25.0+ 支持 ALTER TABLE ADD COLUMN
-- 如果您的SQLite版本 >= 3.25.0，可以直接使用以下命令：

ALTER TABLE backtest_signals 
ADD COLUMN indicator_snapshots_json TEXT;

-- ========================================
-- 如果上述命令失败（SQLite版本过低），请使用以下完整迁移方案：
-- ========================================

/*
-- 步骤1: 创建新表（包含 indicator_snapshots_json 字段）
CREATE TABLE backtest_signals_new (
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
    indicator_snapshots_json TEXT,  -- 新增字段
    patterns_json TEXT,
    debug_json TEXT,
    
    -- 关联信息
    candle_index INTEGER,
    global_index INTEGER,
    
    -- 时间戳
    created_at TEXT DEFAULT CURRENT_TIMESTAMP,
    
    FOREIGN KEY (backtest_id) REFERENCES backtest_runs(id) ON DELETE CASCADE
);

-- 步骤2: 迁移数据（从旧表复制到新表）
INSERT INTO backtest_signals_new (
    id, backtest_id, time, action, signal_price,
    was_executed, reason_if_not_executed,
    confidence, strength, description,
    take_profit, stop_loss, trend,
    configs_json, indicators_json, patterns_json, debug_json,
    candle_index, global_index, created_at
)
SELECT 
    id, backtest_id, time, action, signal_price,
    was_executed, reason_if_not_executed,
    confidence, strength, description,
    take_profit, stop_loss, trend,
    configs_json, indicators_json, patterns_json, debug_json,
    candle_index, global_index, created_at
FROM backtest_signals;

-- 步骤3: 删除旧表
DROP TABLE backtest_signals;

-- 步骤4: 重命名新表
ALTER TABLE backtest_signals_new RENAME TO backtest_signals;

-- 步骤5: 重建索引
CREATE INDEX idx_backtest_signals_time ON backtest_signals(backtest_id, time);
CREATE INDEX idx_backtest_signals_backtest_id ON backtest_signals(backtest_id);
CREATE INDEX idx_backtest_signals_executed ON backtest_signals(backtest_id, was_executed);
CREATE INDEX idx_backtest_signals_action ON backtest_signals(backtest_id, action);
*/
