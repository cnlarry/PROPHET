-- ========================================
-- Prophet 回测信号表 V3.0 (全新创建)
-- 时间: 2025-11-27
-- 支持 Prophet.Core v11.0 完整信号模型
-- ========================================

-- 创建信号表
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

-- 创建索引
CREATE INDEX idx_backtest_signals_backtest_id ON backtest_signals(backtest_id);
CREATE INDEX idx_backtest_signals_time ON backtest_signals(backtest_id, time);
CREATE INDEX idx_backtest_signals_action ON backtest_signals(backtest_id, action);
CREATE INDEX idx_backtest_signals_executed ON backtest_signals(backtest_id, was_executed);

