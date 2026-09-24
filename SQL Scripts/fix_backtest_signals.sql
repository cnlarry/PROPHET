-- ========================================
-- 修复 backtest_signals 表问题
-- 删除所有旧表和旧数据，重新创建 V3.0 结构
-- ========================================

-- 1. 删除所有可能存在的旧表
DROP TABLE IF EXISTS backtest_signals_old;
DROP TABLE IF EXISTS backtest_signals;

-- 2. 创建全新的 V3.0 信号表
CREATE TABLE backtest_signals (
    -- === 主键和关联 ===
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    backtest_id TEXT NOT NULL,
    
    -- === 基本信息 ===
    time TEXT NOT NULL,
    action TEXT NOT NULL,
    signal_price REAL NOT NULL,
    
    -- === 执行状态 ===
    was_executed INTEGER NOT NULL DEFAULT 0,
    reason_if_not_executed TEXT,
    
    -- === 信号强度和描述 ===
    confidence REAL DEFAULT 0.0,
    strength REAL DEFAULT 1.0,
    description TEXT,
    
    -- === 止损止盈 ===
    take_profit REAL,
    stop_loss REAL,
    
    -- === v11.0 新增：趋势判断 ===
    trend TEXT,
    
    -- === v11.0 新增：JSON 数据快照 ===
    configs_json TEXT,
    indicators_json TEXT,
    patterns_json TEXT,
    debug_json TEXT,
    
    -- === 关联信息 ===
    candle_index INTEGER,
    global_index INTEGER,
    
    -- === 时间戳 ===
    created_at TEXT DEFAULT CURRENT_TIMESTAMP,
    
    -- === 外键约束 ===
    FOREIGN KEY (backtest_id) REFERENCES backtest_runs(id) ON DELETE CASCADE
);

-- 3. 创建索引
CREATE INDEX idx_backtest_signals_backtest_id ON backtest_signals(backtest_id);
CREATE INDEX idx_backtest_signals_time ON backtest_signals(backtest_id, time);
CREATE INDEX idx_backtest_signals_action ON backtest_signals(backtest_id, action);
CREATE INDEX idx_backtest_signals_executed ON backtest_signals(backtest_id, was_executed);

-- 4. 更新版本号
INSERT OR REPLACE INTO backtest_db_meta (key, value, updated_at) 
VALUES ('schema_version', '3.0', datetime('now', 'localtime'));

-- 5. 验证
SELECT 'backtest_signals 表已重新创建 (V3.0)' AS status;
PRAGMA table_info(backtest_signals);

