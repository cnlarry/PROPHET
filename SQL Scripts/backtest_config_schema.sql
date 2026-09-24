-- ============================================================================
-- 回测配置表 (SQLite)
-- 用于存储回测配置，只保留一条默认记录
-- ============================================================================

CREATE TABLE IF NOT EXISTS backtest_configs (
    id INTEGER PRIMARY KEY CHECK (id = 1),        -- 固定ID为1，确保只有一条记录
    symbol TEXT NOT NULL DEFAULT 'BTCUSDT',       -- 交易对
    interval TEXT NOT NULL DEFAULT '5m',          -- 时间框架
    start_date TEXT NOT NULL,                      -- 回测开始时间 (ISO格式)
    end_date TEXT NOT NULL,                        -- 回测结束时间 (ISO格式)
    initial_capital REAL NOT NULL DEFAULT 10000,  -- 初始资金
    leverage REAL DEFAULT 10.0,                   -- 杠杆倍数
    position_size_percent REAL DEFAULT 0.05,      -- 仓位比例
    allow_short INTEGER DEFAULT 1,                 -- 是否允许做空 (0/1)
    
    -- 仓位大小计算方法
    position_size_method TEXT DEFAULT 'fixed',    -- fixed/atr/kelly
    atr_period INTEGER DEFAULT 14,                -- ATR周期
    atr_multiplier REAL DEFAULT 2.0,              -- ATR止损倍数
    risk_percent_per_trade REAL DEFAULT 0.01,     -- 每笔交易风险百分比
    max_kelly_fraction REAL DEFAULT 0.25,         -- 最大Kelly比例
    min_kelly_sample_size INTEGER DEFAULT 20,      -- Kelly最小样本数
    
    -- 费用参数
    taker_fee_rate REAL DEFAULT 0.001,            -- Taker手续费率
    maker_fee_rate REAL DEFAULT 0.0005,           -- Maker手续费率
    slippage_rate REAL DEFAULT 0.0005,            -- 滑点率
    
    -- 止盈止损配置
    default_take_profit_percent REAL DEFAULT 0.40, -- 默认止盈百分比
    default_stop_loss_percent REAL DEFAULT 0.20,   -- 默认止损百分比
    enable_default_tpsl INTEGER DEFAULT 1,         -- 是否启用默认止盈止损
    enable_trailing_stop INTEGER DEFAULT 0,        -- 是否启用移动止损
    enable_trailing_take_profit INTEGER DEFAULT 0, -- 是否启用追踪止盈
    
    -- 滑点模式配置
    slippage_mode TEXT DEFAULT 'FixedBps',         -- 滑点模式
    fixed_bps REAL DEFAULT 0.0005,                 -- 固定基点滑点
    fixed_price REAL DEFAULT 0.5,                  -- 固定价格滑点
    pct_of_spread REAL DEFAULT 0.10,              -- 价差百分比滑点
    impact_coefficient REAL DEFAULT 0.1,           -- 市场冲击系数
    impact_exponent REAL DEFAULT 0.5,              -- 市场冲击指数
    slippage_randomness INTEGER DEFAULT 1,         -- 启用滑点随机波动
    slippage_random_factor REAL DEFAULT 0.2,       -- 滑点随机因子
    market_order_multiplier REAL DEFAULT 1.0,      -- 市价单滑点倍数
    stop_order_multiplier REAL DEFAULT 1.5,        -- 止损单滑点倍数
    
    -- 策略参数 (JSON格式)
    parameters_json TEXT,                          -- 策略自定义参数
    
    -- 性能优化配置
    enable_kline_cache INTEGER DEFAULT 1,           -- 启用K线转换缓存
    max_cache_size INTEGER DEFAULT 100,            -- 缓存大小
    
    -- 回测模式配置
    signal_sampling_interval TEXT DEFAULT '5m',    -- 信号采样频率
    use_api_data_source INTEGER DEFAULT 1,         -- 是否从API获取数据
    
    -- 时间戳
    updated_at TEXT NOT NULL DEFAULT (datetime('now', 'localtime'))
);

-- 创建触发器，自动更新 updated_at
CREATE TRIGGER IF NOT EXISTS update_backtest_configs_timestamp 
AFTER UPDATE ON backtest_configs
BEGIN
    UPDATE backtest_configs 
    SET updated_at = datetime('now', 'localtime') 
    WHERE id = 1;
END;
