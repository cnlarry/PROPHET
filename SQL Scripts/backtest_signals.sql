-- SQLite数据库脚本

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
    indicator_snapshots_json TEXT,  -- 新增：结构化指标快照
    patterns_json TEXT,
    debug_json TEXT,
    
    -- 关联信息
    candle_index INTEGER,
    global_index INTEGER,
    
    -- 时间戳
    created_at TEXT DEFAULT CURRENT_TIMESTAMP,
    
    FOREIGN KEY (backtest_id) REFERENCES backtest_runs(id) ON DELETE CASCADE
);

CREATE INDEX idx_backtest_signals_time ON backtest_signals(backtest_id, time);

CREATE INDEX idx_backtest_signals_backtest_id ON backtest_signals(backtest_id);

CREATE INDEX idx_backtest_signals_executed ON backtest_signals(backtest_id, was_executed);

CREATE INDEX idx_backtest_signals_action ON backtest_signals(backtest_id, action);
